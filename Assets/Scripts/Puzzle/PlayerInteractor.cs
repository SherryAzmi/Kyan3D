using UnityEngine;

/// <summary>
/// Per-player interaction: finds the nearest usable Interactable, shows its prompt on this
/// player's half of the HUD, handles tap (Interact) and hold (Power), and carries one object.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    public PlayerInputReader input;
    [Tooltip("Where a carried object sits.")]
    public Transform holdPoint;
    public PuzzleHUD hud;
    [Tooltip("0 = left half of the screen, 1 = right half.")]
    public int hudSide;
    public string interactKey = "□ / E";
    public string powerKey = "R2 / F";

    public Carryable Carried { get; private set; }
    public Interactable Current { get; private set; }

    void Update()
    {
        if (!input) return;

        Current = FindBest();
        string prompt = Current ? Current.Prompt(this) : null;
        if (prompt == null && Carried) prompt = "{I} Put down";

        if (input.InteractPressed)
        {
            if (Current && Current.HandlesInteract(this)) Current.Interact(this);
            else if (Carried) Drop();
        }

        float progress = -1f;
        if (Current && Current.UsesPower(this))
        {
            if (input.PowerHeld) Current.Power(this, Time.deltaTime);
            progress = Current.Progress;
        }

        if (hud) hud.SetPrompt(hudSide, Format(prompt), progress);
    }

    Interactable FindBest()
    {
        Interactable best = null;
        float bestD = float.MaxValue;
        Vector3 pos = transform.position;
        foreach (var it in Interactable.All)
        {
            if (it == Carried) continue;
            float d = it.DistanceTo(pos);
            if (d > it.range || d >= bestD) continue;
            if (it.Prompt(this) == null) continue;
            best = it;
            bestD = d;
        }
        return best;
    }

    string Format(string s) =>
        string.IsNullOrEmpty(s) ? null : s.Replace("{I}", $"[{interactKey}]").Replace("{P}", $"[{powerKey}]");

    public void PickUp(Carryable c)
    {
        if (Carried) return;
        Carried = c;
        c.OnPickedUp(this);
    }

    /// <summary>Hand the carried object to someone else (river, socket). Returns it.</summary>
    public Carryable TakeCarried()
    {
        var c = Carried;
        Carried = null;
        return c;
    }

    public void Drop()
    {
        if (!Carried) return;
        var c = TakeCarried();
        Vector3 spot = transform.position + transform.forward * 0.9f + Vector3.up * 2f;
        if (Physics.Raycast(spot, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            c.OnDropped(hit.point + Vector3.up * 0.25f);
        else
            c.ReturnHome(); // dropped over the chasm: it goes back where it came from
    }
}
