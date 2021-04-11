using LightReflectiveMirror.Endpoints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LightReflectiveMirror
{
    public partial class Program
    {
        private void ConfigureTransport(Assembly asm)
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

                if (NATConnections.ContainsKey(clientID))
                    NATConnections.Remove(clientID);

                if (_pendingNATPunches.TryGetByFirst(clientID, out _))
                    _pendingNATPunches.Remove(clientID);
            };

            transport.ServerStart(conf.TransportPort);

            WriteLogMessage("OK", ConsoleColor.Green);
        }

        private static void ConfigureEndpoint()
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

        private void ConfigurePunchthrough()
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
                catch (Exception e)
                {
                    WriteLogMessage("FAILED\n" + e, ConsoleColor.DarkRed);
                }
            }
            catch (Exception e)
            {
                WriteLogMessage("FAILED\nCheck if port is in use.", ConsoleColor.DarkRed, true);
                Console.WriteLine(e);
            }
        }

        private static void ConfigureDocker()
        {
            // Docker variables.
            if (ushort.TryParse(Environment.GetEnvironmentVariable("LRM_ENDPOINT_PORT"), out ushort endpointPort))
                conf.EndpointPort = endpointPort;

            if (ushort.TryParse(Environment.GetEnvironmentVariable("LRM_TRANSPORT_PORT"), out ushort transportPort))
                conf.TransportPort = transportPort;

            if (ushort.TryParse(Environment.GetEnvironmentVariable("LRM_PUNCHER_PORT"), out ushort puncherPort))
                conf.NATPunchtroughPort = puncherPort;

            string LBAuthKey = Environment.GetEnvironmentVariable("LRM_LB_AUTHKEY");
            if (!string.IsNullOrWhiteSpace(LBAuthKey))
            {
                conf.LoadBalancerAuthKey = LBAuthKey;
                WriteLogMessage("\nLoaded LB auth key from environment variable\n", ConsoleColor.Green);
            }
        }

        void CheckMethods(Type type)
        {
            _awakeMethod = type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _startMethod = type.GetMethod("Start", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _updateMethod = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _lateUpdateMethod = type.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
