using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Opening cutscene: the Present stares at a faded reflection, dark thoughts swarm his head,
/// the mirror cracks and shatters, the screen fades to black and the split-screen scene loads.
/// No player control. Space / Enter / Esc / gamepad South or Start skips.
/// Everything is wired by Tools > Kyan > Build Mirror Cutscene.
/// </summary>
public class MirrorCutscene : MonoBehaviour
{
    [Header("Camera")]
    public Camera cam;
    [Tooltip("Side shot that shows the sad face.")]
    public Transform shotFace;
    [Tooltip("Over-the-shoulder shot looking into the mirror.")]
    public Transform shotShoulder;
    [Tooltip("Where the over-the-shoulder shot slowly pushes in to.")]
    public Transform shotClose;

    [Header("Character")]
    public Transform character;
    [Tooltip("Neck pivot of the real character (bows down as the thoughts grow).")]
    public Transform head;
    [Tooltip("Neck pivot of the reflection (copies the real head).")]
    public Transform reflectionHead;
    public GameObject reflection;

    [Header("Mirror")]
    public Renderer mirrorGlass;
    public Material crackMaterial;
    public Material shardMaterial;
    [Tooltip("Impact point on the glass, in glass local space (-0.5..0.5).")]
    public Vector2 impactPoint = new Vector2(0.1f, 0.2f);
    public int shardCount = 55;

    [Header("Atmosphere")]
    public Light[] roomLights;
    public ParticleSystem darkSmoke;
    public Volume darkVolume;

    [Header("UI")]
    public Image fadeImage;
    public Image flashImage;
    public Text captionText;
    public Text skipHint;

    [Header("Thoughts")]
    public string[] thoughts =
    {
        "Not good enough.", "Why even try?", "You always fail.", "Look at you.",
        "You'll never change.", "Everyone is tired of you.", "Too late.", "Who are you?",
        "Worthless.", "You're a disappointment.", "Stop pretending.", "You ruined it.",
        "I feel worthless.", "I feel like I'm useless."
    };
    [Tooltip("One thought that stays a while: bigger, slower, near the middle of the frame. Shown twice.")]
    public string lingeringThought = "What is the meaning of my life?";
    public float lingeringLife = 5.5f;
    public Color thoughtColor = new Color(0.8f, 0.12f, 0.14f);
    public Color thoughtColorAlt = new Color(0.75f, 0.75f, 0.8f);

    [Header("Timing (seconds)")]
    public float faceShotTime = 4.5f;
    public float shoulderShotTime = 3f;
    public float thoughtsTime = 9f;
    public float captionHoldTime = 2.5f;

    [Header("Caption shown on black before the split screen")]
    [TextArea] public string caption = "...and then I fell in between the worlds of consciousness.";

    [Header("Audio (optional, can stay empty)")]
    public AudioSource sfx;
    public AudioClip whisperClip;
    public AudioClip crackClip;
    public AudioClip shatterClip;
    public AudioClip roomToneClip;
    public AudioClip musicClip;
    public AudioClip heartbeatClip;
    [Range(0f, 1f)] public float roomToneVolume = 0.45f;
    [Range(0f, 1f)] public float musicVolume = 0.35f;
    [Range(0f, 1f)] public float whisperMaxVolume = 0.85f;

    [Header("Next scene (must be in Build Profiles scene list)")]
    public string nextScene = "SplitCamera";

    [Tooltip("Optional: plays first (ordering coffee in the café), then the mirror scene as before.")]
    public CafePrelude prelude;

    class Thought
    {
        public Transform t;
        public TextMesh tm;
        public Vector3 vel;
        public Color color;
        public float age, life, seed;
    }

    readonly List<Thought> live = new List<Thought>();
    readonly List<GameObject> crackSegments = new List<GameObject>();
    Font font;
    Material thoughtMat;
    /// <summary>Every thought stays fully visible at least this long before it fades.</summary>
    const float ReadTime = 1.5f;

