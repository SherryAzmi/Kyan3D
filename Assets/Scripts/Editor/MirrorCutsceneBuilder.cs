using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Tools > Kyan > Build Mirror Cutscene — (re)builds the opening cutscene in Assets/Scenes/Sh/Cutscene.unity
/// and puts Cutscene + SplitCamera at the top of the build scene list.
/// WARNING: wipes everything in the Cutscene scene before rebuilding.
/// </summary>
public static class MirrorCutsceneBuilder
{
    const string ScenePath = "Assets/Scenes/Sh/Cutscene.unity";
    const string SplitScenePath = "Assets/Scenes/Sh/SplitCamera.unity";
    const string MatDir = "Assets/Materials/Cutscene";
    const string ProfilePath = "Assets/Settings/CutsceneDarkProfile.asset";

    [MenuItem("Tools/Kyan/Build Mirror Cutscene")]
    static void BuildFromMenu()
    {
        if (!EditorUtility.DisplayDialog("Build Mirror Cutscene",
                "This deletes everything in Cutscene.unity and rebuilds it. Continue?", "Rebuild", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);

        // ---------- materials ----------
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Materials", "Cutscene");
        var mSkin = Lit("M_CS_Skin", new Color(0.78f, 0.7f, 0.64f), 0.3f);
        var mClothes = Lit("M_CS_Clothes", new Color(0.42f, 0.41f, 0.4f), 0.15f);
        var mHair = Lit("M_CS_Hair", new Color(0.1f, 0.09f, 0.09f), 0.4f);
        var mFace = Lit("M_CS_FaceDark", new Color(0.05f, 0.05f, 0.06f), 0.2f);
        var mTired = Lit("M_CS_TiredEyes", new Color(0.48f, 0.38f, 0.4f), 0.2f);
        var mReflSkin = Lit("M_CS_ReflectionSkin", new Color(0.62f, 0.61f, 0.62f), 0.2f);
        var mReflClothes = Lit("M_CS_ReflectionClothes", new Color(0.36f, 0.36f, 0.38f), 0.1f);
        var mFloor = Lit("M_CS_Floor", new Color(0.16f, 0.15f, 0.17f), 0.25f);
        var mWall = Lit("M_CS_Wall", new Color(0.11f, 0.11f, 0.14f), 0.1f);
        var mFrame = Lit("M_CS_Frame", new Color(0.24f, 0.18f, 0.13f), 0.45f);
        var mGlass = Glass("M_CS_Glass", new Color(0.62f, 0.68f, 0.75f, 0.22f));
        var mShard = Glass("M_CS_Shard", new Color(0.8f, 0.86f, 0.92f, 0.55f));
        var mCrack = Unlit("M_CS_Crack", new Color(0.92f, 0.95f, 1f));
        var mSmoke = Smoke("M_CS_Smoke");

        // ---------- lighting / atmosphere ----------
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.08f, 0.08f, 0.11f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.07f;
        RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.035f);

        var env = new GameObject("--- ENVIRONMENT ---").transform;
        Prim(PrimitiveType.Plane, "Floor", env, Vector3.zero, Vector3.zero, new Vector3(2f, 1f, 2f), mFloor, keepCollider: true);
        Prim(PrimitiveType.Cube, "BackWall", env, new Vector3(0f, 4f, 6f), Vector3.zero, new Vector3(20f, 8f, 0.2f), mWall);

        var lights = new GameObject("--- LIGHTS ---").transform;
        var moon = MakeLight("Cold Fill", lights, LightType.Directional, new Vector3(0f, 3f, 0f), new Color(0.6f, 0.7f, 1f), 0.15f, 0f);
        moon.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var bulb = MakeLight("Bulb", lights, LightType.Point, new Vector3(0.6f, 2.6f, 0.7f), new Color(1f, 0.9f, 0.78f), 3f, 6f);
        var rim = MakeLight("Mirror Rim", lights, LightType.Point, new Vector3(0f, 2.5f, 2.6f), new Color(0.6f, 0.7f, 1f), 1.5f, 4f);

        // ---------- characters ----------
        var chars = new GameObject("--- CHARACTERS ---").transform;
        Transform presentHead, reflHead;
        var present = Figure("Present", chars, Vector3.zero, 0f, mSkin, mClothes, mHair, mFace, mTired, out presentHead);
        var refl = Figure("Reflection", chars, new Vector3(0f, 0f, 3.2f), 180f, mReflSkin, mReflClothes, mHair, mFace, mTired, out reflHead);

        // ---------- mirror ----------
        var mirror = new GameObject("Mirror").transform;
        mirror.position = new Vector3(0f, 0f, 1.6f);
        var glass = Prim(PrimitiveType.Quad, "Glass", mirror, new Vector3(0f, 1.25f, 0f), Vector3.zero, new Vector3(1.1f, 2f, 1f), mGlass);
        glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Prim(PrimitiveType.Cube, "Frame Top", mirror, new Vector3(0f, 2.29f, 0f), Vector3.zero, new Vector3(1.26f, 0.08f, 0.08f), mFrame);
        Prim(PrimitiveType.Cube, "Frame Bottom", mirror, new Vector3(0f, 0.21f, 0f), Vector3.zero, new Vector3(1.26f, 0.08f, 0.08f), mFrame);
        Prim(PrimitiveType.Cube, "Frame Left", mirror, new Vector3(-0.59f, 1.25f, 0f), Vector3.zero, new Vector3(0.08f, 2.16f, 0.08f), mFrame);
        Prim(PrimitiveType.Cube, "Frame Right", mirror, new Vector3(0.59f, 1.25f, 0f), Vector3.zero, new Vector3(0.08f, 2.16f, 0.08f), mFrame);
        Prim(PrimitiveType.Cube, "Foot Left", mirror, new Vector3(-0.55f, 0.1f, 0f), Vector3.zero, new Vector3(0.12f, 0.2f, 0.5f), mFrame);
        Prim(PrimitiveType.Cube, "Foot Right", mirror, new Vector3(0.55f, 0.1f, 0f), Vector3.zero, new Vector3(0.12f, 0.2f, 0.5f), mFrame);

        // ---------- dark smoke ----------
        var smoke = MakeSmoke(mSmoke);

        // ---------- post-processing ----------
        var volGo = new GameObject("Dark Volume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 10f;
        vol.weight = 0.15f;
        vol.sharedProfile = DarkProfile();

        // ---------- camera + shots ----------
        var shots = new GameObject("--- SHOTS ---").transform;
        var shotFace = Shot("Shot Face", shots, new Vector3(0.45f, 1.6f, 0.95f), new Vector3(0f, 1.66f, 0.05f));
        var shotShoulder = Shot("Shot Shoulder", shots, new Vector3(0.5f, 1.82f, -1.2f), new Vector3(0f, 1.45f, 1.6f));
        var shotClose = Shot("Shot Close", shots, new Vector3(0.32f, 1.74f, -0.55f), new Vector3(0f, 1.5f, 1.6f));

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.05f;
        camGo.transform.SetPositionAndRotation(shotFace.position, shotFace.rotation);
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

        // ---------- UI ----------
        var canvasGo = new GameObject("CutsceneUI");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var flash = FullImage("Flash", canvasGo.transform, new Color(1f, 1f, 1f, 0f));
        var fade = FullImage("Fade", canvasGo.transform, Color.black);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var caption = MakeText("Caption", canvasGo.transform, font, 44, FontStyle.Italic, TextAnchor.MiddleCenter,
                               new Color(0.85f, 0.85f, 0.9f, 0f), Vector2.zero, Vector2.one, Vector2.zero);
        caption.text = "...and the pieces fell into two worlds.";
        var hint = MakeText("SkipHint", canvasGo.transform, font, 22, FontStyle.Normal, TextAnchor.LowerRight,
                            new Color(1f, 1f, 1f, 0.4f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 24f));
        hint.rectTransform.sizeDelta = new Vector2(500f, 40f);
        hint.rectTransform.pivot = new Vector2(1f, 0f);
        hint.text = "Space / Enter / (A)  -  skip";

        // ---------- director ----------
        var dirGo = new GameObject("CutsceneDirector");
        var sfx = dirGo.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        var d = dirGo.AddComponent<MirrorCutscene>();
        d.cam = cam;
        d.shotFace = shotFace;
        d.shotShoulder = shotShoulder;
        d.shotClose = shotClose;
        d.character = present.transform;
        d.head = presentHead;
        d.reflectionHead = reflHead;
        d.reflection = refl;
        d.mirrorGlass = glass.GetComponent<Renderer>();
        d.crackMaterial = mCrack;
        d.shardMaterial = mShard;
        d.roomLights = new[] { moon, bulb, rim };
        d.darkSmoke = smoke;
        d.darkVolume = vol;
        d.fadeImage = fade;
        d.flashImage = flash;
        d.captionText = caption;
        d.skipHint = hint;
        d.sfx = sfx;
        d.nextScene = "SplitCamera";
        AudioSetup.WireCutscene(d);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings();
        Debug.Log("[MirrorCutsceneBuilder] Cutscene built and saved. Press Play to watch it.");
    }

    // ================================================================== figure

    static GameObject Figure(string name, Transform parent, Vector3 pos, float yRot,
                             Material skin, Material clothes, Material hair, Material face, Material tired,
                             out Transform headPivot)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.localPosition = pos;
        root.localRotation = Quaternion.Euler(0f, yRot, 0f);

        Prim(PrimitiveType.Capsule, "Leg L", root, new Vector3(-0.11f, 0.4f, 0f), Vector3.zero, new Vector3(0.16f, 0.4f, 0.16f), clothes);
        Prim(PrimitiveType.Capsule, "Leg R", root, new Vector3(0.11f, 0.4f, 0f), Vector3.zero, new Vector3(0.16f, 0.4f, 0.16f), clothes);
        Prim(PrimitiveType.Capsule, "Body", root, new Vector3(0f, 1.05f, 0f), new Vector3(8f, 0f, 0f), new Vector3(0.46f, 0.45f, 0.3f), clothes);
        Prim(PrimitiveType.Capsule, "Arm L", root, new Vector3(-0.3f, 1.0f, 0.02f), new Vector3(6f, 0f, -5f), new Vector3(0.12f, 0.36f, 0.12f), clothes);
        Prim(PrimitiveType.Capsule, "Arm R", root, new Vector3(0.3f, 1.0f, 0.02f), new Vector3(6f, 0f, 5f), new Vector3(0.12f, 0.36f, 0.12f), clothes);
        Prim(PrimitiveType.Sphere, "Hand L", root, new Vector3(-0.33f, 0.66f, 0.06f), Vector3.zero, Vector3.one * 0.1f, skin);
        Prim(PrimitiveType.Sphere, "Hand R", root, new Vector3(0.33f, 0.66f, 0.06f), Vector3.zero, Vector3.one * 0.1f, skin);

        headPivot = new GameObject("Head").transform;
        headPivot.SetParent(root, false);
        headPivot.localPosition = new Vector3(0f, 1.48f, 0.03f);
        headPivot.localRotation = Quaternion.Euler(14f, 0f, 0f); // looking down

        Prim(PrimitiveType.Cylinder, "Neck", headPivot, new Vector3(0f, 0.04f, 0f), Vector3.zero, new Vector3(0.1f, 0.06f, 0.1f), skin);
        Prim(PrimitiveType.Sphere, "Skull", headPivot, new Vector3(0f, 0.2f, 0f), Vector3.zero, Vector3.one * 0.32f, skin);
        Prim(PrimitiveType.Sphere, "Hair", headPivot, new Vector3(0f, 0.3f, -0.05f), Vector3.zero, new Vector3(0.34f, 0.2f, 0.32f), hair);
        // Tired, half-closed eyes with dark circles.
        Prim(PrimitiveType.Sphere, "Eye L", headPivot, new Vector3(-0.06f, 0.215f, 0.142f), Vector3.zero, new Vector3(0.045f, 0.018f, 0.02f), face);
        Prim(PrimitiveType.Sphere, "Eye R", headPivot, new Vector3(0.06f, 0.215f, 0.142f), Vector3.zero, new Vector3(0.045f, 0.018f, 0.02f), face);
        Prim(PrimitiveType.Sphere, "Bag L", headPivot, new Vector3(-0.06f, 0.193f, 0.141f), Vector3.zero, new Vector3(0.05f, 0.02f, 0.015f), tired);
        Prim(PrimitiveType.Sphere, "Bag R", headPivot, new Vector3(0.06f, 0.193f, 0.141f), Vector3.zero, new Vector3(0.05f, 0.02f, 0.015f), tired);
        // Sad brows: inner ends raised.
        Prim(PrimitiveType.Cube, "Brow L", headPivot, new Vector3(-0.062f, 0.258f, 0.135f), new Vector3(0f, 0f, 18f), new Vector3(0.06f, 0.011f, 0.012f), face);
        Prim(PrimitiveType.Cube, "Brow R", headPivot, new Vector3(0.062f, 0.258f, 0.135f), new Vector3(0f, 0f, -18f), new Vector3(0.06f, 0.011f, 0.012f), face);
        // Frown: corners down.
        Prim(PrimitiveType.Cube, "Mouth L", headPivot, new Vector3(-0.024f, 0.12f, 0.147f), new Vector3(0f, 0f, 22f), new Vector3(0.05f, 0.01f, 0.012f), face);
        Prim(PrimitiveType.Cube, "Mouth R", headPivot, new Vector3(0.024f, 0.12f, 0.147f), new Vector3(0f, 0f, -22f), new Vector3(0.05f, 0.01f, 0.012f), face);

        return root.gameObject;
    }

