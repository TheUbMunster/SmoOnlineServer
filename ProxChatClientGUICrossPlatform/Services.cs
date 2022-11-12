#define GDK_UI

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared;
using Discord;
using ENet;

namespace ProxChatClientGUICrossPlatform
{
    internal partial class Services
    {
        //TODO: add message queues for each service to queue up calls while it's null




        private object modelLock = new object();
        private ConcurrentQueue<Action> messageQueue = new ConcurrentQueue<Action>();
        private Logger modelLogger = new Logger("Model");

        private DiscordModel DiscordServ { get; set; }
        private ClientModel ClientServ { get; set; }
        public ProxChat View { get; private set; }

        private string ingameUsername;
        private Dictionary<long, ulong> idToTicker = new Dictionary<long, ulong>();

        public Services(ProxChat belongingTo, string igUsername)
        {
            ingameUsername = igUsername;
            ClientServ = new ClientModel(this);
            Task.Run(() =>
            {
                #region OLD UI Event Subscription
                //onUserConnect += id =>
                //{
                //    ProxChat.Instance.AddMessage(() =>
                //    {
                //        //add that users UI element.
                //        string username = idToUser[id].Username + "#" + idToUser[id].Discriminator;
                //        ProxChat.Instance.AddMemberToList(id, username, id == currentUser!.Value.Id);
                //        //bind buttons
                //    });
                //};

                //onUserDisconnect += id =>
                //{
                //    ProxChat.Instance.AddMessage(() =>
                //    {
                //        //unbind buttons & remove that users UI element.
                //        ProxChat.Instance.RemoveMemberFromList(id);
                //    });
                //};

                //                onImageRecieved += (id, width, height, data) =>
                //                {
                //                    byte[] highlighted = new byte[data.Length];
                //                    ProxChat.Instance.AddMessage(() =>
                //                    {
                //                        //rearrange pixel data
                //                        for (int x = 0; x < width; x++)
                //                        {
                //                            for (int y = 0; y < height; y++)
                //                            {
                //                                int i = (int)(x + (y * width)) * 4;
                //                                byte r = data[i];
                //                                byte g = data[i + 1];
                //                                byte b = data[i + 2];
                //                                byte a = data[i + 3];

                //                                float xx = (((float)x / width) - 0.5f) * 2f;
                //                                float yy = (((float)y / height) - 0.5f) * 2f;
                //                                float distSq = (xx * xx) + (yy * yy);
                //                                bool opaque = distSq < 1f;
                //                                bool greenBorder = distSq < 1f && distSq > (0.925f * 0.925f);

                //                                if (opaque)
                //                                {
                //#if GDK_UI
                //                                    data[i + 2] = b;
                //                                    data[i + 1] = g;
                //                                    data[i] = r;
                //                                    data[i + 3] = a;
                //#else
                //                                    data[i] = b;
                //                                    data[i + 1] = g;
                //                                    data[i + 2] = r;
                //                                    data[i + 3] = a;
                //#endif
                //                                    if (!greenBorder)
                //                                    {
                //#if GDK_UI
                //                                        highlighted[i + 2] = b;
                //                                        highlighted[i + 1] = g;
                //                                        highlighted[i] = r;
                //                                        highlighted[i + 3] = a;
                //#else
                //                                        highlighted[i] = b;
                //                                        highlighted[i + 1] = g;
                //                                        highlighted[i + 2] = r;
                //                                        highlighted[i + 3] = a;
                //#endif
                //                                    }
                //                                    else
                //                                    {
                //#if GDK_UI
                //                                        highlighted[i + 2] = 96;
                //                                        highlighted[i + 1] = 255;
                //                                        highlighted[i] = 64;
                //                                        highlighted[i + 3] = 255;
                //#else
                //                                        highlighted[i] = 96;
                //                                        highlighted[i + 1] = 255;
                //                                        highlighted[i + 2] = 64;
                //                                        highlighted[i + 3] = 255;
                //#endif
                //                                    }
                //                                }
                //                                else
                //                                {
                //                                    data[i + 3] = (byte)0;
                //                                }
                //                            }
                //                        }
                //                        //set the image
                //                        ProxChat.Instance.SetUserImage(id, width, height, data, highlighted);
                //                    });
                //                };

                //onServerConnect += () => //this actually talks about connecting to the discord lobby
                //{ 
                //    ProxChat.Instance.AddMessage(() =>
                //    {
                //        ProxChat.Instance.SetCDCButtonEnabled(true);
                //        ProxChat.Instance.SetCDCButton(false);
                //        ProxChat.Instance.SetConnectionStatus(true);
                //    });
                //};

                //onServerDisconnect += () => //this actually talks about leaving the discord lobby
                //{
                //    idToTicker.Clear();
                //    List<long> ids = new List<long>(idToUser.Keys); //closure capture this instead of the real dict
                //    ProxChat.Instance.AddMessage(() =>
                //    {
                //        foreach (long userId in ids)
                //        {
                //            if (userId != currentUser!.Value.Id)
                //            {
                //                ProxChat.Instance.RemoveMemberFromList(userId);
                //            }
                //        }
                //        AddMessage(() =>
                //        {
                //            idToUser.Clear();
                //            nameToId.Clear();
                //        });
                //        ProxChat.Instance.SetCDCButton(true);
                //        ProxChat.Instance.SetCDCButtonEnabled(true);
                //        ProxChat.Instance.SetConnectionStatus(false);
                //    });
                //    client = null;
                //    remotePeer = null;
                //    AddMessage(() =>
                //    {
                //        DisconnectLobby();
                //    });
                //};
                #endregion

                #region Loop
                //try
                //{
                //    //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                //    //sw.Start();
                //    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                //    const int frameTime = 50; //20fps
                //    int waitTime = 20;//how much time can be given to server.Service()
                //    while (true)
                //    {
                //        Event netEvent;
                //        if (client != null && client.IsSet) //what does IsSet mean?
                //        {
                //            int checkResult = client.CheckEvents(out netEvent);
                //            if (checkResult == 0)
                //            {
                //                //no events.
                //                client.Service(waitTime, out netEvent);
                //            }
                //            else if (checkResult < 0)
                //            {
                //                modelLogger.Warn("CheckEvents failed.");
                //            }
                //            sw.Restart();
                //            if (requestDisconnect)
                //            {
                //                requestDisconnect = false;
                //                try
                //                {
                //                    remotePeer!.Value.Disconnect(0);
                //                }
                //                catch { }
                //                //client.Flush();
                //                //client.Dispose();
                //                //onServerDisconnect?.Invoke();
                //            }
                //            else
                //            {
                //                switch (netEvent.Type)
                //                {
                //                    case EventType.None:
                //                        break;
                //                    case EventType.Connect:
                //                        {
                //                            remotePeer = netEvent.Peer;
                //                            HandleConnectEvent(ref netEvent);
                //                        }
                //                        break;
                //                    case EventType.Disconnect:
                //                        {
                //                            remotePeer = null;
                //                            HandleDisconnectEvent(ref netEvent);
                //                        }
                //                        break;
                //                    case EventType.Receive:
                //                        {
                //                            remotePeer = netEvent.Peer;
                //                            HandleRecieveEvent(ref netEvent);
                //                            netEvent.Packet.Dispose();
                //                        }
                //                        break;
                //                    case EventType.Timeout:
                //                        {
                //                            remotePeer = null;
                //                            HandleTimeoutEvent(ref netEvent);
                //                        }
                //                        break;
                //                    default:
                //                        modelLogger.Warn("Invalid netevent type");
                //                        break;
                //                }
                //                if (client != null && client.IsSet)
                //                    client.Flush();
                //            }
                //        }
                //        else
                //        {
                //            System.Threading.Thread.Sleep(waitTime); //simulate the waiting done by service
                //            sw.Restart();
                //        }
                //        discord.RunCallbacks(); //TODO: make all callbacks add to the message loop
                //        lock (modelLock)
                //        {
                //            while (messageQueue.Count > 0)
                //            {
                //                if(messageQueue.TryDequeue(out Action? action))
                //                {
                //                    //modelLogger.Info(action.Method.Name);
                //                    action?.Invoke();
                //                }
                //            }
                //        }
                //        sw.Stop();
                //        waitTime = frameTime - (int)sw.ElapsedMilliseconds;
                //        waitTime = waitTime < 0 ? 0 : waitTime; //if each loop needs to take 50ms, here's how much the next service can take
                //    }
                //}
                //catch (Exception ex)
                //{
                //    //log
                //    modelLogger.Error(ex.ToString());
                //}
                //finally
                //{
                //    discord?.Dispose();
                //    client?.Dispose();
                //}
                #endregion
            });
        }

