using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the tileable ground textures (no downloads): sand albedo + normal (grains, speckles,
/// wind ripples), forest-floor albedo + normal (blade strokes, colour patches, dirt), a large-scale
/// variation map that breaks up tiling, and two grass-blade cards (green and dry) with transparency.
/// Written as PNGs to Assets/Textures/Ground. Deterministic.
/// </summary>
public static class GroundTextures
{
    public const string Dir = "Assets/Textures/Ground";

    [MenuItem("Tools/Kyan/Generate Ground Textures")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(Dir);
        Sand(1024);
        Forest(1024);
        Macro(256);
        GrassCard("grass_card", 512, false);
        GrassCard("grass_card_dry", 512, true);
        AssetDatabase.Refresh();
        Import("sand_albedo", false, true);
        Import("sand_normal", true, true);
        Import("forest_albedo", false, true);
        Import("forest_normal", true, true);
        Import("macro_variation", false, true);
        Import("grass_card", false, false);
        Import("grass_card_dry", false, false);
        Debug.Log($"[GroundTextures] Written to {Dir}.");
    }

    public static Texture2D Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/{name}.png");

    // ================================================================== sand

    static void Sand(int n)
    {
        var col = new Color[n * n];
        var h = new float[n * n];
        Color dark = new Color(0.74f, 0.64f, 0.5f), light = new Color(0.9f, 0.83f, 0.7f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                float f = Fbm(u, v, 4, 5, 1);
                float warp = Fbm(u, v, 3, 3, 7) - 0.5f;
                float rip = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (v * 12f + warp * 3.2f + u * 1f));
                rip = Mathf.Pow(rip, 1.6f);                       // sharper crests like wind ripples
                float g = Hash(x, y, 9);
                var c = Color.Lerp(dark, light, Mathf.Clamp01(f * 0.75f + rip * 0.22f));
                if (g < 0.035f) c *= 0.72f;                         // dark grains
                else if (g > 0.975f) c = Color.Lerp(c, Color.white, 0.45f); // bright quartz grains
                c *= 0.96f + 0.08f * Hash(x, y, 3);                 // fine grain noise
                c.a = 1f;
                col[y * n + x] = c;
                h[y * n + x] = rip * 0.55f + f * 0.3f + g * 0.15f;
            }
        Save("sand_albedo", col, n);
        Save("sand_normal", NormalFrom(h, n, 5f), n);
    }

    // ================================================================== forest floor

    static void Forest(int n)
    {
        var col = new Color[n * n];
        var h = new float[n * n];
        Color g1 = new Color(0.2f, 0.4f, 0.13f), g2 = new Color(0.36f, 0.58f, 0.2f), dry = new Color(0.55f, 0.55f, 0.24f), dirt = new Color(0.33f, 0.25f, 0.15f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                float f = Fbm(u, v, 4, 5, 21);
                float d = Fbm(u, v, 3, 4, 33);
                var c = Color.Lerp(g1, g2, f);
                c = Color.Lerp(c, dry, Mathf.Clamp01((Fbm(u, v, 5, 3, 44) - 0.62f) * 3f) * 0.5f);
                float dirtK = Mathf.Clamp01((d - 0.7f) * 5f);
                c = Color.Lerp(c, dirt, dirtK * 0.8f);
                c *= 0.9f + 0.2f * Hash(x, y, 5);
                col[y * n + x] = c;
                h[y * n + x] = f * 0.4f - dirtK * 0.2f;
            }

        // thousands of short blade strokes seen from above
        var rnd = new System.Random(77);
        for (int k = 0; k < 26000; k++)
        {
            float bx = (float)rnd.NextDouble() * n, by = (float)rnd.NextDouble() * n;
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
            int len = 5 + rnd.Next(10);
            float shade = 0.75f + (float)rnd.NextDouble() * 0.55f;
            Color bc = Color.Lerp(g1, g2, (float)rnd.NextDouble()) * shade;
            for (int s = 0; s < len; s++)
            {
                int px = Wrap(Mathf.RoundToInt(bx + Mathf.Cos(ang) * s), n), py = Wrap(Mathf.RoundToInt(by + Mathf.Sin(ang) * s), n);
                int i = py * n + px;
                float t = s / (float)len;
                col[i] = Color.Lerp(col[i], Color.Lerp(bc * 0.8f, bc * 1.15f, t), 0.75f);
                h[i] += 0.35f * (1f - t * 0.5f);
            }
        }
        for (int i = 0; i < col.Length; i++) col[i].a = 1f;
        Save("forest_albedo", col, n);
        Save("forest_normal", NormalFrom(h, n, 3f), n);
    }

    // ================================================================== large-scale variation

    static void Macro(int n)
    {
        var col = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float f = Fbm(x / (float)n, y / (float)n, 3, 4, 55);
                float g = 0.5f + (f - 0.5f) * 0.35f;  // around 0.5 = no change (detail map is x2)
                col[y * n + x] = new Color(g, g, g, 1f);
            }
        Save("macro_variation", col, n);
    }

    // ================================================================== grass blades (cards)

    static void GrassCard(string name, int n, bool dry)
    {
        var col = new Color[n * n];
        var rnd = new System.Random(dry ? 91 : 19);
        for (int b = 0; b < (dry ? 22 : 34); b++)
        {
            float bx = n * (0.12f + 0.76f * (float)rnd.NextDouble());
            float bh = n * (0.45f + 0.53f * (float)rnd.NextDouble());
            float bw = n * (0.018f + 0.014f * (float)rnd.NextDouble());
            float bend = n * ((float)rnd.NextDouble() - 0.5f) * 0.25f;
            Color baseC, tipC;
            if (dry)
            {
                baseC = Color.Lerp(new Color(0.42f, 0.34f, 0.2f), new Color(0.55f, 0.46f, 0.28f), (float)rnd.NextDouble());
                tipC = Color.Lerp(new Color(0.78f, 0.7f, 0.48f), new Color(0.85f, 0.78f, 0.55f), (float)rnd.NextDouble());
            }
            else
            {
                baseC = Color.Lerp(new Color(0.08f, 0.22f, 0.06f), new Color(0.14f, 0.3f, 0.08f), (float)rnd.NextDouble());
                tipC = Color.Lerp(new Color(0.42f, 0.66f, 0.22f), new Color(0.6f, 0.75f, 0.3f), (float)rnd.NextDouble());
            }
            for (int yy = 0; yy < (int)bh && yy < n; yy++)
            {
                float t = yy / bh;
                float cx = bx + bend * t * t;
                float w = bw * (1f - t * 0.92f);
                for (int xx = Mathf.FloorToInt(cx - w); xx <= Mathf.CeilToInt(cx + w); xx++)
                {
                    if (xx < 0 || xx >= n) continue;
                    float edge = 1f - Mathf.Abs(xx - cx) / Mathf.Max(w, 0.5f);
                    if (edge <= 0f) continue;
                    var c = Color.Lerp(baseC, tipC, t) * (0.85f + 0.25f * edge);
                    c.a = 1f;
                    col[yy * n + xx] = c;
                }
            }
        }
        // bleed colour into transparent pixels so mipmaps don't get dark halos
        for (int i = 0; i < col.Length; i++)
            if (col[i].a == 0f) col[i] = dry ? new Color(0.6f, 0.52f, 0.33f, 0f) : new Color(0.25f, 0.45f, 0.15f, 0f);
        Save(name, col, n);
    }

    // ================================================================== helpers

    static Color[] NormalFrom(float[] h, int n, float strength)
    {
        var o = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = h[y * n + Wrap(x + 1, n)] - h[y * n + Wrap(x - 1, n)];
                float dy = h[Wrap(y + 1, n) * n + x] - h[Wrap(y - 1, n) * n + x];
                var nv = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                o[y * n + x] = new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, nv.z * 0.5f + 0.5f, 1f);
            }
        return o;
    }

    static int Wrap(int i, int n) => ((i % n) + n) % n;

    static float Hash(int x, int y, int s)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xffffff) / 16777215f;
        }
    }

    /// <summary>Tileable value noise: lattice of 'period' cells across the 0..1 square.</summary>
    static float VNoise(float u, float v, int period, int s)
    {
        float x = u * period, y = v * period;
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int x0 = Wrap(xi, period), x1 = Wrap(xi + 1, period), y0 = Wrap(yi, period), y1 = Wrap(yi + 1, period);
        float a = Hash(x0, y0, s), b = Hash(x1, y0, s), c = Hash(x0, y1, s), d = Hash(x1, y1, s);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float u, float v, int basePeriod, int octaves, int s)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        int p = basePeriod;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * VNoise(u, v, p, s + o * 31);
            norm += amp;
            amp *= 0.5f;
            p *= 2;
        }
        return sum / norm;
    }

    static void Save(string name, Color[] px, int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes($"{Dir}/{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void Import(string name, bool normal, bool repeat)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath($"{Dir}/{name}.png");
        if (!imp) return;
        imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        imp.sRGBTexture = !normal && name != "macro_variation"; // the variation map is data (0.5 = unchanged), not a colour
        imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.mipmapEnabled = true;
        imp.anisoLevel = 8;
        imp.alphaIsTransparency = !repeat;
        imp.alphaSource = repeat ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
        imp.SaveAndReimport();
    }
}
