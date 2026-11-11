using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] NetworkRunner networkRunner;
    [SerializeField] GameObject sessionPanel;
    [SerializeField] TMP_Text numberOfPlayersText;
    [SerializeField] TMP_InputField lobbyIdInput;
    [SerializeField] TMP_InputField roomNameInput;
    [SerializeField] TMP_Dropdown maxPlayersDropdown;
    [SerializeField] Button joinLobbyButton;
    [SerializeField] Button startSessionButton;
    [SerializeField] Button leaveRoomButton;
    [SerializeField] TMP_Text playerListText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] TMP_Dropdown roomsDropdown;
    [SerializeField] Button joinRoomButton;
    [SerializeField] NetworkRunner networkRunnerPrefab;
    [SerializeField] TMP_InputField playerNameInput;
    [SerializeField] PlayerNamesManager playerNamesManagerPrefab;
    public PlayerNamesManager playerNamesManagerInstance;
    public static LobbyManager Instance;

    private List<PlayerRef> players = new List<PlayerRef>();
    private Dictionary<PlayerRef, string> playerNames = new Dictionary<PlayerRef, string>();

    //start is called once before the first execution of Update after the MonoBehavior is created
    //also it tells the runner to send its event to lobby manager
    private void Start()
    {
        Instance = this;
        networkRunner.AddCallbacks(this);
        RefreshRoomUI();

        joinLobbyButton.interactable = true;
        startSessionButton.interactable = false;
        leaveRoomButton.interactable = false;
        statusText.text = "Choose a lobby to connect.";
        roomsDropdown.ClearOptions();
        roomsDropdown.interactable = false;
        joinRoomButton.interactable = false;
    }

    public async void JoinLobby()
    {
        //this is to join the lobby when the join lobby button is clicked
        //make sure there is a lobby ID entered, if not give feedback to the user
        if (string.IsNullOrWhiteSpace(lobbyIdInput.text))
        {
            statusText.text = "Please enter a lobby ID.";
            Debug.Log(statusText.text);
            return;
        }

        joinLobbyButton.interactable = false;
        startSessionButton.interactable = false;
        leaveRoomButton.interactable = false;
        statusText.text = "Joining lobby...";
        roomsDropdown.interactable = false;
        joinRoomButton.interactable = false;

        StartGameResult result = await networkRunner.JoinSessionLobby(SessionLobby.Custom, lobbyID: lobbyIdInput.text);

        //checks if the join lobby was successful to print message to console
        if (result.Ok)
        {
            Debug.Log("Joined Lobby");
            startSessionButton.interactable = true;
            roomsDropdown.interactable = roomsDropdown.options.Count > 0;
            joinRoomButton.interactable = roomsDropdown.options.Count > 0;
            leaveRoomButton.interactable = true;
            statusText.text = "Joined lobby: " + networkRunner.LobbyInfo.Name;
        }
        else
        {
            Debug.LogError($"Failed to join lobby: {result.ErrorMessage}");
            statusText.text = "Failed to join lobby: " + result.ErrorMessage;
        }
    }

    public async void StartSession()
    {
        //this is to start the session when the start session button is clicked
        if (string.IsNullOrWhiteSpace(playerNameInput.text))
        {
            statusText.text = "Please enter a player name.";
            return;
        }
        //give fedeback to the user if they try to start a session without entering a room name
        if (string.IsNullOrWhiteSpace(roomNameInput.text))
        {
            statusText.text = "Please enter a room name.";
            Debug.Log(statusText.text);
            return;
        }

        //make sure a session is not already running
        if (networkRunner.IsRunning)
        {
            Debug.Log("A session is already running.");
            return;
        }

        //give feedback to the user if they try to start a session without joining a lobby first
        if (!networkRunner.LobbyInfo.IsValid)
        {
            Debug.Log("Join a lobby first.");
            return;
        }

        startSessionButton.interactable = false;
        leaveRoomButton.interactable = false;
        statusText.text = "Starting session...";
        roomsDropdown.interactable = false;
        joinRoomButton.interactable = false;

        StartGameResult result = await networkRunner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Shared,
            SessionName = roomNameInput.text,
            CustomLobbyName = networkRunner.LobbyInfo.Name,
            PlayerCount = maxPlayersDropdown.value + 2,
            OnGameStarted = OnGameStarted
        });

        if (!result.Ok)
        {
            Debug.LogError($"Game failed to start: {result.ErrorMessage}");
            statusText.text = "Failed to start session: " + result.ErrorMessage;
        }
    }

    private void OnGameStarted(NetworkRunner runner)
    {
        Debug.Log("Game Started");
        leaveRoomButton.interactable = true;
        statusText.text = "Joined room: " + runner.SessionInfo.Name;
        //only the master client cretaes the shared manager
        if (runner.IsSharedModeMasterClient)
        {
            runner.Spawn(playerNamesManagerPrefab);
        }
    }

    public void EndSession()
    {
        //this is to shut down the network runner when the leave button is clicked
        //Leave if we aer in a room or lobby
        if (networkRunner.IsRunning || networkRunner.LobbyInfo.IsValid)
        {
            //Disable buttons while leaving to prevent bugs and unexpected behavior.
            joinLobbyButton.interactable = false;
            startSessionButton.interactable = false;
            leaveRoomButton.interactable = false;
            networkRunner.Shutdown();
        }
    }

    private void RefreshRoomUI()
    {
        //first make sure the network runner is running and not shutdown
        if (networkRunner.IsRunning && !networkRunner.IsShutdown)
        {
            //enable/show the session panel and update the text of players to the number of players in session
            sessionPanel.SetActive(true);
            numberOfPlayersText.text = "Players: " + players.Count;

            playerListText.text = "";

            //iterate through the list of players and print their names to the player list text
            foreach (var player in players)
            {
                if (playerNames.ContainsKey(player))
                {
                    playerListText.text += playerNames[player] + "\n";
                }
                else
                {
                    playerListText.text += $"Player {player.PlayerId}\n";
                }
            }
        }
        else
        {
            //if the network runner is not running or is shutdown, hide the session panel
            sessionPanel.SetActive(false);
        }
    }

    //this prints the player number when they join the game
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        bool isLocalPlayer = false; //initialize it as false, we check if the player is local or not below
        if (runner.LocalPlayer == player) //if the player is the local player, we set isLocalPlayer to true
        {
            isLocalPlayer = true;
        }

        Debug.Log($"Player {player.PlayerId} joined. Is Local Player: {isLocalPlayer}");
        //add the player to the list of players if they are not already in it
        if (!players.Contains(player))
        {
            players.Add(player);
        }
        RefreshRoomUI(); //refresh the room UI to update the number of players in the session
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player.PlayerId} left");
        players.Remove(player);
        playerNames.Remove(player);
        RefreshRoomUI();
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"Session list updated. Found {sessionList.Count} sessions");

        List<string> roomNames = new List<string>();

        //iterate through the session list and add the session names to the roomNames list
        foreach (var session in sessionList)
        {
            roomNames.Add(session.Name);
            Debug.Log($"Session Name: {session.Name}, Player Count: {session.PlayerCount}");
        }

        //clear the rooms dropdown and add the room names to it
        roomsDropdown.ClearOptions();
        roomsDropdown.AddOptions(roomNames);

        //enable the rooms dropdown and join room button if there are rooms available and the start session button is interactable
        bool canJoin = startSessionButton.interactable && roomNames.Count > 0;
        roomsDropdown.interactable = canJoin;
        joinRoomButton.interactable = canJoin;
    }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        //this is to clear the player list and update the UI when the network runner is shutdown
        players.Clear();
        playerNames.Clear();
        playerNamesManagerInstance = null;
        playerListText.text = "";
        statusText.text = "Disconnected: " + shutdownReason;
        sessionPanel.SetActive(false);
        joinLobbyButton.interactable = true;
        startSessionButton.interactable = false;
        leaveRoomButton.interactable = false;
        roomsDropdown.ClearOptions();
        roomsDropdown.interactable = false;
        joinRoomButton.interactable = false;

        //create a frseh runner so the player can connect again
        networkRunner = Instantiate(networkRunnerPrefab);
        networkRunner.AddCallbacks(this);
        Debug.Log($"Runner shut down: {shutdownReason}");
    }

    public void JoinSelectedRoom()
    {
        //this is to join the selected room when the join room button is clicked
        if (roomsDropdown.options.Count == 0)
        {
            statusText.text = "No rooms available.";
            return;
        }

        //set the room name input field to the selected room name from the dropdown
        roomNameInput.text = roomsDropdown.options[roomsDropdown.value].text;

        StartSession();
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {   //this is to give feedback to the user when they are disconnected from the server
        statusText.text = "Disconnected from server: " + reason;
        Debug.Log($"Disconnected from server: {reason}");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {   //this is to give feedback to the user when the connection to the server fails
        statusText.text = "Connection failed: " + reason;
        Debug.LogError($"Connection failed: {reason}");
    }

    public void SendPlayerName()
    {   //this is to send the player name to the player names manager when the player enters their name and clicks the send button
        playerNamesManagerInstance.SetNameRPC(playerNameInput.text.Trim());
    }

    public void SetPlayerName(PlayerRef player, string playerName)
    {
        // Store the received name and update the player list.
        playerNames[player] = playerName;
        RefreshRoomUI();
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
    {
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
    }
}
