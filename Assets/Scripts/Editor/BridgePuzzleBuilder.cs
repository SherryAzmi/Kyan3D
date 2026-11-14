using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Tools > Kyan > Build Bridge Puzzle — builds the Pale Desert (Present, left screen) and the
/// Green Valley (Future, right screen) in SplitCamera.unity with the Broken Bridge puzzle.
/// Keeps the existing players / cameras rig from "Build Split Camera Rig" and re-skins it.
///
/// Both worlds share the same local layout (forest = desert + ForestOffset):
///   spawn z=-15 · 3 sand mounds on the path at x=-6 · Memory River along x=14 (z -40..3)
///   Chasm/Gap z=4..10 across the whole width · altar at (-6, z=15.5)
/// </summary>
public static partial class BridgePuzzleBuilder
{
    const string ScenePath = "Assets/Scenes/Sh/SplitCamera.unity";
    const string MatDir = "Assets/Materials/World";
    static readonly Vector3 ForestOffset = new Vector3(300f, 0f, 0f);

    const float PathX = -6f;
    const float RiverX = 14f;
    const float ChasmNear = 4f, ChasmFar = 10f;
    static readonly float[] MoundZ = { -10f, -5.5f, -1f };

    // materials
    static Material mSand, mSandDark, mCliff,mStone, mStoneLit, mDeadWood, mDesertWater, mDesertDune;
    static Material mGrass, mDirt, mBark, mLeaf, mLeafDark, mRoot, mForestWater, mGlowCyan, mGlowPink;
    static Material mAbyss, mSeed, mFragment, mRipple, mSocketDormant, mSocketAwake, mPresent, mFuture, mBarBg, mBarFill;
    static Material mBeamGold, mBeamCyan, mPetal, mFlowerCenter;

