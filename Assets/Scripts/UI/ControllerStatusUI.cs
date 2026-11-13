using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Shows whether P2's phone controller is connected, on P2's half of the screen.
/// Also logs every device connect/disconnect to the Console (handy when pairing the phone).
/// </summary>
public class ControllerStatusUI : MonoBehaviour
{
    public PlayerInputReader player;
    public Text label;
    [Tooltip("Hide the label this many seconds after the controller connects.")]
    public float hideAfterConnected = 4f;

    float connectedTime = -1f;

    void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
        foreach (var d in InputSystem.devices)
            Debug.Log($"[Input] Device present: {d.displayName}  ({d.layout})");
    }

    void OnDisable() => InputSystem.onDeviceChange -= OnDeviceChange;

    void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed ||
            change == InputDeviceChange.Reconnected || change == InputDeviceChange.Disconnected)
            Debug.Log($"[Input] {change}: {device.displayName}  ({device.layout})");
    }

    void Update()
    {
        if (!label || !player) return;
        string name = player.ConnectedControllerName;

        if (name == null)
        {
            connectedTime = -1f;
            label.enabled = true;
            label.color = new Color(1f, 0.55f, 0.45f);
            label.text = "P2: no controller — connect the phone\n(testing: arrow keys, , . to turn, Right Ctrl jump)";
        }
        else
        {
            if (connectedTime < 0f) connectedTime = Time.unscaledTime;
            label.color = new Color(0.6f, 1f, 0.6f);
            label.text = "P2 controller connected: " + name;
            label.enabled = Time.unscaledTime - connectedTime < hideAfterConnected;
        }
    }
}
