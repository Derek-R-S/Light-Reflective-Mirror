﻿using System.Collections.Generic;
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
        
        
        public string GenerateServerID(string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
        {
            var randomID = "";
            var random = _cachedRandom;
            var length = Program.conf.RandomlyGeneratedIDLength;

            do
            {
                randomID = new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
            }
            while (cachedRooms.ContainsKey(randomID));

            return randomID;
        }
    }
}
