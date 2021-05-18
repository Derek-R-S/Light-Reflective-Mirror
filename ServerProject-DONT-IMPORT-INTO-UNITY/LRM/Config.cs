using System;
using System.Collections.Generic;
using System.Text;

namespace LightReflectiveMirror
{
    public class Config
    {
        //========================
        // Required Settings
        //========================
        public string TransportDLL = "MultiCompiled.dll";
        public string TransportClass = "kcp2k.KcpTransport";
        public string AuthenticationKey = "Secret Auth Key";
        public ushort TransportPort = 7777;
        public int UpdateLoopTime = 10;
        public int UpdateHeartbeatInterval = 100;

        //========================
        // Endpoint REST API Settings
        //========================
        public bool UseEndpoint = true;
        public ushort EndpointPort = 8080;
        public bool EndpointServerList = true;

        //========================
        // Nat Puncher Settings
        //========================
        public bool EnableNATPunchtroughServer = true;
        public ushort NATPunchtroughPort = 7776;

        //========================
        // Load Balancer Settings
        //========================
        public bool UseLoadBalancer = false;
        public string LoadBalancerAuthKey = "AuthKey";
        public string LoadBalancerAddress = "127.0.0.1";
        public ushort LoadBalancerPort = 7070;
        public LRMRegions LoadBalancerRegion = LRMRegions.NorthAmerica;
    }
}
