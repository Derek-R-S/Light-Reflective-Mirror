using Grapevine;
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
    }

    public class EndpointServer
    {
        public bool Start(ushort port = 6969)
        {
            try
            {
                var server = RestServerBuilder.UseDefaults().Build();
                server.Prefixes.Remove($"http://localhost:{1234}/");
                server.Prefixes.Add($"http://*:{port}/");
                server.Router.Options.SendExceptionMessages = false;

                server.AfterStarting += (s) =>
                {
                    string startup = @"
********************************************************************************
* Endpoint Server listening on "+$"{string.Join(", ", server.Prefixes)}" + @"
* Be sure to Port Forward! :^)
********************************************************************************
";
                    Console.WriteLine(startup);
                };

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

