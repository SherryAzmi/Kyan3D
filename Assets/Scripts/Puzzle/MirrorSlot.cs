/// <summary>
/// The broken mirror in one world. Takes that world's half of the last shard, then — once both
/// halves are in — both players hold Power here at the same time to join the mirror.
/// </summary>
public class MirrorSlot : Interactable
{
    [UnityEngine.Tooltip("0 = desert (Present), 1 = forest (Future).")]
    public int world;

    MirrorPuzzle P => MirrorPuzzle.Instance;

    public override string Prompt(PlayerInteractor p)
    {
        if (!P || P.Joined) return null;
        if (!P.HalfPlaced(world))
            return p.Carried ? "{I} Place the mirror shard" : "The mirror is missing a piece";
        if (!P.HalfPlaced(1 - world)) return "Waiting for your other self's half...";
        return P.OtherHolding(world) ? "Hold {P} now! Your other self is holding too" : "Hold {P} together to join the mirror";
    }

    public override bool HandlesInteract(PlayerInteractor p) => P && !P.HalfPlaced(world) && p.Carried;

    public override void Interact(PlayerInteractor p)
    {
        var c = p.TakeCarried();
        P.OnHalfPlaced(world, c);
    }

    public override bool UsesPower(PlayerInteractor p) => P && P.BothPlaced && !P.Joined;
    public override void Power(PlayerInteractor p, float dt) => P.ReportHold(world);
    public override float Progress => P && P.BothPlaced ? P.JoinProgress : -1f;
}
