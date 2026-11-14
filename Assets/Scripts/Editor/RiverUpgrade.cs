using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tools > Kyan > Upgrade Rivers (open scene) — turns every Memory River in the open scene into a natural
/// stream: curved water with the Kyan/Water shader, a curved wet river bed, the bank stones moved onto the
/// curved banks (and swapped for rock models by ModelDresser), a waterfall with a rocky cliff at the river's
/// source, and (Bridge scene) a real waterfall pouring into the chasm.
/// Safe to run again. The scene builders call Upgrade() too.
/// </summary>
public static class RiverUpgrade
{
    const string MatDir = "Assets/Materials/World";
    const string ModelDir = "Assets/TwoWorlds/Models/";

    [MenuItem("Tools/Kyan/Upgrade Rivers (open scene)")]
    static void FromMenu()
    {
        int n = UpgradeOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[RiverUpgrade] Upgraded {n} rivers. Save the scene to keep it.");
    }

    public static int UpgradeOpenScene()
    {
        int n = 0;
        foreach (var r in Object.FindObjectsByType<MemoryRiver>(FindObjectsSortMode.None)) { Upgrade(r); n++; }
        ModelDresser.DressOpenScene();
        return n;
    }

    public static void Upgrade(MemoryRiver mr)
    {
        bool forest = mr.transform.position.x > 150f;
        Transform river = mr.transform, world = river.parent;

        var water = Water(forest);
        var falls = Falls(forest);
        var spray = Spray();

        // ---------- the water ----------
        if (mr.water)
        {
            var w = mr.water.transform;
            // The Bridge river started 20 m behind the play area; it now starts at the source waterfall.
            float minZ = w.localPosition.z - w.localScale.z * 0.5f;
            if (minZ < -20.5f && !river.Find("Dry Bed"))
            {
                float maxZ = w.localPosition.z + w.localScale.z * 0.5f;
                float start = -19.6f;
                w.localPosition = new Vector3(w.localPosition.x, w.localPosition.y, (start + maxZ) * 0.5f);
                w.localScale = new Vector3(w.localScale.x, w.localScale.y, maxZ - start);
            }
            mr.water.sharedMaterial = water;
            if (!w.GetComponent<RiverWaves>()) w.gameObject.AddComponent<RiverWaves>();
        }
        if (!river.GetComponent<RiverWading>()) river.gameObject.AddComponent<RiverWading>();
        mr.bankX = -3.8f; // beyond the widest bend, so objects sent across land on dry ground
        EditorUtility.SetDirty(mr);

        // ---------- the bed ----------
        var bed = river.Find("Dry Bed");
        if (!bed) bed = river.Find("River Bed");
        if (!bed && mr.water)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.name = "River Bed";
            bed = g.transform;
            bed.SetParent(river, false);
            var w = mr.water.transform;
            bed.localPosition = new Vector3(0f, 0.004f, w.localPosition.z);
            bed.localScale = new Vector3(w.localScale.x, 0.008f, w.localScale.z + 0.6f);
            g.GetComponent<Renderer>().sharedMaterial = Lit(forest ? "M_RiverBed_Forest" : "M_RiverBed_Desert",
                forest ? new Color(0.24f, 0.19f, 0.13f) : new Color(0.45f, 0.38f, 0.29f), 0.45f);
            g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        if (bed && !bed.GetComponent<RiverRibbon>()) bed.gameObject.AddComponent<RiverRibbon>();

        // ---------- bank stones onto the curved banks ----------
        foreach (Transform c in river)
        {
            if (c.name != "Bank Stone") continue;
            float wz = c.position.z;
            float side = Mathf.Sign(c.localPosition.x);
            float x = side * (2f * RiverWaves.WidthScale(wz) + 0.45f) + RiverWaves.Meander(wz);
            c.localPosition = new Vector3(x, c.localPosition.y, c.localPosition.z);
            c.name = "Bank Stone (river)";
        }

        // ---------- source waterfall + cliff ----------
        if (!river.Find("Source Falls"))
        {
            float z = -19.9f, h = forest ? 3.4f : 2.4f, wdt = forest ? 3.2f : 1.6f;
            float mx = RiverWaves.Meander(river.position.z + z);
            var root = new GameObject("Source Falls").transform;
            root.SetParent(river, false);
            root.localPosition = new Vector3(mx, h, z);

            string rock = forest ? "Rock_Mossy" : "Rock_Desert";
            Rock(river, rock, new Vector3(mx, 0f, z - 1.2f), (h + 0.3f) / 1.2f, 0f);
            Rock(river, rock, new Vector3(mx - wdt * 0.5f - 1.5f, 0f, z - 0.3f), (h * 0.75f) / 1.2f, 40f);
            Rock(river, rock, new Vector3(mx + wdt * 0.5f + 1.5f, 0f, z - 0.3f), (h * 0.8f) / 1.2f, -35f);

            var wf = root.gameObject.AddComponent<Waterfall>();
            wf.width = wdt;
            wf.height = h - 0.08f;
            wf.outward = 0.45f;
            wf.material = falls;
            wf.feed = mr.water;
            wf.spray = Particles(root, "Spray", new Vector3(0f, -h + 0.1f, 0.6f), wdt, spray, false);
            wf.mist = Particles(root, "Mist", new Vector3(0f, -h + 0.3f, 0.8f), wdt, spray, true);
        }

        // ---------- Bridge: the waterfall into the chasm ----------
        var oldFall = world ? world.Find("Waterfall") : null;
        if (oldFall && !world.Find("Chasm Falls"))
        {
            var r = oldFall.GetComponent<Renderer>();
            if (r) r.enabled = false;
            float lipZ = oldFall.localPosition.z - 0.1f;
            float mx = RiverWaves.Meander(river.position.z + lipZ);
            var root = new GameObject("Chasm Falls").transform;
            root.SetParent(world, false);
            root.localPosition = new Vector3(river.localPosition.x + mx, 0.14f, lipZ);
            var wf = root.gameObject.AddComponent<Waterfall>();
            wf.width = 4f * RiverWaves.WidthScale(river.position.z + lipZ) * 0.9f;
            wf.height = 12f;
            wf.outward = 1.1f;
            wf.material = falls;
            wf.feed = mr.water;
            wf.hearDistance = 20f;
            wf.mist = Particles(root, "Mist", new Vector3(0f, -9f, 1.6f), wf.width * 1.5f, spray, true);
            wf.spray = Particles(root, "Lip Spray", new Vector3(0f, -0.1f, 0.3f), wf.width, spray, false);
        }
    }

