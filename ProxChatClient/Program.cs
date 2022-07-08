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
using System.Threading.Tasks;
using Shared;
using static Shared.Constants;

object lockKey = new object(); //for locks to prevent race conditions

byte defaultVol = 100;
Dictionary<long, User> userCache = new Dictionary<long, User>();
Dictionary<string, long> nameToIdCache = new Dictionary<string, long>();
Dictionary<long, byte> userPrefVolumes = new Dictionary<long, byte>(); //the volumes adjusted by the user (nothing to do with proximity).
Queue<Action> messageQueue = new Queue<Action>();

Discord.Discord discord = null!;
LobbyManager lobbyManager = null!;
VoiceManager voiceManager = null!;

bool proxMode = false;
User? currentUser = null;
Lobby? lob = null;

Task discordTask = Task.Run(() =>
{
    #region Setup
    discord = new Discord.Discord(clientId, (UInt64)Discord.CreateFlags.Default);
    lobbyManager = discord.GetLobbyManager();
    voiceManager = discord.GetVoiceManager();
    // Use your client ID from Discord's developer site.
    discord.SetLogHook(Discord.LogLevel.Debug, (level, message) =>
    {
        Console.WriteLine("Log[{0}] {1}", level, message); //I've never seen this print anything?
    });
    #endregion

    #region Current User aquisition
    var userManager = discord.GetUserManager();
    // GetCurrentUser will error until this fires once.
    userManager.OnCurrentUserUpdate += () =>
    {
        currentUser = userManager.GetCurrentUser();
        userCache[currentUser.Value.Id] = currentUser.Value;
        nameToIdCache[currentUser.Value.Username + "#" + currentUser.Value.Discriminator] = currentUser.Value.Id;
        //broadcast change if in lobby (just use OnMemberUpdate instead)
    };
    while (currentUser == null)
    {
        discord.RunCallbacks(); //do this until we have the current user.
        Thread.Sleep(50);
    }
    #endregion

    #region Event subscriptions
    //lobbyManager.OnLobbyMessage += (lobbyID, userID, data) =>
    //{
    //    Console.WriteLine("lobby message: {0} {1}", lobbyID, Encoding.UTF8.GetString(data));
    //};
    //lobbyManager.OnSpeaking += (long lobbyId, long userId, bool speaking) =>
    //{
    //    Console.WriteLine($"{userId}{(userCache.ContainsKey(userId) ? $" ({userCache[userId].Username + "#" + userCache[userId].Discriminator})" : "")} is{(speaking ? "" : " not")} speaking");
    //};

    //decode and apply message to change another user's volume.
    lobbyManager.OnNetworkMessage += (long lobbyId, long userId, byte channelId, byte[] data) =>
    {
        if (data.Length != 80)
        {
            Console.WriteLine($"Recieved invalid message from {(userCache.ContainsKey(userId) ? userCache[userId].Username + "#" + userCache[userId].Discriminator : userId)}.");
        }
        else
        {
            if (proxMode)
            {
                //update volumes.
                string raw = Encoding.Unicode.GetString(data);
                string user = raw.Substring(0, (32 + 5) * 2); //(max usrname len + # + discrim) * 2 bytes per char
                string vol = raw.Substring(75, 6);
                if (string.IsNullOrEmpty(vol))
                {
                    //default volume for this user.
                    voiceManager.SetLocalVolume(userCache[nameToIdCache[user]].Id, userPrefVolumes[userCache[nameToIdCache[user]].Id]);
                }
                else
                {
                    byte bvol = byte.Parse(vol.Trim());
                    float fvol = (bvol / 100f);
                    byte finalVolume = (byte)(fvol * userPrefVolumes[userCache[nameToIdCache[user]].Id]);
                    voiceManager.SetLocalVolume(userCache[nameToIdCache[user]].Id, finalVolume);
                }
            }
        }
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
            nameToIdCache[user.Username + "#" + user.Discriminator] = user.Id;
            userPrefVolumes[userId] = defaultVol;
            voiceManager.SetLocalVolume(userId, defaultVol);
        });
    };

    lobbyManager.OnMemberDisconnect += (long lobbyId, long userId) =>
    {
        nameToIdCache.Remove(userCache[userId].Username + "#" + userCache[userId].Discriminator);
        userCache.Remove(userId);
        userPrefVolumes.Remove(userId);
    };
    #endregion

    #region Callback loop
    // run callback loop
    try
    {
        while (true)
        {
            lock (lockKey)
            {
                while (messageQueue.Count > 0)
                {
                    messageQueue.Dequeue()(); //double parens? don't do this often in c#!
                }
            }
            discord.RunCallbacks();
            Thread.Sleep(16); //1000 / 16 = ~60 times a second (not including the time it takes to do the callbacks
                              //themselves e.g. if callbacks take ~4ms, then it's only 50 times a second.
        }
    }
    finally
    {
        discord.Dispose();
    }
    #endregion
});

