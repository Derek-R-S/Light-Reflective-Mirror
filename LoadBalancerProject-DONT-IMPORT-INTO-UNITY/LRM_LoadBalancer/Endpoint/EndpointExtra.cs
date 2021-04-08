using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace LightReflectiveMirror.LoadBalancing
{
    public partial class Endpoint
    {
        static void CacheAllServers()
        {
            allCachedServers = JsonConvert.SerializeObject(_allServers);
            NorthAmericaCachedServers = JsonConvert.SerializeObject(_northAmericaServers);
            SouthAmericaCachedServers = JsonConvert.SerializeObject(_southAmericaServers);
            EuropeCachedServers = JsonConvert.SerializeObject(_europeServers);
            AsiaCachedServers = JsonConvert.SerializeObject(_asiaServers);
            AfricaCachedServers = JsonConvert.SerializeObject(_africaServers);
            OceaniaCachedServers = JsonConvert.SerializeObject(_oceaniaServers);
        }

        static void ClearAllServersLists()
        {
            _northAmericaServers.Clear();
            _southAmericaServers.Clear();
            _europeServers.Clear();
            _asiaServers.Clear();
            _africaServers.Clear();
            _oceaniaServers.Clear();
            _allServers.Clear();
        }
    }

    public partial class EndpointServer
    {
        public static List<string> GetLocalIps()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            List<string> bindableIPv4Addresses = new();

            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    bindableIPv4Addresses.Add(ip.ToString());
                }
            }

            bool hasLocal = false;

            for (int i = 0; i < bindableIPv4Addresses.Count; i++)
            {
                if (bindableIPv4Addresses[i] == "127.0.0.1")
                    hasLocal = true;
            }

            if (!hasLocal)
                bindableIPv4Addresses.Add("127.0.0.1");

            return bindableIPv4Addresses;
        }
    }
}
