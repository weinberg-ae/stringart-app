using System;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Prepares the purchased "Ironing steam table" model at runtime:
// places it in front of the player, builds the iron, makes power switch / buttons / gauge interactive,
// adds lights and neon training tools.
public class PM_Station : MonoBehaviour
{
    // Part names inside the FBX model (see the report in the chat).
    const string TableName = "Ironing steam table";
    const string FloorPlane = "Box001";
    const string BoardPart = "3dSolid 38";
    const string SleevePart = "3dSolid 37";
    const string RestPart = "3dSolid 50";
    const string GaugeFace = "3dSolid 46";
    const string GaugeRing = "3dSolid 44";
    const string GaugeNeedle = "3dSolid 45";
    static readonly string[] IronParts = { "SubDMesh", "SubDMesh 1", "SubDMesh 2", "SubDMesh 3",
        "3dSolid 22", "3dSolid 23", "3dSolid 24", "3dSolid 25", "3dSolid 26", "3dSolid 27", "3dSolid 28", "3dSolid 29", "3dSolid 30", "3dSolid 31" };
    static readonly string[] PowerParts = { "3dSolid 41", "3dSolid 42", "3dSolid 43" };
    static readonly string[] BoomParts = { "3dSolid 6", "3dSolid 7", "3dSolid 8", "3dSolid001", "3dSolid 5", "3dSolid 3" };
    static readonly string[] TempButtonParts = { "", "3dSolid 49", "3dSolid 48", "3dSolid 47" }; // index = mode (1..3)

    public const float WorkPressure = 4.0f;
    public const float ReadyPressure = 3.5f;

    public Transform Table { get; private set; }
    public PM_Iron Iron { get; private set; }
    public bool Powered { get; private set; }
    public float Pressure { get; private set; }
    public int Mode { get; private set; }
    public bool PressureOk { get { return Powered && Pressure >= ReadyPressure; } }

    public Action onPowerClicked;
    public Action<int> onModeClicked;

    // Geometry of the work place (world space, after placement).
    public Vector3 PlayerPos { get; private set; }
    public Vector3 Forward { get; private set; }      // from player to table
    public Vector3 Right { get; private set; }
    public Vector3 BoardCenter { get; private set; }
    public float BoardTopY { get; private set; }
    public Quaternion BoardRotation { get; private set; }
    public Vector3 PanelPos { get; private set; }

    readonly Dictionary<PM_Target, List<Transform>> targets = new Dictionary<PM_Target, List<Transform>>();
    readonly Material[] buttonMats = new Material[4];
    readonly Color[] buttonBaseEmission = new Color[4];
    Transform needle, powerKnob;
    Vector3 gaugeCenter, knobCenter;
    float needleAngle;
    Transform tools;

    public List<Transform> Targets(PM_Target t)
    {
        List<Transform> l;
        return targets.TryGetValue(t, out l) ? l : new List<Transform>();
    }

    public bool Setup()
    {
        Table = PM_Util.FindInScene(TableName);
        if (Table == null)
        {
            Debug.LogError("[PM] Не найден объект '" + TableName + "' в сцене. Проверьте имя стола в Hierarchy.");
            return false;
        }
        CleanModel();
        PlaceTable();
        BuildIron();
        BuildControls();
        BuildLights();
        BuildTools();
        Debug.Log("[PM] Станция готова.");
        return true;
    }

    void CleanModel()
    {
        Transform floor = PM_Util.FindDeep(Table, FloorPlane);
        if (floor != null) floor.gameObject.SetActive(false);
        foreach (Camera c in Table.GetComponentsInChildren<Camera>(true)) c.gameObject.SetActive(false);
        foreach (Light l in Table.GetComponentsInChildren<Light>(true)) l.gameObject.SetActive(false);

        // Old manual setup from earlier attempts: remove grab components so they don't conflict.
        Transform old = PM_Util.FindInScene("Real_vr_iron");
        if (old == null) old = PM_Util.FindInScene("REAL_VR_IRON");
        if (old != null)
        {
            var g = old.GetComponent<XRGrabInteractable>();
            if (g != null) DestroyImmediate(g);
            var rb = old.GetComponent<Rigidbody>();
            if (rb != null) DestroyImmediate(rb);
            foreach (Collider c in old.GetComponents<Collider>()) DestroyImmediate(c);
        }
    }

