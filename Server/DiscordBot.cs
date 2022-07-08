using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Shared;
//using Discord;

namespace Server;

public class DiscordBot {
    private Discord.Discord pvcDiscord = new Discord.Discord(Constants.clientId, (long)Discord.CreateFlags.Default);
    private Dictionary<string, long> discUserToId = new Dictionary<string, long>();
    private Dictionary<long, Discord.User> discUserTable = new Dictionary<long, Discord.User>();
    private Discord.Lobby? pvcLobby = null;
    private DiscordClient? DiscordClient;
    private string? Token;
    private Settings.DiscordTable Config => Settings.Instance.Discord;
    private string Prefix => Config.Prefix;
    private readonly Logger Logger = new Logger("Discord");
    private DiscordChannel? LogChannel;
    private bool Reconnecting;

    public DiscordBot() {
        Token = Config.Token;
        Logger.AddLogHandler(Log);
        CommandHandler.RegisterCommand("dscrestart", _ => {
            // this should be async'ed but i'm lazy
            Reconnecting = true;
            Task.Run(Reconnect);
            return "Restarting Discord bot";
        });
        if (Config.Token == null) return;
        Settings.LoadHandler += SettingsLoadHandler;
    }

    private async Task Reconnect() {
        if (DiscordClient != null) // usually null prop works, not here though...`
        {
            await DiscordClient.DisconnectAsync();
            ClosePVCLobbyIfOpen();
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

    public void PVCDiscordCallbackLoop()
    {
        pvcDiscord?.RunCallbacks();
    }

    public void ChangeVolume(string perspectiveUser, string target, float? vol)
    {
        var lobbyManager = pvcDiscord.GetLobbyManager();
        if (pvcLobby != null)
        {
            if (discUserToId.ContainsKey(perspectiveUser))
            {
                byte[] data = System.Text.Encoding.Unicode.GetBytes(target.PadRight(37) + ((vol == null) ? "   " : ((byte)(vol * 100)).ToString()));
                lobbyManager.SendNetworkMessage(pvcLobby.Value.Id, discUserToId[perspectiveUser], 0, data);
            }
            else
            {
                //that user doesn't seem to be in the lobby.
            }
        }
        else
        {
            //don't call it when there's no lobby you goof.
        }
    }

    private void ClosePVCLobbyIfOpen()
    {
        var lobbyManager = pvcDiscord.GetLobbyManager();
        if (pvcLobby != null) //close old lobby if it's still around (restart?)
        {
            using (SemaphoreSlim sm = new SemaphoreSlim(0, 1))
            {
                bool issue = false;
                lobbyManager.DeleteLobby(pvcLobby.Value.Id, (res) =>
                {
                    if (res != Discord.Result.Ok)
                    {
                        Logger.Error("Discord runner had an issue when deleting an old pvc lobby.");
                        issue = true;
                    }
                    sm.Release();
                });
                sm.Wait();
                if (issue)
                {
                    //return; //do something maybe?
                }
            }
            discUserToId.Clear();
            discUserTable.Clear();
        }
    }

    private void OpenPVCLobby()
    {
        var lobbyManager = pvcDiscord.GetLobbyManager();
        var userManager = pvcDiscord.GetUserManager();
        var trans = lobbyManager.GetLobbyCreateTransaction();
        trans.SetCapacity(Settings.Instance.Server.MaxPlayers);
        trans.SetType(Discord.LobbyType.Private);
        lobbyManager.CreateLobby(trans, (Discord.Result res, ref Discord.Lobby lobby) =>
        {
            if (res != Discord.Result.Ok)
            {
                Logger.Error("Discord runner had an issue when creating a pvc lobby.");
                return;
            }
            Logger.Info($"Discord PVC Lobby Id: {lobby.Id} Secret: {lobby.Secret}");
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
            pvcLobby = lobby;
        });
    }

    public void ChangePVCLobbySize(int newSize)
    {
        var lobbyManager = pvcDiscord.GetLobbyManager();
        var trans = lobbyManager.GetLobbyCreateTransaction();
        trans.SetCapacity(Settings.Instance.Server.MaxPlayers);
        lobbyManager.UpdateLobby(pvcLobby.Value.Id, trans, x => { });
    }

    public async Task Run() {
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