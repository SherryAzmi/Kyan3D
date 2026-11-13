using UnityEngine;

/// <summary>A flat glowing ring that grows and fades, then destroys itself.</summary>
public class RippleFX : MonoBehaviour
{
    public float duration = 1.6f;
    public float maxSize = 4f;

    float t;
    float delay;
    Material mat;
    Color baseColor;

    public static void Spawn(Vector3 pos, Material material, float delay)
    {
        if (!material) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(go.GetComponent<Collider>());
        go.name = "Ripple";
        go.transform.position = pos;
        go.transform.localScale = Vector3.zero;
        var r = go.GetComponent<Renderer>();
        r.material = material; // instance, so each ripple fades on its own
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var fx = go.AddComponent<RippleFX>();
        fx.mat = r.material;
        fx.baseColor = fx.mat.GetColor("_BaseColor");
        fx.delay = delay;
    }

    void Update()
    {
        if (delay > 0f) { delay -= Time.deltaTime; return; }
        t += Time.deltaTime;
        float k = t / duration;
        float s = Mathf.Lerp(0.3f, maxSize, 1f - (1f - k) * (1f - k));
        transform.localScale = new Vector3(s, 0.005f, s);
        var c = baseColor;
        c.a = baseColor.a * (1f - k);
        mat.SetColor("_BaseColor", c);
        if (k >= 1f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (mat) Destroy(mat);
    }
}
