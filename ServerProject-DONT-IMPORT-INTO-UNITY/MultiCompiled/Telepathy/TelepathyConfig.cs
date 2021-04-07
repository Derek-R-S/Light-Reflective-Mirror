using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mirror
{
    class TelepathyConfig
    {
        public bool NoDelay = true;

        public int SendTimeout = 5000;

        public int ReceiveTimeout = 30000;

        public int serverMaxMessageSize = 16 * 1024;

        public int serverMaxReceivesPerTick = 10000;

        public int serverSendQueueLimitPerConnection = 10000;

        public int serverReceiveQueueLimitPerConnection = 10000;
    }
}
