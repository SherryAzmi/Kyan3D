using System.Collections;
using UnityEngine;

/// <summary>
/// Puzzle 3 — The Broken Mirror ("Making peace with oneself").
///  0. The two fragments from earlier puzzles fly into the mirror in both worlds.
///  1. Present digs the Sand Mound -> desert half of the last shard.
///     Future blooms the root      -> forest half of the last shard.
///  2. Each places their half in their own mirror (it shows in BOTH mirrors).
///  3. Both hold Power at their mirror at the same time (forgiving: 1.5 s of slack, progress
///     pauses instead of resetting) -> the mirror becomes whole -> MirrorEnding.
/// </summary>
public class MirrorPuzzle : MonoBehaviour
{
    public static MirrorPuzzle Instance { get; private set; }

    [System.Serializable]
    public class MirrorVisual
    {
        public Transform piece1, piece2;   // fragments from Puzzle 1 and 2
        public GameObject halfDesert, halfForest, seam;
        public Light glow;
    }

    public PuzzleHUD hud;
    public Transform present, future;

    [Header("Desert (Present)")]
    public SandMound shardMound;
    public Carryable desertHalf;
    public MirrorSlot desertSlot;

    [Header("Forest (Future)")]
    public BloomSpot bloomSpot;
    public Carryable forestHalf;
    public MirrorSlot forestSlot;

    [Header("Mirrors (index 0 = desert, 1 = forest)")]
    public MirrorVisual[] mirrors = new MirrorVisual[2];

    [Header("Join")]
    public float joinTime = 3f;
    [Tooltip("A hold still counts this long after the player lets go.")]
    public float holdGrace = 1.5f;

    [Header("Guidance")]
    public GuideBeacon desertBeacon;
    public GuideBeacon forestBeacon;

    [Header("Ending")]
    public MirrorEnding ending;

    readonly bool[] placed = new bool[2];
    readonly float[] lastHold = { -99f, -99f };
    float nextLonelyHint;
    Vector3[] piece1Home = new Vector3[2], piece2Home = new Vector3[2];

    public bool Joined { get; private set; }
    public float JoinProgress { get; private set; }
    public bool BothPlaced => placed[0] && placed[1];
    public bool HalfPlaced(int world) => placed[world];
    public bool OtherHolding(int world) => Holding(1 - world);
    bool Holding(int world) => Time.time - lastHold[world] < holdGrace;

    void Awake()
    {
        Instance = this;
        if (desertHalf) desertHalf.gameObject.SetActive(false);
        if (forestHalf) forestHalf.gameObject.SetActive(false);
        for (int i = 0; i < mirrors.Length; i++)
        {
            var m = mirrors[i];
            if (m.halfDesert) m.halfDesert.SetActive(false);
            if (m.halfForest) m.halfForest.SetActive(false);
            if (m.seam) m.seam.SetActive(false);
            if (m.piece1) { piece1Home[i] = m.piece1.position; m.piece1.gameObject.SetActive(false); }
            if (m.piece2) { piece2Home[i] = m.piece2.position; m.piece2.gameObject.SetActive(false); }
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);
        // The fragments already found fly home into the mirror, in both worlds.
        for (int i = 0; i < mirrors.Length; i++)
        {
            StartCoroutine(FlyIn(mirrors[i].piece1, piece1Home[i], 0f));
            StartCoroutine(FlyIn(mirrors[i].piece2, piece2Home[i], 0.8f));
        }
        if (GameAudio.I)
        {
            GameAudio.Play(GameAudio.I.arrive, -1, 0.7f);
            yield return new WaitForSeconds(0.8f);
            GameAudio.Play(GameAudio.I.arrive, -1, 0.7f, 1.12f);
            yield return new WaitForSeconds(1.8f);
        }
        else yield return new WaitForSeconds(2.6f);
        hud?.ShowMessage(0, "The same mirror... two pieces found, one still missing.\nSomething is buried in the sand.", 6f);
        hud?.ShowMessage(1, "The same mirror... two pieces found, one still missing.\nSomething waits beneath the moss.", 6f);
    }

