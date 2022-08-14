using Gtk;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime;
using System.Linq;
using UI = Gtk.Builder.ObjectAttribute;
using IOPath = System.IO.Path;
using SAction = System.Action;
using Shared;

namespace ProxChatClientGUICrossPlatform
{
    internal class ProxChat : Window
    {
        #region OSAPI
        public static class KeyService
        {
            static KeyService()
            {
                foreach (WinVirtualKeys key in Enum.GetValues(typeof(WinVirtualKeys)))
                {
                    keyStates[(int)key] = KeyState.None;
                }
            }

            public enum KeyState
            {
                Pressed = 0,
                Held,
                Released,
                None
            }
            [DllImport("User32.dll")]
            private static extern short GetAsyncKeyState(int vKey);

            public static event Action<int>? OnKeyPressed;
            public static event Action<int>? OnKeyReleased;

            private static Dictionary<int, KeyState> keyStates = new Dictionary<int, KeyState>();

            public static void TickKeys()
            {
                foreach (WinVirtualKeys kkey in Enum.GetValues(typeof(WinVirtualKeys)))
                {
                    int key = (int)kkey;
                    bool keyPressedNow;
                    switch(Environment.OSVersion.Platform)
                    {
                        case PlatformID.Unix:
                            return; //TODO: FIX ME
                            break;
                        case PlatformID.Win32NT:
                            short keyState = GetAsyncKeyState((int)key);
                            keyPressedNow = ((keyState >> 15) & 0x0001) == 0x0001;
                            break;
                        default:
                        case PlatformID.Other:
                            return; //can't handle this
                    }
                    //bool keyPressedRecently = ((keyState >> 0) & 0x0001) == 0x0001;
                    //decay
                    if (keyStates[key] == KeyState.Pressed && keyPressedNow)
                    {
                        keyStates[key] = KeyState.Held;
                    }
                    else if (keyStates[key] == KeyState.Held && !keyPressedNow)
                    {
                        keyStates[key] = KeyState.Released;
                        try
                        {
                            OnKeyReleased?.Invoke(key);
                        }
                        catch { }
                    }
                    else if (keyStates[key] == KeyState.Released)
                    {
                        keyStates[key] = KeyState.None;
                    }
                    //check pressed
                    if (keyPressedNow && keyStates[key] == KeyState.None) //pressed now
                    {
                        keyStates[key] = KeyState.Pressed;
                        try
                        {
                            OnKeyPressed?.Invoke(key);
                        }
                        catch { } //keeps accessing disposed settings menu because it can't unsub fast enough
                    }

                    //docs say this isn't perfectly reliable
                    //if (keyPressedRecently) //pressed between previous and current tick
                    //{
                    //    
                    //}
                }
            }
        }
#endregion

        public static ProxChat Instance = null!;

        [UI] private readonly Image settingsGearImage = ResizeImage(IOPath.Join("Images", "Gear.png"), 75, 75);
        [UI] private readonly Image connectImage = ResizeImage(IOPath.Join("Images", "connect.png"), 75, 75);
        [UI] private readonly Image disconnectImage = ResizeImage(IOPath.Join("Images", "disconnect.png"), 75, 75);
        [UI] private readonly Image teamImage = ResizeImage(IOPath.Join("Images", "team.png"), 75, 125);
        [UI] private readonly Image globalImage = ResizeImage(IOPath.Join("Images", "global.png"), 75, 125);

        [UI] private Button settingsButton;
        [UI] private Button connectDisconnectButton;
        [UI] private Button globalButton;
        [UI] private Button teamButton;
        [UI] private TextView connectionStatusRTB;
        [UI] private TextView identityLabelRTB;
        [UI] private ListBox userListBox;

