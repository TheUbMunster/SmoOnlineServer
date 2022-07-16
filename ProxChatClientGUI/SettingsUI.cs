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
    public partial class SettingsUI : Form
    {
        private enum RecordingMode
        {
            None = 0,
            Deafen,
            PushGlobal,
            PushTeam,
            Action
        }

        //settings:
        /*
         * igusername (requres restart of program)
         * 
         * serverport (requres restart of program)
         * 
         * serverhost (requres restart of program)
         * 
         * default volume
         * 
         * keybinds:
         *
         * push-to-talk/push-to-mute/always on <-
         * push-to-team                         |   
         * push-to-global                       |
         *                                      /
         * toggle deafen                       /
         * toggle mute (only if "always on")  /
         */

        private RecordingMode recordingMode = RecordingMode.None;
        private Action<Keys> OnKeyPressedAction;
        public SettingsUI()
        {
            InitializeComponent();
            //fill fields with values
            igUsernameTextBox.Text = Settings.Instance.IngameName!;
            serverPortTextBox.Text = Settings.Instance.ServerPort!.ToString();
            serverHostTextBox.Text = Settings.Instance.ServerHost!;
            defaultVolumeTextBox.Text = Settings.Instance.DefaultVolume!.ToString();
            speakModeComboBox.SelectedItem = Settings.Instance.SpeakMode!;
            speakModeComboBox.SelectedValueChanged += (_, _) => SetActionLabel();
            try
            {
                string? name = Enum.GetName(typeof(Keys), int.Parse(Settings.Instance.PushToTeam!.ToString()!));
                pushToTeamTextBox.Text = name ?? "Click me to set a keybind...";
            }
            catch
            {
                Settings.Instance.PushToTeam = null;
                pushToTeamTextBox.Text = "Click me to set a keybind...";
            }
            try
            {
                string? name = Enum.GetName(typeof(Keys), int.Parse(Settings.Instance.PushToGlobal!.ToString()!));
                pushToGlobalTextBox.Text = name ?? "Click me to set a keybind...";
            }
            catch
            {
                Settings.Instance.PushToGlobal = null;
                pushToGlobalTextBox.Text = "Click me to set a keybind...";
            }
            try
            {
                string? name = Enum.GetName(typeof(Keys), int.Parse(Settings.Instance.SpeakAction!.ToString()!));
                pushToActionTextBox.Text = name ?? "Click me to set a keybind...";
            }
            catch
            {
                Settings.Instance.SpeakAction = null;
                pushToActionTextBox.Text = "Click me to set a keybind...";
            }
            try
            {
                string? name = Enum.GetName(typeof(Keys), int.Parse(Settings.Instance.ToggleDeafen!.ToString()!));
                toggleDeafenTextBox.Text = name ?? "Click me to set a keybind...";
            }
            catch
            {
                Settings.Instance.ToggleDeafen = null;
                toggleDeafenTextBox.Text = "Click me to set a keybind...";
            }
            SetActionLabel();
            OnKeyPressedAction = (Keys key) =>
            {
                Invoke(() =>
                {
                    switch (recordingMode)
                    {
                        case RecordingMode.Deafen:
                            {
                                toggleDeafenTextBox.Text = key.ToString();
                                Settings.Instance.ToggleDeafen = key;
                            }
                            break;
                        case RecordingMode.PushGlobal:
                            {
                                pushToGlobalTextBox.Text = key.ToString();
                                Settings.Instance.PushToGlobal = key;
                            }
                            break;
                        case RecordingMode.PushTeam:
                            {
                                pushToTeamTextBox.Text = key.ToString();
                                Settings.Instance.PushToTeam = key;
                            }
                            break;
                        case RecordingMode.Action:
                            {
                                pushToActionTextBox.Text = key.ToString();
                                Settings.Instance.SpeakAction = key;
                            }
                            break;
                        case RecordingMode.None:
                        default: //this shouldn't happen
                            break;
                    }
                    recordingMode = RecordingMode.None;
                });
            };
            ProxChat.KeyService.OnKeyPressed += OnKeyPressedAction;
        }

        private void SetActionLabel()
        {
            switch (speakModeComboBox.SelectedItem)
            {
                case "Always On":
                    {
                        toggleMuteLabel.Text = "Toggle Mute:";
                    }
                    break;
                case "Push-To-Talk":
                    {
                        toggleMuteLabel.Text = "Push-To-Talk Action Key:";
                    }
                    break;
                case "Push-To-Mute":
                    {
                        toggleMuteLabel.Text = "Push-To-Mute Action Key:";
                    }
                    break;
                default:
                    //popup with error message and set it to "Always On"
                    speakModeComboBox.Items.Remove(speakModeComboBox.SelectedItem);
                    speakModeComboBox.SelectedItem = "Always On";
                    MessageBox.Show("Settings loaded an invalid Speak Mode from settings.json, Speak Mode is now set to \"Always On\"");
                    break;
            }
        }

        private void toggleDeafen_click(object sender, EventArgs e)
        {
            recordingMode = RecordingMode.Deafen;
        }

        private void actionKey_click(object sender, EventArgs e)
        {
            recordingMode = RecordingMode.Action;
        }

        private void pushToGlobal_click(object sender, EventArgs e)
        {
            recordingMode = RecordingMode.PushGlobal;
        }

        private void pushToTeam_click(object sender, EventArgs e)
        {
            recordingMode = RecordingMode.PushTeam;
        }

        private void confirmButton_Click(object sender, EventArgs e)
        {
            //check if everything's valid (restart if applicable)
            bool needToCloseIfRunning = false;
            if (igUsernameTextBox.Text != Settings.Instance.IngameName)
            {
                Settings.Instance.IngameName = igUsernameTextBox.Text;
                needToCloseIfRunning = true;
            }
            if (ushort.TryParse(serverPortTextBox.Text, out ushort port))
            {
                if (port != Settings.Instance.ServerPort)
                {
                    Settings.Instance.ServerPort = port;
                    needToCloseIfRunning = true;
                }
            }
            else
            {
                MessageBox.Show("The port is invalid, make sure it's a number between 0 and 65535.");
            }
            if (serverHostTextBox.Text != Settings.Instance.ServerHost)
            {
                Settings.Instance.ServerHost = serverHostTextBox.Text;
                needToCloseIfRunning = true;
            }
            if (byte.TryParse(defaultVolumeTextBox.Text, out byte vol) && vol <= 200)
            {
                Settings.Instance.DefaultVolume = vol;
            }
            else
            {
                MessageBox.Show("The Default Volume is invalid, make sure it's a number between 0 and 200.");
            }

            Settings.SaveSettings();
            if (needToCloseIfRunning/* && ProxChat.Instance.*/)
            {
                Application.Restart();
                Environment.Exit(0); //there isn't a better way to do this other than to: start up another app
                                     //whose sole purpose is to reopen this one, (then close this one).
            }
            switch (speakModeComboBox.SelectedItem.ToString())
            {
                case "Always On":
                    break;
                case "Push-To-Talk":
                    //need to mute the player (if they aren't already muted, simulate mute button press)
                    break;
                case "Push-To-Mute":
                    //need to unmute the player (if they aren't already unmuted, simulate unmute button press)
                    break;
            }
            Close();
        }

        private void onFormClosed(object sender, FormClosingEventArgs e)
        {
            ProxChat.KeyService.OnKeyPressed -= OnKeyPressedAction;
        }
    }
}