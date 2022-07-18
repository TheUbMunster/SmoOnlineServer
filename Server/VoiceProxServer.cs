using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Numerics;
using System.Runtime.InteropServices;
using ENet;
using Shared;

namespace Server
{
    class VoiceProxServer
    {
        public static VoiceProxServer Instance { get; private set; }

        static VoiceProxServer()
        {
            if (Instance == null)
            {
                Instance = new VoiceProxServer();
            }
        }

        private Host server;
        private Logger pvcLogger = new Logger("VoiceProxServer");
        private Dictionary<string, Peer> discordToPeer = new Dictionary<string, Peer>();
        private string? ip = null;

        private object messageKey = new object();
        private Queue<Action> messageQueue = new Queue<Action>();

        private List<Peer> pendingClients = new List<Peer>();

        public event Action<string, string>? OnClientConnect; //discord, ingame
        public event Action<string>? OnClientDisconnect; //discord

        private bool sendVolData = false;
        private Dictionary<string, string> igToDiscord = new Dictionary<string, string>(); //dont clear this ever
        private Dictionary<string, Vector3> igToPos = new Dictionary<string, Vector3>();
        private Dictionary<string, string?> igToStage = new Dictionary<string, string?>();
        private Dictionary<string, Dictionary<string, float?>> igToIgsToDirtyVols = new Dictionary<string, Dictionary<string, float?>>(); //null = dont change
        private Dictionary<string, Dictionary<string, float?>> igToIgsToLastSetVols = new Dictionary<string, Dictionary<string, float?>>();
        private Dictionary<string, Dictionary<string, ulong>> igToIgsToTickers = new Dictionary<string, Dictionary<string, ulong>>();

        public void SetProxChatEnabled(bool enabled)
        {
            sendVolData = enabled;
            if (!sendVolData)
            {
                //send nulls
                SendNullVolInfo();
            }
        }

        private void SendNullVolInfo()
        {
            foreach (var perspective in igToIgsToDirtyVols)
            {
                Dictionary<string, (float? volume, ulong ticker)> dict = new Dictionary<string, (float? volume, ulong ticker)>();
                foreach (var kvp in igToDiscord)
                {
                    if (kvp.Value != perspective.Key)
                        dict.Add(kvp.Value, (null, ++igToIgsToTickers[perspective.Key][kvp.Key]));
                }
                var packet = new PVCMultiDataPacket()
                {
                    Volumes = dict
                };
                string recip = perspective.Key;
                AddMessage(() =>
                {
                    SendPacket(packet, recip);
                });
            }
        }

        private void SendCachedVolInfo()
        {
            foreach (var perspective in igToIgsToDirtyVols)
            {
                var data = igToIgsToDirtyVols[perspective.Key].Where(x => x.Value != null);
                if (data.Any())
                {
                    Dictionary<string, (float? vol, ulong ticker)> vols = new Dictionary<string, (float?, ulong)>();
                    foreach (var elem in data)
                    {
                        if (igToDiscord.ContainsKey(elem.Key))
                        {
                            vols.Add(igToDiscord[elem.Key], (elem.Value, igToIgsToTickers[perspective.Key][elem.Key]));
                        }
                    }
                    var packet = new PVCMultiDataPacket() 
                    {
                        Volumes = vols
                    };
                    string recip = perspective.Key;
                    AddMessage(() =>
                    {
                        SendPacket(packet, recip);
                    });
                    igToIgsToDirtyVols[perspective.Key].Clear();
                }
            }
        }

