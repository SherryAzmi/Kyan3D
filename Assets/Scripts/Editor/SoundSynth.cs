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
        for (int v = 1; v <= 4; v++) Save("sfx_step_sand_" + v, SandStep(), 0.6f);
        for (int v = 1; v <= 4; v++) Save("sfx_step_grass_" + v, GrassStep(), 0.55f);
        Save("sfx_fall", FallWhoosh(), 0.7f);
        Save("sfx_hurt", Reverb(Hurt(), 0.2f), 0.95f);
        Save("sfx_throw", Reverb(Throw(), 0.3f), 0.8f);
        Save("sfx_arrive_neon", Reverb(ArriveNeon(), 0.4f), 0.8f);
        Save("sfx_respawn", Reverb(Respawn(), 0.4f), 0.85f);
        Save("sfx_message", Reverb(Message(), 0.3f), 0.55f);
        for (int v = 1; v <= 4; v++) Save("sfx_pen_" + v, PenStroke(), 0.45f);
        Save("music_finale", Loop(Reverb(Finale(48f), 0.45f), 3f), 0.5f);
        for (int v = 1; v <= 6; v++) Save("sfx_key_" + v, Reverb(KeyClick(false), 0.08f), 0.5f);
        Save("sfx_key_space", Reverb(KeyClick(true), 0.08f), 0.5f);
        Save("sfx_key_enter", Reverb(KeyEnter(), 0.1f), 0.55f);
        Save("music_letter", Loop(Reverb(LetterTheme(40f), 0.5f), 3f), 0.5f);

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

    static float[] FallWhoosh()
    {
        // air rushing past, getting louder and lower as you drop
        var s = New(1.1f);
        var n = White(s.Length);
        float lp = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float cutoff = Mathf.Lerp(2200f, 500f, t / 1.1f);
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SR)) * (n[i] - lp);
            float env = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((1.1f - t) / 0.15f);
            s[i] = lp * env * (0.8f + 0.2f * Mathf.Sin(t * 37f));
        }
        return s;
    }

    static float[] Hurt()
    {
        // a heavy body thud plus a short falling "damage" tone
        var s = New(0.75f);
        var crunch = LowPass(White(s.Length), 1200f);
        float ph = 0f, ph2 = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 38f, Mathf.Clamp01(t / 0.2f)) * t) * Mathf.Exp(-t * 9f);
            float f = Mathf.Lerp(330f, 120f, Mathf.Clamp01(t / 0.45f));
            ph += 2f * Mathf.PI * f / SR;
            ph2 += 2f * Mathf.PI * f * 1.5f / SR;
            // soft square-ish tone = the "you got hurt" sting
            float tone = (Mathf.Sin(ph) + 0.33f * Mathf.Sin(3f * ph) + 0.25f * Mathf.Sin(ph2)) * Env(t - 0.02f, 0.01f, 5f);
            s[i] = 0.9f * thud + 0.45f * crunch[i] * Mathf.Exp(-t * 18f) + 0.35f * tone;
        }
        return s;
    }

    static float[] Throw()
    {
        // a quick airy swish (the toss) and a magic shimmer that rises as the river takes it
        var s = New(1.0f);
        var n = White(s.Length);
        float bp = 0f, lp = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float cutoff = Mathf.Lerp(600f, 3200f, Mathf.Clamp01(t / 0.25f));
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SR)) * (n[i] - lp);
            bp += (1f - Mathf.Exp(-2f * Mathf.PI * 300f / SR)) * (lp - bp);
            float swish = (lp - bp) * Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t * 7f);
            s[i] = 1.1f * swish;
        }
        float[] up = { 659.25f, 783.99f, 987.77f, 1318.5f };
        for (int k = 0; k < up.Length; k++) AddBell(s, 0.12f + k * 0.07f, up[k], 0.16f, 0.5f);
        return s;
    }

    static float[] ArriveNeon()
    {
        // a soft "whoomp" of light swelling in, then a bright sparkling chord
        var s = New(1.8f);
        var n = LowPass(White(s.Length), 2500f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float swell = Mathf.Clamp01(t / 0.3f) * Mathf.Exp(-Mathf.Max(0f, t - 0.3f) * 9f);
            s[i] = 0.35f * n[i] * swell + 0.25f * Sin(Mathf.Lerp(180f, 420f, Mathf.Clamp01(t / 0.3f)), t) * swell;
        }
        float[] chord = { 783.99f, 1046.5f, 1318.5f, 1567.98f, 2093f };
        for (int k = 0; k < chord.Length; k++) AddBell(s, 0.28f + k * 0.045f, chord[k], 0.2f, 1.1f);
        for (int k = 0; k < 10; k++) AddBell(s, 0.35f + (float)rnd.NextDouble() * 0.9f, 2400f + (float)rnd.NextDouble() * 2400f, 0.05f, 0.15f); // glitter
        return s;
    }

    static float[] Respawn()
    {
        // rewinding swell (reverse-like rise) into a warm, calm chime: "you are back"
        var s = New(2.0f);
        var n = LowPass(White(s.Length), 3000f);
        float ph = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float rise = t < 0.7f ? Mathf.Pow(t / 0.7f, 2.2f) : Mathf.Exp(-(t - 0.7f) * 14f);
            ph += 2f * Mathf.PI * Mathf.Lerp(220f, 660f, Mathf.Clamp01(t / 0.7f)) / SR;
            s[i] = rise * (0.25f * n[i] + 0.3f * Mathf.Sin(ph) + 0.12f * Mathf.Sin(ph * 2.005f));
        }
        float[] chord = { 523.25f, 659.25f, 783.99f, 1046.5f };
        for (int k = 0; k < chord.Length; k++) AddBell(s, 0.7f + k * 0.03f, chord[k], 0.22f, 1.3f);
        return s;
    }

    static float[] Message()
    {
        // a soft, friendly "new message" chime: two quick rising notes with a gentle glow
        var s = New(1.1f);
        AddBell(s, 0f, 880f, 0.32f, 0.35f);
        AddBell(s, 0.09f, 1318.5f, 0.28f, 0.55f);
        AddBell(s, 0.09f, 1760f, 0.06f, 0.3f);
        return s;
    }

    static float[] PenStroke()
    {
        // one stroke of a pen on paper: a short, papery scratch with a little grain
        float len = 0.05f + (float)rnd.NextDouble() * 0.05f;
        var s = New(len + 0.02f);
        var n = White(s.Length);
        float lp = 0f, hp = 0f, prev = 0f;
        float cutoff = 3500f + (float)rnd.NextDouble() * 2500f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SR)) * (n[i] - lp);
            hp = 0.97f * (hp + lp - prev); prev = lp;                         // remove the rumble
            float grain = 0.7f + 0.3f * Mathf.Sin(t * 2f * Mathf.PI * (90f + 60f * (float)rnd.NextDouble())); // paper fibres
            float env = Mathf.Clamp01(t / 0.006f) * Mathf.Clamp01((len - t) / 0.02f + 0.0f);
            s[i] = hp * grain * env;
        }
        return s;
    }

    /// <summary>
    /// One key on a soft keyboard: a crisp plastic click as the key bottoms out, a short low "thock"
    /// from the case, and a faint click as it springs back up. Space bar = deeper and longer.
    /// </summary>
    static float[] KeyClick(bool space)
    {
        float r() => (float)rnd.NextDouble();
        var s = New(0.16f);
        var n = White(s.Length);
        float clickHz = (space ? 1800f : 2600f) + r() * 1200f;
        float thockHz = (space ? 120f : 190f) + r() * 50f;
        float upAt = 0.07f + r() * 0.03f;
        float bp = 0f, lp = 0f, ph = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * clickHz / SR)) * (n[i] - lp);
            bp += (1f - Mathf.Exp(-2f * Mathf.PI * 700f / SR)) * (lp - bp);
            float click = (lp - bp) * Mathf.Exp(-t * 900f);                       // down: sharp tick
            float up = (lp - bp) * 0.35f * Mathf.Exp(-Mathf.Max(0f, t - upAt) * 1200f) * (t >= upAt ? 1f : 0f);
            ph += 2f * Mathf.PI * Mathf.Lerp(thockHz * 1.3f, thockHz, Mathf.Clamp01(t / 0.01f)) / SR;
            float thock = Mathf.Sin(ph) * Mathf.Exp(-t * (space ? 45f : 70f)) * Mathf.Clamp01(t / 0.0015f);
            float rattle = space ? 0.25f * n[i] * Mathf.Exp(-t * 120f) : 0f;      // the long bar rattles a bit
            s[i] = 1.2f * click + up + (space ? 0.8f : 0.55f) * thock + rattle;
        }
        return s;
    }

    static float[] KeyEnter()
    {
        // the return key: a firmer double click (the long key) with a fuller thock
        var a = KeyClick(true);
        var b = KeyClick(false);
        var s = New(0.22f);
        int off = (int)(0.012f * SR);
        for (int i = 0; i < a.Length; i++) s[i] += a[i] * 0.9f;
        for (int i = 0; i < b.Length && i + off < s.Length; i++) s[i + off] += b[i] * 0.6f;
        return s;
    }

    static float[] LetterTheme(float sec)
    {
        // Music under the letter: tender and a little bittersweet. Am - F - C - G, slow piano and soft
        // strings, a simple melody that rises at the end of each phrase (sad, then hopeful).
        var s = New(sec);
        float[][] chords =
        {
            new[] { 110f, 220f, 261.63f, 329.63f },      // Am
            new[] { 87.31f, 174.61f, 220f, 261.63f },    // F
            new[] { 130.81f, 196f, 261.63f, 329.63f },   // C
            new[] { 98f, 196f, 246.94f, 293.66f },       // G
        };
        float bar = sec / 8f;
        for (int b = 0; b < 8; b++)
        {
            var ch = chords[b % 4];
            float t0 = b * bar;
            var part = Pad(bar + 2f, ch, new[] { 0.6f, 0.45f, 0.35f, 0.25f }, 0.05f);
            int off = (int)(t0 * SR);
            for (int i = 0; i < part.Length; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Clamp01(t / 1.5f) * Mathf.Clamp01((bar + 2f - t) / 2f);
                s[(off + i) % s.Length] += 0.45f * part[i] * env;
            }
            AddPiano(s, t0, ch[0], 0.3f, 3f);
            // slow rolling arpeggio: root - fifth - third - octave
            float[] arp = { ch[1], ch[2], ch[3], ch[1] * 2f };
            for (int k = 0; k < 8; k++) AddPiano(s, t0 + 0.3f + k * (bar - 0.4f) / 8f, arp[k % 4], 0.1f, 1.8f);
        }
        // melody: E - C - G - B (first time), E - F - G - C' (second time: rises, hopeful)
        float[] m1 = { 659.25f, 523.25f, 783.99f, 493.88f };
        float[] m2 = { 659.25f, 698.46f, 783.99f, 1046.5f };
        for (int b = 0; b < 4; b++) AddPiano(s, b * bar + bar * 0.25f, m1[b], 0.17f, 2.6f);
        for (int b = 0; b < 4; b++) AddPiano(s, (4 + b) * bar + bar * 0.25f, m2[b], 0.17f, 2.6f);
        return s;
    }

    /// <summary>Piano-like note: a hammer tick, then harmonics that fade at different speeds.</summary>
    static void AddPiano(float[] s, float at, float f, float amp, float sustain)
    {
        int a = (int)(at * SR);
        for (int i = a; i < s.Length; i++)
        {
            float t = (i - a) / (float)SR;
            float e = Mathf.Exp(-t / sustain) * Mathf.Clamp01(t / 0.003f);
            if (t > 0.1f && e < 0.0004f) break;
            float x = 0.6f * Sin(f, t) + 0.28f * Sin(f * 2.002f, t) * Mathf.Exp(-t * 1.5f)
                    + 0.12f * Sin(f * 3.005f, t) * Mathf.Exp(-t * 3f) + 0.06f * Sin(f * 4.01f, t) * Mathf.Exp(-t * 5f);
            s[i] += amp * e * x;
        }
    }

    static float[] Finale(float sec)
    {
        // The ending theme: slow piano over warm strings. C - G - Am - F, twice; the second time the
        // melody climbs higher, like letting the light in.
        var s = New(sec);
        float[][] chords =
        {
            new[] { 130.81f, 196f, 261.63f, 329.63f },   // C
            new[] { 98f, 196f, 246.94f, 293.66f },       // G
            new[] { 110f, 220f, 261.63f, 329.63f },      // Am
            new[] { 87.31f, 174.61f, 261.63f, 349.23f }, // F
        };
        float bar = sec / 8f;
        for (int b = 0; b < 8; b++)
        {
            var ch = chords[b % 4];
            float t0 = b * bar;
            // strings: a soft pad for the bar
            var part = Pad(bar + 2f, ch, new[] { 0.7f, 0.5f, 0.4f, 0.3f }, 0.04f);
            int off = (int)(t0 * SR);
            for (int i = 0; i < part.Length; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Clamp01(t / 1.2f) * Mathf.Clamp01((bar + 2f - t) / 2f);
                s[(off + i) % s.Length] += 0.55f * part[i] * env;
            }
            // piano: bass note, then a gentle broken chord
            AddPiano(s, t0, ch[0], 0.32f, 2.6f);
            float[] arp = { ch[1], ch[2], ch[3], ch[2] * 2f, ch[3], ch[2] };
            for (int k = 0; k < arp.Length; k++) AddPiano(s, t0 + 0.5f + k * (bar - 0.8f) / arp.Length, arp[k], 0.13f, 1.6f);
        }
        // melody (one note or two per bar), higher the second time round
        float[] mel1 = { 659.25f, 587.33f, 523.25f, 523.25f };
        float[] mel2 = { 783.99f, 783.99f, 880f, 698.46f };
        float[] mel2b = { 659.25f, 587.33f, 659.25f, 659.25f };
        for (int b = 0; b < 4; b++) AddPiano(s, b * bar + 0.2f, mel1[b], 0.2f, 2.2f);
        for (int b = 0; b < 4; b++)
        {
            AddPiano(s, (4 + b) * bar + 0.2f, mel2[b], 0.2f, 2.2f);
            AddPiano(s, (4 + b) * bar + bar * 0.55f, mel2b[b], 0.15f, 2f);
        }
        return s;
    }

    static float[] SandStep()
    {
        // heel then toe pressing into dry sand: a soft muffled crunch made of many tiny grain clicks
        var s = New(0.32f);
        var body = LowPass(HighPass(White(s.Length), 300f), 2600f);
        float heel = 0f, toe = 0.07f + (float)rnd.NextDouble() * 0.03f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float e = Env(t - heel, 0.008f, 14f) + 0.75f * Env(t - toe, 0.01f, 12f);
            s[i] = body[i] * e * 0.8f + 0.25f * Mathf.Sin(2f * Mathf.PI * 70f * t) * Env(t - heel, 0.004f, 30f);
        }
        var grains = HighPass(White(s.Length), 2500f);
        for (int k = 0; k < 60; k++)
        {
            float at = (float)rnd.NextDouble() * 0.2f;
            int a = (int)(at * SR), len = (int)(0.002f * SR + rnd.Next(60));
            float amp = 0.15f + (float)rnd.NextDouble() * 0.3f;
            for (int i = 0; i < len && a + i < s.Length; i++) s[a + i] += grains[a + i] * amp * (1f - i / (float)len);
        }
        return s;
    }

    static float[] GrassStep()
    {
        // foot brushing through grass: a quick airy rustle with a soft thud underneath
        var s = New(0.36f);
        var rustle = HighPass(LowPass(White(s.Length), 7000f), 1800f);
        float second = 0.05f + (float)rnd.NextDouble() * 0.04f;
        float seed = (float)rnd.NextDouble() * 50f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float flutter = 0.55f + 0.45f * Mathf.PerlinNoise(t * 140f, seed);
            float e = Env(t, 0.015f, 9f) + 0.6f * Env(t - second, 0.02f, 10f);
            s[i] = rustle[i] * e * flutter * 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 85f * t) * Env(t, 0.005f, 28f);
        }
        return s;
    }

    /// <summary>Attack-decay envelope: 0 before t=0, rises over 'attack' seconds, then decays.</summary>
    static float Env(float t, float attack, float decay) =>
        t < 0f ? 0f : Mathf.Clamp01(t / attack) * Mathf.Exp(-Mathf.Max(0f, t - attack) * decay);

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
