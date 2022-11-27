using Gtk;
using System;

namespace ProxChatClientGUICrossPlatform
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.Init();

            var app = new Application("org.ProxChatClientGUICrossPlatform.ProxChatClientGUICrossPlatform", GLib.ApplicationFlags.None);
            app.Register(GLib.Cancellable.Current);

            var win = new ProxChat();
            app.AddWindow(win);

            win.Show();
            Application.Run();
        }
    }
}
