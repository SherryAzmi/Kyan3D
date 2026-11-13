using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Plays after the Mirror Fragment is collected: controls stop, the split screen fades into one
/// full-screen camera in the desert, a flower grows from the sand, warm light spreads, a short
/// self-acceptance note appears, then everything fades to black and the next scene loads.
/// </summary>
public class FragmentCutscene : MonoBehaviour
{
    [Header("Camera")]
    public Camera cinematicCam;
    [Tooltip("The camera orbits around this point (the flower).")]
    public Transform focus;
    public float orbitRadius = 3.8f;
    public float orbitHeight = 1.25f;
    [Tooltip("Degrees around the flower; 0 = +Z. 125..165 keeps the Present beside the flower, not in front of it.")]
    public float orbitStartAngle = 125f;
    public float orbitEndAngle = 165f;

    [Header("Flower")]
    public Transform flower;
    public Transform petals;
    public Light flowerLight;
    public float growTime = 4f;
    public Material rippleMaterial;

    [Header("World")]
    public Transform present;
    public Light sun;
    public Color sunWarmColor = new Color(1f, 0.86f, 0.68f);
    public float sunWarmIntensity = 1.7f;

    [Header("Turn these off during the cutscene")]
    public Behaviour[] disableOnPlay;
    public Canvas[] hideCanvases;

    [Header("UI")]
    public Image fadeImage;
    public Text headerText;
    public Text noteText;
    [Tooltip("Dark band behind the note so it reads on bright sand.")]
    public Image noteBackdrop;
    [Range(0f, 1f)] public float backdropAlpha = 0.5f;
    public string header = "Mirror Fragment 1 of 3  ·  Recognizing pain";
    [TextArea(3, 6)]
    public string note = "I was tired. I was allowed to be.\nHurting doesn't make me less worthy of love.\nI'm still here, and that's a start.";
    public float noteHoldTime = 6f;

    [Header("Next scene (must be in Build Profiles scene list)")]
    public string nextScene = "Puzzle2";

    bool playing;
    float orbitT;
    float orbitDuration = 1f;
    Vector3 petalScale;

    void Awake()
    {
        if (cinematicCam) cinematicCam.enabled = false;
        if (flower) flower.localScale = Vector3.zero;
        if (petals) { petalScale = petals.localScale; petals.localScale = Vector3.zero; }
        if (flowerLight) flowerLight.intensity = 0f;
        SetAlpha(fadeImage, 0f);
        SetAlpha(headerText, 0f);
        SetAlpha(noteText, 0f);
        SetAlpha(noteBackdrop, 0f);
    }

    public void Play()
    {
        if (playing) return;
        playing = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        foreach (var b in disableOnPlay) if (b) b.enabled = false;
        if (headerText) headerText.text = header;
        if (noteText) noteText.text = note;

        // 1. Out of the split screen.
        yield return Fade(fadeImage, 0f, 1f, 0.9f);
        foreach (var c in hideCanvases) if (c) c.enabled = false;
        if (present && focus)
        {
            Vector3 look = focus.position - present.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) present.rotation = Quaternion.LookRotation(look);
        }
        orbitDuration = 1.2f + growTime + 1.6f + noteHoldTime + 3.5f;
        orbitT = 0f;
        if (cinematicCam) cinematicCam.enabled = true;
        UpdateOrbit();

        // 2. Into the desert, the flower grows.
        StartCoroutine(Fade(fadeImage, 1f, 0f, 1.2f));
        yield return new WaitForSeconds(0.6f);
        if (focus) RippleFX.Spawn(focus.position + Vector3.up * 0.05f, rippleMaterial, 0f);
        Color sunFrom = sun ? sun.color : Color.white;
        float sunIntFrom = sun ? sun.intensity : 1f;
        for (float t = 0f; t < growTime; t += Time.deltaTime)
        {
            float k = t / growTime;
            float stem = EaseOutBack(Mathf.Clamp01(k / 0.6f));
            if (flower) flower.localScale = Vector3.one * stem;
            float bloom = EaseOutBack(Mathf.Clamp01((k - 0.45f) / 0.55f));
            if (petals) petals.localScale = petalScale * bloom;
            if (flowerLight) flowerLight.intensity = 2.5f * Mathf.Clamp01(k * 1.5f);
            if (sun)
            {
                sun.color = Color.Lerp(sunFrom, sunWarmColor, k);
                sun.intensity = Mathf.Lerp(sunIntFrom, sunWarmIntensity, k);
            }
            yield return null;
        }
        if (focus) RippleFX.Spawn(focus.position + Vector3.up * 0.05f, rippleMaterial, 0f);

        // 3. The note.
        StartCoroutine(Fade(noteBackdrop, 0f, backdropAlpha, 1f));
        yield return Fade(headerText, 0f, 1f, 1f);
        yield return new WaitForSeconds(0.4f);
        yield return Fade(noteText, 0f, 1f, 1.6f);
        yield return new WaitForSeconds(noteHoldTime);
        StartCoroutine(Fade(headerText, 1f, 0f, 1f));
        StartCoroutine(Fade(noteBackdrop, backdropAlpha, 0f, 1f));
        yield return Fade(noteText, 1f, 0f, 1f);

        // 4. Fade out, next scene.
        yield return Fade(fadeImage, 0f, 1f, 1.5f);
        if (!Application.CanStreamedLevelBeLoaded(nextScene))
        {
            Debug.LogError($"[FragmentCutscene] Scene '{nextScene}' is not in the build scene list. " +
                           "Add it under File > Build Profiles > Scene List.");
            yield break;
        }
        SceneFadeIn.Request();
        SceneManager.LoadScene(nextScene);
    }

    void Update()
    {
        if (!playing || !cinematicCam || !cinematicCam.enabled) return;
        orbitT += Time.deltaTime;
        UpdateOrbit();
    }

    void UpdateOrbit()
    {
        if (!focus || !cinematicCam) return;
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(orbitT / orbitDuration));
        float a = Mathf.Lerp(orbitStartAngle, orbitEndAngle, k) * Mathf.Deg2Rad;
        float r = Mathf.Lerp(orbitRadius, orbitRadius * 0.75f, k);
        Vector3 pos = focus.position + new Vector3(Mathf.Sin(a) * r, orbitHeight, Mathf.Cos(a) * r);
        cinematicCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(focus.position + Vector3.up * 0.45f - pos));
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
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
}
