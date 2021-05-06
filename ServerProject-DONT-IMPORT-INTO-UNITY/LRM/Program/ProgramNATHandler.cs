using System;
using System.Net;

namespace LightReflectiveMirror
{
    partial class Program
    {
        void RunNATPunchLoop()
        {
            WriteLogMessage("OK\n", ConsoleColor.Green);
            IPEndPoint remoteEndpoint = new(IPAddress.Any, conf.NATPunchtroughPort);

            // Stock Data server sends to everyone:
            var serverResponse = new byte[1] { 1 };

            byte[] readData;
            bool isConnectionEstablished;
            int pos;
            string connectionID;

            while (true)
            {
                readData = _punchServer.Receive(ref remoteEndpoint);
                pos = 0;
                try
                {
                    isConnectionEstablished = readData.ReadBool(ref pos);

                    if (isConnectionEstablished)
                    {
                        connectionID = readData.ReadString(ref pos);

                        if (_pendingNATPunches.TryGetBySecond(connectionID, out pos))
                        {
                            NATConnections.Add(pos, new IPEndPoint(remoteEndpoint.Address, remoteEndpoint.Port));
                            _pendingNATPunches.Remove(pos);
                            Console.WriteLine("Client Successfully Established Puncher Connection. " + remoteEndpoint.ToString());
                        }
                    }

                    _punchServer.Send(serverResponse, 1, remoteEndpoint);
                }
                catch
                {
                    // ignore, packet got fucked up or something.
                }
            }
        }
    }
}
