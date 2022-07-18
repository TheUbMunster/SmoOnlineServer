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
            //SingleData,
            WalkieTalkie,
            ClientHandshake,
            Lobby,
            Error
        }

        public PacketType PType { get; private protected init; }
    }

    public class PVCMultiDataPacket : PVCPacket
    {
        //because udp doesn't guarantee order, ticker is a value indicating when the value was generated.
        //this is used by the client to make sure that if it tries to set the volume of a user, it isn't
        //outdated.
        public Dictionary<string, (float? volume, ulong ticker)> Volumes { get; set; } = null!;

        public PVCMultiDataPacket()
        {
            PType = PacketType.MultiData;
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