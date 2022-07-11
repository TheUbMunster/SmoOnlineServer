using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Shared;
//using Discord;

namespace Server;

public class DiscordBot {
    private Dictionary<string, long> discUserToId = new Dictionary<string, long>();
    private Dictionary<long, Discord.User> discUserTable = new Dictionary<long, Discord.User>();
    private DiscordClient? DiscordClient;
    private string? Token;
    private Settings.DiscordTable Config => Settings.Instance.Discord;
    private string Prefix => Config.Prefix;
    private readonly Logger Logger = new Logger("Discord");
    private DiscordChannel? LogChannel;
    private bool Reconnecting;

    #region Discord voice stuff
    private Queue<Action> messageQueue = new Queue<Action>();
    private object lockKey = new object();
    private Discord.Discord? pvcDiscord = null;
    private Discord.Lobby? pvcLobby = null;
    private Discord.LobbyManager? lobbyManager = null;
    private Discord.VoiceManager? voiceManager = null;
    private Discord.UserManager? userManager = null;
    private Discord.User? currentUser = null;
    #endregion

    public DiscordBot() {
        Token = Config.Token;
        Logger.AddLogHandler(Log);
        CommandHandler.RegisterCommand("dscrestart", _ => {
            // this should be async'ed but i'm lazy
            Reconnecting = true;
            Task.Run(Reconnect);
            return "Restarting Discord bot";
        });

        CommandHandler.RegisterCommand("jc", _ =>
        {
            return $"jl {pvcLobby.Value.Id} {pvcLobby.Value.Secret}";
        });
        CommandHandler.RegisterCommand("lll", _ =>
        {
            if (pvcLobby != null)
            {
                lock (lockKey)
                {
                    var users = lobbyManager.GetMemberUsers(pvcLobby.Value.Id);
                    return $"All users in the lobby: {string.Join(",\n", users.Select(x => $"{x.Id}: {x.Username}#{x.Discriminator}"))}";
                }
            }
            else
            {
                return "Not currently in a lobby.";
            }
        });

        if (Config.Token == null) return;
        Settings.LoadHandler += SettingsLoadHandler;
        #region Discord voice lobby stuff
        using (SemaphoreSlim sm = new SemaphoreSlim(0, 1))
        {
            Task discordTask = Task.Run(() =>
            {
                #region Setup
                pvcDiscord = new Discord.Discord(Constants.clientId, (UInt64)Discord.CreateFlags.Default);
                lobbyManager = pvcDiscord.GetLobbyManager();
                voiceManager = pvcDiscord.GetVoiceManager();
                userManager = pvcDiscord.GetUserManager();
                userManager.OnCurrentUserUpdate += () =>
                {
                    currentUser = userManager.GetCurrentUser();
                };
                while (currentUser == null)
                {
                    pvcDiscord.RunCallbacks();
                    Thread.Sleep(16);
                }
                sm.Release();
                //pvcDiscord.SetLogHook(Discord.LogLevel.Debug, (level, message) =>
                //{
                //    Logger.Info($"Log[{level}] {message}");
                //});
                #endregion

                #region Callback loop
                try
                {
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    sw.Start();
                    while (true)
                    {
                        lock (lockKey)
                        {
                            try
                            {
                                while (messageQueue.Count > 0)
                                {
                                    messageQueue.Dequeue()();
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Issue in message loop: {ex.ToString()}");
                            }
                        }
                        pvcDiscord.RunCallbacks();
                        lobbyManager.FlushNetwork();
                        while (sw.ElapsedMilliseconds < 16) { } //busy wait because all forms of "sleep" have poor accuracy (granularity of like 10ms, caused by the scheduler event cadence)
                        sw.Restart(); //1000 / 16 = ~60 times a second (not including the time it takes to do the callbacks
                                      //themselves e.g. if callbacks take ~4ms, then it's only 50 times a second.
                    }
                }
                finally
                {
                    pvcDiscord.Dispose();
                }
                #endregion
            });
            sm.Wait();
        }
        #endregion
    }

    private async Task Reconnect() {
        if (DiscordClient != null) // usually null prop works, not here though...`
        {
            await DiscordClient.DisconnectAsync();
        }
        await Run();
    }

    private async void SettingsLoadHandler() {
        try {
            if (DiscordClient == null || Token != Config.Token)
                await Run();
            if (Config.LogChannel != null)
                LogChannel = await (DiscordClient?.GetChannelAsync(ulong.Parse(Config.LogChannel)) ??
                                    throw new NullReferenceException("Discord client not setup yet!"));
        } catch (Exception e) {
            Logger.Error($"Failed to get log channel \"{Config.LogChannel}\"");
            Logger.Error(e);
        }
    }

    private async void Log(string source, string level, string text, ConsoleColor _) {
        try {
            if (DiscordClient != null && LogChannel != null) {
                await DiscordClient.SendMessageAsync(LogChannel,
                    $"```{Logger.PrefixNewLines(text, $"{level} [{source}]")}```");
            }
        } catch (Exception e) {
            // don't log again, it'll just stack overflow the server!
            if (Reconnecting) return; // skip if reconnecting
            await Console.Error.WriteLineAsync("Exception in discord logger");
            await Console.Error.WriteLineAsync(e.ToString());
        }
    }

    


    public void ChangeVolume(string perspectiveUser, string target, float? vol)
    {
        lock (lockKey)
        {
            messageQueue.Enqueue(() =>
            {
                if (pvcLobby != null)
                {
                    if (discUserToId.ContainsKey(perspectiveUser))
                    {
                        byte[] data = System.Text.Encoding.Unicode.GetBytes(/*perspectiveUser.PadRight(37) + */target.PadRight(37) + ((vol == null) ? "   " : ((byte)(vol * 100)).ToString().PadRight(3)));
                        //todo: make this more efficent to send messages in batches to single users
                        lobbyManager.SendNetworkMessage(pvcLobby.Value.Id, discUserToId[perspectiveUser], 0, data);
                        //Console.WriteLine("sent vol dat");
                    }
                    else
                    {
                        //that user doesn't seem to be in the lobby.
                        //Logger.Error($"Attempt to change volume of users from the perspective of {perspectiveUser}, even though they aren't in the lobby.");
                    }
                }
                else
                {
                    //don't call it when there's no lobby you goof.
                    Logger.Error("Call to ChangeVolume when there is no lobby.");
                }
            });
        }
    }

    private void ClosePVCLobbyIfOpen()
    {
        lock (lockKey)
        {
            messageQueue.Enqueue(() =>
            {
                if (pvcLobby != null) //close old lobby if it's still around (restart?)
                {
                    lobbyManager.DeleteLobby(pvcLobby.Value.Id, (res) =>
                    {
                        if (res != Discord.Result.Ok)
                        {
                            Logger.Info("Discord runner closed the pvc lobby.");
                            discUserToId.Clear();
                            discUserTable.Clear();
                            pvcLobby = null;
                        }
                        else
                        {
                            Logger.Error("Discord runner had an issue when deleting an old pvc lobby.");
                        }
                    });
                }
            });
        }
    }

    private void OpenPVCLobby()
    {
        lock (lockKey)
        {
            messageQueue.Enqueue(() =>
            {
                var trans = lobbyManager.GetLobbyCreateTransaction();
                trans.SetCapacity(Settings.Instance.Server.MaxPlayers);
                trans.SetType(Discord.LobbyType.Private);
                Logger.Info("Attempting to open pvc lobby");
                lobbyManager.CreateLobby(trans, (Discord.Result res, ref Discord.Lobby lobby) =>
                {
                    if (res != Discord.Result.Ok)
                    {
                        Logger.Error("Discord runner had an issue when creating a pvc lobby.");
                        return;
                    }
                    Logger.Info($"Discord PVC Lobby Id: {lobby.Id} Secret: {lobby.Secret}");
                    discUserTable[currentUser.Value.Id] = currentUser.Value;
                    discUserToId[currentUser.Value.Username + "#" + currentUser.Value.Discriminator] = currentUser.Value.Id;
                    pvcLobby = lobby;
                    lobbyManager.ConnectNetwork(lobby.Id);
                    lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
                    lobbyManager.OnMemberConnect += (long lobbyId, long userId) =>
                    {
                        userManager.GetUser(userId, (Discord.Result res, ref Discord.User user) =>
                        {
                            discUserTable[user.Id] = user;
                            discUserToId[user.Username + "#" + user.Discriminator] = user.Id;
                        });
                    };
                    lobbyManager.OnMemberDisconnect += (long lobbyId, long userId) =>
                    {
                        discUserToId.Remove(discUserTable[userId].Username + "#" + discUserTable[userId].Discriminator);
                        discUserTable.Remove(userId);
                    };
                });
            });
        }
    }

    public void ChangePVCLobbySize(int newSize)
    {
        lock (lockKey)
        {
            messageQueue.Enqueue(() =>
            {
                if (pvcLobby != null)
                {
                    var trans = lobbyManager.GetLobbyCreateTransaction();
                    trans.SetCapacity(Settings.Instance.Server.MaxPlayers); //race condition probably
                    lobbyManager.UpdateLobby(pvcLobby.Value.Id, trans, x => 
                    {
                        if (x != Discord.Result.Ok)
                        {
                            Logger.Error("The discord runner had an issue changing the pvc lobby size");
                        }
                        else
                        {
                            Logger.Error($"The discord runner changed the pvc lobby size to {Settings.Instance.Server.MaxPlayers}.");
                        }
                    });
                }
            });
        }
    }


    public async Task Run() {
        ClosePVCLobbyIfOpen();
        OpenPVCLobby();


        Token = Config.Token;
        DiscordClient?.Dispose();
        if (Config.Token == null) {
            DiscordClient = null;
            return;
        }

        try {
            DiscordClient = new DiscordClient(new DiscordConfiguration {
                Token = Config.Token,
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.None
            });
            await DiscordClient.ConnectAsync(new DiscordActivity("Hide and Seek", DSharpPlus.Entities.ActivityType.Competing));
            SettingsLoadHandler();
            Logger.Info(
                $"Discord bot logged in as {DiscordClient.CurrentUser.Username}#{DiscordClient.CurrentUser.Discriminator}");
            Reconnecting = false;
            string mentionPrefix = $"{DiscordClient.CurrentUser.Mention} ";
            DiscordClient.MessageCreated += async (_, args) => {
                if (args.Author.IsCurrent) return;
                try {
                    DiscordMessage msg = args.Message;
                    if (string.IsNullOrEmpty(Prefix)) {
                        await msg.Channel.TriggerTypingAsync();
                        await msg.RespondAsync(string.Join('\n',
                            CommandHandler.GetResult(msg.Content).ReturnStrings));
                    } else if (msg.Content.StartsWith(Prefix)) {
                        await msg.Channel.TriggerTypingAsync();
                        await msg.RespondAsync(string.Join('\n',
                            CommandHandler.GetResult(msg.Content[Prefix.Length..]).ReturnStrings));
                    } else if (msg.Content.StartsWith(mentionPrefix)) {
                        await msg.Channel.TriggerTypingAsync();
                        await msg.RespondAsync(string.Join('\n',
                            CommandHandler.GetResult(msg.Content[mentionPrefix.Length..]).ReturnStrings));
                    }
                } catch (Exception e) {
                    Logger.Error(e);
                }
            };
            DiscordClient.ClientErrored += (_, args) => {
                Logger.Error("Discord client caught an error in handler!");
                Logger.Error(args.Exception);
                return Task.CompletedTask;
            };
            DiscordClient.SocketErrored += (_, args) => {
                Logger.Error("Discord client caught an error on socket!");
                Logger.Error(args.Exception);
                return Task.CompletedTask;
            };
        } catch (Exception e) {
            Logger.Error("Exception occurred in discord runner!");
            Logger.Error(e);
        }
    }
}