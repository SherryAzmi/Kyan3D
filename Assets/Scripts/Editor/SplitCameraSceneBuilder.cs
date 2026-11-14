using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Tools > Kyan > Build Split Camera Rig — (re)builds the 2-player split-screen test rig in the open scene.
/// </summary>
public static class SplitCameraSceneBuilder
{
    [MenuItem("Tools/Kyan/Build Split Camera Rig")]
    public static void Build()
    {
        // ---------- cleanup ----------
        foreach (var n in new[] { "Main Camera", "--- ENVIRONMENT ---", "--- PLAYERS ---", "--- CAMERAS ---", "SplitScreenUI" })
        {
            var g = GameObject.Find(n);
            if (g) Object.DestroyImmediate(g);
        }

        // ---------- materials ----------
        var matGround = MakeMat("M_TestGround", new Color(0.45f, 0.5f, 0.45f));
        var matLight = MakeMat("M_LightSoul", new Color(1f, 0.78f, 0.25f));
        var matVoid = MakeMat("M_VoidSoul", new Color(0.12f, 0.12f, 0.15f));
        var matBlock = MakeMat("M_TestBlock", new Color(0.7f, 0.7f, 0.75f));

        // ---------- environment ----------
        var env = new GameObject("--- ENVIRONMENT ---");
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "TestGround";
        ground.transform.SetParent(env.transform);
        ground.transform.localScale = new Vector3(6, 1, 6);
        ground.GetComponent<Renderer>().sharedMaterial = matGround;

        var rnd = new System.Random(7);
        for (int i = 0; i < 12; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = "TestBlock_" + i;
            b.transform.SetParent(env.transform);
            float h = 1f + (float)rnd.NextDouble() * 3f;
            b.transform.position = new Vector3((float)(rnd.NextDouble() * 40 - 20), h / 2f, (float)(rnd.NextDouble() * 40 - 20));
            b.transform.localScale = new Vector3(2, h, 2);
            b.GetComponent<Renderer>().sharedMaterial = matBlock;
        }

        // ---------- players ----------
        var playersRoot = new GameObject("--- PLAYERS ---");
        var lightP = MakePlayer(playersRoot.transform, "LightSoul_P1", new Vector3(-3, 0, 0), matLight, SoulType.Light, 0,
            KeyboardDebugLayout.WasdMouse, new Color(1f, 0.7f, 0.15f));   // warm gold light bar
        var voidP = MakePlayer(playersRoot.transform, "VoidSoul_P2", new Vector3(3, 0, 0), matVoid, SoulType.Void, 1,
            KeyboardDebugLayout.Arrows, new Color(0.45f, 0.2f, 1f));      // violet light bar

        // ---------- cameras ----------
        var camRoot = new GameObject("--- CAMERAS ---");
        var lightCam = MakeOutputCam(camRoot.transform, "Camera_Light_Left", new Rect(0f, 0f, 0.5f, 1f), OutputChannels.Channel01, true);
        var voidCam = MakeOutputCam(camRoot.transform, "Camera_Void_Right", new Rect(0.5f, 0f, 0.5f, 1f), OutputChannels.Channel02, false);
        MakeVCam(camRoot.transform, "CM_Light_Orbit", lightP.transform.Find("CameraTarget"), OutputChannels.Channel01, lightP.GetComponent<PlayerInputReader>());
        MakeVCam(camRoot.transform, "CM_Void_Orbit", voidP.transform.Find("CameraTarget"), OutputChannels.Channel02, voidP.GetComponent<PlayerInputReader>());
        lightP.GetComponent<SoulMovement>().viewCamera = lightCam.transform;
        voidP.GetComponent<SoulMovement>().viewCamera = voidCam.transform;

        // ---------- divider UI ----------
        var ui = new GameObject("SplitScreenUI");
        var canvas = ui.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = ui.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var div = new GameObject("Divider");
        div.transform.SetParent(ui.transform, false);
        var img = div.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(6, 0);
        rt.anchoredPosition = Vector2.zero;

        // Controller status labels (top of each half)
        MakeStatusLabel(ui.transform, "P1_ControllerStatus", 0f, 0.5f, lightP.GetComponent<PlayerInputReader>(), true);
        MakeStatusLabel(ui.transform, "P2_ControllerStatus", 0.5f, 1f, voidP.GetComponent<PlayerInputReader>(), false);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[Kyan] Split camera rig built.");
    }

