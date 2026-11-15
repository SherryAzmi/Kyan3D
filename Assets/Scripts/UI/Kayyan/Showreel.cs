#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor-only: records a short gameplay video (menu, then the first puzzle) from the Game view, with the
/// game's own sound, into an AVI file (Motion JPEG + PCM audio, written by AviWriter: no extra software needed).
/// A virtual controller is plugged in, so Player 1 plays with the "controller" and Player 2 with the
/// keyboard and mouse (the one-controller rule in PlayerInputReader), both driven by scripted input.
/// Started by ShotDriver for the job "video".
/// </summary>
public class Showreel : MonoBehaviour
{
    const int W = 960, H = 540;
    const float Fps = 15f;

    AviWriter avi;
    RenderTexture rt;
    Texture2D tex;
    bool recording;
    float startTime;
    long audioWritten;
    Gamepad pad;
    Keyboard kb;
    readonly List<GameObject> taps = new List<GameObject>();

    public IEnumerator Run(string path)
    {
        // input: let scripted input through while the editor is in the background, and put the settings back afterwards
        // (never replace InputSystem.settings itself: a destroyed settings object breaks all editor input)
        var settings = InputSystem.settings;
        var oldBackground = settings.backgroundBehavior;
        var oldEditorBehavior = settings.editorInputBehaviorInPlayMode;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        restoreInput = () =>
        {
            settings.backgroundBehavior = oldBackground;
            settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
        };
        pad = InputSystem.AddDevice<Gamepad>("Showreel Controller");
        kb = Keyboard.current;
        Debug.Log("[Showreel] controllers: " + Gamepad.all.Count + ", keyboard: " + (kb != null));

        rt = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32);
        tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        int rate = AudioSettings.outputSampleRate;
        avi = new AviWriter(path, W, H, rate, 2);
        recording = true;
        startTime = Time.unscaledTime;
        StartCoroutine(CaptureLoop());