        private object uiLock = new object();
        private Model model = null!;
        private Dictionary<long, bool> isDirectHeldDown = new Dictionary<long, bool>();
        private bool isTeamHeldDown = false;
        private bool isGlobalHeldDown = false;
        public Logger viewLogger = new Logger("UI");
        private ConcurrentQueue<SAction> messageQueue = new ConcurrentQueue<SAction>();
        private long? mainUserId = null;
        private Dictionary<long, LobbyMember> idToMember = new Dictionary<long, LobbyMember>();

        public ProxChat() : this(new Builder("MainWindow.glade")) { }

        private ProxChat(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);
            DeleteEvent += Window_DeleteEvent;

#region Logger Setup
            DateTime launchTime = DateTime.Now;
            Logger.AddLogHandler((source, level, text, _) =>
            {
                DateTime logtime = DateTime.Now;
                string data = Logger.PrefixNewLines(text, $"{{{logtime}}} {level} [{source}]");
                Directory.CreateDirectory("logs");
                string filename = IOPath.Combine("logs", $"log_{launchTime.Month}-{launchTime.Day}-{launchTime.Year}--{launchTime.Hour}-{launchTime.Minute}-{launchTime.Second}.txt");
                File.AppendAllText(filename, data);
            });
            #endregion

            #region Populate UI Fields
            Icon = new Gdk.Pixbuf(File.ReadAllBytes(IOPath.Combine("Images", "icon.ico")));
            settingsButton = (Button)builder.GetObject("settingsButton");
            connectDisconnectButton = (Button)builder.GetObject("connectDisconnectButton");
            connectionStatusRTB = (TextView)builder.GetObject("connectionStatusRTB");
            identityLabelRTB = (TextView)builder.GetObject("identityLabelRTB");
            userListBox = (ListBox)builder.GetObject("userListBox");
            
            connectionStatusRTB.Buffer = new TextBuffer(new TextTagTable());
            connectionStatusRTB.Show();
            identityLabelRTB.Buffer = new TextBuffer(new TextTagTable());
            identityLabelRTB.Show();

            settingsButton.Image = settingsGearImage;
            globalButton.Image = globalImage;
            teamButton.Image = teamImage;
            #endregion

            #region Settings of UI fields
            this.SetSizeRequest(700, 400); //set minimum size of main window
            settingsButton.SetSizeRequest(75, 75);
            settingsButton.Clicked += settingsButton_Click;
            connectDisconnectButton.SetSizeRequest(75, 75);
            connectDisconnectButton.Clicked += connectDisconnectButton_Click;
            this.AddEvents((int)Gdk.EventMask.ButtonPressMask);
            this.AddEvents((int)Gdk.EventMask.ButtonReleaseMask);
            globalButton.AddEvents((int)Gdk.EventMask.ButtonPressMask);
            globalButton.AddEvents((int)Gdk.EventMask.ButtonReleaseMask);
            teamButton.AddEvents((int)Gdk.EventMask.ButtonPressMask);
            teamButton.AddEvents((int)Gdk.EventMask.ButtonReleaseMask);
            globalButton.ButtonPressEvent += (_, _) =>
            {
                viewLogger.Info("Click");
                OnPressGlobalButton(true);
            };
            globalButton.ButtonReleaseEvent += (_, _) =>
            {
                viewLogger.Info("unClick");
                OnPressGlobalButton(false);
            };
            teamButton.ButtonPressEvent += (_, _) =>
            {
                OnPressTeamButton(true);
            };
            teamButton.ButtonReleaseEvent += (_, _) =>
            {
                OnPressTeamButton(false);
            };
#pragma warning disable CS0612 //This may be obsolete, but what is the new way to change font??????????
            connectionStatusRTB.OverrideBackgroundColor(StateFlags.Normal, StyleContext.GetBackgroundColor(StateFlags.Normal));
            identityLabelRTB.OverrideBackgroundColor(StateFlags.Normal, StyleContext.GetBackgroundColor(StateFlags.Normal));
            userListBox.OverrideBackgroundColor(StateFlags.Normal, StyleContext.GetBackgroundColor(StateFlags.Normal));
            connectionStatusRTB.ModifyFont(Pango.FontDescription.FromString("monospace 20"));
            identityLabelRTB.ModifyFont(Pango.FontDescription.FromString("monospace 13"));
#pragma warning restore CS0612
            userListBox.SelectionMode = SelectionMode.None;
            //userListBox.focus
#endregion

