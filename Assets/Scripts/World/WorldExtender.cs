using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes each world feel open instead of an 80 x 80 m tile floating in the void:
///  - extends the ground out to <see cref="HalfSize"/> m around each world (stopping before the other world),
///  - in level 1 stretches the chasm along the whole ground so it reads as a canyon,
///  - scatters copies of the world's own props (dunes, dead trees / trees, rocks) beyond the play area,
///  - adds a horizon haze per camera in that world's sky colour so the ground fades into the sky.
/// The invisible "Bounds" walls (+/-20 m) that keep the players inside are left as they are.
/// Installs itself in every scene with a desert / forest root; no builder or scene changes.
/// </summary>
public static class WorldExtender
{
    public const float HalfSize = 148f;   // worlds are 300 m apart: desert reaches x 148, forest starts at x 152
    const float CoreHalf = 40f;           // the builders' ground is 80 x 80 m
    const float ChasmNear = 4f, ChasmFar = 10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Install(s);

    static int LevelFor(string scene) => scene == "Puzzle2" ? 2 : scene == "Puzzle3" ? 3 : 1;

    static void Install(Scene s)
    {
        var desert = GameObject.Find("--- DESERT (Present) ---");
        var forest = GameObject.Find("--- FOREST (Future) ---");
        if (!desert || desert.scene != s) { Haze.Deactivate(); return; }
        if (desert.transform.Find("Open Land")) return;

        Extend(desert.transform, true, 11);
        if (forest) Extend(forest.transform, false, 23);

        int level = LevelFor(s.name);
        Color desertHaze = level == 1 ? new Color(0.13f, 0.15f, 0.26f) : level == 2 ? new Color(0.92f, 0.66f, 0.48f) : new Color(0.84f, 0.87f, 0.89f);
        Color forestHaze = new Color(0.62f, 0.8f, 0.88f);
        Haze.Install(desertHaze, forestHaze);
    }

    // ------------------------------------------------------------------ ground

    static void Extend(Transform world, bool isDesert, int seed)
    {
        var root = new GameObject("Open Land").transform;
        root.SetParent(world, false);

        Material groundMat = FindMaterial(world, "Ground South", "Ground");
        bool hasChasm = world.Find(isDesert ? "Chasm Floor" : "Gap Floor");
        float H = HalfSize, C = CoreHalf, band = H - C;

        // ring of ground around the builder's 80 x 80 m core (top surface at y = 0, like the core)
        Ground(root, "Land North", new Vector3(0f, -0.5f, (H + C) / 2f), new Vector3(2f * H, 1f, band), groundMat);
        Ground(root, "Land South", new Vector3(0f, -0.5f, -(H + C) / 2f), new Vector3(2f * H, 1f, band), groundMat);
        if (hasChasm)
        {
            // east / west bands leave the chasm open so it continues as a canyon
            float sLen = ChasmNear + C, nLen = C - ChasmFar;
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (H + C) / 2f;
                Ground(root, "Land Side S", new Vector3(x, -0.5f, (-C + ChasmNear) / 2f), new Vector3(band, 1f, sLen), groundMat);
                Ground(root, "Land Side N", new Vector3(x, -0.5f, (ChasmFar + C) / 2f), new Vector3(band, 1f, nLen), groundMat);
            }
            // continue the canyon floor and walls with NEW pieces on both sides (never stretch the originals:
            // other things, like the waterfalls, may be attached to them)
            foreach (var name in new[] { isDesert ? "Chasm Floor" : "Gap Floor", "Cliff South", "Cliff North" })
            {
                var t = world.Find(name);
                if (!t || !t.TryGetComponent<Renderer>(out var r)) continue;
                foreach (float side in new[] { -1f, 1f })
                {
                    var lp = t.localPosition;
                    var ls = t.localScale;
                    Ground(root, name + " (far)", new Vector3(side * (H + C) / 2f, lp.y, lp.z), new Vector3(band, ls.y, ls.z), r.sharedMaterial);
                }
            }
        }
        else
        {
            Ground(root, "Land East", new Vector3((H + C) / 2f, -0.5f, 0f), new Vector3(band, 1f, 2f * C), groundMat);
            Ground(root, "Land West", new Vector3(-(H + C) / 2f, -0.5f, 0f), new Vector3(band, 1f, 2f * C), groundMat);
        }

