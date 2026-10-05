using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Floating holographic screen with Hebrew text and pressable 3D buttons.
public class PM_Panel : MonoBehaviour
{
    static TMP_FontAsset font;

    TextMeshProUGUI title, body, status, counter;
    string rawBody;
    Color accent = new Color(0.1f, 0.95f, 1f);
    CanvasGroup group;
    readonly List<Image> accentImages = new List<Image>();
    Image headerImage, sepImage, bgImage;
    float appear = 1f;
    readonly List<GameObject> buttons = new List<GameObject>();

    public static TMP_FontAsset Font()
    {
        if (font != null) return font;
        Font f = Resources.Load<Font>("PM_Fonts/VarelaRound-Regular");
        if (f != null)
        {
            // Big atlas (2048) so all letters fit in ONE texture — with a small atlas some Hebrew letters were blank.
            font = TMP_FontAsset.CreateFontAsset(f, 64, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            // Put every letter into the atlas right away, otherwise Hebrew letters appear missing or overlapping.
            if (font != null)
            {
                var chars = new System.Text.StringBuilder();
                for (char c = 'א'; c <= 'ת'; c++) chars.Append(c);
                for (char c = ' '; c <= '~'; c++) chars.Append(c);
                chars.Append("•—–°×״׳");
                font.TryAddCharacters(chars.ToString());
                RemoveKerning(font);
            }
        }
        if (font == null)
        {
            Debug.LogWarning("[PM] Шрифт с ивритом не найден в Assets/Resources/PM_Fonts — иврит может не отображаться.");
            font = TMP_Settings.defaultFontAsset;
        }
        return font;
    }

    // Sets our Hebrew font on a text and switches off kerning/font features.
    // The font's kerning pairs are made for right-to-left order; our text is already reversed,
    // so the pairs hit the wrong letters — letters climbed on each other and random gaps appeared.
    public static void Prepare(TextMeshProUGUI t)
    {
        t.font = Font();
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
        var features = t.GetType().GetProperty("fontFeatures", flags);
        if (features != null)
        {
            var list = features.GetValue(t, null) as System.Collections.IList;
            if (list != null) list.Clear();
            else if (features.CanWrite) features.SetValue(t, Activator.CreateInstance(features.PropertyType), null);
        }
        else
        {
            var kerning = t.GetType().GetProperty("enableKerning", flags);
            if (kerning != null && kerning.CanWrite) kerning.SetValue(t, false, null);
        }
    }

    // Empties the kerning / mark positioning tables that the font asset read from the font file.
    static void RemoveKerning(TMP_FontAsset fa)
    {
        try
        {
            var all = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var tableProp = fa.GetType().GetProperty("fontFeatureTable", all);
            object table = tableProp != null ? tableProp.GetValue(fa, null) : null;
            if (table == null) return;
            foreach (var f in table.GetType().GetFields(all))
            {
                object v = f.GetValue(table);
                if (v is System.Collections.IList) ((System.Collections.IList)v).Clear();
                else if (v is System.Collections.IDictionary) ((System.Collections.IDictionary)v).Clear();
            }
        }
        catch (Exception e) { Debug.Log("[PM] Kerning not removed: " + e.Message); }
    }

    // Layout of this screen (pixels; 1000 px = 1 meter).
    int wPx = 1000, hPx = 700, bodyChars = 50, titleChars = 34;
    float titleSize = 52, bodySize = 34;
    bool hasStatus = true;

    public float HeightMeters { get { return hPx * 0.001f; } }

    // Main screen.
    public static PM_Panel Create(Transform parent)
    {
        return Create(parent, "PM_Panel", 1000, 700, 52, 34, 50, true);
    }

    // Generic screen (also used for the small info cards).
    public static PM_Panel Create(Transform parent, string name, int widthPx, int heightPx, float titleSize, float bodySize, int bodyChars, bool status)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        var p = root.AddComponent<PM_Panel>();
        p.wPx = widthPx; p.hPx = heightPx; p.titleSize = titleSize; p.bodySize = bodySize;
        p.bodyChars = bodyChars; p.titleChars = Mathf.Max(16, (int)(widthPx / (titleSize * 0.55f))); p.hasStatus = status;
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
        rt.sizeDelta = new Vector2(wPx, hPx);
        rt.localScale = Vector3.one * 0.001f;

        var bg = MakeRect(canvasGo.transform, "Background", 0, 0, wPx, hPx);
        group = canvasGo.AddComponent<CanvasGroup>();
        bgImage = bg.gameObject.AddComponent<Image>();
        bgImage.color = new Color(0.0f, 0.05f, 0.09f, 0.62f);
        var header = MakeRect(canvasGo.transform, "Header", 0, 0, wPx, titleSize * 1.7f + 25);
        headerImage = header.gameObject.AddComponent<Image>();
        headerImage.color = new Color(0.1f, 0.95f, 1f, 0.10f);
        MakeFrame(canvasGo.transform, wPx, hPx, 2, new Color(0.1f, 0.95f, 1f, 0.35f));
        MakeCorners(canvasGo.transform, wPx, hPx, 70, 6, PM_Util.Cyan);
        foreach (Image im in canvasGo.GetComponentsInChildren<Image>(true))
            if (im.name.StartsWith("C") || im.name.StartsWith("Frame")) accentImages.Add(im);
        float titleH = titleSize * 1.7f;
        float sepY = 20 + titleH + 3;
        var sep = MakeRect(canvasGo.transform, "Line", 30, sepY, wPx - 60, 3);
        sepImage = sep.gameObject.AddComponent<Image>();
        sepImage.color = new Color(0.1f, 0.95f, 1f, 0.6f);

        float statusH = hasStatus ? 80 : 0;
        float bodyY = sepY + 17;
        title = MakeText(canvasGo.transform, "Title", 30, 20, wPx - 60, titleH, titleSize, PM_Util.Cyan);
        body = MakeText(canvasGo.transform, "Body", 30, bodyY, wPx - 60, hPx - bodyY - statusH - 15, bodySize, Color.white);
        status = MakeText(canvasGo.transform, "Status", 30, hPx - 95, wPx - 60, 75, 34, PM_Util.Yellow);
        status.gameObject.SetActive(hasStatus);
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

    static void MakeCorners(Transform parent, float w, float h, float len, float t, Color c)
    {
        MakeRect(parent, "C1a", 0, 0, len, t).gameObject.AddComponent<Image>().color = c;
        MakeRect(parent, "C1b", 0, 0, t, len).gameObject.AddComponent<Image>().color = c;
        MakeRect(parent, "C2a", w - len, 0, len, t).gameObject.AddComponent<Image>().color = c;
        MakeRect(parent, "C2b", w - t, 0, t, len).gameObject.AddComponent<Image>().color = c;
        MakeRect(parent, "C3a", 0, h - t, len, t).gameObject.AddComponent<Image>().color = c;
        MakeRect(parent, "C3b", 0, h - len, t, len).gameObject.AddComponent<Image>().color = c;
        MakeRect(parent, "C4a", w - len, h - t, len, t).gameObject.AddComponent<Image>().color = c;
        MakeRect(parent, "C4b", w - t, h - len, t, len).gameObject.AddComponent<Image>().color = c;
    }

    // Color theme of the screen (frame, corners, header, title). Used to match fiber groups.
    public void SetAccent(Color c)
    {
        foreach (Image im in accentImages)
        {
            float a = im.name.StartsWith("Frame") ? 0.35f : 1f;
            im.color = new Color(c.r, c.g, c.b, a);
        }
        if (headerImage != null) headerImage.color = new Color(c.r, c.g, c.b, 0.12f);
        if (sepImage != null) sepImage.color = new Color(c.r, c.g, c.b, 0.6f);
        if (title != null) title.color = Color.Lerp(c, Color.white, 0.25f);
        if (bgImage != null) bgImage.color = new Color(c.r * 0.15f, c.g * 0.15f, c.b * 0.15f, 0.7f);
        if (body != null) body.color = Color.Lerp(Color.white, c, 0.15f);
        accent = c;
        RenderBody();
        if (headerImage != null) headerImage.color = new Color(c.r, c.g, c.b, 0.25f);
    }

    void Update()
    {
        if (appear >= 1f) return;
        appear = Mathf.Min(1f, appear + Time.deltaTime * 3.5f);
        float e = Mathf.SmoothStep(0f, 1f, appear);
        if (group != null) group.alpha = e;
        transform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, e);
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, float x, float y, float w, float h, float size, Color c)
    {
        var r = MakeRect(parent, name, x, y, w, h);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        Prepare(t);
        t.fontSize = size;
        t.color = c;
        t.alignment = TextAlignmentOptions.Top;   // centered lines
        t.raycastTarget = false;
        t.text = "";
        return t;
    }