        // ---------------- main menu
        yield return Wait(4.5f);
        var menu = MainMenu.Current;
        if (menu)
        {
            UISound.Click(); menu.DebugPage("settings"); yield return Wait(1.2f);
            var sliders = FindObjectsByType<Slider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var s in sliders)
            {
                float v = s.value;
                for (float t = 0f; t < 1f; t += 0.1f) { s.value = Mathf.Lerp(v, 0.55f, t); yield return Wait(0.06f); }
                for (float t = 0f; t < 1f; t += 0.1f) { s.value = Mathf.Lerp(0.55f, 1f, t); yield return Wait(0.06f); }
                break;
            }
            yield return Wait(0.6f);
            UISound.Click(); menu.DebugPage("controls"); yield return Wait(4.5f);
            UISound.Click(); menu.DebugPage("main"); yield return Wait(1.2f);
        }
        UISound.Click();
        SceneManager.LoadScene("SplitCamera");
        yield return Wait(5f);

        // ---------------- puzzle 1
        var hud = FindFirstObjectByType<PuzzleHUD>();
        if (hud) hud.ShowBoth("The sand hides something... follow the light.", 8f);
        yield return Wait(2.5f);

        // both walk: Player 1 with the left stick, Player 2 with W
        SetStick(new Vector2(0f, 1f)); Keys(Key.W);
        yield return Wait(3f);
        SetStick(new Vector2(1f, 0.4f)); Keys(Key.W, Key.D);
        yield return Wait(2.5f);
        Pad(jump: true); Keys(Key.W, Key.Space); yield return Wait(0.2f); Pad(jump: false); Keys(Key.W);
        yield return Wait(1.2f);
        SetStick(Vector2.zero); Keys();
        yield return Wait(0.5f);

        // Player 1 digs a mound: the R2 prompt floats above it, then R2 is held
        PlayerInteractor p1 = null, p2 = null;
        foreach (var p in FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None)) { if (p.hudSide == 0) p1 = p; else p2 = p; }
        var mound = FindFirstObjectByType<SandMound>();
        if (p1 && mound) Teleport(p1, mound.transform.position + new Vector3(-1.4f, 0.3f, 0f));
        var spot = FindFirstObjectByType<RootSocket>();
        if (p2 && spot) Teleport(p2, spot.transform.position + new Vector3(-1.4f, 0.3f, 0f));
        yield return Wait(2.2f);
        Trigger(1f); Keys(Key.F);
        yield return Wait(4f);
        Trigger(0f); Keys();
        yield return Wait(1f);

        // the controls panels: Player 1 shows the controller, Player 2 the keyboard and mouse
        PauseMenu.DebugShow("help2"); yield return Wait(4.5f);
        PauseMenu.DebugShow("help1"); yield return Wait(4.5f);
        PauseMenu.DebugShow("settings"); yield return Wait(3f);
        PauseMenu.DebugShow("pause"); yield return Wait(3f);
        PauseMenu.DebugShow("none"); yield return Wait(2f);

        recording = false;
        yield return null;
        avi.Close();
        Debug.Log("[Showreel] saved " + path + " (" + new FileInfo(path).Length / 1024 + " KB)");
        if (pad != null) InputSystem.RemoveDevice(pad);
        restoreInput?.Invoke();
    }

    System.Action restoreInput;

    void OnDestroy()
    {
        // also when Play mode is stopped early
        if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        restoreInput?.Invoke();
    }

    static WaitForSecondsRealtime Wait(float s) => new WaitForSecondsRealtime(s);

    // ---------------------------------------------------------------- scripted input

    void SetStick(Vector2 v) { if (pad != null) InputSystem.QueueDeltaStateEvent(pad.leftStick, v); }
    void Trigger(float v) { if (pad != null) InputSystem.QueueDeltaStateEvent(pad.rightTrigger, v); }
    void Pad(bool jump) { if (pad != null) InputSystem.QueueDeltaStateEvent(pad.buttonSouth, jump ? 1f : 0f); }
    void Keys(params Key[] keys) { if (kb != null) InputSystem.QueueStateEvent(kb, new KeyboardState(keys)); }

    static void Teleport(PlayerInteractor p, Vector3 pos)
    {
        var cc = p.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        p.transform.position = pos;
        if (cc) cc.enabled = true;
    }

    // ---------------------------------------------------------------- capture

    IEnumerator CaptureLoop()
    {
        float last = -1f, tapCheck = 0f;
        while (recording)
        {
            yield return new WaitForEndOfFrame();
            if (Time.unscaledTime >= tapCheck)
            {
                tapCheck = Time.unscaledTime + 0.4f;
                var l = FindFirstObjectByType<AudioListener>();
                if (l && !l.GetComponent<AudioTap>()) l.gameObject.AddComponent<AudioTap>();
            }
            float now = Time.unscaledTime;
            if (now - last < 1f / Fps) continue;
            last = now;

            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            Graphics.Blit(shot, rt);
            Destroy(shot);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply(false);
            RenderTexture.active = null;

            // audio since the last frame, padded with silence so it never falls behind the clock
            byte[] audio = AudioTap.Drain();
            long want = (long)((now - startTime) * avi.AudioRate);
            long have = audioWritten + audio.Length / (2 * avi.AudioChannels);
            if (have < want)
            {
                var padded = new byte[audio.Length + (want - have) * 2 * avi.AudioChannels];
                System.Array.Copy(audio, padded, audio.Length);
                audio = padded;
            }
            audioWritten += audio.Length / (2 * avi.AudioChannels);
            avi.AddFrame(tex.EncodeToJPG(72), audio, now - startTime);
        }
    }
}

/// <summary>Copies everything the AudioListener hears (the final mix) so it can go into the video.</summary>
public class AudioTap : MonoBehaviour
{
    static readonly object Lock = new object();
    static readonly Queue<short[]> Chunks = new Queue<short[]>();

    void OnAudioFilterRead(float[] data, int channels)
    {
        var s = new short[data.Length];
        for (int i = 0; i < data.Length; i++) s[i] = (short)(Mathf.Clamp(data[i], -1f, 1f) * 32767f);
        lock (Lock) Chunks.Enqueue(s);
    }

    public static byte[] Drain()
    {
        lock (Lock)
        {
            int total = 0;
            foreach (var c in Chunks) total += c.Length;
            var bytes = new byte[total * 2];
            int o = 0;
            while (Chunks.Count > 0)
            {
                var c = Chunks.Dequeue();
                System.Buffer.BlockCopy(c, 0, bytes, o, c.Length * 2);
                o += c.Length * 2;
            }
            return bytes;
        }
    }
}

/// <summary>A small AVI writer: Motion JPEG video + 16-bit PCM audio, with the index at the end.</summary>
public class AviWriter
{
    readonly FileStream fs;
    readonly BinaryWriter bw;
    readonly int w, h;
    public readonly int AudioRate, AudioChannels;

    readonly List<long> idxOffset = new List<long>();
    readonly List<int> idxSize = new List<int>();
    readonly List<bool> idxVideo = new List<bool>();
    long riffSizePos, hdrlSizePos, moviSizePos, moviBase;
    long avihMicroPos, avihFramesPos, strhScalePos, strhRatePos, strhLenPos, audLenPos;
    int frames;
    long audioSamples;
    float lastTime;

