using UnityEngine;

/// <summary>
/// Turns a flat water box into a natural river: the mesh is replaced at runtime by a fine grid
/// (same local -0.5..0.5 footprint, so any scaling script, e.g. the Dry Bed river filling, still works)
/// that meanders gently, varies in width, and rolls with waves flowing down the river.
/// uv.x runs across the river (0..1), uv.y along it in metres (the Kyan/Water shader uses both).
/// The curve depends only on world Z, so the desert and forest rivers stay mirror images.
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
    [Tooltip("Grid density: vertices per metre along the river.")]
    public float resolution = 3f;
    [Tooltip("Sideways bends (metres). 0 = straight.")]
    public float meander = 1f;
    [Tooltip("Brightness pulse on the water colour (0 = off).")]
    public float shimmer = 0.05f;

    /// <summary>Sideways offset of the river centre at a world Z (metres). Shared by bed, banks and falls.</summary>
    public static float Meander(float worldZ) =>
        0.85f * Mathf.Sin(worldZ * 0.21f + 0.6f) + 0.3f * Mathf.Sin(worldZ * 0.53f + 2.1f);

    /// <summary>Width multiplier at a world Z, so the river widens and narrows naturally.</summary>
    public static float WidthScale(float worldZ) =>
        1f + 0.13f * Mathf.Sin(worldZ * 0.37f + 1.1f) + 0.05f * Mathf.Sin(worldZ * 1.13f);

    Mesh mesh;
    Vector3[] baseVerts, verts;
    Vector2[] uvs;
    int cols, rows;
    Renderer rend;
    MaterialPropertyBlock block;

    void Start()
    {
        rend = GetComponent<Renderer>();
        Build();
    }

    void Build()
    {
        Vector3 s = transform.lossyScale;
        float fullZ = Mathf.Max(Mathf.Abs(s.z), 20f); // detail for the full length, even while a river is still filling
        cols = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(s.x) * 3f), 8, 40);
        rows = Mathf.Clamp(Mathf.CeilToInt(fullZ * resolution), 16, 220);

        baseVerts = new Vector3[(cols + 1) * (rows + 1)];
        uvs = new Vector2[baseVerts.Length];
        for (int r = 0; r <= rows; r++)
            for (int c = 0; c <= cols; c++)
                baseVerts[r * (cols + 1) + c] = new Vector3(c / (float)cols - 0.5f, 0.5f, r / (float)rows - 0.5f);

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
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().mesh = mesh;
    }

    void Update()
    {
        if (mesh == null || (rend && !rend.enabled)) return;
        Vector3 s = transform.lossyScale;
        float sx = Mathf.Max(Mathf.Abs(s.x), 0.0001f), sy = Mathf.Max(Mathf.Abs(s.y), 0.0001f), sz = Mathf.Abs(s.z);
        float k = 2f * Mathf.PI / Mathf.Max(0.1f, wavelength);
        float time = Time.time;
        float originZ = transform.position.z;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 b = baseVerts[i];
            float z = b.z * sz;                         // metres along the strip
            float worldZ = originZ + z;
            float width = WidthScale(worldZ);
            float x = b.x * sx * width;                 // metres across
            float h = Mathf.Sin(k * (z - flowSpeed * time))
                    + 0.45f * Mathf.Sin(k * 1.9f * (z - flowSpeed * 1.3f * time) + 1.3f)
                    + choppiness * 0.35f * Mathf.Sin(k * 2.7f * x + time * 2.3f + z * 0.7f);
            float edge = Mathf.Clamp01((0.5f - Mathf.Abs(b.x)) * 4f); // calmer at the banks

            verts[i].x = (x + meander * Meander(worldZ)) / sx;
            // lifted so even the deepest trough stays above the river bed / ground
            verts[i].y = b.y + (amplitude * 1.8f + h * amplitude * edge) / sy;
            verts[i].z = b.z;
            uvs[i] = new Vector2(b.x + 0.5f, worldZ);
        }
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Read the live colour every frame: other scripts (e.g. the Dry Bed brightening) may change it.
        var mat = rend ? rend.sharedMaterial : null;
        if (rend && mat && shimmer > 0f && mat.HasProperty("_BaseColor"))
        {
            Color baseColor = mat.GetColor("_BaseColor");
            block ??= new MaterialPropertyBlock();
            float p = 1f + shimmer * Mathf.Sin(time * 1.7f);
            rend.GetPropertyBlock(block);
            block.SetColor("_BaseColor", new Color(baseColor.r * p, baseColor.g * p, baseColor.b * p, baseColor.a));
            rend.SetPropertyBlock(block);
        }
    }

    void OnDestroy()
    {
        if (mesh) Destroy(mesh);
    }
}
