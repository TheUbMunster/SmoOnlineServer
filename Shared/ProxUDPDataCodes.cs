using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public static partial class Constants
    {
        public enum UDPCodes : uint
        {
            NormalDisconnect = 0,
            ForceDisconnect = 1,
            YouveBeenBanned = 69420
        }
    }
}
