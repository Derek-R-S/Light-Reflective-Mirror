using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
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
        public Dictionary<int, IPEndPoint> NATConnections = new Dictionary<int, IPEndPoint>();
        private BiDictionary<int, string> _pendingNATPunches = new BiDictionary<int, string>();
        private int _currentHeartbeatTimer = 0;

        private string _externalIp;
        private byte[] _NATRequest = new byte[500];
        private int _NATRequestPosition = 0;

        private UdpClient _punchServer;

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
                _externalIp = await GetExternalIp();

                WriteLogMessage("Loading Assembly... ", ConsoleColor.White, true);
                try
                { 
                    var asm = Assembly.LoadFile(Directory.GetCurrentDirectory() + @"\" + conf.TransportDLL);
                    WriteLogMessage($"OK", ConsoleColor.Green);

                    WriteLogMessage("\nLoading Transport Class... ", ConsoleColor.White, true);

                    transport = asm.CreateInstance(conf.TransportClass) as Transport;

                    if (transport != null)
                    {
                        var transportClass = asm.GetType(conf.TransportClass);
                        WriteLogMessage("OK", ConsoleColor.Green);

                        WriteLogMessage("\nLoading Transport Methods... ", ConsoleColor.White, true);
                        CheckMethods(transportClass);
                        WriteLogMessage("OK", ConsoleColor.Green);

                        WriteLogMessage("\nInvoking Transport Methods...");

                        if (_awakeMethod != null)
                            _awakeMethod.Invoke(transport, null);

                        if (_startMethod != null)
                            _startMethod.Invoke(transport, null);

                        WriteLogMessage("\nStarting Transport... ", ConsoleColor.White, true);

                        transport.OnServerError = (clientID, error) => 
                        {
                            WriteLogMessage($"Transport Error, Client: {clientID}, Error: {error}", ConsoleColor.Red);
                        };

                        transport.OnServerConnected = (clientID) =>
                        {
                            WriteLogMessage($"Transport Connected, Client: {clientID}", ConsoleColor.Cyan);
                            _currentConnections.Add(clientID);
                            _relay.ClientConnected(clientID);

                            if (conf.EnableNATPunchtroughServer)
                            {
                                string natID = Guid.NewGuid().ToString();
                                _pendingNATPunches.Add(clientID, natID);
                                _NATRequestPosition = 0;
                                _NATRequest.WriteByte(ref _NATRequestPosition, (byte)OpCodes.RequestNATConnection);
                                _NATRequest.WriteString(ref _NATRequestPosition, natID);
                                transport.ServerSend(clientID, 0, new ArraySegment<byte>(_NATRequest, 0, _NATRequestPosition));
                            }
                        };

                        _relay = new RelayHandler(transport.GetMaxPacketSize(0));

                        transport.OnServerDataReceived = _relay.HandleMessage;
                        transport.OnServerDisconnected = (clientID) =>
                        {
                            _currentConnections.Remove(clientID);
                            _relay.HandleDisconnect(clientID);

                            if(NATConnections.ContainsKey(clientID))
                                NATConnections.Remove(clientID);

                            if(_pendingNATPunches.TryGetByFirst(clientID, out _))
                                _pendingNATPunches.Remove(clientID);
                        };

                        transport.ServerStart();

                        WriteLogMessage("OK", ConsoleColor.Green);

                        if (conf.UseEndpoint)
                        {
                            WriteLogMessage("\nStarting Endpoint Service... ", ConsoleColor.White, true);
                            var endpoint = new EndpointServer();

                            if (endpoint.Start(conf.EndpointPort))
                            {
                                WriteLogMessage("OK", ConsoleColor.Green);
                            }
                            else
                            {
                                WriteLogMessage("FAILED\nPlease run as administrator or check if port is in use.", ConsoleColor.DarkRed);
                            }
                        }

                        if (conf.EnableNATPunchtroughServer)
                        {
                            WriteLogMessage("\nStarting NatPunchthrough Socket... ", ConsoleColor.White, true);

                            try
                            {
                                _punchServer = new UdpClient(conf.NATPunchtroughPort);

                                WriteLogMessage("OK\n", ConsoleColor.Green, true);

                                WriteLogMessage("\nStarting NatPunchthrough Thread... ", ConsoleColor.White, true);
                                var natThread = new Thread(new ThreadStart(RunNATPunchLoop));

                                try
                                {
                                    natThread.Start();
                                }
                                catch(Exception e)
                                {
                                    WriteLogMessage("FAILED\n" + e, ConsoleColor.DarkRed);
                                }
                            }
                            catch(Exception e)
                            {
                                WriteLogMessage("FAILED\nCheck if port is in use.", ConsoleColor.DarkRed, true);
                                Console.WriteLine(e);
                            }
                        }
                    }
                    else
                    {
                        WriteLogMessage("FAILED\nClass not found, make sure to included namespaces!", ConsoleColor.DarkRed);
                        Console.ReadKey();
                        Environment.Exit(0);
                    }
                }
                catch(Exception e)
                {
                    WriteLogMessage("FAILED\nException: " + e, ConsoleColor.DarkRed);
                    Console.ReadKey();
                    Environment.Exit(0);
                }

                await RegisterSelfToLoadBalancer();
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


        async Task<bool> RegisterSelfToLoadBalancer()
        {

            try
            {
                // replace hard coded value for config value later
                var uri = new Uri("http://localhost:7070/api/auth");
                string externalip = _externalIp.Normalize().Trim();
                string port = conf.EndpointPort.ToString();
                HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(uri);

                myRequest.Headers.Add("Auth", "AuthKey");
                myRequest.Headers.Add("Port", port);

                WebResponse myResponse = await myRequest.GetResponseAsync();

                return true;
            }
            catch
            {
                // error adding or load balancer unavailable
                WriteLogMessage("Error registering", ConsoleColor.Red);
                return false;
            }

        }

        async Task<string> GetExternalIp()
        {
            HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create("https://ipv4.icanhazip.com/");
            WebResponse myResponse = await myRequest.GetResponseAsync();

            Stream stream = myResponse.GetResponseStream();
            var ip = new StreamReader(stream).ReadToEnd();

            return ip;
        }

        void RunNATPunchLoop()
        {
            WriteLogMessage("OK\n", ConsoleColor.Green);
            IPEndPoint remoteEndpoint = new(IPAddress.Any, conf.NATPunchtroughPort);

            // Stock Data server sends to everyone:
            var serverResponse = new byte[1] { 1 };

            byte[] readData;
            bool isConnectionEstablishment;
            int pos;
            string connectionID;

            while (true)
            {
                readData = _punchServer.Receive(ref remoteEndpoint);
                pos = 0;
                try
                {
                    isConnectionEstablishment = readData.ReadBool(ref pos);

                    if (isConnectionEstablishment)
                    {
                        connectionID = readData.ReadString(ref pos);

                        if (_pendingNATPunches.TryGetBySecond(connectionID, out pos))
                        {
                            NATConnections.Add(pos, new IPEndPoint(remoteEndpoint.Address, remoteEndpoint.Port));
                            _pendingNATPunches.Remove(pos);
                            Console.WriteLine("Client Successfully Established Puncher Connection. " + remoteEndpoint.ToString());
                        }
                    }

                    _punchServer.Send(serverResponse, 1, remoteEndpoint);
                }
                catch
                {
                    // ignore, packet got fucked up or something.
                }
            }
        }

        static void WriteLogMessage(string message, ConsoleColor color = ConsoleColor.White, bool oneLine = false)
        {
            Console.ForegroundColor = color;
            if (oneLine)
                Console.Write(message);
            else
                Console.WriteLine(message);
        }

        void CheckMethods(Type type)
        {
            _awakeMethod         = type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _startMethod         = type.GetMethod("Start", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _updateMethod        = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _lateUpdateMethod    = type.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
    }
}
