# Super Mario Odyssey: Online Server

This is my fork of the official server for the [Super Mario Odyssey: Online](https://github.com/CraftyBoss/SuperMarioOdysseyOnline) mod.
Big credit to the team who are working on the server and the client, as this fork relies on
[Sanae's version of the server](https://github.com/Sanae6/SmoOnlineServer)


## Windows Server Setup

1. Download latest build from [Releases](https://github.com/TheUbMunster/SmoOnlineServer/releases)
2. Run `Server.exe`
3. `settings.json` is autogenerated in step 2, modify it however you'd like. (Instructions regarding the meaning of each setting are provided below)

## Building Server (Mac/Linux Setup)

Must have the [.NET 6 SDK](https://dotnet.microsoft.com/en-us/download) and Git installed.
Run these commands in your shell:
```shell
git clone https://github.com/TheUbMunster/SmoOnlineServer
cd SmoOnlineServer
# replace run with build to only build the server
dotnet run --project Server/Server.csproj -c Release
```
If you ran `dotnet build` instead of `dotnet run`, you can find the binary at `Server/bin/net6.0/Release/Server.exe`


## Commands

Run `help` to get what commands are available in the server console.
Run the `loadsettings` command in the console to update the settings without restarting.
Server address and port will require a server restart, but everything else should update when you run `loadsettings`.
If you run into strange behavior regarding settings, try the `restartserver` command to restart the server automatically.

[//]: # (TODO: Document all commands, possibly rename them too.)

## Settings

### Server
Address: the ip address of the server, default: 0.0.0.0 # this shouldn't be changed  
Port: the port of the server, default 1027  
Maxplayers: the max amount of players that can join, default: 8  
Flip: flips the player upside down, defaults: enabled: true, pov: both  
Scenario: sync's scenario's for all players on the server, default: false  
Banlist: banned people are unable to join the server, default: false  
PersistShines/Moons: Allows the server to remember moon progress across crashes/restarts  

### Discord
Token: the token of the bot you want to load into, default: null (make sure to put quotes around this)  
AppID: the discord developer "application id"/"client id"/"app id" found in the discord developer dashboard (make sure this has no quotes around it)
Prefix: the bot prefix to be used, default: $  
CommandChannel: this channel can have commands typed in, but shows none of the server logs (this way people can administrate, but don't have access to logs that contain sensitive info e.g. ip addresses), default: null (make sure to put quotes around this)  
LogChannel: logs the server console to that channel and can run commands, default: null (make sure to put quotes around this)  
PVCPort: the port that proximity voice chat clients connect to, default: 12000  
AutoSendPVCPassword: whether or not to automatically send the lobby secret for people to join (this prevents unwanted people from joining the vc), default: true  
BeginHearingThreshold: the in-game distance at which you just barely begin to hear another person, default: 3500  
FullHearingThreshold: the in-game distance at which someone who's close doesn't get any louder as they get closer, default: 750  

## Setup

### Server
Setup for this server as opposed to the official server is very similar, no additional (vs regular server, make sure to set your variables in the settings.json file)
actions need to be taken unless/until you want to enable the voice proximity feature.
Although the discord bot/token for the regular server (I believe) is optional for literal game functionality purposes, *for voice proximity to function, it is* ***required***. You need to put your discord
bot token in the server settings json, as the bot is what manages/creates/deletes voice lobbies for the clients to join. Once this set set up, you can enable voice proximity.
To do so, run `voiceprox on/off` to enable/disable voice proximity. Enter `voiceprox` to see if it's currently enabled.

### Client
To use the client, you need .NET 6.0 desktop runtime installed (Client is a WinForms app). If you don't have it installed, it should prompt you. If installing, make sure
not to select "Console" or "Server", you need the desktop one. This client as-is, only works on windows, if support is demanded for other platforms, I'll look into it.

This client is designed to be simple and do as much of the setup as it can automatically. The whole idea is that you are playing the game on your switch/emulator, and you
have this program running on your computer. Instead of using discord itself, use this application for voice chat instead. However, some settings do need to be setup upon
the first time you connect to a server. First, it asks for the host (this is the IP address of the server, to find this, run `pvcip` on the server), Second, it requires
your in-game username (This is the name of the profile you select when starting the game and the name that shows up on your nametag in-game).

That's all you need to interact with the client. You can change your settings (set keybinds for mute/undeafen, change to PTT or PTM, edit your port and ip/host etc.) To connect
to the server, click the "green phone" button in the top right to "join the call". Click the "red phone" while you're connected to disconnect.

For troubleshooting, see the [release](https://github.com/TheUbMunster/SmoOnlineServer/releases) notes.

#### Client settings
Application ID: The discord app id, (this is required), to get this, run "appid" on the server, default: ""
ServerPort: The port that proximity voice chat connects to in the server (required), default: 12000  
ServerHost: The IP/Hostname of the server, default: null  
IngameName: Your in-game username, default: null  
DefaultVolume: The volume at which newly connected users who do not have a volume saved in preferences will be set to, default: 50  
VolumePrefs (Not in GUI): A dictionary of discord usernames to volume preferences (as you move people's volume sliders around, the client will remember and save them here), default: {}  
SpeakMode: "Always On"/"Push-To-Talk"/"Push-To-Mute", default: "Always On"  
PushToTeam: The keybind for the team communication button, default: null (As of release v1.0.0-alpha, this is not yet implemented)  
PushToGlobal: The keybind for the global communication button, default: null (As of release v1.0.0-alpha, this is not yet implemented)  
ToggleDeafen: The keybind to toggle your deafen, default: null  
SpeakAction: The keybind for "SpeakMode" action (When set to "Always On", this functions as a toggle mute keybind), default: null  
PercievedVolumeSliderEnabled: Whether or not to show the percieved volume of users (enabling this might make it easy to accidentally cheat, as when you move close to a user
in-game, you can see this slider rise), default: false  