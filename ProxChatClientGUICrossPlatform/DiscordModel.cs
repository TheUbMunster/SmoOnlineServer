using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Shared;
using Discord;
using static ProxChatClientGUICrossPlatform.ExtMethods;

namespace ProxChatClientGUICrossPlatform
{
    internal partial class Services
    {
        //TODO make model partial and have these classes as private members
        /// <summary>
        /// <b>Author: TheUbMunster</b><br></br><br></br>
        /// An instance to interface with the discord gamesdk to easily perform discord voice tasks.
        /// </summary>
        private class DiscordModel : IDisposable
        {
            #region Member Vars
            private static uint instanceId = 0;

            private uint ID;
            private long clientId;
            private Services model;
            private bool disposed = false;

            private Discord.Discord discord = null!;
            private LobbyManager lobbyManager = null!;
            private VoiceManager voiceManager = null!;
            private ImageManager imageManager = null!;
            private UserManager userManager = null!;
            private User? currentUser = null;
            private Lobby? lob = null;
            //private PVCLobbyPacket lobPack = null!;

            private Logger discordLogger = new Logger("DiscordService");
            private ConcurrentQueue<Action> messageQueue = new ConcurrentQueue<Action>();

            //private Dictionary<string, PVCMultiDataPacket.VolTick> nameToVolCache = new Dictionary<string, PVCMultiDataPacket.VolTick>();
            //private Dictionary<long, float> idToVolPercent = new Dictionary<long, float>(); //what % of user pref vol should users be set to with SetLocalVol
            //private Dictionary<string, long> nameToId = new Dictionary<string, long>();
            //private Dictionary<long, User> idToUser = new Dictionary<long, User>();

            //public event Action<long>? onUserConnect;
            //public event Action<long>? onUserDisconnect;
            //public event Action<long, uint, uint, byte[]>? onImageRecieved;
            //public event Action<long, uint>? onLobbyClose;
            #endregion

            #region Ctor
            /// <summary>
            /// Creates a discord service object from the specified discord developer "application ID", 
            /// and immediately starts a internal message loop to handle tasks.
            /// 
            /// This allows you to change user settings like local user volume and mute/unmute status.
            /// 
            /// If the main loop throws an exception, the object is immediately disposed, and any messages
            /// to be executed in the internal message loop are not executed.
            /// </summary>
            /// <param name="belongingTo">The model object that this service belongs to</param>
            /// <param name="applicationId">The discord "application ID"/"client ID"</param>
            public DiscordModel(Services belongingTo, long applicationId)
            {
                discordLogger.Info($"DiscordService of ID: {(ID = instanceId++)} was created");
                this.clientId = applicationId;
                this.model = belongingTo;
                Task.Run(Loop);
            }
            #endregion

            private void Loop()
            {
                #region Setup
                try
                {
                    discord = new Discord.Discord(clientId, (UInt64)Discord.CreateFlags.Default);
                }
                catch (Exception ex)
                {
                    discordLogger.Error("Couldn't load the discord! " + ex.ToString());
                    return;
                }
                if (discord == null)
                {
                    discordLogger.Error("Discord loaded without exception, but was null!");
                    return;
                }
                lobbyManager = discord.GetLobbyManager();
                voiceManager = discord.GetVoiceManager();
                imageManager = discord.GetImageManager();
                userManager = discord.GetUserManager();
                discord.SetLogHook(LogLevel.Debug, (level, message) =>
                {
                    discordLogger.Info($"DiscordLog[{level}] {message}");
                });
                UserManager.CurrentUserUpdateHandler upd = () =>
                {
                    currentUser = userManager.GetCurrentUser();
                    discordLogger.Info($"Main user loaded: {currentUser.Value.Id}, \"{currentUser.Value.FullUsername()}\"");
                    //idToUser[currentUser.Value.Id] = currentUser.Value;
                    //nameToId[currentUser.Value.FullUsername()] = currentUser.Value.Id;
                    //onUserConnect?.Invoke(currentUser.Value.Id);
                    model.OnServiceDiscordLobbyMemberConnect(currentUser.Value.Id, currentUser.Value.FullUsername(), true);
                    FetchImage(currentUser.Value.Id);
                };
                userManager.OnCurrentUserUpdate += upd;
                while (currentUser == null) //add timeout for image data
                {
                    discord.RunCallbacks();
                }
                userManager.OnCurrentUserUpdate -= upd; //if the user changes nick in the middle of a game it will mess things up.

                lobbyManager.OnMemberConnect += (long lobbyId, long userId) =>
                {
                    OnLobbyMemberConnect(userId);
                };
                lobbyManager.OnMemberDisconnect += (long lobbyId, long userId) =>
                {
                    OnLobbyMemberDisconnect(userId);
                };
                lobbyManager.OnLobbyDelete += (long lobbyId, uint reason) =>
                {
                    OnLobbyDelete(lobbyId, reason);
                };
                lobbyManager.OnSpeaking += (long lobbyId, long userId, bool speaking) =>
                {
                    OnUserTalkingChange(userId, speaking);
                };
                #endregion

                #region Loop
                try
                {
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    const int fps = 20;
                    const int frameTime = 1000 / fps;

                    while (true)
                    {
                        sw.Restart();
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
                        }
                    }
                }
                catch (Exception ex)
                {
                    discordLogger.Error(ex.ToString());
                }
                finally
                {
                    discordLogger.Info("ClientService loop has exited, has been disposed.");
                    Dispose();
                }
                #endregion
            }

