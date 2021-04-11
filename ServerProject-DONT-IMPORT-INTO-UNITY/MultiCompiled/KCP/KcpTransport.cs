//#if MIRROR <- commented out because MIRROR isn't defined on first import yet
using System;
using System.IO;
using System.Linq;
using System.Net;
using Mirror;
using Newtonsoft.Json;

namespace kcp2k
{
    public class KcpTransport : Transport
    {
        // scheme used by this transport
        public const string Scheme = "kcp";

        // common
        public static int ConnectionTimeout = 10000;
        
        public bool NoDelay = true;
        
        public uint Interval = 10;
        
        public int FastResend = 2;
        
        public bool CongestionWindow = false; // KCP 'NoCongestionWindow' is false by default. here we negate it for ease of use.
        
        public uint SendWindowSize = 4096; //Kcp.WND_SND; 32 by default. Mirror sends a lot, so we need a lot more.
        
        public uint ReceiveWindowSize = 4096; //Kcp.WND_RCV; 128 by default. Mirror sends a lot, so we need a lot more.

        // server & client
        KcpServer server;
        KcpClient client;

        // debugging
        public bool debugLog;
        // show statistics in OnGUI
        public bool statisticsGUI;
        // log statistics for headless servers that can't show them in GUI
        public bool statisticsLog;

        public override void Awake()
        {

            KCPConfig conf = new KCPConfig();
            if (!File.Exists("KCPConfig.json"))
            {
                File.WriteAllText("KCPConfig.json", JsonConvert.SerializeObject(conf, Formatting.Indented));
            }
            else
            {
                conf = JsonConvert.DeserializeObject<KCPConfig>(File.ReadAllText("KCPConfig.json"));
            }

            NoDelay = conf.NoDelay;
            Interval = conf.Interval;
            FastResend = conf.FastResend;
            CongestionWindow = conf.CongestionWindow;
            SendWindowSize = conf.SendWindowSize;
            ReceiveWindowSize = conf.ReceiveWindowSize;
            ConnectionTimeout = conf.ConnectionTimeout;

            // logging
            //   Log.Info should use Debug.Log if enabled, or nothing otherwise
            //   (don't want to spam the console on headless servers)
            if (debugLog)
                Log.Info = Console.WriteLine;
            else
                Log.Info = _ => { };
            Log.Warning = Console.WriteLine;
            Log.Error = Console.WriteLine;

            // client
            client = new KcpClient(
                () => OnClientConnected.Invoke(),
                (message) => OnClientDataReceived.Invoke(message, 0),
                () => OnClientDisconnected.Invoke()
            );

            // server
            server = new KcpServer(
                (connectionId) => OnServerConnected.Invoke(connectionId),
                (connectionId, message) => OnServerDataReceived.Invoke(connectionId, message, 0),
                (connectionId) => OnServerDisconnected.Invoke(connectionId),
                NoDelay,
                Interval,
                FastResend,
                CongestionWindow,
                SendWindowSize,
                ReceiveWindowSize
            );


            Console.WriteLine("KcpTransport initialized!");
        }

        // all except WebGL
        public override bool Available() => true;

        // client
        public override bool ClientConnected() => client.connected;
        public override void ClientConnect(string address) { }
        public override void ClientSend(int channelId, ArraySegment<byte> segment)
        {
            // switch to kcp channel.
            // unreliable or reliable.
            // default to reliable just to be sure.
            switch (channelId)
            {
                case 1:
                    client.Send(segment, KcpChannel.Unreliable);
                    break;
                default:
                    client.Send(segment, KcpChannel.Reliable);
                    break;
            }
        }
        public override void ClientDisconnect() => client.Disconnect();

        // scene change message will disable transports.
        // kcp processes messages in an internal loop which should be
        // stopped immediately after scene change (= after disabled)
        // => kcp has tests to guaranteed that calling .Pause() during the
        //    receive loop stops the receive loop immediately, not after.
        void OnEnable()
        {
            // unpause when enabled again
            client?.Unpause();
            server?.Unpause();
        }

        void OnDisable()
        {
            // pause immediately when not enabled anymore
            client?.Pause();
            server?.Pause();
        }

        // server
        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder();
            builder.Scheme = Scheme;
            builder.Host = Dns.GetHostName();
            return builder.Uri;
        }
        public override bool ServerActive() => server.IsActive();
        public override void ServerStart(ushort requestedPort) => server.Start(requestedPort);
        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
        {
            // switch to kcp channel.
            // unreliable or reliable.
            // default to reliable just to be sure.
            switch (channelId)
            {
                case 1:
                    server.Send(connectionId, segment, KcpChannel.Unreliable);
                    break;
                default:
                    server.Send(connectionId, segment, KcpChannel.Reliable);
                    break;
            }
        }
        public override bool ServerDisconnect(int connectionId)
        {
            server.Disconnect(connectionId);
            return true;
        }
        public override string ServerGetClientAddress(int connectionId) => server.GetClientAddress(connectionId);
        public override void ServerStop() => server.Stop();

        public override void Update()
        {
            server.TickIncoming();
            server.TickOutgoing();
        }

        // common
        public override void Shutdown() {}

        // max message size
        public override int GetMaxPacketSize(int channelId = 0)
        {
            // switch to kcp channel.
            // unreliable or reliable.
            // default to reliable just to be sure.
            switch (channelId)
            {
                case 1:
                    return KcpConnection.UnreliableMaxMessageSize;
                default:
                    return KcpConnection.ReliableMaxMessageSize;
            }
        }


        // server statistics
        public int GetAverageMaxSendRate() =>
            server.connections.Count > 0
                ? server.connections.Values.Sum(conn => (int)conn.MaxSendRate) / server.connections.Count
                : 0;
        public int GetAverageMaxReceiveRate() =>
            server.connections.Count > 0
                ? server.connections.Values.Sum(conn => (int)conn.MaxReceiveRate) / server.connections.Count
                : 0;
        int GetTotalSendQueue() =>
            server.connections.Values.Sum(conn => conn.SendQueueCount);
        int GetTotalReceiveQueue() =>
            server.connections.Values.Sum(conn => conn.ReceiveQueueCount);
        int GetTotalSendBuffer() =>
            server.connections.Values.Sum(conn => conn.SendBufferCount);
        int GetTotalReceiveBuffer() =>
            server.connections.Values.Sum(conn => conn.ReceiveBufferCount);

        // PrettyBytes function from DOTSNET
        // pretty prints bytes as KB/MB/GB/etc.
        // long to support > 2GB
        // divides by floats to return "2.5MB" etc.
        public static string PrettyBytes(long bytes)
        {
            // bytes
            if (bytes < 1024)
                return $"{bytes} B";
            // kilobytes
            else if (bytes < 1024L * 1024L)
                return $"{(bytes / 1024f):F2} KB";
            // megabytes
            else if (bytes < 1024 * 1024L * 1024L)
                return $"{(bytes / (1024f * 1024f)):F2} MB";
            // gigabytes
            return $"{(bytes / (1024f * 1024f * 1024f)):F2} GB";
        }

        public override string ToString() => "KCP";
    }
}
//#endif MIRROR <- commented out because MIRROR isn't defined on first import yet
