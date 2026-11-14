using UnityEngine;

/// <summary>
/// Sits on a MemoryRiver. While a player walks through the water: small ripples at their feet and
/// splashy footstep sounds. When a player is near the river: a soft flowing-water loop that fades in.
/// Only reacts where the water is actually visible (so a dry or half-filled river stays silent there).
/// </summary>
[RequireComponent(typeof(MemoryRiver))]
public class RiverWading : MonoBehaviour
{
    [Tooltip("Seconds between footsteps while walking in the water.")]
    public float stepInterval = 0.38f;
    [Tooltip("Minimum walking speed (m/s) that counts as stepping.")]
    public float minSpeed = 0.6f;
    [Tooltip("Size of the footstep ripple (metres).")]
    public float rippleSize = 1.3f;
    [Tooltip("Players closer than this (metres) hear the river flowing.")]
    public float hearDistance = 9f;
    [Range(0f, 1f)] public float flowVolume = 0.45f;

    class Wader
    {
        public Transform t;
        public Vector3 last;
        public float nextStep;
    }

    MemoryRiver river;
    Wader[] waders;

    void Start()
    {
        river = GetComponent<MemoryRiver>();
        var ccs = FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
        waders = new Wader[ccs.Length];
        for (int i = 0; i < ccs.Length; i++) waders[i] = new Wader { t = ccs[i].transform, last = ccs[i].transform.position };
    }

    bool WaterAt(Vector3 p)
    {
        var w = river.water;
        if (!w || !w.enabled || !w.gameObject.activeInHierarchy) return false;
        Bounds b = w.bounds;
        return p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z && Mathf.Abs(p.y - b.center.y) < 1.5f;
    }

    void Update()
    {
        if (!river || waders == null) return;
        float nearest = float.MaxValue;

        foreach (var wd in waders)
        {
            if (!wd.t) continue;
            Vector3 p = wd.t.position;
            Vector3 d = p - wd.last;
            d.y = 0f;
            float speed = d.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            wd.last = p;

            float dist = river.DistanceTo(p);
            if (dist < nearest && (river.water && river.water.enabled)) nearest = dist;

            if (speed > 30f || speed < minSpeed || !WaterAt(p)) continue; // teleports and standing still don't splash
            if (Time.time < wd.nextStep) continue;
            wd.nextStep = Time.time + stepInterval * Mathf.Clamp(3f / speed, 0.6f, 1.3f);

            Vector3 at = new Vector3(p.x, river.water.bounds.max.y + 0.01f, p.z);
            RippleFX.Spawn(at, river.rippleMaterial, 0f, rippleSize, 0.9f);
            var a = GameAudio.I;
            if (a) GameAudio.Play(a.waterStep, GameAudio.SideOf(p), Random.Range(0.6f, 0.9f), Random.Range(0.85f, 1.15f));
        }

        // The river flows audibly when someone is near it.
        var ga = GameAudio.I;
        if (ga && nearest < hearDistance)
        {
            float v = flowVolume * (1f - nearest / hearDistance);
            GameAudio.Hold("riverNear" + GetInstanceID(), ga.waterFlow, GameAudio.SideOf(transform.position), 1f, v);
        }
    }
}
