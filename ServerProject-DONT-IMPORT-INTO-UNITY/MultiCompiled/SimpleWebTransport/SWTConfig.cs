using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace Mirror
{
    class SWTConfig
    {
        public int maxMessageSize = 16 * 1024;

        public int handshakeMaxSize = 3000;

        public bool noDelay = true;

        public int sendTimeout = 5000;

        public int receiveTimeout = 20000;

        public int serverMaxMessagesPerTick = 10000;

        public bool waitBeforeSend = false;


        public bool clientUseWss;

        public bool sslEnabled;

        public string sslCertJson = "./cert.json";
        public SslProtocols sslProtocols = SslProtocols.Tls12;
    }
}
