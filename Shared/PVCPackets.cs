using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public class PVCPacket
    {
        public enum PacketType
        {
            MultiData = 1,
            SingleData,
            WalkieTalkie,
            ClientHandshake,
            Lobby,
            Error
        }

        protected static ulong MultiTicker = 0;
        protected static ulong SingleTicker = 0;

        public PacketType PType { get; private protected init; }
    }

    public class PVCMultiDataPacket : PVCPacket
    {
        //TODO: verify correctness of ticker handling
        //Ticker handling: if Tick is higher than saved tick for this packet genre, then:
        //  if SingleTick is higher than the saved single tick, overwrite all
        //  else overwrite all except the user specified by the last single packet.
        public ulong Tick { get; private set; } = MultiTicker++; //client should obey the packet with the highest tick number
        public ulong SingleTick { get; private set; } = SingleTicker;
        public Dictionary<string, byte?> Volumes { get; } = new Dictionary<string, byte?>();

        public PVCMultiDataPacket()
        {
            PType = PacketType.MultiData;
        }
    }

    public class PVCSingleDataPacket : PVCPacket
    {
        //Ticker handling: if Tick is higher than saved tick for this packet genre, then:
        //  if MultiTick is higher than the saved multi tick, overwrite
        //  else ignore packet
        public ulong Tick { get; private set; } = SingleTicker++; //client should obey the packet with the highest tick number
        public ulong MultiTick { get; private set; } = MultiTicker;
        public string DiscordUsername { get; set; } = null!;
        public byte? Volume { get; set; } = null;

        public PVCSingleDataPacket()
        {
            PType = PacketType.SingleData;
        }
    }

    public class PVCClientHandshakePacket : PVCPacket
    {
        public string DiscordUsername { get; set; } = null!;//includes discriminator
        public string IngameUsername { get; set; } = null!;

        public PVCClientHandshakePacket()
        {
            PType = PacketType.ClientHandshake;
        }
    }

    public class PVCWalkieTalkiePacket : PVCPacket
    {
        public bool TeamOnly { get; set; }
        public bool Enable { get; set; } //true for enable, false for disable.

        public PVCWalkieTalkiePacket()
        {
            PType = PacketType.WalkieTalkie;
        }
    }

    public class PVCLobbyPacket : PVCPacket
    {
        public long LobbyId { get; set; }
        public string Secret { get; set; } = null!;

        public PVCLobbyPacket()
        {
            PType = PacketType.Lobby;
        }
    }

    public class PVCErrorPacket : PVCPacket
    {
        public string ErrorMessage { get; set; } = null!;

        public PVCErrorPacket()
        {
            PType = PacketType.Error;
        }
    }
}