    Bounds ActiveBounds(Transform root)
    {
        bool has = false;
        Bounds b = new Bounds();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    void PlaceTable()
    {
        XROrigin origin = FindAnyObjectByType<XROrigin>();
        Transform o = origin != null ? origin.transform : null;
        PlayerPos = o != null ? o.position : Vector3.zero;
        Vector3 f = o != null ? o.forward : Vector3.forward;
        f.y = 0;
        if (f.sqrMagnitude < 0.001f) f = Vector3.forward;
        f.Normalize();
        Forward = f;
        Right = Vector3.Cross(Vector3.up, f).normalized;

        Transform board = PM_Util.FindDeep(Table, BoardPart);
        Transform rest = PM_Util.FindDeep(Table, RestPart);
        if (rest != null) targets[PM_Target.Rest] = new List<Transform> { rest };
        Transform btn = PM_Util.FindDeep(Table, TempButtonParts[2]);
        if (board == null || btn == null)
        {
            Debug.LogWarning("[PM] Не найдены доска/кнопки — стол не передвинут автоматически.");
            return;
        }

        // 1) Turn the table so that its control panel faces the player.
        Bounds bb = PM_Util.WorldBounds(board);
        Vector3 longAxis = bb.size.x >= bb.size.z ? Vector3.right : Vector3.forward;
        Vector3 perp = Vector3.Cross(Vector3.up, longAxis);
        Vector3 toBtn = PM_Util.WorldBounds(btn).center - bb.center;
        Vector3 front = Vector3.Dot(toBtn, perp) >= 0 ? perp : -perp;
        float angle = Vector3.SignedAngle(front, -f, Vector3.up);
        Table.RotateAround(bb.center, Vector3.up, angle);

        // 2) Move it: board front edge 0.42 m in front of the player, work zone centered, feet on the floor.
        bb = PM_Util.WorldBounds(board);
        Vector3 anchor = bb.center;
        if (rest != null) anchor = Vector3.Lerp(bb.center, PM_Util.WorldBounds(rest).center, 0.35f);
        float depth = Mathf.Abs(f.x) * bb.size.x + Mathf.Abs(f.z) * bb.size.z;
        float lateral = Vector3.Dot(anchor - PlayerPos, Right);
        float frontEdge = Vector3.Dot(bb.center - PlayerPos, f) - depth * 0.5f;
        Vector3 delta = -lateral * Right + (0.42f - frontEdge) * f;
        Bounds all = ActiveBounds(Table);
        delta.y = PlayerPos.y - all.min.y;
        Table.position += delta;

        bb = PM_Util.WorldBounds(board);
        BoardCenter = bb.center;
        BoardTopY = bb.max.y;
        BoardRotation = Quaternion.LookRotation(f, Vector3.up);
        PanelPos = new Vector3(bb.center.x, PlayerPos.y + 1.5f, bb.center.z) + f * (depth * 0.5f + 0.35f);
        Debug.Log("[PM] Стол поставлен перед игроком. Высота доски: " + (BoardTopY - PlayerPos.y).ToString("F2") + " м");
    }

    void BuildIron()
    {
        List<Transform> parts = PM_Util.FindAll(Table, IronParts);
        if (parts.Count == 0) { Debug.LogError("[PM] Детали утюга не найдены."); return; }
        Iron = PM_Iron.Assemble(parts.ToArray(), Table);
        targets[PM_Target.Iron] = new List<Transform> { Iron.transform };
    }

    void BuildControls()
    {
        // Power switch.
        List<Transform> power = PM_Util.FindAll(Table, PowerParts);
        if (power.Count > 0)
        {
            Bounds b = PM_Util.WorldBounds(power);
            knobCenter = b.center;
            var pivot = new GameObject("PM_PowerKnob").transform;
            pivot.position = knobCenter;
            pivot.SetParent(Table, true);
            foreach (Transform p in power) p.SetParent(pivot, true);
            powerKnob = pivot;
            var hit = PM_Util.HitBox("PM_PowerHit", b, 1.8f, 0.07f, Table);
            PM_Clickable.Add(hit, () => { if (onPowerClicked != null) onPowerClicked(); });
            targets[PM_Target.Power] = power;
        }

        // Pressure gauge.
        Transform face = PM_Util.FindDeep(Table, GaugeFace);
        needle = PM_Util.FindDeep(Table, GaugeNeedle);
        if (face != null)
        {
            gaugeCenter = PM_Util.WorldBounds(face).center;
            var list = new List<Transform> { face };
            Transform ring = PM_Util.FindDeep(Table, GaugeRing);
            if (ring != null) list.Add(ring);
            targets[PM_Target.Gauge] = list;
            // Gauge label.
            Label(gaugeCenter + Vector3.up * 0.06f - Forward * 0.01f, "bar", 30, PM_Util.Cyan);
        }
        needleAngle = 0f;          // in the model the needle points up = 3 bar
        SetNeedle(0f);

        // Temperature buttons.
        var allButtons = new List<Transform>();
        for (int m = 1; m <= 3; m++)
        {
            Transform b = PM_Util.FindDeep(Table, TempButtonParts[m]);
            if (b == null) { Debug.LogWarning("[PM] Не найдена кнопка " + TempButtonParts[m]); continue; }
            allButtons.Add(b);
            Renderer r = b.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                buttonMats[m] = r.material;
                buttonBaseEmission[m] = buttonMats[m].HasProperty("_EmissionColor") ? buttonMats[m].GetColor("_EmissionColor") : Color.black;
            }
            Bounds bb = PM_Util.WorldBounds(b);
            int mode = m;
            var hit = PM_Util.HitBox("PM_TempHit_" + m, bb, 1.6f, 0.045f, Table);
            PM_Clickable.Add(hit, () => { if (onModeClicked != null) onModeClicked(mode); });
            Label(bb.center + Vector3.up * 0.045f - Forward * 0.01f, PM_Content.ModeDots[m] + "\n" + PM_Content.ModeTemp[m], 20, PM_Util.ModeColor(m));
        }
        targets[PM_Target.TempButtons] = allButtons;
        RefreshButtons();

        // Other learning targets.
        targets[PM_Target.Boom] = PM_Util.FindAll(Table, BoomParts);
        Transform sleeve = PM_Util.FindDeep(Table, SleevePart);
        if (sleeve != null) targets[PM_Target.SleeveBoard] = new List<Transform> { sleeve };
        Transform board = PM_Util.FindDeep(Table, BoardPart);
        if (board != null)
        {
            targets[PM_Target.Board] = new List<Transform> { board };
            // Collider so the iron and fabric "see" the board.
            if (board.GetComponent<Collider>() == null) board.gameObject.AddComponent<MeshCollider>();
        }
    }

