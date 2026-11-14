using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The first moments of the opening cutscene, before the mirror: the Present stands at the café counter
/// and orders a coffee; the barista nods and turns to the machine. While he waits his eyes find a framed
/// sign on the wall, "What is the meaning of your life?". He stares at it, then walks to the mirror.
/// Ends on black at the mirror; MirrorCutscene then plays exactly as before.
/// Built into Cutscene.unity by Tools > Kyan > Dress Opening Café (CafeBuilder).
/// </summary>
public class CafePrelude : MonoBehaviour
{
    [Header("People")]
    public Transform character;
    public Animator characterAnim;
    [Tooltip("The mirror reflection figure: kept mirrored while he walks around.")]
    public Transform reflection;
    public Animator reflectionAnim;
    public Transform barista;
    public Animator baristaAnim;

    [Header("Places")]
    public Transform counterStand;
    public Transform sign;

    [Header("Shots")]
    public Transform shotCounter, shotSign, shotStare, shotWalk;

    [Header("Text")]
    public Text subtitle;
    public string orderLine = "One coffee, please.";
    public string baristaLine = "Coming right up.";
    public float walkSpeed = 0.9f;
    public float mirrorZ = 1.6f;

    [Header("Barista look")]
    public Color apron = new Color(0.2f, 0.3f, 0.26f), trousers = new Color(0.12f, 0.12f, 0.13f), hair = new Color(0.2f, 0.12f, 0.08f);

    Camera cam;
    Vector3 homePos;
    Quaternion homeRot;
    CharacterAnimator posture;
    float baristaNod, lookAtSign;
    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int GroundedId = Animator.StringToHash("Grounded");

    void Start()
    {
        if (!barista) return;
        foreach (var r in barista.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mats = r.materials;
            foreach (var m in mats)
            {
                if (!m || !m.HasProperty("_BaseColor")) continue;
                if (m.name.Contains("Hoodie")) m.SetColor("_BaseColor", apron);
                else if (m.name.Contains("Pants")) m.SetColor("_BaseColor", trousers);
                else if (m.name.Contains("Hair") || m.name.Contains("Beard")) m.SetColor("_BaseColor", hair);
                else if (m.name.Contains("Scarf")) m.SetColor("_BaseColor", new Color(0.85f, 0.8f, 0.7f));
            }
            r.materials = mats;
        }
        if (baristaAnim) { baristaAnim.SetFloat(SpeedId, 0f); baristaAnim.SetBool(GroundedId, true); }
    }

    /// <summary>Runs inside MirrorCutscene's sequence (so skipping stops it too).</summary>
    public IEnumerator Play(MirrorCutscene mc)
    {
        cam = mc.cam;
        homePos = character.position;
        homeRot = character.rotation;
        // a tired walk on the way to the mirror (removed again before the mirror scene)
        posture = character.gameObject.AddComponent<CharacterAnimator>();
        posture.animator = characterAnim;
        posture.depression = 1f;
        posture.groundFeet = false;

        character.SetPositionAndRotation(counterStand.position, counterStand.rotation);
        Shot(shotCounter);
        yield return Fade(mc.fadeImage, 1f, 0f, 1.6f);
        yield return new WaitForSeconds(0.6f);

        // he orders
        yield return Say(orderLine, 1.8f);
        baristaNod = 1f;
        yield return new WaitForSeconds(0.4f);
        yield return Say(baristaLine, 1.4f);
        baristaNod = 0f;
        // the barista turns to the machine
        if (barista)
        {
            Quaternion b0 = barista.rotation, b1 = barista.rotation * Quaternion.Euler(0f, 180f, 0f);
            for (float t = 0f; t < 0.8f; t += Time.deltaTime) { barista.rotation = Quaternion.Slerp(b0, b1, Mathf.SmoothStep(0f, 1f, t / 0.8f)); yield return null; }
        }
        yield return new WaitForSeconds(0.5f);

        // his eyes find the sign
        for (float t = 0f; t < 1.2f; t += Time.deltaTime) { lookAtSign = Mathf.SmoothStep(0f, 1f, t / 1.2f); yield return null; }
        Shot(shotSign);
        yield return Push(shotSign, 0.25f, 3.2f);
        Shot(shotStare);
        yield return Push(shotStare, 0.12f, 2.6f);

        // he walks to the mirror
        Shot(shotWalk);
        yield return WalkTo(homePos);
        Quaternion r0 = character.rotation;
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            character.rotation = Quaternion.Slerp(r0, homeRot, Mathf.SmoothStep(0f, 1f, t / 0.8f));
            lookAtSign = 1f - t / 0.8f;
            yield return null;
        }
        character.SetPositionAndRotation(homePos, homeRot);
        lookAtSign = 0f;
        yield return new WaitForSeconds(0.8f);
        yield return Fade(mc.fadeImage, 0f, 1f, 1.1f);

