using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Kyan > Dress Scene With Models — swaps the simple shapes in the open scene for the team's
/// low-poly models in Assets/TwoWorlds/Models. Matching is by GameObject name. The original object keeps
/// its colliders and scripts (so every puzzle still works); only its renderers are hidden and the model is
/// added as a child named "Model". Running it again does nothing to objects that already have a model.
/// The scene builders call DressOpenScene() at the end, so rebuilt scenes are dressed too.
/// </summary>
public static class ModelDresser
{
    const string Dir = "Assets/TwoWorlds/Models/";

    enum Fit
    {
        Native,      // model at its own size (x scale), standing on the ground at the object's position
        FitGround,   // scaled so its footprint matches the object's width, standing on the ground
        FitCenter,   // scaled to the object's largest size, centred on the object (floating pickups)
    }

    class Rule
    {
        public Func<Transform, bool> match;
        public string[] models;          // one is picked per object (deterministic)
        public Fit fit;
        public float scale = 1f, scaleJitter;
        public bool randomYaw, tintFromSource;
        public string[] extras;          // small plants scattered beside it
    }

    static readonly Rule[] Rules =
    {
        // ---- desert scenery ----
        new Rule { match = t => t.name == "Dead Tree", models = new[] { "DeadTree_Desert" }, fit = Fit.Native, scale = 0.9f, scaleJitter = 0.2f, randomYaw = true, extras = new[] { "DryShrub" } },
        new Rule { match = t => t.name == "Broken Pillar", models = new[] { "Rock_Desert", "Pot_Broken" }, fit = Fit.Native, scale = 1f, scaleJitter = 0.3f, randomYaw = true },
        new Rule { match = t => t.name == "Buried Chair", models = new[] { "Chair_Buried" }, fit = Fit.Native },
        new Rule { match = t => t.name == "Buried Frame", models = new[] { "PictureFrame_Broken" }, fit = Fit.Native, randomYaw = true },

        // ---- forest scenery ----
        new Rule { match = t => t.name == "Tree", models = new[] { "Tree_Round", "Tree_Tall" }, fit = Fit.Native, scale = 0.75f, scaleJitter = 0.25f, randomYaw = true, extras = new[] { "Bush", "Fern", "GrassTuft" } },
        new Rule { match = t => t.name == "Glow Plant", models = new[] { "GlowPlant", "Mushroom_Glow" }, fit = Fit.Native, scale = 0.8f, scaleJitter = 0.2f, randomYaw = true },
        new Rule { match = t => t.name == "Stem" && t.parent && t.parent.name == "Props", models = null },  // hide only
        new Rule { match = t => t.name == "Rock" || t.name == "Spring Rocks", models = new[] { "Rock_Mossy" }, fit = Fit.FitGround, randomYaw = true },

        // ---- puzzle objects ----
        new Rule { match = t => t.name.StartsWith("Sand Mound"), models = new[] { "SandMound_Large" }, fit = Fit.FitGround, tintFromSource = true },
        new Rule { match = t => t.name == "Memory Seed", models = new[] { "MemorySeed" }, fit = Fit.FitCenter, scale = 1.2f },
        new Rule { match = t => t.name == "Mirror Fragment", models = new[] { "MirrorFragment" }, fit = Fit.FitCenter, scale = 1.2f },
        new Rule { match = t => t.name == "Picture Frame", models = new[] { "PictureFrame_Broken" }, fit = Fit.FitCenter, scale = 1.1f },
        new Rule { match = t => t.name == "Fragment Altar" || t.name.StartsWith("Altar (") || t.name == "Fragment Altar (forest)", models = new[] { "FragmentShrine" }, fit = Fit.FitGround, scale = 1.7f },
        new Rule { match = t => t.name == "Spring Gate", models = new[] { "WaterGate" }, fit = Fit.FitGround },
    };

    [MenuItem("Tools/Kyan/Dress Scene With Models")]
    static void DressFromMenu()
    {
        int n = DressOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[ModelDresser] Dressed {n} objects. Save the scene to keep it.");
    }

