using Grapevine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LightReflectiveMirror.LoadBalancing
{
    [RestResource]
    public class Endpoint
    {
        [RestRoute("Get", "/api/auth")]
        public async Task ReceiveAuthKey(IHttpContext context)
        {
            var req = context.Request.Headers;
            string receivedAuthKey = req[0];
            string address = req[1];

            Console.WriteLine("Received auth req [" + req[0] + "] == [" + Program.conf.AuthKey+"]");

            // if server is authenticated
            if (receivedAuthKey == Program.conf.AuthKey)
            {
                Console.WriteLine("Server accepted: " + address);

                await Program.instance.AddServer(address);

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

            KeyValuePair<string, RelayStats> lowest = new("Dummy", new RelayStats { ConnectedClients = int.MaxValue });

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].Value.ConnectedClients < lowest.Value.ConnectedClients)
                {
                    lowest = servers[i];
                }
            }

            // respond with the server ip
            // if the string is still dummy then theres no servers
            await context.Response.SendResponseAsync(lowest.Key != "Dummy" ? lowest.Key : HttpStatusCode.InternalServerError);
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
                    server.Prefixes.Add($"http://*:{port}/");
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
    }
}
