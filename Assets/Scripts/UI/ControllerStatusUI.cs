using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Shows on a player's half of the screen whether their PlayStation controller is connected.
/// Hides itself a few seconds after the controller connects.
/// </summary>
public class ControllerStatusUI : MonoBehaviour
{
    public PlayerInputReader player;
    [Tooltip("Show the \"plug in controller\" / \"connected\" notice at the top of the half. Off by default.")]
    public bool showStatus = false;
    public Text label;
    [Tooltip("Hide the label this many seconds after the controller connects.")]
    public float hideAfterConnected = 4f;
    [Tooltip("Log every device connect/disconnect to the Console (enable on one instance only).")]
    public bool logDeviceChanges = false;

    float connectedTime = -1f;

    void OnEnable()
    {
        if (!logDeviceChanges) return;
        InputSystem.onDeviceChange += OnDeviceChange;
        foreach (var d in InputSystem.devices)
            Debug.Log($"[Input] Device present: {d.displayName}  ({d.layout})");
    }

    void OnDisable()
    {
        if (logDeviceChanges) InputSystem.onDeviceChange -= OnDeviceChange;
    }

    static void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed ||
            change == InputDeviceChange.Reconnected || change == InputDeviceChange.Disconnected)
            Debug.Log($"[Input] {change}: {device.displayName}  ({device.layout})");
    }

    void Update()
    {
        if (!label) return;
        if (!showStatus) { label.enabled = false; return; } // the red/green controller notice is hidden
        if (!player) return;
        var pad = player.Pad;
        string who = player.soul == SoulType.Light ? "P1 Present" : "P2 Future";

        if (pad == null)
        {
            connectedTime = -1f;
            label.enabled = true;
            label.color = new Color(1f, 0.55f, 0.45f);
            label.text = $"{who}: plug in controller #{player.gamepadIndex + 1}\n(keyboard test: {KeyboardHint(player.keyboardDebug)})";
        }
        else
        {
            if (connectedTime < 0f) connectedTime = Time.unscaledTime;
            label.color = new Color(0.6f, 1f, 0.6f);
            label.text = $"{who}: {pad.displayName} connected";
            label.enabled = Time.unscaledTime - connectedTime < hideAfterConnected;
        }
    }

    static string KeyboardHint(KeyboardDebugLayout l)
    {
        switch (l)
        {
            case KeyboardDebugLayout.WasdMouse: return "WASD + mouse, Space jump, E interact, hold F power";
            case KeyboardDebugLayout.Arrows: return "arrows, , . turn, Enter jump, R-Shift interact, hold R-Ctrl power";
            default: return "off";
        }
    }
}