    /// <summary>Dresses every matching object in the open scene. Returns how many were dressed.</summary>
    public static int DressOpenScene()
    {
        int count = 0;
        var all = new List<Transform>();
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            all.AddRange(root.GetComponentsInChildren<Transform>(true));

        foreach (var t in all)
        {
            if (!t || t.Find("Model")) continue;
            foreach (var rule in Rules)
            {
                if (!rule.match(t)) continue;
                if (Dress(t, rule)) count++;
                break;
            }
        }
        return count;
    }

    static bool Dress(Transform t, Rule rule)
    {
        // Size of the shape we are replacing (all its renderers).
        var rends = t.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return false;
        Bounds src = rends[0].bounds;
        foreach (var r in rends) src.Encapsulate(r.bounds);
        foreach (var r in rends) r.enabled = false;
        if (rule.models == null) return true; // hide-only rule

        uint seed = Hash(t.position);
        string modelName = rule.models[seed % (uint)rule.models.Length];
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + modelName + ".fbx");
        if (!prefab)
        {
            foreach (var r in rends) r.enabled = true;
            Debug.LogWarning($"[ModelDresser] Model '{modelName}' not found.");
            return false;
        }

        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        model.name = "Model";
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        float yaw = rule.randomYaw ? (seed % 360u) : 0f;
        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f) * t.rotation);
        Bounds mb = ModelBounds(model);

        float jitter = rule.scaleJitter > 0f ? 1f + rule.scaleJitter * (((seed >> 8) % 1000u) / 500f - 1f) : 1f;
        float s = rule.scale * jitter;
        if (rule.fit == Fit.FitGround)
            s *= Mathf.Max(src.size.x, src.size.z) / Mathf.Max(mb.size.x, mb.size.z, 0.01f);
        else if (rule.fit == Fit.FitCenter)
            s *= Mathf.Max(src.size.x, src.size.y, src.size.z) / Mathf.Max(mb.size.x, mb.size.y, mb.size.z, 0.01f);

        model.transform.localScale = Vector3.one * s;
        mb = ModelBounds(model); // measured at the origin, after scaling

        Vector3 pos;
        if (rule.fit == Fit.FitCenter)
            pos = src.center - mb.center;                                    // centred on the old shape
        else if (rule.fit == Fit.FitGround)
            pos = new Vector3(src.center.x - mb.center.x,                   // standing on the ground (or on the
                              Mathf.Max(0f, src.min.y) - mb.min.y,           // old shape's base if that is higher)
                              src.center.z - mb.center.z);
        else
            pos = new Vector3(t.position.x, 0f, t.position.z);             // native size, pivot on the ground
        model.transform.position = pos;
        model.transform.SetParent(t, true);

        if (rule.tintFromSource)
        {
            var src0 = t.GetComponent<Renderer>();
            if (src0)
            {
                var tint = t.gameObject.AddComponent<ModelTint>();
                tint.source = src0;
                tint.targets = model.GetComponentsInChildren<Renderer>(true);
            }
        }

        if (rule.extras != null && rule.extras.Length > 0 && t.parent)
        {
            string extraName = rule.extras[(seed >> 4) % (uint)rule.extras.Length];
            var ep = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + extraName + ".fbx");
            if (ep)
            {
                var extra = (GameObject)PrefabUtility.InstantiatePrefab(ep);
                extra.name = "Model";
                foreach (var c in extra.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
                float a = (seed % 628u) / 100f;
                extra.transform.SetPositionAndRotation(new Vector3(t.position.x + Mathf.Cos(a) * 1.8f, 0f, t.position.z + Mathf.Sin(a) * 1.8f),
                                                       Quaternion.Euler(0f, (seed >> 3) % 360u, 0f));
                extra.transform.localScale = Vector3.one * (0.8f + ((seed >> 12) % 40u) / 100f);
                var holder = new GameObject(extraName + " (extra)").transform;
                holder.SetParent(t.parent, true);
                holder.position = extra.transform.position;
                extra.transform.SetParent(holder, true);
            }
        }
        return true;
    }

    static Bounds ModelBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static uint Hash(Vector3 p)
    {
        unchecked
        {
            uint h = 2166136261;
            h = (h ^ (uint)Mathf.RoundToInt(p.x * 10f)) * 16777619;
            h = (h ^ (uint)Mathf.RoundToInt(p.y * 10f)) * 16777619;
            h = (h ^ (uint)Mathf.RoundToInt(p.z * 10f)) * 16777619;
            h ^= h >> 13;
            return h;
        }
    }
}
