using System;
using System.Drawing.Imaging;
using Shared;

namespace ProxChatClientGUI
{
    public partial class ProxChat : Form
    {
        public static ProxChat Instance = null!;
        private object uiLock = new object();
        private Queue<Action> messageQueue = new Queue<Action>();

        private Logger viewLogger = new Logger("UI");

        Dictionary<long, int> clientIdToDisplayIndex = new Dictionary<long, int>();

        private Model model;


        public ProxChat()
        {
            InitializeComponent();
            try
            {
                settingsButton.BackgroundImage = Image.FromFile("Images\\Gear.png");
                settingsButton.BackgroundImageLayout = ImageLayout.Stretch;
                settingsButton.Text = "";
            }
            catch { }
            SetConnectionStatus(false);
            SetIdentity("bananaman#1234", "haberdashery");
            AddSelfToList();
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

        private void AddSelfToList()
        {
            LobbyMember lm = new LobbyMember();
            lm.Dock = DockStyle.Fill;
            userTablePanel.Controls.Add(lm, 0, 0);

        }

        private void AddMemberToList()
        {

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
    }
}