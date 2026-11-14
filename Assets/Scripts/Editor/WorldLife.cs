using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tools > Kyan > Add World Life (open scene)
///  Forest: patches of wildflowers (4 colours), fluttering butterflies, floating pollen.
///  Desert: dead cacti (some leaning or broken), rolling tumbleweeds, extra dry bushes, blowing sand.
/// Everything keeps off paths, the river, puzzle objects and the spawn. Safe to run again (replaces).
/// The Bridge and Dry Bed builders call it too.
/// </summary>
public static class WorldLife
{
    const string TexDir = "Assets/Textures/Life";
    const string MatDir = "Assets/Materials/World";
    const string MeshDir = "Assets/Meshes/Generated";
    const string ModelDir = "Assets/TwoWorlds/Models/";
    static readonly string[] Generated = { "Flowers", "Butterflies", "Pollen", "Dead Cacti", "Tumbleweeds", "Blowing Sand", "Dry Bushes",
                                           "Green Patches", "Desert Flowers", "Desert Butterflies" };

    static readonly Color[] FlowerColors =
    {
        new Color(1f, 0.45f, 0.7f), new Color(1f, 0.85f, 0.2f), new Color(0.97f, 0.97f, 0.94f), new Color(0.66f, 0.42f, 0.98f)
    };
    static readonly Color[] WingColors = { new Color(1f, 0.55f, 0.1f), new Color(0.25f, 0.6f, 1f), new Color(1f, 0.9f, 0.3f), new Color(0.95f, 0.45f, 0.75f) };

