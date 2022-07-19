using System.Collections.Concurrent;
using System.Net;
using System.Numerics;
using System.Text;
using Server;
using Shared;
using Shared.Packet.Packets;
using Timer = System.Timers.Timer;


bool proxChat = false; //off by default.
//Dictionary<Client, (Vector3 pos, byte? scen, string? stage)> clientPositionCorrelate = new Dictionary<Client, (Vector3, byte?, string?)>();
//Dictionary<Client, Dictionary<Client, float>> playerVolumes = new Dictionary<Client, Dictionary<Client, float>>(); //volume [0f : 0% - 1f : 100%]
//Dictionary<string, string> igToDiscord = new Dictionary<string, string>(); //ingame -> discord

Server.Server server = new Server.Server();
HashSet<int> shineBag = new HashSet<int>();

CancellationTokenSource cts = new CancellationTokenSource();
Task listenTask = server.Listen(cts.Token);
Logger consoleLogger = new Logger("Console");
await DiscordBot.Instance.Run();
{
    var e = VoiceProxServer.Instance; //access
    consoleLogger.Info("VPS instance loaded " + e.ToString()); //and unstrippable usage force static ctor.
}

server.ClientJoined += (c, _) =>
{
    if (Settings.Instance.BanList.Enabled
        && (Settings.Instance.BanList.Players.Contains(c.Id)
            || Settings.Instance.BanList.IpAddresses.Contains(
                ((IPEndPoint)c.Socket!.RemoteEndPoint!).Address.ToString())))
        throw new Exception($"Banned player attempted join: {c.Name}");
    c.Metadata["shineSync"] = new ConcurrentBag<int>();
    c.Metadata["loadedSave"] = false;
    c.Metadata["scenario"] = (byte?)0;
    c.Metadata["2d"] = false;
    c.Metadata["speedrun"] = false;
    foreach (Client client in server.ClientsConnected) {
        try {
            c.Send((GamePacket)client.Metadata["lastGamePacket"]!, client).Wait();
        } catch {
            // lol who gives a fuck
        }
    }
};

async Task ClientSyncShineBag(Client client)
{
    try
    {
        if ((bool?)client.Metadata["speedrun"] ?? false) return;
        ConcurrentBag<int> clientBag = (ConcurrentBag<int>)(client.Metadata["shineSync"] ??= new ConcurrentBag<int>());
        foreach (int shine in shineBag.Except(clientBag).ToArray())
        {
            clientBag.Add(shine);
            await client.Send(new ShinePacket
            {
                ShineId = shine
            });
        }
    }
    catch
    {
        // errors that can happen when sending will crash the server :)
    }
}

async void SyncShineBag()
{
    try
    {
        await Parallel.ForEachAsync(server.Clients.ToArray(), async (client, _) => await ClientSyncShineBag(client));
    }
    catch
    {
        // errors that can happen shines change will crash the server :)
    }
}

Timer timer = new Timer(120000);
timer.AutoReset = true;
timer.Enabled = true;
timer.Elapsed += (_, _) => { SyncShineBag(); };
timer.Start();

float MarioSize(bool is2d) => is2d ? 180 : 160;