    // ================================================================== pieces

    static void Rock(Transform parent, string model, Vector3 localPos, float scale, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + model + ".fbx");
        if (!prefab) return;
        var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        g.name = "Falls Rock";
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        g.transform.localScale = Vector3.one * scale;
        foreach (var c in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
    }

    static ParticleSystem Particles(Transform parent, string name, Vector3 localPos, float width, Material mat, bool isMist)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // emit upwards
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = isMist ? 120 : 500;
        if (isMist)
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startColor = new Color(1f, 1f, 1f, 0.13f);
            main.gravityModifier = -0.02f;
        }
        else
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.4f);
            main.startColor = new Color(1f, 1f, 1f, 0.55f);
            main.gravityModifier = 0.7f;
        }
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        var em = ps.emission;
        em.rateOverTime = isMist ? 6f * width : 22f * width;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = isMist ? 35f : 22f;
        shape.radius = width * 0.45f;
        shape.radiusThickness = 1f;
        shape.scale = new Vector3(1f, 0.35f, 1f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, isMist ? 0.6f : 0.8f, 1f, isMist ? 1.6f : 1.3f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
        return ps;
    }

    // ================================================================== materials

    static Material Get(string name, Shader shader)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader) m.shader = shader;
        return m;
    }

    static Material Water(bool forest)
    {
        var m = Get(forest ? "M_Water_Forest" : "M_Water_Desert", Shader.Find("Kyan/Water"));
        if (forest)
        {
            m.SetColor("_BaseColor", new Color(0.25f, 0.78f, 0.92f, 0.66f));
            m.SetColor("_DeepColor", new Color(0.04f, 0.36f, 0.5f, 0.9f));
            m.SetColor("_FoamColor", new Color(1f, 1f, 1f, 0.85f));
            m.SetFloat("_FlowSpeed", 0.75f);
            m.SetFloat("_NormalStrength", 1.1f);
            m.SetFloat("_Smoothness", 0.93f);
            m.SetFloat("_EdgeFoam", 0.14f);
        }
        else
        {
            m.SetColor("_BaseColor", new Color(0.42f, 0.5f, 0.56f, 0.72f));
            m.SetColor("_DeepColor", new Color(0.18f, 0.25f, 0.3f, 0.92f));
            m.SetColor("_FoamColor", new Color(0.88f, 0.86f, 0.8f, 0.55f));
            m.SetFloat("_FlowSpeed", 0.45f);
            m.SetFloat("_NormalStrength", 0.8f);
            m.SetFloat("_Smoothness", 0.88f);
            m.SetFloat("_EdgeFoam", 0.1f);
        }
        m.SetFloat("_RippleScale", 1.1f);
        m.SetFloat("_Streaks", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Falls(bool forest)
    {
        var src = Water(forest);
        var m = Get(forest ? "M_Falls_Forest" : "M_Falls_Desert", src.shader);
        m.CopyPropertiesFromMaterial(src);
        m.SetFloat("_Streaks", 1f);
        m.SetFloat("_FlowSpeed", 2.6f);
        m.SetFloat("_RippleScale", 0.8f);
        m.SetFloat("_EdgeFoam", 0.25f);
        var foam = m.GetColor("_FoamColor"); foam.a = 0.9f; m.SetColor("_FoamColor", foam);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Lit(string name, Color c, float smooth)
    {
        var m = Get(name, Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Spray()
    {
        var m = Get("M_Water_Spray", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        var src = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        if (src && src.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }
}