            SetConnectionStatus(false);
            SetCDCButton(true);
            SetIdentity("", "");
            Instance = this;

            #region Critical Settings
            if (Settings.Instance.DiscordAppID == null)
            {
                ResponseType? res = null;
                while (res != ResponseType.Ok)
                {
                    TextPopup popup = new TextPopup()
                    {
                        InfoText = $"Enter the discord Application ID{(res == null ? "" : " (This is required)")}:",
                        LabelText = "Enter the discord App ID"
                    };
                    res = popup.ShowDialog(this);
                    if (res == ResponseType.Ok && long.TryParse(popup.InfoResult, out long did))
                    {
                        Settings.Instance.DiscordAppID = did;
                        Settings.SaveSettings();
                    }
                    else if (res == ResponseType.Reject)
                    {
                        Drawn += (_, _) => { Application.Quit(); };
                        return;
                    }
                }
            }
            if (Settings.Instance.ServerHost == null)
            {
                ResponseType? res = null;
                while (res != ResponseType.Ok)
                {
                    TextPopup popup = new TextPopup()
                    {
                        InfoText = $"Enter the hosname (or IP address) of the server{(res == null ? "" : " (This is required)")}:",
                        LabelText = "Enter the Hostname"
                    };
                    res = popup.ShowDialog(this);
                    if (res == ResponseType.Ok)
                    {
                        Settings.Instance.ServerHost = popup.InfoResult;
                        Settings.SaveSettings();
                    }
                    else if (res == ResponseType.Reject)
                    {
                        Drawn += (_, _) => { Application.Quit(); };
                        return;
                    }
                }
            }
            if (Settings.Instance.IngameName == null)
            {
                ResponseType? res = null;
                while (res != ResponseType.Ok)
                {
                    TextPopup popup = new TextPopup()
                    {
                        InfoText = $"Enter your SMO Online In-Game username{(res == null ? "" : " (This is required)")}:",
                        LabelText = "Enter your Username"
                    };
                    res = popup.ShowDialog(this);
                    if (res == ResponseType.Ok)
                    {
                        Settings.Instance.IngameName = popup.InfoResult;
                        Settings.SaveSettings();
                    }
                    else if (res == ResponseType.Reject)
                    {
                        Drawn += (_, _) => { Application.Quit(); };
                        return;
                    }
                }
            }
            #endregion

            #region Keybind Subs
            KeyService.OnKeyPressed += (int key) =>
            {
                if (key == Settings.Instance.ToggleDeafen)
                {
                    if (mainUserId != null)
                    {
                        var lm = GetLobbyMemberUI(mainUserId!.Value);
                        if (lm != null)
                        {
                            lm.Deaf = !lm.Deaf;
                        }
                        else
                        {
                            viewLogger.Warn("Couldn't deafen the user via keybind because their UI isn't loaded yet!");
                        }
                    }
                    else
                    {
                        viewLogger.Warn("Couldn't deafen the user via keybind because they aren't loaded yet!");
                    }
                }
                else if (key == Settings.Instance.PushToTeam)
                {
                    OnPressTeamButton(true);
                }
                else if (key == Settings.Instance.PushToGlobal)
                {
                    OnPressGlobalButton(true);
                }
                else if (key == Settings.Instance.SpeakAction)
                {
                    switch (Settings.Instance.SpeakMode)
                    {
                        case "Always On":
                            {
                                if (mainUserId != null)
                                {
                                    var lm = GetLobbyMemberUI(mainUserId!.Value);
                                    if (lm != null)
                                        lm.Muted = !lm.Muted;
                                    else
                                        viewLogger.Warn("Attempted to toggle the mute of the main user, but that user doesn't exist in the UI!");
                                }
                                else
                                {
                                    viewLogger.Warn("Couldn't mute the user via keybind because they aren't loaded yet!");
                                }
                            }
                            break;
                        case "Push-To-Talk":
                            {
                                SetSelfMute(false);
                            }
                            break;
                        case "Push-To-Mute":
                            {
                                SetSelfMute(true);
                            }
                            break;
                    }
                }
            };
            KeyService.OnKeyReleased += (int key) =>
            {
                /*//if (key == Settings.Instance.ToggleDeafen)
                //{
                //    //do nothing
                //}
                //else */
                if (key == Settings.Instance.PushToTeam)
                {
                    OnPressTeamButton(false);
                }
                else if (key == Settings.Instance.PushToGlobal)
                {
                    OnPressGlobalButton(false);
                }
                else if (key == Settings.Instance.SpeakAction)
                {
                    switch (Settings.Instance.SpeakMode)
                    {
                        case "Push-To-Talk":
                            {
                                SetSelfMute(true);
                            }
                            break;
                        case "Push-To-Mute":
                            {
                                SetSelfMute(false);
                            }
                            break;
                    }
                }
            };
            #endregion

