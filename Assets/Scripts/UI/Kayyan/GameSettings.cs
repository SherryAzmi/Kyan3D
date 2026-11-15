using UnityEngine;

/// <summary>
/// Player settings that persist between sessions (PlayerPrefs). Master volume and mute are applied to
/// the whole game through AudioListener.volume. Music and Sound effects are multipliers (1 = the level the
/// game was tuned at) read by GameAudio and the cutscenes.
/// </summary>
public static class GameSettings
{
    const string Prefix = "kayyan.";

    public static float Master { get => Get("master"); set { Set("master", value); Apply(); } }
    public static float Music { get => Get("music"); set => Set("music", value); }
    public static float Sfx { get => Get("sfx"); set => Set("sfx", value); }
    public static bool Muted
    {
        get => PlayerPrefs.GetInt(Prefix + "muted", 0) == 1;
        set { PlayerPrefs.SetInt(Prefix + "muted", value ? 1 : 0); Apply(); }
    }

    static float Get(string key) => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + key, 1f));
    static void Set(string key, float v) => PlayerPrefs.SetFloat(Prefix + key, Mathf.Clamp01(v));

    public static void Apply() => AudioListener.volume = Muted ? 0f : Master;

    public static void Save() => PlayerPrefs.Save();

    public static void Reset()
    {
        Set("master", 1f); Set("music", 1f); Set("sfx", 1f);
        PlayerPrefs.SetInt(Prefix + "muted", 0);
        Apply();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() => Apply();
}
