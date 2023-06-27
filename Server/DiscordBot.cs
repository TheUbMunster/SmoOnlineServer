using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Shared;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Server;

public class DiscordBot {

    #region VoiceProxTypeDefs
    public struct Lobby
    {
        public enum LobbyType
        {
            Private = 1,
            Public = 2
        }
        public uint Capacity { get; init; }
        public long Id { get; init; }
        public bool Locked { get; init; }
        public long OwnerId { get; init; }
        public LobbyType Type { get; init; }
        public string Secret { get; init; }
    }
    #endregion

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
    private DiscordChannel? CommandChannel;
    private DiscordChannel? LogChannel;
    private bool Reconnecting;

    private HttpClient webClient = new HttpClient();
    private Lobby? pvcLobby = null;
    private object lobbyLock = new object();

    private DiscordBot() {
        Token = Config.Token;
        Logger.AddLogHandler(Log);
        CommandHandler.RegisterCommand("dscrestart", _ => {
            // this should be async'ed but i'm lazy
            Reconnecting = true;
            Task.Run(Reconnect);
            return "Restarting Discord bot";
        });
        if (Config.CommandChannel == null)
            Logger.Warn("You probably should set your CommandChannel in settings.json");
        if (Config.LogChannel == null)
            Logger.Warn("You probably should set your LogChannel in settings.json");
        if (Config.Token == null)
        {
            Logger.Warn("No discord bot token is set in the server settings! You cannot use voice proximity until you assign a bot token!");
            return;
        }
        webClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bot " + Config.Token);
        //I don't think this is necessary because access to the static member "Settings.Instance" will
        //trigger a call to the static Settings constructor, which therefore will load the settings.
        //That means if you access any setting via Settings.Instance."SomeSetting", it will automatically
        //load the settings.
        Settings.LoadHandler += SettingsLoadHandler;
        Logger.Info("Discord bot ctor completed");
    }

    private async Task Reconnect() {
        if (DiscordClient != null) // usually null prop works, not here though...`
        {
            await DiscordClient.DisconnectAsync();
        }
        await Run();
    }

    private async void SettingsLoadHandler() {
        if (DiscordClient == null || Token != Config.Token) {
            await Run();
        }

        if (DiscordClient == null) {
            Logger.Error(new NullReferenceException("Discord client not setup yet!"));
            return;
        }

        if (Config.CommandChannel != null) {
            try {
                CommandChannel = await DiscordClient.GetChannelAsync(ulong.Parse(Config.CommandChannel));
            } catch (Exception e) {
                Logger.Error($"Failed to get command channel \"{Config.CommandChannel}\"");
                Logger.Error(e);
            }
        }

        if (Config.LogChannel != null) {
            try {
                LogChannel = await DiscordClient.GetChannelAsync(ulong.Parse(Config.LogChannel));
            } catch (Exception e) {
                Logger.Error($"Failed to get log channel \"{Config.LogChannel}\"");
                Logger.Error(e);
            }
        }
    }

    private static List<string> SplitMessage(string message, int maxSizePerElem = 2000)
    {
        List<string> result = new List<string>();
        for (int i = 0; i < message.Length; i += maxSizePerElem) 
        {
            result.Add(message.Substring(i, message.Length - i < maxSizePerElem ? message.Length - i : maxSizePerElem));
        }
        return result;
    }

    private async void Log(string source, string level, string text, ConsoleColor _) {
        try {
            if (DiscordClient != null && LogChannel != null) {
                foreach (string mesg in SplitMessage(Logger.PrefixNewLines(text, $"{level} [{source}]"), 1994)) //room for 6 '`'
                    await DiscordClient.SendMessageAsync(LogChannel, $"```{mesg}```");
            }
        } catch (Exception e) {
            // don't log again, it'll just stack overflow the server!
            if (Reconnecting) return; // skip if reconnecting
            await Console.Error.WriteLineAsync("Exception in discord logger");
            await Console.Error.WriteLineAsync(e.ToString());
        }
    }

    /// <summary>
    /// Note: *Only* call this when quitting the program
    /// </summary>
    public void ClosePVCLobbyForQuit()
    {
        lock (lobbyLock)
        {
            if (pvcLobby != null)
            {
                try
                {
                    webClient.DeleteAsync($"https://discord.com/api/v10/lobbies/{pvcLobby.Value.Id}").Wait();
                }
                catch (Exception e)
                {
                    Logger.Warn("Attempted to close the discord lobby: " + e.ToString());
                }
            }
        }
    }