            model = new Model(Settings.Instance.IngameName!);
            Task.Run(() =>
            {
                try
                {
                    System.Timers.Timer walkieTimer = new System.Timers.Timer(200) { AutoReset = true }; //5 per second (seems resonable)
                    walkieTimer.Start();
                    walkieTimer.Elapsed += (_, _) =>
                    {
                        lock (uiLock)
                        {
                            if (isGlobalHeldDown)
                            {
                                model.AddMessage(() =>
                                {
                                    model.SendWalkieTalkiePacket(null, false);
                                });
                            }
                            else if (isTeamHeldDown)
                            {
                                model.AddMessage(() =>
                                {
                                    model.SendWalkieTalkiePacket(null, true);
                                });
                            }
                            else
                            {
                                foreach (var user in isDirectHeldDown)
                                {
                                    if (user.Value)
                                    {
                                        model.AddMessage(() =>
                                        {
                                            model.SendWalkieTalkiePacket(user.Key, true);
                                        });
                                        break; //should only be one true in the collection
                                    }
                                }
                            }
                        }
                    };
                    //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    //sw.Start();
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    const int frameTime = 50; //20fps
                    int waitTime = 20;//how much time can be waited
                    while (true)
                    {
                        Thread.Sleep(waitTime);
                        sw.Restart();
                        lock (uiLock)
                        {
                            while (messageQueue.Count > 0)
                            {
                                if (messageQueue.TryDequeue(out SAction? action))
                                {
                                    Invoke(() => action?.Invoke());
                                }
                            }
                            KeyService.TickKeys();
                        }
                        sw.Stop();
                        waitTime = frameTime - (int)sw.ElapsedMilliseconds;
                        waitTime = waitTime < 0 ? 0 : waitTime;
                    }
                }
                catch (Exception ex)
                {
                    viewLogger.Error(ex.ToString());
                }
            });
        }

#region Set UI
        public void SetConnectionStatus(bool isConnected)
        {
            connectionStatusRTB.Buffer.Text = "";
            TextBuffer buf = connectionStatusRTB.Buffer;
            TextIter itr = buf.EndIter;
            if (isConnected)
            {
                buf.InsertMarkup(ref itr, "Status: <span color=\"green\">Connected</span>");
            }
            else
            {
                buf.InsertMarkup(ref itr, "Status: <span color=\"red\">Disconnected</span>");
            }
        }

        public void SetIdentity(string discord, string ingame)
        {
            identityLabelRTB.Buffer.Text = "";
            TextBuffer buf = identityLabelRTB.Buffer;
            TextIter itr = buf.EndIter;
            buf.InsertMarkup(ref itr, $"<span color=\"dark blue\">Discord:</span> {discord}\n<span color=\"dark blue\">In-Game:</span> {ingame}");
        }