        public void OnPlayerUpdate(string igPlayer, Vector3 pos, string? stage)
        {
            AddMessage(() =>
            {
                const float beginHearingThreshold = 3500f;
                const float fullHearingThreshold = 750f;
                const float soundEpsilon = 0.01f; //what percent volume change results in an update in the client's volumes.
                float ClampedInvLerp(float a, float b, float v)
                {
                    return v < a ? 0 : (v > b ? 1 : (v - a) / (b - a)); //see "linear interpolation"
                }

                igToStage[igPlayer] = stage;
                igToPos[igPlayer] = pos;
                if (!igToIgsToDirtyVols.ContainsKey(igPlayer))
                {
                    igToIgsToDirtyVols[igPlayer] = new Dictionary<string, float?>();
                }
                if (!igToIgsToLastSetVols.ContainsKey(igPlayer))
                {
                    igToIgsToLastSetVols[igPlayer] = new Dictionary<string, float?>();
                }
                if (!igToIgsToTickers.ContainsKey(igPlayer))
                {
                    igToIgsToTickers[igPlayer] = new Dictionary<string, ulong>();
                }
                foreach (var kvp in igToPos)
                {
                    float dist = Vector3.Distance(kvp.Value, igToPos[igPlayer]);
                    float setVol;
                    if ((igToStage[igPlayer] ?? "dontmake") != (igToStage[kvp.Key] ?? "theseequal"))
                    {
                        //if both were null then != would fail, but the ??'s with the nonequal strings makes sure that they don't
                        //stages aren't the same *or* both stages are null
                        setVol = 0f;
                    }
                    else if (dist > beginHearingThreshold)
                    {
                        //too quiet (0%)
                        setVol = 0f;
                    }
                    else if (dist < fullHearingThreshold)
                    {
                        //full vol (100%)
                        setVol = 1f;
                    }
                    else
                    {
                        setVol = 1f - ClampedInvLerp(fullHearingThreshold, beginHearingThreshold, dist);
                        //semi-linearize from 1/((dist^2)*log(dist))
                        setVol *= setVol; //may sound better without this.
                    }
                    //within epsilon instead exact ==
                    //if epsilon, don't set setVol or else it can slowly drift away from real vol by being within delta within +/- direction long enough
                    float oldVol = igToIgsToLastSetVols[kvp.Key][igPlayer] ?? -100000f; //if was never set, must set
                    if (Math.Abs(oldVol - setVol) > soundEpsilon)
                    {
                        //must change
                        igToIgsToTickers[kvp.Key][igPlayer]++;
                        igToIgsToTickers[igPlayer][kvp.Key]++;
                        igToIgsToDirtyVols[kvp.Key][igPlayer] = setVol;
                        igToIgsToDirtyVols[igPlayer][kvp.Key] = setVol;
                        igToIgsToLastSetVols[kvp.Key][igPlayer] = setVol;
                        igToIgsToLastSetVols[igPlayer][kvp.Key] = setVol;
                    }
                    //else //no need to change
                }
            });
        }