    // Small floating label (no Hebrew) facing the player.
    void Label(Vector3 pos, string text, float size, Color c)
    {
        var go = new GameObject("PM_Label", typeof(RectTransform));
        go.transform.SetParent(Table, true);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(Forward, Vector3.up);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(70, 60);
        rt.localScale = Vector3.one * 0.0007f;
        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(go.transform, false);
        t.rectTransform.sizeDelta = new Vector2(70, 60);
        t.font = PM_Panel.Font();
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = c;
        t.text = text;
        t.raycastTarget = false;
    }

    void BuildLights()
    {
        var key = new GameObject("PM_KeyLight").AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.1f;
        key.color = new Color(1f, 0.97f, 0.92f);
        key.shadows = LightShadows.Soft;
        key.transform.rotation = Quaternion.LookRotation(Forward + Vector3.down * 1.4f + Right * 0.4f);

        var rim = new GameObject("PM_RimLight").AddComponent<Light>();
        rim.type = LightType.Point;
        rim.color = PM_Util.Cyan;
        rim.intensity = 1.5f;
        rim.range = 3f;
        rim.transform.position = BoardCenter + Forward * 0.8f + Vector3.up * 0.6f;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.24f);

        // Invisible floor: the XR rig has gravity and would fall into the void without it.
        if (PM_Util.FindInScene("Floor_Collider") == null)
        {
            var floor = new GameObject("PM_InvisibleFloor");
            floor.transform.position = PlayerPos - Vector3.up * 0.05f;
            floor.AddComponent<BoxCollider>().size = new Vector3(60f, 0.1f, 60f);
        }

