using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared;
using Discord;
using ENet;

namespace ProxChatClientGUI
{
    internal class Model
    {
        private object modelLock = new object();
        private ConcurrentQueue<Action> messageQueue = new ConcurrentQueue<Action>();

        private Logger modelLogger = new Logger("VoiceProxService");

        private Discord.Discord discord = null!;
        private LobbyManager lobbyManager = null!;
        private VoiceManager voiceManager = null!;
        private ImageManager imageManager = null!;
        private UserManager userManager = null!;
        private User? currentUser = null;
        private Lobby? lob = null;
        private PVCLobbyPacket lobPack = null!;

        private Dictionary<string, long> nameToId = new Dictionary<string, long>();
        private Dictionary<long, User> idToUser = new Dictionary<long, User>();
        private Dictionary<long, float> idToVolPercent = new Dictionary<long, float>(); //what % of user pref vol should users be set to with SetLocalVol
        private Dictionary<string, PVCMultiDataPacket.VolTick> nameToVolCache = new Dictionary<string, PVCMultiDataPacket.VolTick>();
        //private ulong singleTicker = 0; 
        //private string lastSingleUsername = null!;
        //private ulong multiTicker = 0;
        private Dictionary<long, ulong> idToTicker = new Dictionary<long, ulong>();

        private string ingameUsername;

        private Host? client = null;
        private Peer? remotePeer = null;
        private bool requestDisconnect = false;

        private event Action<long>? onUserConnect;
        private event Action<long>? onUserDisconnect;
        private event Action<long, uint, uint, byte[]>? onImageRecieved;
        private event Action? onServerConnect;
        private event Action? onServerDisconnect;

