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
    [Tooltip("Off = a decorative river: you can touch it, but it won't carry objects to the other world.")]
    public bool canCarry = true;

    bool HasWater => water && water.enabled && water.gameObject.activeInHierarchy;

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

    /// <summary>The nearest stretch of water to the player, not the river's pivot.</summary>
    public override Vector3 FocusPoint(Vector3 from)
    {
        Vector3 local = transform.InverseTransformPoint(from);
        local.z = Mathf.Clamp(local.z, zMin, zMax);
        local.x = Mathf.Clamp(local.x, -halfWidth * 0.5f, halfWidth * 0.5f);
        local.y = 0f;
        return transform.TransformPoint(local);
    }

    /// <summary>
    /// The water's edge on the player's own side, level with the player (for the guide beam): no need
    /// to cross the river or walk to its end to reach it. Follows the river's curves.
    /// </summary>
    public Vector3 NearBankPoint(Vector3 from)
    {
        Vector3 local = transform.InverseTransformPoint(from);
        float z = Mathf.Clamp(local.z, zMin + 1f, zMax - 1f);
        float worldZ = transform.TransformPoint(new Vector3(0f, 0f, z)).z;
        var waves = water ? water.GetComponent<RiverWaves>() : null;
        bool curvy = waves;
        float centre = curvy ? waves.meander * RiverWaves.Meander(worldZ) : 0f;
        float half = halfWidth * (curvy ? RiverWaves.WidthScale(worldZ) : 1f);
        float side = local.x >= centre ? 1f : -1f;
        float x = centre + side * Mathf.Max(0.2f, half - 0.45f); // just inside the near edge
        // stay inside the touch range of the straight interaction strip
        x = Mathf.Clamp(x, -(halfWidth + range * 0.8f), halfWidth + range * 0.8f);
        return transform.TransformPoint(new Vector3(x, 0f, z));
    }

    public override string Prompt(PlayerInteractor p)
    {
        if (p.Carried) return canCarry ? $"{{I}} Let the river carry the {p.Carried.displayName}" : null;
        return HasWater ? "{I} Touch the water" : "{I} Touch the dry riverbed";
    }

    public override bool HandlesInteract(PlayerInteractor p) => !p.Carried || canCarry;

    public override void Interact(PlayerInteractor p)
    {
        float z = Mathf.Clamp(transform.InverseTransformPoint(p.transform.position).z, zMin + 1f, zMax - 1f);
        bool carrying = p.Carried;
        Ripple(z);
        if (other) other.Ripple(z);
        var a = GameAudio.I;
        if (a && HasWater)
        {
            GameAudio.Play(a.splash, transform.position);
            if (other) GameAudio.Play(a.splash, other.transform.position, 0.5f); // the echo in the other world
        }
        onTouched?.Invoke(this, z, carrying);
        MirrorCrackDivider.Pulse(carrying ? 1.2f : 0.8f); // the crack flares: something reached the other world

        if (p.Carried && other && canCarry)
        {
            var c = p.TakeCarried();
            c.PlaceAt(other.transform.TransformPoint(new Vector3(other.bankX, 0.35f, z)));
            // throw sound + neon splash here, then it pops out on the other bank with its own burst and sound
            NeonFX.RiverThrow(transform.TransformPoint(new Vector3(0f, 0.05f, z)), c);
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
