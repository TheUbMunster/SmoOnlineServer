using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Shared;
using System.Net.Http;
using System.Net.Http.Headers;
//using Discord;

namespace Server;

public class DiscordBot {
    static DiscordBot()
    {
        Instance = new DiscordBot();
    }

    public static DiscordBot Instance { get; private set; }

    private DiscordClient? DiscordClient;
    private string? Token;
    private Settings.DiscordTable Config => Settings.Instance.Discord;
    private string Prefix => Config.Prefix;
    private readonly Logger Logger = new Logger("Discord");
    private DiscordChannel? LogChannel;
    private bool Reconnecting;

    //#region Discord voice stuff
    //private Dictionary<string, long> discUserToId = new Dictionary<string, long>();
    //private Dictionary<long, Discord.User> discUserTable = new Dictionary<long, Discord.User>();
    private Queue<Action> messageQueue = new Queue<Action>();
    private object lockKey = new object();
    //private Discord.Discord? pvcDiscord = null;
    private HttpClient webClient = new HttpClient();
    private Discord.Lobby? pvcLobby = null;
    //private Discord.LobbyManager? lobbyManager = null;
    //private Discord.VoiceManager? voiceManager = null;
    //private Discord.UserManager? userManager = null;
    //private Discord.User? currentUser = null;
    //#endregion

    private DiscordBot() {
        Token = Config.Token;
        Logger.AddLogHandler(Log);
        webClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bot " + Constants.botToken);
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
        //CommandHandler.RegisterCommand("lll", _ =>
        //{
        //    if (pvcLobby != null)
        //    {
        //        lock (lockKey)
        //        {
        //            var users = lobbyManager.GetMemberUsers(pvcLobby.Value.Id);
        //            return $"All users in the lobby: {string.Join(",\n", users.Select(x => $"{x.Id}: {x.Username}#{x.Discriminator}"))}";
        //        }
        //    }
        //    else
        //    {
        //        return "Not currently in a lobby.";
        //    }
        //});

        if (Config.Token == null) return;
        Settings.LoadHandler += SettingsLoadHandler;

        #region Discord voice lobby stuff
        Task discordTask = Task.Run(() =>
        {
            try
            {
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                while (true)
                {
                    lock (lockKey)
                    {
                        while (messageQueue.Count > 0)
                        {
                            messageQueue.Dequeue()();
                        }
                    }
                    //pvcDiscord.RunCallbacks();
                    //lobbyManager.FlushNetwork();

                    //send off pending messages to voice clients?
                    while (sw.ElapsedMilliseconds < 16) { } //busy wait because all forms of "sleep" have poor accuracy (granularity of like 10ms, caused by the scheduler event cadence)
                    sw.Restart(); //1000 / 16 = ~60 times a second (not including the time it takes to do the callbacks
                                  //themselves e.g. if callbacks take ~4ms, then it's only 50 times a second.
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Issue in message loop: {e.ToString()}");
            }
        });
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

    private void OpenPVCLobby()
    {
        lock (lockKey)
        {
            messageQueue.Enqueue(async () =>
            {
                try
                {
                    var payload = new
                    {
                        application_id = Constants.clientId.ToString(),
                        type = ((int)Discord.LobbyType.Private).ToString(),
                        capacity = (Settings.Instance.Server.MaxPlayers + 1).ToString()
                    };
                    HttpContent content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json");
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    Logger.Info("Attempting to open pvc lobby");
                    var response = await webClient.PostAsync("https://discord.com/api/v10/lobbies", content);
                    string json = await response.Content.ReadAsStringAsync();
                    Newtonsoft.Json.Linq.JObject j = Newtonsoft.Json.Linq.JObject.Parse(json);
                    pvcLobby = new Discord.Lobby()
                    {
                        Capacity = uint.Parse(j["capacity"].ToString()),
                        Id = long.Parse(j["id"].ToString()),
                        Locked = bool.Parse(j["locked"].ToString()),
                        OwnerId = long.Parse(j["owner_id"].ToString()),
                        Type = (Discord.LobbyType)int.Parse(j["type"].ToString()),
                        Secret = j["secret"].ToString()
                    };
                    //send lobby packets to connected vcp clients so they can join the voice lobby without manually entering in the info
                    //VoiceProxServer.Instance.SendAllLobbyPacket(new PVCLobbyPacket() { LobbyId = pvcLobby.Value.Id, Secret = pvcLobby.Value.Secret });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            });
        }
    }

    private void ClosePVCLobbyIfOpen()
    {
        lock (lockKey)
        {
            messageQueue.Enqueue(async () =>
            {
                if (pvcLobby != null)
                {
                    try
                    {
                        var response = await webClient.DeleteAsync($"https://discord.com/api/v10/lobbies/{pvcLobby.Value.Id}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex.ToString());
                    }
                }
            });
        }
    }

    public void ChangePVCLobbySize(int newSize)
    {
        lock (lockKey)
        {
            messageQueue.Enqueue(async () =>
            {
                if (pvcLobby != null)
                {
                    try
                    {
                        var payload = new
                        {
                            capacity = newSize
                        };
                        HttpContent content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json");
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                        var response = await webClient.PatchAsync($"https://discord.com/api/v10/lobbies/{pvcLobby.Value.Id}", content);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex.ToString());
                    }
                }
            });
        }
    }

    public (long id, string secret)? GetLobbyInfo()
    {
        lock (lockKey)
        {
            if (pvcLobby != null)
                return (pvcLobby.Value.Id, pvcLobby.Value.Secret);
            else
                return null;
        }
    }

    public async Task Run() {
        ClosePVCLobbyIfOpen();
        OpenPVCLobby(); //TODO: only do this when a client requests to connect. (empty lobbies get gcd in 15 seconds)


        Token = Config.Token;
        DiscordClient?.Dispose();
        if (Config.Token == null) {
            DiscordClient = null;
            return;
        }

        try {
            DiscordClient = new DiscordClient(new DiscordConfiguration {
                Token = Config.Token,
                MinimumLogLevel = LogLevel.None
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

    ~DiscordBot()
    {
        webClient?.Dispose();
    }
}