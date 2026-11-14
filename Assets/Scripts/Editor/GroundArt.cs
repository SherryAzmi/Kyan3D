using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tools > Kyan > Realistic Ground (open scene)
///  - Desert ground and dunes get the textured sand (grains, wind ripples, normal map, large-scale variation).
///  - Forest ground gets the textured forest floor, plus thousands of 3D grass clumps (alpha-cut blade cards).
///  - A few dry grass tufts in the desert.
///  - Sand / grass footstep sounds on both players.
/// Ground meshes get world-space UVs (one texture tile every few metres, never stretched).
/// Safe to run again (old grass fields are replaced). The Bridge and Dry Bed builders call it too.
/// </summary>
public static class GroundArt
{
    const string MatDir = "Assets/Materials/World";
    const string MeshDir = "Assets/Meshes/Generated";
    const float SandTile = 5f, ForestTile = 4f;

    [MenuItem("Tools/Kyan/Realistic Ground (open scene)")]
    static void FromMenu()
    {
        ApplyOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[GroundArt] Done. Save the scene to keep it.");
    }

    public static string ApplyOpenScene()
    {
        if (!GroundTextures.Load("sand_albedo")) GroundTextures.GenerateAll();
        if (!SoundSynth.Load("sfx_step_sand_1")) SoundSynth.GenerateAll();
        Directory.CreateDirectory(MeshDir);
        string scene = EditorSceneManager.GetActiveScene().name;

        var sand = SandMat();
        var forest = ForestMat();
        var grass = GrassMat("M_Grass_Blades", "grass_card");
        var dryGrass = GrassMat("M_Grass_Dry", "grass_card_dry");

        int grounds = 0;
        Physics.SyncTransforms();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            string n = r.name;
            bool isForest = r.transform.position.x > 150f && r.transform.position.x < 450f;
            if (n == "Ground" || n == "Ground South" || n == "Ground North")
            {
                if (!InWorld(r.transform)) continue;
                r.GetComponent<MeshFilter>().sharedMesh = TopMesh(r.transform, isForest ? ForestTile : SandTile, $"{scene}_{(isForest ? "forest" : "desert")}_{n}");
                r.sharedMaterial = isForest ? forest : sand;
                grounds++;
            }
            else if (n == "Dune" && InWorld(r.transform)) r.sharedMaterial = sand;
        }

