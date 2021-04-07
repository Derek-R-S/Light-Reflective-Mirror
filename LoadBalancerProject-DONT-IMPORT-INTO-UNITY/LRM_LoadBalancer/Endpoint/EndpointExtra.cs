using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightReflectiveMirror.LoadBalancing
{
    public partial class Endpoint
    {
        /// <summary>
        /// We can write all server operations in here, 
        /// to make it more clean.
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="onComplete"></param>
        public static void PerformActionToAllServers(LRMServerOpCode operation, Action onComplete = null)
        {
            switch (operation)
            {
                case LRMServerOpCode.Clear:
                    for (int i = 0; i < _allServersToPerformActionOn.Count; i++)
                        _allServersToPerformActionOn[i].Item1.Clear();
                    break;
                 
                // Removes the old cached string and reserialzes the new one
                case LRMServerOpCode.Cache:
                    for (int i = 0; i < _allServersToPerformActionOn.Count; i++)
                    {
                        var tuple = _allServersToPerformActionOn[i];
                        var serializedData = JsonConvert.SerializeObject(_allServersToPerformActionOn[i].Item1);

                        _allServersToPerformActionOn.Remove(tuple);
                        _allServersToPerformActionOn.Add(new Tuple<List<Room>, string>(tuple.Item1, serializedData));
                    }
                    break;
                default:
                    break;
            }


        }
    }
}