    public void SetContent(string titleText, string bodyText, string counterText)
    {
        title.text = PM_Hebrew.Visual(titleText, titleChars);
        rawBody = bodyText;
        RenderBody();
        counter.text = counterText ?? "";
        appear = 0f;
        SetStatus("", Color.white);
    }

    // Body with highlighting: labels in the screen color, families / temperatures / safety words in their colors.
    void RenderBody()
    {
        if (body == null || rawBody == null) return;
        body.text = PM_Hebrew.Visual(rawBody, bodyChars, PM_Util.Hex(Color.Lerp(accent, Color.white, 0.35f)));
    }

    public void SetStatus(string text, Color c)
    {
        status.text = PM_Hebrew.Visual(text, 50);
        status.color = c;
    }

    // Buttons are laid out right-to-left under the screen. Each entry: label + action.
    public void SetButtons(params KeyValuePair<string, Action>[] items)
    {
        SetButtons(0, items);
    }

    // primary = index of the green (main) button.
    public void SetButtons(int primary, params KeyValuePair<string, Action>[] items)
    {
        SetButtonGrid(4, primary, 0.22f, items);
    }

    // Buttons in rows under the screen (perRow per row, right to left, rows going down).
    public void SetButtonGrid(int perRow, int primary, float w, params KeyValuePair<string, Action>[] items)
    {
        foreach (GameObject b in buttons) Destroy(b);
        buttons.Clear();
        float gap = 0.04f;
        for (int i = 0; i < items.Length; i++)
        {
            int row = i / perRow, col = i % perRow;
            int inRow = Mathf.Min(perRow, items.Length - row * perRow);
            float total = inRow * w + (inRow - 1) * gap;
            float x = total * 0.5f - w * 0.5f - col * (w + gap); // first item on the right
            float y = -HeightMeters * 0.5f - 0.07f - row * 0.11f;
            buttons.Add(MakeButton(items[i].Key, items[i].Value, new Vector3(x, y, -0.03f), w, i == primary));
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
        var mat = PM_Util.NeonMaterial(c, 0.45f);
        mat.SetColor("_BaseColor", new Color(0.02f, 0.03f, 0.05f));
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
        t.enableAutoSizing = true;   // long labels shrink instead of overflowing the button
        t.fontSizeMin = 22;
        t.fontSizeMax = 38;
        t.text = PM_Hebrew.VisualLine(label);

        var click = PM_Clickable.Add(root, action);
        click.onHover = h => mat.SetColor("_EmissionColor", c * (h ? 2.2f : 0.45f));
        var edge = new[] { new Vector3(-w / 2, -0.0375f, -0.011f), new Vector3(w / 2, -0.0375f, -0.011f), new Vector3(w / 2, 0.0375f, -0.011f), new Vector3(-w / 2, 0.0375f, -0.011f) };
        PM_Util.Line(root.transform, "Edge", edge, c, 0.003f, true);
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
