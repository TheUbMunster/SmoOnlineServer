namespace ProxChatClientGUI
{
    partial class SettingsUI
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.settingsLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.toggleDeafenTextBox = new System.Windows.Forms.TextBox();
            this.pushToActionTextBox = new System.Windows.Forms.TextBox();
            this.pushToGlobalTextBox = new System.Windows.Forms.TextBox();
            this.pushToTeamTextBox = new System.Windows.Forms.TextBox();
            this.defaultVolumeTextBox = new System.Windows.Forms.TextBox();
            this.serverHostTextBox = new System.Windows.Forms.TextBox();
            this.serverPortTextBox = new System.Windows.Forms.TextBox();
            this.toggleDeafenLabel = new System.Windows.Forms.Label();
            this.toggleMuteLabel = new System.Windows.Forms.Label();
            this.pushToGlobalLabel = new System.Windows.Forms.Label();
            this.pushToTeamLabel = new System.Windows.Forms.Label();
            this.speakModeLabel = new System.Windows.Forms.Label();
            this.defaultVolLabel = new System.Windows.Forms.Label();
            this.serverHostLabel = new System.Windows.Forms.Label();
            this.serverPortLabel = new System.Windows.Forms.Label();
            this.ingameUsernameLabel = new System.Windows.Forms.Label();
            this.igUsernameTextBox = new System.Windows.Forms.TextBox();
            this.speakModeComboBox = new System.Windows.Forms.ComboBox();
            this.confirmButton = new System.Windows.Forms.Button();
            this.settingsLayoutPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // settingsLayoutPanel
            // 
            this.settingsLayoutPanel.ColumnCount = 2;
            this.settingsLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.settingsLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.settingsLayoutPanel.Controls.Add(this.toggleDeafenTextBox, 1, 8);
            this.settingsLayoutPanel.Controls.Add(this.pushToActionTextBox, 1, 7);
            this.settingsLayoutPanel.Controls.Add(this.pushToGlobalTextBox, 1, 6);
            this.settingsLayoutPanel.Controls.Add(this.pushToTeamTextBox, 1, 5);
            this.settingsLayoutPanel.Controls.Add(this.defaultVolumeTextBox, 1, 3);
            this.settingsLayoutPanel.Controls.Add(this.serverHostTextBox, 1, 2);
            this.settingsLayoutPanel.Controls.Add(this.serverPortTextBox, 1, 1);
            this.settingsLayoutPanel.Controls.Add(this.toggleDeafenLabel, 0, 8);
            this.settingsLayoutPanel.Controls.Add(this.toggleMuteLabel, 0, 7);
            this.settingsLayoutPanel.Controls.Add(this.pushToGlobalLabel, 0, 6);
            this.settingsLayoutPanel.Controls.Add(this.pushToTeamLabel, 0, 5);
            this.settingsLayoutPanel.Controls.Add(this.speakModeLabel, 0, 4);
            this.settingsLayoutPanel.Controls.Add(this.defaultVolLabel, 0, 3);
            this.settingsLayoutPanel.Controls.Add(this.serverHostLabel, 0, 2);
            this.settingsLayoutPanel.Controls.Add(this.serverPortLabel, 0, 1);
            this.settingsLayoutPanel.Controls.Add(this.ingameUsernameLabel, 0, 0);
            this.settingsLayoutPanel.Controls.Add(this.igUsernameTextBox, 1, 0);
            this.settingsLayoutPanel.Controls.Add(this.speakModeComboBox, 1, 4);
            this.settingsLayoutPanel.Controls.Add(this.confirmButton, 0, 9);
            this.settingsLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.settingsLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.settingsLayoutPanel.Name = "settingsLayoutPanel";
            this.settingsLayoutPanel.RowCount = 10;
            this.settingsLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.settingsLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.settingsLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.settingsLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.settingsLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.settingsLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.settingsLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.settingsLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.settingsLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.settingsLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.settingsLayoutPanel.Size = new System.Drawing.Size(612, 311);
            this.settingsLayoutPanel.TabIndex = 0;
            // 
            // toggleDeafenTextBox
            // 
            this.toggleDeafenTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.toggleDeafenTextBox.Location = new System.Drawing.Point(247, 251);
            this.toggleDeafenTextBox.Name = "toggleDeafenTextBox";
            this.toggleDeafenTextBox.ReadOnly = true;
            this.toggleDeafenTextBox.Size = new System.Drawing.Size(362, 23);
            this.toggleDeafenTextBox.TabIndex = 24;
            this.toggleDeafenTextBox.Click += new System.EventHandler(this.toggleDeafen_click);
            // 
            // pushToActionTextBox
            // 
            this.pushToActionTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pushToActionTextBox.Location = new System.Drawing.Point(247, 220);
            this.pushToActionTextBox.Name = "pushToActionTextBox";
            this.pushToActionTextBox.ReadOnly = true;
            this.pushToActionTextBox.Size = new System.Drawing.Size(362, 23);
            this.pushToActionTextBox.TabIndex = 23;
            this.pushToActionTextBox.Click += new System.EventHandler(this.actionKey_click);
            // 
            // pushToGlobalTextBox
            // 
            this.pushToGlobalTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pushToGlobalTextBox.Location = new System.Drawing.Point(247, 189);
            this.pushToGlobalTextBox.Name = "pushToGlobalTextBox";
            this.pushToGlobalTextBox.ReadOnly = true;
            this.pushToGlobalTextBox.Size = new System.Drawing.Size(362, 23);
            this.pushToGlobalTextBox.TabIndex = 22;
            this.pushToGlobalTextBox.Click += new System.EventHandler(this.pushToGlobal_click);
            // 
            // pushToTeamTextBox
            // 
            this.pushToTeamTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pushToTeamTextBox.Location = new System.Drawing.Point(247, 158);
            this.pushToTeamTextBox.Name = "pushToTeamTextBox";
            this.pushToTeamTextBox.ReadOnly = true;
            this.pushToTeamTextBox.Size = new System.Drawing.Size(362, 23);
            this.pushToTeamTextBox.TabIndex = 21;
            this.pushToTeamTextBox.Click += new System.EventHandler(this.pushToTeam_click);
            // 
            // defaultVolumeTextBox
            // 
            this.defaultVolumeTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.defaultVolumeTextBox.Location = new System.Drawing.Point(247, 96);
            this.defaultVolumeTextBox.Name = "defaultVolumeTextBox";
            this.defaultVolumeTextBox.Size = new System.Drawing.Size(362, 23);
            this.defaultVolumeTextBox.TabIndex = 19;
            // 
            // serverHostTextBox
            // 
            this.serverHostTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.serverHostTextBox.Location = new System.Drawing.Point(247, 65);
            this.serverHostTextBox.Name = "serverHostTextBox";
            this.serverHostTextBox.Size = new System.Drawing.Size(362, 23);
            this.serverHostTextBox.TabIndex = 18;
            // 
            // serverPortTextBox
            // 
            this.serverPortTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.serverPortTextBox.Location = new System.Drawing.Point(247, 34);
            this.serverPortTextBox.Name = "serverPortTextBox";
            this.serverPortTextBox.Size = new System.Drawing.Size(362, 23);
            this.serverPortTextBox.TabIndex = 17;
            // 
            // toggleDeafenLabel
            // 
            this.toggleDeafenLabel.AutoSize = true;
            this.toggleDeafenLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.toggleDeafenLabel.Location = new System.Drawing.Point(3, 248);
            this.toggleDeafenLabel.Name = "toggleDeafenLabel";
            this.toggleDeafenLabel.Size = new System.Drawing.Size(238, 31);
            this.toggleDeafenLabel.TabIndex = 16;
            this.toggleDeafenLabel.Text = "Toggle Deafen:";
            this.toggleDeafenLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // toggleMuteLabel
            // 
            this.toggleMuteLabel.AutoSize = true;
            this.toggleMuteLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.toggleMuteLabel.Location = new System.Drawing.Point(3, 217);
            this.toggleMuteLabel.Name = "toggleMuteLabel";
            this.toggleMuteLabel.Size = new System.Drawing.Size(238, 31);
            this.toggleMuteLabel.TabIndex = 14;
            this.toggleMuteLabel.Text = "Toggle Mute:";
            this.toggleMuteLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pushToGlobalLabel
            // 
            this.pushToGlobalLabel.AutoSize = true;
            this.pushToGlobalLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pushToGlobalLabel.Location = new System.Drawing.Point(3, 186);
            this.pushToGlobalLabel.Name = "pushToGlobalLabel";
            this.pushToGlobalLabel.Size = new System.Drawing.Size(238, 31);
            this.pushToGlobalLabel.TabIndex = 12;
            this.pushToGlobalLabel.Text = "Push-To-Talk (Global):";
            this.pushToGlobalLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pushToTeamLabel
            // 
            this.pushToTeamLabel.AutoSize = true;
            this.pushToTeamLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pushToTeamLabel.Location = new System.Drawing.Point(3, 155);
            this.pushToTeamLabel.Name = "pushToTeamLabel";
            this.pushToTeamLabel.Size = new System.Drawing.Size(238, 31);
            this.pushToTeamLabel.TabIndex = 10;
            this.pushToTeamLabel.Text = "Push-To-Talk (Team):";
            this.pushToTeamLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // speakModeLabel
            // 
            this.speakModeLabel.AutoSize = true;
            this.speakModeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.speakModeLabel.Location = new System.Drawing.Point(3, 124);
            this.speakModeLabel.Name = "speakModeLabel";
            this.speakModeLabel.Size = new System.Drawing.Size(238, 31);
            this.speakModeLabel.TabIndex = 8;
            this.speakModeLabel.Text = "Speak mode:";
            this.speakModeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // defaultVolLabel
            // 
            this.defaultVolLabel.AutoSize = true;
            this.defaultVolLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.defaultVolLabel.Location = new System.Drawing.Point(3, 93);
            this.defaultVolLabel.Name = "defaultVolLabel";
            this.defaultVolLabel.Size = new System.Drawing.Size(238, 31);
            this.defaultVolLabel.TabIndex = 6;
            this.defaultVolLabel.Text = "Default Volume (0-200, joining users will be assigned this volume):";
            this.defaultVolLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // serverHostLabel
            // 
            this.serverHostLabel.AutoSize = true;
            this.serverHostLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.serverHostLabel.Location = new System.Drawing.Point(3, 62);
            this.serverHostLabel.Name = "serverHostLabel";
            this.serverHostLabel.Size = new System.Drawing.Size(238, 31);
            this.serverHostLabel.TabIndex = 4;
            this.serverHostLabel.Text = "Server Host/IP (Changing while connected requires app restart):";
            this.serverHostLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // serverPortLabel
            // 
            this.serverPortLabel.AutoSize = true;
            this.serverPortLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.serverPortLabel.Location = new System.Drawing.Point(3, 31);
            this.serverPortLabel.Name = "serverPortLabel";
            this.serverPortLabel.Size = new System.Drawing.Size(238, 31);
            this.serverPortLabel.TabIndex = 2;
            this.serverPortLabel.Text = "Server Port (Changing while connected requires app restart):";
            this.serverPortLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // ingameUsernameLabel
            // 
            this.ingameUsernameLabel.AutoSize = true;
            this.ingameUsernameLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ingameUsernameLabel.Location = new System.Drawing.Point(3, 0);
            this.ingameUsernameLabel.Name = "ingameUsernameLabel";
            this.ingameUsernameLabel.Size = new System.Drawing.Size(238, 31);
            this.ingameUsernameLabel.TabIndex = 0;
            this.ingameUsernameLabel.Text = "In-Game Username (Changing while connected requires app restart):";
            this.ingameUsernameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // igUsernameTextBox
            // 
            this.igUsernameTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.igUsernameTextBox.Location = new System.Drawing.Point(247, 3);
            this.igUsernameTextBox.Name = "igUsernameTextBox";
            this.igUsernameTextBox.Size = new System.Drawing.Size(362, 23);
            this.igUsernameTextBox.TabIndex = 1;
            // 
            // speakModeComboBox
            // 
            this.speakModeComboBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.speakModeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.speakModeComboBox.FormattingEnabled = true;
            this.speakModeComboBox.Items.AddRange(new object[] {
            "Always On",
            "Push-To-Talk",
            "Push-To-Mute"});
            this.speakModeComboBox.Location = new System.Drawing.Point(247, 127);
            this.speakModeComboBox.Name = "speakModeComboBox";
            this.speakModeComboBox.Size = new System.Drawing.Size(362, 23);
            this.speakModeComboBox.TabIndex = 20;
            // 
            // confirmButton
            // 
            this.settingsLayoutPanel.SetColumnSpan(this.confirmButton, 2);
            this.confirmButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.confirmButton.Location = new System.Drawing.Point(3, 282);
            this.confirmButton.Name = "confirmButton";
            this.confirmButton.Size = new System.Drawing.Size(606, 26);
            this.confirmButton.TabIndex = 25;
            this.confirmButton.Text = "Confirm";
            this.confirmButton.UseVisualStyleBackColor = true;
            this.confirmButton.Click += new System.EventHandler(this.confirmButton_Click);
            // 
            // SettingsUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(612, 311);
            this.Controls.Add(this.settingsLayoutPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "SettingsUI";
            this.Text = "Settings";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.onFormClosed);
            this.settingsLayoutPanel.ResumeLayout(false);
            this.settingsLayoutPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private TableLayoutPanel settingsLayoutPanel;
        private Label ingameUsernameLabel;
        private TextBox igUsernameTextBox;
        private Label toggleDeafenLabel;
        private Label toggleMuteLabel;
        private Label pushToGlobalLabel;
        private Label pushToTeamLabel;
        private Label speakModeLabel;
        private Label defaultVolLabel;
        private Label serverHostLabel;
        private Label serverPortLabel;
        private TextBox defaultVolumeTextBox;
        private TextBox serverHostTextBox;
        private TextBox serverPortTextBox;
        private TextBox toggleDeafenTextBox;
        private TextBox pushToActionTextBox;
        private TextBox pushToGlobalTextBox;
        private TextBox pushToTeamTextBox;
        private ComboBox speakModeComboBox;
        private Button confirmButton;
    }
}