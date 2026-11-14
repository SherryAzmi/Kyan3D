using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Kyan > Setup Audio (all scenes) — generates the sounds if needed, then wires them into
/// Cutscene, SplitCamera and Puzzle3 WITHOUT rebuilding those scenes (only the audio objects change).
/// The scene builders also call these helpers, so rebuilt scenes keep their sound.
/// </summary>
public static class AudioSetup
{
    static readonly string[] GameplayScenes = { "Assets/Scenes/Sh/SplitCamera.unity", "Assets/Scenes/Sh/Puzzle2.unity", "Assets/Scenes/Sh/Puzzle3.unity" };
    const string CutscenePath = "Assets/Scenes/Sh/Cutscene.unity";

    [MenuItem("Tools/Kyan/Setup Audio (all scenes)")]
    public static void SetupAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!SoundSynth.Load("music_journey")) SoundSynth.GenerateAll();

        var cs = EditorSceneManager.OpenScene(CutscenePath, OpenSceneMode.Single);
        var director = Object.FindAnyObjectByType<MirrorCutscene>();
        if (director) WireCutscene(director);
        EditorSceneManager.MarkSceneDirty(cs);
        EditorSceneManager.SaveScene(cs);

        foreach (var path in GameplayScenes)
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) continue;
            var s = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            AddGameAudio();
            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s);
        }
        Debug.Log("[AudioSetup] Audio wired into Cutscene, SplitCamera and Puzzle3.");
    }

    public static void WireCutscene(MirrorCutscene d)
    {
        d.whisperClip = SoundSynth.Load("cs_whisper");
        d.crackClip = SoundSynth.Load("cs_crack");
        d.shatterClip = SoundSynth.Load("cs_shatter");
        d.roomToneClip = SoundSynth.Load("cs_room_tone");
        d.musicClip = SoundSynth.Load("cs_sad_pad");
        d.heartbeatClip = SoundSynth.Load("cs_heartbeat");
        if (!d.sfx) d.sfx = d.GetComponent<AudioSource>();
        if (!d.sfx) d.sfx = d.gameObject.AddComponent<AudioSource>();
        d.sfx.playOnAwake = false;
        EditorUtility.SetDirty(d);
    }

    /// <summary>Adds (or replaces) the GameAudio object in the open scene.</summary>
    public static GameAudio AddGameAudio()
    {
        var old = GameObject.Find("GameAudio");
        if (old) Object.DestroyImmediate(old);
        var a = new GameObject("GameAudio").AddComponent<GameAudio>();
        a.music = SoundSynth.Load("music_journey");
        a.ambienceLeft = SoundSynth.Load("amb_desert_wind");
        a.ambienceRight = SoundSynth.Load("amb_forest");
        a.dig = SoundSynth.Load("sfx_dig_loop");
        a.digDone = SoundSynth.Load("sfx_dig_done");
        a.pickup = SoundSynth.Load("sfx_pickup");
        a.drop = SoundSynth.Load("sfx_drop");
        a.splash = SoundSynth.Load("sfx_splash");
        a.arrive = SoundSynth.Load("sfx_arrive_neon");
        a.throwItem = SoundSynth.Load("sfx_throw");
        a.respawn = SoundSynth.Load("sfx_respawn");
        a.message = SoundSynth.Load("sfx_message");
        a.penStrokes = new[] { SoundSynth.Load("sfx_pen_1"), SoundSynth.Load("sfx_pen_2"), SoundSynth.Load("sfx_pen_3"), SoundSynth.Load("sfx_pen_4") };
        a.finaleMusic = SoundSynth.Load("music_finale");
        a.letterMusic = SoundSynth.Load("music_letter");
        a.typeKeys = new AudioClip[6];
        for (int v = 1; v <= 6; v++) a.typeKeys[v - 1] = SoundSynth.Load("sfx_key_" + v);
        a.typeSpace = SoundSynth.Load("sfx_key_space");
        a.typeEnter = SoundSynth.Load("sfx_key_enter");
        a.place = SoundSynth.Load("sfx_place");
        a.grow = SoundSynth.Load("sfx_grow_loop");
        a.bridgeDone = SoundSynth.Load("sfx_bridge_done");
        a.fragment = SoundSynth.Load("sfx_fragment");
        a.bloom = SoundSynth.Load("sfx_bloom");
        a.joinHum = SoundSynth.Load("sfx_join_loop");
        a.mirrorWhole = SoundSynth.Load("sfx_mirror_whole");
        a.click = SoundSynth.Load("sfx_click");
        a.waterFlow = SoundSynth.Load("sfx_water_flow");
        a.waterStep = SoundSynth.Load("sfx_water_step");
        a.fall = SoundSynth.Load("sfx_fall");
        a.hurt = SoundSynth.Load("sfx_hurt");
        a.sandSteps = new[] { SoundSynth.Load("sfx_step_sand_1"), SoundSynth.Load("sfx_step_sand_2"), SoundSynth.Load("sfx_step_sand_3"), SoundSynth.Load("sfx_step_sand_4") };
        a.grassSteps = new[] { SoundSynth.Load("sfx_step_grass_1"), SoundSynth.Load("sfx_step_grass_2"), SoundSynth.Load("sfx_step_grass_3"), SoundSynth.Load("sfx_step_grass_4") };
        a.endingMusic = SoundSynth.Load("music_ending");
        return a;
    }
}
