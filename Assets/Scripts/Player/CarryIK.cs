using UnityEngine;

/// <summary>
/// While a player carries something, both hands hold it in front of the chest (hand IK), so the
/// walk animation's arm swing can't pull the hands away from it. CharacterAnimator adds this to the
/// model and sets <see cref="weight"/> / <see cref="halfGrip"/>; the carried item is then placed between
/// the hands every frame, so it always moves with the player.
/// Needs "IK Pass" on the Animator Controller's base layer.
/// </summary>
[RequireComponent(typeof(Animator))]
public class CarryIK : MonoBehaviour
{
    [Range(0f, 1f)] public float weight;
    [Tooltip("The player root (its forward is where the item is held).")]
    public Transform body;
    [Tooltip("Half the distance between the hands (set from the carried item's size).")]
    public float halfGrip = 0.14f;
    public float height = 1.05f;
    public float reach = 0.42f;

    Animator anim;

    void Awake() => anim = GetComponent<Animator>();

    /// <summary>Where the item is held (world), in front of the chest.</summary>
    public Vector3 GripCentre => body.position + Vector3.up * height + body.forward * reach;

    void OnAnimatorIK(int layer)
    {
        if (!body || weight <= 0.001f) return;
        Vector3 c = GripCentre, r = body.right, f = body.forward;
        Hand(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, c - r * halfGrip, c - r * (halfGrip + 0.3f) - f * 0.25f + Vector3.down * 0.25f, -1f);
        Hand(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, c + r * halfGrip, c + r * (halfGrip + 0.3f) - f * 0.25f + Vector3.down * 0.25f, 1f);
    }

    void Hand(AvatarIKGoal goal, AvatarIKHint hint, Vector3 pos, Vector3 elbow, float side)
    // (hand rotation is left to the animation: forcing it twists the wrists on these rigs)
    {
        anim.SetIKPositionWeight(goal, weight);
        anim.SetIKPosition(goal, pos);
        anim.SetIKHintPositionWeight(hint, weight * 0.8f);
        anim.SetIKHintPosition(hint, elbow);
    }
}
