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
    partial class Program
    {
        public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();
        public List<Room> GetRooms() => _relay.rooms;

        public async Task MainAsync()
        {
            WriteTitle();
            instance = this;
            _startupTime = DateTime.Now;
            using (WebClient wc = new WebClient())
                publicIP = wc.DownloadString("http://ipv4.icanhazip.com").Replace("\\r", "").Replace("\\n", "").Trim();

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

                WriteLogMessage("Loading Assembly... ", ConsoleColor.White, true);
                try
                { 
                    var asm = Assembly.LoadFile(Path.GetFullPath(conf.TransportDLL));
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
                                _NATRequest.WriteInt(ref _NATRequestPosition, conf.NATPunchtroughPort);
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
                            var endpointService = new EndpointServer();

                            if (endpointService.Start(conf.EndpointPort))
                            {
                                WriteLogMessage("OK", ConsoleColor.Green);
                                Endpoint.RoomsModified();
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

                if (conf.UseLoadBalancer)
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

                    if (conf.UseLoadBalancer)
                    {
                        if (DateTime.Now > Endpoint.lastPing.AddSeconds(60))
                        {
                            // Dont await that on main thread. It would cause a lag spike for clients.
                            RegisterSelfToLoadBalancer();
                        }
                    }

                    GC.Collect();
                }

                await Task.Delay(conf.UpdateLoopTime);
            }
        }

        private async Task<bool> RegisterSelfToLoadBalancer()
        {
            Endpoint.lastPing = DateTime.Now;
            try
            {
                // replace hard coded value for config value later
                if (conf.LoadBalancerAddress.ToLower() == "localhost")
                    conf.LoadBalancerAddress = "127.0.0.1";

                var uri = new Uri($"http://{conf.LoadBalancerAddress}:{conf.LoadBalancerPort}/api/auth");
                string endpointPort = conf.EndpointPort.ToString();
                string gamePort = 7777.ToString();
                HttpWebRequest authReq = (HttpWebRequest)WebRequest.Create(uri);

                authReq.Headers.Add("Auth", conf.LoadBalancerAuthKey);
                authReq.Headers.Add("EndpointPort", endpointPort);
                authReq.Headers.Add("GamePort", gamePort);
                authReq.Headers.Add("PIP", publicIP); // Public IP

                var res = await authReq.GetResponseAsync();

                return true;
            }
            catch
            {
                // error adding or load balancer unavailable
                WriteLogMessage("Error registering - Load Balancer probably timed out.", ConsoleColor.Red);
                return false;
            }

        }

        void CheckMethods(Type type)
        {
            _awakeMethod         = type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _startMethod         = type.GetMethod("Start", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _updateMethod        = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _lateUpdateMethod    = type.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