        // Neon ring on the floor where the player stands.
        var ring = new GameObject("PM_FloorRing");
        ring.transform.position = PlayerPos + Vector3.up * 0.01f;
        PM_Util.Line(ring.transform, "Ring", PM_Util.Circle(0.35f, 64, Vector3.right, Vector3.forward, Vector3.zero), PM_Util.Cyan, 0.012f, true);
        PM_Util.Line(ring.transform, "Ring2", PM_Util.Circle(0.42f, 64, Vector3.right, Vector3.forward, Vector3.zero), new Color(0.1f, 0.95f, 1f, 0.35f), 0.006f, true);
    }

    // ---------- Neon training tools (no 3D models needed) ----------
    void BuildTools()
    {
        tools = new GameObject("PM_Tools").transform;
        tools.position = new Vector3(BoardCenter.x, BoardTopY, BoardCenter.z);
        tools.rotation = BoardRotation;

        // Tailor's ham: egg-shaped wireframe.
        var ham = new GameObject("PM_Ham").transform;
        ham.SetParent(tools, false);
        ham.localPosition = new Vector3(-0.5f, 0.08f, 0);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 6f;
            var pts = new Vector3[40];
            for (int k = 0; k < 40; k++)
            {
                float t = k * Mathf.PI * 2f / 40f;
                float x = Mathf.Cos(t) * 0.15f;
                float r = Mathf.Sin(t) * (x > 0 ? 0.09f - x * 0.15f : 0.09f);
                pts[k] = new Vector3(x, r * Mathf.Cos(a) * 0.8f, r * Mathf.Sin(a));
            }
            PM_Util.Line(ham, "Long" + i, pts, PM_Util.Cyan, 0.004f, true);
        }
        for (int j = 1; j < 6; j++)
        {
            float x = -0.15f + j * 0.05f;
            float t = Mathf.Acos(Mathf.Clamp(x / 0.15f, -1f, 1f));
            float r = Mathf.Sin(t) * (x > 0 ? 0.09f - x * 0.15f : 0.09f);
            var pts = new Vector3[32];
            for (int k = 0; k < 32; k++)
            {
                float a = k * Mathf.PI * 2f / 32f;
                pts[k] = new Vector3(x, Mathf.Cos(a) * r * 0.8f, Mathf.Sin(a) * r);
            }
            PM_Util.Line(ham, "Ring" + j, pts, PM_Util.Cyan, 0.004f, true);
        }
        targets[PM_Target.Ham] = new List<Transform> { ham };

        // Point presser / clapper: wooden base + pointed top board on a post.
        var pp = new GameObject("PM_PointPresser").transform;
        pp.SetParent(tools, false);
        pp.localPosition = new Vector3(-0.17f, 0, 0);
        WireBox(pp, new Vector3(0, 0.02f, 0), new Vector3(0.30f, 0.04f, 0.08f), PM_Util.Yellow);
        WireBox(pp, new Vector3(0.05f, 0.08f, 0), new Vector3(0.03f, 0.08f, 0.03f), PM_Util.Yellow);
        Vector3[] top =
        {
            new Vector3(-0.15f, 0.12f, -0.03f), new Vector3(0.09f, 0.12f, -0.03f), new Vector3(0.15f, 0.12f, 0f),
            new Vector3(0.09f, 0.12f, 0.03f), new Vector3(-0.15f, 0.12f, 0.03f)
        };
        PM_Util.Line(pp, "Top", top, PM_Util.Yellow, 0.004f, true);
        for (int i = 0; i < top.Length; i++) top[i].y = 0.135f;
        PM_Util.Line(pp, "Top2", top, PM_Util.Yellow, 0.004f, true);
        targets[PM_Target.PointPresser] = new List<Transform> { pp };

        // Pressing cloth: translucent sheet.
        var cloth = new GameObject("PM_Cloth").transform;
        cloth.SetParent(tools, false);
        cloth.localPosition = new Vector3(0.17f, 0, 0);
        Sheet(cloth, "Cloth", new Vector3(0, 0.006f, 0), new Vector2(0.27f, 0.22f), new Color(1f, 1f, 1f, 0.35f), Color.white);
        targets[PM_Target.Cloth] = new List<Transform> { cloth };

        // Fusible: baking paper above and below the fusible interfacing (exploded view).
        var fus = new GameObject("PM_Fusible").transform;
        fus.SetParent(tools, false);
        fus.localPosition = new Vector3(0.5f, 0, 0);
        Sheet(fus, "PaperBottom", new Vector3(0, 0.01f, 0), new Vector2(0.26f, 0.2f), new Color(0.9f, 0.85f, 0.7f, 0.35f), PM_Util.Cyan);
        Sheet(fus, "Interfacing", new Vector3(0, 0.05f, 0), new Vector2(0.2f, 0.14f), new Color(1f, 0.85f, 0.1f, 0.5f), PM_Util.Yellow);
        Sheet(fus, "PaperTop", new Vector3(0, 0.09f, 0), new Vector2(0.26f, 0.2f), new Color(0.9f, 0.85f, 0.7f, 0.35f), PM_Util.Cyan);
        targets[PM_Target.Fusible] = new List<Transform> { fus };

        ShowTools(false);
    }

    void WireBox(Transform parent, Vector3 c, Vector3 s, Color col)
    {
        Vector3 h = s * 0.5f;
        Vector3[] bottom = { c + new Vector3(-h.x, -h.y, -h.z), c + new Vector3(h.x, -h.y, -h.z), c + new Vector3(h.x, -h.y, h.z), c + new Vector3(-h.x, -h.y, h.z) };
        Vector3[] topR = { c + new Vector3(-h.x, h.y, -h.z), c + new Vector3(h.x, h.y, -h.z), c + new Vector3(h.x, h.y, h.z), c + new Vector3(-h.x, h.y, h.z) };
        PM_Util.Line(parent, "B", bottom, col, 0.004f, true);
        PM_Util.Line(parent, "T", topR, col, 0.004f, true);
        for (int i = 0; i < 4; i++) PM_Util.Line(parent, "E" + i, new[] { bottom[i], topR[i] }, col, 0.004f, false);
    }

    void Sheet(Transform parent, string name, Vector3 pos, Vector2 size, Color fill, Color edge)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.name = name;
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localRotation = Quaternion.Euler(90, 0, 0);
        q.transform.localScale = new Vector3(size.x, size.y, 1);
        q.GetComponent<Renderer>().material = PM_Util.TransparentMaterial(fill);
        Vector3[] e =
        {
            pos + new Vector3(-size.x / 2, 0.001f, -size.y / 2), pos + new Vector3(size.x / 2, 0.001f, -size.y / 2),
            pos + new Vector3(size.x / 2, 0.001f, size.y / 2), pos + new Vector3(-size.x / 2, 0.001f, size.y / 2)
        };
        PM_Util.Line(parent, name + "_Edge", e, edge, 0.003f, true);
    }

    public void Restyle()
    {
        var keep = new HashSet<string> { GaugeFace, GaugeRing, GaugeNeedle, "3dSolid 39", "3dSolid 40", "3dSolid 51", RestPart };
        for (int m = 1; m <= 3; m++) keep.Add(TempButtonParts[m]);
        Transform board = PM_Util.FindDeep(Table, BoardPart);
        if (board == null) return;
        PM_Look.RestyleStation(Table, keep, board, PM_Util.WorldBounds(board), Forward, Right);
    }

    public void ShowTools(bool on)
    {
        if (tools != null) tools.gameObject.SetActive(on);
    }

    // Where a point of light should float for the given parts.
    public Vector3 AnchorPoint(PM_Anchor anchor, List<Transform> parts)
    {
        if (anchor == PM_Anchor.BoardLeft)
            return new Vector3(BoardCenter.x, BoardTopY + 0.05f, BoardCenter.z) - Right * 0.58f - Forward * 0.12f;
        bool wasActive = tools != null && tools.gameObject.activeSelf;
        if (tools != null) tools.gameObject.SetActive(true);
        Bounds b = PM_Util.WorldBounds(parts);
        if (tools != null) tools.gameObject.SetActive(wasActive);
        float depth = Mathf.Abs(Forward.x) * b.size.x + Mathf.Abs(Forward.z) * b.size.z;
        switch (anchor)
        {
            case PM_Anchor.Front:
                return b.center - Forward * (depth * 0.5f + 0.05f);
            case PM_Anchor.FrontBelow:
                return b.center - Forward * (depth * 0.5f + 0.05f) - Vector3.up * 0.06f;
            case PM_Anchor.IronFront:
                return new Vector3(b.center.x, b.min.y + 0.03f, b.center.z) - Forward * (depth * 0.5f + 0.06f) + Right * 0.06f;
            default:
                return new Vector3(b.center.x, b.max.y + 0.06f, b.center.z);
        }
    }

    // ---------- Runtime state ----------
    public void SetPower(bool on)
    {
        if (Powered == on) return;
        Powered = on;
        if (powerKnob != null) powerKnob.RotateAround(knobCenter, -Forward, on ? 90f : -90f);
        if (PM_Audio.I != null)
        {
            if (on) PM_Audio.I.Play("power_on");
            PM_Audio.I.SetBoiler(on);
        }
        if (!on) { Pressure = 0f; SetMode(0); }
        RefreshButtons();
    }

    public void SetMode(int m)
    {
        Mode = m;
        RefreshButtons();
    }

    public void RefreshButtons()
    {
        for (int m = 1; m <= 3; m++)
        {
            Material mat = buttonMats[m];
            if (mat == null || !mat.HasProperty("_EmissionColor")) continue;
            mat.EnableKeyword("_EMISSION");
            Color c = PM_Util.ModeColor(m);
            if (!Powered) mat.SetColor("_EmissionColor", buttonBaseEmission[m]);
            else mat.SetColor("_EmissionColor", c * (Mode == m ? 3f : 0.25f));
        }
    }

    void SetNeedle(float bar)
    {
        if (needle == null) return;
        float target = -135f + 45f * Mathf.Clamp(bar, 0f, 6f);
        float d = target - needleAngle;
        if (Mathf.Abs(d) < 0.01f) return;
        needle.RotateAround(gaugeCenter, -Forward, d);
        needleAngle = target;
    }

    void Update()
    {
        if (Table == null) return;
        if (Powered) Pressure = Mathf.MoveTowards(Pressure, WorkPressure, Time.deltaTime * 0.45f);
        else Pressure = Mathf.MoveTowards(Pressure, 0f, Time.deltaTime * 1.5f);
        float wobble = Powered && Pressure >= WorkPressure - 0.01f ? Mathf.Sin(Time.time * 7f) * 0.04f : 0f;
        SetNeedle(Pressure + wobble);
        if (Iron != null) Iron.steamAllowed = PressureOk;
    }
}
