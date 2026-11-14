using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One per gameplay scene. Plays music + two ambience beds (desert panned left, forest panned right,
/// matching the split screen) and all puzzle sound effects. Every clip is an Inspector slot, so any
/// generated sound can be swapped for a real recording.
/// Scripts call the static helpers (GameAudio.Play, GameAudio.Hold); they do nothing if no GameAudio exists.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio I { get; private set; }

    [Header("Music and ambience (looped)")]
    public AudioClip music;
    public AudioClip ambienceLeft;   // desert (Present)
    public AudioClip ambienceRight;  // forest (Future)
    [Range(0f, 1f)] public float musicVolume = 0.32f;
    [Range(0f, 1f)] public float ambienceVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 0.85f;

    [Header("Puzzle SFX")]
    public AudioClip dig;          // loop while digging
    public AudioClip digDone;
    public AudioClip pickup;
    public AudioClip drop;
    public AudioClip splash;
    public AudioClip arrive;
    public AudioClip place;
    public AudioClip grow;         // loop while growing roots / blooming
    public AudioClip bridgeDone;
    public AudioClip fragment;
    public AudioClip bloom;
    public AudioClip joinHum;      // loop while both hold the mirror
    public AudioClip mirrorWhole;
    public AudioClip click;

    [Header("Ending")]
    public AudioClip endingMusic;

    [Tooltip("World X above which a sound counts as the forest (right screen).")]
    public float forestMinX = 150f;
    [Tooltip("World X above which a sound is in the ending room (centre).")]
    public float endingMinX = 450f;

    AudioSource musicSrc, ambLeftSrc, ambRightSrc;
    readonly List<AudioSource> oneShots = new List<AudioSource>();
    readonly Dictionary<string, AudioSource> holds = new Dictionary<string, AudioSource>();
    readonly Dictionary<string, float> holdSeen = new Dictionary<string, float>();

    void Awake()
    {
        I = this;
        musicSrc = MakeSource("Music", music, true, 0f, 0f);
        ambLeftSrc = MakeSource("Ambience Left", ambienceLeft, true, -0.6f, 0f);
        ambRightSrc = MakeSource("Ambience Right", ambienceRight, true, 0.6f, 0f);
        for (int i = 0; i < 8; i++) oneShots.Add(MakeSource("SFX " + i, null, false, 0f, 1f));
    }

    IEnumerator Start()
    {
        // Ease in under the scene fade.
        for (float t = 0f; t < 2.5f; t += Time.unscaledDeltaTime)
        {
            float k = t / 2.5f;
            musicSrc.volume = musicVolume * k;
            ambLeftSrc.volume = ambRightSrc.volume = ambienceVolume * k;
            yield return null;
        }
        musicSrc.volume = musicVolume;
        ambLeftSrc.volume = ambRightSrc.volume = ambienceVolume;
    }

    void OnDestroy()
    {
        if (I == this) I = null;
    }

    AudioSource MakeSource(string name, AudioClip clip, bool loop, float pan, float vol)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.loop = loop;
        s.panStereo = pan;
        s.volume = vol;
        s.clip = clip;
        if (loop && clip) s.Play();
        return s;
    }

    // ---------------------------------------------------------------- static API

    /// <summary>Which screen a world position belongs to: 0 left (desert), 1 right (forest), -1 centre.</summary>
    public static int SideOf(Vector3 worldPos)
    {
        if (!I) return -1;
        if (worldPos.x >= I.endingMinX) return -1;
        return worldPos.x >= I.forestMinX ? 1 : 0;
    }

    static float Pan(int side) => side == 0 ? -0.55f : side == 1 ? 0.55f : 0f;

    /// <summary>Play a one-shot. side: 0 left, 1 right, -1 centre.</summary>
    public static void Play(AudioClip clip, int side = -1, float volume = 1f, float pitch = 1f)
    {
        if (!I || !clip) return;
        AudioSource free = null;
        foreach (var s in I.oneShots) if (!s.isPlaying) { free = s; break; }
        if (!free) free = I.oneShots[0];
        free.clip = clip;
        free.panStereo = Pan(side);
        free.volume = I.sfxVolume * volume;
        free.pitch = pitch;
        free.Play();
    }

    public static void Play(AudioClip clip, Vector3 at, float volume = 1f) => Play(clip, SideOf(at), volume);

    /// <summary>
    /// Call every frame while something is held (digging, growing, joining). The loop starts on the
    /// first call and fades out by itself shortly after the calls stop.
    /// </summary>
    public static void Hold(string key, AudioClip clip, int side, float pitch = 1f, float volume = 1f)
    {
        if (!I || !clip) return;
        if (!I.holds.TryGetValue(key, out var s) || !s)
        {
            s = I.MakeSource("Hold " + key, clip, true, Pan(side), 0f);
            I.holds[key] = s;
        }
        if (!s.isPlaying) s.Play();
        s.pitch = pitch;
        s.volume = Mathf.MoveTowards(s.volume, I.sfxVolume * volume, Time.unscaledDeltaTime * 4f);
        I.holdSeen[key] = Time.unscaledTime;
    }

    /// <summary>Cross-fade the music to another clip and fade the ambience beds.</summary>
    public static void ChangeMusic(AudioClip clip, float seconds, float ambienceTo = 0f)
    {
        if (!I) return;
        I.StartCoroutine(I.CrossFade(clip, seconds, ambienceTo));
    }

    IEnumerator CrossFade(AudioClip clip, float seconds, float ambienceTo)
    {
        float m0 = musicSrc.volume, a0 = ambLeftSrc.volume;
        for (float t = 0f; t < seconds * 0.5f; t += Time.unscaledDeltaTime)
        {
            float k = t / (seconds * 0.5f);
            musicSrc.volume = Mathf.Lerp(m0, 0f, k);
            ambLeftSrc.volume = ambRightSrc.volume = Mathf.Lerp(a0, ambienceVolume * ambienceTo, k);
            yield return null;
        }
        if (clip)
        {
            musicSrc.clip = clip;
            musicSrc.Play();
        }
        for (float t = 0f; t < seconds * 0.5f; t += Time.unscaledDeltaTime)
        {
            musicSrc.volume = Mathf.Lerp(0f, musicVolume * 1.3f, t / (seconds * 0.5f));
            yield return null;
        }
    }

    void Update()
    {
        foreach (var kv in holds)
        {
            if (!kv.Value || !kv.Value.isPlaying) continue;
            if (Time.unscaledTime - holdSeen[kv.Key] > 0.12f)
            {
                kv.Value.volume = Mathf.MoveTowards(kv.Value.volume, 0f, Time.unscaledDeltaTime * 3f);
                if (kv.Value.volume <= 0.001f) kv.Value.Stop();
            }
        }
    }
}
