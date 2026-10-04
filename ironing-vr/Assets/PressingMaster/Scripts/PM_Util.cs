using System.Collections.Generic;
using UnityEngine;

// Small helpers shared by all Pressing Master scripts.
public static class PM_Util
{
    public static readonly Color Cyan = new Color(0.1f, 0.95f, 1f);
    public static readonly Color Blue = new Color(0.15f, 0.45f, 1f);
    public static readonly Color Yellow = new Color(1f, 0.85f, 0.1f);
    public static readonly Color Red = new Color(1f, 0.15f, 0.1f);
    public static readonly Color Green = new Color(0.2f, 1f, 0.45f);

    public static Color ModeColor(int mode)
    {
        if (mode == 1) return Blue;
        if (mode == 2) return Yellow;
        if (mode == 3) return Red;
        return Cyan;
    }

    // Finds a child (any depth) with an exact name. Includes inactive objects.
    public static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    public static List<Transform> FindAll(Transform root, params string[] names)
    {
        var list = new List<Transform>();
        foreach (string n in names)
        {
            Transform t = FindDeep(root, n);
            if (t != null) list.Add(t);
            else Debug.LogWarning("[PM] Не найдена деталь модели: " + n);
        }
        return list;
    }

    public static Transform FindInScene(string name)
    {
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.name == name && t.gameObject.scene.IsValid()) return t;
        return null;
    }

    // World bounds of all renderers of the given objects (and their children).
    public static Bounds WorldBounds(IEnumerable<Transform> objects)
    {
        bool has = false;
        Bounds b = new Bounds();
        foreach (Transform t in objects)
        {
            if (t == null) continue;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
        }
        return b;
    }

    public static Bounds WorldBounds(Transform t)
    {
        return WorldBounds(new[] { t });
    }

    public static Shader LitShader()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        return s;
    }

    public static Shader UnlitShader()
    {
        Shader s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        return s;
    }

    // Solid neon material (URP Lit + emission).
    public static Material NeonMaterial(Color c, float intensity)
    {
        var m = new Material(LitShader());
        m.SetColor("_BaseColor", c * 0.2f);
        m.SetColor("_Color", c * 0.2f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        return m;
    }

    // Transparent unlit material (uses vertex/material color alpha).
    public static Material TransparentMaterial(Color c)
    {
        var m = new Material(UnlitShader());
        m.color = c;
        return m;
    }

    public static LineRenderer Line(Transform parent, string name, Vector3[] points, Color color, float width, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = loop;
        lr.positionCount = points.Length;
        lr.SetPositions(points);
        lr.widthMultiplier = width;
        lr.material = TransparentMaterial(Color.white);
        lr.startColor = color;
        lr.endColor = color;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    public static Vector3[] Circle(float radius, int segments, Vector3 axisA, Vector3 axisB, Vector3 center)
    {
        var pts = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            pts[i] = center + axisA * (Mathf.Cos(a) * radius) + axisB * (Mathf.Sin(a) * radius);
        }
        return pts;
    }

    // Box collider object with world-axis-aligned size around given world bounds.
    public static GameObject HitBox(string name, Bounds worldBounds, float grow, float minSize, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.position = worldBounds.center;
        go.transform.rotation = Quaternion.identity;
        var bc = go.AddComponent<BoxCollider>();
        Vector3 size = worldBounds.size * grow;
        size.x = Mathf.Max(size.x, minSize);
        size.y = Mathf.Max(size.y, minSize);
        size.z = Mathf.Max(size.z, minSize);
        bc.size = size;
        if (parent != null) go.transform.SetParent(parent, true);
        return go;
    }

    public static Texture2D SoftDot(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        float h = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - h) * (x - h) + (y - h) * (y - h)) / h;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
