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
        var ending = GameObject.Find("--- ENDING ROOM ---");
        if (ending && ending.scene == s) ExtendEnding(ending.transform, forest ? forest.transform : null, desert.transform);

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
        Horizon(root, isDesert, hasChasm, seed, groundMat ? groundMat.shader : null);
    }

    // ------------------------------------------------------------------ horizon hills

    // Ring profiles (radius factor, height factor) from the foot up; a last radius of 0 is a peak,
    // otherwise the top is closed with a flat cap (mesas).
    static readonly Vector2[] Rounded = { new Vector2(1f, 0f), new Vector2(0.78f, 0.42f), new Vector2(0.45f, 0.84f), new Vector2(0f, 1f) };
    static readonly Vector2[] Peak = { new Vector2(1f, 0f), new Vector2(0.62f, 0.38f), new Vector2(0.28f, 0.78f), new Vector2(0f, 1f) };
    static readonly Vector2[] Mesa = { new Vector2(1f, 0f), new Vector2(0.86f, 0.5f), new Vector2(0.7f, 0.94f), new Vector2(0.64f, 1f) };

    // ------------------------------------------------------------------ level 3 ending room

    const float EndingCore = 12f;   // the builder's ending floor is 24 x 24 m
    const float EndingHalf = 125f;

    /// <summary>The ending room (x = 600) is a 24 m floor in the void. Surround it with the healed
    /// world: forest ground with tiled texture, trees, rocks and glow plants from the forest's own props,
    /// a few last dunes and dead trees far off on the left (the desert side), green hills on the horizon,
    /// and let the ending camera see as far as the gameplay cameras.</summary>
    static void ExtendEnding(Transform room, Transform forest, Transform desert)
    {
        if (room.Find("Open Land")) return;
        var root = new GameObject("Open Land").transform;
        root.SetParent(room, false);

        // ground: the room floor's own (forest) material, with world-space UVs like the floor
        var floor = room.Find("Floor (forest side)");
        Material mat = floor && floor.TryGetComponent<Renderer>(out var fr) ? fr.sharedMaterial : null;
        float C = EndingCore, H = EndingHalf;
        var rects = new[]
        {
            new Rect(-H, -H, H - C, 2f * H),      // west
            new Rect(C, -H, H - C, 2f * H),       // east
            new Rect(-C, C, 2f * C, H - C),       // north (behind the mirror)
            new Rect(-C, -H, 2f * C, H - C),      // south (behind the camera)
        };
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        foreach (var r in rects)
        {
            int i = verts.Count;
            foreach (var p in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin) })
            {
                verts.Add(new Vector3(p.x, 0f, p.y));
                Vector3 w = room.TransformPoint(new Vector3(p.x, 0f, p.y));
                uvs.Add(new Vector2(w.x / 4f, w.z / 4f));
            }
            tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        var mesh = new Mesh { name = "Ending Land" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        var land = new GameObject("Land");
        land.transform.SetParent(root, false);
        land.AddComponent<MeshFilter>().sharedMesh = mesh;
        var lr = land.AddComponent<MeshRenderer>();
        if (mat) lr.sharedMaterial = mat;

        // scenery copied from the worlds' own props
        var rnd = new System.Random(606);
        float R() => (float)rnd.NextDouble();
        bool Clear(Vector3 p) =>
            (Mathf.Abs(p.x) > C + 2f || Mathf.Abs(p.z) > C + 2f)                 // outside the room
            && !(Mathf.Abs(p.x) < 9f && p.z > 0f && p.z < 34f);                  // keep the view behind the mirror open

        void Place(Transform world, string name, int count, float minS, float maxS, bool leftOnly, float minDist)
        {
            var props = world ? world.Find("Props") : null;
            if (!props) return;
            var src = new List<Transform>();
            foreach (Transform c in props) if (c.name == name) src.Add(c);
            if (src.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = Vector3.zero;
                bool ok = false;
                for (int tries = 0; tries < 25 && !ok; tries++)
                {
                    float ang = R() * Mathf.PI * 2f;
                    float dist = Mathf.Lerp(minDist, H - 20f, Mathf.Sqrt(R()));
                    p = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                    ok = Clear(p) && (!leftOnly || p.x < -25f);
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
        Place(forest, "Tree", 170, 0.8f, 1.5f, false, 15f);
        Place(forest, "Rock", 30, 0.8f, 2f, false, 15f);
        Place(forest, "Glow Plant", 50, 0.8f, 1.3f, false, 14f);
        Place(desert, "Dune", 14, 0.9f, 2f, true, 30f);       // the last of the desert, far off on the desert side
        Place(desert, "Dead Tree", 5, 0.8f, 1.3f, true, 30f);

        // green hills all round: the world is healed
        Horizon(root, false, false, 31, mat ? mat.shader : null);

        // the ending camera was limited to 60 m (it only saw the room); let it see the land and the hills
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (cam.name == "Ending Camera") cam.farClipPlane = 120f;
    }

    /// <summary>A ring of flat-shaded low-poly hills (near) and mountains (far) so the horizon isn't a
    /// flat line: sandy dunes and mesas in the desert, green hills and peaks in the forest. They sit
    /// 58-112 m from the world centre, inside the 120 m view limit, half-faded by the haze.</summary>
    static void Horizon(Transform root, bool isDesert, bool hasChasm, int seed, Shader groundShader)
    {
        var rnd = new System.Random(seed * 7 + 3);
        float R() => (float)rnd.NextDouble();

        Color nearCol = isDesert ? new Color(0.78f, 0.66f, 0.50f) : new Color(0.33f, 0.58f, 0.27f);
        Color farCol = isDesert ? new Color(0.60f, 0.47f, 0.37f) : new Color(0.27f, 0.45f, 0.37f);

        // (count, min/max distance, min/max footprint radius, min/max height, profiles)
        var rings = new (int n, float d0, float d1, float r0, float r1, float h0, float h1, Vector2[][] shapes, Color col)[]
        {
            (22, 92f, 112f, 18f, 30f, 22f, 42f, isDesert ? new[] { Mesa, Peak, Mesa } : new[] { Peak, Peak, Rounded }, farCol),
            (26, 60f, 82f, 10f, 18f, 7f, 16f, isDesert ? new[] { Rounded, Rounded, Mesa } : new[] { Rounded }, nearCol),
        };

        foreach (var ring in rings)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float offset = R() * Mathf.PI * 2f;
            for (int i = 0; i < ring.n; i++)
            {
                float ang = offset + (i + (R() - 0.5f) * 0.7f) / ring.n * Mathf.PI * 2f;
                float dist = Mathf.Lerp(ring.d0, ring.d1, R());
                var c = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                float rad = Mathf.Lerp(ring.r0, ring.r1, R());
                // keep the level 1 canyon open where it runs out through the hills
                if (hasChasm && Mathf.Abs(c.z - (ChasmNear + ChasmFar) * 0.5f) < rad + 4f) continue;
                float h = Mathf.Lerp(ring.h0, ring.h1, R());
                var shape = ring.shapes[rnd.Next(ring.shapes.Length)];
                AddHill(verts, tris, rnd, c, rad, h, R() * 360f, 0.7f + R() * 0.6f, shape);
            }

            var mesh = new Mesh { name = "Horizon Hills" };
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); // every triangle has its own vertices, so this is flat shading
            mesh.RecalculateBounds();

            var go = new GameObject(ring.d0 > 85f ? "Horizon Mountains" : "Horizon Hills");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = HillMaterial(ring.col, groundShader);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
    }

    static void AddHill(List<Vector3> verts, List<int> tris, System.Random rnd, Vector3 centre, float radius,
                        float height, float yaw, float stretch, Vector2[] shape)
    {
        float R() => (float)rnd.NextDouble();
        int seg = 7 + rnd.Next(4);
        var rot = Quaternion.Euler(0f, yaw, 0f);
        int rings = shape.Length;
        var p = new Vector3[rings, seg];
        for (int k = 0; k < rings; k++)
            for (int s = 0; s < seg; s++)
            {
                float a = (s + (k > 0 ? (R() - 0.5f) * 0.5f : 0f)) / seg * Mathf.PI * 2f;
                float rr = radius * shape[k].x * (shape[k].x > 0f ? 0.8f + R() * 0.35f : 1f);
                float hh = height * shape[k].y * (k == 0 ? 1f : 0.88f + R() * 0.2f);
                if (k == 0) hh = -2.5f; // sink the foot into the ground
                var local = new Vector3(Mathf.Cos(a) * rr * stretch, hh, Mathf.Sin(a) * rr / stretch);
                p[k, s] = centre + rot * local;
            }

        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        for (int k = 0; k < rings - 1; k++)
            for (int s = 0; s < seg; s++)
            {
                int n = (s + 1) % seg;
                // winding: clockwise seen from outside, so the faces point outward
                Tri(p[k, s], p[k + 1, s], p[k, n]);
                if (shape[k + 1].x > 0f) Tri(p[k, n], p[k + 1, s], p[k + 1, n]);
            }

        if (shape[rings - 1].x > 0f) // flat cap (mesa)
        {
            var top = Vector3.zero;
            for (int s = 0; s < seg; s++) top += p[rings - 1, s];
            top /= seg;
            for (int s = 0; s < seg; s++) Tri(p[rings - 1, s], top, p[rings - 1, (s + 1) % seg]);
        }
    }

    static readonly Dictionary<Color, Material> hillMats = new Dictionary<Color, Material>();

    static Material HillMaterial(Color c, Shader groundShader)
    {
        if (hillMats.TryGetValue(c, out var m) && m) return m;
        // Reuse the ground's shader: it is always in a build (Shader.Find can return a stripped shader).
        var shader = groundShader ? groundShader : Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(shader) { name = "Horizon Hills" };
        m.SetColor("_BaseColor", c);
        m.SetColor("_Color", c);
        m.SetFloat("_Smoothness", 0.05f);
        m.SetFloat("_Glossiness", 0.05f);
        hillMats[c] = m;
        return m;
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
            bool d = cam.name == "Camera_Light_Left", f = cam.name == "Camera_Void_Right" || cam.name == "Ending Camera";
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
