using System;
using System.Collections.Generic;
using System.Text;

namespace LightReflectiveMirror
{
    class Config
    {
        public string TransportDLL = "MultiCompiled.dll";
        public string TransportClass = "Mirror.SimpleWebTransport";
        public string AuthenticationKey = "Secret Auth Key";
        public int UpdateLoopTime = 10;
        public int UpdateHeartbeatInterval = 100;
        public bool UseEndpoint = false;
        public ushort EndpointPort = 6969;
    }
}
