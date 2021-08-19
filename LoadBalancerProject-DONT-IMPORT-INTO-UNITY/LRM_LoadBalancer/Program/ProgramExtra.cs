using System.Collections.Generic;
using System.Linq;

namespace LightReflectiveMirror.LoadBalancing
{
    partial class Program
    { 

        public long GetTotalCCU()
        {
            long temp = 0;

            foreach (var item in availableRelayServers)
                temp += item.Value.connectedClients;

            return temp;
        }

        public long GetTotalServers()
        {
            int temp = 0;

            foreach (var item in availableRelayServers)
                temp += item.Value.roomCount;

            return temp;
        }

        public string GenerateServerID()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var randomID = "";
            var random = _cachedRandom;

            do
            {
                randomID = new string(Enumerable.Repeat(chars, conf.RandomlyGeneratedIDLength)
                                                        .Select(s => s[random.Next(s.Length)]).ToArray());
            }
            while (cachedRooms.ContainsKey(randomID));

            return randomID;
        }
    }
}
