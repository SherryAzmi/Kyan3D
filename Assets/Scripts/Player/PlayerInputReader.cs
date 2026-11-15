using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

public enum SoulType { Light, Void }

/// <summary>Keyboard controls used for testing/debugging when no controller is plugged in.</summary>
public enum KeyboardDebugLayout
{
    None,
    WasdMouse, // WASD move, mouse look (click to lock, Esc to unlock), Space jump, E interact, hold F power
    Arrows     // Arrows move, , . turn camera (Numpad 8/2 tilt), Enter jump, Right Shift interact, hold Right Ctrl power
}

/// <summary>
/// Reads input for ONE player.
/// Main control = PlayStation controller (any Gamepad works: DualShock 4, DualSense, Xbox...).
///   Light Soul (P1) = controller #1, Void Soul (P2) = controller #2 (by the order they were plugged in).
///   Left stick move · Right stick camera · Cross (X) jump · Square interact · hold R2 power.
/// Keyboard debug controls are always available as a fallback while testing.
/// </summary>
public class PlayerInputReader : MonoBehaviour
{
    public SoulType soul = SoulType.Light;

    [Tooltip("0 = first connected controller (P1 / Light), 1 = second (P2 / Void).")]
    public int gamepadIndex = 0;

    [Tooltip("Keyboard controls for testing without controllers.")]
    public KeyboardDebugLayout keyboardDebug = KeyboardDebugLayout.WasdMouse;

    [Tooltip("Colour of the PlayStation controller light bar for this player.")]
    public Color lightBarColor = new Color(1f, 0.7f, 0.15f);

    [Range(0f, 0.5f)] public float stickDeadZone = 0.15f;

    public Vector2 Move { get; private set; }
    /// <summary>Look input this frame. If LookIsMouseDelta, it's mouse pixels; otherwise stick (-1..1).</summary>
    public Vector2 Look { get; private set; }
    public bool LookIsMouseDelta { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool InteractPressed { get; private set; }
    /// <summary>True while the power button is held (dig / grow).</summary>
    public bool PowerHeld { get; private set; }

    /// <summary>The controller assigned to this player right now (null if not plugged in).</summary>
    public Gamepad Pad
    {
        get
        {
            var pads = Gamepad.all;
            return gamepadIndex < pads.Count ? pads[gamepadIndex] : null;
        }
    }

    /// <summary>
    /// Which keyboard layout this player uses right now, depending on how many controllers are plugged in:
    ///   0 controllers: the layouts set above (P1 WASD + mouse, P2 arrows), for testing.
    ///   1 controller:  Player 1 uses the controller only, Player 2 uses the keyboard and mouse (WASD + mouse).
    ///   2+ controllers: both players use their own controller; no keyboard.
    /// </summary>
    public KeyboardDebugLayout ActiveLayout => LayoutFor(gamepadIndex, keyboardDebug);

    public static KeyboardDebugLayout LayoutFor(int playerIndex, KeyboardDebugLayout configured)
    {
        int pads = Gamepad.all.Count;
        if (pads == 0) return configured;
        if (pads == 1) return playerIndex == 0 ? KeyboardDebugLayout.None : KeyboardDebugLayout.WasdMouse;
        return KeyboardDebugLayout.None;
    }

    Gamepad lastPad;

    void Update()
    {
        Move = Vector2.zero; Look = Vector2.zero; LookIsMouseDelta = false;
        JumpPressed = false; InteractPressed = false; PowerHeld = false;

        var pad = Pad;
        if (pad != lastPad) { OnPadChanged(pad); lastPad = pad; }

        if (PauseMenu.IsPaused) return; // pause / settings / help panel is open: ignore the game controls

        if (pad != null) ReadPad(pad);

        switch (ActiveLayout)
        {
            case KeyboardDebugLayout.WasdMouse: ReadWasdMouse(); break;
            case KeyboardDebugLayout.Arrows: ReadArrows(); break;
        }
    }

    void ReadPad(Gamepad pad)
    {
        Move = DeadZone(pad.leftStick.ReadValue());
        Look = DeadZone(pad.rightStick.ReadValue());
        JumpPressed = pad.buttonSouth.wasPressedThisFrame;    // Cross (X) on PlayStation
        InteractPressed = pad.buttonWest.wasPressedThisFrame; // Square on PlayStation
        PowerHeld = pad.rightTrigger.ReadValue() > 0.4f;       // R2
    }

    // Keyboard only fills in what the controller didn't, so both can be used at once while testing.
    void ReadWasdMouse()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb != null)
        {
            if (Move == Vector2.zero)
                Move = Axis(kb.aKey.isPressed, kb.dKey.isPressed, kb.sKey.isPressed, kb.wKey.isPressed);
            JumpPressed |= kb.spaceKey.wasPressedThisFrame;
            InteractPressed |= kb.eKey.wasPressedThisFrame;
            PowerHeld |= kb.fKey.isPressed;
            if (kb.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
        }
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) SetCursorLocked(true);
            if (Look == Vector2.zero && Cursor.lockState == CursorLockMode.Locked)
            {
                Look = mouse.delta.ReadValue();
                LookIsMouseDelta = Look != Vector2.zero;
            }
        }
    }

    void ReadArrows()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (Move == Vector2.zero)
            Move = Axis(kb.leftArrowKey.isPressed, kb.rightArrowKey.isPressed, kb.downArrowKey.isPressed, kb.upArrowKey.isPressed);
        if (Look == Vector2.zero)
            Look = Axis(kb.commaKey.isPressed || kb.numpad4Key.isPressed, kb.periodKey.isPressed || kb.numpad6Key.isPressed,
                        kb.numpad2Key.isPressed, kb.numpad8Key.isPressed);
        JumpPressed |= kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
        InteractPressed |= kb.rightShiftKey.wasPressedThisFrame;
        PowerHeld |= kb.rightCtrlKey.isPressed;
    }

    void OnPadChanged(Gamepad pad)
    {
        if (pad == null)
        {
            Debug.Log($"[Input] {soul} Soul: controller #{gamepadIndex + 1} not connected — using keyboard ({keyboardDebug}).");
            return;
        }
        Debug.Log($"[Input] {soul} Soul uses controller #{gamepadIndex + 1}: {pad.displayName} ({pad.layout})");
        if (pad is DualShockGamepad ps) ps.SetLightBarColor(lightBarColor);
    }

    void OnDisable()
    {
        if (lastPad is DualShockGamepad ps) ps.ResetHaptics();
        lastPad = null;
    }

    Vector2 DeadZone(Vector2 v) => v.magnitude < stickDeadZone ? Vector2.zero : v;

    static Vector2 Axis(bool left, bool right, bool down, bool up)
    {
        var v = new Vector2((right ? 1 : 0) - (left ? 1 : 0), (up ? 1 : 0) - (down ? 1 : 0));
        return Vector2.ClampMagnitude(v, 1f);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
