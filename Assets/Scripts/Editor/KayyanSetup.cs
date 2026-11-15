using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the MainMenu scene (a camera and the MainMenu component, which builds the whole menu at runtime)
/// and puts it first in the Build Settings. Runs by itself once after scripts compile; also available as
/// Tools > Kayyan > Build Main Menu Scene.
/// </summary>
[InitializeOnLoad]
public static class KayyanSetup
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";

    static KayyanSetup() => EditorApplication.delayCall += Ensure;

    [MenuItem("Tools/Kayyan/Build Main Menu Scene")]
    static void MenuItem()
    {
        Build();
        EnsureBuildSettings();
        Debug.Log("[Kayyan] Main menu scene built: " + ScenePath);
    }

    static void Ensure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!File.Exists(ScenePath)) Build();
        EnsureBuildSettings();
    }

    static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        var c = cam.GetComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = Color.black;
        SceneManager.MoveGameObjectToScene(cam, scene);

        var menu = new GameObject("Main Menu", typeof(MainMenu));
        SceneManager.MoveGameObjectToScene(menu, scene);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.Refresh();
    }

    static void EnsureBuildSettings()
    {
        if (!File.Exists(ScenePath)) return;
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Count > 0 && list[0].path == ScenePath && list[0].enabled) return;
        list.RemoveAll(s => s.path == ScenePath);
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
