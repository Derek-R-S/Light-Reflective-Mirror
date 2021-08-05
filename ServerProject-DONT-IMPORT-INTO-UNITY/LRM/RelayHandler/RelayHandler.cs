using System;
using System.Buffers;
using System.Linq;

namespace LightReflectiveMirror
{
    public partial class RelayHandler
    {
        // constructor for new relay handler
        public RelayHandler(int maxPacketSize)
        {
            this._maxPacketSize = maxPacketSize;
            _sendBuffers = ArrayPool<byte>.Create(maxPacketSize, 50);
        }

        /// <summary>
        /// Checks if a server id already is in use.
        /// </summary>
        /// <param name="id">The ID to check for</param>
        /// <returns></returns>
        private bool DoesServerIdExist(string id) => _cachedRooms.ContainsKey(id);

        private string GenerateRoomID()
        {
            const int LENGTH = 5;
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var randomID = "";
            var random = _cachedRandom;

            do
            {
                randomID = new string(Enumerable.Repeat(chars, LENGTH)
                                                        .Select(s => s[random.Next(s.Length)]).ToArray());
            }
            while (DoesServerIdExist(randomID));

            return randomID;
        }

        /// <summary>
        /// Generates a random server ID.
        /// </summary>
        /// <returns></returns>
        private string GetRandomServerID()
        {
            if (!Program.conf.UseLoadBalancer)
            {
                return GenerateRoomID();
            }
            else
            {
                // ping load balancer here
                var uri = new Uri($"http://{Program.conf.LoadBalancerAddress}:{Program.conf.LoadBalancerPort}/api/get/id");
                string randomID = Program.webClient.DownloadString(uri).Replace("\\r", "").Replace("\\n", "").Trim();

                return randomID;
            }
        }

        /// <summary>
        /// This is called when a client wants to send data to another player.
        /// </summary>
        /// <param name="clientId">The ID of the client who is sending the data</param>
        /// <param name="clientData">The binary data the client is sending</param>
        /// <param name="channel">The channel the client is sending this data on</param>
        /// <param name="sendTo">Who to relay the data to</param>
        private void ProcessData(int clientId, byte[] clientData, int channel, int sendTo = -1)
        {
            Room room = _cachedClientRooms[clientId];

            if (room != null)
            {
                if (room.hostId == clientId)
                {
                    if (room.clients.Contains(sendTo))
                    {
                        SendData(clientData, channel, sendTo);
                    }
                }
                else
                {
                    SendDataToRoomHost(clientId, clientData, channel, room);
                }
            }
        }

        private void SendData(byte[] clientData, int channel, int sendTo)
        {
            int pos = 0;
            byte[] sendBuffer = _sendBuffers.Rent(_maxPacketSize);

            sendBuffer.WriteByte(ref pos, (byte)OpCodes.GetData);
            sendBuffer.WriteBytes(ref pos, clientData);

            Program.transport.ServerSend(sendTo, channel, new ArraySegment<byte>(sendBuffer, 0, pos));
            _sendBuffers.Return(sendBuffer);
        }

        private void SendDataToRoomHost(int clientId, byte[] clientData, int channel, Room room)
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

        /// <summary>
        /// Called when a client wants to request their own ID.
        /// </summary>
        /// <param name="clientId">The client requesting their ID</param>
        private void SendClientID(int clientId)
        {
            int pos = 0;
            byte[] sendBuffer = _sendBuffers.Rent(5);

            sendBuffer.WriteByte(ref pos, (byte)OpCodes.GetID);
            sendBuffer.WriteInt(ref pos, clientId);

            Program.transport.ServerSend(clientId, 0, new ArraySegment<byte>(sendBuffer, 0, pos));
            _sendBuffers.Return(sendBuffer);
        }
    }
}