        public Model(string igUsername)
        {
            ingameUsername = igUsername;
            Task.Run(() =>
            {
                #region UI Event Subscription
                onUserConnect += id =>
                {
                    ProxChat.Instance.AddMessage(() =>
                    {
                        //add that users UI element.
                        string username = idToUser[id].Username + "#" + idToUser[id].Discriminator;
                        ProxChat.Instance.AddMemberToList(id, username, id == currentUser!.Value.Id);
                        //bind buttons
                    });
                };

                onUserDisconnect += id =>
                {
                    ProxChat.Instance.AddMessage(() =>
                    {
                        //unbind buttons & remove that users UI element.
                        ProxChat.Instance.RemoveMemberFromList(id);
                    });
                };

                onImageRecieved += (id, width, height, data) =>
                {
                    byte[] highlighted = new byte[data.Length];
                    ProxChat.Instance.AddMessage(() =>
                    {
                        //rearrange pixel data
                        for (int x = 0; x < width; x++)
                        {
                            for (int y = 0; y < height; y++)
                            {
                                int i = (int)(x + (y * width)) * 4;
                                byte r = data[i];
                                byte g = data[i + 1];
                                byte b = data[i + 2];
                                byte a = data[i + 3];

                                float xx = (((float)x / width) - 0.5f) * 2f;
                                float yy = (((float)y / height) - 0.5f) * 2f;
                                float distSq = (xx * xx) + (yy * yy);
                                bool opaque = distSq < 1f;
                                bool greenBorder = distSq < 1f && distSq > (0.925f * 0.925f);

                                if (opaque)
                                {
                                    data[i] = b;
                                    data[i + 1] = g;
                                    data[i + 2] = r;
                                    data[i + 3] = a;
                                    if (!greenBorder)
                                    {
                                        highlighted[i] = b;
                                        highlighted[i + 1] = g;
                                        highlighted[i + 2] = r;
                                        highlighted[i + 3] = a;
                                    }
                                    else
                                    {
                                        highlighted[i] = 64;
                                        highlighted[i + 1] = 255;
                                        highlighted[i + 2] = 64;
                                        highlighted[i + 3] = 255;
                                    }
                                }
                                else
                                {
                                    data[i + 3] = (byte)0;
                                }
                            }
                        }
                        //set the image
                        ProxChat.Instance.SetUserImage(id, width, height, data, highlighted);
                    });
                };

                onServerConnect += () =>
                { 
                    ProxChat.Instance.AddMessage(() =>
                    {
                        ProxChat.Instance.SetCDCButtonEnabled(true);
                        ProxChat.Instance.SetCDCButton(false);
                        ProxChat.Instance.SetConnectionStatus(true);
                    });
                };

                onServerDisconnect += () =>
                {
                    idToTicker.Clear();
                    List<long> ids = new List<long>(idToUser.Keys); //closure capture this instead of the real dict
                    ProxChat.Instance.AddMessage(() =>
                    {
                        foreach (long userId in ids)
                        {
                            if (userId != currentUser!.Value.Id)
                            {
                                ProxChat.Instance.RemoveMemberFromList(userId);
                            }
                        }
                        AddMessage(() =>
                        {
                            idToUser.Clear();
                            nameToId.Clear();
                        });
                        ProxChat.Instance.SetCDCButton(true);
                        ProxChat.Instance.SetCDCButtonEnabled(true);
                        ProxChat.Instance.SetConnectionStatus(false);
                    });
                    client = null;
                    remotePeer = null;
                    AddMessage(() =>
                    {
                        DisconnectLobby();
                    });
                };
#endregion

                #region Setup
                Library.Initialize();
                discord = new Discord.Discord(Constants.clientId, (UInt64)Discord.CreateFlags.Default);
                lobbyManager = discord.GetLobbyManager();
                voiceManager = discord.GetVoiceManager();
                imageManager = discord.GetImageManager();
                userManager = discord.GetUserManager();
                discord.SetLogHook(LogLevel.Debug, (level, message) =>
                {
                    modelLogger.Info($"DiscordLog[{level}] {message}");
                });
                UserManager.CurrentUserUpdateHandler upd = () =>
                {
                    currentUser = userManager.GetCurrentUser();
                    idToUser[currentUser.Value.Id] = currentUser.Value;
                    nameToId[currentUser.Value.Username + "#" + currentUser.Value.Discriminator] = currentUser.Value.Id;
                    onUserConnect?.Invoke(currentUser.Value.Id);
                    FetchImage(currentUser.Value.Id);
                };
                userManager.OnCurrentUserUpdate += upd;
                while (currentUser == null) //add timeout for image data
                {
                    discord.RunCallbacks();
                }
                userManager.OnCurrentUserUpdate -= upd; //if the user changes nick in the middle of a game it will mess things up.
#endregion

                #region Self Event Subscription
                lobbyManager.OnMemberConnect += (long lobbyId, long userId) =>
                {
                    idToVolPercent[userId] = 0f;
                    voiceManager.SetLocalVolume(userId, 0);
                    userManager.GetUser(userId, (Result res, ref User user) =>
                    {
                        if (res != Result.Ok)
                        {
                            modelLogger.Error("GetUser failed in OnMemberConnect, connected user is stuck muted.");
                            return;
                        }
                        idToUser[userId] = user;
                        string userName = user.Username + "#" + user.Discriminator;
                        nameToId[userName] = user.Id;
                        if (nameToVolCache.ContainsKey(userName))
                        {
                            float percentVol = nameToVolCache[userName].Volume ?? 1f;
                            long userId = nameToId[userName];
                            ProxChat.Instance.AddMessage(() =>
                            {
                                byte finalVol = (byte)(percentVol * Settings.Instance.VolumePrefs![userName]);
                                AddMessage(() =>
                                {
                                    idToVolPercent[userId] = percentVol;
                                    voiceManager.SetLocalVolume(userId, finalVol);
                                });
                                ProxChat.Instance.SetPercievedVolume(userId, percentVol);
                            });
                        }
                        voiceManager.SetLocalMute(userId, false);
                        onUserConnect?.Invoke(userId);
                        ProxChat.Instance.AddMessage(() =>
                        {
                            byte vol = Settings.Instance.VolumePrefs![userName];
                            AddMessage(() =>
                            {
                                idToVolPercent[userId] = 1f;
                                modelLogger.Info($"{userName} joined the lobby and volume was set to {vol}.");
                                voiceManager.SetLocalVolume(userId, vol);
                            });
                            ProxChat.Instance.SetPercievedVolume(userId, 1f);
                        });
                        FetchImage(userId);
                    });
                };

                lobbyManager.OnMemberDisconnect += (long lobbyId, long userId) =>
                {
                    //idToUser keynotfoundexception
                    string userName = idToUser[userId].Username + "#" + idToUser[userId].Discriminator;
                    nameToId.Remove(userName);
                    idToUser.Remove(userId);
                    modelLogger.Info(userName + " left the lobby.");
                    onUserDisconnect?.Invoke(userId);
                };

                lobbyManager.OnLobbyDelete += (long lobbyId, uint reason) =>
                {
                    modelLogger.Info("Discord VC lobby closed because: " + reason);
                    onServerDisconnect?.Invoke();
                };

                lobbyManager.OnSpeaking += (long lobbyId, long userId, bool speaking) =>
                {
                    //if (idToUser.ContainsKey(userId))
                    //    modelLogger.Info($"{idToUser[userId].Username}#{idToUser[userId].Discriminator} is {(speaking ? "speaking." : "not speaking.")}");
                    //else
                    //    modelLogger.Info($"{userId} is {(speaking ? "speaking." : "not speaking.")}");
                    ProxChat.Instance.SetUserTalkingHighlighted(userId, speaking);
                };
                #endregion

                #region Loop
                try
                {
                    //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    //sw.Start();
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    const int frameTime = 50; //20fps
                    int waitTime = 20;//how much time can be given to server.Service()
                    while (true)
                    {
                        Event netEvent;
                        if (client != null && client.IsSet) //what does IsSet mean?
                        {
                            int checkResult = client.CheckEvents(out netEvent);
                            if (checkResult == 0)
                            {
                                //no events.
                                client.Service(waitTime, out netEvent);
                            }
                            else if (checkResult < 0)
                            {
                                modelLogger.Warn("CheckEvents failed.");
                            }
                            sw.Restart();
                            if (requestDisconnect)
                            {
                                requestDisconnect = false;
                                try
                                {
                                    remotePeer!.Value.Disconnect(0);
                                }
                                catch { }
                                //client.Flush();
                                //client.Dispose();
                                //onServerDisconnect?.Invoke();
                            }
                            else
                            {
                                switch (netEvent.Type)
                                {
                                    case EventType.None:
                                        break;
                                    case EventType.Connect:
                                        {
                                            remotePeer = netEvent.Peer;
                                            HandleConnectEvent(ref netEvent);
                                        }
                                        break;
                                    case EventType.Disconnect:
                                        {
                                            remotePeer = null;
                                            HandleDisconnectEvent(ref netEvent);
                                        }
                                        break;
                                    case EventType.Receive:
                                        {
                                            remotePeer = netEvent.Peer;
                                            HandleRecieveEvent(ref netEvent);
                                            netEvent.Packet.Dispose();
                                        }
                                        break;
                                    case EventType.Timeout:
                                        {
                                            remotePeer = null;
                                            HandleTimeoutEvent(ref netEvent);
                                        }
                                        break;
                                    default:
                                        modelLogger.Warn("Invalid netevent type");
                                        break;
                                }
                                if (client != null && client.IsSet)
                                    client.Flush();
                            }
                        }
                        else
                        {
                            Thread.Sleep(waitTime); //simulate the waiting done by service
                            sw.Restart();
                        }
                        discord.RunCallbacks(); //TODO: make all callbacks add to the message loop
                        lock (modelLock)
                        {
                            while (messageQueue.Count > 0)
                            {
                                if(messageQueue.TryDequeue(out Action? action))
                                {
                                    //modelLogger.Info(action.Method.Name);
                                    action?.Invoke();
                                }
                            }
                        }
                        sw.Stop();
                        waitTime = frameTime - (int)sw.ElapsedMilliseconds;
                        waitTime = waitTime < 0 ? 0 : waitTime; //if each loop needs to take 50ms, here's how much the next service can take
                    }
                }
                catch (Exception ex)
                {
                    //log
                    modelLogger.Error(ex.ToString());
                }
                finally
                {
                    discord?.Dispose();
                    client?.Dispose();
                }
#endregion
            });
        }

