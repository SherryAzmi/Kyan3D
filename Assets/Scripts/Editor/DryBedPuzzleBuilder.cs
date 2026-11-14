using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tools > Kyan > Build Dry Bed Puzzle (Puzzle 2) — builds the second puzzle into Assets/Scenes/Sh/Puzzle2.unity.
/// Partial class of BridgePuzzleBuilder so it reuses the same materials, primitives, HUD, players and cutscene helpers.
///
/// Same 40 x 40 m layout as Puzzle 1 (forest = desert + ForestOffset, spawn z=-15, Memory River along x=14),
/// but with no chasm: the river bed is dry and the second fragment sits in the forest.
///   desert : 3 wet-sand mounds beside the river bed (x ~ 8, z -12..-2), picture frame buried in the last one
///   forest : spring gate z=-18.4 + root wheel at (10.4, -17), frame pedestal (-3.4, 2.4),
///            altar (-6, 15.5) inside a root cage, fragment on the altar
/// If Puzzle2.unity is still the placeholder, it is first replaced by a copy of SplitCamera.unity (players + cameras rig).
/// </summary>
public static partial class BridgePuzzleBuilder
{
    const string Puzzle3Path = "Assets/Scenes/Sh/Puzzle3.unity";

    static readonly Vector3[] DryMoundPos =
    {
        new Vector3(8.2f, 0f, -12f), new Vector3(9.4f, 0f, -7.2f), new Vector3(8.0f, 0f, -2.5f),
    };
    static readonly Vector3 WheelPos = new Vector3(RiverX - 3.6f, 0f, -17f);
    static readonly Vector3 GatePos = new Vector3(RiverX, 1.3f, -18.4f);
    static readonly Vector3 PedestalPos = new Vector3(-3.4f, 0f, 2.4f);
    static readonly Vector3 AltarPos = new Vector3(PathX, 0f, 15.5f);

    static Material mDryMound, mWetPatch, mDryBed, mFrameWood, mFrameArt;