        // back to the mirror scene exactly as it was
        if (posture) Destroy(posture);
        characterAnim.SetFloat(SpeedId, 0f);
        if (reflectionAnim) reflectionAnim.SetFloat(SpeedId, 0f);
        yield return null;
    }

    IEnumerator WalkTo(Vector3 target)
    {
        target.y = character.position.y;
        while (true)
        {
            Vector3 d = target - character.position;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.03f) break;
            Vector3 dir = d / dist;
            character.rotation = Quaternion.RotateTowards(character.rotation, Quaternion.LookRotation(dir), 300f * Time.deltaTime);
            float speed = walkSpeed * Mathf.Clamp01(dist / 0.5f + 0.3f);
            character.position += dir * Mathf.Min(dist, speed * Time.deltaTime);
            if (reflectionAnim) reflectionAnim.SetFloat(SpeedId, speed);
            // follow him from the shot's position
            if (cam) cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation,
                Quaternion.LookRotation(character.position + Vector3.up * 1.1f - cam.transform.position), 1f - Mathf.Exp(-3f * Time.deltaTime));
            yield return null;
        }
        character.position = target;
        if (reflectionAnim) reflectionAnim.SetFloat(SpeedId, 0f);
    }

    void LateUpdate()
    {
        // the reflection stays a mirror image of him while he moves around the café
        if (reflection && character && posture)
        {
            Vector3 p = character.position;
            reflection.position = new Vector3(p.x, reflection.position.y, 2f * mirrorZ - p.z);
            Vector3 f = character.forward;
            reflection.rotation = Quaternion.LookRotation(new Vector3(f.x, 0f, -f.z));
        }
        // his head turns towards the sign
        if (lookAtSign > 0f && characterAnim && sign)
        {
            var head = characterAnim.GetBoneTransform(HumanBodyBones.Head);
            if (head)
            {
                Vector3 to = (sign.position - head.position).normalized;
                Vector3 fwd = character.forward;
                var turn = Quaternion.FromToRotation(fwd, Vector3.Slerp(fwd, to, 0.85f));
                head.rotation = Quaternion.Slerp(Quaternion.identity, turn, lookAtSign) * head.rotation;
            }
        }
        // the barista nods
        if (baristaAnim && baristaNod > 0f)
        {
            var bh = baristaAnim.GetBoneTransform(HumanBodyBones.Head);
            if (bh) bh.rotation = Quaternion.AngleAxis(Mathf.Abs(Mathf.Sin(Time.time * 7f)) * 10f, barista.right) * bh.rotation;
        }
    }

    // ------------------------------------------------------------------ helpers

    IEnumerator Say(string line, float hold)
    {
        if (!subtitle || string.IsNullOrEmpty(line)) { yield return new WaitForSeconds(hold); yield break; }
        subtitle.text = line;
        yield return Fade(subtitle, 0f, 1f, 0.3f);
        yield return new WaitForSeconds(hold);
        yield return Fade(subtitle, 1f, 0f, 0.3f);
    }

    void Shot(Transform s)
    {
        if (cam && s) cam.transform.SetPositionAndRotation(s.position, s.rotation);
    }

    IEnumerator Push(Transform s, float distance, float seconds)
    {
        if (!cam || !s) { yield return new WaitForSeconds(seconds); yield break; }
        Vector3 p0 = s.position, p1 = s.position + s.forward * distance;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            cam.transform.position = Vector3.Lerp(p0, p1, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
    }

    static IEnumerator Fade(Graphic g, float from, float to, float seconds)
    {
        if (!g) yield break;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            var c = g.color; c.a = Mathf.Lerp(from, to, t / seconds); g.color = c;
            yield return null;
        }
        var e = g.color; e.a = to; g.color = e;
    }
}
