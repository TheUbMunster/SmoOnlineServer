//using DotNetOpenAuth;
//using DotNetOpenAuth.OAuth2;
using System.Web;
using System.Net;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Discord;
using System;
using System.Text;



// Use your client ID from Discord's developer site.
var discord = new Discord.Discord(clientId, (UInt64)Discord.CreateFlags.Default);
discord.SetLogHook(Discord.LogLevel.Debug, (level, message) =>
{
    Console.WriteLine("Log[{0}] {1}", level, message);
});

var applicationManager = discord.GetApplicationManager();
// Get the current locale. This can be used to determine what text or audio the user wants.
Console.WriteLine("Current Locale: {0}", applicationManager.GetCurrentLocale());
// Get the current branch. For example alpha or beta.
Console.WriteLine("Current Branch: {0}", applicationManager.GetCurrentBranch());

var userManager = discord.GetUserManager();
User? currentUser = null;

Dictionary<long, User> userCache = new Dictionary<long, User>();
// The auth manager fires events as information about the current user changes.
// This event will fire once on init.
//
// GetCurrentUser will error until this fires once.
userManager.OnCurrentUserUpdate += () =>
{
    currentUser = userManager.GetCurrentUser();
    userCache[currentUser.Value.Id] = currentUser.Value;
};

var lobbyManager = discord.GetLobbyManager();
lobbyManager.OnLobbyMessage += (lobbyID, userID, data) =>
{
    Console.WriteLine("lobby message: {0} {1}", lobbyID, Encoding.UTF8.GetString(data));
};

lobbyManager.OnSpeaking += (long lobbyId, long userId, bool speaking) =>
{
    Console.WriteLine($"{userId}{(userCache.ContainsKey(userId) ? $" ({userCache[userId].Username + "#" + userCache[userId].Discriminator})" : "")} is{(speaking ? "" : " not")} speaking");
};

lobbyManager.OnMemberConnect += (long lobbyId, long userId) =>
{
    userManager.GetUser(userId, (Result res, ref User user) =>
    {
        if (res != Result.Ok)
        {
            Console.WriteLine("GetUser failed in OnMemberConnect");
            return;
        }
        userCache[userId] = user;
    });
};

