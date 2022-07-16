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
    public partial class TextPopup : Form
    {
        public string InfoText
        {
            set
            {
                infoLabel.Text = value;
            }
        }

        public string LabelText
        {
            set
            {
                Text = value;
            }
        }

        private string? infoRes;
        public string? InfoResult { get; private set; }

        public TextPopup()
        {
            InitializeComponent();
            DialogResult = DialogResult.Abort;
        }

        private void dataTextBox_TextChanged(object sender, EventArgs e)
        {
            infoRes = dataTextBox.Text;
        }

        private void confirmButton_Click(object sender, EventArgs e)
        {
            InfoResult = infoRes;
            DialogResult = DialogResult.OK;
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
