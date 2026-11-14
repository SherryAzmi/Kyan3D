using System.Collections;
using UnityEngine;

/// <summary>
/// Puzzle 2, The Dry Bed (second fragment: Accepting the past). Holds the shared state of both worlds:
///  1. The Memory River is dry. The Present touches it -> ripples reach the forest and the root wheel wakes.
///  2. The Future holds Power on the root wheel -> the spring gate opens and the river flows
///     down the bed in BOTH worlds (the desert sand along it turns wet and soft).
///  3. The Present digs the 3 wet mounds; the last one uncovers a broken Picture Frame.
///  4. The Present drops the frame in the Memory River -> it appears on the forest bank.
///  5. The Future places it on the pedestal -> the root cage around the altar sinks.
///  6. The Future takes the Mirror Fragment -> FragmentCutscene -> next scene.
/// A light beam in each world points that player at their next target (same as Puzzle 1).
/// </summary>
public class DryBedPuzzle : MonoBehaviour
{
    public static DryBedPuzzle Instance { get; private set; }

    public PuzzleHUD hud;

    [Header("Players")]
    public Transform present;
    public Transform future;

    [Header("Memory River (both worlds)")]
    public MemoryRiver desertRiver;
    public MemoryRiver forestRiver;
    [Tooltip("Water strips (children of each river). Scaled along local Z as the river fills.")]
    public Transform desertWater;
    public Transform forestWater;
    public Renderer desertWaterRenderer;
    public float riverStartZ = -20f;
    public float riverLength = 23f;
    public float fillTime = 6f;

    [Header("Forest (Future)")]
    public RootWheel wheel;
    public Transform springGate;
    public float gateDropDepth = 3f;
    public Light springGlow;
    public FramePedestal pedestal;
    [Tooltip("Roots that ring the altar. They sink when the frame is placed.")]
    public Transform[] cageRoots;
    public float cageDropDepth = 3.6f;
    public MirrorFragment fragment;
    public Light fragmentLight;

    [Header("Desert (Present)")]
    public WetSandMound[] mounds;
    [Tooltip("Dark patches of wet sand that spread under each mound once the river has flowed.")]
    public Transform[] wetPatches;
    public Material wetMoundMaterial;
    public Carryable frame;

    [Header("Guidance")]
    public GuideBeacon desertBeacon;
    public GuideBeacon forestBeacon;
    [Tooltip("World X above which a position counts as the forest.")]
    public float forestMinX = 150f;

    [Header("Ending")]
    public FragmentCutscene fragmentCutscene;
    public Color desertRiverBrightColor = new Color(0.25f, 0.85f, 1f, 0.7f);

    float fill;
    bool filling;
    int moundsDug;
    bool signalSent, gateOpened, riverFilled, frameSent, framePlaced, cageOpen, fragmentTaken;
    float springGlowBase;
    Vector3[] patchScales;

    void Awake()
    {
        Instance = this;

        patchScales = new Vector3[wetPatches.Length];
        for (int i = 0; i < wetPatches.Length; i++)
        {
            patchScales[i] = wetPatches[i].localScale;
            wetPatches[i].localScale = new Vector3(0.01f, patchScales[i].y, 0.01f);
        }

        SetFill(0f);
        if (frame) frame.gameObject.SetActive(false);
        if (fragment) fragment.enabled = false;           // dormant until the cage opens
        if (springGlow) { springGlowBase = springGlow.intensity; springGlow.intensity = springGlowBase * 0.15f; }
    }

