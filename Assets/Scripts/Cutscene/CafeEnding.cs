using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The last scene, after Level 3: the café in the morning, with a few customers.
///  1. Fade in from white. The Future self stands at the mirror, still heavy; the café is dim and grey.
///  2. Close on the reflection. His eyes light up a warm amber; the heart on his hoodie glows.
///     The café wakes with him: sunlight, lamps, colour.
///  3. His own face. He stands up straight.
///  4. He walks to the counter, takes the cup of coffee (held in his hand with IK), walks to a table, sits.
///  5. The camera drifts back. He takes a sip: "I feel warm and full inside." Then "The End", Play again / Quit.
/// Built by Tools > Kyan > Build Café Ending (CafeEndingBuilder).
/// </summary>
[DefaultExecutionOrder(100)]
public class CafeEnding : MonoBehaviour
{
    [Header("Camera and shots")]
    public Camera cam;
    public Transform shotShoulder, shotReflection, shotFace, shotWide, shotFinal, shotFinalEnd, shotSign;
    [Tooltip("The framed \"What is the meaning of your life?\" he looks at in the end.")]
    public Transform sign;
    [Tooltip("Smirk pieces (person and reflection), shown in the last close-up.")]
    public GameObject[] smirk;
    [Tooltip("The smiling mouth (person and reflection): replaces his flat lips as he wakes up.")]
    public GameObject[] smile;
    [Tooltip("The middle of his mouth (the cup goes here when he sips).")]
    public Transform mouth;
    public Transform[] dimples;
    public float normalFov = 40f;
    [Tooltip("Narrower lens on the reflection's face, so the eyes read.")]
    public float reflectionFov = 22f;

    [Header("The person")]
    public Transform person;
    public Animator personAnim;
    public HandIK hands;
    public RuntimeAnimatorController tiredController, aliveController;
    public MirrorReflection reflection;
    public Transform cupReflection;

    [Header("Places")]
    public Transform counterStand, coffeeSpot, seatApproach, seat, cupRest;
    public Transform cup;
    public ParticleSystem steam;

    [Header("Eyes full of life")]
    public Color irisColor = new Color(0.95f, 0.6f, 0.22f);
    [Tooltip("How much the eyes glow (kept low: a warm shine, not a light).")]
    public float irisGlow = 0.35f;
    public Color heartColor = new Color(1f, 0.38f, 0.6f);
    public Light eyeGlow;

    [Header("The café wakes up")]
    public Light[] lamps;
    public Renderer[] bulbs, leds, windows;
    public Material bulbOn, bulbOff, ledOn, ledOff, windowDawn, windowDay;
    public Light sun, windowLight;
    public Volume dullVolume;
    public Color ambientDawn = new Color(0.26f, 0.27f, 0.31f), ambientDay = new Color(0.42f, 0.39f, 0.35f);

    [Header("UI")]
    public Image fadeImage;
    public Text lineText;
    [TextArea] public string line = "I feel warm and full inside.";
    [Tooltip("Shown after he has looked at the sign and smirked.")]
    [TextArea] public string seedLine = "I may not see the flowers today,\nbut every seed I plant is a promise to my tomorrow.";
    public Text theEndText;
    public CanvasGroup buttons;
    public Button playAgainButton, quitButton;
    public string firstScene = "Cutscene";

    [Header("Sound")]
    public AudioSource music;
    public AudioSource sfx;
    public AudioClip awakenClip, pickupClip, sitClip;
    [Range(0f, 1f)] public float musicVolume = 0.5f;

    [Header("Timing")]
    public float walkSpeed = 1.35f;

    /// <summary>The music volume with the player's Music setting applied.</summary>
    float MusicLevel => musicVolume * GameSettings.Music;

    enum Cup { OnCounter, InHand, OnTable }
    enum Hands { Free, Reach, Carry, Table, Sip }
    Cup cupState = Cup.OnCounter;
    Hands handMode = Hands.Free;
    float sitWeight, sitDrop, rW, lW, sipTilt, lookAtSign;
    Transform lookTarget;
    float[] lampBase;
    float sunBase, windowBase;
    readonly List<Material> irisMats = new List<Material>(), heartMats = new List<Material>();
    readonly List<Color> irisFrom = new List<Color>(), heartFrom = new List<Color>();
    Animator anim => personAnim;

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int GroundedId = Animator.StringToHash("Grounded");
    static readonly Color DimHeart = new Color(0.45f, 0.38f, 0.42f);

