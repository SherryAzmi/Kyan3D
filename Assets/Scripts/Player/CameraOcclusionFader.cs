using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Self-installing: anything standing between a player's camera and that player (a tree canopy, a
/// trunk, a cactus, a rock...) fades to see-through, and fades back once the view is clear.
/// Works on meshes without colliders (the camera's Deoccluder only knows colliders, and most props
/// have none). URP Lit / Simple Lit / Unlit materials get a transparent copy whose alpha is faded;
/// any other shader is hidden instead (it still casts its shadow). No scene or builder changes.
/// </summary>
[DefaultExecutionOrder(10000)] // after Cinemachine has placed the cameras
public class CameraOcclusionFader : MonoBehaviour
{
    const float FadedAlpha = 0.25f;
    const float FadeSpeed = 6f;          // alpha per second
    const float Padding = 0.35f;         // how close to the line of sight still counts as blocking
    const float MaxSize = 25f;           // ground, hills, water: never faded
    const float Reach = 38f;             // only props near a play area (cameras stay inside it)

    class Entry
    {
        public Renderer r;
        public Bounds bounds;
        public Material[] original, faded; // faded == null: shader can't fade, hide instead
        public Color[] baseColors;
        public ShadowCastingMode shadow;
        public float alpha = 1f;
        public bool blocking, usingFaded, noFade;
        public bool water; // river water, waterfall sheet, mist and spray: fade when the camera gets into or very close to them
    }

    const float WaterDistance = 1.5f; // camera closer than this to water (or inside it): the water fades

    /// <summary>River water, waterfalls, mist and spray (not the river bed, banks or rocks).</summary>
    static bool IsWater(Renderer r)
    {
        string n = r.name.ToLowerInvariant();
        if (n.Contains("rock") || n.Contains("stone") || n.Contains("bed") || n.Contains("bank")) return false;
        if (r.GetComponentInParent<Waterfall>()) return true;
        if (n.Contains("water") || n.Contains("mist") || n.Contains("spray") || n.Contains("falls")) return true;
        foreach (var m in r.sharedMaterials)
            if (m && m.name.ToLowerInvariant().Contains("water")) return true;
        return false;
    }

    struct View { public Camera cam; public Transform player; }

    readonly List<Entry> entries = new List<Entry>();
    readonly List<View> views = new List<View>();
    static readonly Dictionary<Material, Material> fadedCopies = new Dictionary<Material, Material>();
    MaterialPropertyBlock mpb;
    bool collected;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Install();

    static void Install()
    {
        if (FindAnyObjectByType<CameraOcclusionFader>()) return;
        if (FindObjectsByType<SoulMovement>(FindObjectsSortMode.None).Length == 0) return; // no players here
        new GameObject("Camera Occlusion Fader").AddComponent<CameraOcclusionFader>();
    }