//TODO: ADD KEYBINDS

//TODO: VISUAL UI

//TODO: ADD LOG FILE FEATURE (something to do with the discord log too?)

#region Command Registry
CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length != 0)
    {
        return "Usage: deafen (no arguments.)";
    }
    else
    {
        lock (lockKey)
        {
            messageQueue.Enqueue(new Action(() =>
            {
                voiceManager.SetSelfDeaf(true);
                voiceManager.SetSelfMute(true);
            }));
        }
        return "You have been deafened.";
    }
}, "deafen", "d");

CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length != 0)
    {
        return "Usage: undeafen (no arguments.)";
    }
    else
    {
        lock (lockKey)
        {
            messageQueue.Enqueue(new Action(() =>
            {
                voiceManager.SetSelfDeaf(false);
                voiceManager.SetSelfMute(false);
            }));
        }
        return "You have been undeafened.";
    }
}, "undeafen", "ud");

CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length != 0)
    {
        return "Usage: vollist (no arguments.)";
    }
    else
    {
        lock (lockKey)
        {
            messageQueue.Enqueue(new Action(() =>
            {
                StringBuilder resultMessage = new StringBuilder();
                //lock for critical section handled around the dequeue in the discord loop
                foreach (var kvp in nameToIdCache)
                {
                    if (kvp.Value != currentUser.Value.Id)
                    {
                        resultMessage.Append($"{kvp.Key}: {userPrefVolumes[kvp.Value]}\n");
                    }
                    else
                    {
                        resultMessage.Append($"{kvp.Key}: {userPrefVolumes[kvp.Value]} ({(voiceManager.IsSelfDeaf() ? "deafened" : "not deafened")}) " +
                            $"({(voiceManager.IsSelfMute() ? "muted" : "not muted")})\n");
                    }
                }
                Console.Write(resultMessage.ToString());
            }));
        }
        return "";
    }
}, "vollist", "vl");

CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length == 1)
    {
        //set to default.
        lock (lockKey) //lock for enqueue
        {
            messageQueue.Enqueue(new Action(() =>
            {
                //lock for critical section handled around the dequeue in the discord loop
                if (nameToIdCache.ContainsKey(args[0]))
                {
                    long userId = nameToIdCache[args[0]];
                    if (proxMode)
                    {
                        float ratio = ((float)defaultVol) / userPrefVolumes[userId];
                        byte oldVol = voiceManager.GetLocalVolume(userId);
                        byte newVol = (byte)(oldVol * ratio);
                        voiceManager.SetLocalVolume(userId, newVol);
                    }
                    else
                    {
                        voiceManager.SetLocalVolume(userId, defaultVol);
                    }
                    userPrefVolumes[userId] = defaultVol;
                    Console.WriteLine($"{args[0]}'s volume was set to {defaultVol}.");
                }
                else
                {
                    Console.WriteLine("That user does not appear to be in the lobby. No action was taken.");
                }
            }));
        }
        return "";
    }
    else if (args.Length == 2)
    {
        if (byte.TryParse(args[1], out var vol))
        {
            lock (lockKey) //lock for enqueue
            {
                messageQueue.Enqueue(new Action(() =>
                {
                    //lock for critical section handled around the dequeue in the discord loop
                    if (nameToIdCache.ContainsKey(args[0]))
                    {
                        long userId = nameToIdCache[args[0]];
                        if (proxMode)
                        {
                            float ratio = ((float)vol) / userPrefVolumes[userId];
                            byte oldVol = voiceManager.GetLocalVolume(userId);
                            byte newVol = (byte)(oldVol * ratio);
                            voiceManager.SetLocalVolume(userId, newVol);
                        }
                        else
                        {
                            voiceManager.SetLocalVolume(userId, vol);
                        }
                        userPrefVolumes[userId] = vol;
                        Console.WriteLine($"{args[0]}'s volume was set to {vol}.");
                    }
                    else
                    {
                        Console.WriteLine("That user does not appear to be in the lobby. No action was taken.");
                    }
                }));
            }
            return "";
        }
        else
        {
            return "Usage: volset <username> <optional: volume 0-200> (volume must be a number.)";
        }
    }
    else
    {
        return "Usage: volset <username> <optional: volume 0-200>";
    }
}, "volset", "vs");

CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length != 0)
    {
        return "Usage: voldefaultall (no arguments.)";
    }
    else
    {
        lock (lockKey) //lock for enqueue
        {
            messageQueue.Enqueue(new Action(() =>
            {
                //lock for critical section handled around the dequeue in the discord loop
                foreach (var userId in userCache.Keys)
                {
                    if (userId != currentUser.Value.Id)
                    {
                        if (proxMode)
                        {
                            float ratio = ((float)defaultVol) / userPrefVolumes[userId];
                            byte oldVol = voiceManager.GetLocalVolume(userId);
                            byte newVol = (byte)(oldVol * ratio);
                            voiceManager.SetLocalVolume(userId, newVol);
                        }
                        else
                        {
                            voiceManager.SetLocalVolume(userId, defaultVol);
                        }
                        userPrefVolumes[userId] = defaultVol;
                    }
                }
                Console.WriteLine($"Set the volumes of all of the users to the default volume of {defaultVol}.");
            }));
        }
        return "";
    }
}, "voldefaultall", "vda");

CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length != 1 || !byte.TryParse(args[0].Trim(), out var value))
    {
        return "Usage: volsetdefault <volume 0-200>";
    }
    else
    {
        lock (lockKey) //avoid race conditions.
        {
            defaultVol = value;
        }
        return $"Set the default volume to {value}, any new users joining will be set to this volume.";
    }
}, "volsetdefault", "vsd");

CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length != 2)
    {
        return "Usage: joinlobby <lobby id> <lobby secret>";
    }
    else
    {
        lock (lockKey) //lock so we can enqueue
        {
            messageQueue.Enqueue(new Action(() =>
            {
                //lock for critical section handled around the dequeue in the discord loop
                lobbyManager.ConnectLobby(long.Parse(args[0]), args[1], (Result res, ref Lobby lobby) =>
                {
                    StringBuilder resultMessage = new StringBuilder(); //aggregating so that messages don't get
                                                                       //mixed up with other threads.
                    if (res != Result.Ok)
                    {
                        Console.WriteLine("Something went wrong when joining the lobby.");
                        return;
                    }
                    else
                    {
                        resultMessage.Append("Joined the lobby successfully.\n");
                    }
                    IEnumerable<User> users = lobbyManager.GetMemberUsers(lobby.Id);
                    resultMessage.Append("All users in the lobby: " +
                        $"{string.Join(",\n", users.Select(x => $"{x.Id}: {x.Username}#{x.Discriminator}"))}\n");
                    foreach (User u in lobbyManager.GetMemberUsers(lobby.Id))
                    {
                        if (u.Id != currentUser.Value.Id)
                        {
                            userCache[u.Id] = u;
                            nameToIdCache[u.Username + "#" + u.Discriminator] = u.Id;
                            userPrefVolumes[u.Id] = defaultVol;
                            voiceManager.SetLocalVolume(u.Id, defaultVol);
                        }
                    }

                    lobbyManager.ConnectVoice(lobby.Id, x =>
                    {
                        if (res != Result.Ok)
                        {
                            resultMessage.Append("Something went wrong when joining vc.\n");
                        }
                        else
                        {
                            resultMessage.Append("Joined vc.\n");
                        }
                    });
                    Console.Write(resultMessage.ToString());
                    lobbyManager.ConnectNetwork(lobby.Id);
                    lob = lobby;
                });
            }));
        }
        return ""; //result message will be printed through the discord loop instead.
    }
}, "joinlobby", "jl");

