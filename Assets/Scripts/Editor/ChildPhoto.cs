using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tools > Kyan > Render Childhood Photo — renders the Future as a small, happy child (big head, small body,
/// yellow raincoat, blue trousers, red boots, no beard) standing in a sunny meadow, then turns the render into
/// an old printed photo (warm faded colours, soft vignette, grain, white border).
/// Done in a temporary scene that is thrown away. Output: Assets/Textures/Life/child_photo.png
/// </summary>
public static class ChildPhoto
{
    public const string OutPath = "Assets/Textures/Life/child_photo.png";
    const string CharPath = "Assets/TwoWorlds/Characters/Character_Future.fbx";
    static readonly Vector3 Stage = new Vector3(5000f, 0f, 0f);
    static Vector3 photoLook = Stage + new Vector3(0f, 0.62f, 0f);
    static float photoHeight = 1.1f;

    [MenuItem("Tools/Kyan/Render Childhood Photo")]
    public static string Render()
    {
        var keep = EditorSceneManager.GetActiveScene();
        var temp = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(temp);
        foreach (var g in temp.GetRootGameObjects())
            if (g.GetComponent<Camera>()) Object.DestroyImmediate(g);
            else if (g.GetComponent<Light>())
            {
                g.transform.rotation = Quaternion.Euler(38f, 150f, 0f); // warm afternoon sun from behind the camera
                var l = g.GetComponent<Light>();
                l.color = new Color(1f, 0.93f, 0.8f);
                l.intensity = 1.25f;
                l.shadows = LightShadows.Soft;
            }

        string result;
        AnimationMode.StartAnimationMode();
        try
        {
            BuildMeadow();
            var child = BuildChild();
            var tex = Shoot(1024);
            Age(tex);
            Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
            File.WriteAllBytes(OutPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            result = "photo written";
        }
        finally
        {
            AnimationMode.StopAnimationMode();
            EditorSceneManager.SetActiveScene(keep);
            EditorSceneManager.CloseScene(temp, true);
        }
        AssetDatabase.ImportAsset(OutPath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(OutPath);
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return result;
    }

    /// <summary>Puts the photo on the canvas of the broken picture frame (Puzzle 2), on both faces.</summary>
    [MenuItem("Tools/Kyan/Put Childhood Photo In Frame (open scene)")]
    public static string PlaceInFrame()
    {
        if (!File.Exists(OutPath)) Render();
        Transform frame = null;
        foreach (var c in Object.FindObjectsByType<Carryable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.name == "Picture Frame") frame = c.transform;
        if (!frame) return "no picture frame in this scene";

        // the canvas of the team's frame model if it is there, else the frame's own "Picture" panel
        Transform canvas = null;
        foreach (var r in frame.GetComponentsInChildren<MeshRenderer>(true))
            if (r.name == "Canvas") canvas = r.transform;
        if (!canvas) canvas = frame.Find("Picture");
        if (!canvas) return "no canvas found on the frame";

        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/World/M_Child_Photo.mat");
        if (!mat)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, "Assets/Materials/World/M_Child_Photo.mat");
        }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(OutPath));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.35f); // a little glossy, like photo paper
        mat.SetFloat("_AlphaClip", 1f);     // the worn rounded corners
        mat.SetFloat("_Cutoff", 0.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(mat);

        var mf = canvas.GetComponent<MeshFilter>();
        Bounds b = mf ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
        float size = Mathf.Min(b.size.x, b.size.y) * 0.96f, depth = b.extents.z + 0.004f;
        foreach (var side in new[] { -1f, 1f })
        {
            string n = side < 0 ? "Photo (front)" : "Photo (back)";
            var old = canvas.Find(n);
            if (old) Object.DestroyImmediate(old.gameObject);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = n;
            q.transform.SetParent(canvas, false);
            q.transform.localPosition = b.center + new Vector3(0f, 0f, side * depth);
            q.transform.localRotation = side < 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            q.transform.localScale = new Vector3(size, size, 1f);
            q.GetComponent<Renderer>().sharedMaterial = mat;
        }
        return "photo placed on " + canvas.name;
    }

    // ================================================================== the scene in the photo

    static void BuildMeadow()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.position = Stage;
        ground.transform.localScale = Vector3.one * 4f;
        var gm = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/World/M_Ground_Forest.mat"));
        gm.SetTextureScale("_BaseMap", new Vector2(10f, 10f));
        ground.GetComponent<Renderer>().sharedMaterial = gm;

        var rnd = new System.Random(7);
        var grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/World/M_Grass_Blades.mat");
        var flowers = new Material[4];
        for (int i = 0; i < 4; i++) flowers[i] = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/World/M_Flower_{i}.mat");
        for (int k = 0; k < 260; k++)
        {
            var p = Stage + new Vector3(((float)rnd.NextDouble() - 0.5f) * 9f, 0f, -1.5f - (float)rnd.NextDouble() * 7f + 2.2f);
            if (Mathf.Abs(p.x - Stage.x) < 0.45f && Mathf.Abs(p.z - Stage.z) < 0.45f) continue; // the child's feet
            bool flower = rnd.NextDouble() < 0.35;
            var mat = flower ? flowers[rnd.Next(4)] : grass;
            float s = flower ? 0.35f : 0.45f;
            for (int q = 0; q < 2; q++)
            {
                var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(card.GetComponent<Collider>());
                card.transform.position = p + Vector3.up * s * 0.5f;
                card.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 180f + q * 90f, 0f);
                card.transform.localScale = new Vector3(s * 1.2f, s, 1f);
                card.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }
        foreach (var (model, pos, scale) in new[] { ("Tree_Round", new Vector3(-2.6f, 0f, -5f), 0.8f), ("Tree_Tall", new Vector3(3.2f, 0f, -7f), 0.65f), ("Bush", new Vector3(1.7f, 0f, -2.2f), 0.8f) })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/TwoWorlds/Models/{model}.fbx");
            if (!prefab) continue;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            g.transform.position = Stage + pos;
            g.transform.localScale = Vector3.one * scale;
        }
    }

    static GameObject BuildChild()
    {
        // Sampling a humanoid clip resets the character's own position, so it stands inside a holder
        // that carries the placement, the turn towards the camera and the child size.
        var holder = new GameObject("Child").transform;
        holder.position = Stage;
        holder.rotation = Quaternion.Euler(0f, 12f, 0f); // turned a little towards the camera, like a posed snapshot
        holder.localScale = Vector3.one * 0.6f;          // small body
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(holder, false);

        // happy pose from the Future's idle animation
        var clip = CharacterSetup.Clip("Future_Idle");
        if (clip)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(go, clip, clip.length * 0.3f);
            AnimationMode.EndSampling();
        }

        // child proportions: a big head, short legs and arms (the small overall size comes from the holder)
        var anim = go.GetComponent<Animator>();
        Transform Bone(HumanBodyBones b) => anim && anim.isHuman ? anim.GetBoneTransform(b) : null;
        // A 3-year-old: head about a quarter of the height, short chubby legs and arms, round tummy.
        // Limbs are shortened ALONG the bone only (and kept thick), otherwise they just get thin.
        void Shape(HumanBodyBones b, HumanBodyBones next, float length, float thick)
        {
            var t = Bone(b);
            var n = Bone(next);
            if (!t) return;
            Vector3 s = Vector3.one * thick;
            if (n)
            {
                Vector3 d = t.InverseTransformPoint(n.position);
                int axis = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (Mathf.Abs(d.x) > Mathf.Abs(d.z) ? 0 : 2) : (Mathf.Abs(d.y) > Mathf.Abs(d.z) ? 1 : 2);
                s[axis] = length;
            }
            t.localScale = s;
        }
        Shape(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.55f, 1.35f);
        Shape(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.55f, 1.35f);
        Shape(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.75f, 1.0f);
        Shape(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.75f, 1.0f);
        Shape(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.62f, 1.3f);
        Shape(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.62f, 1.3f);
        Shape(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.8f, 1.0f);
        Shape(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.8f, 1.0f);
        Shape(HumanBodyBones.Spine, HumanBodyBones.Chest, 0.8f, 1.22f);   // short, round tummy
        Shape(HumanBodyBones.Chest, HumanBodyBones.Neck, 0.85f, 0.95f);
        var headT = Bone(HumanBodyBones.Head);
        if (headT) headT.localScale = Vector3.one * 2.15f;                 // a big round toddler head

        // a happy "yay!" pose: both arms thrown up, head tilted up to the sun
        void Turn(HumanBodyBones b, float deg, Vector3 axis) { var t = Bone(b); if (t) t.rotation = Quaternion.AngleAxis(deg, axis) * t.rotation; }
        Vector3 fwd = holder.forward, right = holder.right;
        Turn(HumanBodyBones.LeftUpperArm, -140f, fwd);
        Turn(HumanBodyBones.RightUpperArm, 140f, fwd);
        Turn(HumanBodyBones.LeftLowerArm, -25f, fwd);
        Turn(HumanBodyBones.RightLowerArm, 25f, fwd);
        Turn(HumanBodyBones.Head, -10f, right);

        // shorter legs lift the feet off the ground: lower the child until the soles touch the grass
        var lf = Bone(HumanBodyBones.LeftFoot);
        var rf = Bone(HumanBodyBones.RightFoot);
        if (lf && rf)
        {
            float feet = Mathf.Min(lf.position.y, rf.position.y) - Stage.y;
            holder.position -= Vector3.up * (feet - 0.045f);
        }
        var headBone = Bone(HumanBodyBones.Head);
        if (headBone) photoLook = (headBone.position + Stage) * 0.5f + Vector3.up * 0.05f;
        photoHeight = headBone ? headBone.position.y - Stage.y + 0.35f : 1.1f;

        // the outfit: yellow raincoat, blue trousers, red boots, and no beard (he's a kid)
        Color skin = Color.white;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            foreach (var m in r.sharedMaterials)
                if (m && m.name.Contains("Skin") && m.HasProperty("_BaseColor")) skin = m.GetColor("_BaseColor");
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (!mats[i]) continue;
                var m = new Material(mats[i]);
                string n = mats[i].name;
                if (n.Contains("Hoodie")) m.SetColor("_BaseColor", new Color(1f, 0.8f, 0.12f));
                else if (n.Contains("Pants")) m.SetColor("_BaseColor", new Color(0.22f, 0.42f, 0.85f));
                else if (n.Contains("Shoes")) m.SetColor("_BaseColor", new Color(0.86f, 0.14f, 0.12f));
                else if (n.Contains("Scarf")) m.SetColor("_BaseColor", new Color(1f, 0.92f, 0.55f));
                else if (n.Contains("Beard")) m.SetColor("_BaseColor", skin);
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
        return go;
    }

    static Texture2D Shoot(int size)
    {
        var camGo = new GameObject("Photo Camera");
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 80f;
        // frame the whole child (arms up included) with a little meadow around him
        Vector3 look = photoLook;
        float dist = photoHeight * 0.68f / Mathf.Tan(15f * Mathf.Deg2Rad);
        camGo.transform.position = look + new Vector3(dist * 0.12f, 0.12f, dist);
        camGo.transform.rotation = Quaternion.LookRotation(look - camGo.transform.position);

        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
        return tex;
    }

    // ================================================================== make it look like an old print

    /// <summary>
    /// Turns a clean render into a photo from decades ago: soft, sepia-toned and faded, with grain,
    /// dust, scratches, a fold crease and a yellowed, stained paper border with worn corners.
    /// </summary>
    static void Age(Texture2D tex)
    {
        int n = tex.width;
        var src = tex.GetPixels();
        var px = new Color[src.Length];
        var rnd = new System.Random(11);

        // soft focus of an old lens (small blur)
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Color s = Color.clear;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        s += src[Mathf.Clamp(y + dy, 0, n - 1) * n + Mathf.Clamp(x + dx, 0, n - 1)];
                px[y * n + x] = s / 9f;
            }

        // scratches: thin vertical lines, a few short ones
        var scratch = new float[n * n];
        for (int k = 0; k < 14; k++)
        {
            int sx = rnd.Next(n), y0 = rnd.Next(n), len = n / 6 + rnd.Next(n / 2);
            float strength = 0.15f + 0.3f * (float)rnd.NextDouble(), drift = ((float)rnd.NextDouble() - 0.5f) * 0.08f;
            for (int t = 0; t < len; t++)
            {
                int yy = y0 + t, xx = sx + (int)(t * drift);
                if (yy >= n || xx < 0 || xx >= n) break;
                scratch[yy * n + xx] = Mathf.Max(scratch[yy * n + xx], strength);
            }
        }
        // dust specks
        for (int k = 0; k < 180; k++)
        {
            int cx = rnd.Next(n), cy = rnd.Next(n), r = rnd.Next(1, 3);
            float a = rnd.NextDouble() < 0.5 ? -0.35f : 0.3f; // dark or light dust
            for (int yy = cy - r; yy <= cy + r; yy++)
                for (int xx = cx - r; xx <= cx + r; xx++)
                    if (xx >= 0 && yy >= 0 && xx < n && yy < n) scratch[yy * n + xx] = a;
        }

        float border = n * 0.06f, bottom = border * 1.7f;
        float creaseX = n * 0.63f; // the photo was folded once
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float u = x / (float)n - 0.5f, v = y / (float)n - 0.5f;
                // yellowed, stained paper (worse towards the edges and corners)
                float edgeDist = Mathf.Min(Mathf.Min(x, n - 1 - x), Mathf.Min(y, n - 1 - y)) / (float)n;
                float stain = Mathf.PerlinNoise(x * 0.012f + 3f, y * 0.012f + 7f);
                float age = Mathf.Clamp01(0.35f + stain * 0.5f + (0.08f - Mathf.Min(edgeDist, 0.08f)) * 6f);
                Color paper = Color.Lerp(new Color(0.94f, 0.9f, 0.8f), new Color(0.78f, 0.67f, 0.48f), age * 0.75f);

                bool inBorder = x < border || y < bottom || x > n - border || y > n - border;
                Color c;
                if (inBorder) c = paper * (0.96f + 0.06f * (float)rnd.NextDouble());
                else
                {
                    c = px[i];
                    // sepia toning, faded contrast, lifted blacks
                    float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    var sepia = new Color(lum * 1.07f, lum * 0.88f, lum * 0.66f);
                    c = Color.Lerp(c, sepia, 0.62f);
                    c = Color.Lerp(new Color(0.5f, 0.44f, 0.35f), c, 0.78f);       // faded towards a warm grey
                    c = c * 0.86f + new Color(0.1f, 0.08f, 0.05f);
                    // uneven fading and a strong vignette of an old print
                    c = Color.Lerp(c, paper, stain * 0.18f);
                    c *= 0.72f + 0.28f * (1f - Mathf.Clamp01((u * u + v * v) * 1.6f));
                    // grain, scratches, dust
                    float grain = ((float)rnd.NextDouble() - 0.5f) * 0.09f;
                    c += new Color(grain, grain * 0.95f, grain * 0.9f);
                    float s = scratch[i];
                    if (s > 0f) c = Color.Lerp(c, new Color(0.95f, 0.92f, 0.85f), s);
                    else if (s < 0f) c *= 1f + s;
                }
                // the fold crease (a light line with a dark shadow next to it)
                float d = x - (creaseX + Mathf.Sin(y * 0.01f) * 6f);
                if (Mathf.Abs(d) < 1.5f) c = Color.Lerp(c, new Color(0.96f, 0.93f, 0.86f), 0.55f);
                else if (d > 1.5f && d < 4.5f) c *= 0.88f;
                // worn, rounded corners
                float cr = n * 0.035f;
                float qx = Mathf.Min(x, n - 1 - x), qy = Mathf.Min(y, n - 1 - y);
                if (qx < cr && qy < cr && new Vector2(cr - qx, cr - qy).magnitude > cr) c = new Color(0f, 0f, 0f, 0f);
                else c.a = 1f;
                px[i] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
    }
}
