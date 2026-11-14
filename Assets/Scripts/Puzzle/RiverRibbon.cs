using UnityEngine;

/// <summary>
/// The river bed: replaces a flat box with a ribbon that follows the same bends and width changes as
/// the water (RiverWaves.Meander / WidthScale), slightly wider, with a ragged natural edge.
/// Built once at start (the bed doesn't move).
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class RiverRibbon : MonoBehaviour
{
    [Tooltip("Width compared with the box it replaces.")]
    public float widthMultiplier = 1.15f;
    [Tooltip("How ragged the edge is (metres).")]
    public float raggedness = 0.25f;
    public float resolution = 2f;

    void Start()
    {
        Vector3 s = transform.lossyScale;
        float sx = Mathf.Max(Mathf.Abs(s.x), 0.0001f), sz = Mathf.Abs(s.z);
        int cols = 8, rows = Mathf.Clamp(Mathf.CeilToInt(sz * resolution), 8, 200);
        var v = new Vector3[(cols + 1) * (rows + 1)];
        var uv = new Vector2[v.Length];
        float originZ = transform.position.z;
        for (int r = 0; r <= rows; r++)
        {
            float lz = r / (float)rows - 0.5f;
            float worldZ = originZ + lz * sz;
            float w = RiverWaves.WidthScale(worldZ) * widthMultiplier;
            float ragL = raggedness * (Mathf.PerlinNoise(worldZ * 0.8f, 3.1f) - 0.5f) * 2f;
            float ragR = raggedness * (Mathf.PerlinNoise(worldZ * 0.8f, 7.7f) - 0.5f) * 2f;
            for (int c = 0; c <= cols; c++)
            {
                float u = c / (float)cols;
                float x = (u - 0.5f) * sx * w + (u < 0.5f ? ragL : ragR) * Mathf.Abs(u - 0.5f) * 2f;
                v[r * (cols + 1) + c] = new Vector3((x + RiverWaves.Meander(worldZ)) / sx, 0.5f, lz);
                uv[r * (cols + 1) + c] = new Vector2(u, worldZ);
            }
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
        var mesh = new Mesh { name = "River Bed" };
        mesh.vertices = v;
        mesh.uv = uv;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().mesh = mesh;
    }
}