Lobby? lob = null;
bool success = false;
while (!success)
{
    Console.Write("Would you like to make a lobby (0), or join a lobby (1): ");
    string? inp = Console.ReadLine();
    if (inp == null)
    {
        Console.WriteLine("Null input, try again.");
        continue;
    }
    switch (inp.Trim())
    {
        case "0":
            {
                var trans = lobbyManager.GetLobbyCreateTransaction();
                trans.SetCapacity(4);
                trans.SetType(LobbyType.Private);
                lobbyManager.CreateLobby(trans, (Result res, ref Lobby lobby) =>
                {
                    if (res != Result.Ok)
                    {
                        Console.WriteLine("Something went wrong when creating the lobby");
                        return;
                    }
                    Console.WriteLine($"ID: {lobby.Id} Secret: {lobby.Secret}");
                    Console.WriteLine($"All users in the lobby: {string.Join(",\n", lobbyManager.GetMemberUsers(lobby.Id).Select(x => x.Username + "#" + x.Discriminator))}");
                    foreach (User u in lobbyManager.GetMemberUsers(lobby.Id))
                    {
                        userCache[u.Id] = u;
                    }

                    lobbyManager.ConnectVoice(lobby.Id, x =>
                    {
                        if (res != Result.Ok)
                        {
                            Console.WriteLine("Something went wrong when joining vc");
                        }
                        else
                        {
                            Console.WriteLine("Joined vc");
                        }
                    });

                    lobbyManager.ConnectNetwork(lobby.Id);
                    lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);

                    lob = lobby;
                });
                success = true;
            }
            break;
        case "1":
            {
                bool joinSuccess = false;
                while (!joinSuccess)
                {
                    Console.WriteLine("Enter in the lobby id: ");
                    string? id = Console.ReadLine();
                    Console.WriteLine("Enter in the lobby secret: ");
                    string? secret = Console.ReadLine();
                    if (id == null || secret == null)
                    {
                        continue;
                    }
                    lobbyManager.ConnectLobby(long.Parse(id), secret, (Result res, ref Lobby lobby) =>
                    {
                        if (res != Result.Ok)
                        {
                            Console.WriteLine("Something went wrong when joining the lobby");
                            return;
                        }
                        Console.WriteLine($"All users in the lobby: {string.Join(",\n", lobbyManager.GetMemberUsers(lobby.Id))}");
                        foreach (User u in lobbyManager.GetMemberUsers(lobby.Id))
                        {
                            userCache[u.Id] = u;
                        }

                        lobbyManager.ConnectVoice(lobby.Id, x =>
                        {
                            if (res != Result.Ok)
                            {
                                Console.WriteLine("Something went wrong when joining vc");
                            }
                            else
                            {
                                Console.WriteLine("Joined vc");
                            }
                        });
                        lobbyManager.ConnectNetwork(lobby.Id);
                        lob = lobby;
                    });
                    joinSuccess = true;
                }
                success = true;
            }
            break;
        default:
            Console.WriteLine("Enter in just a \"0\" or \"1\"");
            continue;
    }
}
while (currentUser == null)
{
    discord.RunCallbacks();
    Thread.Sleep(100);
}
VoiceManager vm = discord.GetVoiceManager();
try
{
    while (true)
    {
        ConsoleKeyInfo key = default;
        if (Console.KeyAvailable)
        {
            key = Console.ReadKey(true);
            while (Console.KeyAvailable)
            {
                Console.ReadKey(true);
            }
        }
        if (key.Key == ConsoleKey.V)
        {
            foreach (User u in userCache.Values)
            {
                if (u.Id == currentUser.Value.Id)
                    continue;
                if (vm.GetLocalVolume(u.Id) != 10)
                {
                    vm.SetLocalVolume(u.Id, 10);
                }
                else
                {
                    vm.SetLocalVolume(u.Id, 100);
                }
            }
        }
        if (key.Key == ConsoleKey.M)
        {
            foreach (User u in userCache.Values)
            {
                if (u.Id == currentUser.Value.Id)
                    continue;
                if (vm.IsLocalMute(u.Id))
                {
                    vm.SetLocalMute(u.Id, false);
                }
                else
                {
                    vm.SetLocalMute(u.Id, true);
                }
            }
        }
        discord.RunCallbacks();
        Thread.Sleep(100);
    }
}
finally
{
    discord.Dispose();
}

#region Old
//const string scope = "identify";//%20voice%20rpc%20rpc.voice.write"; //"voice";// "identify%20voice";//%20rpc.voice.write%20voice";
//const string redirect = "http%3A%2F%2Flocalhost%3A5022%2F";

//Console.WriteLine("Hello, World!");

//Discord.Discord discord = new Discord.Discord(long.Parse(clientID), (ulong)Discord.CreateFlags.Default);
//discord.SetLogHook(Discord.LogLevel.Debug, (level, message) =>
//{
//    Console.WriteLine("Log[{0}] {1}", level, message);
//});
////var voiceManager = discord.GetVoiceManager();
//string? usrnm = null;
//var userManager = discord.GetUserManager();
//userManager.OnCurrentUserUpdate += () =>
//{
//    var user = userManager.GetCurrentUser();
//    usrnm = user.Username;
//    //voiceManager.SetLocalMute(user.Id, true);
//};
//var ovr = discord.GetOverlayManager();
//ovr.OpenVoiceSettings((res) =>
//{
//    if (res == Discord.Result.Ok)
//    {
//        Console.WriteLine("OpenVoiceSettings");
//    }
//});

#endregion