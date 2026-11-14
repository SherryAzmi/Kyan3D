using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Grey, cold shade over the desert (left) half of the split screen. It is heaviest at the far left
/// edge and fades out toward the mirror crack, so the light escaping from the crack seems to bring
/// colour back to the Present's world. Created by DesertMood; the values change per level.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class DesertShade : MaskableGraphic
{
    [Tooltip("Shade colour at full strength (dark, cold grey).")]
    public Color shade = new Color(0.10f, 0.11f, 0.14f, 1f);
    [Range(0f, 1f)] public float strength = 0.7f;
    [Tooltip("Width (canvas units, 1920-wide reference) of the colourful zone beside the crack.")]
    public float colourZone = 260f;
    [Tooltip("Extra darkening at the top and bottom of the desert half.")]
    [Range(0f, 1f)] public float topBottom = 0.35f;

    [Tooltip("How far (canvas units) the edge of the shade wanders left and right.")]
    public float wiggle = 70f;
    [Tooltip("How fast the waves along the edge travel. 0 = still.")]
    public float drift = 0.45f;
    [Tooltip("Seconds for one slow breath of the shade (in and out).")]
    public float breathPeriod = 5.5f;
    [Tooltip("How far (canvas units) the edge pushes toward the crack and back on each breath.")]
    public float breathReach = 90f;
    [Tooltip("How far (canvas units) the light pushes the grey back when the crack flares.")]
    public float lightPush = 320f;

    void Update() => SetVerticesDirty(); // always moving

    // Smooth, organic offset of the shade's edge at height y (canvas units).
    float Edge(float y, float t)
    {
        return wiggle * (0.55f * Mathf.Sin(y * 0.0105f + t * 1.0f + 0.7f)
                       + 0.30f * Mathf.Sin(y * 0.027f - t * 1.7f + 2.1f)
                       + 0.15f * Mathf.Sin(y * 0.061f + t * 2.3f + 4.4f));
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float left = r.xMin, cx = r.center.x;
        float now = Application.isPlaying ? Time.unscaledTime : 0f;
        float t = now * drift;
        float breath = Mathf.Sin(now * 2f * Mathf.PI / Mathf.Max(0.5f, breathPeriod));     // -1..1
        float glow = MirrorCrackDivider.Glow;                                                  // 0..1 when the crack flares
        float zone = colourZone + breathReach * breath + lightPush * glow;                     // colourful zone breathes
        float wig = wiggle * (1f + 0.3f * Mathf.Sin(now * 1.3f));                              // waves grow and shrink
        float alphaMul = (1f - 0.1f * breath) * (1f - 0.35f * glow);

        const int rows = 64;     // vertical resolution of the wavy edge
        const int steps = 10;    // horizontal steps in the fade, eased so no hard band shows
        float maxEnd = cx - 18f; // never shade the crack itself

        for (int j = 0; j < rows; j++)
        {
            float y0 = Mathf.Lerp(r.yMin, r.yMax, j / (float)rows);
            float y1 = Mathf.Lerp(r.yMin, r.yMax, (j + 1) / (float)rows);
            float e0 = Edge(y0, t) * wig / wiggle, e1 = Edge(y1, t) * wig / wiggle;
            // fade region for each row: wanders with the edge, and gets softer (wider) where it wanders right
            float end0 = Mathf.Min(maxEnd, maxEnd - wig + e0 * 0.6f), end1 = Mathf.Min(maxEnd, maxEnd - wig + e1 * 0.6f);
            float st0 = Mathf.Max(left, end0 - zone + e0), st1 = Mathf.Max(left, end1 - zone + e1);

            // full shade from the left edge to where the fade begins
            Color full = Shade(alphaMul, y0, r), fullTop = Shade(alphaMul, y1, r);
            Quad(vh, new Vector2(left, y0), new Vector2(st0, y0), new Vector2(st1, y1), new Vector2(left, y1), full, full, fullTop, fullTop);

            // eased fade toward the crack
            for (int k = 0; k < steps; k++)
            {
                float a = k / (float)steps, b = (k + 1) / (float)steps;
                float xa0 = Mathf.Lerp(st0, end0, a), xb0 = Mathf.Lerp(st0, end0, b);
                float xa1 = Mathf.Lerp(st1, end1, a), xb1 = Mathf.Lerp(st1, end1, b);
                float fa = Ease(a) * alphaMul, fb = Ease(b) * alphaMul;
                Quad(vh, new Vector2(xa0, y0), new Vector2(xb0, y0), new Vector2(xb1, y1), new Vector2(xa1, y1),
                     Shade(fa, y0, r), Shade(fb, y0, r), Shade(fb, y1, r), Shade(fa, y1, r));
            }
        }
    }

    // 1 at the start of the fade, 0 at the crack, with soft shoulders (smootherstep).
    static float Ease(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - x * x * x * (x * (x * 6f - 15f) + 10f);
    }

    // Shade colour at a given strength, a little heavier toward the top and bottom (soft vignette).
    Color Shade(float k, float y, Rect r)
    {
        float v = Mathf.Abs((y - r.center.y) / (r.height * 0.5f));     // 0 middle .. 1 edges
        float edge = Mathf.Clamp01((v - 0.55f) / 0.45f);
        float a = strength * k * (1f + topBottom * edge * edge);
        return new Color(shade.r, shade.g, shade.b, Mathf.Clamp01(a));
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
}
