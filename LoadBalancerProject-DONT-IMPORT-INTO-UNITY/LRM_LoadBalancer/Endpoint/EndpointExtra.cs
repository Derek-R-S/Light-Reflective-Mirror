using Newtonsoft.Json;

namespace LightReflectiveMirror.LoadBalancing
{
    public partial class Endpoint
    {
        void CacheAllServers()
        {
            allCachedServers = JsonConvert.SerializeObject(_allServers);
            NorthAmericaCachedServers = JsonConvert.SerializeObject(_northAmericaServers);
            SouthAmericaCachedServers = JsonConvert.SerializeObject(_southAmericaServers);
            EuropeCachedServers = JsonConvert.SerializeObject(_europeServers);
            AsiaCachedServers = JsonConvert.SerializeObject(_asiaServers);
            AfricaCachedServers = JsonConvert.SerializeObject(_africaServers);
            OceaniaCachedServers = JsonConvert.SerializeObject(_oceaniaServers);
        }

        void ClearAllServersLists()
        {
            _northAmericaServers.Clear();
            _southAmericaServers.Clear();
            _europeServers.Clear();
            _asiaServers.Clear();
            _africaServers.Clear();
            _oceaniaServers.Clear();
            _allServers.Clear();
        }
    }
}
