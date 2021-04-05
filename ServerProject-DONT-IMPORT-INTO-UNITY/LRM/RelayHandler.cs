using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace LightReflectiveMirror
{
    public class RelayHandler
    {
        public List<Room> rooms = new List<Room>();
        private List<int> _pendingAuthentication = new List<int>();
        private ArrayPool<byte> _sendBuffers;
        private int _maxPacketSize = 0;

        public RelayHandler(int maxPacketSize)
        {
            this._maxPacketSize = maxPacketSize;
            _sendBuffers = ArrayPool<byte>.Create(maxPacketSize, 50);
        }

        public void ClientConnected(int clientId)
        {
            _pendingAuthentication.Add(clientId);
            var buffer = _sendBuffers.Rent(1);
            int pos = 0;
            buffer.WriteByte(ref pos, (byte)OpCodes.AuthenticationRequest);
            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(buffer, 0, pos));
            _sendBuffers.Return(buffer);
        }

        public void HandleMessage(int clientId, ArraySegment<byte> segmentData, int channel)
        {
            try
            {
                var data = segmentData.Array;
                int pos = segmentData.Offset;

                OpCodes opcode = (OpCodes)data.ReadByte(ref pos);

                if (_pendingAuthentication.Contains(clientId))
                {
                    if (opcode == OpCodes.AuthenticationResponse)
                    {
                        string authResponse = data.ReadString(ref pos);
                        if (authResponse == Program.conf.AuthenticationKey)
                        {
                            _pendingAuthentication.Remove(clientId);
                            int writePos = 0;
                            var sendBuffer = _sendBuffers.Rent(1);
                            sendBuffer.WriteByte(ref writePos, (byte)OpCodes.Authenticated);
                            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(sendBuffer, 0, writePos));
                        }
                    }
                    return;
                }

                switch (opcode)
                {
                    case OpCodes.CreateRoom:
                        CreateRoom(clientId, data.ReadInt(ref pos), data.ReadString(ref pos), data.ReadBool(ref pos), data.ReadString(ref pos), data.ReadBool(ref pos), data.ReadString(ref pos), data.ReadBool(ref pos), data.ReadInt(ref pos));
                        break;
                    case OpCodes.RequestID:
                        SendClientID(clientId);
                        break;
                    case OpCodes.LeaveRoom:
                        LeaveRoom(clientId);
                        break;
                    case OpCodes.JoinServer:
                        JoinRoom(clientId, data.ReadInt(ref pos), data.ReadBool(ref pos), data.ReadString(ref pos));
                        break;
                    case OpCodes.KickPlayer:
                        LeaveRoom(data.ReadInt(ref pos), clientId);
                        break;
                    case OpCodes.SendData:
                        ProcessData(clientId, data.ReadBytes(ref pos), channel, data.ReadInt(ref pos));
                        break;
                    case OpCodes.UpdateRoomData:
                        var plyRoom = GetRoomForPlayer(clientId);

                        if (plyRoom == null)
                            return;

                        bool newName = data.ReadBool(ref pos);
                        if (newName)
                            plyRoom.serverName = data.ReadString(ref pos);

                        bool newData = data.ReadBool(ref pos);
                        if (newData)
                            plyRoom.serverData = data.ReadString(ref pos);

                        bool newPublicStatus = data.ReadBool(ref pos);
                        if (newPublicStatus)
                            plyRoom.isPublic = data.ReadBool(ref pos);

                        bool newPlayerCap = data.ReadBool(ref pos);
                        if (newPlayerCap)
                            plyRoom.maxPlayers = data.ReadInt(ref pos);

                        break;
                }
            }
            catch
            {
                // Do Nothing. Client probably sent some invalid data.
            }
        }

        public void HandleDisconnect(int clientId) => LeaveRoom(clientId);

        void ProcessData(int clientId, byte[] clientData, int channel, int sendTo = -1)
        {
            Room playersRoom = GetRoomForPlayer(clientId);

            if(playersRoom != null)
            {
                Room room = playersRoom;

                if(room.hostId == clientId)
                {
                    if (room.clients.Contains(sendTo))
                    {
                        int pos = 0;
                        byte[] sendBuffer = _sendBuffers.Rent(_maxPacketSize);

                        sendBuffer.WriteByte(ref pos, (byte)OpCodes.GetData);
                        sendBuffer.WriteBytes(ref pos, clientData);

                        Program.transport.ServerSend(sendTo, channel, new ArraySegment<byte>(sendBuffer, 0, pos));
                        _sendBuffers.Return(sendBuffer);
                    }
                }
                else
                {
                    // We are not the host, so send the data to the host.
                    int pos = 0;
                    byte[] sendBuffer = _sendBuffers.Rent(_maxPacketSize);

                    sendBuffer.WriteByte(ref pos, (byte)OpCodes.GetData);
                    sendBuffer.WriteBytes(ref pos, clientData);
                    sendBuffer.WriteInt(ref pos, clientId);

                    Program.transport.ServerSend(room.hostId, channel, new ArraySegment<byte>(sendBuffer, 0, pos));
                    _sendBuffers.Return(sendBuffer);
                }
            }
        }

        Room GetRoomForPlayer(int clientId)
        {
            for(int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].hostId == clientId)
                    return rooms[i];

                if (rooms[i].clients.Contains(clientId))
                    return rooms[i];
            }

            return null;
        }

        void JoinRoom(int clientId, int serverId, bool canDirectConnect, string localIP)
        {
            LeaveRoom(clientId);

            for(int i = 0; i < rooms.Count; i++)
            {
                if(rooms[i].serverId == serverId)
                {
                    if(rooms[i].clients.Count < rooms[i].maxPlayers)
                    {
                        rooms[i].clients.Add(clientId);

                        int sendJoinPos = 0;
                        byte[] sendJoinBuffer = _sendBuffers.Rent(500);

                        if (canDirectConnect && Program.instance.NATConnections.ContainsKey(clientId))
                        {
                            sendJoinBuffer.WriteByte(ref sendJoinPos, (byte)OpCodes.DirectConnectIP);

                            if (Program.instance.NATConnections[clientId].Address.Equals(rooms[i].hostIP.Address))
                                sendJoinBuffer.WriteString(ref sendJoinPos, rooms[i].hostLocalIP == localIP ? "127.0.0.1" : rooms[i].hostLocalIP);
                            else
                                sendJoinBuffer.WriteString(ref sendJoinPos, rooms[i].hostIP.Address.ToString());

                            sendJoinBuffer.WriteInt(ref sendJoinPos, rooms[i].useNATPunch ? rooms[i].hostIP.Port : rooms[i].port);
                            sendJoinBuffer.WriteBool(ref sendJoinPos, rooms[i].useNATPunch);

                            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(sendJoinBuffer, 0, sendJoinPos));

                            if (rooms[i].useNATPunch)
                            {
                                sendJoinPos = 0;
                                sendJoinBuffer.WriteByte(ref sendJoinPos, (byte)OpCodes.DirectConnectIP);
                                Console.WriteLine(Program.instance.NATConnections[clientId].Address.ToString());
                                sendJoinBuffer.WriteString(ref sendJoinPos, Program.instance.NATConnections[clientId].Address.ToString());
                                sendJoinBuffer.WriteInt(ref sendJoinPos, Program.instance.NATConnections[clientId].Port);
                                sendJoinBuffer.WriteBool(ref sendJoinPos, true);

                                Program.transport.ServerSend(rooms[i].hostId, 0, new ArraySegment<byte>(sendJoinBuffer, 0, sendJoinPos));
                            }

                            _sendBuffers.Return(sendJoinBuffer);

                            return;
                        }
                        else
                        {

                            sendJoinBuffer.WriteByte(ref sendJoinPos, (byte)OpCodes.ServerJoined);
                            sendJoinBuffer.WriteInt(ref sendJoinPos, clientId);

                            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(sendJoinBuffer, 0, sendJoinPos));
                            Program.transport.ServerSend(rooms[i].hostId, 0, new ArraySegment<byte>(sendJoinBuffer, 0, sendJoinPos));
                            _sendBuffers.Return(sendJoinBuffer);
                            return;
                        }
                    }
                }
            }

            // If it got to here, then the server was not found, or full. Tell the client.
            int pos = 0;
            byte[] sendBuffer = _sendBuffers.Rent(1);

            sendBuffer.WriteByte(ref pos, (byte)OpCodes.ServerLeft);

            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(sendBuffer, 0, pos));
            _sendBuffers.Return(sendBuffer);
        }

        void CreateRoom(int clientId, int maxPlayers, string serverName, bool isPublic, string serverData, bool useDirectConnect, string hostLocalIP, bool useNatPunch, int port)
        {
            LeaveRoom(clientId);

            IPEndPoint hostIP = null;
            Program.instance.NATConnections.TryGetValue(clientId, out hostIP);

            Room room = new Room
            {
                hostId = clientId,
                maxPlayers = maxPlayers,
                serverName = serverName,
                isPublic = isPublic,
                serverData = serverData,
                clients = new List<int>(),
                serverId = GetRandomServerID(),
                hostIP = hostIP,
                hostLocalIP = hostLocalIP,
                supportsDirectConnect = hostIP == null ? false : useDirectConnect,
                port = port,
                useNATPunch = useNatPunch
            };

            rooms.Add(room);

            int pos = 0;
            byte[] sendBuffer = _sendBuffers.Rent(5);

            sendBuffer.WriteByte(ref pos, (byte)OpCodes.RoomCreated);
            sendBuffer.WriteInt(ref pos, clientId);

            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(sendBuffer, 0, pos));
            _sendBuffers.Return(sendBuffer);
        }

        void LeaveRoom(int clientId, int requiredHostId = -1)
        {
            for(int i = 0; i < rooms.Count; i++)
            {
                if(rooms[i].hostId == clientId)
                {
                    int pos = 0;
                    byte[] sendBuffer = _sendBuffers.Rent(1);
                    sendBuffer.WriteByte(ref pos, (byte)OpCodes.ServerLeft);

                    for(int x = 0; x < rooms[i].clients.Count; x++)
                        Program.transport.ServerSend(rooms[i].clients[x], 0, new ArraySegment<byte>(sendBuffer, 0, pos));

                    _sendBuffers.Return(sendBuffer);
                    rooms[i].clients.Clear();
                    rooms.RemoveAt(i);
                    return;
                }
                else
                {
                    if (requiredHostId >= 0 && rooms[i].hostId != requiredHostId)
                        continue;

                    if(rooms[i].clients.RemoveAll(x => x == clientId) > 0)
                    {
                        int pos = 0;
                        byte[] sendBuffer = _sendBuffers.Rent(5);

                        sendBuffer.WriteByte(ref pos, (byte)OpCodes.PlayerDisconnected);
                        sendBuffer.WriteInt(ref pos, clientId);

                        Program.transport.ServerSend(rooms[i].hostId, 0, new ArraySegment<byte>(sendBuffer, 0, pos));
                        _sendBuffers.Return(sendBuffer);
                    }
                }
            }
        }

        void SendClientID(int clientId)
        {
            int pos = 0;
            byte[] sendBuffer = _sendBuffers.Rent(5);

            sendBuffer.WriteByte(ref pos, (byte)OpCodes.GetID);
            sendBuffer.WriteInt(ref pos, clientId);

            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(sendBuffer, 0, pos));
            _sendBuffers.Return(sendBuffer);
        }

        int GetRandomServerID()
        {
            Random rand = new Random();
            int temp = rand.Next(int.MinValue, int.MaxValue);

            while (DoesServerIdExist(temp))
                temp = rand.Next(int.MinValue, int.MaxValue);

            return temp;
        }

        bool DoesServerIdExist(int id)
        {
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].serverId == id)
                    return true;

            return false;
        }
    }

    public enum OpCodes
    {
        Default = 0, RequestID = 1, JoinServer = 2, SendData = 3, GetID = 4, ServerJoined = 5, GetData = 6, CreateRoom = 7, ServerLeft = 8, PlayerDisconnected = 9, RoomCreated = 10,
        LeaveRoom = 11, KickPlayer = 12, AuthenticationRequest = 13, AuthenticationResponse = 14, Authenticated = 17, UpdateRoomData = 18, ServerConnectionData = 19, RequestNATConnection = 20,
        DirectConnectIP = 21
    }
}
