using UnityEngine;

/// <summary>
/// Future ability (Puzzle 2): the old root wheel that holds back the spring.
/// It stays stuck until the Present touches the dry river (DryBedPuzzle wakes it),
/// then hold Power to turn it; when full the spring gate opens and the river flows.
/// </summary>
public class RootWheel : Interactable
{
    public float turnTime = 5f;
    [Tooltip("The wheel mesh that spins while turning.")]
    public Transform wheel;
    public Renderer glow;
    public Material dormantMaterial;
    public Material awakeMaterial;

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
        return IsAwake ? "Hold {P} to turn the root wheel" : "An old root wheel. It won't budge...";
    }

    public override bool IsSolid => true;
    public override bool UsesPower(PlayerInteractor p) => IsAwake && !done;
    public override float Progress => IsAwake && !done ? progress : -1f;

    public override void Power(PlayerInteractor p, float dt)
    {
        if (!IsAwake || done) return;
        progress = Mathf.Clamp01(progress + dt / turnTime);
        if (wheel) wheel.Rotate(0f, 0f, -140f * dt, Space.Self);
        var a = GameAudio.I;
        if (a) GameAudio.Hold("wheel", a.grow, GameAudio.SideOf(transform.position), 0.7f + 0.4f * progress);
        if (progress >= 1f)
        {
            done = true;
            if (a) GameAudio.Play(a.digDone, transform.position);
            if (DryBedPuzzle.Instance) DryBedPuzzle.Instance.OnGateOpened();
        }
    }
}