    static void MakeStatusLabel(Transform canvas, string name, float xMin, float xMax, PlayerInputReader player, bool logDevices)
    {
        var go = new GameObject(name);
        go.transform.SetParent(canvas, false);
        var txt = go.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 28;
        txt.alignment = TextAnchor.UpperCenter;
        txt.raycastTarget = false;
        var rt = txt.rectTransform;
        rt.anchorMin = new Vector2(xMin, 1); rt.anchorMax = new Vector2(xMax, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, 90); rt.anchoredPosition = new Vector2(0, -20);
        go.AddComponent<Outline>().effectColor = Color.black;
        var status = go.AddComponent<ControllerStatusUI>();
        status.player = player;
        status.label = txt;
        status.logDeviceChanges = logDevices;
    }

    static Material MakeMat(string name, Color c)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        string p = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, p);
        }
        m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject MakePlayer(Transform parent, string name, Vector3 pos, Material mat, SoulType soul, int padIndex,
        KeyboardDebugLayout kbLayout, Color lightBar)
    {
        var root = new GameObject(name);
        root.tag = "Player";
        root.transform.SetParent(parent);
        root.transform.position = pos;

        var cc = root.AddComponent<CharacterController>();
        cc.height = 2f; cc.radius = 0.4f; cc.center = new Vector3(0, 1, 0);

        var reader = root.AddComponent<PlayerInputReader>();
        reader.soul = soul;
        reader.gamepadIndex = padIndex;
        reader.keyboardDebug = kbLayout;
        reader.lightBarColor = lightBar;
        root.AddComponent<SoulMovement>().input = reader;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0, 1, 0);
        body.GetComponent<Renderer>().sharedMaterial = mat;

        var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "FacingMarker";
        Object.DestroyImmediate(nose.GetComponent<Collider>());
        nose.transform.SetParent(root.transform, false);
        nose.transform.localPosition = new Vector3(0, 1.5f, 0.45f);
        nose.transform.localScale = new Vector3(0.3f, 0.15f, 0.2f);
        nose.GetComponent<Renderer>().sharedMaterial = mat;

        var t = new GameObject("CameraTarget").transform;
        t.SetParent(root.transform, false);
        t.localPosition = new Vector3(0, 1.6f, 0);
        return root;
    }

    static Camera MakeOutputCam(Transform parent, string name, Rect rect, OutputChannels channel, bool audio)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        var cam = go.AddComponent<Camera>();
        cam.rect = rect;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 120f; // never see the other world (300 m away)
        go.AddComponent<UniversalAdditionalCameraData>();
        var brain = go.AddComponent<CinemachineBrain>();
        brain.ChannelMask = channel; // this brain only "sees" its own player's CinemachineCamera
        if (audio) { go.AddComponent<AudioListener>(); go.tag = "MainCamera"; }
        return cam;
    }

    static void MakeVCam(Transform parent, string name, Transform target, OutputChannels channel, PlayerInputReader reader)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);

        var vcam = go.AddComponent<CinemachineCamera>();
        vcam.Follow = target;
        vcam.LookAt = target;
        vcam.OutputChannel = channel;
        var lens = vcam.Lens;
        lens.FieldOfView = 60f; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 120f;
        vcam.Lens = lens;

        var orb = go.AddComponent<CinemachineOrbitalFollow>();
        orb.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
        orb.Radius = 5.5f;
        var ts = orb.TrackerSettings;
        ts.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
        ts.PositionDamping = new Vector3(0.2f, 0.2f, 0.2f);
        orb.TrackerSettings = ts;
        var h = orb.HorizontalAxis;
        h.Range = new Vector2(-180, 180); h.Wrap = true; h.Value = 0; h.Center = 0;
        orb.HorizontalAxis = h;
        var v = orb.VerticalAxis;
        v.Range = new Vector2(-15, 70); v.Wrap = false; v.Value = 18; v.Center = 18;
        orb.VerticalAxis = v;

        var comp = go.AddComponent<CinemachineRotationComposer>();
        comp.Damping = new Vector2(0.1f, 0.1f);

        var deo = go.AddComponent<CinemachineDeoccluder>(); // pushes camera in front of walls
        deo.IgnoreTag = "Player";

        go.AddComponent<OrbitCameraInput>().input = reader;
    }
}
