using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for anything a player can use. PlayerInteractor picks the nearest one in range
/// that returns a prompt, shows the prompt, and forwards Interact (tap) and Power (hold).
/// In prompts, {I} becomes the player's interact key and {P} the power key.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    public static readonly List<Interactable> All = new List<Interactable>();

    [Tooltip("How close (metres, flat distance) a player must be.")]
    public float range = 2.5f;

    protected virtual void OnEnable() => All.Add(this);
    protected virtual void OnDisable() => All.Remove(this);

    /// <summary>Flat (XZ) distance from a player position; override for long objects like the river.</summary>
    public virtual float DistanceTo(Vector3 p)
    {
        Vector3 d = transform.position - p;
        if (Mathf.Abs(d.y) > 3f) return float.MaxValue;
        d.y = 0f;
        return d.magnitude;
    }

    /// <summary>Text to show this player, or null if this object has nothing to offer them right now.</summary>
    public abstract string Prompt(PlayerInteractor p);

    public virtual bool HandlesInteract(PlayerInteractor p) => false;
    public virtual void Interact(PlayerInteractor p) { }

    public virtual bool UsesPower(PlayerInteractor p) => false;
    public virtual void Power(PlayerInteractor p, float dt) { }

    /// <summary>Where the player looks and the camera aims while using this (seen from a player at <paramref name="from"/>).</summary>
    public virtual Vector3 FocusPoint(Vector3 from) => transform.position;

    /// <summary>Whether a tap (Interact) briefly holds the player still and turns the camera onto this.</summary>
    public virtual bool FocusOnTap => true;

    /// <summary>Players can't walk into this while true (InteractableSolids gives it a blocker at scene load).</summary>
    public virtual bool IsSolid => false;

    /// <summary>0..1 for the hold bar, or negative to hide it.</summary>
    public virtual float Progress => -1f;
}
