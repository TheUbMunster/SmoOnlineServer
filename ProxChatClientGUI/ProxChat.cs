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

        Dictionary<long, int> clientIdToDisplayIndex = new Dictionary<long, int>();
        //Dictionary<long, string> idToName = new Dictionary<long, string>();

        private List<IDisposable> toDispose = new List<IDisposable>();

        private Model model = null!;


        public ProxChat()
        {
            InitializeComponent();
            try
            {
                settingsButton.BackgroundImage = gearImage;
                settingsButton.BackgroundImageLayout = ImageLayout.Zoom;
                settingsButton.Text = "";
            } catch { }
            try
            {
                teamButton.BackgroundImage = teamImage;
                teamButton.BackgroundImageLayout = ImageLayout.Zoom;
                teamButton.Text = "";
            } catch { }
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

            //start the model
            model = new Model(Settings.Instance.IngameName!);
            Task.Run(() =>
            {
                try
                {
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
                        }
                        KeyService.TickKeys();
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
            if (!clientIdToDisplayIndex.ContainsKey(userId))
            {
                LobbyMember lm = new LobbyMember();
                lm.SetDeafButtonCallback(() => OnPressDeafenButton(userId));
                lm.SetMuteButtonCallback(() => OnPressMuteButton(userId));
                lm.SetDirectButtonCallback((bool wasPressed) => OnPressDirectButton(userId, wasPressed));
                lm.SetVolumeSliderCallback((byte vol) => OnChangeVolume(userId, vol));
                lm.SetUsername(username);
                if (isSelf)
                    lm.RemoveSelfUI();
                else
                    lm.RemoveOtherUI();
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
                    clientIdToDisplayIndex[userId] = clientIdToDisplayIndex.Count;
                    userTablePanel.Controls.Add(lm, 0, clientIdToDisplayIndex.Count);
                }
            }
        }

        public void RemoveMemberFromList(long userId)
        {
            if (clientIdToDisplayIndex.ContainsKey(userId))
            {
                int row = clientIdToDisplayIndex[userId];
                var lm = userTablePanel.Controls.OfType<LobbyMember>().First(x => userTablePanel.GetRow(x) == row);
                if (lm != null)
                {
                    userTablePanel.Controls.Remove(lm);
                    lm.Dispose();
                    //TODO: SHUFFLE USERS UP TO FILL THE GAP
                }
            }
        }

        public void SetUserImage(long userId, uint size, byte[] imageData)
        {
            if (clientIdToDisplayIndex.ContainsKey(userId))
            {
                try
                {
                    Bitmap output = new Bitmap((int)size, (int)size, PixelFormat.Format32bppArgb);
                    BitmapData bData = output.LockBits(new Rectangle(0, 0, (int)size, (int)size), ImageLockMode.WriteOnly, output.PixelFormat);
                    IntPtr ptr = bData.Scan0;
                    Marshal.Copy(imageData, 0, ptr, imageData.Length);
                    output.UnlockBits(bData);
                    int row = clientIdToDisplayIndex[userId];
                    var lm = userTablePanel.Controls.OfType<LobbyMember>().First(x => userTablePanel.GetRow(x) == row);
                    toDispose.Add(output);
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

        private void OnPressDeafenButton(long userId)
        {

        }

        private void OnPressMuteButton(long userId)
        {

        }

        private void OnChangeVolume(long userId, byte newVolume)
        {

        }

        private void OnPressDirectButton(long userId, bool wasPressed)
        {

        }

        //call this as voiceprox vols change. (should be 100% if voiceprox is off).
        public void PercievedVolumeChange(long userId, float percent)
        {
            if (clientIdToDisplayIndex.ContainsKey(userId))
            {
                int row = clientIdToDisplayIndex[userId];
                var lm = userTablePanel.Controls.OfType<LobbyMember>().First(x => userTablePanel.GetRow(x) == row);
                lm.SetVolumeDisplaySlider(percent);
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

        ~ProxChat()
        {
            foreach (var elem in toDispose)
            {
                elem?.Dispose();
            }
        }

        private void settingsButton_Click(object sender, EventArgs e)
        {
            new SettingsUI().Show(this);
        }

        private void connectDisconnectButton_Click(object sender, EventArgs e)
        {
            model.ConnectToServer(Settings.Instance.ServerHost!, Settings.Instance.ServerPort.ToString()!);
        }
    }
}