    void Start()
    {
        if (playAgainButton) playAgainButton.onClick.AddListener(() => SceneManager.LoadScene(firstScene));
        if (quitButton) quitButton.onClick.AddListener(Quit);
        if (buttons) { buttons.alpha = 0f; buttons.interactable = false; buttons.blocksRaycasts = false; }
        SetAlpha(theEndText, 0f);
        SetAlpha(lineText, 0f);
        if (lineText) lineText.text = line;
        SetAlpha(fadeImage, 1f);
        if (anim) { anim.runtimeAnimatorController = tiredController; anim.SetBool(GroundedId, true); }
        if (!hands && anim) hands = anim.GetComponent<HandIK>();
        CollectFaceMaterials();
        Eyes(0f);
        Morning(0f);
        if (cam) cam.fieldOfView = normalFov;
        if (cam && shotShoulder) cam.transform.SetPositionAndRotation(shotShoulder.position, shotShoulder.rotation);
        if (steam) steam.Play();
        StartCoroutine(Run());
    }

    // ------------------------------------------------------------------ the sequence

    IEnumerator Run()
    {
        if (music) { music.volume = 0f; music.Play(); StartCoroutine(Volume(music, MusicLevel * 0.55f, 4f)); }

        // 1. Back from white: at the mirror, the café still asleep.
        yield return Fade(fadeImage, 1f, 0f, 2.2f);
        yield return new WaitForSeconds(1.4f);

        // 2. Close on the reflection: the eyes light up, the café wakes.
        StartCoroutine(Lens(normalFov, reflectionFov, 3.2f));
        yield return MoveCamera(shotShoulder, shotReflection, 3.2f);
        yield return new WaitForSeconds(0.6f);
        if (sfx && awakenClip) sfx.PlayOneShot(awakenClip, 0.8f);
        StartCoroutine(Volume(music, MusicLevel, 5f));
        for (float t = 0f; t < 2.6f; t += Time.deltaTime)
        {
            Eyes(Mathf.SmoothStep(0f, 1f, t / 2.6f));
            Morning(Mathf.SmoothStep(0f, 1f, t / 5f));
            yield return null;
        }
        Eyes(1f);
        StartCoroutine(MorningRest(2.6f / 5f, 2.4f));
        yield return new WaitForSeconds(1.6f);

        // 3. His own face. He stands up straight.
        Cut(shotFace);
        if (cam) cam.fieldOfView = normalFov;
        if (anim && aliveController) anim.runtimeAnimatorController = aliveController;
        if (anim) anim.SetBool(GroundedId, true);
        yield return Drift(shotFace, 0.08f, 3.2f);

        // 4. The coffee, a table.
        Cut(shotWide);
        lookTarget = person;
        yield return new WaitForSeconds(0.4f);
        yield return WalkTo(counterStand.position);
        yield return TurnTo(counterStand.rotation, 0.5f);
        yield return TakeCup();
        yield return WalkTo(seatApproach.position);
        yield return TurnTo(seat.rotation, 0.45f);
        yield return SitDown();

        // 5. Back over the café; a sip; the line; The End.
        lookTarget = null;
        var camMove = StartCoroutine(MoveCamera(null, shotFinal, 3f, fromCurrent: true));
        yield return new WaitForSeconds(3.2f);
        yield return Sip();
        yield return Fade(lineText, 0f, 1f, 1.2f);
        yield return new WaitForSeconds(3f);
        yield return Fade(lineText, 1f, 0f, 1.2f);
        StopCoroutine(camMove);

        // 6. He looks up at the sign on the wall... and smirks.
        for (float t = 0f; t < 1.2f; t += Time.deltaTime) { lookAtSign = Mathf.SmoothStep(0f, 1f, t / 1.2f); yield return null; }
        if (shotSign)
        {
            Cut(shotSign);
            yield return Drift(shotSign, 0.2f, 2.6f);
        }
        CloseUp();
        yield return new WaitForSeconds(0.7f);
        foreach (var s in smile) if (s) s.SetActive(false);
        foreach (var s in smirk) if (s) s.SetActive(true);

        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.6f);
            Smirk(k);
            yield return null;
        }
        Smirk(1f);
        yield return new WaitForSeconds(1.2f);
        // the thought he leaves with, after seeing the sign
        if (lineText && !string.IsNullOrEmpty(seedLine))
        {
            lineText.text = seedLine;
            yield return Fade(lineText, 0f, 1f, 1.2f);
            yield return new WaitForSeconds(4.5f);
            yield return Fade(lineText, 1f, 0f, 1.2f);
        }
        else yield return new WaitForSeconds(1.6f);

        // 7. The End.
        Cut(shotFinalEnd);
        StartCoroutine(Drift(shotFinalEnd, 0.6f, 8f));
        yield return new WaitForSeconds(1f);
        yield return new WaitForSeconds(2.2f);
        // the new end panel (title, closing line, Play again / Main menu / Quit) replaces the old plain text and buttons
        EndPanel.Show();
    }

    IEnumerator WalkTo(Vector3 target)
    {
        target.y = person.position.y;
        while (true)
        {
            Vector3 d = target - person.position;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.03f) break;
            Vector3 dir = d / dist;
            person.rotation = Quaternion.RotateTowards(person.rotation, Quaternion.LookRotation(dir), 360f * Time.deltaTime);
            float speed = walkSpeed * Mathf.Clamp01(dist / 0.5f + 0.25f);
            person.position += dir * Mathf.Min(dist, speed * Time.deltaTime);
            anim.SetFloat(SpeedId, speed, 0.15f, Time.deltaTime);
            yield return null;
        }
        person.position = target;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime) { anim.SetFloat(SpeedId, 0f, 0.1f, Time.deltaTime); yield return null; }
        anim.SetFloat(SpeedId, 0f);
    }

    IEnumerator TurnTo(Quaternion rot, float seconds)
    {
        Quaternion from = person.rotation;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            person.rotation = Quaternion.Slerp(from, rot, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        person.rotation = rot;
    }

    IEnumerator TakeCup()
    {
        handMode = Hands.Reach;
        yield return new WaitForSeconds(0.75f);
        cupState = Cup.InHand;
        if (sfx && pickupClip) sfx.PlayOneShot(pickupClip, 0.7f);
        yield return new WaitForSeconds(0.15f);
        handMode = Hands.Carry;
        yield return new WaitForSeconds(0.6f);
    }

    IEnumerator SitDown()
    {
        var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        float hipsHeight = hips ? hips.position.y - person.position.y : 0.95f;
        sitDrop = Mathf.Max(0f, hipsHeight - 0.57f);
        Vector3 from = person.position, to = new Vector3(seat.position.x, person.position.y, seat.position.z) - seat.forward * 0.08f;
        if (sfx && sitClip) sfx.PlayOneShot(sitClip, 0.5f);
        for (float t = 0f; t < 1.1f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.1f);
            person.position = Vector3.Lerp(from, to, k);
            sitWeight = k;
            yield return null;
        }
        sitWeight = 1f;
        // set the cup down in front of him, both hands around it
        handMode = Hands.Table;
        yield return new WaitForSeconds(0.9f);
        cupState = Cup.OnTable;
        if (cup && cupRest) cup.SetPositionAndRotation(cupRest.position, cupRest.rotation);
    }

    IEnumerator Sip()
    {
        cupState = Cup.InHand;
        handMode = Hands.Sip;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime) { sipTilt = Mathf.SmoothStep(0f, 1f, (t - 0.5f) / 0.7f); yield return null; }
        yield return new WaitForSeconds(1.3f);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) { sipTilt = 1f - t / 0.5f; yield return null; }
        sipTilt = 0f;
        handMode = Hands.Table;
        yield return new WaitForSeconds(1.0f);
        cupState = Cup.OnTable;
        if (cup && cupRest) cup.SetPositionAndRotation(cupRest.position, cupRest.rotation);
    }

    // ------------------------------------------------------------------ hands (IK goals for the next frame)

    void Update()
    {
        if (!hands || !anim || !person) return;
        var chestB = anim.GetBoneTransform(HumanBodyBones.Chest);
        var headB = anim.GetBoneTransform(HumanBodyBones.Head);
        if (!chestB || !headB) return;
        Vector3 f = person.forward, r = person.right, up = Vector3.up;
        Vector3 chest = chestB.position, head = headB.position;

        Vector3 rGoal = hands.rightTarget, lGoal = hands.leftTarget;
        float rWant = 0f, lWant = 0f;
        // the wrist sits a little behind and to the right of the cup it holds
        Vector3 Wrist(Vector3 cupCentre) => cupCentre + r * 0.05f - f * 0.07f;
        switch (handMode)
        {
            case Hands.Reach:
                rGoal = Wrist(cup.position + up * 0.05f); rWant = 1f; break;
            case Hands.Carry:
                rGoal = Wrist(chest + f * 0.3f + r * 0.08f - up * 0.2f); rWant = 1f; break;
            case Hands.Table:
                rGoal = Wrist(cupRest.position + up * 0.05f); rWant = 1f;
                lGoal = cupRest.position + up * 0.04f - r * 0.06f - f * 0.08f; lWant = 0.85f; break;
            case Hands.Sip:
                rGoal = Wrist(MouthPoint(head, f) + f * 0.05f - up * 0.035f); rWant = 1f;
                lGoal = cupRest.position + up * 0.03f - r * 0.1f - f * 0.12f; lWant = 0.5f; break;
        }
        float k = 1f - Mathf.Exp(-5f * Time.deltaTime);
        rW = Mathf.Lerp(rW, rWant, k);
        lW = Mathf.Lerp(lW, lWant, k);
        hands.rightTarget = hands.rightTarget == Vector3.zero ? rGoal : Vector3.Lerp(hands.rightTarget, rGoal, k);
        hands.leftTarget = hands.leftTarget == Vector3.zero ? lGoal : Vector3.Lerp(hands.leftTarget, lGoal, k);
        hands.rightWeight = rW;
        hands.leftWeight = lW;
        hands.rightElbow = person.position + up * (1.0f - sitDrop * sitWeight) + r * 0.45f - f * 0.15f;
        hands.leftElbow = person.position + up * (1.0f - sitDrop * sitWeight) - r * 0.45f - f * 0.15f;
    }

    // ------------------------------------------------------------------ after the animation

    void LateUpdate()
    {
        if (!anim || !anim.isHuman || !person) return;
        Vector3 right = person.right;

        if (sitWeight > 0f)
        {
            anim.transform.localPosition = new Vector3(0f, -sitDrop * sitWeight, 0f);
            Rot(HumanBodyBones.LeftUpperLeg, Quaternion.AngleAxis(-88f * sitWeight, right));
            Rot(HumanBodyBones.RightUpperLeg, Quaternion.AngleAxis(-88f * sitWeight, right));
            Rot(HumanBodyBones.LeftLowerLeg, Quaternion.AngleAxis(92f * sitWeight, right));
            Rot(HumanBodyBones.RightLowerLeg, Quaternion.AngleAxis(92f * sitWeight, right));
        }
        if (sipTilt > 0f) Rot(HumanBodyBones.Head, Quaternion.AngleAxis(-8f * sipTilt, right));
        if (lookAtSign > 0f && sign)
        {
            // he looks up at the sign
            var headB = anim.GetBoneTransform(HumanBodyBones.Head);
            if (headB)
            {
                Vector3 to = (sign.position - headB.position).normalized, fwd = person.forward;
                var turn = Quaternion.FromToRotation(fwd, Vector3.Slerp(fwd, to, 0.8f));
                headB.rotation = Quaternion.Slerp(Quaternion.identity, turn, lookAtSign) * headB.rotation;
            }
        }

        // the cup sits in the palm of the posed hand, upright (tipped towards the mouth while sipping)
        if (cupState == Cup.InHand && cup && hands)
        {
            hands.PlaceInHand(cup, true, person.position + Vector3.up * 1.0f, 0.045f, 0.05f,
                              Quaternion.LookRotation(person.forward) * Quaternion.AngleAxis(-35f * sipTilt, Vector3.right));
            // while sipping, the rim meets his lips (not his cheek)
            if (sipTilt > 0f && mouth)
            {
                Vector3 rimToBase = cup.rotation * new Vector3(0f, -0.09f, 0f);
                Vector3 want = mouth.position + person.forward * 0.035f - Vector3.up * 0.005f + rimToBase;
                cup.position = Vector3.Lerp(cup.position, want, sipTilt);
            }
        }

        if (lookTarget && cam)
        {
            var want = Quaternion.LookRotation(lookTarget.position + Vector3.up * 1.0f - cam.transform.position);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, want, 1f - Mathf.Exp(-3f * Time.deltaTime));
        }
    }

    Vector3[] smirkBase, dimpleBase;

    void Smirk(float k)
    {
        if (smirkBase == null)
        {
            smirkBase = new Vector3[smirk.Length];
            for (int i = 0; i < smirk.Length; i++) smirkBase[i] = smirk[i] ? smirk[i].transform.localScale : Vector3.one;
            dimpleBase = new Vector3[dimples.Length];
            for (int i = 0; i < dimples.Length; i++) dimpleBase[i] = dimples[i] ? dimples[i].localScale : Vector3.one;
        }
        for (int i = 0; i < smirk.Length; i++) if (smirk[i]) smirk[i].transform.localScale = smirkBase[i] * Mathf.Max(0.001f, k);
        for (int i = 0; i < dimples.Length; i++) if (dimples[i]) dimples[i].localScale = dimpleBase[i] * (1f + 0.35f * k);
    }

    /// <summary>A close-up on his face from the direction he is looking (towards the sign).</summary>
    void CloseUp()
    {
        var head = anim ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        if (!cam || !head) return;
        Vector3 h = head.position + Vector3.up * 0.06f;
        Vector3 toSign = sign ? (sign.position - h).normalized : person.forward;
        Vector3 dir = Vector3.Slerp(person.forward, toSign, 0.6f);
        dir.y = Mathf.Clamp(dir.y, -0.1f, 0.25f);
        dir.Normalize();
        cam.fieldOfView = 30f;
        cam.transform.position = h + dir * 0.75f + person.right * 0.06f;
        cam.transform.LookAt(h + Vector3.up * 0.01f);
    }

    void Rot(HumanBodyBones b, Quaternion q)
    {
        var t = anim.GetBoneTransform(b);
        if (t) t.rotation = q * t.rotation;
    }

    // ------------------------------------------------------------------ eyes, room, camera

    void CollectFaceMaterials()
    {
        var renderers = new List<Renderer>();
        if (person) renderers.AddRange(person.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        if (reflection) renderers.AddRange(reflection.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        foreach (var r in renderers)
        {
            var mats = r.materials; // instances: only this cutscene changes them
            foreach (var m in mats)
            {
                if (!m || !m.HasProperty("_BaseColor")) continue;
                if (m.name.Contains("Iris")) { irisMats.Add(m); irisFrom.Add(m.GetColor("_BaseColor")); m.EnableKeyword("_EMISSION"); }
                else if (m.name.Contains("Lips")) lipsMats.Add(m);
                else if (m.name.Contains("Skin")) skinColor = m.GetColor("_BaseColor");
                else if (m.name.Contains("DimHeart") || m.name.Contains("Light_Glow"))
                { heartMats.Add(m); heartFrom.Add(DimHeart); m.EnableKeyword("_EMISSION"); }
            }
            r.materials = mats;
        }
    }

    void Eyes(float k)
    {
        for (int i = 0; i < irisMats.Count; i++)
        {
            irisMats[i].SetColor("_BaseColor", Color.Lerp(irisFrom[i], irisColor, k));
            irisMats[i].SetColor("_EmissionColor", irisColor * (irisGlow * k));
        }
        for (int i = 0; i < heartMats.Count; i++)
        {
            heartMats[i].SetColor("_BaseColor", Color.Lerp(heartFrom[i], heartColor, k));
            heartMats[i].SetColor("_EmissionColor", heartColor * (0.8f * k));
        }
        if (eyeGlow) eyeGlow.intensity = Mathf.Sin(k * Mathf.PI) * 0.35f + k * 0.05f;
        // he smiles as his eyes light up: the flat lips fade into the skin, the smiling mouth appears
        bool smiling = k > 0.45f;
        if (smiling && !lipsHidden) { foreach (var m in lipsMats) if (m) HideMaterial(m); lipsHidden = true; }
        if (smiling && smile != null) foreach (var s in smile) if (s && !s.activeSelf && !(smirk != null && smirk.Length > 0 && smirk[0] && smirk[0].activeSelf)) s.SetActive(true);
    }

    readonly List<Material> lipsMats = new List<Material>();
    bool lipsHidden;

    /// <summary>Makes a (per-renderer instance) material draw nothing: fully transparent.</summary>
    static void HideMaterial(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        var c = m.GetColor("_BaseColor"); c.a = 0f; m.SetColor("_BaseColor", c);
        m.SetShaderPassEnabled("ShadowCaster", false);
        m.SetShaderPassEnabled("DepthOnly", false);
        m.SetShaderPassEnabled("DepthNormals", false);
    }
    Color skinColor = new Color(0.9f, 0.72f, 0.6f);

    /// <summary>The middle of his mouth (from the face markers, or a guess from the head bone).</summary>
    Vector3 MouthPoint(Vector3 head, Vector3 f) => mouth ? mouth.position : head + f * 0.13f - Vector3.up * 0.07f;

    /// <summary>0 = grey dawn, lamps off; 1 = warm morning, lamps on, full colour.</summary>
    void Morning(float k)
    {
        if (lampBase == null)
        {
            lampBase = new float[lamps.Length];
            for (int i = 0; i < lamps.Length; i++) lampBase[i] = lamps[i] ? lamps[i].intensity : 0f;
            sunBase = sun ? sun.intensity : 0f;
            windowBase = windowLight ? windowLight.intensity : 0f;
        }
        for (int i = 0; i < lamps.Length; i++)
            if (lamps[i]) { lamps[i].enabled = k > 0.01f; lamps[i].intensity = lampBase[i] * k; }
        foreach (var b in bulbs) if (b) b.sharedMaterial = k > 0.35f ? bulbOn : bulbOff;
        foreach (var l in leds) if (l) l.sharedMaterial = k > 0.55f ? ledOn : ledOff;
        foreach (var w in windows) if (w) w.sharedMaterial = k > 0.25f ? windowDay : windowDawn;
        if (sun) sun.intensity = sunBase * Mathf.Lerp(0.35f, 1f, k);
        if (windowLight) windowLight.intensity = windowBase * Mathf.Lerp(0.4f, 1f, k);
        if (dullVolume) dullVolume.weight = 1f - k;
        RenderSettings.ambientLight = Color.Lerp(ambientDawn, ambientDay, k);
    }

    IEnumerator MorningRest(float from, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            Morning(Mathf.Lerp(from, 1f, Mathf.SmoothStep(0f, 1f, t / seconds)));
            yield return null;
        }
        Morning(1f);
    }

    void Cut(Transform shot)
    {
        if (cam && shot) { cam.transform.SetPositionAndRotation(shot.position, shot.rotation); cam.fieldOfView = normalFov; }
    }

    IEnumerator MoveCamera(Transform a, Transform b, float seconds, bool fromCurrent = false)
    {
        if (!cam || !b) yield break;
        Vector3 p0 = fromCurrent || !a ? cam.transform.position : a.position;
        Quaternion r0 = fromCurrent || !a ? cam.transform.rotation : a.rotation;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            cam.transform.SetPositionAndRotation(Vector3.Lerp(p0, b.position, k), Quaternion.Slerp(r0, b.rotation, k));
            yield return null;
        }
        cam.transform.SetPositionAndRotation(b.position, b.rotation);
    }

    IEnumerator Lens(float from, float to, float seconds)
    {
        if (!cam) yield break;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            cam.fieldOfView = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        cam.fieldOfView = to;
    }

    IEnumerator Drift(Transform shot, float distance, float seconds)
    {
        Vector3 p0 = shot.position, p1 = shot.position + shot.forward * distance;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            cam.transform.position = Vector3.Lerp(p0, p1, t / seconds);
            yield return null;
        }
    }

    // ------------------------------------------------------------------ helpers

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static IEnumerator Volume(AudioSource s, float to, float seconds)
    {
        if (!s) yield break;
        float from = s.volume;
        for (float t = 0f; t < seconds; t += Time.deltaTime) { s.volume = Mathf.Lerp(from, to, t / seconds); yield return null; }
        s.volume = to;
    }

    static IEnumerator Fade(Graphic g, float from, float to, float seconds)
    {
        if (!g) yield break;
        for (float t = 0f; t < seconds; t += Time.deltaTime) { SetAlpha(g, Mathf.Lerp(from, to, t / seconds)); yield return null; }
        SetAlpha(g, to);
    }

    static void SetAlpha(Graphic g, float a)
    {
        if (!g) return;
        var c = g.color;
        c.a = a;
        g.color = c;
    }
}
