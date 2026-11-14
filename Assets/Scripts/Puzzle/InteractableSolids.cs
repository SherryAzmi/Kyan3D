using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Self-installing: when a scene loads, every Interactable whose IsSolid is true (sand mounds, root
/// socket, root wheel, frame pedestal, bloom root) gets an invisible upright capsule blocker so players can't walk
/// into it. It covers the object's own meshes, from the ground up, and is kept small enough that
/// a player standing against it is still in range to use it. It switches off when IsSolid turns false
/// (a dug mound). It sits on the Ignore Raycast layer so cameras, drops and other raycasts ignore it.
/// </summary>
public class InteractableSolids : MonoBehaviour
{
    const float PlayerRadius = 0.4f, ReachMargin = 0.25f, MinHeight = 1.6f;

    Interactable target;
    CapsuleCollider box;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AddAll(); // the first scene has already loaded by now
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => AddAll();

    static void AddAll()
    {
        foreach (var it in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            if (it.IsSolid && !HasSolid(it)) Add(it);
    }

    static bool HasSolid(Interactable it)
    {
        foreach (var c in it.GetComponentsInChildren<Collider>(true))
            if (!c.isTrigger) return true;
        return it.transform.parent && it.transform.parent.Find(it.name + " (solid)");
    }

    static void Add(Interactable it)
    {
        // Footprint: the object's own meshes, not child interactables (a buried seed) or effects.
        bool any = false;
        Bounds b = default;
        foreach (var r in it.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            var owner = r.GetComponentInParent<Interactable>();
            if (owner != it) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) b = new Bounds(it.transform.position, Vector3.one * 0.8f);

        // From the ground under the object to its top (at least MinHeight, so it can't be stepped onto).
        float ground = b.min.y, found = float.NegativeInfinity;
        var from = new Vector3(b.center.x, b.max.y + 0.5f, b.center.z);
        foreach (var hit in Physics.RaycastAll(from, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.collider.GetComponent<CharacterController>() && hit.point.y <= b.center.y + 0.05f)
                found = Mathf.Max(found, hit.point.y);
        if (!float.IsNegativeInfinity(found)) ground = found;
        float top = Mathf.Max(b.max.y, ground + MinHeight);

        // Round footprint, kept inside the use range: a player pressed against it can still reach it.
        float maxRadius = Mathf.Max(0.3f, it.range - PlayerRadius - ReachMargin);
        float radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z), 0.3f, maxRadius);

        var go = new GameObject(it.name + " (solid)");
        go.layer = 2; // Ignore Raycast
        go.transform.SetParent(it.transform.parent, false);
        go.transform.SetPositionAndRotation(new Vector3(b.center.x, (ground + top) * 0.5f, b.center.z), Quaternion.identity);
        var parentScale = it.transform.parent ? it.transform.parent.lossyScale : Vector3.one;
        go.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);

        var solid = go.AddComponent<InteractableSolids>();
        solid.target = it;
        // Upright capsule: the straight part spans ground..top; the rounded ends poke into the ground
        // and above the object, so the full radius blocks right down at foot level.
        solid.box = go.AddComponent<CapsuleCollider>();
        solid.box.direction = 1;
        solid.box.radius = radius;
        solid.box.height = top - ground + radius * 2f;
    }

    void Update()
    {
        if (!target) { box.enabled = false; return; }
        box.enabled = target.isActiveAndEnabled && target.IsSolid;
    }
}