        private VoiceProxServer()
        {
            Library.Initialize();
            server = new Host();
            Address adr = new Address() { Port = Settings.Instance.Discord.PVCPort };
            IPHostEntry entry = Dns.GetHostEntry(adr.GetHost());
            if (entry.AddressList.Length > 0)
            {
                for (int i = 0; i < entry.AddressList.Length; i++)
                {
                    if (entry.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                    {
                        ip = entry.AddressList[i].ToString();
                        break;
                    }
                    else if (entry.AddressList[i].IsIPv4MappedToIPv6)
                    {
                        ip = entry.AddressList[i].MapToIPv4().ToString();
                        break;
                    }
                }
                if (ip == null)
                {
                    pvcLogger.Error("DNS could not resolve this programs IP.");
                }
            }
            else
            {
                pvcLogger.Error("DNS could not resolve this programs IP.");
            }
            server.Create(adr, 16); //discord voice cannot support more than 16 people per lobby.
            OnClientConnect += (string discord, string ingame) =>
            {
                int before = igToDiscord.Count;
                igToDiscord[discord] = ingame;
                if (igToDiscord.Count == 1 && before == 0)
                {
                    igToPos.Clear();
                    igToStage.Clear();
                    igToIgsToDirtyVols.Clear();
                    igToIgsToLastSetVols.Clear();
                    igToIgsToTickers.Clear();
                    DiscordBot.Instance.CloseThenOpenPVCLobby().ContinueWith((task) =>
                    {
                        AddMessage(() =>
                        {
                            SendLobbyPacketsToPending();
                        });
                    });
                }
            };
            OnClientDisconnect += (string discord) =>
            {
                igToDiscord.Remove(discord);
            };
            Task.Run(Loop);
        }

        private void Loop()
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            const int frameTime = 50; //20fps
            int waitTime = 20;//how much time can be given to server.Service()
            while (true)
            {
                Event netEvent;
                int checkResult = server.CheckEvents(out netEvent);
                if (checkResult == 0)
                {
                    //no events.
                    server.Service(waitTime, out netEvent);
                }
                else if (checkResult < 0)
                {
                    pvcLogger.Error("CheckEvents failed.");
                }
                sw.Restart(); //start timing non-service work
                switch (netEvent.Type)
                {
                    case EventType.None:
                        break;
                    case EventType.Connect:
                        //this ip address will probably only be contained in the list if the ip of the pvc client and
                        //game are the same (probably only the case if you're running an emulator and pvc client on
                        //the same pc).
                        if (netEvent.Peer.IsSet && Settings.Instance.BanList.IpAddresses.Contains(netEvent.Peer.IP))
                        {
                            //banned user tried to join
                            netEvent.Peer.DisconnectNow(69420);
                        }
                        else
                        {
                            HandleConnectEvent(ref netEvent);
                        }
                        break;
                    case EventType.Disconnect:
                        //drop the client, remove cached data
                        HandleDisconnectEvent(ref netEvent);
                        break;
                    case EventType.Receive:
                        HandleRecieveEvent(ref netEvent);
                        netEvent.Packet.Dispose();
                        break;
                    case EventType.Timeout:
                        //drop the client without removing cached data
                        HandleTimeoutEvent(ref netEvent);
                        break;
                    default:
                        //what the h-e double hockey sticks
                        pvcLogger.Error("Invalid NetEvent type.");
                        break;
                }
                server.Flush();
                //message loop
                lock (messageKey)
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
                    if (sendVolData)
                    {
                        SendCachedVolInfo();
                    }
                }
                sw.Stop();
                waitTime = frameTime - (int)sw.ElapsedMilliseconds;
                waitTime = waitTime < 0 ? 0 : waitTime; //if each loop needs to take 50ms, here's how much the next service can take
            }
        }

        private void HandleConnectEvent(ref Event netEvent)
        {
            netEvent.Peer.Timeout(32, 1000, 4000); //figure out what these arguments mean
            pvcLogger.Info("Client connected");
        }

        private void HandleDisconnectEvent(ref Event netEvent)
        {
            pvcLogger.Info("A client disconnected");
            Peer p = netEvent.Peer;
            //FIX ME
            //p.ID was 0 in this expression, couldn't remove out of discordToPeer because (client timed
            //out and p.ID was 0 which didn't match any of the peers ids?)
            //Consider FirstOrDefault
            string discord = discordToPeer.First(x => x.Value.ID == p.ID).Key;
            discordToPeer.Remove(discord);
            OnClientDisconnect?.Invoke(discord);
        }

