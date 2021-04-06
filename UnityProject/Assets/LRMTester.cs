using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;
using Newtonsoft.Json;
using LightReflectiveMirror;

public class LRMTester : MonoBehaviour
{
    public Transform serverListParent;
    public GameObject serverListEntry;

    private LightReflectiveMirrorTransport _LRM;

    void Start()
    {
        if (_LRM == null)
        {
            _LRM = (LightReflectiveMirrorTransport)Transport.activeTransport;
            _LRM.serverListUpdated.AddListener(OnServerListUpdated);
        }
    }

    void OnDisable()
    {
        if (_LRM != null)
            _LRM.serverListUpdated.RemoveListener(OnServerListUpdated);
    }

    public void RequestServerList()
    {
        _LRM.RequestServerList();
    }

    public void OnServerListUpdated()
    {
        foreach (Transform t in serverListParent)
            Destroy(t.gameObject);

        for(int i = 0; i < _LRM.relayServerList.Count; i++)
        {
            var serverEntry = Instantiate(serverListEntry, serverListParent);

            serverEntry.transform.GetChild(0).GetComponent<Text>().text = $"{_LRM.relayServerList[i].serverName + " - " + JsonConvert.SerializeObject(_LRM.relayServerList[i].relayInfo)}";
            int serverID = _LRM.relayServerList[i].serverId;
            serverEntry.GetComponent<Button>().onClick.AddListener(() => { NetworkManager.singleton.networkAddress = serverID.ToString(); NetworkManager.singleton.StartClient(); });
        }
    }
}
