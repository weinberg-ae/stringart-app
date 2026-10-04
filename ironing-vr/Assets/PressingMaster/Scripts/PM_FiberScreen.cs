using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A floating holographic screen for one fiber: glowing symbol + name.
// Click = the screen grows and shows the explanation (with narration); click again = collapse.
public class PM_FiberScreen : MonoBehaviour
{
    public PM_HotspotInfo info;
    public bool Visited { get; private set; }
    public bool Expanded { get; private set; }
    public System.Action<PM_FiberScreen> onClick;

    const float W = 0.46f, H = 0.5f, WE = 0.7f, HE = 0.86f;   // collapsed / expanded size (meters)

    RectTransform canvasRt;
    Image bg, header;
    TextMeshProUGUI title, sub, body;
    Transform symbol;
    BoxCollider box;
    float grow;          // 0 = collapsed, 1 = expanded
    float phase;
    bool hovered;
    Color acc;

    public static PM_FiberScreen Create(PM_HotspotInfo info, string groupText, Vector3 pos)
    {
        var go = new GameObject("PM_Fiber_" + info.fiber);
        go.transform.position = pos;
        var s = go.AddComponent<PM_FiberScreen>();
        s.info = info;
        s.Build(groupText);
        return s;
    }

    void Build(string groupText)
    {
        acc = info.accent;
        phase = Random.value * 6f;
        var cgo = new GameObject("Canvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasRt = cgo.GetComponent<RectTransform>();
        canvasRt.localScale = Vector3.one * 0.001f;

        bg = Stretch(cgo.transform, "Bg", 0, 0, 0, 0).gameObject.AddComponent<Image>();
        bg.color = new Color(acc.r * 0.16f, acc.g * 0.16f, acc.b * 0.16f, 0.72f);
        header = Stretch(cgo.transform, "Header", 0, 0, 0, -1).gameObject.AddComponent<Image>();
        header.color = new Color(acc.r, acc.g, acc.b, 0.28f);
        var hr = header.rectTransform; hr.anchorMin = new Vector2(0, 1); hr.anchorMax = new Vector2(1, 1); hr.pivot = new Vector2(0.5f, 1); hr.sizeDelta = new Vector2(0, 80); hr.anchoredPosition = Vector2.zero;
        foreach (var e in new[] { new Vector4(0, 0, 1, 0), new Vector4(0, 1, 1, 1), new Vector4(0, 0, 0, 1), new Vector4(1, 0, 1, 1) })
        {
            var edge = new GameObject("Edge", typeof(RectTransform)).AddComponent<Image>();
            edge.transform.SetParent(cgo.transform, false);
            var r = edge.rectTransform;
            r.anchorMin = new Vector2(e.x, e.y); r.anchorMax = new Vector2(e.z, e.w);
            r.sizeDelta = (e.x == e.z) ? new Vector2(5, 0) : new Vector2(0, 5);
            r.anchoredPosition = Vector2.zero;
            edge.color = acc;
        }

        title = Text(cgo.transform, "Title", 44, Color.white);
        Pin(title.rectTransform, 1, -12, 70);
        title.text = PM_Hebrew.VisualLine(info.label);
        sub = Text(cgo.transform, "Sub", 26, acc);
        Pin(sub.rectTransform, 0, 14, 40);
        sub.text = PM_Hebrew.VisualLine(groupText);
        body = Text(cgo.transform, "Body", 27, Color.Lerp(Color.white, acc, 0.25f));
        body.alignment = TextAlignmentOptions.Top;
        body.text = PM_Hebrew.Visual(info.body, 36);
        body.gameObject.SetActive(false);

        symbol = new GameObject("Symbol").transform;
        symbol.SetParent(transform, false);
        symbol.localPosition = new Vector3(0, -0.01f, -0.01f);
        PM_Shapes.Fiber(info.fiber, symbol, acc);
        symbol.localScale = Vector3.one * 0.75f;

        box = gameObject.AddComponent<BoxCollider>();
        var click = PM_Clickable.Add(gameObject, () => { if (onClick != null) onClick(this); });
        click.onHover = v => hovered = v;
        ApplySize();
    }

    static RectTransform Stretch(Transform parent, string name, float l, float b, float r, float t)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(r, t);
        return rt;
    }

    static TextMeshProUGUI Text(Transform parent, string name, float size, Color c)
    {
        var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        PM_Panel.Prepare(t);
        t.fontSize = size;
        t.color = c;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    // Anchors a text strip to the top (side=1) or bottom (side=0) of the screen.
    static void Pin(RectTransform r, int side, float offset, float height)
    {
        r.anchorMin = new Vector2(0, side); r.anchorMax = new Vector2(1, side);
        r.pivot = new Vector2(0.5f, side);
        r.sizeDelta = new Vector2(-20, height);
        r.anchoredPosition = new Vector2(0, offset);
    }

    public void SetExpanded(bool e)
    {
        Expanded = e;
        if (e) Visited = true;
        body.gameObject.SetActive(e);
    }

    void ApplySize()
    {
        float k = Mathf.SmoothStep(0f, 1f, grow);
        float w = Mathf.Lerp(W, WE, k), h = Mathf.Lerp(H, HE, k);
        canvasRt.sizeDelta = new Vector2(w * 1000f, h * 1000f);
        box.size = new Vector3(w, h, 0.04f);
        // Collapsed: big symbol in the middle. Expanded: small symbol under the title, text below.
        symbol.localPosition = new Vector3(0, Mathf.Lerp(-0.01f, h * 0.5f - 0.15f, k), -0.01f);
        symbol.localScale = Vector3.one * Mathf.Lerp(0.75f, 0.45f, k);
        var br = body.rectTransform;
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(25, 60); br.offsetMax = new Vector2(-25, -230);
        float glow = Visited ? 0.72f : 0.62f + 0.1f * Mathf.Sin(Time.time * 2f + phase);
        bg.color = new Color(acc.r * 0.16f, acc.g * 0.16f, acc.b * 0.16f, hovered ? 0.85f : glow);
    }

    void Update()
    {
        grow = Mathf.MoveTowards(grow, Expanded ? 1f : 0f, Time.deltaTime * 3f);
        ApplySize();
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 d = transform.position - cam.transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
        symbol.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 0.7f + phase) * 6f);
    }
}
