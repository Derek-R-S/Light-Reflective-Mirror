using System;
using System.Collections.Generic;
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

        public static string allCachedServers = "[]";
        public static string NorthAmericaCachedServers = "[]";
        public static string SouthAmericaCachedServers = "[]";
        public static string EuropeCachedServers = "[]";
        public static string AsiaCachedServers = "[]";
        public static string AfricaCachedServers = "[]";
        public static string OceaniaCachedServers = "[]";

        private static List<Room> _northAmericaServers = new();
        private static List<Room> _southAmericaServers = new();
        private static List<Room> _europeServers = new();
        private static List<Room> _africaServers = new();
        private static List<Room> _asiaServers = new();
        private static List<Room> _oceaniaServers = new();
        private static List<Room> _allServers = new();

        private LoadBalancerStats _stats
        {
            get => new()
            {
                nodeCount = Program.instance.availableRelayServers.Count,
                uptime = DateTime.Now - Program.startupTime,
                CCU = Program.instance.GetTotalCCU(),
                totalServerCount = Program.instance.GetTotalServers(),
            };
        }
    }
}
