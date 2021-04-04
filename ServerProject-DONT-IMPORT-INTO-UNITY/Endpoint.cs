using Grapevine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace LightReflectiveMirror.Endpoints
{
    [Serializable]
    struct RelayStats
    {
        public int ConnectedClients;
        public int RoomCount;
        public int PublicRoomCount;
        public TimeSpan Uptime;
    }

    [RestResource]
    public class Endpoint
    {
        [RestRoute("Get", "/api/stats")]
        public async Task Stats(IHttpContext context)
        {
            RelayStats stats = new RelayStats
            {
                ConnectedClients = Program.instance.GetConnections(),
                RoomCount = Program.instance.GetRooms().Count,
                PublicRoomCount = Program.instance.GetPublicRoomCount(),
                Uptime = Program.instance.GetUptime()
            };

            string json = JsonConvert.SerializeObject(stats, Formatting.Indented);
            await context.Response.SendResponseAsync(json);
        }

        [RestRoute("Get", "/api/servers")]
        public async Task ServerList(IHttpContext context)
        {
            if (Program.conf.EndpointServerList)
            {
                string json = JsonConvert.SerializeObject(Program.instance.GetRooms(), Formatting.Indented);
                await context.Response.SendResponseAsync(json);
            }
            else
            {
                await context.Response.SendResponseAsync("Access Denied");
            }
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

                Action<IServiceCollection> configServices = (services) =>
                {
                    services.AddLogging(configure => configure.AddConsole());
                    services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.None);
                };

                Action<IRestServer> configServer = (server) =>
                {
                    server.Prefixes.Add($"http://*:{port}/");
                };

                var server = new RestServerBuilder(new ServiceCollection(), config, configServices, configServer).Build();
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

