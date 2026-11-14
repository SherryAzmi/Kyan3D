using UnityEngine;

/// <summary>
/// Simple camera-relative third-person movement (CharacterController).
/// Uses this player's own camera so "forward" matches what they see on their half of the screen.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class SoulMovement : MonoBehaviour
{
    public PlayerInputReader input;
    [Tooltip("This player's output Camera (left or right half of the split screen).")]
    public Transform viewCamera;

    public float moveSpeed = 5f;
    public float turnSpeed = 720f;   // degrees per second
    public float jumpHeight = 1.2f;
    public float gravity = -20f;
    [Tooltip("A jump pressed this long before landing still happens when you land (seconds).")]
    public float jumpBuffer = 0.15f;
    [Tooltip("You can still jump this long after walking off an edge or a bump (seconds).")]
    public float coyoteTime = 0.12f;

    CharacterController cc;
    float verticalVelocity;
    float lastJumpPress = -99f, lastGrounded = -99f;

    PlayerInteractor interactor;

    /// <summary>The Present (P1, Light soul) is tired: walks slower than the Future. 0 = use moveSpeed as set.</summary>
    public static float PresentSpeed = 3f;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        interactor = GetComponent<PlayerInteractor>();
        if (PresentSpeed > 0f && input && input.soul == SoulType.Light) moveSpeed = PresentSpeed;
    }

    void Update()
    {
        // While using something (digging, growing, placing...) the player stands still and faces it.
        bool focused = interactor && interactor.isActiveAndEnabled && interactor.HasFocus;
        Vector2 m = input != null && !focused ? input.Move : Vector2.zero;

        // Camera-relative direction on the ground plane
        Vector3 fwd = viewCamera ? viewCamera.forward : Vector3.forward;
        Vector3 right = viewCamera ? viewCamera.right : Vector3.right;
        fwd.y = 0; right.y = 0;
        fwd.Normalize(); right.Normalize();
        Vector3 move = (fwd * m.y + right * m.x) * moveSpeed;

        if (move.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(move.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }
        else if (focused)
        {
            Vector3 to = interactor.FocusPoint - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.04f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(to.normalized, Vector3.up), turnSpeed * 0.6f * Time.deltaTime);
        }

        // Forgiving jump: remember the press for a moment and allow it shortly after leaving the ground,
        // so a jump is never "eaten" by a frame where the controller briefly isn't grounded.
        if (input != null && input.JumpPressed && !focused) lastJumpPress = Time.time;
        if (cc.isGrounded)
        {
            lastGrounded = Time.time;
            if (verticalVelocity < 0) verticalVelocity = -2f;
        }
        bool canJump = Time.time - lastGrounded <= coyoteTime && verticalVelocity <= 0f;
        if (canJump && Time.time - lastJumpPress <= jumpBuffer)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastJumpPress = -99f;
            lastGrounded = -99f;
        }
        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        cc.Move(move * Time.deltaTime);
    }
}