    static IEnumerator FlyIn(Transform piece, Vector3 home, float delay)
    {
        if (!piece) yield break;
        yield return new WaitForSeconds(delay);
        piece.gameObject.SetActive(true);
        Vector3 from = home + new Vector3(0f, 6f, -4f);
        Quaternion rot = piece.rotation;
        for (float t = 0f; t < 1.6f; t += Time.deltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / 1.6f, 3f);
            piece.position = Vector3.Lerp(from, home, k);
            piece.rotation = rot * Quaternion.Euler(0f, 0f, (1f - k) * 360f);
            yield return null;
        }
        piece.position = home;
        piece.rotation = rot;
    }

    // ---------------------------------------------------------------- events

    public void OnMoundDug(SandMound m)
    {
        if (m != shardMound || !desertHalf) return;
        desertHalf.gameObject.SetActive(true);
        Vector3 p = m.transform.position;
        desertHalf.PlaceAt(new Vector3(p.x, 0.45f, p.z));
        hud?.ShowMessage(0, "Half of a mirror shard. Bring it to the mirror.", 5f);
    }

    public void OnBloomed(BloomSpot b)
    {
        if (b != bloomSpot || !forestHalf) return;
        forestHalf.gameObject.SetActive(true);
        forestHalf.PlaceAt(b.top ? b.top.position : b.transform.position + Vector3.up);
        hud?.ShowMessage(1, "The root lifted half of a mirror shard. Bring it to the mirror.", 5f);
    }

    public void OnHalfPlaced(int world, Carryable c)
    {
        placed[world] = true;
        MirrorCrackDivider.Pulse(1.2f);
        if (c) c.gameObject.SetActive(false);
        if (GameAudio.I)
        {
            GameAudio.Play(GameAudio.I.place, world);
            GameAudio.Play(GameAudio.I.arrive, 1 - world, 0.6f); // it shows up in the other mirror too
        }
        foreach (var m in mirrors)
        {
            var half = world == 0 ? m.halfDesert : m.halfForest;
            if (half) half.SetActive(true);
            if (m.seam) m.seam.SetActive(BothPlaced);
        }
        if (BothPlaced)
            hud?.ShowBoth("Both halves are here. Hold Power together to make it whole.", 6f);
        else
        {
            hud?.ShowMessage(world, "Your half is in place. The other half must come from the other world.", 5f);
            hud?.ShowMessage(1 - world, "A piece just appeared in your mirror... your other self placed it!", 5f);
        }
    }

    public void ReportHold(int world) => lastHold[world] = Time.time;

    // ---------------------------------------------------------------- update

    void Update()
    {
        if (BothPlaced && !Joined)
        {
            bool a = Holding(0), b = Holding(1);
            if (a && b)
            {
                JoinProgress = Mathf.Clamp01(JoinProgress + Time.deltaTime * PlayerInteractor.HoldSpeed / joinTime);
                MirrorCrackDivider.Hold(JoinProgress);
                if (GameAudio.I) GameAudio.Hold("join", GameAudio.I.joinHum, -1, 0.8f + 0.7f * JoinProgress, 0.6f + 0.4f * JoinProgress);
                foreach (var m in mirrors) if (m.glow) m.glow.intensity = 1f + 5f * JoinProgress;
                if (JoinProgress >= 1f) Join();
            }
            else if ((a || b) && Time.time > nextLonelyHint)
            {
                nextLonelyHint = Time.time + 4f;
                hud?.ShowMessage(a ? 1 : 0, "Your other self is holding the mirror... hold Power with them!", 3f);
            }
        }
        UpdateGuides();
    }

    void Join()
    {
        Joined = true;
        MirrorCrackDivider.Pulse(2f);
        if (GameAudio.I) GameAudio.Play(GameAudio.I.mirrorWhole);
        foreach (var m in mirrors) if (m.seam) m.seam.SetActive(false);
        if (desertBeacon) desertBeacon.SetTarget(null);
        if (forestBeacon) forestBeacon.SetTarget(null);
        Debug.Log("[MirrorPuzzle] Mirror whole. Starting the ending.");
        if (ending) ending.Play();
        else hud?.ShowBoth("The mirror is whole.", 6f);
    }

    void UpdateGuides()
    {
        if (Joined) return;
        if (desertBeacon) desertBeacon.SetTarget(Target(0, shardMound && !shardMound.Dug ? shardMound.transform : null, desertHalf, desertSlot));
        if (forestBeacon) forestBeacon.SetTarget(Target(1, bloomSpot && !bloomSpot.Bloomed ? bloomSpot.transform : null, forestHalf, forestSlot));
    }

    Vector3? Target(int world, Transform source, Carryable half, MirrorSlot slot)
    {
        if (placed[world]) return slot ? slot.transform.position : (Vector3?)null; // wait / hold at the mirror
        if (source) return source.position;
        if (half && half.gameObject.activeInHierarchy && !half.Holder) return half.transform.position;
        return slot ? slot.transform.position : (Vector3?)null;
    }
}
