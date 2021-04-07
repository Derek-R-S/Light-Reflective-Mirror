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
                temp += item.Value.ConnectedClients;

            return temp;
        }

        public long GetTotalServers()
        {
            int temp = 0;

            foreach (var item in availableRelayServers)
                temp += item.Value.RoomCount;

            return temp;
        }

        public string GenerateServerID()
        {
            const int LENGTH = 5;
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var randomID = "";

            do
            {
                var random = new System.Random();
                randomID = new string(Enumerable.Repeat(chars, LENGTH)
                                                        .Select(s => s[random.Next(s.Length)]).ToArray());
            }
            while (DoesServerIdExist(randomID));

            return randomID;
        }

        /// <summary>
        /// Checks if a server id already is in use.
        /// </summary>
        /// <param name="id">The ID to check for</param>
        /// <returns></returns>
        bool DoesServerIdExist(string id)
        { 
            var infos = new List<RelayServerInfo>(availableRelayServers.Values.ToList());

            foreach (var info in infos)
            {
                foreach (var server in info.serversConnectedToRelay)
                {
                    if (server.serverId == id)
                        return true;
                }
            }

            return false;
        }
    }
}
