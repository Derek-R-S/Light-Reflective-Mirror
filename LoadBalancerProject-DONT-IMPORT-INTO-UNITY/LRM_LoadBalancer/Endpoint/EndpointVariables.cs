using System;
using System.Collections.Generic;
using System.Linq;

namespace LightReflectiveMirror.LoadBalancing
{
    public partial class Endpoint
    {
        /// <summary>
        /// Used as a control variable for load balancer to
        /// give the lowest pop. server
        /// </summary>
        private static readonly KeyValuePair<RelayAddress, RelayServerInfo> lowest = 
            new(new() { address = "Dummy" }, new() { connectedClients = int.MaxValue });

        private static Dictionary<LRMRegions, List<Room>> _regionRooms = new();
        private static Dictionary<LRMRegions, string> _cachedRegionRooms = new();

        private LoadBalancerStats _stats
        {
            get => new()
            {
                nodeCount = Program.instance.availableRelayServers.Count,
                uptime = DateTime.Now - Program.startupTime,
                CCU = Program.instance.GetTotalCCU(),
                totalServerCount = Program.instance.GetTotalServers(),
                connectedNodes = Program.instance.availableRelayServers.ToList()
            };
        }
    }
}
