using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Task 9 lighting, for every puzzle level (runs at the end of WorldLife.ApplyOpenScene):
///  - Guide beams become neon: a white-hot core, the coloured beam, a wide soft halo, a glow ring on
///    the ground, a stronger light, and a gentle bloom so they really shine (gold in the desert, cyan in the forest).
///  - Both players carry a soft light from above and behind that casts soft shadows, so each character
///    stands out and has a shadow to read. The dark side gets a cool moonlight, the happy side a warm one.
/// </summary>
public static class NeonLighting
{
    const string MatDir = "Assets/Materials/World";
    const string GlowTex = "Assets/Textures/Life/beam_glow.png";
    const string BloomProfile = "Assets/Settings/NeonGlow.asset";

    static readonly Color Gold = new Color(1f, 0.6f, 0.12f), Cyan = new Color(0.25f, 1f, 0.85f);

    [MenuItem("Tools/Kyan/Neon beams + player lights (open scene)")]
    static void FromMenu()
    {
        Debug.Log("[NeonLighting] " + ApplyOpenScene());
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    public static string ApplyOpenScene()
    {
        int beams = 0, lights = 0;
        foreach (var gb in Object.FindObjectsByType<GuideBeacon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool forest = gb.transform.position.x > 150f;
            NeonBeam(gb, forest ? Cyan : Gold, forest ? "Cyan" : "Gold");
            beams++;
        }
        foreach (var n in new[] { "Present_P1", "Future_P2" })
        {
            var p = GameObject.Find(n);
            if (!p) continue;
            PlayerLight(p.transform, n == "Present_P1");
            lights++;
        }
        bool bloom = AddBloom();
        return $" neon beams={beams}, player lights={lights}, bloom={bloom}";
    }

    // ------------------------------------------------------------------ beams

    static void NeonBeam(GuideBeacon gb, Color c, string tag)
    {
        var root = gb.transform;
        foreach (var n in new[] { "Beam Core", "Beam Halo", "Beam Ring" })
        {
            var old = root.Find(n);
            if (old) Object.DestroyImmediate(old.gameObject);
        }

        // main beam: brighter, HDR so it blooms
        gb.beam.sharedMaterial = BeamMat("M_Beam_" + tag, c * 2.4f, 0.55f, false);

        var core = Cylinder("Beam Core", root, new Vector3(0f, 12f, 0f), new Vector3(0.2f, 12f, 0.2f),
                            BeamMat("M_Beam_" + tag + "_Core", Color.Lerp(c, Color.white, 0.55f) * 3.2f, 0.9f, true));
        var halo = Cylinder("Beam Halo", root, new Vector3(0f, 12f, 0f), new Vector3(1.9f, 12f, 1.9f),
                            BeamMat("M_Beam_" + tag + "_Halo", c * 1.4f, 0.16f, true));

        var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ring.name = "Beam Ring";
        Object.DestroyImmediate(ring.GetComponent<Collider>());
        ring.transform.SetParent(root, false);
        ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ring.transform.localScale = new Vector3(3.2f, 3.2f, 1f);
        var ringMat = BeamMat("M_Beam_" + tag + "_Ring", c * 2f, 0.85f, true);
        ringMat.SetTexture("_BaseMap", GlowTexture());
        var rr = ring.GetComponent<Renderer>();
        rr.sharedMaterial = ringMat;
        rr.shadowCastingMode = ShadowCastingMode.Off;

        gb.extras = new[] { core.GetComponent<Renderer>(), halo.GetComponent<Renderer>(), rr };
        // the Neon layer is drawn above the desert's grey mood shade (see NeonOverlay)
        NameNeonLayer();
        foreach (var r in new[] { gb.beam, gb.extras[0], gb.extras[1], gb.extras[2] })
            r.gameObject.layer = NeonOverlay.NeonLayer;
        gb.glowIntensity = 4.5f;
        if (gb.glow)
        {
            gb.glow.color = Color.Lerp(c, Color.white, 0.25f);
            gb.glow.range = 9f;
            gb.glow.shadows = LightShadows.None;
        }
        EditorUtility.SetDirty(gb);
    }

    static void NameNeonLayer()
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layer = tm.FindProperty("layers").GetArrayElementAtIndex(NeonOverlay.NeonLayer);
        if (layer.stringValue == "Neon") return;
        layer.stringValue = "Neon";
        tm.ApplyModifiedProperties();
    }

