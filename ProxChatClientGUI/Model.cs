using System;
using System.Collections.Generic;
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
        //public class PublicModelInfo
        //{
        //    public static PublicModelInfo Instance { get; private set; }
        //    static PublicModelInfo()
        //    {
        //        Instance = new PublicModelInfo();
        //    }
        //    private PublicModelInfo() { }

        //    //all data that the view can access should be put in here.
        //}
        private object modelLock = new object();
        private Queue<Action> messageQueue = new Queue<Action>();

        private Logger modelLogger = new Logger("VoiceProxService");

        private Discord.Discord discord = null!;
        private LobbyManager lobbyManager = null!;
        private VoiceManager voiceManager = null!;
        private ImageManager imageManager = null!;
        private UserManager userManager = null!;
        private User? currentUser = null;
        private Lobby? lob = null;

        private Dictionary<string, long> nameToId = new Dictionary<string, long>();
        private Dictionary<long, User> idToUser = new Dictionary<long, User>();
        private Dictionary<long, float> idToVolPercent = new Dictionary<long, float>(); //what % of user pref vol should users be set to with SetLocalVol

        private ulong singleTicker = 0;
        private string lastSingleUsername = null!;
        private ulong multiTicker = 0;

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
                        //unbind buttons
                        //remove that users UI element.
                        ProxChat.Instance.RemoveMemberFromList(id);
                    });
                };

                onImageRecieved += (id, width, height, data) =>
                {
                    ProxChat.Instance.AddMessage(() =>
                    {
                        //rearrange pixel data
                        for (int i = 0; i < data.Length; i += 4)
                        {
                            byte r = data[i];
                            byte g = data[i + 1];
                            byte b = data[i + 2];
                            byte a = data[i + 3];

                            data[i] = b;
                            data[i + 1] = g;
                            data[i + 2] = r;
                            data[i + 3] = a;
                        }
                        //set the image
                        ProxChat.Instance.SetUserImage(id, width, height, data);
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
                    //TODO: CLEAR OUT OLD DATA (Make sure this is correct)
                    singleTicker = 0;
                    multiTicker = 0;
                    lastSingleUsername = null!;
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
                        ProxChat.Instance.SetCDCButton(true);
                        ProxChat.Instance.SetCDCButtonEnabled(true);
                        ProxChat.Instance.SetConnectionStatus(false);
                    });
                    //idToPic.Clear();
                    idToUser.Clear();
                    nameToId.Clear();
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
                        onUserConnect?.Invoke(userId);
                        ProxChat.Instance.AddMessage(() =>
                        {
                            byte vol = Settings.Instance.VolumePrefs![userName];
                            AddMessage(() =>
                            {
                                idToVolPercent[userId] = 1f;
                                voiceManager.SetLocalVolume(userId, vol);
                            });
                            ProxChat.Instance.SetPercievedVolume(userId, 1f);
                        });
                        FetchImage(userId);
                    });
                };

                lobbyManager.OnMemberDisconnect += (long lobbyId, long userId) =>
                {
                    string userName = idToUser[userId].Username + "#" + idToUser[userId].Discriminator;
                    nameToId.Remove(userName);
                    idToUser.Remove(userId);
                    onUserDisconnect?.Invoke(userId);
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
                        discord.RunCallbacks();
                        lock (modelLock)
                        {
                            while (messageQueue.Count > 0)
                            {
                                var action = messageQueue.Dequeue();
                                modelLogger.Info(action.Method.Name);
                                if (action.Method.Name.Contains("<.ctor>b__20") || action.Method.Name.Contains("<.ctor>b__19"))
                                {
                                    modelLogger.Info($"This might be the ExecutionEngineException cause: " + action.Method.GetMethodBody()!.ToString() ?? "");
                                }
                                action();
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
            lock (modelLock)
            {
                messageQueue.Enqueue(action);
            }
        }

        #region NetEvent Handlers
        void HandleConnectEvent(ref Event netEvent)
        {
            modelLogger.Info("Client successfully connected to server.");
            SendPacket(new PVCClientHandshakePacket()
            {
                DiscordUsername = currentUser!.Value.Username + "#" + currentUser!.Value.Discriminator,
                IngameUsername = ingameUsername
            }, netEvent.Peer);
            onServerConnect?.Invoke();
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
                                        string username = pair.Key;
                                        long userId = nameToId[username];
                                        ProxChat.Instance.AddMessage(() =>
                                        {
                                            float fVol = vol.HasValue ? (vol.Value / 100f) : 1f;
                                            byte finalVol = (byte)(fVol * Settings.Instance.VolumePrefs![username]);
                                            AddMessage(() =>
                                            {
                                                idToVolPercent[userId] = fVol;
                                                voiceManager.SetLocalVolume(userId, finalVol);
                                            });
                                            ProxChat.Instance.SetPercievedVolume(userId, fVol);
                                        });

                                        #region Old
                                        //if (vol == null)
                                        //{
                                        //    //default volume for this user.
                                        //    ProxChat.Instance.AddMessage(() =>
                                        //    {
                                        //        byte vol = Settings.Instance.VolumePrefs![pair.Key];
                                        //        AddMessage(() =>
                                        //        {
                                        //            voiceManager.SetLocalVolume(nameToId[pair.Key], vol);
                                        //        });
                                        //        ProxChat.Instance.PercievedVolumeChange(nameToId[pair.Key], 1f);
                                        //    });
                                        //}
                                        //else
                                        //{
                                        //    float fvol = (vol.Value / 100f);
                                        //    string username = pair.Key;
                                        //    ProxChat.Instance.AddMessage(() =>
                                        //    {
                                        //        byte finalVolume = (byte)(fvol * Settings.Instance.VolumePrefs![username]);
                                        //        AddMessage(() =>
                                        //        {
                                        //            voiceManager.SetLocalVolume(nameToId[username], finalVolume);
                                        //        });
                                        //        ProxChat.Instance.PercievedVolumeChange(nameToId[username], fvol);
                                        //    });
                                        //}
                                        #endregion
                                    }
                                    multiTicker = multiPacket.Tick;
                                } //else the multipacket is too old, no point in applying it
                            });
                        }
                        break;
                    case PVCSingleDataPacket singlePacket:
                        {
                            //FIX TODO: race condition nameToId might not have the entry yet.
                            if (singlePacket.Tick > singleTicker) //only accept if newer
                            {
                                if (singlePacket.MultiTick >= multiTicker) //only overwrite if newer than last multi we received.
                                {
                                    lastSingleUsername = singlePacket.DiscordUsername;
                                    singleTicker = singlePacket.Tick;
                                    byte? vol = singlePacket.Volume;
                                    string username = singlePacket.DiscordUsername;
                                    long userId = nameToId[username];
                                    ProxChat.Instance.AddMessage(() =>
                                    {
                                        float fVol = vol.HasValue ? (vol.Value / 100f) : 1f;
                                        byte finalVol = (byte)(fVol * Settings.Instance.VolumePrefs![username]);
                                        AddMessage(() =>
                                        {
                                            idToVolPercent[userId] = fVol;
                                            voiceManager.SetLocalVolume(userId, finalVol);
                                        });
                                        ProxChat.Instance.SetPercievedVolume(userId, fVol);
                                    });

                                    #region Old
                                    //if (vol == null)
                                    //{
                                    //    //default volume for this user.
                                    //    ProxChat.Instance.AddMessage(() =>
                                    //    {
                                    //        byte vol = Settings.Instance.VolumePrefs![singlePacket.DiscordUsername];
                                    //        AddMessage(() =>
                                    //        {
                                    //            voiceManager.SetLocalVolume(nameToId[singlePacket.DiscordUsername], vol);
                                    //        });
                                    //        ProxChat.Instance.PercievedVolumeChange(nameToId[singlePacket.DiscordUsername], 1f);
                                    //    });
                                    //}
                                    //else
                                    //{
                                    //    float fvol = (vol.Value / 100f);
                                    //    ProxChat.Instance.AddMessage(() =>
                                    //    {
                                    //        byte finalVolume = (byte)(fvol * Settings.Instance.VolumePrefs![singlePacket.DiscordUsername]);
                                    //        AddMessage(() =>
                                    //        {
                                    //            voiceManager.SetLocalVolume(nameToId[singlePacket.DiscordUsername], finalVolume);
                                    //        });
                                    //        ProxChat.Instance.PercievedVolumeChange(nameToId[singlePacket.DiscordUsername], fvol);
                                    //    });
                                    //}
                                    #endregion
                                }
                            }
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
                            long id = lobbyPacket.LobbyId;
                            string secret = lobbyPacket.Secret;
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
                                            return;
                                        }
                                        else
                                        {
                                            modelLogger.Info("Joined the lobby successfully.");
                                        }
                                        IEnumerable<User> users = lobbyManager.GetMemberUsers(lobby.Id);
                                        modelLogger.Info("All users in the lobby:\n" +
                                            $"{string.Join(",\n", users.Select(x => $"{x.Id}: {x.Username}#{x.Discriminator}"))}\n");
                                        foreach (User u in users)
                                        {
                                            if (u.Id != currentUser!.Value.Id)
                                            {
                                                idToUser[u.Id] = u;
                                                string username = u.Username + "#" + u.Discriminator;
                                                nameToId[username] = u.Id;
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
                                    });
                                });
                            });
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
