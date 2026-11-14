using UnityEngine;

/// <summary>
/// Puts a humanoid's hands where a script wants them (Unity's built-in humanoid IK). Sits next to the
/// Animator; the controller's layer needs "IK Pass" on (CafeEndingBuilder turns it on for AC_Future).
/// Set the targets (wrist positions, world space) and weights from another script every frame.
/// Also holds things: PlaceInHand puts an object against the palm of the posed hand.
/// </summary>
[RequireComponent(typeof(Animator))]
public class HandIK : MonoBehaviour
{
    public Vector3 rightTarget, leftTarget;
    [Range(0f, 1f)] public float rightWeight, leftWeight;
    [Tooltip("Where the elbows point (world). Zero = let the solver choose.")]
    public Vector3 rightElbow, leftElbow;

    Animator anim;
    public Animator Anim => anim ? anim : anim = GetComponent<Animator>();

    void OnAnimatorIK(int layer)
    {
        var a = Anim;
        a.SetIKPositionWeight(AvatarIKGoal.RightHand, rightWeight);
        a.SetIKPosition(AvatarIKGoal.RightHand, rightTarget);
        a.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftWeight);
        a.SetIKPosition(AvatarIKGoal.LeftHand, leftTarget);
        a.SetIKHintPositionWeight(AvatarIKHint.RightElbow, rightElbow != Vector3.zero ? rightWeight * 0.6f : 0f);
        a.SetIKHintPosition(AvatarIKHint.RightElbow, rightElbow);
        a.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, leftElbow != Vector3.zero ? leftWeight * 0.6f : 0f);
        a.SetIKHintPosition(AvatarIKHint.LeftElbow, leftElbow);
    }

    /// <summary>The middle of the palm of a posed hand, and the direction the palm faces
    /// (towards 'inward', e.g. the body's middle or the thing held).</summary>
    public bool Palm(bool right, Vector3 inward, out Vector3 centre, out Vector3 normal)
    {
        var a = Anim;
        var hand = a.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        var mid = a.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
        var index = a.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
        var little = a.GetBoneTransform(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
        centre = Vector3.zero; normal = Vector3.zero;
        if (!hand) return false;
        Vector3 fingers = mid ? mid.position - hand.position : hand.up * 0.08f;
        centre = hand.position + fingers * 0.75f;
        Vector3 across = index && little ? index.position - little.position : hand.right;
        normal = Vector3.Cross(fingers, across).normalized;
        if (Vector3.Dot(normal, inward - centre) < 0f) normal = -normal;
        return true;
    }

    /// <summary>Holds an object (whose pivot is at its base, like a cup) upright against the palm.</summary>
    public void PlaceInHand(Transform obj, bool right, Vector3 inward, float radius, float gripHeight, Quaternion uprightYaw)
    {
        if (!obj || !Palm(right, inward, out var c, out var n)) return;
        Vector3 flatN = new Vector3(n.x, 0f, n.z);
        if (flatN.sqrMagnitude < 1e-4f) flatN = n;
        obj.position = c + flatN.normalized * radius - Vector3.up * gripHeight;
        obj.rotation = uprightYaw;
    }
}
