using System;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using Shared;

namespace ProxChatClientGUI
{
    public partial class ProxChat : Form
    {
        #region WinAPI
        public static class KeyService
        {
            static KeyService()
            {
                foreach (Keys key in Enum.GetValues(typeof(Keys)))
                {
                    keyStates[key] = KeyState.None;
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

            public static event Action<Keys>? OnKeyPressed;
            public static event Action<Keys>? OnKeyReleased;

            private static Dictionary<Keys, KeyState> keyStates = new Dictionary<Keys, KeyState>();

            public static void TickKeys()
            {
                foreach (Keys key in Enum.GetValues(typeof(Keys)))
                {
                    short keyState = GetAsyncKeyState((int)key);
                    bool keyPressedNow = ((keyState >> 15) & 0x0001) == 0x0001;
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

        private static readonly Image gearImage = Image.FromFile("Images\\Gear.png");
        private static readonly Image teamImage = Image.FromFile("Images\\team.png");
        private static readonly Image globalImage = Image.FromFile("Images\\global.png");
        private static readonly Image connectImage = Image.FromFile("Images\\connect.png");
        private static readonly Image disconnectImage = Image.FromFile("Images\\disconnect.png");

        public static ProxChat Instance = null!;
        private object uiLock = new object();
        private Queue<Action> messageQueue = new Queue<Action>();

        private Logger viewLogger = new Logger("UI");

        private long? mainUserId = null;
        private Dictionary<long, int> clientIdToDisplayIndex = new Dictionary<long, int>();
        private Dictionary<long, bool> isDirectHeldDown = new Dictionary<long, bool>();
        private bool isTeamHeldDown = false;
        private bool isGlobalHeldDown = false;

        //private List<IDisposable> toDispose = new List<IDisposable>();

        private Model model = null!;


        public ProxChat()
        {
            DateTime launchTime = DateTime.Now;
            Logger.AddLogHandler((source, level, text, _) =>
            {
                DateTime logtime = DateTime.Now;
                string data = Logger.PrefixNewLines(text, $"{{{logtime}}} {level} [{source}]");
                File.AppendAllText($"log_{launchTime.Month}-{launchTime.Day}-{launchTime.Year}--{launchTime.Hour}-{launchTime.Minute}-{launchTime.Second}.txt", data);
            });
            InitializeComponent();
            var font = new System.Drawing.Text.PrivateFontCollection();
            font.AddFontFile("Fonts\\RobotoMono-VariableFont_wght.ttf");
            connectionStatusRTB.Font = new Font(Font.FontFamily, 26f, FontStyle.Bold, GraphicsUnit.Pixel);
            identityLabel.Font = new Font(Font.FontFamily, 12f, FontStyle.Regular, GraphicsUnit.Pixel);
            try
            {
                settingsButton.BackgroundImage = gearImage;
                settingsButton.BackgroundImageLayout = ImageLayout.Zoom;
                settingsButton.Text = "";
            }
            catch { }
            try
            {
                teamButton.BackgroundImage = teamImage;
                teamButton.BackgroundImageLayout = ImageLayout.Zoom;
                teamButton.Text = "";
            }
            catch { }
            try
            {
                globalButton.BackgroundImage = globalImage;
                globalButton.BackgroundImageLayout = ImageLayout.Zoom;
                globalButton.Text = "";
            }
            catch { }
            connectDisconnectButton.BackgroundImageLayout = ImageLayout.Zoom;
            SetConnectionStatus(false);
            SetCDCButton(true);
            SetIdentity("", "");
            Instance = this;

            #region Critical Settings
            if (Settings.Instance.ServerHost == null)
            {
                DialogResult? res = null;
                while (res != DialogResult.OK)
                {
                    TextPopup popup = new TextPopup()
                    {
                        InfoText = $"Enter the hosname (or IP address) of the server{(res == null ? "" : " (This is required)")}:",
                        LabelText = "Enter the Hostname"
                    };
                    res = popup.ShowDialog(this);
                    if (res == DialogResult.OK)
                    {
                        Settings.Instance.ServerHost = popup.InfoResult;
                        Settings.SaveSettings();
                    }
                    else if (res == DialogResult.Cancel)
                    {
                        Load += (_, _) => Close();
                        return;
                    }
                }
            }
            if (Settings.Instance.IngameName == null)
            {
                DialogResult? res = null;
                while (res != DialogResult.OK)
                {
                    TextPopup popup = new TextPopup()
                    {
                        InfoText = $"Enter your SMO Online In-Game username{(res == null ? "" : "(This is required)")}:",
                        LabelText = "Enter your Username"
                    };
                    res = popup.ShowDialog(this);
                    if (res == DialogResult.OK)
                    {
                        Settings.Instance.IngameName = popup.InfoResult;
                        Settings.SaveSettings();
                    }
                    else if (res == DialogResult.Cancel)
                    {
                        Load += (_, _) => Close();
                        return;
                    }
                }
            }
            #endregion

            #region Keybind Subs
            KeyService.OnKeyPressed += (Keys key) =>
            {
                if (key == Settings.Instance.ToggleDeafen)
                {
                    if (mainUserId != null)
                    {
                        int row = clientIdToDisplayIndex[mainUserId.Value];
                        var lm = userTablePanel.Controls.OfType<LobbyMember>().First(x => userTablePanel.GetRow(x) == row);
                        lm.Deaf = !lm.Deaf;
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
                                    int row = clientIdToDisplayIndex[mainUserId.Value];
                                    var lm = userTablePanel.Controls.OfType<LobbyMember>().First(x => userTablePanel.GetRow(x) == row);
                                    lm.Muted = !lm.Muted;
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
            KeyService.OnKeyReleased += (Keys key) =>
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

            //start the model
            model = new Model(Settings.Instance.IngameName!);
            Task.Run(() =>
            {
                try
                {
                    System.Timers.Timer walkieTimer = new System.Timers.Timer(200) { AutoReset = true }; //5 per second (seems resonable)
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
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    sw.Start();
                    while (true)
                    {
                        lock (uiLock)
                        {
                            while (messageQueue.Count > 0)
                            {
                                Invoke(messageQueue.Dequeue());
                            }
                            KeyService.TickKeys();
                        }
                        //TODO: fix busy wait
                        while (sw.ElapsedMilliseconds < 16) { } //this busy wait allows some time for people to add more messages.
                        sw.Restart();
                    }
                }
                catch (Exception ex)
                {
                    viewLogger.Error(ex.ToString());
                }
            });
        }

        #region Set UI
        public void SetIdentity(string discord, string ingame)
        {
            identityLabel.Text = $"Discord: {discord}\nIn-Game: {ingame}";
        }

        public void SetConnectionStatus(bool isConnected)
        {
            connectionStatusRTB.Clear();
            if (isConnected)
            {
                connectionStatusRTB.SelectionColor = Color.Black;
                connectionStatusRTB.AppendText("Status: ");
                connectionStatusRTB.SelectionColor = Color.Green;
                connectionStatusRTB.AppendText("Connected");
            }
            else
            {
                connectionStatusRTB.SelectionColor = Color.Black;
                connectionStatusRTB.AppendText("Status: ");
                connectionStatusRTB.SelectionColor = Color.Red;
                connectionStatusRTB.AppendText("Disconnected");
            }
            connectionStatusRTB.SelectAll();
            connectionStatusRTB.SelectionAlignment = HorizontalAlignment.Center;
        }

        public void AddMemberToList(long userId, string username, bool isSelf = false)
        {
            try
            {
                if (!clientIdToDisplayIndex.ContainsKey(userId))
                {
                    mainUserId = isSelf ? userId : mainUserId;
                    LobbyMember lm = new LobbyMember();
                    lm.SetVolumeSlider(Settings.Instance.VolumePrefs![username]); //do before set callback
                    lm.SetMuteButtonCallback((muted) => OnMuteChange(userId, muted));
                    lm.SetVolumeSliderCallback((byte vol) => OnChangeVolume(username, vol));
                    lm.SetUsername(username);
                    if (isSelf)
                    {
                        lm.SetDeafButtonCallback((deaf) => OnDeafChange(deaf));
                        lm.RemoveSelfUI();
                        //SHUFFLE UP SELF
                    }
                    else
                    {
                        lm.SetDirectButtonCallback((bool wasPressed) => OnPressDirectButton(userId, wasPressed));
                        lm.RemoveOtherUI();
                    }
                    lm.Dock = DockStyle.Fill;
                    if (clientIdToDisplayIndex.Count == 0)
                    {
                        userTablePanel.Controls.Add(lm, 0, 0);
                        clientIdToDisplayIndex[userId] = 0;
                        SetIdentity(username, Settings.Instance.IngameName!);
                    }
                    else
                    {
                        userTablePanel.RowCount++;
                        var rs = new RowStyle(userTablePanel.RowStyles[0].SizeType, userTablePanel.RowStyles[0].Height);
                        userTablePanel.RowStyles.Add(rs);
                        userTablePanel.Controls.Add(lm, 0, clientIdToDisplayIndex.Count);
                        clientIdToDisplayIndex[userId] = clientIdToDisplayIndex.Count;
                    }
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
                if (clientIdToDisplayIndex.ContainsKey(userId))
                {
                    int row = clientIdToDisplayIndex[userId];
                    var lm = GetLobbyMemberUI(userId);
                    if (lm != null)
                    {
                        userTablePanel.Controls.Remove(lm);
                        lm.SetDeafButtonCallback(null);
                        lm.SetMuteButtonCallback(null);
                        lm.SetDirectButtonCallback(null);
                        lm.SetVolumeSliderCallback(null);
                        lm.DisposeUserImage();
                        lm.Dispose();
                        isDirectHeldDown.Remove(userId);
                        clientIdToDisplayIndex.Remove(userId);
                        if (clientIdToDisplayIndex.Count > 0)
                        {
                            for (int i = row; i < clientIdToDisplayIndex.Count; i++)
                            {
                                long userIdOfEntryBelow = clientIdToDisplayIndex.First(x => x.Value == i + 1).Key;
                                LobbyMember lmn = (LobbyMember)userTablePanel.GetControlFromPosition(0, i + 1);
                                userTablePanel.Controls.Remove(lmn);
                                OnPressDirectButton(userIdOfEntryBelow, false);
                                userTablePanel.Controls.Add(lmn, 0, i);
                            }
                            userTablePanel.RowCount--;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                viewLogger.Error("Could not remove a user from the list: " + ex.ToString());
            }
        }

        public void SetUserImage(long userId, uint width, uint height, byte[] imageData)
        {
            if (clientIdToDisplayIndex.ContainsKey(userId))
            {
                try
                {
                    Bitmap output = new Bitmap((int)width, (int)height, PixelFormat.Format32bppArgb);
                    BitmapData bData = output.LockBits(new Rectangle(0, 0, (int)width, (int)height), ImageLockMode.WriteOnly, output.PixelFormat);
                    IntPtr ptr = bData.Scan0;
                    Marshal.Copy(imageData, 0, ptr, imageData.Length);
                    output.UnlockBits(bData);
                    var lm = GetLobbyMemberUI(userId);
                    lm.SetUserImage(output);
                }
                catch (Exception e)
                {
                    viewLogger.Warn("Issue deserializing image: " + e.ToString());
                }
            }
            else
            {
                viewLogger.Warn("Tried to set an image for a user that wasn't present in the UI, maybe they disconnected really fast?");
            }
        }

        public LobbyMember GetLobbyMemberUI(long userId)
        {
            int row = clientIdToDisplayIndex[userId];
            return userTablePanel.Controls.OfType<LobbyMember>().First(x => userTablePanel.GetRow(x) == row);
        }

        public void SetCDCButton(bool connect = true)
        {
            if (connect)
            {
                try
                {
                    connectDisconnectButton.BackgroundImage = connectImage;
                    connectDisconnectButton.Text = "";
                }
                catch (Exception e)
                {
                    viewLogger.Warn("Couldn't set the connect image for the button: " + e.ToString());
                    connectDisconnectButton.Text = "connect";
                }
            }
            else
            {
                try
                {
                    connectDisconnectButton.BackgroundImage = disconnectImage;
                    connectDisconnectButton.Text = "";
                }
                catch (Exception e)
                {
                    viewLogger.Warn("Couldn't set the disconnect image for the button: " + e.ToString());
                    connectDisconnectButton.Text = "disconnect";
                }
            }
        }

        public void SetCDCButtonEnabled(bool enabled)
        {
            connectDisconnectButton.Enabled = enabled;
        }

        //public void SetUsernameForUID(long userId, string username)
        //{
        //    idToName[userId] = username;
        //}
        #endregion

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

        public string? PromptForLobbySecret()
        {
            string? secret = null;
            DialogResult? res = null;
            while (res != DialogResult.OK)
            {
                TextPopup popup = new TextPopup()
                {
                    InfoText = $"Enter the discord lobby secret (Ask server host){(res == null ? "" : " (This is required)")}:",
                    LabelText = "Enter the Lobby Secret"
                };
                res = popup.ShowDialog(this);
                if (res == DialogResult.OK)
                {
                    secret = popup.InfoResult;
                }
                else if (res == DialogResult.Cancel)
                {
                    break; //"secret" gonna stay null
                }
            }
            return secret;
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

        //switch these over to sending a packet every quarter second, and have the clients restore their prox volume if they havent received one in the last second or so
        private void OnPressDirectButton(long userId, bool wasPressed)
        {
            isDirectHeldDown[userId] = wasPressed;
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

        public void SetSelfMute(bool muted)
        {
            if (mainUserId != null)
            {
                int row = clientIdToDisplayIndex[mainUserId.Value];
                var lm = userTablePanel.Controls.OfType<LobbyMember>().First(x => userTablePanel.GetRow(x) == row);
                lm.Muted = muted;
            }
            else
            {
                viewLogger.Warn("Tried to mute the user via PTT or PTM, the mainUserId was null.");
            }
        }

        //call this as voiceprox vols change. (should be 100% if voiceprox is off).
        public void SetPercievedVolume(long userId, float percent)
        {
            if (clientIdToDisplayIndex.ContainsKey(userId))
            {
                int row = clientIdToDisplayIndex[userId];
                var lm = userTablePanel.Controls.OfType<LobbyMember>().First(x => userTablePanel.GetRow(x) == row);
                lm.SetPercievedVolumeLevel(percent);
            }
            else
            {
                viewLogger.Warn("Attempt to change percieved volume of user not present in the userTablePanel");
            }
        }

        public void AddMessage(Action action)
        {
            lock (uiLock)
            {
                messageQueue.Enqueue(action);
            }
        }

        //~ProxChat()
        //{
        //    foreach (var elem in toDispose)
        //    {
        //        elem?.Dispose();
        //    }
        //}

        private void settingsButton_Click(object sender, EventArgs e)
        {
            new SettingsUI().Show(this);
        }

        private void connectDisconnectButton_Click(object sender, EventArgs e)
        {
            SetCDCButtonEnabled(false); //disable connect button so no tomfoolery occurs during connect/disconnect
            model.AddMessage(() =>
            {
                model.ConnectToServer(Settings.Instance.ServerHost!, Settings.Instance.ServerPort.ToString()!);
            });
        }

        private void globalButton_OnMouseDown(object sender, MouseEventArgs e)
        {
            OnPressGlobalButton(true);
        }

        private void globalButton_OnMouseUp(object sender, MouseEventArgs e)
        {
            OnPressGlobalButton(false);
        }

        private void teamButton_OnMouseDown(object sender, MouseEventArgs e)
        {
            OnPressTeamButton(true);
        }

        private void teamButton_OnMouseUp(object sender, MouseEventArgs e)
        {
            OnPressTeamButton(false);
        }
    }
}