using Grapevine;
using LightReflectiveMirror.Compression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private static string _cachedServerList = "[]";
        private static string _cachedCompressedServerList;
        public static DateTime lastPing = DateTime.Now;

        private static List<Room> _rooms { get => Program.instance.GetRooms().Where(x => x.isPublic).ToList(); }

        private RelayStats _stats { get => new()
        {
            ConnectedClients = Program.instance.GetConnections(),
            RoomCount = Program.instance.GetRooms().Count,
            PublicRoomCount = Program.instance.GetPublicRoomCount(),
            Uptime = Program.instance.GetUptime()
        }; }

        public static void RoomsModified()
        {
            _cachedServerList = JsonConvert.SerializeObject(_rooms, Formatting.Indented);
            _cachedCompressedServerList = _cachedServerList.Compress();

            if (Program.conf.UseLoadBalancer)
                Program.instance.UpdateLoadBalancerServers();
        }

        [RestRoute("Get", "/api/stats")]
        public async Task Stats(IHttpContext context)
        {
            lastPing = DateTime.Now;
            string json = JsonConvert.SerializeObject(_stats, Formatting.Indented);
            await context.Response.SendResponseAsync(json);
        }

        [RestRoute("Get", "/api/servers")]
        public async Task ServerList(IHttpContext context)
        {
            if (Program.conf.EndpointServerList)
            {
                await context.Response.SendResponseAsync(_cachedServerList);
            }
            else
                await context.Response.SendResponseAsync(HttpStatusCode.Forbidden);
        }

        [RestRoute("Get", "/api/compressed/servers")]
        public async Task ServerListCompressed(IHttpContext context)
        {
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");

            if (Program.conf.EndpointServerList)
            {
                await context.Response.SendResponseAsync(_cachedCompressedServerList);
            }
            else
                await context.Response.SendResponseAsync(HttpStatusCode.Forbidden);
        }

        [RestRoute("Options", "/api/compressed/servers")]
        public async Task ServerListCompressedOptions(IHttpContext context)
        {
            var originHeaders = context.Request.Headers["Access-Control-Request-Headers"];

            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", originHeaders);

            await context.Response.SendResponseAsync(HttpStatusCode.Ok);
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
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
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
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }

        }
    }
}

