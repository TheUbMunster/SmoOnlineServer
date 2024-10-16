﻿using System;
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
        private static Shared.Logger lobbyMemberLogger = new Shared.Logger("LobbyMemberUI");

        private static Image crossedHeadphones = Image.FromFile("Images\\headphones-crossed.png");
        private static Image headphones = Image.FromFile("Images\\headphones.png");
        private static Image crossedMicrophone = Image.FromFile("Images\\mic-crossed.png");
        private static Image microphone = Image.FromFile("Images\\mic.png");
        private static Image walkieTalkie = Image.FromFile("Images\\direct.png");

        private Action<bool>? muteCallback; //true mute, false unmute
        private Action<bool>? deafCallback; //true deaf, false undeaf
        private Action<bool>? directCallback;
        private Action<byte>? volumeCallback;

        private Image? normalUser;
        private Image? highUser;

        private bool muted = false;
        public bool Muted
        {
            get => muted;
            set
            {
                //todo
                //switch (muted, value)
                //{
                //    case (true, true):
                //        //mute but already muted
                //        break;
                //    case (false, false):
                //        //ensure undeaf
                //        deafCallback?.Invoke(false);
                //        break;
                //    case (true, false):
                //        //unmuting
                //        deafenButton.BackgroundImage = headphones;
                //        muteButton.BackgroundImage = microphone;
                //        muteCallback?.Invoke(false);
                //        deafCallback?.Invoke(false);
                //        break;
                //    case (false, true):
                //        //muting
                //        muteButton.BackgroundImage = crossedMicrophone;
                //        muteCallback?.Invoke(true);
                //        break;
                //}
                //muted = value;
                //muteButton.Text = "";

                try
                {
                    if (value)
                    {
                        muteButton.BackgroundImage = crossedMicrophone;
                    }
                    else
                    {
                        muteButton.BackgroundImage = microphone;
                        if (Deaf)
                        {
                            deafenButton.BackgroundImage = headphones;
                            deafCallback?.Invoke(false);
                        }
                    }
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
                //switch (deaf, value)
                //{
                //    case (true, true):
                //        //deafening when already deafened
                //        break;
                //    case (false, false):
                //        //undeafening when already undeafened
                //        break;
                //    case (true, false):
                //        //undeafening
                //        deafenButton.BackgroundImage = headphones;
                //        muteButton.BackgroundImage = microphone;
                //        muteCallback?.Invoke(false);
                //        deafCallback?.Invoke(false);
                //        break;
                //    case (false, true):
                //        //deafeaning
                //        deafenButton.BackgroundImage = crossedHeadphones;
                //        muteButton.BackgroundImage = crossedMicrophone;
                //        muteCallback?.Invoke(true);
                //        deafCallback?.Invoke(true);
                //        break;
                //}
                //deaf = value;
                //deafenButton.Text = "";

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
                    Muted = value;
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

        public long? UserId { get; private set; }

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
        }

        #region Sets
        #region UI
        public void SetUserTalking(bool talking)
        {
            if (talking)
                userPicture.BackgroundImage = highUser;
            else
                userPicture.BackgroundImage = normalUser;
        }

        public void SetUserImages(Image? normal, Image? high)
        {
            normalUser = normal;
            highUser = high;
            userPicture.BackgroundImage = normal;
        }

        public void DisposeUserImages()
        {
            userPicture.BackgroundImage = null;
            normalUser?.Dispose();
            highUser?.Dispose();
        }

        public void SetUserInfo(string username, long userId)
        {
            UserId = userId;
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
            int val = (int)(percent * 100f);
            val = val < 0 ? 0 : (val > 100 ? 100 : val);
            if (!volumePercieved.IsDisposed && !volumePercieved.Disposing)
            {
                //strange hack to get rid of the glow effect.
                volumePercieved.Minimum = val;
                volumePercieved.Value = volumePercieved.Minimum;
                volumePercieved.Minimum = 0;
                //volumePercieved.Value = val; //"correct" way
            }
        }

        public void SetPercievedVolumeVisible(bool visible)
        {
            if (volumePercieved != null && !volumePercieved.IsDisposed && !volumePercieved.Disposing)
                volumePercieved.Visible = visible;
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
            muted = !muted;
            Muted = !muted;
        }

        public void SetDeafButtonCallback(Action<bool>? callback)
        {
            deafCallback = callback;
            deaf = !deaf;
            Deaf = !deaf;
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
        private void volumeSlider_MouseUp(object sender, MouseEventArgs e)
        {
            lobbyMemberLogger.Info("Sending volume to setvolume callback...");
            volumeCallback?.Invoke((byte)volumeSlider.Value);
        }

        private void directSpeakButton_Click(object sender, MouseEventArgs e)
        {
            //Capture = true;
            lobbyMemberLogger.Info("Sending callback to direct speak to " + usernameLabel.Text);
            directCallback?.Invoke(true);
        }

        private void directSpeakButton_Release(object sender, MouseEventArgs e)
        {
            //Capture = false;
            lobbyMemberLogger.Info("Sending callback to no longer direct speak to " + usernameLabel.Text);
            directCallback?.Invoke(false);
        }

        private void muteButton_Click(object sender, EventArgs e)
        {
            lobbyMemberLogger.Info("Clicked mute button, " + !Muted);
            Muted = !Muted;
        }

        private void deafenButton_Click(object sender, EventArgs e)
        {
            lobbyMemberLogger.Info("Clicked deaf button, " + !Deaf);
            Deaf = !Deaf;
        }
        #endregion
    }
}