using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LightReflectiveMirror.LoadBalancing
{
    class Program
    {
        /// <summary>
        /// Keeps track of all available relays.
        /// Key is server address, value is CCU/Info.
        /// </summary>
        public Dictionary<string, RelayStats> availableRelayServers = new();

        private int _pingDelay = 10000;
        const string API_PATH = "/api/stats";
        const string CONFIG_PATH = "config.json";

        public static Config conf;
        public static Program instance;

        public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            WriteTitle();
            instance = this;

            if (!File.Exists(CONFIG_PATH))
            {
                File.WriteAllText(CONFIG_PATH, JsonConvert.SerializeObject(new Config(), Formatting.Indented));
                WriteLogMessage("A config.json file was generated. Please configure it to the proper settings and re-run!", ConsoleColor.Yellow);
                Console.ReadKey();
                Environment.Exit(0);
            }
            else
            {
                conf = JsonConvert.DeserializeObject<Config>(File.ReadAllText(CONFIG_PATH));
                _pingDelay = conf.ConnectedServerPingRate;

                if (new EndpointServer().Start(conf.EndpointPort))
                    WriteLogMessage("Endpoint server started successfully", ConsoleColor.Green);
                else
                    WriteLogMessage("Endpoint server started unsuccessfully", ConsoleColor.Red);
            }

            var pingThread = new Thread(new ThreadStart(() => PingServers()));
            pingThread.Start();

            // keep console alive
            await Task.Delay(-1);
        }


        public async Task AddServer(string serverIP)
        {
            var stats = await InitialPingServer(serverIP);

            if(stats.PublicRoomCount != -1)
                availableRelayServers.Add(serverIP, stats);
        }

        async Task<RelayStats> InitialPingServer(string serverIP) 
        {
            string url = serverIP + API_PATH;
            HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(url);

            try
            {
                WebResponse response = await myRequest.GetResponseAsync();
                var reader = new StreamReader(response.GetResponseStream());

                return JsonConvert.DeserializeObject<RelayStats>(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                // server doesnt exist anymore probably
                // do more shit here

                return new RelayStats { PublicRoomCount = -1 };
            }
        }

        async Task PingServers()
        {
            while (true)
            {
                foreach (var server in availableRelayServers)
                {
                    string url = server + API_PATH;
                    HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(url);

                    try 
                    {
                        WebResponse response = await myRequest.GetResponseAsync();

                        var reader = new StreamReader(response.GetResponseStream());

                        availableRelayServers.Remove(server.Key);
                        availableRelayServers.Add(server.Key, JsonConvert.DeserializeObject<RelayStats>(reader.ReadToEnd()));
                    }
                    catch (Exception ex)
                    {
                        // server doesnt exist anymore probably
                        // do more shit here

                        availableRelayServers.Remove(server.Key);
                    }
                }

                await Task.Delay(_pingDelay);
            }
        }

        void WriteTitle()
        {
            string t = @"  
                           w  c(..)o   (
  _       _____   __  __    \__(-)    __)
 | |     |  __ \ |  \/  |       /\   (
 | |     | |__) || \  / |      /(_)___)
 | |     |  _  / | |\/| |      w /|
 | |____ | | \ \ | |  | |       | \
 |______||_|  \_\|_|  |_|      m  m copyright monkesoft 2021
";

            string load = $"Chimp Event Listener Initializing... OK" +
                            "\nHarambe Memorial Initializing...     OK" +
                            "\nBananas Initializing...              OK\n";

            WriteLogMessage(t, ConsoleColor.Green);
            WriteLogMessage(load, ConsoleColor.Cyan);
        }

        static void WriteLogMessage(string message, ConsoleColor color = ConsoleColor.White, bool oneLine = false)
        {
            Console.ForegroundColor = color;
            if (oneLine)
                Console.Write(message);
            else
                Console.WriteLine(message);
        }

        [Serializable]
        public struct RelayStats
        {
            public int ConnectedClients;
            public int RoomCount;
            public int PublicRoomCount;
            public TimeSpan Uptime;
        }

    }
}
