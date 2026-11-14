using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Kyan > Setup Characters (all scenes)
///  1. Imports the Mixamo FBX files in Assets/TwoWorlds/Animations as Humanoid clips (looping where needed,
///     root motion baked so the game scripts keep control of movement).
///  2. Builds AC_Present and AC_Future: Idle/Walk blend on Speed, Action (dig / grow) and Jump.
///  3. Puts the two rigged characters into every scene: the players in the three puzzles, the Present and
///     his reflection in the opening cutscene, and the four figures of the ending.
/// The original capsules stay (hidden) so colliders and scripts are untouched.
///
/// Expected files (any Mixamo clip works, only the file name matters):
///   Present_Idle, Present_Walk, Present_Action, Future_Idle, Future_Walk, Future_Action, Jump  (.fbx)
/// </summary>
public static class CharacterSetup
{
    public const string AnimDir = "Assets/TwoWorlds/Animations";
    const string CharDir = "Assets/TwoWorlds/Characters";
    static readonly string[] Required = { "Present_Idle", "Present_Walk", "Present_Action", "Future_Idle", "Future_Walk", "Future_Action", "Jump" };
    static readonly string[] Scenes =
    {
        "Assets/Scenes/Sh/Cutscene.unity", "Assets/Scenes/Sh/SplitCamera.unity",
        "Assets/Scenes/Sh/Puzzle2.unity", "Assets/Scenes/Sh/Puzzle3.unity"
    };

