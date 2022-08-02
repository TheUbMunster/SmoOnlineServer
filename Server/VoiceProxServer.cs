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
    /// <summary>
    /// <b>Author: TheUbMunster</b><br></br><br></br>
    /// </summary>
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

        private event Action<string, string>? OnClientConnect; //discord, ingame
        private event Action<string>? OnClientDisconnect; //discord

        private VolumeCalculation volCalc = new VolumeCalculation();

        private int discordUserCount = 0;

        #region old volume stuff
        //private Dictionary<string, string> igToDiscord = new Dictionary<string, string>(); //dont clear this ever
        //private Dictionary<string, string> discordToIg = new Dictionary<string, string>(); //dont clear this ever
        //private bool sendVolData = false; //send vol data as it's calculated to pvc clients (is voiceprox enabled?)
        //private Dictionary<string, Vector3> igToPos = new Dictionary<string, Vector3>();
        //private Dictionary<string, string?> igToStage = new Dictionary<string, string?>();
        //private Dictionary<string, Dictionary<string, float?>> igToIgsToDirtyVols = new Dictionary<string, Dictionary<string, float?>>(); //null = dont change
        //private Dictionary<string, Dictionary<string, float?>> igToIgsToLastSetVols = new Dictionary<string, Dictionary<string, float?>>();
        //private Dictionary<string, Dictionary<string, ulong>> igToIgsToTickers = new Dictionary<string, Dictionary<string, ulong>>();
        //private Dictionary<string, PVCWalkieTalkiePacket> discordToWalkieOverrides = new Dictionary<string, PVCWalkieTalkiePacket>();
        //private Dictionary<string, long> discordToSysTickCountOnRecieveWalkie = new Dictionary<string, long>();
        //private Dictionary<string, PVCMultiDataPacket> igToPendingPackets = new Dictionary<string, PVCMultiDataPacket>();

        //private void EnsureEntryExists<T>(Dictionary<string, Dictionary<string, T>> dict, string first, string second)
        //{
        //    if (!dict.ContainsKey(first))
        //        dict.Add(first, new Dictionary<string, T>());
        //    if (!dict[first].ContainsKey(second))
        //        dict[first].Add(second, default(T)!);
        //}

        //private void EnsureEntryExists<T>(Dictionary<string, Dictionary<string, T>> dict, string first)
        //{
        //    if (!dict.ContainsKey(first))
        //        dict.Add(first, new Dictionary<string, T>());
        //}

        //public void SetProxChatEnabled(bool enabled)
        //{
        //    sendVolData = enabled;
        //    if (!sendVolData)
        //    {
        //        //enable pvc, slience everyone
        //        SendSetVolInfo(0);
        //    }
        //    else
        //    {
        //        //disable pvc, put everyone back to default
        //        SendSetVolInfo(null);
        //    }
        //}

        //private void SendSetVolInfo(float? vol = null)
        //{
        //    foreach (var perspective in igToIgsToDirtyVols)
        //    {
        //        Dictionary<string, PVCMultiDataPacket.VolTick> dict = new Dictionary<string, PVCMultiDataPacket.VolTick>();
        //        foreach (var kvp in igToDiscord)
        //        {
        //            if (kvp.Value != perspective.Key)
        //            {
        //                EnsureEntryExists(igToIgsToTickers, perspective.Key, kvp.Key);
        //                dict.Add(kvp.Value, new PVCMultiDataPacket.VolTick()
        //                {
        //                    Volume = vol,
        //                    Ticker = ++igToIgsToTickers[perspective.Key][kvp.Key]
        //                });
        //            }
        //        }
        //        var packet = new PVCMultiDataPacket()
        //        {
        //            Volumes = dict
        //        };
        //        string recip = perspective.Key;
        //        AddMessage(() =>
        //        {
        //            if (igToDiscord.ContainsKey(recip))
        //                SendPacket(packet, igToDiscord[recip]);
        //            else
        //                igToPendingPackets[recip] = packet;
        //        });
        //    }
        //}

        //private void WalkieTalkieCalculate()
        //{
        //    List<string> toRemove = null!;
        //    foreach (var kvp in discordToWalkieOverrides)
        //    {
        //        toRemove ??= new List<string>();
        //        kvp.Value.KeepAliveMS -= (Environment.TickCount64 - discordToSysTickCountOnRecieveWalkie[kvp.Key]);
        //        if (kvp.Value.KeepAliveMS > 0)
        //        {
        //            //override the vol to null (client sets to default)
        //            switch (kvp.Value.GetWalkieMode())
        //            {
        //                case PVCWalkieTalkiePacket.WalkieMode.Individual:
        //                    {
        //                        EnsureEntryExists(igToIgsToDirtyVols, discordToIg[kvp.Value.SpecificDiscordRecipient!]);
        //                        igToIgsToDirtyVols[discordToIg[kvp.Value.SpecificDiscordRecipient!]][discordToIg[kvp.Key]] = null;
        //                        EnsureEntryExists(igToIgsToTickers, discordToIg[kvp.Value.SpecificDiscordRecipient!]);
        //                        if (!igToIgsToTickers[discordToIg[kvp.Value.SpecificDiscordRecipient!]].ContainsKey(discordToIg[kvp.Key]))
        //                            igToIgsToTickers[discordToIg[kvp.Value.SpecificDiscordRecipient!]][discordToIg[kvp.Key]] = 0;
        //                        else
        //                            igToIgsToTickers[discordToIg[kvp.Value.SpecificDiscordRecipient!]][discordToIg[kvp.Key]]++;
        //                    }
        //                    break;
        //                case PVCWalkieTalkiePacket.WalkieMode.Team:
        //                    //TODO TODO TODO TODO TODO TODO TODO
        //                    break;
        //                case PVCWalkieTalkiePacket.WalkieMode.Global:
        //                    foreach (var kvp2 in igToDiscord)
        //                    {
        //                        if (kvp2.Key != discordToIg[kvp.Value.DiscordSource])
        //                        {
        //                            //igToIgsToDirtyVols[kvp2.Key][discordToIg[kvp.Key]] = null;

        //                            EnsureEntryExists(igToIgsToDirtyVols, kvp2.Key);
        //                            igToIgsToDirtyVols[kvp2.Key][discordToIg[kvp.Key]] = null;
        //                            EnsureEntryExists(igToIgsToTickers, kvp2.Key);
        //                            if (!igToIgsToTickers[kvp2.Key].ContainsKey(discordToIg[kvp.Key]))
        //                                igToIgsToTickers[kvp2.Key][discordToIg[kvp.Key]] = 0;
        //                            else
        //                                igToIgsToTickers[kvp2.Key][discordToIg[kvp.Key]]++;
        //                        }
        //                    }
        //                    break;
        //                default:
        //                    break;
        //            }
        //        }
        //        else
        //        {
        //            //TODO send last set vols instead?
        //            toRemove.Add(kvp.Key);
        //        }
        //    }
        //    if (toRemove != null)
        //        toRemove.ForEach(x => discordToWalkieOverrides.Remove(x));
        //}

        //private void SendCachedVolInfo()
        //{
        //    foreach (var perspective in igToIgsToDirtyVols)
        //    {
        //        var data = igToIgsToDirtyVols[perspective.Key].Where(x => x.Value != null);
        //        if (data.Any())
        //        {
        //            Dictionary<string, PVCMultiDataPacket.VolTick> vols = new Dictionary<string, PVCMultiDataPacket.VolTick>();
        //            foreach (var elem in data)
        //            {
        //                if (igToDiscord.ContainsKey(elem.Key))
        //                {
        //                    //pvcLogger.Info($"Making {igToDiscord[perspective.Key]}'s volume for {igToDiscord[elem.Key]} = {elem.Value}.");
        //                    vols.Add(igToDiscord[elem.Key], new PVCMultiDataPacket.VolTick()
        //                    {
        //                        Volume = elem.Value,
        //                        Ticker = igToIgsToTickers[perspective.Key][elem.Key] //NO ++ here
        //                    });
        //                }
        //            }
        //            var packet = new PVCMultiDataPacket() 
        //            {
        //                Volumes = vols
        //            };
        //            string recip = perspective.Key;
        //            AddMessage(() =>
        //            {
        //                if (igToDiscord.ContainsKey(recip))
        //                    SendPacket(packet, igToDiscord[recip]);
        //                else
        //                    igToPendingPackets[recip] = packet;
        //            });
        //            igToIgsToDirtyVols[perspective.Key].Clear();
        //        }
        //    }
        //}

        //public void OnPlayerUpdate(string igPlayer, Vector3 pos, string? stage, bool disableForMismatchingScenesOnly = false)
        //{
        //    AddMessage(() =>
        //    {
        //        const float soundEpsilon = 0.01f; //what percent volume change results in an update in the client's volumes.
        //        float ClampedInvLerp(float a, float b, float v)
        //        {
        //            return v < a ? 0 : (v > b ? 1 : (v - a) / (b - a)); //see "linear interpolation"
        //        }

        //        igToStage[igPlayer] = stage;
        //        if (!disableForMismatchingScenesOnly)
        //            igToPos[igPlayer] = pos;
        //        EnsureEntryExists(igToIgsToDirtyVols, igPlayer);
        //        EnsureEntryExists(igToIgsToLastSetVols, igPlayer);
        //        EnsureEntryExists(igToIgsToTickers, igPlayer);
        //        foreach (var kvp in igToPos)
        //        {
        //            if (kvp.Key == igPlayer)
        //                continue;
        //            EnsureEntryExists(igToIgsToDirtyVols, kvp.Key);
        //            EnsureEntryExists(igToIgsToLastSetVols, kvp.Key);
        //            EnsureEntryExists(igToIgsToTickers, kvp.Key);
        //            if (disableForMismatchingScenesOnly)
        //            {
        //                if ((igToStage[igPlayer] ?? "dont make") != (igToStage[kvp.Key] ?? "these equal"))
        //                {
        //                    if (!igToIgsToTickers[kvp.Key].ContainsKey(igPlayer))
        //                        igToIgsToTickers[kvp.Key][igPlayer] = 0;
        //                    else
        //                        igToIgsToTickers[kvp.Key][igPlayer]++;

        //                    if (!igToIgsToTickers[igPlayer].ContainsKey(kvp.Key))
        //                        igToIgsToTickers[igPlayer][kvp.Key] = 0;
        //                    else
        //                        igToIgsToTickers[igPlayer][kvp.Key]++;

        //                    igToIgsToDirtyVols[kvp.Key][igPlayer] = 0;
        //                    igToIgsToDirtyVols[igPlayer][kvp.Key] = 0;
        //                    igToIgsToLastSetVols[kvp.Key][igPlayer] = 0;
        //                    igToIgsToLastSetVols[igPlayer][kvp.Key] = 0;
        //                }
        //                continue;
        //            }
        //            float dist = Vector3.Distance(kvp.Value, igToPos[igPlayer]);
        //            float setVol;
        //            if ((igToStage[igPlayer] ?? "dont make") != (igToStage[kvp.Key] ?? "these equal"))
        //            {
        //                //if both were null then != would fail, but the ??'s with the nonequal strings makes sure that they don't
        //                //stages aren't the same *or* both stages are null
        //                setVol = 0f;
        //            }
        //            else if (dist > Settings.Instance.Discord.BeginHearingThreshold)
        //            {
        //                //too quiet (0%)
        //                setVol = 0f;
        //            }
        //            else if (dist < Settings.Instance.Discord.FullHearingThreshold)
        //            {
        //                //full vol (100%)
        //                setVol = 1f;
        //            }
        //            else
        //            {
        //                setVol = 1f - ClampedInvLerp(Settings.Instance.Discord.FullHearingThreshold, Settings.Instance.Discord.BeginHearingThreshold, dist);
        //                //semi-linearize from 1/((dist^2)*log(dist))
        //                setVol *= setVol; //may sound better without this.
        //            }
        //            float oldVol = (igToIgsToLastSetVols[kvp.Key].ContainsKey(igPlayer) ? igToIgsToLastSetVols[kvp.Key][igPlayer] ?? -100000f : -100000f); //if was never set, must set
        //            if (Math.Abs(oldVol - setVol) > soundEpsilon || (setVol == 0 && oldVol != 0) || (setVol == 1 && oldVol != 1))
        //            {
        //                //must change
        //                if (!igToIgsToTickers[kvp.Key].ContainsKey(igPlayer))
        //                    igToIgsToTickers[kvp.Key][igPlayer] = 0;
        //                else
        //                    igToIgsToTickers[kvp.Key][igPlayer]++;

        //                if (!igToIgsToTickers[igPlayer].ContainsKey(kvp.Key))
        //                    igToIgsToTickers[igPlayer][kvp.Key] = 0;
        //                else
        //                    igToIgsToTickers[igPlayer][kvp.Key]++;

        //                igToIgsToDirtyVols[kvp.Key][igPlayer] = setVol;
        //                igToIgsToDirtyVols[igPlayer][kvp.Key] = setVol;
        //                igToIgsToLastSetVols[kvp.Key][igPlayer] = setVol;
        //                igToIgsToLastSetVols[igPlayer][kvp.Key] = setVol;
        //            }
        //            //else //no need to change
        //        }
        //    });
        //}
        #endregion

        private VoiceProxServer()
        {
            DateTime launchTime = DateTime.Now;
            Logger.AddLogHandler((source, level, text, _) =>
            {
                DateTime logtime = DateTime.Now;
                string data = Logger.PrefixNewLines(text, $"{{{logtime}}} {level} [{source}]");
                Directory.CreateDirectory("logs");
                File.AppendAllText($"logs\\log_{launchTime.Month}-{launchTime.Day}-{launchTime.Year}--{launchTime.Hour}-{launchTime.Minute}-{launchTime.Second}.txt", data);
            });
            Library.Initialize();
            server = new Host();
            Address adr = new Address() { Port = Settings.Instance.Discord.PVCPort };
            try
            {
                IPHostEntry entry = Dns.GetHostEntry("");
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
            }
            catch //for some reason Dns.GetHostEntry likes to throw exceptions
            {
                pvcLogger.Warn("Issue resolving dns stuff in VoiceProxServer");
            }
            server.Create(adr, 16); //discord voice cannot support more than 16 people per lobby.
            OnClientConnect += (string discord, string ingame) =>
            {
                int before = discordUserCount++;
                //int before = igToDiscord.Count;
                //igToDiscord[ingame] = discord;
                //discordToIg[discord] = ingame;
                //if (igToPendingPackets.ContainsKey(ingame))
                //{
                //    SendPacket(igToPendingPackets[ingame], igToDiscord[ingame]);
                //    igToPendingPackets.Remove(ingame);
                //}
                volCalc.SetIGDiscordAssoc(ingame, discord);
                if (discordUserCount == 1 && before == 0)
                {
                    pvcLogger.Info("Was no lobby, first user joined, now attempting to CloseThenOpenPVCLobby.");
                    //igToPos.Clear();
                    //igToStage.Clear();
                    //igToIgsToDirtyVols.Clear();
                    //igToIgsToLastSetVols.Clear();
                    //igToIgsToTickers.Clear();
                    DiscordBot.Instance.CloseThenOpenPVCLobby().ContinueWith((task) =>
                    {
                        if (!task.Result)
                        {
                            pvcLogger.Warn("CloseThenOpenPVCLobby failed.");
                        }
                        else
                        {
                            AddMessage(() =>
                            {
                                SendLobbyPacketsToPending();
                            });
                        }
                    });
                }
                else
                {
                    //var packet = volCalc.GetCorrectVolumePacketForUser(discord);
                    var packet = volCalc.GetZeroedVolumePacketForUser(discord);
                    if (packet != null)
                        AddMessage(() =>
                        {
                            SendPacket(packet, discord);
                        });
                }
            };
            OnClientDisconnect += (string discord) =>
            {
                StringBuilder mesg = new StringBuilder("OnClientDisconnect: " + discord);
                discordUserCount--;
                bool success = volCalc.RemoveIGDiscordAssocIfExists(discord);
                if (!success)
                    mesg.Append($", for some reason they weren't in the ig-discord association.");
                pvcLogger.Info(mesg.ToString());
                //igToDiscord.Remove(discordToIg[discord]);
                //discordToIg.Remove(discord);
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
                    server.Service(waitTime, out netEvent); //instead of 1 per loop, call repeatedly with 0 as arg until it returns eventtype none then wait the remainder?
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
                    //WalkieTalkieCalculate();
                    //if (sendVolData)
                    //{
                    //    SendCachedVolInfo();
                    //}
                    volCalc.GetVolumePacketsForThisFrameAndClearCache().ForEach(x =>
                    {
                        //no addmessage, we're already locked
                        //if (igToDiscord.ContainsKey(x.igRecipient))
                        SendPacket(x.packet, x.discordRecipient);
                    });
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
            string discord = discordToPeer.FirstOrDefault(x => x.Value.ID == p.ID).Key;
            if (discordToPeer.ContainsKey(discord))
            {
                discordToPeer.Remove(discord);
                OnClientDisconnect?.Invoke(discord);
            }
            else
            {
                //a peer having an id of 0 means that the peer is the "server" and we're the "client"
                //the server should never be a "client", so getting a peer with id 0 doesn't make sense.
                pvcLogger.Info("Strange client timeout with id 0.");
            }
        }

        private void HandleRecieveEvent(ref Event netEvent)
        {
            Console.WriteLine("Recieved something");
            //if (!discordToPeer.Values.Contains(netEvent.Peer))
            {
                PVCPacket? packet = Protocol.Deserialize<PVCPacket>(netEvent.Packet.Data, netEvent.Packet.Length);
                if (packet != null)
                {
                    switch (packet)
                    {
                        case PVCWalkieTalkiePacket walkiePacket:
                            {
                                //the user is enabling team *or* global vc *or* individual vc
                                //pvcLogger.Info($"Got walkie packet: recip: {walkiePacket.SpecificRecipient ?? "null"}, teamonly: {walkiePacket.TeamOnly}");

                                volCalc.OnRecieveWalkieTalkie(walkiePacket);
                                //if (discordToWalkieOverrides.ContainsKey(walkiePacket.DiscordSource))
                                //{
                                //    if (discordToWalkieOverrides[walkiePacket.DiscordSource].WalkieTick < walkiePacket.WalkieTick)
                                //    {
                                //        //this packet is newer
                                //        discordToWalkieOverrides[walkiePacket.DiscordSource] = walkiePacket;
                                //        discordToSysTickCountOnRecieveWalkie[walkiePacket.DiscordSource] = Environment.TickCount64;
                                //    }
                                //}
                                //else
                                //{
                                //    discordToWalkieOverrides[walkiePacket.DiscordSource] = walkiePacket;
                                //    discordToSysTickCountOnRecieveWalkie[walkiePacket.DiscordSource] = Environment.TickCount64;
                                //}
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

        public void OnPlayerUpdate(string ingame, Vector3 pos)
        {
            AddMessage(() =>
            {
                volCalc.OnRecieveUserData(ingame, pos);
            });
        }

        public void OnPlayerUpdate(string ingame, string? stage)
        {
            AddMessage(() =>
            {
                volCalc.OnRecieveUserData(ingame, stage);
            });
        }

        public void OnPlayerUpdate(string ingame, VolumeCalculation.Team team)
        {
            AddMessage(() =>
            {
                volCalc.OnRecieveUserData(ingame, team);
            });
        }

        public void SetProxChatEnabled(bool enabled)
        {
            AddMessage(() =>
            {
                volCalc.SetVoiceProxEnabled(enabled);
            });
        }

        public void KickUserIfConnected(IPAddress addr)
        {
            Peer? p = null;
            foreach (var peer in discordToPeer.Select(x => x.Value).Union(pendingClients))
            {
                if (peer.IsSet)
                {
                    try
                    {
                        IPAddress ipad = IPAddress.Parse(peer.IP);
                        if (addr.Equals(ipad))
                        {
                            peer.DisconnectNow(69420);//69420 is banned code
                            p = peer;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        pvcLogger.Warn("Tried to parse ips to kick the recently banned user and something went wrong: " + ex.ToString());
                    }
                }
            }
            if (p != null)
            {
                var e = discordToPeer.FirstOrDefault(x => x.Value.ID == p.Value.ID);
                if (e.Value.ID == p.Value.ID)
                {
                    discordToPeer.Remove(e.Key);
                }
                else
                {
                    pendingClients.Remove(e.Value);
                }
            }
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
            Console.WriteLine("Sent info");
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
                            SendPacket(new PVCLobbyPacket()
                            {
                                LobbyId = lobbyInfo.Value.id,
                                Secret = Settings.Instance.Discord.AutoSendPVCPassword ? lobbyInfo.Value.secret : null
                            }, pending);
                        }
                        catch (Exception ex)
                        {
                            //they might not still be trying to connect/might have dcd
                            pvcLogger.Error("Tried to send packet to a pending client, but something went wrong: " + ex.ToString());
                        }
                    }
                    pendingClients.Clear();
                });
            }
        }

        public void AddMessage(Action message)
        {
            lock (messageKey)
            {
                messageQueue.Enqueue(message);
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
