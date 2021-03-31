using System;
using System.Collections.Generic;
using System.Text;

namespace LightReflectiveMirror
{
    public class Room
    {
        public int serverId;
        public int hostId;
        public string serverName;
        public string serverData;
        public bool isPublic;
        public int maxPlayers;
        public List<int> clients;
    }
}
