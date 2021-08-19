// Ignorance 1.4.x
// Ignorance. It really kicks the Unity LLAPIs ass.
// https://github.com/SoftwareGuy/Ignorance
// -----------------
// Copyright (c) 2019 - 2020 Matt Coburn (SoftwareGuy/Coburn64)
// Ignorance Transport is licensed under the MIT license. Refer
// to the LICENSE file for more information.
// -----------------
// Ignorance Experimental (New) Version
// -----------------
using ENet;
using Mirror;
using System;
using System.Collections.Generic;

namespace IgnoranceTransport
{
    public class Ignorance : Transport
    {
        public int port = 7777;

        public IgnoranceLogType LogType = IgnoranceLogType.Standard;
        public bool DebugDisplay = false;

        public bool serverBindsAll = true;
        public string serverBindAddress = string.Empty;
        public int serverMaxPeerCapacity = 50;
        public int serverMaxNativeWaitTime = 1;

        public int clientMaxNativeWaitTime = 3;

        public IgnoranceChannelTypes[] Channels = new[] { IgnoranceChannelTypes.Reliable, IgnoranceChannelTypes.Unreliable };

        public int PacketBufferCapacity = 4096;

        public int MaxAllowedPacketSize = 33554432;

        public IgnoranceClientStats ClientStatistics;

        public override bool Available()
        {
            return true;
        }

        public override void Awake()
        {
            if (LogType != IgnoranceLogType.Nothing)
                Console.WriteLine($"Thanks for using Ignorance {IgnoranceInternals.Version}. Keep up to date, report bugs and support the developer at https://github.com/SoftwareGuy/Ignorance!");
        }

        public override string ToString()
        {
            return $"Ignorance v{IgnoranceInternals.Version}";
        }

        public override void ClientConnect(string address)
        {
            ClientState = ConnectionState.Connecting;
            cachedConnectionAddress = address;

            // Initialize.
            InitializeClientBackend();

            // Get going.            
            ignoreDataPackets = false;

            // Start!
            Client.Start();
        }

        public override void ClientConnect(Uri uri)
        {
            if (uri.Scheme != IgnoranceInternals.Scheme)
                throw new ArgumentException($"You used an invalid URI: {uri}. Please use {IgnoranceInternals.Scheme}://host:port instead", nameof(uri));

            if (!uri.IsDefaultPort)
				// Set the communication port to the one specified.
                port = uri.Port;

            // Pass onwards to the proper handler.
            ClientConnect(uri.Host);
        }

        public override bool ClientConnected() => ClientState == ConnectionState.Connected;

        public override void ClientDisconnect()
        {
            if (Client != null)
                Client.Stop();

			// TODO: Figure this one out to see if it's related to a race condition.
			// Maybe experiment with a while loop to pause main thread when disconnecting, 
			// since client might not stop on a dime.			
			// while(Client.IsAlive) ;
            // v1.4.0b1: Probably fixed in IgnoranceClient.cs; need further testing.
			
            // ignoreDataPackets = true;
            ClientState = ConnectionState.Disconnected;
        }


        // v1.4.0b6: Mirror rearranged the ClientSend params, so we need to apply a fix for that or
        // we end up using the obsoleted version. The obsolete version isn't a fatal error, but
        // it's best to stick with the new structures.
        public override void ClientSend(int channelId, ArraySegment<byte> segment)
        {
            if (Client == null)
            {
                
                return;
            }

            if (channelId < 0 || channelId > Channels.Length)
            {
               
                return;
            }

            // Create our struct...
            Packet clientOutgoingPacket = default;
            int byteCount = segment.Count;
            int byteOffset = segment.Offset;
            // Set our desired flags...
            PacketFlags desiredFlags = (PacketFlags)Channels[channelId];

            // Create the packet.
            clientOutgoingPacket.Create(segment.Array, byteOffset, byteCount + byteOffset, desiredFlags);
            // byteCount

            // Enqueue the packet.
            IgnoranceOutgoingPacket dispatchPacket = new IgnoranceOutgoingPacket
            {
                Channel = (byte)channelId,
                Payload = clientOutgoingPacket
            };

            // Pass the packet onto the thread for dispatch.
            Client.Outgoing.Enqueue(dispatchPacket);
        }

