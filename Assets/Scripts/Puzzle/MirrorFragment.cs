using UnityEngine;

/// <summary>The Mirror Fragment on the altar. Floats and spins; Interact collects it.</summary>
public class MirrorFragment : Interactable
{
    bool taken;
    Vector3 basePos;

    void Start() => basePos = transform.position;

    public override string Prompt(PlayerInteractor p) => taken ? null : "{I} Take the Mirror Fragment";
    public override bool HandlesInteract(PlayerInteractor p) => !taken;

    public override void Interact(PlayerInteractor p)
    {
        taken = true;
        if (BridgePuzzle.Instance) BridgePuzzle.Instance.OnFragmentCollected(this);
        gameObject.SetActive(false);
    }

    void Update()
    {
        transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 1.5f) * 0.12f;
        transform.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World);
    }
}