    public async Task<bool> CloseThenOpenPVCLobby()
    {
        if (Config.Token == null)
        {
            Logger.Warn("An attempt was made to open the PVC lobby without the discord bot token set! You need to set the discord bot token to use voice proximity!");
            return false;
        }
        bool success = false;
        try
        {
            lock (lobbyLock)
            {
                if (pvcLobby != null)
                {
                    //fire and forget, if it fails it is garbage collected in 15s anyways
                    webClient.DeleteAsync($"https://discord.com/api/v10/lobbies/{pvcLobby.Value.Id}");
                }
            }
            if (Settings.Instance.Discord.AppID == null)
            {
                Settings.Instance.Discord.AppID = (long?)DiscordClient?.CurrentUser.Id;
                Logger.Warn("The AppID was not set in settings, attempting to glean this information from the bot.");
                if (Settings.Instance.Discord.AppID != null)
                {
                    Logger.Info("Gleaned the AppID " + Settings.Instance.Discord.AppID);
                    Settings.SaveSettings();
                }
            }
            var payload = new
            {
                application_id = Settings.Instance.Discord.AppID.ToString(),
                type = ((int)Lobby.LobbyType.Private).ToString(),
                capacity = (Settings.Instance.Server.MaxPlayers + 1).ToString()
            };
            HttpContent content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            Logger.Info("Attempting to open PVC lobby");
            var response = await webClient.PostAsync("https://discord.com/api/v10/lobbies", content);
            string json = await response.Content.ReadAsStringAsync();
            Newtonsoft.Json.Linq.JObject j = Newtonsoft.Json.Linq.JObject.Parse(json);
            if (j.ContainsKey("retry_after"))
            {
                //TODO: try open lobby after defer.
                float time = float.Parse(j["retry_after"].ToString());
                Logger.Warn("PVC lobby could not be opened (rate limited), retry after " + time);
                success = false;
            }
            else
            {
                lock (lobbyLock)
                {
                    //j can be a rate limit response, check for that case
                    pvcLobby = new Lobby()
                    {
                        Capacity = uint.Parse(j["capacity"].ToString()),
                        Id = long.Parse(j["id"].ToString()),
                        Locked = bool.Parse(j["locked"].ToString()),
                        OwnerId = long.Parse(j["owner_id"].ToString()),
                        Type = (Lobby.LobbyType)int.Parse(j["type"].ToString()),
                        Secret = j["secret"].ToString()
                    };
                    Logger.Info("PVC lobby open.");
                }
                success = true;
            }
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
                MinimumLogLevel = LogLevel.None,
                Intents = DiscordIntents.GuildMessages
            });
            Logger.Info("About to log in the bot...");
            await DiscordClient.ConnectAsync(new DiscordActivity("Hide and Seek", DSharpPlus.Entities.ActivityType.Competing));
            SettingsLoadHandler();
            Logger.Info(
                $"Discord bot logged in as {DiscordClient.CurrentUser.Username}#{DiscordClient.CurrentUser.Discriminator}");
            Reconnecting = false;
            string mentionPrefix = $"{DiscordClient.CurrentUser.Mention}";
            DiscordClient.MessageCreated += async (_, args) => {
                if (args.Author.IsCurrent) return; //dont respond to commands from ourselves (prevent "sql-injection" esq attacks)
                //prevent commands via dm and non-public channels
                if (CommandChannel == null) {
                    if (args.Channel is DiscordDmChannel)
                        return; //no dm'ing the bot allowed!
                }
                else if (args.Channel.Id != CommandChannel.Id && (LogChannel != null && args.Channel.Id != LogChannel.Id))
                    return;
                //run command
                try {
                    DiscordMessage msg = args.Message;
                    string? resp = null;
                    if (string.IsNullOrEmpty(Prefix)) {
                        await msg.Channel.TriggerTypingAsync();
                        resp = string.Join('\n', CommandHandler.GetResult(msg.Content).ReturnStrings);
                    } else if (msg.Content.StartsWith(Prefix)) {
                        await msg.Channel.TriggerTypingAsync();
                        resp = string.Join('\n', CommandHandler.GetResult(msg.Content[Prefix.Length..]).ReturnStrings);
                    } else if (msg.Content.StartsWith(mentionPrefix)) {
                        await msg.Channel.TriggerTypingAsync();
                        resp = string.Join('\n', CommandHandler.GetResult(msg.Content[mentionPrefix.Length..].TrimStart()).ReturnStrings);
                    }
                    if (resp != null)
                    {
                        foreach (string mesg in SplitMessage(resp))
                            await msg.RespondAsync(mesg);
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
