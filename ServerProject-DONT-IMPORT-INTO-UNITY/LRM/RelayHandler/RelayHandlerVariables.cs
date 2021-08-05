using System.Buffers;
using System.Collections.Generic;

namespace LightReflectiveMirror
{
    public partial class RelayHandler
    {
        public List<Room> rooms = new();
        private List<int> _pendingAuthentication = new();
        private ArrayPool<byte> _sendBuffers;
        private int _maxPacketSize = 0;
        private Dictionary<int, Room> _cachedClientRooms = new();
        private Dictionary<string, Room> _cachedRooms = new();

        private System.Random _cachedRandom = new();
    }

    public enum OpCodes
    {
        Default = 0, RequestID = 1, JoinServer = 2, SendData = 3, GetID = 4, ServerJoined = 5, GetData = 6, CreateRoom = 7, ServerLeft = 8, PlayerDisconnected = 9, RoomCreated = 10,
        LeaveRoom = 11, KickPlayer = 12, AuthenticationRequest = 13, AuthenticationResponse = 14, Authenticated = 17, UpdateRoomData = 18, ServerConnectionData = 19, RequestNATConnection = 20,
        DirectConnectIP = 21
    }
}
