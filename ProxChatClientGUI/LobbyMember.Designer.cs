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
            this.userImage = new System.Windows.Forms.Label();
            this.muteButton = new System.Windows.Forms.Button();
            this.deafenButton = new System.Windows.Forms.Button();
            this.directSpeakButton = new System.Windows.Forms.Button();
            this.volumeSlider = new System.Windows.Forms.TrackBar();
            this.volumePercieved = new System.Windows.Forms.ProgressBar();
            this.userLayoutPanel.SuspendLayout();
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
            this.userLayoutPanel.Controls.Add(this.userImage, 0, 0);
            this.userLayoutPanel.Controls.Add(this.muteButton, 1, 0);
            this.userLayoutPanel.Controls.Add(this.deafenButton, 1, 1);
            this.userLayoutPanel.Controls.Add(this.directSpeakButton, 3, 0);
            this.userLayoutPanel.Controls.Add(this.volumeSlider, 2, 0);
            this.userLayoutPanel.Controls.Add(this.volumePercieved, 2, 1);
            this.userLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.userLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.userLayoutPanel.Name = "userLayoutPanel";
            this.userLayoutPanel.RowCount = 2;
            this.userLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.userLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.userLayoutPanel.Size = new System.Drawing.Size(500, 100);
            this.userLayoutPanel.TabIndex = 0;
            // 
            // userImage
            // 
            this.userImage.AutoSize = true;
            this.userImage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.userImage.Location = new System.Drawing.Point(3, 0);
            this.userImage.Name = "userImage";
            this.userLayoutPanel.SetRowSpan(this.userImage, 2);
            this.userImage.Size = new System.Drawing.Size(94, 100);
            this.userImage.TabIndex = 0;
            this.userImage.Text = "image";
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
            // volumeSlider
            // 
            this.volumeSlider.Dock = System.Windows.Forms.DockStyle.Fill;
            this.volumeSlider.LargeChange = 10;
            this.volumeSlider.Location = new System.Drawing.Point(153, 3);
            this.volumeSlider.Maximum = 200;
            this.volumeSlider.Name = "volumeSlider";
            this.volumeSlider.Size = new System.Drawing.Size(244, 44);
            this.volumeSlider.TabIndex = 4;
            this.volumeSlider.TickStyle = System.Windows.Forms.TickStyle.None;
            this.volumeSlider.Value = 150;
            this.volumeSlider.Scroll += new System.EventHandler(this.volumeSlider_Scroll);
            // 
            // volumePercieved
            // 
            this.volumePercieved.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.volumePercieved.Location = new System.Drawing.Point(153, 53);
            this.volumePercieved.Name = "volumePercieved";
            this.volumePercieved.Size = new System.Drawing.Size(244, 22);
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
            this.userLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.volumeSlider)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private TableLayoutPanel userLayoutPanel;
        private Label userImage;
        private Button muteButton;
        private Button deafenButton;
        private Button directSpeakButton;
        private TrackBar volumeSlider;
        private ProgressBar volumePercieved;
    }
}