            //TODO: make model commands return disposed state
            #region Model Commands
            public void DisconnectLobby(Action<bool>? callback = null)
            {
                messageQueue.Enqueue(() =>
                {
                    if (lob != null)
                    {
                        discordLogger.Info("Leaving the current lobby...");
                        lobbyManager.DisconnectLobby(lob.Value.Id, res =>
                        {
                            if (res == Result.Ok || res == Result.NotFound)
                            {
                                discordLogger.Info("You left the lobby");
                            }
                            else
                            {
                                discordLogger.Info($"Something went wrong with leaving the lobby: {res.ToString()}");
                            }
                            lob = null;
                            callback?.Invoke(true);
                        });
                    }
                    else
                    {
                        callback?.Invoke(false);
                    }
                });
            }

            public void ConnectLobby(long id, string secret, Action<bool>? callback = null)
            {
                messageQueue.Enqueue(() =>
                {
                    if (lob != null)
                    {
                        callback?.Invoke(false);
                    }
                    else
                    {
                        lobbyManager.ConnectLobby(id, secret, (Result res, ref Lobby lobby) =>
                        {
                            if (res != Result.Ok)
                            {
                                discordLogger.Info("Something went wrong when joining the lobby.");
                            }
                            else
                            {
                                discordLogger.Info("Joined the lobby successfully.");
                                bool success = false;
                                lobbyManager.ConnectVoice(lobby.Id, x =>
                                {
                                    if (x != Result.Ok)
                                    {
                                        discordLogger.Info("Something went wrong when joining vc.");
                                    }
                                    else
                                    {
                                        discordLogger.Info("Joined vc.");
                                        success = true;
                                    }
                                });
                                if (success)
                                {
                                    lob = lobby;
                                    IEnumerable<User> users = lobbyManager.GetMemberUsers(lobby.Id);
                                    discordLogger.Info("All users in the lobby:\n" +
                                            $"{string.Join(",\n", users.Select(x => $"{x.Id}: {x.Username}#{x.Discriminator}"))}");
                                    foreach (User user in users)
                                    {
                                        model.OnServiceDiscordLobbyMemberConnect(user.Id, user.FullUsername(), currentUser != null ? currentUser.Value.Id == user.Id : false);
                                    }
                                    callback?.Invoke(true);
                                }
                                else
                                    callback?.Invoke(false);
                            }
                        });
                    }
                });
            }

            public void SetUserVolume(long userId, float percentage)
            {
                messageQueue.Enqueue(() =>
                {
                    byte vol = (byte)(percentage * 200f);
                    vol = (byte)(vol > 200 ? 200 : vol);
                    voiceManager.SetLocalVolume(userId, vol);
                });
            }

            public void SetUserMute(long userId, bool muteStatus)
            {
                messageQueue.Enqueue(() =>
                {
                    if (currentUser != null && currentUser.Value.Id == userId)
                        voiceManager.SetSelfMute(muteStatus);
                    else
                        voiceManager.SetLocalMute(userId, muteStatus);
                });
            }

            public void SetDeafStatus(bool deafStatus)
            {
                messageQueue.Enqueue(() =>
                {
                    voiceManager.SetSelfDeaf(deafStatus);
                });
            }
            #endregion

            #region Upcall Events
            private void OnUserTalkingChange(long userId, bool isSpeaking)
            {
                model.OnServiceDiscordLobbyMemberTalk(userId, isSpeaking);
            }

            private void OnLobbyDelete(long lobbyId, uint reason)
            {
                discordLogger.Info("Discord VC lobby closed because: " + reason);
                model.OnServiceDiscordLobbyDelete(lobbyId, reason);
            }

            private void OnLobbyMemberConnect(long userId)
            {
                if (currentUser != null && currentUser.Value.Id == userId)
                {
                    discordLogger.Error("OnLobbyMemberConnect was called with id of current user! This should only be called with other users.");
                    return;
                }
                //idToVolPercent[userId] = 0f;
                voiceManager.SetLocalVolume(userId, 0);
                voiceManager.SetLocalMute(userId, false);
                userManager.GetUser(userId, (Result res, ref User user) =>
                {
                    if (res != Result.Ok)
                    {
                        discordLogger.Error("GetUser failed in OnMemberConnect, connected user is stuck muted.");
                    }
                    else
                    {
                        model.OnServiceDiscordLobbyMemberConnect(userId, user.FullUsername(), false);
                        FetchImage(userId);
                    }
                    #region Old
                    //if (res != Result.Ok)
                    //{
                    //    discordLogger.Error("GetUser failed in OnMemberConnect, connected user is stuck muted.");
                    //    return;
                    //}
                    //idToUser[userId] = user;
                    //string userName = user.Username + "#" + user.Discriminator;
                    //nameToId[userName] = user.Id;
                    //if (nameToVolCache.ContainsKey(userName)) //TODO: is this necessary when line 231 is just gonna set it to something else?
                    //{
                    //    discordLogger.Info($"Applying cached vol info of {nameToVolCache[userName]} for {userName}.");
                    //    float percentVol = nameToVolCache[userName].Volume ?? 1f;
                    //    long userId = nameToId[userName];
                    //    ProxChat.Instance.AddMessage(() =>
                    //    {
                    //        byte finalVol = (byte)(percentVol * Settings.Instance.VolumePrefs![userName]);
                    //        AddMessage(() =>
                    //        {
                    //            idToVolPercent[userId] = percentVol;
                    //            voiceManager.SetLocalVolume(userId, finalVol);
                    //        });
                    //        ProxChat.Instance.SetPercievedVolume(userId, percentVol);
                    //    });
                    //}
                    //voiceManager.SetLocalMute(userId, false);
                    //onUserConnect?.Invoke(userId);
                    //ProxChat.Instance.AddMessage(() =>
                    //{
                    //    byte vol = Settings.Instance.VolumePrefs![userName];
                    //    AddMessage(() =>
                    //    {
                    //        idToVolPercent[userId] = 1f;
                    //        discordLogger.Info($"{userName} joined the lobby and volume was set to {vol}.");
                    //        voiceManager.SetLocalVolume(userId, vol);
                    //    });
                    //    ProxChat.Instance.SetPercievedVolume(userId, 1f);
                    //});
                    //FetchImage(userId);
                    #endregion
                });
                //inform view with just userId, update with username and image later
                model.OnServiceDiscordLobbyMemberConnect(userId, false);
            }

            private void OnLobbyMemberDisconnect(long userId)
            {
                //TODO: inform view
                discordLogger.Info($"{userId} left the lobby.");
                model.OnServiceDiscordLobbyMemberDisconnect(userId);
                //if (idToUser.ContainsKey(userId))
                //{
                //    //string userName = idToUser[userId].FullUsername();
                //    //nameToId.Remove(userName);
                //    //idToUser.Remove(userId);
                //}
                //else
                //{
                //    discordLogger.Info(userId + " left the lobby (They weren't in the model's usertable, unknown username)");
                //}
            }

            private void OnLobbyMemberImage(long userId, uint width, uint height, byte[] data)
            {
                //inform view.
                model.OnServiceDiscordLobbyMemberImageRecieve(userId, width, height, data);
            }
            #endregion

            #region Helper Methods
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
                        discordLogger.Warn($"Failed to get the profile picture for the main user");
                        return;
                    }
                    else
                    {
                        discordLogger.Info("Recieved raw image data for " + returnedHandle.Id);
                        byte[] data = imageManager.GetData(returnedHandle);
                        ImageDimensions dim = imageManager.GetDimensions(returnedHandle);
                        OnLobbyMemberImage(returnedHandle.Id, dim.Width, dim.Height, data);
                    }
                });
            }
            #endregion

            #region Memory Managment
            public void Dispose()
            {
                if (!disposed)
                {
                    discordLogger.Info($"DiscordService of ID: {ID} was disposed");
                    disposed = true;
                    discord.Dispose();
                }
                else
                {
                    //this shouldn't matter, but it shouldn't be disposed more than once.
                    discordLogger.Warn($"DiscordService of ID {ID} attempt to dispose, but it was already disposed");
                }
            }
            #endregion

            #region Old
            //private void DCConnectToLobby(long id, string secret)
            //{
            //    DisconnectLobby(() =>
            //    {
            //        AddMessage(() =>
            //        {
            //            modelLogger.Info("Beginning to connect to lobby...");
            //            lobbyManager.ConnectLobby(id, secret, (Result res, ref Lobby lobby) =>
            //            {
            //                if (res != Result.Ok)
            //                {
            //                    modelLogger.Info("Something went wrong when joining the lobby.");
            //                    //set ui back because it failed
            //                    AddMessage(() =>
            //                    {
            //                        requestDisconnect = true;
            //                    });
            //                    ProxChat.Instance.AddMessage(() =>
            //                    {
            //                        ProxChat.Instance.SetCDCButtonEnabled(true);
            //                        ProxChat.Instance.SetCDCButton(true);
            //                        ProxChat.Instance.SetConnectionStatus(false);
            //                    });
            //                    return;
            //                }
            //                else
            //                {
            //                    discordLogger.Info("Joined the lobby successfully.");
            //                }
            //                IEnumerable<User> users = lobbyManager.GetMemberUsers(lobby.Id);
            //                discordLogger.Info("All users in the lobby:\n" +
            //                    $"{string.Join(",\n", users.Select(x => $"{x.Id}: {x.Username}#{x.Discriminator}"))}");
            //                long currId = currentUser!.Value.Id;
            //                foreach (User u in users)
            //                {
            //                    if (u.Id != currentUser!.Value.Id)
            //                    {
            //                        AddMessage(() =>
            //                        {
            //                            idToUser[u.Id] = u;
            //                            string username = u.Username + "#" + u.Discriminator;
            //                            nameToId[username] = u.Id;
            //                            voiceManager.SetLocalMute(u.Id, false);
            //                            onUserConnect?.Invoke(u.Id);
            //                            ProxChat.Instance.AddMessage(() =>
            //                            {
            //                                byte vol = Settings.Instance.VolumePrefs![username];
            //                                AddMessage(() =>
            //                                {
            //                                    idToVolPercent[u.Id] = 1f;
            //                                    voiceManager.SetLocalVolume(u.Id, vol);
            //                                    discordLogger.Info($"Set {u.Username}#{u.Discriminator}'s volume to {vol}");
            //                                });
            //                                ProxChat.Instance.SetPercievedVolume(u.Id, 1f);
            //                            });
            //                            FetchImage(u.Id);
            //                        });
            //                    }
            //                }

            //                lobbyManager.ConnectVoice(lobby.Id, x =>
            //                {
            //                    if (res != Result.Ok)
            //                    {
            //                        discordLogger.Info("Something went wrong when joining vc.");
            //                    }
            //                    else
            //                    {
            //                        discordLogger.Info("Joined vc.");
            //                    }
            //                });
            //                lobbyManager.ConnectNetwork(lobby.Id);
            //                lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
            //                lob = lobby;
            //                AddMessage(() =>
            //                {
            //                    onServerConnect?.Invoke();
            //                });
            //            });
            //        });
            //    });
            //}
            #endregion
        }
    }
}
