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
    public PM_WaterTank Tank { get; private set; }
    readonly TextMeshProUGUI[] tempLabels = new TextMeshProUGUI[4];
    readonly float[] labelFlash = new float[4];
    public float IronTemp { get; private set; } = 20f;   // °C of the soleplate (it heats up slowly)
    public float TargetTemp { get { return Powered && Mode > 0 ? (Mode == 1 ? 110f : Mode == 2 ? 150f : 200f) : 20f; } }
    public bool fastHeat;   // practice: the iron reaches a new temperature quickly
    // Temperature level the soleplate really has now (0 = still cold), not just the selected button.
    public int EffectiveMode { get { return IronTemp >= 185f ? 3 : IronTemp >= 135f ? 2 : IronTemp >= 95f ? 1 : 0; } }
    public bool IronReady { get { return Powered && Mode > 0 && Mathf.Abs(IronTemp - TargetTemp) < 8f; } }
    // Steam that comes out wet: the station is not ready yet, or the tank is overfilled.
    public bool WillSpit { get { return !PressureOk || (Mode > 0 && IronTemp < TargetTemp - 25f) || (Tank != null && Tank.Overfilled); } }

    public Action onPowerClicked;
    public Action<int> onModeClicked;

    // Geometry of the work place (world space, after placement).
    public Vector3 PlayerPos { get; private set; }
    public Vector3 Forward { get; private set; }      // from player to table
    public Vector3 Right { get; private set; }
    public Vector3 BoardCenter { get; private set; }
    public float BoardTopY { get; private set; }
    public Bounds BoardBounds { get; private set; }
    public Quaternion BoardRotation { get; private set; }
    public Vector3 PanelPos { get; private set; }

    readonly Dictionary<PM_Target, List<Transform>> targets = new Dictionary<PM_Target, List<Transform>>();
    readonly Material[] buttonMats = new Material[4];
    readonly Color[] buttonBaseEmission = new Color[4];
    readonly Material[] capMats = new Material[4];
    readonly Vector3[] buttonCenters = new Vector3[4];
    public Vector3 ButtonCenter(int m) { return buttonCenters[Mathf.Clamp(m, 0, 3)]; }
    Transform needle, powerKnob;
    Vector3 gaugeCenter, knobCenter;
    float needleAngle;
    Transform tools;
    Bounds shelf; bool hasShelf;
    public Transform SleevePivot { get; private set; }
    float sleeveAngle, sleeveTarget, sleeveSwing = 75f;

    public void SetTarget(PM_Target t, List<Transform> parts) { targets[t] = parts; }

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
        BuildSleeve();
        BuildTools();
        // Water tank (sight glass + fill / drain) at the right side of the station.
        // beside the mirror, clear of the points of light of the station
        Tank = PM_WaterTank.Create(PlayerPos + Right * 1.0f + Forward * 0.35f + Vector3.up * 1.45f, PlayerPos + Vector3.up * 1.6f);
        targets[PM_Target.Water] = new List<Transform> { Tank.transform.GetChild(0) };
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
        BoardBounds = bb;
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
        Iron.SetTipDirection(-Forward);   // at the rest the tip points to the player
        targets[PM_Target.Iron] = new List<Transform> { Iron.transform };
        BuildHose();
        AddSolids();
    }

    // The model's hose is a fixed mesh that stayed behind when the iron was lifted.
    // Find it (a tall part touching the top of the iron), hide it and connect a flexible hose instead.
    // Big closed parts of the station (the steam generator body under the iron rest etc.) get a box collider,
    // so the iron cannot be pushed into them from the side.
    void AddSolids()
    {
        int n = 0;
        foreach (MeshRenderer r in Table.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.name.StartsWith("PM_") || r.GetComponent<Collider>() != null) continue;
            if (Iron != null && r.transform.IsChildOf(Iron.transform)) continue;
            string nm = r.name;
            if (nm == BoardPart || nm == SleevePart || nm.StartsWith("Box")) continue;
            Bounds b = r.bounds;
            if (b.size.x < 0.12f || b.size.y < 0.12f || b.size.z < 0.12f || b.size.magnitude > 2.2f) continue;
            if (b.max.y > BoardTopY + 0.05f) continue;    // not the tall boom / tank frame
            // a trigger: the pointing ray goes through it (the points of light near it stay clickable),
            // only the iron is pushed out of it (PM_Iron.KeepOutOfSolids)
            var bc = r.gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            r.gameObject.AddComponent<PM_Solid>();
            n++;
        }
        // the red iron rest pad as well (thin, so the size rule above skips it)
        Transform rest = PM_Util.FindDeep(Table, RestPart);
        if (rest != null)
            foreach (MeshRenderer r in rest.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponent<PM_Solid>() != null) continue;
                var bc = r.gameObject.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                r.gameObject.AddComponent<PM_Solid>();
                n++;
            }
        Debug.Log("[PM] Твёрдые части станции (утюг не проходит сквозь них): " + n);
    }

    void BuildHose()
    {
        Bounds ib = PM_Util.MeshBounds(Iron.transform);   // the iron itself (the steam particles are not part of it)
        Bounds probe = ib;
        probe.Expand(new Vector3(0.04f, 0.06f, 0.04f));
        Renderer best = null;
        var others = new List<Renderer>();
        foreach (MeshRenderer r in Table.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.transform.IsChildOf(Iron.transform)) continue;
            string n = r.name;
            if (n == BoardPart || n == SleevePart || n == RestPart || n.StartsWith("Box") || n.StartsWith("PM_")) continue;
            Bounds b = r.bounds;
            if (b.size.magnitude > 1.0f || b.size.y < 0.15f) continue;     // not the table body, not small parts
            if (!b.Intersects(probe) || b.max.y < ib.max.y + 0.1f) continue; // must touch the iron and go up
            others.Add(r);
            if (best == null || b.max.y > best.bounds.max.y) best = r;
        }
        if (best == null) { Debug.Log("[PM] Шланг модели не найден — гибкий шланг идёт от стойки."); }
        Bounds hb = best != null ? best.bounds : new Bounds(ib.center + Vector3.up * 0.4f - Forward * 0.1f, Vector3.one * 0.1f);
        // Top end: the upper corner farthest from the iron. Bottom end: the point of the iron closest to the hose.
        Vector3 anchor = hb.center, ironCenter = ib.center;
        float far = -1f;
        for (int i = 0; i < 4; i++)
        {
            Vector3 c = new Vector3((i & 1) == 0 ? hb.min.x : hb.max.x, hb.max.y, (i & 2) == 0 ? hb.min.z : hb.max.z);
            float dd = new Vector2(c.x - ironCenter.x, c.z - ironCenter.z).sqrMagnitude;
            if (dd > far) { far = dd; anchor = c; }
        }
        Vector3 end = ib.ClosestPoint(new Vector3(hb.center.x, hb.min.y, hb.center.z));
        Vector3 hoseOut = (anchor - end).normalized;
        // The iron has a metal spring at the back of its handle where the hose goes in (its highest small part):
        // the hose is fixed to the top of that spring.
        Renderer coil = null;
        foreach (MeshRenderer r in Iron.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.name.StartsWith("PM_") || r.bounds.size.magnitude > 0.12f) continue;
            if (coil == null || r.bounds.max.y > coil.bounds.max.y) coil = r;
        }
        if (coil != null && coil.bounds.max.y > ib.max.y - 0.02f)
        {
            Bounds cb = coil.bounds;
            Vector3 back = cb.center - ib.center; back.y = 0f;
            back = back.sqrMagnitude > 1e-6f ? back.normalized : -Forward;
            float reach = Mathf.Abs(back.x) * cb.extents.x + Mathf.Abs(back.z) * cb.extents.z;
            end = new Vector3(cb.center.x, cb.max.y - 0.004f, cb.center.z) + back * reach * 0.7f;
            hoseOut = (back * reach + Vector3.up * cb.size.y).normalized;   // the spring points up and back
            Debug.Log("[PM] Шланг крепится к пружине утюга: " + coil.name);
        }
        // Hide every static piece of hose that touched the iron (it would stay behind when the iron is lifted).
        foreach (Renderer r in others) { r.enabled = false; Debug.Log("[PM] Шланг модели заменён гибким: " + r.name); }
        PM_Hose.Create(Table, anchor, Iron.transform, end, hoseOut);
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
            buttonCenters[m] = bb.center;
            int mode = m;
            // Neon cap in front of the button: lights up in the color of its fiber group.
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(cap.GetComponent<Collider>());
            cap.name = "PM_ButtonCap_" + m;
            float d = Mathf.Abs(Forward.x) * bb.size.x + Mathf.Abs(Forward.z) * bb.size.z;
            float size = Mathf.Max(bb.size.y, Mathf.Abs(Right.x) * bb.size.x + Mathf.Abs(Right.z) * bb.size.z) * 1.15f;
            cap.transform.position = bb.center - Forward * (d * 0.5f + 0.004f);
            cap.transform.rotation = Quaternion.FromToRotation(Vector3.up, -Forward);
            cap.transform.localScale = new Vector3(size, 0.003f, size);
            capMats[m] = PM_Util.NeonMaterial(PM_Util.ModeColor(m), 0f);
            cap.GetComponent<Renderer>().material = capMats[m];
            cap.transform.SetParent(Table, true);
            var hit = PM_Util.HitBox("PM_TempHit_" + m, bb, 1.6f, 0.045f, Table);
            PM_Clickable.Add(hit, () => { if (onModeClicked != null) onModeClicked(mode); });
            // Temperature label: a bit higher than the button's own ON/OFF print, on a dark plate, sharp outline.
            tempLabels[m] = Label(bb.center + Vector3.up * 0.075f - Forward * 0.02f, PM_Content.ModeDots[m] + "\n" + PM_Content.ModeTemp[m], 22, PM_Util.ModeColor(m), true);
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
    TextMeshProUGUI Label(Vector3 pos, string text, float size, Color c, bool plate = false)
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
        PM_Panel.Prepare(t);
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = c;
        t.text = text;
        t.raycastTarget = false;
        if (plate)
        {
            t.fontStyle = FontStyles.Bold;
            t.outlineWidth = 0.22f;
            t.outlineColor = new Color32(0, 0, 0, 255);
            var bg = new GameObject("Plate", typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
            bg.transform.SetParent(go.transform, false);
            bg.transform.SetAsFirstSibling();
            bg.rectTransform.sizeDelta = new Vector2(66, 58);
            bg.color = new Color(0.01f, 0.015f, 0.03f, 0.82f);
            bg.raycastTarget = false;
        }
        return t;
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
        // Lower shelf: a large flat horizontal part right under the board (not a foot or a leg bar):
        // it must lie under the board's footprint, be wide in both directions and not too low.
        float bestScore = 0f;
        Transform boardT = PM_Util.FindDeep(Table, BoardPart);
        Bounds boardB = boardT != null ? PM_Util.WorldBounds(boardT) : new Bounds(new Vector3(BoardCenter.x, BoardTopY, BoardCenter.z), new Vector3(1.2f, 0.05f, 0.4f));
        string shelfName = null;
        foreach (MeshRenderer r in Table.GetComponentsInChildren<MeshRenderer>(true))
        {
            Bounds b = r.bounds;
            if (b.size.y > 0.08f || r.name.StartsWith("PM_")) continue;
            float drop = BoardTopY - b.max.y;
            if (drop < 0.15f || drop > 0.85f) continue;
            float w = Mathf.Min(b.size.x, b.size.z), l = Mathf.Max(b.size.x, b.size.z);
            if (w < 0.18f || l < 0.35f) continue;                       // bars and feet are narrow
            float ox = Mathf.Max(0f, Mathf.Min(b.max.x, boardB.max.x + 0.05f) - Mathf.Max(b.min.x, boardB.min.x - 0.05f));
            float oz = Mathf.Max(0f, Mathf.Min(b.max.z, boardB.max.z + 0.05f) - Mathf.Max(b.min.z, boardB.min.z - 0.05f));
            float under = (ox * oz) / Mathf.Max(0.0001f, b.size.x * b.size.z);   // part of the shelf that is under the board
            if (under < 0.6f) continue;
            float score = b.size.x * b.size.z * under * (1.2f - drop);        // prefer large and high
            if (score > bestScore) { bestScore = score; shelf = b; hasShelf = true; shelfName = r.name; }
        }
        Debug.Log(hasShelf ? "[PM] Полка для инструментов: " + shelfName + " (" + shelf.size.x.ToString("F2") + " × " + shelf.size.z.ToString("F2") + " м)"
                           : "[PM] Полка под доской не найдена — инструменты будут на подставке под доской.");
        if (!hasShelf)
        {
            // No shelf in the model: a simple tray under the board.
            var tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tray.name = "PM_ToolTray";
            Destroy(tray.GetComponent<Collider>());
            tray.transform.position = new Vector3(boardB.center.x, BoardTopY - 0.42f, boardB.center.z);
            tray.transform.rotation = BoardRotation;
            tray.transform.localScale = new Vector3(Mathf.Min(0.9f, boardB.size.x * 0.75f), 0.02f, 0.3f);
            tray.GetComponent<Renderer>().sharedMaterial = PM_Util.TransparentMaterial(new Color(0.1f, 0.95f, 1f, 0.25f));
            shelf = tray.GetComponent<Renderer>().bounds; hasShelf = true;
        }
        tools = new GameObject("PM_Tools").transform;
        tools.position = new Vector3(BoardCenter.x, BoardTopY, BoardCenter.z);
        tools.rotation = BoardRotation;

        // Real pressing tools (meshes from tools/avatar/tools3d.py, as in the reference photos).
        // Two rows on the board; local X runs along the board.
        // Sizes as asked: the donkey twice the first model, collar board and chest pad +60%, shoulder presser 1.5x.
        // Front row: donkey and chest pad; back row: collar board and the clapper.
        Transform ham = Tool("PM_Donkey", "tool_donkey", new Color(0.98f, 0.80f, 0.85f), new Vector3(-0.30f, 0f, -0.06f), 0f);         // חמור (קבנצ'יק)
        ham.localScale = Vector3.one * 2f;
        targets[PM_Target.Ham] = new List<Transform> { ham };
        Transform pp = Tool("PM_PointPresser", "tool_collarboard", new Color(0.91f, 0.94f, 0.98f), new Vector3(0.36f, 0f, 0.19f), 0f); // צווארונים ופינות
        pp.localScale = Vector3.one * 1.6f;
        targets[PM_Target.PointPresser] = new List<Transform> { pp };
        // chest / cup pad: the model is real size (as in the reference photo), not scaled; pink as in the photo
        Transform chestPad = Tool("PM_ChestPad", "tool_chestpad", new Color(0.97f, 0.80f, 0.84f), new Vector3(0.40f, 0f, -0.06f), 0f);
        targets[PM_Target.ChestPad] = new List<Transform> { chestPad };
        Transform sh = Tool("PM_Shoulder", "tool_shoulder", new Color(0.42f, 0.62f, 0.93f), new Vector3(-0.50f, 0f, 0.15f), 0f);
        sh.localScale = Vector3.one * 1.5f;   // stands on the floor under the board
        targets[PM_Target.Shoulder] = new List<Transform> { sh };
        clapper = Tool("PM_Clapper", "tool_clapper", Color.white, new Vector3(-0.32f, 0f, 0.2f), 0f);
        clapper.localScale = Vector3.one * 1.3f;
        targets[PM_Target.Clapper] = new List<Transform> { clapper };
        clapper.gameObject.SetActive(PM_Rewards.Has(PM_Rewards.Clapper));

        // Pressing cloth: translucent sheet.
        var cloth = new GameObject("PM_Cloth").transform;
        cloth.SetParent(tools, false);
        cloth.localPosition = new Vector3(-0.04f, 0, 0.13f);
        Sheet(cloth, "Cloth", new Vector3(0, 0.006f, 0), new Vector2(0.26f, 0.17f), new Color(1f, 1f, 1f, 0.35f), Color.white);
        targets[PM_Target.Cloth] = new List<Transform> { cloth };

        // Fusible: baking paper above and below the fusible interfacing (exploded view).
        var fus = new GameObject("PM_Fusible").transform;
        fus.SetParent(tools, false);
        fus.localPosition = new Vector3(0.28f, 0, 0.13f);
        Sheet(fus, "PaperBottom", new Vector3(0, 0.01f, 0), new Vector2(0.22f, 0.17f), new Color(0.9f, 0.85f, 0.7f, 0.35f), PM_Util.Cyan);
        Sheet(fus, "Interfacing", new Vector3(0, 0.05f, 0), new Vector2(0.17f, 0.12f), new Color(1f, 0.85f, 0.1f, 0.5f), PM_Util.Yellow);
        Sheet(fus, "PaperTop", new Vector3(0, 0.09f, 0), new Vector2(0.22f, 0.17f), new Color(0.9f, 0.85f, 0.7f, 0.35f), PM_Util.Cyan);
        targets[PM_Target.Fusible] = new List<Transform> { fus };
        foreach (Transform t in new[] { ham, pp, chestPad, sh, clapper }) { toolHome[t] = t.localPosition; toolScale[t] = t.localScale; }

        ShowTools(false);
    }

    Transform clapper;
    static Material woodMat;
    public void ShowClapper(bool on) { if (clapper != null) clapper.gameObject.SetActive(on); }

    // A pressing tool: padded cloth (color) + beech wood.
    Transform Tool(string name, string mesh, Color cloth, Vector3 local, float yaw)
    {
        var t = new GameObject(name).transform;
        t.SetParent(tools, false);
        t.localPosition = local;
        t.localRotation = Quaternion.Euler(0, yaw, 0);
        Mesh m = PM_Body.LoadMesh(mesh);
        if (m == null) return t;
        if (woodMat == null)
        {
            woodMat = new Material(PM_Util.LitShader());
            Texture2D wt = Resources.Load<Texture2D>("PM_Avatar/tex_wood");
            if (wt != null) { wt.wrapMode = TextureWrapMode.Repeat; woodMat.SetTexture("_BaseMap", wt); woodMat.SetTexture("_MainTex", wt); }
            woodMat.SetFloat("_Smoothness", 0.35f);
        }
        var cm = new Material(PM_Util.LitShader());
        cm.SetColor("_BaseColor", cloth); cm.SetColor("_Color", cloth);
        cm.SetFloat("_Smoothness", 0.12f);
        Texture2D knit = Resources.Load<Texture2D>("PM_Avatar/tex_knit");
        if (knit != null) { cm.SetTexture("_BaseMap", knit); cm.SetTexture("_MainTex", knit); cm.SetTextureScale("_BaseMap", new Vector2(3f, 3f)); }
        var dark = new Material(PM_Util.LitShader());
        dark.SetColor("_BaseColor", new Color(0.25f, 0.25f, 0.27f)); dark.SetFloat("_Metallic", 0.8f); dark.SetFloat("_Smoothness", 0.7f);
        var vis = new GameObject("Mesh");
        vis.transform.SetParent(t, false);
        vis.AddComponent<MeshFilter>().sharedMesh = m;
        var r = vis.AddComponent<MeshRenderer>();
        var mats = new Material[m.subMeshCount];
        for (int i = 0; i < mats.Length; i++) mats[i] = i == 0 ? cm : i == 1 ? woodMat : dark;
        r.sharedMaterials = mats;
        // Solid: the iron rests on the tool and glides over its curved top instead of going through it.
        vis.AddComponent<MeshCollider>().sharedMesh = m;
        return t;
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

    // Tools in use stand on the board (and the sleeve board swings away to make room);
    // otherwise they wait on the lower shelf of the station. In a tool exercise only THAT tool is on the board.
    // The shoulder presser is tall: when not on the board it stands on the floor under the board.
    Transform toolOnBoard;
    public void ShowTools(bool on) { ShowTools(on, PM_Target.None); }
    public void ShowTools(bool on, PM_Target focus)
    {
        if (tools == null) return;
        // everything back into the tool set first
        foreach (PM_Target k in new[] { PM_Target.Ham, PM_Target.PointPresser, PM_Target.ChestPad, PM_Target.Shoulder, PM_Target.Clapper })
        {
            List<Transform> l;
            if (!targets.TryGetValue(k, out l) || l.Count == 0 || l[0] == null) continue;
            Transform t = l[0];
            Vector3 home;
            if (toolHome.TryGetValue(t, out home) && t.parent != tools)
            {
                t.SetParent(tools, false);
                t.localPosition = home; t.localRotation = Quaternion.identity; t.localScale = toolScale[t];
            }
        }
        if (toolOnBoard == null)
        {
            toolOnBoard = new GameObject("PM_ToolOnBoard").transform;
            toolOnBoard.rotation = BoardRotation;
        }
        toolOnBoard.position = new Vector3(BoardCenter.x, BoardTopY, BoardCenter.z);
        toolOnBoard.rotation = BoardRotation;
        if (on)
        {
            tools.gameObject.SetActive(true);
            tools.position = new Vector3(BoardCenter.x, BoardTopY, BoardCenter.z);
            tools.localScale = Vector3.one;
            SwingSleeve(true);
            ShoulderOnFloor();
            return;
        }
        if (hasShelf)
        {
            // Spread to fit inside the shelf, so nothing sticks into the station.
            tools.gameObject.SetActive(true);
            Vector3 along = BoardRotation * Vector3.right, across = BoardRotation * Vector3.forward;
            float len = Mathf.Abs(along.x) * shelf.size.x + Mathf.Abs(along.z) * shelf.size.z;
            float wid = Mathf.Abs(across.x) * shelf.size.x + Mathf.Abs(across.z) * shelf.size.z;
            float sc = Mathf.Clamp(Mathf.Min((len - 0.06f) / 1.36f, (wid - 0.03f) / 0.52f), 0.4f, 0.9f);
            tools.localScale = Vector3.one * sc;
            tools.position = new Vector3(shelf.center.x, shelf.max.y + 0.004f, shelf.center.z);
        }
        else tools.gameObject.SetActive(false);
        ShoulderOnFloor();
        // the tool of the exercise goes up onto the board
        if (focus != PM_Target.None && Targets(focus).Count > 0)
        {
            Transform t = Targets(focus)[0];
            t.SetParent(toolOnBoard, false);
            t.localPosition = new Vector3(0.12f, 0f, -0.02f);    // in the middle of the board, in front of the player
            t.localRotation = Quaternion.identity;
            t.localScale = toolScale.ContainsKey(t) ? toolScale[t] : Vector3.one;
            SwingSleeve(true);
        }
    }

    // The shoulder presser is tall: it always stands on the floor under the left part of the board.
    void ShoulderOnFloor()
    {
        Transform sh = Targets(PM_Target.Shoulder).Count > 0 ? Targets(PM_Target.Shoulder)[0] : null;
        if (sh == null || toolOnBoard == null) return;
        sh.SetParent(toolOnBoard, false);
        Bounds bb = BoardBounds.size.sqrMagnitude > 0f ? BoardBounds : new Bounds(BoardCenter, new Vector3(1.2f, 0.05f, 0.4f));
        Vector3 along = BoardRotation * Vector3.right;
        float halfLen = Mathf.Abs(along.x) * bb.extents.x + Mathf.Abs(along.z) * bb.extents.z;
        sh.position = new Vector3(BoardCenter.x, PlayerPos.y + 0.002f, BoardCenter.z) - along * (halfLen - 0.30f);
        sh.localRotation = Quaternion.identity;
        sh.localScale = toolScale.ContainsKey(sh) ? toolScale[sh] : Vector3.one;
    }

    readonly Dictionary<Transform, Vector3> toolHome = new Dictionary<Transform, Vector3>();
    readonly Dictionary<Transform, Vector3> toolScale = new Dictionary<Transform, Vector3>();

    public void SwingSleeve(bool away) { sleeveTarget = away ? sleeveSwing : 0f; }
    public float SleeveAngle { get { return sleeveAngle; } }
    public bool sleeveDragging;
    // Angle set by the hand: between "over the board" (0) and fully swung away (±90°).
    public void SetSleeveAngle(float a)
    {
        float lo = Mathf.Min(0f, sleeveSwing * 1.2f), hi = Mathf.Max(0f, sleeveSwing * 1.2f);
        sleeveTarget = Mathf.Clamp(a, lo, hi);
    }
    public void ToggleSleeve() { SwingSleeve(sleeveTarget == 0f); }

    // The sleeve board stands on a swing arm: find the board + its arm, and rotate them around the arm's end.
    void BuildSleeve()
    {
        Transform sleeve = PM_Util.FindDeep(Table, SleevePart);
        if (sleeve == null) return;
        Bounds sb = PM_Util.WorldBounds(sleeve);
        Bounds probe = sb; probe.Expand(0.05f);
        var group = new List<Transform> { sleeve };
        Bounds armB = new Bounds(); bool hasArm = false;
        foreach (MeshRenderer r in Table.GetComponentsInChildren<MeshRenderer>(true))
        {
            Transform t = r.transform;
            if (t == sleeve || t.IsChildOf(sleeve) || (Iron != null && t.IsChildOf(Iron.transform))) continue;
            string n = r.name;
            if (n == BoardPart || n == RestPart || n.StartsWith("Box") || n.StartsWith("PM_")) continue;
            Bounds b = r.bounds;
            if (b.size.magnitude > 1.2f || !b.Intersects(probe)) continue;
            group.Add(t);
            if (!hasArm) { armB = b; hasArm = true; } else armB.Encapsulate(b);
        }
        // Pivot: the end of the arm farthest from the sleeve board (where it is mounted).
        Vector3 pivot = sb.center;
        Bounds pb = hasArm ? armB : sb;
        float far = -1f;
        for (int i = 0; i < 4; i++)
        {
            Vector3 c = new Vector3((i & 1) == 0 ? pb.min.x : pb.max.x, pb.center.y, (i & 2) == 0 ? pb.min.z : pb.max.z);
            float d = new Vector2(c.x - sb.center.x, c.z - sb.center.z).sqrMagnitude;
            if (d > far) { far = d; pivot = Vector3.Lerp(c, pb.center, 0.1f); }
        }
        SleevePivot = new GameObject("SleeveSwing").transform;
        SleevePivot.SetParent(Table, true);
        SleevePivot.position = new Vector3(pivot.x, sb.center.y, pivot.z);
        SleevePivot.rotation = Table.rotation;
        foreach (Transform t in group) t.SetParent(SleevePivot, true);
        // Swing to the side that takes the sleeve board away from the main board.
        Vector3 toS = sb.center - SleevePivot.position; toS.y = 0;
        Vector3 toB = BoardCenter - SleevePivot.position; toB.y = 0;
        float plus = Vector3.Distance(Quaternion.Euler(0, 75f, 0) * toS, toB), minus = Vector3.Distance(Quaternion.Euler(0, -75f, 0) * toS, toB);
        sleeveSwing = plus > minus ? 75f : -75f;
        // Grab the sleeve board with the hand (Grip) and swing it around its arm, like a real one.
        var handle = new GameObject("PM_SleeveGrab");
        handle.transform.SetParent(SleevePivot, false);
        handle.transform.position = sb.center;
        handle.transform.rotation = Quaternion.identity;
        var box = handle.AddComponent<BoxCollider>();
        box.size = sb.size + new Vector3(0.02f, 0.03f, 0.02f);
        handle.AddComponent<PM_SleeveHandle>().station = this;
        Debug.Log("[PM] Шарнир шарвулона: " + group.Count + " деталей.");
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
            case PM_Anchor.FrontAbove:   // flat parts lying on the board: in front of the part, above the board surface
                return new Vector3(b.center.x, Mathf.Max(b.max.y, BoardTopY) + 0.07f, b.center.z) - Forward * (depth * 0.5f + 0.04f);
            case PM_Anchor.IronFront:
                return new Vector3(b.center.x, b.min.y + 0.03f, b.center.z) - Forward * (depth * 0.5f + 0.06f) + Right * 0.06f;
            default:
                return new Vector3(b.center.x, b.max.y + 0.06f, b.center.z);
        }
    }

    // ---------- Runtime state ----------
    // Practice / exam shortcuts: the station is already hot (no waiting).
    public void MakeReady()
    {
        if (!Powered) SetPower(true);
        Pressure = WorkPressure;
        if (Tank != null && (Tank.Low || Tank.Overfilled)) Tank.Level = 0.6f;
        IronTemp = TargetTemp;
        fastHeat = true;
    }

    public void SetPower(bool on)
    {
        if (Powered == on) return;
        Powered = on;
        if (powerKnob != null) powerKnob.RotateAround(knobCenter, -Forward, on ? 90f : -90f);
        if (PM_Audio.I != null)
        {
            if (on) PM_Audio.I.Play("power_on");
            if (on) PM_Look.PulseRing(new Vector3(BoardCenter.x, PlayerPos.y + 0.01f, BoardCenter.z), PM_Util.Cyan);
            PM_Audio.I.SetBoiler(on);
        }
        if (!on) { Pressure = 0f; SetMode(0); fastHeat = false; }
        RefreshButtons();
    }

    public void SetMode(int m)
    {
        if (m > 0 && m != Mode && tempLabels[m] != null)
        {
            labelFlash[m] = 1f;   // the chosen number pops up with a flash of light
            PM_Look.Burst(tempLabels[m].transform.position - Forward * 0.01f, PM_Util.ModeColor(m));
        }
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
        for (int m = 1; m <= 3; m++)
        {
            if (capMats[m] == null) continue;
            Color c = PM_Util.ModeColor(m);
            // Selected button: blinks while the iron heats up, steady light when it is ready.
            float k = !Powered ? 0.05f : (Mode == m ? (IronReady ? 3.5f : 0.4f + 3.2f * Mathf.Abs(Mathf.Sin(Time.time * 4f))) : 0.6f);
            capMats[m].SetColor("_EmissionColor", c * k);
            capMats[m].SetColor("_BaseColor", c * (Powered ? 0.35f : 0.08f));
        }
        if (SleevePivot != null && !Mathf.Approximately(sleeveAngle, sleeveTarget))
        {
            float prev = sleeveAngle;
            sleeveAngle = Mathf.MoveTowards(sleeveAngle, sleeveTarget, Time.deltaTime * (sleeveDragging ? 400f : 90f));
            SleevePivot.Rotate(0, sleeveAngle - prev, 0, Space.World);
        }
        // Temperature numbers: the selected one is bigger; on selection it flashes white and pops.
        for (int m = 1; m <= 3; m++)
        {
            TextMeshProUGUI t = tempLabels[m];
            if (t == null) continue;
            labelFlash[m] = Mathf.MoveTowards(labelFlash[m], 0f, Time.deltaTime * 1.6f);
            bool sel = Powered && Mode == m;
            float target = (sel ? 1.4f : 1f) + labelFlash[m] * 0.5f;
            Transform lt = t.transform.parent;
            float cur = lt.localScale.x / 0.0007f;
            lt.localScale = Vector3.one * 0.0007f * Mathf.Lerp(cur, target, Time.deltaTime * 10f);
            Color baseC = PM_Util.ModeColor(m) * (Powered ? 1f : 0.55f);
            baseC.a = 1f;
            t.color = Color.Lerp(baseC, Color.white, labelFlash[m]);
        }
        // Real stations need 2–5 minutes; here it is shortened to about 45 seconds.
        if (Powered) Pressure = Mathf.MoveTowards(Pressure, WorkPressure, Time.deltaTime * 0.08f);
        else Pressure = Mathf.MoveTowards(Pressure, 0f, Time.deltaTime * 1.5f);
        float wobble = Powered && Pressure >= WorkPressure - 0.01f ? Mathf.Sin(Time.time * 7f) * 0.04f : 0f;
        SetNeedle(Pressure + wobble);
        float rate = fastHeat ? 14f : (TargetTemp > IronTemp ? 8f : 4f);   // practice: quicker, but a cold iron still spits for a few seconds
        IronTemp = Mathf.MoveTowards(IronTemp, TargetTemp, Time.deltaTime * rate);
        if (Iron != null)
        {
            Iron.steamAllowed = Powered && Tank != null && !Tank.Empty;
            Iron.spitting = Iron.Steaming && WillSpit;
            Iron.heat = Mathf.Clamp01((IronTemp - 20f) / 180f);
            if (Iron.Steaming && Tank != null) Tank.Use(Time.deltaTime * 0.006f);
        }
    }
}

// Hand grip on the sleeve board: while held, the board turns with the hand around the arm's pivot.
public class PM_SleeveHandle : MonoBehaviour
{
    public PM_Station station;
    UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;
    Transform hand;
    Vector3 startVec;
    float startAngle;

    void Start()
    {
        interactable = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        interactable.selectEntered.AddListener(a =>
        {
            hand = a.interactorObject.transform;
            startVec = Flat(hand.position - station.SleevePivot.position);
            startAngle = station.SleeveAngle;
            station.sleeveDragging = true;
            PM_Clickable.Haptic(hand, 0.4f, 0.06f);
        });
        interactable.selectExited.AddListener(a => { hand = null; station.sleeveDragging = false; });
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    void Update()
    {
        if (hand == null || station == null || station.SleevePivot == null) return;
        Vector3 cur = Flat(hand.position - station.SleevePivot.position);
        if (cur.sqrMagnitude < 0.0004f || startVec.sqrMagnitude < 0.0004f) return;
        station.SetSleeveAngle(startAngle + Vector3.SignedAngle(startVec, cur, Vector3.up));
    }
}

// Marks a part of the station the iron must not go into (its collider is a trigger, so rays pass through).
public class PM_Solid : MonoBehaviour { }
