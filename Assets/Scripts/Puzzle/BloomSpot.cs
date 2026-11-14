using UnityEngine;

/// <summary>
/// Future ability: hold Power to make a root grow out of the ground. When it's fully grown,
/// MirrorPuzzle is told (it reveals the forest half of the mirror shard on top).
/// </summary>
public class BloomSpot : Interactable
{
    public float growTime = 3f;
    [Tooltip("Grows on Y from 0 to its built height.")]
    public Transform stalk;
    [Tooltip("Opens (scales up) in the last part of the growth.")]
    public Transform bud;
    [Tooltip("Where the revealed object should appear.")]
    public Transform top;

    public bool Bloomed { get; private set; }
    float progress;
    Vector3 stalkScale, budScale;

    void Start()
    {
        if (stalk) { stalkScale = stalk.localScale; stalk.localScale = new Vector3(stalkScale.x, 0.001f, stalkScale.z); }
        if (bud) { budScale = bud.localScale; bud.localScale = Vector3.zero; }
    }

    public override string Prompt(PlayerInteractor p) => Bloomed || p.Carried ? null : "Hold {P} to bloom";
    public override bool UsesPower(PlayerInteractor p) => !Bloomed;
    public override float Progress => progress;

    public override void Power(PlayerInteractor p, float dt)
    {
        if (Bloomed) return;
        progress = Mathf.Clamp01(progress + dt / growTime);
        var a = GameAudio.I;
        if (a) GameAudio.Hold("bloom" + GetInstanceID(), a.grow, GameAudio.SideOf(transform.position), 0.9f + 0.4f * progress);
        if (stalk) stalk.localScale = new Vector3(stalkScale.x, Mathf.Max(0.001f, stalkScale.y * progress), stalkScale.z);
        if (bud) bud.localScale = budScale * Mathf.Clamp01((progress - 0.6f) / 0.4f);
        if (progress >= 1f)
        {
            Bloomed = true;
            if (a) GameAudio.Play(a.bloom, transform.position);
            if (MirrorPuzzle.Instance) MirrorPuzzle.Instance.OnBloomed(this);
        }
    }
}
