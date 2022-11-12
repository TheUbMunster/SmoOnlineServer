using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Shared;
using ENet;

namespace ProxChatClientGUICrossPlatform
{
    internal partial class Services
    {
        private class ClientModel : IDisposable
        {
            #region Member Vars
            private static uint instanceId = 0;

            private uint ID;
            private Services model;
            private string discordUsername;
            private string igUsername;
            private bool disposed = false;

            private Logger clientLogger = new Logger("Client");
            private ConcurrentQueue<Action> messageQueue = new ConcurrentQueue<Action>();

            private Host? client = null;
            private Peer? remotePeer = null;
            private bool requestDisconnect = false;

            private PVCLobbyPacket? lobPack = null;
            //private event Action? onServerConnect;
            //private event Action? onServerDisconnect;
            #endregion

            #region Ctor
            public ClientModel(Services belongingTo/*, string discordUsername, string igUsername*/)
            {
                //this.discordUsername = discordUsername;
                //this.igUsername = igUsername;
                this.model = belongingTo;
                Task.Run(Loop);
            }
            #endregion

            private void Loop()
            {
                #region Setup
                Library.Initialize();
                #endregion

                #region Loop
                try
                {
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    const int fps = 20;
                    const int frameTime = 1000 / fps;
                    int waitTime = (fps / 2) + (fps % 2); //how much time can be given to servicing

                    while (true)
                    {
                        bool processedEvent = true;
                        sw.Restart();
                        while (processedEvent)
                        {
                            if (client != null && client.IsSet) //what does IsSet mean?
                            {
                                Event netEvent;
                                int checkResult = client.CheckEvents(out netEvent);
                                if (checkResult == 0)
                                {
                                    //no events from checkevents, service instead.
                                    //if waitTime != 0, will return 1 if event was dispatched
                                    client.Service(waitTime, out netEvent);
                                }
                                else if (checkResult < 0)
                                {
                                    clientLogger.Warn("CheckEvents failed.");
                                }
                                if (requestDisconnect)
                                {
                                    requestDisconnect = false;
                                    try
                                    {
                                        remotePeer!.Value.Disconnect((uint)Constants.UDPCodes.NormalDisconnect);
                                    }
                                    catch 
                                    {
                                        try
                                        {
                                            remotePeer!.Value.DisconnectNow((uint)Constants.UDPCodes.ForceDisconnect);
                                        } catch { }
                                    }
                                    remotePeer = null;
                                    client = null;
                                    processedEvent = false;
                                }
                                else
                                {
                                    switch (netEvent.Type)
                                    {
                                        case EventType.None:
                                            processedEvent = false;
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
                                            clientLogger.Warn("Invalid netevent type");
                                            break;
                                    }
                                    client?.Flush();
                                }
                            }
                            else
                            {
                                //can't pump the udp (not connected)
                                break;
                            }
                        }
                        //run message loop
                        while (messageQueue.Count > 0)
                        {
                            if (messageQueue.TryDequeue(out Action? action))
                            {
                                //modelLogger.Info(action.Method.Name);
                                if (disposed)
                                    break;
                                else
                                    action?.Invoke();
                            }
                        }
                        sw.Stop();
                        if (disposed)
                            break;
                        int delta = frameTime - (int)sw.ElapsedMilliseconds;
                        delta = delta < 0 ? 0 : delta;
                        const int schedulerPrecisionMS = 10; //I think this is the case on most os's
                        if (delta / schedulerPrecisionMS > 0)
                        {
                            //enough remaining time it is worth it to thread.sleep
                            Thread.Sleep(delta);
                            waitTime = delta;
                        }
                    }
                }
                catch (Exception ex)
                {
                    clientLogger.Error(ex.ToString());
                }
                finally
                {
                    clientLogger.Info("ClientService loop has exited, has been disposed.");
                    Dispose();
                }
                #endregion
            }

