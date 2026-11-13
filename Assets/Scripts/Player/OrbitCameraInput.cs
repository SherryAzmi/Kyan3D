using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Drives a Cinemachine OrbitalFollow camera from ONE player's PlayerInputReader.
/// Put this on the CinemachineCamera (the one with OrbitalFollow).
/// </summary>
[RequireComponent(typeof(CinemachineOrbitalFollow))]
public class OrbitCameraInput : MonoBehaviour
{
    public PlayerInputReader input;

    [Header("Mouse (degrees per pixel)")]
    public float mouseSensitivityX = 0.15f;
    public float mouseSensitivityY = 0.10f;

    [Header("Gamepad (degrees per second at full stick)")]
    public float gamepadSpeedX = 180f;
    public float gamepadSpeedY = 110f;

    public bool invertY = false;

    CinemachineOrbitalFollow orbital;

    void Awake() => orbital = GetComponent<CinemachineOrbitalFollow>();

    void Update()
    {
        if (input == null) return;

        Vector2 look = input.Look;
        if (look == Vector2.zero) return;

        float dx, dy;
        if (input.LookIsMouseDelta)
        {
            dx = look.x * mouseSensitivityX;
            dy = look.y * mouseSensitivityY;
        }
        else
        {
            dx = look.x * gamepadSpeedX * Time.deltaTime;
            dy = look.y * gamepadSpeedY * Time.deltaTime;
        }
        if (!invertY) dy = -dy; // mouse/stick up = camera looks up (orbit goes down)

        // Horizontal: wrap around 360
        var h = orbital.HorizontalAxis;
        float hv = h.Value + dx;
        float hSpan = h.Range.y - h.Range.x;
        if (h.Wrap && hSpan > 0f) hv = Mathf.Repeat(hv - h.Range.x, hSpan) + h.Range.x;
        else hv = Mathf.Clamp(hv, h.Range.x, h.Range.y);
        h.Value = hv;
        orbital.HorizontalAxis = h;

        // Vertical: clamp
        var v = orbital.VerticalAxis;
        v.Value = Mathf.Clamp(v.Value + dy, v.Range.x, v.Range.y);
        orbital.VerticalAxis = v;
    }
}
