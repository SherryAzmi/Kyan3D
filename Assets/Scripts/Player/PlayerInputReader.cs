using UnityEngine;
using UnityEngine.InputSystem;

public enum SoulType { Light, Void }

public enum InputScheme
{
    KeyboardMouse, // P1: WASD + mouse look + Space jump
    Gamepad        // P2: phone gamepad emulator — left stick move, right stick look, South button jump
}

/// <summary>
/// Reads input for ONE player from a fixed device, so two local players never share input.
/// Other scripts (movement, camera) read Move / Look / JumpPressed from here.
/// </summary>
public class PlayerInputReader : MonoBehaviour
{
    public SoulType soul = SoulType.Light;
    public InputScheme scheme = InputScheme.KeyboardMouse;

    [Tooltip("Which connected gamepad to use when scheme = Gamepad (0 = first gamepad found).")]
    public int gamepadIndex = 0;

    [Tooltip("Also allow arrow keys + Right Ctrl for the Gamepad player, so you can test without the phone.")]
    public bool keyboardFallbackForGamepad = true;

    public Vector2 Move { get; private set; }
    /// <summary>Look delta for this frame. Mouse = pixels, gamepad = stick (-1..1).</summary>
    public Vector2 Look { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool IsMouse => scheme == InputScheme.KeyboardMouse;

    void Update()
    {
        Move = Vector2.zero; Look = Vector2.zero; JumpPressed = false; InteractPressed = false;

        if (scheme == InputScheme.KeyboardMouse) ReadKeyboardMouse();
        else ReadGamepad();
    }

    void ReadKeyboardMouse()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        if (kb != null)
        {
            Vector2 m = Vector2.zero;
            if (kb.wKey.isPressed) m.y += 1;
            if (kb.sKey.isPressed) m.y -= 1;
            if (kb.dKey.isPressed) m.x += 1;
            if (kb.aKey.isPressed) m.x -= 1;
            Move = Vector2.ClampMagnitude(m, 1f);
            JumpPressed = kb.spaceKey.wasPressedThisFrame;
            InteractPressed = kb.eKey.wasPressedThisFrame;

            if (kb.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
        }

        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) SetCursorLocked(true);
            if (Cursor.lockState == CursorLockMode.Locked)
                Look = mouse.delta.ReadValue();
        }
    }

    /// <summary>Name of the controller P2 is using right now (null if none).</summary>
    public string ConnectedControllerName
    {
        get
        {
            var pads = UnityEngine.InputSystem.Gamepad.all;
            if (gamepadIndex < pads.Count) return pads[gamepadIndex].displayName;
            var sticks = Joystick.all;
            if (gamepadIndex < sticks.Count) return sticks[gamepadIndex].displayName + " (generic joystick)";
            return null;
        }
    }

    const float StickDeadZone = 0.15f;

    static Vector2 DeadZone(Vector2 v) => v.magnitude < StickDeadZone ? Vector2.zero : v;

    void ReadGamepad()
    {
        var pads = UnityEngine.InputSystem.Gamepad.all;
        if (gamepadIndex < pads.Count)
        {
            // Xbox / XInput / DualShock style — phone apps that emulate an Xbox 360 pad land here.
            var pad = pads[gamepadIndex];
            Move = DeadZone(pad.leftStick.ReadValue());
            Look = DeadZone(pad.rightStick.ReadValue());
            JumpPressed = pad.buttonSouth.wasPressedThisFrame;
            InteractPressed = pad.buttonWest.wasPressedThisFrame;
        }
        else if (gamepadIndex < Joystick.all.Count)
        {
            // Fallback: generic Bluetooth HID controller that Unity doesn't recognise as a Gamepad.
            var js = Joystick.all[gamepadIndex];
            Move = DeadZone(js.stick.ReadValue());
            JumpPressed = js.trigger.wasPressedThisFrame;
        }

        // Test without the phone: arrows move, Right Ctrl jumps, Right Shift interacts,
        // Numpad 4/6/8/2 (or , . for yaw) rotate the camera.
        var kb = Keyboard.current;
        if (keyboardFallbackForGamepad && kb != null && Move == Vector2.zero && Look == Vector2.zero)
        {
            Vector2 m = Vector2.zero;
            if (kb.upArrowKey.isPressed) m.y += 1;
            if (kb.downArrowKey.isPressed) m.y -= 1;
            if (kb.rightArrowKey.isPressed) m.x += 1;
            if (kb.leftArrowKey.isPressed) m.x -= 1;
            Move = Vector2.ClampMagnitude(m, 1f);

            Vector2 l = Vector2.zero;
            if (kb.numpad6Key.isPressed || kb.periodKey.isPressed) l.x += 1;
            if (kb.numpad4Key.isPressed || kb.commaKey.isPressed) l.x -= 1;
            if (kb.numpad8Key.isPressed) l.y += 1;
            if (kb.numpad2Key.isPressed) l.y -= 1;
            Look = l;

            JumpPressed |= kb.rightCtrlKey.wasPressedThisFrame;
            InteractPressed |= kb.rightShiftKey.wasPressedThisFrame;
        }
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
