using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// The café the story starts and ends in: a small low-poly coffee shop (dark green counters, brown wood,
/// cream walls, menu board, shelves of jars and bottles, espresso machine, pastry case, pendant lamps,
/// a window with a bar and stools, tables and chairs, a plant), built from primitives like the rest of
/// the game.
///
/// The standing mirror sits against the back wall, which has an opening the size of the glass. Behind it
/// is a flipped copy of the café, so the mirror "reflects" the room behind the person (the reflection
/// figure already stands in that flipped space).
///
/// Used by:
///  - Tools > Kyan > Dress Opening Café: puts the closed, night-time café around the opening mirror in
///    Cutscene.unity (keeps the mirror, characters, shots and director; MirrorCutsceneBuilder calls it too).
///  - CafeEndingBuilder: the same café in the morning for the final cutscene.
/// Room (local): x -5..5, z -7..1.72 (back wall), height 3.2. The person stands at the origin facing +z.
/// </summary>
public static class CafeBuilder
{
    public const float MirrorZ = 1.6f;
    const float X0 = -5f, X1 = 5f, Z0 = -7f, ZBack = 1.72f, H = 3.2f, WallT = 0.15f;
    const string MatDir = "Assets/Materials/Cafe";
    const string CutscenePath = "Assets/Scenes/Sh/Cutscene.unity";

    public class Refs
    {
        public Transform root, reflection;
        public Transform coffeeSpot, seat, seatApproach, counterStand;
        public Transform sign; // the framed "What is the meaning of your life?" on the mirror wall
        public readonly List<Light> lamps = new List<Light>();       // pendant lamps (real room and reflection)
        public readonly List<Transform> chairs = new List<Transform>(); // free chairs (round table, small table), facing their table
        public readonly List<Transform> stools = new List<Transform>(); // bar stools under the window
        public readonly List<Renderer> bulbs = new List<Renderer>(); // their bulbs
        public readonly List<Renderer> leds = new List<Renderer>();  // light strips along the ceiling
        public readonly List<Renderer> windows = new List<Renderer>();
        public Light windowLight;                                    // moon (night) or sun (day) through the window
        public Light windowLightReflection;
    }

    // materials
    static Material mFloor, mFloorLine, mWall, mCeiling, mGreen, mGreenDark, mTop, mWood, mWoodDark, mBrown,
                    mChair, mMetal, mMenu, mInk, mJar, mBottle, mCup, mPlant, mPlantDark, mPot, mBag, mBagDark,
                    mPastry, mPastryDark, mCaseGlass, mBulbOn, mBulbOff, mLedOn, mLedOff, mWindowNight, mWindowDay;

    // ================================================================== menu: opening

