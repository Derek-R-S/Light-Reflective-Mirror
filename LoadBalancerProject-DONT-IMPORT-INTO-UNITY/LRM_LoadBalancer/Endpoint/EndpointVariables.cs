using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightReflectiveMirror.LoadBalancing
{
    public partial class Endpoint
    {
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

        /// <summary>
        /// This holds all the servers. It's a bit confusing,
        /// but basically if we have a container for them then we
        /// can shorten up methods that involve operations with all of them.
        /// </summary>
        private static List<Tuple<List<Room>, string>> _allServersToPerformActionOn = new()
        {
            new Tuple<List<Room>, string>(_northAmericaServers, NorthAmericaCachedServers),
            new Tuple<List<Room>, string>(_southAmericaServers, SouthAmericaCachedServers),
            new Tuple<List<Room>, string>(_europeServers, EuropeCachedServers),
            new Tuple<List<Room>, string>(_africaServers, AfricaCachedServers),
            new Tuple<List<Room>, string>(_asiaServers, AsiaCachedServers),
            new Tuple<List<Room>, string>(_oceaniaServers, OceaniaCachedServers),
            new Tuple<List<Room>, string>(_allServers, allCachedServers),
        };

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
