using System;
using System.IO;
using Fusion;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CourseProjectSetup
{
    public const string LobbyPath = "Assets/Scenes/Lobby.unity";

    [MenuItem("Multiplayer Course/Create Lobby Scene")]
    public static void CreateLobby()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(LobbyPath))
        {
            EditorSceneManager.OpenScene(LobbyPath);
            Debug.Log("[Course Setup] Opened existing Lobby scene.");
            return;
        }
        EditorSettings.serializationMode = SerializationMode.ForceText;
        // Use normal domain reload for repeatable networking tests in Play mode.
        EditorSettings.enterPlayModeOptionsEnabled = false;
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultScreenWidth = 1100;
        PlayerSettings.defaultScreenHeight = 700;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        // Editor-only, one-time scene creation: Unity supplies a camera tagged MainCamera in this new scene.
        // Camera.main retrieves that generated camera; it is never searched for during gameplay.
        var camera = Camera.main;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.06f, 0.10f);
        var runnerObject = new GameObject("NetworkRunner");
        var runner = runnerObject.AddComponent<NetworkRunner>();
        runnerObject.AddComponent<NetworkSceneManagerDefault>();
        var lobby = new GameObject("LobbyManager").AddComponent<LobbyManager>();
        var serializedLobby = new SerializedObject(lobby);
        serializedLobby.FindProperty("networkRunner").objectReferenceValue = runner;
        serializedLobby.ApplyModifiedPropertiesWithoutUndo();
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, LobbyPath);
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
            { new EditorBuildSettingsScene(LobbyPath, true) };
        foreach (var existing in EditorBuildSettings.scenes)
            if (existing.path != LobbyPath && File.Exists(existing.path)) scenes.Add(existing);
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = lobby.gameObject;
        Debug.Log("[Course Setup] Lobby saved with NetworkRunner, scene manager and StartGame callback. Lobby is build scene 0.");
    }

    [MenuItem("Multiplayer Course/Prepare Multiplayer Play Mode")]
    public static void PrepareMultiplayerPlayMode()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        NetworkProjectConfig.UnloadGlobal();
        AssetDatabase.ImportAsset("Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion",
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.SaveAssets();
        var config = AssetDatabase.LoadAssetAtPath<NetworkProjectConfigAsset>("Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion");
        if (config == null) throw new InvalidOperationException("The original Fusion configuration could not be imported.");
        const string copyPath = "Assets/Course/Resources/HomeworkNetworkProjectConfig.asset";
        Directory.CreateDirectory("Assets/Course/Resources");
        var copy = AssetDatabase.LoadAssetAtPath<NetworkProjectConfigAsset>(copyPath);
        if (copy == null)
        {
            copy = UnityEngine.Object.Instantiate(config);
            AssetDatabase.CreateAsset(copy, copyPath);
        }
        else EditorUtility.CopySerialized(config, copy);
        copy.name = "HomeworkNetworkProjectConfig";
        AssetDatabase.SetLabels(copy, Array.Empty<string>());
        EditorUtility.SetDirty(copy);
        AssetDatabase.SaveAssets();
        Debug.Log("[Homework1 Bonus] Exported the current Fusion configuration as a standard Resource for Multiplayer Play Mode. Max players=" + config.Config.Simulation.PlayerCount);
    }

    [MenuItem("Multiplayer Course/Build Windows Test Client")]
    public static void BuildClient()
    {
        PrepareMultiplayerPlayMode();
        string output = Path.GetFullPath("Builds/Windows/MultiplayerCourse.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { LobbyPath },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        Debug.Log("[Course Build] " + report.summary.result + " | Errors=" + report.summary.totalErrors +
                  " | Warnings=" + report.summary.totalWarnings + " | " + output);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed.");
    }
}

