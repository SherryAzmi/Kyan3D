using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Kyan > Generate Sounds — synthesizes every sound and music loop the game uses and writes
/// them as 16-bit 44.1 kHz mono WAV files into Assets/Audio/Generated. Deterministic (fixed seeds),
/// so running it again gives the same files. Replace any file with a real recording of the same
/// name, or drag a different clip into the GameAudio / MirrorCutscene slots.
/// </summary>
public static class SoundSynth
{
    public const string Dir = "Assets/Audio/Generated";
    const int SR = 44100;
    static System.Random rnd;

    [MenuItem("Tools/Kyan/Generate Sounds")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(Dir);
        rnd = new System.Random(1234);

        // ---- opening cutscene ----
        Save("cs_room_tone", Loop(RoomTone(12f), 1.5f), 0.5f);
        Save("cs_sad_pad", Loop(Reverb(SadPad(16f), 0.35f), 2f), 0.5f);
        Save("cs_whisper", Loop(Whisper(8f), 0.8f), 0.6f);
        Save("cs_heartbeat", Heartbeat(), 0.9f);
        Save("cs_crack", Reverb(Crack(), 0.25f), 0.85f);
        Save("cs_shatter", Reverb(Shatter(), 0.3f), 0.95f);

        // ---- gameplay beds ----
        Save("amb_desert_wind", Loop(DesertWind(14f), 2f), 0.55f);
        Save("amb_forest", Loop(Forest(14f), 2f), 0.5f);
        Save("music_journey", Loop(Reverb(Journey(24f), 0.4f), 2.5f), 0.45f);
        Save("music_ending", Loop(Reverb(Ending(24f), 0.45f), 2.5f), 0.45f);

        // ---- puzzle sfx ----
        Save("sfx_dig_loop", Loop(Dig(1.6f), 0.2f), 0.6f);
        Save("sfx_dig_done", DigDone(), 0.85f);
        Save("sfx_pickup", Reverb(Pickup(), 0.2f), 0.7f);
        Save("sfx_drop", Drop(), 0.7f);
        Save("sfx_splash", Reverb(Splash(), 0.2f), 0.75f);
        Save("sfx_arrive", Reverb(Bells(new[] { 783.99f, 1174.66f }, 0.12f, 1.6f, 3f), 0.3f), 0.7f);
        Save("sfx_place", Reverb(Place(), 0.25f), 0.75f);
        Save("sfx_grow_loop", Loop(Grow(2.4f), 0.3f), 0.6f);
        Save("sfx_bridge_done", Reverb(Bells(new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.14f, 2.6f, 2f), 0.35f), 0.75f);
        Save("sfx_fragment", Reverb(Fragment(), 0.4f), 0.8f);
        Save("sfx_bloom", Reverb(Bloom(), 0.35f), 0.7f);
        Save("sfx_join_loop", Loop(JoinHum(2.4f), 0.4f), 0.55f);
        Save("sfx_mirror_whole", Reverb(Bells(new[] { 261.63f, 329.63f, 392f, 523.25f, 659.25f, 1046.5f }, 0.05f, 5f, 0.9f), 0.45f), 0.9f);
        Save("sfx_click", Click(), 0.6f);
        Save("sfx_water_flow", Loop(WaterFlow(3f), 0.5f), 0.6f);
        Save("sfx_water_step", WaterStep(), 0.7f);

        AssetDatabase.Refresh();
        Debug.Log($"[SoundSynth] Sounds written to {Dir}.");
    }

    // ================================================================== recipes

