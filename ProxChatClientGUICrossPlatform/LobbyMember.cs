﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;
using IOPath = System.IO.Path;

namespace ProxChatClientGUICrossPlatform
{
    internal class LobbyMember
    {
        private static Shared.Logger lobbyMemberLogger = new Shared.Logger("LobbyMemberUI");

        [UI] private readonly Image directImage = ProxChat.ResizeImage(IOPath.Join(".", "Images", "direct.png"), 100, 100);
        [UI] private readonly Image headphonesImage = ProxChat.ResizeImage(IOPath.Join(".", "Images", "headphones.png"), 50, 50);
        [UI] private readonly Image headphonesCrossedImage = ProxChat.ResizeImage(IOPath.Join(".", "Images", "headphones-crossed.png"), 50, 50);
        [UI] private readonly Image micImage = ProxChat.ResizeImage(IOPath.Join(".", "Images", "mic.png"), 50, 50);
        [UI] private readonly Image micCrossedImage = ProxChat.ResizeImage(IOPath.Join(".", "Images", "mic-crossed.png"), 50, 50);

        private Action<bool>? muteCallback; //true mute, false unmute
        private Action<bool>? deafCallback; //true deaf, false undeaf
        private Action<bool>? directCallback;
        private Action<byte>? volumeCallback;

