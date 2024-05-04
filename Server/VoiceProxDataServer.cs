using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Numerics;
using System.Runtime.InteropServices;
using ENet;
using Shared;
using Shared.VoiceProxNetworking;

namespace Server
{
   /// <summary>
   /// <b>Author: TheUbMunster</b><br></br><br></br>
   /// 
   /// The UDP server that communicates volume data, etc to all voice proximity clients.
   /// </summary>
   class VoiceProxDataServer
   {
      public static VoiceProxDataServer Instance { get; private set; }

      static VoiceProxDataServer()
      {
         if (Instance == null)
         {
            Instance = new VoiceProxDataServer();
         }
      }

      private Host server;
      private Logger pvcLogger = new Logger("VoiceProxServer");
      private Dictionary<string, Peer> discordToPeer = new Dictionary<string, Peer>();
      private string? ip = null;

      private object messageKey = new object();
      private Queue<Action> messageQueue = new Queue<Action>();

      private List<Peer> pendingClients = new List<Peer>();

      private event Action<string, string>? OnClientConnect; //discord, ingame
      private event Action<string>? OnClientDisconnect; //discord

      private VolumeCalculation volCalc = new VolumeCalculation();

      private int discordUserCount = 0;

      private VoiceProxDataServer()
      {
         DateTime launchTime = DateTime.Now;
         Logger.AddLogHandler((source, level, text, _) =>
         {
            DateTime logtime = DateTime.Now;
            string data = Logger.PrefixNewLines(text, $"{{{logtime}}} {level} [{source}]");
            Directory.CreateDirectory("logs");
            string filename = Path.Combine("logs", $"log_{launchTime.Month}-{launchTime.Day}-{launchTime.Year}--{launchTime.Hour}-{launchTime.Minute}-{launchTime.Second}.txt");
            File.AppendAllText(filename, data);
         });
         Library.Initialize();
         server = new Host();
         Address adr = new Address() { Port = Settings.Instance.Discord.PVCPort };
         try
         {
            IPHostEntry entry = Dns.GetHostEntry("");
            if (entry.AddressList.Length > 0)
            {
               for (int i = 0; i < entry.AddressList.Length; i++)
               {
                  if (entry.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                  {
                     ip = entry.AddressList[i].ToString();
                     break;
                  }
                  else if (entry.AddressList[i].IsIPv4MappedToIPv6)
                  {
                     ip = entry.AddressList[i].MapToIPv4().ToString();
                     break;
                  }
               }
               if (ip == null)
               {
                  pvcLogger.Error("DNS could not resolve this programs IP.");
               }
            }
            else
            {
               pvcLogger.Error("DNS could not resolve this programs IP.");
            }
         }
         catch //for some reason Dns.GetHostEntry likes to throw exceptions
         {
            pvcLogger.Warn("Issue resolving dns stuff in VoiceProxServer");
         }
         adr.SetIP("0.0.0.0");
         server.Create(adr, 16); //discord voice cannot support more than 16 people per lobby.
         OnClientConnect += (string discord, string ingame) =>
         {
            int before = discordUserCount++;
            //int before = igToDiscord.Count;
            //igToDiscord[ingame] = discord;
            //discordToIg[discord] = ingame;
            //if (igToPendingPackets.ContainsKey(ingame))
            //{
            //    SendPacket(igToPendingPackets[ingame], igToDiscord[ingame]);
            //    igToPendingPackets.Remove(ingame);
            //}
            volCalc.SetIGDiscordAssoc(ingame, discord);
            if (discordUserCount != 0 && before == 0)
            {
               pvcLogger.Info("Was no lobby, first user joined, now attempting to CloseThenOpenPVCLobby.");
               //igToPos.Clear();
               //igToStage.Clear();
               //igToIgsToDirtyVols.Clear();
               //igToIgsToLastSetVols.Clear();
               //igToIgsToTickers.Clear();
               DiscordBot.Instance.CloseThenOpenPVCLobby().ContinueWith((task) =>
               {
                  if (!task.Result)
                  {
                     pvcLogger.Warn("CloseThenOpenPVCLobby failed.");
                  }
                  else
                  {
                     AddMessage(() =>
                     {
                        SendLobbyPacketsToPending();
                     });
                  }
               });
            }
            else
            {
               //var packet = volCalc.GetCorrectVolumePacketForUser(discord);
               var packet = volCalc.GetZeroedVolumePacketForUser(discord);
               if (packet != null)
                  AddMessage(() =>
                  {
                     SendPacket(packet, discord);
                  });
            }
         };
         OnClientDisconnect += (string discord) =>
         {
            StringBuilder mesg = new StringBuilder("OnClientDisconnect: " + discord);
            discordUserCount--;
            bool success = volCalc.RemoveIGDiscordAssocIfExists(discord);
            if (!success)
               mesg.Append($", for some reason they weren't in the ig-discord association.");
            pvcLogger.Info(mesg.ToString());
            //igToDiscord.Remove(discordToIg[discord]);
            //discordToIg.Remove(discord);
         };
         Task.Run(Loop);
      }

      private void Loop()
      {
         System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
         const int frameTime = 50; //20fps
         int waitTime = 20;//how much time can be given to server.Service()
         while (true)
         {
            Event netEvent;
            int checkResult = server.CheckEvents(out netEvent);
            if (checkResult == 0)
            {
               //no events.
               server.Service(waitTime, out netEvent); //instead of 1 per loop, call repeatedly with 0 as arg until it returns eventtype none then wait the remainder?
            }
            else if (checkResult < 0)
            {
               pvcLogger.Error("CheckEvents failed.");
            }
            sw.Restart(); //start timing non-service work
            switch (netEvent.Type)
            {
               case EventType.None:
                  break;
               case EventType.Connect:
                  //this ip address will probably only be contained in the list if the ip of the pvc client and
                  //game are the same (probably only the case if you're running an emulator and pvc client on
                  //the same pc).
                  if (netEvent.Peer.IsSet && Settings.Instance.BanList.IpAddresses.Contains(netEvent.Peer.IP))
                  {
                     //banned user tried to join
                     netEvent.Peer.DisconnectNow(69420);
                  }
                  else
                  {
                     HandleConnectEvent(ref netEvent);
                  }
                  break;
               case EventType.Disconnect:
                  //drop the client, remove cached data
                  HandleDisconnectEvent(ref netEvent);
                  break;
               case EventType.Receive:
                  HandleRecieveEvent(ref netEvent);
                  netEvent.Packet.Dispose();
                  break;
               case EventType.Timeout:
                  //drop the client without removing cached data
                  HandleTimeoutEvent(ref netEvent);
                  break;
               default:
                  //what the h-e double hockey sticks
                  pvcLogger.Error("Invalid NetEvent type.");
                  break;
            }
            server.Flush();
            //message loop
            lock (messageKey)
            {
               try
               {
                  while (messageQueue.Count > 0)
                  {
                     messageQueue.Dequeue()();
                  }
               }
               catch (Exception ex)
               {
                  Console.WriteLine($"Issue in message loop: {ex.ToString()}");
               }
               //WalkieTalkieCalculate();
               //if (sendVolData)
               //{
               //    SendCachedVolInfo();
               //}
               volCalc.GetVolumePacketsForThisFrameAndClearCache().ForEach(x =>
               {
                  //no addmessage, we're already locked
                  //if (igToDiscord.ContainsKey(x.igRecipient))
                  SendPacket(x.packet, x.discordRecipient);
               });
            }
            sw.Stop();
            waitTime = frameTime - (int)sw.ElapsedMilliseconds;
            waitTime = waitTime < 0 ? 0 : waitTime; //if each loop needs to take 50ms, here's how much the next service can take
         }
      }

      private void HandleConnectEvent(ref Event netEvent)
      {
         netEvent.Peer.Timeout(32, 1000, 4000); //figure out what these arguments mean
         pvcLogger.Info("Client connected");
      }

      private void HandleDisconnectEvent(ref Event netEvent)
      {
         pvcLogger.Info("A client disconnected");
         Peer p = netEvent.Peer;
         //FIX ME
         //p.ID was 0 in this expression, couldn't remove out of discordToPeer because (client timed
         //out and p.ID was 0 which didn't match any of the peers ids?)
         //Consider FirstOrDefault
         string discord = discordToPeer.FirstOrDefault(x => x.Value.ID == p.ID).Key;
         if (discordToPeer.ContainsKey(discord))
         {
            discordToPeer.Remove(discord);
            OnClientDisconnect?.Invoke(discord);
         }
         else
         {
            //a peer having an id of 0 means that the peer is the "server" and we're the "client"
            //the server should never be a "client", so getting a peer with id 0 doesn't make sense.
            pvcLogger.Info("Strange client timeout with id 0.");
         }
      }

      private void HandleRecieveEvent(ref Event netEvent)
      {
         //Console.WriteLine("Recieved something");
         //if (!discordToPeer.Values.Contains(netEvent.Peer))
         {
            PVCPacket? packet = Protocol.Deserialize<PVCPacket>(netEvent.Packet.Data, netEvent.Packet.Length);
            if (packet != null)
            {
               switch (packet)
               {
                  case PVCWalkieTalkiePacket walkiePacket:
                     {
                        //the user is enabling team *or* global vc *or* individual vc
                        //pvcLogger.Info($"Got walkie packet: recip: {walkiePacket.SpecificRecipient ?? "null"}, teamonly: {walkiePacket.TeamOnly}");

                        volCalc.OnRecieveWalkieTalkie(walkiePacket);
                        //if (discordToWalkieOverrides.ContainsKey(walkiePacket.DiscordSource))
                        //{
                        //    if (discordToWalkieOverrides[walkiePacket.DiscordSource].WalkieTick < walkiePacket.WalkieTick)
                        //    {
                        //        //this packet is newer
                        //        discordToWalkieOverrides[walkiePacket.DiscordSource] = walkiePacket;
                        //        discordToSysTickCountOnRecieveWalkie[walkiePacket.DiscordSource] = Environment.TickCount64;
                        //    }
                        //}
                        //else
                        //{
                        //    discordToWalkieOverrides[walkiePacket.DiscordSource] = walkiePacket;
                        //    discordToSysTickCountOnRecieveWalkie[walkiePacket.DiscordSource] = Environment.TickCount64;
                        //}
                     }
                     break;
                  case PVCMultiDataPacket multiPacket:
                     {
                        //the server should never recieve a multi data packet
                        Peer p = netEvent.Peer;
                        AddMessage(() =>
                        {
                           SendPacket(new PVCErrorPacket() { ErrorMessage = "Why are you sending the server a PVCMultiDataPacket?" }, p);
                        });
                     }
                     break;
                  case PVCClientHandshakePacket handshakePacket:
                     {
                        if (handshakePacket.DiscordUsername == null || handshakePacket.IngameUsername == null)
                        {
                           pvcLogger.Error("HandleRecieve got a bad handshake packet.");
                        }
                        else
                        {
                           pvcLogger.Info("Successful client handshake");
                        }
                        discordToPeer[handshakePacket.DiscordUsername!] = netEvent.Peer;
                        OnClientConnect?.Invoke(handshakePacket.DiscordUsername!, handshakePacket.IngameUsername!);
                        //send the packet only after the lobby is open.
                        var lobbyInfo = DiscordBot.Instance.GetLobbyInfo();
                        if (lobbyInfo != null)
                        {
                           Peer p = netEvent.Peer;
                           AddMessage(() =>
                           {
                              SendPacket(new PVCLobbyPacket()
                              {
                                 LobbyId = lobbyInfo.Value.id,
                                 Secret = Settings.Instance.Discord.AutoSendPVCPassword ? lobbyInfo.Value.secret : null
                              }, p);
                           });
                        }
                        else
                        {
                           //add them to pending.
                           pendingClients.Add(netEvent.Peer);
                        }
                     }
                     break;
                  case PVCLobbyPacket lobbyPacket:
                     {
                        //the server should never recieve a lobby packet
                        Peer p = netEvent.Peer;
                        AddMessage(() =>
                        {
                           SendPacket(new PVCErrorPacket() { ErrorMessage = "Why are you sending the server a PVCLobbyPacket?" }, p);
                        });
                     }
                     break;
                  case PVCErrorPacket errorPacket:
                     {
                        pvcLogger.Error($"Error packet from client: \"{errorPacket.ErrorMessage ?? "(No error message present.)"}\"");
                     }
                     break;
                  default:
                     pvcLogger.Error("HandleRecieve got a packet with a bad packet type.");
                     break;
               }
            }
            else
            {
               pvcLogger.Error($"HandleRecieve got an unparseable packet (contents): {Marshal.PtrToStringAnsi(netEvent.Packet.Data, netEvent.Packet.Length)}");
            }
         }
      }

      private void HandleTimeoutEvent(ref Event netEvent)
      {
         pvcLogger.Warn($"A client timed out");
         HandleDisconnectEvent(ref netEvent);
      }

      public void OnPlayerUpdate(string ingame, Vector3 pos)
      {
         AddMessage(() =>
         {
            volCalc.OnRecieveUserData(ingame, pos);
         });
      }

      public void OnPlayerUpdate(string ingame, string? stage)
      {
         AddMessage(() =>
         {
            volCalc.OnRecieveUserData(ingame, stage);
         });
      }

      public void OnPlayerUpdate(string ingame, VolumeCalculation.Team team)
      {
         AddMessage(() =>
         {
            volCalc.OnRecieveUserData(ingame, team);
         });
      }

      public void SetProxChatEnabled(bool enabled)
      {
         AddMessage(() =>
         {
            volCalc.SetVoiceProxEnabled(enabled);
         });
      }

      public void KickUserIfConnected(IPAddress addr)
      {
         Peer? p = null;
         foreach (var peer in discordToPeer.Select(x => x.Value).Union(pendingClients))
         {
            if (peer.IsSet)
            {
               try
               {
                  IPAddress ipad = IPAddress.Parse(peer.IP);
                  if (addr.Equals(ipad))
                  {
                     peer.DisconnectNow(69420);//69420 is banned code
                     p = peer;
                     break;
                  }
               }
               catch (Exception ex)
               {
                  pvcLogger.Warn("Tried to parse ips to kick the recently banned user and something went wrong: " + ex.ToString());
               }
            }
         }
         if (p != null)
         {
            var e = discordToPeer.FirstOrDefault(x => x.Value.ID == p.Value.ID);
            if (e.Value.ID == p.Value.ID)
            {
               discordToPeer.Remove(e.Key);
            }
            else
            {
               pendingClients.Remove(e.Value);
            }
         }
      }

      public void SendPacket<T>(T packet, string discordUsername) where T : PVCPacket
      {
         if (discordToPeer.ContainsKey(discordUsername))
         {
            SendPacket(packet, discordToPeer[discordUsername]);
         }
         else
         {
            pvcLogger.Error($"Tried to send packet to {discordUsername}, but that user isn't present in the usertable. (No action was taken.)");
         }
      }

      private void SendPacket<T>(T packet, Peer recipient) where T : PVCPacket
      {
         //Console.WriteLine("Sent info");
         byte[] data = Protocol.Serialize(packet);
         Packet pack = default(Packet); //ENet will dispose automatically when sending.
         pack.Create(data, PacketFlags.Reliable);
         bool result = recipient.Send(0, ref pack);
         if (!result)
         {
            pvcLogger.Error("Sendpacket failed.");
         }
      }

      public string? GetServerIP()
      {
         return ip;
      }

      public void SendLobbyPacketsToPending()
      {
         var lobbyInfo = DiscordBot.Instance.GetLobbyInfo();
         if (lobbyInfo == null)
            pvcLogger.Error("Lobby was null when trying to connect pending clients.");
         else
         {
            pvcLogger.Info("Sent lobby join info packet to pending clients.");
            AddMessage(() =>
            {
               bool sass = Settings.Instance.Discord.AutoSendPVCPassword;
               bool noSassFirstPending = true;
               foreach (Peer pending in pendingClients)
               {
                  try
                  {
                     SendPacket(new PVCLobbyPacket()
                     {
                        LobbyId = lobbyInfo.Value.id,
                        Secret = (sass || noSassFirstPending) ? lobbyInfo.Value.secret : null
                     }, pending);
                     noSassFirstPending = false; //sass for first client regardless of setting.
                  }
                  catch (Exception ex)
                  {
                     //they might not still be trying to connect/might have dcd
                     pvcLogger.Error("Tried to send packet to a pending client, but something went wrong: " + ex.ToString());
                  }
               }
               pendingClients.Clear();
            });
         }
      }

      public void AddMessage(Action message)
      {
         lock (messageKey)
         {
            messageQueue.Enqueue(message);
         }
      }

      ~VoiceProxDataServer()
      {
         //send "closing server" packet to all clients.
         ip = null;
         server.Flush();
         server.Dispose();
         Library.Deinitialize();
      }
   }
}