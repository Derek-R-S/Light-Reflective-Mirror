using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kcp2k
{
    class KCPConfig
    {
        public bool NoDelay = true;

        public uint Interval = 10;

        public int FastResend = 2;

        public bool CongestionWindow = false; // KCP 'NoCongestionWindow' is false by default. here we negate it for ease of use.

        public uint SendWindowSize = 4096; //Kcp.WND_SND; 32 by default. Mirror sends a lot, so we need a lot more.

        public uint ReceiveWindowSize = 4096; //Kcp.WND_RCV; 128 by default. Mirror sends a lot, so we need a lot more.

        public int ConnectionTimeout = 10000; // Time in miliseconds it takes for a connection to time out.
    }
}
