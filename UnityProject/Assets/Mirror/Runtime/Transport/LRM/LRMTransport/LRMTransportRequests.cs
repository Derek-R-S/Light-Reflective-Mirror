using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace LightReflectiveMirror
{
    public partial class LightReflectiveMirrorTransport : Transport
    {
        public static System.Func<string, System.Type, object> OnDeserializeJson;

        public void RequestServerList(LRMRegions searchRegion = LRMRegions.Any)
        {
            if (_isAuthenticated && _connectedToRelay)
                StartCoroutine(GetServerList(searchRegion));
            else
                Debug.Log("You must be connected to Relay to request server list!");
        }

        IEnumerator RelayConnect()
        {
            string url = $"http://{loadBalancerAddress}:{loadBalancerPort}/api/join/";
            serverStatus = "Waiting for LLB...";
            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                // Request and wait for the desired page.
                webRequest.SetRequestHeader("x-Region", ((int)region).ToString());
                webRequest.SetRequestHeader("Access-Control-Allow-Credentials", "true");
                webRequest.SetRequestHeader("Access-Control-Allow-Headers", "Accept, X-Access-Token, X-Application-Name, X-Request-Sent-Time");
                webRequest.SetRequestHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                webRequest.SetRequestHeader("Access-Control-Allow-Origin", "*");

                yield return webRequest.SendWebRequest();
                var result = webRequest.downloadHandler.text;
#if UNITY_2020_1_OR_NEWER
                switch (webRequest.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                    case UnityWebRequest.Result.DataProcessingError:
                    case UnityWebRequest.Result.ProtocolError:
                        Debug.LogWarning("LRM | Network Error while getting a relay to join from Load Balancer.");
                        break;

                    case UnityWebRequest.Result.Success:
                        var parsedAddress = (RelayAddress)OnDeserializeJson.Invoke(result, typeof(RelayAddress));
                        Connect(parsedAddress.Address, parsedAddress.Port);
                        endpointServerPort = parsedAddress.EndpointPort;
                        break;
                }
#else
                if (webRequest.isNetworkError || webRequest.isHttpError)
                {
                    Debug.LogWarning("LRM | Network Error while getting a relay to join from Load Balancer.");
                }
                else
                {
                    // join here
                    var parsedAddress = (RelayAddress)OnDeserializeJson.Invoke(result,typeof(RelayAddress));
                    Connect(parsedAddress.Address, parsedAddress.Port);
                    endpointServerPort = parsedAddress.EndpointPort;
                }
#endif
            }
        }

        IEnumerator JoinOtherRelayAndMatch(Room? roomValue, string ID)
        {
            var room = new Room();

            // using load balancer, we NEED the server's relay address
            if (roomValue.HasValue)
                room = roomValue.Value;
            else
            {
                _serverListUpdated = false;
                RequestServerList();

                yield return new WaitUntil(() => _serverListUpdated);

                var foundRoom = GetServerForID(ID);

                if (foundRoom.HasValue)
                {
                    room = foundRoom.Value;
                }
                else
                {
                    Debug.LogWarning("LRM | Client tried to join a server that does not exist!");
                    OnClientDisconnected?.Invoke();
                    yield break;
                }
            }

            // Wait for disconnection
            DisconnectFromRelay();

            while (IsAuthenticated())
                yield return new WaitForEndOfFrame();

            endpointServerPort = room.relayInfo.EndpointPort;
            Connect(room.relayInfo.Address, room.relayInfo.Port);

            while (!IsAuthenticated())
                yield return new WaitForEndOfFrame();

            int pos = 0;
            _directConnected = false;
            _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.JoinServer);
            _clientSendBuffer.WriteString(ref pos, room.serverId);
            _clientSendBuffer.WriteBool(ref pos, _directConnectModule != null);

            if (GetLocalIp() == null)
                _clientSendBuffer.WriteString(ref pos, "0.0.0.0");
            else
                _clientSendBuffer.WriteString(ref pos, GetLocalIp());

            _isClient = true;

            clientToServerTransport.ClientSend(0, new System.ArraySegment<byte>(_clientSendBuffer, 0, pos));
        }

        IEnumerator GetServerList(LRMRegions region)
        {
            if (!useLoadBalancer)
            {
                string uri = $"http://{serverIP}:{endpointServerPort}/api/compressed/servers";

                using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
                {
                    webRequest.SetRequestHeader("Access-Control-Allow-Credentials", "true");
                    webRequest.SetRequestHeader("Access-Control-Allow-Headers", "Accept, X-Access-Token, X-Application-Name, X-Request-Sent-Time");
                    webRequest.SetRequestHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    webRequest.SetRequestHeader("Access-Control-Allow-Origin", "*");

                    // Request and wait for the desired page.
                    yield return webRequest.SendWebRequest();
                    var result = webRequest.downloadHandler.text;

#if UNITY_2020_1_OR_NEWER
                    switch (webRequest.result)
                    {
                        case UnityWebRequest.Result.ConnectionError:
                        case UnityWebRequest.Result.DataProcessingError:
                        case UnityWebRequest.Result.ProtocolError:
                            Debug.LogWarning("LRM | Network Error while retreiving the server list!");
                            break;

                        case UnityWebRequest.Result.Success:
                            relayServerList?.Clear();
                            relayServerList = (List<Room>)OnDeserializeJson.Invoke(result.Decompress(), typeof(List<Room>));
                            serverListUpdated?.Invoke();
                            break;
                    }
#else
                    if (webRequest.isNetworkError || webRequest.isHttpError)
                    {
                        Debug.LogWarning("LRM | Network Error while retreiving the server list!");
                    }
                    else
                    {
                        relayServerList?.Clear();
                        relayServerList = (List<Room>)OnDeserializeJson.Invoke(result.Decompress(), typeof(List<Room>));
                        serverListUpdated?.Invoke();
                    }
#endif
                }
            }
            else // get master list from load balancer
            {
                yield return StartCoroutine(RetrieveMasterServerListFromLoadBalancer(region));
            }

        }

        /// <summary>
        /// Gets master list from the LB.
        /// This can be optimized but for now it is it's
        /// own separate method, so i can understand wtf is going on :D
        /// </summary>
        /// <returns></returns>
        IEnumerator RetrieveMasterServerListFromLoadBalancer(LRMRegions region)
        {
            string uri = $"http://{loadBalancerAddress}:{loadBalancerPort}/api/masterlist/";

            using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
            {
                webRequest.SetRequestHeader("x-Region", ((int)region).ToString());
                // Request and wait for the desired page.
                yield return webRequest.SendWebRequest();
                var result = webRequest.downloadHandler.text;

#if UNITY_2020_1_OR_NEWER
                switch (webRequest.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                    case UnityWebRequest.Result.DataProcessingError:
                    case UnityWebRequest.Result.ProtocolError:
                        Debug.LogWarning("LRM | Network Error while retreiving the server list!");
                        break;

                    case UnityWebRequest.Result.Success:
                        relayServerList?.Clear();
                        relayServerList = (List<Room>)OnDeserializeJson.Invoke(result, typeof(List<Room>));
                        serverListUpdated?.Invoke();
                        _serverListUpdated = true;
                        break;
                }
#else
                if (webRequest.isNetworkError || webRequest.isHttpError)
                {
                    Debug.LogWarning("LRM | Network Error while retreiving the server list!");
                }
                else
                {
                    relayServerList?.Clear();
                    relayServerList = (List<Room>)OnDeserializeJson.Invoke(result, typeof(List<Room>));
                    serverListUpdated?.Invoke();
                    _serverListUpdated = true;
                }
#endif
            }
        }
    }
}