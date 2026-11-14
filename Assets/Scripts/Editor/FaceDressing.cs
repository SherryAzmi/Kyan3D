using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Extra face and hair pieces for the Future self in the café ending (the models have no facial rig):
///  - a fuller head of hair: a rounded cap (plus a few tufts) over the top and back of the skull,
///  - two soft dimples at the corners of the mouth,
///  - a smirk (one mouth corner lifted, a deeper dimple), hidden until the last close-up.
/// Everything is placed from the model's own mesh (lips, skin, hair, eye positions) and parented to the
/// head bone. The same pieces go on the reflection model so the mirror shows them too.
/// </summary>
public static class FaceDressing
{
    public class Parts
    {
        public GameObject[] smile;   // a curved smiling mouth (person and reflection), shown once he wakes
        public GameObject[] smirk;   // the lopsided version, for the last close-up
        public Transform[] dimples;  // grow a little when he smirks
        public Transform mouth;      // the middle of his mouth (the cup goes here when he sips)
    }

    public static Parts Dress(GameObject person, GameObject reflection)
    {
        var parts = new Parts();
        var anim = person.GetComponent<Animator>();
        var smr = person.GetComponentInChildren<SkinnedMeshRenderer>();
        var head = anim ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        if (!smr || !head) return parts;

        // ---- measure the face (bind pose) ----
        var baked = new Mesh();
        smr.BakeMesh(baked, true);
        var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
        var verts = baked.vertices;
        var mats = smr.sharedMaterials;
        List<Vector3> Part(string key)
        {
            var list = new List<Vector3>();
            for (int s = 0; s < baked.subMeshCount && s < mats.Length; s++)
            {
                if (!mats[s] || !mats[s].name.Contains(key)) continue;
                foreach (var i in baked.GetTriangles(s)) list.Add(m.MultiplyPoint3x4(verts[i]));
            }
            return list;
        }
        var lips = Part("Lips");
        var skin = Part("Skin");
        var hair = Part("Hair");
        var eyes = Part("EyeWhite");
        Material hairMat = null, skinMat = null, lipsMat = null;
        foreach (var mt in mats)
        {
            if (!mt) continue;
            if (mt.name.Contains("Hair") && !hairMat) hairMat = mt;
            if (mt.name.Contains("Skin") && !skinMat) skinMat = mt;
            if (mt.name.Contains("Lips") && !lipsMat) lipsMat = mt;
        }
        Object.DestroyImmediate(baked);
        if (lips.Count == 0 || skin.Count == 0) return parts;

        Bounds B(List<Vector3> l) { var b = new Bounds(l[0], Vector3.zero); foreach (var v in l) b.Encapsulate(v); return b; }
        var lipsB = B(lips);
        float eyeY = eyes.Count > 0 ? B(eyes).center.y : lipsB.center.y + 0.06f;
        var skull = new List<Vector3>();
        foreach (var v in skin) if (v.y > eyeY - 0.01f && v.y > head.position.y) skull.Add(v);
        foreach (var v in hair) skull.Add(v);
        var skullB = skull.Count > 0 ? B(skull) : new Bounds(head.position + Vector3.up * 0.12f, Vector3.one * 0.22f);

        // the face surface (largest z) near a point on the front of the face
        Vector3 Surface(float x, float y)
        {
            // keep exactly this x and y; take only the depth of the skin there
            float bestZ = float.MinValue;

            foreach (var v in skin)
                if (Mathf.Abs(v.x - x) < 0.006f && Mathf.Abs(v.y - y) < 0.006f && v.z > bestZ) bestZ = v.z;
            return new Vector3(x, y, bestZ > float.MinValue ? bestZ : lipsB.max.z);
        }

        // ---- materials ----
        Color skinC = skinMat ? skinMat.GetColor("_BaseColor") : new Color(0.9f, 0.72f, 0.6f);
        Color lipsC = lipsMat ? lipsMat.GetColor("_BaseColor") : new Color(0.75f, 0.45f, 0.4f);
        var mDimple = Mat("M_Cafe_Dimple", skinC * 0.8f);

        // ---- build on the person, then copy the same local pieces to the reflection ----
        var pieces = new GameObject("Face Extras");
        pieces.transform.SetParent(head, false);
        pieces.transform.localPosition = Vector3.zero;
        pieces.transform.localRotation = Quaternion.identity;
        pieces.transform.localScale = Vector3.one;

        // hair: one smooth cap hugging the top and back of the skull, fuller at the sides, a swept fringe
        if (hairMat)
        {
            Vector3 c = skullB.center, s = skullB.size;
            Piece(pieces.transform, "Hair Cap", PrimitiveType.Sphere, c + Vector3.up * s.y * 0.17f - Vector3.forward * s.z * 0.06f,
                  Quaternion.identity, new Vector3(s.x * 1.03f, s.y * 0.86f, s.z * 1.0f), hairMat);
            Piece(pieces.transform, "Hair Back", PrimitiveType.Sphere, c - Vector3.forward * s.z * 0.22f - Vector3.up * s.y * 0.02f,
                  Quaternion.identity, new Vector3(s.x * 0.97f, s.y * 0.78f, s.z * 0.72f), hairMat);
            foreach (var side in new[] { -1f, 1f })
                Piece(pieces.transform, "Hair Side", PrimitiveType.Sphere, c + new Vector3(side * s.x * 0.36f, s.y * 0.06f, -s.z * 0.06f),
                      Quaternion.Euler(0f, 0f, side * -8f), new Vector3(s.x * 0.36f, s.y * 0.62f, s.z * 0.78f), hairMat);
            // the fringe sweeps across the forehead from his right to his left
            Piece(pieces.transform, "Fringe", PrimitiveType.Sphere, c + new Vector3(s.x * 0.06f, s.y * 0.3f, s.z * 0.3f),
                  Quaternion.Euler(-28f, 0f, -14f), new Vector3(s.x * 0.92f, s.y * 0.26f, s.z * 0.46f), hairMat);
            Piece(pieces.transform, "Fringe Tip", PrimitiveType.Sphere, c + new Vector3(-s.x * 0.24f, s.y * 0.2f, s.z * 0.36f),
                  Quaternion.Euler(-30f, 10f, -32f), new Vector3(s.x * 0.36f, s.y * 0.16f, s.z * 0.26f), hairMat);
        }

        // how far forward the face is at a point (skin or lips)
        float Front(float x, float y)
        {
            float z = float.MinValue;
            foreach (var v in skin) if (Mathf.Abs(v.x - x) < 0.006f && Mathf.Abs(v.y - y) < 0.006f && v.z > z) z = v.z;
            // (skin only: the flat lips are hidden while he smiles, so the new mouth sits on the skin)
            return z > float.MinValue ? z : lipsB.max.z;
        }

        // dimples, at the corners of the smile
        float half = lipsB.extents.x;
        float cx = lipsB.center.x, cy = lipsB.center.y;
        var dims = new List<Transform>();
        foreach (var side in new[] { -1f, 1f })
        {
            var at = Surface(cx + side * (half + 0.013f), cy + 0.009f);
            var d = Piece(pieces.transform, "Dimple", PrimitiveType.Sphere, at + Vector3.forward * 0.0015f,
                          Quaternion.Euler(0f, side * 25f, side * 18f), new Vector3(0.0035f, 0.014f, 0.003f), mDimple);
            dims.Add(d.transform);
        }

        // the mouth: the model's flat lips are recoloured to skin by the cutscene, and these curves drawn on top
        var mLipLine = Mat("M_Cafe_LipLine", lipsC * 0.55f);
        var mLip = Mat("M_Cafe_Lip", lipsC * 0.95f);
        var smile = Mouth(pieces.transform, "Smile", cx, cy, half * 1.12f, 0.011f, 0.011f, Front, mLipLine, mLip);
        var smirk = Mouth(pieces.transform, "Smirk", cx + half * 0.06f, cy + 0.001f, half * 1.1f, 0.002f, 0.017f, Front, mLipLine, mLip);
        smile.SetActive(false);
        smirk.SetActive(false);
        var mouth = new GameObject("Mouth").transform;
        mouth.position = new Vector3(cx, cy, Front(cx, cy) + 0.004f);
        mouth.SetParent(pieces.transform, true);

        // the same on the reflection's head
        var rAnim = reflection ? reflection.GetComponent<Animator>() : null;
        var rHead = rAnim ? rAnim.GetBoneTransform(HumanBodyBones.Head) : null;
        GameObject rSmile = null, rSmirk = null;
        if (rHead)
        {
            var copy = Object.Instantiate(pieces, rHead);
            copy.name = "Face Extras";
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;
            var t = copy.transform.Find("Smile"); if (t) rSmile = t.gameObject;
            t = copy.transform.Find("Smirk"); if (t) rSmirk = t.gameObject;
            foreach (Transform c in copy.transform) if (c.name == "Dimple") dims.Add(c);
        }
        parts.smile = rSmile ? new[] { smile, rSmile } : new[] { smile };
        parts.smirk = rSmirk ? new[] { smirk, rSmirk } : new[] { smirk };
        parts.dimples = dims.ToArray();
        parts.mouth = mouth;
        return parts;
    }

