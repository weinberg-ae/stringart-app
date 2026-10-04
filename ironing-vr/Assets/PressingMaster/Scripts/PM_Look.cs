using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Visual style of the "neon void": post-processing glow, holographic grid floor, floating particles,
// graphite restyle of the station with neon outline.
public static class PM_Look
{
    public static void PostFX()
    {
        var go = new GameObject("PM_PostFX");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 50f;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = prof.Add<Bloom>(true);
        bloom.intensity.Override(1.4f);
        bloom.threshold.Override(0.75f);
        bloom.scatter.Override(0.75f);
        var vig = prof.Add<Vignette>(true);
        vig.intensity.Override(0.38f);
        vig.smoothness.Override(0.5f);
        var tone = prof.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);
        var ca = prof.Add<ColorAdjustments>(true);
        ca.contrast.Override(18f);
        ca.saturation.Override(12f);
        ca.postExposure.Override(0.25f);
        vol.profile = prof;

        if (Camera.main != null)
        {
            var data = Camera.main.GetComponent<UniversalAdditionalCameraData>();
            if (data != null) data.renderPostProcessing = true;
        }
    }

    // Sparkle burst (finished ironing).
    public static void Burst(Vector3 pos, Color c)
    {
        var go = new GameObject("PM_Burst");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
        main.startColor = new ParticleSystem.MinMaxGradient(c, Color.white);
        main.gravityModifier = 0.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 90) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        var m = PM_Util.TransparentMaterial(Color.white);
        m.mainTexture = PM_Util.SoftDot(32);
        pr.material = m;
        ps.Play();
        Object.Destroy(go, 3f);
    }

    // Light wave running over the floor (power on).
    public static void PulseRing(Vector3 center, Color c)
    {
        var go = new GameObject("PM_Pulse");
        go.transform.position = center;
        var p = go.AddComponent<PM_Pulse>();
        p.color = c;
    }

    // Small vertical light ring + sparks in front of a pressed button.
    public static void ButtonPulse(Vector3 pos, Color c, Vector3 towardViewer)
    {
        var go = new GameObject("PM_ButtonPulse");
        go.transform.position = pos + towardViewer * 0.01f;
        go.transform.rotation = Quaternion.LookRotation(-towardViewer, Vector3.up);
        var p = go.AddComponent<PM_Pulse>();
        p.color = c;
        p.maxRadius = 0.35f;
        p.duration = 0.7f;
        p.width = 0.012f;
        p.vertical = true;
        Burst(pos + towardViewer * 0.02f, c);
    }

    // Neon grid on the floor that fades out with distance.
    public static void GridFloor(Vector3 center, Vector3 forward, Vector3 right)
    {
        var root = new GameObject("PM_GridFloor").transform;
        root.position = center + Vector3.up * 0.003f;
        float half = 9f, step = 0.5f;
        int n = Mathf.RoundToInt(half * 2f / step);
        for (int i = 0; i <= n; i++)
        {
            float o = -half + i * step;
            float a = Mathf.Clamp01(1f - Mathf.Abs(o) / half);
            AddGridLine(root, right * o - forward * half, right * o + forward * half, a);
            AddGridLine(root, forward * o - right * half, forward * o + right * half, a);
        }
    }

    static void AddGridLine(Transform root, Vector3 a, Vector3 b, float strength)
    {
        var lr = PM_Util.Line(root, "G", new[] { a, (a + b) * 0.5f, b }, PM_Util.Cyan, 0.006f, false);
        var g = new Gradient();
        float peak = 0.45f * strength * strength;
        g.SetKeys(new[] { new GradientColorKey(PM_Util.Cyan, 0), new GradientColorKey(PM_Util.Cyan, 1) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.5f), new GradientAlphaKey(0f, 1f) });
        lr.colorGradient = g;
    }

    // Slowly drifting glowing particles around the player.
    public static void Dust(Vector3 center)
    {
        var go = new GameObject("PM_Dust");
        go.transform.position = center + Vector3.up * 1.5f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 14f;
        main.startSpeed = 0.03f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.018f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 1f, 1f, 0.8f), new Color(1f, 1f, 1f, 0.5f));
        main.maxParticles = 220;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;
        var em = ps.emission;
        em.rateOverTime = 16f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(8f, 3f, 8f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.05f;
        noise.frequency = 0.3f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        var pr = go.GetComponent<ParticleSystemRenderer>();
        var m = PM_Util.TransparentMaterial(Color.white);
        m.mainTexture = PM_Util.SoftDot(32);
        pr.material = m;
        ps.Play();
    }

    // Graphite look for the big parts of the station + neon outline around the ironing board.
    public static void RestyleStation(Transform table, HashSet<string> keepNames, Transform boardPart, Bounds board, Vector3 forward, Vector3 right)
    {
        var metal = new Material(PM_Util.LitShader());
        metal.SetColor("_BaseColor", new Color(0.13f, 0.14f, 0.17f));
        metal.SetFloat("_Metallic", 0.75f);
        metal.SetFloat("_Smoothness", 0.62f);
        var cover = new Material(PM_Util.LitShader());
        cover.SetColor("_BaseColor", new Color(0.09f, 0.1f, 0.12f));
        cover.SetFloat("_Metallic", 0f);
        cover.SetFloat("_Smoothness", 0.18f);
        foreach (MeshRenderer r in table.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (keepNames.Contains(r.name)) continue;
            if (r.name.StartsWith("Box") || r.name.StartsWith("Cylinder")) continue; // labels and screws keep their look
            bool ours = false;
            for (Transform t = r.transform; t != null && t != table; t = t.parent)
                if (t.name.StartsWith("PM_")) { ours = true; break; }
            if (ours) continue;                                                       // iron, knob, points of light
            Material use = (boardPart != null && r.transform == boardPart) ? cover : metal;
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = use;
            r.sharedMaterials = mats;
        }

        // Neon outline of the board (top edge).
        Vector3 c = board.center;
        float y = board.max.y + 0.002f;
        float hx = Mathf.Abs(right.x) * board.extents.x + Mathf.Abs(right.z) * board.extents.z;
        float hz = Mathf.Abs(forward.x) * board.extents.x + Mathf.Abs(forward.z) * board.extents.z;
        var root = new GameObject("PM_BoardOutline").transform;
        root.position = new Vector3(c.x, y, c.z);
        Vector3[] pts =
        {
            -right * hx - forward * hz, right * hx - forward * hz, right * hx + forward * hz, -right * hx + forward * hz
        };
        PM_Util.Line(root, "Outline", pts, PM_Util.Cyan, 0.006f, true);
        var glow = PM_Util.Line(root, "Glow", pts, new Color(0.1f, 0.95f, 1f, 0.25f), 0.03f, true);
        glow.transform.localPosition = Vector3.down * 0.001f;
    }
}

// Expanding glowing ring on the floor.
public class PM_Pulse : MonoBehaviour
{
    public Color color = Color.cyan;
    public float maxRadius = 7f, duration = 1.8f, width = 0.03f;
    public bool vertical;
    LineRenderer lr;
    float t;

    void Start()
    {
        lr = PM_Util.Line(transform, "Ring", PM_Util.Circle(1f, 96, Vector3.right, vertical ? Vector3.up : Vector3.forward, Vector3.zero), color, width, true);
    }

    void Update()
    {
        t += Time.deltaTime / duration;
        float r = Mathf.Lerp(0.02f, maxRadius, t);
        lr.transform.localScale = vertical ? new Vector3(r, r, 1f) : new Vector3(r, 1f, r);
        Color c = color; c.a = (1f - t) * 0.9f;
        lr.startColor = c; lr.endColor = c;
        if (t >= 1f) Destroy(gameObject);
    }
}
