namespace ProxChatClientGUI
{
    partial class ProxChat
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.topbarLayout = new System.Windows.Forms.TableLayoutPanel();
            this.connectionStatusRTB = new System.Windows.Forms.RichTextBox();
            this.identityLabel = new System.Windows.Forms.Label();
            this.settingsButton = new System.Windows.Forms.Button();
            this.connectDisconnectButton = new System.Windows.Forms.Button();
            this.mainTablePanel = new System.Windows.Forms.TableLayoutPanel();
            this.userTablePanel = new System.Windows.Forms.TableLayoutPanel();
            this.globalButton = new System.Windows.Forms.Button();
            this.teamButton = new System.Windows.Forms.Button();
            this.topbarLayout.SuspendLayout();
            this.mainTablePanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // topbarLayout
            // 
            this.topbarLayout.ColumnCount = 3;
            this.mainTablePanel.SetColumnSpan(this.topbarLayout, 2);
            this.topbarLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.topbarLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.topbarLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.topbarLayout.Controls.Add(this.connectionStatusRTB, 1, 0);
            this.topbarLayout.Controls.Add(this.identityLabel, 1, 1);
            this.topbarLayout.Controls.Add(this.settingsButton, 0, 0);
            this.topbarLayout.Controls.Add(this.connectDisconnectButton, 2, 0);
            this.topbarLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.topbarLayout.Location = new System.Drawing.Point(3, 3);
            this.topbarLayout.Name = "topbarLayout";
            this.topbarLayout.RowCount = 2;
            this.topbarLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.topbarLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.topbarLayout.Size = new System.Drawing.Size(678, 74);
            this.topbarLayout.TabIndex = 1;
            // 
            // connectionStatusRTB
            // 
            this.connectionStatusRTB.BackColor = System.Drawing.SystemColors.Control;
            this.connectionStatusRTB.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.connectionStatusRTB.Dock = System.Windows.Forms.DockStyle.Fill;
            this.connectionStatusRTB.Enabled = false;
            this.connectionStatusRTB.Font = new System.Drawing.Font("Monotxt_IV50", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.connectionStatusRTB.Location = new System.Drawing.Point(83, 3);
            this.connectionStatusRTB.Multiline = false;
            this.connectionStatusRTB.Name = "connectionStatusRTB";
            this.connectionStatusRTB.ReadOnly = true;
            this.connectionStatusRTB.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.None;
            this.connectionStatusRTB.Size = new System.Drawing.Size(512, 31);
            this.connectionStatusRTB.TabIndex = 0;
            this.connectionStatusRTB.Text = "ConnectionStatus";
            // 
            // identityLabel
            // 
            this.identityLabel.AutoSize = true;
            this.identityLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.identityLabel.Font = new System.Drawing.Font("Monotxt_IV50", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.identityLabel.Location = new System.Drawing.Point(83, 37);
            this.identityLabel.Name = "identityLabel";
            this.identityLabel.Size = new System.Drawing.Size(512, 37);
            this.identityLabel.TabIndex = 5;
            this.identityLabel.Text = "Discord: mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm#mmmm\r\nIn-Game: Moomoss";
            this.identityLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // settingsButton
            // 
            this.settingsButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.settingsButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.settingsButton.Location = new System.Drawing.Point(3, 3);
            this.settingsButton.Name = "settingsButton";
            this.topbarLayout.SetRowSpan(this.settingsButton, 2);
            this.settingsButton.Size = new System.Drawing.Size(74, 68);
            this.settingsButton.TabIndex = 2;
            this.settingsButton.Text = "settings";
            this.settingsButton.UseVisualStyleBackColor = true;
            // 
            // connectDisconnectButton
            // 
            this.connectDisconnectButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.connectDisconnectButton.Location = new System.Drawing.Point(601, 3);
            this.connectDisconnectButton.Name = "connectDisconnectButton";
            this.topbarLayout.SetRowSpan(this.connectDisconnectButton, 2);
            this.connectDisconnectButton.Size = new System.Drawing.Size(74, 68);
            this.connectDisconnectButton.TabIndex = 3;
            this.connectDisconnectButton.Text = "conn/disconn";
            this.connectDisconnectButton.UseVisualStyleBackColor = true;
            // 
            // mainTablePanel
            // 
            this.mainTablePanel.ColumnCount = 2;
            this.mainTablePanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 85F));
            this.mainTablePanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            this.mainTablePanel.Controls.Add(this.topbarLayout, 0, 0);
            this.mainTablePanel.Controls.Add(this.userTablePanel, 0, 1);
            this.mainTablePanel.Controls.Add(this.globalButton, 1, 1);
            this.mainTablePanel.Controls.Add(this.teamButton, 1, 2);
            this.mainTablePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainTablePanel.Location = new System.Drawing.Point(0, 0);
            this.mainTablePanel.Name = "mainTablePanel";
            this.mainTablePanel.RowCount = 3;
            this.mainTablePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.mainTablePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.mainTablePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.mainTablePanel.Size = new System.Drawing.Size(684, 361);
            this.mainTablePanel.TabIndex = 2;
            // 
            // userTablePanel
            // 
            this.userTablePanel.AutoScroll = true;
            this.userTablePanel.ColumnCount = 1;
            this.userTablePanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.userTablePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.userTablePanel.Location = new System.Drawing.Point(3, 83);
            this.userTablePanel.Name = "userTablePanel";
            this.userTablePanel.RowCount = 1;
            this.mainTablePanel.SetRowSpan(this.userTablePanel, 2);
            this.userTablePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.userTablePanel.Size = new System.Drawing.Size(575, 275);
            this.userTablePanel.TabIndex = 2;
            // 
            // globalButton
            // 
            this.globalButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.globalButton.Location = new System.Drawing.Point(584, 83);
            this.globalButton.Name = "globalButton";
            this.globalButton.Size = new System.Drawing.Size(97, 134);
            this.globalButton.TabIndex = 3;
            this.globalButton.Text = "global";
            this.globalButton.UseVisualStyleBackColor = true;
            // 
            // teamButton
            // 
            this.teamButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.teamButton.Location = new System.Drawing.Point(584, 223);
            this.teamButton.Name = "teamButton";
            this.teamButton.Size = new System.Drawing.Size(97, 135);
            this.teamButton.TabIndex = 4;
            this.teamButton.Text = "team";
            this.teamButton.UseVisualStyleBackColor = true;
            // 
            // ProxChat
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(684, 361);
            this.Controls.Add(this.mainTablePanel);
            this.MinimumSize = new System.Drawing.Size(700, 400);
            this.Name = "ProxChat";
            this.Text = "SMOOnline PVC Client";
            this.topbarLayout.ResumeLayout(false);
            this.topbarLayout.PerformLayout();
            this.mainTablePanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private TableLayoutPanel topbarLayout;
        private Button settingsButton;
        private TableLayoutPanel mainTablePanel;
        private TableLayoutPanel userTablePanel;
        private Button connectDisconnectButton;
        private Label identityLabel;
        private RichTextBox connectionStatusRTB;
        private Button globalButton;
        private Button teamButton;
    }
}