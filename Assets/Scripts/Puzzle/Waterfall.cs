using UnityEngine;

/// <summary>
/// A waterfall: a curved sheet of falling water (Kyan/Water material with streaks) that pours from this
/// object's position downwards and slightly forward (+Z), with spray and mist particles at the bottom
/// and a roaring water sound when a player is near.
/// If 'feed' is set, the falls only run while that renderer is visible (e.g. a Dry Bed river that has
/// not filled yet stays dry).
/// </summary>
public class Waterfall : MonoBehaviour
{
    public float width = 3f;
    public float height = 3f;
    [Tooltip("How far the water arcs forward before it falls (metres).")]
    public float outward = 0.6f;
    public Material material;
    [Tooltip("Optional: the falls run only while this renderer is visible.")]
    public Renderer feed;
    public ParticleSystem spray;
    public ParticleSystem mist;
    public float hearDistance = 16f;
    [Range(0f, 1f)] public float volume = 0.6f;

    MeshRenderer sheet;
    Transform[] listeners;
    bool running = true;

    void Awake()
    {
        var go = new GameObject("Falling Water");
        go.transform.SetParent(transform, false);
        var mf = go.AddComponent<MeshFilter>();
        sheet = go.AddComponent<MeshRenderer>();
        sheet.sharedMaterial = material;
        sheet.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mf.mesh = BuildSheet();
    }

    void Start()
    {
        var ccs = FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
        listeners = new Transform[ccs.Length];
        for (int i = 0; i < ccs.Length; i++) listeners[i] = ccs[i].transform;
        SetRunning(!feed || feed.enabled, true);
    }

    Mesh BuildSheet()
    {
        int cols = 12, rows = Mathf.Clamp(Mathf.CeilToInt(height * 6f), 12, 80);
        var v = new Vector3[(cols + 1) * (rows + 1)];
        var uv = new Vector2[v.Length];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;                          // 0 = lip, 1 = bottom
            float spread = 1f + 0.25f * t;                      // the sheet widens as it falls
            float z = outward * Mathf.Sqrt(t) + 0.15f * t;      // arcs out over the edge
            for (int c = 0; c <= cols; c++)
            {
                float u = c / (float)cols;
                float wobble = 0.05f * Mathf.Sin(u * 9f + t * 5f);
                v[r * (cols + 1) + c] = new Vector3((u - 0.5f) * width * spread, -t * height, z + wobble);
                uv[r * (cols + 1) + c] = new Vector2(u, t * height);
            }
        }
        var tris = new int[cols * rows * 6];
        int k = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int i = r * (cols + 1) + c;
                tris[k++] = i; tris[k++] = i + 1; tris[k++] = i + cols + 1;
                tris[k++] = i + 1; tris[k++] = i + cols + 2; tris[k++] = i + cols + 1;
            }
        var m = new Mesh { name = "Waterfall Sheet" };
        m.vertices = v;
        m.uv = uv;
        m.triangles = tris;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    void SetRunning(bool on, bool force = false)
    {
        if (on == running && !force) return;
        running = on;
        if (sheet) sheet.enabled = on;
        foreach (var ps in new[] { spray, mist })
        {
            if (!ps) continue;
            var em = ps.emission;
            em.enabled = on;
            if (on && !ps.isPlaying) ps.Play();
        }
    }

    void Update()
    {
        SetRunning(!feed || (feed.enabled && feed.gameObject.activeInHierarchy));
        if (!running || listeners == null) return;

        var a = GameAudio.I;
        if (!a) return;
        float nearest = float.MaxValue;
        foreach (var l in listeners)
            if (l) nearest = Mathf.Min(nearest, Vector3.Distance(l.position, transform.position));
        if (nearest < hearDistance)
            GameAudio.Hold("falls" + GetInstanceID(), a.waterFlow, GameAudio.SideOf(transform.position), 0.7f,
                           volume * (1f - nearest / hearDistance));
    }
}
