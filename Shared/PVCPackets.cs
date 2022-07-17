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

        public PacketType PType { get; private protected init; }
    }

    public abstract class PVCDataPacket : PVCPacket
    {
        protected static ulong MultiTicker = 0;
        protected static ulong SingleTicker = 0;
    }

    public class PVCMultiDataPacket : PVCDataPacket
    {
        //need per-user ticking because if one multi is made with users 1 and 2,
        //then another multi is made with 3 and 4, and the second one arrives first,
        //it will be set, but the first one will be ignored (even though they don't conflict)
        //(This change will mean unifying single & multi ticking)

        //TODO: verify correctness of ticker handling
        //Ticker handling: if Tick is higher than saved tick for this packet genre, then:
        //  if SingleTick is higher than the saved single tick, overwrite all
        //  else overwrite all except the user specified by the last single packet.
        public ulong Tick { get; init; } = MultiTicker++; //client should obey the packet with the highest tick number
        public ulong SingleTick { get; init; } = SingleTicker;
        public Dictionary<string, byte?> Volumes { get; } = new Dictionary<string, byte?>();

        public PVCMultiDataPacket()
        {
            PType = PacketType.MultiData;
        }
    }

    public class PVCSingleDataPacket : PVCDataPacket
    {
        //Ticker handling: if Tick is higher than saved tick for this packet genre, then:
        //  if MultiTick is higher than the saved multi tick, overwrite
        //  else ignore packet
        public ulong Tick { get; init; } = SingleTicker++; //client should obey the packet with the highest tick number
        public ulong MultiTick { get; init; } = MultiTicker;
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
        private static ulong WalkieTicker = 0;
        public enum WalkieMode
        {
            Individual = 1,
            Team,
            Global
        }

        public bool TeamOnly { get; set; }
        public int KeepAliveMS { get; set; } //add a setting to adjust this
        public ulong WalkieTick { get; init; } = WalkieTicker++;
        public string? SpecificRecipient { get; set; } //if this isn't null, then teamonly is ignored.

        public WalkieMode GetWalkieMode()
        {
            if (SpecificRecipient != null)
                return WalkieMode.Individual;
            else if (TeamOnly)
                return WalkieMode.Team;
            else
                return WalkieMode.Global;
        }

        public PVCWalkieTalkiePacket()
        {
            PType = PacketType.WalkieTalkie;
        }
    }

    public class PVCLobbyPacket : PVCPacket
    {
        public long LobbyId { get; set; }
        public string? Secret { get; set; }

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