using UnityEngine;

/// <summary>
/// Drives a character's Animator from what the character is doing. Works for players
/// (CharacterController + PlayerInteractor) and for scripted figures in cutscenes (it measures
/// how fast the transform moves).
/// Animator parameters: Speed (float, m/s), Grounded (bool), Action (bool = digging / growing / turning).
/// While carrying something, both arms are lifted forward on top of the animation.
/// </summary>
public class CharacterAnimator : MonoBehaviour
{
    public Animator animator;
    [Tooltip("Optional: the player's interactor (Action and Carry come from it).")]
    public PlayerInteractor interactor;
    [Tooltip("Optional: for players. Grounded comes from it.")]
    public CharacterController controller;
    [Tooltip("Scripted figures: turn to face the way they walk, then back to their resting direction.")]
    public bool faceMovement;
    [Tooltip("Walk speed (m/s) at which the walk animation plays at normal speed.")]
    public float walkSpeedRef = 1.5f;

    Vector3 lastPos;
    float speed;
    float carryWeight;
    Quaternion restRotation;

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int GroundedId = Animator.StringToHash("Grounded");
    static readonly int ActionId = Animator.StringToHash("Action");

    void Start()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!controller) controller = GetComponent<CharacterController>();
        if (!interactor) interactor = GetComponent<PlayerInteractor>();
        lastPos = transform.position;
        restRotation = transform.rotation;
    }

    void Update()
    {
        if (!animator) return;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;
        Vector3 flat = new Vector3(delta.x, 0f, delta.z);
        float raw = flat.magnitude / dt;
        if (raw > 30f) raw = 0f; // teleport (respawn), not walking
        speed = Mathf.Lerp(speed, raw, 1f - Mathf.Exp(-12f * dt));

        bool grounded = controller ? controller.isGrounded || controller.velocity.y > -1f && controller.velocity.y < 0.5f : true;
        bool action = false, carrying = false;
        if (interactor && interactor.enabled && interactor.input)
        {
            var cur = interactor.Current;
            action = interactor.input.PowerHeld && cur && cur.UsesPower(interactor);
            carrying = interactor.Carried;
        }

        animator.SetFloat(SpeedId, speed);
        animator.SetBool(GroundedId, grounded);
        animator.SetBool(ActionId, action);
        carryWeight = Mathf.MoveTowards(carryWeight, carrying && !action ? 1f : 0f, dt * 4f);

        if (faceMovement)
        {
            Quaternion target = speed > 0.2f && flat.sqrMagnitude > 0.000001f ? Quaternion.LookRotation(flat) : restRotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-8f * dt));
        }
    }

    void LateUpdate()
    {
        if (!animator || carryWeight <= 0.001f || !animator.isHuman) return;
        // Lift both arms forward, as if holding something in front of the chest.
        Lift(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 1f);
        Lift(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, -1f);
    }

    void Lift(HumanBodyBones upper, HumanBodyBones lower, float side)
    {
        var u = animator.GetBoneTransform(upper);
        var l = animator.GetBoneTransform(lower);
        Vector3 fwd = transform.forward, right = transform.right;
        if (u)
        {
            Quaternion lift = Quaternion.AngleAxis(-70f * carryWeight, right) * Quaternion.AngleAxis(12f * side * carryWeight, transform.up);
            u.rotation = lift * u.rotation;
        }
        if (l) l.rotation = Quaternion.AngleAxis(-25f * carryWeight, right) * l.rotation;
    }
}