        public override bool ServerActive()
        {
            // Very simple check.
            return Server != null && Server.IsAlive;
        }

        public override bool ServerDisconnect(int connectionId) => ServerDisconnectLegacy(connectionId);

        public override string ServerGetClientAddress(int connectionId)
        {
            if (ConnectionLookupDict.TryGetValue(connectionId, out PeerConnectionData details))
                return $"{details.IP}:{details.Port}";

            return "(unavailable)";
        }

        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
        {
            // Debug.Log($"ServerSend({connectionId}, {channelId}, <{segment.Count} byte segment>)");

            if (Server == null)
            {
               // Debug.LogError("Cannot enqueue data packet; our Server object is null. Something has gone wrong.");
                return;
            }

            if (channelId < 0 || channelId > Channels.Length)
            {
             //   Debug.LogError("Channel ID is out of bounds.");
                return;
            }

            // Packet Struct
            Packet serverOutgoingPacket = default;
            int byteCount = segment.Count;
            int byteOffset = segment.Offset;
            PacketFlags desiredFlags = (PacketFlags)Channels[channelId];

            // Create the packet.
            serverOutgoingPacket.Create(segment.Array, byteOffset, byteCount + byteOffset, desiredFlags);

            // Enqueue the packet.
            IgnoranceOutgoingPacket dispatchPacket = new IgnoranceOutgoingPacket
            {
                Channel = (byte)channelId,
                NativePeerId = (uint)connectionId - 1, // ENet's native peer ID will be ConnID - 1
                Payload = serverOutgoingPacket
            };

            Server.Outgoing.Enqueue(dispatchPacket);

        }

        public override void ServerStart(ushort _port)
        {
            if (LogType != IgnoranceLogType.Nothing)
                Console.WriteLine("Ignorance Server Instance starting up...");
            port = _port;

            InitializeServerBackend();

            Server.Start();
        }

        public override void ServerStop()
        {
            if (Server != null)
            {
                if (LogType != IgnoranceLogType.Nothing)
                    Console.WriteLine("Ignorance Server Instance shutting down...");

                Server.Stop();
            }

            ConnectionLookupDict.Clear();
        }

        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder
            {
                Scheme = IgnoranceInternals.Scheme,
                Host = serverBindAddress,
                Port = port
            };

            return builder.Uri;
        }

        public override void Shutdown()
        {
            // TODO: Nothing needed here?
        }

        private void InitializeServerBackend()
        {
            if (Server == null)
            {
               // Debug.LogWarning("IgnoranceServer reference for Server mode was null. This shouldn't happen, but to be safe we'll reinitialize it.");
                Server = new IgnoranceServer();
            }

            // Set up the new IgnoranceServer reference.
            if (serverBindsAll)
                // MacOS is special. It's also a massive thorn in my backside.
                Server.BindAddress = IgnoranceInternals.BindAllMacs;
            else
                // Use the supplied bind address.
                Server.BindAddress = serverBindAddress;

            // Sets port, maximum peers, max channels, the server poll time, maximum packet size and verbosity.
            Server.BindPort = port;
            Server.MaximumPeers = serverMaxPeerCapacity;
            Server.MaximumChannels = Channels.Length;
            Server.PollTime = serverMaxNativeWaitTime;
            Server.MaximumPacketSize = MaxAllowedPacketSize;
            Server.Verbosity = (int)LogType;

            // Initializes the packet buffer.
            // Allocates once, that's it.
            if (InternalPacketBuffer == null)
                InternalPacketBuffer = new byte[PacketBufferCapacity];
        }

