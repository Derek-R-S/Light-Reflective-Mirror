using Grapevine;
using LightReflectiveMirror.Debug;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using HttpStatusCode = Grapevine.HttpStatusCode;

namespace LightReflectiveMirror.LoadBalancing
{

    [RestResource]
    public class Endpoint
    {
        public static string allCachedServers = "[]";
        public static string NorthAmericaCachedServers = "[]";
        public static string SouthAmericaCachedServers = "[]";
        public static string EuropeCachedServers = "[]";
        public static string AsiaCachedServers = "[]";
        public static string AfricaCachedServers = "[]";
        public static string OceaniaCachedServers = "[]";

        private static List<Room> northAmericaServers = new();
        private static List<Room> southAmericaServers = new();
        private static List<Room> europeServers = new();
        private static List<Room> africaServers = new();
        private static List<Room> asiaServers = new();
        private static List<Room> oceaniaServers = new();
        private static List<Room> allServers = new();

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

        /// <summary>
        /// Sent from an LRM server node
        /// adds it to the list if authenticated.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [RestRoute("Get", "/api/auth")]
        public async Task ReceiveAuthKey(IHttpContext context)
        {
            var req = context.Request;
            string receivedAuthKey = req.Headers["Authorization"];
            string endpointPort = req.Headers["x-EndpointPort"];
            string gamePort = req.Headers["x-GamePort"];
            string publicIP = req.Headers["x-PIP"];
            string region = req.Headers["x-Region"];
            int regionId = 1;

            string address = context.Request.RemoteEndPoint.Address.ToString();
            Logger.WriteLogMessage("Received auth req [" + receivedAuthKey + "] == [" + Program.conf.AuthKey + "]");

            // if server is authenticated
            if (receivedAuthKey != null && region != null && int.TryParse(region, out regionId) && address != null && endpointPort != null && gamePort != null && receivedAuthKey == Program.conf.AuthKey)
            {
                Logger.WriteLogMessage($"Server accepted: {address}:{gamePort}");

                try
                {
                    var _gamePort = Convert.ToUInt16(gamePort);
                    var _endpointPort = Convert.ToUInt16(endpointPort);
                    await Program.instance.AddServer(address, _gamePort, _endpointPort, publicIP, regionId);
                }
                catch
                {
                    await context.Response.SendResponseAsync(HttpStatusCode.BadRequest);
                }

                await context.Response.SendResponseAsync(HttpStatusCode.Ok);
            }
            else
                await context.Response.SendResponseAsync(HttpStatusCode.Forbidden);
        }

        /// <summary>
        /// Called on the load balancer when a relay node had a change in their servers. This recompiles the cached values.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [RestRoute("Get", "/api/roomsupdated")]
        public async Task ServerListUpdate(IHttpContext context)
        {
            // Dont allow unauthorizated access waste computing resources.
            string auth = context.Request.Headers["Authorization"];

            if (!string.IsNullOrEmpty(auth) && auth == Program.conf.AuthKey)
            {
                var relays = Program.instance.availableRelayServers.ToList();
                ClearAllServersLists();
                List<Room> requestedRooms;

                for (int i = 0; i < relays.Count; i++)
                {
                    requestedRooms = await Program.instance.RequestServerListFromNode(relays[i].Key.address, relays[i].Key.endpointPort);
                    allServers.AddRange(requestedRooms);

                    switch (relays[i].Key.serverRegion)
                    {
                        case (LRMRegions.NorthAmerica):
                            northAmericaServers.AddRange(requestedRooms);
                            break;
                        case (LRMRegions.SouthAmerica):
                            southAmericaServers.AddRange(requestedRooms);
                            break;
                        case (LRMRegions.Europe):
                            europeServers.AddRange(requestedRooms);
                            break;
                        case (LRMRegions.Africa):
                            africaServers.AddRange(requestedRooms);
                            break;
                        case (LRMRegions.Asia):
                            asiaServers.AddRange(requestedRooms);
                            break;
                        case (LRMRegions.Oceania):
                            oceaniaServers.AddRange(requestedRooms);
                            break;
                    }
                }

                CacheAllServers();
            }
        }