        #region Discord Service Event calls
        private void OnServiceDiscordLobbyMemberConnect(long userId, bool isMainUser = false)
        {
            ProxChat.Instance.AddMessage(() =>
            {
                //add that users UI element.
                string username = idToUser[id].Username + "#" + idToUser[id].Discriminator;
                ProxChat.Instance.AddMemberToList(id, username, id == currentUser!.Value.Id);
                //bind buttons
            });
        }

        private void OnServiceDiscordLobbyMemberConnect(long userId, string username, bool isMainUser = false)
        {
            if (isMainUser)
            {
                //View.SetDiscordUsername
                ClientServ.SetDiscordUsername(username);
            }







            ProxChat.Instance.AddMessage(() =>
            {
                //add that users UI element.
                string username = idToUser[id].Username + "#" + idToUser[id].Discriminator;
                ProxChat.Instance.AddMemberToList(id, username, id == currentUser!.Value.Id);
                //bind buttons
            });
        }

        private void OnServiceDiscordLobbyMemberDisconnect(long userId)
        {
            ProxChat.Instance.AddMessage(() =>
            {
                //unbind buttons & remove that users UI element.
                ProxChat.Instance.RemoveMemberFromList(id);
            });
        }

        private void OnServiceDiscordLobbyMemberImageRecieve(long userId, uint width, uint height, byte[] data)
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