        public void AddMessage(Action action)
        {
            messageQueue.Enqueue(action);
        }

#region NetEvent Handlers
        void HandleConnectEvent(ref Event netEvent)
        {
            modelLogger.Info("Client successfully connected to server.");
            Peer p = netEvent.Peer;
            AddMessage(() =>
            {
                SendPacket(new PVCClientHandshakePacket()
                {
                    DiscordUsername = currentUser!.Value.Username + "#" + currentUser!.Value.Discriminator,
                    IngameUsername = ingameUsername
                }, p);
            });
        }

        void HandleRecieveEvent(ref Event netEvent)
        {
            PVCPacket? packet = Protocol.Deserialize<PVCPacket>(netEvent.Packet.Data, netEvent.Packet.Length);
            if (packet != null)
            {
                switch (packet)
                {
                    case PVCWalkieTalkiePacket walkiePacket:
                        {
                            Peer p = netEvent.Peer;
                            AddMessage(() =>
                            {
                                //the client should never recieve a walkie packet
                                SendPacket(new PVCErrorPacket() { ErrorMessage = "Why are you sending the client a PVCWalkieTalkiePacket?" }, p);
                            });
                        }
                        break;
                    case PVCMultiDataPacket multiPacket:
                        {
                            //FIX TODO: race condition nameToId might not have the entry yet.
                            AddMessage(() =>
                            {
                                foreach (var kvp in multiPacket.Volumes)
                                {
                                    if (nameToId.ContainsKey(kvp.Key))
                                    {
                                        if (!idToTicker.ContainsKey(nameToId[kvp.Key]))
                                            idToTicker[nameToId[kvp.Key]] = 0;
                                        if (kvp.Value.Ticker > idToTicker[nameToId[kvp.Key]])
                                        {
                                            float percentVol = kvp.Value.Volume ?? 1f;
                                            string username = kvp.Key;
                                            long userId = nameToId[username];
                                            ProxChat.Instance.AddMessage(() =>
                                            {
                                                byte finalVol = (byte)(percentVol * Settings.Instance.VolumePrefs![username]);
                                                AddMessage(() =>
                                                {
                                                    idToVolPercent[userId] = percentVol;
                                                    voiceManager.SetLocalVolume(userId, finalVol);
                                                });
                                                ProxChat.Instance.SetPercievedVolume(userId, percentVol);
                                            });
                                        }
                                        //else the volume data for this user is outdated.
                                    }
                                    else
                                    {
                                        //cache for later
                                        //modelLogger.Warn("Could not set volume from multipacket for user because that user isn't set yet. (Add volume caching feature)");
                                        modelLogger.Info("Had to cache a volume from a multipacket because that user isn't set yet.");
                                        if (!nameToVolCache.ContainsKey(kvp.Key))
                                        {
                                            nameToVolCache[kvp.Key] = kvp.Value;
                                        }
                                        if (nameToVolCache[kvp.Key].Ticker < kvp.Value.Ticker)
                                        {
                                            nameToVolCache[kvp.Key] = kvp.Value;
                                        }
                                    }
                                }
                            });
                        }
                        break;
                    case PVCClientHandshakePacket handshakePacket:
                        {
                            Peer p = netEvent.Peer;
                            AddMessage(() =>
                            {
                                //the client should never recieve a client handshake packet
                                SendPacket(new PVCErrorPacket() { ErrorMessage = "Why are you sending the client a PVCClientHandshakePacket?" }, p);
                            });
                        }
                        break;
                    case PVCLobbyPacket lobbyPacket:
                        {
                            lobPack = lobbyPacket;
                            if (lobPack.Secret != null)
                            {
                                DCConnectToLobby(lobPack.LobbyId, lobPack.Secret!);
                            }
                            else
                            {
                                ProxChat.Instance.AddMessage(() =>
                                {
                                    string? maybeSecret = ProxChat.Instance.PromptForLobbySecret();
                                    AddMessage(() =>
                                    {
                                        if (maybeSecret != null)
                                        {
                                            lobPack!.Secret = maybeSecret;
                                            AddMessage(() =>
                                            {
                                                DCConnectToLobby(lobPack.LobbyId, lobPack.Secret!);
                                            });
                                        }
                                        else
                                        {
                                            if (client != null)
                                            {
                                                requestDisconnect = true;
                                            }
                                        }
                                    });
                                });
                            }                           
                        }
                        break;
                    case PVCErrorPacket errorPacket:
                        {
                            modelLogger.Error($"Error packet from server: \"{errorPacket.ErrorMessage ?? "(No error message present.)"}\"");
                        }
                        break;
                    default:
                        modelLogger.Error("HandleRecieve got a packet with a bad packet type.");
                        break;
                }
            }
            else
            {
                modelLogger.Error($"HandleRecieve got an unparseable packet (contents): {System.Runtime.InteropServices.Marshal.PtrToStringAnsi(netEvent.Packet.Data, netEvent.Packet.Length)}");
            }
        }

