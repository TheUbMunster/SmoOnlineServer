using Gtk;
using System;
using UI = Gtk.Builder.ObjectAttribute;
using IOPath = System.IO.Path;

namespace ProxChatClientGUICrossPlatform
{
    internal class MainWindow : Window
    {
        [UI] private readonly Image settingsGearImage;
        [UI] private readonly Image connectImage;
        [UI] private readonly Image disconnectImage;
        [UI] private readonly Image directImage;
        [UI] private readonly Image teamImage;
        [UI] private readonly Image globalImage;
        [UI] private readonly Image headphonesImage;
        [UI] private readonly Image headphonesCrossedImage;
        [UI] private readonly Image micImage;
        [UI] private readonly Image micCrossedImage;
        [UI] private Button settingsButton;
        public MainWindow() : this(new Builder("MainWindow.glade")) { }

        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);
            DeleteEvent += Window_DeleteEvent;
            //populate UI fields
            settingsButton = (Button)builder.GetObject("settingsButton");

            settingsGearImage = ResizeImage(IOPath.Join("Images", "Gear.png"), 75, 75);
            settingsGearImage.Show();
            settingsButton.Add(settingsGearImage);

            connectImage = ResizeImage(IOPath.Join("Images", "connect.png"), 75, 75);
            connectImage.Show();

            disconnectImage = ResizeImage(IOPath.Join("Images", "disconnect.png"), 75, 75);
            disconnectImage.Show();

            //settings of UI fields
            this.SetSizeRequest(700, 400); //set minimum size of main window

            //settingsButton.Clicked += (_,_) => { Application.Quit(); }; //testing

            settingsButton.SetSizeRequest(75, 75);
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
        }

        #region Utility
        private static Image ResizeImage(string path, int width, int height)
        {
            Gdk.Pixbuf pixbuf = new Gdk.Pixbuf(path);
            pixbuf = pixbuf.ScaleSimple(width, height, Gdk.InterpType.Bilinear);
            return new Image(pixbuf);
        }
        #endregion
    }
}