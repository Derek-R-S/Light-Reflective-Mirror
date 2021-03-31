using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LightReflectiveMirror.Endpoints;
using Mirror;
using Newtonsoft.Json;

namespace LightReflectiveMirror
{
    class Program
    {
        public static Transport transport;
        public static Program instance;
        public static Config conf;

        private RelayHandler _relay;
        private MethodInfo _awakeMethod;
        private MethodInfo _startMethod;
        private MethodInfo _updateMethod;
        private MethodInfo _lateUpdateMethod;

        private DateTime _startupTime;

        private List<int> _currentConnections = new List<int>();
        private int _currentHeartbeatTimer = 0;

        private const string CONFIG_PATH = "config.json";

        public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public int GetConnections() => _currentConnections.Count;
        public TimeSpan GetUptime() => DateTime.Now - _startupTime;
        public int GetPublicRoomCount() => _relay.rooms.Where(x => x.isPublic).Count();
        public List<Room> GetRooms() => _relay.rooms;

        public async Task MainAsync()
        {
            WriteTitle();
            instance = this;
            _startupTime = DateTime.Now;

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
                try
                { 
                    var asm = Assembly.LoadFile(Directory.GetCurrentDirectory() + @"\" + conf.TransportDLL);
                    WriteLogMessage($"Loaded Assembly: {asm.FullName}", ConsoleColor.Green);

                    transport = asm.CreateInstance(conf.TransportClass) as Transport;

                    if (transport != null)
                    {
                        var transportClass = asm.GetType(conf.TransportClass);

                        WriteLogMessage($"Loaded Transport: {transportClass.Name}! Loading Methods...", ConsoleColor.Green);
                        CheckMethods(transportClass);

                        if (_awakeMethod != null)
                        {
                            _awakeMethod.Invoke(transport, null);
                            WriteLogMessage("Called Awake on transport.", ConsoleColor.Yellow);
                        }

                        if (_startMethod != null)
                        {
                            _awakeMethod.Invoke(transport, null);
                            WriteLogMessage("Called Start on transport.", ConsoleColor.Yellow);
                        }

                        WriteLogMessage("Starting Transport...", ConsoleColor.Green);

                        transport.OnServerError = (clientID, error) => 
                        {
                            WriteLogMessage($"Transport Error, Client: {clientID}, Error: {error}", ConsoleColor.Red);
                        };

                        transport.OnServerConnected = (clientID) =>
                        {
                            WriteLogMessage($"Transport Connected, Client: {clientID}", ConsoleColor.Cyan);
                            _currentConnections.Add(clientID);
                            _relay.ClientConnected(clientID);
                        };

                        _relay = new RelayHandler(transport.GetMaxPacketSize(0));

                        transport.OnServerDataReceived = _relay.HandleMessage;
                        transport.OnServerDisconnected = (clientID) =>
                        {
                            _currentConnections.Remove(clientID);
                            _relay.HandleDisconnect(clientID);
                        };

                        transport.ServerStart();

                        WriteLogMessage("Transport Started!", ConsoleColor.Green);

                        if (conf.UseEndpoint)
                        {
                            var endpoint = new EndpointServer();

                            if (endpoint.Start(conf.EndpointPort))
                                WriteLogMessage("Endpoint Service Started!", ConsoleColor.Green);
                            else
                                WriteLogMessage("Endpoint failure, please run as administrator.", ConsoleColor.Red);
                        }
                    }
                    else
                    {
                        WriteLogMessage("Transport Class not found! Please make sure to include namespaces.", ConsoleColor.Red);
                        Console.ReadKey();
                        Environment.Exit(0);
                    }
                }
                catch(Exception e)
                {
                    WriteLogMessage("Exception: " + e, ConsoleColor.Red);
                    Console.ReadKey();
                    Environment.Exit(0);
                }
            }

            while (true)
            {
                if (_updateMethod != null) _updateMethod.Invoke(transport, null); 
                if (_lateUpdateMethod != null) _lateUpdateMethod.Invoke(transport, null); 

                _currentHeartbeatTimer++;

                if(_currentHeartbeatTimer >= conf.UpdateHeartbeatInterval)
                {
                    _currentHeartbeatTimer = 0;

                    for(int i = 0; i < _currentConnections.Count; i++)
                        transport.ServerSend(_currentConnections[i], 0, new ArraySegment<byte>(new byte[] { 200 }));

                    GC.Collect();
                }

                await Task.Delay(conf.UpdateLoopTime);
            }
        }

        static void WriteLogMessage(string message, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
        }

        void CheckMethods(Type type)
        {
            _awakeMethod         = type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _startMethod         = type.GetMethod("Start", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _updateMethod        = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _lateUpdateMethod    = type.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (_awakeMethod != null) WriteLogMessage("'Awake' Loaded!", ConsoleColor.Yellow); 
            if (_startMethod != null) WriteLogMessage("'Start' Loaded!", ConsoleColor.Yellow); 
            if (_updateMethod != null) WriteLogMessage("'Update' Loaded!", ConsoleColor.Yellow);
            if (_lateUpdateMethod != null) WriteLogMessage("'LateUpdate' Loaded!", ConsoleColor.Yellow);
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
                            "\nHarambe Memorial Initializing... OK" +
                            "\nBananas initializing... OK\n";

            WriteLogMessage(t, ConsoleColor.Green);
            WriteLogMessage(load, ConsoleColor.Cyan);
        }
    }
}