    /// <summary>A mouth drawn as two curves (a thin dark line where the lips meet, a fuller lower lip),
    /// its corners lifted by 'liftLeft' / 'liftRight' metres: equal = a smile, uneven = a smirk.</summary>
    static GameObject Mouth(Transform parent, string name, float cx, float cy, float halfWidth, float liftLeft, float liftRight,
                            System.Func<float, float, float> front, Material line, Material lip)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        const int N = 14;
        float Y(float t) => cy + (t < 0f ? liftLeft : liftRight) * t * t;                    // the line where the lips meet
        float Low(float t) => Y(t) - 0.0055f * (1f - t * t);                                   // the middle of the lower lip
        for (int i = 0; i < N; i++)
        {
            float t0 = -1f + 2f * i / N, t1 = -1f + 2f * (i + 1) / N, tm = (t0 + t1) * 0.5f;
            // the lip line
            Seg(g.transform, "Lip Line", cx + t0 * halfWidth, Y(t0), cx + t1 * halfWidth, Y(t1), 0.0032f, front, line, 0.0035f);
            // the lower lip, fuller in the middle
            float th = 0.0075f * (1f - 0.55f * tm * tm);
            Seg(g.transform, "Lower Lip", cx + t0 * halfWidth * 0.86f, Low(t0 * 0.86f), cx + t1 * halfWidth * 0.86f, Low(t1 * 0.86f), th, front, lip, 0.0028f);
        }
        return g;
    }

    static void Seg(Transform parent, string name, float x0, float y0, float x1, float y1, float thickness,
                    System.Func<float, float, float> front, Material mat, float lift)
    {
        float mx = (x0 + x1) * 0.5f, my = (y0 + y1) * 0.5f;
        float len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) * 1.9f; // overlapping, so the curve reads smooth
        float ang = Mathf.Atan2(y1 - y0, x1 - x0) * Mathf.Rad2Deg;
        var p = new Vector3(mx, my, front(mx, my) + lift);
        Piece(parent, name, PrimitiveType.Sphere, p, Quaternion.Euler(0f, 0f, ang), new Vector3(len, thickness, 0.004f), mat);
    }

    static GameObject Piece(Transform parent, string name, PrimitiveType type, Vector3 worldPos, Quaternion worldRot, Vector3 worldSize, Material mat)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetPositionAndRotation(worldPos, worldRot);
        g.transform.localScale = worldSize;
        g.transform.SetParent(parent, true);
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    static Material Mat(string name, Color c)
    {
        string path = $"Assets/Materials/Cafe/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        c.a = 1f;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(m);
        return m;
    }
}