        public void SetCDCButton(bool connect)
        {
            if (connect)
            {
                connectDisconnectButton.Image = connectImage;
                //try
                //{
                //    connectDisconnectButton.BackgroundImage = connectImage;
                //    connectDisconnectButton.Text = "";
                //}
                //catch (Exception e)
                //{
                //    viewLogger.Warn("Couldn't set the connect image for the button: " + e.ToString());
                //    connectDisconnectButton.Text = "connect";
                //}
            }
            else
            {
                connectDisconnectButton.Image = disconnectImage;
                //try
                //{
                //    connectDisconnectButton.BackgroundImage = disconnectImage;
                //    connectDisconnectButton.Text = "";
                //}
                //catch (Exception e)
                //{
                //    viewLogger.Warn("Couldn't set the disconnect image for the button: " + e.ToString());
                //    connectDisconnectButton.Text = "disconnect";
                //}
            }
        }

        public void SetCDCButtonEnabled(bool enabled)
        {
            connectDisconnectButton.Sensitive = enabled;
        }

        public void AddMemberToList(long userId, string username, bool isSelf = false)
        {
            try
            {
                var lm = GetLobbyMemberUI(userId);
                if (lm == null)
                {
                    mainUserId = isSelf ? userId : mainUserId;
                    lm = new LobbyMember(isSelf,
                        (muted) => OnMuteChange(userId, muted),
                        (deaf) => OnDeafChange(deaf),
                        (bool wasPressed) => OnPressDirectButton(userId, wasPressed),
                        (byte vol) => OnChangeVolume(username, vol),
                        Settings.Instance.VolumePrefs![username]);
                    idToMember[userId] = lm;
                    lm.SetUserInfo(username, userId);
                    if (isSelf)
                    {
                        userListBox.Insert(lm.lbr, 0);
                        SetIdentity(username, Settings.Instance.IngameName!);
                    }
                    else
                        userListBox.Add(lm.lbr);
                }
                else
                {
                    viewLogger.Warn("Attempt to add the same user to the UI more than once was made!");
                }
            }
            catch (Exception ex)
            {
                viewLogger.Error("Could not add a user to the list: " + ex.ToString());
            }
        }

        public void RemoveMemberFromList(long userId)
        {
            try
            {
                var lm = GetLobbyMemberUI(userId);
                if (lm != null)
                {
                    userListBox.Remove(lm.lbr);
                    lm.DisposeUserImages();
                    lm.lbr.Destroy();
                    idToMember.Remove(userId);

                    //int row = userTablePanel.GetRow(lm);
                    //userTablePanel.Controls.Remove(lm);
                    //lm.SetDeafButtonCallback(null);
                    //lm.SetMuteButtonCallback(null);
                    //lm.SetDirectButtonCallback(null);
                    //lm.SetVolumeSliderCallback(null);
                    //lm.DisposeUserImages();
                    //lm.Dispose();
                    //isDirectHeldDown.Remove(userId);
                    //for (int i = row; i < userTablePanel.Controls.Count; i++)
                    //{
                    //    //long userIdOfEntryBelow = clientIdToDisplayIndex.First(x => x.Value == i + 1).Key;
                    //    LobbyMember lmn = (LobbyMember)userTablePanel.GetControlFromPosition(0, i + 1);
                    //    lmn.TabIndex--;
                    //    userTablePanel.Controls.Remove(lmn);
                    //    if (lmn.UserId != null)
                    //        OnPressDirectButton(lmn.UserId.Value, false);
                    //    userTablePanel.Controls.Add(lmn, 0, i);
                    //}
                    //userTablePanel.RowCount--;
                }
                else
                {
                    viewLogger.Warn("Attempt to remove the user " + userId + " from the table, but they weren't present.");
                }
            }
            catch (Exception ex)
            {
                viewLogger.Error("Could not remove a user from the list: " + ex.ToString());
            }
        }

