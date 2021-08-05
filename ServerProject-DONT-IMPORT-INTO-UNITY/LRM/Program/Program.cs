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

        public async Task MainAsync()
        {
            WriteTitle();
            instance = this;
            _startupTime = DateTime.Now;

            GetPublicIP();

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

                ConfigureDocker();

                WriteLogMessage("Loading Assembly... ", ConsoleColor.White, true);
                try
                {
                    var asm = Assembly.LoadFile(Path.GetFullPath(conf.TransportDLL));
                    WriteLogMessage($"OK", ConsoleColor.Green);

                    WriteLogMessage("\nLoading Transport Class... ", ConsoleColor.White, true);

                    transport = asm.CreateInstance(conf.TransportClass) as Transport;

                    if (transport != null)
                    {
                        ConfigureTransport(asm);

                        if (conf.UseEndpoint)
                            ConfigureEndpoint();

                        if (conf.EnableNATPunchtroughServer)
                            ConfigurePunchthrough();
                    }
                    else
                    {
                        WriteLogMessage("FAILED\nClass not found, make sure to included namespaces!", ConsoleColor.DarkRed);
                        Console.ReadKey();
                        Environment.Exit(0);
                    }
                }
                catch (Exception e)
                {
                    WriteLogMessage("FAILED\nException: " + e, ConsoleColor.DarkRed);
                    Console.ReadKey();
                    Environment.Exit(0);
                }

                if (conf.UseLoadBalancer)
                    await RegisterSelfToLoadBalancer();
            }

            await HeartbeatLoop();
        }

        private async Task HeartbeatLoop()
        {
            // default heartbeat data
            byte[] heartbeat = new byte[] { 200 };

            while (true)
            {
                try
                {
                    if (_updateMethod != null) _updateMethod.Invoke(transport, null);
                    if (_lateUpdateMethod != null) _lateUpdateMethod.Invoke(transport, null);
                }
                catch (Exception e)
                {
                    WriteLogMessage("Error During Transport Update! " + e, ConsoleColor.Red);
                }

                _currentHeartbeatTimer++;

                if (_currentHeartbeatTimer >= conf.UpdateHeartbeatInterval)
                {
                    _currentHeartbeatTimer = 0;

                    for (int i = 0; i < _currentConnections.Count; i++)
                        transport.ServerSend(_currentConnections[i], 0, new ArraySegment<byte>(heartbeat));

                    if (conf.UseLoadBalancer)
                    {
                        if (DateTime.Now > Endpoint.lastPing.AddSeconds(60))
                        {
                            // Dont await that on main thread. It would cause a lag spike for clients.
#pragma warning disable CS4014
                            RegisterSelfToLoadBalancer();
#pragma warning restore CS4014 
                        }
                    }

                    GC.Collect();
                }

                await Task.Delay(conf.UpdateLoopTime);
            }
        }

        public async void UpdateLoadBalancerServers()
        {
            try
            {
				using(WebClient wc = new())
                {
					wc.Headers.Add("Authorization", conf.LoadBalancerAuthKey);
					await wc.DownloadStringTaskAsync($"http://{conf.LoadBalancerAddress}:{conf.LoadBalancerPort}/api/roomsupdated");
				}
            }
            catch { } // LLB might be down, ignore.
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
                string gamePort = conf.TransportPort.ToString();
                HttpWebRequest authReq = (HttpWebRequest)WebRequest.Create(uri);

                ConfigureHeaders(endpointPort, gamePort, authReq);

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

        private static void ConfigureHeaders(string endpointPort, string gamePort, HttpWebRequest authReq)
        {
            authReq.Headers.Add("Authorization", conf.LoadBalancerAuthKey);
            authReq.Headers.Add("x-EndpointPort", endpointPort);
            authReq.Headers.Add("x-GamePort", gamePort);
            authReq.Headers.Add("x-PIP", publicIP); // Public IP
            authReq.Headers.Add("x-Region", ((int)conf.LoadBalancerRegion).ToString());
        }
    }
}