    [MenuItem("Tools/Kyan/Setup Characters (all scenes)")]
    public static void SetupAll()
    {
        var missing = MissingClips();
        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog("Setup Characters",
                $"Missing in {AnimDir}:\n\n{string.Join(".fbx\n", missing)}.fbx", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ConfigureClips();
        BuildControllers();
        foreach (var path in Scenes)
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) continue;
            var s = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int n = PlaceCharacters();
            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s);
            Debug.Log($"[CharacterSetup] {s.name}: {n} characters placed.");
        }
    }

    public static List<string> MissingClips()
    {
        var missing = new List<string>();
        foreach (var r in Required)
            if (!File.Exists($"{AnimDir}/{r}.fbx")) missing.Add(r);
        return missing;
    }

    // ================================================================== 1. clips

    public static void ConfigureClips()
    {
        foreach (var r in Required)
        {
            string path = $"{AnimDir}/{r}.fbx";
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            if (!imp) continue;
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.SaveAndReimport(); // creates the default clip list

            var clips = imp.defaultClipAnimations;
            if (clips.Length == 0) continue;
            var c = clips[0];
            c.name = r;
            c.loopTime = r != "Jump";
            c.lockRootRotation = true;      // bake into pose: the game moves the character, not the clip
            c.keepOriginalOrientation = true;
            // Jump is a fall-and-land clip that starts with the body ~1 m above its original root. Measuring
            // its height from the feet keeps the body on the player (the CharacterController does the real
            // jump), so the body and a carried object move together.
            c.lockRootHeightY = true;
            c.keepOriginalPositionY = r != "Jump";
            c.heightFromFeet = r == "Jump"; // Jump: body height measured from the feet, so it stays on the player
            c.lockRootPositionXZ = true;
            c.keepOriginalPositionXZ = true;
            imp.clipAnimations = new[] { c };
            imp.SaveAndReimport();
        }
    }

    public static AnimationClip Clip(string name)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath($"{AnimDir}/{name}.fbx"))
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        return null;
    }

    // ================================================================== 2. controllers

    public static void BuildControllers()
    {
        // The Present's own walk clip is a tiny in-place shuffle that slides at game speed, so the Present
        // walks with the normal walk, played slower (and SoulMovement.PresentSpeed / CharacterAnimator slump make it tired).
        BuildController("Present", 1.3f, "Future_Walk", SoulMovement.PresentSpeed, 0.75f); // slower, heavier steps
        BuildController("Future", 1.6f, "Future_Walk", 5.5f, 0.6f);
    }

    static void BuildController(string who, float walkRef, string walkClip, float topSpeed, float topRate)
    {
        string path = $"{AnimDir}/AC_{who}.controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Action", AnimatorControllerParameterType.Bool);
        var ps = ac.parameters; ps[1].defaultBool = true; ac.parameters = ps;

        var sm = ac.layers[0].stateMachine;
        var tree = new BlendTree { name = "Locomotion", blendParameter = "Speed", blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, ac);
        var walk = Clip(walkClip);
        tree.children = new[]
        {
            new ChildMotion { motion = Clip($"{who}_Idle"), threshold = 0f, timeScale = 1f },
            new ChildMotion { motion = walk, threshold = walkRef, timeScale = 1f },
            new ChildMotion { motion = walk, threshold = topSpeed, timeScale = topSpeed / walkRef * topRate }, // faster steps at full speed
        };
        var loco = sm.AddState("Locomotion");
        loco.motion = tree;
        sm.defaultState = loco;

        var action = sm.AddState("Action");
        action.motion = Clip($"{who}_Action");
        var jump = sm.AddState("Jump");
        jump.motion = Clip("Jump");

        Transition(loco, action, 0.2f).AddCondition(AnimatorConditionMode.If, 0, "Action");
        Transition(action, loco, 0.25f).AddCondition(AnimatorConditionMode.IfNot, 0, "Action");
        Transition(loco, jump, 0.1f).AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
        Transition(jump, loco, 0.15f).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
    }

    static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float dur)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = dur;
        return t;
    }

    static RuntimeAnimatorController Controller(string who) =>
        AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{AnimDir}/AC_{who}.controller");

    // ================================================================== 3. placing

    /// <summary>Puts the characters into the open scene. Safe to run again (replaces earlier ones).</summary>
    public static int PlaceCharacters()
    {
        if (!Controller("Present") || !Controller("Future")) return 0;
        int n = 0;

        // ---- players (three puzzles) ----
        n += Player("Present_P1", "Present") ? 1 : 0;
        n += Player("Future_P2", "Future") ? 1 : 0;

        // ---- opening cutscene ----
        var director = Object.FindAnyObjectByType<MirrorCutscene>();
        if (director)
        {
            if (director.character)
            {
                var m = Dress(director.character, "Present", "Present", 0f);
                director.head = Bone(m, HumanBodyBones.Head) ?? director.head;
                n++;
            }
            if (director.reflection)
            {
                var m = Dress(director.reflection.transform, "Present", "Present", 0f);
                director.reflectionHead = Bone(m, HumanBodyBones.Head) ?? director.reflectionHead;
                n++;
            }
            EditorUtility.SetDirty(director);
        }

        // ---- ending (Puzzle 3) ----
        var ending = Object.FindAnyObjectByType<MirrorEnding>();
        if (ending)
        {
            if (ending.presentFig) { Dress(ending.presentFig, "Present", "Present", 0f, true); n++; }
            if (ending.futureFig) { Dress(ending.futureFig, "Future", "Future", 0f, true); n++; }
            if (ending.reflectionFig)
            {
                var root = ending.reflectionFig.transform.parent ? ending.reflectionFig.transform.parent : ending.reflectionFig.transform;
                ending.reflectionPresent = Dress(root, "Present", "Present", 0f, false, "Character Present");
                ending.reflectionFuture = Dress(root, "Future", "Future", 0f, false, "Character Future");
                ending.reflectionFuture.SetActive(false);
                n++;
            }
            if (ending.wholeFig)
            {
                // the whole person: the Present's body, standing like the Future
                Dress(ending.wholeFig.transform, "Present", "Future", 0f);
                n++;
            }
            EditorUtility.SetDirty(ending);
        }
        return n;
    }

    static bool Player(string name, string who)
    {
        var p = GameObject.Find(name);
        if (!p) return false;
        Dress(p.transform, who, who, 0f);
        var ca = p.GetComponent<CharacterAnimator>();
        if (!ca) ca = p.AddComponent<CharacterAnimator>();
        ca.animator = p.transform.Find("Character").GetComponent<Animator>();
        ca.interactor = p.GetComponent<PlayerInteractor>();
        ca.controller = p.GetComponent<CharacterController>();
        ca.faceMovement = false; // SoulMovement turns the player
        EditorUtility.SetDirty(p);
        return true;
    }

    /// <summary>
    /// Hides the renderers under 'root' (the old primitives), adds the character model as a child and gives it an Animator.
    /// </summary>
    static GameObject Dress(Transform root, string body, string animSet, float yaw, bool walker = false,
                            string childName = "Character")
    {
        var old = root.Find(childName);
        if (old) Object.DestroyImmediate(old.gameObject);
        // Hide the old primitives, but never another character model under the same root.
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            if (!UnderCharacter(r.transform, root)) r.enabled = false;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharDir}/Character_{body}.fbx");
        var m = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        m.name = childName;
        m.transform.SetParent(root, false);
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        m.transform.localScale = Vector3.one;

        var anim = m.GetComponent<Animator>();
        if (!anim) anim = m.AddComponent<Animator>();
        anim.runtimeAnimatorController = Controller(animSet);
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var smr in m.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.updateWhenOffscreen = true;
            smr.enabled = true;
        }

        if (walker)
        {
            var ca = root.GetComponent<CharacterAnimator>();
            if (!ca) ca = root.gameObject.AddComponent<CharacterAnimator>();
            ca.animator = anim;
            ca.faceMovement = true;
        }
        return m;
    }

    static bool UnderCharacter(Transform t, Transform root)
    {
        for (; t && t != root; t = t.parent)
            if (t.name.StartsWith("Character")) return true;
        return false;
    }

    static Transform Bone(GameObject model, HumanBodyBones b)
    {
        var a = model ? model.GetComponent<Animator>() : null;
        return a && a.isHuman ? a.GetBoneTransform(b) : null;
    }
}
