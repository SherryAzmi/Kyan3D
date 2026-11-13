using UnityEngine;

/// <summary>An object a player can pick up and carry (the Memory Seed). Bobs and spins while lying down.</summary>
public class Carryable : Interactable
{
    public string displayName = "Memory Seed";

    public PlayerInteractor Holder { get; private set; }
    Vector3 home;
    Vector3 restPos;

    void Start()
    {
        home = transform.position;
        restPos = transform.position;
    }

    public override string Prompt(PlayerInteractor p) =>
        Holder || p.Carried ? null : $"{{I}} Pick up the {displayName}";

    public override bool HandlesInteract(PlayerInteractor p) => !Holder && !p.Carried;
    public override void Interact(PlayerInteractor p) => p.PickUp(this);

    public void OnPickedUp(PlayerInteractor p)
    {
        Holder = p;
        transform.SetParent(p.holdPoint, false);
        transform.localPosition = Vector3.zero;
    }

    public void OnDropped(Vector3 worldPos)
    {
        Holder = null;
        transform.SetParent(null, true);
        PlaceAt(worldPos);
    }

    /// <summary>Put the object somewhere (also used by the river to send it to the other world).</summary>
    public void PlaceAt(Vector3 worldPos)
    {
        Holder = null;
        transform.SetParent(null, true);
        transform.position = worldPos;
        restPos = worldPos;
        home = worldPos;
    }

    public void ReturnHome() => PlaceAt(home);

    void Update()
    {
        if (Holder || transform.parent) return;
        transform.position = restPos + Vector3.up * (0.15f + Mathf.Sin(Time.time * 2f) * 0.08f);
        transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
    }
}
