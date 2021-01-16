using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace LightReflectiveMirror
{
    public class RelayHandler
    {
        List<Room> rooms = new List<Room>();
        List<int> pendingAuthentication = new List<int>();
        ArrayPool<byte> sendBuffers;
        int maxPacketSize = 0;

        public RelayHandler(int maxPacketSize)
        {
            this.maxPacketSize = maxPacketSize;
            sendBuffers = ArrayPool<byte>.Create(maxPacketSize, 50);
        }

        public void ClientConnected(int clientId)
        {
            pendingAuthentication.Add(clientId);
            var buffer = sendBuffers.Rent(1);
            int pos = 0;
            buffer.WriteByte(ref pos, (byte)OpCodes.AuthenticationRequest);
            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(buffer, 0, pos));
            sendBuffers.Return(buffer);
        }

        public void HandleMessage(int clientId, ArraySegment<byte> segmentData, int channel)
        {
            try
            {
                var data = segmentData.Array;
                int pos = 0;

                OpCodes opcode = (OpCodes)data.ReadByte(ref pos);

                if (pendingAuthentication.Contains(clientId))
                {
                    if (opcode == OpCodes.AuthenticationResponse)
                    {
                        string authResponse = data.ReadString(ref pos);
                        if (authResponse == Program.conf.AuthenticationKey)
                        {
                            pendingAuthentication.Remove(clientId);
                            int writePos = 0;
                            var sendBuffer = sendBuffers.Rent(1);
                            sendBuffer.WriteByte(ref writePos, (byte)OpCodes.Authenticated);
                            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(sendBuffer, 0, writePos));
                        }
                    }
                    return;
                }

                switch (opcode)
                {
                    case OpCodes.CreateRoom:
                        CreateRoom(clientId, data.ReadInt(ref pos), data.ReadString(ref pos), data.ReadBool(ref pos), data.ReadString(ref pos));
                        break;
                    case OpCodes.RequestID:
                        SendClientID(clientId);
                        break;
                    case OpCodes.LeaveRoom:
                        LeaveRoom(clientId);
                        break;
                    case OpCodes.JoinServer:
                        JoinRoom(clientId, data.ReadInt(ref pos));
                        break;
                    case OpCodes.KickPlayer:
                        LeaveRoom(data.ReadInt(ref pos), clientId);
                        break;
                    case OpCodes.SendData:
                        ProcessData(clientId, data.ReadBytes(ref pos), channel, data.ReadInt(ref pos));
                        break;
                    case OpCodes.RequestServers:
                        SendServerList(clientId);
                        break;
                    case OpCodes.UpdateRoomData:
                        var room = GetRoomForPlayer(clientId);
                        if (room == null)
                            return;

                        var plyRoom = room.Value;

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

        public void HandleDisconnect(int clientId)
        {
            LeaveRoom(clientId);
        }

        void SendServerList(int clientId)
        {
            int pos = 0;
            var buffer = sendBuffers.Rent(500);
            buffer.WriteByte(ref pos, (byte)OpCodes.ServerListReponse);
            for(int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].isPublic)
                {
                    buffer.WriteBool(ref pos, true);
                    buffer.WriteString(ref pos, rooms[i].serverName);
                    buffer.WriteString(ref pos, rooms[i].serverData);
                    buffer.WriteInt(ref pos, rooms[i].hostId);
                    buffer.WriteInt(ref pos, rooms[i].maxPlayers);
                    buffer.WriteInt(ref pos, rooms[i].clients.Count + 1);
                }
            }
            buffer.WriteBool(ref pos, false);
            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(buffer, 0, pos));
            sendBuffers.Return(buffer);
        }

        void ProcessData(int clientId, byte[] clientData, int channel, int sendTo = -1)
        {
            Room? playersRoom = GetRoomForPlayer(clientId);

            if(playersRoom != null)
            {
                Room room = playersRoom.Value;

                if(room.hostId == clientId)
                {
                    if (room.clients.Contains(sendTo))
                    {
                        int pos = 0;
                        byte[] sendBuffer = sendBuffers.Rent(maxPacketSize);

                        sendBuffer.WriteByte(ref pos, (byte)OpCodes.GetData);
                        sendBuffer.WriteBytes(ref pos, clientData);

                        Program.transport.ServerSend(sendTo, channel, new ArraySegment<byte>(sendBuffer, 0, pos));
                        sendBuffers.Return(sendBuffer);
                    }
                }
                else
                {
                    // We are not the host, so send the data to the host.
                    int pos = 0;
                    byte[] sendBuffer = sendBuffers.Rent(maxPacketSize);

                    sendBuffer.WriteByte(ref pos, (byte)OpCodes.GetData);
                    sendBuffer.WriteBytes(ref pos, clientData);
                    sendBuffer.WriteInt(ref pos, clientId);

                    Program.transport.ServerSend(room.hostId, channel, new ArraySegment<byte>(sendBuffer, 0, pos));
                    sendBuffers.Return(sendBuffer);
                }
            }
        }

        Room? GetRoomForPlayer(int clientId)
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

        void JoinRoom(int clientId, int serverId)
        {
            LeaveRoom(clientId);

            for(int i = 0; i < rooms.Count; i++)
            {
                if(rooms[i].hostId == serverId)
                {
                    if(rooms[i].clients.Count < rooms[i].maxPlayers)
                    {
                        rooms[i].clients.Add(clientId);

                        int sendJoinPos = 0;
                        byte[] sendJoinBuffer = sendBuffers.Rent(5);

                        sendJoinBuffer.WriteByte(ref sendJoinPos, (byte)OpCodes.ServerJoined);
                        sendJoinBuffer.WriteInt(ref sendJoinPos, clientId);

                        Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(sendJoinBuffer, 0, sendJoinPos));
                        Program.transport.ServerSend(serverId, 0, new ArraySegment<byte>(sendJoinBuffer, 0, sendJoinPos));
                        sendBuffers.Return(sendJoinBuffer);
                        return;
                    }
                }
            }

            // If it got to here, then the server was not found, or full. Tell the client.
            int pos = 0;
            byte[] sendBuffer = sendBuffers.Rent(1);

            sendBuffer.WriteByte(ref pos, (byte)OpCodes.ServerLeft);

            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(sendBuffer, 0, pos));
            sendBuffers.Return(sendBuffer);
        }

        void CreateRoom(int clientId, int maxPlayers, string serverName, bool isPublic, string serverData)
        {
            LeaveRoom(clientId);

            Room room = new Room();
            room.hostId = clientId;
            room.maxPlayers = maxPlayers;
            room.serverName = serverName;
            room.isPublic = isPublic;
            room.serverData = serverData;
            room.clients = new List<int>();

            rooms.Add(room);

            int pos = 0;
            byte[] sendBuffer = sendBuffers.Rent(5);

            sendBuffer.WriteByte(ref pos, (byte)OpCodes.RoomCreated);
            sendBuffer.WriteInt(ref pos, clientId);

            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(sendBuffer, 0, pos));
            sendBuffers.Return(sendBuffer);
        }

        void LeaveRoom(int clientId, int requiredHostId = -1)
        {
            for(int i = 0; i < rooms.Count; i++)
            {
                if(rooms[i].hostId == clientId)
                {
                    int pos = 0;
                    byte[] sendBuffer = sendBuffers.Rent(1);
                    sendBuffer.WriteByte(ref pos, (byte)OpCodes.ServerLeft);

                    for(int x = 0; x < rooms[i].clients.Count; x++)
                        Program.transport.ServerSend(rooms[i].clients[x], 0, new ArraySegment<byte>(sendBuffer, 0, pos));

                    sendBuffers.Return(sendBuffer);
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
                        byte[] sendBuffer = sendBuffers.Rent(5);

                        sendBuffer.WriteByte(ref pos, (byte)OpCodes.PlayerDisconnected);
                        sendBuffer.WriteInt(ref pos, clientId);

                        Program.transport.ServerSend(rooms[i].hostId, 0, new ArraySegment<byte>(sendBuffer, 0, pos));
                        sendBuffers.Return(sendBuffer);
                    }
                }
            }
        }

        void SendClientID(int clientId)
        {
            int pos = 0;
            byte[] sendBuffer = sendBuffers.Rent(5);

            sendBuffer.WriteByte(ref pos, (byte)OpCodes.GetID);
            sendBuffer.WriteInt(ref pos, clientId);

            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(sendBuffer, 0, pos));
            sendBuffers.Return(sendBuffer);
        }
    }

    public enum OpCodes
    {
        Default = 0, RequestID = 1, JoinServer = 2, SendData = 3, GetID = 4, ServerJoined = 5, GetData = 6, CreateRoom = 7, ServerLeft = 8, PlayerDisconnected = 9, RoomCreated = 10,
        LeaveRoom = 11, KickPlayer = 12, AuthenticationRequest = 13, AuthenticationResponse = 14, RequestServers = 15, ServerListReponse = 16, Authenticated = 17, UpdateRoomData = 18, ServerConnectionData = 19
    }
}
