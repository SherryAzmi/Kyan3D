using UnityEngine;

/// <summary>
/// The desert (Present) camera's own sky, one per level:
///   level 1 = night (stars and a pale moon), level 2 = sunrise, level 3 = clear morning day.
/// The sky is painted into a small panoramic texture at load time and shown with the
/// Resources/DesertSky material (Skybox/Panoramic). Only the left camera gets it; the forest keeps its sky.
/// Called by DesertMood when it installs.
/// </summary>
public static class DesertSky
{
    const int W = 1024, H = 512;
    // Panoramic mapping: u = 0.25 is straight ahead (+Z, where both players face at spawn), u = 0.5 is +X.

    public static void Apply(Camera cam, int level)
    {
        if (!cam) return;
        var baseMat = Resources.Load<Material>("DesertSky");
        if (!baseMat) { Debug.LogWarning("[DesertSky] Resources/DesertSky.mat missing; desert sky unchanged."); return; }

        var mat = new Material(baseMat) { name = "Desert Sky (level " + level + ")" };
        mat.SetTexture("_MainTex", Paint(level));
        if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", 1f);

        var sky = cam.GetComponent<Skybox>();
        if (!sky) sky = cam.gameObject.AddComponent<Skybox>();
        sky.material = mat;
        cam.clearFlags = CameraClearFlags.Skybox;
    }

    // ---------------------------------------------------------------- painting

    struct Stop { public float at; public Color c; public Stop(float a, Color col) { at = a; c = col; } }

    static Color Grad(Stop[] stops, float x)
    {
        if (x <= stops[0].at) return stops[0].c;
        for (int i = 1; i < stops.Length; i++)
            if (x <= stops[i].at)
                return Color.Lerp(stops[i - 1].c, stops[i].c, (x - stops[i - 1].at) / (stops[i].at - stops[i - 1].at));
        return stops[stops.Length - 1].c;
    }

    static float Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return (h ^ (h >> 16)) / (float)uint.MaxValue;
    }

    static Texture2D Paint(int level)
    {
        Stop[] sky;
        float bodyU, bodyElev, bodyRadius, glowSize, glowAmount;
        Color bodyColor, glowColor;
        bool stars;

        switch (level)
        {
            case 1: // night
                sky = new[] { new Stop(0f, new Color(0.14f, 0.16f, 0.28f)), new Stop(0.25f, new Color(0.06f, 0.07f, 0.16f)), new Stop(1f, new Color(0.015f, 0.02f, 0.06f)) };
                bodyU = 0.235f; bodyElev = 3.6f; bodyRadius = 0.045f; glowSize = 0.16f; glowAmount = 0.35f;
                bodyColor = new Color(0.93f, 0.92f, 0.85f); glowColor = new Color(0.55f, 0.6f, 0.8f);
                stars = true;
                break;
            case 2: // sunrise
                sky = new[] { new Stop(0f, new Color(1f, 0.6f, 0.3f)), new Stop(0.07f, new Color(0.98f, 0.72f, 0.45f)), new Stop(0.22f, new Color(0.78f, 0.56f, 0.6f)),
                              new Stop(0.55f, new Color(0.38f, 0.4f, 0.64f)), new Stop(1f, new Color(0.17f, 0.22f, 0.46f)) };
                bodyU = 0.26f; bodyElev = 2.2f; bodyRadius = 0.05f; glowSize = 0.3f; glowAmount = 0.7f;
                bodyColor = new Color(1f, 0.94f, 0.78f); glowColor = new Color(1f, 0.7f, 0.4f);
                stars = false;
                break;
            default: // clear morning day
                sky = new[] { new Stop(0f, new Color(0.86f, 0.89f, 0.9f)), new Stop(0.15f, new Color(0.7f, 0.81f, 0.92f)), new Stop(1f, new Color(0.34f, 0.57f, 0.86f)) };
                bodyU = 0.24f; bodyElev = 4f; bodyRadius = 0.04f; glowSize = 0.2f; glowAmount = 0.35f;
                bodyColor = new Color(1f, 0.98f, 0.9f); glowColor = new Color(1f, 0.95f, 0.8f);
                stars = false;
                break;
        }

        float bodyE = bodyElev * Mathf.Deg2Rad;
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            float e = ((y + 0.5f) / H - 0.5f) * Mathf.PI;          // elevation, -pi/2 .. pi/2
            float up = Mathf.Clamp01(e / (Mathf.PI * 0.5f));        // 0 horizon .. 1 zenith
            Color row = Grad(sky, Mathf.Sqrt(up));
            if (e < 0f) row = Grad(sky, 0f) * 0.55f;                // below the horizon (mostly hidden by the ground)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W;
                float du = Mathf.DeltaAngle(u * 360f, bodyU * 360f) * Mathf.Deg2Rad;
                float dx = du * Mathf.Cos(e), dy = e - bodyE;
                float d = Mathf.Sqrt(dx * dx + dy * dy);            // angular distance to the sun / moon

                Color c = row;
                c += glowColor * (glowAmount * Mathf.Exp(-d / glowSize));
                if (level == 2 && e >= 0f)                           // warm band along the horizon, strongest under the sun
                    c += glowColor * (0.35f * Mathf.Exp(-e / 0.08f) * Mathf.Exp(-Mathf.Abs(du) / 0.9f));
                if (d < bodyRadius) c = Color.Lerp(c, bodyColor, Mathf.Clamp01((bodyRadius - d) / (bodyRadius * 0.25f)));

                if (stars && e > 0.06f)
                {
                    float h = Hash(x, y);
                    if (h < 0.0022f) c += Color.white * (0.45f + 0.55f * Hash(y, x)) * Mathf.Clamp01(up * 3f);
                }
                c.a = 1f;
                px[y * W + x] = c;
            }
        }

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "Desert Sky Texture", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }
}
