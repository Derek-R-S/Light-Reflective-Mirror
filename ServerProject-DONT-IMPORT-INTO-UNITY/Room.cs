using System;
using System.Collections.Generic;
using System.Text;

namespace LightReflectiveMirror
{
    class Room
    {
        public int hostId;
        public string serverName;
        public string serverData;
        public bool isPublic;
        public int maxPlayers;
        public List<int> clients;
    }
}