        Scatter(world, root, isDesert, hasChasm, seed);
    }

    static void Ground(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        Object.Destroy(g.GetComponent<Collider>()); // players never get here (Bounds walls), keep physics light
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        if (mat) g.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Material FindMaterial(Transform world, params string[] names)
    {
        foreach (var n in names)
        {
            var t = world.Find(n);
            if (t && t.TryGetComponent<Renderer>(out var r)) return r.sharedMaterial;
        }
        var any = world.GetComponentInChildren<Renderer>();
        return any ? any.sharedMaterial : null;
    }

    // ------------------------------------------------------------------ scenery

    static void Scatter(Transform world, Transform root, bool isDesert, bool hasChasm, int seed)
    {
        var props = world.Find("Props");
        if (!props) return;

        // templates: the world's own props, so the style always matches
        var templates = new Dictionary<string, List<Transform>>();
        foreach (Transform c in props)
        {
            if (!templates.TryGetValue(c.name, out var list)) templates[c.name] = list = new List<Transform>();
            list.Add(c);
        }

        var plan = isDesert
            ? new (string name, int count, float minScale, float maxScale)[] { ("Dune", 70, 0.9f, 2.2f), ("Dead Tree", 22, 0.8f, 1.5f), ("Broken Pillar", 10, 0.8f, 1.4f) }
            : new (string name, int count, float minScale, float maxScale)[] { ("Tree", 150, 0.8f, 1.5f), ("Rock", 30, 0.8f, 2f), ("Glow Plant", 40, 0.8f, 1.3f) };

        var rnd = new System.Random(seed);
        float R() => (float)rnd.NextDouble();
        foreach (var (name, count, minS, maxS) in plan)
        {
            if (!templates.TryGetValue(name, out var src) || src.Count == 0) continue;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = Vector3.zero;
                bool ok = false;
                for (int tries = 0; tries < 20 && !ok; tries++)
                {
                    float ang = R() * Mathf.PI * 2f;
                    float dist = Mathf.Lerp(26f, HalfSize - 6f, Mathf.Sqrt(R()));
                    p = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                    bool inPlay = Mathf.Abs(p.x) < 24f && Mathf.Abs(p.z) < 24f;
                    bool inChasm = hasChasm && p.z > ChasmNear - 3f && p.z < ChasmFar + 3f;
                    ok = !inPlay && !inChasm && Mathf.Abs(p.x) < HalfSize - 4f && Mathf.Abs(p.z) < HalfSize - 4f;
                }
                if (!ok) continue;
                var t = src[rnd.Next(src.Count)];
                var copy = Object.Instantiate(t.gameObject, root).transform;
                copy.name = name + " (far)";
                copy.localPosition = new Vector3(p.x, t.localPosition.y, p.z);
                copy.localRotation = Quaternion.Euler(t.localEulerAngles.x, R() * 360f, t.localEulerAngles.z);
                copy.localScale = t.localScale * Mathf.Lerp(minS, maxS, R());
                foreach (var col in copy.GetComponentsInChildren<Collider>()) Object.Destroy(col);
            }
        }
    }

    // ------------------------------------------------------------------ horizon haze

    /// <summary>Per-camera linear fog: the desert camera gets the desert's sky colour, the forest camera the forest's.
    /// Other cameras (cinematic, ending) keep the scene's own fog settings.</summary>
    static class Haze
    {
        static Color desert, forest;
        static bool hooked, active;
        static bool origFog; static Color origColor; static FogMode origMode; static float origStart, origEnd, origDensity;

        public static void Install(Color desertColor, Color forestColor)
        {
            desert = desertColor; forest = forestColor;
            origFog = RenderSettings.fog; origColor = RenderSettings.fogColor; origMode = RenderSettings.fogMode;
            origStart = RenderSettings.fogStartDistance; origEnd = RenderSettings.fogEndDistance; origDensity = RenderSettings.fogDensity;
            active = true;
            if (hooked) return;
            hooked = true;
            RenderPipelineManager.beginCameraRendering += Begin;
        }

        public static void Deactivate() => active = false;

        static void Begin(ScriptableRenderContext ctx, Camera cam)
        {
            if (!active) return;
            bool d = cam.name == "Camera_Light_Left", f = cam.name == "Camera_Void_Right";
            if (d || f)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 45f;
                RenderSettings.fogEndDistance = 118f;
                RenderSettings.fogColor = d ? desert : forest;
            }
            else
            {
                RenderSettings.fog = origFog; RenderSettings.fogColor = origColor; RenderSettings.fogMode = origMode;
                RenderSettings.fogStartDistance = origStart; RenderSettings.fogEndDistance = origEnd; RenderSettings.fogDensity = origDensity;
            }
        }
    }
}
