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
using static Shared.Constants;

//TODO: put all the discord stuff in another thread so the main thread can be used for commands in the console.

// Use your client ID from Discord's developer site.
var discord = new Discord.Discord(clientId, (UInt64)Discord.CreateFlags.Default);
discord.SetLogHook(Discord.LogLevel.Debug, (level, message) =>
{
    Console.WriteLine("Log[{0}] {1}", level, message);
});

//var applicationManager = discord.GetApplicationManager();
//// Get the current locale. This can be used to determine what text or audio the user wants.
//Console.WriteLine("Current Locale: {0}", applicationManager.GetCurrentLocale());
//// Get the current branch. For example alpha or beta.
//Console.WriteLine("Current Branch: {0}", applicationManager.GetCurrentBranch());

var userManager = discord.GetUserManager();
User? currentUser = null;

byte defaultVol = 100;
Dictionary<long, User> userCache = new Dictionary<long, User>();
Dictionary<string, long> nameToIdCache = new Dictionary<string, long>();
Dictionary<long, byte> userVolumes = new Dictionary<long, byte>();
// The auth manager fires events as information about the current user changes.
// This event will fire once on init.
//
// GetCurrentUser will error until this fires once.
userManager.OnCurrentUserUpdate += () =>
{
    currentUser = userManager.GetCurrentUser();
    userCache[currentUser.Value.Id] = currentUser.Value;
    nameToIdCache[currentUser.Value.Username + "#" + currentUser.Value.Discriminator] = currentUser.Value.Id;
    //broadcast change if in lobby
};
while (currentUser == null)
{
    discord.RunCallbacks();
    Thread.Sleep(100);
}
var lobbyManager = discord.GetLobbyManager();
//lobbyManager.OnLobbyMessage += (lobbyID, userID, data) =>
//{
//    Console.WriteLine("lobby message: {0} {1}", lobbyID, Encoding.UTF8.GetString(data));
//};
VoiceManager vm = discord.GetVoiceManager();

lobbyManager.OnNetworkMessage += (long lobbyId, long userId, byte channelId, byte[] data) =>
{
    if (data.Length != 80)
    {
        Console.WriteLine($"Recieved invalid message from {(userCache.ContainsKey(userId) ? userCache[userId].Username + "#" + userCache[userId].Discriminator : userId)}.");
    }
    else
    {
        //update volumes.
        string raw = Encoding.Unicode.GetString(data);
        string user = raw.Substring(0, (32 + 5) * 2); //(max usrname len + # + discrim) * 2 bytes per char
        string vol = raw.Substring(75, 6);
        if (string.IsNullOrEmpty(vol))
        {
            //default volume for this user.
            vm.SetLocalVolume(userCache[nameToIdCache[user]].Id, userVolumes[userCache[nameToIdCache[user]].Id]);
        }
        else
        {
            byte bvol = byte.Parse(vol.Trim());
            float fvol = (bvol / 100f);
            byte finalVolume = (byte)(fvol * userVolumes[userCache[nameToIdCache[user]].Id]);
            vm.SetLocalVolume(userCache[nameToIdCache[user]].Id, finalVolume);
        }
    }
};

//lobbyManager.OnSpeaking += (long lobbyId, long userId, bool speaking) =>
//{
//    Console.WriteLine($"{userId}{(userCache.ContainsKey(userId) ? $" ({userCache[userId].Username + "#" + userCache[userId].Discriminator})" : "")} is{(speaking ? "" : " not")} speaking");
//};
//sendnetworkmessage/onnetworkmessage
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
        nameToIdCache[user.Username + "#" + user.Discriminator] = user.Id;
        userVolumes[userId] = defaultVol;
    });
};

lobbyManager.OnMemberDisconnect += (long lobbyId, long userId) =>
{
    nameToIdCache.Remove(userCache[userId].Username + "#" + userCache[userId].Discriminator);
    userCache.Remove(userId);
    userVolumes.Remove(userId);
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
                    //should just be you in the lobby
                    //Console.WriteLine($"All users in the lobby: {string.Join(",\n", lobbyManager.GetMemberUsers(lobby.Id).Select(x => x.Username + "#" + x.Discriminator))}");
                    //foreach (User u in lobbyManager.GetMemberUsers(lobby.Id))
                    //{
                    //    userCache[u.Id] = u;
                    //    nameToIdCache[u.Username + "#" + u.Discriminator] = u.Id;
                    //}

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
                    //UpdateActivity(discord, lobby);
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
                            if (u.Id != currentUser.Value.Id)
                            {
                                userCache[u.Id] = u;
                                nameToIdCache[u.Username + "#" + u.Discriminator] = u.Id;
                                userVolumes[u.Id] = defaultVol;
                            }
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


try
{
    while (true)
    {
        //ConsoleKeyInfo key = default;
        //if (Console.KeyAvailable)
        //{
        //    key = Console.ReadKey(true);
        //    while (Console.KeyAvailable)
        //    {
        //        Console.ReadKey(true);
        //    }
        //}
        //if (key.Key == ConsoleKey.V)
        //{
        //    foreach (User u in userCache.Values)
        //    {
        //        if (u.Id == currentUser.Value.Id)
        //            continue;
        //        if (vm.GetLocalVolume(u.Id) != 10)
        //        {
        //            vm.SetLocalVolume(u.Id, 10);
        //        }
        //        else
        //        {
        //            vm.SetLocalVolume(u.Id, 100);
        //        }
        //    }
        //}
        //if (key.Key == ConsoleKey.M)
        //{
        //    foreach (User u in userCache.Values)
        //    {
        //        if (u.Id == currentUser.Value.Id)
        //            continue;
        //        if (vm.IsLocalMute(u.Id))
        //        {
        //            vm.SetLocalMute(u.Id, false);
        //        }
        //        else
        //        {
        //            vm.SetLocalMute(u.Id, true);
        //        }
        //    }
        //}
        discord.RunCallbacks();
        Thread.Sleep(100);
    }
}
finally
{
    discord.Dispose();
}

//return;

//static void UpdateActivity(Discord.Discord discord, Discord.Lobby lobby)
//{
//    var activityManager = discord.GetActivityManager();
//    var lobbyManager = discord.GetLobbyManager();

//    var activity = new Discord.Activity
//    {
//        State = "olleh",
//        Details = "foo details",
//        Timestamps =
//            {
//                Start = 5,
//                End = 6,
//            },
//        Assets =
//            {
//                LargeImage = "foo largeImageKey",
//                LargeText = "foo largeImageText",
//                SmallImage = "foo smallImageKey",
//                SmallText = "foo smallImageText",
//            },
//        Party = {
//               Id = lobby.Id.ToString(),
//               Size = {
//                    CurrentSize = lobbyManager.MemberCount(lobby.Id),
//                    MaxSize = (int)lobby.Capacity,
//                },
//            },
//        Secrets = {
//                Join = lobbyManager.GetLobbyActivitySecret(lobby.Id),
//            },
//        Instance = true,
//    };

//    activityManager.UpdateActivity(activity, result =>
//    {
//        Console.WriteLine("Update Activity {0}", result);

//        // Send an invite to another user for this activity.
//        // Receiver should see an invite in their DM.
//        // Use a relationship user's ID for this.
//        // activityManager
//        //   .SendInvite(
//        //       364843917537050624,
//        //       Discord.ActivityActionType.Join,
//        //       "",
//        //       inviteResult =>
//        //       {
//        //           Console.WriteLine("Invite {0}", inviteResult);
//        //       }
//        //   );
//    });
//}

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