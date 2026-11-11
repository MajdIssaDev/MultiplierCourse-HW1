using Fusion;
using UnityEngine;

public class PlayerNamesManager : NetworkBehaviour
{   //this class is for managing player names in the game
    public override void Spawned()
    {
        base.Spawned();

        // Store the local reference when this network object appears.
        LobbyManager.Instance.playerNamesManagerInstance = this;

        Debug.Log("Player names manager spawned.");
        RequestNamesRPC();
    }

    [Rpc]
    public void RequestNamesRPC()
    {
        // Ask each player to send their own name.
        LobbyManager.Instance.SendPlayerName();
    }

    [Rpc]
    public void SetNameRPC(string playerName, RpcInfo info = default)
    {
        // Use the sender's identity to store the correct name.
        LobbyManager.Instance.SetPlayerName(info.Source, playerName);
    }
}