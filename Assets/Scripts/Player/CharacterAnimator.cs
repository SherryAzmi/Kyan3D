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
    CarryIK carryIK;
    Transform carriedItem;
    float carriedHalfWidth = 0.14f;

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

        // Players: the game moves the character, never the clips. With root motion simply turned off,
        // Unity still puts a clip's root movement into the body (the jump clip would drop the body ~1 m
        // while the player is in the air). Turning it on and swallowing it in OnAnimatorMove throws it away.
        if (controller && animator)
        {
            animator.applyRootMotion = true;
            if (!animator.GetComponent<RootMotionSink>()) animator.gameObject.AddComponent<RootMotionSink>();
            // players hold carried things with both hands (IK), whatever the legs are doing
            if (interactor && animator.isHuman)
            {
                carryIK = animator.GetComponent<CarryIK>();
                if (!carryIK) carryIK = animator.gameObject.AddComponent<CarryIK>();
                carryIK.body = transform;
            }
        }
    }

    float airTime;
    /// <summary>On the ground, with a short grace time so steps and slopes don't count as jumps.</summary>
    public bool Grounded { get; private set; } = true;

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

        // (the vertical speed alone isn't enough: at the top of a jump it is ~0 too, which used to flip
        // the animator back to walking mid-air)
        if (controller)
        {
            airTime = controller.isGrounded ? 0f : airTime + dt;
            Grounded = airTime < 0.15f && controller.velocity.y < 1f;
        }
        bool grounded = Grounded;
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
        if (carryIK)
        {
            var item = carrying ? interactor.Carried.transform : null;
            if (item != carriedItem)
            {
                carriedItem = item;
                if (item) carriedHalfWidth = GripHalfWidth(item);
            }
            carryIK.weight = carryWeight;
            carryIK.halfGrip = carriedHalfWidth;
        }

        if (faceMovement)
        {
            Quaternion target = speed > 0.2f && flat.sqrMagnitude > 0.000001f ? Quaternion.LookRotation(flat) : restRotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-8f * dt));
        }
    }

    // ------------------------------------------------------------------ feet on the ground
    // Players only: the CharacterController hovers a skin width above the floor and each clip holds the
    // hips at its own height, so the model can float. After the animation has posed the body, slide the
    // model up/down so the lowest sole sits on the ground under the player.
    [Header("Feet on the ground (players)")]
    public bool groundFeet = true;
    Transform footL, footR;
    float soleOffset;     // foot bone height above the bottom of the sole (from the mesh)
    float footOffset;     // current vertical shift of the model
    Vector3 modelBase;
    bool feetReady;

    void SetupFeet()
    {
        feetReady = true;
        if (!groundFeet || !controller || !animator || !animator.isHuman) { groundFeet = false; return; }
        footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        footR = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (!footL || !footR) { groundFeet = false; return; }
        modelBase = animator.transform.localPosition;

        // sole offset from the bind pose: lowest mesh point vs the lower foot bone
        float meshMin = float.MaxValue;
        var baked = new Mesh();
        foreach (var smr in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            smr.BakeMesh(baked, true);
            var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            foreach (var v in baked.vertices) meshMin = Mathf.Min(meshMin, m.MultiplyPoint3x4(v).y);
        }
        Destroy(baked);
        float footMin = Mathf.Min(footL.position.y, footR.position.y);
        soleOffset = meshMin < float.MaxValue ? Mathf.Clamp(footMin - meshMin, 0f, 0.25f) : 0.08f;
    }

    void GroundFeet()
    {
        var model = animator.transform;
        bool grounded = Grounded;
        float target = footOffset;
        if (grounded)
        {
            Vector3 from = transform.position + Vector3.up * 0.6f;
            // the ground must really be under the feet (the top of a jump also has ~0 vertical speed)
            if (Physics.Raycast(from, Vector3.down, out var hit, 1.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.point.y > transform.position.y - 0.25f)
            {
                float sole = Mathf.Min(footL.position.y, footR.position.y) - soleOffset;
                target = Mathf.Clamp(footOffset + hit.point.y - sole, -0.35f, 0.35f);
            }
        }
        // in the air: keep the current offset, so the body doesn't jump up a few cm as the feet leave the ground
        footOffset = Mathf.Lerp(footOffset, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
        Vector3 up = model.parent ? model.parent.InverseTransformVector(Vector3.up) : Vector3.up;
        model.localPosition = modelBase + up * footOffset;
    }

    // ------------------------------------------------------------------ the Present's depressed posture
    [Header("Depressed posture (the Present)")]
    [Tooltip("0 = none, 1 = full: hunched back, head hanging, shoulders rolled in, arms hanging with little swing, " +
             "a slight sway. -1 = automatic (the Present has it, the Future doesn't).")]
    public float depression = -1f;
    float slumpWeight, swayPhase;
    Quaternion restUpperL, restUpperR, restLowerL, restLowerR;
    bool haveRestArms;
    float stillTime;

    void Slump()
    {
        if (!animator.isHuman) return;
        float k = depression >= 0f ? depression
                : interactor && interactor.input && interactor.input.soul == SoulType.Light ? 1f : 0f;
        if (k <= 0f) return;
        bool acting = interactor && interactor.input && interactor.input.PowerHeld;
        bool walking = speed > 0.3f;
        // full while walking, a bit less standing (the idle clip is already sad), little while digging
        slumpWeight = Mathf.MoveTowards(slumpWeight, acting ? 0.25f : walking ? 1f : 0.6f, Time.deltaTime * 2f);
        float w = k * slumpWeight;

        Vector3 right = transform.right, fwd = transform.forward, up = transform.up;
        Transform B(HumanBodyBones b) => animator.GetBoneTransform(b);
        void Rot(HumanBodyBones b, Quaternion q) { var t = B(b); if (t) t.rotation = q * t.rotation; }

        // arms: remember how they hang in the idle pose, then pull the walk swing toward it
        var ul = B(HumanBodyBones.LeftUpperArm); var ur = B(HumanBodyBones.RightUpperArm);
        var ll = B(HumanBodyBones.LeftLowerArm); var lr = B(HumanBodyBones.RightLowerArm);
        stillTime = walking ? 0f : stillTime + Time.deltaTime;
        if (!walking && !acting && stillTime > 0.6f && ul && ur && ll && lr)
        {
            restUpperL = ul.localRotation; restUpperR = ur.localRotation;
            restLowerL = ll.localRotation; restLowerR = lr.localRotation;
            haveRestArms = true;
        }
        if (haveRestArms && walking && !acting && !Carrying)
        {
            float damp = 0.6f * w; // 60% less arm swing: arms just hang
            ul.localRotation = Quaternion.Slerp(ul.localRotation, restUpperL, damp);
            ur.localRotation = Quaternion.Slerp(ur.localRotation, restUpperR, damp);
            ll.localRotation = Quaternion.Slerp(ll.localRotation, restLowerL, damp);
            lr.localRotation = Quaternion.Slerp(lr.localRotation, restLowerR, damp);
        }

        // a slow, heavy sway from side to side while walking
        swayPhase += Time.deltaTime * (walking ? 3.2f : 0.8f);
        float sway = Mathf.Sin(swayPhase) * 3f * w;

        // hunched back and hanging head (degrees forward)
        Rot(HumanBodyBones.Spine, Quaternion.AngleAxis(6f * w, right) * Quaternion.AngleAxis(sway, fwd));
        Rot(HumanBodyBones.Chest, Quaternion.AngleAxis(9f * w, right));
        Rot(HumanBodyBones.UpperChest, Quaternion.AngleAxis(5f * w, right));
        Rot(HumanBodyBones.Neck, Quaternion.AngleAxis(10f * w, right));
        Rot(HumanBodyBones.Head, Quaternion.AngleAxis(16f * w, right) * Quaternion.AngleAxis(-sway * 1.5f, fwd));

        // shoulders roll forward and drop
        Rot(HumanBodyBones.LeftShoulder, Quaternion.AngleAxis(10f * w, up) * Quaternion.AngleAxis(8f * w, fwd));
        Rot(HumanBodyBones.RightShoulder, Quaternion.AngleAxis(-10f * w, up) * Quaternion.AngleAxis(-8f * w, fwd));
    }

    bool Carrying => interactor && interactor.Carried;

    void LateUpdate()
    {
        if (!animator) return;
        if (!feetReady) SetupFeet();
        if (groundFeet) GroundFeet();
        Slump();
        if (carryWeight <= 0.001f || !animator.isHuman) return;
        if (carryIK)
        {
            // the hands already hold the grip (IK); put the item right between them, after the posture tweaks
            PlaceBetweenHands();
            return;
        }
        // Lift both arms forward, as if holding something in front of the chest.
        Lift(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 1f);
        Lift(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, -1f);
    }

    void PlaceBetweenHands()
    {
        if (!carriedItem || !interactor || interactor.Carried == null || carriedItem != interactor.Carried.transform) return;
        var l = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        var r = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (!l || !r) return;
        Vector3 between = (l.position + r.position) * 0.5f + transform.forward * 0.05f;
        // the item's pivot may not be its centre: move it so its visual centre sits between the hands
        Vector3 centreOffset = carriedItem.position - VisualCentre(carriedItem);
        carriedItem.position = Vector3.Lerp(carryIK.GripCentre, between, carryWeight) + centreOffset;
        carriedItem.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
    }

    static Vector3 VisualCentre(Transform item)
    {
        bool any = false;
        var b = new Bounds(item.position, Vector3.zero);
        foreach (var rend in item.GetComponentsInChildren<MeshRenderer>())
        {
            if (!rend.enabled) continue;
            if (!any) { b = rend.bounds; any = true; } else b.Encapsulate(rend.bounds);
        }
        return any ? b.center : item.position;
    }

    /// <summary>Half the grip width: hands go on the sides of the item.</summary>
    float GripHalfWidth(Transform item)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var rend in item.GetComponentsInChildren<MeshRenderer>())
        {
            if (!rend.enabled) continue;
            if (!any) { b = rend.bounds; any = true; } else b.Encapsulate(rend.bounds);
        }
        float half = any ? Mathf.Max(b.extents.x, b.extents.z) : 0.12f;
        return Mathf.Clamp(half + 0.04f, 0.1f, 0.3f);
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
