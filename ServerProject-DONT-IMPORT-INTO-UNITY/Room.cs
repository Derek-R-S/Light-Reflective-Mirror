﻿using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net;

namespace LightReflectiveMirror
{
    [JsonObject(MemberSerialization.OptOut)]
    public class Room
    {
        public int serverId;
        public int hostId;
        public string serverName;
        public string serverData;
        public bool isPublic;
        public int maxPlayers;
        public List<int> clients;
        [JsonIgnore]
        public bool supportsDirectConnect = false;
        [JsonIgnore]
        public IPEndPoint hostIP;
        [JsonIgnore]
        public string hostLocalIP;
        [JsonIgnore]
        public bool useNATPunch = false;
        [JsonIgnore]
        public int port;
    }
}
