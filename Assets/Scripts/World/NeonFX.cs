using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Neon particle effects (one per gameplay scene, placed by the builders):
///  - the Memory Seed gem glows neon green, with a soft halo and sparkles that trail behind it,
///  - throwing something into the Memory River: a neon splash where it goes in,
///  - it comes out on the other world's bank a moment later: a neon burst + sound,
///  - a player hurt in the thorns: a burst where they got hurt and a rising ring of light where they respawn.
/// All particles are on the Neon layer, so the desert's grey mood never dulls them.
/// </summary>
public class NeonFX : MonoBehaviour
{
    public static NeonFX I { get; private set; }

    public Material particleMat;   // additive soft dot
    public Material gemMat;        // neon green core
    public Material gemHaloMat;    // additive neon halo
    public Material starMat;       // additive four-point sparkle star

    public static readonly Color Gold = new Color(1f, 0.72f, 0.18f);
    public static readonly Color Cyan = new Color(0.3f, 1f, 0.9f);
    public static readonly Color Gem = new Color(0.35f, 1f, 0.45f);

    public static Color SideColor(Vector3 p) => p.x > 150f ? Cyan : Gold;

    void Awake() => I = this;
    void OnDestroy() { if (I == this) I = null; }

    void Start()
    {
        foreach (var c in FindObjectsByType<Carryable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.displayName == "Memory Seed") MakeGemNeon(c);
    }

    // ------------------------------------------------------------------ the gem