server.PacketHandler = (c, p) =>
{
    switch (p)
    {
        case GamePacket gamePacket:
            {
                c.Logger.Info($"Got game packet {gamePacket.Stage}->{gamePacket.ScenarioNum}");
                c.Metadata["scenario"] = gamePacket.ScenarioNum;
                c.Metadata["2d"] = gamePacket.Is2d;
                c.Metadata["lastGamePacket"] = gamePacket;
                VoiceProxServer.Instance.OnPlayerUpdate(c.Name, Vector3.Zero, gamePacket.Stage, true);
                switch (gamePacket.Stage)
                {
                    case "CapWorldHomeStage" when gamePacket.ScenarioNum == 0:
                        c.Metadata["speedrun"] = true;
                        ((ConcurrentBag<int>)(c.Metadata["shineSync"] ??= new ConcurrentBag<int>())).Clear();
                        shineBag.Clear();
                        c.Logger.Info("Entered Cap on new save, preventing moon sync until Cascade");
                        break;
                    case "WaterfallWorldHomeStage":
                        if (c.Metadata.ContainsKey("speedrun"))
                        {
                            c.Metadata["speedrun"] = false;
                        }
                        bool wasSpeedrun;
                        if (c.Metadata.ContainsKey("speedrun"))
                        {
                            wasSpeedrun = (bool)c.Metadata["speedrun"]!; //this threw keynotpresent under some circumstance on a non 100% file
                        }
                        else
                        {
                            c.Metadata["speedrun"] = wasSpeedrun = false;
                        }

                        c.Metadata["speedrun"] = false;
                        if (wasSpeedrun)
                            Task.Run(async () =>
                            {
                                c.Logger.Info("Entered Cascade with moon sync disabled, enabling moon sync");
                                await Task.Delay(15000);
                                await ClientSyncShineBag(c);
                            });
                        break;
                }

                if (Settings.Instance.Scenario.MergeEnabled)
                {
                    server.BroadcastReplace(gamePacket, c, (from, to, gp) =>
                    {
                        gp.ScenarioNum = (byte?)to.Metadata["scenario"] ?? 200;
                        to.Send(gp, from);
                    });
                    return false;
                }

                break;
            }
        case TagPacket tagPacket:
            {
                if ((tagPacket.UpdateType & TagPacket.TagUpdate.State) != 0) c.Metadata["seeking"] = tagPacket.IsIt;
                if ((tagPacket.UpdateType & TagPacket.TagUpdate.Time) != 0)
                    c.Metadata["time"] = new Time(tagPacket.Minutes, tagPacket.Seconds, DateTime.Now);
                break;
            }
        case CostumePacket:
            ClientSyncShineBag(c);
            c.Metadata["loadedSave"] = true;
            break;
        case ShinePacket shinePacket:
            {
                if (c.Metadata["loadedSave"] is false) break;
                ConcurrentBag<int> playerBag;
                //was throwing keynotfoundexception
                if (c.Metadata.ContainsKey("shineSync"))
                    playerBag = (ConcurrentBag<int>)c.Metadata["shineSync"];
                else
                    c.Metadata["shineSync"] = playerBag = new ConcurrentBag<int>();
                shineBag.Add(shinePacket.ShineId);
                if (playerBag.Contains(shinePacket.ShineId)) break;
                c.Logger.Info($"Got moon {shinePacket.ShineId}");
                playerBag.Add(shinePacket.ShineId);
                SyncShineBag();
                break;
            }
        case PlayerPacket playerPacket when Settings.Instance.Flip.Enabled
                                            && Settings.Instance.Flip.Pov is FlipOptions.Both or FlipOptions.Others
                                            && Settings.Instance.Flip.Players.Contains(c.Id):
            {
                playerPacket.Position += Vector3.UnitY * MarioSize((bool)c.Metadata["2d"]);
                playerPacket.Rotation *= Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationX(MathF.PI))
                                         * Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationY(MathF.PI));
                server.Broadcast(playerPacket, c);
                string? stage = c.Metadata.ContainsKey("lastGamePacket") ? ((GamePacket?)c.Metadata["lastGamePacket"])?.Stage : null;
                VoiceProxServer.Instance.OnPlayerUpdate(c.Name, playerPacket.Position, stage);
                return false;
            }
        case PlayerPacket playerPacket when Settings.Instance.Flip.Enabled
                                            && Settings.Instance.Flip.Pov is FlipOptions.Both or FlipOptions.Self
                                            && !Settings.Instance.Flip.Players.Contains(c.Id):
            {
                server.BroadcastReplace(playerPacket, c, (from, to, sp) =>
                {
                    if (Settings.Instance.Flip.Players.Contains(to.Id))
                    {
                        sp.Position += Vector3.UnitY * MarioSize((bool)c.Metadata["2d"]);
                        sp.Rotation *= Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationX(MathF.PI))
                                       * Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationY(MathF.PI));
                    }

                    to.Send(sp, from);
                });
                string? stage = c.Metadata.ContainsKey("lastGamePacket") ? ((GamePacket?)c.Metadata["lastGamePacket"])?.Stage : null;
                VoiceProxServer.Instance.OnPlayerUpdate(c.Name, playerPacket.Position, stage);
                //UpdateProxChatDataForPlayer(c, (playerPacket.Position, scen, stage)); //this might also need to be in the other playerPacket case?
                return false;
            }

    }

    return true;
};

