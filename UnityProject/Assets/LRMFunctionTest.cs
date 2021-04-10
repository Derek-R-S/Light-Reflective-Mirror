using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LightReflectiveMirror;
using Mirror;

public class LRMFunctionTest : MonoBehaviour
{
    public Text functionDisplay;
    private LightReflectiveMirrorTransport _LRM;
    private bool _serverListUpdated = false;

    void Start()
    {
        _LRM = (LightReflectiveMirrorTransport)Transport.activeTransport;
        _LRM.serverListUpdated.AddListener(ServerListUpdated);
        StartCoroutine(TestLRM());
    }

    void ServerListUpdated() => _serverListUpdated = true;

    IEnumerator TestLRM()
    {
        DisplayText("Waiting for LRM to authenticate...");
        yield return new WaitUntil(() => _LRM.IsAuthenticated());
        DisplayText("<color=lime>Authenticated!</color>");

        DisplayText("Attempting hosting a room...");
        _LRM.serverName = "Default Server Name";
        _LRM.extraServerData = "Default Server Data";
        _LRM.maxServerPlayers = 5;
        _LRM.isPublicServer = true;
        NetworkManager.singleton.StartHost();
        yield return new WaitUntil(() => _LRM.serverId.Length > 4);
        DisplayText($"<color=lime>Room created! ID: {_LRM.serverId}</color>");
        DisplayText("Requesting Server Data Change...");
        _LRM.UpdateRoomName("Updated Server Name");
        _LRM.UpdateRoomData("Updated Server Data");
        _LRM.UpdateRoomPlayerCount(10);
        yield return new WaitForSeconds(1); // Give LRM time to process
        DisplayText("Requesting Server List...");
        _LRM.RequestServerList();
        yield return new WaitUntil(() => _serverListUpdated);
        foreach (var server in _LRM.relayServerList)
            DisplayText($"Got Server: {server.serverName}, {server.serverData}, {server.maxPlayers}");
    }

    void DisplayText(string msg)
    {
        functionDisplay.text += $"\n{msg}";
    }
}
