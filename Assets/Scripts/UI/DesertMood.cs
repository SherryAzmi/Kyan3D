using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// The Present's mood, seen only on the left (desert) half of the split screen.
/// Level 1 is dark, washed-out and cold, like depression; every level lets more light and colour back in.
/// Colour is strongest right beside the mirror crack, as if its light were healing the desert.
///
/// How: a local post-processing Volume around the desert world. Only the desert camera stands inside it
/// (the forest is 300 m away), so the forest half is not affected even though both share one sun.
/// Installs itself in any scene that has a "--- DESERT (Present) ---" root; no scene or builder changes.
/// </summary>
public static class DesertMood
{
    struct Mood
    {
        public float exposure, saturation, contrast;   // whole desert camera (mild)
        public Color filter;
        public float shade, colourZone;                 // grey shade, fading out toward the crack
    }

    // Level 1 = depressed, level 3 = almost healed.
    static readonly Mood[] Levels =
    {
        new Mood { exposure = -0.45f, saturation = -18f, contrast = 8f, filter = new Color(0.88f, 0.91f, 0.98f), shade = 0.62f, colourZone = 380f },
        new Mood { exposure = -0.25f, saturation = -10f, contrast = 4f, filter = new Color(0.94f, 0.95f, 0.99f), shade = 0.4f, colourZone = 520f },
        new Mood { exposure = -0.05f, saturation = -2f, contrast = 0f, filter = new Color(1f, 0.99f, 0.97f), shade = 0.14f, colourZone = 700f },
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Install(s);

    static int LevelFor(string scene)
    {
        switch (scene)
        {
            case "Puzzle2": return 2;
            case "Puzzle3": return 3;
            default: return 1;
        }
    }

    static void Install(Scene s)
    {
        var desert = GameObject.Find("--- DESERT (Present) ---");
        if (!desert || desert.scene != s) return;
        if (desert.transform.Find("Desert Mood Volume")) return;

        var m = Levels[Mathf.Clamp(LevelFor(s.name), 1, Levels.Length) - 1];

        var go = new GameObject("Desert Mood Volume");
        go.transform.SetParent(desert.transform, false);
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(240f, 200f, 240f); // the whole desert world, far from the forest at +300 m
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = false;
        vol.priority = 10f;
        vol.blendDistance = 0f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Desert Mood (runtime)";
        var ca = profile.Add<ColorAdjustments>(true);
        ca.postExposure.Override(m.exposure);
        ca.saturation.Override(m.saturation);
        ca.contrast.Override(m.contrast);
        ca.colorFilter.Override(m.filter);
        vol.sharedProfile = profile;

        // The heavy grey sits in a screen-space shade that fades out toward the mirror crack,
        // so the light from the crack seems to bring colour back to the desert.
        var ui = GameObject.Find("SplitScreenUI");
        if (ui && ui.scene == s && !ui.GetComponentInChildren<DesertShade>(true))
        {
            var sg = new GameObject("Desert Shade", typeof(RectTransform), typeof(CanvasRenderer));
            sg.transform.SetParent(ui.transform, false);
            sg.transform.SetSiblingIndex(0); // under the crack and the labels
            var rt = (RectTransform)sg.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var shade = sg.AddComponent<DesertShade>();
            shade.raycastTarget = false;
            shade.strength = m.shade;
            shade.colourZone = m.colourZone;
        }

        // Post-processing must be on for the desert camera (only it is inside the volume).
        foreach (var name in new[] { "Camera_Light_Left" })
        {
            var cam = GameObject.Find(name);
            if (cam && cam.TryGetComponent<UniversalAdditionalCameraData>(out var data)) data.renderPostProcessing = true;
            if (cam) DesertSky.Apply(cam.GetComponent<Camera>(), LevelFor(s.name)); // night, sunrise, day
        }
    }
}
