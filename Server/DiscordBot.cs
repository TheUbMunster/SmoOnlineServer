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
    //private Discord.Discord? pvcDiscord = null;
    private HttpClient webClient = new HttpClient();
    private Discord.Lobby? pvcLobby = null;
    private object lobbyLock = new object();
    //private Discord.LobbyManager? lobbyManager = null;
    //private Discord.VoiceManager? voiceManager = null;
    //private Discord.UserManager? userManager = null;
    //private Discord.User? currentUser = null;
    //#endregion

    private DiscordBot() {
        Token = Config.Token;
        Logger.AddLogHandler(Log);
        CommandHandler.RegisterCommand("dscrestart", _ => {
            // this should be async'ed but i'm lazy
            Reconnecting = true;
            Task.Run(Reconnect);
            return "Restarting Discord bot";
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
        webClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bot " + Config.Token);
        Settings.LoadHandler += SettingsLoadHandler;

        #region Discord voice lobby stuff
        //Task discordTask = Task.Run(() =>
        //{
        //    try
        //    {
        //        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        //        sw.Start();
        //        while (true)
        //        {
        //            lock (lockKey)
        //            {
        //                while (messageQueue.Count > 0)
        //                {
        //                    messageQueue.Dequeue()();
        //                }
        //            }
        //            //pvcDiscord.RunCallbacks();
        //            //lobbyManager.FlushNetwork();

        //            //send off pending messages to voice clients?
        //            while (sw.ElapsedMilliseconds < 16) { } //busy wait because all forms of "sleep" have poor accuracy (granularity of like 10ms, caused by the scheduler event cadence)
        //            sw.Restart(); //1000 / 16 = ~60 times a second (not including the time it takes to do the callbacks
        //                          //themselves e.g. if callbacks take ~4ms, then it's only 50 times a second.
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error($"Issue in message loop: {e.ToString()}");
        //    }
        //});
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

    public async Task<bool> OpenPVCLobby()
    {
        bool success = false;
        try
        {
            lock (lobbyLock)
            {
                if (pvcLobby != null)
                {
                    //fire and forget, if it fails it is garbage collected in 15s anyways

                    //ADD ALL CLIENTS TO PENDING LIST
                    webClient.DeleteAsync($"https://discord.com/api/v10/lobbies/{pvcLobby.Value.Id}");
                }
            }
            var payload = new
            {
                application_id = Constants.clientId.ToString(),
                type = ((int)Discord.LobbyType.Private).ToString(),
                capacity = (Settings.Instance.Server.MaxPlayers + 1).ToString()
            };
            HttpContent content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            Logger.Info("Attempting to open PVC lobby");
            var response = await webClient.PostAsync("https://discord.com/api/v10/lobbies", content);
            string json = await response.Content.ReadAsStringAsync();
            Newtonsoft.Json.Linq.JObject j = Newtonsoft.Json.Linq.JObject.Parse(json);
            lock (lobbyLock)
            {
                pvcLobby = new Discord.Lobby()
                {
                    Capacity = uint.Parse(j["capacity"].ToString()),
                    Id = long.Parse(j["id"].ToString()),
                    Locked = bool.Parse(j["locked"].ToString()),
                    OwnerId = long.Parse(j["owner_id"].ToString()),
                    Type = (Discord.LobbyType)int.Parse(j["type"].ToString()),
                    Secret = j["secret"].ToString()
                };
                Logger.Info("PVC lobby open.");
            }
            success = true;
            //send lobby packets to connected vcp clients so they can join the voice lobby without manually entering in the info
            //VoiceProxServer.Instance.SendAllLobbyPacket(new PVCLobbyPacket() { LobbyId = pvcLobby.Value.Id, Secret = pvcLobby.Value.Secret });
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString());
        }
        return success;
    }

    public async Task<bool> ChangePVCLobbySize(int newSize)
    {
        bool success = false;
        try
        {
            var payload = new
            {
                capacity = newSize
            };
            HttpContent content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            long id;
            lock (lobbyLock)
            {
                id = pvcLobby!.Value.Id; //nullderef caught by catch, no need for if
            }
            var response = await webClient.PatchAsync($"https://discord.com/api/v10/lobbies/{id}", content);
            success = true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString());
        }
        return success;
    }

    public (long id, string secret)? GetLobbyInfo()
    {
        lock (lobbyLock)
        {
            if (pvcLobby != null)
                return (pvcLobby.Value.Id, pvcLobby.Value.Secret);
            else
                return null;
        }
    }

    public async Task Run() {
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