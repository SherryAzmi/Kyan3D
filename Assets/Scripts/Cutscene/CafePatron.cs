using UnityEngine;

/// <summary>
/// A café customer in the final cutscene: sits on a chair or bar stool and does one thing, with the
/// prop held in the hands by HandIK — reads a book, reads the newspaper, listens to music (headphones,
/// nodding), types on a laptop, or drinks a coffee (with a sip now and then).
/// Uses the game's own character models with recoloured clothes and hair (set up by CafeEndingBuilder).
/// </summary>
[DefaultExecutionOrder(100)]
public class CafePatron : MonoBehaviour
{
    public enum Activity { Book, Newspaper, Music, Laptop, Coffee }

    public Activity activity;
    public Animator anim;
    public HandIK ik;
    [Tooltip("The chair or stool: position = the seat's centre on the floor, forward = the way they face.")]
    public Transform seat;
    public float seatHeight = 0.48f;
    public bool stool;
    [Tooltip("Book, newspaper, headphones, laptop or cup.")]
    public Transform prop;
    [Tooltip("Where the cup stands on the table (Coffee).")]
    public Transform cupRest;

    [Header("Look")]
    public Color top = new Color(0.35f, 0.45f, 0.6f);
    public Color bottom = new Color(0.25f, 0.25f, 0.28f);
    public Color hair = new Color(0.15f, 0.1f, 0.08f);
    [Tooltip("Random offset so the customers don't all move in step.")]
    public float phase;
    [Tooltip("Their mirror image's model (gets the same colours).")]
    public Transform reflectionModel;

    float drop = -1f;
    Vector3 rGoal, lGoal;
    float rW, lW, rWGoal, lWGoal;
    bool cupInHand;
    float sip; // 0..1 how far the cup is raised
    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int GroundedId = Animator.StringToHash("Grounded");

    void Start()
    {
        if (!anim) anim = GetComponentInChildren<Animator>();
        if (!ik && anim) ik = anim.GetComponent<HandIK>();
        if (anim) { anim.SetFloat(SpeedId, 0f); anim.SetBool(GroundedId, true); }
        if (seat) transform.SetPositionAndRotation(seat.position - seat.forward * 0.05f, seat.rotation);
        Tint();
        if (activity == Activity.Music && prop && anim)
        {
            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (head)
            {
                // over the top of the head, ear cups at the ears
                prop.SetPositionAndRotation(head.position + Vector3.up * 0.13f + transform.forward * 0.01f, transform.rotation);
                prop.SetParent(head, true);
            }
        }
        if (anim) anim.Update(Random.Range(0f, 3f)); // start the idles at different points
    }

