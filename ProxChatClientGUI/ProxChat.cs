using System;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using Shared;

namespace ProxChatClientGUI
{
    public partial class ProxChat : Form
    {
        private static readonly Image gearImage = Image.FromFile("Images\\Gear.png");
        private static readonly Image teamImage = Image.FromFile("Images\\team.png");
        private static readonly Image globalImage = Image.FromFile("Images\\global.png");

        public static ProxChat Instance = null!;
        private object uiLock = new object();
        private Queue<Action> messageQueue = new Queue<Action>();

        private Logger viewLogger = new Logger("UI");

        Dictionary<long, int> clientIdToDisplayIndex = new Dictionary<long, int>();

        private List<IDisposable> toDispose = new List<IDisposable>();

        private Model model;


        public ProxChat()
        {
            InitializeComponent();
            try
            {
                settingsButton.BackgroundImage = gearImage;
                settingsButton.BackgroundImageLayout = ImageLayout.Stretch;
                settingsButton.Text = "";
            } catch { }
            try
            {
                teamButton.BackgroundImage = teamImage;
                teamButton.BackgroundImageLayout = ImageLayout.Stretch;
                teamButton.Text = "";
            } catch { }
            try
            {
                globalButton.BackgroundImage = globalImage;
                globalButton.BackgroundImageLayout = ImageLayout.Stretch;
                globalButton.Text = "";
            }
            catch { }
            SetConnectionStatus(false);
            SetIdentity("", "");
            Instance = this;
            model = new Model();
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
        #endregion

        public void AddMemberToList(long userId, bool isSelf = false)
        {
            if (!clientIdToDisplayIndex.ContainsKey(userId))
            {
                LobbyMember lm = new LobbyMember();
                lm.SetDeafButtonCallback(() => OnPressDeafenButton(userId));
                lm.SetMuteButtonCallback(() => OnPressMuteButton(userId));
                lm.SetDirectButtonCallback((bool wasPressed) => OnPressDirectButton(userId, wasPressed));
                lm.SetVolumeSliderCallback((byte vol) => OnChangeVolume(userId, vol));
                if (isSelf)
                    lm.RemoveSelfUI();
                else
                    lm.RemoveOtherUI();
                lm.Dock = DockStyle.Fill;
                if (clientIdToDisplayIndex.Count == 0)
                {
                    userTablePanel.Controls.Add(lm, 0, 0);
                    clientIdToDisplayIndex[userId] = 0;
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
        private void OnPercievedVolumeChange(long userId, float percent)
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

        private Image ModelGetUserImage(long userId)
        {
            throw new NotImplementedException();
        }

        public void AddMessage(Action action)
        {
            lock (uiLock)
            {
                messageQueue.Enqueue(action);
            }
        }

        //keybinds:
        /*
         * push-to-talk
         * push-to-team
         * push-to-global
         * 
         * toggle/push-to-(action)
         *      deafen
         *      mute
         */
        ~ProxChat()
        {
            foreach (var elem in toDispose)
            {
                elem?.Dispose();
            }
        }
    }
}