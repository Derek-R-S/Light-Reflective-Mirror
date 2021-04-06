using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace LightReflectiveMirror.LoadBalancing
{
    // for stats
    [Serializable]
    public struct RelayServerInfo
    {
        public int ConnectedClients;
        public int RoomCount;
        public int PublicRoomCount;
        public TimeSpan Uptime;
    }

    [Serializable]
    internal struct LoadBalancerStats
    {
        public int NodeCount;
        public TimeSpan Uptime;
        public long CCU;
        public long TotalServerCount;
    }

    // container for relay address info
    [JsonObject(MemberSerialization.OptOut)]
    public struct RelayAddress
    {
        public ushort Port;
        public ushort EndpointPort;
        public string Address;
        [JsonIgnore]
        public string EndpointAddress;
    }

    [Serializable]
    public struct Room
    {
        public int serverId;
        public int hostId;
        public string serverName;
        public string serverData;
        public bool isPublic;
        public int maxPlayers;
        public List<int> clients;

        public RelayAddress relayInfo;
    }
}
