using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace LightReflectiveMirror.LoadBalancing
{
    // for stats
    [Serializable]
    public struct RelayServerInfo
    {
        public int connectedClients;
        public int roomCount;
        public int publicRoomCount;
        public TimeSpan uptime;

        [JsonIgnore]
        public List<Room> serversConnectedToRelay;
    }

    [Serializable]
    internal struct LoadBalancerStats
    {
        public int nodeCount;
        public TimeSpan uptime;
        public long CCU;
        public long totalServerCount;
        public List<KeyValuePair<RelayAddress, RelayServerInfo>> connectedNodes;
    }

    // container for relay address info
    [JsonObject(MemberSerialization.OptOut)]
    public struct RelayAddress
    {
        public ushort port;
        public ushort endpointPort;
        public string address;
        public LRMRegions serverRegion;
        [JsonIgnore]
        public string endpointAddress;
    }

    [Serializable]
    [JsonObject(MemberSerialization.OptOut)]
    public struct Room
    {
        public string serverId;
        public int hostId;
        public string serverName;
        public string serverData;
        public bool isPublic;
        public int currentPlayers { get => clients.Count + 1; }
        public int maxPlayers;

        [JsonIgnore]
        public List<int> clients;

        public RelayAddress relayInfo;
    }

    public enum LRMRegions { Any, NorthAmerica, SouthAmerica, Europe, Asia, Africa, Oceania }
}
