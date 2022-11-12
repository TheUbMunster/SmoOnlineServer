using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;

namespace ProxChatClientGUICrossPlatform
{
    internal class SettingsUI : Window
    {
        private enum RecordingMode
        {
            None = 0,
            Deafen,
            PushGlobal,
            PushTeam,
            Action
        }

        private int? teamKey = null;
        private int? globalKey = null;
        private int? toggleDeafenKey = null;
        private int? speakActionKey = null;

        private RecordingMode recordingMode = RecordingMode.None;
        private Action<int> OnKeyPressedAction;
        public bool serverWasRunningUponOpening = true;

        [UI] private Label discordAppIdLabel;
        [UI] private Label percievedVolumeLabel;
        [UI] private Label ingameUsernameLabel;
        [UI] private Label serverPortLabel;
        [UI] private Label serverHostLabel;
        [UI] private Label defaultVolLabel;
        [UI] private Label speakModeLabel;
        [UI] private Label pushToTeamLabel;
        [UI] private Label pushToGlobalLabel;
        [UI] private Label toggleMuteLabel;
        [UI] private Label toggleDeafenLabel;
        [UI] private Button confirmButton;
        [UI] private Entry appIdTextBox;
        [UI] private Entry igUsernameTextBox;
        [UI] private Entry serverPortTextBox;
        [UI] private Entry serverHostTextBox;
        [UI] private Entry defaultVolumeTextBox;

        [UI] private Button pushToTeamButton;
        [UI] private Button pushToGlobalButton;
        [UI] private Button pushToActionButton;
        [UI] private Button toggleDeafenButton;

        [UI] private CheckButton percievedVolumeCheckBox;
        [UI] private ComboBox speakModeComboBox;

        public SettingsUI() : this(new Builder("SettingsUI.glade")) { }
        private SettingsUI(Builder builder) : base(builder.GetRawOwnedObject("SettingsUI"))
        {
            TypeHint = Gdk.WindowTypeHint.Dialog;
            SetSizeRequest(650, 400);

            #region Populate UI Fields
            discordAppIdLabel = (Label)builder.GetObject("discordAppIdLabel");
            percievedVolumeLabel = (Label)builder.GetObject("percievedVolumeLabel");
            ingameUsernameLabel = (Label)builder.GetObject("ingameUsernameLabel");
            serverPortLabel = (Label)builder.GetObject("serverPortLabel");
            serverHostLabel = (Label)builder.GetObject("serverHostLabel");
            defaultVolLabel = (Label)builder.GetObject("defaultVolLabel");
            speakModeLabel = (Label)builder.GetObject("speakModeLabel"); 
            pushToTeamLabel = (Label)builder.GetObject("pushToTeamLabel"); 
            pushToGlobalLabel = (Label)builder.GetObject("pushToGlobalLabel");
            toggleMuteLabel = (Label)builder.GetObject("toggleMuteLabel"); 
            toggleDeafenLabel = (Label)builder.GetObject("toggleDeafenLabel");
            confirmButton = (Button)builder.GetObject("confirmButton");
            appIdTextBox = (Entry)builder.GetObject("appIdTextBox");
            igUsernameTextBox = (Entry)builder.GetObject("igUsernameTextBox");
            serverPortTextBox = (Entry)builder.GetObject("serverPortTextBox");
            serverHostTextBox = (Entry)builder.GetObject("serverHostTextBox");
            defaultVolumeTextBox = (Entry)builder.GetObject("defaultVolumeTextBox");
            pushToTeamButton = (Button)builder.GetObject("pushToTeamButton");
            pushToGlobalButton = (Button)builder.GetObject("pushToGlobalButton");
            pushToActionButton = (Button)builder.GetObject("pushToActionButton");
            toggleDeafenButton = (Button)builder.GetObject("toggleDeafenButton");
            percievedVolumeCheckBox = (CheckButton)builder.GetObject("percievedVolumeCheckBox");
            speakModeComboBox = (ComboBox)builder.GetObject("speakModeComboBox");
            Icon = new Gdk.Pixbuf(System.IO.File.ReadAllBytes(System.IO.Path.Combine("Images", "icon.ico")));
            #endregion

            #region Platform switching for keybinds
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    //do nothing because it's fine
                    break;
                default:
                case PlatformID.Unix:
                case PlatformID.Other:
                    //disable keybind buttons because it doesn't work on non-windows for now
                    pushToActionButton.Sensitive = false;
                    pushToTeamButton.Sensitive = false;
                    pushToGlobalButton.Sensitive = false;
                    toggleDeafenButton.Sensitive = false;
                    toggleDeafenButton.TooltipText = pushToGlobalButton.TooltipText = toggleDeafenButton.TooltipText = 
                    pushToActionButton.TooltipText = "Keybinds are currently only supported on windows.";
                    break;
            }
            #endregion