    /// <summary>The font material, drawn on top of everything (so heads, the mirror frame or walls never cut a thought off).</summary>
    Material ThoughtMaterial()
    {
        if (thoughtMat) return thoughtMat;
        thoughtMat = new Material(font.material);
        thoughtMat.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
        thoughtMat.renderQueue = 4000;
        return thoughtMat;
    }
    Transform crackRoot;
    Vector3 camPos;
    Quaternion camRot;
    float shake;
    float distress;
    float[] lightBase;
    Quaternion headBase;
    float headBow;
    bool headAnimated;
    Vector3 charBaseScale, charBasePos;
    bool inPrelude;
    Coroutine main;
    bool ending;
    float fixedDtBase;
    AudioSource roomSrc, musicSrc, whisperSrc;

    AudioSource LoopSource(AudioClip clip, float volume)
    {
        if (!clip) return null;
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = volume;
        s.Play();
        return s;
    }

    IEnumerator FadeSource(AudioSource s, float to, float duration)
    {
        if (!s) yield break;
        float from = s.volume;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            if (!s) yield break;
            s.volume = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        if (s) s.volume = to;
    }

    void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        fixedDtBase = Time.fixedDeltaTime;
        Time.timeScale = 1f;

        lightBase = new float[roomLights.Length];
        for (int i = 0; i < roomLights.Length; i++) lightBase[i] = roomLights[i] ? roomLights[i].intensity : 0f;
        headBase = head ? head.localRotation : Quaternion.identity;
        headAnimated = head && head.GetComponentInParent<Animator>();
        if (character) { charBaseScale = character.localScale; charBasePos = character.localPosition; }

        SetAlpha(fadeImage, 1f);
        SetAlpha(flashImage, 0f);
        SetAlpha(captionText, 0f);
        if (captionText) captionText.text = caption;
        if (darkVolume) darkVolume.weight = 0.15f;
        SetSmoke(0f);
        BuildCracks();

        roomSrc = LoopSource(roomToneClip, 0f);
        musicSrc = LoopSource(musicClip, 0f);
        whisperSrc = LoopSource(whisperClip, 0f);
        StartCoroutine(FadeSource(roomSrc, roomToneVolume, 3f));
        StartCoroutine(FadeSource(musicSrc, musicVolume, 4f));

