using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightReflectiveMirror.LoadBalancing
{
    class Config
    {
        public int ConnectedServerPingRate = 10000;
        public string AuthKey = "AuthKey";
        public ushort EndpointPort = 7070;
        public bool ShowDebugLogs = false;
    }
}
