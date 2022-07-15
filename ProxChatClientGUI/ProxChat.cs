using System;
using System.Drawing.Imaging;

namespace ProxChatClientGUI
{
    public partial class ProxChat : Form
    {
        Dictionary<long, int> clientIdToDisplayIndex = new Dictionary<long, int>();

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
    }
}