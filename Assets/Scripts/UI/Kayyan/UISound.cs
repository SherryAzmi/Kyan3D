using UnityEngine;

/// <summary>
/// Sounds for the menus and panels, generated in code (soft wooden plucks and chimes, no audio files needed).
/// Hover, click, panel open, panel close, slider tick. They follow the Sound effects and Master settings
/// and keep playing while the game is paused.
/// </summary>
public static class UISound
{
    static AudioSource src;
    static AudioClip hover, click, open, close, tick;
    static float quietUntil, lastTick;

    public static void Hover() { if (Time.unscaledTime >= quietUntil) Play(hover, 0.45f); }
    public static void Click() => Play(click, 0.8f);
    public static void Open() { quietUntil = Time.unscaledTime + 0.2f; Play(open, 0.8f); }
    public static void Close() { quietUntil = Time.unscaledTime + 0.2f; Play(close, 0.7f); }

    public static void Tick()
    {
        if (Time.unscaledTime - lastTick < 0.07f) return;
        lastTick = Time.unscaledTime;
        Play(tick, 0.4f);
    }

    static void Play(AudioClip c, float volume)
    {
        Ensure();
        if (!src || !c) return;
        src.PlayOneShot(c, volume * Mathf.Max(0.05f, GameSettings.Sfx));
    }

    static void Ensure()
    {
        if (src) return;
        var go = new GameObject("Kayyan UI Sound");
        Object.DontDestroyOnLoad(go);
        src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = true;

        hover = Make(0.07f, (t, n) => Pluck(t, 1040f, 38f) * 0.5f);
        tick = Make(0.05f, (t, n) => Pluck(t, 1400f, 70f) * 0.4f);
        click = Make(0.16f, (t, n) => Pluck(t, 520f, 22f) * 0.8f + Pluck(t, 780f, 26f) * 0.5f * (t > 0.045f ? 1f : 0f) + Noise(t, 90f) * 0.25f);
        open = Make(0.55f, (t, n) =>
            Pluck(t, 523.25f, 7f) * 0.55f +
            (t > 0.07f ? Pluck(t - 0.07f, 659.25f, 7f) * 0.5f : 0f) +
            (t > 0.14f ? Pluck(t - 0.14f, 783.99f, 6f) * 0.5f : 0f) +
            (t > 0.21f ? Pluck(t - 0.21f, 1046.5f, 7f) * 0.35f : 0f));
        close = Make(0.38f, (t, n) =>
            Pluck(t, 783.99f, 9f) * 0.55f +
            (t > 0.08f ? Pluck(t - 0.08f, 523.25f, 8f) * 0.55f : 0f));
    }

    /// <summary>A plucked tone: a sine and its octave with a fast attack and an exponential decay.</summary>
    static float Pluck(float t, float freq, float decay)
    {
        float env = Mathf.Min(1f, t * 600f) * Mathf.Exp(-decay * t);
        return env * (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t));
    }

    static float Noise(float t, float decay) => (Random.value * 2f - 1f) * Mathf.Exp(-decay * t);

    static AudioClip Make(float seconds, System.Func<float, int, float> f)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(seconds * rate);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float fade = Mathf.Clamp01((n - i) / (rate * 0.01f)); // no click at the end
            data[i] = Mathf.Clamp(f(i / (float)rate, i), -1f, 1f) * fade;
        }
        var clip = AudioClip.Create("ui", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