    [MenuItem("Tools/Kyan/Build Bridge Puzzle")]
    static void BuildFromMenu()
    {
        if (!EditorUtility.DisplayDialog("Build Bridge Puzzle",
                "Rebuilds the desert + forest worlds in SplitCamera.unity (players and cameras are kept). Continue?",
                "Build", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var n in new[] { "--- ENVIRONMENT ---", "--- DESERT (Present) ---", "--- FOREST (Future) ---", "PuzzleUI", "BridgePuzzle",
                                  "FragmentCutscene", "CinematicUI", "Cinematic Camera" })
        {
            var g = GameObject.Find(n);
            if (g) Object.DestroyImmediate(g);
        }

        MakeMaterials();

        // ---------- worlds ----------
        var desert = new GameObject("--- DESERT (Present) ---").transform;
        var forest = new GameObject("--- FOREST (Future) ---").transform;
        forest.position = ForestOffset;

        BuildGround(desert, mSand, mDesertWater, true);
        BuildGround(forest, mGrass, mForestWater, false);
        DecorateDesert(desert);
        DecorateForest(forest);

        // ---------- rivers ----------
        var riverD = MakeRiver(desert, mDesertWater);
        var riverF = MakeRiver(forest, mForestWater);
        riverD.other = riverF;
        riverF.other = riverD;

        // ---------- desert puzzle pieces ----------
        var pathRoot = Child(desert, "Ancient Path");
        var mounds = new SandMound[MoundZ.Length];
        for (float z = -13f; z <= 2.6f; z += 1.5f)
        {
            var stone = Box("Path Stone", pathRoot, new Vector3(PathX + Mathf.Sin(z * 3f) * 0.15f, 0.03f, z),
                            new Vector3(1.6f, 0.12f, 1.1f), mStone, false);
            stone.transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(z * 7f) * 8f, 0f);
        }
        for (int i = 0; i < MoundZ.Length; i++)
        {
            var mound = Sph("Sand Mound " + (i + 1), desert, new Vector3(PathX, 0f, MoundZ[i]), new Vector3(3.4f, 1.6f, 3.4f), mSandDark);
            var sm = mound.AddComponent<SandMound>();
            sm.range = 2.6f;
            sm.digTime = 3f;
            sm.revealedStone = Box("Rune Stone " + (i + 1), pathRoot, new Vector3(PathX, 0.05f, MoundZ[i]),
                                   new Vector3(1.3f, 0.14f, 1.3f), mStone, false).GetComponent<Renderer>();
            sm.revealedMaterial = mStoneLit;
            mounds[i] = sm;
        }
        for (float z = ChasmFar + 0.9f; z <= 14.2f; z += 1.5f)
            Box("Path Stone", pathRoot, new Vector3(PathX, 0.03f, z), new Vector3(1.6f, 0.12f, 1.1f), mStone, false);

        var stones = new Transform[4];
        for (int i = 0; i < 4; i++)
            stones[i] = Box("Bridge Stone " + (i + 1), desert, new Vector3(PathX, -0.18f, ChasmNear + 0.75f + i * 1.5f),
                            new Vector3(2.2f, 0.35f, 1.35f), mStone, false).transform;
        var desertWalk = Walkway(desert, "Bridge Walkway");

        // seed (hidden until the last mound is dug)
        var seedGo = Sph("Memory Seed", null, new Vector3(PathX, 0.4f, MoundZ[2]), Vector3.one * 0.38f, mSeed);
        seedGo.transform.SetParent(desert, true);
        AddLight(seedGo.transform, Vector3.zero, new Color(0.7f, 1f, 0.5f), 2f, 4f);
        var seed = seedGo.AddComponent<Carryable>();
        seed.range = 2f;
        seed.displayName = "Memory Seed";

        // altar + fragment across the chasm
        Box("Fragment Altar", desert, new Vector3(PathX, 0.5f, 15.5f), new Vector3(1.3f, 1f, 1.3f), mStone, true);
        var frag = Box("Mirror Fragment", desert, new Vector3(PathX, 1.6f, 15.5f), new Vector3(0.45f, 0.75f, 0.04f), mFragment, false);
        frag.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        var fragLight = AddLight(frag.transform, Vector3.zero, new Color(0.75f, 0.85f, 1f), 0.6f, 5f);
        frag.AddComponent<MirrorFragment>().range = 2.2f;

        // ---------- forest puzzle pieces ----------
        Cyl("Root Stump South", forest, new Vector3(PathX, 0.3f, ChasmNear - 0.6f), new Vector3(1.6f, 0.3f, 1.6f), mBark);
        Cyl("Root Stump North", forest, new Vector3(PathX, 0.3f, ChasmFar + 0.6f), new Vector3(1.6f, 0.3f, 1.6f), mBark);
        float half = (ChasmFar - ChasmNear) * 0.5f + 0.6f;
        var roots = new[]
        {
            Root(forest, "Root S1", new Vector3(PathX - 0.35f, 0.25f, ChasmNear - 0.6f), 0f, half, 0.5f),
            Root(forest, "Root S2", new Vector3(PathX + 0.35f, 0.15f, ChasmNear - 0.6f), 0f, half, 0.4f),
            Root(forest, "Root N1", new Vector3(PathX + 0.3f, 0.25f, ChasmFar + 0.6f), 180f, half, 0.5f),
            Root(forest, "Root N2", new Vector3(PathX - 0.3f, 0.15f, ChasmFar + 0.6f), 180f, half, 0.4f),
        };
        var forestWalk = Walkway(forest, "Root Walkway");

        Cyl("Socket Pedestal", forest, new Vector3(-3.4f, 0.5f, 2.4f), new Vector3(0.8f, 0.5f, 0.8f), mStone);
        var socketRing = Cyl("Socket Glow", forest, new Vector3(-3.4f, 1.02f, 2.4f), new Vector3(0.95f, 0.04f, 0.95f), mSocketDormant);
        var socket = socketRing.AddComponent<RootSocket>();
        socket.range = 2.3f;
        socket.growTime = 5f;
        socket.glow = socketRing.GetComponent<Renderer>();
        socket.dormantMaterial = mSocketDormant;
        socket.awakeMaterial = mSocketAwake;
        var slot = Child(forest, "Seed Slot");
        slot.localPosition = new Vector3(-3.4f, 1.3f, 2.4f);
        socket.seedSlot = slot;

        // empty mirrored altar on the forest side (the fragment only exists in the Present's world)
        Box("Altar (forest)", forest, new Vector3(PathX, 0.5f, 15.5f), new Vector3(1.3f, 1f, 1.3f), mStone, true);
        Sph("Glow Bloom", forest, new Vector3(PathX, 1.15f, 15.5f), Vector3.one * 0.35f, mGlowPink);

        // ---------- guidance beams ----------
        var desertBeacon = Beacon(desert, "Guide Beam (desert)", mBeamGold, new Color(1f, 0.85f, 0.5f));
        var forestBeacon = Beacon(forest, "Guide Beam (forest)", mBeamCyan, new Color(0.55f, 1f, 0.9f));

        // ---------- flower that grows when the fragment is taken ----------
        var flower = BuildFlower(desert, new Vector3(PathX + 1.5f, 0f, 14.2f), out var petals, out var flowerLight);

        // ---------- puzzle manager + HUD ----------
        var hud = BuildHUD();
        var puzzle = new GameObject("BridgePuzzle").AddComponent<BridgePuzzle>();
        puzzle.hud = hud;
        puzzle.desertRiver = riverD;
        puzzle.fragment = frag.GetComponent<MirrorFragment>();
        puzzle.desertBeacon = desertBeacon;
        puzzle.forestBeacon = forestBeacon;
        puzzle.mounds = mounds;
        puzzle.memorySeed = seed;
        puzzle.desertBridgeStones = stones;
        puzzle.desertBridgeWalkway = desertWalk;
        puzzle.fragmentLight = fragLight;
        puzzle.rootSocket = socket;
        puzzle.bridgeRoots = roots;
        puzzle.forestBridgeWalkway = forestWalk;

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

        // ---------- fragment cutscene (flower + note) ----------
        puzzle.fragmentCutscene = BuildFragmentCutscene(flower, petals, flowerLight, presentGo,
                                                        sun ? sun.GetComponent<Light>() : null, hud);
        AudioSetup.AddGameAudio();
        ModelDresser.DressOpenScene();
        CharacterSetup.PlaceCharacters();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EnsurePuzzle2Placeholder();
        AssetDatabase.SaveAssets();
        Debug.Log("[BridgePuzzleBuilder] Desert + forest built with the Broken Bridge puzzle.");
    }

