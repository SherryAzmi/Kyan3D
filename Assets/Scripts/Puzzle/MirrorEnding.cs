using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The ending, played when the mirror is whole:
///  1. The split-screen divider slides away into white.
///  2. One full-screen room: the floor is half desert, half forest, the whole mirror stands on the seam.
///  3. The Present walks in from the desert side, the Future from the forest side.
///  4. The mirror shows ONE reflection whose colour blends from grey to gold. Final note.
///  5. They step together and become one. Closing message, then Play again / Quit.
/// </summary>
public class MirrorEnding : MonoBehaviour
{
    [Header("Camera")]
    public Camera endingCam;
    public Transform camStart, camEnd;

    [Header("Figures")]
    public Transform presentFig;
    public Transform futureFig;
    public Transform presentStand, futureStand, meetPoint;
    public Renderer reflectionFig;
    [Tooltip("Character models in the reflection. If set, the reflection turns from the Present into the Future instead of changing colour.")]
    public GameObject reflectionPresent;
    public GameObject reflectionFuture;
    public GameObject wholeFig;
    public Color presentColor = new Color(0.62f, 0.59f, 0.54f);
    public Color futureColor = new Color(1f, 0.72f, 0.35f);
    public Color wholeColor = new Color(0.9f, 0.72f, 0.55f);
    public Light mirrorLight;

    [Header("Turn these off during the ending")]
    public Behaviour[] disableOnPlay;
    public Canvas[] hideCanvases;
    public RectTransform splitDivider;

    [Header("UI")]
    public Image whiteImage;
    public Image noteBackdrop;
    public Text headerText;
    public Text noteText;
    public Text closingText;
    public CanvasGroup buttons;
    public Button playAgainButton;
    public Button quitButton;

    [Header("Text")]
    public string header = "Mirror Fragment 3 of 3  ·  Making peace with oneself";
    [TextArea(3, 6)]
    public string note = "The tired me and the hopeful me were never two different people.\nI don't have to leave one behind to become the other.\nI can hold both, and still be whole.";
    [TextArea(2, 4)]
    public string closing = "Loving yourself is a journey.\nYou don't have to become someone else to deserve it.";
    public float noteHoldTime = 6f;

    [Header("Play again loads this scene")]
    public string firstScene = "Cutscene";
    [Tooltip("After the closing message, load this scene (the café ending). Empty = show Play again / Quit here.")]
    public string thenLoad = "CafeEnding";

    bool playing;
    float camT, camDuration = 30f;
    Material reflMat, wholeMat;
    GameObject reflectionRoot;

    bool ModelReflection => reflectionPresent && reflectionFuture;

    void Awake()
    {
        NotePanel.Replace(ref headerText, ref noteText, ref noteBackdrop); // the note sits on a paper panel
        if (endingCam) endingCam.enabled = false;
        if (wholeFig) wholeFig.SetActive(false);
        if (ModelReflection) reflectionRoot = reflectionPresent.transform.parent.gameObject;
        else if (reflectionFig) { reflMat = reflectionFig.material; reflectionRoot = reflectionFig.gameObject; }
        if (reflectionRoot) reflectionRoot.SetActive(false);
        if (wholeFig && !wholeFig.GetComponentInChildren<Animator>()) wholeMat = wholeFig.GetComponentInChildren<Renderer>().material;
        SetAlpha(whiteImage, 0f);
        SetAlpha(noteBackdrop, 0f);
        SetAlpha(headerText, 0f);
        SetAlpha(noteText, 0f);
        SetAlpha(closingText, 0f);
        if (buttons) { buttons.alpha = 0f; buttons.interactable = false; buttons.blocksRaycasts = false; }
        if (playAgainButton) playAgainButton.onClick.AddListener(PlayAgain);
        if (quitButton) quitButton.onClick.AddListener(Quit);
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
        // the finale has its own piano theme (falls back to the ending music)
        if (GameAudio.I) GameAudio.ChangeMusic(GameAudio.I.finaleMusic ? GameAudio.I.finaleMusic : GameAudio.I.endingMusic, 4f, 0.25f);
        if (headerText) headerText.text = header;
        if (noteText) noteText.text = note;
        if (closingText) closingText.text = closing;

        // 1. The mirrors flare, the divider slides away into white.
        yield return new WaitForSeconds(0.8f);
        float divW = splitDivider ? splitDivider.sizeDelta.x : 0f;
        for (float t = 0f; t < 1.4f; t += Time.deltaTime)
        {
            float k = t / 1.4f;
            if (splitDivider) splitDivider.sizeDelta = new Vector2(Mathf.Lerp(divW, 0f, k), splitDivider.sizeDelta.y);
            SetAlpha(whiteImage, k);
            yield return null;
        }
        SetAlpha(whiteImage, 1f);

        // 2. One room.
        foreach (var c in hideCanvases) if (c) c.enabled = false;
        if (endingCam) endingCam.enabled = true;
        camT = 0f;
        UpdateCamera();
        yield return new WaitForSeconds(0.4f);
        StartCoroutine(Fade(whiteImage, 1f, 0f, 1.8f));

        // 3. They walk in from their own sides.
        Vector3 pFrom = presentFig.position, fFrom = futureFig.position;
        for (float t = 0f; t < 3.5f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 3.5f);
            float bob = Mathf.Abs(Mathf.Sin(t * 6f)) * 0.05f * (1f - k);
            presentFig.position = Vector3.Lerp(pFrom, presentStand.position, k) + Vector3.up * bob;
            futureFig.position = Vector3.Lerp(fFrom, futureStand.position, k) + Vector3.up * bob;
            yield return null;
        }

