using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProxChatClientGUI
{
    public partial class LobbyMember : UserControl
    {
        private static Image crossedHeadphones = Image.FromFile("Images\\headphones-crossed.png");
        private static Image headphones = Image.FromFile("Images\\headphones.png");
        private static Image crossedMicrophone = Image.FromFile("Images\\mic-crossed.png");
        private static Image microphone = Image.FromFile("Images\\mic.png");
        private static Image walkieTalkie = Image.FromFile("Images\\direct.png");

        private Action<bool>? muteCallback; //true mute, false unmute
        private Action<bool>? deafCallback; //true deaf, false undeaf
        private Action<bool>? directCallback;
        private Action<byte>? volumeCallback;

        private bool muted = false;
        public bool Muted
        {
            get => muted;
            set
            {
                try
                {
                    if (value)
                    {
                        muteButton.BackgroundImage = crossedMicrophone;
                    }
                    else
                    {
                        muteButton.BackgroundImage = microphone;
                    }
                    if (value != muted)
                        muteCallback?.Invoke(value);
                    muteButton.Text = "";
                }
                catch
                {
                    muteButton.Text = muted ? "unmute" : "mute";
                }
                muted = value;
            }
        }

        private bool deaf = false;
        public bool Deaf
        {
            get => deaf;
            set
            {
                try
                {
                    if (value)
                    {
                        deafenButton.BackgroundImage = crossedHeadphones;
                    }
                    else
                    {
                        deafenButton.BackgroundImage = headphones;
                    }
                    if (value != deaf)
                        deafCallback?.Invoke(value);
                    deafenButton.Text = "";
                }
                catch
                {
                    deafenButton.Text = deaf ? "undeaf" : "deaf";
                }
                deaf = value;
            }
        }

        public LobbyMember()
        {
            InitializeComponent();
            var font = new System.Drawing.Text.PrivateFontCollection();
            font.AddFontFile("Fonts\\Teko-Bold.ttf");
            usernameLabel.Font = new Font(Font.FontFamily, 16f, FontStyle.Bold, GraphicsUnit.Pixel);
            muteButton.BackgroundImageLayout = ImageLayout.Stretch;
            deafenButton.BackgroundImageLayout = ImageLayout.Stretch;
            directSpeakButton.BackgroundImageLayout = ImageLayout.Stretch;
            MaximumSize = new Size(9999, Height);
            MinimumSize = new Size(0, Height);
            userPicture.BackgroundImageLayout = ImageLayout.Stretch;
            try
            {
                directSpeakButton.BackgroundImage = walkieTalkie;
                directSpeakButton.Text = "";
            }
            catch
            {
                directSpeakButton.Text = "direct";
            }
            Muted = false;
            Deaf = false;
        }

        #region Sets
        #region UI
        public void SetUserImage(Image? img)
        {
            if (img != null)
            {
                userPicture.BackgroundImage = img;
            }
            else
            {
                userPicture.BackgroundImage = null;
            }
        }

        public void SetUsername(string username)
        {
            usernameLabel.Text = username;
        }

        public void RemoveSelfUI()
        {
            Controls.Remove(directSpeakButton);
            Controls.Remove(volumePercieved);
            Controls.Remove(volumeSlider);
            directSpeakButton.Dispose();
            volumePercieved.Dispose();
            volumeSlider.Dispose();
            directCallback = null;
            volumeCallback = null;
        }

        public void RemoveOtherUI()
        {
            Controls.Remove(deafenButton);
            deafenButton.Dispose();
            deafCallback = null;
        }

        public void SetPercievedVolumeLevel(float percent)
        {
            int val = (int)percent;
            if (!volumePercieved.IsDisposed && !volumePercieved.Disposing)
                volumePercieved.Value = val < 0 ? 0 : (val > 100 ? 100 : val);
        }

        public void SetVolumeSlider(byte level)
        {
            volumeSlider.Value = level;
        }
        #endregion

        #region Set Callbacks
        public void SetMuteButtonCallback(Action<bool>? callback)
        {
            muteCallback = callback;
        }

        public void SetDeafButtonCallback(Action<bool>? callback)
        {
            deafCallback = callback;
        }

        /// <param name="callback">true if pressed, false if released</param>
        public void SetDirectButtonCallback(Action<bool>? callback)
        {
            directCallback = callback;
        }

        public void SetVolumeSliderCallback(Action<byte>? callback)
        {
            volumeCallback = callback;
        }
        #endregion
        #endregion

        #region Callbacks
        private void volumeSlider_Scroll(object sender, EventArgs e)
        {
            volumeCallback?.Invoke((byte)volumeSlider.Value);
        }

        private void directSpeakButton_Click(object sender, MouseEventArgs e)
        {
            directCallback?.Invoke(true);
        }

        private void directSpeakButton_Release(object sender, MouseEventArgs e)
        {
            directCallback?.Invoke(false);
        }
        #endregion

        private void muteButton_Click(object sender, EventArgs e)
        {
            Muted = !Muted;
        }

        private void deafenButton_Click(object sender, EventArgs e)
        {
            Deaf = !Deaf;
        }
    }
}