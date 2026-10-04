using System.Collections.Generic;
using UnityEngine;

// Glowing line drawings: fiber symbols (in the air above the station) and background silhouettes
// (garments, sewing patterns, rulers) that float far away in the void.
public static class PM_Shapes
{
    // ---------- helpers ----------
    static void Draw(Transform parent, Vector3[] pts, Color c, float width, bool loop)
    {
        PM_Util.Line(parent, "L", pts, c, width, loop);
        Color halo = c; halo.a = 0.18f;
        PM_Util.Line(parent, "H", pts, halo, width * 4f, loop);
    }

    static Vector3[] Poly(params float[] xy)
    {
        var p = new Vector3[xy.Length / 2];
        for (int i = 0; i < p.Length; i++) p[i] = new Vector3(xy[i * 2], xy[i * 2 + 1], 0);
        return p;
    }

    static Vector3[] Scale(Vector3[] p, float s, Vector3 offset)
    {
        var r = new Vector3[p.Length];
        for (int i = 0; i < p.Length; i++) r[i] = p[i] * s + offset;
        return r;
    }

    static Vector3[] Circle(float r, Vector3 c, int n = 24)
    {
        return PM_Util.Circle(r, n, Vector3.right, Vector3.up, c);
    }

    // ---------- fiber symbols (about 0.5 m wide) ----------
    public static void Fiber(PM_FabricType t, Transform parent, Color c)
    {
        const float w = 0.006f;
        switch (t)
        {
            case PM_FabricType.Cotton:   // flat twisted ribbon (convolutions)
            {
                int n = 60;
                var a = new Vector3[n]; var b = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    float x = -0.25f + 0.5f * i / (n - 1);
                    float ph = x * 38f;
                    a[i] = new Vector3(x, Mathf.Sin(ph) * 0.03f + Mathf.Sin(x * 6f) * 0.02f, 0);
                    b[i] = new Vector3(x, -Mathf.Sin(ph) * 0.03f + Mathf.Sin(x * 6f) * 0.02f, 0);
                }
                Draw(parent, a, c, w, false); Draw(parent, b, c, w, false);
                break;
            }
            case PM_FabricType.Linen:    // long straight fiber with nodes
            {
                Draw(parent, Poly(-0.27f, 0.012f, 0.27f, 0.02f), c, w, false);
                Draw(parent, Poly(-0.27f, -0.012f, 0.27f, -0.004f), c, w, false);
                for (int i = 0; i < 6; i++)
                {
                    float x = -0.22f + i * 0.09f, y = 0.004f + x * 0.015f;
                    Draw(parent, Poly(x - 0.012f, y + 0.026f, x + 0.012f, y - 0.026f), c, w * 0.8f, false);
                    Draw(parent, Poly(x + 0.012f, y + 0.026f, x - 0.012f, y - 0.026f), c, w * 0.8f, false);
                }
                break;
            }
            case PM_FabricType.Wool:     // crimped fiber with scales
            {
                int n = 70;
                var p = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    float x = -0.26f + 0.52f * i / (n - 1);
                    p[i] = new Vector3(x, Mathf.Sin(x * 34f) * 0.045f, 0);
                }
                Draw(parent, p, c, w * 1.4f, false);
                for (int i = 4; i < n - 4; i += 5)
                {
                    Vector3 q = p[i];
                    Draw(parent, new[] { q + new Vector3(-0.01f, 0.018f, 0), q + new Vector3(0.008f, 0.006f, 0), q + new Vector3(-0.01f, -0.006f, 0) }, c, w * 0.7f, false);
                }
                break;
            }
            case PM_FabricType.Silk:     // smooth continuous double filament + triangular cross-section
            {
                int n = 60;
                var a = new Vector3[n]; var b = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    float x = -0.26f + 0.44f * i / (n - 1);
                    float y = Mathf.Sin(x * 7f) * 0.05f;
                    a[i] = new Vector3(x, y + 0.008f + Mathf.Sin(x * 20f) * 0.006f, 0);
                    b[i] = new Vector3(x, y - 0.008f - Mathf.Sin(x * 20f) * 0.006f, 0);
                }
                Draw(parent, a, c, w, false); Draw(parent, b, c, w, false);
                Draw(parent, Poly(0.21f, -0.03f, 0.27f, -0.03f, 0.24f, 0.025f), c, w, true);
                break;
            }
            case PM_FabricType.Polyester: // PET molecule: benzene ring + ester groups + chain
            {
                Draw(parent, PM_Util.Circle(0.06f, 6, Vector3.right, Vector3.up, Vector3.zero), c, w, true);
                Draw(parent, Circle(0.035f, Vector3.zero, 20), c, w * 0.7f, true);
                Draw(parent, Poly(-0.06f, 0f, -0.12f, 0.035f, -0.18f, 0f, -0.24f, 0.035f), c, w, false);
                Draw(parent, Poly(-0.12f, 0.035f, -0.12f, 0.09f), c, w, false);
                Draw(parent, Circle(0.014f, new Vector3(-0.12f, 0.105f, 0), 12), c, w, true);
                Draw(parent, Poly(0.06f, 0f, 0.12f, -0.035f, 0.18f, 0f, 0.24f, -0.035f), c, w, false);
                Draw(parent, Poly(0.12f, -0.035f, 0.12f, -0.09f), c, w, false);
                Draw(parent, Circle(0.014f, new Vector3(0.12f, -0.105f, 0), 12), c, w, true);
                Draw(parent, Circle(0.012f, new Vector3(-0.18f, 0f, 0), 10), c, w * 0.8f, true);
                Draw(parent, Circle(0.012f, new Vector3(0.18f, 0f, 0), 10), c, w * 0.8f, true);
                break;
            }
        }
    }

    // ---------- background silhouettes (about 1 m) ----------
    public static void Silhouette(int kind, Transform parent, Color c)
    {
        const float w = 0.01f;
        switch (kind % 9)
        {
            case 0: // shirt
                Draw(parent, Poly(-0.15f, 0.45f, -0.45f, 0.3f, -0.35f, 0.05f, -0.25f, 0.12f, -0.25f, -0.45f, 0.25f, -0.45f, 0.25f, 0.12f,
                    0.35f, 0.05f, 0.45f, 0.3f, 0.15f, 0.45f, 0.06f, 0.36f, 0f, 0.3f, -0.06f, 0.36f), c, w, true);
                Draw(parent, Poly(0f, 0.3f, 0f, -0.45f), c, w * 0.6f, false);
                break;
            case 1: // dress
                Draw(parent, Poly(-0.12f, 0.5f, -0.16f, 0.2f, -0.12f, 0.05f, -0.4f, -0.5f, 0.4f, -0.5f, 0.12f, 0.05f, 0.16f, 0.2f, 0.12f, 0.5f,
                    0.05f, 0.42f, -0.05f, 0.42f), c, w, true);
                break;
            case 2: // trousers
                Draw(parent, Poly(-0.22f, 0.5f, 0.22f, 0.5f, 0.26f, -0.5f, 0.06f, -0.5f, 0f, 0.1f, -0.06f, -0.5f, -0.26f, -0.5f), c, w, true);
                break;
            case 3: // sewing pattern piece with notches and grain line
                Draw(parent, Poly(-0.3f, 0.45f, 0.15f, 0.45f, 0.32f, 0.2f, 0.25f, -0.45f, -0.3f, -0.45f, -0.35f, 0.1f), c, w, true);
                Draw(parent, Poly(-0.05f, 0.3f, -0.05f, -0.3f), c, w * 0.6f, false);
                Draw(parent, Poly(-0.08f, 0.25f, -0.05f, 0.3f, -0.02f, 0.25f), c, w * 0.6f, false);
                Draw(parent, Poly(-0.08f, -0.25f, -0.05f, -0.3f, -0.02f, -0.25f), c, w * 0.6f, false);
                Draw(parent, Poly(0.3f, 0.02f, 0.25f, 0.02f), c, w * 0.6f, false);
                break;
            case 4: // L-square ruler with ticks
                Draw(parent, Poly(-0.4f, 0.5f, -0.32f, 0.5f, -0.32f, -0.42f, 0.4f, -0.42f, 0.4f, -0.5f, -0.4f, -0.5f), c, w, true);
                for (int i = 0; i < 9; i++) Draw(parent, Poly(-0.4f, 0.4f - i * 0.1f, -0.36f, 0.4f - i * 0.1f), c, w * 0.5f, false);
                for (int i = 0; i < 7; i++) Draw(parent, Poly(-0.25f + i * 0.1f, -0.5f, -0.25f + i * 0.1f, -0.46f), c, w * 0.5f, false);
                break;
            case 5: // French curve
            {
                int n = 40;
                var p = new Vector3[n * 2];
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)(n - 1);
                    float x = -0.4f + 0.8f * t;
                    p[i] = new Vector3(x, Mathf.Sin(t * 3.4f) * 0.3f - 0.1f, 0);
                    p[n * 2 - 1 - i] = new Vector3(x, Mathf.Sin(t * 3.4f) * 0.18f - 0.05f + 0.1f * t, 0);
                }
                Draw(parent, p, c, w, true);
                break;
            }
            case 6: // scissors
                Draw(parent, Circle(0.08f, new Vector3(-0.28f, 0.1f, 0)), c, w, true);
                Draw(parent, Circle(0.08f, new Vector3(-0.28f, -0.12f, 0)), c, w, true);
                Draw(parent, Poly(-0.21f, 0.07f, 0.4f, -0.08f, -0.05f, -0.01f), c, w, false);
                Draw(parent, Poly(-0.21f, -0.09f, 0.4f, 0.06f, -0.05f, -0.01f), c, w, false);
                break;
            case 7: // measuring tape spiral
            {
                int n = 80;
                var p = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    float a = i * 0.25f, r = 0.05f + i * 0.0045f;
                    p[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
                }
                Draw(parent, p, c, w, false);
                Draw(parent, new[] { p[n - 1], p[n - 1] + new Vector3(0.35f, -0.25f, 0) }, c, w, false);
                break;
            }
            default: // needle and thread
                Draw(parent, Poly(-0.4f, 0.4f, 0.35f, -0.35f), c, w, false);
                Draw(parent, Circle(0.02f, new Vector3(-0.36f, 0.36f, 0), 10), c, w * 0.6f, true);
                var th = new Vector3[40];
                for (int i = 0; i < 40; i++) { float t = i / 39f; th[i] = new Vector3(-0.36f + t * 0.5f, 0.36f - t * 0.9f + Mathf.Sin(t * 12f) * 0.06f, 0); }
                Draw(parent, th, c, w * 0.6f, false);
                break;
        }
    }

    // Ring of silhouettes far away around the player.
    public static void Atmosphere(Vector3 center)
    {
        var root = new GameObject("PM_Atmosphere").transform;
        root.position = center;
        Color[] cols = { new Color(0.1f, 0.95f, 1f, 0.35f), new Color(1f, 0.3f, 0.6f, 0.3f), new Color(1f, 0.85f, 0.2f, 0.28f), new Color(0.6f, 0.4f, 1f, 0.3f) };
        var rnd = new System.Random(5);
        int count = 18;
        for (int i = 0; i < count; i++)
        {
            float ang = i * Mathf.PI * 2f / count + (float)rnd.NextDouble() * 0.2f;
            float rad = 4.5f + (float)rnd.NextDouble() * 2.5f;
            var item = new GameObject("Silhouette" + i).transform;
            item.SetParent(root, false);
            item.localPosition = new Vector3(Mathf.Cos(ang) * rad, 0.9f + (float)rnd.NextDouble() * 2.4f, Mathf.Sin(ang) * rad);
            item.localScale = Vector3.one * (0.8f + (float)rnd.NextDouble() * 0.7f);
            Silhouette(i, item, cols[i % cols.Length]);
            var f = item.gameObject.AddComponent<PM_Float>();
            f.faceCenter = root;
            f.tilt = ((float)rnd.NextDouble() - 0.5f) * 30f;
        }
    }
}

// Slow floating + facing a point.
public class PM_Float : MonoBehaviour
{
    public Transform faceCenter;
    public float tilt;
    public float amplitude = 0.06f;
    Vector3 basePos;
    float phase;

    void Start()
    {
        basePos = transform.localPosition;
        phase = Random.value * 10f;
    }

    void Update()
    {
        transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * 0.5f + phase) * amplitude);
        Vector3 target = faceCenter != null ? faceCenter.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        Vector3 d = transform.position - target;
        d.y = 0;
        if (d.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(d, Vector3.up) * Quaternion.Euler(0, 0, tilt + Mathf.Sin(Time.time * 0.3f + phase) * 4f);
    }
}
