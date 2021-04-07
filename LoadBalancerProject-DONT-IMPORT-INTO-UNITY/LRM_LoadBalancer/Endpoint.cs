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
        private LoadBalancerStats _stats
        {
            get => new()
            {
                NodeCount = Program.instance.availableRelayServers.Count,
                Uptime = DateTime.Now - Program.startupTime,
                CCU = Program.instance.GetTotalCCU(),
                TotalServerCount = Program.instance.GetTotalServers(),
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
            string receivedAuthKey = req.Headers["x-Auth"];
            string endpointPort = req.Headers["x-EndpointPort"];
            string gamePort = req.Headers["x-GamePort"];
            string publicIP = req.Headers["x-PIP"];

            string address = context.Request.RemoteEndPoint.Address.ToString();
            Logger.WriteLogMessage("Received auth req [" + receivedAuthKey + "] == [" + Program.conf.AuthKey + "]");

            // if server is authenticated
            if (receivedAuthKey != null && address != null && endpointPort != null && gamePort != null && receivedAuthKey == Program.conf.AuthKey)
            {
                Logger.WriteLogMessage($"Server accepted: {address}:{gamePort}");

                try
                {
                    var _gamePort = Convert.ToUInt16(gamePort);
                    var _endpointPort = Convert.ToUInt16(endpointPort);
                    await Program.instance.AddServer(address, _gamePort, _endpointPort, publicIP);
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
                await context.Response.SendResponseAsync(HttpStatusCode.NoContent);
                return;
            }

            KeyValuePair<RelayAddress, RelayServerInfo> lowest = new(new RelayAddress { Address = "Dummy" }, new RelayServerInfo { ConnectedClients = int.MaxValue });

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].Value.ConnectedClients < lowest.Value.ConnectedClients)
                {
                    lowest = servers[i];
                }
            }

            // respond with the server ip
            // if the string is still dummy then theres no servers
            if (lowest.Key.Address != "Dummy")
            {
                // ping server to ensure its online.
                var chosenServer = await Program.instance.ManualPingServer(lowest.Key.Address, lowest.Key.EndpointPort);

                if (chosenServer.HasValue)
                    await context.Response.SendResponseAsync(JsonConvert.SerializeObject(lowest.Key));
                else
                    await context.Response.SendResponseAsync(HttpStatusCode.BadGateway);
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
            var relays = Program.instance.availableRelayServers.ToList();
            List<Room> masterList = new();

            foreach (var relay in relays)
            {
                var serversOnRelay = await Program.instance.GetServerListFromIndividualRelay(relay.Key.Address, relay.Key.EndpointPort);

                if(serversOnRelay != null)
                {
                    masterList.AddRange(serversOnRelay);
                }
                else { continue; }
            }

            // we have servers, send em!
            if (masterList.Any())
                await context.Response.SendResponseAsync(JsonConvert.SerializeObject(masterList));
            // no servers or maybe no relays, fuck you
            else
                await context.Response.SendResponseAsync(HttpStatusCode.NoContent);
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
                    foreach(string ip in GetLocalIps())
                        server.Prefixes.Add($"http://{ip}:{port}/");
                }).Build();

                server.Router.Options.SendExceptionMessages = false;
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

            return bindableIPv4Addresses;
        }
        #endregion

    }

}