        public void SetUserImage(long userId, uint width, uint height, byte[] imageData, byte[] highlightedImageData)
        {
            try
            {
                var lm = GetLobbyMemberUI(userId);
                if (lm != null)
                {
                    Image normal;
                    Gdk.Pixbuf pbn = new Gdk.Pixbuf(imageData, true, 8, (int)width, (int)height, (int)width * 4);
                    normal = new Image(pbn);

                    Image high;
                    Gdk.Pixbuf pbh = new Gdk.Pixbuf(highlightedImageData, true, 8, (int)width, (int)height, (int)width * 4);
                    high = new Image(pbh);
                    //Bitmap normal;
                    //{
                    //    normal = new Bitmap((int)width, (int)height, PixelFormat.Format32bppArgb);
                    //    BitmapData bData = normal.LockBits(new Rectangle(0, 0, (int)width, (int)height), ImageLockMode.WriteOnly, normal.PixelFormat);
                    //    IntPtr ptr = bData.Scan0;
                    //    Marshal.Copy(imageData, 0, ptr, imageData.Length);
                    //    normal.UnlockBits(bData);
                    //}
                    //Bitmap high;
                    //{
                    //    high = new Bitmap((int)width, (int)height, PixelFormat.Format32bppArgb);
                    //    BitmapData bData = high.LockBits(new Rectangle(0, 0, (int)width, (int)height), ImageLockMode.WriteOnly, high.PixelFormat);
                    //    IntPtr ptr = bData.Scan0;
                    //    Marshal.Copy(highlightedImageData, 0, ptr, highlightedImageData.Length);
                    //    high.UnlockBits(bData);
                    //}

                    lm.SetUserImages(normal, high);
                }
                else
                {
                    viewLogger.Warn("Tried to set an image for a user that wasn't present in the UI, maybe they disconnected really fast?");
                }
            }
            catch (Exception e)
            {
                viewLogger.Warn("Issue deserializing image: " + e.ToString());
            }
        }

        public void SetUserTalkingHighlighted(long userId, bool talking)
        {
            try
            {
                var lm = GetLobbyMemberUI(userId);
                if (lm != null)
                {
                    lm.SetUserTalking(talking);
                }
                else
                {
                    viewLogger.Warn($"Can't set {userId} to be highlighted/unhighlighted because they aren't in the user table.");
                }
            }
            catch (Exception ex)
            {
                viewLogger.Warn($"Tried to highlight/unhighlight {userId} from talking and something went wrong." + ex.ToString());
            }
        }

        public LobbyMember? GetLobbyMemberUI(long userId)
        {
            return idToMember.ContainsKey(userId) ? idToMember[userId] : null;
            //return userTablePanel.Controls.OfType<LobbyMember>().FirstOrDefault(x => x.UserId == userId);
        }

        public void SetPercievedVolume(long userId, float percent)
        {
            var lm = GetLobbyMemberUI(userId);
            if (lm != null)
            {
                lm.SetPercievedVolumeLevel(percent);
            }
            else
            {
                viewLogger.Warn("Attempt to change percieved volume of user not present in the userTablePanel");
            }
        }

        public void SetPercievedVolumeVisible(bool visible)
        {
            foreach (var lm in idToMember)
            {
                lm.Value.SetPercievedVolumeVisible(visible);
            }
        }

        public void SetSelfMute(bool muted)
        {
            if (mainUserId != null)
            {
                //int row = clientIdToDisplayIndex[mainUserId.Value];
                //var lm = userTablePanel.Controls.OfType<LobbyMember>().First(x => userTablePanel.GetRow(x) == row);
                var lm = GetLobbyMemberUI(mainUserId!.Value);
                if (lm != null)
                {
                    lm.Muted = muted;
                }
                else
                {
                    viewLogger.Warn("Tried to mute the user via PTT or PTM, but the UI element wasn't present.");
                }
            }
            else
            {
                viewLogger.Warn("Tried to mute the user via PTT or PTM, the mainUserId was null.");
            }
        }