        void CacheAllServers()
        {
            allCachedServers = JsonConvert.SerializeObject(allServers);
            NorthAmericaCachedServers = JsonConvert.SerializeObject(northAmericaServers);
            SouthAmericaCachedServers = JsonConvert.SerializeObject(southAmericaServers);
            EuropeCachedServers = JsonConvert.SerializeObject(europeServers);
            AsiaCachedServers = JsonConvert.SerializeObject(asiaServers);
            AfricaCachedServers = JsonConvert.SerializeObject(africaServers);
            OceaniaCachedServers = JsonConvert.SerializeObject(oceaniaServers);
        }

        void ClearAllServersLists()
        {
            northAmericaServers.Clear();
            southAmericaServers.Clear();
            europeServers.Clear();
            asiaServers.Clear();
            africaServers.Clear();
            oceaniaServers.Clear();
            allServers.Clear();
        }

        /// <summary>
        /// Hooks into from unity side, client will call this to 
        /// find the least populated server to join
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [RestRoute("Get", "/api/join/")]
        public async Task JoinRelay(IHttpContext context)
        {
            // need to copy over in order to avoid
            // collection being modified while iterating.
            var servers = Program.instance.availableRelayServers.ToList();

            if (servers.Count == 0)
            {
                await context.Response.SendResponseAsync(HttpStatusCode.RangeNotSatisfiable);
                return;
            }

            KeyValuePair<RelayAddress, RelayServerInfo> lowest = new(new RelayAddress { Address = "Dummy" }, new RelayServerInfo { ConnectedClients = int.MaxValue });

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].Value.connectedClients < lowest.Value.connectedClients)
                {
                    lowest = servers[i];
                }
            }

            // respond with the server ip
            // if the string is still dummy then theres no servers
            if (lowest.Key.address != "Dummy")
            {
                await context.Response.SendResponseAsync(JsonConvert.SerializeObject(lowest.Key));
            }
            else
            {
                await context.Response.SendResponseAsync(HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Returns all the servers on all the relay nodes.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [RestRoute("Get", "/api/masterlist/")]
        public async Task GetMasterServerList(IHttpContext context)
        {
            string region = context.Request.Headers["x-Region"];

            if(int.TryParse(region, out int regionID))
            {
                switch ((LRMRegions)regionID)
                {
                    case LRMRegions.Any:
                        await context.Response.SendResponseAsync(allCachedServers);
                        break;
                    case LRMRegions.NorthAmerica:
                        await context.Response.SendResponseAsync(NorthAmericaCachedServers);
                        break;
                    case LRMRegions.SouthAmerica:
                        await context.Response.SendResponseAsync(SouthAmericaCachedServers);
                        break;
                    case LRMRegions.Europe:
                        await context.Response.SendResponseAsync(EuropeCachedServers);
                        break;
                    case LRMRegions.Africa:
                        await context.Response.SendResponseAsync(AfricaCachedServers);
                        break;
                    case LRMRegions.Asia:
                        await context.Response.SendResponseAsync(AsiaCachedServers);
                        break;
                    case LRMRegions.Oceania:
                        await context.Response.SendResponseAsync(OceaniaCachedServers);
                        break;
                }

                return;
            }

            // They didnt submit a region header, just give them all servers as they probably are viewing in browser.
            await context.Response.SendResponseAsync(allCachedServers);
        }

        /// <summary>
        /// Returns stats. you're welcome
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [RestRoute("Get", "/api/stats/")]
        public async Task GetStats(IHttpContext context)
        {
            await context.Response.SendResponseAsync(JsonConvert.SerializeObject(_stats));
        }

        [RestRoute("Get", "/api/get/id")]
        public async Task GetServerID(IHttpContext context)
        {
            await context.Response.SendResponseAsync(Program.instance.GenerateServerID());
        }
    }

    #region Startup

    public class EndpointServer
    {
        public bool Start(ushort port = 7070)
        {
            try
            {
                var config = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

                var server = new RestServerBuilder(new ServiceCollection(), config,
                (services) =>
                {
                    services.AddLogging(configure => configure.AddConsole());
                    services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.None);
                }, (server) =>
                {
                    foreach (string ip in GetLocalIps())
                    {
                        server.Prefixes.Add($"http://{ip}:{port}/");
                    }
                }).Build();

                server.Router.Options.SendExceptionMessages = true;
                server.Start();

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static List<string> GetLocalIps()
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
        #endregion

    }

}