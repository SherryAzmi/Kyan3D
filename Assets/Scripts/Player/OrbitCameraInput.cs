using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Drives a Cinemachine OrbitalFollow camera from ONE player's PlayerInputReader.
/// Put this on the CinemachineCamera (the one with OrbitalFollow).
/// While that player is using something (PlayerInteractor.HasFocus: holding Power on it, or just
/// after a tap), the stick/mouse is ignored and the camera swings round behind the player's shoulder,
/// moves in, and aims between the player and the object so both stay in frame.
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

    [Header("Focus while interacting")]
    public bool focusOnInteract = true;
    [Tooltip("Camera distance while focused (normal distance is the orbit's Radius).")]
    public float focusRadius = 4.2f;
    [Tooltip("Camera pitch while focused (degrees, higher = looking down more).")]
    public float focusPitch = 24f;
    [Tooltip("How far round to the side of the player the camera sits, so the player doesn't block the object (degrees).")]
    public float focusSideAngle = 30f;
    [Tooltip("0 = aim at the player, 1 = aim at the object.")]
    [Range(0f, 1f)] public float focusAimBias = 0.55f;
    [Tooltip("How quickly the camera swings onto the object (higher = faster).")]
    public float focusSpeed = 5f;

    CinemachineOrbitalFollow orbital;
    CinemachineCamera vcam;
    PlayerInteractor interactor;
    Transform lookTarget, aim;
    Vector3 aimOffset;
    float baseRadius, focusWeight, side = 1f;
    bool wasFocused;

    void Awake()
    {
        orbital = GetComponent<CinemachineOrbitalFollow>();
        vcam = GetComponent<CinemachineCamera>();
        baseRadius = orbital.Radius;
    }

    void Start()
    {
        if (input)
            foreach (var pi in FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None))
                if (pi.input == input) { interactor = pi; break; }

        // Aim at a point that rides on the player and can slide toward the object being used.
        if (vcam && vcam.LookAt)
        {
            lookTarget = vcam.LookAt;
            aim = new GameObject(name + " FocusAim").transform;
            aim.SetParent(lookTarget, false);
            vcam.LookAt = aim;
        }
    }

    void Update()
    {
        if (input == null) return;
        float dt = Time.deltaTime;

        bool focused = focusOnInteract && interactor && interactor.isActiveAndEnabled && interactor.HasFocus;
        focusWeight = Mathf.MoveTowards(focusWeight, focused ? 1f : 0f, dt * focusSpeed * 0.6f);
        float w = Mathf.SmoothStep(0f, 1f, focusWeight);
        orbital.Radius = Mathf.Lerp(baseRadius, focusRadius, w);

        Vector3 wantOffset = Vector3.zero;
        if (focused && lookTarget)
            wantOffset = (interactor.FocusPoint - lookTarget.position) * focusAimBias;
        aimOffset = Vector3.Lerp(aimOffset, wantOffset, 1f - Mathf.Exp(-focusSpeed * dt));
        if (aim) aim.localPosition = lookTarget.InverseTransformVector(aimOffset);

        if (focused)
        {
            Vector3 d = interactor.FocusPoint - interactor.transform.position;
            d.y = 0f;
            var h = orbital.HorizontalAxis;
            if (d.sqrMagnitude > 0.04f)
            {
                float heading = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                // Stay on whichever side of the player the camera already is, so it swings the short way.
                if (!wasFocused) side = Mathf.DeltaAngle(heading, h.Value) >= 0f ? 1f : -1f;
                float want = heading + side * focusSideAngle;
                h.Value += Mathf.DeltaAngle(h.Value, want) * (1f - Mathf.Exp(-focusSpeed * dt));
                h.Value = WrapH(h, h.Value);
                orbital.HorizontalAxis = h;
            }
            var v = orbital.VerticalAxis;
            v.Value = Mathf.Lerp(v.Value, Mathf.Clamp(focusPitch, v.Range.x, v.Range.y), 1f - Mathf.Exp(-focusSpeed * dt));
            orbital.VerticalAxis = v;
            wasFocused = true;
            return; // the player can't look around while using something
        }
        wasFocused = false;

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
            dx = look.x * gamepadSpeedX * dt;
            dy = look.y * gamepadSpeedY * dt;
        }
        if (!invertY) dy = -dy; // mouse/stick up = camera looks up (orbit goes down)

        // Horizontal: wrap around 360
        var hz = orbital.HorizontalAxis;
        hz.Value = WrapH(hz, hz.Value + dx);
        orbital.HorizontalAxis = hz;

        // Vertical: clamp
        var vt = orbital.VerticalAxis;
        vt.Value = Mathf.Clamp(vt.Value + dy, vt.Range.x, vt.Range.y);
        orbital.VerticalAxis = vt;
    }

    static float WrapH(InputAxis h, float hv)
    {
        float span = h.Range.y - h.Range.x;
        if (h.Wrap && span > 0f) return Mathf.Repeat(hv - h.Range.x, span) + h.Range.x;
        return Mathf.Clamp(hv, h.Range.x, h.Range.y);
    }
}