    void MakeGemNeon(Carryable c)
    {
        foreach (var r in c.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (r.name == "Halo") r.sharedMaterial = gemHaloMat ? gemHaloMat : r.sharedMaterial;
            else if (gemMat) r.sharedMaterial = gemMat;
            r.gameObject.layer = NeonOverlay.NeonLayer;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        foreach (var l in c.GetComponentsInChildren<Light>(true))
        {
            l.color = Gem;
            l.intensity = 3f;
            l.range = 5f;
        }

        // soft glow ball around the gem
        var glow = Make("Neon Glow", c.transform.position, c.transform, false);
        var m = glow.main;
        m.loop = true; m.startLifetime = 0.6f; m.startSpeed = 0f; m.startSize = 1.1f;
        m.startColor = new Color(Gem.r, Gem.g, Gem.b, 0.55f); m.maxParticles = 4;
        var e = glow.emission; e.rateOverTime = 6f;
        var sh = glow.shape; sh.enabled = false;
        Fade(glow, true);
        glow.Play();

        // sparkles drifting off it (world space: a neon trail while it is carried)
        var sp = Make("Neon Sparkles", c.transform.position, c.transform, true);
        m = sp.main;
        m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
        m.startColor = new ParticleSystem.MinMaxGradient(Gem, Color.Lerp(Gem, Color.white, 0.6f));
        m.gravityModifier = -0.04f; m.maxParticles = 80;
        e = sp.emission; e.rateOverTime = 22f;
        sh = sp.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.22f;
        Fade(sp, false);
        sp.Play();
        // restart by themselves whenever the gem is hidden and shown again (picked up, carried, dug out)
        var gm = glow.main; gm.playOnAwake = true;
        var sm = sp.main; sm.playOnAwake = true;
    }

    // ------------------------------------------------------------------ river

    /// <summary>Something was thrown into the river at 'at' and comes out at 'item' on the other bank.</summary>
    public static void RiverThrow(Vector3 at, Carryable item, float delay = 0.7f)
    {
        var a = GameAudio.I;
        if (a) GameAudio.Play(a.throwItem, at);
        Burst(at + Vector3.up * 0.1f, SideColor(at), 50, 2.2f, 0.16f, 1f, true);
        if (I && item) I.StartCoroutine(I.Arrive(item, delay));
        else if (a && item) GameAudio.Play(a.arrive, item.transform.position);
    }

    IEnumerator Arrive(Carryable item, float delay)
    {
        // hidden while "travelling" through the river, then it pops out in a flash of light
        var hidden = new List<Renderer>();
        foreach (var r in item.GetComponentsInChildren<Renderer>())
            if (r.enabled) { r.enabled = false; hidden.Add(r); }
        yield return new WaitForSeconds(delay);
        foreach (var r in hidden) if (r) r.enabled = true;
        if (!item) yield break;
        foreach (var ps in item.GetComponentsInChildren<ParticleSystem>()) if (!ps.isPlaying) ps.Play();
        Vector3 p = item.transform.position;
        Burst(p, SideColor(p), 70, 2.6f, 0.18f, 1.2f, true);
        Flash(p, SideColor(p), 2.2f);
        if (GameAudio.I) GameAudio.Play(GameAudio.I.arrive, p);
    }

    // ------------------------------------------------------------------ respawn

    /// <summary>A player got hurt at 'from' and is put back at 'to'.</summary>
    public static void Respawn(Vector3 from, Vector3 to, int side)
    {
        Color c = SideColor(to);
        Burst(from + Vector3.up * 0.8f, new Color(1f, 0.3f, 0.3f), 40, 3f, 0.14f, 0.8f, false); // hurt: red sparks
        if (I) I.StartCoroutine(I.RespawnFX(to, c, side));
    }

    IEnumerator RespawnFX(Vector3 to, Color c, int side)
    {
        yield return new WaitForSeconds(0.12f);
        if (GameAudio.I) GameAudio.Play(GameAudio.I.respawn, side, 0.9f);
        Flash(to + Vector3.up * 0.9f, c, 3.2f);

        // a ring of light rising around the player
        var ring = Make("Respawn Ring", to + Vector3.up * 0.05f, null, true);
        var m = ring.main;
        m.loop = false; m.duration = 0.6f;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
        m.startSpeed = 0f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
        m.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, 0.6f));
        m.maxParticles = 160; m.stopAction = ParticleSystemStopAction.Destroy;
        var e = ring.emission; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 70), new ParticleSystem.Burst(0.25f, 50) });
        var sh = ring.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.75f; sh.radiusThickness = 0f;
        sh.rotation = new Vector3(-90f, 0f, 0f);
        var v = ring.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
        // all three axes must use the same curve mode (two constants), or Unity ignores the module
        v.x = new ParticleSystem.MinMaxCurve(0f, 0f); v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        v.y = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
        v.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f); v.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
        v.orbitalY = new ParticleSystem.MinMaxCurve(2.2f, 2.8f);
        Fade(ring, false);
        ring.Play();

        // a few bright sparks shooting up through the middle
        Burst(to + Vector3.up * 0.2f, Color.Lerp(c, Color.white, 0.4f), 30, 4f, 0.1f, 0.9f, true, 12f);
    }

    // ------------------------------------------------------------------ magic flower stars

    static Gradient Rainbow()
    {
        var g = new Gradient();
        g.SetKeys(new[]
        {
            new GradientColorKey(new Color(1f, 0.35f, 0.8f), 0f),    // pink
            new GradientColorKey(new Color(0.65f, 0.4f, 1f), 0.25f), // violet
            new GradientColorKey(new Color(0.3f, 0.9f, 1f), 0.5f),   // cyan
            new GradientColorKey(new Color(0.45f, 1f, 0.5f), 0.75f), // green
            new GradientColorKey(new Color(1f, 0.9f, 0.35f), 1f),    // gold
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        g.mode = GradientMode.Fixed; // whole colours, no muddy in-betweens
        return g;
    }

    /// <summary>
    /// Neon stars spiral up around a growing flower for 'duration' seconds (twinkling, every colour),
    /// then call <see cref="StarBurst"/> when it opens.
    /// </summary>
    public static void MagicStars(Vector3 pos, float duration, float radius = 0.7f)
    {
        if (!I) return;
        var ps = I.Make("Magic Stars", pos, null, true);
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = I.starMat ? I.starMat : I.particleMat;
        var m = ps.main;
        m.loop = false; m.duration = duration;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
        m.startSpeed = 0f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.36f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new ParticleSystem.MinMaxGradient(Rainbow()) { mode = ParticleSystemGradientMode.RandomColor };
        m.maxParticles = 500; m.stopAction = ParticleSystemStopAction.Destroy;
        var e = ps.emission; e.rateOverTime = 60f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius; sh.radiusThickness = 0.4f;
        sh.rotation = new Vector3(-90f, 0f, 0f);
        var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
        v.x = new ParticleSystem.MinMaxCurve(0f, 0f); v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        v.y = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
        v.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f); v.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
        v.orbitalY = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        var n = ps.noise; n.enabled = true; n.strength = 0.25f; n.frequency = 0.6f; n.scrollSpeed = 0.4f;
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        Twinkle(ps);
        ps.Play();
    }

    /// <summary>The flower opens: a burst of neon stars in every colour and a soft flash.</summary>
    public static void StarBurst(Vector3 pos, int count = 90)
    {
        if (!I) return;
        var ps = I.Make("Star Burst", pos, null, true);
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = I.starMat ? I.starMat : I.particleMat;
        var m = ps.main;
        m.loop = false; m.duration = 0.3f;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.6f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.42f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new ParticleSystem.MinMaxGradient(Rainbow()) { mode = ParticleSystemGradientMode.RandomColor };
        m.gravityModifier = -0.05f; // drift up a little, like fireflies
        m.maxParticles = count; m.stopAction = ParticleSystemStopAction.Destroy;
        var e = ps.emission; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.25f;
        var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 0.4f; lv.dampen = 0.08f; // burst out, then float
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
        Twinkle(ps);
        ps.Play();
        Flash(pos, new Color(1f, 0.55f, 0.9f, 0.5f), 1.4f);
    }

    /// <summary>Stars pop in, twinkle (size flickers) and fade.</summary>
    static void Twinkle(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        // two flicker curves out of phase: each star twinkles at its own moment
        AnimationCurve Flicker(int phase)
        {
            var c = new AnimationCurve();
            for (int i = 0; i <= 10; i++)
                c.AddKey(i / 10f, ((i + phase) % 2 == 0 ? 1f : 0.4f) * Mathf.Lerp(1f, 0.5f, i / 10f));
            return c;
        }
        sz.size = new ParticleSystem.MinMaxCurve(1f, Flicker(0), Flicker(1));
    }

    // ------------------------------------------------------------------ building blocks

    public static void Burst(Vector3 pos, Color c, int count, float speed, float size, float life, bool up, float coneAngle = 0f)
    {
        if (!I) return;
        var ps = I.Make("Neon Burst", pos, null, true);
        var m = ps.main;
        m.loop = false; m.duration = 0.2f;
        m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
        m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
        m.startSize = new ParticleSystem.MinMaxCurve(size * 0.4f, size);
        m.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, 0.55f));
        m.gravityModifier = up ? 0.25f : 0.6f;
        m.maxParticles = count; m.stopAction = ParticleSystemStopAction.Destroy;
        var e = ps.emission; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
        var sh = ps.shape;
        if (coneAngle > 0f) { sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = coneAngle; sh.radius = 0.15f; sh.rotation = new Vector3(-90f, 0f, 0f); }
        else if (up) { sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.25f; sh.rotation = new Vector3(-90f, 0f, 0f); }
        else { sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f; }
        I.Fade(ps, false);
        ps.Play();
    }

    /// <summary>One big soft glow that blooms and fades.</summary>
    public static void Flash(Vector3 pos, Color c, float size)
    {
        if (!I) return;
        var ps = I.Make("Neon Flash", pos, null, true);
        var m = ps.main;
        m.loop = false; m.duration = 0.1f; m.startLifetime = 0.55f; m.startSpeed = 0f; m.startSize = size;
        m.startColor = new Color(c.r, c.g, c.b, c.a < 1f ? c.a : 0.85f); m.maxParticles = 1; m.stopAction = ParticleSystemStopAction.Destroy;
        var e = ps.emission; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        var sh = ps.shape; sh.enabled = false;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.2f)));
        I.Fade(ps, false);
        ps.Play();
    }

    ParticleSystem Make(string name, Vector3 pos, Transform parent, bool worldSpace)
    {
        var go = new GameObject(name);
        go.layer = NeonOverlay.NeonLayer;
        go.transform.position = pos;
        if (parent) go.transform.SetParent(parent, true);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.playOnAwake = false;
        m.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        m.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = particleMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    /// <summary>Fade out (and shrink) over each particle's life; 'pulse' fades in too.</summary>
    void Fade(ParticleSystem ps, bool pulse)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  pulse ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) }
                        : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        if (pulse) return;
        var sz = ps.sizeOverLifetime;
        if (!sz.enabled)
        {
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        }
    }
}
