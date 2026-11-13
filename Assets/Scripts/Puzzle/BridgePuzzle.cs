using System.Collections;
using UnityEngine;

/// <summary>
/// The Broken Bridge puzzle (first puzzle). Holds the shared state of both worlds:
///  1. Present digs all Sand Mounds  -> Ancient Path revealed, Memory Seed appears at the last mound,
///     the Root Socket in the forest wakes up.
///  2. Present drops the seed in the Memory River -> it appears on the forest bank.
///  3. Future places it in the Root Socket and holds Power -> roots grow across the Gap
///     while the Ancient Path stones rise out of the desert Chasm.
///  4. Bridge complete -> both bridges become walkable. Present takes the Mirror Fragment.
/// </summary>
public class BridgePuzzle : MonoBehaviour
{
    public static BridgePuzzle Instance { get; private set; }

    public PuzzleHUD hud;

    [Header("Desert (Present)")]
    public SandMound[] mounds;
    public Carryable memorySeed;
    [Tooltip("Path stones that rise out of the Chasm, in order from near edge to far edge.")]
    public Transform[] desertBridgeStones;
    public float stoneHiddenDepth = 8f;
    public Collider desertBridgeWalkway;
    public Light fragmentLight;

    [Header("Forest (Future)")]
    public RootSocket rootSocket;
    [Tooltip("Root pieces that grow along their local Z (scale z from 0 to full).")]
    public Transform[] bridgeRoots;
    public Collider forestBridgeWalkway;

    [Header("Ending")]
    public string fragmentTitle = "Mirror Fragment found";
    public string fragmentMemory = "Recognizing pain.";

    Vector3[] stoneTargets;
    float[] rootLengths;
    int moundsDug;

    void Awake()
    {
        Instance = this;

        stoneTargets = new Vector3[desertBridgeStones.Length];
        for (int i = 0; i < desertBridgeStones.Length; i++)
        {
            stoneTargets[i] = desertBridgeStones[i].position;
            desertBridgeStones[i].gameObject.SetActive(false);
        }
        rootLengths = new float[bridgeRoots.Length];
        for (int i = 0; i < bridgeRoots.Length; i++) rootLengths[i] = bridgeRoots[i].localScale.z;

        SetBridgeGrowth(0f);
        if (desertBridgeWalkway) desertBridgeWalkway.enabled = false;
        if (forestBridgeWalkway) forestBridgeWalkway.enabled = false;
        if (memorySeed) memorySeed.gameObject.SetActive(false);
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(1.5f);
        if (!hud) yield break;
        hud.ShowMessage(0, "The sand hides something... (hold Power near a mound)", 6f);
        hud.ShowMessage(1, "The roots can't reach the other side of the gap.", 6f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- events

    public void OnMoundDug(SandMound m)
    {
        moundsDug++;
        if (moundsDug < mounds.Length)
        {
            hud?.ShowMessage(0, $"An ancient path lies beneath the sand... ({moundsDug}/{mounds.Length})");
            return;
        }

        // Last mound: the seed appears where it was.
        if (memorySeed)
        {
            memorySeed.gameObject.SetActive(true);
            Vector3 p = m.transform.position;
            memorySeed.PlaceAt(new Vector3(p.x, 0.35f, p.z)); // ground level, not the sunken mound
        }
        if (rootSocket) rootSocket.Wake();
        hud?.ShowMessage(0, "A glowing seed was buried here. Maybe the river can carry it...", 6f);
        hud?.ShowMessage(1, "The stone socket by the gap starts to hum...", 5f);
    }

    public void OnObjectCrossedRiver(Carryable c, MemoryRiver from, MemoryRiver to)
    {
        if (c != memorySeed || !hud) return;
        int fromSide = from.transform.position.x < to.transform.position.x ? 0 : 1;
        hud.ShowMessage(fromSide, "The river took the seed away...");
        hud.ShowMessage(1 - fromSide, "Something glowing washed up on the riverbank!", 5f);
    }

    public void OnSeedPlaced()
    {
        hud?.ShowMessage(1, "The seed takes root. Hold Power to make the roots grow!", 5f);
        hud?.ShowMessage(0, "The ground trembles...", 3f);
    }

    /// <summary>0..1 — roots extend in the forest, path stones rise in the desert, at the same time.</summary>
    public void SetBridgeGrowth(float k)
    {
        for (int i = 0; i < bridgeRoots.Length; i++)
        {
            var s = bridgeRoots[i].localScale;
            s.z = Mathf.Max(0.001f, rootLengths[i] * k);
            bridgeRoots[i].localScale = s;
        }

        int n = desertBridgeStones.Length;
        for (int i = 0; i < n; i++)
        {
            float local = Mathf.Clamp01(k * 1.6f - i * (0.6f / Mathf.Max(1, n - 1)));
            var stone = desertBridgeStones[i];
            stone.gameObject.SetActive(local > 0f);
            float e = 1f - (1f - local) * (1f - local);
            stone.position = stoneTargets[i] + Vector3.down * (stoneHiddenDepth * (1f - e));
        }
    }

    public void OnBridgeComplete()
    {
        SetBridgeGrowth(1f);
        if (desertBridgeWalkway) desertBridgeWalkway.enabled = true;
        if (forestBridgeWalkway) forestBridgeWalkway.enabled = true;
        hud?.ShowMessage(0, "The ancient path became a bridge! Cross it.", 5f);
        hud?.ShowMessage(1, "The roots reached the other side. Your other self can cross now.", 5f);
        if (fragmentLight) StartCoroutine(Pulse(fragmentLight, 2f, 6f));
    }

    public void OnFragmentCollected(MirrorFragment f)
    {
        hud?.ShowBoth($"{fragmentTitle}\n<size=30><i>{fragmentMemory}</i></size>", 6f);
        if (fragmentLight) StartCoroutine(Pulse(fragmentLight, fragmentLight.intensity, 0f));
        Debug.Log("[BridgePuzzle] Puzzle complete: Mirror Fragment collected.");
    }

    static IEnumerator Pulse(Light l, float from, float to)
    {
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            l.intensity = Mathf.Lerp(from, to, t / 1.5f);
            yield return null;
        }
        l.intensity = to;
    }
}
