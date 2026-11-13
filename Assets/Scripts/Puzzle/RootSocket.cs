using UnityEngine;

/// <summary>
/// Future ability: place the Memory Seed here, then hold Power to grow the Bridge Roots.
/// Growth is live: BridgePuzzle extends the roots (forest) and raises the path (desert) as you hold.
/// </summary>
public class RootSocket : Interactable
{
    public float growTime = 5f;
    public Transform seedSlot;
    public Renderer glow;
    public Material dormantMaterial;
    public Material awakeMaterial;

    Carryable seed;
    float progress;
    bool done;

    public bool IsAwake { get; private set; }

    public void Wake()
    {
        IsAwake = true;
        if (glow && awakeMaterial) glow.sharedMaterial = awakeMaterial;
    }

    public override string Prompt(PlayerInteractor p)
    {
        if (done) return null;
        if (seed) return "Hold {P} to grow the roots";
        if (p.Carried) return "{I} Place the " + p.Carried.displayName;
        return IsAwake ?"The socket hums... it is waiting for something" : "An empty stone socket";
    }

    public override bool HandlesInteract(PlayerInteractor p) => !seed && p.Carried;

    public override void Interact(PlayerInteractor p)
    {
        seed = p.TakeCarried();
        seed.enabled = false; // no longer pick-up-able
        seed.transform.SetParent(seedSlot, false);
        seed.transform.localPosition = Vector3.zero;
        Wake();
        if (BridgePuzzle.Instance) BridgePuzzle.Instance.OnSeedPlaced();
    }

    public override bool UsesPower(PlayerInteractor p) => seed && !done;
    public override float Progress => seed ? progress : -1f;

    public override void Power(PlayerInteractor p, float dt)
    {
        if (!seed || done) return;
        progress = Mathf.Clamp01(progress + dt / growTime);
        if (BridgePuzzle.Instance) BridgePuzzle.Instance.SetBridgeGrowth(progress);
        if (progress >= 1f)
        {
            done = true;
            if (BridgePuzzle.Instance) BridgePuzzle.Instance.OnBridgeComplete();
        }
    }
}
