using LightReflectiveMirror.Debug;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LightReflectiveMirror.LoadBalancing
{
    partial class Program
    {
        /// <summary>
        /// Keeps track of all the LRM nodes registered to the Load Balancer.
        /// </summary>
        public Dictionary<RelayAddress, RelayServerInfo> availableRelayServers = new();
        public static Dictionary<string, Room> cachedRooms = new();

        private int _pingDelay = 10000;
        public static bool showDebugLogs = false;
        public static DateTime startupTime;
        const string API_PATH = "/api/stats";
        readonly string CONFIG_PATH = System.Environment.GetEnvironmentVariable("LRM_LB_CONFIG_PATH") ?? "config.json";

        public static Config conf;
        public static Program instance;

        public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            WriteTitle();

            instance = this;
            startupTime = DateTime.Now;

            if (!File.Exists(CONFIG_PATH))
            {
                File.WriteAllText(CONFIG_PATH, JsonConvert.SerializeObject(new Config(), Formatting.Indented));
                Logger.ForceLogMessage("A config.json file was generated. Please configure it to the proper settings and re-run!", ConsoleColor.Yellow);
                Console.ReadKey();
                Environment.Exit(0);
            }
            else
            {
                conf = JsonConvert.DeserializeObject<Config>(File.ReadAllText(CONFIG_PATH));
                Logger.ConfigureLogger(new Logger.LogConfiguration { sendLogs = conf.ShowDebugLogs });

                _pingDelay = conf.ConnectedServerPingRate;
                showDebugLogs = conf.ShowDebugLogs;

                if (new EndpointServer().Start(conf.EndpointPort))
                    Logger.ForceLogMessage("Endpoint server started successfully", ConsoleColor.Green);
                else
                    Logger.ForceLogMessage("Endpoint server started unsuccessfully", ConsoleColor.Red);
            }

            var pingThread = new Thread(new ThreadStart(PingServers));
            pingThread.Start();

            // keep console alive
            await Task.Delay(-1);
        }


        /// <summary>
        /// Called when a new server requested that we add them to our load balancer.
        /// </summary>
        /// <param name="serverIP"></param>
        /// <param name="port"></param>
        /// <param name="endpointPort"></param>
        /// <param name="publicIP"></param>
        /// <returns></returns>
        public async Task AddServer(string serverIP, ushort port, ushort endpointPort, string publicIP, int regionId)
        {
            var relayAddr = new RelayAddress { port = port, endpointPort = endpointPort, address = publicIP, endpointAddress = serverIP.Trim(), serverRegion = (LRMRegions)regionId };

            if (availableRelayServers.ContainsKey(relayAddr))
            {
                Logger.ForceLogMessage($"LRM Node {serverIP}:{endpointPort} tried to register while already registered!");
                return;
            }

            var stats = await RequestStatsFromNode(serverIP, endpointPort);

            if (stats.HasValue)
            {
                Logger.ForceLogMessage($"LRM Node Registered! {serverIP}:{endpointPort}", ConsoleColor.Green);
                availableRelayServers.Add(relayAddr, stats.Value);
            }
            else
            {
                Logger.ForceLogMessage($"LRM Node Failed to respond to ping back. Make sure {serverIP}:{endpointPort} is port forwarded!");
            }
        }

        /// <summary>
        /// Called when we want to get the server info from a server.
        /// </summary>
        /// <param name="serverIP"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public async Task<RelayServerInfo?> RequestStatsFromNode(string serverIP, ushort port)
        {
            using (WebClient wc = new WebClient())
            {
                try
                {
                    string receivedStats = await wc.DownloadStringTaskAsync($"http://{serverIP}:{port}{API_PATH}");

                    var stats = JsonConvert.DeserializeObject<RelayServerInfo>(receivedStats);

                    if (stats.serversConnectedToRelay == null)
                        stats.serversConnectedToRelay = new List<Room>();

                    return stats;
                }
                catch (Exception e)
                {
                    // Server failed to respond to stats, dont add to load balancer.
                    return null;
                }
            }
        }

        /// <summary>
        /// Called when we want to check if a server is alive.
        /// </summary>
        /// <param name="serverIP"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public async Task<bool> HealthCheckNode(string serverIP, ushort port)
        {
            using (WebClient wc = new WebClient())
            {
                try
                {
                    await wc.DownloadStringTaskAsync($"http://{serverIP}:{port}{API_PATH}");

                    // If it got to here, then the server is healthy!
                    return true;
                }
                catch (Exception e)
                {
                    // Server failed to respond
                    return false;
                }
            }
        }

        /// <summary>
        /// Called when we want to get the list of rooms in a specific LRM node.
        /// </summary>
        /// <param name="serverIP"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public async Task<List<Room>> RequestServerListFromNode(string serverIP, ushort port)
        {
            using (WebClient wc = new WebClient())
            {
                try
                {
                    string receivedStats = await wc.DownloadStringTaskAsync($"http://{serverIP}:{port}/api/servers");
                    var stats = JsonConvert.DeserializeObject<List<Room>>(receivedStats);

                    // If they have no servers, it will return null as json for some reason.
                    if (stats == null)
                        return new List<Room>();
                    else
                        return stats;
                }
                catch (Exception e)
                {
                    // Server failed to respond
                    return new List<Room>();
                }
            }
        }

        /// <summary>
        /// A thread constantly running and making sure LRM nodes are still healthy.
        /// </summary>
        async void PingServers()
        {
            while (true)
            {
                Logger.WriteLogMessage("Pinging " + availableRelayServers.Count + " available relays");

                // Create a new list so we can modify the collection in our loop.
                var keys = new List<RelayAddress>(availableRelayServers.Keys);

                for (int i = 0; i < keys.Count; i++)
                {

                    var stats = await RequestStatsFromNode(keys[i].endpointAddress, keys[i].endpointPort);

                    if (stats.HasValue)
                    {
                        availableRelayServers[keys[i]] = stats.Value;
                    }
                    else
                    {
                        Logger.ForceLogMessage($"Server {keys[i].address}:{keys[i].port} failed a health check, removing from load balancer.", ConsoleColor.Red);
                        availableRelayServers.Remove(keys[i]);
                    }
                }

                GC.Collect();
                await Task.Delay(_pingDelay);
            }
        }

        void WriteTitle()
        {
            string t = @"  
  _        _____    __  __                                                
 | |      |  __ \  |  \/  |                                               
 | |      | |__) | | \  / |                                               
 | |      |  _  /  | |\/| |                                               
 | |____  | | \ \  | |  | |                           w  c(..)o   (                  
 |______| |_|  \_\ |_|  |_|                            \__(-)    __)
  _         ____               _____                       /\   (              
 | |       / __ \      /\     |  __ \                     /(_)___)             
 | |      | |  | |    /  \    | |  | |                    w /|                
 | |      | |  | |   / /\ \   | |  | |                     | \                
 | |____  | |__| |  / ____ \  | |__| |                    m  m copyright monkesoft 2021                
 |______|  \____/  /_/    \_\ |_____/    
  ____               _                   _   _    _____   ______   _____  
 |  _ \      /\     | |          /\     | \ | |  / ____| |  ____| |  __ \ 
 | |_) |    /  \    | |         /  \    |  \| | | |      | |__    | |__) |
 |  _ <    / /\ \   | |        / /\ \   | . ` | | |      |  __|   |  _  / 
 | |_) |  / ____ \  | |____   / ____ \  | |\  | | |____  | |____  | | \ \ 
 |____/  /_/    \_\ |______| /_/    \_\ |_| \_|  \_____| |______| |_|  \_\
";

            string load = $"Chimp Event Listener Initializing... OK" +
                            "\nHarambe Memorial Initializing...     OK" +
                            "\nBananas Initializing...              OK\n";

            Logger.ForceLogMessage(t, ConsoleColor.Green);
            Logger.ForceLogMessage(load, ConsoleColor.Cyan);
        }
    }
}
