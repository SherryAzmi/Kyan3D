using UnityEngine;

/// <summary>
/// Footstep sounds that match the ground: sand in the desert (Present), grass in the forest (Future).
/// One step every 'stride' metres walked while grounded; a random variation each time, panned to the
/// player's half of the screen. Silent in the river (RiverWading plays the splashes there).
/// </summary>
public class Footsteps : MonoBehaviour
{
    [Tooltip("Metres walked per footstep.")]
    public float stride = 1.5f;
    [Range(0f, 1f)] public float volume = 0.55f;
    [Tooltip("World X from which the ground is forest (grass).")]
    public float forestMinX = 150f;
    [Tooltip("World X from which we are past the forest (e.g. an ending room): no ground sound.")]
    public float forestMaxX = 450f;

    CharacterController cc;
    Vector3 last;
    float walked;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        last = transform.position;
    }

    void Update()
    {
        var a = GameAudio.I;
        Vector3 p = transform.position;
        Vector3 d = p - last;
        d.y = 0f;
        last = p;
        float m = d.magnitude;
        if (!a || m > 2f) { walked = 0f; return; }               // teleport / respawn
        if (cc && !cc.isGrounded && cc.velocity.y < -1.5f) return; // falling or jumping
        walked += m;
        if (walked < stride) return;
        walked = 0f;
        if (InWater(p) || p.x >= forestMaxX) return;

        var set = p.x >= forestMinX ? a.grassSteps : a.sandSteps;
        if (set == null || set.Length == 0) return;
        GameAudio.Play(set[Random.Range(0, set.Length)], GameAudio.SideOf(p), volume * Random.Range(0.8f, 1f), Random.Range(0.9f, 1.12f));
    }

    static bool InWater(Vector3 p)
    {
        foreach (var it in Interactable.All)
        {
            if (!(it is MemoryRiver r) || !r.water || !r.water.enabled) continue;
            Bounds b = r.water.bounds;
            if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z && r.DistanceTo(p) < 0.01f) return true;
        }
        return false;
    }
}