#region Old ProxChat data calc
//TODO: Migrate this to VoiceProxServer.cs
//void UpdateProxChatDataForPlayer(Client c, (Vector3 playerPos, byte? scen, string? stage) playerInfo)
//{
//    clientPositionCorrelate[c] = playerInfo;
//    bool forceSendVolData = false;

//    if (!playerVolumes.ContainsKey(c)) //this client hasn't been entered yet
//    {
//        playerVolumes.Add(c, new Dictionary<Client, float>()); //this client's personal dictionary for other client local volumes.
//        //O(playercount)
//        foreach (Client cc in server.Clients)
//        {
//            if (!playerVolumes.ContainsKey(cc))
//            {
//                playerVolumes[cc] = new Dictionary<Client, float>();
//            }
//            if (cc != c)
//            {
//                //my perspective of other players
//                playerVolumes[c][cc] = 0f; //muted by default. (only applies when voiceprox is on).
//                //other players for me
//                playerVolumes[cc][c] = 0f; //"" ""
//            }
//        }
//        forceSendVolData = true; //because this user is new we need to mute everyone for them and mute them for everyone.
//    }

//    //TODO: load from config and/or make editable with a command.
//    const float beginHearingThreshold = 3500f; //how far away other people are when you just barely start to hear them.
//    const float fullHearingThreshold = 750f; //players within this distance are max volume.
//    const float soundEpsilon = 0.005f; //if change in volume is lower than this amount, then don't bother changing it.

//    float ClampedInvLerp(float a, float b, float v)
//    {
//        return v < a ? 0 : (v > b ? 1 : (v - a) / (b - a)); //see "linear interpolation"
//    }

//    PVCMultiDataPacket mainClient = new PVCMultiDataPacket();
//    //O(playerCount)
//    var c1 = clientPositionCorrelate[c];
//    lock (igToDiscord) //prevent modification by pvc server
//    {
//        foreach (var c2 in clientPositionCorrelate)
//        {
//            if (c == c2.Key)
//                continue;
//            bool significantVolChange = false;
//            float dist = Vector3.Distance(c1.pos, c2.Value.pos);
//            float vol = 1f - ClampedInvLerp(fullHearingThreshold, beginHearingThreshold, dist);
//            //may or may not want this
//            vol *= vol; //to linearize volume (which humans interpret logarithmically), we use exp to inverse and linearize. x^2 is cheap and close enough.
//            if (Math.Abs(vol - playerVolumes[c][c2.Key]) > soundEpsilon)
//            {
//                if (/*c1.scen == c2.Value.scen && */c1.stage == c2.Value.stage /*&& c1.scen != null*/ && c1.stage != null)
//                {
//                    playerVolumes[c][c2.Key] = vol;
//                    playerVolumes[c2.Key][c] = vol;
//                }
//                else
//                {
//                    playerVolumes[c][c2.Key] = 0f;
//                    playerVolumes[c2.Key][c] = 0f;
//                }
//                significantVolChange = true;
//            }
//            //cache data to send.
//            if ((igToDiscord.ContainsKey(c.Name) && igToDiscord.ContainsKey(c2.Key.Name)) && (forceSendVolData || significantVolChange))
//            {
//                mainClient.Volumes[igToDiscord[c2.Key.Name]] = proxChat ? (byte)(100 * playerVolumes[c][c2.Key]) : null;
//                //bot.ChangeVolume(igToDiscord[c.Name], igToDiscord[c2.Key.Name], proxChat ? playerVolumes[c][c2.Key] : null);
//                var packet = new PVCSingleDataPacket();
//                packet.Volume = proxChat ? (byte)(100 * playerVolumes[c2.Key][c]) : null;
//                packet.DiscordUsername = igToDiscord[c.Name];

//                //send "packet"

//                VoiceProxServer.Instance.SendPacket(packet, igToDiscord[c2.Key.Name]);
//                //bot.ChangeVolume(igToDiscord[c2.Key.Name], igToDiscord[c.Name], proxChat ? playerVolumes[c2.Key][c] : null);
//            }
//        }
//        //send "mainClient"

//        VoiceProxServer.Instance.SendPacket(mainClient, igToDiscord[c.Name]);
//    }
//}
#endregion



