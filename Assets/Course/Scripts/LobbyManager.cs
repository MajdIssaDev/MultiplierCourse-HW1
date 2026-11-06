using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;

// Homework 1: the Photon callbacks mutate the model and notify the UI.
// No Update, periodic room queries, or frame-based player-list refreshes.
public sealed class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public enum LobbyState { Offline, JoiningLobby, InLobby, JoiningRoom, InRoom, Leaving }
    [SerializeField] private NetworkRunner networkRunner;
    [SerializeField] private string sessionName = "OurGameID";
    [SerializeField] private string region = "eu";
    public string LobbyName { get; private set; } = "Homework1";
    public string Region => region;
    public string RoomName { get; private set; } = "";
    public int MaxPlayers { get; private set; }
    public int LocalPlayerId { get; private set; }
    public string ClientLabel { get; private set; } = "Game client";
    public string DefaultRoom => sessionName;
    public string PlayerName { get; private set; } = "Player";
    public readonly Dictionary<int, string> PlayerNames = new Dictionary<int, string>();
    private readonly HashSet<int> namesSent = new HashSet<int>();
    private static readonly ReliableKey NameMessage = ReliableKey.FromInts(0x4857314E, 1, 0, 0);
    private static readonly UTF8Encoding NameEncoding = new UTF8Encoding(false, true);
    // Session (room) property that lists the player names, so the lobby can show who is in each room.
    private const string NamesKey = "names";
    private string publishedNames = "";
    public LobbyState State { get; private set; }
    public string Status { get; private set; } = "Choose a lobby name to connect.";
    public readonly List<SessionInfo> Sessions = new List<SessionInfo>();
    public readonly SortedSet<int> Players = new SortedSet<int>();
    public readonly List<string> Events = new List<string>();
    public event Action Changed;
    private CancellationTokenSource cancellation;
    private int operation;

    private void Awake()
    {
        Application.runInBackground = true;
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "-room") sessionName = args[++i];
            else if (args[i] == "-region") region = args[++i];
            else if (args[i] == "-lobby") LobbyName = args[++i];
            else if (args[i] == "-client") ClientLabel = args[++i];
            else if (args[i] == "-name") PlayerName = CleanName(args[++i]);
        }
        gameObject.AddComponent<HomeworkLobbyView>().Bind(this);
    }

    private void Publish(string message = null)
    {
        if (message != null)
        {
            Status = message;
            Events.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
            if (Events.Count > 6) Events.RemoveAt(0);
            Debug.Log("[Homework1] " + message + " | Client=" + ClientLabel +
                " | Lobby=" + LobbyName + " | Room=" + RoomName +
                " | Players=" + string.Join(",", Players));
        }
        Changed?.Invoke();
    }

    private bool Busy => State == LobbyState.JoiningLobby || State == LobbyState.JoiningRoom || State == LobbyState.Leaving;

    private static string CleanName(string value)
    {
        var name = new string((value ?? "").Where(c => !char.IsControl(c)).Take(24).ToArray()).Trim();
        if (name.Length > 0 && char.IsHighSurrogate(name[name.Length - 1])) name = name.Substring(0, name.Length - 1);
        return name;
    }

    public void SetPlayerName(string value)
    {
        if (State != LobbyState.Offline && State != LobbyState.InLobby) return;
        PlayerName = CleanName(value);
    }

    public string DisplayName(int id) => PlayerNames.TryGetValue(id, out var name) ? name : "Player " + id;

    // Names of the players inside a room, as published by that room's master client.
    public static string RoomPlayerNames(SessionInfo session)
    {
        if (session == null || session.Properties == null) return "";
        return session.Properties.TryGetValue(NamesKey, out var value) && value.IsString ? (string)value : "";
    }

    // Only the Shared Mode master client may change room properties. It republishes the
    // name list whenever a player joins, leaves or sends their name (callback driven, no Update).
    private void PublishRoomNames(NetworkRunner runner)
    {
        if (runner == null || !runner.IsRunning || !runner.IsSharedModeMasterClient || runner.SessionInfo == null) return;
        var names = string.Join(", ", Players.Select(DisplayName));
        if (names.Length > 80) names = names.Substring(0, 77) + "...";
        if (names == publishedNames) return;
        publishedNames = names;
        try { runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty> { { NamesKey, names } }); }
        catch (Exception e) { Debug.LogWarning("[Homework1] Could not publish room names: " + e.Message); }
    }

    // Every peer sends only its own name, both to existing peers and late joiners.
    // In Shared mode the PlayerRef passed to OnReliableDataReceived is not the sender,
    // so the payload starts with the sender's PlayerId (4 bytes) followed by the UTF-8 name.
    private void SendName(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsRunning || player == runner.LocalPlayer || namesSent.Contains(player.PlayerId)) return;
        var nameBytes = NameEncoding.GetBytes(PlayerName);
        var payload = new byte[4 + nameBytes.Length];
        BitConverter.GetBytes(runner.LocalPlayer.PlayerId).CopyTo(payload, 0);
        nameBytes.CopyTo(payload, 4);
        runner.SendReliableDataToPlayer(player, NameMessage, payload);
        namesSent.Add(player.PlayerId);
    }

    public async void JoinLobby(string name)
    {
        if (State != LobbyState.Offline) return;
        name = (name ?? "").Trim();
        if (name.Length == 0 || name.Length > 64) { Publish("Enter a lobby name (1-64 characters)."); return; }
        if (!PhotonAppSettings.TryGetGlobal(out var settings) ||
            !Guid.TryParse(settings.AppSettings.AppIdFusion, out var id) || id == Guid.Empty)
        { Publish("Fusion App ID is missing. Configure it in Fusion Hub."); return; }

        LobbyName = name;
        int ticket = ++operation;
        cancellation?.Dispose();
        cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        State = LobbyState.JoiningLobby;
        Sessions.Clear(); Players.Clear();
        Publish("Joining lobby " + name + "...");
        try
        {
            if (networkRunner == null) networkRunner = new GameObject("NetworkRunner").AddComponent<NetworkRunner>();
            var runner = networkRunner;
            runner.AddCallbacks(this);
            var app = settings.AppSettings.GetCopy();
            app.FixedRegion = region;
            app.AppVersion = "multiplayer-course-homework-1";
            var result = await runner.JoinSessionLobby(SessionLobby.Custom, name,
                customAppSettings: app, cancellationToken: token);
            if (ticket != operation) return;
            if (!result.Ok) { await Fail("Lobby join failed: " + result.ShutdownReason); return; }
            State = LobbyState.InLobby;
            Publish("Lobby joined successfully. Waiting for room updates.");
        }
        catch (Exception e) { if (ticket == operation) await Fail("Lobby join failed: " + e.Message); }
    }

    public async void EnterRoom(string name, int capacity, bool create)
    {
        if (State != LobbyState.InLobby) return;
        if (string.IsNullOrWhiteSpace(PlayerName)) { Publish("Enter your player name first (up to 24 characters)."); return; }
        name = (name ?? "").Trim();
        if (name.Length == 0 || name.Length > 64) { Publish("Enter a room name (1-64 characters)."); return; }
        if (create && (capacity < 2 || capacity > 10)) { Publish("Maximum players must be from 2 to 10."); return; }
        if (create && Sessions.Any(s => s.Name == name)) { Publish("That room already exists. Select it in Available rooms."); return; }
        int sceneIndex = gameObject.scene.buildIndex;
        if (sceneIndex < 0) { Publish("Add the Lobby scene to Build Settings before joining a room."); return; }
        int ticket = ++operation;
        State = LobbyState.JoiningRoom;
        RoomName = name;
        MaxPlayers = capacity;
        Players.Clear();
        PlayerNames.Clear(); namesSent.Clear();
        Publish((create ? "Creating " : "Joining ") + name + "...");
        var runner = networkRunner;
        try
        {
            // A runner is recreated after leaving. Retrieve its scene manager once per connection,
            // adding one if needed so every runner has an explicit scene-management implementation.
            if (!runner.TryGetComponent<NetworkSceneManagerDefault>(out var sceneManager))
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            // Adopt the already loaded Lobby scene instead of loading a duplicate and rebuilding its UI.
            sceneManager.IsSceneTakeOverEnabled = true;
            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Shared,
                Scene = SceneRef.FromIndex(sceneIndex),
                SceneManager = sceneManager,
                SessionName = name,
                CustomLobbyName = LobbyName,
                PlayerCount = capacity,
                EnableClientSessionCreation = create,
                IsOpen = true,
                IsVisible = true,
                SessionProperties = create ? new Dictionary<string, SessionProperty> { { NamesKey, PlayerName } } : null,
                StartGameCancellationToken = cancellation.Token,
                OnGameStarted = OnGameStarted
            });
            if (ticket != operation) return;
            if (!result.Ok) { await Fail("Room join/create failed: " + result.ShutdownReason); return; }
            State = LobbyState.InRoom;
            RoomName = runner.SessionInfo.Name;
            MaxPlayers = runner.SessionInfo.MaxPlayers;
            LocalPlayerId = runner.LocalPlayer.PlayerId;
            // Initial snapshot only on successful connection; subsequent changes are callbacks.
            Players.Clear();
            foreach (var player in runner.ActivePlayers) Players.Add(player.PlayerId);
            PlayerNames[LocalPlayerId] = PlayerName;
            foreach (var player in runner.ActivePlayers) SendName(runner, player);
            publishedNames = "";
            PublishRoomNames(runner);
            Sessions.Clear();
            Publish("Room connected successfully: " + RoomName + ".");
        }
        catch (Exception e) { if (ticket == operation) await Fail("Room join/create failed: " + e.Message); }
    }

    private void OnGameStarted(NetworkRunner runner)
    {
        if (runner != networkRunner || (State != LobbyState.JoiningRoom && State != LobbyState.InRoom)) return;
        Publish("Photon OnGameStarted: session is ready.");
    }

    private async Task ReleaseRunner()
    {
        var runner = networkRunner;
        networkRunner = null;
        PlayerNames.Clear(); namesSent.Clear();
        if (runner == null) return;
        runner.RemoveCallbacks(this);
        try { await runner.Shutdown(); }
        finally { if (runner != null) Destroy(runner.gameObject); }
    }

    private async Task Fail(string message)
    {
        State = LobbyState.Leaving;
        Publish(message);
        try { await ReleaseRunner(); }
        catch (Exception e) { Debug.LogWarning("[Homework1] Cleanup: " + e.Message); }
        Sessions.Clear(); Players.Clear(); RoomName = "";
        State = LobbyState.Offline;
        Publish(message + ". Join the lobby to retry.");
    }

    public async void LeaveOrCancel()
    {
        if (State == LobbyState.Offline || State == LobbyState.Leaving) return;
        bool returnToLobby = State == LobbyState.InRoom;
        bool wasBusy = Busy;
        ++operation; // Late completions and callbacks cannot restore the cancelled state.
        State = LobbyState.Leaving;
        Publish(wasBusy ? "Cancelling connection..." : "Leaving...");
        if (wasBusy) cancellation?.Cancel();
        try { await ReleaseRunner(); }
        catch (Exception e) { Debug.LogWarning("[Homework1] Leave: " + e.Message); }
        Sessions.Clear(); Players.Clear(); RoomName = "";
        State = LobbyState.Offline;
        Publish(wasBusy ? "Connection cancelled." : "Left successfully.");
        if (returnToLobby) JoinLobby(LobbyName);
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> list)
    {
        if (runner != networkRunner || (State != LobbyState.JoiningLobby && State != LobbyState.InLobby)) return;
        Sessions.Clear();
        Sessions.AddRange(list.Where(s => s.IsVisible).OrderBy(s => s.Name));
        Publish("Photon OnSessionListUpdated: " + Sessions.Count + " room(s).");
    }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner != networkRunner) return;
        Players.Add(player.PlayerId);
        if (player == runner.LocalPlayer) PlayerNames[player.PlayerId] = PlayerName;
        else SendName(runner, player);
        PublishRoomNames(runner);
        Publish("Photon OnPlayerJoined: Player " + player.PlayerId + " joined.");
    }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner != networkRunner) return;
        Players.Remove(player.PlayerId);
        PlayerNames.Remove(player.PlayerId);
        namesSent.Remove(player.PlayerId);
        PublishRoomNames(runner); // also covers a new master client taking over
        Publish("Photon OnPlayerLeft: Player " + player.PlayerId + " left.");
    }
    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        if (runner != networkRunner) return;
        Publish("Photon OnShutdown: " + reason);
        if (Busy) return; // The operation's awaited result handles its failure.
        networkRunner = null;
        Players.Clear(); Sessions.Clear(); RoomName = "";
        PlayerNames.Clear(); namesSent.Clear();
        State = LobbyState.Offline;
        Publish("Disconnected: " + reason + ". Join the lobby to reconnect.");
    }
    public void OnConnectedToServer(NetworkRunner runner) { if (runner == networkRunner) Publish("Photon OnConnectedToServer received."); }
    public void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason reason)
    { if (runner == networkRunner) Publish("Photon OnConnectFailed: " + reason); }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    { if (runner == networkRunner) Publish("Photon OnDisconnectedFromServer: " + reason); }
    public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { request.Accept(); }
    public void OnInput(NetworkRunner r, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner r, PlayerRef player, NetworkInput input) { }
    #pragma warning disable CS0618 // Required legacy member of Fusion 2.1's callback interface.
    public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr message) { }
    #pragma warning restore CS0618
    public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner r, HostMigrationToken token) { }
    public void OnReliableDataReceived(NetworkRunner r, PlayerRef p, ReliableKey key, ReadOnlySpan<byte> data)
    {
        if (r != networkRunner || !key.Equals(NameMessage) || data.Length <= 4 || data.Length > 100) return;
        if (State != LobbyState.JoiningRoom && State != LobbyState.InRoom) return;
        var bytes = data.ToArray();
        int sender = BitConverter.ToInt32(bytes, 0);
        if (sender == r.LocalPlayer.PlayerId || sender <= 0) return;
        string name;
        try { name = CleanName(NameEncoding.GetString(bytes, 4, bytes.Length - 4)); }
        catch (DecoderFallbackException) { return; }
        if (name.Length == 0) return;
        if (PlayerNames.TryGetValue(sender, out var previous) && previous == name) return;
        PlayerNames[sender] = name;
        PublishRoomNames(r);
        Publish("Player name received: " + name + " (Player " + sender + ").");
    }
    public void OnReliableDataProgress(NetworkRunner r, PlayerRef p, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner r) { }
    public void OnSceneLoadStart(NetworkRunner r) { }
    public void OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef p) { }
    public void OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef p) { }
    private void OnDestroy() { cancellation?.Cancel(); cancellation?.Dispose(); }
}