    static GameObject Cylinder(string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return g;
    }

    static Material BeamMat(string name, Color hdr, float alpha, bool additive)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(m, path);
        }
        hdr.a = alpha;
        m.SetColor("_BaseColor", hdr);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f); // see the inside of the beam too
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent + (additive ? 1 : 0);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Texture2D GlowTexture()
    {
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTex);
        if (t) return t;
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = new Vector2(x - S * 0.5f + 0.5f, y - S * 0.5f + 0.5f).magnitude / (S * 0.5f);
                float ring = Mathf.Exp(-Mathf.Pow((d - 0.42f) / 0.07f, 2f));   // thin bright ring
                float fill = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f) * 0.55f;   // soft pool of light
                float a = Mathf.Clamp01(ring + fill);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        Directory.CreateDirectory(Path.GetDirectoryName(GlowTex));
        File.WriteAllBytes(GlowTex, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(GlowTex);
        var imp = (TextureImporter)AssetImporter.GetAtPath(GlowTex);
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTex);
    }

    // ------------------------------------------------------------------ player lights

    static void PlayerLight(Transform p, bool darkSide)
    {
        foreach (var n in new[] { "Aura", "Soft Light" })
        {
            var old = p.Find(n);
            if (old) Object.DestroyImmediate(old.gameObject);
        }
        var go = new GameObject("Soft Light");
        go.transform.SetParent(p, false);
        // above and a little behind: lights the character the camera sees and drops a soft shadow in front
        // (a spot, not a point light: one shadow map instead of six, so the shadows stay sharp)
        go.transform.localPosition = new Vector3(0.6f, 3.4f, -1.6f);
        go.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 0f, 0.9f) - go.transform.localPosition);
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.spotAngle = 95f;
        l.innerSpotAngle = 40f;
        l.color = darkSide ? new Color(0.72f, 0.8f, 1f) : new Color(1f, 0.86f, 0.64f);
        l.intensity = darkSide ? 5f : 3.5f;
        l.range = 9f;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = darkSide ? 0.6f : 0.5f;
        l.shadowBias = 0.05f;
        l.shadowNormalBias = 0.4f;
        l.renderMode = LightRenderMode.ForcePixel;
        EditorUtility.SetDirty(p.gameObject);
    }

    // ------------------------------------------------------------------ bloom

    static bool AddBloom()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BloomProfile);
        if (!profile)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BloomProfile));
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, BloomProfile);
        }
        profile.components.RemoveAll(c => !c);
        if (!profile.TryGet<Bloom>(out var bloom))
        {
            bloom = profile.Add<Bloom>(true);
            bloom.name = "Bloom";
            bloom.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(bloom, profile); // keep it inside the profile file
        }
        bloom.threshold.Override(1.15f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.7f);
        bloom.highQualityFiltering.Override(true);
        EditorUtility.SetDirty(bloom);
        EditorUtility.SetDirty(profile);

        var scene = EditorSceneManager.GetActiveScene();
        GameObject vol = null;
        foreach (var r in scene.GetRootGameObjects()) if (r.name == "Neon Glow") vol = r;
        if (!vol) vol = new GameObject("Neon Glow");
        var v = vol.GetComponent<Volume>();
        if (!v) v = vol.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = 1f;
        v.sharedProfile = profile;

        bool any = false;
        foreach (var n in new[] { "Camera_Light_Left", "Camera_Void_Right" })
        {
            var cam = GameObject.Find(n);
            if (cam && cam.TryGetComponent<UniversalAdditionalCameraData>(out var data))
            {
                data.renderPostProcessing = true;
                EditorUtility.SetDirty(data);
                any = true;
            }
        }
        return any;
    }
}
