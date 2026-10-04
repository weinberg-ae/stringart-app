using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// A glowing "point of light" on a part of the station. Touch it (or point the ray and press Trigger)
// to open an info card. Turns green after it was opened.
public class PM_Hotspot : MonoBehaviour
{
    public PM_HotspotInfo info;
    public List<Transform> parts;           // model parts to glow while the card is open
    public bool Visited { get; private set; }
    public Action<PM_Hotspot> onClick;

    Transform visual;
    Material core, halo;
    float phase;
    bool hovered;
    Transform label;
    Transform ring;
    LineRenderer ringLine;

    public static PM_Hotspot Create(PM_HotspotInfo info, Vector3 worldPos, Transform parent, List<Transform> parts)
    {
        var go = new GameObject("PM_Hotspot_" + info.id);
        go.transform.position = worldPos;
        if (parent != null) go.transform.SetParent(parent, true);
        var h = go.AddComponent<PM_Hotspot>();
        h.info = info;
        h.parts = parts;
        h.Build();
        return h;
    }

    void Build()
    {
        phase = UnityEngine.Random.value * 6f;
        Color acc = info.accent;
        var sc = gameObject.AddComponent<SphereCollider>();
        sc.radius = info.isFiber ? 0.2f : 0.055f;

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        core = PM_Util.NeonMaterial(acc, 3f);
        halo = PM_Util.TransparentMaterial(new Color(acc.r, acc.g, acc.b, 0.22f));
        if (info.isFiber)
        {
            PM_Shapes.Fiber(info.fiber, visual, acc);
            BuildLabel(-0.13f, 40, acc);
            var cl = PM_Clickable.Add(gameObject, () => { if (onClick != null) onClick(this); });
            cl.onHover = v => hovered = v;
            return;
        }
        var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(c.GetComponent<Collider>());
        c.transform.SetParent(visual, false);
        c.transform.localScale = Vector3.one * 0.028f;
        c.GetComponent<Renderer>().material = core;
        var h = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(h.GetComponent<Collider>());
        h.transform.SetParent(visual, false);
        h.transform.localScale = Vector3.one * 0.065f;
        h.GetComponent<Renderer>().material = halo;
        BuildLabel(0.055f, 34, Color.white);

        ring = new GameObject("Ring").transform;
        ring.SetParent(transform, false);
        ringLine = PM_Util.Line(ring, "Circle", PM_Util.Circle(0.035f, 40, Vector3.right, Vector3.up, Vector3.zero), acc, 0.003f, true);

        var click = PM_Clickable.Add(gameObject, () => { if (onClick != null) onClick(this); });
        click.onHover = v => hovered = v;
    }

    void BuildLabel(float y, float size, Color col)
    {
        var lg = new GameObject("Label", typeof(RectTransform));
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0, y, 0);
        var canvas = lg.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = lg.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(260, 50);
        rt.localScale = Vector3.one * 0.0006f;
        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(lg.transform, false);
        t.rectTransform.sizeDelta = new Vector2(260, 50);
        PM_Panel.Prepare(t);
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = col;
        t.raycastTarget = false;
        t.text = PM_Hebrew.VisualLine(info.label);
        label = lg.transform;
    }

    public void SetVisited(bool v)
    {
        Visited = v;
        if (info.isFiber) return;
        Color col = v ? PM_Util.Green : info.accent;
        core.SetColor("_EmissionColor", col * 3f);
        halo.color = new Color(col.r, col.g, col.b, 0.22f);
    }

    void Update()
    {
        Camera cam = Camera.main;
        if (info.isFiber)
        {
            float k = Visited ? 1f : 1f + 0.06f * Mathf.Sin(Time.time * 2f + phase);
            visual.localScale = Vector3.one * (hovered ? k * 1.2f : k);
            if (cam != null)
            {
                Vector3 fd = transform.position - cam.transform.position;
                fd.y = 0;
                if (fd.sqrMagnitude > 0.001f) visual.rotation = Quaternion.LookRotation(fd, Vector3.up) * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 0.6f + phase) * 8f);
            }
        }
        else
        {
            float s = 1f + 0.18f * Mathf.Sin(Time.time * 3.2f + phase);
            if (hovered) s *= 1.5f;
            visual.localScale = Vector3.one * s;
        }
        if (cam != null && label != null)
        {
            Vector3 ld = label.position - cam.transform.position;
            ld.y = 0;
            if (ld.sqrMagnitude > 0.0001f) label.rotation = Quaternion.LookRotation(ld, Vector3.up);
        }
        if (ring == null) return;
        float rt = Mathf.Repeat(Time.time * 0.6f + phase, 1f);
        ring.localScale = Vector3.one * (1f + rt * 2.2f);
        Color rc = Visited ? PM_Util.Green : info.accent;
        rc.a = (1f - rt) * 0.7f;
        ringLine.startColor = rc;
        ringLine.endColor = rc;
        if (cam != null) ring.rotation = Quaternion.LookRotation(ring.position - cam.transform.position, Vector3.up);
        if (cam != null && label != null)
        {
            Vector3 d = label.position - cam.transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.0001f) label.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }
}
