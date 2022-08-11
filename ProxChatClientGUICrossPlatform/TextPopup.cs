using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;

namespace ProxChatClientGUICrossPlatform
{
    internal class TextPopup : Dialog
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
                Title = value;
            }
        }

        private string? infoRes;
        public string? InfoResult { get; private set; }

        [UI] private Label infoLabel;
        [UI] private Entry dataTextBox;
        [UI] private Button cancelButton;
        [UI] private Button confirmButton;

        public TextPopup() : this(new Builder("TextPopup.glade")) { }
        private TextPopup(Builder builder) : base(builder.GetRawOwnedObject("TextPopup"))
        {
            SetSizeRequest(500, 100);

            infoLabel = ((Label)builder.GetObject("infoLabel"));
            dataTextBox = ((Entry)builder.GetObject("dataTextBox"));
            cancelButton = ((Button)builder.GetObject("cancelButton"));
            confirmButton = ((Button)builder.GetObject("confirmButton"));

            dataTextBox.Changed += dataTextBox_TextChanged;
            cancelButton.Clicked += cancelButton_Click;
            confirmButton.Clicked += confirmButton_Click;
        }

        private void dataTextBox_TextChanged(object sender, EventArgs e)
        {
            infoRes = dataTextBox.Text;
        }

        private void confirmButton_Click(object sender, EventArgs e)
        {
            InfoResult = infoRes;
            Respond(ResponseType.Ok);
            Destroy();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Respond(ResponseType.Reject);
            Destroy();
        }

        public ResponseType ShowDialog(Window owner)
        {
            Parent = owner;
            SetPosition(WindowPosition.CenterOnParent);
            ShowAll();

            ResponseType resp = ResponseType.None;
            try
            {
                resp = (ResponseType)Run();
            }
            finally
            {
                Destroy();
            }

            return resp;
        }
    }
}
