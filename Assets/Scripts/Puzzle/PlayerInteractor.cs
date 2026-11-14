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

    /// <summary>Speeds up every hold-Power action at once (dig, grow, turn the wheel, bloom, join the mirror).
    /// 1 = the times set on each object (dig 3 s, grow 4-5 s...), 2 = twice as fast.</summary>
    public static float HoldSpeed = 2f;

    [Header("Focus while interacting")]
    [Tooltip("After a tap (place, touch the river...), the player stays still facing the object this long (seconds). 0 = off.")]
    public float tapFocusTime = 0.6f;

    public Carryable Carried { get; private set; }
    public Interactable Current { get; private set; }

    /// <summary>True while the player is using something: holding Power on it, or just after a tap.
    /// SoulMovement then holds the player still and OrbitCameraInput turns the camera onto FocusPoint.</summary>
    public bool HasFocus { get; private set; }
    public Vector3 FocusPoint { get; private set; }

    float tapFocusUntil = -1f;
    Vector3 tapFocusPoint;

    void OnDisable() => HasFocus = false;

    void Update()
    {
        if (!input) return;

        Current = FindBest();
        string prompt = Current ? Current.Prompt(this) : null;
        if (prompt == null && Carried) prompt = "{I} Put down";

        if (input.InteractPressed)
        {
            if (Current && Current.HandlesInteract(this))
            {
                if (Current.FocusOnTap && tapFocusTime > 0f)
                {
                    tapFocusPoint = Current.FocusPoint(transform.position);
                    tapFocusUntil = Time.time + tapFocusTime;
                }
                Current.Interact(this);
            }
            else if (Carried) Drop();
        }

        float progress = -1f;
        bool holding = false;
        if (Current && Current.UsesPower(this))
        {
            if (input.PowerHeld)
            {
                holding = true;
                FocusPoint = Current.FocusPoint(transform.position);
                Current.Power(this, Time.deltaTime * HoldSpeed);
            }
            progress = Current.Progress;
        }
        if (!holding && Time.time < tapFocusUntil) FocusPoint = tapFocusPoint;
        HasFocus = holding || Time.time < tapFocusUntil;

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
