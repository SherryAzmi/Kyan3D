using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// River state per level (the water follows the story):
///  - Puzzle 1 (Bridge): the desert half of the Memory River is dry (empty riverbed, no waterfalls);
///    the forest half still flows. The seed still crosses as before.
///  - Puzzle 2: both halves start dry and fill when the Future opens the gate (DryBedPuzzle).
///  - Puzzle 3: the water is back: a decorative flowing river with its waterfall in both worlds,
///    along the west side (it can be touched, but it doesn't carry the mirror shards).
/// </summary>
public static partial class BridgePuzzleBuilder
{
    const float Puzzle3RiverX = -14f;

    [MenuItem("Tools/Kyan/Rivers/Dry the desert river (open scene)")]
    public static void DryDesertRiver()
    {
        var dryBed = DryBedMaterial();
        foreach (var r in Object.FindObjectsByType<MemoryRiver>(FindObjectsSortMode.None))
        {
            if (r.transform.position.x >= 150f || !r.water) continue; // forest keeps its water
            r.water.enabled = false;
            var bed = r.transform.Find("River Bed");
            if (bed) bed.GetComponent<Renderer>().sharedMaterial = dryBed;
            EditorUtility.SetDirty(r.water);
            EditorUtility.SetDirty(r);
        }
    }

    [MenuItem("Tools/Kyan/Rivers/Add Puzzle 3 rivers (open scene)")]
    public static void AddPuzzle3Rivers()
    {
        MakeMaterials();
        MemoryRiver desertRiver = null, forestRiver = null;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            bool forest = root.name == "--- FOREST (Future) ---";
            if (!forest && root.name != "--- DESERT (Present) ---") continue;
            var world = root.transform;
            var old = world.Find("Memory River");
            if (old) Object.DestroyImmediate(old.gameObject);
            ClearRiverLane(world);
            var mr = DecorRiver(world, forest ? mForestWater : mDesertWater);
            if (forest) forestRiver = mr; else desertRiver = mr;
        }
        if (desertRiver && forestRiver)
        {
            desertRiver.other = forestRiver;
            forestRiver.other = desertRiver;
        }
        foreach (var mr in new[] { desertRiver, forestRiver })
            if (mr) RiverUpgrade.Upgrade(mr);
    }

    static MemoryRiver DecorRiver(Transform world, Material water)
    {
        var river = Child(world, "Memory River");
        river.localPosition = new Vector3(Puzzle3RiverX, 0f, 0f);
        // the whole length of the play area (RiverUpgrade starts it at the source waterfall)
        var strip = Box("Water", river, new Vector3(0f, 0.02f, -10.25f), new Vector3(4f, 0.04f, 59.5f), water, false);
        strip.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        for (float z = -19f; z <= 18.5f; z += 2.2f)
        {
            Box("Bank Stone", river, new Vector3(-2.2f, 0.08f, z + Mathf.Sin(z) * 0.4f), new Vector3(0.5f, 0.2f, 0.7f), mStone, false);
            Box("Bank Stone", river, new Vector3(2.2f, 0.08f, z + Mathf.Cos(z) * 0.4f), new Vector3(0.5f, 0.2f, 0.7f), mStone, false);
        }
        var mr = river.gameObject.AddComponent<MemoryRiver>();
        mr.range = 1.6f;
        mr.zMin = -20f;
        mr.zMax = 19f;
        mr.halfWidth = 2f;
        mr.rippleMaterial = mRipple;
        mr.water = strip.GetComponent<Renderer>();
        mr.canCarry = false; // decoration: the shards must not float away
        return mr;
    }

    /// <summary>Removes scenery that would stand in the new river.</summary>
    static void ClearRiverLane(Transform world)
    {
        foreach (var props in new[] { world.Find("Props") })
        {
            if (!props) continue;
            for (int i = props.childCount - 1; i >= 0; i--)
            {
                var c = props.GetChild(i);
                if (Mathf.Abs(c.localPosition.x - Puzzle3RiverX) < 4.8f && Mathf.Abs(c.localPosition.z) < 21f)
                    Object.DestroyImmediate(c.gameObject);
            }
        }
    }

    static Material DryBedMaterial()
    {
        const string path = "Assets/Materials/World/M_RiverBed_Dry.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", new Color(0.5f, 0.46f, 0.4f));
            m.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }
}
