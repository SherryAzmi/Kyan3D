using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Tools > Kyan > Build Mirror Puzzle (Puzzle 3) — builds Assets/Scenes/Sh/Puzzle3.unity:
/// the split-screen rig, a desert and a forest each with the broken mirror, the split last shard
/// (dig / bloom), the join, and the ending room. Shares helpers with BridgePuzzleBuilder.
///
/// Layout (both worlds, forest = desert + ForestOffset):
///   spawn z=-15 · broken mirror at (0, 7) facing the spawn · desert mound at (-6, -2) ·
///   forest bloom root at (11, -2) · ending room at x=600.
/// </summary>
public static partial class BridgePuzzleBuilder
{
    const string Puzzle3Path = "Assets/Scenes/Sh/Puzzle3.unity";
    static readonly Vector3 MirrorPos = new Vector3(0f, 0f, 7f);
    static readonly Vector3 EndingOrigin = new Vector3(600f, 0f, 0f);

    static Material mMirrorBack, mSlotOutline, mMirrorPiece, mSeam, mEndGlass, mMoss, mFrameWood;

    [MenuItem("Tools/Kyan/Build Mirror Puzzle (Puzzle 3)")]
    static void BuildPuzzle3FromMenu()
    {
        if (!EditorUtility.DisplayDialog("Build Mirror Puzzle",
                "Rebuilds the worlds, puzzle and ending in Puzzle3.unity (players and cameras are kept). Continue?",
                "Build", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildPuzzle3();
    }

    public static void BuildPuzzle3()
    {
        // ---------- scene + split-screen rig ----------
        UnityEngine.SceneManagement.Scene scene;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Puzzle3Path))
            scene = EditorSceneManager.OpenScene(Puzzle3Path, OpenSceneMode.Single);
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, Puzzle3Path);
        }
        if (!GameObject.Find("--- PLAYERS ---")) SplitCameraSceneBuilder.Build();

        foreach (var n in new[] { "--- ENVIRONMENT ---", "--- DESERT (Present) ---", "--- FOREST (Future) ---", "PuzzleUI",
                                  "MirrorPuzzle", "MirrorEnding", "EndingUI", "Ending Camera", "--- ENDING ROOM ---", "EventSystem" })
        {
            var g = GameObject.Find(n);
            if (g) Object.DestroyImmediate(g);
        }

        MakeMaterials();
        MakeMirrorMaterials();

        // ---------- worlds ----------
        var desert = new GameObject("--- DESERT (Present) ---").transform;
        var forest = new GameObject("--- FOREST (Future) ---").transform;
        forest.position = ForestOffset;
        FlatGround(desert, mSand);
        FlatGround(forest, mGrass);
        DecorateDesert(desert);
        DecorateForest(forest);

        var mirrorD = BuildBrokenMirror(desert);
        var mirrorF = BuildBrokenMirror(forest);
        var slotD = MirrorSlotAt(desert, 0);
        var slotF = MirrorSlotAt(forest, 1);

        // desert: the mound hiding the desert half
        var mound = Sph("Sand Mound (shard)", desert, new Vector3(-6f, 0f, -2f), new Vector3(3.2f, 1.5f, 3.2f), mSandDark);
        var sm = mound.AddComponent<SandMound>();
        sm.range = 2.6f;
        sm.digTime = 3f;
        var halfD = ShardHalf(desert, "Mirror Shard (desert half)", new Vector3(-6f, 0.45f, -2f));

        // forest: the root that lifts the forest half
        var bloomRoot = Child(forest, "Bloom Root");
        bloomRoot.localPosition = new Vector3(11f, 0f, -2f);
        Sph("Moss", bloomRoot, new Vector3(0f, 0f, 0f), new Vector3(1.6f, 0.4f, 1.6f), mMoss);
        var stalk = Child(bloomRoot, "Stalk");
        stalk.localScale = new Vector3(1f, 1.3f, 1f);
        Cyl("Stalk Mesh", stalk, new Vector3(0f, 0.5f, 0f), new Vector3(0.28f, 0.5f, 0.28f), mRoot);
        var bud = Sph("Bud", bloomRoot, new Vector3(0f, 1.35f, 0f), new Vector3(0.6f, 0.3f, 0.6f), mGlowPink);
        var top = Child(bloomRoot, "Top");
        top.localPosition = new Vector3(0f, 1.75f, 0f);
        var bloom = bloomRoot.gameObject.AddComponent<BloomSpot>();
        bloom.range = 2.4f;
        bloom.growTime = 3f;
        bloom.stalk = stalk;
        bloom.bud = bud.transform;
        bloom.top = top;
        var halfF = ShardHalf(forest, "Mirror Shard (forest half)", new Vector3(11f, 1.75f, -2f));

        // guidance
        var beamD = Beacon(desert, "Guide Beam (desert)", mBeamGold, new Color(1f, 0.85f, 0.5f));
        var beamF = Beacon(forest, "Guide Beam (forest)", mBeamCyan, new Color(0.55f, 1f, 0.9f));

        // ---------- HUD, puzzle ----------
        var hud = BuildHUD();
        var mp = new GameObject("MirrorPuzzle").AddComponent<MirrorPuzzle>();
        mp.hud = hud;
        mp.shardMound = sm;
        mp.desertHalf = halfD;
        mp.desertSlot = slotD;
        mp.bloomSpot = bloom;
        mp.forestHalf = halfF;
        mp.forestSlot = slotF;
        mp.mirrors = new[] { mirrorD, mirrorF };
        mp.desertBeacon = beamD;
        mp.forestBeacon = beamF;

        var presentGo = SetupPlayer(new[] { "Present_P1", "LightSoul_P1" }, "Present_P1", Vector3.zero, mPresent, hud, 0, "□ / E", "R2 / F", false);
        var futureGo = SetupPlayer(new[] { "Future_P2", "VoidSoul_P2" }, "Future_P2", ForestOffset, mFuture, hud, 1, "□ / R-Shift", "R2 / R-Ctrl", true);
        if (presentGo) mp.present = presentGo.transform;
        if (futureGo) mp.future = futureGo.transform;
        SetupCamera("Camera_Light_Left", new Color(0.8f, 0.76f, 0.68f));
        SetupCamera("Camera_Void_Right", new Color(0.55f, 0.78f, 0.88f));
        var sun = GameObject.Find("Directional Light");
        if (sun)
        {
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            var l = sun.GetComponent<Light>();
            l.color = new Color(1f, 0.95f, 0.86f);
            l.intensity = 1.3f;
        }
        RenderSettings.fog = false;

        // ---------- ending ----------
        mp.ending = BuildEnding(hud);
        AudioSetup.AddGameAudio();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(x => x.path == Puzzle3Path))
        {
            int at = scenes.FindIndex(x => x.path == Puzzle2Path);
            scenes.Insert(at >= 0 ? at + 1 : scenes.Count, new EditorBuildSettingsScene(Puzzle3Path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        Debug.Log("[MirrorPuzzleBuilder] Puzzle3 built: broken mirror, split shard, join, ending.");
    }

    // ================================================================== pieces

    static void FlatGround(Transform world, Material ground)
    {
        Box("Ground", world, new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f), ground, true);
        var walls = Child(world, "Bounds");
        Wall(walls, new Vector3(-20f, 3f, 0f), new Vector3(1f, 6f, 42f));
        Wall(walls, new Vector3(20f, 3f, 0f), new Vector3(1f, 6f, 42f));
        Wall(walls, new Vector3(0f, 3f, -20f), new Vector3(42f, 6f, 1f));
        Wall(walls, new Vector3(0f, 3f, 20f), new Vector3(42f, 6f, 1f));
    }

    static void MirrorFrame(Transform root)
    {
        Box("Frame Top", root, new Vector3(0f, 2.29f, 0f), new Vector3(1.26f, 0.08f, 0.08f), mFrameWood, false);
        Box("Frame Bottom", root, new Vector3(0f, 0.21f, 0f), new Vector3(1.26f, 0.08f, 0.08f), mFrameWood, false);
        Box("Frame Left", root, new Vector3(-0.59f, 1.25f, 0f), new Vector3(0.08f, 2.16f, 0.08f), mFrameWood, false);
        Box("Frame Right", root, new Vector3(0.59f, 1.25f, 0f), new Vector3(0.08f, 2.16f, 0.08f), mFrameWood, false);
        Box("Foot Left", root, new Vector3(-0.55f, 0.1f, 0f), new Vector3(0.12f, 0.2f, 0.5f), mFrameWood, false);
        Box("Foot Right", root, new Vector3(0.55f, 0.1f, 0f), new Vector3(0.12f, 0.2f, 0.5f), mFrameWood, false);
    }

    static MirrorPuzzle.MirrorVisual BuildBrokenMirror(Transform world)
    {
        var root = Child(world, "Broken Mirror");
        root.localPosition = MirrorPos;
        root.localScale = Vector3.one * 1.6f; // bigger than the opening mirror so it reads on the split screen
        MirrorFrame(root);
        Box("Backing", root, new Vector3(0f, 1.25f, 0.03f), new Vector3(1.1f, 2f, 0.02f), mMirrorBack, false);

        Vector3 s = new Vector3(0.52f, 0.96f, 0.012f), o = new Vector3(0.53f, 0.97f, 0.006f);
        var p1 = new Vector3(-0.275f, 1.745f, 0f);
        var p2 = new Vector3(0.275f, 1.745f, 0f);
        var hD = new Vector3(-0.275f, 0.755f, 0f);
        var hF = new Vector3(0.275f, 0.755f, 0f);
        foreach (var p in new[] { p1, p2, hD, hF })
            Box("Slot Outline", root, p + new Vector3(0f, 0f, 0.012f), o, mSlotOutline, false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        var v = new MirrorPuzzle.MirrorVisual
        {
            piece1 = Box("Piece 1 (Bridge)", root, p1 + new Vector3(0f, 0f, -0.01f), s, mMirrorPiece, false).transform,
            piece2 = Box("Piece 2 (Puzzle 2)", root, p2 + new Vector3(0f, 0f, -0.01f), s, mMirrorPiece, false).transform,
            halfDesert = Box("Piece 3 (desert half)", root, hD + new Vector3(0f, 0f, -0.01f), s, mMirrorPiece, false),
            halfForest = Box("Piece 3 (forest half)", root, hF + new Vector3(0f, 0f, -0.01f), s, mMirrorPiece, false),
            seam = Box("Seam", root, new Vector3(0f, 0.755f, -0.02f), new Vector3(0.025f, 0.97f, 0.01f), mSeam, false),
            glow = AddLight(root, new Vector3(0f, 1.3f, -0.6f), new Color(0.85f, 0.92f, 1f), 1f, 6f)
        };
        return v;
    }

    static MirrorSlot MirrorSlotAt(Transform world, int index)
    {
        var t = Child(world, "Mirror Slot");
        t.localPosition = MirrorPos + new Vector3(0f, 0f, -1.1f);
        var ms = t.gameObject.AddComponent<MirrorSlot>();
        ms.world = index;
        ms.range = 2.4f;
        return ms;
    }

    static Carryable ShardHalf(Transform world, string name, Vector3 localPos)
    {
        var go = Box(name, world, localPos, new Vector3(0.4f, 0.6f, 0.04f), mMirrorPiece, false);
        AddLight(go.transform, Vector3.zero, new Color(0.85f, 0.92f, 1f), 1.5f, 4f);
        var c = go.AddComponent<Carryable>();
        c.displayName = "Mirror Shard";
        c.range = 2f;
        return c;
    }

    // ================================================================== ending

    static MirrorEnding BuildEnding(PuzzleHUD hud)
    {
        var room = new GameObject("--- ENDING ROOM ---").transform;
        room.position = EndingOrigin;
        Box("Floor (desert side)", room, new Vector3(-6f, -0.5f, 0f), new Vector3(12f, 1f, 24f), mSand, true);
        Box("Floor (forest side)", room, new Vector3(6f, -0.5f, 0f), new Vector3(12f, 1f, 24f), mGrass, true);
        Sph("Dune", room, new Vector3(-8f, -0.6f, 6f), new Vector3(6f, 2f, 4f), mDesertDune);
        Sph("Dune", room, new Vector3(-5f, -0.8f, 9f), new Vector3(5f, 2f, 3f), mDesertDune);
        for (int i = 0; i < 6; i++)
            Sph("Glow Plant", room, new Vector3(3f + i * 1.3f, 0.3f, 4f + (i % 2) * 2f), Vector3.one * 0.22f, i % 2 == 0 ? mGlowCyan : mGlowPink);

        var mirror = Child(room, "Whole Mirror");
        mirror.localPosition = new Vector3(0f, 0f, 1.2f);
        mirror.localScale = Vector3.one * 1.6f;
        MirrorFrame(mirror);
        var glass = Prim(PrimitiveType.Quad, "Glass", mirror, new Vector3(0f, 1.25f, 0f), new Vector3(1.1f, 2f, 1f), mEndGlass, false);
        glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var mirrorLight = AddLight(room, new Vector3(0f, 2.4f, 0f), new Color(1f, 0.9f, 0.75f), 2f, 9f);

        var presentFig = Figure(room, "Present (ending)", new Vector3(-6.5f, 0f, -0.8f), mPresent, false);
        var futureFig = Figure(room, "Future (ending)", new Vector3(6.5f, 0f, -0.8f), mFuture, true);
        var reflection = Figure(room, "Reflection", new Vector3(0f, 0f, 3.2f), mPresent, false);
        var whole = Figure(room, "Whole", new Vector3(0f, 0f, -0.8f), mFuture, true);
        var presentStand = Child(room, "Present Stand"); presentStand.localPosition = new Vector3(-0.9f, 0f, -0.8f);
        var futureStand = Child(room, "Future Stand"); futureStand.localPosition = new Vector3(0.9f, 0f, -0.8f);
        var meet = Child(room, "Meet Point"); meet.localPosition = new Vector3(0f, 0f, -0.8f);

        var camStart = Child(room, "Cam Start");
        camStart.localPosition = new Vector3(0f, 1.8f, -8.5f);
        camStart.rotation = Quaternion.LookRotation(new Vector3(0f, 1.3f, 1.2f) - camStart.localPosition);
        var camEnd = Child(room, "Cam End");
        camEnd.localPosition = new Vector3(0f, 1.6f, -5.2f);
        camEnd.rotation = Quaternion.LookRotation(new Vector3(0f, 1.4f, 1.2f) - camEnd.localPosition);

        var camGo = new GameObject("Ending Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.depth = 10f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.93f, 0.88f, 0.8f);
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 60f;
        camGo.AddComponent<UniversalAdditionalCameraData>();
        camGo.transform.SetPositionAndRotation(camStart.position, camStart.rotation);
        cam.enabled = false;

        // ---------- UI ----------
        var canvasGo = new GameObject("EndingUI");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var backdrop = new GameObject("Note Backdrop").AddComponent<Image>();
        backdrop.transform.SetParent(canvasGo.transform, false);
        backdrop.color = new Color(0f, 0f, 0f, 0f);
        backdrop.raycastTarget = false;
        Anchor(backdrop.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(0f, 30f));
        var header = UIText(canvasGo.transform, "Header", font, 30, TextAnchor.LowerCenter);
        Anchor(header.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-200f, 50f), new Vector2(0f, 300f));
        header.color = new Color(1f, 0.9f, 0.7f, 0f);
        header.fontStyle = FontStyle.Bold;
        var note = UIText(canvasGo.transform, "Note", font, 38, TextAnchor.UpperCenter);
        Anchor(note.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(-200f, 220f), new Vector2(0f, 280f));
        note.color = new Color(1f, 1f, 1f, 0f);
        note.fontStyle = FontStyle.Italic;
        note.lineSpacing = 1.2f;
        var closing = UIText(canvasGo.transform, "Closing", font, 52, TextAnchor.MiddleCenter);
        Anchor(closing.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-200f, -400f), new Vector2(0f, 330f));
        closing.color = new Color(0.24f, 0.17f, 0.12f, 0f); // dark warm brown: reads on the light room
        closing.lineSpacing = 1.2f;
        var closingOutline = closing.GetComponent<Outline>();
        if (closingOutline) closingOutline.effectColor = new Color(1f, 0.95f, 0.85f, 0.6f);

        var buttonsGo = new GameObject("Buttons", typeof(RectTransform));
        buttonsGo.transform.SetParent(canvasGo.transform, false);
        var group = buttonsGo.AddComponent<CanvasGroup>();
        Anchor((RectTransform)buttonsGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(640f, 80f), new Vector2(0f, 110f));
        var play = MakeButton(buttonsGo.transform, "Play again", font, new Vector2(-160f, 0f));
        var quit = MakeButton(buttonsGo.transform, "Quit", font, new Vector2(160f, 0f));
        var nav = play.navigation; nav.mode = Navigation.Mode.Explicit; nav.selectOnRight = quit; play.navigation = nav;
        nav = quit.navigation; nav.mode = Navigation.Mode.Explicit; nav.selectOnLeft = play; quit.navigation = nav;

        var white = new GameObject("White").AddComponent<Image>();
        white.transform.SetParent(canvasGo.transform, false);
        white.color = new Color(1f, 1f, 1f, 0f);
        white.raycastTarget = false;
        white.rectTransform.anchorMin = Vector2.zero;
        white.rectTransform.anchorMax = Vector2.one;
        white.rectTransform.offsetMin = white.rectTransform.offsetMax = Vector2.zero;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        // ---------- director ----------
        var e = new GameObject("MirrorEnding").AddComponent<MirrorEnding>();
        e.endingCam = cam;
        e.camStart = camStart;
        e.camEnd = camEnd;
        e.presentFig = presentFig.transform;
        e.futureFig = futureFig.transform;
        e.presentStand = presentStand;
        e.futureStand = futureStand;
        e.meetPoint = meet;
        e.reflectionFig = reflection.GetComponentInChildren<Renderer>();
        e.wholeFig = whole;
        e.mirrorLight = mirrorLight;
        e.whiteImage = white;
        e.noteBackdrop = backdrop;
        e.headerText = header;
        e.noteText = note;
        e.closingText = closing;
        e.buttons = group;
        e.playAgainButton = play;
        e.quitButton = quit;
        e.firstScene = "Cutscene";

        var off = new System.Collections.Generic.List<Behaviour>();
        off.AddRange(Object.FindObjectsByType<SoulMovement>(FindObjectsSortMode.None));
        off.AddRange(Object.FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None));
        off.AddRange(Object.FindObjectsByType<OrbitCameraInput>(FindObjectsSortMode.None));
        e.disableOnPlay = off.ToArray();
        var hide = new System.Collections.Generic.List<Canvas>();
        if (hud) hide.Add(hud.GetComponent<Canvas>());
        var split = GameObject.Find("SplitScreenUI");
        if (split)
        {
            hide.Add(split.GetComponent<Canvas>());
            var div = split.transform.Find("Divider");
            if (div) e.splitDivider = (RectTransform)div;
        }
        e.hideCanvases = hide.ToArray();
        return e;
    }

    static GameObject Figure(Transform parent, string name, Vector3 localPos, Material mat, bool glow)
    {
        var root = Child(parent, name);
        root.localPosition = localPos;
        root.localRotation = Quaternion.Euler(0f, name == "Reflection" ? 180f : 0f, 0f);
        Prim(PrimitiveType.Capsule, "Body", root, new Vector3(0f, 1f, 0f), Vector3.one, mat, false);
        if (glow) AddLight(root, new Vector3(0f, 1.3f, -0.4f), new Color(1f, 0.8f, 0.5f), 1.2f, 3.5f);
        return root.gameObject;
    }

    static Button MakeButton(Transform parent, string label, Font font, Vector2 pos)
    {
        var go = new GameObject(label + " Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.45f);
        var b = go.AddComponent<Button>();
        var colors = b.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = new Color(1f, 0.85f, 0.55f, 1f);
        colors.selectedColor = new Color(1f, 0.85f, 0.55f, 1f);
        colors.pressedColor = new Color(0.9f, 0.7f, 0.4f, 1f);
        b.colors = colors;
        Anchor((RectTransform)go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(280f, 72f), pos);
        var t = UIText(go.transform, "Label", font, 32, TextAnchor.MiddleCenter);
        Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        t.text = label;
        return b;
    }

    static void MakeMirrorMaterials()
    {
        mMirrorBack = Lit("M_Mirror_Backing", new Color(0.07f, 0.07f, 0.09f), 0.6f);
        mSlotOutline = Unlit("M_Mirror_SlotOutline", new Color(0.85f, 0.92f, 1f, 0.16f));
        MakeTransparent(mSlotOutline);
        mMirrorPiece = Unlit("M_Mirror_Piece", new Color(0.82f, 0.9f, 1f));
        mSeam = Unlit("M_Mirror_Seam", new Color(1f, 0.85f, 0.5f));
        mEndGlass = Glassy("M_Mirror_EndingGlass", new Color(0.75f, 0.82f, 0.9f, 0.22f));
        mMoss = Lit("M_Forest_Moss", new Color(0.18f, 0.42f, 0.2f), 0.1f);
        mFrameWood = Lit("M_Mirror_Frame", new Color(0.3f, 0.22f, 0.15f), 0.45f);
    }
}