    [MenuItem("Tools/Kyan/Build Dry Bed Puzzle (Puzzle 2)")]
    static void BuildPuzzle2FromMenu()
    {
        if (!EditorUtility.DisplayDialog("Build Dry Bed Puzzle (Puzzle 2)",
                "Rebuilds the desert + forest worlds in Puzzle2.unity (players and cameras are kept). " +
                "If Puzzle2.unity is still the placeholder it is replaced by a copy of SplitCamera.unity first. Continue?",
                "Build", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildPuzzle2();
    }

    public static void BuildPuzzle2()
    {
        AssetDatabase.Refresh();

        // Make sure Puzzle2.unity has the players / cameras rig.
        string full = Path.Combine(Directory.GetCurrentDirectory(), Puzzle2Path);
        bool hasRig = File.Exists(full) && File.ReadAllText(full).Contains("Present_P1");
        if (!hasRig)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); // make sure Puzzle2 is not the open scene
            if (File.Exists(full)) AssetDatabase.DeleteAsset(Puzzle2Path);
            if (!AssetDatabase.CopyAsset(ScenePath, Puzzle2Path))
            {
                Debug.LogError("[DryBedPuzzleBuilder] Could not copy SplitCamera.unity to Puzzle2.unity.");
                return;
            }
            AssetDatabase.Refresh();
        }

        var scene = EditorSceneManager.OpenScene(Puzzle2Path, OpenSceneMode.Single);
        foreach (var n in new[] { "--- ENVIRONMENT ---", "--- DESERT (Present) ---", "--- FOREST (Future) ---", "PuzzleUI",
                                  "BridgePuzzle", "DryBedPuzzle", "FragmentCutscene", "CinematicUI", "Cinematic Camera" })
        {
            var g = GameObject.Find(n);
            while (g) { Object.DestroyImmediate(g); g = GameObject.Find(n); }
        }

        MakeMaterials();
        MakeDryBedMaterials();

        // ---------- worlds ----------
        var desert = new GameObject("--- DESERT (Present) ---").transform;
        var forest = new GameObject("--- FOREST (Future) ---").transform;
        forest.position = ForestOffset;

        BuildFlatGround(desert, mSand);
        BuildFlatGround(forest, mGrass);
        DecorateDesert(desert);
        DecorateForest(forest);

        // keep the puzzle spots clear, and avoid a second buried frame
        var dProps = desert.Find("Props");
        var fProps = forest.Find("Props");
        var buried = dProps ? dProps.Find("Buried Frame") : null;
        if (buried) Object.DestroyImmediate(buried.gameObject);
        ClearAround(dProps, DryMoundPos, 3.4f);
        ClearAround(fProps, new[] { WheelPos, PedestalPos, AltarPos, new Vector3(PathX + 2.6f, 0f, 13.6f) }, 3.4f);
        ClearAround(fProps, new[] { GatePos }, 2.2f);

        // ---------- dry rivers ----------
        var riverD = MakeDryRiver(desert, mDesertWater, out var waterD);
        var riverF = MakeDryRiver(forest, mForestWater, out var waterF);
        riverD.other = riverF;
        riverF.other = riverD;

        // ---------- desert: wet-sand mounds + the buried frame ----------
        var mounds = new WetSandMound[DryMoundPos.Length];
        var patches = new Transform[DryMoundPos.Length];
        for (int i = 0; i < DryMoundPos.Length; i++)
        {
            Vector3 p = DryMoundPos[i];
            var patch = Cyl("Wet Patch " + (i + 1), desert, new Vector3(p.x, 0.012f, p.z), new Vector3(6f, 0.006f, 6f), mWetPatch);
            patches[i] = patch.transform;
            var mound = Sph("Sand Mound " + (i + 1), desert, p, new Vector3(3.4f, 1.6f, 3.4f), mDryMound);
            var sm = mound.AddComponent<WetSandMound>();
            sm.range = 2.6f;
            sm.digTime = 3f;
            mounds[i] = sm;
        }

        var frameRoot = Child(desert, "Picture Frame");
        frameRoot.localPosition = DryMoundPos[DryMoundPos.Length - 1] + new Vector3(0f, 0.5f, 0f);
        Box("Frame", frameRoot, Vector3.zero, new Vector3(0.9f, 0.7f, 0.08f), mFrameWood, false);
        Box("Picture", frameRoot, new Vector3(0f, 0f, -0.045f), new Vector3(0.72f, 0.52f, 0.02f), mFrameArt, false);
        AddLight(frameRoot, Vector3.zero, new Color(1f, 0.85f, 0.6f), 1.5f, 4f);
        var frame = frameRoot.gameObject.AddComponent<Carryable>();
        frame.range = 2f;
        frame.displayName = "Picture Frame";

        // the desert altar is empty: the second fragment is in the Future's world
        Box("Altar (desert)", desert, new Vector3(PathX, 0.5f, 15.5f), new Vector3(1.3f, 1f, 1.3f), mStone, true);
        Sph("Glow Bloom", desert, new Vector3(PathX, 1.15f, 15.5f), Vector3.one * 0.3f, mGlowPink);

        // ---------- forest: spring gate + root wheel ----------
        var gate = Box("Spring Gate", forest, GatePos, new Vector3(5f, 2.6f, 0.6f), mStone, false);
        Cyl("Wheel Pedestal", forest, WheelPos + new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.45f, 0.8f), mStone);
        var wheelGlow = Cyl("Wheel Glow", forest, WheelPos + new Vector3(0f, 0.92f, 0f), new Vector3(0.95f, 0.04f, 0.95f), mSocketDormant);
        var wheelRoot = Child(forest, "Root Wheel");
        wheelRoot.localPosition = WheelPos + new Vector3(0f, 1.9f, 0f);
        var wheelDisc = Child(wheelRoot, "Wheel");
        var ring = Cyl("Wheel Ring", wheelDisc, Vector3.zero, new Vector3(1.5f, 0.05f, 1.5f), mRoot);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        for (int i = 0; i < 4; i++)
        {
            var spoke = Box("Spoke", wheelDisc, Vector3.zero, new Vector3(1.5f, 0.09f, 0.09f), mRoot, false);
            spoke.transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
        }
        Sph("Hub", wheelDisc, Vector3.zero, Vector3.one * 0.28f, mBark);
        var wheel = wheelGlow.AddComponent<RootWheel>();
        wheel.range = 2.4f;
        wheel.turnTime = 5f;
        wheel.wheel = wheelDisc;
        wheel.glow = wheelGlow.GetComponent<Renderer>();
        wheel.dormantMaterial = mSocketDormant;
        wheel.awakeMaterial = mSocketAwake;

        // ---------- forest: frame pedestal ----------
        Cyl("Frame Pedestal", forest, PedestalPos + new Vector3(0f, 0.5f, 0f), new Vector3(0.8f, 0.5f, 0.8f), mStone);
        var pedGlow = Cyl("Pedestal Glow", forest, PedestalPos + new Vector3(0f, 1.02f, 0f), new Vector3(0.95f, 0.04f, 0.95f), mSocketDormant);
        var pedestal = pedGlow.AddComponent<FramePedestal>();
        pedestal.range = 2.3f;
        pedestal.glow = pedGlow.GetComponent<Renderer>();
        pedestal.awakeMaterial = mSocketAwake;
        var slot = Child(forest, "Frame Slot");
        slot.localPosition = PedestalPos + new Vector3(0f, 1.45f, 0f);
        pedestal.slot = slot;