            #region NetEvent Handlers
            void HandleConnectEvent(ref Event netEvent)
            {
                clientLogger.Info("Client successfully connected to server.");
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
                                //the client should never recieve a walkie packet
                                SendPacket(new PVCErrorPacket() { ErrorMessage = "Why are you sending the client a PVCWalkieTalkiePacket?" }, netEvent.Peer);
                            }
                            break;
                        case PVCMultiDataPacket multiPacket:
                            {
                                messageQueue.Enqueue(() =>
                                {
                                    OnRecieveDataPacket(multiPacket);
                                    //foreach (var kvp in multiPacket.Volumes)
                                    //{
                                    //if (nameToId.ContainsKey(kvp.Key))
                                    //{
                                    //    if (!idToTicker.ContainsKey(nameToId[kvp.Key]))
                                    //        idToTicker[nameToId[kvp.Key]] = 0;
                                    //    if (kvp.Value.Ticker > idToTicker[nameToId[kvp.Key]])
                                    //    {
                                    //        float percentVol = kvp.Value.Volume ?? 1f;
                                    //        string username = kvp.Key;
                                    //        long userId = nameToId[username];
                                    //        ProxChat.Instance.AddMessage(() =>
                                    //        {
                                    //            byte finalVol = (byte)(percentVol * Settings.Instance.VolumePrefs![username]);
                                    //            AddMessage(() =>
                                    //            {
                                    //                idToVolPercent[userId] = percentVol;
                                    //                voiceManager.SetLocalVolume(userId, finalVol);
                                    //            });
                                    //            ProxChat.Instance.SetPercievedVolume(userId, percentVol);
                                    //        });
                                    //    }
                                    //    //else the volume data for this user is outdated.
                                    //}
                                    //else
                                    //{
                                    //    //cache for later
                                    //    if (!nameToVolCache.ContainsKey(kvp.Key))
                                    //    {
                                    //        modelLogger.Info($"Had to cache a volume for {kvp.Key} because that user isn't set yet.");
                                    //        nameToVolCache[kvp.Key] = kvp.Value;
                                    //    }
                                    //    else if (nameToVolCache[kvp.Key].Ticker < kvp.Value.Ticker)
                                    //    {
                                    //        modelLogger.Info($"Had to cache a volume for {kvp.Key} because that user isn't set yet.");
                                    //        nameToVolCache[kvp.Key] = kvp.Value;
                                    //    }
                                    //}
                                    //}
                                });
                            }
                            break;
                        case PVCClientHandshakePacket handshakePacket:
                            {
                                //the client should never recieve a client handshake packet
                                SendPacket(new PVCErrorPacket() { ErrorMessage = "Why are you sending the client a PVCClientHandshakePacket?" }, netEvent.Peer);
                            }
                            break;
                        case PVCLobbyPacket lobbyPacket:
                            {
                                lobPack = lobbyPacket;
                                messageQueue.Enqueue(() =>
                                {
                                    if (lobPack.Secret != null)
                                    {
                                        OnRecieveLobbyInfo(/*(long)lobPack.AppID, */lobPack.LobbyId, lobPack.Secret);
                                        //DCConnectToLobby(lobPack.LobbyId, lobPack.Secret!);
                                    }
                                    else
                                    {
                                        OnRecieveLimitedLobbyInfo(/*(long)lobPack.AppID, */lobPack.LobbyId);
                                        //ProxChat.Instance.AddMessage(() =>
                                        //{
                                        //    string? maybeSecret = ProxChat.Instance.PromptForLobbySecret();
                                        //    AddMessage(() =>
                                        //    {
                                        //        if (maybeSecret != null)
                                        //        {
                                        //            lobPack!.Secret = maybeSecret;
                                        //            AddMessage(() =>
                                        //            {
                                        //                DCConnectToLobby(lobPack.LobbyId, lobPack.Secret!);
                                        //            });
                                        //        }
                                        //        else
                                        //        {
                                        //            if (client != null)
                                        //            {
                                        //                requestDisconnect = true;
                                        //            }
                                        //        }
                                        //    });
                                        //});
                                    }
                                });
                            }
                            break;
                        case PVCAppIdPacket appIdPacket:
                            {
                                Peer p = netEvent.Peer;
                                messageQueue.Enqueue(() =>
                                {
                                    OnRecieveAppId(appIdPacket.AppID);
                                    SendPacket(new PVCClientHandshakePacket()
                                    {
                                        DiscordUsername = discordUsername,
                                        IngameUsername = igUsername
                                    }, p);
                                });
                            }
                            break;
                        case PVCErrorPacket errorPacket:
                            {
                                clientLogger.Error($"Error packet from server: \"{errorPacket.ErrorMessage ?? "(No error message present.)"}\"");
                            }
                            break;
                        default:
                            clientLogger.Error("HandleRecieve got a packet with a bad packet type.");
                            break;
                    }
                }
                else
                {
                    clientLogger.Error($"HandleRecieve got an unparseable packet (contents): {System.Runtime.InteropServices.Marshal.PtrToStringAnsi(netEvent.Packet.Data, netEvent.Packet.Length)}");
                }
            }

            void HandleTimeoutEvent(ref Event netEvent)
            {
                clientLogger.Warn($"Timeout from server, Code: {(netEvent.Data == 69420 ? "(You've been banned)." : netEvent.Data.ToString())}");
                OnServerTimeout();
            }

            void HandleDisconnectEvent(ref Event netEvent)
            {
                clientLogger.Info($"Disconnect from server, Code: {(netEvent.Data == 69420 ? "(You've been banned)." : netEvent.Data.ToString())}");
                OnServerDisconnect();
            }
            #endregion

            //TODO: make model commands return disposed state
            #region Model Commands
            //move retrigger cooldowns to the model instead of the view
            public void SendWalkieTalkiePacket(string discordUsernameSource, string? specificDiscordRecipient, bool teamOnly)
            {
                if (remotePeer.HasValue)
                {
                    SendPacket(new PVCWalkieTalkiePacket()
                    {
                        SpecificDiscordRecipient = specificDiscordRecipient,
                        TeamOnly = teamOnly,
                        DiscordSource = discordUsernameSource
                    },
                    remotePeer.Value);
                }
                else
                {
                    //modelLogger.Info(Environment.StackTrace);
                    clientLogger.Warn("Attempt to send walkie packet to server, but peer is null.");
                }
            }

            public void SetDiscordUsername(string discordUsername)
            {

            }

            public void SetIngameUsername(string igUsername)
            {

            }

            public void ConnectToServer(string host, string port, Action<bool>? callback = null)
            {
                messageQueue.Enqueue(() =>
                {
                    bool success = false;
                    try
                    {
                        if (requestDisconnect)
                        {
                            remotePeer.Value.DisconnectNow((uint)Constants.UDPCodes.ForceDisconnect);
                            client = null;
                            remotePeer = null;
                        }
                        if (client == null)
                        {
                            client = new Host();
                            client.Create();
                            Address adr = new Address() { Port = ushort.Parse(port) };
                            adr.SetHost(host);
                            client.Connect(adr);
                            success = true;
                        }
                        else
                        {
                            //requestDisconnect = true;
                            clientLogger.Warn("Attempt to connect to a server while already connected to a server!");
                        }
                    }
                    catch (Exception e)
                    {
                        clientLogger.Warn(e.ToString());
                    }
                    callback?.Invoke(success);
                });
            }

            public void DisconnectFromServer()
            {
                messageQueue.Enqueue(() =>
                {
                    if (client != null)
                    {
                        requestDisconnect = true;
                    }
                    else
                    {
                        clientLogger.Info("Attempt to disconnect from server, but we weren't connected to one to begin with! (No action taken)");
                    }
                });
            }

            public void GetConnectedState(Action<bool>? callback = null)
            {
                messageQueue.Enqueue(() =>
                {
                    if (client != null && client.IsSet && (remotePeer?.IsSet ?? false) && !requestDisconnect)
                        callback?.Invoke(false);
                    else
                        callback?.Invoke(true);
                });
            }
            #endregion

            #region Upcall Events
            private void OnServerDisconnect()
            {
                model.OnServiceClientServerDisconnect();
            }

            private void OnServerTimeout()
            {
                model.OnServiceClientServerTimeout();
            }

            public void OnRecieveLobbyInfo(long lobbyId, string secret)
            {
                model.OnServiceClientRecieveLobbyInfo(/*appId, */lobbyId, secret);
            }

            public void OnRecieveLimitedLobbyInfo(long lobbyId)
            {
                model.OnServiceClientRecieveLimitedLobbyInfo(/*appId, */lobbyId);
            }

            public void OnRecieveDataPacket(PVCMultiDataPacket dataPacket)
            {
                model.OnServiceClientRecieveDataPacket(dataPacket);
            }

            public void OnRecieveAppId(ulong appId)
            {
                model.OnServiceClientRecieveAppId(appId);
            }
            #endregion

            #region Helper Methods
            void SendPacket<T>(T packet, Peer recipient) where T : PVCPacket
            {
                messageQueue.Enqueue(() =>
                {
                    if (!recipient.IsSet)
                    {
                        clientLogger.Warn("Attempt to send a packet to a peer whose IsSet == false");
                        return;
                    }
                    byte[] data = Protocol.Serialize(packet);
                    Packet pack = default(Packet); //ENet will dispose automatically when sending.
                    pack.Create(data, PacketFlags.Reliable);
                    bool result = recipient.Send(0, ref pack);
                    if (!result)
                    {
                        clientLogger.Warn($"Sendpacket failed on packet type {packet.PType}.");
                    }
                });
            }
            #endregion

            #region Memory Managment
            public void Dispose()
            {
                if (!disposed)
                {
                    clientLogger.Info($"ClientService of ID: {ID} was disposed");
                    disposed = true;
                    Library.Deinitialize();
                    client?.Dispose();
                }
                else
                {
                    //this shouldn't matter, but it shouldn't be disposed more than once.
                    clientLogger.Warn($"ClientService of ID {ID} attempt to dispose, but it was already disposed");
                }
            }
            #endregion
        }
    }
}
