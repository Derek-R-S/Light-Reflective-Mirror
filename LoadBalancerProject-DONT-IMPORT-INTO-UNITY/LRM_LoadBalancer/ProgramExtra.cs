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
    }
}
