using System;

namespace LightReflectiveMirror
{
    public partial class RelayHandler
    {
        /// <summary>
        /// Invoked when a client connects to this LRM server.
        /// </summary>
        /// <param name="clientId">The ID of the client who connected.</param>
        public void ClientConnected(int clientId)
        {
            _pendingAuthentication.Add(clientId);
            var buffer = _sendBuffers.Rent(1);
            int pos = 0;
            buffer.WriteByte(ref pos, (byte)OpCodes.AuthenticationRequest);
            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(buffer, 0, pos));
            _sendBuffers.Return(buffer);
        }

        /// <summary>
        /// Handles the processing of data from a client.
        /// </summary>
        /// <param name="clientId">The client who sent the data</param>
        /// <param name="segmentData">The binary data</param>
        /// <param name="channel">The channel the client sent the data on</param>
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

        /// <summary>
        /// Invoked when a client disconnects from the relay.
        /// </summary>
        /// <param name="clientId">The ID of the client who disconnected</param>
        public void HandleDisconnect(int clientId) => LeaveRoom(clientId);
    }
}