        private void OnDeafChange(bool deaf)
        {
            model.AddMessage(() =>
            {
                model.SetDeaf(deaf);
            });
        }

        private void OnMuteChange(long userId, bool muted)
        {
            model.AddMessage(() =>
            {
                model.SetMute(userId, muted);
            });
        }

        private void OnChangeVolume(string username, byte newVolume)
        {
            Settings.Instance.VolumePrefs![username] = newVolume;
            Settings.SaveSettings();
            model.AddMessage(() =>
            {
                model.RecalculateRealVolume(username, newVolume);
            });
        }

        private void OnPressDirectButton(long userId, bool wasPressed)
        {
            isDirectHeldDown[userId] = wasPressed;
        }

        public string? PromptForLobbySecret()
        {
            string? secret = null;
            ResponseType? res = null;
            while (res != ResponseType.Ok)
            {
                TextPopup popup = new TextPopup()
                {
                    InfoText = $"Enter the discord lobby secret (Ask server host){(res == null ? "" : " (This is required)")}:",
                    LabelText = "Enter the Lobby Secret"
                };
                res = popup.ShowDialog(this);
                if (res == ResponseType.Ok)
                {
                    secret = popup.InfoResult;
                }
                else if (res == ResponseType.Reject)
                {
                    break; //"secret" gonna stay null
                }
            }
            return secret;
        }

        private void OnPressTeamButton(bool wasPressed)
        {
            if (wasPressed)
            {
                if (!isTeamHeldDown && !isGlobalHeldDown && isDirectHeldDown.All(x => !x.Value))
                {
                    isTeamHeldDown = true;
                }
            }
            else
            {
                isTeamHeldDown = false;
            }
        }

        private void OnPressGlobalButton(bool wasPressed)
        {
            if (wasPressed)
            {
                if (!isTeamHeldDown && !isGlobalHeldDown && isDirectHeldDown.All(x => !x.Value))
                {
                    isGlobalHeldDown = true;
                }
            }
            else
            {
                isGlobalHeldDown = false;
            }
        }
        #endregion

        #region Callbacks
        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
        }

        private void settingsButton_Click(object sender, EventArgs e)
        {
            SetCDCButtonEnabled(false);
            SettingsUI sett = new SettingsUI();
            sett.serverWasRunningUponOpening = model.IsConnectedToServer();
            sett.Show(this);
        }

        private void connectDisconnectButton_Click(object sender, EventArgs e)
        {
            SetCDCButtonEnabled(false); //disable connect button so no tomfoolery occurs during connect/disconnect
            model.AddMessage(() =>
            {
                model.ConnectToServer(Settings.Instance.ServerHost!, Settings.Instance.ServerPort.ToString()!);
            });
        }
        #endregion

        #region ModelIO
        public void AddMessage(SAction action)
        {
            messageQueue.Enqueue(action);
        }

        private void Invoke(SAction action)
        {
            ManualResetEventSlim rs = new ManualResetEventSlim();
            Application.Invoke((s, a) =>
            {
                action?.Invoke();
                rs.Set();
            });
            rs.Wait();
        }
        #endregion

        #region Utility
        public static Image ResizeImage(string path, int width, int height)
        {
            Gdk.Pixbuf pixbuf = new Gdk.Pixbuf(path);
            pixbuf = pixbuf.ScaleSimple(width, height, Gdk.InterpType.Bilinear);
            Image img = new Image(pixbuf);
            img.ShowAll();
            return img;
        }

        public static void ResizeImage(ref Image image, int width, int height)
        {
            Gdk.Pixbuf pixbuf = image.Pixbuf.Copy();
            pixbuf = pixbuf.ScaleSimple(width, height, Gdk.InterpType.Bilinear);
            image = new Image(pixbuf);
            image.ShowAll();
        }
        #endregion
    }
}