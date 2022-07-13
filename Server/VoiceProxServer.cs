using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Text.Json;
using Shared;

namespace Server
{
    class VoiceProxServer
    {
        private static VoiceProxServer? instance;
        public static VoiceProxServer Instance 
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }
                else
                {
                    return instance = new VoiceProxServer();
                }
            } 
        }
        private static Logger logger = new Logger("VoiceProxServer");
        //private object lockKey = new object();
        private Dictionary<string, IPAddress> discordToIp = new Dictionary<string, IPAddress>();
        private Dictionary<IPAddress, Socket> ipToSocket = new Dictionary<IPAddress, Socket>();
        //private Queue<Action<byte[]>> toSendQueue = new Queue<Action<byte[]>>(); //not neccessary?
        public event Action<byte[]>? onMessageRecieved;

        private VoiceProxServer()
        {
            Task.Run(StartListener);
        }

        public bool SendDataPacket(PVCDataPacket data, string discordName)
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(data);
            return SendMessage(json, discordName);
        }

        public bool SendErrorPacket(PVCErrorPacket err, string discordName)
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(err);
            return SendMessage(json, discordName);
        }

        public bool SendAllLobbyPacket(PVCLobbyPacket lobby)
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(lobby);
            bool result = true;
            foreach (string discordName in discordToIp.Keys)
            {
                result &= SendMessage(json, discordName);
            }
            return result;
        }

        private void SendErrorPacket(PVCErrorPacket err, IPAddress ip)
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(err);
            SendMessage(json, ip);
        }

        /// <summary>
        /// Sends a message to the pvc client of the given discord user.
        /// </summary>
        /// <param name="data">A serialized packet</param>
        /// <param name="discordName">The discord username to send the data to (includes discriminator)</param>
        /// <returns>true if the user was found and the message was sent, false otherwise</returns>
        private bool SendMessage(byte[] data, string discordName)
        {
            if (discordToIp.ContainsKey(discordName))
            {
                SendMessage(data, discordToIp[discordName]);
                return true;
            }
            else
            {
                return false;
            }
        }

        private void SendMessage(byte[] data, IPAddress ip)
        {
            try
            {
                if (ipToSocket.ContainsKey(ip))
                {
                    ipToSocket[ip].Send(data);
                }
                else
                {
                    ipToSocket[ip] = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { Blocking = false, NoDelay = true};
                    IPEndPoint ep = new IPEndPoint(ip, Constants.pvcPort);
                    ipToSocket[ip].Bind(ep);
                    ipToSocket[ip].Send(data);
                }
            }
            catch (Exception e)
            {
                logger.Error(e.ToString());
            }
        }

        private void StartListener()
        {
            UdpClient listener = new UdpClient(Constants.pvcPort); //maybe have recieve in the server be tcp
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, Constants.pvcPort);
            try
            {
                while (true)
                {
                    byte[] dat = listener.Receive(ref ep);
                    if (!Settings.Instance.BanList.IpAddresses.Contains(ep.Address.ToString())) //can only filter ipv4?
                    {
                        onMessageRecieved?.Invoke(dat);
                    }
                    else
                    {
                        //this client is banned.
                        SendErrorPacket(new PVCErrorPacket() { ErrorMessage = "U banned #rip #getrekt" }, ep.Address);
                        logger.Warn("Banned user attempted to join.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.ToString());
            }
            finally
            {
                listener.Close();
            }
        }

        ~VoiceProxServer()
        {
            foreach (var s in ipToSocket.Values)
            {
                s.Dispose();
            }
        }
    }
}