        // ---------- forest: altar, root cage, the second Mirror Fragment ----------
        Box("Fragment Altar", forest, new Vector3(PathX, 0.5f, 15.5f), new Vector3(1.3f, 1f, 1.3f), mStone, true);
        var frag = Box("Mirror Fragment", forest, new Vector3(PathX, 1.6f, 15.5f), new Vector3(0.45f, 0.75f, 0.04f), mFragment, false);
        frag.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        var fragLight = AddLight(frag.transform, Vector3.zero, new Color(0.75f, 0.95f, 0.9f), 0.8f, 5f);
        var fragment = frag.AddComponent<MirrorFragment>();
        fragment.range = 2.2f;

        var cage = new List<Transform>();
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(PathX + Mathf.Sin(a) * 1.7f, 1.5f, 15.5f + Mathf.Cos(a) * 1.7f);
            var r = Prim(PrimitiveType.Cylinder, "Cage Root " + (i + 1), forest, pos, new Vector3(0.45f, 1.5f, 0.45f), mRoot, true);
            r.transform.localRotation = Quaternion.Euler(Mathf.Cos(a) * 7f, 0f, -Mathf.Sin(a) * 7f); // lean inward
            cage.Add(r.transform);
        }

        var springLightT = fProps ? fProps.Find("Spring Glow") : null;
        Light springLight = springLightT ? springLightT.GetComponent<Light>() : null;

        // ---------- guidance beams ----------
        var desertBeacon = Beacon(desert, "Guide Beam (desert)", mBeamGold, new Color(1f, 0.85f, 0.5f));
        var forestBeacon = Beacon(forest, "Guide Beam (forest)", mBeamCyan, new Color(0.55f, 1f, 0.9f));

        // ---------- flower that grows when the fragment is taken ----------
        var flower = BuildFlower(forest, new Vector3(PathX + 2.6f, 0f, 13.6f), out var petals, out var flowerLight);
        flower.name = "Memory Flower";

        // ---------- puzzle manager + HUD ----------
        var hud = BuildHUD();
        var puzzle = new GameObject("DryBedPuzzle").AddComponent<DryBedPuzzle>();
        puzzle.hud = hud;
        puzzle.desertRiver = riverD;
        puzzle.forestRiver = riverF;
        puzzle.desertWater = waterD;
        puzzle.forestWater = waterF;
        puzzle.desertWaterRenderer = waterD.GetComponent<Renderer>();
        puzzle.riverStartZ = -20f;
        puzzle.riverLength = 23f;
        puzzle.fillTime = 6f;
        puzzle.wheel = wheel;
        puzzle.springGate = gate.transform;
        puzzle.springGlow = springLight;
        puzzle.pedestal = pedestal;
        puzzle.cageRoots = cage.ToArray();
        puzzle.fragment = fragment;
        puzzle.fragmentLight = fragLight;
        puzzle.mounds = mounds;
        puzzle.wetPatches = patches;
        puzzle.wetMoundMaterial = mWetPatch;
        puzzle.frame = frame;
        puzzle.desertBeacon = desertBeacon;
        puzzle.forestBeacon = forestBeacon;

        // ---------- players, cameras, light ----------
        var presentGo = SetupPlayer(new[] { "Present_P1", "LightSoul_P1" }, "Present_P1", Vector3.zero, mPresent, hud, 0, "□ / E", "R2 / F", false);
        var futureGo = SetupPlayer(new[] { "Future_P2", "VoidSoul_P2" }, "Future_P2", ForestOffset, mFuture, hud, 1, "□ / R-Shift", "R2 / R-Ctrl", true);
        if (presentGo) puzzle.present = presentGo.transform;
        if (futureGo) puzzle.future = futureGo.transform;
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

        // ---------- fragment 2 cutscene: the Future, in the forest ----------
        var fc = BuildFragmentCutscene(flower, petals, flowerLight, futureGo, sun ? sun.GetComponent<Light>() : null, hud);
        fc.header = "Mirror Fragment 2 of 3  ·  Accepting the past";
        fc.note = "It happened. I can't change it.\nI can carry it without letting it carry me.\nThe past is part of me, and it is not all of me.";
        fc.nextScene = "Puzzle3";
        fc.rippleMaterial = mRipple;
        if (fc.cinematicCam) fc.cinematicCam.backgroundColor = new Color(0.55f, 0.78f, 0.88f);
        puzzle.fragmentCutscene = fc;
        AudioSetup.AddGameAudio();
        ModelDresser.DressOpenScene();
        CharacterSetup.PlaceCharacters();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EnsureInBuildSettings(Puzzle2Path, ScenePath);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Puzzle3Path)) EnsureInBuildSettings(Puzzle3Path, Puzzle2Path);
        AssetDatabase.SaveAssets();
        Debug.Log("[DryBedPuzzleBuilder] Puzzle 2 (The Dry Bed) built into Puzzle2.unity.");
    }

    // ================================================================== pieces

    static void MakeDryBedMaterials()
    {
        mDryMound = Lit("M_Desert_DryMound", new Color(0.72f, 0.68f, 0.6f), 0.05f);
        mWetPatch = Lit("M_Desert_WetSand", new Color(0.42f, 0.34f, 0.25f), 0.35f);
        mDryBed = Lit("M_RiverBed_Dry", new Color(0.5f, 0.46f, 0.4f), 0.05f);
        mFrameWood = Lit("M_Frame_Wood", new Color(0.42f, 0.28f, 0.16f), 0.3f);
        mFrameArt = Unlit("M_Frame_Art", new Color(1f, 0.9f, 0.65f));
    }

    static void BuildFlatGround(Transform world, Material ground)
    {
        Box("Ground", world, new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f), ground, true);
        var walls = Child(world, "Bounds");
        Wall(walls, new Vector3(-20f, 3f, 0f), new Vector3(1f, 6f, 42f));
        Wall(walls, new Vector3(20f, 3f, 0f), new Vector3(1f, 6f, 42f));
        Wall(walls, new Vector3(0f, 3f, -20f), new Vector3(42f, 6f, 1f));
        Wall(walls, new Vector3(0f, 3f, 20f), new Vector3(42f, 6f, 1f));
    }

    /// <summary>Memory River with a dry bed; the water strip starts hidden and is filled by DryBedPuzzle.</summary>
    static MemoryRiver MakeDryRiver(Transform world, Material water, out Transform waterStrip)
    {
        var river = Child(world, "Memory River");
        river.localPosition = new Vector3(RiverX, 0f, 0f);
        Box("Dry Bed", river, new Vector3(0f, 0.01f, -8.5f), new Vector3(4.4f, 0.02f, 23f), mDryBed, false);
        var strip = Box("Water", river, new Vector3(0f, 0.02f, -8.5f), new Vector3(4f, 0.04f, 23f), water, false);
        strip.AddComponent<RiverWaves>();
        strip.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        waterStrip = strip.transform;
        for (float z = -19f; z <= 2.5f; z += 2.2f)
        {
            Box("Bank Stone", river, new Vector3(-2.4f, 0.08f, z + Mathf.Sin(z) * 0.4f), new Vector3(0.5f, 0.2f, 0.7f), mStone, false);
            Box("Bank Stone", river, new Vector3(2.4f, 0.08f, z + Mathf.Cos(z) * 0.4f), new Vector3(0.5f, 0.2f, 0.7f), mStone, false);
        }
        Sph("End Rocks", river, new Vector3(0f, 0.2f, 3.4f), new Vector3(4.6f, 1f, 1.4f), mStone);

        var mr = river.gameObject.AddComponent<MemoryRiver>();
        river.gameObject.AddComponent<RiverWading>();
        mr.range = 1.6f;
        mr.zMin = -20f;
        mr.zMax = 3f;
        mr.halfWidth = 2f;
        mr.bankX = -3f;
        mr.rippleMaterial = mRipple;
        mr.water = strip.GetComponent<Renderer>();
        return mr;
    }

    /// <summary>Removes decoration props within 'radius' (flat distance) of any of the given points.</summary>
    static void ClearAround(Transform props, Vector3[] points, float radius)
    {
        if (!props) return;
        for (int i = props.childCount - 1; i >= 0; i--)
        {
            var c = props.GetChild(i);
            if (c.name.StartsWith("Spring")) continue;
            foreach (var p in points)
            {
                Vector2 d = new Vector2(c.localPosition.x - p.x, c.localPosition.z - p.z);
                if (d.magnitude < radius) { Object.DestroyImmediate(c.gameObject); break; }
            }
        }
    }

    static void EnsureInBuildSettings(string path, string after)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(x => x.path == path)) return;
        int at = scenes.FindIndex(x => x.path == after);
        scenes.Insert(at >= 0 ? at + 1 : scenes.Count, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
