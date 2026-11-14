using UnityEngine;

/// <summary>
/// Spawns butterflies at runtime that wander around this object (the forest), fluttering between
/// flower height and head height. Each butterfly is a thin body with two wings that flap.
/// </summary>
public class Butterflies : MonoBehaviour
{
    public int count = 18;
    [Tooltip("Half-size of the area they wander in (metres, around this object).")]
    public float range = 18f;
    public Vector2 height = new Vector2(0.5f, 2.6f);
    public Material[] wingMaterials;
    public Material bodyMaterial;

    void Start()
    {
        if (wingMaterials == null || wingMaterials.Length == 0) return;
        var rnd = new System.Random(GetInstanceID());
        for (int i = 0; i < count; i++)
        {
            var b = new GameObject("Butterfly").AddComponent<Butterfly>();
            b.transform.SetParent(transform, false);
            b.home = transform.position;
            b.range = range;
            b.height = height;
            b.transform.position = b.RandomPoint();
            b.Build(wingMaterials[i % wingMaterials.Length], bodyMaterial, 0.8f + 0.5f * (float)rnd.NextDouble());
        }
    }
}

public class Butterfly : MonoBehaviour
{
    public Vector3 home;
    public float range = 18f;
    public Vector2 height = new Vector2(0.5f, 2.6f);

    Transform left, right;
    Vector3 target, velocity;
    float speed, flapRate, phase;

    public Vector3 RandomPoint() =>
        home + new Vector3(Random.Range(-range, range), Random.Range(height.x, height.y), Random.Range(-range, range));

    public void Build(Material wing, Material body, float size)
    {
        transform.localScale = Vector3.one * 0.22f * size;
        var b = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(b.GetComponent<Collider>());
        b.transform.SetParent(transform, false);
        b.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        b.transform.localScale = new Vector3(0.12f, 0.45f, 0.12f);
        var br = b.GetComponent<Renderer>();
        if (body) br.sharedMaterial = body;
        br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        left = Wing(wing, -1f);
        right = Wing(wing, 1f);
        speed = Random.Range(1.2f, 2.2f);
        flapRate = Random.Range(9f, 13f);
        phase = Random.value * 10f;
        target = RandomPoint();
    }

    Transform Wing(Material mat, float side)
    {
        var hinge = new GameObject(side < 0 ? "Wing L" : "Wing R").transform;
        hinge.SetParent(transform, false);
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(hinge, false);
        // lying flat, reaching out sideways from the body
        q.transform.localPosition = new Vector3(side * 0.5f, 0f, 0f);
        q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        q.transform.localScale = new Vector3(side, 1f, 1f); // mirror the left wing
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return hinge;
    }

    void Update()
    {
        float t = Time.time + phase;
        // wander: steer smoothly towards a target, pick a new one when close
        Vector3 to = target - transform.position;
        if (to.magnitude < 0.6f) target = RandomPoint();
        Vector3 desired = to.normalized * speed;
        desired.y += Mathf.Sin(t * 3.1f) * 0.6f; // fluttery up-and-down
        velocity = Vector3.Lerp(velocity, desired, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
        transform.position += velocity * Time.deltaTime;
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        if (flat.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 1f - Mathf.Exp(-5f * Time.deltaTime));

        float flap = Mathf.Sin(t * flapRate) * 65f;
        if (left) left.localRotation = Quaternion.Euler(0f, 0f, -flap);
        if (right) right.localRotation = Quaternion.Euler(0f, 0f, flap);
    }
}
