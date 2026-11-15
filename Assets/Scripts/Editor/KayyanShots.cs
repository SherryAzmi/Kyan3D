using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Starts Play mode for the screenshot helper (ShotDriver) when Temp/kayyan_shots.txt appears.
/// Line 1 of the file is the job (menu, puzzle, note, end), line 2 the output folder.
/// </summary>
[InitializeOnLoad]
public static class KayyanShots
{
    const string Running = "kayyan.shots.running";

    static KayyanShots()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Running, false))
            {
                SessionState.SetBool(Running, false);
                EditorSceneManager.playModeStartScene = null;
            }
        };
    }

    static void Poll()
    {
        // Temp/kayyan_stop.txt: leave Play mode (used if a capture run is interrupted)
        if (File.Exists("Temp/kayyan_stop.txt"))
        {
            File.Delete("Temp/kayyan_stop.txt");
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            SessionState.SetBool(Running, false);
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (SessionState.GetBool(Running, false) || !File.Exists("Temp/kayyan_shots.txt")) return;

        var job = File.ReadAllLines("Temp/kayyan_shots.txt")[0].Trim();
        string scene = job == "menu" || job == "video" ? "Assets/Scenes/MainMenu.unity"
                     : job == "end" ? "Assets/Scenes/Sh/CafeEnding.unity"
                     : "Assets/Scenes/Sh/SplitCamera.unity";
        var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
        if (!asset) { Debug.LogWarning("[Shots] scene not found: " + scene); File.Delete("Temp/kayyan_shots.txt"); return; }

        SessionState.SetBool(Running, true);
        EditorSceneManager.playModeStartScene = asset;
        EditorApplication.isPlaying = true;
    }
}
