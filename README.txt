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