CommandHandler.RegisterCommandAliases(args =>
{
    //clear everything
    if (args.Length != 0)
    {
        return "Usage: leavelobby (no arguments.)";
    }
    else
    {
        if (lob != null)
        {
            lock (lockKey)
            {
                messageQueue.Enqueue(new Action(() =>
                {
                    lobbyManager.DisconnectLobby(lob.Value.Id, res =>
                    {
                        if (res != Result.Ok)
                        {
                            Console.WriteLine("You left the lobby");
                            //empty the dictionaries
                        }
                        else
                        {
                            Console.WriteLine($"Something went wrong with leaving the lobby: {res.ToString()}");
                        }
                    });
                }));
            }
            return "";
        }
        else
        {
            return "You aren't in a lobby to begin with! (No action was taken.)";
        }
    }
}, "leavelobby", "ll");

CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length != 0)
    {
        return "Usage: proxon (No arguments.)";
    }
    else
    {
        proxMode = true;
        lock (lockKey) //lock for enqueue
        {
            messageQueue.Enqueue(new Action(() =>
            {
                //lock for critical section handled around the dequeue in the discord loop
                foreach (var userId in userCache.Keys)
                {
                    if (userId != currentUser.Value.Id)
                    {
                        voiceManager.SetLocalVolume(userId, 0);
                    }
                }
                Console.WriteLine($"Successfully enabled voice proximity.");
            }));
        }
        return "";
    }
}, "proxon", "pon");

CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length != 0)
    {
        return "Usage: proxoff (No arguments.)";
    }
    else
    {
        proxMode = false;
        lock (lockKey) //lock for enqueue
        {
            messageQueue.Enqueue(new Action(() =>
            {
                //lock for critical section handled around the dequeue in the discord loop
                foreach (var userId in userCache.Keys)
                {
                    if (userId != currentUser.Value.Id)
                    {
                        voiceManager.SetLocalVolume(userId, userPrefVolumes[userId]);
                    }
                }
                Console.WriteLine($"Successfully disabled voice proximity.");
            }));
        }
        return "";
    }
}, "proxoff", "poff");

CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length == 0)
    {
        lock (lockKey)
        {
            messageQueue.Enqueue(new Action(() =>
            {
                voiceManager.SetSelfMute(true);
            }));
        }
        return "You have been muted.";
    }
    else if (args.Length == 1)
    {
        if (nameToIdCache.ContainsKey(args[0]))
        {
            lock (lockKey)
            {
                messageQueue.Enqueue(new Action(() =>
                {
                    voiceManager.SetLocalMute(nameToIdCache[args[0]], true);
                }));
            }
            return $"The user \"{args[0]}\" has been muted locally.";
        }
        else
        {
            return $"The user \"{args[0]}\" doesn't appear to be in the lobby. (No action was taken.)";
        }
    }
    else
    {
        return "Usage: mute <optional: username>";
    }
}, "mute", "m");

CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length == 0)
    {
        lock (lockKey)
        {
            messageQueue.Enqueue(new Action(() =>
            {
                voiceManager.SetSelfMute(false);
            }));
        }
        return "You have been unmuted.";
    }
    else if (args.Length == 1)
    {
        if (nameToIdCache.ContainsKey(args[0]))
        {
            lock (lockKey)
            {
                messageQueue.Enqueue(new Action(() =>
                {
                    voiceManager.SetLocalMute(nameToIdCache[args[0]], false);
                }));
            }
            return $"The user \"{args[0]}\" has been unmuted locally.";
        }
        else
        {
            return $"The user \"{args[0]}\" doesn't appear to be in the lobby. (No action was taken.)";
        }
    }
    else
    {
        return "Usage: unmute <optional: username>";
    }
}, "unmute", "um");

//testing purposes only
CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length != 0)
    {
        return "Usage: startlobby (No arguments.)";
    }
    else
    {
        messageQueue.Enqueue(new Action(() =>
        {
            var trans = lobbyManager.GetLobbyCreateTransaction();
            trans.SetCapacity(4);
            trans.SetType(LobbyType.Private);
            lobbyManager.CreateLobby(trans, (Result res, ref Lobby lobby) =>
            {
                StringBuilder resultMessage = new StringBuilder();
                if (res != Result.Ok)
                {
                    Console.WriteLine("Something went wrong when creating the lobby");
                    return;
                }
                resultMessage.Append($"ID: {lobby.Id} Secret: {lobby.Secret}");
                lobbyManager.ConnectVoice(lobby.Id, x =>
                {
                    if (res != Result.Ok)
                    {
                        resultMessage.Append("Something went wrong when joining vc");
                    }
                    else
                    {
                        resultMessage.Append("Joined vc");
                    }
                });
                lobbyManager.ConnectNetwork(lobby.Id);
                lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
                lob = lobby;
                Console.WriteLine(resultMessage.ToString());
            });
        }));
        return "";
    }
}, "startlobby");