    [MenuItem("Tools/Kyan/Add World Life (open scene)")]
    static void FromMenu()
    {
        Debug.Log("[WorldLife] " + ApplyOpenScene());
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    public static string ApplyOpenScene()
    {
        EnsureTextures();
        Directory.CreateDirectory(MeshDir);
        string scene = EditorSceneManager.GetActiveScene().name;
        Physics.SyncTransforms();
        string res = "";
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "--- ENDING ROOM ---") { res += EndingRoom(root.transform, scene); continue; }
            if (root.name != "--- FOREST (Future) ---" && root.name != "--- DESERT (Present) ---") continue;
            foreach (var n in Generated)
            {
                var old = root.transform.Find(n);
                if (old) Object.DestroyImmediate(old.gameObject);
            }
            if (root.name.Contains("FOREST")) res += Forest(root.transform, scene);
            else res += Desert(root.transform, scene, GroundArt.Level(scene));
        }
        res += NeonLighting.ApplyOpenScene();
        AssetDatabase.SaveAssets();
        return res;
    }

    // ================================================================== forest

    static string Forest(Transform world, string scene)
    {
        var rnd = new System.Random(101);
        var ctx = new Placement(world, false);

        // wildflower patches
        var lists = new List<Vector3>[FlowerColors.Length];
        for (int i = 0; i < lists.Length; i++) lists[i] = new List<Vector3>();
        int flowers = 0;
        for (int patch = 0; patch < 28; patch++)
        {
            if (!ctx.Random(rnd, 0f, out var centre)) continue;
            int main = rnd.Next(FlowerColors.Length);
            int n = 14 + rnd.Next(22);
            for (int k = 0; k < n; k++)
            {
                float a = (float)rnd.NextDouble() * 6.283f, d = Mathf.Sqrt((float)rnd.NextDouble()) * 2.2f;
                var p = centre + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                if (!ctx.Valid(p, 0f, out var g)) continue;
                int c = rnd.NextDouble() < 0.8 ? main : rnd.Next(FlowerColors.Length);
                lists[c].Add(g);
                flowers++;
            }
        }
        var holder = Child(world, "Flowers");
        for (int c = 0; c < lists.Length; c++)
        {
            if (lists[c].Count == 0) continue;
            var mesh = Cards(world, lists[c], 0.42f, 0.38f, 2, rnd, $"{scene}_flowers_{c}");
            var go = Child(holder, "Flowers " + c).gameObject;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = CardMat("M_Flower_" + c, "flower_" + c);
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // butterflies
        var bf = Child(world, "Butterflies").gameObject.AddComponent<Butterflies>();
        bf.count = 18;
        bf.range = 17f;
        var wings = new Material[WingColors.Length];
        for (int i = 0; i < wings.Length; i++) wings[i] = UnlitCardMat("M_Butterfly_" + i, "wing_" + i);
        bf.wingMaterials = wings;
        bf.bodyMaterial = Lit("M_Butterfly_Body", new Color(0.12f, 0.1f, 0.08f), 0.3f);

        // pollen drifting in the light
        Particles(world, "Pollen", new Vector3(0f, 1.6f, 0f), new Vector3(38f, 3f, 38f), new Color(1f, 0.95f, 0.65f, 0.85f),
                  new Vector2(0.03f, 0.08f), new Vector2(6f, 9f), 28f, Vector3.zero, 0.25f);
        return $"forest: {flowers} flowers, 18 butterflies, pollen; ";
    }

    // ================================================================== desert

    /// <summary>
    /// The desert is the Present's mind: level 1 is dead and empty; each solved level it heals a little —
    /// fewer dead things, then grass, green patches, flowers and finally butterflies.
    /// </summary>
    static string Desert(Transform world, string scene, int level)
    {
        int cactusCount = level == 1 ? 11 : level == 2 ? 5 : 2;
        int fallenCount = level == 1 ? 3 : level == 2 ? 1 : 0;
        int weedCount = level == 1 ? 4 : level == 2 ? 2 : 0;
        int bushCount = level == 1 ? 45 : level == 2 ? 20 : 6;
        float dustRate = level == 1 ? 16f : level == 2 ? 6f : 0f;

        var rnd = new System.Random(202);
        var ctx = new Placement(world, true);
        var dry = Lit("M_Cactus_Dead", new Color(0.5f, 0.46f, 0.34f), 0.1f);
        var dryDark = Lit("M_Cactus_Dead_Dark", new Color(0.36f, 0.32f, 0.24f), 0.1f);

        // dead cacti
        var cacti = Child(world, "Dead Cacti");
        int cn = 0;
        for (int k = 0; k < 40 && cn < cactusCount; k++)
        {
            if (!ctx.Random(rnd, 2.2f, out var p)) continue;
            DeadCactus(cacti, world.InverseTransformPoint(p), rnd, rnd.NextDouble() < 0.6 ? dry : dryDark);
            ctx.Block(p, 2.5f);
            cn++;
        }
        // a couple of fallen cactus trunks
        for (int k = 0; k < 10 && cn < cactusCount + fallenCount; k++)
        {
            if (!ctx.Random(rnd, 1.5f, out var p)) continue;
            var log = Prim(PrimitiveType.Cylinder, "Fallen Cactus", cacti, world.InverseTransformPoint(p) + Vector3.up * 0.2f,
                           Quaternion.Euler(90f, (float)rnd.NextDouble() * 360f, 0f), new Vector3(0.4f, 0.9f + (float)rnd.NextDouble() * 0.6f, 0.4f), dryDark, false);
            ctx.Block(p, 2f);
            cn++;
        }

        // tumbleweeds
        var tw = Child(world, "Tumbleweeds");
        var twMesh = TumbleweedMesh();
        var twMat = Lit("M_Tumbleweed", new Color(0.52f, 0.42f, 0.28f), 0.05f);
        for (int i = 0; i < weedCount; i++)
        {
            var go = new GameObject("Tumbleweed");
            go.transform.SetParent(tw, false);
            go.transform.localPosition = new Vector3(-20f + i * 9f, 0.45f, -14f + i * 6f);
            go.transform.localScale = Vector3.one * (0.8f + 0.15f * i);
            go.AddComponent<MeshFilter>().sharedMesh = twMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = twMat;
            var t = go.AddComponent<Tumbleweed>();
            t.areaCentre = world.position;
            t.radius = 0.45f * go.transform.localScale.x;
            t.speed = 1.8f + 0.5f * i;
        }

        // extra dry bushes (team model)
        var bushes = Child(world, "Dry Bushes");
        var shrub = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "DryShrub.fbx");
        int bn = 0;
        if (shrub)
            for (int k = 0; k < 120 && bn < bushCount; k++)
            {
                if (!ctx.Random(rnd, 0.8f, out var p)) continue;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(shrub);
                g.name = "Dry Bush";
                g.transform.SetParent(bushes, false);
                g.transform.localPosition = world.InverseTransformPoint(p);
                g.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                g.transform.localScale = Vector3.one * (0.7f + 0.7f * (float)rnd.NextDouble());
                foreach (var c in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                bn++;
            }

        // sand blowing low across the ground (calms down as the desert heals)
        if (dustRate > 0f)
            Particles(world, "Blowing Sand", new Vector3(-6f, 0.35f, 0f), new Vector3(30f, 0.5f, 40f), new Color(0.86f, 0.76f, 0.6f, 0.16f),
                      new Vector2(0.5f, 1.6f), new Vector2(5f, 8f), dustRate, new Vector3(3f, 0f, 1f), 0.05f);

        // ---------- healing: green patches, flowers, butterflies ----------
        int patchCount = level == 1 ? 0 : level == 2 ? 4 : 14;
        int pn = 0;
        if (patchCount > 0)
        {
            var patches = Child(world, "Green Patches");
            var pm = PatchMat();
            for (int k = 0; k < 80 && pn < patchCount; k++)
            {
                if (!ctx.Random(rnd, 1.2f, out var p)) continue;
                var q = Prim(PrimitiveType.Quad, "Green Patch", patches, world.InverseTransformPoint(p) + Vector3.up * (0.012f + pn * 0.0005f),
                             Quaternion.Euler(90f, (float)rnd.NextDouble() * 360f, 0f),
                             new Vector3(2.5f + 3f * (float)rnd.NextDouble(), 2f + 2.5f * (float)rnd.NextDouble(), 1f), pm, false);
                q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                pn++;
            }
        }

        int flowerCount = 0;
        if (level >= 2)
        {
            var lists = new List<Vector3>[FlowerColors.Length];
            for (int i = 0; i < lists.Length; i++) lists[i] = new List<Vector3>();
            if (level == 2)
            {
                // just a few brave flowers on their own
                for (int k = 0; k < 40 && flowerCount < 6; k++)
                    if (ctx.Random(rnd, 0f, out var p)) { lists[rnd.Next(FlowerColors.Length)].Add(p); flowerCount++; }
            }
            else
            {
                // small meadows starting to appear
                for (int patch = 0; patch < 7; patch++)
                {
                    if (!ctx.Random(rnd, 0f, out var c)) continue;
                    int main = rnd.Next(FlowerColors.Length);
                    for (int k = 0; k < 12; k++)
                    {
                        float a = (float)rnd.NextDouble() * 6.283f, d = Mathf.Sqrt((float)rnd.NextDouble()) * 1.8f;
                        if (!ctx.Valid(c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), 0f, out var g)) continue;
                        lists[rnd.NextDouble() < 0.8 ? main : rnd.Next(FlowerColors.Length)].Add(g);
                        flowerCount++;
                    }
                }
            }
            var holder = Child(world, "Desert Flowers");
            for (int c = 0; c < lists.Length; c++)
            {
                if (lists[c].Count == 0) continue;
                var go = Child(holder, "Flowers " + c).gameObject;
                go.AddComponent<MeshFilter>().sharedMesh = Cards(world, lists[c], 0.42f, 0.38f, 2, rnd, $"{scene}_desert_flowers_{c}");
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = CardMat("M_Flower_" + c, "flower_" + c);
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        if (level >= 3)
        {
            var bf = Child(world, "Desert Butterflies").gameObject.AddComponent<Butterflies>();
            bf.count = 4;
            bf.range = 15f;
            var wings = new Material[WingColors.Length];
            for (int i = 0; i < wings.Length; i++) wings[i] = UnlitCardMat("M_Butterfly_" + i, "wing_" + i);
            bf.wingMaterials = wings;
            bf.bodyMaterial = Lit("M_Butterfly_Body", new Color(0.12f, 0.1f, 0.08f), 0.3f);
        }

        return $"desert (level {level}): {cn} dead cacti, {weedCount} tumbleweeds, {bn} dry bushes, {pn} green patches, {flowerCount} flowers{(level >= 3 ? ", 4 butterflies" : "")}; ";
    }

    /// <summary>A soft, round carpet of grass lying on the sand (alpha-blended, ragged edge).</summary>
    static Material PatchMat()
    {
        string tex = $"{TexDir}/grass_patch.png";
        if (!File.Exists(tex))
        {
            const int n = 256;
            var px = new Color[n * n];
            var rnd = new System.Random(404);
            float ox = (float)rnd.NextDouble() * 100f, oy = (float)rnd.NextDouble() * 100f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n - 0.5f, v = y / (float)n - 0.5f;
                    float ang = Mathf.Atan2(v, u);
                    float edge = 0.36f + 0.1f * Mathf.PerlinNoise(Mathf.Cos(ang) * 2f + ox, Mathf.Sin(ang) * 2f + oy);
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((edge - r) / 0.12f);
                    float g = Mathf.PerlinNoise(x * 0.08f + ox, y * 0.08f + oy);
                    var c = Color.Lerp(new Color(0.26f, 0.45f, 0.16f), new Color(0.42f, 0.58f, 0.22f), g);
                    c *= 0.85f + 0.3f * (float)rnd.NextDouble();
                    c.a = a * (0.75f + 0.25f * g);
                    px[y * n + x] = c;
                }
            Directory.CreateDirectory(TexDir);
            SaveTex("grass_patch", px);
            AssetDatabase.Refresh();
            var imp = (TextureImporter)AssetImporter.GetAtPath(tex);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
        var m = GetMat("M_Grass_Patch", "Universal Render Pipeline/Lit");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.05f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent - 10;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void DeadCactus(Transform parent, Vector3 localPos, System.Random rnd, Material mat)
    {
        var root = Child(parent, "Dead Cactus");
        root.localPosition = localPos;
        float tilt = (float)rnd.NextDouble() < 0.5 ? (float)rnd.NextDouble() * 22f : 0f;
        root.localRotation = Quaternion.Euler(tilt, (float)rnd.NextDouble() * 360f, 0f);
        float h = 1.8f + (float)rnd.NextDouble() * 1.8f, r = 0.2f + (float)rnd.NextDouble() * 0.1f;
        bool broken = rnd.NextDouble() < 0.3;

        var trunk = Prim(PrimitiveType.Cylinder, "Trunk", root, new Vector3(0f, h * 0.5f, 0f), Quaternion.identity, new Vector3(r * 2f, h * 0.5f, r * 2f), mat, true);
        if (!broken) Prim(PrimitiveType.Sphere, "Top", root, new Vector3(0f, h, 0f), Quaternion.identity, Vector3.one * r * 2f, mat, false);
        // dry vertical ribs
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            Prim(PrimitiveType.Cube, "Rib", root, new Vector3(Mathf.Cos(a) * r * 0.95f, h * 0.5f, Mathf.Sin(a) * r * 0.95f),
                 Quaternion.Euler(0f, -i * 60f, 0f), new Vector3(0.03f, h * 0.96f, 0.03f), mat, false);
        }

        int arms = rnd.Next(3);
        for (int a = 0; a < arms; a++)
        {
            float side = a == 0 ? 1f : -1f;
            float ay = h * (0.4f + 0.25f * (float)rnd.NextDouble());
            float ar = r * 0.75f, stub = 0.35f + (float)rnd.NextDouble() * 0.2f;
            var arm = Child(root, "Arm");
            arm.localPosition = new Vector3(0f, ay, 0f);
            arm.localRotation = Quaternion.Euler(0f, side > 0 ? 0f : 180f, 0f);
            Prim(PrimitiveType.Cylinder, "Stub", arm, new Vector3(r + stub * 0.5f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(ar * 2f, stub * 0.5f, ar * 2f), mat, false);
            Prim(PrimitiveType.Sphere, "Elbow", arm, new Vector3(r + stub, 0f, 0f), Quaternion.identity, Vector3.one * ar * 2f, mat, false);
            // dead arms droop down; a few still point up
            bool droop = rnd.NextDouble() < 0.65;
            float len = 0.5f + (float)rnd.NextDouble() * 0.7f;
            float dir = droop ? -1f : 1f;
            Prim(PrimitiveType.Cylinder, "Arm Up", arm, new Vector3(r + stub + (droop ? 0.06f : 0f), dir * len * 0.5f, 0f),
                 Quaternion.Euler(0f, 0f, droop ? -12f : 0f), new Vector3(ar * 2f, len * 0.5f, ar * 2f), mat, false);
            Prim(PrimitiveType.Sphere, "Arm Tip", arm, new Vector3(r + stub + (droop ? 0.16f : 0f), dir * len, 0f), Quaternion.identity, Vector3.one * ar * 2f, mat, false);
        }
    }

    static Mesh TumbleweedMesh()
    {
        string path = $"{MeshDir}/Tumbleweed.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) return existing;
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var rnd = new System.Random(303);
        var parts = new List<CombineInstance>();
        for (int i = 0; i < 140; i++)
        {
            Vector3 a = RandomOnSphere(rnd) * 0.45f, b = RandomOnSphere(rnd) * 0.45f;
            b = Vector3.Lerp(a, b, 0.35f + 0.3f * (float)rnd.NextDouble()); // short tangled chords
            Vector3 mid = (a + b) * 0.5f;
            float len = (b - a).magnitude;
            if (len < 0.05f) continue;
            float th = 0.012f + 0.01f * (float)rnd.NextDouble();
            parts.Add(new CombineInstance
            {
                mesh = cube,
                transform = Matrix4x4.TRS(mid, Quaternion.LookRotation(b - a), new Vector3(th, th, len))
            });
        }
        var m = new Mesh { name = "Tumbleweed" };
        m.CombineMeshes(parts.ToArray(), true, true);
        m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Vector3 RandomOnSphere(System.Random r)
    {
        float z = (float)r.NextDouble() * 2f - 1f, a = (float)r.NextDouble() * 6.283f, s = Mathf.Sqrt(1f - z * z);
        return new Vector3(s * Mathf.Cos(a), z, s * Mathf.Sin(a));
    }

    // ================================================================== ending room

    /// <summary>
    /// The last cutscene room (two halves meeting at the mirror). The forest half gets the full forest;
    /// the desert half has almost become forest too: grass and flowers spread over it and a young tree grows,
    /// with only a little desert left at the back (sandy patches, one low dune, a dead cactus with flowers).
    /// The walk to the mirror and the space in front of the camera are kept clear.
    /// </summary>
    static string EndingRoom(Transform room, string scene)
    {
        var old = room.Find("Ending Life");
        if (old) Object.DestroyImmediate(old.gameObject);
        var life = Child(room, "Ending Life");
        var rnd = new System.Random(505);

        // ---------- ground: forest everywhere ----------
        var forestMat = GroundArt.ForestMat();
        foreach (var n in new[] { "Floor (desert side)", "Floor (forest side)" })
        {
            var f = room.Find(n);
            if (!f) continue;
            f.GetComponent<MeshFilter>().sharedMesh = GroundArt.TopMesh(f, 4f, $"{scene}_ending_{(n.Contains("desert") ? "desert" : "forest")}");
            f.GetComponent<Renderer>().sharedMaterial = forestMat;
        }

        // ---------- the little desert that is left ----------
        var sandMat = GroundArt.SandMat();
        int dune = 0;
        foreach (Transform c in room)
            if (c.name == "Dune")
            {
                var r = c.GetComponent<Renderer>();
                if (dune++ == 0)
                {
                    if (r) r.sharedMaterial = sandMat;
                    c.localScale = new Vector3(c.localScale.x * 0.8f, c.localScale.y * 0.6f, c.localScale.z * 0.8f); // one low dune left
                }
                else c.gameObject.SetActive(false);
            }
        var sandPatch = SandPatchMat();
        var patches = Child(life, "Sand Patches");
        // where the camera can see them: the left middle and back
        // the Present walks in over the last bit of sand, then onto the grass by the mirror
        Vector3[] sp = { new Vector3(-6.2f, 0f, -0.9f), new Vector3(-8.8f, 0f, 0.6f), new Vector3(-7.4f, 0f, 3.4f), new Vector3(-4.6f, 0f, 1.6f), new Vector3(-8f, 0f, 7f) };
        for (int i = 0; i < sp.Length; i++)
        {
            var q = Prim(PrimitiveType.Quad, "Sand Patch", patches, sp[i] + Vector3.up * (0.012f + i * 0.0005f),
                         Quaternion.Euler(90f, (float)rnd.NextDouble() * 360f, 0f), new Vector3(3f + 2f * (float)rnd.NextDouble(), 2.5f + 2f * (float)rnd.NextDouble(), 1f), sandPatch, false);
            q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        var cactus = Child(life, "Last Cactus");
        DeadCactus(cactus, new Vector3(-7.6f, 0f, 3.6f), rnd, Lit("M_Cactus_Dead", new Color(0.5f, 0.46f, 0.34f), 0.1f));

        // ---------- grass, dry tufts, flowers ----------
        bool OnSand(Vector3 p)
        {
            foreach (var s in sp) if (new Vector2(p.x - s.x, p.z - s.z).magnitude < 1.7f) return true;
            return false;
        }
        bool Clear(Vector3 p) =>
            p.z > -3.5f && p.z < 11.5f && Mathf.Abs(p.x) < 11.5f   // nothing tall right in front of the camera
            && new Vector2(p.x, p.z + 0.8f).magnitude > 1.8f      // the meeting point
            && new Vector2(p.x, p.z - 1.2f).magnitude > 1.6f      // the mirror
            && !(Mathf.Abs(p.z + 0.8f) < 0.7f && Mathf.Abs(p.x) < 7f) // the walk in from each side
            && new Vector2(p.x + 7.6f, p.z - 3.6f).magnitude > 0.9f; // the cactus

        var grass = new List<Vector3>();
        var dry = new List<Vector3>();
        for (int k = 0; k < 2600; k++)
        {
            var p = new Vector3(((float)rnd.NextDouble() * 2f - 1f) * 11.5f, 0f, -6f + (float)rnd.NextDouble() * 17.5f);
            if (!Clear(p)) continue;
            if (OnSand(p)) { if (rnd.NextDouble() < 0.12) dry.Add(p); continue; } // the last sandy spots: only dry tufts
            bool left = p.x < 0f;
            // the left half thins out towards the old desert at the back-left
            float desertness = left ? Mathf.Clamp01((-p.x - 4f) / 7f) * Mathf.Clamp01((p.z + 1f) / 9f) : 0f;
            if (rnd.NextDouble() < desertness * 0.85f) { if (rnd.NextDouble() < 0.15) dry.Add(p); continue; }
            if (left && rnd.NextDouble() < 0.4) continue; // sparser than the forest side: still healing
            grass.Add(p);
        }
        AddCards(life, "Grass", room, grass, 0.6f, 0.48f, 3, rnd, $"{scene}_ending_grass", GroundArt.GrassMat("M_Grass_Blades", "grass_card"));
        AddCards(life, "Dry Tufts", room, dry, 0.45f, 0.36f, 3, rnd, $"{scene}_ending_dry", GroundArt.GrassMat("M_Grass_Dry", "grass_card_dry"));

        var flowers = new List<Vector3>[FlowerColors.Length];
        for (int i = 0; i < flowers.Length; i++) flowers[i] = new List<Vector3>();
        int fn = 0;
        Vector3[] patchCentres =
        {
            new Vector3(4f, 0f, 5f), new Vector3(8.5f, 0f, -2f), new Vector3(9f, 0f, 9f), new Vector3(2.5f, 0f, -2.8f), new Vector3(6f, 0f, 1.5f),
            new Vector3(-2.2f, 0f, 2.5f), new Vector3(-6f, 0f, -2.5f), new Vector3(-2.5f, 0f, -3f),
            new Vector3(-6.3f, 0f, 4.6f) // beside the dead cactus: life coming back
        };
        foreach (var c in patchCentres)
        {
            int main = rnd.Next(FlowerColors.Length);
            for (int k = 0; k < 16; k++)
            {
                float a = (float)rnd.NextDouble() * 6.283f, d = 0.5f + Mathf.Sqrt((float)rnd.NextDouble()) * 1.6f;
                var p = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                if (!Clear(p) || OnSand(p)) continue;
                flowers[rnd.NextDouble() < 0.8 ? main : rnd.Next(FlowerColors.Length)].Add(p);
                fn++;
            }
        }
        for (int c = 0; c < flowers.Length; c++)
            AddCards(life, "Flowers " + c, room, flowers[c], 0.42f, 0.38f, 2, rnd, $"{scene}_ending_flowers_{c}", CardMat("M_Flower_" + c, "flower_" + c));

        // ---------- trees and bushes ----------
        var props = Child(life, "Trees");
        Model(props, "Tree_Round", new Vector3(7.5f, 0f, 8.5f), 0.75f, 20f);
        Model(props, "Tree_Tall", new Vector3(10.5f, 0f, 4f), 0.6f, 140f);
        Model(props, "Tree_Round", new Vector3(4f, 0f, 11f), 0.65f, 260f);
        Model(props, "Tree_Round", new Vector3(-6.5f, 0f, 9.5f), 0.4f, 80f);   // a young tree where the desert was
        Model(props, "Bush", new Vector3(9.5f, 0f, -3.5f), 0.9f, 30f);
        Model(props, "Fern", new Vector3(3.2f, 0f, 2.2f), 1f, 0f);
        Model(props, "Bush", new Vector3(-4f, 0f, 8f), 0.6f, 200f);
        Model(props, "Mushroom_Glow", new Vector3(5.5f, 0f, -1f), 1f, 0f);

        // ---------- butterflies and pollen ----------
        var bf = Child(life, "Butterflies").gameObject.AddComponent<Butterflies>();
        bf.count = 10;
        bf.range = 9f;
        bf.height = new Vector2(0.6f, 2.8f);
        var wings = new Material[WingColors.Length];
        for (int i = 0; i < wings.Length; i++) wings[i] = UnlitCardMat("M_Butterfly_" + i, "wing_" + i);
        bf.wingMaterials = wings;
        bf.bodyMaterial = Lit("M_Butterfly_Body", new Color(0.12f, 0.1f, 0.08f), 0.3f);
        bf.transform.localPosition = new Vector3(0f, 0f, 3f);
        Particles(life, "Pollen", new Vector3(0f, 1.6f, 3f), new Vector3(22f, 3f, 16f), new Color(1f, 0.95f, 0.65f, 0.85f),
                  new Vector2(0.03f, 0.08f), new Vector2(6f, 9f), 18f, Vector3.zero, 0.25f);

        return $"ending room: {grass.Count} grass, {dry.Count} dry tufts, {fn} flowers, 8 trees/bushes, 10 butterflies; ";
    }

    static void AddCards(Transform parent, string name, Transform space, List<Vector3> localPoints, float w, float h, int planes,
                         System.Random rnd, string asset, Material mat)
    {
        if (localPoints.Count == 0) return;
        var world = new List<Vector3>();
        foreach (var p in localPoints) world.Add(space.TransformPoint(p));
        var go = Child(parent, name).gameObject;
        go.transform.position = space.position; // Cards builds relative to 'space'
        go.transform.rotation = space.rotation;
        go.AddComponent<MeshFilter>().sharedMesh = Cards(space, world, w, h, planes, rnd, asset);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    static void Model(Transform parent, string model, Vector3 localPos, float scale, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + model + ".fbx");
        if (!prefab) return;
        var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        g.transform.localScale = Vector3.one * scale;
        foreach (var c in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
    }

    /// <summary>Soft round patch of sand (alpha-blended) lying on the grass.</summary>
    static Material SandPatchMat()
    {
        string tex = $"{TexDir}/sand_patch.png";
        if (!File.Exists(tex))
        {
            const int n = 256;
            var px = new Color[n * n];
            var rnd = new System.Random(606);
            float ox = (float)rnd.NextDouble() * 100f, oy = (float)rnd.NextDouble() * 100f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n - 0.5f, v = y / (float)n - 0.5f, ang = Mathf.Atan2(v, u);
                    float edge = 0.33f + 0.12f * Mathf.PerlinNoise(Mathf.Cos(ang) * 2f + ox, Mathf.Sin(ang) * 2f + oy);
                    float a = Mathf.Clamp01((edge - Mathf.Sqrt(u * u + v * v)) / 0.14f);
                    float g = Mathf.PerlinNoise(x * 0.06f + ox, y * 0.06f + oy);
                    float rip = 0.5f + 0.5f * Mathf.Sin(y * 0.3f + g * 6f);
                    var c = Color.Lerp(new Color(0.76f, 0.67f, 0.52f), new Color(0.88f, 0.8f, 0.66f), g * 0.7f + rip * 0.3f);
                    c *= 0.94f + 0.12f * (float)rnd.NextDouble();
                    c.a = a;
                    px[y * n + x] = c;
                }
            Directory.CreateDirectory(TexDir);
            SaveTex("sand_patch", px);
            AssetDatabase.Refresh();
            var imp = (TextureImporter)AssetImporter.GetAtPath(tex);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
        var m = GetMat("M_Sand_Patch", "Universal Render Pipeline/Lit");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.1f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent - 10;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ================================================================== placement

    class Placement
    {
        readonly Transform world;
        readonly bool desert;
        readonly MemoryRiver[] rivers;
        readonly List<Vector4> blockers = new List<Vector4>(); // xyz + radius

        public Placement(Transform world, bool desert)
        {
            this.world = world;
            this.desert = desert;
            rivers = Object.FindObjectsByType<MemoryRiver>(FindObjectsSortMode.None);
            foreach (var it in Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None))
                if (it is not MemoryRiver) Block(it.transform.position, 3f);
            Block(world.position + new Vector3(0f, 0f, -15f), 3.5f); // spawn
        }

        public void Block(Vector3 p, float r) => blockers.Add(new Vector4(p.x, p.y, p.z, r));

        public bool Random(System.Random rnd, float clearance, out Vector3 ground)
        {
            for (int t = 0; t < 30; t++)
            {
                var p = world.position + new Vector3(((float)rnd.NextDouble() * 2f - 1f) * 18.5f, 0f, ((float)rnd.NextDouble() * 2f - 1f) * 18.5f);
                if (Valid(p, clearance, out ground)) return true;
            }
            ground = default;
            return false;
        }

        public bool Valid(Vector3 p, float clearance, out Vector3 ground)
        {
            ground = default;
            Vector3 l = p - world.position;
            if (Mathf.Abs(l.x) > 19f || Mathf.Abs(l.z) > 19f) return false;
            if (desert && Mathf.Abs(l.x + 6f) < 1.6f + clearance) return false;            // Ancient Path
            if (!Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (!hit.collider.name.StartsWith("Ground") || Mathf.Abs(hit.point.y) > 0.06f) return false;
            foreach (var r in rivers)
            {
                Vector3 rl = r.transform.InverseTransformPoint(hit.point);
                if (rl.z < r.zMin - 2f || rl.z > r.zMax + 2f) continue;
                if (Mathf.Abs(rl.x - RiverWaves.Meander(hit.point.z)) < 2f * RiverWaves.WidthScale(hit.point.z) + 1.2f + clearance) return false;
            }
            foreach (var b in blockers)
                if (new Vector2(b.x - p.x, b.z - p.z).magnitude < b.w + clearance) return false;
            ground = hit.point;
            return true;
        }
    }

    // ================================================================== meshes, particles, materials

    static Mesh Cards(Transform world, List<Vector3> points, float w, float h, int planes, System.Random rnd, string name)
    {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var uv = new List<Vector2>();
        var tris = new List<int>();
        foreach (var gp in points)
        {
            Vector3 c = world.InverseTransformPoint(gp);
            float s = 0.8f + 0.6f * (float)rnd.NextDouble(), yaw = (float)rnd.NextDouble() * 360f;
            for (int q = 0; q < planes; q++)
            {
                Vector3 right = Quaternion.Euler(0f, yaw + q * (180f / planes), 0f) * Vector3.right * (w * s * 0.5f);
                int bi = v.Count;
                v.Add(c - right); v.Add(c + right); v.Add(c + right + Vector3.up * h * s); v.Add(c - right + Vector3.up * h * s);
                for (int j = 0; j < 4; j++) n.Add(Vector3.up);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                tris.AddRange(new[] { bi, bi + 2, bi + 1, bi, bi + 3, bi + 2 });
            }
        }
        var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        string path = $"{MeshDir}/{name}.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old) { old.Clear(); EditorUtility.CopySerialized(m, old); Object.DestroyImmediate(m); return old; }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void Particles(Transform world, string name, Vector3 localPos, Vector3 box, Color color, Vector2 size, Vector2 life,
                          float rate, Vector3 drift, float noise)
    {
        var go = Child(world, name).gameObject;
        go.transform.localPosition = localPos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = color;
        main.maxParticles = 600;
        var em = ps.emission;
        em.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = box;
        if (drift != Vector3.zero)
        {
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(drift.x * 0.7f, drift.x * 1.3f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.15f);
            vel.z = new ParticleSystem.MinMaxCurve(drift.z * 0.7f, drift.z * 1.3f);
        }
        if (noise > 0f)
        {
            var nz = ps.noise;
            nz.enabled = true;
            nz.strength = noise;
            nz.frequency = 0.4f;
        }
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Water_Spray.mat");
        ps.Play();
    }

    static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material mat, bool keepCollider)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        if (!keepCollider) Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    static Material GetMat(string name, string shader)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    static Material Lit(string name, Color c, float smooth)
    {
        var m = GetMat(name, "Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void AlphaCut(Material m)
    {
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.45f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cull", 0f);
        m.doubleSidedGI = true;
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)RenderQueue.AlphaTest;
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
    }

    static Material CardMat(string name, string tex)
    {
        var m = GetMat(name, "Universal Render Pipeline/Lit");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{tex}.png"));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.1f);
        AlphaCut(m);
        return m;
    }

    static Material UnlitCardMat(string name, string tex)
    {
        var m = GetMat(name, "Universal Render Pipeline/Unlit");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{tex}.png"));
        m.SetColor("_BaseColor", Color.white);
        AlphaCut(m);
        return m;
    }

    // ================================================================== textures (wings, flowers)

    static void EnsureTextures()
    {
        if (File.Exists($"{TexDir}/wing_0.png") && File.Exists($"{TexDir}/flower_0.png")) return;
        Directory.CreateDirectory(TexDir);
        for (int i = 0; i < WingColors.Length; i++) SaveTex("wing_" + i, Wing(256, WingColors[i], 40 + i));
        for (int i = 0; i < FlowerColors.Length; i++) SaveTex("flower_" + i, Flower(256, FlowerColors[i], 70 + i));
        AssetDatabase.Refresh();
        foreach (var f in Directory.GetFiles(TexDir, "*.png"))
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(f.Replace('\\', '/'));
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
    }

    static Color[] Wing(int n, Color main, int seed)
    {
        var px = new Color[n * n];
        var rnd = new System.Random(seed);
        var spots = new List<Vector3>();
        for (int i = 0; i < 9; i++) spots.Add(new Vector3(0.45f + 0.5f * (float)rnd.NextDouble(), 0.15f + 0.75f * (float)rnd.NextDouble(), 0.02f + 0.025f * (float)rnd.NextDouble()));
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                // upper lobe + lower lobe, attached at the body (u = 0)
                float up = Ell(u, v, 0.48f, 0.64f, 0.48f, 0.34f), lo = Ell(u, v, 0.34f, 0.3f, 0.32f, 0.26f);
                float inside = Mathf.Max(up, lo);
                if (inside <= 0f) { px[y * n + x] = new Color(main.r, main.g, main.b, 0f); continue; }
                var c = Color.Lerp(main * 0.75f, main, u);
                float border = Mathf.Clamp01((0.22f - inside) / 0.22f);
                c = Color.Lerp(c, new Color(0.06f, 0.05f, 0.05f), Mathf.SmoothStep(0f, 1f, border * 1.4f));
                foreach (var s in spots)
                    if (border > 0.35f && (new Vector2(u, v) - new Vector2(s.x, s.y)).magnitude < s.z) c = new Color(0.97f, 0.96f, 0.92f);
                // veins
                float vein = Mathf.Abs(Mathf.Sin(Mathf.Atan2(v - 0.45f, u) * 9f));
                if (vein < 0.08f && border < 0.5f) c *= 0.55f;
                c.a = 1f;
                px[y * n + x] = c;
            }
        return px;
    }

    static float Ell(float u, float v, float cx, float cy, float rx, float ry)
    {
        float dx = (u - cx) / rx, dy = (v - cy) / ry;
        return 1f - Mathf.Sqrt(dx * dx + dy * dy);
    }

    static Color[] Flower(int n, Color petal, int seed)
    {
        var px = new Color[n * n];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0.3f, 0.5f, 0.2f, 0f);
        var rnd = new System.Random(seed);
        int stems = 4 + rnd.Next(3);
        for (int s = 0; s < stems; s++)
        {
            float bx = n * (0.2f + 0.6f * (float)rnd.NextDouble()), top = n * (0.55f + 0.35f * (float)rnd.NextDouble());
            float bend = n * ((float)rnd.NextDouble() - 0.5f) * 0.15f;
            Color stem = new Color(0.18f, 0.42f, 0.12f, 1f);
            float cx = bx;
            for (int y = 0; y < (int)top; y++)
            {
                float t = y / top;
                cx = bx + bend * t * t;
                for (int x = (int)cx - 2; x <= (int)cx + 2; x++) if (x >= 0 && x < n) px[y * n + x] = stem;
                // a leaf half way up
                if (Mathf.Abs(t - 0.35f) < 0.08f)
                    for (int x = (int)cx; x < (int)cx + (int)(n * 0.08f * (1f - Mathf.Abs(t - 0.35f) / 0.08f)); x++)
                        if (x >= 0 && x < n) px[y * n + x] = new Color(0.25f, 0.55f, 0.18f, 1f);
            }
            // blossom: petals around a centre
            float pr = n * (0.06f + 0.03f * (float)rnd.NextDouble());
            Vector2 c = new Vector2(cx, top);
            for (int p = 0; p < 6; p++)
            {
                float a = p * Mathf.PI / 3f + s;
                Disc(px, n, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * pr, pr * 0.75f, petal * (0.88f + 0.12f * (p % 2)));
            }
            Disc(px, n, c, pr * 0.55f, new Color(1f, 0.78f, 0.15f));
        }
        return px;
    }

    static void Disc(Color[] px, int n, Vector2 c, float r, Color col)
    {
        col.a = 1f;
        for (int y = (int)(c.y - r); y <= (int)(c.y + r); y++)
            for (int x = (int)(c.x - r); x <= (int)(c.x + r); x++)
            {
                if (x < 0 || y < 0 || x >= n || y >= n) continue;
                if ((new Vector2(x, y) - c).magnitude <= r) px[y * n + x] = col;
            }
    }

    static void SaveTex(string name, Color[] px, int n = 256)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes($"{TexDir}/{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}
