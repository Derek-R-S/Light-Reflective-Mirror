using Mirror.SimpleWeb;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Security.Authentication;

namespace Mirror
{
    public class SimpleWebTransport : Transport
    {
        public const string NormalScheme = "ws";
        public const string SecureScheme = "wss";

        public int maxMessageSize = 16 * 1024;

        public int handshakeMaxSize = 3000;

        public bool noDelay = true;

        public int sendTimeout = 5000;

        public int receiveTimeout = 20000;

        public int serverMaxMessagesPerTick = 10000;

        public int clientMaxMessagesPerTick = 1000;

        public bool batchSend = true;

        public bool waitBeforeSend = false;


        public bool clientUseWss;

        public bool sslEnabled;
        
        public string sslCertJson = "./cert.json";
        public SslProtocols sslProtocols = SslProtocols.Tls12;

        Log.Levels _logLevels = Log.Levels.none;

        /// <summary>
        /// <para>Gets _logLevels field</para>
        /// <para>Sets _logLevels and Log.level fields</para>
        /// </summary>
        public Log.Levels LogLevels
        {
            get => _logLevels;
            set
            {
                _logLevels = value;
                Log.level = _logLevels;
            }
        }

        void OnValidate()
        {
            if (maxMessageSize > ushort.MaxValue)
            {
                Console.WriteLine($"max supported value for maxMessageSize is {ushort.MaxValue}");
                maxMessageSize = ushort.MaxValue;
            }

            Log.level = _logLevels;
        }

        SimpleWebServer server;

        TcpConfig TcpConfig => new TcpConfig(noDelay, sendTimeout, receiveTimeout);

        public override bool Available()
        {
            return true;
        }
        public override int GetMaxPacketSize(int channelId = 0)
        {
            return maxMessageSize;
        }

        public override void Awake()
        {
            Log.level = _logLevels;


            SWTConfig conf = new SWTConfig();
            if (!File.Exists("SWTConfig.json"))
            {
                File.WriteAllText("SWTConfig.json", JsonConvert.SerializeObject(conf, Formatting.Indented));
            }
            else
            {
                conf = JsonConvert.DeserializeObject<SWTConfig>(File.ReadAllText("SWTConfig.json"));
            }

            maxMessageSize = conf.maxMessageSize;
            handshakeMaxSize = conf.handshakeMaxSize;
            noDelay = conf.noDelay;
            sendTimeout = conf.sendTimeout;
            receiveTimeout = conf.receiveTimeout;
            serverMaxMessagesPerTick = conf.serverMaxMessagesPerTick;
            waitBeforeSend = conf.waitBeforeSend;
            clientUseWss = conf.clientUseWss;
            sslEnabled = conf.sslEnabled;
            sslCertJson = conf.sslCertJson;
            sslProtocols = conf.sslProtocols;
        }

        public override void Shutdown()
        {
            server?.Stop();
            server = null;
        }

        #region Client
        string GetClientScheme() => (sslEnabled || clientUseWss) ? SecureScheme : NormalScheme;
        string GetServerScheme() => sslEnabled ? SecureScheme : NormalScheme;
        public override bool ClientConnected()
        {
            // not null and not NotConnected (we want to return true if connecting or disconnecting)
            return false;
        }

        public override void ClientConnect(string hostname) { }

        public override void ClientDisconnect() { }

        public override void ClientSend(int channelId, ArraySegment<byte> segment) { }
        #endregion

        #region Server
        public override bool ServerActive()
        {
            return server != null && server.Active;
        }

        public override void ServerStart(ushort requestedPort)
        {
            if (ServerActive())
            {
                Console.WriteLine("SimpleWebServer Already Started");
            }

            SslConfig config = SslConfigLoader.Load(this);
            server = new SimpleWebServer(serverMaxMessagesPerTick, TcpConfig, maxMessageSize, handshakeMaxSize, config);

            server.onConnect += OnServerConnected.Invoke;
            server.onDisconnect += OnServerDisconnected.Invoke;
            server.onData += (int connId, ArraySegment<byte> data) => OnServerDataReceived.Invoke(connId, data, 0);
            server.onError += OnServerError.Invoke;

            SendLoopConfig.batchSend = batchSend || waitBeforeSend;
            SendLoopConfig.sleepBeforeSend = waitBeforeSend;

            server.Start(requestedPort);
        }

        public override void ServerStop()
        {
            if (!ServerActive())
            {
                Console.WriteLine("SimpleWebServer Not Active");
            }

            server.Stop();
            server = null;
        }

        public override bool ServerDisconnect(int connectionId)
        {
            if (!ServerActive())
            {
                Console.WriteLine("SimpleWebServer Not Active");
                return false;
            }

            return server.KickClient(connectionId);
        }

        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
        {
            if (!ServerActive())
            {
                Console.WriteLine("SimpleWebServer Not Active");
                return;
            }

            if (segment.Count > maxMessageSize)
            {
                Console.WriteLine("Message greater than max size");
                return;
            }

            if (segment.Count == 0)
            {
                Console.WriteLine("Message count was zero");
                return;
            }

            server.SendOne(connectionId, segment);
            return;
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            return server.GetClientAddress(connectionId);
        }

        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder
            {
                Scheme = GetServerScheme(),
                Host = Dns.GetHostName()
            };
            return builder.Uri;
        }

        public override void Update()
        {
            server?.ProcessMessageQueue();
        }
        #endregion
    }
}