    // ================================================================== world pieces

    static void BuildGround(Transform world, Material ground, Material water, bool desert)
    {
        Box("Ground South", world, new Vector3(0f, -0.5f, -18f), new Vector3(80f, 1f, 44f), ground, true);
        Box("Ground North", world, new Vector3(0f, -0.5f, 25f), new Vector3(80f, 1f, 30f), ground, true);
        Box(desert ? "Chasm Floor" : "Gap Floor", world, new Vector3(0f, -20f, 7f), new Vector3(80f, 1f, 6f), mAbyss, false);
        // cliff faces so the chasm reads as deep
        Box("Cliff South", world, new Vector3(0f, -5f, ChasmNear - 0.05f), new Vector3(80f, 9f, 0.1f), desert ? mCliff : mDirt, false);
        Box("Cliff North", world, new Vector3(0f, -5f, ChasmFar + 0.05f), new Vector3(80f, 9f, 0.1f), desert ? mCliff : mDirt, false);

        // invisible border (40 x 40 play area)
        var walls = Child(world, "Bounds");
        Wall(walls, new Vector3(-20f, 3f, 0f), new Vector3(1f, 6f, 42f));
        Wall(walls, new Vector3(20f, 3f, 0f), new Vector3(1f, 6f, 42f));
        Wall(walls, new Vector3(0f, 3f, -20f), new Vector3(42f, 6f, 1f));
        Wall(walls, new Vector3(0f, 3f, 20f), new Vector3(42f, 6f, 1f));

        // waterfall where the river drops into the chasm
        var fall = Box("Waterfall", world, new Vector3(RiverX, -5f, ChasmNear + 0.12f), new Vector3(3.6f, 10f, 0.08f), water, false);
        fall.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    static MemoryRiver MakeRiver(Transform world, Material water)
    {
        var river = Child(world, "Memory River");
        river.localPosition = new Vector3(RiverX, 0f, 0f);
        var strip = Box("Water", river, new Vector3(0f, 0.02f, -18.5f), new Vector3(4f, 0.04f, 43f), water, false);
        strip.AddComponent<RiverWaves>();
        strip.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        for (float z = -19f; z <= 3f; z += 2.2f)
        {
            Box("Bank Stone", river, new Vector3(-2.2f, 0.08f, z + Mathf.Sin(z) * 0.4f), new Vector3(0.5f, 0.2f, 0.7f), mStone, false);
            Box("Bank Stone", river, new Vector3(2.2f, 0.08f, z + Mathf.Cos(z) * 0.4f), new Vector3(0.5f, 0.2f, 0.7f), mStone, false);
        }
        var mr = river.gameObject.AddComponent<MemoryRiver>();
        river.gameObject.AddComponent<RiverWading>();
        mr.range = 1.6f;
        mr.zMin = -20f;
        mr.zMax = 3f;
        mr.halfWidth = 2f;
        mr.bankX = -3f;
        mr.rippleMaterial = mRipple;
        mr.water = strip.GetComponent<Renderer>();
        RiverUpgrade.Upgrade(mr);
        return mr;
    }

    static Transform Root(Transform world, string name, Vector3 pos, float yaw, float length, float thickness)
    {
        var pivot = Child(world, name);
        pivot.localPosition = pos;
        pivot.localRotation = Quaternion.Euler(-4f, yaw, 0f);
        pivot.localScale = new Vector3(1f, 1f, length);
        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(c.GetComponent<Collider>());
        c.name = "Root Mesh";
        c.transform.SetParent(pivot, false);
        c.transform.localPosition = new Vector3(0f, 0f, 0.5f);
        c.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        c.transform.localScale = new Vector3(thickness, 0.5f, thickness);
        c.GetComponent<Renderer>().sharedMaterial = mRoot;
        return pivot;
    }

    static Collider Walkway(Transform world, string name)
    {
        var w = Child(world, name);
        w.localPosition = new Vector3(PathX, -0.25f, (ChasmNear + ChasmFar) * 0.5f);
        var bc = w.gameObject.AddComponent<BoxCollider>();
        bc.size = new Vector3(2.4f, 0.5f, ChasmFar - ChasmNear + 0.4f);
        return bc;
    }

    static void DecorateDesert(Transform world)
    {
        var props = Child(world, "Props");
        var rnd = new System.Random(11);
        for (int i = 0; i < 26; i++)
        {
            Vector3 p = FreeSpot(rnd, 28f);
            float s = 3f + (float)rnd.NextDouble() * 6f;
            Sph("Dune", props, new Vector3(p.x, -s * 0.25f, p.z), new Vector3(s * 1.6f, s * 0.6f, s), mDesertDune);
        }
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = FreeSpot(rnd, 18f);
            float h = 1f + (float)rnd.NextDouble() * 2.5f;
            var pil = Box("Broken Pillar", props, new Vector3(p.x, h * 0.5f - 0.2f, p.z), new Vector3(0.8f, h, 0.8f), mStone, true);
            pil.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 14f - 7f, (float)rnd.NextDouble() * 90f, 0f);
        }
        for (int i = 0; i < 5; i++)
        {
            Vector3 p = FreeSpot(rnd, 18f);
            var tree = Child(props, "Dead Tree");
            tree.localPosition = p;
            Cyl("Trunk", tree, new Vector3(0f, 1.2f, 0f), new Vector3(0.25f, 1.2f, 0.25f), mDeadWood);
            var b1 = Cyl("Branch", tree, new Vector3(0.35f, 2f, 0f), new Vector3(0.12f, 0.6f, 0.12f), mDeadWood);
            b1.transform.localRotation = Quaternion.Euler(0f, 0f, -40f);
            var b2 = Cyl("Branch", tree, new Vector3(-0.3f, 1.6f, 0.1f), new Vector3(0.1f, 0.5f, 0.1f), mDeadWood);
            b2.transform.localRotation = Quaternion.Euler(10f, 0f, 45f);
        }
        // a half-buried picture frame and chair (buried memories)
        var frame = Box("Buried Frame", props, new Vector3(3f, 0.35f, -8f), new Vector3(1.2f, 0.9f, 0.08f), mDeadWood, false);
        frame.transform.localRotation = Quaternion.Euler(-25f, 30f, 5f);
        var chair = Child(props, "Buried Chair");
        chair.localPosition = new Vector3(-12f, -0.2f, -12f);
        chair.localRotation = Quaternion.Euler(15f, 40f, 10f);
        Box("Seat", chair, new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 0.1f, 0.9f), mDeadWood, false);
        Box("Back", chair, new Vector3(0f, 1f, -0.4f), new Vector3(0.9f, 1f, 0.1f), mDeadWood, false);
    }

    static void DecorateForest(Transform world)
    {
        var props = Child(world, "Props");
        var rnd = new System.Random(23);
        for (int i = 0; i < 30; i++)
        {
            Vector3 p = FreeSpot(rnd, 19f);
            float h = 2.5f + (float)rnd.NextDouble() * 3f;
            var tree = Child(props, "Tree");
            tree.localPosition = p;
            Cyl("Trunk", tree, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.45f, h * 0.5f, 0.45f), mBark).AddComponent<CapsuleCollider>();
            float c = 2.2f + (float)rnd.NextDouble() * 1.6f;
            Sph("Canopy", tree, new Vector3(0f, h + c * 0.3f, 0f), new Vector3(c, c * 0.85f, c), rnd.NextDouble() < 0.5 ? mLeaf : mLeafDark);
            Sph("Canopy", tree, new Vector3(c * 0.3f, h + c * 0.6f, 0.2f), Vector3.one * c * 0.7f, mLeaf);
        }
        for (int i = 0; i < 24; i++)
        {
            Vector3 p = FreeSpot(rnd, 19f);
            var g = rnd.NextDouble() < 0.6 ? mGlowCyan : mGlowPink;
            Cyl("Stem", props, p + new Vector3(0f, 0.25f, 0f), new Vector3(0.05f, 0.25f, 0.05f), mLeafDark);
            Sph("Glow Plant", props, p + new Vector3(0f, 0.55f, 0f), Vector3.one * 0.22f, g);
        }
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = FreeSpot(rnd, 19f);
            Sph("Rock", props, p, new Vector3(1.2f, 0.7f, 1f), mStone);
        }
        // the spring the river comes from
        Sph("Spring Rocks", props, new Vector3(RiverX, 0.2f, -19.5f), new Vector3(5f, 1.4f, 2f), mStone);
        var spring = AddLight(props, new Vector3(RiverX, 1f, -18f), new Color(0.5f, 0.95f, 1f), 2f, 8f);
        spring.name = "Spring Glow";
    }

    /// <summary>Random spot that avoids the path, river, chasm, spawn and altar.</summary>
    static Vector3 FreeSpot(System.Random rnd, float extent)
    {
        for (int tries = 0; tries < 60; tries++)
        {
            float x = ((float)rnd.NextDouble() * 2f - 1f) * extent;
            float z = ((float)rnd.NextDouble() * 2f - 1f) * extent;
            bool bad = Mathf.Abs(x - PathX) < 3.5f
                    || Mathf.Abs(x - RiverX) < 4f
                    || (z > ChasmNear - 2.5f && z < ChasmFar + 2.5f)
                    || (Mathf.Abs(x) < 4f && z < -11f && z > -19f)
                    || (Mathf.Abs(x + 3.4f) < 2.5f && Mathf.Abs(z - 2.4f) < 2.5f);
            if (extent > 20f) bad = Mathf.Abs(x) < 19f && Mathf.Abs(z) < 19f; // outside dressing only
            if (!bad) return new Vector3(x, 0f, z);
        }
        return new Vector3(-17f, 0f, -17f);
    }

    // ================================================================== players / cameras / HUD

    static GameObject SetupPlayer(string[] names, string newName, Vector3 worldOrigin, Material mat, PuzzleHUD hud,
                                  int side, string interactKey, string powerKey, bool glow)
    {
        GameObject p = null;
        foreach (var n in names) { p = GameObject.Find(n); if (p) break; }
        if (!p) { Debug.LogError($"[BridgePuzzleBuilder] Player '{names[0]}' not found. Run Tools > Kyan > Build Split Camera Rig first."); return null; }

        p.name = newName;
        var cc = p.GetComponent<CharacterController>();
        cc.enabled = false;
        p.transform.SetPositionAndRotation(worldOrigin + new Vector3(0f, 0.1f, -15f), Quaternion.identity);
        cc.enabled = true;

        foreach (var r in p.GetComponentsInChildren<Renderer>())
            if (r.name == "Body" || r.name == "FacingMarker") r.sharedMaterial = mat;

        var hold = p.transform.Find("HoldPoint");
        if (!hold)
        {
            hold = new GameObject("HoldPoint").transform;
            hold.SetParent(p.transform, false);
        }
        hold.localPosition = new Vector3(0f, 1.15f, 0.65f);

        var inter = p.GetComponent<PlayerInteractor>();
        if (!inter) inter = p.AddComponent<PlayerInteractor>();
        inter.input = p.GetComponent<PlayerInputReader>();
        inter.holdPoint = hold;
        inter.hud = hud;
        inter.hudSide = side;
        inter.interactKey = interactKey;
        inter.powerKey = powerKey;

        var resp = p.GetComponent<PlayerRespawn>();
        if (!resp) resp = p.AddComponent<PlayerRespawn>();
        resp.respawnPoint = worldOrigin + new Vector3(0f, 0.1f, 0f);
        resp.killY = -6f;

        var aura = p.transform.Find("Aura");
        if (glow && !aura) AddLight(p.transform, new Vector3(0f, 1.3f, 0f), new Color(1f, 0.8f, 0.5f), 1.5f, 4f).name = "Aura";
        if (!glow && aura) Object.DestroyImmediate(aura.gameObject);
        EditorUtility.SetDirty(p);
        return p;
    }

    // ================================================================== guidance, flower, cutscene, Puzzle 2

    static GuideBeacon Beacon(Transform world, string name, Material beamMat, Color lightColor)
    {
        var root = Child(world, name);
        var beam = Cyl("Beam", root, new Vector3(0f, 12f, 0f), new Vector3(0.7f, 12f, 0.7f), beamMat);
        beam.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var gb = root.gameObject.AddComponent<GuideBeacon>();
        gb.beam = beam.GetComponent<Renderer>();
        gb.glow = AddLight(root, new Vector3(0f, 1.2f, 0f), lightColor, 0f, 6f);
        return gb;
    }

    static Transform BuildFlower(Transform world, Vector3 pos, out Transform petals, out Light light)
    {
        var root = Child(world, "Desert Flower");
        root.localPosition = pos;
        Cyl("Stem", root, new Vector3(0f, 0.36f, 0f), new Vector3(0.05f, 0.36f, 0.05f), mLeafDark);
        var leafL = Sph("Leaf", root, new Vector3(-0.12f, 0.24f, 0f), new Vector3(0.24f, 0.05f, 0.1f), mLeaf);
        leafL.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
        var leafR = Sph("Leaf", root, new Vector3(0.12f, 0.34f, 0f), new Vector3(0.24f, 0.05f, 0.1f), mLeaf);
        leafR.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);

        var head = Child(root, "Head");
        head.localPosition = new Vector3(0f, 0.74f, 0f);
        head.localRotation = Quaternion.Euler(-20f, 0f, 0f);
        Sph("Center", head, Vector3.zero, Vector3.one * 0.12f, mFlowerCenter);
        petals = Child(head, "Petals");
        for (int i = 0; i < 6; i++)
        {
            var pivot = Child(petals, "Petal Pivot");
            pivot.localRotation = Quaternion.Euler(0f, i * 60f, 0f);
            var petal = Sph("Petal", pivot, new Vector3(0f, 0f, 0.13f), new Vector3(0.12f, 0.035f, 0.2f), mPetal);
            petal.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        }
        light = AddLight(root, new Vector3(0f, 0.9f, 0.3f), new Color(1f, 0.8f, 0.6f), 0f, 5f);
        return root;
    }

    static FragmentCutscene BuildFragmentCutscene(Transform flower, Transform petals, Light flowerLight,
                                                  GameObject present, Light sun, PuzzleHUD hud)
    {
        var camGo = new GameObject("Cinematic Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.depth = 10f; // drawn over both split-screen cameras
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.86f, 0.8f, 0.68f);
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 120f;
        camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        cam.enabled = false;

        var canvasGo = new GameObject("CinematicUI");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
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
        var note = UIText(canvasGo.transform, "Note", font, 40, TextAnchor.UpperCenter);
        Anchor(note.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(-200f, 220f), new Vector2(0f, 280f));
        note.color = new Color(1f, 1f, 1f, 0f);
        note.fontStyle = FontStyle.Italic;
        note.lineSpacing = 1.2f;
        var fade = new GameObject("Fade").AddComponent<Image>();
        fade.transform.SetParent(canvasGo.transform, false);
        fade.color = new Color(0f, 0f, 0f, 0f);
        fade.raycastTarget = false;
        fade.rectTransform.anchorMin = Vector2.zero;
        fade.rectTransform.anchorMax = Vector2.one;
        fade.rectTransform.offsetMin = fade.rectTransform.offsetMax = Vector2.zero;

        var fc = new GameObject("FragmentCutscene").AddComponent<FragmentCutscene>();
        fc.cinematicCam = cam;
        fc.focus = flower;
        fc.flower = flower;
        fc.petals = petals;
        fc.flowerLight = flowerLight;
        fc.rippleMaterial = mRipple;
        fc.present = present ? present.transform : null;
        fc.sun = sun;
        fc.fadeImage = fade;
        fc.headerText = header;
        fc.noteText = note;
        fc.noteBackdrop = backdrop;
        fc.nextScene = "Puzzle2";

        var off = new System.Collections.Generic.List<Behaviour>();
        off.AddRange(Object.FindObjectsByType<SoulMovement>(FindObjectsSortMode.None));
        off.AddRange(Object.FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None));
        off.AddRange(Object.FindObjectsByType<OrbitCameraInput>(FindObjectsSortMode.None));
        fc.disableOnPlay = off.ToArray();

        var hide = new System.Collections.Generic.List<Canvas>();
        if (hud) hide.Add(hud.GetComponent<Canvas>());
        var split = GameObject.Find("SplitScreenUI");
        if (split) hide.Add(split.GetComponent<Canvas>());
        fc.hideCanvases = hide.ToArray();
        return fc;
    }

    const string Puzzle2Path = "Assets/Scenes/Sh/Puzzle2.unity";

    /// <summary>Creates a placeholder Puzzle 2 scene once. Never overwrites an existing one.</summary>
    static void EnsurePuzzle2Placeholder()
    {
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(Puzzle2Path))
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(s);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.12f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var lightGo = new GameObject("Directional Light");
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var canvasGo = new GameObject("PlaceholderUI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var title = UIText(canvasGo.transform, "Title", font, 72, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 60f));
            title.text = "Puzzle 2";
            var sub = UIText(canvasGo.transform, "Subtitle", font, 34, TextAnchor.MiddleCenter);
            Anchor(sub.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, -30f));
            sub.text = "Accepting the past  ·  coming soon";
            sub.color = new Color(0.8f, 0.85f, 0.9f);

            EditorSceneManager.SaveScene(s, Puzzle2Path);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
            EditorSceneManager.CloseScene(s, true);
        }

        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(x => x.path == Puzzle2Path))
        {
            int at = scenes.FindIndex(x => x.path == ScenePath);
            scenes.Insert(at >= 0 ? at + 1 : scenes.Count, new EditorBuildSettingsScene(Puzzle2Path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    static void SetupCamera(string name, Color sky)
    {
        var go = GameObject.Find(name);
        if (!go) return;
        var cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = sky;
        cam.farClipPlane = 120f; // the other world (300 m away) is never visible
    }

    static PuzzleHUD BuildHUD()
    {
        var go = new GameObject("PuzzleUI");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var hud = go.AddComponent<PuzzleHUD>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        for (int i = 0; i < 2; i++)
        {
            float x0 = i * 0.5f, x1 = x0 + 0.5f;

            var prompt = UIText(go.transform, i == 0 ? "Prompt Left" : "Prompt Right", font, 30, TextAnchor.LowerCenter);
            Anchor(prompt.rectTransform, new Vector2(x0, 0f), new Vector2(x1, 0f), new Vector2(0.5f, 0f), new Vector2(-40f, 50f), new Vector2(0f, 95f));
            prompt.text = "";
            hud.prompts[i] = prompt;

            var bar = new GameObject(i == 0 ? "Bar Left" : "Bar Right").AddComponent<Image>();
            bar.transform.SetParent(go.transform, false);
            bar.material = null;
            bar.color = new Color(0f, 0f, 0f, 0.55f);
            bar.raycastTarget = false;
            Anchor(bar.rectTransform, new Vector2(x0 + 0.17f, 0f), new Vector2(x1 - 0.17f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(0f, 70f));
            var fill = new GameObject("Fill").AddComponent<Image>();
            fill.transform.SetParent(bar.transform, false);
            fill.color = i == 0 ? new Color(0.95f, 0.85f, 0.6f) : new Color(0.55f, 1f, 0.6f);
            fill.raycastTarget = false;
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(0f, 1f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            bar.gameObject.SetActive(false);
            hud.barRoots[i] = bar.gameObject;
            hud.barFills[i] = frt;

            var msg = UIText(go.transform, i == 0 ? "Message Left" : "Message Right", font, 34, TextAnchor.UpperCenter);
            Anchor(msg.rectTransform, new Vector2(x0, 1f), new Vector2(x1, 1f), new Vector2(0.5f, 1f), new Vector2(-60f, 140f), new Vector2(0f, -175f));
            msg.color = new Color(1f, 1f, 1f, 0f);
            msg.text = "";
            hud.messages[i] = msg;
        }
        return hud;
    }

    static Text UIText(Transform parent, string name, Font font, int size, TextAnchor align)
    {
        var t = new GameObject(name).AddComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        return t;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    // ================================================================== primitives

    static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (parent) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject Box(string n, Transform p, Vector3 pos, Vector3 s, Material m, bool col) => Prim(PrimitiveType.Cube, n, p, pos, s, m, col);
    static GameObject Sph(string n, Transform p, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Sphere, n, p, pos, s, m, false);
    static GameObject Cyl(string n, Transform p, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Cylinder, n, p, pos, s, m, false);

    static void Wall(Transform parent, Vector3 pos, Vector3 size)
    {
        var w = Child(parent, "Wall");
        w.localPosition = pos;
        w.gameObject.AddComponent<BoxCollider>().size = size;
    }

    static Light AddLight(Transform parent, Vector3 localPos, Color c, float intensity, float range)
    {
        var go = new GameObject("Glow Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    // ================================================================== materials

    static void MakeMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Materials", "World");

        mSand = Lit("M_Desert_Sand", new Color(0.82f, 0.77f, 0.66f), 0.1f);
        mSandDark = Lit("M_Desert_SandMound", new Color(0.74f, 0.66f, 0.52f), 0.05f);
        mCliff = Lit("M_Desert_Cliff", new Color(0.42f, 0.35f, 0.27f), 0.05f);
        mDesertDune =Lit("M_Desert_Dune", new Color(0.86f, 0.81f, 0.71f), 0.05f);
        mStone = Lit("M_Stone", new Color(0.55f, 0.53f, 0.5f), 0.2f);
        mStoneLit = Unlit("M_Stone_Revealed", new Color(1f, 0.88f, 0.55f));
        mDeadWood = Lit("M_Desert_DeadWood", new Color(0.35f, 0.3f, 0.26f), 0.1f);
        mDesertWater = Glassy("M_Desert_River", new Color(0.38f, 0.47f, 0.56f, 0.85f));

        mGrass = Lit("M_Forest_Grass", new Color(0.3f, 0.58f, 0.25f), 0.15f);
        mDirt = Lit("M_Forest_Dirt", new Color(0.32f, 0.24f, 0.16f), 0.1f);
        mBark = Lit("M_Forest_Bark", new Color(0.36f, 0.24f, 0.15f), 0.15f);
        mLeaf = Lit("M_Forest_Leaf", new Color(0.25f, 0.7f, 0.3f), 0.25f);
        mLeafDark = Lit("M_Forest_LeafDark", new Color(0.12f, 0.45f, 0.22f), 0.25f);
        mRoot = Lit("M_Forest_Root", new Color(0.45f, 0.3f, 0.18f), 0.2f);
        mForestWater = Glassy("M_Forest_River", new Color(0.25f, 0.85f, 1f, 0.7f));
        mGlowCyan = Unlit("M_Glow_Cyan", new Color(0.4f, 1f, 0.95f));
        mGlowPink = Unlit("M_Glow_Pink", new Color(1f, 0.55f, 0.9f));

        mAbyss = Unlit("M_Abyss", new Color(0.04f, 0.03f, 0.03f));
        mSeed = Unlit("M_MemorySeed", new Color(0.75f, 1f, 0.45f));
        mFragment = Unlit("M_MirrorFragment", new Color(0.85f, 0.92f, 1f));
        mRipple = Unlit("M_Ripple", new Color(0.6f, 1f, 1f, 0.8f));
        MakeTransparent(mRipple);
        mSocketDormant = Lit("M_Socket_Dormant", new Color(0.3f, 0.32f, 0.3f), 0.3f);
        mSocketAwake = Unlit("M_Socket_Awake", new Color(0.45f, 1f, 0.55f));
        mPresent = Lit("M_Present", new Color(0.62f, 0.59f, 0.54f), 0.1f);
        mFuture = Lit("M_Future", new Color(1f, 0.72f, 0.35f), 0.4f);
        mBeamGold = Unlit("M_Beam_Gold", new Color(1f, 0.62f, 0.15f, 0.6f));
        MakeTransparent(mBeamGold);
        mBeamCyan = Unlit("M_Beam_Cyan", new Color(0.4f, 1f, 0.85f, 0.5f));
        MakeTransparent(mBeamCyan);
        mPetal = Lit("M_Flower_Petal", new Color(1f, 0.72f, 0.82f), 0.35f);
        mFlowerCenter = Unlit("M_Flower_Center", new Color(1f, 0.85f, 0.3f));
    }

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

    static Material Glassy(string name, Color c)
    {
        var m = Lit(name, c, 0.92f);
        MakeTransparent(m);
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
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
    }
}