        // 4. One reflection, blending from the Present to the Future. The note.
        if (reflectionRoot) reflectionRoot.SetActive(true);
        StartCoroutine(BlendReflection(noteHoldTime + 3f));
        yield return new WaitForSeconds(1f);
        StartCoroutine(Fade(noteBackdrop, 0f, 0.5f, 1f));
        yield return Fade(headerText, 0f, 1f, 1f);
        // the last note is written like a letter too
        LetterWriter.UseHandwriting(noteText);
        SetAlpha(noteText, 1f);
        yield return LetterWriter.Write(noteText, note);
        yield return new WaitForSeconds(noteHoldTime);
        StartCoroutine(Fade(headerText, 1f, 0f, 1f));
        StartCoroutine(Fade(noteBackdrop, 0.5f, 0f, 1f));
        yield return Fade(noteText, 1f, 0f, 1f);

        // 5. They step together and become one.
        Vector3 pAt = presentFig.position, fAt = futureFig.position;
        for (float t = 0f; t < 1.6f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.6f);
            presentFig.position = Vector3.Lerp(pAt, meetPoint.position, k);
            futureFig.position = Vector3.Lerp(fAt, meetPoint.position, k);
            yield return null;
        }
        yield return Fade(whiteImage, 0f, 0.9f, 0.3f);
        presentFig.gameObject.SetActive(false);
        futureFig.gameObject.SetActive(false);
        if (wholeFig) { wholeFig.transform.position = meetPoint.position; wholeFig.SetActive(true); }
        if (wholeMat) wholeMat.SetColor("_BaseColor", wholeColor);
        if (reflMat) reflMat.SetColor("_BaseColor", wholeColor);
        if (mirrorLight) mirrorLight.intensity *= 1.6f;
        yield return Fade(whiteImage, 0.9f, 0f, 1.2f);

        // 6. Closing message, then the buttons.
        yield return new WaitForSeconds(0.6f);
        yield return Fade(closingText, 0f, 1f, 2f);

        // 7. Then the café: he wakes up at the mirror, takes a coffee, sits down. "The End" is there.
        if (!string.IsNullOrEmpty(thenLoad) && Application.CanStreamedLevelBeLoaded(thenLoad))
        {
            yield return new WaitForSeconds(3.5f);
            yield return Fade(whiteImage, 0f, 1f, 2f);
            yield return new WaitForSeconds(0.3f);
            SceneManager.LoadScene(thenLoad);
            yield break;
        }

        yield return new WaitForSeconds(1.5f);
        if (buttons)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime) { buttons.alpha = t; yield return null; }
            buttons.alpha = 1f;
            buttons.interactable = true;
            buttons.blocksRaycasts = true;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (EventSystem.current && playAgainButton) EventSystem.current.SetSelectedGameObject(playAgainButton.gameObject);
    }

    IEnumerator BlendReflection(float duration)
    {
        if (ModelReflection)
        {
            // The tired reflection slowly becomes the hopeful one: same person, same place.
            reflectionPresent.SetActive(true);
            reflectionFuture.SetActive(false);
            yield return new WaitForSeconds(duration * 0.55f);
            Transform t = reflectionRoot.transform;
            Vector3 s0 = t.localScale;
            for (float k = 0f; k < 0.25f; k += Time.deltaTime) { t.localScale = s0 * (1f - 0.08f * k / 0.25f); yield return null; }
            reflectionPresent.SetActive(false);
            reflectionFuture.SetActive(true);
            if (mirrorLight) mirrorLight.intensity *= 1.3f;
            for (float k = 0f; k < 0.25f; k += Time.deltaTime) { t.localScale = s0 * (0.92f + 0.08f * k / 0.25f); yield return null; }
            t.localScale = s0;
            yield break;
        }
        if (!reflMat) yield break;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            // grey -> gold, settling on the blend of both
            Color c = k < 0.7f ? Color.Lerp(presentColor, futureColor, k / 0.7f)
                               : Color.Lerp(futureColor, wholeColor, (k - 0.7f) / 0.3f);
            reflMat.SetColor("_BaseColor", c);
            yield return null;
        }
    }

    void Update()
    {
        if (!playing || !endingCam || !endingCam.enabled) return;
        camT += Time.deltaTime;
        UpdateCamera();
    }

    void UpdateCamera()
    {
        if (!camStart || !camEnd || !endingCam) return;
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(camT / camDuration));
        endingCam.transform.SetPositionAndRotation(Vector3.Lerp(camStart.position, camEnd.position, k),
                                                   Quaternion.Slerp(camStart.rotation, camEnd.rotation, k));
    }

    void PlayAgain()
    {
        if (GameAudio.I) GameAudio.Play(GameAudio.I.click);
        if (!Application.CanStreamedLevelBeLoaded(firstScene))
        {
            Debug.LogError($"[MirrorEnding] Scene '{firstScene}' is not in the build scene list.");
            return;
        }
        SceneFadeIn.Request();
        SceneManager.LoadScene(firstScene);
    }

    void Quit()
    {
        if (GameAudio.I) GameAudio.Play(GameAudio.I.click);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static IEnumerator Fade(Graphic g, float from, float to, float duration)
    {
        if (!g) yield break;
        for (float t = 0f; t < duration; t += Time.deltaTime)
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

    void OnDestroy()
    {
        if (reflMat) Destroy(reflMat);
        if (wholeMat) Destroy(wholeMat);
    }
}
