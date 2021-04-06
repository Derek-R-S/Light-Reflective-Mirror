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

        private RelayStats _stats { get => new RelayStats 
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
            if (Program.conf.EndpointServerList)
            {
                await context.Response.SendResponseAsync(_cachedCompressedServerList);
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

