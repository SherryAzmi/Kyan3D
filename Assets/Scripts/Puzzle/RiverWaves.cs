using UnityEngine;

/// <summary>
/// Turns a flat water box into moving water: the mesh is replaced at runtime by a fine grid
/// (same local -0.5..0.5 footprint, so any scaling script, e.g. the Dry Bed river filling, still works)
/// whose top surface rolls with waves that flow down the river, plus a gentle colour shimmer.
/// Wave sizes are in metres and stay the same however the object is scaled.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class RiverWaves : MonoBehaviour
{
    [Tooltip("Height of the waves in metres.")]
    public float amplitude = 0.05f;
    [Tooltip("Distance between wave crests in metres.")]
    public float wavelength = 2.2f;
    [Tooltip("How fast the waves travel down the river (m/s). Negative = the other way.")]
    public float flowSpeed = 1.4f;
    [Tooltip("Small cross ripples so it doesn't look like a washboard.")]
    public float choppiness = 0.5f;
    [Tooltip("Grid density: vertices per metre.")]
    public float resolution = 2.5f;
    [Tooltip("Brightness pulse on the water colour (0 = off).")]
    public float shimmer = 0.08f;

    Mesh mesh;
    Vector3[] baseVerts, verts;
    int cols, rows;
    Renderer rend;
    MaterialPropertyBlock block;
    Color baseColor;
    float builtForZ;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend && rend.sharedMaterial && rend.sharedMaterial.HasProperty("_BaseColor"))
            baseColor = rend.sharedMaterial.GetColor("_BaseColor");
        Build();
    }

    void Build()
    {
        Vector3 s = transform.lossyScale;
        builtForZ = Mathf.Max(s.z, 0.001f);
        // Size the grid for the river at full length so a growing river keeps enough detail.
        float fullZ = Mathf.Max(builtForZ, 20f);
        cols = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(s.x) * resolution), 4, 40);
        rows = Mathf.Clamp(Mathf.CeilToInt(fullZ * resolution), 8, 200);

        baseVerts = new Vector3[(cols + 1) * (rows + 1)];
        var uv = new Vector2[baseVerts.Length];
        for (int r = 0; r <= rows; r++)
            for (int c = 0; c <= cols; c++)
            {
                int i = r * (cols + 1) + c;
                baseVerts[i] = new Vector3(c / (float)cols - 0.5f, 0.5f, r / (float)rows - 0.5f);
                uv[i] = new Vector2(c / (float)cols, r / (float)rows);
            }
        var tris = new int[cols * rows * 6];
        int t = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int i = r * (cols + 1) + c;
                tris[t++] = i; tris[t++] = i + cols + 1; tris[t++] = i + 1;
                tris[t++] = i + 1; tris[t++] = i + cols + 1; tris[t++] = i + cols + 2;
            }

        verts = (Vector3[])baseVerts.Clone();
        mesh = new Mesh { name = "River Waves" };
        mesh.MarkDynamic();
        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().mesh = mesh;
    }

    void Update()
    {
        if (mesh == null || (rend && !rend.enabled)) return;
        Vector3 s = transform.lossyScale;
        float sx = Mathf.Abs(s.x), sy = Mathf.Max(Mathf.Abs(s.y), 0.0001f), sz = Mathf.Abs(s.z);
        float k = 2f * Mathf.PI / Mathf.Max(0.1f, wavelength);
        float time = Time.time;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 b = baseVerts[i];
            float x = b.x * sx, z = b.z * sz;                  // metres along the strip
            float h = Mathf.Sin(k * (z - flowSpeed * time))
                    + 0.45f * Mathf.Sin(k * 1.9f * (z - flowSpeed * 1.3f * time) + 1.3f)
                    + choppiness * 0.35f * Mathf.Sin(k * 2.7f * x + time * 2.3f + z * 0.7f);
            // calmer at the banks
            float edge = Mathf.Clamp01((0.5f - Mathf.Abs(b.x)) * 4f);
            // lifted so even the deepest trough stays above the river bed / ground
            verts[i].y = b.y + (amplitude * 1.8f + h * amplitude * edge) / sy;
        }
        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Read the live colour every frame: other scripts (e.g. the Dry Bed brightening) may change it.
        var mat = rend ? rend.sharedMaterial : null;
        if (mat && mat.HasProperty("_BaseColor")) baseColor = mat.GetColor("_BaseColor");
        if (rend && shimmer > 0f && baseColor.a > 0f)
        {
            block ??= new MaterialPropertyBlock();
            float p = 1f + shimmer * Mathf.Sin(time * 1.7f);
            var c = new Color(baseColor.r * p, baseColor.g * p, baseColor.b * p, baseColor.a);
            rend.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            rend.SetPropertyBlock(block);
        }
    }

    void OnDestroy()
    {
        if (mesh) Destroy(mesh);
    }
}