        private void HandleRecieveEvent(ref Event netEvent)
        {
            if (!discordToPeer.Values.Contains(netEvent.Peer))
            {
                PVCPacket? packet = Protocol.Deserialize<PVCPacket>(netEvent.Packet.Data, netEvent.Packet.Length);
                if (packet != null)
                {
                    switch (packet)
                    {
                        case PVCWalkieTalkiePacket walkiePacket:
                            {
                                //the user is enabling team *or* global vc *or* individual vc
                                pvcLogger.Info($"Got walkie packet: recip: {walkiePacket.SpecificRecipient ?? "null"}, teamonly: {walkiePacket.TeamOnly}");
                            }
                            break;
                        case PVCMultiDataPacket multiPacket:
                            {
                                //the server should never recieve a multi data packet
                                Peer p = netEvent.Peer;
                                AddMessage(() =>
                                {
                                    SendPacket(new PVCErrorPacket() { ErrorMessage = "Why are you sending the server a PVCMultiDataPacket?" }, p);
                                });
                            }
                            break;
                        case PVCClientHandshakePacket handshakePacket:
                            {
                                if (handshakePacket.DiscordUsername == null || handshakePacket.IngameUsername == null)
                                {
                                    pvcLogger.Error("HandleRecieve got a bad handshake packet.");
                                }
                                else
                                {
                                    pvcLogger.Info("Successful client handshake");
                                }
                                discordToPeer[handshakePacket.DiscordUsername!] = netEvent.Peer;
                                OnClientConnect?.Invoke(handshakePacket.DiscordUsername!, handshakePacket.IngameUsername!);
                                //send the packet only after the lobby is open.
                                var lobbyInfo = DiscordBot.Instance.GetLobbyInfo();
                                if (lobbyInfo != null)
                                {
                                    Peer p = netEvent.Peer;
                                    AddMessage(() =>
                                    {
                                        SendPacket(new PVCLobbyPacket()
                                        {
                                            LobbyId = lobbyInfo.Value.id,
                                            Secret = Settings.Instance.Discord.AutoSendPVCPassword ? lobbyInfo.Value.secret : null
                                        }, p);
                                    });
                                }
                                else
                                {
                                    //add them to pending.
                                    pendingClients.Add(netEvent.Peer);
                                }
                            }
                            break;
                        case PVCLobbyPacket lobbyPacket:
                            {
                                //the server should never recieve a lobby packet
                                Peer p = netEvent.Peer;
                                AddMessage(() =>
                                {
                                    SendPacket(new PVCErrorPacket() { ErrorMessage = "Why are you sending the server a PVCLobbyPacket?" }, p);
                                });
                            }
                            break;
                        case PVCErrorPacket errorPacket:
                            {
                                pvcLogger.Error($"Error packet from client: \"{errorPacket.ErrorMessage ?? "(No error message present.)"}\"");
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
        }

        private void HandleTimeoutEvent(ref Event netEvent)
        {
            pvcLogger.Warn($"A client timed out");
            HandleDisconnectEvent(ref netEvent);
        }

        public void SendPacket<T>(T packet, string discordUsername) where T : PVCPacket
        {
            if (discordToPeer.ContainsKey(discordUsername))
            {
                SendPacket(packet, discordToPeer[discordUsername]);
            }
            else
            {
                pvcLogger.Error($"Tried to send packet to {discordUsername}, but that user isn't present in the usertable. (No action was taken.)");
            }
        }

        private void SendPacket<T>(T packet, Peer recipient) where T : PVCPacket
        {
            byte[] data = Protocol.Serialize(packet);
            Packet pack = default(Packet); //ENet will dispose automatically when sending.
            pack.Create(data, PacketFlags.Reliable);
            bool result = recipient.Send(0, ref pack);
            if (!result)
            {
                pvcLogger.Error("Sendpacket failed.");
            }
        }

        public string? GetServerIP()
        {
            return ip;
        }

        public void AddMessage(Action message)
        {
            lock (messageKey)
            {
                messageQueue.Enqueue(message);
            }
        }

        public void SendLobbyPacketsToPending()
        {
            var lobbyInfo = DiscordBot.Instance.GetLobbyInfo();
            if (lobbyInfo == null)
                pvcLogger.Error("Lobby was null when trying to connect pending clients.");
            else
            {
                pvcLogger.Info("Sent lobby join info packet to pending clients.");
                AddMessage(() =>
                {
                    foreach (Peer pending in pendingClients)
                    {
                        try
                        {
                            SendPacket(new PVCLobbyPacket() { LobbyId = lobbyInfo.Value.id, Secret = lobbyInfo.Value.secret }, pending);
                        }
                        catch { } //they might not still be trying to connect/might have dcd
                    }
                    pendingClients.Clear();
                });
            }
        }

        ~VoiceProxServer()
        {
            //send "closing server" packet to all clients.
            ip = null;
            server.Flush();
            server.Dispose();
            Library.Deinitialize();
        }

        #region Old
        //private class ClientState
        //{
        //    public TcpClient TcpClient { get; set; }
        //    public StringBuilder buff = new StringBuilder();
        //    public byte[] bbuff = new byte[1024];

        //    public string[] GetMessages(int maxCount = -1)
        //    {
        //        string[] parts = Regex.Split(buff.ToString(), @$"(?<=[{Constants.pvcPacketDelim}])");
        //        List<string> result = new List<string>();
        //        foreach (string s in parts)
        //        {
        //            if (s.Length == 0)
        //            {
        //                continue;
        //            }
        //            if (s[s.Length - 1] != Constants.pvcPacketDelim)
        //            {
        //                break;
        //            }
        //            //if we're here, then "s" is a single, whole message
        //            result.Add(s.Substring(0, s.Length - 1));
        //            buff.Remove(0, s.Length);
        //            if (maxCount != -1 && result.Count >= maxCount)
        //            {
        //                break;
        //            }
        //        }
        //        return result.ToArray();
        //    }
        //}

        //private static VoiceProxServer? instance;
        //public static VoiceProxServer Instance
        //{
        //    get
        //    {
        //        if (instance != null)
        //        {
        //            return instance;
        //        }
        //        else
        //        {
        //            return instance = new VoiceProxServer();
        //        }
        //    }
        //}
        //private static Logger logger = new Logger("VoiceProxServer");
        //private object lockKey = new object();
        //private Dictionary<string, IPAddress> discordToIp = new Dictionary<string, IPAddress>();
        //private Dictionary<IPAddress, ClientState> ipToClient = new Dictionary<IPAddress, ClientState>();
        //private Queue<Action> messageQueue = new Queue<Action>();
        ////private event Action<ClientState>? onMessageRecieved;

        //private VoiceProxServer()
        //{
        //    Task.Run(StartListener);
        //    //write to clients (send)
        //    Task.Run(() =>
        //    {
        //        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        //        sw.Start();
        //        try
        //        {
        //            while (true)
        //            {
        //                lock (lockKey)
        //                {
        //                    while (messageQueue.Count > 0)
        //                    {
        //                        messageQueue.Dequeue()();
        //                    }
        //                }
        //                int msDelay = 50 - (int)sw.ElapsedMilliseconds;
        //                if (msDelay >= 10) //margin of 10ms
        //                    Thread.Sleep(msDelay);
        //                sw.Restart();
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            logger.Error(ex.ToString());
        //        }
        //    });
        //    //read from clients (recieve)
        //    Task.Run(() =>
        //    {
        //        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        //        sw.Start();
        //        Queue<IPAddress> toRemove = new Queue<IPAddress>();
        //        try
        //        {
        //            while (true)
        //            {
        //                lock (lockKey)
        //                {
        //                    foreach (var cs in ipToClient.Values)
        //                    {
        //                        if (!cs.TcpClient.Connected)
        //                        {
        //                            var ip = ((IPEndPoint)cs.TcpClient.Client.RemoteEndPoint).Address;
        //                            toRemove.Enqueue(ip);
        //                            cs.TcpClient.Dispose();
        //                            continue;
        //                        }
        //                        int toRead = cs.TcpClient.Available;
        //                        bool completeMessagePresent = false;
        //                        while (toRead > 0)
        //                        {
        //                            var ns = cs.TcpClient.GetStream();
        //                            int c = Math.Max(cs.bbuff.Length, toRead);
        //                            ns.Read(cs.bbuff, 0, c);
        //                            for (int i = 0; i < c && !completeMessagePresent; i++)
        //                            {
        //                                completeMessagePresent |= (cs.bbuff[i] == Constants.pvcPacketDelim);
        //                            }
        //                            cs.buff.Append(Encoding.UTF8.GetString(cs.bbuff, 0, c));
        //                            toRead -= c;
        //                        }
        //                    }
        //                    while (toRemove.Count > 0)
        //                    {
        //                        ipToClient.Remove(toRemove.Dequeue());
        //                    }
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            logger.Error(ex.ToString());
        //        }
        //    });
        //}

        //#region Send
        //public bool SendDataPacket(PVCDataPacket data, string discordName)
        //{
        //    byte[] json = JsonSerializer.SerializeToUtf8Bytes(data);
        //    return SendMessage(json, discordName);
        //}

        //public bool SendErrorPacket(PVCErrorPacket err, string discordName)
        //{
        //    byte[] json = JsonSerializer.SerializeToUtf8Bytes(err);
        //    return SendMessage(json, discordName);
        //}

        //public bool SendAllLobbyPacket(PVCLobbyPacket lobby)
        //{
        //    byte[] json = JsonSerializer.SerializeToUtf8Bytes(lobby);
        //    bool result = true;
        //    foreach (string discordName in discordToIp.Keys)
        //    {
        //        result &= SendMessage(json, discordName);
        //    }
        //    return result;
        //}

        //public bool SendSingleDataPacket(PVCSingleDataPacket data, string discordName)
        //{
        //    byte[] json = JsonSerializer.SerializeToUtf8Bytes(data);
        //    return SendMessage(json, discordName);
        //}

        //private void SendErrorPacket(PVCErrorPacket err, IPAddress ip)
        //{
        //    byte[] json = JsonSerializer.SerializeToUtf8Bytes(err);
        //    SendMessage(json, ip);
        //}

        ///// <summary>
        ///// Sends a message to the pvc client of the given discord user.
        ///// </summary>
        ///// <param name="data">A serialized packet</param>
        ///// <param name="discordName">The discord username to send the data to (includes discriminator)</param>
        ///// <returns>true if the user was found and the message was sent, false otherwise</returns>
        //private bool SendMessage(byte[] data, string discordName)
        //{
        //    if (discordToIp.ContainsKey(discordName))
        //    {
        //        SendMessage(data, discordToIp[discordName]);
        //        return true;
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //}

        //private void SendMessage(byte[] data, IPAddress ip)
        //{
        //    try
        //    {
        //        if (ipToClient.ContainsKey(ip))
        //        {
        //            lock (lockKey)
        //            {
        //                messageQueue.Enqueue(() =>
        //                {
        //                    var client = ipToClient[ip].TcpClient;
        //                    if (client.Connected)
        //                    {
        //                        NetworkStream ns = ipToClient[ip].TcpClient.GetStream();
        //                        ns.WriteAsync(data);
        //                    }
        //                    else
        //                    {
        //                        client.Dispose();
        //                        ipToClient.Remove(ip);
        //                    }
        //                });
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        logger.Error(e.ToString());
        //    }
        //}
        //#endregion

        //private void StartListener()
        //{
        //    TcpListener listener = new TcpListener(IPAddress.Parse(Settings.Instance.Server.Address), Constants.pvcPort);
        //    listener.Start();
        //    try
        //    {
        //        while (true)
        //        {
        //            TcpClient client = listener.AcceptTcpClient();
        //            if (client.Connected)
        //            {
        //                //byte[] dat = listener.Receive(ref ep);
        //                IPAddress ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
        //                if (!Settings.Instance.BanList.IpAddresses.Contains(ip.ToString())) //can only filter ipv4?
        //                {
        //                    client.NoDelay = true;
        //                    ipToClient[ip] = new ClientState() { TcpClient = client };
        //                    //onMessageRecieved?.Invoke(dat);
        //                    //client.con
        //                }
        //                else
        //                {
        //                    //this client is banned.
        //                    SendErrorPacket(new PVCErrorPacket() { ErrorMessage = "U banned #rip #getrekt" }, ip);
        //                    logger.Warn("Banned user attempted to join.");
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error(ex.ToString());
        //    }
        //    finally
        //    {
        //        listener.Stop();
        //    }
        //}

        //~VoiceProxServer()
        //{
        //    foreach (var s in ipToClient.Values)
        //    {
        //        s?.TcpClient?.Dispose();
        //    }
        //}
        #endregion
    }
}
