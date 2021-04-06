using System;
using System.Buffers;

namespace LightReflectiveMirror
{
    public partial class RelayHandler
    {

        public RelayHandler(int maxPacketSize)
        {
            this._maxPacketSize = maxPacketSize;
            _sendBuffers = ArrayPool<byte>.Create(maxPacketSize, 50);
        }

        /// <summary>
        /// This is called when a client wants to send data to another player.
        /// </summary>
        /// <param name="clientId">The ID of the client who is sending the data</param>
        /// <param name="clientData">The binary data the client is sending</param>
        /// <param name="channel">The channel the client is sending this data on</param>
        /// <param name="sendTo">Who to relay the data to</param>
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


        /// <summary>
        /// Called when a client wants to request their own ID.
        /// </summary>
        /// <param name="clientId">The client requesting their ID</param>
        void SendClientID(int clientId)
        {
            int pos = 0;
            byte[] sendBuffer = _sendBuffers.Rent(5);

            sendBuffer.WriteByte(ref pos, (byte)OpCodes.GetID);
            sendBuffer.WriteInt(ref pos, clientId);

            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(sendBuffer, 0, pos));
            _sendBuffers.Return(sendBuffer);
        }

        /// <summary>
        /// Generates a random server ID.
        /// </summary>
        /// <returns></returns>
        int GetRandomServerID()
        {
            Random rand = new Random();
            int temp = rand.Next(int.MinValue, int.MaxValue);

            while (DoesServerIdExist(temp))
                temp = rand.Next(int.MinValue, int.MaxValue);

            return temp;
        }

        /// <summary>
        /// Checks if a server id already is in use.
        /// </summary>
        /// <param name="id">The ID to check for</param>
        /// <returns></returns>
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
