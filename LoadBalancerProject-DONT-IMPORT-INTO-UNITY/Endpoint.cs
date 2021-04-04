using Grapevine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
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

            // if server is authenticated
            if (req[0] == Program.conf.AuthKey)
            {
                var address = context.Request.RemoteEndPoint.Address.ToString();
                await Program.instance.AddServer(address);

                Console.WriteLine("Server accepted: " + address);

                await context.Response.SendResponseAsync(HttpStatusCode.Ok);
            }
            else
                await context.Response.SendResponseAsync(HttpStatusCode.Forbidden);
        }
    }

    public class EndpointServer
    {
        public bool Start(ushort port = 8080)
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