        private void FetchImage(long userId)
        {
            ImageHandle imgH = new ImageHandle()
            {
                Id = userId,
                Size = 512,
                Type = ImageType.User
            };
            //currentUser.Value.Avatar //look into this
            imageManager.Fetch(imgH, false, (result, returnedHandle) =>
            {
                if (result != Result.Ok)
                {
                    modelLogger.Warn($"Failed to get the profile picture for the main user");
                    return;
                }
                else
                {
                    try
                    {
                        byte[] data = imageManager.GetData(returnedHandle);
                        ImageDimensions dim = imageManager.GetDimensions(returnedHandle);
                        //idToPic[returnedHandle.Id] = data;
                        onImageRecieved?.Invoke(returnedHandle.Id, dim.Width, dim.Height, data);
                    }
                    catch (Exception ex)
                    {
                        modelLogger.Warn($"Issue deserializing image for the main user. Error: {ex.ToString()}");
                        //return null
                    }
                }
            });
        }

        void HandleTimeoutEvent(ref Event netEvent)
        {
            modelLogger.Warn($"Timeout from server, Code: {(netEvent.Data == 69420 ? "(You've been banned)." : netEvent.Data.ToString())}");
            onServerDisconnect?.Invoke();
        }

        void HandleDisconnectEvent(ref Event netEvent)
        {
            modelLogger.Info($"Disconnect from server, Code: {(netEvent.Data == 69420 ? "(You've been banned)." : netEvent.Data.ToString())}");
            onServerDisconnect?.Invoke();
        }

        void SendPacket<T>(T packet, Peer recipient) where T : PVCPacket
        {
            if (!recipient.IsSet)
            {
                modelLogger.Warn("Attempt to send a packet to a peer whose IsSet == false");
                return;
            }
            byte[] data = Protocol.Serialize(packet);
            Packet pack = default(Packet); //ENet will dispose automatically when sending.
            pack.Create(data, PacketFlags.Reliable);
            bool result = recipient.Send(0, ref pack);
            if (!result)
            {
                modelLogger.Warn($"Sendpacket failed on packet type {packet.PType}.");
            }
        }
#endregion

        public void SendWalkieTalkiePacket(long? directRecipientUserId, bool teamOnly)
        {
            if (remotePeer.HasValue)
            {
                string? username = directRecipientUserId.HasValue ? 
                    idToUser[directRecipientUserId.Value].Username + "#" + idToUser[directRecipientUserId.Value].Discriminator : null;
                AddMessage(() =>
                {
                    SendPacket(new PVCWalkieTalkiePacket()
                    {
                        SpecificDiscordRecipient = username,
                        TeamOnly = teamOnly,
                        DiscordSource = currentUser!.Value.Username + "#" + currentUser!.Value.Discriminator
                    },
                    remotePeer.Value);
                });
            }
            else
            {
                modelLogger.Warn("Attempt to send walkie packet to server, but peer is null.");
            }
        }

        private void DCConnectToLobby(long id, string secret)
        {
            DisconnectLobby(() =>
            {
                AddMessage(() =>
                {
                    modelLogger.Info("Beginning to connect to lobby...");
                    lobbyManager.ConnectLobby(id, secret, (Result res, ref Lobby lobby) =>
                    {
                        if (res != Result.Ok)
                        {
                            modelLogger.Info("Something went wrong when joining the lobby.");
                            //set ui back because it failed
                            AddMessage(() =>
                            {
                                requestDisconnect = true;
                            });
                            ProxChat.Instance.AddMessage(() =>
                            {
                                ProxChat.Instance.SetCDCButtonEnabled(true);
                                ProxChat.Instance.SetCDCButton(true);
                                ProxChat.Instance.SetConnectionStatus(false);
                            });
                            return;
                        }
                        else
                        {
                            modelLogger.Info("Joined the lobby successfully.");
                        }
                        IEnumerable<User> users = lobbyManager.GetMemberUsers(lobby.Id);
                        modelLogger.Info("All users in the lobby:\n" +
                            $"{string.Join(",\n", users.Select(x => $"{x.Id}: {x.Username}#{x.Discriminator}"))}");
                        long currId = currentUser!.Value.Id;
                        foreach (User u in users)
                        {
                            if (u.Id != currentUser!.Value.Id)
                            {
                                AddMessage(() =>
                                {
                                    idToUser[u.Id] = u;
                                    string username = u.Username + "#" + u.Discriminator;
                                    nameToId[username] = u.Id;
                                    voiceManager.SetLocalMute(u.Id, false);
                                    onUserConnect?.Invoke(u.Id);
                                    ProxChat.Instance.AddMessage(() =>
                                    {
                                        byte vol = Settings.Instance.VolumePrefs![username];
                                        AddMessage(() =>
                                        {
                                            idToVolPercent[u.Id] = 1f;
                                            voiceManager.SetLocalVolume(u.Id, vol);
                                            modelLogger.Info($"Set {u.Username}#{u.Discriminator}'s volume to {vol}");
                                        });
                                        ProxChat.Instance.SetPercievedVolume(u.Id, 1f);
                                    });
                                    FetchImage(u.Id);
                                });
                            }
                        }

                        lobbyManager.ConnectVoice(lobby.Id, x =>
                        {
                            if (res != Result.Ok)
                            {
                                modelLogger.Info("Something went wrong when joining vc.");
                            }
                            else
                            {
                                modelLogger.Info("Joined vc.");
                            }
                        });
                        lobbyManager.ConnectNetwork(lobby.Id);
                        lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
                        lob = lobby;
                        AddMessage(() =>
                        {
                            onServerConnect?.Invoke();
                        });
                    });
                });
            });
        }

        private void DisconnectLobby(Action? callback = null)
        {
            if (lob != null)
            {
                //disconnect from prev lobby.
                modelLogger.Info("Leaving the current lobby...");
                lobbyManager.DisconnectLobby(lob.Value.Id, res =>
                {
                    if (res == Result.Ok || res == Result.NotFound)
                    {
                        modelLogger.Info("You left the lobby");
                        idToUser.Clear();
                        nameToId.Clear();
                    }
                    else
                    {
                        modelLogger.Info($"Something went wrong with leaving the lobby: {res.ToString()}");
                    }
                    lob = null;
                    callback?.Invoke();
                });
            }
            else
            {
                callback?.Invoke();
            }
        }

        public void ConnectToServer(string host, string port)
        {
            try
            {
                if (client != null)
                {
                    requestDisconnect = true;
                }
                else
                {
                    client = new Host();
                    client.Create();
                    Address adr = new Address() { Port = ushort.Parse(port) };
                    adr.SetHost(host);
                    client.Connect(adr);
                }
            }
            catch (Exception e)
            {
                modelLogger.Warn(e.ToString());
            }
        }

        public bool IsConnectedToServer()
        {
            return client != null;
        }

        public void RecalculateRealVolume(string username, byte newVolume)
        {
            try
            {
                int newVol = (int)(idToVolPercent[nameToId[username]] * (int)newVolume);
                newVol = (newVol < 0 ? 0 : (newVol > 200 ? 200 : newVol));
                modelLogger.Info("Recalc'd a users volume to: " + newVol);
                voiceManager.SetLocalVolume(nameToId[username], (byte)newVol);
            }
            catch (Exception ex)
            {
                modelLogger.Warn("Attempt to recalculate the volume of a user failed: " + ex.ToString());
            }
        }

        public void SetMute(long userId, bool isMuted)
        {
            if (userId == currentUser!.Value.Id)
            {
                voiceManager.SetSelfMute(isMuted);
            }
            else
            {
                voiceManager.SetLocalMute(userId, isMuted);
            }
        }

        public void SetDeaf(bool isDeaf)
        {
            voiceManager.SetSelfDeaf(isDeaf);
        }
    }
}