#region Command registry
//CommandHandler.RegisterCommand("help", _ => $"Valid commands: {string.Join(", ", CommandHandler.Handlers.Keys)}");

CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length != 0)
    {
        return "Usage: help (no arguments)";
    }
    else
    {   //dont linewrap this string to keep horizontal length from exceeding 120 (newlines are literal)
        string helpInfo = @"
dscrestart - restarts the discord bot (this has nothing to do with voice proximity, but a bot token does have to be in the server settings to use voice proximity)

pvcip - prints the IP address that voice proximity clients should use to connect to the server

getlobbysecret, gls - prints the lobby secret (If clients are prompted to enter this in their clients, that's because setautosendsecret is off)

setautosendsecret, sass <on|off> - enables/disables the automatic sending of the discord lobby secret to clients when attempting to connect

voiceproxdistances, vpxd <optional (both or neither): beginheardist, fullheardist> - prints or sets the hearing distances for voice proximity

voiceprox <optional: on|off> - prints the current state or enables/disables voice proximity

rejoin <usernames...> - forces listed connected (game) clients to leave and rejoin

crash <usernames...> - forces listed connected (game) clients to crash

ban <usernames...> - bans the users with the given usernames (this bans the IP, so this will only ban voiceprox clients if they were on the same network)

send <stage> <id> <scenario[-1..127]> <player/*> - sends the user to the specified stage

sendall <stage> - sends all users to the specified stage

scenario merge <optional: true/false> - prints the current status of or sets the scenario merge setting

tag - run command to see options

maxplayers <newplayercount> - sets the maximum number of players that can be in the game server *and* voiceprox lobby

list - lists all connected clients (for the game server) and their guid's

flip - run command to see options

shine - run command to see options

loadsettings - immediately reloads the settings

exit, quit, q - closes the server safely

help, h - prints this helpful help message!";
        return helpInfo;
    }
}, "help", "h");

CommandHandler.RegisterCommandAliases(args => 
{
    if (args.Length != 0)
    {
        return "Usage: pvcip (no arguments.)";
    }
    else
    {
        return VoiceProxServer.Instance.GetServerIP() ?? "(The server does not appear to be running.)";
    }
}, "pvcip");

CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length != 0)
    {
        return "Usage: getlobbysecret (no arguments.)";
    }
    else
    {
        return DiscordBot.Instance.GetLobbyInfo()?.secret ?? "(The lobby does not appear to be open.)";
    }
}, "getlobbysecret", "gls");

CommandHandler.RegisterCommandAliases(args => 
{
    if (args.Length == 0)
    {
        return "Need to specify on or off. (Usage: setautosendsecret <on|off>)";
    }
    else if (args.Length > 1)
    {
        return "Too many arguments. (Usage: setautosendsecret <on|off>)";
    }
    switch (args[0].ToLower())
    {
        case "true":
        case "on":
            Settings.Instance.Discord.AutoSendPVCPassword = true;
            Settings.SaveSettings();
            return "Enabled the auto-sending of the lobby secret to clients.";
        case "off":
        case "false":
            Settings.Instance.Discord.AutoSendPVCPassword = false;
            Settings.SaveSettings();
            return "Disabled the auto-sending of the lobby secret to clients.";
        default:
            return "Usage: setautosendsecret <on|off>";
    }
}, "setautosendsecret", "sass");

CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length == 0)
    {
        return $"beginheardist: {Settings.Instance.Discord.BeginHearingThreshold} fullheardist: {Settings.Instance.Discord.FullHearingThreshold}";
    }
    else if (args.Length == 2)
    {
        bool b = float.TryParse(args[0], out var beginHearingThreshold);
        bool f = float.TryParse(args[1], out var fullHearingThreshold);
        if (b && f && beginHearingThreshold > fullHearingThreshold)
        {
            Settings.Instance.Discord.FullHearingThreshold = fullHearingThreshold;
            Settings.Instance.Discord.BeginHearingThreshold = beginHearingThreshold;
            Settings.SaveSettings();
            return $"The hearing thresholds were set. beginheardist: {beginHearingThreshold} fullheardist: {fullHearingThreshold}";
        }
        else
        {
            return "Usage: voiceproxdistances <optional (both or neither): beginheardist, fullheardist> (Make sure both are positive numbers and beginheardist > fullheardist.)";
        }
    }
    else
    {
        return "Usage: voiceproxdistances <optional (both or neither): beginheardist, fullheardist>";
    }
}, "voiceproxdistances", "vpxd");

CommandHandler.RegisterCommand("voiceprox", args =>
{
    if (args.Length == 0)
    {
        return "Voice proximity is currently " + (proxChat ? "enabled." : "disabled.");
    }
    else if (args.Length > 1)
    {
        return "Too many arguments. (Usage: voiceprox <optional: on|off>)";
    }
    bool before = proxChat;
    switch (args[0])
    {
        case "on":
            proxChat = true;
            //set people's volumes
            break;
        case "off":
            proxChat = false;
            //return everyone to full volume
            break;
        default:
            return "Usage: voiceprox <optional: on|off>";
    }
    if (before != proxChat)
        VoiceProxServer.Instance.AddMessage(() =>
        {
            VoiceProxServer.Instance.SetProxChatEnabled(proxChat);
        });
    return "Turned voice proximity " + (proxChat ? "on." : "off.") + (proxChat == before ? " (it was already in that state.)" : "");
});

CommandHandler.RegisterCommand("rejoin", args =>
{
    bool moreThanOne = false;
    StringBuilder builder = new StringBuilder();
    Client[] clients = (args.Length == 1 && args[0] == "*"
        ? server.Clients.Where(c =>
            c.Connected && args.Any(x => c.Name.StartsWith(x) || (Guid.TryParse(x, out Guid result) && result == c.Id)))
        : server.Clients.Where(c => c.Connected)).ToArray();
    foreach (Client user in clients)
    {
        if (moreThanOne) builder.Append(", ");
        builder.Append(user.Name);
        user.Dispose();
        moreThanOne = true;
    }

    return clients.Length > 0 ? $"Caused {builder} to rejoin" : "Usage: rejoin <usernames...>";
});

CommandHandler.RegisterCommand("crash", args =>
{
    bool moreThanOne = false;
    StringBuilder builder = new StringBuilder();
    Client[] clients = (args.Length == 1 && args[0] == "*"
        ? server.Clients.Where(c =>
            c.Connected && args.Any(x => c.Name.StartsWith(x) || (Guid.TryParse(x, out Guid result) && result == c.Id)))
        : server.Clients.Where(c => c.Connected)).ToArray();
    foreach (Client user in clients)
    {
        if (moreThanOne) builder.Append(", ");
        moreThanOne = true;
        builder.Append(user.Name);
        Task.Run(async () =>
        {
            await user.Send(new ChangeStagePacket
            {
                Id = "$among$us/SubArea",
                Stage = "$agogusStage",
                Scenario = 21,
                SubScenarioType = 69 // invalid id
            });
            user.Dispose();
        });
    }

    return clients.Length > 0 ? $"Crashed {builder}" : "Usage: crash <usernames...>";
});

CommandHandler.RegisterCommand("ban", args =>
{
    bool moreThanOne = false;
    StringBuilder builder = new StringBuilder();

    Client[] clients = (args.Length == 1 && args[0] == "*"
        ? server.Clients.Where(c =>
            c.Connected && args.Any(x => c.Name.StartsWith(x) || (Guid.TryParse(x, out Guid result) && result == c.Id)))
        : server.Clients.Where(c => c.Connected)).ToArray();
    foreach (Client user in clients)
    {
        if (moreThanOne) builder.Append(", ");
        moreThanOne = true;
        builder.Append(user.Name);
        Task.Run(async () =>
        {
            await user.Send(new ChangeStagePacket
            {
                Id = "$agogus/banned4lyfe",
                Stage = "$ejected",
                Scenario = 69,
                SubScenarioType = 21 // invalid id
            });
            IPEndPoint? endpoint = (IPEndPoint?)user.Socket?.RemoteEndPoint;
            Settings.Instance.BanList.Players.Add(user.Id);
            if (endpoint != null) Settings.Instance.BanList.IpAddresses.Add(endpoint.ToString());
            user.Dispose();
        });
    }

    if (clients.Length > 0)
    {
        Settings.SaveSettings();
        return $"Banned {builder}.";
    }

    return "Usage: ban <usernames...>";
});

CommandHandler.RegisterCommand("send", args =>
{
    const string optionUsage = "Usage: send <stage> <id> <scenario[-1..127]> <player/*>";
    if (args.Length < 4)
        return optionUsage;

    string stage = args[0];
    string id = args[1];

    if (Constants.MapNames.TryGetValue(stage.ToLower(), out string? mapName))
    {
        stage = mapName;
    }

    if (!stage.Contains("Stage") && !stage.Contains("Zone"))
    {
        return "Invalid Stage Name! ```cap  ->  Cap Kingdom\ncascade  ->  Cascade Kingdom\nsand  ->  Sand Kingdom\nlake  ->  Lake Kingdom\nwooded  ->  Wooded Kingdom\ncloud  ->  Cloud Kingdom\nlost  ->  Lost Kingdom\nmetro  ->  Metro Kingdom\nsea  ->  Sea Kingdom\nsnow  ->  Snow Kingdom\nlunch  ->  Luncheon Kingdom\nruined  ->  Ruined Kingdom\nbowser  ->  Bowser's Kingdom\nmoon  ->  Moon Kingdom\nmush  ->  Mushroom Kingdom\ndark  ->  Dark Side\ndarker  ->  Darker Side```";
    }

    if (!sbyte.TryParse(args[2], out sbyte scenario) || scenario < -1)
        return $"Invalid scenario number {args[2]} (range: [-1 to 127])";
    Client[] players = args[3] == "*"
        ? server.Clients.Where(c => c.Connected).ToArray()
        : server.Clients.Where(c =>
                c.Connected
                && args[3..].Any(x => c.Name.StartsWith(x) || (Guid.TryParse(x, out Guid result) && result == c.Id)))
            .ToArray();
    Parallel.ForEachAsync(players, async (c, _) =>
    {
        await c.Send(new ChangeStagePacket
        {
            Stage = stage,
            Id = id,
            Scenario = scenario,
            SubScenarioType = 0
        });
    }).Wait();
    return $"Sent players to {stage}:{scenario}";
});

CommandHandler.RegisterCommand("sendall", args =>
{
    const string optionUsage = "Usage: sendall <stage>";
    if (args.Length < 1)
        return optionUsage;

    string stage = args[0];

    if (Constants.MapNames.TryGetValue(stage.ToLower(), out string? mapName))
    {
        stage = mapName;
    }

    if (!stage.Contains("Stage") && !stage.Contains("Zone"))
    {
        return "Invalid Stage Name! ```cap  ->  Cap Kingdom\ncascade  ->  Cascade Kingdom\nsand  ->  Sand Kingdom\nlake  ->  Lake Kingdom\nwooded  ->  Wooded Kingdom\ncloud  ->  Cloud Kingdom\nlost  ->  Lost Kingdom\nmetro  ->  Metro Kingdom\nsea  ->  Sea Kingdom\nsnow  ->  Snow Kingdom\nlunch  ->  Luncheon Kingdom\nruined  ->  Ruined Kingdom\nbowser  ->  Bowser's Kingdom\nmoon  ->  Moon Kingdom\nmush  ->  Mushroom Kingdom\ndark  ->  Dark Side\ndarker  ->  Darker Side```";
    }

    Client[] players = server.Clients.Where(c => c.Connected).ToArray();

    Parallel.ForEachAsync(players, async (c, _) =>
    {
        await c.Send(new ChangeStagePacket
        {
            Stage = stage,
            Id = "",
            Scenario = -1,
            SubScenarioType = 0
        });
    }).Wait();

    return $"Sent players to {stage}:{-1}";
});

CommandHandler.RegisterCommand("scenario", args =>
{
    const string optionUsage = "Valid options: merge [true/false]";
    if (args.Length < 1)
        return optionUsage;
    switch (args[0])
    {
        case "merge" when args.Length == 2:
            {
                if (bool.TryParse(args[1], out bool result))
                {
                    Settings.Instance.Scenario.MergeEnabled = result;
                    Settings.SaveSettings();
                    return result ? "Enabled scenario merge" : "Disabled scenario merge";
                }

                return optionUsage;
            }
        case "merge" when args.Length == 1:
            {
                return $"Scenario merging is {Settings.Instance.Scenario.MergeEnabled}";
            }
        default:
            return optionUsage;
    }
});

CommandHandler.RegisterCommand("tag", args =>
{
    const string optionUsage =
        "Valid options:\n\ttime <user/*> <minutes[0-65535]> <seconds[0-59]>\n\tseeking <user/*> <true/false>\n\tstart <time> <seekers>";
    if (args.Length < 3)
        return optionUsage;
    switch (args[0])
    {
        case "time" when args.Length == 4:
            {
                if (args[1] != "*" && server.Clients.All(x => x.Name != args[1])) return $"Cannot find user {args[1]}";
                Client? client = server.Clients.FirstOrDefault(x => x.Name == args[1]);
                if (!ushort.TryParse(args[2], out ushort minutes))
                    return $"Invalid time for minutes {args[2]} (range: 0-65535)";
                if (!byte.TryParse(args[3], out byte seconds) || seconds >= 60)
                    return $"Invalid time for seconds {args[3]} (range: 0-59)";
                TagPacket tagPacket = new TagPacket
                {
                    UpdateType = TagPacket.TagUpdate.Time,
                    Minutes = minutes,
                    Seconds = seconds
                };
                if (args[1] == "*")
                    server.Broadcast(tagPacket);
                else
                    client?.Send(tagPacket);
                return $"Set time for {(args[1] == "*" ? "everyone" : args[1])} to {minutes}:{seconds}";
            }
        case "seeking" when args.Length == 3:
            {
                if (args[1] != "*" && server.Clients.All(x => x.Name != args[1])) return $"Cannot find user {args[1]}";
                Client? client = server.Clients.FirstOrDefault(x => x.Name == args[1]);
                if (!bool.TryParse(args[2], out bool seeking)) return $"Usage: tag seeking {args[1]} <true/false>";
                TagPacket tagPacket = new TagPacket
                {
                    UpdateType = TagPacket.TagUpdate.State,
                    IsIt = seeking
                };
                if (args[1] == "*")
                    server.Broadcast(tagPacket);
                else
                    client?.Send(tagPacket);
                return $"Set {(args[1] == "*" ? "everyone" : args[1])} to {(seeking ? "seeker" : "hider")}";
            }
        case "start" when args.Length > 2:
            {
                if (!byte.TryParse(args[1], out byte time)) return $"Invalid countdown seconds {args[1]} (range: 0-255)";
                string[] seekerNames = args[2..];
                Client[] seekers = server.Clients.Where(c => seekerNames.Contains(c.Name)).ToArray();
                if (seekers.Length != seekerNames.Length)
                    return
                        $"Couldn't find seeker{(seekerNames.Length > 1 ? "s" : "")}: {string.Join(", ", seekerNames.Where(name => server.Clients.All(c => c.Name != name)))}";
                Task.Run(async () =>
                {
                    int realTime = 1000 * time;
                    await Task.Delay(realTime);
                    await Task.WhenAll(
                        Parallel.ForEachAsync(seekers, async (seeker, _) =>
                            await server.Broadcast(new TagPacket
                            {
                                UpdateType = TagPacket.TagUpdate.State,
                                IsIt = true
                            }, seeker)),
                        Parallel.ForEachAsync(server.Clients.Except(seekers), async (hider, _) =>
                            await server.Broadcast(new TagPacket
                            {
                                UpdateType = TagPacket.TagUpdate.State,
                                IsIt = false
                            }, hider)
                        )
                    );
                    consoleLogger.Info($"Started game with seekers {string.Join(", ", seekerNames)}");
                });
                return $"Starting game in {time} seconds with seekers {string.Join(", ", seekerNames)}";
            }
        default:
            return optionUsage;
    }
});

CommandHandler.RegisterCommand("maxplayers", args =>
{
    const string optionUsage = "Valid usage: maxplayers <playercount>";
    if (args.Length != 1) return optionUsage;
    if (!ushort.TryParse(args[0], out ushort maxPlayers)) return optionUsage;
    Settings.Instance.Server.MaxPlayers = maxPlayers;
    Settings.SaveSettings();
    DiscordBot.Instance.ChangePVCLobbySize(maxPlayers);
    foreach (Client client in server.Clients)
        client.Dispose(); // reconnect all players
    return $"Saved and set max players to {maxPlayers}";
});

CommandHandler.RegisterCommand("list",
    _ => $"List: {string.Join("\n\t", server.Clients.Where(x => x.Connected).Select(x => $"{x.Name} ({x.Id})"))}");

CommandHandler.RegisterCommand("flip", args =>
{
    const string optionUsage =
        "Valid options: \n\tlist\n\tadd <user id>\n\tremove <user id>\n\tset <true/false>\n\tpov <both/self/others>";
    if (args.Length < 1)
        return optionUsage;
    switch (args[0])
    {
        case "list" when args.Length == 1:
            return "User ids: " + string.Join(", ", Settings.Instance.Flip.Players.ToList());
        case "add" when args.Length == 2:
            {
                if (Guid.TryParse(args[1], out Guid result))
                {
                    Settings.Instance.Flip.Players.Add(result);
                    Settings.SaveSettings();
                    return $"Added {result} to flipped players";
                }

                return $"Invalid user id {args[1]}";
            }
        case "remove" when args.Length == 2:
            {
                if (Guid.TryParse(args[1], out Guid result))
                {
                    string output = Settings.Instance.Flip.Players.Remove(result)
                        ? $"Removed {result} to flipped players"
                        : $"User {result} wasn't in the flipped players list";
                    Settings.SaveSettings();
                    return output;
                }

                return $"Invalid user id {args[1]}";
            }
        case "set" when args.Length == 2:
            {
                if (bool.TryParse(args[1], out bool result))
                {
                    Settings.Instance.Flip.Enabled = result;
                    Settings.SaveSettings();
                    return result ? "Enabled player flipping" : "Disabled player flipping";
                }

                return optionUsage;
            }
        case "pov" when args.Length == 2:
            {
                if (Enum.TryParse(args[1], true, out FlipOptions result))
                {
                    Settings.Instance.Flip.Pov = result;
                    Settings.SaveSettings();
                    return $"Point of view set to {result}";
                }

                return optionUsage;
            }
        default:
            return optionUsage;
    }
});

CommandHandler.RegisterCommand("shine", args =>
{
    const string optionUsage = "Valid options: list, clear, sync, send";
    if (args.Length < 1)
        return optionUsage;
    switch (args[0])
    {
        case "list" when args.Length == 1:
            return $"Shines: {string.Join(", ", shineBag)}";
        case "clear" when args.Length == 1:
            shineBag.Clear();
            foreach (ConcurrentBag<int> playerBag in server.Clients.Select(serverClient =>
                (ConcurrentBag<int>)serverClient.Metadata["shineSync"])) playerBag.Clear();

            return "Cleared shine bags";
        case "sync" when args.Length == 1:
            SyncShineBag();
            return "Synced shine bag automatically";
        case "send" when args.Length >= 3:
            if (int.TryParse(args[1], out int id))
            {
                Client[] players = args[2] == "*"
                    ? server.Clients.Where(c => c.Connected).ToArray()
                    : server.Clients.Where(c => c.Connected && args[3..].Contains(c.Name)).ToArray();
                Parallel.ForEachAsync(players, async (c, _) =>
                {
                    await c.Send(new ShinePacket
                    {
                        ShineId = id
                    });
                }).Wait();
                return $"Sent Shine Num {id}";
            }

            return optionUsage;
        default:
            return optionUsage;
    }
});

CommandHandler.RegisterCommand("loadsettings", _ =>
{
    Settings.LoadSettings();
    return "Loaded settings.json";
});

CommandHandler.RegisterCommandAliases(_ =>
{
    DiscordBot.Instance.ClosePVCLobbyForQuit();
    cts.Cancel();
    return "Shutting down";
}, "exit", "quit", "q");
#endregion

#region Event Subscription
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    consoleLogger.Info("Received Ctrl+C");
    DiscordBot.Instance.ClosePVCLobbyForQuit();
    cts.Cancel();
};

AppDomain.CurrentDomain.ProcessExit += (_, e) =>
{
    DiscordBot.Instance.ClosePVCLobbyForQuit();
    cts.Cancel();
};
#endregion

#region Input loop
Task.Run(() =>
{
    consoleLogger.Info("Run help command for valid commands.");
    while (true)
    {
        string? text = Console.ReadLine();
        if (text != null)
        {
            foreach (string returnString in CommandHandler.GetResult(text).ReturnStrings)
            {
                consoleLogger.Info(returnString);
            }
        }
    }
});
#endregion

await listenTask;