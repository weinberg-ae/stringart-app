using System.Collections.Generic;
using UnityEngine;

// Pulsing neon glow on a group of objects + a floating marker above them.
public class PM_Highlight : MonoBehaviour
{
    class Entry { public Material mat; public bool hadEmission; public Color oldEmission; }

    readonly List<Entry> entries = new List<Entry>();
    GameObject marker;
    Color color = PM_Util.Cyan;
    Vector3 markerBase;

    public static PM_Highlight Create(string name)
    {
        var go = new GameObject(name);
        return go.AddComponent<PM_Highlight>();
    }

    public void Show(IList<Transform> targets, Color c, bool withMarker = true)
    {
        Clear();
        if (targets == null || targets.Count == 0) return;
        color = c;
        foreach (Transform t in targets)
        {
            if (t == null) continue;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer || r is ParticleSystemRenderer) continue;
                foreach (Material m in r.materials)
                {
                    if (!m.HasProperty("_EmissionColor")) continue;
                    var e = new Entry { mat = m, hadEmission = m.IsKeywordEnabled("_EMISSION"), oldEmission = m.GetColor("_EmissionColor") };
                    m.EnableKeyword("_EMISSION");
                    entries.Add(e);
                }
            }
        }
        if (withMarker)
        {
            Bounds b = PM_Util.WorldBounds(targets);
            if (marker == null) marker = BuildMarker();
            markerBase = new Vector3(b.center.x, b.max.y + 0.08f, b.center.z);
            marker.transform.position = markerBase;
            marker.SetActive(true);
        }
    }

    public void Clear()
    {
        foreach (Entry e in entries)
        {
            if (e.mat == null) continue;
            e.mat.SetColor("_EmissionColor", e.oldEmission);
            if (!e.hadEmission) e.mat.DisableKeyword("_EMISSION");
        }
        entries.Clear();
        if (marker != null) marker.SetActive(false);
    }

    GameObject BuildMarker()
    {
        var root = new GameObject("PM_Marker");
        root.transform.SetParent(transform, false);
        // Neon arrow made of lines (pointing down).
        Vector3[] arrow =
        {
            new Vector3(-0.03f, 0.06f, 0), new Vector3(0.03f, 0.06f, 0), new Vector3(0.03f, 0.03f, 0),
            new Vector3(0.05f, 0.03f, 0), new Vector3(0, -0.02f, 0), new Vector3(-0.05f, 0.03f, 0),
            new Vector3(-0.03f, 0.03f, 0)
        };
        PM_Util.Line(root.transform, "ArrowA", arrow, PM_Util.Cyan, 0.006f, true);
        var b = PM_Util.Line(root.transform, "ArrowB", arrow, PM_Util.Cyan, 0.006f, true);
        b.transform.localRotation = Quaternion.Euler(0, 90, 0);
        return root;
    }

    void Update()
    {
        float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 5f);
        Color em = color * (1.2f * pulse);
        foreach (Entry e in entries)
            if (e.mat != null) e.mat.SetColor("_EmissionColor", em);
        if (marker != null && marker.activeSelf)
        {
            marker.transform.position = markerBase + Vector3.up * (0.02f * Mathf.Sin(Time.time * 3f));
            marker.transform.Rotate(0, 90f * Time.deltaTime, 0, Space.World);
        }
    }
}