    void Tint()
    {
        var renderers = new System.Collections.Generic.List<SkinnedMeshRenderer>(GetComponentsInChildren<SkinnedMeshRenderer>(true));
        if (reflectionModel) renderers.AddRange(reflectionModel.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        foreach (var r in renderers)
        {
            var mats = r.materials;
            foreach (var m in mats)
            {
                if (!m || !m.HasProperty("_BaseColor")) continue;
                string n = m.name;
                if (n.Contains("Hoodie")) m.SetColor("_BaseColor", top);
                else if (n.Contains("Pants")) m.SetColor("_BaseColor", bottom);
                else if (n.Contains("Hair") || n.Contains("Beard")) m.SetColor("_BaseColor", hair);
                else if (n.Contains("Glow") || n.Contains("DimHeart")) m.SetColor("_BaseColor", top * 0.8f);
                else if (n.Contains("Scarf")) m.SetColor("_BaseColor", Color.Lerp(top, Color.white, 0.4f));
            }
            r.materials = mats;
        }
    }

    // ------------------------------------------------------------------ hands (set before the next IK pass)

    void Update()
    {
        if (!anim || !ik) return;
        var chestB = anim.GetBoneTransform(HumanBodyBones.Chest);
        var headB = anim.GetBoneTransform(HumanBodyBones.Head);
        if (!chestB || !headB) return;
        Vector3 f = transform.forward, r = transform.right, up = Vector3.up;
        Vector3 chest = chestB.position, head = headB.position;
        float t = Time.time + phase;

        switch (activity)
        {
            case Activity.Book:
            {
                // a book held up in front of the chest, a page turned now and then
                Vector3 c = chest + f * 0.32f + up * 0.06f;
                Vector3 toFace = (head - c).normalized;
                var rot = Quaternion.LookRotation(-toFace, up);
                if (prop) prop.SetPositionAndRotation(c, rot);
                Vector3 br = rot * Vector3.right;
                float turn = Mathf.Clamp01(Mathf.Sin(t * 0.9f) * 6f - 5f); // a short flick every ~7 s
                rGoal = c + br * 0.11f - up * 0.04f + toFace * turn * 0.05f;
                lGoal = c - br * 0.11f - up * 0.04f;
                rWGoal = lWGoal = 1f;
                break;
            }
            case Activity.Newspaper:
            {
                Vector3 c = chest + f * 0.38f + up * 0.1f;
                Vector3 toFace = (head - c).normalized;
                var rot = Quaternion.LookRotation(-toFace, up) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.4f) * 2f);
                if (prop) prop.SetPositionAndRotation(c, rot);
                Vector3 br = rot * Vector3.right;
                rGoal = c + br * 0.26f - up * 0.06f;
                lGoal = c - br * 0.26f - up * 0.06f;
                rWGoal = lWGoal = 1f;
                break;
            }
            case Activity.Laptop:
            {
                if (!prop) break;
                Vector3 kb = prop.position - f * 0.05f + up * 0.03f;
                float tap = Mathf.PerlinNoise(t * 6f, 0.3f) - 0.5f, tap2 = Mathf.PerlinNoise(0.7f, t * 6f) - 0.5f;
                rGoal = kb + r * 0.09f + up * Mathf.Max(0f, tap) * 0.03f - f * 0.05f;
                lGoal = kb - r * 0.09f + up * Mathf.Max(0f, tap2) * 0.03f - f * 0.05f;
                rWGoal = lWGoal = 1f;
                break;
            }
            case Activity.Music:
            {
                // forearms resting on the bar in front
                Vector3 bar = new Vector3(transform.position.x, 1.1f, transform.position.z) + f * 0.42f;
                rGoal = bar + r * 0.16f - f * 0.06f;
                lGoal = bar - r * 0.16f - f * 0.06f;
                rWGoal = lWGoal = 0.9f;
                break;
            }
            case Activity.Coffee:
            {
                // hand around the cup on the table; every ~9 s lift it for a sip
                float cycle = Mathf.Repeat(t, 9f);
                float want = cycle > 6f && cycle < 8.4f ? 1f : 0f;
                sip = Mathf.MoveTowards(sip, want, Time.deltaTime * 1.4f);
                cupInHand = sip > 0.02f;
                Vector3 rest = cupRest ? cupRest.position : chest + f * 0.4f - up * 0.4f;
                Vector3 mouth = head + f * 0.13f - up * 0.07f;
                Vector3 cupC = Vector3.Lerp(rest + up * 0.05f, mouth, Mathf.SmoothStep(0f, 1f, sip));
                rGoal = cupC + r * 0.05f - f * 0.06f;
                lGoal = rest + up * 0.03f - r * 0.12f - f * 0.1f;
                rWGoal = 1f;
                lWGoal = 0.7f * (1f - sip);
                if (!cupInHand && prop && cupRest) prop.SetPositionAndRotation(cupRest.position, cupRest.rotation);
                break;
            }
        }
        float k = 1f - Mathf.Exp(-8f * Time.deltaTime);
        rW = Mathf.Lerp(rW, rWGoal, k);
        lW = Mathf.Lerp(lW, lWGoal, k);
        ik.rightTarget = Vector3.Lerp(ik.rightTarget == Vector3.zero ? rGoal : ik.rightTarget, rGoal, k);
        ik.leftTarget = Vector3.Lerp(ik.leftTarget == Vector3.zero ? lGoal : ik.leftTarget, lGoal, k);
        ik.rightWeight = rW;
        ik.leftWeight = lW;
        ik.rightElbow = transform.position + up * 0.9f + r * 0.45f - f * 0.1f;
        ik.leftElbow = transform.position + up * 0.9f - r * 0.45f - f * 0.1f;
    }

    // ------------------------------------------------------------------ sitting (after the animation)

    void LateUpdate()
    {
        if (!anim || !anim.isHuman) return;
        var model = anim.transform;
        if (drop < 0f)
        {
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            drop = hips ? Mathf.Max(0f, hips.position.y - model.position.y - (seatHeight + 0.09f)) : 0.4f;
        }
        model.localPosition = new Vector3(0f, -drop, 0f);
        Vector3 right = transform.right;
        float thigh = stool ? -78f : -88f, shin = stool ? 68f : 92f;
        Rot(HumanBodyBones.LeftUpperLeg, Quaternion.AngleAxis(thigh, right));
        Rot(HumanBodyBones.RightUpperLeg, Quaternion.AngleAxis(thigh, right));
        Rot(HumanBodyBones.LeftLowerLeg, Quaternion.AngleAxis(shin, right));
        Rot(HumanBodyBones.RightLowerLeg, Quaternion.AngleAxis(shin, right));

        float t = Time.time + phase;
        if (activity == Activity.Music)
        {
            // nodding along
            Rot(HumanBodyBones.Head, Quaternion.AngleAxis(Mathf.Abs(Mathf.Sin(t * 3.4f)) * 9f, right));
            Rot(HumanBodyBones.Spine, Quaternion.AngleAxis(Mathf.Sin(t * 1.7f) * 2.5f, transform.forward));
        }
        else if (activity == Activity.Book || activity == Activity.Newspaper || activity == Activity.Laptop)
        {
            Rot(HumanBodyBones.Head, Quaternion.AngleAxis(12f, right)); // looking down at it
        }
        else if (activity == Activity.Coffee)
        {
            Rot(HumanBodyBones.Head, Quaternion.AngleAxis(-6f * sip, right));
            if (cupInHand && prop && ik)
                ik.PlaceInHand(prop, true, transform.position + Vector3.up * 1.0f, 0.045f, 0.05f,
                               Quaternion.LookRotation(transform.forward) * Quaternion.AngleAxis(-28f * sip, Vector3.right));
        }
    }

    void Rot(HumanBodyBones b, Quaternion q)
    {
        var t = anim.GetBoneTransform(b);
        if (t) t.rotation = q * t.rotation;
    }
}
