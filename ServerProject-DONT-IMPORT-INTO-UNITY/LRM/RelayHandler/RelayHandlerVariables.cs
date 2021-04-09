using System.Buffers;
using System.Collections.Generic;

namespace LightReflectiveMirror
{
    public partial class RelayHandler
    {
        public List<Room> rooms = new List<Room>();
        private List<int> _pendingAuthentication = new List<int>();
        private ArrayPool<byte> _sendBuffers;
        private int _maxPacketSize = 0;
        private Dictionary<int, Room> _cachedClientRooms = new Dictionary<int, Room>();
    }
}