    // ================================================================== helpers

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localEuler,
                           Vector3 localScale, Material mat, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static Light MakeLight(string name, Transform parent, LightType type, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = type;
        l.color = color;
        l.intensity = intensity;
        if (range > 0f) l.range = range;
        l.shadows = LightShadows.Soft;
        return l;
    }

    static Transform Shot(string name, Transform parent, Vector3 pos, Vector3 lookAt)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = pos;
        t.rotation = Quaternion.LookRotation(lookAt - pos);
        return t;
    }

    static ParticleSystem MakeSmoke(Material mat)
    {
        var go = new GameObject("Dark Smoke");
        go.transform.position = new Vector3(0f, 1.2f, 0.3f);
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.03f, 0.02f, 0.04f, 0.55f),
                                                             new Color(0.12f, 0.02f, 0.05f, 0.45f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;
        main.playOnAwake = true;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.7f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    static Image FullImage(string name, Transform parent, Color color)
    {
        var img = new GameObject(name).AddComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = color;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return img;
    }

    static Text MakeText(string name, Transform parent, Font font, int size, FontStyle style, TextAnchor anchor,
                         Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos)
    {
        var t = new GameObject(name).AddComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        if (anchorMin == Vector2.zero && anchorMax == Vector2.one) rt.offsetMin = rt.offsetMax = Vector2.zero;
        return t;
    }

    // ================================================================== materials

    static Material GetOrCreate(string name, Shader shader)
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

    static Material Lit(string name, Color c, float smooth)
    {
        var m = GetOrCreate(name, Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Unlit(string name, Color c)
    {
        var m = GetOrCreate(name, Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void MakeTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.DisableKeyword("_ALPHAMODULATE_ON");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    static Material Glass(string name, Color c)
    {
        var m = GetOrCreate(name, Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.95f);
        m.SetFloat("_Metallic", 0.3f);
        MakeTransparent(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Smoke(string name)
    {
        var m = GetOrCreate(name, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        var src = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        if (src && src.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
        m.SetColor("_BaseColor", Color.white);
        MakeTransparent(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    static VolumeProfile DarkProfile()
    {
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (!p)
        {
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, ProfilePath);
        }

        if (!p.TryGet(out Vignette v)) { v = p.Add<Vignette>(true); AssetDatabase.AddObjectToAsset(v, p); }
        v.intensity.Override(0.55f);
        v.smoothness.Override(0.6f);
        v.color.Override(Color.black);

        if (!p.TryGet(out ColorAdjustments ca)) { ca = p.Add<ColorAdjustments>(true); AssetDatabase.AddObjectToAsset(ca, p); }
        ca.saturation.Override(-75f);
        ca.postExposure.Override(-0.6f);
        ca.contrast.Override(20f);

        if (!p.TryGet(out ChromaticAberration chr)) { chr = p.Add<ChromaticAberration>(true); AssetDatabase.AddObjectToAsset(chr, p); }
        chr.intensity.Override(0.5f);

        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
    }

    static void AddToBuildSettings()
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene(SplitScenePath, true)
        };
        foreach (var s in EditorBuildSettings.scenes)
            if (s.path != ScenePath && s.path != SplitScenePath) list.Add(s);
        EditorBuildSettings.scenes = list.ToArray();
    }
}