    [MenuItem("Tools/Kyan/Dress Opening Café")]
    static void DressOpeningFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(CutscenePath, OpenSceneMode.Single);
        DressOpening();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[CafeBuilder] The opening cutscene now takes place in the café.");
    }

    /// <summary>Replaces the opening's plain floor and back wall with the night café (open scene: Cutscene).</summary>
    public static void DressOpening()
    {
        var envGo = GameObject.Find("--- ENVIRONMENT ---");
        var env = envGo ? envGo.transform : new GameObject("--- ENVIRONMENT ---").transform;
        foreach (var n in new[] { "Floor", "BackWall", "Café", "Café (reflection)" })
        {
            var old = env.Find(n);
            if (old) Object.DestroyImmediate(old.gameObject);
        }
        // a little more light than the bare mirror room had, so the café reads (it still dims with the thoughts)
        RenderSettings.ambientLight = new Color(0.24f, 0.23f, 0.27f);
        var fill = GameObject.Find("Cold Fill");
        if (fill && fill.TryGetComponent<Light>(out var fl)) fl.intensity = 0.35f;

        var refs = Build(env, night: true);
        // a pendant shade for the cutscene's warm bulb, which hangs over the person
        var bulb = GameObject.Find("Bulb");
        if (bulb) PendantShade(refs.root, refs.root.InverseTransformPoint(bulb.transform.position) + Vector3.up * 0.14f, true);

        var d = Object.FindAnyObjectByType<MirrorCutscene>();
        if (d)
        {
            // the café's lights dim with the rest of the room as the dark thoughts grow
            var lights = new List<Light>();
            foreach (var l in d.roomLights) if (l && !l.transform.IsChildOf(refs.root) && !(refs.reflection && l.transform.IsChildOf(refs.reflection))) lights.Add(l);
            foreach (var l in refs.lamps) if (l.enabled) lights.Add(l);
            if (refs.windowLight) lights.Add(refs.windowLight);
            if (refs.windowLightReflection) lights.Add(refs.windowLightReflection);
            d.roomLights = lights.ToArray();
            d.prelude = Prelude(refs, d);
            EditorUtility.SetDirty(d);
        }
    }

    // ------------------------------------------------------------------ the opening's first moments

    /// <summary>Barista, shots and subtitle for CafePrelude: he orders a coffee, stares at the sign, walks to the mirror.</summary>
    static CafePrelude Prelude(Refs refs, MirrorCutscene d)
    {
        foreach (var n in new[] { "--- PRELUDE ---" })
        {
            var old = GameObject.Find(n);
            if (old) Object.DestroyImmediate(old);
        }
        var root = new GameObject("--- PRELUDE ---").transform;

        // the barista, behind the service counter, facing the customer
        var futurePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TwoWorlds/Characters/Character_Future.fbx");
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/TwoWorlds/Animations/AC_Future.controller");
        var barista = new GameObject("Barista").transform;
        barista.SetParent(root, false);
        barista.SetPositionAndRotation(new Vector3(-3.78f, 0f, refs.counterStand.position.z), Quaternion.Euler(0f, 90f, 0f));
        Animator bAnim = null;
        if (futurePrefab)
        {
            var m = (GameObject)PrefabUtility.InstantiatePrefab(futurePrefab);
            m.name = "Character";
            m.transform.SetParent(barista, false);
            bAnim = m.GetComponent<Animator>();
            if (!bAnim) bAnim = m.AddComponent<Animator>();
            bAnim.runtimeAnimatorController = ctrl;
            bAnim.applyRootMotion = false;
            bAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // shots
        Transform Shot(string n, Vector3 pos, Vector3 look)
        {
            var t = new GameObject(n).transform;
            t.SetParent(root, false);
            t.position = pos;
            t.rotation = Quaternion.LookRotation(look - pos);
            return t;
        }
        var sign = refs.sign.position;
        var stand = refs.counterStand.position;
        var shotCounter = Shot("Shot Counter", new Vector3(-1.15f, 1.75f, 0.35f), new Vector3(-2.9f, 1.2f, stand.z - 0.1f));
        var shotSign = Shot("Shot Sign", sign + new Vector3(0.25f, -0.08f, -1.25f), sign);
        var head = stand + Vector3.up * 1.52f;
        var toSign = (sign - head).normalized;
        var shotStare = Shot("Shot Stare", head + toSign * 0.95f + new Vector3(0.18f, 0.02f, 0f), head);
        var shotWalk = Shot("Shot Walk", new Vector3(2.4f, 2.05f, -3.4f), new Vector3(-1f, 1f, -0.8f));

        // subtitle line on the cutscene's canvas
        Text subtitle = null;
        var canvas = GameObject.Find("CutsceneUI");
        if (canvas)
        {
            var old = canvas.transform.Find("Subtitle");
            if (old) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("Subtitle", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetSiblingIndex(0); // under the fade
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0.05f); rt.anchorMax = new Vector2(1f, 0.16f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            subtitle = go.AddComponent<Text>();
            subtitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            subtitle.fontSize = 40;
            subtitle.fontStyle = FontStyle.Italic;
            subtitle.alignment = TextAnchor.MiddleCenter;
            subtitle.color = new Color(0.95f, 0.93f, 0.9f, 0f);
            subtitle.raycastTarget = false;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.7f);
            sh.effectDistance = new Vector2(2f, -2f);
        }

        var p = root.gameObject.AddComponent<CafePrelude>();
        p.character = d.character;
        p.characterAnim = d.character ? d.character.GetComponentInChildren<Animator>() : null;
        p.reflection = d.reflection ? d.reflection.transform : null;
        p.reflectionAnim = d.reflection ? d.reflection.GetComponentInChildren<Animator>() : null;
        p.barista = barista;
        p.baristaAnim = bAnim;
        p.counterStand = refs.counterStand;
        p.sign = refs.sign;
        p.shotCounter = shotCounter; p.shotSign = shotSign; p.shotStare = shotStare; p.shotWalk = shotWalk;
        p.subtitle = subtitle;
        p.mirrorZ = MirrorZ;
        return p;
    }

    // ================================================================== the café

    /// <summary>Builds the café (and its flipped copy behind the mirror) under 'parent' at its origin.</summary>
    public static Refs Build(Transform parent, bool night)
    {
        Materials();
        var refs = new Refs();
        var root = new GameObject("Café").transform;
        root.SetParent(parent, false);
        refs.root = root;

        Shell(root, night, refs);
        Counters(root, refs);
        WindowSide(root, night, refs);
        Tables(root, refs, night);
        Lamps(root, night, refs);

        // ---- the flipped copy behind the mirror (without the mirror wall itself) ----
        var copy = Object.Instantiate(root.gameObject, parent).transform;
        copy.name = "Café (reflection)";
        copy.localPosition = new Vector3(0f, 0f, 2f * MirrorZ);
        copy.localRotation = Quaternion.identity;
        copy.localScale = new Vector3(1f, 1f, -1f);
        var wall = copy.Find("Mirror Wall");
        if (wall) Object.DestroyImmediate(wall.gameObject);
        foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var n in new[] { "Coffee Spot", "Seat", "Seat Approach", "Counter Stand" })
        {
            var t = FindDeep(copy, n);
            if (t) Object.DestroyImmediate(t.gameObject);
        }
        refs.reflection = copy;
        foreach (var l in copy.GetComponentsInChildren<Light>(true))
        {
            if (l.name == "Window Light") { refs.windowLightReflection = l; l.shadows = LightShadows.None; }
            else if (l.name == "Lamp Light") refs.lamps.Add(l);
        }
        foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name == "Bulb") refs.bulbs.Add(r);
            else if (r.name == "Light Strip") refs.leds.Add(r);
            else if (r.name == "Window Glass") refs.windows.Add(r);
        }
        return refs;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var f = FindDeep(c, name); if (f) return f; }
        return null;
    }

    // ------------------------------------------------------------------ walls, floor, ceiling

    static void Shell(Transform root, bool night, Refs refs)
    {
        float midZ = (Z0 + ZBack) * 0.5f, depth = ZBack - Z0;
        var shell = Child(root, "Room");

        var floor = Box("Floor", shell, new Vector3(0f, -0.05f, midZ), new Vector3(X1 - X0, 0.1f, depth), mFloor, true);
        for (float x = X0 + 0.55f; x < X1; x += 0.55f)
            Box("Plank Line", floor.transform.parent, new Vector3(x, 0.002f, midZ), new Vector3(0.015f, 0.004f, depth), mFloorLine);
        Box("Ceiling", shell, new Vector3(0f, H + 0.05f, midZ), new Vector3(X1 - X0 + 0.3f, 0.1f, depth + 0.3f), mCeiling);

        // left wall (behind the counters)
        Box("Wall Left", shell, new Vector3(X0 - WallT / 2f, H / 2f, midZ), new Vector3(WallT, H, depth), mWall);
        // right wall with the window opening (z -3.5..-1.5, y 1.0..2.4)
        float wz0 = -3.5f, wz1 = -1.5f, wy0 = 1.0f, wy1 = 2.4f, rx = X1 + WallT / 2f;
        Box("Wall Right", shell, new Vector3(rx, wy0 / 2f, midZ), new Vector3(WallT, wy0, depth), mWall);
        Box("Wall Right", shell, new Vector3(rx, (wy1 + H) / 2f, midZ), new Vector3(WallT, H - wy1, depth), mWall);
        Box("Wall Right", shell, new Vector3(rx, (wy0 + wy1) / 2f, (Z0 + wz0) / 2f), new Vector3(WallT, wy1 - wy0, wz0 - Z0), mWall);
        Box("Wall Right", shell, new Vector3(rx, (wy0 + wy1) / 2f, (wz1 + ZBack) / 2f), new Vector3(WallT, wy1 - wy0, ZBack - wz1), mWall);
        // front wall with the door (x 2.2..3.3, up to 2.2)
        float dx0 = 2.2f, dx1 = 3.3f, dh = 2.2f, fz = Z0 - WallT / 2f;
        Box("Wall Front", shell, new Vector3((X0 + dx0) / 2f, H / 2f, fz), new Vector3(dx0 - X0, H, WallT), mWall);
        Box("Wall Front", shell, new Vector3((dx1 + X1) / 2f, H / 2f, fz), new Vector3(X1 - dx1, H, WallT), mWall);
        Box("Wall Front", shell, new Vector3((dx0 + dx1) / 2f, (dh + H) / 2f, fz), new Vector3(dx1 - dx0, H - dh, WallT), mWall);

        // green wainscot + wood rail on the side and front walls
        float wh = 0.9f;
        Box("Wainscot", shell, new Vector3(X0 + 0.015f, wh / 2f, midZ), new Vector3(0.03f, wh, depth), mGreenDark);
        Box("Wainscot", shell, new Vector3(X1 - 0.015f, wh / 2f, midZ), new Vector3(0.03f, wh, depth), mGreenDark);
        Box("Wainscot", shell, new Vector3((X0 + dx0) / 2f, wh / 2f, Z0 + 0.015f), new Vector3(dx0 - X0, wh, 0.03f), mGreenDark);
        Box("Wainscot", shell, new Vector3((dx1 + X1) / 2f, wh / 2f, Z0 + 0.015f), new Vector3(X1 - dx1, wh, 0.03f), mGreenDark);
        Box("Rail", shell, new Vector3(X0 + 0.03f, wh, midZ), new Vector3(0.05f, 0.05f, depth), mWood);
        Box("Rail", shell, new Vector3(X1 - 0.03f, wh, midZ), new Vector3(0.05f, 0.05f, depth), mWood);
        Box("Rail", shell, new Vector3((X0 + dx0) / 2f, wh, Z0 + 0.03f), new Vector3(dx0 - X0, 0.05f, 0.05f), mWood);
        Box("Rail", shell, new Vector3((dx1 + X1) / 2f, wh, Z0 + 0.03f), new Vector3(X1 - dx1, 0.05f, 0.05f), mWood);

        // light strips along the top of the walls (like the reference)
        var led = night ? mLedOff : mLedOn;
        refs.leds.Add(Box("Light Strip", shell, new Vector3(X0 + 0.04f, H - 0.04f, midZ), new Vector3(0.04f, 0.03f, depth), led, shadow: false).GetComponent<Renderer>());
        refs.leds.Add(Box("Light Strip", shell, new Vector3(X1 - 0.04f, H - 0.04f, midZ), new Vector3(0.04f, 0.03f, depth), led, shadow: false).GetComponent<Renderer>());

        // door
        var door = Child(shell, "Door");
        Box("Door Frame", door, new Vector3(dx0 + 0.04f, dh / 2f, Z0 + 0.02f), new Vector3(0.08f, dh, 0.12f), mWoodDark);
        Box("Door Frame", door, new Vector3(dx1 - 0.04f, dh / 2f, Z0 + 0.02f), new Vector3(0.08f, dh, 0.12f), mWoodDark);
        Box("Door Frame", door, new Vector3((dx0 + dx1) / 2f, dh - 0.04f, Z0 + 0.02f), new Vector3(dx1 - dx0, 0.08f, 0.12f), mWoodDark);
        Box("Door", door, new Vector3((dx0 + dx1) / 2f, (dh - 0.08f) / 2f, Z0 - 0.02f), new Vector3(dx1 - dx0 - 0.16f, dh - 0.08f, 0.05f), mWood);
        refs.windows.Add(Box("Window Glass", door, new Vector3((dx0 + dx1) / 2f, 1.5f, Z0 + 0.01f), new Vector3(0.6f, 0.8f, 0.02f), night ? mWindowNight : mWindowDay, shadow: false).GetComponent<Renderer>());
        Box("Handle", door, new Vector3(dx0 + 0.2f, 1.0f, Z0 + 0.04f), new Vector3(0.04f, 0.2f, 0.04f), mMetal);
        Box("Sign", door, new Vector3((dx0 + dx1) / 2f, 1.0f, Z0 + 0.03f), new Vector3(0.36f, 0.16f, 0.01f), mMenu);
        // pictures on the front wall
        Picture(shell, new Vector3(-1.4f, 1.8f, Z0 + 0.02f), new Vector2(0.7f, 0.5f), 0f);
        Picture(shell, new Vector3(-0.4f, 1.65f, Z0 + 0.02f), new Vector2(0.45f, 0.6f), 0f);

        // ---- the mirror wall: an opening exactly the size of the mirror glass ----
        var mw = Child(root, "Mirror Wall");
        float bz = ZBack + WallT / 2f, hx = 0.55f, hy0 = 0.25f, hy1 = 2.25f;
        Box("Wall Back", mw, new Vector3((X0 - hx) / 2f, H / 2f, bz), new Vector3(-hx - X0, H, WallT), mWall);
        Box("Wall Back", mw, new Vector3((hx + X1) / 2f, H / 2f, bz), new Vector3(X1 - hx, H, WallT), mWall);
        Box("Wall Back", mw, new Vector3(0f, (hy1 + H) / 2f, bz), new Vector3(2f * hx, H - hy1, WallT), mWall);
        Box("Wall Back", mw, new Vector3(0f, hy0 / 2f, bz), new Vector3(2f * hx, hy0, WallT), mWall);
        Box("Wainscot", mw, new Vector3((X0 - hx) / 2f, wh / 2f, ZBack - 0.015f), new Vector3(-hx - X0, wh, 0.03f), mGreenDark);
        Box("Wainscot", mw, new Vector3((hx + X1) / 2f, wh / 2f, ZBack - 0.015f), new Vector3(X1 - hx, wh, 0.03f), mGreenDark);
        Box("Rail", mw, new Vector3((X0 - hx) / 2f, wh, ZBack - 0.03f), new Vector3(-hx - X0, 0.05f, 0.05f), mWood);
        Box("Rail", mw, new Vector3((hx + X1) / 2f, wh, ZBack - 0.03f), new Vector3(X1 - hx, 0.05f, 0.05f), mWood);
        refs.leds.Add(Box("Light Strip", mw, new Vector3(0f, H - 0.04f, ZBack - 0.04f), new Vector3(X1 - X0, 0.03f, 0.04f), led, shadow: false).GetComponent<Renderer>());

        // menu board (left of the mirror)
        var menu = Child(mw, "Menu Board");
        menu.localPosition = new Vector3(-2.9f, 2.0f, ZBack - 0.03f);
        Box("Frame", menu, Vector3.zero, new Vector3(1.3f, 0.95f, 0.04f), mGreen);
        Box("Board", menu, new Vector3(0f, 0f, -0.01f), new Vector3(1.18f, 0.83f, 0.04f), mMenu);
        Box("Title", menu, new Vector3(0f, 0.3f, -0.035f), new Vector3(0.36f, 0.06f, 0.01f), mInk);
        for (int i = 0; i < 4; i++)
        {
            Box("Line", menu, new Vector3(-0.2f, 0.13f - i * 0.13f, -0.035f), new Vector3(0.5f, 0.03f, 0.01f), mInk);
            Box("Price", menu, new Vector3(0.38f, 0.13f - i * 0.13f, -0.035f), new Vector3(0.1f, 0.03f, 0.01f), mInk);
        }
        // the framed question, between the menu and the mirror
        var signT = Child(mw, "Sign");
        signT.localPosition = new Vector3(-1.5f, 1.72f, ZBack - 0.03f);
        Box("Frame", signT, Vector3.zero, new Vector3(0.66f, 0.5f, 0.04f), mWoodDark);
        Box("Card", signT, new Vector3(0f, 0f, -0.012f), new Vector3(0.56f, 0.4f, 0.03f), mMenu);
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(signT, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, -0.03f);
        var tm = textGo.AddComponent<TextMesh>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.font = font;
        tm.text = "What is the\nmeaning of\nyour life?";
        tm.fontSize = 64;
        tm.characterSize = 0.0105f;
        tm.lineSpacing = 0.95f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = new Color(0.05f, 0.07f, 0.07f);
        textGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        refs.sign = signT;

        // floating shelves (right of the mirror)
        var sh = Child(mw, "Shelves");
        sh.localPosition = new Vector3(2.7f, 0f, ZBack - 0.14f);
        for (int i = 0; i < 2; i++)
        {
            float y = 1.55f + i * 0.55f;
            Box("Shelf", sh, new Vector3(0f, y, 0f), new Vector3(1.4f, 0.05f, 0.26f), mWood);
            Jar(sh, new Vector3(-0.5f, y + 0.025f, 0f), 0.11f, 0.2f, i == 0 ? mJar : mBottle);
            Jar(sh, new Vector3(-0.32f, y + 0.025f, 0f), 0.09f, 0.26f, mBottle);
            Cup(sh, new Vector3(0.05f, y + 0.025f, 0f));
            Cup(sh, new Vector3(0.22f, y + 0.025f, 0.02f));
            Jar(sh, new Vector3(0.5f, y + 0.025f, 0f), 0.13f, 0.16f, i == 0 ? mGreen : mJar);
        }
        SmallPlant(sh, new Vector3(-0.1f, 2.125f, 0f));
    }

    // ------------------------------------------------------------------ counters (left side)

    static void Counters(Transform root, Refs refs)
    {
        var c = Child(root, "Counters");
        // back counter along the left wall
        float bx = X0 + 0.4f, bz0 = -5.8f, bz1 = -0.8f, bzM = (bz0 + bz1) / 2f, len = bz1 - bz0;
        Box("Back Counter", c, new Vector3(bx, 0.47f, bzM), new Vector3(0.8f, 0.94f, len), mGreen);
        Box("Back Counter Top", c, new Vector3(bx, 0.965f, bzM), new Vector3(0.86f, 0.05f, len + 0.06f), mTop);
        for (float z = bz0 + 0.6f; z < bz1 - 0.1f; z += 0.62f)
            Box("Door Line", c, new Vector3(bx + 0.405f, 0.47f, z), new Vector3(0.01f, 0.7f, 0.02f), mGreenDark);
        float top = 0.99f;

        // espresso machine
        var em = Child(c, "Espresso Machine");
        em.localPosition = new Vector3(bx - 0.05f, top, -2.4f);
        Box("Body", em, new Vector3(0f, 0.24f, 0f), new Vector3(0.5f, 0.48f, 0.72f), mBrown);
        Box("Top", em, new Vector3(0f, 0.5f, 0f), new Vector3(0.52f, 0.04f, 0.74f), mMetal);
        Box("Drip Tray", em, new Vector3(0.28f, 0.03f, 0f), new Vector3(0.12f, 0.06f, 0.6f), mMetal);
        for (int i = -1; i <= 1; i += 2)
        {
            Cyl("Group Head", em, new Vector3(0.28f, 0.3f, i * 0.17f), new Vector3(0.09f, 0.05f, 0.09f), mMetal);
            Cup(em, new Vector3(0.28f, 0.06f, i * 0.17f));
        }
        Cup(em, new Vector3(-0.1f, 0.52f, -0.2f)); Cup(em, new Vector3(-0.1f, 0.52f, 0.0f)); Cup(em, new Vector3(-0.1f, 0.52f, 0.2f));
        // grinder
        var gr = Child(c, "Grinder");
        gr.localPosition = new Vector3(bx, top, -1.55f);
        Box("Base", gr, new Vector3(0f, 0.14f, 0f), new Vector3(0.22f, 0.28f, 0.22f), mMetal);
        Cyl("Hopper", gr, new Vector3(0f, 0.38f, 0f), new Vector3(0.18f, 0.1f, 0.18f), mBottle);
        // jars and canisters
        Jar(c, new Vector3(bx, top, -3.4f), 0.16f, 0.24f, mJar);
        Jar(c, new Vector3(bx, top, -3.65f), 0.16f, 0.3f, mJar);
        Jar(c, new Vector3(bx + 0.1f, top, -4.4f), 0.14f, 0.2f, mGreenDark);
        Jar(c, new Vector3(bx - 0.1f, top, -4.6f), 0.14f, 0.26f, mGreenDark);
        for (int i = 0; i < 4; i++) Cup(c, new Vector3(bx + 0.12f, top + i * 0.075f, -5.2f));

        // shelves on the left wall above the back counter
        for (int i = 0; i < 2; i++)
        {
            float y = 1.75f + i * 0.55f;
            Box("Shelf", c, new Vector3(X0 + 0.15f, y, -3.4f), new Vector3(0.3f, 0.05f, 2.6f), mWood);
            for (int k = 0; k < 6; k++)
            {
                float z = -4.5f + k * 0.42f;
                if ((k + i) % 3 == 0) Jar(c, new Vector3(X0 + 0.15f, y + 0.025f, z), 0.1f, 0.26f, mBottle);
                else if ((k + i) % 3 == 1) Jar(c, new Vector3(X0 + 0.15f, y + 0.025f, z), 0.13f, 0.17f, mJar);
                else Cup(c, new Vector3(X0 + 0.15f, y + 0.025f, z));
            }
        }

        // service counter facing the room
        float fx = -2.9f, fz0 = -5.2f, fz1 = -1.6f, fzM = (fz0 + fz1) / 2f;
        Box("Front Counter", c, new Vector3(fx, 0.5f, fzM), new Vector3(0.6f, 1.0f, fz1 - fz0), mGreen);
        Box("Front Counter Top", c, new Vector3(fx, 1.025f, fzM), new Vector3(0.72f, 0.05f, fz1 - fz0 + 0.08f), mTop);
        for (float z = fz0 + 0.45f; z < fz1; z += 0.6f)
            Box("Panel Line", c, new Vector3(fx + 0.305f, 0.5f, z), new Vector3(0.01f, 0.8f, 0.02f), mGreenDark);
        Box("Front Counter End", c, new Vector3(fx, 0.5f, fz1 + 0.02f), new Vector3(0.62f, 1.0f, 0.04f), mGreenDark);
        // pastry case
        var pc = Child(c, "Pastry Case");
        pc.localPosition = new Vector3(fx, 1.05f, -3.6f);
        Box("Case Base", pc, new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.04f, 1.1f), mMetal);
        Box("Case Glass", pc, new Vector3(0f, 0.22f, 0f), new Vector3(0.48f, 0.36f, 1.06f), mCaseGlass, shadow: false);
        Box("Case Top", pc, new Vector3(0f, 0.41f, 0f), new Vector3(0.5f, 0.03f, 1.1f), mMetal);
        for (int i = 0; i < 4; i++)
        {
            Sph("Pastry", pc, new Vector3(-0.08f, 0.09f, -0.38f + i * 0.25f), new Vector3(0.16f, 0.08f, 0.12f), i % 2 == 0 ? mPastry : mPastryDark);
            Sph("Pastry", pc, new Vector3(0.1f, 0.09f, -0.3f + i * 0.25f), new Vector3(0.12f, 0.1f, 0.12f), mPastryDark);
        }
        // register
        var reg = Child(c, "Register");
        reg.localPosition = new Vector3(fx, 1.05f, -4.7f);
        Box("Base", reg, new Vector3(0f, 0.04f, 0f), new Vector3(0.3f, 0.08f, 0.3f), mMetal);
        var scr = Box("Screen", reg, new Vector3(0.02f, 0.2f, 0f), new Vector3(0.03f, 0.2f, 0.28f), mBottle);
        scr.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
        // the spot where the coffee waits (used by the ending), and where the person stands to take it
        var spot = Child(c, "Coffee Spot");
        spot.localPosition = new Vector3(fx + 0.15f, 1.05f, -2.05f);
        refs.coffeeSpot = spot;
        var stand = Child(c, "Counter Stand");
        stand.localPosition = new Vector3(fx + 0.85f, 0f, -2.05f);
        stand.localRotation = Quaternion.Euler(0f, -90f, 0f); // facing the counter
        refs.counterStand = stand;
    }

    // ------------------------------------------------------------------ window, bar, plant, shelf (right side)

    static void WindowSide(Transform root, bool night, Refs refs)
    {
        var w = Child(root, "Window Side");
        float x = X1 - 0.05f, z0 = -3.5f, z1 = -1.5f, y0 = 1.0f, y1 = 2.4f, zm = (z0 + z1) / 2f, ym = (y0 + y1) / 2f;
        Box("Window Frame", w, new Vector3(x, y0, zm), new Vector3(0.2f, 0.08f, z1 - z0 + 0.08f), mWood);
        Box("Window Frame", w, new Vector3(x, y1, zm), new Vector3(0.2f, 0.08f, z1 - z0 + 0.08f), mWood);
        Box("Window Frame", w, new Vector3(x, ym, z0), new Vector3(0.2f, y1 - y0, 0.08f), mWood);
        Box("Window Frame", w, new Vector3(x, ym, z1), new Vector3(0.2f, y1 - y0, 0.08f), mWood);
        Box("Mullion", w, new Vector3(x + 0.04f, ym, zm), new Vector3(0.06f, y1 - y0, 0.05f), mWood);
        Box("Mullion", w, new Vector3(x + 0.04f, ym, zm), new Vector3(0.06f, 0.05f, z1 - z0), mWood);
        refs.windows.Add(Box("Window Glass", w, new Vector3(X1 + 0.12f, ym, zm), new Vector3(0.02f, y1 - y0, z1 - z0), night ? mWindowNight : mWindowDay, shadow: false).GetComponent<Renderer>());

        // moonlight / sunlight coming in through the window
        var lg = new GameObject("Window Light");
        lg.transform.SetParent(w, false);
        lg.transform.localPosition = new Vector3(X1 + 2.6f, 3.6f, zm - 0.6f);
        lg.transform.LookAt(lg.transform.parent.TransformPoint(new Vector3(1.2f, 0f, zm + 0.4f)));
        var l = lg.AddComponent<Light>();
        l.type = LightType.Spot;
        l.spotAngle = 62f;
        l.range = 14f;
        l.color = night ? new Color(0.55f, 0.65f, 1f) : new Color(1f, 0.86f, 0.62f);
        l.intensity = night ? 5f : 9f;
        l.shadows = LightShadows.Soft;
        refs.windowLight = l;

        // bar under the window, three stools
        Box("Bar Top", w, new Vector3(X1 - 0.3f, 1.05f, zm), new Vector3(0.6f, 0.05f, 3.0f), mTop);
        Box("Bar Front", w, new Vector3(X1 - 0.12f, 0.52f, zm), new Vector3(0.2f, 1.02f, 3.0f), mGreenDark);
        for (int i = -1; i <= 1; i++) refs.stools.Add(Stool(w, new Vector3(X1 - 0.85f, 0f, zm + i * 0.95f)));

        // plant in the back corner
        var p = Child(w, "Plant");
        p.localPosition = new Vector3(X1 - 0.5f, 0f, ZBack - 0.5f);
        Cyl("Pot", p, new Vector3(0f, 0.25f, 0f), new Vector3(0.45f, 0.25f, 0.45f), mPot);
        var rnd = new System.Random(7);
        for (int i = 0; i < 9; i++)
        {
            float a = i * 40f + (float)rnd.NextDouble() * 20f, tilt = 6f + (float)rnd.NextDouble() * 14f, h = 0.7f + (float)rnd.NextDouble() * 0.6f;
            var leaf = Box("Leaf", p, Vector3.zero, new Vector3(0.12f, h, 0.03f), i % 3 == 0 ? mPlantDark : mPlant);
            leaf.transform.localRotation = Quaternion.Euler(0f, a, tilt);
            leaf.transform.localPosition = new Vector3(0f, 0.5f, 0f) + leaf.transform.localRotation * new Vector3(0f, h / 2f, 0f);
        }

        // green display shelf with coffee bags
        var ds = Child(w, "Display Shelf");
        ds.localPosition = new Vector3(X1 - 0.3f, 0f, -0.2f);
        Box("Side", ds, new Vector3(0f, 0.9f, -0.6f), new Vector3(0.45f, 1.8f, 0.05f), mGreen);
        Box("Side", ds, new Vector3(0f, 0.9f, 0.6f), new Vector3(0.45f, 1.8f, 0.05f), mGreen);
        for (int i = 0; i < 4; i++)
        {
            float y = 0.1f + i * 0.52f;
            Box("Shelf", ds, new Vector3(0f, y, 0f), new Vector3(0.45f, 0.04f, 1.2f), mGreen);
            if (i == 3) continue;
            for (int k = 0; k < 4; k++)
                Box("Coffee Bag", ds, new Vector3(-0.02f, y + 0.17f, -0.42f + k * 0.28f), new Vector3(0.14f, 0.3f, 0.2f), (k + i) % 2 == 0 ? mBag : mBagDark);
        }
    }

    // ------------------------------------------------------------------ tables

    static void Tables(Transform root, Refs refs, bool night)
    {
        var t = Child(root, "Tables");
        // the table the person sits at in the end
        var a = new Vector3(1.8f, 0f, -2.6f);
        SquareTable(t, a);
        var seatChair = Chair(t, a + new Vector3(0f, 0f, -0.68f), 0f);  // faces +z (towards the mirror wall)
        Chair(t, a + new Vector3(0f, 0f, 0.68f), 180f);
        Chair(t, a + new Vector3(0.68f, 0f, 0f), -90f);
        Chair(t, a + new Vector3(-0.68f, 0f, 0f), 90f);
        var seat = Child(t, "Seat");
        seat.localPosition = seatChair.localPosition;
        seat.localRotation = seatChair.localRotation;
        refs.seat = seat;
        var appr = Child(t, "Seat Approach");
        appr.localPosition = seatChair.localPosition + new Vector3(-0.62f, 0f, 0f); // beside the chair, on the room side
        appr.localRotation = seatChair.localRotation;
        refs.seatApproach = appr;

        // round table near the front
        var b = new Vector3(-0.6f, 0f, -5.1f);
        Cyl("Table Top", t, b + new Vector3(0f, 0.75f, 0f), new Vector3(0.9f, 0.025f, 0.9f), mWoodDark);
        Cyl("Table Leg", t, b + new Vector3(0f, 0.37f, 0f), new Vector3(0.08f, 0.37f, 0.08f), mMetal);
        Cyl("Table Foot", t, b + new Vector3(0f, 0.02f, 0f), new Vector3(0.45f, 0.02f, 0.45f), mMetal);
        for (int i = 0; i < 3; i++)
        {
            float ang = 30f + i * 120f;
            var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
            refs.chairs.Add(Chair(t, b + dir * 0.7f, ang + 180f));
        }
        // small table for two by the door
        var c = new Vector3(2.4f, 0f, -5.5f);
        SquareTable(t, c, 0.7f);
        refs.chairs.Add(Chair(t, c + new Vector3(-0.6f, 0f, 0f), 90f));
        refs.chairs.Add(Chair(t, c + new Vector3(0.6f, 0f, 0f), -90f));
        if (night) Cup(t, c + new Vector3(0.1f, 0.775f, 0.05f)); // left behind (the morning café has customers instead)
    }

    static void SquareTable(Transform p, Vector3 at, float size = 0.9f)
    {
        Box("Table Top", p, at + new Vector3(0f, 0.75f, 0f), new Vector3(size, 0.05f, size), mWoodDark);
        float o = size / 2f - 0.07f;
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Box("Table Leg", p, at + new Vector3(sx * o, 0.365f, sz * o), new Vector3(0.06f, 0.73f, 0.06f), mWoodDark);
    }

    static Transform Chair(Transform p, Vector3 at, float yaw)
    {
        var c = Child(p, "Chair");
        c.localPosition = at;
        c.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Box("Seat", c, new Vector3(0f, 0.46f, 0f), new Vector3(0.42f, 0.05f, 0.42f), mChair);
        Box("Back", c, new Vector3(0f, 0.74f, -0.19f), new Vector3(0.42f, 0.5f, 0.05f), mChair);
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Box("Leg", c, new Vector3(sx * 0.17f, 0.22f, sz * 0.17f), new Vector3(0.04f, 0.44f, 0.04f), mChair);
        return c;
    }

    static Transform Stool(Transform p, Vector3 at)
    {
        var s = Child(p, "Stool");
        s.localPosition = at;
        Cyl("Seat", s, new Vector3(0f, 0.74f, 0f), new Vector3(0.38f, 0.03f, 0.38f), mBrown);
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Box("Leg", s, new Vector3(sx * 0.12f, 0.36f, sz * 0.12f), new Vector3(0.04f, 0.72f, 0.04f), mWoodDark);
        Box("Foot Rest", s, new Vector3(0f, 0.28f, 0.12f), new Vector3(0.26f, 0.03f, 0.03f), mWoodDark);
        Box("Foot Rest", s, new Vector3(0f, 0.28f, -0.12f), new Vector3(0.26f, 0.03f, 0.03f), mWoodDark);
        s.localRotation = Quaternion.Euler(0f, 90f, 0f); // facing the window
        return s;
    }

    // ------------------------------------------------------------------ pendant lamps

    static void Lamps(Transform root, bool night, Refs refs)
    {
        var l = Child(root, "Lamps");
        // (position, on at night?)
        var lamps = new (Vector3 at, bool nightOn)[]
        {
            (new Vector3(1.8f, 2.45f, -2.6f), true),    // over the table
            (new Vector3(-0.6f, 2.45f, -5.1f), false),
            (new Vector3(-2.9f, 2.5f, -4.4f), true),    // over the counter
            (new Vector3(-2.9f, 2.5f, -2.6f), false),
            (new Vector3(X1 - 0.6f, 2.5f, -2.5f), true),
        };
        foreach (var (at, nightOn) in lamps)
        {
            bool on = !night || nightOn;
            var bulb = PendantShade(l, at, true);
            refs.bulbs.Add(bulb);
            bulb.sharedMaterial = on ? mBulbOn : mBulbOff;
            var lg = new GameObject("Lamp Light");
            lg.transform.SetParent(l, false);
            lg.transform.localPosition = at - new Vector3(0f, 0.16f, 0f);
            var light = lg.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.55f);
            light.range = night ? 5.5f : 4.5f;
            light.intensity = night ? 2f : 1.6f;
            light.shadows = LightShadows.None;
            light.enabled = on;
            refs.lamps.Add(light);
        }
    }

    /// <summary>Cord, black shade and bulb hanging from the ceiling. Returns the bulb.</summary>
    static Renderer PendantShade(Transform p, Vector3 at, bool withBulb)
    {
        var g = Child(p, "Pendant");
        g.localPosition = at;
        float cord = H - at.y;
        Cyl("Cord", g, new Vector3(0f, cord / 2f, 0f), new Vector3(0.015f, cord / 2f, 0.015f), mMetal);
        Cyl("Shade", g, new Vector3(0f, -0.04f, 0f), new Vector3(0.26f, 0.09f, 0.26f), mMetal);
        Cyl("Shade Top", g, new Vector3(0f, 0.06f, 0f), new Vector3(0.12f, 0.04f, 0.12f), mMetal);
        var bulb = Sph("Bulb", g, new Vector3(0f, -0.14f, 0f), Vector3.one * 0.1f, mBulbOn, shadow: false);
        if (!withBulb) bulb.GetComponent<Renderer>().sharedMaterial = mBulbOff;
        return bulb.GetComponent<Renderer>();
    }

    // ------------------------------------------------------------------ small props

    static void Jar(Transform p, Vector3 at, float d, float h, Material m)
    {
        Cyl("Jar", p, at + new Vector3(0f, h / 2f, 0f), new Vector3(d, h / 2f, d), m);
        Cyl("Lid", p, at + new Vector3(0f, h + 0.012f, 0f), new Vector3(d * 0.8f, 0.012f, d * 0.8f), mWoodDark);
    }

    public static Transform Cup(Transform p, Vector3 at)
    {
        var c = Child(p, "Cup");
        c.localPosition = at;
        Cyl("Cup Body", c, new Vector3(0f, 0.045f, 0f), new Vector3(0.08f, 0.045f, 0.08f), mCup);
        Box("Handle", c, new Vector3(0.05f, 0.05f, 0f), new Vector3(0.025f, 0.05f, 0.015f), mCup);
        return c;
    }

    static void SmallPlant(Transform p, Vector3 at)
    {
        Cyl("Pot", p, at + new Vector3(0f, 0.07f, 0f), new Vector3(0.13f, 0.07f, 0.13f), mGreenDark);
        Sph("Leaves", p, at + new Vector3(0f, 0.2f, 0f), new Vector3(0.22f, 0.18f, 0.22f), mPlant);
    }

    static void Picture(Transform p, Vector3 at, Vector2 size, float yaw)
    {
        var g = Child(p, "Picture");
        g.localPosition = at;
        g.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Box("Frame", g, Vector3.zero, new Vector3(size.x, size.y, 0.03f), mWoodDark);
        Box("Art", g, new Vector3(0f, 0f, 0.012f), new Vector3(size.x - 0.08f, size.y - 0.08f, 0.02f), mGreen);
        Box("Art", g, new Vector3(-size.x * 0.12f, -size.y * 0.1f, 0.024f), new Vector3(size.x * 0.4f, size.y * 0.35f, 0.01f), mBag);
    }

    // ------------------------------------------------------------------ primitives

    static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider, bool shadow)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        if (!shadow) r.shadowCastingMode = ShadowCastingMode.Off;
        return g;
    }

    static GameObject Box(string n, Transform p, Vector3 pos, Vector3 size, Material m, bool collider = false, bool shadow = true) =>
        Prim(PrimitiveType.Cube, n, p, pos, size, m, collider, shadow);
    // Cyl: size = (diameter, half height, diameter), like Unity's cylinder scale
    static GameObject Cyl(string n, Transform p, Vector3 pos, Vector3 size, Material m) => Prim(PrimitiveType.Cylinder, n, p, pos, size, m, false, true);
    static GameObject Sph(string n, Transform p, Vector3 pos, Vector3 size, Material m, bool shadow = true) => Prim(PrimitiveType.Sphere, n, p, pos, size, m, false, shadow);

    // ------------------------------------------------------------------ materials

    static void Materials()
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Materials", "Cafe");
        mFloor = Lit("M_Cafe_Floor", new Color(0.33f, 0.22f, 0.17f), 0.25f);
        mFloorLine = Lit("M_Cafe_FloorLine", new Color(0.24f, 0.16f, 0.12f), 0.2f);
        mWall = Lit("M_Cafe_Wall", new Color(0.82f, 0.77f, 0.69f), 0.05f);
        mCeiling = Lit("M_Cafe_Ceiling", new Color(0.2f, 0.2f, 0.2f), 0.05f);
        mGreen = Lit("M_Cafe_Green", new Color(0.27f, 0.38f, 0.33f), 0.2f);
        mGreenDark = Lit("M_Cafe_GreenDark", new Color(0.18f, 0.26f, 0.23f), 0.2f);
        mTop = Lit("M_Cafe_CounterTop", new Color(0.12f, 0.13f, 0.14f), 0.45f);
        mWood = Lit("M_Cafe_Wood", new Color(0.42f, 0.27f, 0.18f), 0.3f);
        mWoodDark = Lit("M_Cafe_WoodDark", new Color(0.17f, 0.12f, 0.12f), 0.35f);
        mBrown = Lit("M_Cafe_Brown", new Color(0.36f, 0.19f, 0.15f), 0.4f);
        mChair = Lit("M_Cafe_Chair", new Color(0.19f, 0.27f, 0.26f), 0.25f);
        mMetal = Lit("M_Cafe_Metal", new Color(0.08f, 0.08f, 0.09f), 0.6f);
        mMenu = Lit("M_Cafe_Menu", new Color(0.92f, 0.92f, 0.88f), 0.1f);
        mInk = Lit("M_Cafe_Ink", new Color(0.15f, 0.2f, 0.19f), 0.1f);
        mJar = Lit("M_Cafe_Jar", new Color(0.78f, 0.72f, 0.6f), 0.4f);
        mBottle = Lit("M_Cafe_Bottle", new Color(0.09f, 0.12f, 0.12f), 0.6f);
        mCup = Lit("M_Cafe_Cup", new Color(0.93f, 0.91f, 0.87f), 0.5f);
        mPlant = Lit("M_Cafe_Plant", new Color(0.33f, 0.52f, 0.33f), 0.2f);
        mPlantDark = Lit("M_Cafe_PlantDark", new Color(0.22f, 0.38f, 0.25f), 0.2f);
        mPot = Lit("M_Cafe_Pot", new Color(0.14f, 0.13f, 0.13f), 0.3f);
        mBag = Lit("M_Cafe_Bag", new Color(0.72f, 0.62f, 0.48f), 0.15f);
        mBagDark = Lit("M_Cafe_BagDark", new Color(0.36f, 0.25f, 0.19f), 0.15f);
        mPastry = Lit("M_Cafe_Pastry", new Color(0.86f, 0.64f, 0.36f), 0.3f);
        mPastryDark = Lit("M_Cafe_PastryDark", new Color(0.45f, 0.27f, 0.15f), 0.3f);
        mCaseGlass = Lit("M_Cafe_CaseGlass", new Color(0.8f, 0.88f, 0.9f, 0.18f), 0.95f);
        MakeTransparent(mCaseGlass);
        mBulbOn = Unlit("M_Cafe_BulbOn", new Color(1f, 0.88f, 0.62f));
        mBulbOff = Lit("M_Cafe_BulbOff", new Color(0.3f, 0.28f, 0.25f), 0.6f);
        mLedOn = Unlit("M_Cafe_LightStripOn", new Color(1f, 0.86f, 0.62f));
        mLedOff = Lit("M_Cafe_LightStripOff", new Color(0.25f, 0.23f, 0.2f), 0.2f);
        mWindowNight = Unlit("M_Cafe_WindowNight", new Color(0.07f, 0.09f, 0.16f));
        mWindowDay = Unlit("M_Cafe_WindowDay", new Color(0.86f, 0.93f, 1f));
        AssetDatabase.SaveAssets();
    }

    public static Material Shared(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");

    static Material GetOrCreate(string name, Shader shader)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
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

    public static void MakeTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
    }
}
