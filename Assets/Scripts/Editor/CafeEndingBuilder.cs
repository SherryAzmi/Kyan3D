using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Tools > Kyan > Build Café Ending — (re)builds Assets/Scenes/Sh/CafeEnding.unity: the café in the
/// morning, the mirror, the person and his true mirror reflection, the cup of coffee, the camera
/// shots, "The End" UI and the CafeEnding director. Adds the scene to the build list after Puzzle3.
/// Level 3's MirrorEnding loads it after its closing message. Wipes the scene before rebuilding.
/// </summary>
public static class CafeEndingBuilder
{
    public const string ScenePath = "Assets/Scenes/Sh/CafeEnding.unity";
    const string ProfilePath = "Assets/Settings/CafeDullProfile.asset";
    const string AnimDir = "Assets/TwoWorlds/Animations";

    [MenuItem("Tools/Kyan/Build Café Ending")]
    static void BuildFromMenu()
    {
        if (!EditorUtility.DisplayDialog("Build Café Ending",
                "This (re)builds CafeEnding.unity from scratch. Continue?", "Build", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    public static void Build()
    {
        var scene = System.IO.File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (var r in scene.GetRootGameObjects()) Object.DestroyImmediate(r);

        // ---------- light and atmosphere ----------
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.26f, 0.27f, 0.31f);
        RenderSettings.fog = false;

        // ---------- the café (morning) ----------
        var env = new GameObject("--- ENVIRONMENT ---").transform;
        var cafe = CafeBuilder.Build(env, night: false);

        var lights = new GameObject("--- LIGHTS ---").transform;
        var sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(lights, false);
        sunGo.transform.rotation = Quaternion.Euler(32f, -95f, 0f); // low morning sun from the window side
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.88f, 0.7f);
        sun.intensity = 1.1f;
        sun.shadows = LightShadows.Soft;
        var glowGo = new GameObject("Eye Glow");
        glowGo.transform.SetParent(lights, false);
        glowGo.transform.position = new Vector3(0f, 1.65f, 1.0f);
        var eyeGlow = glowGo.AddComponent<Light>();
        eyeGlow.type = LightType.Point;
        eyeGlow.color = new Color(1f, 0.7f, 0.35f);
        eyeGlow.range = 2.5f;
        eyeGlow.intensity = 0f;

        // ---------- the mirror (same as the opening) ----------
        var mFrame = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Cutscene/M_CS_Frame.mat");
        // clearer, less shiny glass than the opening's: the morning sun would flare on it, and his eyes must show
        var mGlass = Lit("M_Cafe_MirrorGlass", new Color(0.8f, 0.86f, 0.9f, 0.1f), 0.3f);
        CafeBuilder.MakeTransparent(mGlass);
        var mirror = new GameObject("Mirror").transform;
        mirror.position = new Vector3(0f, 0f, CafeBuilder.MirrorZ);
        var glass = Prim(PrimitiveType.Quad, "Glass", mirror, new Vector3(0f, 1.25f, 0f), new Vector3(1.1f, 2f, 1f), mGlass);
        glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Prim(PrimitiveType.Cube, "Frame Top", mirror, new Vector3(0f, 2.29f, 0f), new Vector3(1.26f, 0.08f, 0.08f), mFrame);
        Prim(PrimitiveType.Cube, "Frame Bottom", mirror, new Vector3(0f, 0.21f, 0f), new Vector3(1.26f, 0.08f, 0.08f), mFrame);
        Prim(PrimitiveType.Cube, "Frame Left", mirror, new Vector3(-0.59f, 1.25f, 0f), new Vector3(0.08f, 2.16f, 0.08f), mFrame);
        Prim(PrimitiveType.Cube, "Frame Right", mirror, new Vector3(0.59f, 1.25f, 0f), new Vector3(0.08f, 2.16f, 0.08f), mFrame);
        Prim(PrimitiveType.Cube, "Foot Left", mirror, new Vector3(-0.55f, 0.1f, 0f), new Vector3(0.12f, 0.2f, 0.5f), mFrame);
        Prim(PrimitiveType.Cube, "Foot Right", mirror, new Vector3(0.55f, 0.1f, 0f), new Vector3(0.12f, 0.2f, 0.5f), mFrame);

        // ---------- the person and his reflection ----------
        var chars = new GameObject("--- CHARACTERS ---").transform;
        var tired = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{AnimDir}/AC_Present.controller");
        var alive = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{AnimDir}/AC_Future.controller");
        // the cheerful Future self (he starts with the tired idle and straightens up as he wakes)
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TwoWorlds/Characters/Character_Future.fbx");
        var presentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TwoWorlds/Characters/Character_Present.fbx");
        EnableIKPass(alive);
        var person = new GameObject("Person").transform;
        person.SetParent(chars, false);
        var personModel = Model(prefab, person, tired);
        var handIK = personModel.AddComponent<HandIK>();
        var mirrorSpace = new GameObject("Mirror Space").transform;
        mirrorSpace.SetParent(chars, false);
        mirrorSpace.localPosition = new Vector3(0f, 0f, 2f * CafeBuilder.MirrorZ);
        mirrorSpace.localScale = new Vector3(1f, 1f, -1f);
        var refl = new GameObject("Reflection").transform;
        refl.SetParent(mirrorSpace, false);
        var reflModel = Model(prefab, refl, tired);
        // a fuller head of hair and dimples; a smirk for the very end (same on the reflection)
        var faceParts = FaceDressing.Dress(personModel, reflModel);

        // ---------- the coffee ----------
        var props = new GameObject("--- COFFEE ---").transform;
        var cup = CafeBuilder.Cup(props, cafe.coffeeSpot.position);
        cup.name = "Coffee";
        cup.rotation = Quaternion.Euler(0f, 90f, 0f);
        var coffee = Prim(PrimitiveType.Cylinder, "Coffee Surface", cup, new Vector3(0f, 0.085f, 0f), new Vector3(0.068f, 0.002f, 0.068f),
                          Lit("M_Cafe_Coffee", new Color(0.24f, 0.14f, 0.08f), 0.6f));
        var steam = Steam(cup);
        var cupCopy = Object.Instantiate(cup.gameObject, mirrorSpace).transform;
        cupCopy.name = "Coffee (reflection)";
        foreach (var ps in cupCopy.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(ps.gameObject);
        var cupRest = new GameObject("Cup Rest").transform;
        cupRest.SetParent(props, false);
        cupRest.position = cafe.seat.position + cafe.seat.forward * 0.42f + Vector3.up * 0.775f;
        cupRest.rotation = cafe.seat.rotation * Quaternion.Euler(0f, 90f, 0f);

        var mr = refl.gameObject.AddComponent<MirrorReflection>();
        mr.source = person;
        mr.sourceModel = personModel.transform;
        mr.model = reflModel.transform;
        mr.mirrorObjects = new[] { cup };
        mr.mirrorCopies = new[] { cupCopy };

        // ---------- other people in the café ----------
        Patrons(cafe, prefab, presentPrefab, alive, mirrorSpace);

        // ---------- post: the grey dawn, washed away as he wakes ----------
        var volGo = new GameObject("Dull Volume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 5f;
        vol.weight = 1f;
        vol.sharedProfile = DullProfile();

        // ---------- camera and shots ----------
        var shots = new GameObject("--- SHOTS ---").transform;
        var shoulder = Shot("Shot Shoulder", shots, new Vector3(0.5f, 1.82f, -1.2f), new Vector3(0f, 1.45f, 1.6f));
        var reflection = Shot("Shot Reflection", shots, new Vector3(0.04f, 1.56f, 1.42f), new Vector3(0f, 1.52f, 3.2f));
        var face = Shot("Shot Face", shots, new Vector3(0.22f, 1.55f, 0.8f), new Vector3(0f, 1.5f, 0f));
        var wide = Shot("Shot Wide", shots, new Vector3(3.9f, 2.55f, 0.9f), new Vector3(-0.8f, 0.9f, -2.2f));
        var seatPos = cafe.seat.position;
        var final = Shot("Shot Final", shots, seatPos + new Vector3(1.0f, 1.55f, 2.3f), seatPos + new Vector3(0f, 0.9f, 0.1f));
        var finalEnd = Shot("Shot Final End", shots, new Vector3(-0.9f, 2.85f, 0.95f), seatPos + new Vector3(0.2f, 0.3f, -1.2f));
        var signShot = Shot("Shot Sign", shots, cafe.sign.position + new Vector3(0.35f, -0.06f, -1.35f), cafe.sign.position);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 60f;
        camGo.transform.SetPositionAndRotation(shoulder.position, shoulder.rotation);
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

        // ---------- UI ----------
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasGo = new GameObject("EndUI");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var line = UIText(canvasGo.transform, "Line", font, 50, FontStyle.Italic, new Color(1f, 0.97f, 0.9f, 0f));
        Stretch(line.rectTransform, new Vector2(0f, 0.06f), new Vector2(1f, 0.2f));
        line.text = "I feel warm and full inside.";
        var lineShadow = line.gameObject.AddComponent<Shadow>();
        lineShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        lineShadow.effectDistance = new Vector2(2f, -2f);
        var theEnd = UIText(canvasGo.transform, "The End", font, 110, FontStyle.Italic, new Color(1f, 0.95f, 0.86f, 0f));
        Stretch(theEnd.rectTransform, new Vector2(0f, 0.55f), new Vector2(1f, 0.85f));
        theEnd.text = "The End";
        var shadow = theEnd.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(3f, -3f);
        var buttonsGo = new GameObject("Buttons", typeof(RectTransform));
        buttonsGo.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)buttonsGo.transform, new Vector2(0f, 0.1f), new Vector2(1f, 0.25f));
        var group = buttonsGo.AddComponent<CanvasGroup>();
        var play = MakeButton(buttonsGo.transform, "Play again", font, new Vector2(-170f, 0f));
        var quit = MakeButton(buttonsGo.transform, "Quit", font, new Vector2(170f, 0f));
        var fadeGo = new GameObject("Fade", typeof(RectTransform));
        fadeGo.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)fadeGo.transform, Vector2.zero, Vector2.one);
        var fade = fadeGo.AddComponent<Image>();
        fade.color = Color.white;
        fade.raycastTarget = false;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        // ---------- sound ----------
        var dirGo = new GameObject("CafeEndingDirector");
        var music = dirGo.AddComponent<AudioSource>();
        music.clip = SoundSynth.Load("music_ending");
        music.loop = true;
        music.playOnAwake = false;
        var sfx = dirGo.AddComponent<AudioSource>();
        sfx.playOnAwake = false;