        private void InitializeClientBackend()
        {
            if (Client == null)
            {
              //  Debug.LogWarning("Ignorance: IgnoranceClient reference for Client mode was null. This shouldn't happen, but to be safe we'll reinitialize it.");
                Client = new IgnoranceClient();
            }

            // Sets address, port, channels to expect, verbosity, the server poll time and maximum packet size.
            Client.ConnectAddress = cachedConnectionAddress;
            Client.ConnectPort = port;
            Client.ExpectedChannels = Channels.Length;
            Client.PollTime = clientMaxNativeWaitTime;
            Client.MaximumPacketSize = MaxAllowedPacketSize;
            Client.Verbosity = (int)LogType;

            // Initializes the packet buffer.
            // Allocates once, that's it.
            if (InternalPacketBuffer == null)
                InternalPacketBuffer = new byte[PacketBufferCapacity];
        }

        private void ProcessServerPackets()
        {
            IgnoranceIncomingPacket incomingPacket;
            IgnoranceConnectionEvent connectionEvent;
            int adjustedConnectionId;
            Packet payload;

            // Incoming connection events.
            while (Server.ConnectionEvents.TryDequeue(out connectionEvent))
            {
                adjustedConnectionId = (int)connectionEvent.NativePeerId + 1;

                // TODO: Investigate ArgumentException: An item with the same key has already been added. Key: <id>
                ConnectionLookupDict.Add(adjustedConnectionId, new PeerConnectionData
                {
                    NativePeerId = connectionEvent.NativePeerId,
                    IP = connectionEvent.IP,
                    Port = connectionEvent.Port
                });

                OnServerConnected?.Invoke(adjustedConnectionId);
            }

            // Handle incoming data packets.
            // Console.WriteLine($"Server Incoming Queue is {Server.Incoming.Count}");
            while (Server.Incoming.TryDequeue(out incomingPacket))
            {
                adjustedConnectionId = (int)incomingPacket.NativePeerId + 1;
                payload = incomingPacket.Payload;

                int length = payload.Length;
                ArraySegment<byte> dataSegment;

                // Copy to working buffer and dispose of it.
                if (length > InternalPacketBuffer.Length)
                {
                    byte[] oneFreshNTastyGcAlloc = new byte[length];

                    payload.CopyTo(oneFreshNTastyGcAlloc);
                    dataSegment = new ArraySegment<byte>(oneFreshNTastyGcAlloc, 0, length);
                }
                else
                {
                    payload.CopyTo(InternalPacketBuffer);
                    dataSegment = new ArraySegment<byte>(InternalPacketBuffer, 0, length);
                }

                payload.Dispose();

                OnServerDataReceived?.Invoke(adjustedConnectionId, dataSegment, incomingPacket.Channel);
            }

            // Disconnection events.
            while (Server.DisconnectionEvents.TryDequeue(out IgnoranceConnectionEvent disconnectionEvent))
            {
                adjustedConnectionId = (int)disconnectionEvent.NativePeerId + 1;

                ConnectionLookupDict.Remove(adjustedConnectionId);

                // Invoke Mirror handler.
                OnServerDisconnected?.Invoke(adjustedConnectionId);
            }
        }

