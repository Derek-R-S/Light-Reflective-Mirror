using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace LightReflectiveMirror
{
    public class LightReflectiveMirrorTransport : Transport
    {
        [Header("Connection Variables")]
        public Transport clientToServerTransport;
        public string serverIP = "34.67.125.123";
        public float heartBeatInterval = 3;
        public bool connectOnAwake = true;
        public string authenticationKey = "Secret Auth Key";
        public UnityEvent diconnectedFromRelay;
        [Header("Server Hosting Data")]
        public string serverName = "My awesome server!";
        public string extraServerData = "Map 1";
        public int maxServerPlayers = 10;
        public bool isPublicServer = true;
        [Header("Server List")]
        public UnityEvent serverListUpdated;
        public List<RelayServerInfo> relayServerList { private set; get; } = new List<RelayServerInfo>();
        [Header("Server Information")]
        public int serverId = -1;

        private byte[] _clientSendBuffer;
        private bool _connectedToRelay = false;
        private bool _isClient = false;
        private bool _isServer = false;
        private bool _isAuthenticated = false;
        private int _currentMemberId;
        private BiDictionary<int, int> _connectedRelayClients = new BiDictionary<int, int>();
        public bool IsAuthenticated() => _isAuthenticated;

        private void Awake()
        {
            if (clientToServerTransport is LightReflectiveMirrorTransport)
            {
                throw new Exception("Haha real funny... Use a different transport.");
            }

            if (connectOnAwake)
                ConnectToRelay();

            InvokeRepeating(nameof(SendHeartbeat), heartBeatInterval, heartBeatInterval);
        }

        private void OnEnable()
        {
            clientToServerTransport.OnClientConnected = ConnectedToRelay;
            clientToServerTransport.OnClientDataReceived = DataReceived;
            clientToServerTransport.OnClientDisconnected = Disconnected;
        }

        void Disconnected() => diconnectedFromRelay?.Invoke();

        public void ConnectToRelay()
        {
            if (!_connectedToRelay)
            {
                _clientSendBuffer = new byte[clientToServerTransport.GetMaxPacketSize()];

                clientToServerTransport.ClientConnect(serverIP);
            }
        }

        void SendHeartbeat()
        {
            if (_connectedToRelay)
            {
                int pos = 0;
                _clientSendBuffer.WriteByte(ref pos, 200);
                clientToServerTransport.ClientSend(0, new ArraySegment<byte>(_clientSendBuffer, 0, pos));
            }
        }

        void ConnectedToRelay()
        {
            _connectedToRelay = true;
        }

        public void RequestServerList()
        {
            if (_isAuthenticated)
            {
                int pos = 0;
                _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.RequestServers);
                clientToServerTransport.ClientSend(0, new ArraySegment<byte>(_clientSendBuffer, 0, pos));
            }
        }

        void DataReceived(ArraySegment<byte> segmentData, int channel)
        {
            try
            {
                var data = segmentData.Array;
                int pos = segmentData.Offset;

                OpCodes opcode = (OpCodes)data.ReadByte(ref pos);

                switch (opcode)
                {
                    case OpCodes.Authenticated:
                        _isAuthenticated = true;
                        break;
                    case OpCodes.AuthenticationRequest:
                        SendAuthKey();
                        break;
                    case OpCodes.GetData:
                        var recvData = data.ReadBytes(ref pos);

                        if (_isServer)
                            OnServerDataReceived?.Invoke(_connectedRelayClients.GetByFirst(data.ReadInt(ref pos)), new ArraySegment<byte>(recvData), channel);

                        if (_isClient)
                            OnClientDataReceived?.Invoke(new ArraySegment<byte>(recvData), channel);
                        break;
                    case OpCodes.ServerLeft:
                        if (_isClient)
                        {
                            _isClient = false;
                            OnClientDisconnected?.Invoke();
                        }
                        break;
                    case OpCodes.PlayerDisconnected:
                        if (_isServer)
                        {
                            int user = data.ReadInt(ref pos);
                            OnServerDisconnected?.Invoke(_connectedRelayClients.GetByFirst(user));
                            _connectedRelayClients.Remove(user);
                        }
                        break;
                    case OpCodes.RoomCreated:
                        serverId = data.ReadInt(ref pos);
                        break;
                    case OpCodes.ServerJoined:
                        int clientId = data.ReadInt(ref pos);
                        if (_isClient)
                        {
                            OnClientConnected?.Invoke();
                        }
                        if (_isServer)
                        {
                            _connectedRelayClients.Add(clientId, _currentMemberId);
                            OnServerConnected?.Invoke(_currentMemberId);
                            _currentMemberId++;
                        }
                        break;
                    case OpCodes.ServerListReponse:
                        relayServerList.Clear();
                        while(data.ReadBool(ref pos))
                        {
                            relayServerList.Add(new RelayServerInfo()
                            {
                                serverName = data.ReadString(ref pos),
                                serverData = data.ReadString(ref pos),
                                serverId = data.ReadInt(ref pos),
                                maxPlayers = data.ReadInt(ref pos),
                                currentPlayers = data.ReadInt(ref pos)
                            });
                        }
                        serverListUpdated?.Invoke();
                        break;
                }
            }
            catch { }
        }

        public void UpdateRoomInfo(string newServerName = null, string newServerData = null, bool? newServerIsPublic = null, int? newPlayerCap = null)
        {
            if (_isServer)
            {
                int pos = 0;

                _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.UpdateRoomData);

                if (!string.IsNullOrEmpty(newServerName))
                {
                    _clientSendBuffer.WriteBool(ref pos, true);
                    _clientSendBuffer.WriteString(ref pos, newServerName);
                }
                else
                    _clientSendBuffer.WriteBool(ref pos, false);

                if (!string.IsNullOrEmpty(newServerData))
                {
                    _clientSendBuffer.WriteBool(ref pos, true);
                    _clientSendBuffer.WriteString(ref pos, newServerData);
                }
                else
                    _clientSendBuffer.WriteBool(ref pos, false);

                if (newServerIsPublic != null)
                {
                    _clientSendBuffer.WriteBool(ref pos, true);
                    _clientSendBuffer.WriteBool(ref pos, newServerIsPublic.Value);
                }
                else
                    _clientSendBuffer.WriteBool(ref pos, false);

                if (newPlayerCap != null)
                {
                    _clientSendBuffer.WriteBool(ref pos, true);
                    _clientSendBuffer.WriteInt(ref pos, newPlayerCap.Value);
                }
                else
                    _clientSendBuffer.WriteBool(ref pos, false);

                clientToServerTransport.ClientSend(0, new ArraySegment<byte>(_clientSendBuffer, 0, pos));
            }
        }

        void SendAuthKey()
        {
            int pos = 0;
            _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.AuthenticationResponse);
            _clientSendBuffer.WriteString(ref pos, authenticationKey);
            clientToServerTransport.ClientSend(0, new ArraySegment<byte>(_clientSendBuffer, 0, pos));
        }

        public override bool Available() => _connectedToRelay;

        public override void ClientConnect(string address)
        {
            int hostId = 0;
            if (!Available() || !int.TryParse(address, out hostId))
            {
                Debug.Log("Not connected to relay or invalid server id!");
                OnClientDisconnected?.Invoke();
                return;
            }

            if (_isClient || _isServer)
            {
                throw new Exception("Cannot connect while hosting/already connected!");
            }

            int pos = 0;
            _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.JoinServer);
            _clientSendBuffer.WriteInt(ref pos, hostId);

            _isClient = true;

            clientToServerTransport.ClientSend(0, new System.ArraySegment<byte>(_clientSendBuffer, 0, pos));
        }

        public override void ClientConnect(Uri uri)
        {
            ClientConnect(uri.Host);
        }

        public override bool ClientConnected() => _isClient;

        public override void ClientDisconnect()
        {
            _isClient = false;

            int pos = 0;
            _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.LeaveRoom);

            clientToServerTransport.ClientSend(0, new ArraySegment<byte>(_clientSendBuffer, 0, pos));
        }

        public override void ClientSend(int channelId, ArraySegment<byte> segment)
        {
            int pos = 0;
            _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.SendData);
            _clientSendBuffer.WriteBytes(ref pos, segment.Array.Take(segment.Count).ToArray());
            _clientSendBuffer.WriteInt(ref pos, 0);

            clientToServerTransport.ClientSend(channelId, new ArraySegment<byte>(_clientSendBuffer, 0, pos));
        }

        public override int GetMaxPacketSize(int channelId = 0)
        {
            return clientToServerTransport.GetMaxPacketSize(channelId);
        }

        public override bool ServerActive() => _isServer;

        public override bool ServerDisconnect(int connectionId)
        {
            int relayId;
            
            if(_connectedRelayClients.TryGetBySecond(connectionId, out relayId))
            {
                int pos = 0;
                _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.KickPlayer);
                _clientSendBuffer.WriteInt(ref pos, relayId);
                return true;
            }

            return false;
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            return _connectedRelayClients.GetBySecond(connectionId).ToString();
        }

        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
        {
            int pos = 0;
            _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.SendData);
            _clientSendBuffer.WriteBytes(ref pos, segment.Array.Take(segment.Count).ToArray());
            _clientSendBuffer.WriteInt(ref pos, _connectedRelayClients.GetBySecond(connectionId));

            clientToServerTransport.ClientSend(channelId, new ArraySegment<byte>(_clientSendBuffer, 0, pos));
        }

        public override void ServerStart()
        {
            if (!Available())
            {
                Debug.Log("Not connected to relay! Server failed to start.");
                return;
            }

            if(_isClient || _isServer)
            {
                Debug.Log("Cannot host while already hosting or connected!");
                return;
            }

            _isServer = true;
            _connectedRelayClients = new BiDictionary<int, int>();
            _currentMemberId = 1;

            int pos = 0;
            _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.CreateRoom);
            _clientSendBuffer.WriteInt(ref pos, maxServerPlayers);
            _clientSendBuffer.WriteString(ref pos, serverName);
            _clientSendBuffer.WriteBool(ref pos, isPublicServer);
            _clientSendBuffer.WriteString(ref pos, extraServerData);

            clientToServerTransport.ClientSend(0, new ArraySegment<byte>(_clientSendBuffer, 0, pos));
        }

        public override void ServerStop()
        {
            if (_isServer)
            {
                _isServer = false;
                int pos = 0;
                _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.LeaveRoom);

                clientToServerTransport.ClientSend(0, new ArraySegment<byte>(_clientSendBuffer, 0, pos));
            }
        }

        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder();
            builder.Scheme = "LRM";
            builder.Host = serverId.ToString();
            return builder.Uri;
        }

        public override void Shutdown()
        {
            _isAuthenticated = false;
            _isClient = false;
            _isServer = false;
            _connectedToRelay = false;
            clientToServerTransport.Shutdown();
        }


        public enum OpCodes
        {
            Default = 0, RequestID = 1, JoinServer = 2, SendData = 3, GetID = 4, ServerJoined = 5, GetData = 6, CreateRoom = 7, ServerLeft = 8, PlayerDisconnected = 9, RoomCreated = 10,
            LeaveRoom = 11, KickPlayer = 12, AuthenticationRequest = 13, AuthenticationResponse = 14, RequestServers = 15, ServerListReponse = 16, Authenticated = 17, UpdateRoomData = 18, ServerConnectionData = 19
        }
    }

    [Serializable]
    public struct RelayServerInfo
    {
        public string serverName;
        public int currentPlayers;
        public int maxPlayers;
        public int serverId;
        public string serverData;
    }
}