            //fill fields with values
            appIdTextBox.Text = "depricated"; //Settings.Instance.DiscordAppID.ToString();
            igUsernameTextBox.Text = Settings.Instance.IngameName!;
            serverPortTextBox.Text = Settings.Instance.ServerPort!.ToString();
            serverHostTextBox.Text = Settings.Instance.ServerHost!;
            defaultVolumeTextBox.Text = Settings.Instance.DefaultVolume!.ToString();
            speakModeComboBox.ActiveId = Settings.Instance.SpeakMode!;
            percievedVolumeCheckBox.Active = Settings.Instance.PercievedVolumeSliderEnabled!.Value;

            #region Event Subscription
            try
            {
                string? name = Enum.GetName(typeof(WinVirtualKeys), Settings.Instance.PushToTeam!);
                pushToTeamButton.Label = name ?? "Click me to set a keybind...";
            }
            catch
            {
                Settings.Instance.PushToTeam = null;
                pushToTeamButton.Label = "Click me to set a keybind...";
            }
            try
            {
                string? name = Enum.GetName(typeof(WinVirtualKeys), Settings.Instance.PushToGlobal!);
                pushToGlobalButton.Label = name ?? "Click me to set a keybind...";
            }
            catch
            {
                Settings.Instance.PushToGlobal = null;
                pushToGlobalButton.Label = "Click me to set a keybind...";
            }
            try
            {
                string? name = Enum.GetName(typeof(WinVirtualKeys), Settings.Instance.SpeakAction!);
                pushToActionButton.Label = name ?? "Click me to set a keybind...";
            }
            catch
            {
                Settings.Instance.SpeakAction = null;
                pushToActionButton.Label = "Click me to set a keybind...";
            }
            try
            {
                string? name = Enum.GetName(typeof(WinVirtualKeys), Settings.Instance.ToggleDeafen!);
                toggleDeafenButton.Label = name ?? "Click me to set a keybind...";
            }
            catch
            {
                Settings.Instance.ToggleDeafen = null;
                toggleDeafenButton.Label = "Click me to set a keybind...";
            }
            speakModeComboBox.Changed += (_, _) => SetActionLabel();
            SetActionLabel(); //change label right now
            OnKeyPressedAction = (int ikey) =>
            {
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Win32NT:
                        WinVirtualKeys key = (WinVirtualKeys)ikey;
                        Invoke(() =>
                        {
                            switch (recordingMode)
                            {
                                case RecordingMode.Deafen:
                                    {
                                        toggleDeafenButton.Label = key.ToString();
                                        toggleDeafenKey = ikey;
                                    }
                                    break;
                                case RecordingMode.PushGlobal:
                                    {
                                        pushToGlobalButton.Label = key.ToString();
                                        globalKey = ikey;
                                    }
                                    break;
                                case RecordingMode.PushTeam:
                                    {
                                        pushToTeamButton.Label = key.ToString();
                                        teamKey = ikey;
                                    }
                                    break;
                                case RecordingMode.Action:
                                    {
                                        pushToActionButton.Label = key.ToString();
                                        speakActionKey = ikey;
                                    }
                                    break;
                                case RecordingMode.None:
                                default: //"default" shouldn't happen
                                    break;
                            }
                            recordingMode = RecordingMode.None;
                            Settings.SaveSettings();
                        });
                        break;
                    default: //can't do keybinds on non-windows for now
                        break;
                }
                
            };
            toggleDeafenButton.Clicked += toggleDeafen_click;
            pushToActionButton.Clicked += actionKey_click;
            pushToGlobalButton.Clicked += pushToGlobal_click;
            pushToTeamButton.Clicked += pushToTeam_click;
            confirmButton.Clicked += confirmButton_Click;
            ProxChat.KeyService.OnKeyPressed += OnKeyPressedAction;
            DeleteEvent += (s, e) =>
            {
                ProxChat.Instance.SetCDCButtonEnabled(true);
            };
            #endregion
        }

        private void toggleDeafen_click(object sender, EventArgs e)
        {
            recordingMode = RecordingMode.Deafen;
            toggleDeafenButton.Label = "Recording...";
        }

        private void actionKey_click(object sender, EventArgs e)
        {
            recordingMode = RecordingMode.Action;
            pushToActionButton.Label = "Recording...";
        }

        private void pushToGlobal_click(object sender, EventArgs e)
        {
            recordingMode = RecordingMode.PushGlobal;
            pushToGlobalButton.Label = "Recording...";
        }

        private void pushToTeam_click(object sender, EventArgs e)
        {
            recordingMode = RecordingMode.PushTeam;
            pushToTeamButton.Label = "Recording...";
        }

        private void confirmButton_Click(object sender, EventArgs e)
        {
            //check if everything's valid (restart if applicable)
            bool needToClose = false;
            if (long.TryParse(appIdTextBox.Text, out long appId))
            {
                if (Settings.Instance.DiscordAppID.HasValue && Settings.Instance.DiscordAppID.Value != appId)
                {
                    Settings.Instance.DiscordAppID = appId;
                    needToClose = true;
                }
            }
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
                //MessageBox.Show("The port is invalid, make sure it's a number between 0 and 65535.");
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
                //MessageBox.Show("The Default Volume is invalid, make sure it's a number between 0 and 200.");
            }
            Settings.Instance.SpeakMode = speakModeComboBox.ActiveId.ToString();
            Settings.Instance.ToggleDeafen = toggleDeafenKey;
            Settings.Instance.PushToGlobal = globalKey;
            Settings.Instance.PushToTeam = teamKey;
            Settings.Instance.SpeakAction = speakActionKey;
            bool before = Settings.Instance.PercievedVolumeSliderEnabled!.Value;
            Settings.Instance.PercievedVolumeSliderEnabled = percievedVolumeCheckBox.Active;
            if (before != Settings.Instance.PercievedVolumeSliderEnabled)
            {
                ProxChat.Instance.SetPercievedVolumeVisible(Settings.Instance.PercievedVolumeSliderEnabled!.Value);
            }
            Settings.SaveSettings();
            if ((needToCloseIfRunning && serverWasRunningUponOpening) || needToClose)
            {
                //Application.Restart();
                string? path = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
                if (path != null) //path is probably just "Server", but in the context of the assembly, that's all you need to restart it.
                {
                    System.Diagnostics.Process.Start(path);
                }
                Environment.Exit(0); //there isn't a better way to do this other than to: start up another app
                                     //whose sole purpose is to reopen this one, (then close this one).
            }
            switch (Settings.Instance.SpeakMode)
            {
                case "Always On":
                    break;
                case "Push-To-Talk":
                    ProxChat.Instance.SetSelfMute(true);
                    break;
                case "Push-To-Mute":
                    ProxChat.Instance.SetSelfMute(false);
                    break;
            }
            Close();
        }

        private void SetActionLabel()
        {
            //MainWindow.Instance.viewLogger.Info(speakModeComboBox.ActiveId);
            switch (speakModeComboBox.ActiveId)
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
                    speakModeComboBox.ActiveId = "Always On";
                    //MessageBox.Show("Settings loaded an invalid Speak Mode from settings.json, Speak Mode is now set to \"Always On\"");
                    break;
            }
        }

        public void Show(Window owner)
        {
            Parent = owner;
            SetPosition(WindowPosition.CenterOnParent);
            ShowAll();
        }

        private void Invoke(System.Action action)
        {
            System.Threading.ManualResetEventSlim rs = new System.Threading.ManualResetEventSlim();
            Application.Invoke((s, a) =>
            {
                action?.Invoke();
                rs.Set();
            });
            rs.Wait();
        }
    }
}
