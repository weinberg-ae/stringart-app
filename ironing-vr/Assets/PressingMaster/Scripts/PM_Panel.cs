using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Floating holographic screen with Hebrew text and pressable 3D buttons.
public class PM_Panel : MonoBehaviour
{
    public const float Width = 1.0f;   // meters
    public const float Height = 0.7f;  // meters

    static TMP_FontAsset font;

    TextMeshProUGUI title, body, status, counter;
    readonly List<GameObject> buttons = new List<GameObject>();

    public static TMP_FontAsset Font()
    {
        if (font != null) return font;
        Font f = Resources.Load<Font>("PM_Fonts/VarelaRound-Regular");
        if (f != null) font = TMP_FontAsset.CreateFontAsset(f);
        if (font == null)
        {
            Debug.LogWarning("[PM] Шрифт с ивритом не найден в Assets/Resources/PM_Fonts — иврит может не отображаться.");
            font = TMP_Settings.defaultFontAsset;
        }
        return font;
    }

    public static PM_Panel Create(Transform parent)
    {
        var root = new GameObject("PM_Panel");
        root.transform.SetParent(parent, false);
        var p = root.AddComponent<PM_Panel>();
        p.Build();
        return p;
    }

    void Build()
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1000, 700);
        rt.localScale = Vector3.one * 0.001f;

        var bg = MakeRect(canvasGo.transform, "Background", 0, 0, 1000, 700);
        bg.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.03f, 0.07f, 0.9f);
        MakeFrame(canvasGo.transform, 1000, 700, 4, PM_Util.Cyan);
        var sep = MakeRect(canvasGo.transform, "Line", 30, 108, 940, 3);
        sep.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.95f, 1f, 0.6f);

        title = MakeText(canvasGo.transform, "Title", 30, 20, 940, 85, 52, PM_Util.Cyan);
        body = MakeText(canvasGo.transform, "Body", 30, 125, 940, 470, 34, Color.white);
        status = MakeText(canvasGo.transform, "Status", 30, 605, 940, 75, 34, PM_Util.Yellow);
        counter = MakeText(canvasGo.transform, "Counter", 30, 30, 200, 50, 26, new Color(0.5f, 0.8f, 0.9f));
        counter.alignment = TextAlignmentOptions.TopLeft;
    }

    static RectTransform MakeRect(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0, 1);
        r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    static void MakeFrame(Transform parent, float w, float h, float t, Color c)
    {
        MakeRect(parent, "FrameT", 0, 0, w, t).gameObject.AddComponent<Image>().color = c;
        MakeRect(parent, "FrameB", 0, h - t, w, t).gameObject.AddComponent<Image>().color = c;
        MakeRect(parent, "FrameL", 0, 0, t, h).gameObject.AddComponent<Image>().color = c;
        MakeRect(parent, "FrameR", w - t, 0, t, h).gameObject.AddComponent<Image>().color = c;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, float x, float y, float w, float h, float size, Color c)
    {
        var r = MakeRect(parent, name, x, y, w, h);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = Font();
        t.fontSize = size;
        t.color = c;
        t.alignment = TextAlignmentOptions.TopRight;
        t.raycastTarget = false;
        t.text = "";
        return t;
    }

    public void SetContent(string titleText, string bodyText, string counterText)
    {
        title.text = PM_Hebrew.Visual(titleText, 34);
        body.text = PM_Hebrew.Visual(bodyText, 50);
        counter.text = counterText ?? "";
        SetStatus("", Color.white);
    }

    public void SetStatus(string text, Color c)
    {
        status.text = PM_Hebrew.Visual(text, 50);
        status.color = c;
    }

    // Buttons are laid out right-to-left under the screen. Each entry: label + action.
    public void SetButtons(params KeyValuePair<string, Action>[] items)
    {
        foreach (GameObject b in buttons) Destroy(b);
        buttons.Clear();
        float w = 0.22f, gap = 0.04f;
        float total = items.Length * w + (items.Length - 1) * gap;
        for (int i = 0; i < items.Length; i++)
        {
            float x = total * 0.5f - w * 0.5f - i * (w + gap); // first item on the right
            buttons.Add(MakeButton(items[i].Key, items[i].Value, new Vector3(x, -Height * 0.5f - 0.07f, -0.03f), w, i == 0));
        }
    }

    GameObject MakeButton(string label, Action action, Vector3 localPos, float w, bool primary)
    {
        var root = new GameObject("Btn_" + label);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = localPos;
        var bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(w, 0.08f, 0.05f);

        Color c = primary ? PM_Util.Green : PM_Util.Cyan;
        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(vis.GetComponent<Collider>());
        vis.transform.SetParent(root.transform, false);
        vis.transform.localScale = new Vector3(w, 0.075f, 0.02f);
        var mat = PM_Util.NeonMaterial(c, 0.6f);
        vis.GetComponent<Renderer>().material = mat;

        var canvasGo = new GameObject("Label", typeof(RectTransform));
        canvasGo.transform.SetParent(root.transform, false);
        canvasGo.transform.localPosition = new Vector3(0, 0, -0.012f);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w * 1000, 75);
        rt.localScale = Vector3.one * 0.001f;
        var t = MakeText(canvasGo.transform, "Text", 0, 8, w * 1000, 65, 38, Color.white);
        t.alignment = TextAlignmentOptions.Center;
        t.text = PM_Hebrew.VisualLine(label);

        var click = PM_Clickable.Add(root, action);
        click.onHover = h => mat.SetColor("_EmissionColor", c * (h ? 2.2f : 0.6f));
        return root;
    }

    // Places the screen at a world position, facing the given viewer point.
    public void Place(Vector3 position, Vector3 viewer)
    {
        transform.position = position;
        Vector3 dir = position - viewer;
        dir.y = 0;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(8f, 0, 0);
    }
}