        // ---------- director ----------
        var d = dirGo.AddComponent<CafeEnding>();
        d.cam = cam;
        d.shotShoulder = shoulder; d.shotReflection = reflection; d.shotFace = face;
        d.shotWide = wide; d.shotFinal = final; d.shotFinalEnd = finalEnd; d.shotSign = signShot;
        d.sign = cafe.sign;
        d.smirk = faceParts.smirk;
        d.smile = faceParts.smile;
        d.mouth = faceParts.mouth;
        d.dimples = faceParts.dimples;
        d.person = person;
        d.personAnim = personModel.GetComponent<Animator>();
        d.hands = handIK;
        d.lineText = line;
        d.tiredController = tired;
        d.aliveController = alive;
        d.reflection = mr;
        d.cupReflection = cupCopy;
        d.counterStand = cafe.counterStand; d.coffeeSpot = cafe.coffeeSpot;
        d.seatApproach = cafe.seatApproach; d.seat = cafe.seat; d.cupRest = cupRest;
        d.cup = cup;
        d.steam = steam;
        d.eyeGlow = eyeGlow;
        d.lamps = cafe.lamps.ToArray();
        d.bulbs = cafe.bulbs.ToArray();
        d.leds = cafe.leds.ToArray();
        d.windows = cafe.windows.ToArray();
        d.bulbOn = CafeBuilder.Shared("M_Cafe_BulbOn"); d.bulbOff = CafeBuilder.Shared("M_Cafe_BulbOff");
        d.ledOn = CafeBuilder.Shared("M_Cafe_LightStripOn"); d.ledOff = CafeBuilder.Shared("M_Cafe_LightStripOff");
        d.windowDawn = CafeBuilder.Shared("M_Cafe_WindowNight"); d.windowDay = CafeBuilder.Shared("M_Cafe_WindowDay");
        d.sun = sun;
        d.windowLight = cafe.windowLight;
        d.dullVolume = vol;
        d.fadeImage = fade;
        d.theEndText = theEnd;
        d.buttons = group;
        d.playAgainButton = play;
        d.quitButton = quit;
        d.music = music;
        d.sfx = sfx;
        d.awakenClip = SoundSynth.Load("sfx_mirror_whole");
        d.pickupClip = SoundSynth.Load("sfx_pickup");
        d.sitClip = SoundSynth.Load("sfx_place");

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();
        Debug.Log("[CafeEndingBuilder] CafeEnding.unity built.");
    }

    // ================================================================== customers

    static void Patrons(CafeBuilder.Refs cafe, GameObject future, GameObject present, RuntimeAnimatorController ctrl, Transform mirrorSpace)
    {
        var root = new GameObject("--- CUSTOMERS ---").transform;
        var mBookA = Lit("M_Cafe_BookRed", new Color(0.62f, 0.2f, 0.18f), 0.2f);
        var mPages = Lit("M_Cafe_Pages", new Color(0.95f, 0.93f, 0.86f), 0.1f);
        var mPaper = Lit("M_Cafe_Newspaper", new Color(0.86f, 0.85f, 0.8f), 0.05f);
        var mPrint = Lit("M_Cafe_Print", new Color(0.25f, 0.25f, 0.27f), 0.05f);
        var mDark = Lit("M_Cafe_Gadget", new Color(0.1f, 0.1f, 0.12f), 0.5f);
        var mSilver = Lit("M_Cafe_Silver", new Color(0.7f, 0.72f, 0.75f), 0.6f);
        var mScreen = Lit("M_Cafe_Screen", new Color(0.55f, 0.75f, 0.9f), 0.8f);
        var mPhones = Lit("M_Cafe_Headphones", new Color(0.95f, 0.45f, 0.35f), 0.4f);

        // round table: a reader and a coffee drinker; small table: the newspaper; bar: music and a laptop
        var c = cafe.chairs;
        var s = cafe.stools;

        // 1. reading a book
        var book = new GameObject("Book").transform;
        book.SetParent(root, false);
        Prim(PrimitiveType.Cube, "Cover", book, Vector3.zero, new Vector3(0.3f, 0.22f, 0.025f), mBookA);
        Prim(PrimitiveType.Cube, "Pages", book, new Vector3(0f, 0f, -0.008f), new Vector3(0.29f, 0.21f, 0.02f), mPages);
        Prim(PrimitiveType.Cube, "Spine", book, new Vector3(0f, 0f, -0.019f), new Vector3(0.01f, 0.21f, 0.004f), mBookA);
        Patron(root, "Reader", present, ctrl, c[0], 0.48f, false, CafePatron.Activity.Book, book, null,
               new Color(0.62f, 0.33f, 0.3f), new Color(0.2f, 0.22f, 0.3f), new Color(0.12f, 0.08f, 0.06f), 0.0f, mirrorSpace);

        // 2. a coffee, sipped now and then
        var cupSpot = new GameObject("Cup Spot").transform;
        cupSpot.SetParent(root, false);
        cupSpot.position = c[1].position + c[1].forward * 0.36f + Vector3.up * 0.775f;
        cupSpot.rotation = Quaternion.LookRotation(c[1].forward);
        var cup = CafeBuilder.Cup(root, cupSpot.position);
        cup.rotation = cupSpot.rotation;
        Patron(root, "Coffee Drinker", future, ctrl, c[1], 0.48f, false, CafePatron.Activity.Coffee, cup, cupSpot,
               new Color(0.3f, 0.45f, 0.65f), new Color(0.82f, 0.77f, 0.66f), new Color(0.25f, 0.16f, 0.1f), 2.3f, mirrorSpace);

        // 3. the newspaper
        var paper = new GameObject("Newspaper").transform;
        paper.SetParent(root, false);
        Prim(PrimitiveType.Cube, "Sheet", paper, Vector3.zero, new Vector3(0.56f, 0.38f, 0.006f), mPaper);
        for (int i = 0; i < 6; i++)
        {
            Prim(PrimitiveType.Cube, "Print", paper, new Vector3(-0.13f, 0.12f - i * 0.05f, -0.004f), new Vector3(0.22f, 0.02f, 0.002f), mPrint);
            Prim(PrimitiveType.Cube, "Print", paper, new Vector3(0.14f, 0.12f - i * 0.05f, 0.004f), new Vector3(0.22f, 0.02f, 0.002f), mPrint);
        }
        Prim(PrimitiveType.Cube, "Photo", paper, new Vector3(0.14f, 0.05f, -0.004f), new Vector3(0.2f, 0.14f, 0.002f), mPrint);
        Patron(root, "Newspaper Reader", present, ctrl, c[3], 0.48f, false, CafePatron.Activity.Newspaper, paper, null,
               new Color(0.46f, 0.5f, 0.36f), new Color(0.15f, 0.15f, 0.17f), new Color(0.6f, 0.6f, 0.6f), 4.1f, mirrorSpace);

        // 4. music on headphones, at the bar
        var phones = new GameObject("Headphones").transform;
        phones.SetParent(root, false);
        Prim(PrimitiveType.Cube, "Band", phones, new Vector3(0f, 0.02f, 0f), new Vector3(0.21f, 0.025f, 0.04f), mPhones);
        foreach (var side in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Cube, "Band Side", phones, new Vector3(side * 0.1f, -0.04f, 0f), new Vector3(0.02f, 0.11f, 0.035f), mPhones);
            var ear = Prim(PrimitiveType.Cylinder, "Ear Cup", phones, new Vector3(side * 0.105f, -0.11f, 0f), new Vector3(0.08f, 0.02f, 0.08f), mDark);
            ear.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        var musicSeat = SeatAt(root, s[2]);
        Patron(root, "Music Listener", future, ctrl, musicSeat, 0.76f, true, CafePatron.Activity.Music, phones, null,
               new Color(0.6f, 0.35f, 0.6f), new Color(0.15f, 0.2f, 0.35f), new Color(0.08f, 0.06f, 0.05f), 1.2f, mirrorSpace);

        // 5. typing on a laptop, at the bar
        var laptopSeat = SeatAt(root, s[0]);
        var laptop = new GameObject("Laptop").transform;
        laptop.SetParent(root, false);
        laptop.position = laptopSeat.position + laptopSeat.forward * 0.5f + Vector3.up * 1.078f;
        laptop.rotation = Quaternion.LookRotation(-laptopSeat.forward); // keyboard towards the person
        Prim(PrimitiveType.Cube, "Base", laptop, Vector3.zero, new Vector3(0.32f, 0.015f, 0.22f), mSilver);
        var screen = new GameObject("Screen").transform;
        screen.SetParent(laptop, false);
        screen.localPosition = new Vector3(0f, 0.0f, -0.11f);
        screen.localRotation = Quaternion.Euler(-105f, 0f, 0f);
        Prim(PrimitiveType.Cube, "Lid", screen, new Vector3(0f, 0f, 0.11f), new Vector3(0.32f, 0.012f, 0.22f), mSilver);
        Prim(PrimitiveType.Cube, "Display", screen, new Vector3(0f, -0.007f, 0.11f), new Vector3(0.29f, 0.002f, 0.19f), mScreen);
        Patron(root, "Laptop User", present, ctrl, laptopSeat, 0.76f, true, CafePatron.Activity.Laptop, laptop, null,
               new Color(0.85f, 0.82f, 0.74f), new Color(0.3f, 0.25f, 0.2f), new Color(0.42f, 0.26f, 0.13f), 3.3f, mirrorSpace);
    }

    static Transform SeatAt(Transform parent, Transform stool)
    {
        var t = new GameObject(stool.name + " Seat").transform;
        t.SetParent(parent, false);
        t.SetPositionAndRotation(stool.position, stool.rotation);
        return t;
    }

    static void Patron(Transform parent, string name, GameObject prefab, RuntimeAnimatorController ctrl, Transform seat,
                       float seatHeight, bool stool, CafePatron.Activity activity, Transform prop, Transform cupRest,
                       Color top, Color bottom, Color hair, float phase, Transform mirrorSpace)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(seat.position, seat.rotation);
        var model = Model(prefab, root, ctrl);
        var ik = model.AddComponent<HandIK>();
        var p = root.gameObject.AddComponent<CafePatron>();
        p.activity = activity;
        p.anim = model.GetComponent<Animator>();
        p.ik = ik;
        p.seat = seat;
        p.seatHeight = seatHeight;
        p.stool = stool;
        p.prop = prop;
        p.cupRest = cupRest;
        p.top = top; p.bottom = bottom; p.hair = hair;
        p.phase = phase;

        // their mirror image, for when they show in the mirror
        var refl = new GameObject(name + " (reflection)").transform;
        refl.SetParent(mirrorSpace, false);
        var reflModel = Model(prefab, refl, ctrl);
        var mr = refl.gameObject.AddComponent<MirrorReflection>();
        mr.source = root;
        mr.sourceModel = model.transform;
        mr.model = reflModel.transform;
        if (prop)
        {
            var copy = Object.Instantiate(prop.gameObject, mirrorSpace).transform;
            copy.name = prop.name + " (reflection)";
            mr.mirrorObjects = new[] { prop };
            mr.mirrorCopies = new[] { copy };
        }
        p.reflectionModel = reflModel.transform;
    }

    /// <summary>Hand IK (holding the cup, books...) needs "IK Pass" on the controller's base layer.</summary>
    static void EnableIKPass(RuntimeAnimatorController rc)
    {
        if (!(rc is UnityEditor.Animations.AnimatorController ac)) return;
        var layers = ac.layers;
        if (layers.Length == 0 || layers[0].iKPass) return;
        layers[0].iKPass = true;
        ac.layers = layers;
        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
    }

    // ================================================================== helpers

    static GameObject Model(GameObject prefab, Transform parent, RuntimeAnimatorController controller)
    {
        var m = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        m.name = "Character";
        m.transform.SetParent(parent, false);
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.identity;
        var anim = m.GetComponent<Animator>();
        if (!anim) anim = m.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var smr in m.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
        return m;
    }

    static ParticleSystem Steam(Transform cup)
    {
        var go = new GameObject("Steam");
        go.transform.SetParent(cup, false);
        go.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 2.2f;
        main.startSpeed = 0.12f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new Color(1f, 1f, 1f, 0.22f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        var em = ps.emission;
        em.rateOverTime = 7f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.025f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.25f, 0.25f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2f));
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.05f;
        noise.frequency = 0.8f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        var smoke = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Cutscene/M_CS_Smoke.mat");
        if (smoke) r.sharedMaterial = smoke;
        return ps;
    }

    static VolumeProfile DullProfile()
    {
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (!p)
        {
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, ProfilePath);
        }
        if (!p.TryGet<ColorAdjustments>(out var ca)) ca = p.Add<ColorAdjustments>(true);
        ca.saturation.Override(-70f);
        ca.postExposure.Override(-0.15f);
        ca.contrast.Override(8f);
        ca.colorFilter.Override(new Color(0.82f, 0.88f, 1f));
        if (!p.TryGet<Vignette>(out var vg)) vg = p.Add<Vignette>(true);
        vg.intensity.Override(0.38f);
        vg.smoothness.Override(0.5f);
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
    }

    static void AddToBuildSettings()
    {
        var list = EditorBuildSettings.scenes.ToList();
        list.RemoveAll(s => s.path == ScenePath);
        int at = list.FindIndex(s => s.path.EndsWith("/Puzzle3.unity"));
        list.Insert(at >= 0 ? at + 1 : list.Count, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static Transform Shot(string name, Transform parent, Vector3 pos, Vector3 lookAt)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = pos;
        t.rotation = Quaternion.LookRotation(lookAt - pos);
        return t;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        if (m) g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static Material Lit(string name, Color c, float smooth)
    {
        string path = $"Assets/Materials/Cafe/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Text UIText(Transform parent, string name, Font font, int size, FontStyle style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Button MakeButton(Transform parent, string label, Font font, Vector2 pos)
    {
        var go = new GameObject(label + " Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.45f);
        var b = go.AddComponent<Button>();
        var colors = b.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.85f, 0.55f, 1f);
        colors.selectedColor = new Color(1f, 0.85f, 0.55f, 1f);
        colors.pressedColor = new Color(0.9f, 0.7f, 0.4f, 1f);
        b.colors = colors;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(280f, 72f);
        rt.anchoredPosition = pos;
        var t = UIText(go.transform, "Label", font, 32, FontStyle.Normal, Color.white);
        Stretch(t.rectTransform, Vector2.zero, Vector2.one);
        t.text = label;
        return b;
    }
}
