using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Test driver for HoldPromptUI: hold a keyboard key (or Cross on a controller) to fill the
/// ring, let go to drain it, and it resets a moment after completing.
/// Remove this component once the real game drives the prompt (PuzzleHUD / PlayerInteractor).
/// </summary>
[RequireComponent(typeof(HoldPromptUI))]
public class HoldPromptDemo : MonoBehaviour
{
    [Tooltip("Keyboard key to hold.")]
    public Key holdKey = Key.X;
    [Tooltip("Controller whose Cross (X) button also works. 0 = first controller, 1 = second.")]
    public int gamepadIndex = 0;
    [Tooltip("Seconds of holding needed to complete.")]
    public float holdTime = 1.5f;
    [Tooltip("How fast the ring empties when the button is released early.")]
    public float drainSpeed = 2f;
    [Tooltip("Seconds the tick stays before the prompt resets.")]
    public float resetDelay = 1.2f;

    HoldPromptUI prompt;
    float progress;
    float completedAt = -1f;

    void Awake() => prompt = GetComponent<HoldPromptUI>();

    void Update()
    {
        if (completedAt >= 0f)
        {
            if (Time.unscaledTime - completedAt < resetDelay) return;
            completedAt = -1f;
            progress = 0f;
        }

        if (IsHeld()) progress += Time.unscaledDeltaTime / Mathf.Max(0.01f, holdTime);
        else progress -= Time.unscaledDeltaTime * drainSpeed;
        progress = Mathf.Clamp01(progress);

        prompt.SetProgress(progress);
        if (progress >= 1f) completedAt = Time.unscaledTime;
    }

    bool IsHeld()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[holdKey].isPressed) return true;
        var pads = Gamepad.all;
        return gamepadIndex >= 0 && gamepadIndex < pads.Count && pads[gamepadIndex].buttonSouth.isPressed;
    }
}
