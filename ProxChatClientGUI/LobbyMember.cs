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
        private static Image walkieTalkie = null!;

        private Action? muteCallback;
        private Action? deafCallback;
        private Action<bool>? directCallback;
        private Action<byte>? volumeCallback;

        public LobbyMember()
        {
            InitializeComponent();
            muteButton.BackgroundImageLayout = ImageLayout.Stretch;
            deafenButton.BackgroundImageLayout = ImageLayout.Stretch;
            directSpeakButton.BackgroundImageLayout = ImageLayout.Stretch;
            MaximumSize = new Size(9999, Height);
            MinimumSize = new Size(0, Height);

            try
            {
                directSpeakButton.BackgroundImage = walkieTalkie;
                directSpeakButton.Text = "";
            }
            catch
            {
                directSpeakButton.Text = "direct";
            }
            SetMuteButtonImage(false);
            SetDeafenButtonImage(false);
        }

        #region Sets
        public void SetUserImage(Image? img)
        {
            if (img != null)
            {
                userImage.Image = img;
                userImage.Text = "";
            }
            else
            {
                userImage.Text = "Image";
            }
        }

        public void SetMuteButtonImage(bool muted)
        {
            try
            {
                if (muted)
                {
                    muteButton.BackgroundImage = crossedMicrophone;
                }
                else
                {
                    muteButton.BackgroundImage = microphone;
                }
                muteButton.Text = "";
            }
            catch 
            {
                muteButton.Text = muted ? "unmute" : "mute";
            }
        }

        public void SetDeafenButtonImage(bool deafened)
        {
            try
            {
                if (deafened)
                {
                    deafenButton.BackgroundImage = crossedHeadphones;
                }
                else
                {
                    deafenButton.BackgroundImage = headphones;
                }
                deafenButton.Text = "";
            }
            catch
            {
                deafenButton.Text = deafened ? "undeaf" : "deaf";
            }
        }

        public void DisableDeafenButton()
        {
            deafenButton.Enabled = false;
        }

        public void SetVolumeDisplaySlider(float percent)
        {
            int val = (int)percent;
            volumePercieved.Value = val < 0 ? 0 : (val > 100 ? 100 : val);
        }

        public void SetMuteButtonCallback(Action? callback)
        {
            muteCallback = callback;
        }

        public void SetDeafButtonCallback(Action? callback)
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

        #region Callbacks
        private void muteButton_Click(object sender, EventArgs e)
        {
            muteCallback?.Invoke();
        }

        private void deafenButton_Click(object sender, EventArgs e)
        {
            deafCallback?.Invoke();
        }

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

        private void directSpeakButton_Release(object sender, EventArgs e)
        {
            directCallback?.Invoke(false);
        }
        #endregion
    }
}