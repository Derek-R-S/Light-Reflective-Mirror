using Grapevine;
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
            string receivedAuthKey = req.Headers["Auth"];
            string endpointPort = req.Headers["EndpointPort"];
            string gamePort = req.Headers["GamePort"];

            string address = context.Request.RemoteEndPoint.Address.ToString();

            Console.WriteLine("Received auth req [" + receivedAuthKey + "] == [" + Program.conf.AuthKey+"]");

            // if server is authenticated
            if (receivedAuthKey != null && address != null && endpointPort != null && gamePort != null && receivedAuthKey == Program.conf.AuthKey)
            {
                Console.WriteLine($"Server accepted: {address}:{gamePort}");
                var _gamePort = Convert.ToUInt16(gamePort);
                var _endpointPort = Convert.ToUInt16(endpointPort);
                await Program.instance.AddServer(address, _gamePort, _endpointPort);

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

            if(servers.Count == 0)
            {
                await context.Response.SendResponseAsync(HttpStatusCode.ServiceUnavailable);
                return;
            }

            KeyValuePair<RelayAddress, RelayStats> lowest = new(new RelayAddress { Address = "Dummy" }, new RelayStats { ConnectedClients = int.MaxValue });

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
                await Program.instance.ManualPingServer(lowest.Key.Address, lowest.Key.Port);
                await context.Response.SendResponseAsync(JsonConvert.SerializeObject(lowest.Key));
            }
            else
            {
                await context.Response.SendResponseAsync(HttpStatusCode.InternalServerError);
            }
        }
    }

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
                    server.Prefixes.Add($"http://{GetLocalIp()}:{port}/");
                    server.Prefixes.Add($"http://127.0.0.1:{port}/");
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

        private static string GetLocalIp()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && ip.ToString() != "127.0.0.1")
                {
                    return ip.ToString();
                }
            }

            return null;
        }
    }
}
