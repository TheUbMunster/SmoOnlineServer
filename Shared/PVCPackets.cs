using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public class PVCDataPacket
    {
        public static ulong Ticker = 0;
        public ulong Tick { get; set; } //client should obey the packet with the highest tick number
        public Dictionary<string, byte?> Volumes { get; } = new Dictionary<string, byte?>();
    }

    public class PVCSingleDataPacket
    {

    }

    public class PVCClientHandshakePacket
    {
        public string? DiscordUsername { get; set; } //includes discriminator
        public string? IngameUsername { get; set; }
    }

    public class PVCLobbyPacket
    {
        public long LobbyId { get; set; }
        public string? Secret { get; set; }
    }

    public class PVCDisconnectPacket
    {
        public bool IntendToRejoin { get; set; }
    }

    public class PVCErrorPacket
    {
        public string? ErrorMessage { get; set; }
    }
}