                        float xx = (((float)x / width) - 0.5f) * 2f;
                        float yy = (((float)y / height) - 0.5f) * 2f;
                        float distSq = (xx * xx) + (yy * yy);
                        bool opaque = distSq < 1f;
                        bool greenBorder = distSq < 1f && distSq > (0.925f * 0.925f);
                        if (opaque) ;
                        else if (greenBorder)
                        {
                            highlighted[i + 2] = 96;
                            highlighted[i + 1] = 255; //green?
                            highlighted[i] = 64;
                            highlighted[i + 3] = 255; //alpha?
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
        }

        private void OnServiceDiscordLobbyDelete(long lobbyId, uint reason)
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
        }

        private void OnServiceDiscordLobbyMemberTalk(long userId, bool isTalking)
        {

        }
        #endregion

        #region Client Service Event calls
        private void OnServiceClientServerDisconnect()
        {

        }

        private void OnServiceClientServerTimeout()
        {

        }

        private void OnServiceClientRecieveLobbyInfo(/*long appId, */long lobbyId, string secret)
        {

        }

        private void OnServiceClientRecieveLimitedLobbyInfo(/*long appId, */long lobbyId)
        {

        }

        private void OnServiceClientRecieveDataPacket(PVCMultiDataPacket dataPacket)
        {

        }

        private void OnServiceClientRecieveAppId(ulong appId)
        {
            if (DiscordServ == null)
                DiscordServ = new DiscordModel(this, (long)appId);
            else
            {
                modelLogger.Warn("Recieved discord app id, but discord service is already initialized?!");
            }
        }
        #endregion

        #region Service Commands
        public void SendWalkieTalkiePacket(string discordUsernameSource, string? specificDiscordRecipient, bool teamOnly)
        {
            if (ClientServ != null)
            {
                ClientServ.SendWalkieTalkiePacket(discordUsernameSource, specificDiscordRecipient, teamOnly);
            }
            else
            {
                modelLogger.Warn("Tried to send a walkie-talkie packet, but client service was null");
            }
        }

        public void SetMute(long userId, bool muted)
        {
            if (DiscordServ != null)
            {
                DiscordServ.SetUserMute(userId, muted);
            }
            else
            {
                modelLogger.Warn("Tried to mute/unmute a user, but discord service was null");
            }
        }

        public void SetDeaf(bool deaf)
        {
            if (DiscordServ != null)
            {
                DiscordServ.SetDeafStatus(deaf);
            }
            else
            {
                modelLogger.Warn("Tried to deaf/undeaf, but discord service was null");
            }
        }

        public void ConnectToServer(string host, string port, Action<bool>? callback = null)
        {
            if (ClientServ != null)
                ClientServ.ConnectToServer(host, port, callback);
            else
            {
                modelLogger.Warn("Tried to connect to server, but the client service was null!");
                callback?.Invoke(false);
            }
        }

        public void DisconnectFromServer()
        {
            if (ClientServ != null)
                ClientServ.DisconnectFromServer();
            else
            {
                modelLogger.Warn("Tried to disconnect from server, but the client service was null!");
            }
        }

        public void GetServerConnectedState(Action<bool>? callback = null)
        {
            if (ClientServ != null)
                ClientServ.GetConnectedState(callback);
            else
            {
                modelLogger.Warn("Tried to check server connected state, but the client service was null!");
                callback?.Invoke(false);
            }
        }
        #endregion

        #region Old
        //public void AddMessage(Action action)
        //{
        //    messageQueue.Enqueue(action);
        //}

        //public void ConnectToServer(string host, string port)
        //{
        //    try
        //    {
        //        if (client != null)
        //        {
        //            requestDisconnect = true;
        //        }
        //        else
        //        {
        //            client = new Host();
        //            client.Create();
        //            Address adr = new Address() { Port = ushort.Parse(port) };
        //            adr.SetHost(host);
        //            client.Connect(adr);
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        modelLogger.Warn(e.ToString());
        //    }
        //}

        //public bool IsConnectedToServer()
        //{
        //    return client != null;
        //}

        //public void RecalculateRealVolume(string username, byte newVolume)
        //{
        //    try
        //    {
        //        int newVol = (int)(idToVolPercent[nameToId[username]] * (int)newVolume);
        //        newVol = (newVol < 0 ? 0 : (newVol > 200 ? 200 : newVol));
        //        modelLogger.Info("Recalc'd a users volume to: " + newVol);
        //        voiceManager.SetLocalVolume(nameToId[username], (byte)newVol);
        //    }
        //    catch (Exception ex)
        //    {
        //        modelLogger.Warn("Attempt to recalculate the volume of a user failed: " + ex.ToString());
        //    }
        //}

        //public void SetMute(long userId, bool isMuted)
        //{
        //    if (userId == currentUser!.Value.Id)
        //    {
        //        voiceManager.SetSelfMute(isMuted);
        //    }
        //    else
        //    {
        //        voiceManager.SetLocalMute(userId, isMuted);
        //    }
        //}

        //public void SetDeaf(bool isDeaf)
        //{
        //    voiceManager.SetSelfDeaf(isDeaf);
        //}
        #endregion
    }

    public static class ExtMethods
    {
        public static string FullUsername(this User user) => user.Username + "#" + user.Discriminator;
    }
}