    public AviWriter(string path, int width, int height, int audioRate, int audioChannels)
    {
        w = width; h = height; AudioRate = audioRate; AudioChannels = audioChannels;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        bw = new BinaryWriter(fs);

        Four("RIFF"); riffSizePos = fs.Position; bw.Write(0); Four("AVI ");
        hdrlSizePos = BeginList("hdrl");

        Four("avih"); bw.Write(56);
        avihMicroPos = fs.Position; bw.Write(66667);          // microseconds per frame (patched)
        bw.Write(1000000);                                    // max bytes per second
        bw.Write(0);
        bw.Write(0x10);                                       // has index
        avihFramesPos = fs.Position; bw.Write(0);             // total frames (patched)
        bw.Write(0); bw.Write(2); bw.Write(0x100000);
        bw.Write(w); bw.Write(h);
        bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

        long v = BeginList("strl");
        Four("strh"); bw.Write(56);
        Four("vids"); Four("MJPG"); bw.Write(0); bw.Write((short)0); bw.Write((short)0); bw.Write(0);
        strhScalePos = fs.Position; bw.Write(1000);
        strhRatePos = fs.Position; bw.Write(15000);
        bw.Write(0);
        strhLenPos = fs.Position; bw.Write(0);
        bw.Write(0x100000); bw.Write(-1); bw.Write(0);
        bw.Write((short)0); bw.Write((short)0); bw.Write((short)w); bw.Write((short)h);
        Four("strf"); bw.Write(40);
        bw.Write(40); bw.Write(w); bw.Write(h); bw.Write((short)1); bw.Write((short)24); Four("MJPG");
        bw.Write(w * h * 3); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
        EndList(v);

        long a = BeginList("strl");
        Four("strh"); bw.Write(56);
        Four("auds"); bw.Write(0); bw.Write(0); bw.Write((short)0); bw.Write((short)0); bw.Write(0);
        bw.Write(1); bw.Write(AudioRate); bw.Write(0);
        audLenPos = fs.Position; bw.Write(0);
        bw.Write(0x10000); bw.Write(-1); bw.Write(2 * AudioChannels);
        bw.Write((short)0); bw.Write((short)0); bw.Write((short)0); bw.Write((short)0);
        Four("strf"); bw.Write(16);
        bw.Write((short)1); bw.Write((short)AudioChannels); bw.Write(AudioRate);
        bw.Write(AudioRate * 2 * AudioChannels); bw.Write((short)(2 * AudioChannels)); bw.Write((short)16);
        EndList(a);

        EndList(hdrlSizePos);

        moviSizePos = BeginList("movi");
        moviBase = fs.Position - 4; // the offset base is the 'movi' tag
    }

    public void AddFrame(byte[] jpeg, byte[] audio, float time)
    {
        Chunk("00dc", jpeg, true);
        frames++;
        if (audio != null && audio.Length > 0)
        {
            Chunk("01wb", audio, false);
            audioSamples += audio.Length / (2 * AudioChannels);
        }
        lastTime = time;
    }

    public void Close()
    {
        EndList(moviSizePos);
        Four("idx1"); bw.Write(idxOffset.Count * 16);
        for (int i = 0; i < idxOffset.Count; i++)
        {
            Four(idxVideo[i] ? "00dc" : "01wb");
            bw.Write(idxVideo[i] ? 0x10 : 0x10);
            bw.Write((int)idxOffset[i]);
            bw.Write(idxSize[i]);
        }
        long end = fs.Position;
        Patch(riffSizePos, (int)(end - 8));

        float dur = Mathf.Max(0.1f, lastTime);
        float fps = frames / dur;
        Patch(avihMicroPos, Mathf.RoundToInt(1000000f / fps));
        Patch(avihFramesPos, frames);
        Patch(strhScalePos, 1000);
        Patch(strhRatePos, Mathf.RoundToInt(fps * 1000f));
        Patch(strhLenPos, frames);
        Patch(audLenPos, (int)audioSamples);
        bw.Flush();
        bw.Close();
        fs.Close();
    }

    void Chunk(string id, byte[] data, bool video)
    {
        long pos = fs.Position;
        Four(id); bw.Write(data.Length); bw.Write(data);
        if ((data.Length & 1) == 1) bw.Write((byte)0);
        idxOffset.Add(pos - moviBase);
        idxSize.Add(data.Length);
        idxVideo.Add(video);
    }

    long BeginList(string type)
    {
        Four("LIST");
        long pos = fs.Position;
        bw.Write(0);
        Four(type);
        return pos;
    }

    void EndList(long sizePos) => Patch(sizePos, (int)(fs.Position - sizePos - 4));

    void Patch(long pos, int value)
    {
        long keep = fs.Position;
        fs.Position = pos;
        bw.Write(value);
        fs.Position = keep;
    }

    void Four(string s) => bw.Write(Encoding.ASCII.GetBytes(s));
}
#endif
