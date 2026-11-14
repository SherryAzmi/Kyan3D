using UnityEngine;

/// <summary>
/// Forest pedestal (Puzzle 2): the Future places the Picture Frame here, which makes the
/// roots around the fragment altar loosen and sink.
/// </summary>
public class FramePedestal : Interactable
{
    public Transform slot;
    public Renderer glow;
    public Material awakeMaterial;

    Carryable item;

    public bool Filled => item;

    public override string Prompt(PlayerInteractor p)
    {
        if (item) return null;
        if (p.Carried) return "{I} Place the " + p.Carried.displayName;
        return "A stone pedestal. Something belongs here...";
    }

    public override bool HandlesInteract(PlayerInteractor p) => !item && p.Carried;

    public override void Interact(PlayerInteractor p)
    {
        item = p.TakeCarried();
        item.enabled = false; // no longer pick-up-able
        item.transform.SetParent(slot ? slot : transform, false);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        if (glow && awakeMaterial) glow.sharedMaterial = awakeMaterial;
        if (DryBedPuzzle.Instance) DryBedPuzzle.Instance.OnFramePlaced();
    }
}
