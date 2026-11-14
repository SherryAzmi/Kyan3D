using UnityEngine;

/// <summary>
/// A glowing neon beam of light that shows a player where to go next.
/// The puzzle calls SetTarget() every frame; null fades the beam out.
/// Layers: a bright core, the main beam, a wide soft halo, a glow ring on the ground and a light.
/// </summary>
public class GuideBeacon : MonoBehaviour
{
    public Renderer beam;
    [Tooltip("Extra layers (core, halo, ground ring) that fade and pulse with the beam.")]
    public Renderer[] extras;
    public Light glow;
    public float followSpeed = 5f;
    public float glowIntensity = 2.5f;

    Material[] mats;
    Color[] baseColors;
    Renderer[] all;
    Vector3 beamScale;
    float visible;
    Vector3? target;

    void Awake()
    {
        int n = 1 + (extras != null ? extras.Length : 0);
        all = new Renderer[n];
        all[0] = beam;
        for (int i = 1; i < n; i++) all[i] = extras[i - 1];
        mats = new Material[n];
        baseColors = new Color[n];
        for (int i = 0; i < n; i++)
        {
            if (!all[i]) continue;
            mats[i] = all[i].material; // own instance so the alpha can pulse
            baseColors[i] = mats[i].GetColor("_BaseColor");
        }
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
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
        for (int i = 0; i < all.Length; i++)
        {
            if (!all[i]) continue;
            var c = baseColors[i];
            c.a = baseColors[i].a * v * (i == 1 ? Mathf.Lerp(1f, pulse, 0.4f) : pulse); // the core flickers less
            mats[i].SetColor("_BaseColor", c);
            all[i].enabled = v > 0.001f;
        }
        float w = 0.85f + 0.15f * pulse;
        beam.transform.localScale = new Vector3(beamScale.x * w, beamScale.y, beamScale.z * w);
        if (glow) glow.intensity = glowIntensity * v * pulse;
    }

    void OnDestroy()
    {
        if (mats == null) return;
        foreach (var m in mats) if (m) Destroy(m);
    }
}