CommandHandler.RegisterCommandAliases(args =>
{
    if (args.Length != 0)
    {
        return "Usage: help (no arguments)";
    }
    else
    {   //dont linewrap this string to keep horizontal length from exceeding 120 (newlines are literal)
        string helpInfo = @"
deafen, d - equivalent to a discord deafen (you can't hear anyone, nobody can hear you)

undeafen, ud - equivalent to a discord undeafen

mute, m <optional: username> - mutes the specified user locally, if no user is specified, you are muted globally

unmute, um <optional: username> - unmutes the specified user locally, if no user is specified, you are unmuted globally

vollist, vl - lists the set volume levels for everyone in the lobby

volset, vs <username> <optional: volume 0-200> - sets the volume of the specified user to the specified amount, if no volume is specified, the volume will be set to default

voldefaultall, vda - sets everyone to the default volume level

volsetdefault, vsd <volume 0-200> - sets a new default volume level, this does not change the volume of any user (default is 100)

joinlobby, jl <lobby id> <lobby secret> - joins your client to a voice lobby for proximity chat

leavelobby, ll - leaves the lobby your in (if you aren't in a lobby, this does nothing)

proxon, pon - enables voice proximity, this will mute all other user until proximity data arrives

proxoff, poff - disables voice proximity, this will restore all users to their pre-proximity chat volumes

startlobby - [TESTING ONLY] starts a voice lobby.

help, h - shows this helpful command list
";
        return helpInfo;
    }
}, "help", "h");
#endregion

#region Command loop
Console.WriteLine("Loading...");
while (currentUser == null || lobbyManager == null || voiceManager == null)
{
    Thread.Sleep(50); //currentUser etc. has to be assigned before the user is allowed to run commands.
}
Console.Clear();

Console.WriteLine("Run \"help\" for valid commands");
while (true)
{
    string? text = Console.ReadLine();
    if (text != null)
    {
        foreach (string returnString in CommandHandler.GetResult(text).ReturnStrings)
        {
            Console.WriteLine(returnString);
        }
    }
}
#endregion



#region Old
//Lobby? lob = null; //need to lock/unlock this.
//bool success = false;
//while (!success)
//{
//    Console.Write("Would you like to make a lobby (0), or join a lobby (1): ");
//    string? inp = Console.ReadLine();
//    if (inp == null)
//    {
//        Console.WriteLine("Null input, try again.");
//        continue;
//    }
//    switch (inp.Trim())
//    {
//        case "0":
//            {
//                var trans = lobbyManager.GetLobbyCreateTransaction();
//                trans.SetCapacity(4);
//                trans.SetType(LobbyType.Private);
//                lobbyManager.CreateLobby(trans, (Result res, ref Lobby lobby) =>
//                {
//                    if (res != Result.Ok)
//                    {
//                        Console.WriteLine("Something went wrong when creating the lobby");
//                        return;
//                    }
//                    Console.WriteLine($"ID: {lobby.Id} Secret: {lobby.Secret}");
//                    //should just be you in the lobby
//                    //Console.WriteLine($"All users in the lobby: {string.Join(",\n", lobbyManager.GetMemberUsers(lobby.Id).Select(x => x.Username + "#" + x.Discriminator))}");
//                    //foreach (User u in lobbyManager.GetMemberUsers(lobby.Id))
//                    //{
//                    //    userCache[u.Id] = u;
//                    //    nameToIdCache[u.Username + "#" + u.Discriminator] = u.Id;
//                    //}

//                    lobbyManager.ConnectVoice(lobby.Id, x =>
//                    {
//                        if (res != Result.Ok)
//                        {
//                            Console.WriteLine("Something went wrong when joining vc");
//                        }
//                        else
//                        {
//                            Console.WriteLine("Joined vc");
//                        }
//                    });
//                    lobbyManager.ConnectNetwork(lobby.Id);
//                    lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
//                    //UpdateActivity(discord, lobby);
//                    lob = lobby;
//                });
//                success = true;
//            }
//            break;
//        case "1":
//            {
//                bool joinSuccess = false;
//                while (!joinSuccess)
//                {
//                    Console.WriteLine("Enter in the lobby id: ");
//                    string? id = Console.ReadLine();
//                    Console.WriteLine("Enter in the lobby secret: ");
//                    string? secret = Console.ReadLine();
//                    if (id == null || secret == null)
//                    {
//                        continue;
//                    }
//                    lobbyManager.ConnectLobby(long.Parse(id), secret, (Result res, ref Lobby lobby) =>
//                    {
//                        if (res != Result.Ok)
//                        {
//                            Console.WriteLine("Something went wrong when joining the lobby");
//                            return;
//                        }
//                        Console.WriteLine($"All users in the lobby: {string.Join(",\n", lobbyManager.GetMemberUsers(lobby.Id))}");
//                        foreach (User u in lobbyManager.GetMemberUsers(lobby.Id))
//                        {
//                            if (u.Id != currentUser.Value.Id)
//                            {
//                                userCache[u.Id] = u;
//                                nameToIdCache[u.Username + "#" + u.Discriminator] = u.Id;
//                                userVolumes[u.Id] = defaultVol;
//                            }
//                        }

//                        lobbyManager.ConnectVoice(lobby.Id, x =>
//                        {
//                            if (res != Result.Ok)
//                            {
//                                Console.WriteLine("Something went wrong when joining vc");
//                            }
//                            else
//                            {
//                                Console.WriteLine("Joined vc");
//                            }
//                        });
//                        lobbyManager.ConnectNetwork(lobby.Id);
//                        lob = lobby;
//                    });
//                    joinSuccess = true;
//                }
//                success = true;
//            }
//            break;
//        default:
//            Console.WriteLine("Enter in just a \"0\" or \"1\"");
//            continue;
//    }
//}




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