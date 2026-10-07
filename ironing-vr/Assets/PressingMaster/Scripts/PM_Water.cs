using UnityEngine;

// Water tank of the station with a sight glass (MIN / MAX) and two buttons: fill and drain.
// Too little water: no steam. Above MAX: the iron spits water. Tap water leaves limescale (brown stains).
public class PM_WaterTank : MonoBehaviour
{
    public const float Min = 0.25f, Max = 0.85f;
    public float Level = 0.12f;
    public float Limescale = 0.35f;      // how much scale is already in the boiler (brown flecks when spitting)
    public bool Overfilled { get { return Level > Max; } }
    public bool Low { get { return Level < Min; } }
    public bool Empty { get { return Level < 0.04f; } }

    PM_Panel panel;
    Transform column;
    Material waterMat;
    const float TubeH = 0.34f;

    public static PM_WaterTank Create(Vector3 pos, Vector3 viewer)
    {
        var go = new GameObject("PM_WaterTank");
        var t = go.AddComponent<PM_WaterTank>();
        t.Build(pos, viewer);
        return t;
    }

    void Build(Vector3 pos, Vector3 viewer)
    {
        panel = PM_Panel.Create(transform, "PM_TankPanel", 300, 520, 38, 24, 16, false);
        panel.Place(pos, viewer);
        panel.SetContent(PM_Content.TankTitle, "", "");
        panel.SetAccent(PM_Util.Cyan);
        panel.SetButtonGrid(2, -1, 0.12f, -panel.HeightMeters * 0.5f - 0.06f, false,
            new System.Collections.Generic.KeyValuePair<string, System.Action>(PM_Content.BtnFill, Fill),
            new System.Collections.Generic.KeyValuePair<string, System.Action>(PM_Content.BtnDrain, Drain));

        Transform p = panel.transform;
        // Glass tube
        var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(tube.GetComponent<Collider>());
        tube.transform.SetParent(p, false);
        tube.transform.localPosition = new Vector3(0, -0.04f, -0.03f);
        tube.transform.localScale = new Vector3(0.07f, TubeH * 0.5f, 0.07f);
        tube.GetComponent<Renderer>().material = PM_Util.TransparentMaterial(new Color(0.7f, 0.9f, 1f, 0.18f));
        // Water column (scaled by the level)
        var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(col.GetComponent<Collider>());
        col.transform.SetParent(p, false);
        waterMat = PM_Util.NeonMaterial(new Color(0.2f, 0.7f, 1f), 0.8f);
        col.GetComponent<Renderer>().material = waterMat;
        column = col.transform;
        // MIN / MAX marks
        foreach (float m in new[] { Min, Max })
        {
            float y = -0.04f - TubeH * 0.5f + TubeH * m;
            PM_Util.Line(p, "Mark", new[] { new Vector3(-0.06f, y, -0.04f), new Vector3(0.06f, y, -0.04f) }, m == Max ? PM_Util.Red : PM_Util.Yellow, 0.004f, false);
            Label(p, m == Max ? "MAX" : "MIN", new Vector3(0.1f, y, -0.04f), m == Max ? PM_Util.Red : PM_Util.Yellow);
        }
        Apply();
    }

    static void Label(Transform parent, string text, Vector3 pos, Color c)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120, 40);
        rt.localScale = Vector3.one * 0.001f;
        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
        t.transform.SetParent(go.transform, false);
        t.rectTransform.sizeDelta = new Vector2(120, 40);
        PM_Panel.Prepare(t);
        t.fontSize = 26;
        t.color = c;
        t.alignment = TMPro.TextAlignmentOptions.Center;
        t.text = text;
    }

    void Fill() { Level = Mathf.Min(1f, Level + 0.08f); Apply(); if (PM_Audio.I != null) PM_Audio.I.Play("steam_burst", 0.25f); }
    void Drain() { Level = Mathf.Max(0f, Level - 0.08f); Apply(); }

    public void Use(float amount) { Level = Mathf.Max(0f, Level - amount); Apply(); }

    void Apply()
    {
        if (column == null) return;
        float h = Mathf.Max(0.002f, TubeH * Level);
        column.localScale = new Vector3(0.055f, h * 0.5f, 0.055f);
        column.localPosition = new Vector3(0, -0.04f - TubeH * 0.5f + h * 0.5f, -0.03f);
    }

    void Update()
    {
        Color c = Overfilled ? PM_Util.Red : (Low ? PM_Util.Yellow : new Color(0.2f, 0.7f, 1f));
        float k = Overfilled || Low ? 0.6f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 4f)) : 0.8f;
        waterMat.SetColor("_EmissionColor", c * k);
    }
}