        main = StartCoroutine(Run());
    }

    // ------------------------------------------------------------------ sequence

    IEnumerator Run()
    {
        // 0. (Café) He orders a coffee, stares at the sign on the wall, walks to the mirror; ends on black.
        if (prelude && prelude.isActiveAndEnabled)
        {
            inPrelude = true; // the prelude drives the camera and the character itself
            yield return prelude.Play(this);
            inPrelude = false;
        }

        // 1. Fade in on the sad face, slow push-in.
        yield return null;   // let the character's idle pose apply, then frame the face where the head really is
        AimFaceShot();
        SetShot(shotFace);
        StartCoroutine(Fade(fadeImage, 1f, 0f, 2f));
        yield return Dolly(shotFace.position, shotFace.rotation,
                           shotFace.position + shotFace.forward * 0.35f, shotFace.rotation, faceShotTime);

        // 2. Cut over the shoulder: the faded reflection. Slow push-in continues through the thoughts.
        StartCoroutine(Dolly(shotShoulder.position, shotShoulder.rotation,
                             shotClose.position, shotClose.rotation, shoulderShotTime + thoughtsTime));
        yield return new WaitForSeconds(shoulderShotTime);

        // 3. The thoughts.
        int crackShown = 0;
        float elapsed = 0f, nextSpawn = 0f, nextBeat = 0f;
        int lingerShown = 0;
        while (elapsed < thoughtsTime)
        {
            float k = elapsed / thoughtsTime;
            distress = k;

            if (whisperSrc)
            {
                whisperSrc.volume = whisperMaxVolume * Mathf.Clamp01(k * 1.4f);
                whisperSrc.pitch = 0.9f + 0.25f * k;
            }
            if (musicSrc) musicSrc.volume = musicVolume * (1f - 0.6f * k);
            if (elapsed >= nextBeat)
            {
                if (sfx && heartbeatClip) sfx.PlayOneShot(heartbeatClip, 0.5f + 0.5f * k);
                nextBeat = elapsed + Mathf.Lerp(1.1f, 0.42f, k);
            }

            // the question that stays: early on, and again when the noise is loudest
            if (lingerShown == 0 && k >= 0.06f || lingerShown == 1 && k >= 0.55f) { SpawnThought(k, lingeringThought, true); lingerShown++; }

            if (elapsed >= nextSpawn)
            {
                SpawnThought(k);
                if (k > 0.75f && live.Count < 14) SpawnThought(k); // they start piling on
                nextSpawn = elapsed + Mathf.Lerp(0.85f, 0.22f, k); // slow enough that each one can be read
            }

            SetSmoke(Mathf.Lerp(4f, 90f, k));
            for (int i = 0; i < roomLights.Length; i++)
                if (roomLights[i]) roomLights[i].intensity = lightBase[i] * Mathf.Lerp(1f, 0.3f, k);
            if (darkVolume) darkVolume.weight = Mathf.Lerp(0.15f, 1f, k);
            shake = Mathf.Lerp(0f, 0.035f, k * k);
            headBow = Mathf.Lerp(0f, 18f, k);
            if (head && !headAnimated) head.localRotation = headBase * Quaternion.Euler(headBow, 0f, 0f);

            // Cracks spread over the last 45 %.
            int crackTarget = Mathf.FloorToInt(Mathf.InverseLerp(0.55f, 1f, k) * crackSegments.Count);
            if (crackTarget > crackShown)
            {
                if (crackShown == 0) Play(crackClip);
                while (crackShown < crackTarget) crackSegments[crackShown++].SetActive(true);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
        foreach (var c in crackSegments) c.SetActive(true);

        // 4. Shatter. Everything else cuts out: the impact lands in silence.
        Play(shatterClip);
        if (whisperSrc) whisperSrc.Stop();
        if (musicSrc) musicSrc.Stop();
        if (roomSrc) roomSrc.volume = roomToneVolume * 0.3f;
        Shatter();
        StartCoroutine(Fade(flashImage, 0.9f, 0f, 0.7f));
        shake = 0.14f;
        Time.timeScale = 0.3f;
        Time.fixedDeltaTime = fixedDtBase * Time.timeScale;
        yield return new WaitForSecondsRealtime(1.6f);
        Time.timeScale = 1f;
        Time.fixedDeltaTime = fixedDtBase;
        shake = 0.02f;
        yield return new WaitForSeconds(0.6f);

        // 5. Fade to black, caption, load the split screen.
        yield return End(false);
    }

    IEnumerator End(bool skipped)
    {
        ending = true;
        if (skipHint) skipHint.enabled = false;
        float fadeTime = skipped ? 0.5f : 1.8f;
        StartCoroutine(FadeSource(roomSrc, 0f, fadeTime));
        StartCoroutine(FadeSource(musicSrc, 0f, fadeTime));
        StartCoroutine(FadeSource(whisperSrc, 0f, fadeTime));
        yield return Fade(fadeImage, fadeImage ? fadeImage.color.a : 0f, 1f, fadeTime);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = fixedDtBase;
        shake = 0f;
        foreach (var th in live) if (th.t) Destroy(th.t.gameObject);
        live.Clear();
        SetSmoke(0f);

        if (!skipped && captionText)
        {
            yield return Fade(captionText, 0f, 1f, 1.2f);
            yield return new WaitForSecondsRealtime(captionHoldTime);
            yield return Fade(captionText, 1f, 0f, 1.2f);
        }

        if (!Application.CanStreamedLevelBeLoaded(nextScene))
        {
            Debug.LogError($"[MirrorCutscene] Scene '{nextScene}' is not in the build scene list. " +
                           "Add it under File > Build Profiles > Scene List.");
            yield break;
        }
        SceneFadeIn.Request();
        SceneManager.LoadScene(nextScene);
    }

    // ------------------------------------------------------------------ per frame

    void Update()
    {
        if (!ending && SkipPressed())
        {
            if (main != null) StopCoroutine(main);
            StopAllCoroutines();
            StartCoroutine(End(true));
        }

        // Camera with shake (not during the café prelude, which moves the camera and him itself).
        if (inPrelude) { UpdateThoughts(); return; }
        if (cam)
        {
            float t = Time.unscaledTime;
            Vector3 off = new Vector3(Mathf.PerlinNoise(t * 9f, 0.1f) - 0.5f,
                                      Mathf.PerlinNoise(0.3f, t * 9f) - 0.5f, 0f) * 2f * shake;
            Quaternion rot = Quaternion.Euler((Mathf.PerlinNoise(t * 7f, 5f) - 0.5f) * shake * 40f,
                                              (Mathf.PerlinNoise(9f, t * 7f) - 0.5f) * shake * 40f, 0f);
            cam.transform.SetPositionAndRotation(camPos + camRot * off, camRot * rot);
        }

        // Breathing + trembling.
        if (character)
        {
            float breath = Mathf.Sin(Time.time * 1.4f) * 0.012f;
            character.localScale = new Vector3(charBaseScale.x, charBaseScale.y * (1f + breath), charBaseScale.z);
            Vector3 tremble = new Vector3(Mathf.PerlinNoise(Time.time * 20f, 1f) - 0.5f, 0f,
                                          Mathf.PerlinNoise(2f, Time.time * 20f) - 0.5f) * 0.02f * distress;
            character.localPosition = charBasePos + tremble;
        }
        if (head && reflectionHead && !headAnimated) reflectionHead.localRotation = head.localRotation;

        UpdateThoughts();
    }

    // With a rigged, animated character the bow is added on top of the animation, after it has run.
    void LateUpdate()
    {
        if (!headAnimated || headBow <= 0.01f) return;
        if (head && character) head.rotation = Quaternion.AngleAxis(headBow, character.right) * head.rotation;
        if (reflectionHead && reflection)
            reflectionHead.rotation = Quaternion.AngleAxis(headBow, reflection.transform.right) * reflectionHead.rotation;
    }

    static bool SkipPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame ||
                           kb.escapeKey.wasPressedThisFrame)) return true;
        var gp = Gamepad.current;
        return gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame);
    }

    // ------------------------------------------------------------------ thoughts

    void SpawnThought(float k, string text = null, bool linger = false)
    {
        if (thoughts == null || thoughts.Length == 0 || !head) return;

        var go = new GameObject("Thought");
        var tm = go.AddComponent<TextMesh>();
        if (linger && string.IsNullOrEmpty(text)) return;
        tm.text = text ?? thoughts[Random.Range(0, thoughts.Length)];
        tm.font = font;
        tm.fontSize = 96;
        tm.characterSize = linger ? 0.0085f : Random.Range(0.006f, 0.011f) * (1f + k * 0.7f);
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        go.GetComponent<MeshRenderer>().sharedMaterial = ThoughtMaterial();

        Vector3 headPos = head.position + Vector3.up * 0.17f;
        Vector3 offset = linger ? new Vector3(Random.Range(-0.15f, 0.15f), 0.42f, 0.6f)
                                : new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-0.3f, 0.75f), Random.Range(0.05f, 1.2f));
        go.transform.position = headPos + offset;
        if (linger && cam) // centred near the top of the frame, a fixed distance from the camera
            go.transform.position = cam.transform.position + cam.transform.forward * 1.9f + cam.transform.up * 0.36f;

        var th = new Thought
        {
            t = go.transform,
            tm = tm,
            color = linger ? new Color(0.92f, 0.9f, 0.95f) : Random.value < 0.65f ? thoughtColor : thoughtColorAlt,
            vel = linger ? Vector3.up * 0.01f : (offset.normalized * 0.12f + Vector3.up * 0.05f) * Random.Range(0.5f, 1.5f),
            life = linger ? lingeringLife : Random.Range(ReadTime + 0.85f, ReadTime + 2f),
            seed = Random.value * 100f
        };
        tm.color = new Color(th.color.r, th.color.g, th.color.b, 0f);
        live.Add(th);
    }

    void UpdateThoughts()
    {
        Vector3 camP = cam ? cam.transform.position : Vector3.zero;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var th = live[i];
            if (!th.t) { live.RemoveAt(i); continue; }

            th.age += Time.deltaTime;
            if (th.age >= th.life) { Destroy(th.t.gameObject); live.RemoveAt(i); continue; }

            // quick fade in, then fully there for at least ReadTime, then fade out
            float a = Mathf.Clamp01(th.age / 0.25f) * Mathf.Clamp01((th.life - th.age) / 0.6f);
            a *= 0.88f + 0.12f * Mathf.PerlinNoise(th.seed, Time.time * 8f); // a slight flicker
            th.tm.color = new Color(th.color.r, th.color.g, th.color.b, a);

            Vector3 jitter = new Vector3(Mathf.PerlinNoise(th.seed, Time.time * 15f) - 0.5f,
                                         Mathf.PerlinNoise(Time.time * 15f, th.seed) - 0.5f, 0f) * 0.01f * distress;
            th.t.position += th.vel * Time.deltaTime + jitter;
            th.t.rotation = Quaternion.LookRotation(th.t.position - camP);
        }
    }

    // ------------------------------------------------------------------ mirror

    void BuildCracks()
    {
        if (!mirrorGlass || !crackMaterial) return;
        Transform g = mirrorGlass.transform;
        Vector2 half = new Vector2(g.lossyScale.x, g.lossyScale.y) * 0.5f;

        crackRoot = new GameObject("Cracks").transform;
        crackRoot.SetPositionAndRotation(g.position, g.rotation);

        Vector2 start = new Vector2(impactPoint.x * g.lossyScale.x, impactPoint.y * g.lossyScale.y);
        var rays = new List<List<GameObject>>();
        const int rayCount = 10;
        for (int r = 0; r < rayCount; r++)
        {
            var ray = new List<GameObject>();
            float ang = r * (360f / rayCount) + Random.Range(-14f, 14f);
            Vector2 p = start;
            for (int s = 0; s < 4; s++)
            {
                float len = Random.Range(0.12f, 0.32f);
                Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                Vector2 q = p + dir * len;
                q.x = Mathf.Clamp(q.x, -half.x, half.x);
                q.y = Mathf.Clamp(q.y, -half.y, half.y);
                float realLen = (q - p).magnitude;
                if (realLen < 0.02f) break;

                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(seg.GetComponent<Collider>());
                seg.name = "Crack";
                seg.transform.SetParent(crackRoot, false);
                seg.transform.localPosition = new Vector3((p.x + q.x) * 0.5f, (p.y + q.y) * 0.5f, -0.006f);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
                seg.transform.localScale = new Vector3(realLen, Random.Range(0.006f, 0.012f), 0.004f);
                seg.GetComponent<Renderer>().sharedMaterial = crackMaterial;
                seg.SetActive(false);
                ray.Add(seg);

                p = q;
                ang += Random.Range(-25f, 25f);
            }
            rays.Add(ray);
        }
        // Reveal order: grow all rays outward together (segment 0 of each ray, then 1, ...).
        for (int s = 0; s < 4; s++)
            foreach (var ray in rays)
                if (s < ray.Count) crackSegments.Add(ray[s]);
    }

    void Shatter()
    {
        if (!mirrorGlass) return;
        Transform g = mirrorGlass.transform;
        Vector3 impact = g.TransformPoint(new Vector3(impactPoint.x, impactPoint.y, 0f));
        Vector3 towardViewer = -g.forward;

        mirrorGlass.enabled = false;
        if (crackRoot) crackRoot.gameObject.SetActive(false);
        if (reflection) reflection.SetActive(false);

        for (int i = 0; i < shardCount; i++)
        {
            Vector3 local = new Vector3(Random.Range(-0.48f, 0.48f), Random.Range(-0.48f, 0.48f), 0f);
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "Shard";
            shard.transform.SetPositionAndRotation(g.TransformPoint(local),
                                                   g.rotation * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            shard.transform.localScale = new Vector3(Random.Range(0.05f, 0.2f), Random.Range(0.05f, 0.26f), 0.012f);
            if (shardMaterial) shard.GetComponent<Renderer>().sharedMaterial = shardMaterial;

            var rb = shard.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            Vector3 away = (shard.transform.position - impact).normalized;
            rb.linearVelocity = away * Random.Range(1f, 3f) + towardViewer * Random.Range(1.5f, 4f) +
                                Vector3.up * Random.Range(0f, 1.5f);
            rb.angularVelocity = Random.insideUnitSphere * 12f;
        }

        // Thoughts burst outward and die.
        foreach (var th in live)
        {
            if (!th.t) continue;
            th.vel = (th.t.position - impact).normalized * 3f;
            th.life = Mathf.Min(th.life, th.age + 0.6f);
        }
        if (darkSmoke) darkSmoke.Emit(150);
        SetSmoke(0f);
    }

    // ------------------------------------------------------------------ helpers

    [Header("Face shot framing")]
    [Tooltip("Aim the first shot at the character's real head at runtime (works for any character model).")]
    public bool aimFaceShotAtHead = true;
    [Tooltip("Camera offset from the face: x = to the character's right, y = up, z = in front.")]
    public Vector3 faceShotOffset = new Vector3(0.4f, -0.18f, 0.85f);
    [Tooltip("How far above the head bone the face centre is (metres).")]
    public float faceAimUp = 0.1f;

    void AimFaceShot()
    {
        if (!aimFaceShotAtHead || !shotFace) return;
        if (!head)
        {
            Debug.LogWarning("[MirrorCutscene] Face shot: no head assigned, keeping the built-in camera position.");
            return;
        }
        Transform basis = character ? character : head;
        Vector3 fwd = Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 face = head.position + Vector3.up * faceAimUp + fwd * 0.06f;
        Vector3 pos = face + right * faceShotOffset.x + Vector3.up * faceShotOffset.y + fwd * faceShotOffset.z;
        shotFace.SetPositionAndRotation(pos, Quaternion.LookRotation(face - pos));
        Debug.Log($"[MirrorCutscene] Face shot aimed at '{head.name}' at {face:F2}, camera at {pos:F2}.");
    }

    void SetShot(Transform shot)
    {
        if (!shot) return;
        camPos = shot.position;
        camRot = shot.rotation;
    }

    IEnumerator Dolly(Vector3 p0, Quaternion r0, Vector3 p1, Quaternion r1, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            camPos = Vector3.Lerp(p0, p1, k);
            camRot = Quaternion.Slerp(r0, r1, k);
            yield return null;
        }
        camPos = p1;
        camRot = r1;
    }

    static IEnumerator Fade(Graphic g, float from, float to, float duration)
    {
        if (!g) yield break;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            SetAlpha(g, Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetAlpha(g, to);
    }

    static void SetAlpha(Graphic g, float a)
    {
        if (!g) return;
        var c = g.color;
        c.a = a;
        g.color = c;
    }

    void SetSmoke(float rate)
    {
        if (!darkSmoke) return;
        var em = darkSmoke.emission;
        em.rateOverTime = rate;
    }

    void Play(AudioClip clip)
    {
        if (sfx && clip) sfx.PlayOneShot(clip);
    }

    void OnDisable()
    {
        Time.timeScale = 1f;
        if (fixedDtBase > 0f) Time.fixedDeltaTime = fixedDtBase;
    }
}
