using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The split-screen divider drawn as a crack in a mirror, with light escaping through it.
/// - Each level lets more light through (SplitCamera = level 1, Puzzle2 = level 2, Puzzle3 = level 3).
/// - Pulse() makes the light flare when the two players affect each other's world
///   (river ripples and transfers, the bridge growing, the gate opening, the mirror halves...).
///
/// Installs itself: in any scene that has a "SplitScreenUI" canvas with a "Divider" image, the plain
/// black line is hidden and this crack is drawn in its place. No scene or builder changes needed.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class MirrorCrackDivider : MaskableGraphic
{
    static MirrorCrackDivider current;

    [Header("Light through the crack")]
    [Range(0f, 1f)] public float levelLight = 0.3f;
    public Color lightColor = new Color(1f, 0.82f, 0.45f, 1f);
    public Color glassColor = new Color(0.04f, 0.04f, 0.06f, 0.95f);
    [Tooltip("How long a cooperation pulse takes to fade (seconds).")]
    public float pulseFade = 1.4f;
    public int seed = 7;

    float pulse;      // 0..1+, decays
    float boost;      // held boost (e.g. while both hold the mirror), set every frame
    float boostUntil;

    // crack shape, in 0..1 of the rect height (y) and pixels of horizontal offset (x)
    Vector2[] trunk;
    struct Branch { public int at; public float dir, len, angle; }
    Branch[] branches;
    int[] rayPoints;

    // ---------------------------------------------------------------- public API

    /// <summary>Flare the light in the crack. 1 = a normal cross-world action.</summary>
    public static void Pulse(float strength = 1f)
    {
        if (current) current.pulse = Mathf.Max(current.pulse, Mathf.Clamp(strength, 0f, 2f));
    }

    /// <summary>How strongly the crack is flaring right now, 0..1 (pulses and held boosts, not the level's base light).</summary>
    public static float Glow => current ? Mathf.Clamp01(current.pulse * 0.6f + current.boost * 0.5f) : 0f;

    /// <summary>Keep extra light in the crack while something is held (call every frame), 0..1.</summary>
    public static void Hold(float amount)
    {
        if (!current) return;
        current.boost = Mathf.Clamp01(amount);
        current.boostUntil = Time.unscaledTime + 0.15f;
    }

    // ---------------------------------------------------------------- auto install

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Install(s);

    static void Install(Scene s)
    {
        var ui = GameObject.Find("SplitScreenUI");
        if (!ui || ui.scene != s) return;
        if (ui.GetComponentInChildren<MirrorCrackDivider>(true)) return;

        var old = ui.transform.Find("Divider");
        if (old && old.TryGetComponent<Graphic>(out var g)) g.enabled = false;

        var go = new GameObject("Mirror Crack", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(ui.transform, false);
        go.transform.SetSiblingIndex(old ? old.GetSiblingIndex() + 1 : 0);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var crack = go.AddComponent<MirrorCrackDivider>();
        crack.raycastTarget = false;
        crack.levelLight = LightForScene(s.name);
    }

    static float LightForScene(string name)
    {
        switch (name)
        {
            case "Puzzle2": return 0.6f;
            case "Puzzle3": return 0.9f;
            default: return 0.35f; // SplitCamera / level 1
        }
    }

    // ---------------------------------------------------------------- lifecycle

    protected override void OnEnable()
    {
        base.OnEnable();
        current = this;
        BuildShape();
    }

    protected override void OnDisable()
    {
        if (current == this) current = null;
        base.OnDisable();
    }

    void Update()
    {
        pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime / pulseFade);
        if (Time.unscaledTime > boostUntil) boost = Mathf.MoveTowards(boost, 0f, Time.unscaledDeltaTime * 2f);
        SetVerticesDirty(); // the light flickers, so redraw every frame (small mesh)
    }

    void BuildShape()
    {
        var rnd = new System.Random(seed);
        float R() => (float)rnd.NextDouble();

        const int n = 34;
        trunk = new Vector2[n];
        float x = 0f;
        for (int i = 0; i < n; i++)
        {
            float y = 1f - i / (float)(n - 1);
            if (i > 0 && i < n - 1)
            {
                x += (R() - 0.5f) * 26f;
                if (R() < 0.18f) x += (R() < 0.5f ? -1f : 1f) * (10f + R() * 14f); // sharp kink
                x = Mathf.Clamp(x, -26f, 26f);
            }
            trunk[i] = new Vector2(i == 0 || i == n - 1 ? 0f : x, y);
        }

        branches = new Branch[9];
        for (int i = 0; i < branches.Length; i++)
            branches[i] = new Branch
            {
                at = 2 + (int)(R() * (n - 4)),
                dir = i % 2 == 0 ? -1f : 1f,
                len = 30f + R() * 80f,
                angle = 20f + R() * 50f,
            };

        rayPoints = new int[12];
        for (int i = 0; i < rayPoints.Length; i++) rayPoints[i] = 1 + (int)(R() * (n - 2));
    }

    // ---------------------------------------------------------------- mesh

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (trunk == null) BuildShape();
        Rect r = rectTransform.rect;
        float cx = r.center.x;

        float t = Application.isPlaying ? Time.unscaledTime : 0f;
        float flicker = 0.88f + 0.12f * Mathf.Sin(t * 7.3f) * Mathf.Sin(t * 3.1f + 1f);
        float light = Mathf.Clamp01(levelLight + 0.55f * pulse + 0.5f * boost) * flicker;

        Color lit = lightColor;
        Color clear = new Color(lit.r, lit.g, lit.b, 0f);

        // The crack is an OPENING between two jagged glass edges. The left edge always stays left of the
        // screen centre and the right edge always right of it, so the straight seam where the two camera
        // views meet is always hidden inside the light.
        int n = trunk.Length;
        var L = new Vector2[n];
        var R = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float y = r.yMin + trunk[i].y * r.height;
            float hw = 2f + 3f * light;                                  // base half-width of the opening
            float jagL = 2f + ((i * 53 + 11) % 11) * 1.3f;               // each edge gets its own jagged
            float jagR = 2f + ((i * 29 + 5) % 11) * 1.3f;                // offset so neither side runs straight
            L[i] = new Vector2(cx + Mathf.Min(trunk[i].x - hw, 0f) - jagL, y);
            R[i] = new Vector2(cx + Mathf.Max(trunk[i].x + hw, 0f) + jagR, y);
        }

        // 1. soft glow spilling out on both sides
        float glowW = 16f + 90f * light;
        Color glow = new Color(lit.r, lit.g, lit.b, 0.3f + 0.5f * light);
        for (int i = 0; i < n - 1; i++)
        {
            Quad(vh, L[i] + Vector2.left * glowW, L[i], L[i + 1], L[i + 1] + Vector2.left * glowW, clear, glow, glow, clear);
            Quad(vh, R[i], R[i] + Vector2.right * glowW, R[i + 1] + Vector2.right * glowW, R[i + 1], glow, clear, clear, glow);
        }

        // 2. rays of light escaping from points along the crack
        for (int k = 0; k < rayPoints.Length; k++)
        {
            float side = k % 2 == 0 ? -1f : 1f;
            Vector2 o = side < 0 ? L[rayPoints[k]] : R[rayPoints[k]];
            float wobble = 0.75f + 0.25f * Mathf.Sin(t * (1.3f + k * 0.37f) + k);
            float len = (60f + 260f * light) * wobble * (0.6f + 0.4f * ((k * 37) % 10) / 10f);
            float spread = 14f + 30f * light;
            float tilt = ((k * 53) % 7 - 3) * 6f;
            Vector2 tip = o + new Vector2(side * len, tilt);
            Color rayCol = new Color(lit.r, lit.g, lit.b, (0.22f + 0.45f * light) * wobble);
            Tri(vh, o + Vector2.up * 4f, o + Vector2.down * 4f, tip + Vector2.up * spread, rayCol, rayCol, clear);
            Tri(vh, o + Vector2.down * 4f, tip + Vector2.down * spread, tip + Vector2.up * spread, rayCol, clear, clear);
        }

        // 3. the opening, fully opaque: golden at the edges, near-white in the middle
        Color edgeLit = new Color(lit.r, lit.g, lit.b, 1f);
        Color hot = new Color(1f, 0.98f, 0.9f, 1f);
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 m0 = (L[i] + R[i]) * 0.5f, m1 = (L[i + 1] + R[i + 1]) * 0.5f;
            Quad(vh, L[i], m0, m1, L[i + 1], edgeLit, hot, hot, edgeLit);
            Quad(vh, m0, R[i], R[i + 1], m1, hot, edgeLit, edgeLit, hot);
        }

        // 4. dark glass edges on both sides, and small side cracks
        for (int i = 0; i < n - 1; i++)
        {
            Line(vh, L[i], L[i + 1], 3f, glassColor);
            Line(vh, R[i], R[i + 1], 3f, glassColor);
        }
        foreach (var br in branches)
        {
            Vector2 o = br.dir < 0 ? L[br.at] : R[br.at];
            float ang = br.angle * Mathf.Deg2Rad;
            Vector2 mid = o + new Vector2(br.dir * Mathf.Cos(ang) * br.len * 0.55f, Mathf.Sin(ang) * br.len * 0.3f);
            Vector2 end = mid + new Vector2(br.dir * br.len * 0.45f, -Mathf.Sin(ang) * br.len * 0.25f);
            Line(vh, o, mid, 3.5f, glassColor);
            Line(vh, mid, end, 2.5f, glassColor);
            Color thin = new Color(lit.r, lit.g, lit.b, 0.4f + 0.6f * light);
            Line(vh, o, mid, 1.2f, thin);
            Line(vh, mid, end, 0.9f, thin);
        }
    }

    static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
    {
        Vector2 d = (b - a).normalized;
        Vector2 n = new Vector2(-d.y, d.x) * (width * 0.5f);
        Quad(vh, a - n, a + n, b + n, b - n, c, c, c, c);
    }

    static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, ca, Vector4.zero);
        vh.AddVert(b, cb, Vector4.zero);
        vh.AddVert(c, cc, Vector4.zero);
        vh.AddVert(d, cd, Vector4.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }

    static void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, ca, Vector4.zero);
        vh.AddVert(b, cb, Vector4.zero);
        vh.AddVert(c, cc, Vector4.zero);
        vh.AddTriangle(i, i + 1, i + 2);
    }
}