        private Gdk.Pixbuf? normalUser;
        private Gdk.Pixbuf? highUser;

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
                        muteButton.Image = micCrossedImage;
                    }
                    else
                    {
                        muteButton.Image = micImage;
                        if (Deaf)
                        {
                            if (deafenButton != null)
                            {
                                deafenButton.Image = headphonesImage;
                                deafCallback?.Invoke(false);
                            }
                        }
                    }
                    muteCallback?.Invoke(value);
                    muteButton.Label = "";
                }
                catch
                {
                    muteButton.Label = muted ? "unmute" : "mute";
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
                    if (deafenButton != null)
                    {
                        if (value)
                        {
                            deafenButton.Image = headphonesCrossedImage;
                        }
                        else
                        {
                            deafenButton.Image = headphonesImage;
                        }
                    }
                    Muted = value;
                    if (deafenButton != null)
                    {
                        deafCallback?.Invoke(value);
                        deafenButton.Label = "";
                    }
                }
                catch
                {
                    if (deafenButton != null)
                        deafenButton.Label = deaf ? "undeaf" : "deaf";
                }
                deaf = value;
            }
        }
        public long? UserId { get; private set; }

        public ListBoxRow lbr { get; private set; }
        [UI] private readonly Button muteButton;
        [UI] private readonly Button deafenButton = null;
        [UI] private readonly Button directButton = null;
        [UI] private readonly Label usernameLabel;
        [UI] private readonly ProgressBar volumePercieved = null;
        [UI] private readonly Scale volumeSlider = null;
        [UI] private readonly Image displayUserImage = null;
        public LobbyMember(bool isSelf, Action<bool>? mute, Action<bool>? deaf, Action<bool>? direct, Action<byte>? vol, byte startSlider)
        {
            lbr = new ListBoxRow();
            lbr.Selectable = false;
            lbr.Activatable = false;
            Grid mainGrid = new Grid();
            lbr.Add(mainGrid);
            Grid centerGrid = new Grid();
            mainGrid.Attach(centerGrid, 2, 0, 1, 2);
            //add image (done later)
            //add mute button
            muteButton = new Button();
            muteButton.AlwaysShowImage = true;
#pragma warning disable CS0612
            muteButton.MarginLeft = 10;
#pragma warning restore CS0612
            muteButton.Vexpand = true;
            muteButton.SetSizeRequest(50, 50);
            muteButton.Clicked += (_, _) => 
            {
                lobbyMemberLogger.Info("Clicked mute button, " + !Muted);
                Muted = !Muted; 
            };
            muteCallback = mute;
            muted = !muted;
            Muted = !muted;
            mainGrid.Attach(muteButton, 1, 0, 1, 1);
            muteButton.ShowAll();
            if (isSelf)
            {
                //add deaf
                deafenButton = new Button();
                deafenButton.AlwaysShowImage = true;
                deafenButton.SetSizeRequest(50, 50);
#pragma warning disable CS0612
                deafenButton.MarginLeft = 10;
#pragma warning restore CS0612
                deafenButton.Clicked += (_, _) => 
                {
                    lobbyMemberLogger.Info("Clicked deaf button, " + !Deaf);
                    Deaf = !Deaf; 
                };
                deafCallback = deaf;
                this.deaf = !this.deaf;
                Deaf = !this.deaf;
                mainGrid.Attach(deafenButton, 1, 1, 1, 1);
                deafenButton.ShowAll();
            }
            else
            {
                //add percieved vol
                volumePercieved = new ProgressBar();
                centerGrid.Attach(volumePercieved, 0, 2, 1, 1);
#pragma warning disable CS0612
                volumePercieved.MarginRight = volumePercieved.MarginLeft = 12;
#pragma warning restore CS0612
                volumePercieved.Vexpand = true;
                volumePercieved.Hexpand = true;
                volumePercieved.ShowAll();
                //add vol slider
                volumeSlider = new Scale(Orientation.Horizontal, 0, 200, 1);
                volumeSlider.Value = startSlider;
                volumeSlider.Vexpand = true;
                volumeSlider.Hexpand = true;
                centerGrid.Attach(volumeSlider, 0, 0, 1, 1);
                volumeCallback = vol;
                volumeSlider.ChangeValue += (_, _) =>
                {
                    lobbyMemberLogger.Info("Sending volume to setvolume callback...");
                    volumeCallback?.Invoke((byte)volumeSlider.Value);
                };
                volumeSlider.DrawValue = false;
                volumeSlider.ShowAll();
                //add direct button
                directButton = new Button();
#pragma warning disable CS0612
                directButton.MarginRight = 10;
#pragma warning restore CS0612
                directButton.AlwaysShowImage = true;
                directButton.SetSizeRequest(100, 100);
                directButton.Image = directImage;
                directButton.AddEvents((int)Gdk.EventMask.ButtonPressMask);
                directButton.AddEvents((int)Gdk.EventMask.ButtonReleaseMask);
                directCallback = direct;
                directButton.ButtonPressEvent += (_, _) =>
                {
                    lobbyMemberLogger.Info("Sending callback to direct speak to " + usernameLabel.Text);
                    directCallback?.Invoke(true);
                };
                directButton.ButtonReleaseEvent += (_, _) =>
                {
                    lobbyMemberLogger.Info("Sending callback to no longer direct speak to " + usernameLabel.Text);
                    directCallback?.Invoke(false);
                };
                mainGrid.Attach(directButton, 3, 0, 1, 2);
                directButton.ShowAll();
            }
            //add username
            usernameLabel = new Label();
#pragma warning disable CS0612
            usernameLabel.SetAlignment(0, 0.5f);
            usernameLabel.SetPadding(8, 0);
            usernameLabel.ModifyFont(Pango.FontDescription.FromString("monospace 12"));
#pragma warning restore CS0612
            usernameLabel.Vexpand = true;
            centerGrid.Attach(usernameLabel, 0, 1, 1, 1);
            usernameLabel.ShowAll();
            displayUserImage = new Image();
            mainGrid.Attach(displayUserImage, 0, 0, 1, 2);
            lbr.ShowAll();
        }

        #region Sets
        #region UI
        public void SetUserTalking(bool talking)
        {
            if (talking)
            {
                displayUserImage.Pixbuf = highUser;
            }
            else
            {
                displayUserImage.Pixbuf = normalUser;
            }
        }

        public void SetUserImages(Gdk.Pixbuf? normal, Gdk.Pixbuf? high)
        {
            normal = normal.ScaleSimple(100, 100, Gdk.InterpType.Bilinear);
            high = high.ScaleSimple(100, 100, Gdk.InterpType.Bilinear);
            normalUser = normal;
            highUser = high;
        }

        public void DisposeUserImages()
        {
            normalUser.Dispose();
            normalUser = null;
            highUser.Dispose();
            highUser = null;
        }

        public void SetUserInfo(string username, long userId)
        {
            UserId = userId;
            usernameLabel.Text = username;
        }

        public void SetPercievedVolumeLevel(float percent)
        {
            if (volumePercieved != null)
                volumePercieved.Fraction = percent;
        }

        public void SetPercievedVolumeVisible(bool visible)
        {
            if (volumePercieved != null)
                volumePercieved.Visible = visible;
        }

        public void SetVolumeSlider(byte level)
        {
            volumeSlider.Value = level;
        }
        #endregion
        #endregion
    }
}
