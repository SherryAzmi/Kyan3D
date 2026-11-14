using UnityEngine;

/// <summary>
/// Present ability: hold Power to dig. The mound shakes and sinks; when it's gone the
/// path stone underneath lights up and BridgePuzzle is told.
/// </summary>
public class SandMound : Interactable
{
    public float digTime = 3f;
    [Tooltip("Path stone revealed underneath (lit up when dug).")]
    public Renderer revealedStone;
    public Material revealedMaterial;

    public bool Dug { get; private set; }
    float progress;
    Vector3 basePos, baseScale;
    float lastDigTime = -1f;

    void Start()
    {
        basePos = transform.position;
        baseScale = transform.localScale;
    }

    public override string Prompt(PlayerInteractor p) => Dug || p.Carried ? null : "Hold {P} to dig";
    public override bool UsesPower(PlayerInteractor p) => !Dug;
    public override float Progress => progress;

    public override void Power(PlayerInteractor p, float dt)
    {
        if (Dug) return;
        progress = Mathf.Clamp01(progress + dt / digTime);
        lastDigTime = Time.time;
        var a = GameAudio.I;
        if (a) GameAudio.Hold("dig" + GetInstanceID(), a.dig, GameAudio.SideOf(transform.position));
        if (progress >= 1f) Finish();
    }

    void Update()
    {
        if (Dug) return;
        bool digging = Time.time - lastDigTime < 0.1f;
        Vector3 shake = digging ? Random.insideUnitSphere * 0.04f : Vector3.zero;
        shake.y = 0f;
        transform.position = basePos + Vector3.down * (baseScale.y * 0.5f * progress) + shake;
        transform.localScale = Vector3.Scale(baseScale, new Vector3(1f - progress * 0.25f, 1f - progress * 0.7f, 1f - progress * 0.25f));
    }

    void Finish()
    {
        Dug = true;
        if (GameAudio.I) GameAudio.Play(GameAudio.I.digDone, transform.position);
        gameObject.SetActive(false); // also removes it from Interactable.All
        if (revealedStone && revealedMaterial) revealedStone.sharedMaterial = revealedMaterial;
        if (BridgePuzzle.Instance) BridgePuzzle.Instance.OnMoundDug(this);
        if (MirrorPuzzle.Instance) MirrorPuzzle.Instance.OnMoundDug(this);
    }
}
