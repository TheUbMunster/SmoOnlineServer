using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Shared
{
    public static class Protocol
    {
        public static byte[] Serialize<T>(T packet) where T : PVCPacket
        {
            return Encoding.Unicode.GetBytes(JsonSerializer.Serialize<T>(packet));
        }

        public static T? Deserialize<T>(byte[] data) where T : PVCPacket
        {
            return (T?)Deserialize(Encoding.Unicode.GetString(data));
        }

        public static T? Deserialize<T>(IntPtr datStart, int length) where T : PVCPacket
        {
            return (T?)Deserialize(Marshal.PtrToStringUni(datStart, length));
        }

        private static PVCPacket? Deserialize(string s)
        {
            try
            {
                using (var jDoc = JsonDocument.Parse(s))
                {
                    switch (jDoc.RootElement.GetProperty("PType").GetInt32())
                    {
                        case (int)PVCPacket.PacketType.MultiData:
                            return jDoc.RootElement.Deserialize<PVCMultiDataPacket>();
                        //case (int)PVCPacket.PacketType.SingleData:
                        //    return jDoc.RootElement.Deserialize<PVCSingleDataPacket>();
                        case (int)PVCPacket.PacketType.WalkieTalkie:
                            return jDoc.RootElement.Deserialize<PVCWalkieTalkiePacket>();
                        case (int)PVCPacket.PacketType.ClientHandshake:
                            return jDoc.RootElement.Deserialize<PVCClientHandshakePacket>();
                        case (int)PVCPacket.PacketType.Lobby:
                            return jDoc.RootElement.Deserialize<PVCLobbyPacket>();
                        case (int)PVCPacket.PacketType.AppID:
                            return jDoc.RootElement.Deserialize<PVCAppIdPacket>();
                        case (int)PVCPacket.PacketType.Error:
                            return jDoc.RootElement.Deserialize<PVCErrorPacket>();
                        default:
                            return null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}