        int clumps = 0;
        // The desert heals level by level (same levels as DesertMood): fewer dry tufts, more green grass.
        int level = Level(scene);
        int dryCount = level == 1 ? 260 : level == 2 ? 150 : 60;
        int greenCount = level == 1 ? 0 : level == 2 ? 150 : 1100;
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "--- FOREST (Future) ---") clumps += Scatter(root.transform, grass, 2600, 1f, false, $"{scene}_forest_grass");
            else if (root.name == "--- DESERT (Present) ---")
            {
                clumps += Scatter(root.transform, dryGrass, dryCount, 0.75f, true, $"{scene}_desert_grass");
                var oldGreen = root.transform.Find("Grass Field (green)");
                if (oldGreen) Object.DestroyImmediate(oldGreen.gameObject);
                if (greenCount > 0)
                {
                    clumps += Scatter(root.transform, grass, greenCount, 0.85f, true, $"{scene}_desert_green", "Grass Field (green)");
                }
            }
        }

        int feet = 0;
        foreach (var name in new[] { "Present_P1", "Future_P2" })
        {
            var p = GameObject.Find(name);
            if (!p) continue;
            if (!p.GetComponent<Footsteps>()) p.AddComponent<Footsteps>();
            if (!p.GetComponent<BoundaryWarning>()) p.AddComponent<BoundaryWarning>();
            feet++;
        }
        var ga = Object.FindAnyObjectByType<GameAudio>();
        if (ga)
        {
            ga.sandSteps = new[] { SoundSynth.Load("sfx_step_sand_1"), SoundSynth.Load("sfx_step_sand_2"), SoundSynth.Load("sfx_step_sand_3"), SoundSynth.Load("sfx_step_sand_4") };
            ga.grassSteps = new[] { SoundSynth.Load("sfx_step_grass_1"), SoundSynth.Load("sfx_step_grass_2"), SoundSynth.Load("sfx_step_grass_3"), SoundSynth.Load("sfx_step_grass_4") };
            EditorUtility.SetDirty(ga);
        }
        AssetDatabase.SaveAssets();
        return $"grounds={grounds} grassClumps={clumps} footsteps={feet} audio={(ga ? "ok" : "none")}";
    }

    static bool InWorld(Transform t)
    {
        for (; t; t = t.parent)
            if (t.name == "--- DESERT (Present) ---" || t.name == "--- FOREST (Future) ---") return true;
        return false;
    }

    // ================================================================== ground mesh with world UVs

    public static Mesh TopMesh(Transform t, float tile, string assetName)
    {
        const int div = 8;
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var tris = new List<int>();
        for (int z = 0; z <= div; z++)
            for (int x = 0; x <= div; x++)
            {
                var local = new Vector3(x / (float)div - 0.5f, 0.5f, z / (float)div - 0.5f);
                var w = t.TransformPoint(local);
                v.Add(local);
                uv.Add(new Vector2(w.x / tile, w.z / tile));
            }
        for (int z = 0; z < div; z++)
            for (int x = 0; x < div; x++)
            {
                int i = z * (div + 1) + x;
                tris.AddRange(new[] { i, i + div + 1, i + 1, i + 1, i + div + 1, i + div + 2 });
            }
        var m = new Mesh { name = assetName };
        m.SetVertices(v);
        m.SetUVs(0, uv);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateTangents();
        m.RecalculateBounds();
        return SaveMesh(m, assetName);
    }

    static Mesh SaveMesh(Mesh m, string assetName)
    {
        string path = $"{MeshDir}/{assetName.Replace(' ', '_')}.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old)
        {
            old.Clear();
            EditorUtility.CopySerialized(m, old);
            Object.DestroyImmediate(m);
            EditorUtility.SetDirty(old);
            return old;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ================================================================== grass

    /// <summary>Desert healing level from the scene, same as DesertMood: Bridge 1, Puzzle2 2, Puzzle3 3.</summary>
    public static int Level(string scene) => scene == "Puzzle2" ? 2 : scene == "Puzzle3" ? 3 : 1;

    static int Scatter(Transform world, Material mat, int attempts, float sizeScale, bool desert, string assetName, string objectName = "Grass Field")
    {
        var oldField = world.Find(objectName);
        if (oldField) Object.DestroyImmediate(oldField.gameObject);

        var rivers = Object.FindObjectsByType<MemoryRiver>(FindObjectsSortMode.None);
        var blockers = new List<Vector3>();
        foreach (var it in Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            if (it is not MemoryRiver) blockers.Add(it.transform.position);

        var rnd = new System.Random((desert ? 5 : 6) + objectName.Length * 31); // each field gets its own spots
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var uv = new List<Vector2>();
        var tris = new List<int>();
        int placed = 0;
        for (int k = 0; k < attempts; k++)
        {
            float lx = ((float)rnd.NextDouble() * 2f - 1f) * 26f, lz = ((float)rnd.NextDouble() * 2f - 1f) * 26f;
            Vector3 p = world.position + new Vector3(lx, 0f, lz);
            if (desert && Mathf.Abs(lx + 6f) < 1.3f) continue;                       // keep the Ancient Path clear
            if (!Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!hit.collider.name.StartsWith("Ground") || Mathf.Abs(hit.point.y) > 0.06f) continue; // not on a prop, not in the chasm
            if (NearRiver(rivers, hit.point)) continue;
            bool blocked = false;
            foreach (var b in blockers)
                if (new Vector2(b.x - p.x, b.z - p.z).sqrMagnitude < 2.4f * 2.4f) { blocked = true; break; }
            if (blocked) continue;

            float s = sizeScale * (0.7f + 0.6f * (float)rnd.NextDouble());
            float w = 0.6f * s, h = 0.48f * s;
            float yaw = (float)rnd.NextDouble() * 360f;
            Vector3 c = world.InverseTransformPoint(hit.point);
            for (int q = 0; q < 3; q++)
            {
                var rot = Quaternion.Euler(0f, yaw + q * 60f, 0f);
                Vector3 right = rot * Vector3.right * (w * 0.5f);
                int bi = v.Count;
                v.Add(c - right); v.Add(c + right); v.Add(c + right + Vector3.up * h); v.Add(c - right + Vector3.up * h);
                for (int j = 0; j < 4; j++) n.Add(Vector3.up); // soft, even lighting like real grass
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                tris.AddRange(new[] { bi, bi + 2, bi + 1, bi, bi + 3, bi + 2 });
            }
            placed++;
        }

        var m = new Mesh { name = assetName, indexFormat = IndexFormat.UInt32 };
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetUVs(0, uv);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        m = SaveMesh(m, assetName);

        var go = new GameObject(objectName);
        go.transform.SetParent(world, false);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        return placed;
    }

    static bool NearRiver(MemoryRiver[] rivers, Vector3 p)
    {
        foreach (var r in rivers)
        {
            Vector3 l = r.transform.InverseTransformPoint(p);
            if (l.z < r.zMin - 1.5f || l.z > r.zMax + 1.5f) continue;
            float half = 2f * RiverWaves.WidthScale(p.z) + 1.1f;
            if (Mathf.Abs(l.x - RiverWaves.Meander(p.z)) < half) return true;
        }
        return false;
    }

    // ================================================================== materials

    static Material Get(string name)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    static Material Ground(string name, string albedo, string normal, Color tint, float smooth, float bump)
    {
        var m = Get(name);
        m.SetTexture("_BaseMap", GroundTextures.Load(albedo));
        m.SetColor("_BaseColor", tint);
        m.SetTexture("_BumpMap", GroundTextures.Load(normal));
        m.SetFloat("_BumpScale", bump);
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        // large-scale light/dark patches so the tiling doesn't show
        m.SetTexture("_DetailAlbedoMap", GroundTextures.Load("macro_variation"));
        m.SetTextureScale("_DetailAlbedoMap", new Vector2(0.18f, 0.18f));
        m.SetFloat("_DetailAlbedoMapScale", 1f);
        m.EnableKeyword("_DETAIL_MULX2");
        EditorUtility.SetDirty(m);
        return m;
    }

    public static Material SandMat() => Ground("M_Ground_Sand", "sand_albedo", "sand_normal", Color.white, 0.12f, 1.1f);
    public static Material ForestMat() => Ground("M_Ground_Forest", "forest_albedo", "forest_normal", Color.white, 0.08f, 0.8f);

    public static Material GrassMat(string name, string card)
    {
        var m = Get(name);
        m.SetTexture("_BaseMap", GroundTextures.Load(card));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.05f);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.45f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cull", 0f); // both sides
        m.doubleSidedGI = true;
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)RenderQueue.AlphaTest;
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }
}
