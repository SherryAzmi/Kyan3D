using UnityEngine;

/// <summary>
/// A soft looping theme for the main menu, generated in code in the same plucked style as the UI sounds:
/// a slow chord loop (Am, F, C, G) with a gentle arpeggio, a warm pad, a low bass note and a quiet melody.
/// MenuMusic.Create() returns a seamless 16 second clip. Volume is the player's Music setting.
/// </summary>
public static class MenuMusic
{
    const int Rate = 32000;
    const float Bar = 4f;          // seconds per bar
    const int Bars = 4;

    static readonly float[][] Chords =
    {
        new[] { 220.00f, 261.63f, 329.63f, 440.00f },  // Am
        new[] { 220.00f, 261.63f, 349.23f, 440.00f },  // F (A C F)
        new[] { 261.63f, 329.63f, 392.00f, 523.25f },  // C
        new[] { 246.94f, 293.66f, 392.00f, 493.88f },  // G (B D G)
    };
    static readonly float[] Bass = { 110.00f, 87.31f, 130.81f, 98.00f };
    // a quiet melody: (bar, beat in eighths, frequency, length)
    static readonly float[][] Melody =
    {
        new[] { 0f, 2f, 659.25f }, new[] { 0f, 5f, 587.33f },
        new[] { 1f, 1f, 523.25f }, new[] { 1f, 4f, 659.25f },
        new[] { 2f, 2f, 783.99f }, new[] { 2f, 6f, 659.25f },
        new[] { 3f, 0f, 587.33f }, new[] { 3f, 3f, 493.88f }, new[] { 3f, 6f, 587.33f },
    };

    public static AudioClip Create()
    {
        int loop = Mathf.RoundToInt(Bar * Bars * Rate);
        var buf = new float[loop * 2]; // two passes: the second one holds the tails of the first, so it loops cleanly

        for (int pass = 0; pass < 2; pass++)
            for (int bar = 0; bar < Bars; bar++)
            {
                float t0 = pass * Bar * Bars + bar * Bar;
                var ch = Chords[bar];

                // pad: the chord as slow sines
                foreach (float f in ch) AddPad(buf, t0, Bar, f * 0.5f, 0.045f);

                // bass: one low note at the start of the bar and one at the third beat
                AddPluck(buf, t0, Bass[bar], 2.2f, 0.30f);
                AddPluck(buf, t0 + 2f, Bass[bar] * 1.5f, 2.6f, 0.16f);

                // arpeggio in eighth notes
                int[] pattern = { 0, 1, 2, 3, 2, 1, 2, 1 };
                for (int i = 0; i < 8; i++)
                    AddPluck(buf, t0 + i * 0.5f, ch[pattern[i]], 4.2f, i % 4 == 0 ? 0.20f : 0.14f);

                // a few melody notes
                foreach (var m in Melody)
                    if ((int)m[0] == bar) AddPluck(buf, t0 + m[1] * 0.5f, m[2], 2.4f, 0.17f);
            }

        // a soft echo for space
        int d1 = Mathf.RoundToInt(0.375f * Rate), d2 = Mathf.RoundToInt(0.75f * Rate);
        for (int i = buf.Length - 1; i >= d2; i--) buf[i] += buf[i - d1] * 0.28f + buf[i - d2] * 0.16f;

        var data = new float[loop];
        float peak = 0.01f;
        for (int i = 0; i < loop; i++) { data[i] = buf[loop + i]; peak = Mathf.Max(peak, Mathf.Abs(data[i])); }
        float norm = 0.8f / peak;
        for (int i = 0; i < loop; i++) data[i] *= norm;

        var clip = AudioClip.Create("Kayyan Menu Theme", loop, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static void AddPluck(float[] buf, float start, float freq, float decay, float amp)
    {
        int s = Mathf.RoundToInt(start * Rate);
        int len = Mathf.RoundToInt(Mathf.Min(3.2f, 7f / decay) * Rate);
        for (int i = 0; i < len; i++)
        {
            int k = s + i;
            if (k < 0 || k >= buf.Length) continue;
            float t = i / (float)Rate;
            float env = Mathf.Min(1f, t * 400f) * Mathf.Exp(-decay * t);
            float w = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.28f * Mathf.Sin(4f * Mathf.PI * freq * t) + 0.08f * Mathf.Sin(6f * Mathf.PI * freq * t);
            buf[k] += w * env * amp;
        }
    }

    static void AddPad(float[] buf, float start, float length, float freq, float amp)
    {
        int s = Mathf.RoundToInt(start * Rate);
        int len = Mathf.RoundToInt((length + 1.2f) * Rate);
        for (int i = 0; i < len; i++)
        {
            int k = s + i;
            if (k >= buf.Length) break;
            float t = i / (float)Rate;
            float env = Mathf.Clamp01(t / 1.2f) * Mathf.Clamp01((length + 1.2f - t) / 1.2f);
            buf[k] += (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * freq * 1.003f * t)) * env * amp;
        }
    }
}
