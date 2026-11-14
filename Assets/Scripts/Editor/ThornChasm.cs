using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Puzzle 1: the chasm is no longer a bottomless hole. It is a shallow ditch full of thorns (brambles)
/// in both worlds, all along the canyon. Stepping into it hurts and puts the player back at the start,
/// like falling did, so the bridge is still the only way across.
/// </summary>
public static partial class BridgePuzzleBuilder
{
    const float ThornBedY = -1.3f;          // top of the ditch floor
    const float ThornReach = 148f;          // as far as WorldExtender's open land
    const float ThornChunk = 12f;
    const int ThornVariants = 3;

    [MenuItem("Tools/Kyan/Fill the chasm with thorns (open scene)")]
    public static void FillChasmWithThorns()
    {
        var meshes = ThornMeshes();
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            bool forest = root.name == "--- FOREST (Future) ---";
            if (!forest && root.name != "--- DESERT (Present) ---") continue;
            var world = root.transform;
            var floor = world.Find(forest ? "Gap Floor" : "Chasm Floor");
            if (!floor) continue;

            // raise the floor into a solid ditch bottom
            floor.localPosition = new Vector3(0f, ThornBedY - 0.5f, (ChasmNear + ChasmFar) * 0.5f);
            floor.localScale = new Vector3(80f, 1f, ChasmFar - ChasmNear);
            if (!floor.GetComponent<BoxCollider>()) floor.gameObject.AddComponent<BoxCollider>();
            floor.GetComponent<Renderer>().sharedMaterial = ThornMat(forest ? "M_Thorn_Ground_Forest" : "M_Thorn_Ground_Desert",
                forest ? new Color(0.17f, 0.13f, 0.09f) : new Color(0.3f, 0.23f, 0.16f));

            var old = world.Find("Thorns");
            if (old) Object.DestroyImmediate(old.gameObject);
            var thorns = new GameObject("Thorns").transform;
            thorns.SetParent(world, false);
            var mat = ThornMat(forest ? "M_Thorn_Forest" : "M_Thorn_Desert",
                forest ? new Color(0.2f, 0.14f, 0.11f) : new Color(0.24f, 0.18f, 0.13f));
            int i = forest ? 1 : 0;
            for (float x = -ThornReach + ThornChunk * 0.5f; x < ThornReach; x += ThornChunk, i++)
            {
                var g = new GameObject("Thorn Patch");
                g.transform.SetParent(thorns, false);
                g.transform.localPosition = new Vector3(x, ThornBedY, (ChasmNear + ChasmFar) * 0.5f);
                g.transform.localRotation = Quaternion.Euler(0f, (i % 2) * 180f, 0f);
                g.AddComponent<MeshFilter>().sharedMesh = meshes[i % ThornVariants];
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.On;
                GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
            }
            EditorUtility.SetDirty(world.gameObject);
        }

        // touching the thorns = hurt + back to start (no long fall any more, so no whoosh)
        foreach (var resp in Object.FindObjectsByType<PlayerRespawn>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            resp.killY = ThornBedY + 0.35f;
            resp.fallY = -50f;
            EditorUtility.SetDirty(resp);
        }
    }

    static Material ThornMat(string name, Color c)
    {
        string path = "Assets/Materials/World/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.08f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh[] ThornMeshes()
    {
        const string folder = "Assets/Models/Generated";
        if (!AssetDatabase.IsValidFolder("Assets/Models")) AssetDatabase.CreateFolder("Assets", "Models");
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Models", "Generated");
        var result = new Mesh[ThornVariants];
        for (int v = 0; v < ThornVariants; v++)
        {
            string path = $"{folder}/Thorns_{v}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool fresh = !mesh;
            if (fresh) mesh = new Mesh { name = "Thorns_" + v };
            BuildThornMesh(mesh, 1000 + v * 37);
            if (fresh) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
            result[v] = mesh;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    /// <summary>Bramble clumps: thick stems leaning every way, each with small side thorns.</summary>
    static void BuildThornMesh(Mesh mesh, int seed)
    {
        var rnd = new System.Random(seed);
        float R() => (float)rnd.NextDouble();
        var verts = new List<Vector3>();
        var tris = new List<int>();
        float halfZ = (ChasmFar - ChasmNear) * 0.5f - 0.15f, halfX = ThornChunk * 0.5f;

        for (float x = -halfX; x < halfX; x += 0.55f)
            for (float z = -halfZ; z < halfZ; z += 0.55f)
            {
                var c = new Vector3(x + R() * 0.5f, 0f, z + R() * 0.5f);
                if (Mathf.Abs(c.z) > halfZ) continue;
                int stems = 3 + rnd.Next(4);
                for (int s = 0; s < stems; s++)
                {
                    float yaw = R() * Mathf.PI * 2f, tilt = Mathf.Lerp(8f, 58f, R()) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(yaw) * Mathf.Sin(tilt), Mathf.Cos(tilt), Mathf.Sin(yaw) * Mathf.Sin(tilt));
                    float len = Mathf.Lerp(0.45f, 1.0f, R());
                    if (dir.y * len > 0.98f) len = 0.98f / dir.y; // tips stay below the bridge
                    float rad = Mathf.Lerp(0.035f, 0.06f, R());
                    Cone(verts, tris, c, dir, len, rad);
                    // side thorns along the stem
                    for (float t = 0.25f; t < 0.9f; t += 0.18f + R() * 0.1f)
                    {
                        var p = c + dir * (len * t);
                        var side = Vector3.Cross(dir, new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f)).normalized;
                        Cone(verts, tris, p, (side + dir * 0.4f).normalized, Mathf.Lerp(0.09f, 0.17f, R()), rad * 0.45f);
                    }
                }
            }

        mesh.Clear();
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    static void Cone(List<Vector3> verts, List<int> tris, Vector3 basePos, Vector3 dir, float len, float rad)
    {
        const int sides = 5;
        var a = Vector3.Cross(dir, Vector3.right);
        if (a.sqrMagnitude < 0.01f) a = Vector3.Cross(dir, Vector3.forward);
        a.Normalize();
        var b = Vector3.Cross(dir, a);
        var tip = basePos + dir * len;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            var p0 = basePos + (a * Mathf.Cos(a0) + b * Mathf.Sin(a0)) * rad;
            var p1 = basePos + (a * Mathf.Cos(a1) + b * Mathf.Sin(a1)) * rad;
            int n = verts.Count;
            verts.Add(p0); verts.Add(tip); verts.Add(p1); // flat-shaded faces
            tris.Add(n); tris.Add(n + 1); tris.Add(n + 2);
        }
    }
}
