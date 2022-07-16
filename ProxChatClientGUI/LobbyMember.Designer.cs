namespace ProxChatClientGUI
{
    partial class LobbyMember
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.userLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.muteButton = new System.Windows.Forms.Button();
            this.deafenButton = new System.Windows.Forms.Button();
            this.directSpeakButton = new System.Windows.Forms.Button();
            this.userPicture = new System.Windows.Forms.PictureBox();
            this.centerLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.volumeSlider = new System.Windows.Forms.TrackBar();
            this.usernameLabel = new System.Windows.Forms.Label();
            this.volumePercieved = new System.Windows.Forms.ProgressBar();
            this.userLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.userPicture)).BeginInit();
            this.centerLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.volumeSlider)).BeginInit();
            this.SuspendLayout();
            // 
            // userLayoutPanel
            // 
            this.userLayoutPanel.ColumnCount = 4;
            this.userLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.userLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this.userLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.userLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.userLayoutPanel.Controls.Add(this.muteButton, 1, 0);
            this.userLayoutPanel.Controls.Add(this.deafenButton, 1, 1);
            this.userLayoutPanel.Controls.Add(this.directSpeakButton, 3, 0);
            this.userLayoutPanel.Controls.Add(this.userPicture, 0, 0);
            this.userLayoutPanel.Controls.Add(this.centerLayoutPanel, 2, 0);
            this.userLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.userLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.userLayoutPanel.Name = "userLayoutPanel";
            this.userLayoutPanel.RowCount = 2;
            this.userLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.userLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.userLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.userLayoutPanel.Size = new System.Drawing.Size(500, 100);
            this.userLayoutPanel.TabIndex = 0;
            // 
            // muteButton
            // 
            this.muteButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.muteButton.Location = new System.Drawing.Point(103, 3);
            this.muteButton.Name = "muteButton";
            this.muteButton.Size = new System.Drawing.Size(44, 44);
            this.muteButton.TabIndex = 1;
            this.muteButton.Text = "mute";
            this.muteButton.UseVisualStyleBackColor = true;
            this.muteButton.Click += new System.EventHandler(this.muteButton_Click);
            // 
            // deafenButton
            // 
            this.deafenButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.deafenButton.Location = new System.Drawing.Point(103, 53);
            this.deafenButton.Name = "deafenButton";
            this.deafenButton.Size = new System.Drawing.Size(44, 44);
            this.deafenButton.TabIndex = 2;
            this.deafenButton.Text = "deaf";
            this.deafenButton.UseVisualStyleBackColor = true;
            this.deafenButton.Click += new System.EventHandler(this.deafenButton_Click);
            // 
            // directSpeakButton
            // 
            this.directSpeakButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.directSpeakButton.Location = new System.Drawing.Point(403, 3);
            this.directSpeakButton.Name = "directSpeakButton";
            this.userLayoutPanel.SetRowSpan(this.directSpeakButton, 2);
            this.directSpeakButton.Size = new System.Drawing.Size(94, 94);
            this.directSpeakButton.TabIndex = 3;
            this.directSpeakButton.Text = "direct";
            this.directSpeakButton.UseVisualStyleBackColor = true;
            this.directSpeakButton.MouseDown += new System.Windows.Forms.MouseEventHandler(this.directSpeakButton_Click);
            this.directSpeakButton.MouseLeave += new System.EventHandler(this.directSpeakButton_Release);
            this.directSpeakButton.MouseUp += new System.Windows.Forms.MouseEventHandler(this.directSpeakButton_Release);
            // 
            // userPicture
            // 
            this.userPicture.Dock = System.Windows.Forms.DockStyle.Fill;
            this.userPicture.Location = new System.Drawing.Point(3, 3);
            this.userPicture.Name = "userPicture";
            this.userLayoutPanel.SetRowSpan(this.userPicture, 2);
            this.userPicture.Size = new System.Drawing.Size(94, 94);
            this.userPicture.TabIndex = 6;
            this.userPicture.TabStop = false;
            // 
            // centerLayoutPanel
            // 
            this.centerLayoutPanel.ColumnCount = 1;
            this.centerLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.centerLayoutPanel.Controls.Add(this.volumeSlider, 0, 0);
            this.centerLayoutPanel.Controls.Add(this.usernameLabel, 0, 1);
            this.centerLayoutPanel.Controls.Add(this.volumePercieved, 0, 2);
            this.centerLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.centerLayoutPanel.Location = new System.Drawing.Point(153, 3);
            this.centerLayoutPanel.Name = "centerLayoutPanel";
            this.centerLayoutPanel.RowCount = 3;
            this.userLayoutPanel.SetRowSpan(this.centerLayoutPanel, 2);
            this.centerLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.centerLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.centerLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.centerLayoutPanel.Size = new System.Drawing.Size(244, 94);
            this.centerLayoutPanel.TabIndex = 7;
            // 
            // volumeSlider
            // 
            this.volumeSlider.Dock = System.Windows.Forms.DockStyle.Fill;
            this.volumeSlider.LargeChange = 10;
            this.volumeSlider.Location = new System.Drawing.Point(3, 3);
            this.volumeSlider.Maximum = 200;
            this.volumeSlider.Name = "volumeSlider";
            this.volumeSlider.Size = new System.Drawing.Size(238, 25);
            this.volumeSlider.TabIndex = 5;
            this.volumeSlider.TickStyle = System.Windows.Forms.TickStyle.None;
            this.volumeSlider.Value = 150;
            // 
            // usernameLabel
            // 
            this.usernameLabel.AutoSize = true;
            this.usernameLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.usernameLabel.Font = new System.Drawing.Font("Teko", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.usernameLabel.ForeColor = System.Drawing.Color.Navy;
            this.usernameLabel.Location = new System.Drawing.Point(3, 31);
            this.usernameLabel.Name = "usernameLabel";
            this.usernameLabel.Size = new System.Drawing.Size(238, 31);
            this.usernameLabel.TabIndex = 6;
            this.usernameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // volumePercieved
            // 
            this.volumePercieved.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.volumePercieved.Location = new System.Drawing.Point(3, 65);
            this.volumePercieved.Name = "volumePercieved";
            this.volumePercieved.Size = new System.Drawing.Size(238, 22);
            this.volumePercieved.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.volumePercieved.TabIndex = 5;
            // 
            // LobbyMember
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.userLayoutPanel);
            this.Name = "LobbyMember";
            this.Size = new System.Drawing.Size(500, 100);
            this.userLayoutPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.userPicture)).EndInit();
            this.centerLayoutPanel.ResumeLayout(false);
            this.centerLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.volumeSlider)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private TableLayoutPanel userLayoutPanel;
        private Button muteButton;
        private Button deafenButton;
        private Button directSpeakButton;
        private ProgressBar volumePercieved;
        private PictureBox userPicture;
        private TableLayoutPanel centerLayoutPanel;
        private TrackBar volumeSlider;
        private Label usernameLabel;
    }
}
