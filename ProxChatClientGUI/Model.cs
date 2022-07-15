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
        private Dictionary<long, byte[]> idToPic = new Dictionary<long, byte[]>();
        
        private ulong singleTicker = 0;
        private string lastSingleUsername = null!;
        private ulong multiTicker = 0;

        private Host? client = null;

        private event Action<long>? onUserConnect;
        private event Action<long>? onUserDisconnect;

        public Model()
        {
            Task.Run(() =>
            {
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
                    //userCache[currentUser.Value.Id] = currentUser.Value;
                    //nameToIdCache[currentUser.Value.Username + "#" + currentUser.Value.Discriminator] = currentUser.Value.Id;
                };
                userManager.OnCurrentUserUpdate += upd;
                while (currentUser == null)
                {
                    discord.RunCallbacks();
                }
                userManager.OnCurrentUserUpdate -= upd; //if the user changes nick in the middle of a game it will mess things up.
                #endregion

                #region Self Event Subscription
                lobbyManager.OnMemberConnect += (long lobbyId, long userId) =>
                {
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
                        byte vol = Settings.Instance.GetUserVolumePreference(userName);
                        voiceManager.SetLocalVolume(userId, Settings.Instance.GetUserVolumePreference(userName));

                        ImageHandle imgH = new ImageHandle()
                        {
                            Id = userId,
                            Size = 512
                        };
                        //currentUser.Value.Avatar //look into this
                        imageManager.Fetch(imgH, false, (result, returnedHandle) =>
                        {
                            if (result != Result.Ok)
                            {
                                modelLogger.Warn($"Failed to get the profile picture for user: {userId}");
                                return;
                            }
                            else
                            {
                                try
                                {
                                    byte[] data = imageManager.GetData(returnedHandle);
                                    idToPic[returnedHandle.Id] = data;
                                    //using (MemoryStream ms = new MemoryStream(data))
                                    //{
                                    //    //Image img = Image.FromStream(ms);
                                    //}
                                }
                                catch (Exception ex)
                                {
                                    modelLogger.Warn($"Issue deserializing image for user: {userId}. Error: {ex.ToString()}");
                                    //return null
                                }
                            }
                        });
                    });
                };

                lobbyManager.OnMemberDisconnect += (long lobbyId, long userId) =>
                {
                    string userName = idToUser[userId].Username + "#" + idToUser[userId].Discriminator;
                    nameToId.Remove(userName);
                    idToUser.Remove(userId);
                    idToPic.Remove(userId);
                };
                #endregion

                //#region UI Event Subscription
                //onUserConnect += id =>
                //{
                //    ProxChat.Instance.AddMessage(() =>
                //    {
                //        //
                //    });
                //};
                //#endregion

                #region Loop
                try
                {
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    sw.Start();
                    while (true)
                    {
                        lock (modelLock)
                        {
                            while (messageQueue.Count > 0)
                            {
                                messageQueue.Dequeue()();
                            }
                        }

                        Event netEvent;
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
                                modelLogger.Warn("CheckEvents failed.");
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
                                    modelLogger.Warn("Invalid netevent type");
                                    break;
                            }
                            client.Flush();
                        }

                        discord.RunCallbacks();

                        while (sw.ElapsedMilliseconds < 16) { } //this busy wait allows some time for people to add more messages.
                        sw.Restart();
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
                IngameUsername = Settings.Instance.GetIGName()!
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
                            //FIX TODO: race condition nameToId might not have the entry yet.
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
                                        voiceManager.SetLocalVolume(nameToId[pair.Key], Settings.Instance.GetUserVolumePreference(pair.Key));
                                    }
                                    else
                                    {
                                        float fvol = (vol.Value / 100f);
                                        byte finalVolume = (byte)(fvol * Settings.Instance.GetUserVolumePreference(pair.Key));
                                        voiceManager.SetLocalVolume(nameToId[pair.Key], finalVolume);
                                    }
                                }
                                multiTicker = multiPacket.Tick;
                            }
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
                                    if (vol == null)
                                    {
                                        //default volume for this user.
                                        voiceManager.SetLocalVolume(nameToId[singlePacket.DiscordUsername], Settings.Instance.GetUserVolumePreference(singlePacket.DiscordUsername));
                                    }
                                    else
                                    {
                                        float fvol = (vol.Value / 100f);
                                        byte finalVolume = (byte)(fvol * Settings.Instance.GetUserVolumePreference(singlePacket.DiscordUsername));
                                        voiceManager.SetLocalVolume(nameToId[singlePacket.DiscordUsername], finalVolume);
                                    }
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
                            AddMessage(() =>
                            {
                                if (lob != null)
                                {
                                    //disconnect from prev lobby.
                                    StringBuilder resultMessage = new StringBuilder();
                                    resultMessage.Append("You are already in a lobby! leaving the current lobby...\n");
                                    lobbyManager.DisconnectLobby(lob.Value.Id, res =>
                                    {
                                        if (res != Result.Ok)
                                        {
                                            resultMessage.Append($"Something went wrong with leaving the lobby: {res.ToString()}");
                                        }
                                        else
                                        {
                                            resultMessage.Append("You left the lobby");
                                            idToUser.Clear();
                                            nameToId.Clear();
                                        }
                                        lob = null;
                                        modelLogger.Info(resultMessage.ToString());
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
                                        modelLogger.Info("Something went wrong when joining the lobby.");
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
                                        if (u.Id != currentUser!.Value.Id)
                                        {
                                            idToUser[u.Id] = u;
                                            string username = u.Username + "#" + u.Discriminator;
                                            nameToId[username] = u.Id;
                                            voiceManager.SetLocalVolume(u.Id, Settings.Instance.GetUserVolumePreference(username));
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
                                        modelLogger.Info(resultMessage.ToString());
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

        void HandleTimeoutEvent(ref Event netEvent)
        {
            HandleDisconnectEvent(ref netEvent);
        }

        void HandleDisconnectEvent(ref Event netEvent)
        {
            modelLogger.Error($"Server disconnected or timed out. Code: {(netEvent.Data == 69420 ? "(You've been banned)." : netEvent.Data.ToString())}");
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

        public void GetUserImage(long userId)
        {
            ImageHandle imgH = new ImageHandle()
            {
                Id = userId,
                Size = 512
            };
            //currentUser.Value.Avatar //look into this
            imageManager.Fetch(imgH, false, (result, returnedHandle) =>
            {
                if (result != Result.Ok)
                {
                    modelLogger.Warn($"Failed to get the profile picture for user: {userId}");
                    return;
                }
                else
                {
                    try
                    {
                        byte[] data = imageManager.GetData(returnedHandle);
                        using (MemoryStream ms = new MemoryStream(data))
                        {
                            //Image img = Image.FromStream(ms);
                        }
                    }
                    catch (Exception ex)
                    {
                        modelLogger.Warn($"Issue deserializing image for user: {userId}. Error: {ex.ToString()}");
                        //return null
                    }
                }
            });
        }

        public void ConnectToServer(string host, string port)
        {
            try
            {
                client?.Dispose();
                client = new Host();
                client.Create();
                Address adr = new Address() { Port = ushort.Parse(port) };
                adr.SetHost(host);
                client.Connect(adr);
            }
            catch (Exception e)
            {
                modelLogger.Warn(e.ToString());
            }
        }
    }
}
