namespace ProxChatClientGUI
{
    partial class TextPopup
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
         this.infoLabel = new System.Windows.Forms.Label();
         this.dataTextBox = new System.Windows.Forms.TextBox();
         this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
         this.confirmButton = new System.Windows.Forms.Button();
         this.cancelButton = new System.Windows.Forms.Button();
         this.tableLayoutPanel1.SuspendLayout();
         this.SuspendLayout();
         // 
         // infoLabel
         // 
         this.infoLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
         this.infoLabel.AutoSize = true;
         this.tableLayoutPanel1.SetColumnSpan(this.infoLabel, 2);
         this.infoLabel.Location = new System.Drawing.Point(6, 0);
         this.infoLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
         this.infoLabel.Name = "infoLabel";
         this.infoLabel.Size = new System.Drawing.Size(1025, 37);
         this.infoLabel.TabIndex = 0;
         this.infoLabel.Text = "infoLabel";
         this.infoLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
         // 
         // dataTextBox
         // 
         this.tableLayoutPanel1.SetColumnSpan(this.dataTextBox, 2);
         this.dataTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
         this.dataTextBox.Location = new System.Drawing.Point(6, 98);
         this.dataTextBox.Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
         this.dataTextBox.Name = "dataTextBox";
         this.dataTextBox.Size = new System.Drawing.Size(1025, 43);
         this.dataTextBox.TabIndex = 1;
         this.dataTextBox.TextChanged += new System.EventHandler(this.dataTextBox_TextChanged);
         // 
         // tableLayoutPanel1
         // 
         this.tableLayoutPanel1.ColumnCount = 2;
         this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
         this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
         this.tableLayoutPanel1.Controls.Add(this.confirmButton, 0, 2);
         this.tableLayoutPanel1.Controls.Add(this.infoLabel, 0, 0);
         this.tableLayoutPanel1.Controls.Add(this.dataTextBox, 0, 1);
         this.tableLayoutPanel1.Controls.Add(this.cancelButton, 0, 2);
         this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
         this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
         this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
         this.tableLayoutPanel1.Name = "tableLayoutPanel1";
         this.tableLayoutPanel1.RowCount = 3;
         this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33332F));
         this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33332F));
         this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33335F));
         this.tableLayoutPanel1.Size = new System.Drawing.Size(1037, 274);
         this.tableLayoutPanel1.TabIndex = 2;
         // 
         // confirmButton
         // 
         this.confirmButton.Dock = System.Windows.Forms.DockStyle.Fill;
         this.confirmButton.Location = new System.Drawing.Point(524, 189);
         this.confirmButton.Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
         this.confirmButton.Name = "confirmButton";
         this.confirmButton.Size = new System.Drawing.Size(507, 78);
         this.confirmButton.TabIndex = 3;
         this.confirmButton.Text = "Confirm";
         this.confirmButton.UseVisualStyleBackColor = true;
         this.confirmButton.Click += new System.EventHandler(this.confirmButton_Click);
         // 
         // cancelButton
         // 
         this.cancelButton.Dock = System.Windows.Forms.DockStyle.Fill;
         this.cancelButton.Location = new System.Drawing.Point(6, 189);
         this.cancelButton.Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
         this.cancelButton.Name = "cancelButton";
         this.cancelButton.Size = new System.Drawing.Size(506, 78);
         this.cancelButton.TabIndex = 2;
         this.cancelButton.Text = "Cancel";
         this.cancelButton.UseVisualStyleBackColor = true;
         this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
         // 
         // TextPopup
         // 
         this.AcceptButton = this.confirmButton;
         this.AutoScaleDimensions = new System.Drawing.SizeF(15F, 37F);
         this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
         this.CancelButton = this.cancelButton;
         this.ClientSize = new System.Drawing.Size(1037, 274);
         this.Controls.Add(this.tableLayoutPanel1);
         this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
         this.Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
         this.MaximizeBox = false;
         this.MinimizeBox = false;
         this.Name = "TextPopup";
         this.ShowInTaskbar = false;
         this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
         this.Text = "TextPopup";
         this.tableLayoutPanel1.ResumeLayout(false);
         this.tableLayoutPanel1.PerformLayout();
         this.ResumeLayout(false);

        }

        #endregion

        private Label infoLabel;
        private TextBox dataTextBox;
        private TableLayoutPanel tableLayoutPanel1;
        private Button cancelButton;
        private Button confirmButton;
    }
}