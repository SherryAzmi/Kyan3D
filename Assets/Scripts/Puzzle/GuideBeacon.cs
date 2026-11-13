using UnityEngine;

/// <summary>
/// A soft, pulsing beam of light that shows a player where to go next.
/// BridgePuzzle calls SetTarget() every frame; null fades the beam out.
/// </summary>
public class GuideBeacon : MonoBehaviour
{
    public Renderer beam;
    public Light glow;
    public float followSpeed = 5f;
    public float glowIntensity = 2.5f;

    Material mat;
    Color baseColor;
    Vector3 beamScale;
    float visible;
    Vector3? target;

    void Awake()
    {
        mat = beam.material; // own instance so the alpha can pulse
        baseColor = mat.GetColor("_BaseColor");
        beamScale = beam.transform.localScale;
        SetVisuals(0f);
    }

    public void SetTarget(Vector3? worldPos) => target = worldPos;

    void Update()
    {
        float want = target.HasValue ? 1f : 0f;
        visible = Mathf.MoveTowards(visible, want, Time.deltaTime * 1.5f);

        if (target.HasValue)
        {
            Vector3 p = target.Value;
            p.y = transform.position.y;
            // Jump straight there when hidden, glide when already showing.
            transform.position = visible < 0.05f ? p : Vector3.Lerp(transform.position, p, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        }
        SetVisuals(visible);
    }

    void SetVisuals(float v)
    {
        float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 3f);
        var c = baseColor;
        c.a = baseColor.a * v * pulse;
        mat.SetColor("_BaseColor", c);
        beam.enabled = v > 0.001f;
        float w = 0.85f + 0.15f * pulse;
        beam.transform.localScale = new Vector3(beamScale.x * w, beamScale.y, beamScale.z * w);
        if (glow) glow.intensity = glowIntensity * v * pulse;
    }

    void OnDestroy()
    {
        if (mat) Destroy(mat);
    }
}
