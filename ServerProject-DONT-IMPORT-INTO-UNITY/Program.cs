using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Mirror;
using Newtonsoft.Json;

namespace LightReflectiveMirror
{
    class Program
    {
        public static Config conf;
        RelayHandler relay;
        public static Transport transport;
        MethodInfo awakeMethod;
        MethodInfo startMethod;
        MethodInfo updateMethod;
        MethodInfo lateUpdateMethod;
        List<int> _currentConnections = new List<int>();
        int _currentHeartbeatTimer = 0;

        public static void Main(string[] args)
        => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {

            if (!File.Exists("config.json"))
            {
                File.WriteAllText("config.json", JsonConvert.SerializeObject(new Config(), Formatting.Indented));
                WriteLogMessage("A config.json file was generated. Please configure it to the proper settings and re-run!", ConsoleColor.Yellow);
                Console.ReadKey();
                Environment.Exit(0);
            }
            else
            {
                conf = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
                try
                {
                    Console.WriteLine(Directory.GetCurrentDirectory());
                    var asm = Assembly.LoadFile(Directory.GetCurrentDirectory() + @"\" + conf.TransportDLL);
                    WriteLogMessage($"Loaded Assembly: {asm.FullName}", ConsoleColor.Green);
                    transport = (Transport)asm.CreateInstance(conf.TransportClass);

                    if (transport != null)
                    {
                        WriteLogMessage($"Loaded Transport: {asm.GetType(conf.TransportClass).Name}! Loading Methods...", ConsoleColor.Green);
                        CheckMethods(asm.GetType(conf.TransportClass));

                        if (awakeMethod != null)
                        {
                            awakeMethod.Invoke(transport, null);
                            WriteLogMessage("Called Awake on transport.", ConsoleColor.Yellow);
                        }

                        if (startMethod != null)
                        {
                            awakeMethod.Invoke(transport, null);
                            WriteLogMessage("Called Start on transport.", ConsoleColor.Yellow);
                        }

                        WriteLogMessage("Starting Transport...", ConsoleColor.Green);

                        transport.OnServerError = (clientID, error) => {
                            WriteLogMessage($"Transport Error, Client: {clientID}, Error: {error}", ConsoleColor.Red);
                        };

                        transport.OnServerConnected = (clientID) =>
                        {
                            WriteLogMessage($"Transport Connected, Client: {clientID}", ConsoleColor.Cyan);
                            _currentConnections.Add(clientID);
                            relay.ClientConnected(clientID);
                        };

                        relay = new RelayHandler(transport.GetMaxPacketSize(0));

                        transport.OnServerDataReceived = relay.HandleMessage;
                        transport.OnServerDisconnected = (clientID) =>
                        {
                            _currentConnections.Remove(clientID);
                            relay.HandleDisconnect(clientID);
                        };

                        transport.ServerStart();

                        WriteLogMessage("Transport Started!", ConsoleColor.Green);
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
                if (updateMethod != null)
                    updateMethod.Invoke(transport, null);

                if (lateUpdateMethod != null)
                    lateUpdateMethod.Invoke(transport, null);

                _currentHeartbeatTimer++;

                if(_currentHeartbeatTimer >= conf.UpdateHeartbeatInterval)
                {
                    _currentHeartbeatTimer = 0;

                    for(int i = 0; i < _currentConnections.Count; i++)
                    {
                        transport.ServerSend(_currentConnections[i], 0, new ArraySegment<byte>(new byte[] { 200 }));
                    }

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
            awakeMethod         = type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            startMethod         = type.GetMethod("Start", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            updateMethod        = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            lateUpdateMethod    = type.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (awakeMethod != null)
                WriteLogMessage("'Awake' Loaded!", ConsoleColor.Yellow);

            if (startMethod != null)
                WriteLogMessage("'Start' Loaded!", ConsoleColor.Yellow);

            if (updateMethod != null)
                WriteLogMessage("'Update' Loaded!", ConsoleColor.Yellow);

            if (lateUpdateMethod != null)
                WriteLogMessage("'LateUpdate' Loaded!", ConsoleColor.Yellow);
        }
    }
}