        private void ProcessClientPackets()
        {
            Packet payload;

            // Handle connection events.
            while (Client.ConnectionEvents.TryDequeue(out IgnoranceConnectionEvent connectionEvent))
            {

                if (connectionEvent.WasDisconnect)
                {
                    // Disconnected from server.
                    ClientState = ConnectionState.Disconnected;

                    ignoreDataPackets = true;
                    OnClientDisconnected?.Invoke();
                }
                else
                {
                    // Connected to server.
                    ClientState = ConnectionState.Connected;

                    ignoreDataPackets = false;
                    OnClientConnected?.Invoke();
                }
            }

            // Now handle the incoming messages.
            while (Client.Incoming.TryDequeue(out IgnoranceIncomingPacket incomingPacket))
            {
                // Temporary fix: if ENet thread is too fast for Mirror, then ignore the packet.
                // This is seen sometimes if you stop the client and there's still stuff in the queue.
                if (ignoreDataPackets)
                {
                    break;
                }

                // Otherwise client recieved data, advise Mirror.
                // print($"Byte array: {incomingPacket.RentedByteArray.Length}. Packet Length: {incomingPacket.Length}");
                payload = incomingPacket.Payload;
                int length = payload.Length;
                ArraySegment<byte> dataSegment;

                // Copy to working buffer and dispose of it.
                if (length > InternalPacketBuffer.Length)
                {
                    // Unity's favourite: A fresh 'n' tasty GC Allocation!
                    byte[] oneFreshNTastyGcAlloc = new byte[length];

                    payload.CopyTo(oneFreshNTastyGcAlloc);
                    dataSegment = new ArraySegment<byte>(oneFreshNTastyGcAlloc, 0, length);
                }
                else
                {
                    payload.CopyTo(InternalPacketBuffer);
                    dataSegment = new ArraySegment<byte>(InternalPacketBuffer, 0, length);
                }

                payload.Dispose();

                OnClientDataReceived?.Invoke(dataSegment, incomingPacket.Channel);
            }

            // Step 3: Handle other commands.
            while (Client.Commands.TryDequeue(out IgnoranceCommandPacket commandPacket))
            {
                switch (commandPacket.Type)
                {
                    // ...
                    default:
                        break;
                }
            }

            // Step 4: Handle status updates.
            if (Client.StatusUpdates.TryDequeue(out IgnoranceClientStats clientStats))
            {
                ClientStatistics = clientStats;
            }
        }

        // Ignorance 1.4.0b5: To use Mirror's polling or not use Mirror's polling, that is up to the developer to decide

        // IMPORTANT: Set Ignorance' execution order before everything else. Yes, that's -32000 !!
        // This ensures it has priority over other things.

        // FixedUpdate can be called many times per frame.
        // Once we've handled stuff, we set a flag so that we don't poll again for this frame.

        private bool fixedUpdateCompletedWork;
        public void FixedUpdate()
        {
            if (fixedUpdateCompletedWork) return;

            ProcessAndExecuteAllPackets();

            // Flip the bool to signal we've done our work.
            fixedUpdateCompletedWork = true;
        }

        // Normally, Mirror blocks Update() due to poor design decisions...
        // But thanks to Vincenzo, we've found a way to bypass that block.
        // Update is called once per frame. We don't have to worry about this shit now.
        public override void Update()
        {
            // Process what FixedUpdate missed, only if the boolean is not set.
            if (!fixedUpdateCompletedWork)
                ProcessAndExecuteAllPackets();

            // Flip back the bool, so it can be reset.
            fixedUpdateCompletedWork = false;
        }

        // Processes and Executes All Packets.
        private void ProcessAndExecuteAllPackets()
        {
            // Process Server Events...
            if (Server.IsAlive)
                ProcessServerPackets();

            // Process Client Events...
            if (Client.IsAlive)
            {
                ProcessClientPackets();
            }
        }

        public override int GetMaxPacketSize(int channelId = 0) => MaxAllowedPacketSize;

        private bool ignoreDataPackets;
        private string cachedConnectionAddress = string.Empty;
        private IgnoranceServer Server = new IgnoranceServer();
        private IgnoranceClient Client = new IgnoranceClient();
        private Dictionary<int, PeerConnectionData> ConnectionLookupDict = new Dictionary<int, PeerConnectionData>();

        private enum ConnectionState { Connecting, Connected, Disconnecting, Disconnected }
        private ConnectionState ClientState = ConnectionState.Disconnected;
        private byte[] InternalPacketBuffer;

        public bool ServerDisconnectLegacy(int connectionId)
        {
            if (Server == null)
            {
             //   Debug.LogError("Cannot enqueue kick packet; our Server object is null. Something has gone wrong.");
                // Return here because otherwise we will get a NRE when trying to enqueue the kick packet.
                return false;
            }

            IgnoranceCommandPacket kickPacket = new IgnoranceCommandPacket
            {
                Type = IgnoranceCommandType.ServerKickPeer,
                PeerId = (uint)connectionId - 1 // ENet's native peer ID will be ConnID - 1
            };

            // Pass the packet onto the thread for dispatch.
            Server.Commands.Enqueue(kickPacket);
            return true;
        }

    }
}