    static float[] RoomTone(float sec)
    {
        var s = New(sec);
        var brown = Brown(s.Length, 0.02f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float lfo = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.13f * t);
            s[i] = lfo * (0.3f * Sin(55f, t) + 0.15f * Sin(82.4f, t) + 0.08f * Sin(110.3f, t)) + 0.5f * brown[i];
        }
        return s;
    }

    static float[] SadPad(float sec)
    {
        // A minor, slow breathing swell
        return Pad(sec, new[] { 110f, 130.81f, 164.81f, 220f, 329.63f }, new[] { 1f, 0.8f, 0.7f, 0.5f, 0.2f }, 0.07f);
    }

    static float[] Journey(float sec)
    {
        // Soft C add9 pad with a sparse pentatonic bell melody: melancholic but hopeful.
        var s = Pad(sec, new[] { 130.81f, 196f, 293.66f, 329.63f }, new[] { 1f, 0.7f, 0.45f, 0.35f }, 0.05f);
        float[] notes = { 523.25f, 587.33f, 659.25f, 783.99f, 880f, 783.99f, 659.25f, 587.33f };
        for (int n = 0; n < 9; n++)
        {
            float start = 1f + n * 2.6f;
            if (start > sec - 2f) break;
            AddBell(s, start, notes[n % notes.Length], 0.12f, 2.2f);
        }
        return s;
    }

    static float[] Ending(float sec)
    {
        // F - C - Am - G, warm and resolved, with a slow bell line.
        var s = New(sec);
        float[][] chords =
        {
            new[] { 174.61f, 220f, 261.63f, 349.23f },
            new[] { 130.81f, 196f, 261.63f, 329.63f },
            new[] { 110f, 220f, 261.63f, 329.63f },
            new[] { 98f, 196f, 246.94f, 293.66f }
        };
        float seg = sec / chords.Length;
        for (int c = 0; c < chords.Length; c++)
        {
            var part = Pad(seg + 2f, chords[c], new[] { 1f, 0.7f, 0.6f, 0.4f }, 0f);
            int off = (int)(c * seg * SR);
            for (int i = 0; i < part.Length; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Clamp01(t / 1.5f) * Mathf.Clamp01((seg + 2f - t) / 2f);
                int j = (off + i) % s.Length;
                s[j] += part[i] * env;
            }
        }
        float[] mel = { 659.25f, 587.33f, 523.25f, 587.33f, 659.25f, 783.99f, 659.25f, 523.25f };
        for (int n = 0; n < mel.Length; n++) AddBell(s, 0.8f + n * 3f, mel[n], 0.1f, 2.5f);
        return s;
    }

    static float[] Pad(float sec, float[] freqs, float[] amps, float breathe)
    {
        var s = New(sec);
        for (int v = 0; v < freqs.Length; v++)
        {
            float f = freqs[v], a = amps[v];
            float ph = (float)rnd.NextDouble() * 6.28f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SR;
                float swell = 1f - breathe + breathe * Mathf.Sin(2f * Mathf.PI * (0.11f + v * 0.017f) * t + ph);
                // three slightly detuned voices = soft chorus
                float x = Sin(f * 0.997f, t + ph) + Sin(f, t) + Sin(f * 1.003f, t - ph) + 0.25f * Sin(f * 2f, t);
                s[i] += a * swell * x * 0.25f;
            }
        }
        return s;
    }

    static float[] Whisper(float sec)
    {
        var s = New(sec);
        var noise = White(s.Length);
        var band = HighPass(LowPass(noise, 5200f), 1400f);
        float t0 = 0.1f;
        while (t0 < sec - 0.5f)
        {
            int syllables = 2 + rnd.Next(4);
            for (int k = 0; k < syllables; k++)
            {
                float len = 0.07f + (float)rnd.NextDouble() * 0.22f;
                int a = (int)(t0 * SR), b = Mathf.Min(s.Length, (int)((t0 + len) * SR));
                float amp = 0.4f + (float)rnd.NextDouble() * 0.6f;
                for (int i = a; i < b; i++)
                {
                    float u = (i - a) / (float)(b - a);
                    s[i] += band[i] * amp * Mathf.Sin(Mathf.PI * u);
                }
                t0 += len + 0.03f + (float)rnd.NextDouble() * 0.08f;
            }
            t0 += 0.25f + (float)rnd.NextDouble() * 0.6f;
        }
        return s;
    }

    static float[] Heartbeat()
    {
        var s = New(0.9f);
        foreach (var (at, amp) in new[] { (0f, 1f), (0.27f, 0.7f) })
        {
            int a = (int)(at * SR);
            float ph = 0f;
            for (int i = a; i < s.Length; i++)
            {
                float t = (i - a) / (float)SR;
                float f = Mathf.Lerp(62f, 38f, Mathf.Clamp01(t / 0.15f));
                ph += 2f * Mathf.PI * f / SR;
                s[i] += amp * Mathf.Sin(ph) * Mathf.Exp(-t * 16f);
            }
        }
        return LowPass(s, 400f);
    }

    static float[] Crack()
    {
        var s = New(1.4f);
        var noise = HighPass(White(s.Length), 1800f);
        float t = 0f;
        for (int k = 0; k < 16; k++)
        {
            t += 0.02f + (float)rnd.NextDouble() * 0.09f * (1f - k / 16f);
            int a = (int)(t * SR), len = (int)((0.004f + (float)rnd.NextDouble() * 0.02f) * SR);
            float amp = 0.5f + (float)rnd.NextDouble() * 0.5f;
            for (int i = 0; i < len && a + i < s.Length; i++) s[a + i] += noise[a + i] * amp * Mathf.Exp(-i / (float)len * 4f);
        }
        // a low glassy creak under it
        for (int i = 0; i < s.Length; i++)
        {
            float u = i / (float)SR;
            s[i] += 0.12f * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(320f, 210f, u / 1.4f) * u) * Mathf.Exp(-u * 2f);
        }
        return s;
    }

    static float[] Shatter()
    {
        var s = New(2.8f);
        var burst = HighPass(White(s.Length), 900f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            s[i] = burst[i] * Mathf.Exp(-t * 7f) * 0.9f + 0.6f * Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 6f);
        }
        for (int k = 0; k < 80; k++)
        {
            float at = Mathf.Pow((float)rnd.NextDouble(), 2f) * 2f;
            float f = 2400f + (float)rnd.NextDouble() * 5200f;
            float dec = 18f + (float)rnd.NextDouble() * 30f;
            float amp = 0.08f + (float)rnd.NextDouble() * 0.22f;
            int a = (int)(at * SR);
            for (int i = a; i < s.Length; i++)
            {
                float t = (i - a) / (float)SR;
                float e = Mathf.Exp(-t * dec);
                if (e < 0.001f) break;
                s[i] += amp * e * (Mathf.Sin(2f * Mathf.PI * f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * f * 2.7f * t));
            }
        }
        return s;
    }

    static float[] DesertWind(float sec)
    {
        var s = New(sec);
        var noise = White(s.Length);
        float lp = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float gust = 0.55f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.07f * t) + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.23f * t + 1f);
            float cutoff = 250f + 650f * gust;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SR);
            lp += a * (noise[i] - lp);
            s[i] = lp * gust;
        }
        return s;
    }

    static float[] Forest(float sec)
    {
        var s = New(sec);
        var breeze = LowPass(White(s.Length), 550f);
        var water = HighPass(LowPass(White(s.Length), 3200f), 900f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float flick = 0.5f + 0.5f * Mathf.PerlinNoise(t * 22f, 0.3f);
            s[i] = 0.5f * breeze[i] * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.09f * t)) + 0.12f * water[i] * flick;
        }
        // birds: little groups of rising chirps
        float at = 0.6f;
        while (at < sec - 1f)
        {
            float f0 = 2600f + (float)rnd.NextDouble() * 1800f;
            int n = 2 + rnd.Next(3);
            for (int k = 0; k < n; k++)
            {
                float len = 0.07f + (float)rnd.NextDouble() * 0.08f;
                int a = (int)(at * SR), b = Mathf.Min(s.Length, (int)((at + len) * SR));
                float ph = 0f;
                for (int i = a; i < b; i++)
                {
                    float u = (i - a) / (float)(b - a);
                    float f = f0 * (1f + 0.35f * u) + 120f * Mathf.Sin(u * 40f);
                    ph += 2f * Mathf.PI * f / SR;
                    s[i] += 0.16f * Mathf.Sin(ph) * Mathf.Sin(Mathf.PI * u);
                }
                at += len + 0.05f;
            }
            at += 0.8f + (float)rnd.NextDouble() * 2.2f;
        }
        return s;
    }

    static float[] Dig(float sec)
    {
        var s = New(sec);
        var noise = LowPass(White(s.Length), 1500f);
        int grain = (int)(0.025f * SR);
        float g = 1f;
        for (int i = 0; i < s.Length; i++)
        {
            if (i % grain == 0) g = 0.3f + (float)rnd.NextDouble() * 0.7f;
            float t = i / (float)SR;
            float stroke = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 2.5f * t)), 0.6f);
            s[i] = noise[i] * g * (0.25f + 0.75f * stroke);
        }
        return s;
    }

    static float[] DigDone()
    {
        var s = New(0.7f);
        var puff = LowPass(White(s.Length), 700f);
        float ph = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            ph += 2f * Mathf.PI * Mathf.Lerp(90f, 42f, Mathf.Clamp01(t / 0.3f)) / SR;
            s[i] = 0.9f * Mathf.Sin(ph) * Mathf.Exp(-t * 7f) + 0.5f * puff[i] * Mathf.Exp(-t * 5f);
        }
        return s;
    }

    static float[] Pickup()
    {
        var s = New(0.5f);
        AddBell(s, 0f, 659.25f, 0.5f, 0.25f);
        AddBell(s, 0.08f, 987.77f, 0.5f, 0.3f);
        return s;
    }

    static float[] Drop()
    {
        var s = New(0.35f);
        var n = LowPass(White(s.Length), 900f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            s[i] = 0.8f * Mathf.Sin(2f * Mathf.PI * 120f * t) * Mathf.Exp(-t * 20f) + 0.4f * n[i] * Mathf.Exp(-t * 25f);
        }
        return s;
    }

    static float[] Splash()
    {
        var s = New(1.2f);
        var n = HighPass(LowPass(White(s.Length), 3500f), 450f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            s[i] = n[i] * Mathf.Exp(-t * 5f) * Mathf.Clamp01(t / 0.01f);
        }
        for (int k = 0; k < 16; k++)
        {
            float at = 0.05f + (float)rnd.NextDouble() * 0.8f;
            float f0 = 350f + (float)rnd.NextDouble() * 700f;
            int a = (int)(at * SR), len = (int)(0.06f * SR);
            float ph = 0f;
            for (int i = 0; i < len && a + i < s.Length; i++)
            {
                float u = i / (float)len;
                ph += 2f * Mathf.PI * f0 * (1f + u) / SR;
                s[a + i] += 0.25f * Mathf.Sin(ph) * Mathf.Sin(Mathf.PI * u);
            }
        }
        return s;
    }

    static float[] Place()
    {
        var s = New(1.2f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            s[i] = 0.6f * Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 18f);
        }
        AddBell(s, 0.02f, 523.25f, 0.45f, 0.9f);
        AddBell(s, 0.06f, 783.99f, 0.35f, 0.9f);
        return s;
    }

    static float[] Grow(float sec)
    {
        var s = New(sec);
        var rumble = LowPass(White(s.Length), 180f);
        float ph = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float f = 68f + 6f * Mathf.PerlinNoise(t * 3f, 0.7f);
            ph += 2f * Mathf.PI * f / SR;
            s[i] = 2.2f * rumble[i] + 0.25f * Mathf.Sin(ph);
        }
        // wooden creaks
        for (int k = 0; k < 5; k++)
        {
            float at = (float)rnd.NextDouble() * (sec - 0.4f);
            int a = (int)(at * SR), len = (int)(0.25f * SR);
            float f0 = 180f + (float)rnd.NextDouble() * 160f;
            for (int i = 0; i < len; i++)
            {
                float u = i / (float)len;
                float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f0 * (1f + 0.3f * u) * i / SR));
                s[a + i] += 0.08f * buzz * Mathf.Sin(Mathf.PI * u);
            }
        }
        return LowPass(s, 2500f);
    }

    static float[] Fragment()
    {
        var s = New(3.5f);
        float[] chord = { 1046.5f, 1318.5f, 1568f, 1975.5f };
        for (int k = 0; k < chord.Length; k++) AddBell(s, k * 0.09f, chord[k], 0.35f, 2.8f);
        for (int k = 0; k < 40; k++)
            AddBell(s, 0.2f + (float)rnd.NextDouble() * 1.8f, 2500f + (float)rnd.NextDouble() * 3500f, 0.04f, 0.4f);
        return s;
    }

    static float[] Bloom()
    {
        var s = New(2.8f);
        float ph1 = 0f, ph2 = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float f = Mathf.Lerp(380f, 820f, Mathf.SmoothStep(0f, 1f, t / 2f));
            ph1 += 2f * Mathf.PI * f / SR;
            ph2 += 2f * Mathf.PI * f * 1.5f / SR;
            float env = Mathf.Clamp01(t / 0.8f) * Mathf.Exp(-Mathf.Max(0f, t - 1.4f) * 2.5f);
            s[i] = env * (0.35f * Mathf.Sin(ph1) + 0.15f * Mathf.Sin(ph2));
        }
        for (int k = 0; k < 18; k++)
            AddBell(s, 0.6f + (float)rnd.NextDouble() * 1.6f, 1800f + (float)rnd.NextDouble() * 2500f, 0.05f, 0.5f);
        return s;
    }

    static float[] JoinHum(float sec)
    {
        var s = New(sec);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            s[i] = 0.4f * Sin(220f, t) + 0.25f * Sin(220.6f, t) + 0.2f * Sin(330f, t) + 0.12f * Sin(440.4f, t);
        }
        return s;
    }

    static float[] WaterStep()
    {
        // a foot sloshing into shallow water: soft low thump, a burst of splash, a few bubbles
        var s = New(0.45f);
        var splash = HighPass(LowPass(White(s.Length), 4000f), 600f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float env = Mathf.Clamp01(t / 0.012f) * Mathf.Exp(-t * 11f);
            s[i] = 0.75f * splash[i] * env + 0.35f * Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 25f);
        }
        for (int k = 0; k < 5; k++)
        {
            float at = 0.03f + (float)rnd.NextDouble() * 0.2f;
            float f0 = 600f + (float)rnd.NextDouble() * 900f;
            int a = (int)(at * SR), len = (int)(0.035f * SR);
            float ph = 0f;
            for (int i = 0; i < len && a + i < s.Length; i++)
            {
                float u = i / (float)len;
                ph += 2f * Mathf.PI * f0 * (1f + 0.9f * u) / SR;
                s[a + i] += 0.18f * Mathf.Sin(ph) * Mathf.Sin(Mathf.PI * u);
            }
        }
        return s;
    }

    static float[] WaterFlow(float sec)
    {
        // rushing water: band-limited noise with a gurgling flicker, plus small bubbles
        var s = New(sec);
        var rush = HighPass(LowPass(White(s.Length), 2200f), 250f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            s[i] = rush[i] * (0.6f + 0.4f * Mathf.PerlinNoise(t * 9f, 2.1f));
        }
        for (int k = 0; k < 40; k++)
        {
            float at = (float)rnd.NextDouble() * (sec - 0.1f);
            float f0 = 500f + (float)rnd.NextDouble() * 900f;
            int a = (int)(at * SR), len = (int)(0.04f * SR);
            float ph = 0f;
            for (int i = 0; i < len && a + i < s.Length; i++)
            {
                float u = i / (float)len;
                ph += 2f * Mathf.PI * f0 * (1f + 0.8f * u) / SR;
                s[a + i] += 0.12f * Mathf.Sin(ph) * Mathf.Sin(Mathf.PI * u);
            }
        }
        return s;
    }

    static float[] Click()
    {
        var s = New(0.12f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            s[i] = Mathf.Sin(2f * Mathf.PI * 1250f * t) * Mathf.Exp(-t * 45f);
        }
        return s;
    }

    static float[] Bells(float[] freqs, float stagger, float sec, float decay)
    {
        var s = New(sec);
        for (int k = 0; k < freqs.Length; k++) AddBell(s, k * stagger, freqs[k], 0.4f, 1f / decay * 1.0f * 3f);
        return s;
    }

    // ================================================================== building blocks

    static float[] New(float sec) => new float[Mathf.CeilToInt(sec * SR)];
    static float Sin(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);

    /// <summary>Bell / chime: fundamental + a few slightly inharmonic partials, exponential decay.</summary>
    static void AddBell(float[] s, float at, float f, float amp, float decaySeconds)
    {
        int a = (int)(at * SR);
        float k = 1f / Mathf.Max(0.05f, decaySeconds);
        for (int i = a; i < s.Length; i++)
        {
            float t = (i - a) / (float)SR;
            float e = Mathf.Exp(-t * 3f * k) * Mathf.Clamp01(t / 0.004f);
            if (t > 0.05f && e < 0.0005f) break;
            s[i] += amp * e * (Sin(f, t) + 0.35f * Sin(f * 2.01f, t) * Mathf.Exp(-t * 4f) + 0.15f * Sin(f * 3.03f, t) * Mathf.Exp(-t * 6f));
        }
    }

    static float[] White(int n)
    {
        var s = new float[n];
        for (int i = 0; i < n; i++) s[i] = (float)rnd.NextDouble() * 2f - 1f;
        return s;
    }

    static float[] Brown(int n, float step)
    {
        var s = new float[n];
        float v = 0f;
        for (int i = 0; i < n; i++)
        {
            v += ((float)rnd.NextDouble() * 2f - 1f) * step;
            v *= 0.998f;
            s[i] = v;
        }
        return s;
    }

    static float[] LowPass(float[] x, float cutoff)
    {
        var y = new float[x.Length];
        float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SR), v = 0f;
        for (int i = 0; i < x.Length; i++) { v += a * (x[i] - v); y[i] = v; }
        return y;
    }

    static float[] HighPass(float[] x, float cutoff)
    {
        var low = LowPass(x, cutoff);
        var y = new float[x.Length];
        for (int i = 0; i < x.Length; i++) y[i] = x[i] - low[i];
        return y;
    }

    /// <summary>Small Schroeder reverb (4 combs + 2 all-passes), wet mixed in.</summary>
    static float[] Reverb(float[] x, float wet)
    {
        int[] combD = { 1557, 1617, 1491, 1422 };
        int[] apD = { 225, 556 };
        int tail = SR * 2;
        var y = new float[x.Length + tail];
        var outW = new float[y.Length];
        foreach (int d in combD)
        {
            var buf = new float[d];
            int p = 0;
            float lp = 0f;
            for (int i = 0; i < y.Length; i++)
            {
                float input = i < x.Length ? x[i] : 0f;
                float o = buf[p];
                lp = o * 0.7f + lp * 0.3f;
                buf[p] = input + lp * 0.83f;
                p = (p + 1) % d;
                outW[i] += o * 0.25f;
            }
        }
        foreach (int d in apD)
        {
            var buf = new float[d];
            int p = 0;
            for (int i = 0; i < y.Length; i++)
            {
                float b = buf[p];
                float o = -outW[i] + b;
                buf[p] = outW[i] + b * 0.5f;
                p = (p + 1) % d;
                outW[i] = o;
            }
        }
        for (int i = 0; i < y.Length; i++) y[i] = (i < x.Length ? x[i] : 0f) + outW[i] * wet;
        // trim trailing silence
        int end = y.Length;
        while (end > x.Length && Mathf.Abs(y[end - 1]) < 0.0005f) end--;
        Array.Resize(ref y, end);
        return y;
    }

    /// <summary>Makes a seamless loop: the last 'fade' seconds are cross-faded into the start.</summary>
    static float[] Loop(float[] x, float fade)
    {
        int f = Mathf.Min((int)(fade * SR), x.Length / 3);
        int n = x.Length - f;
        var y = new float[n];
        Array.Copy(x, y, n);
        for (int i = 0; i < f; i++)
        {
            float k = i / (float)f;
            y[i] = x[i] * k + x[n + i] * (1f - k);
        }
        return y;
    }

    static void Save(string name, float[] s, float peak)
    {
        float max = 0.0001f;
        foreach (var v in s) max = Mathf.Max(max, Mathf.Abs(v));
        float g = peak / max;
        string path = Path.Combine(Dir, name + ".wav");
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int bytes = s.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);      // PCM
            w.Write((short)1);      // mono
            w.Write(SR);
            w.Write(SR * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(bytes);
            foreach (var v in s) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * g * 32767f), -32768, 32767));
        }
    }

    public static AudioClip Load(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Dir}/{name}.wav");
}