    void Collect()
    {
        collected = true;
        views.Clear();
        var centres = new List<Vector3>();
        foreach (var sm in FindObjectsByType<SoulMovement>(FindObjectsSortMode.None))
        {
            if (!sm.viewCamera) continue;
            var cam = sm.viewCamera.GetComponent<Camera>();
            if (!cam) continue;
            views.Add(new View { cam = cam, player = sm.transform });
            centres.Add(new Vector3(sm.transform.position.x < 150f ? 0f : 300f, 0f, 0f)); // desert / forest centre
        }

        entries.Clear();
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            bool water = (r is MeshRenderer || r is ParticleSystemRenderer) && IsWater(r);
            if (!water && (!(r is MeshRenderer) || !r.enabled)) continue;
            if (r.GetComponentInParent<SoulMovement>() || r.GetComponentInParent<Carryable>()) continue;
            var b = r.bounds;
            if (!water && (b.size.x > MaxSize || b.size.z > MaxSize || b.size.y > MaxSize)) continue;
            // water can be hidden or still empty at load (the river fills later): judge it by where it is, not its size
            Vector3 at = water ? r.transform.position : b.center;
            bool near = false;
            foreach (var c in centres)
                if (Mathf.Abs(at.x - c.x) < Reach && Mathf.Abs(at.z - c.z) < Reach) { near = true; break; }
            if (!near) continue;
            entries.Add(new Entry { r = r, bounds = b, original = r.sharedMaterials, shadow = r.shadowCastingMode, water = water });
        }

    }

    void LateUpdate()
    {
        if (!collected) Collect(); // first frame: everything spawned at scene load exists by now
        if (mpb == null) mpb = new MaterialPropertyBlock();

        foreach (var e in entries) e.blocking = false;
        foreach (var v in views)
        {
            if (!v.cam || !v.player || !v.cam.isActiveAndEnabled) continue;
            Vector3 from = v.cam.transform.position;
            Vector3 to = v.player.position + Vector3.up * 1.2f;
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) continue;
            var ray = new Ray(from, d / len);
            foreach (var e in entries)
            {
                if (!e.r) continue;
                if (e.water)
                {
                    // camera in or right next to water (live bounds: the river grows as it fills)
                    var wb = e.r.bounds;
                    if (wb.size.sqrMagnitude > 0.0001f && (wb.ClosestPoint(from) - from).sqrMagnitude < WaterDistance * WaterDistance)
                        e.blocking = true;
                    continue;
                }
                var b = e.bounds;
                b.Expand(Padding * 2f);
                if (b.Contains(from) || b.IntersectRay(ray, out float hit) && hit < len - 0.3f) e.blocking = true;
            }
        }

        float step = FadeSpeed * Time.deltaTime;
        foreach (var e in entries)
        {
            if (!e.r) continue;
            float target = e.blocking ? FadedAlpha : 1f;
            if (Mathf.Approximately(e.alpha, target)) continue;
            e.alpha = Mathf.MoveTowards(e.alpha, target, step);
            Apply(e);
        }
    }

    void Apply(Entry e)
    {
        if (e.alpha >= 1f) { Restore(e); return; }
        if (e.r is ParticleSystemRenderer) { e.r.enabled = e.alpha > 0.55f; return; } // mist and spray just switch off
        if (e.noFade || e.faded == null && (e.noFade = !MakeFaded(e)))
        {
            // shader that can't fade: hide the mesh while it blocks, keep its shadow
            e.r.shadowCastingMode = e.alpha < 0.6f ? ShadowCastingMode.ShadowsOnly : e.shadow;
            return;
        }
        if (!e.usingFaded) { e.r.sharedMaterials = e.faded; e.usingFaded = true; }
        for (int i = 0; i < e.faded.Length; i++)
        {
            e.r.GetPropertyBlock(mpb, i);
            var c = e.baseColors[i];
            c.a *= e.alpha;
            mpb.SetColor("_BaseColor", c);
            e.r.SetPropertyBlock(mpb, i);
        }
    }

    bool MakeFaded(Entry e)
    {
        var src = e.original;
        var faded = new Material[src.Length];
        var colors = new Color[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            var m = src[i];
            if (!m || !m.HasProperty("_Surface") || !m.HasProperty("_BaseColor")) { e.faded = null; return false; }
            if (!fadedCopies.TryGetValue(m, out var f) || !f)
            {
                f = new Material(m) { name = m.name + " (see-through)" };
                f.SetFloat("_Surface", 1f);
                f.SetFloat("_Blend", 0f);
                f.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                f.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (f.HasProperty("_SrcBlendAlpha")) f.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                if (f.HasProperty("_DstBlendAlpha")) f.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                f.SetFloat("_ZWrite", 0f);
                f.SetOverrideTag("RenderType", "Transparent");
                f.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                f.DisableKeyword("_ALPHATEST_ON");
                f.renderQueue = (int)RenderQueue.Transparent;
                fadedCopies[m] = f;
            }
            faded[i] = f;
            colors[i] = m.GetColor("_BaseColor");
        }
        e.faded = faded;
        e.baseColors = colors;
        return true;
    }

    void Restore(Entry e)
    {
        if (!e.r) return;
        e.alpha = 1f;
        if (e.r is ParticleSystemRenderer) e.r.enabled = true;
        if (e.usingFaded)
        {
            e.r.sharedMaterials = e.original;
            e.usingFaded = false;
            for (int i = 0; i < e.original.Length; i++) e.r.SetPropertyBlock(null, i);
        }
        e.r.shadowCastingMode = e.shadow;
    }

    void RestoreAll()
    {
        foreach (var e in entries) if (e.r && e.alpha < 1f) Restore(e);
    }

    void OnDisable() => RestoreAll();
}
