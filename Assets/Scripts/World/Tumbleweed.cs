using UnityEngine;

/// <summary>
/// A tumbleweed blown across the desert: rolls along the wind (spinning by the distance it covers),
/// bounces, speeds up and slows down with gusts. If the ground disappears (the chasm) it drops in.
/// When it leaves the area or falls, it reappears on the upwind side.
/// </summary>
public class Tumbleweed : MonoBehaviour
{
    public Vector3 areaCentre;
    public float areaHalf = 24f;
    public Vector3 wind = new Vector3(1f, 0f, 0.35f);
    public float radius = 0.45f;
    public float speed = 2.2f;

    float phase, vy, airborne;
    bool falling;

    void Start()
    {
        phase = Random.value * 50f;
        wind.y = 0f;
        wind.Normalize();
    }

    void Update()
    {
        float t = Time.time + phase;
        float gust = 0.55f + 0.45f * Mathf.PerlinNoise(t * 0.35f, phase);
        Vector3 step = (wind + new Vector3(0f, 0f, Mathf.Sin(t * 0.5f) * 0.25f)).normalized * speed * gust * Time.deltaTime;
        Vector3 p = transform.position + step;

        bool ground = Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)
                      && hit.collider.name.StartsWith("Ground");
        if (!ground || falling)
        {
            falling = true;               // over the chasm: drop
            vy -= 15f * Time.deltaTime;
            p.y += vy * Time.deltaTime;
        }
        else
        {
            // little bounces as it rolls
            airborne += Time.deltaTime * speed * gust * 1.6f;
            float bounce = Mathf.Abs(Mathf.Sin(airborne)) * 0.35f * gust;
            p.y = hit.point.y + radius + bounce;
        }

        float dist = step.magnitude;
        Vector3 axis = Vector3.Cross(Vector3.up, step.normalized);
        if (axis.sqrMagnitude > 0.0001f) transform.Rotate(axis, dist / radius * Mathf.Rad2Deg, Space.World);
        transform.position = p;

        Vector3 local = p - areaCentre;
        if (p.y < -12f || Mathf.Abs(local.x) > areaHalf || Mathf.Abs(local.z) > areaHalf) Respawn();
    }

    void Respawn()
    {
        falling = false;
        vy = 0f;
        // upwind edge, random spot along it
        Vector3 side = Vector3.Cross(Vector3.up, wind);
        Vector3 start = areaCentre - wind * (areaHalf - 1f) + side * Random.Range(-areaHalf * 0.8f, areaHalf * 0.8f);
        transform.position = new Vector3(start.x, radius, start.z);
    }
}