    IEnumerator Start()
    {
        if (desertRiver) { desertRiver.onTouched += OnRiverTouched; desertRiver.onCrossed += OnObjectCrossed; }
        if (forestRiver) { forestRiver.onTouched += OnRiverTouched; forestRiver.onCrossed += OnObjectCrossed; }
        foreach (var m in mounds) if (m) m.onDug += OnMoundDug;
        if (fragment) fragment.onCollected += OnFragmentCollected;

        yield return new WaitForSeconds(1.5f);
        if (!hud) yield break;
        hud.ShowMessage(0, "The river bed is dry. Touch it and see if anyone answers.", 6f);
        hud.ShowMessage(1, "The spring is blocked. Something is wrong with the old gate.", 6f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- update: river fill + guidance

    void Update()
    {
        if (filling)
        {
            fill = Mathf.Clamp01(fill + Time.deltaTime / fillTime);
            SetFill(fill);
            if (fill >= 1f) { filling = false; OnRiverFilled(); }
        }

        Vector3? d = null, f = null;
        if (fragmentTaken) { }
        else if (cageOpen) { if (fragment) f = fragment.transform.position; }
        else if (framePlaced) { }
        else if (!signalSent) d = RiverPointNear(desertRiver, present ? present.position : Vector3.zero);
        else if (!gateOpened) { if (wheel) f = wheel.transform.position; }
        else if (!riverFilled) { }
        else if (moundsDug < mounds.Length) d = NearestUndugMound();
        else if (frame)
        {
            var holder = frame.Holder;
            Vector3 framePos = frame.transform.position;
            Vector3? pedPos = pedestal ? pedestal.transform.position : (Vector3?)null;
            if (holder == null)
            {
                if (InForest(framePos)) f = framePos;
                else { d = framePos; f = pedPos; }
            }
            else if (InForest(holder.transform.position)) f = pedPos;
            else { d = RiverPointNear(desertRiver, holder.transform.position); f = pedPos; }
        }

        if (desertBeacon) desertBeacon.SetTarget(d);
        if (forestBeacon) forestBeacon.SetTarget(f);
    }

    bool InForest(Vector3 p) => p.x > forestMinX;

    Vector3? NearestUndugMound()
    {
        Vector3 from = present ? present.position : Vector3.zero;
        Vector3? best = null;
        float bestD = float.MaxValue;
        foreach (var m in mounds)
        {
            if (!m || m.Dug) continue;
            float dist = (m.transform.position - from).sqrMagnitude;
            if (dist < bestD) { bestD = dist; best = m.transform.position; }
        }
        return best;
    }

    static Vector3? RiverPointNear(MemoryRiver river, Vector3 p)
    {
        if (!river) return null;
        float z = Mathf.Clamp(river.transform.InverseTransformPoint(p).z, river.zMin + 1f, river.zMax - 1f);
        return river.transform.TransformPoint(new Vector3(0f, 0f, z));
    }

    // ---------------------------------------------------------------- river water

    void SetFill(float k)
    {
        ApplyFill(desertWater, k);
        ApplyFill(forestWater, k);
    }

    void ApplyFill(Transform w, float k)
    {
        if (!w) return;
        var r = w.GetComponent<Renderer>();
        if (r) r.enabled = k > 0.001f;
        var s = w.localScale;
        s.z = Mathf.Max(0.001f, riverLength * k);
        w.localScale = s;
        var p = w.localPosition;
        p.z = riverStartZ + riverLength * k * 0.5f;
        w.localPosition = p;
    }

    // ---------------------------------------------------------------- events

    void OnRiverTouched(MemoryRiver river, float z, bool carrying)
    {
        if (signalSent || carrying || river != desertRiver) return;
        signalSent = true;
        if (wheel) wheel.Wake();
        hud?.ShowMessage(0, "The dry bed ripples... someone heard you.", 5f);
        hud?.ShowMessage(1, "A ripple reached the spring. The root wheel begins to glow.", 6f);
    }

    public void OnGateOpened()
    {
        if (gateOpened) return;
        gateOpened = true;
        filling = true;
        if (springGate) StartCoroutine(Sink(springGate, gateDropDepth, 1.5f));
        if (springGlow) StartCoroutine(PulseLight(springGlow, springGlow.intensity, springGlowBase, 2f));
        hud?.ShowMessage(1, "The gate opens. Water pours down the river!", 5f);
        hud?.ShowMessage(0, "Something is coming down the river...", 5f);
    }

    void OnRiverFilled()
    {
        riverFilled = true;
        foreach (var m in mounds)
        {
            if (!m) continue;
            m.unlocked = true;
            var r = m.GetComponent<Renderer>();
            if (r && wetMoundMaterial) r.sharedMaterial = wetMoundMaterial;
        }
        StartCoroutine(GrowPatches());
        hud?.ShowMessage(0, "The water reached the desert! The sand beside it turns soft. Dig.", 6f);
        hud?.ShowMessage(1, "Your other self can dig now. Keep watching the river.", 5f);
    }

    void OnMoundDug(SandMound m)
    {
        moundsDug++;
        if (moundsDug < mounds.Length)
        {
            hud?.ShowMessage(0, $"The wet sand gives way... ({moundsDug}/{mounds.Length})");
            return;
        }

        if (frame)
        {
            frame.gameObject.SetActive(true);
            Vector3 p = m.transform.position;
            frame.PlaceAt(new Vector3(p.x, 0.5f, p.z));
        }
        hud?.ShowMessage(0, "A broken picture frame was buried here. Maybe the river can carry it...", 6f);
        hud?.ShowMessage(1, "A pedestal waits in the forest. It feels like it's missing something.", 5f);
    }

    void OnObjectCrossed(Carryable c, MemoryRiver from, MemoryRiver to)
    {
        if (c != frame || frameSent) return;
        frameSent = true;
        int fromSide = from == desertRiver ? 0 : 1;
        hud?.ShowMessage(fromSide, "The river took the frame away...");
        hud?.ShowMessage(1 - fromSide, "Something washed up on the riverbank: a broken picture frame!", 6f);
    }

    public void OnFramePlaced()
    {
        framePlaced = true;
        hud?.ShowMessage(1, "The frame fits. The roots around the altar loosen...", 5f);
        hud?.ShowMessage(0, "The ground trembles...", 3f);
        StartCoroutine(OpenCage());
    }

    IEnumerator OpenCage()
    {
        var start = new Vector3[cageRoots.Length];
        for (int i = 0; i < cageRoots.Length; i++) start[i] = cageRoots[i].position;
        const float time = 3f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float e = Mathf.SmoothStep(0f, 1f, t / time);
            for (int i = 0; i < cageRoots.Length; i++)
                cageRoots[i].position = start[i] + Vector3.down * (cageDropDepth * e);
            yield return null;
        }
        foreach (var r in cageRoots)
        {
            var col = r.GetComponent<Collider>();
            if (col) col.enabled = false;
            r.gameObject.SetActive(false);
        }
        cageOpen = true;
        if (fragment) fragment.enabled = true;
        if (fragmentLight) StartCoroutine(PulseLight(fragmentLight, fragmentLight.intensity, 2.2f, 1.5f));
        hud?.ShowMessage(1, "The roots sank. The Mirror Fragment is waiting.", 6f);
    }

    void OnFragmentCollected(MirrorFragment f)
    {
        fragmentTaken = true;
        if (fragmentLight) StartCoroutine(PulseLight(fragmentLight, fragmentLight.intensity, 0f, 1.5f));
        if (desertWaterRenderer) StartCoroutine(BrightenRiver());
        if (springGlow) StartCoroutine(PulseLight(springGlow, springGlow.intensity, springGlowBase * 1.8f, 2f));
        Debug.Log("[DryBedPuzzle] Puzzle complete: Mirror Fragment 2 collected.");
        if (fragmentCutscene) fragmentCutscene.Play();
        else hud?.ShowBoth("Mirror Fragment found", 6f);
    }

    // ---------------------------------------------------------------- small animations

    IEnumerator GrowPatches()
    {
        const float time = 2.5f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float e = 1f - (1f - t / time) * (1f - t / time);
            for (int i = 0; i < wetPatches.Length; i++)
                wetPatches[i].localScale = new Vector3(patchScales[i].x * e, patchScales[i].y, patchScales[i].z * e);
            yield return null;
        }
        for (int i = 0; i < wetPatches.Length; i++) wetPatches[i].localScale = patchScales[i];
    }

    IEnumerator BrightenRiver()
    {
        var mat = desertWaterRenderer.material; // own instance
        Color from = mat.GetColor("_BaseColor");
        for (float t = 0f; t < 3f; t += Time.deltaTime)
        {
            mat.SetColor("_BaseColor", Color.Lerp(from, desertRiverBrightColor, t / 3f));
            yield return null;
        }
        mat.SetColor("_BaseColor", desertRiverBrightColor);
    }

    static IEnumerator Sink(Transform t, float depth, float time)
    {
        Vector3 from = t.position;
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            t.position = from + Vector3.down * (depth * Mathf.SmoothStep(0f, 1f, e / time));
            yield return null;
        }
        t.position = from + Vector3.down * depth;
    }

    static IEnumerator PulseLight(Light l, float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            l.intensity = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        l.intensity = to;
    }
}
