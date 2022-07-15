//using DotNetOpenAuth;
//using DotNetOpenAuth.OAuth2;
using System.Web;
using System.Net;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Discord;
using System;
using System.Text;
using System.Threading.Tasks;
using Shared;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ENet;
using static Shared.Constants;

class Program
{
    //TODO: SWITCH TO LOGGER

    #region WinAPI
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    private static readonly IntPtr winHandle = Process.GetCurrentProcess().MainWindowHandle;
    private const int SW_HIDE = 0;
    private const int SW_RESTORE = 9;

    private static void SetWindowVisible(bool visible)
    {
        if (visible)
            ShowWindow(winHandle, SW_RESTORE);
        else
            ShowWindow(winHandle, SW_HIDE);
    }
    #endregion

    public static void Main(string[] args)
    {
        {
            DateTime launchTime = DateTime.Now;

            //may not be necessary
            if (args.Length > 0)
            {
                foreach (string arg in args)
                {
                    switch (arg)
                    {
                        case "headless":
                            SetWindowVisible(false);
                            break;
                        case "log":
                            Logger.AddLogHandler((source, level, text, _) =>
                            {
                                DateTime logtime = DateTime.Now;
                                string data = Logger.PrefixNewLines(text, $"{{{logtime}}} {level} [{source}]");
                                File.AppendAllText($"log_{launchTime.Month}-{launchTime.Day}-{launchTime.Year}::{launchTime.Hour}-{launchTime.Minute}-{launchTime.Second}.txt", data);
                            });
                            break;
                        default: //no handler
                            break;
                    }
                }
            }
        }
        //IPAddress? serverAddress = null;
        //TcpClient? server = null;
        Library.Initialize();
        Host? client = null;

        object discordLockKey = new object(); //for locks to prevent race conditions
        byte defaultVol = 150; //100% volume for discord is decently louder than 100 for this, hence 150 for default.
        Dictionary<long, User> userCache = new Dictionary<long, User>();
        Dictionary<string, long> nameToIdCache = new Dictionary<string, long>();
        Dictionary<long, byte> userPrefVolumes = new Dictionary<long, byte>(); //the volumes adjusted by the user (nothing to do with proximity).
        Queue<Action> messageQueue = new Queue<Action>();

        Discord.Discord discord = null!;
        LobbyManager lobbyManager = null!;
        VoiceManager voiceManager = null!;

        Logger pvcLogger = new Logger("ProxVoiceChat Client");

        bool proxMode = false;
        User? currentUser = null;
        Lobby? lob = null;

        ulong singleTicker = 0;
        string lastSingleUsername = null!;
        ulong multiTicker = 0;

        //using (SemaphoreSlim sema = new SemaphoreSlim(0, 1))
        //{
        //Task udpTask = Task.Run(() =>
        //{
        //    //server = new TcpClient()
        //    //server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { Blocking = false, NoDelay = true };
        //    sema.Release();
        //});

        Task discordTask = Task.Run(() =>
        {
            #region Setup
            discord = new Discord.Discord(Constants.clientId, (UInt64)Discord.CreateFlags.Default);
            lobbyManager = discord.GetLobbyManager();
            voiceManager = discord.GetVoiceManager();
            // Use your client ID from Discord's developer site.
            discord.SetLogHook(Discord.LogLevel.Debug, (level, message) =>
            {
                pvcLogger.Info($"Log[{level}] {message}");
            });
            #endregion

            #region Current User aquisition
            var userManager = discord.GetUserManager();
            // GetCurrentUser will error until this fires once.
            userManager.OnCurrentUserUpdate += () =>
            {
                currentUser = userManager.GetCurrentUser();
                userCache[currentUser.Value.Id] = currentUser.Value;
                nameToIdCache[currentUser.Value.Username + "#" + currentUser.Value.Discriminator] = currentUser.Value.Id;
                //broadcast change if in lobby (just use OnMemberUpdate instead)
            };
            while (currentUser == null)
            {
                discord.RunCallbacks(); //do this until we have the current user.
                Thread.Sleep(16);
            }
            #endregion

            #region Event subscriptions
            lobbyManager.OnMemberConnect += (long lobbyId, long userId) =>
            {
                userManager.GetUser(userId, (Result res, ref User user) =>
                {
                    if (res != Result.Ok)
                    {
                        pvcLogger.Error("GetUser failed in OnMemberConnect");
                        return;
                    }
                    userCache[userId] = user;
                    string userName = user.Username + "#" + user.Discriminator;
                    nameToIdCache[userName] = user.Id;
                    byte? vol = Settings.Instance.GetUserVolumePreference(userName);
                    if (vol != null)
                    {
                        userPrefVolumes[userId] = vol.Value;
                    }
                    else
                    {
                        userPrefVolumes[userId] = defaultVol;
                        Settings.Instance.SetUserVolumePreference(userName, defaultVol);
                    }
                    voiceManager.SetLocalVolume(userId, userPrefVolumes[userId]);
                });
            };

            lobbyManager.OnMemberDisconnect += (long lobbyId, long userId) =>
            {
                string userName = userCache[userId].Username + "#" + userCache[userId].Discriminator;
                Settings.Instance.SetUserVolumePreference(userName, userPrefVolumes[userId]);
                nameToIdCache.Remove(userName);
                userCache.Remove(userId);
                userPrefVolumes.Remove(userId);
            };
            #endregion

            #region Callback loop
            // run callback loop
            try
            {
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                Event netEvent;
                sw.Start();
                while (true)
                {
                    lock (discordLockKey)
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
                            Console.WriteLine($"Issue in message loop: {ex.ToString()}");
                        }

                        if (client != null && client.IsSet) //what does IsSet mean?
                        {
                            int checkResult = client.CheckEvents(out netEvent);
                            if (checkResult == 0)
                            {
                                //no events.
                                client.Service(0, out netEvent);
                            }
                            else if (checkResult < 0)
                            {
                                Console.WriteLine("CheckEvents failed.");
                            }

                            switch (netEvent.Type)
                            {
                                case EventType.None:
                                    break;
                                case EventType.Connect:
                                    {
                                        HandleConnectEvent(ref netEvent);
                                    }
                                    break;
                                case EventType.Disconnect:
                                    {
                                        HandleDisconnectEvent(ref netEvent);
                                    }
                                    break;
                                case EventType.Receive:
                                    {
                                        HandleRecieveEvent(ref netEvent);
                                    }
                                    break;
                                case EventType.Timeout:
                                    {
                                        HandleTimeoutEvent(ref netEvent);
                                    }
                                    break;
                                default:
                                    Console.WriteLine("Invalid netevent type");
                                    break;
                            }
                            client.Flush();
                        }
                    }
                    discord.RunCallbacks();



                    //lobbyManager.FlushNetwork();
                    while (sw.ElapsedMilliseconds < 16) { }
                    sw.Restart(); //1000 / 16 = ~60 times a second (not including the time it takes to do the callbacks
                                  //themselves e.g. if callbacks take ~4ms, then it's only 50 times a second.
                }
            }
            finally
            {
                discord.Dispose();
                client?.Dispose();
            }
            #endregion
        });
        //    sema.Wait();
        //}

        #region NetEvent Handlers
        void HandleConnectEvent(ref Event netEvent)
        {
            pvcLogger.Info("Client successfully connected to server.");
            SendPacket(new PVCClientHandshakePacket()
            {
                DiscordUsername = currentUser!.Value.Username + "#" + currentUser!.Value.Discriminator,
                IngameUsername = Settings.Instance.IngameName!
            }, netEvent.Peer);
        }

        void HandleRecieveEvent(ref Event netEvent)
        {
            PVCPacket? packet = Protocol.Deserialize<PVCPacket>(netEvent.Packet.Data, netEvent.Packet.Length);
            if (packet != null)
            {
                switch (packet)
                {
                    case PVCWalkieTalkiePacket walkiePacket:
                        //the client should never recieve a walkie packet
                        SendPacket(new PVCErrorPacket() { ErrorMessage = "Why are you sending the client a PVCWalkieTalkiePacket?" }, netEvent.Peer);
                        break;
                    case PVCMultiDataPacket multiPacket:
                        {
                            if (multiPacket.Tick > multiTicker)
                            {
                                foreach (var pair in multiPacket.Volumes)
                                {
                                    if (multiPacket.SingleTick < singleTicker && lastSingleUsername == pair.Key)
                                    {
                                        //don't overwrite the last single, because the last single was newer than this multi.
                                        continue;
                                    }
                                    byte? vol = pair.Value;
                                    if (vol == null)
                                    {
                                        //default volume for this user.
                                        voiceManager.SetLocalVolume(userCache[nameToIdCache[pair.Key]].Id, userPrefVolumes[userCache[nameToIdCache[pair.Key]].Id]);
                                    }
                                    else
                                    {
                                        float fvol = (vol.Value / 100f);
                                        byte finalVolume = (byte)(fvol * userPrefVolumes[userCache[nameToIdCache[pair.Key]].Id]);
                                        voiceManager.SetLocalVolume(userCache[nameToIdCache[pair.Key]].Id, finalVolume);
                                    }
                                }
                                multiTicker = multiPacket.Tick;
                            }
                        }
                        break;
                    case PVCSingleDataPacket singlePacket:
                        {
                            if (singlePacket.Tick > singleTicker) //only accept if newer
                            {
                                if (singlePacket.MultiTick >= multiTicker) //only overwrite if newer than last multi we received.
                                {
                                    lastSingleUsername = singlePacket.DiscordUsername;
                                    singleTicker = singlePacket.Tick;
                                }
                            }
                        }
                        break;
                    case PVCClientHandshakePacket handshakePacket:
                        //the client should never recieve a client handshake packet
                        SendPacket(new PVCErrorPacket() { ErrorMessage = "Why are you sending the client a PVCClientHandshakePacket?" }, netEvent.Peer);
                        break;
                    case PVCLobbyPacket lobbyPacket:
                        {
                            //no lock here (already locked)
                            long id = lobbyPacket.LobbyId;
                            string secret = lobbyPacket.Secret;
                            messageQueue.Enqueue(() =>
                            {
                                if (lob != null)
                                {
                                    //disconnect from prev lobby.
                                    StringBuilder resultMessage = new StringBuilder();
                                    resultMessage.Append("You are already in a lobby! leaving the current lobby...");
                                    lobbyManager.DisconnectLobby(lob.Value.Id, res =>
                                    {
                                        if (res != Result.Ok)
                                        {
                                            resultMessage.Append($"Something went wrong with leaving the lobby: {res.ToString()}");
                                        }
                                        else
                                        {
                                            resultMessage.Append("You left the lobby");
                                            userCache.Clear();
                                            userPrefVolumes.Clear();
                                            nameToIdCache.Clear();
                                        }
                                        lob = null;
                                        pvcLogger.Info(resultMessage.ToString());
                                    });
                                }
                                while (lob != null)
                                {
                                    discord.RunCallbacks();
                                }
                                lobbyManager.ConnectLobby(id, secret, (Result res, ref Lobby lobby) =>
                                {
                                    StringBuilder resultMessage = new StringBuilder(); //aggregating so that messages don't get
                                                                                       //mixed up with other threads.
                                    if (res != Result.Ok)
                                    {
                                        pvcLogger.Info("Something went wrong when joining the lobby.");
                                        return;
                                    }
                                    else
                                    {
                                        resultMessage.Append("Joined the lobby successfully.\n");
                                    }
                                    IEnumerable<User> users = lobbyManager.GetMemberUsers(lobby.Id);
                                    resultMessage.Append("All users in the lobby: " +
                                        $"{string.Join(",\n", users.Select(x => $"{x.Id}: {x.Username}#{x.Discriminator}"))}\n");
                                    foreach (User u in lobbyManager.GetMemberUsers(lobby.Id))
                                    {
                                        if (u.Id != currentUser.Value.Id)
                                        {
                                            userCache[u.Id] = u;
                                            nameToIdCache[u.Username + "#" + u.Discriminator] = u.Id;
                                            userPrefVolumes[u.Id] = defaultVol;
                                            voiceManager.SetLocalVolume(u.Id, defaultVol);
                                        }
                                    }

                                    lobbyManager.ConnectVoice(lobby.Id, x =>
                                    {
                                        if (res != Result.Ok)
                                        {
                                            resultMessage.Append("Something went wrong when joining vc.");
                                        }
                                        else
                                        {
                                            resultMessage.Append("Joined vc.");
                                        }
                                        pvcLogger.Info(resultMessage.ToString());
                                    });
                                    lobbyManager.ConnectNetwork(lobby.Id);
                                    lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
                                    lob = lobby;
                                });
                            });
                        }
                        break;
                    case PVCErrorPacket errorPacket:
                        {
                            pvcLogger.Error($"Error packet from server: \"{errorPacket.ErrorMessage ?? "(No error message present.)"}\"");
                        }
                        break;
                    default:
                        pvcLogger.Error("HandleRecieve got a packet with a bad packet type.");
                        break;
                }
            }
            else
            {
                pvcLogger.Error($"HandleRecieve got an unparseable packet (contents): {Marshal.PtrToStringAnsi(netEvent.Packet.Data, netEvent.Packet.Length)}");
            }
        }

        void HandleTimeoutEvent(ref Event netEvent)
        {
            HandleDisconnectEvent(ref netEvent);
        }

        void HandleDisconnectEvent(ref Event netEvent)
        {
            pvcLogger.Error($"Server disconnected or timed out. Code: {(netEvent.Data == 69420 ? "(You've been banned)." : netEvent.Data.ToString())}");
        }

        void SendPacket<T>(T packet, Peer recipient) where T : PVCPacket
        {
            byte[] data = Protocol.Serialize(packet);
            Packet pack = default(Packet); //ENet will dispose automatically when sending.
            pack.Create(data, PacketFlags.Reliable);
            bool result = recipient.Send(0, ref pack);
            if (!result)
            {
                Console.WriteLine($"Sendpacket failed on packet type {packet.PType}.");
            }
        }
        #endregion

        //TODO: ADD KEYBINDS

        //TODO: VISUAL UI

        //TODO: ADD LOG FILE FEATURE (something to do with the discord log too?)

        #region Command Registry
        //TODO: MAKE ALL OF THESE THAT ARE CALLED BY THE GUI RETURN MEANINGFUL INFO IMMEDIATELY INSTEAD OF LATER IN A WRITELINE

        CommandHandler.RegisterCommandAliases(args =>
        {
            if (args.Length != 0)
            {
                return "Usage: deafen (no arguments.)";
            }
            else
            {
                lock (discordLockKey)
                {
                    messageQueue.Enqueue(() =>
                    {
                        voiceManager.SetSelfDeaf(true);
                        voiceManager.SetSelfMute(true);
                        Console.WriteLine("You have been deafened.");
                    });
                }
                return "";
            }
        }, "deafen", "d");

        CommandHandler.RegisterCommandAliases(args =>
        {
            if (args.Length != 0)
            {
                return "Usage: undeafen (no arguments.)";
            }
            else
            {
                lock (discordLockKey)
                {
                    messageQueue.Enqueue(() =>
                    {
                        voiceManager.SetSelfDeaf(false);
                        voiceManager.SetSelfMute(false);
                        Console.WriteLine("You have been undeafened.");
                    });
                }
                return "";
            }
        }, "undeafen", "ud");

        CommandHandler.RegisterCommandAliases(args =>
        {
            if (args.Length != 0)
            {
                return "Usage: vollist (no arguments.)";
            }
            else
            {
                lock (discordLockKey)
                {
                    messageQueue.Enqueue(() =>
                    {
                        StringBuilder resultMessage = new StringBuilder();
                        //lock for critical section handled around the dequeue in the discord loop
                        foreach (var kvp in nameToIdCache)
                        {
                            if (kvp.Value != currentUser.Value.Id)
                            {
                                resultMessage.Append($"{kvp.Key}: {userPrefVolumes[kvp.Value]}\n");
                            }
                            else
                            {
                                //you are not in the vol dictionary
                                //resultMessage.Append($"{kvp.Key}: {userPrefVolumes[kvp.Value]} ({(voiceManager.IsSelfDeaf() ? "deafened" : "not deafened")}) " +
                                //    $"({(voiceManager.IsSelfMute() ? "muted" : "not muted")})\n");
                            }
                        }
                        Console.Write(resultMessage.ToString());
                    });
                }
                return "";
            }
        }, "vollist", "vl");

        CommandHandler.RegisterCommandAliases(args =>
        {
            if (args.Length != 0)
            {
                return "Usage: truevollist (no arguments.)";
            }
            else
            {
                lock (discordLockKey)
                {
                    messageQueue.Enqueue(() =>
                    {
                        StringBuilder resultMessage = new StringBuilder();
                        //lock for critical section handled around the dequeue in the discord loop
                        foreach (var kvp in nameToIdCache)
                        {
                            if (kvp.Value != currentUser.Value.Id)
                            {
                                resultMessage.Append($"{kvp.Key}: {voiceManager.GetLocalVolume(kvp.Value)}\n");
                            }
                            else
                            {
                                //you are not in the vol dictionary
                                //resultMessage.Append($"{kvp.Key}: {userPrefVolumes[kvp.Value]} ({(voiceManager.IsSelfDeaf() ? "deafened" : "not deafened")}) " +
                                //    $"({(voiceManager.IsSelfMute() ? "muted" : "not muted")})\n");
                            }
                        }
                        Console.Write(resultMessage.ToString());
                    });
                }
                return "";
            }
        }, "truevollist", "tvl");

        CommandHandler.RegisterCommandAliases(args =>
        {
            if (args.Length == 1)
            {
                //set to default.
                lock (discordLockKey) //lock for enqueue
                {
                    messageQueue.Enqueue(() =>
                    {
                        //lock for critical section handled around the dequeue in the discord loop
                        if (nameToIdCache.ContainsKey(args[0]))
                        {
                            long userId = nameToIdCache[args[0]];
                            if (proxMode)
                            {
                                float ratio = ((float)defaultVol) / userPrefVolumes[userId];
                                byte oldVol = voiceManager.GetLocalVolume(userId);
                                byte newVol = (byte)(oldVol * ratio);
                                voiceManager.SetLocalVolume(userId, newVol);
                            }
                            else
                            {
                                voiceManager.SetLocalVolume(userId, defaultVol);
                            }
                            userPrefVolumes[userId] = defaultVol;
                            Console.WriteLine($"{args[0]}'s volume was set to {defaultVol}.");
                        }
                        else
                        {
                            Console.WriteLine("That user does not appear to be in the lobby. No action was taken.");
                        }
                    });
                }
                return "";
            }
            else if (args.Length == 2)
            {
                if (byte.TryParse(args[1], out var vol))
                {
                    lock (discordLockKey) //lock for enqueue
                    {
                        messageQueue.Enqueue(() =>
                        {
                            //lock for critical section handled around the dequeue in the discord loop
                            if (nameToIdCache.ContainsKey(args[0]))
                            {
                                long userId = nameToIdCache[args[0]];
                                if (proxMode)
                                {
                                    float ratio = ((float)vol) / userPrefVolumes[userId];
                                    byte oldVol = voiceManager.GetLocalVolume(userId);
                                    byte newVol = (byte)(oldVol * ratio);
                                    voiceManager.SetLocalVolume(userId, newVol);
                                }
                                else
                                {
                                    voiceManager.SetLocalVolume(userId, vol);
                                }
                                userPrefVolumes[userId] = vol;
                                Console.WriteLine($"{args[0]}'s volume was set to {vol}.");
                            }
                            else
                            {
                                Console.WriteLine("That user does not appear to be in the lobby. No action was taken.");
                            }
                        });
                    }
                    return "";
                }
                else
                {
                    return "Usage: volset <username> <optional: volume 0-200> (volume must be a number.)";
                }
            }
            else
            {
                return "Usage: volset <username> <optional: volume 0-200>";
            }
        }, "volset", "vs");

        CommandHandler.RegisterCommandAliases(args =>
        {
            if (args.Length != 0)
            {
                return "Usage: voldefaultall (no arguments.)";
            }
            else
            {
                lock (discordLockKey) //lock for enqueue
                {
                    messageQueue.Enqueue(() =>
                    {
                        //lock for critical section handled around the dequeue in the discord loop
                        foreach (var userId in userCache.Keys)
                        {
                            if (userId != currentUser.Value.Id)
                            {
                                if (proxMode)
                                {
                                    float ratio = ((float)defaultVol) / userPrefVolumes[userId];
                                    byte oldVol = voiceManager.GetLocalVolume(userId);
                                    byte newVol = (byte)(oldVol * ratio);
                                    voiceManager.SetLocalVolume(userId, newVol);
                                }
                                else
                                {
                                    voiceManager.SetLocalVolume(userId, defaultVol);
                                }
                                userPrefVolumes[userId] = defaultVol;
                            }
                        }
                        Console.WriteLine($"Set the volumes of all of the users to the default volume of {defaultVol}.");
                    });
                }
                return "";
            }
        }, "voldefaultall", "vda");

        CommandHandler.RegisterCommandAliases(args =>
        {
            if (args.Length != 1 || !byte.TryParse(args[0].Trim(), out var value))
            {
                return "Usage: volsetdefault <volume 0-200>";
            }
            else
            {
                lock (discordLockKey) //avoid race conditions.
                {
                    defaultVol = value;
                }
                return $"Set the default volume to {value}, any new users joining will be set to this volume.";
            }
        }, "volsetdefault", "vsd");

        CommandHandler.RegisterCommandAliases(args =>
        {
            if (args.Length != 1)
            {
                return "Usage: setig <ingame username>";
            }
            else
            {
                Settings.Instance.IngameName = args[0];
                return $"Your ingame username has been set to \"{args[0]}\".";
            }
        }, "setig");

        CommandHandler.RegisterCommandAliases(args =>
        {
            if (Settings.Instance.IngameName == null)
            {
                return "You must set your ingame username before you can join a server! run \"setig\".";
            }
            if (args.Length == 1)
            {
                if (IPAddress.TryParse(args[0].Trim(), out var value))
                {
                    Settings.Instance.ServerIP = value.ToString();
                }
                else
                {
                    return "Usage: joinserver <ipaddress> (Make sure the IP address is IPV4, properly formatted, and does not include a port.)";
                }
            }
            else if (args.Length == 0 && Settings.Instance.ServerIP == null)
            {
                return "Usage: joinserver <ipaddress> (You don't have a server IP address saved in settings.json, so you must manually specify one.)";
            }
            Address adr = new Address() { Port = Settings.Instance.ServerPort };
            adr.SetHost(Settings.Instance.ServerIP);
            client?.Dispose();
            client = new Host();
            client.Create();
            client.Connect(adr); //cant send packet until after connected.
            return "Joining server...";
        }, "joinserver", "js");

        CommandHandler.RegisterCommandAliases(args =>
        {
            client?.Dispose(); //there doesn't seem to be an easy better way to do this.
            return "Disconnected from server.";
        }, "leaveserver", "ls");

        CommandHandler.RegisterCommandAliases(args =>
        {
            if (args.Length != 1)
            {
                return "Usage: voiceprox <on|off>";
            }
            else
            {
                switch (args[0])
                {
                    case "on":
                        proxMode = true;
                        break;
                    case "off":
                        proxMode = false;
                        break;
                    default:
                        return "Usage: voiceprox <on|off>";
                }
                lock (discordLockKey) //lock for enqueue
                {
                    messageQueue.Enqueue(() =>
                    {
                        //lock for critical section handled around the dequeue in the discord loop
                        foreach (var userId in userCache.Keys)
                        {
                            if (userId != currentUser.Value.Id)
                            {
                                voiceManager.SetLocalVolume(userId, proxMode ? (byte)0 : userPrefVolumes[userId]);
                            }
                        }
                        Console.WriteLine($"Successfully enabled voice proximity.");
                    });
                }
                return "";
            }
        }, "voiceprox", "vp");

        CommandHandler.RegisterCommandAliases(args =>
        {
            if (args.Length == 0)
            {
                lock (discordLockKey)
                {
                    messageQueue.Enqueue(() =>
                    {
                        voiceManager.SetSelfMute(true);
                        Console.WriteLine("You have been muted.");
                    });
                }
                return "";
            }
            else if (args.Length == 1)
            {
                if (nameToIdCache.ContainsKey(args[0])) //race condition, what if they're present now but are gone when the message is executed?
                {
                    lock (discordLockKey)
                    {
                        messageQueue.Enqueue(() =>
                        {
                            voiceManager.SetLocalMute(nameToIdCache[args[0]], true);
                            Console.WriteLine($"The user \"{args[0]}\" has been muted locally.");
                        });
                    }
                    return "";
                }
                else
                {
                    return $"The user \"{args[0]}\" doesn't appear to be in the lobby. (No action was taken.)";
                }
            }
            else
            {
                return "Usage: mute <optional: username>";
            }
        }, "mute", "m");

        CommandHandler.RegisterCommandAliases(args =>
        {
            if (args.Length == 0)
            {
                lock (discordLockKey)
                {
                    messageQueue.Enqueue(() =>
                    {
                        voiceManager.SetSelfMute(false);
                        Console.WriteLine("You have been unmuted.");
                    });
                }
                return "";
            }
            else if (args.Length == 1)
            {
                if (nameToIdCache.ContainsKey(args[0]))
                {
                    lock (discordLockKey)
                    {
                        messageQueue.Enqueue(() =>
                        {
                            voiceManager.SetLocalMute(nameToIdCache[args[0]], false);
                            Console.WriteLine($"The user \"{args[0]}\" has been unmuted locally.");
                        });
                    }
                    return "";
                }
                else
                {
                    return $"The user \"{args[0]}\" doesn't appear to be in the lobby. (No action was taken.)";
                }
            }
            else
            {
                return "Usage: unmute <optional: username>";
            }
        }, "unmute", "um");

        CommandHandler.RegisterCommandAliases(args =>
        {
            if (args.Length != 0)
            {
                return "Usage: help (no arguments)";
            }
            else
            {   //dont linewrap this string to keep horizontal length from exceeding 120 (newlines are literal)
                string helpInfo = @"
deafen, d - equivalent to a discord deafen (you can't hear anyone, nobody can hear you)

undeafen, ud - equivalent to a discord undeafen

mute, m <optional: username> - mutes the specified user locally, if no user is specified, you are muted globally

unmute, um <optional: username> - unmutes the specified user locally, if no user is specified, you are unmuted globally

vollist, vl - lists the user-set volume levels for everyone in the lobby

truevollist, tvl - lists the current volume levels for everyone in the lobby (e.g. prox chat makes people far away quiet, so for far away players it will report 0)

volset, vs <username> <optional: volume 0-200> - sets the volume of the specified user to the specified amount, if no volume is specified, the volume will be set to default

voldefaultall, vda - sets everyone to the default volume level

volsetdefault, vsd <volume 0-200> - sets a new default volume level, this does not change the volume of any user (default is 100)

joinlobby, jl <lobby id> <lobby secret> - joins your client to a voice lobby for proximity chat

leavelobby, ll - leaves the lobby your in (if you aren't in a lobby, this does nothing)

voiceprox, vp <on|off> - enables/disables voice proximity, enabling will mute all other user until proximity data arrives, disabling will restore all users to their pre-proximity chat volumes

startlobby - [TESTING ONLY] starts a voice lobby.

help, h - shows this helpful command list
";
                return helpInfo;
            }
        }, "help", "h");


        #region Visual API
        //these functions are called only by winforms, not intended to be called by the user.

        CommandHandler.RegisterCommandAliases(args =>
        {
            return Settings.Instance.IngameName != null && Settings.Instance.ServerIP != null ? "true" : "false";
        }, "***haveRequisiteConnectionData");
        #endregion

        //testing purposes only
        //CommandHandler.RegisterCommandAliases(args =>
        //{
        //    if (args.Length != 0)
        //    {
        //        return "Usage: startlobby (No arguments.)";
        //    }
        //    else
        //    {
        //        messageQueue.Enqueue(() =>
        //        {
        //            var trans = lobbyManager.GetLobbyCreateTransaction();
        //            trans.SetCapacity(4);
        //            trans.SetType(LobbyType.Private);
        //            lobbyManager.CreateLobby(trans, (Result res, ref Lobby lobby) =>
        //            {
        //                StringBuilder resultMessage = new StringBuilder();
        //                if (res != Result.Ok)
        //                {
        //                    Console.WriteLine("Something went wrong when creating the lobby");
        //                    return;
        //                }
        //                resultMessage.Append($"ID: {lobby.Id} Secret: {lobby.Secret}");
        //                lobbyManager.ConnectVoice(lobby.Id, x =>
        //                {
        //                    if (res != Result.Ok)
        //                    {
        //                        resultMessage.Append("Something went wrong when joining vc");
        //                    }
        //                    else
        //                    {
        //                        resultMessage.Append("Joined vc");
        //                    }
        //                });
        //                lobbyManager.ConnectNetwork(lobby.Id);
        //                //lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
        //                lob = lobby;
        //                Console.WriteLine(resultMessage.ToString());
        //            });
        //        });
        //        return "";
        //    }
        //}, "startlobby");
        #endregion

        //void UnbindSocket(ref Socket s)
        //{
        //    if (s.IsBound)
        //    {
        //        if (s.Connected)
        //        {
        //            try
        //            {
        //                byte[] json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new PVCDisconnectPacket() { IntendToRejoin = false });
        //                s.Send(json);
        //            }
        //            catch { }
        //        }
        //        s.Dispose();
        //    }
        //    s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { Blocking = false, NoDelay = true };
        //}

        #region Command loop
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Loading...");
        while (currentUser == null || lobbyManager == null || voiceManager == null)
        {
            Thread.Sleep(50); //currentUser etc. has to be assigned before the user is allowed to run commands.
        }
        Console.Clear();

        Console.WriteLine("Run \"help\" for valid commands");
        while (true)
        {
            string? text = Console.ReadLine();
            if (text != null)
            {
                string[] info = CommandHandler.GetResult(text).ReturnStrings;
                if (!Array.TrueForAll(info, x => string.IsNullOrEmpty(x)))
                {
                    Console.WriteLine(string.Join('\n', info));
                }
            }
        }
        #endregion
    }
}