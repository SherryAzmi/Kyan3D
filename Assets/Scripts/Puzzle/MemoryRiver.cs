using UnityEngine;

/// <summary>
/// The Memory River in one world. Each world has one, linked through 'other'.
/// - Touch the water: ripples appear at the same spot in BOTH worlds (communication).
/// - Drop a carried object in: it appears on the other world's bank (object transfer).
/// The river runs along this object's local Z axis between zMin and zMax.
/// </summary>
public class MemoryRiver : Interactable
{
    public MemoryRiver other;
    public float zMin = -20f, zMax = 3f;
    [Tooltip("Half-width of the water strip (metres).")]
    public float halfWidth = 2f;
    [Tooltip("Local X offset of the bank where transferred objects appear.")]
    public float bankX = -3f;
    public Material rippleMaterial;
    public Renderer water;

    /// <summary>Raised on every Interact at the water: (this river, local z, true if the player was carrying something).</summary>
    public System.Action<MemoryRiver, float, bool> onTouched;
    /// <summary>Raised when a carried object has been sent to the other world: (object, from, to).</summary>
    public System.Action<Carryable, MemoryRiver, MemoryRiver> onCrossed;

    public override float DistanceTo(Vector3 p)
    {
        Vector3 local = transform.InverseTransformPoint(p);
        if (Mathf.Abs(local.y) > 3f) return float.MaxValue;
        float dz = local.z < zMin ? zMin - local.z : local.z > zMax ? local.z - zMax : 0f;
        float dx = Mathf.Max(0f, Mathf.Abs(local.x) - halfWidth);
        return new Vector2(dx, dz).magnitude;
    }

    public override string Prompt(PlayerInteractor p) =>
        p.Carried ? $"{{I}} Let the river carry the {p.Carried.displayName}" : "{I} Touch the water";

    public override bool HandlesInteract(PlayerInteractor p) => true;

    public override void Interact(PlayerInteractor p)
    {
        float z = Mathf.Clamp(transform.InverseTransformPoint(p.transform.position).z, zMin + 1f, zMax - 1f);
        bool carrying = p.Carried;
        Ripple(z);
        if (other) other.Ripple(z);
        var a = GameAudio.I;
        if (a)
        {
            GameAudio.Play(a.splash, transform.position);
            if (other) GameAudio.Play(a.splash, other.transform.position, 0.5f); // the echo in the other world
        }
        onTouched?.Invoke(this, z, carrying);
        MirrorCrackDivider.Pulse(carrying ? 1.2f : 0.8f); // the crack flares: something reached the other world

        if (p.Carried && other)
        {
            if (a) GameAudio.Play(a.arrive, other.transform.position);
            var c = p.TakeCarried();
            c.PlaceAt(other.transform.TransformPoint(new Vector3(other.bankX, 0.35f, z)));
            if (BridgePuzzle.Instance) BridgePuzzle.Instance.OnObjectCrossedRiver(c, this, other);
            onCrossed?.Invoke(c, this, other);
        }
    }

    public void Ripple(float localZ)
    {
        Vector3 pos = transform.TransformPoint(new Vector3(0f, 0.08f, localZ));
        RippleFX.Spawn(pos, rippleMaterial, 0f);
        RippleFX.Spawn(pos, rippleMaterial, 0.35f);
        RippleFX.Spawn(pos, rippleMaterial, 0.7f);
    }
}
