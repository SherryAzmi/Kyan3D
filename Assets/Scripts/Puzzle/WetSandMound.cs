using UnityEngine;

/// <summary>
/// A sand mound that is too dry and packed to dig until the Memory River has flowed.
/// After DryBedPuzzle unlocks it, it behaves exactly like a normal SandMound (hold Power to dig).
/// </summary>
public class WetSandMound : SandMound
{
    public bool unlocked;

    public override string Prompt(PlayerInteractor p) =>
        Dug ? null : unlocked ? base.Prompt(p) : "The sand is dry and packed hard...";

    public override bool UsesPower(PlayerInteractor p) => unlocked && base.UsesPower(p);
}
