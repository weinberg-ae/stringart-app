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
        var sc = gameObject.AddComponent<SphereCollider>();
        sc.radius = 0.055f;

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(c.GetComponent<Collider>());
        c.transform.SetParent(visual, false);
        c.transform.localScale = Vector3.one * 0.028f;
        core = PM_Util.NeonMaterial(PM_Util.Cyan, 3f);
        c.GetComponent<Renderer>().material = core;
        var h = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(h.GetComponent<Collider>());
        h.transform.SetParent(visual, false);
        h.transform.localScale = Vector3.one * 0.065f;
        halo = PM_Util.TransparentMaterial(new Color(0.1f, 0.95f, 1f, 0.22f));
        h.GetComponent<Renderer>().material = halo;

        // Short Hebrew label above the point.
        var lg = new GameObject("Label", typeof(RectTransform));
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0, 0.055f, 0);
        var canvas = lg.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = lg.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(260, 50);
        rt.localScale = Vector3.one * 0.0006f;
        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(lg.transform, false);
        t.rectTransform.sizeDelta = new Vector2(260, 50);
        t.font = PM_Panel.Font();
        t.fontSize = 34;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        t.text = PM_Hebrew.VisualLine(info.label);
        label = lg.transform;

        ring = new GameObject("Ring").transform;
        ring.SetParent(transform, false);
        ringLine = PM_Util.Line(ring, "Circle", PM_Util.Circle(0.035f, 40, Vector3.right, Vector3.up, Vector3.zero), PM_Util.Cyan, 0.003f, true);

        var click = PM_Clickable.Add(gameObject, () => { if (onClick != null) onClick(this); });
        click.onHover = v => hovered = v;
    }

    public void SetVisited(bool v)
    {
        Visited = v;
        Color col = v ? PM_Util.Green : PM_Util.Cyan;
        core.SetColor("_EmissionColor", col * 3f);
        halo.color = new Color(col.r, col.g, col.b, 0.22f);
    }

    void Update()
    {
        float s = 1f + 0.18f * Mathf.Sin(Time.time * 3.2f + phase);
        if (hovered) s *= 1.5f;
        visual.localScale = Vector3.one * s;
        Camera cam = Camera.main;
        float rt = Mathf.Repeat(Time.time * 0.6f + phase, 1f);
        ring.localScale = Vector3.one * (1f + rt * 2.2f);
        Color rc = Visited ? PM_Util.Green : PM_Util.Cyan;
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
