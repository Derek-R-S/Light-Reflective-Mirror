// wraps Telepathy for use as HLAPI TransportLayer
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

// Replaced by Kcp November 2020
namespace Mirror
{
    public class TelepathyTransport : Transport
    {
        // scheme used by this transport
        // "tcp4" means tcp with 4 bytes header, network byte order
        public const string Scheme = "tcp4";

        public bool NoDelay = true;

        public int SendTimeout = 5000;

        public int ReceiveTimeout = 30000;

        public int serverMaxMessageSize = 16 * 1024;

        public int serverMaxReceivesPerTick = 10000;

        public int serverSendQueueLimitPerConnection = 10000;

        public int serverReceiveQueueLimitPerConnection = 10000;

        public int clientMaxMessageSize = 16 * 1024;

        public int clientMaxReceivesPerTick = 1000;

        public int clientSendQueueLimit = 10000;

        public int clientReceiveQueueLimit = 10000;

        Telepathy.Client client;
        Telepathy.Server server;

        // scene change message needs to halt  message processing immediately
        // Telepathy.Tick() has a enabledCheck parameter that we can use, but
        // let's only allocate it once.
        Func<bool> enabledCheck;

        public override void Awake()
        {
            TelepathyConfig conf = new TelepathyConfig();
            if (!File.Exists("TelepathyConfig.json"))
            {
                File.WriteAllText("TelepathyConfig.json", JsonConvert.SerializeObject(conf, Formatting.Indented));
            }
            else
            {
                conf = JsonConvert.DeserializeObject<TelepathyConfig>(File.ReadAllText("TelepathyConfig.json"));
            }

            NoDelay = conf.NoDelay;
            SendTimeout = conf.SendTimeout;
            ReceiveTimeout = conf.ReceiveTimeout;
            serverMaxMessageSize = conf.serverMaxMessageSize;
            serverMaxReceivesPerTick = conf.serverMaxReceivesPerTick;
            serverSendQueueLimitPerConnection = conf.serverSendQueueLimitPerConnection;
            serverReceiveQueueLimitPerConnection = conf.serverReceiveQueueLimitPerConnection;

            // create client & server
            client = new Telepathy.Client(clientMaxMessageSize);
            server = new Telepathy.Server(serverMaxMessageSize);

            // tell Telepathy to use Unity's Debug.Log
            Telepathy.Log.Info = Console.WriteLine;
            Telepathy.Log.Warning = Console.WriteLine;
            Telepathy.Log.Error = Console.WriteLine;

            // client hooks
            // other systems hook into transport events in OnCreate or
            // OnStartRunning in no particular order. the only way to avoid
            // race conditions where telepathy uses OnConnected before another
            // system's hook (e.g. statistics OnData) was added is to wrap
            // them all in a lambda and always call the latest hook.
            // (= lazy call)
            client.OnConnected = () => OnClientConnected.Invoke();
            client.OnData = (segment) => OnClientDataReceived.Invoke(segment, 0);
            client.OnDisconnected = () => OnClientDisconnected.Invoke();

            // client configuration
            client.NoDelay = NoDelay;
            client.SendTimeout = SendTimeout;
            client.ReceiveTimeout = ReceiveTimeout;
            client.SendQueueLimit = clientSendQueueLimit;
            client.ReceiveQueueLimit = clientReceiveQueueLimit;

            // server hooks
            // other systems hook into transport events in OnCreate or
            // OnStartRunning in no particular order. the only way to avoid
            // race conditions where telepathy uses OnConnected before another
            // system's hook (e.g. statistics OnData) was added is to wrap
            // them all in a lambda and always call the latest hook.
            // (= lazy call)
            server.OnConnected = (connectionId) => OnServerConnected.Invoke(connectionId);
            server.OnData = (connectionId, segment) => OnServerDataReceived.Invoke(connectionId, segment, 0);
            server.OnDisconnected = (connectionId) => OnServerDisconnected.Invoke(connectionId);

            // server configuration
            server.NoDelay = NoDelay;
            server.SendTimeout = SendTimeout;
            server.ReceiveTimeout = ReceiveTimeout;
            server.SendQueueLimit = serverSendQueueLimitPerConnection;
            server.ReceiveQueueLimit = serverReceiveQueueLimitPerConnection;

            // allocate enabled check only once
            enabledCheck = () => true;

            Console.WriteLine("TelepathyTransport initialized!");
        }

        public override bool Available()
        {
            // C#'s built in TCP sockets run everywhere except on WebGL
            return true;
        }

        // client
        public override bool ClientConnected() => client.Connected;
        public override void ClientConnect(string address) { }
        public override void ClientConnect(Uri uri) { }
        public override void ClientSend(int channelId, ArraySegment<byte> segment) => client.Send(segment);
        public override void ClientDisconnect() => client.Disconnect();
        // messages should always be processed in early update

        // server
        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder();
            builder.Scheme = Scheme;
            builder.Host = Dns.GetHostName();
            return builder.Uri;
        }
        public override bool ServerActive() => server.Active;
        public override void ServerStart(ushort requestedPort) => server.Start(requestedPort);
        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment) => server.Send(connectionId, segment);
        public override bool ServerDisconnect(int connectionId) => server.Disconnect(connectionId);
        public override string ServerGetClientAddress(int connectionId)
        {
            try
            {
                return server.GetClientAddress(connectionId);
            }
            catch (SocketException)
            {
                // using server.listener.LocalEndpoint causes an Exception
                // in UWP + Unity 2019:
                //   Exception thrown at 0x00007FF9755DA388 in UWF.exe:
                //   Microsoft C++ exception: Il2CppExceptionWrapper at memory
                //   location 0x000000E15A0FCDD0. SocketException: An address
                //   incompatible with the requested protocol was used at
                //   System.Net.Sockets.Socket.get_LocalEndPoint ()
                // so let's at least catch it and recover
                return "unknown";
            }
        }
        public override void ServerStop() => server.Stop();
        // messages should always be processed in early update
        public override void Update()
        {
            // note: we need to check enabled in case we set it to false
            // when LateUpdate already started.
            // (https://github.com/vis2k/Mirror/pull/379)

            // process a maximum amount of server messages per tick
            // IMPORTANT: check .enabled to stop processing immediately after a
            //            scene change message arrives!
            server.Tick(serverMaxReceivesPerTick, enabledCheck);
        }

        // common
        public override void Shutdown()
        {
            Console.WriteLine("TelepathyTransport Shutdown()");
            client.Disconnect();
            server.Stop();
        }

        public override int GetMaxPacketSize(int channelId)
        {
            return serverMaxMessageSize;
        }

        public override string ToString()
        {
            return "Telepathy";
        }
    }
}
