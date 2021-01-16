using System;
using System.Collections.Generic;
using System.Text;

namespace LightReflectiveMirror
{
    class Config
    {
        public string TransportDLL = "SimpleWebSocketTransportCompiled.dll";
        public string TransportClass = "Mirror.SimpleWeb.SimpleWebTransport";
        public string AuthenticationKey = "Secret Auth Key";
        public int UpdateLoopTime = 50;
        public int UpdateHeartbeatInterval = 20;
    }
}
