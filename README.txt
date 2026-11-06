Homework 1 - Lobby & Sessions (Photon Fusion 2, Unity 6000.6.6f1)

Open scene: Assets/Scenes/Lobby.unity  (build scene 0)
Scripts:    Assets/Course/Scripts/LobbyManager.cs      - Fusion logic + all callbacks
            Assets/Course/Scripts/HomeworkLobbyView.cs - UI (refreshed only by events, no Update)
Editor:     Assets/Course/Editor/*  (menu "Multiplayer Course", MPPM config helper)

Requirements:
- Join a specific lobby by name/ID (free text)            -> JoinSessionLobby(SessionLobby.Custom, name)
- UI refreshes whenever the lobby changes                 -> OnSessionListUpdated -> Changed event
- Create room with session name + max players (UI input)  -> StartGame(SessionName, PlayerCount)
- Join a session from the room list (button per session)  + "Join by name"
- Success/failure callbacks for every action               -> StartGameResult.Ok / ShutdownReason,
                                                              OnConnectFailed, OnShutdown, OnDisconnected...
- Player list in room, updated on join/leave (no Update)   -> OnPlayerJoined / OnPlayerLeft
- Buttons enabled/disabled by current state                -> LobbyState in HomeworkLobbyView.Refresh()
Bonus:
- Unity Multiplayer Play Mode scenario: Assets/Settings/PlayMode/NewPlayModeScenario.asset (2 players)
- Player names: "Your name" field, sent to other players with reliable data

Photon Fusion 2.1.4 SDK is included in Assets/Photon, so the project opens and compiles as-is.

Opening and testing:
1. Open the repository root in Unity Hub. The project was tested with Unity 6000.6.6f1.
2. Open Assets/Scenes/Lobby.unity and enter Play Mode.
3. Enter a lobby name and player name, then click Join lobby.
4. Create a room with a chosen name and capacity (2-10 players).
5. In a second client, use the same lobby name, a different player name, and click the room row.
6. Verify player names and counts, then leave and observe the callback-driven list updates.

For a standalone client: Multiplayer Course > Build Windows Test Client.
For the Multiplayer Play Mode bonus: use Assets/Settings/PlayMode/NewPlayModeScenario.asset.
Photon Fusion SDK and configured project settings are included in Assets/Photon.
The runtime menu creates and caches its UI components once; comments explain the remaining lookups.

Validation performed:
- Unity import and compilation, including a fresh extraction of the submission project.
- Final Windows build after source/settings cleanup: zero errors and zero warnings.
- Three standalone clients: room discovery, room-list joining, synchronized names and counts,
  player leave updates, full-room rejection, missing-room rejection, and custom room capacity.
- Room removal from the lobby when its last player leaves.

Submission branch: Assignment-1 (Git branch names cannot contain spaces).

AI assistance: AI-assisted code review, live testing, and final code/settings cleanup were used.
