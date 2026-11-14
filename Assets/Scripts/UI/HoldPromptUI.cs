using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Circular hold-to-interact prompt: a ring that fills clockwise from the top while the
/// player holds a button, then swaps the key letter for a tick when it reaches 100%.
/// Call SetProgress(0..1) every frame from whatever tracks the hold (e.g. PuzzleHUD).
/// Drag the Preview slider in the Inspector to see every state without pressing Play.
/// </summary>
public class HoldPromptUI : MonoBehaviour
{
    [Header("Parts (filled in by Kayyan > UI > Add Hold Prompts)")]
    public Image ring;
    public Image startCap;
    public Image endCap;
    [Tooltip("Centred on the prompt; rotating it carries the end cap around the ring.")]
    public RectTransform endCapPivot;
    public TMP_Text keyLabel;
    public Image check;

    [Header("Look")]
    public Color ringColor = new Color(0.94f, 0.91f, 0.85f);

    [Header("Preview")]
    [SerializeField, Range(0f, 1f)] float progress;

    public float Progress => progress;
    public bool IsComplete => progress >= 1f;

    /// <summary>0 = empty ring, 1 = full ring with the tick.</summary>
    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        Refresh();
    }

    public void SetKey(string key)
    {
        if (keyLabel) keyLabel.text = key;
    }

    void OnEnable() => Refresh();
    void OnValidate() => Refresh();

    void Refresh()
    {
        if (!ring) return;

        bool done = progress >= 1f;
        bool partial = progress > 0.001f && !done;

        ring.fillAmount = progress;
        ring.color = ringColor;

        // Round ends on the arc: one fixed at the top, one travelling with the fill.
        if (startCap) { startCap.enabled = partial; startCap.color = ringColor; }
        if (endCap) { endCap.enabled = partial; endCap.color = ringColor; }
        if (endCapPivot) endCapPivot.localEulerAngles = new Vector3(0f, 0f, -360f * progress);

        if (check) { check.enabled = done; check.color = ringColor; }
        if (keyLabel) keyLabel.enabled = !done;
    }
}
