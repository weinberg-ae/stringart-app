using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// MAIN SCRIPT. Put it on one empty object in the scene (for example _Game_Manager) and press Play.
// Keyboard (for testing without the headset): N = next, B = back, P = power, 1/2/3 = temperature,
// Space (hold) = iron the fabric, Left Shift (hold) = steam, M = menu, X = exam.
// Without headset: hold right mouse button to look around, W A S D Q E to move.
public class PM_Game : MonoBehaviour
{
    [Tooltip("Graphite + neon look for the table. Uncheck to keep the original blue model.")]
    public bool restyleTable = true;

    enum State { Menu, Learning, ExamIntro, ExamFabric, ExamResult }

    PM_Station station;
    PM_Panel panel;
    PM_Highlight highlight;
    PM_Fabric fabric;

    // Points of light + info card
    PM_Panel card;
    PM_Highlight cardHighlight;
    readonly List<PM_Hotspot> stationHs = new List<PM_Hotspot>();
    readonly List<PM_Hotspot> toolHs = new List<PM_Hotspot>();
    readonly List<PM_Hotspot> fabricHs = new List<PM_Hotspot>();
    readonly List<PM_FiberScreen> fiberScreens = new List<PM_FiberScreen>();
    public static PM_Game I;
    Vector2 desktopUV;
    float desktopTime = -1f;
    PM_Hotspot openHs;
    PM_Garment garment;
    LineRenderer cardLink;

    State state;
    List<PM_Step> steps;
    int stepIndex;
    bool stepDone;
    float doneTimer;
    float steamAirTime;
    bool modeClickedThisStep;

    // Exam
    readonly List<PM_FabricType> examList = new List<PM_FabricType>();
    readonly List<string> examLines = new List<string>();
    int examIndex, examScore;
    float examTime;
    bool examFinishedFabric;

    void Awake()
    {
        I = this;
        if (FindAnyObjectByType<PM_Audio>() == null) gameObject.AddComponent<PM_Audio>();
    }

    void Start()
    {
        station = gameObject.AddComponent<PM_Station>();
        if (!station.Setup()) { enabled = false; return; }
        station.onPowerClicked = () => station.SetPower(!station.Powered);
        station.onModeClicked = OnModeClicked;
        if (station.Iron != null) station.Iron.onGrab = () => { };

        highlight = PM_Highlight.Create("PM_Highlight");
        panel = PM_Panel.Create(null);
        panel.Place(station.PanelPos, station.PlayerPos + Vector3.up * 1.6f);
        cardHighlight = PM_Highlight.Create("PM_CardHighlight");
        card = PM_Panel.Create(null, "PM_InfoCard", 620, 520, 40, 28, 36, false);
        card.gameObject.SetActive(false);
        BuildHotspots();
        PM_Look.PostFX();
        PM_Look.GridFloor(station.PlayerPos, station.Forward, station.Right);
        PM_Look.Dust(station.PlayerPos + station.Forward * 0.8f);
        if (restyleTable) station.Restyle();
        // Tailor's mannequin standing next to the station (decoration).
        PM_Garment.Show("mannequin", station.PlayerPos + station.Forward * 1.5f - station.Right * 2.5f, 1.6f,
            station.PlayerPos + Vector3.up * 1.6f, null, 0.3f, false);
        ShowMenu();

        // Without a headset: put the camera at eye height looking at the table, mouse + WASD control.
        if (!UnityEngine.XR.XRSettings.isDeviceActive && Camera.main != null)
        {
            Transform cam = Camera.main.transform;
            cam.position = station.PlayerPos + Vector3.up * 1.6f;
            cam.rotation = Quaternion.LookRotation(station.BoardCenter + Vector3.up * 0.4f - cam.position);
            cam.gameObject.AddComponent<PM_DesktopCamera>();
        }
    }

    // ---------------- Menu ----------------
    void ShowMenu()
    {
        state = State.Menu;
        ClearStepVisuals();
        panel.SetContent(PM_Content.MenuTitle, PM_Content.MenuBody, "");
        panel.SetAccent(PM_Util.Cyan);
        panel.SetButtons(Btn(PM_Content.BtnLearn, StartLearning), Btn(PM_Content.BtnExam, StartExam));
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("menu");
    }

    // ---------------- Points of light ----------------
    void BuildHotspots()
    {
        foreach (PM_HotspotInfo info in PM_Content.StationHotspots())
        {
            List<Transform> parts = station.Targets(info.target);
            if (parts.Count == 0) { Debug.LogWarning("[PM] Нет деталей для точки " + info.id); continue; }
            Vector3 pos = station.AnchorPoint(info.anchor, parts);
            Transform parent = info.target == PM_Target.Iron && station.Iron != null ? station.Iron.transform : station.Table;
            stationHs.Add(MakeHotspot(info, pos, parent, parts));
        }
        foreach (PM_HotspotInfo info in PM_Content.ToolHotspots())
        {
            List<Transform> parts = station.Targets(info.target);
            if (parts.Count == 0) continue;
            Vector3 pos = station.AnchorPoint(info.anchor, parts) + Vector3.up * 0.04f;
            toolHs.Add(MakeHotspot(info, pos, parts[0], parts));
        }
        // Floating fiber screens in an arc above and behind the station.
        foreach (PM_HotspotInfo info in PM_Content.FiberHotspots())
        {
            float ang;
            string grp;
            switch (info.fiber)
            {
                case PM_FabricType.Cotton: ang = 56f; grp = "צמחי · תאית"; break;
                case PM_FabricType.Linen: ang = 28f; grp = "צמחי · תאית"; break;
                case PM_FabricType.Wool: ang = 0f; grp = "מן החי · חלבון"; break;
                case PM_FabricType.Silk: ang = -28f; grp = "מן החי · חלבון"; break;
                default: ang = -56f; grp = "כימי · פולימר"; break;
            }
            Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * station.Forward;
            Vector3 pos = station.PlayerPos + dir * 2.4f + Vector3.up * 2.15f;
            PM_FiberScreen fs = PM_FiberScreen.Create(info, grp, pos);
            fs.onClick = OnFiberScreen;
            fiberScreens.Add(fs);
        }
        ShowStationHotspots(false);
    }

    void OnFiberScreen(PM_FiberScreen fs)
    {
        bool open = !fs.Expanded;
        foreach (PM_FiberScreen f in fiberScreens) f.SetExpanded(false);
        fs.SetExpanded(open);
        if (PM_Audio.I != null)
        {
            if (open) PM_Audio.I.PlayVoice("hs_" + fs.info.id);
            else PM_Audio.I.StopVoice();
        }
    }

    // Keyboard/mouse test: called by PM_DesktopCamera while the left mouse button is held on the fabric.
    public void DesktopIron(PM_Fabric f, Vector2 uv, Vector3 point)
    {
        if (f != fabric) return;
        desktopUV = uv;
        desktopTime = Time.time;
        if (station.Iron != null) station.Iron.PlaceAt(point);
    }

    PM_Hotspot MakeHotspot(PM_HotspotInfo info, Vector3 pos, Transform parent, List<Transform> parts)
    {
        PM_Hotspot h = PM_Hotspot.Create(info, pos, parent, parts);
        h.onClick = OpenCard;
        return h;
    }

    void ShowStationHotspots(bool on)
    {
        foreach (PM_Hotspot h in stationHs) if (h != null) h.gameObject.SetActive(on);
        foreach (PM_FiberScreen f in fiberScreens) if (f != null) { f.gameObject.SetActive(on); if (!on) f.SetExpanded(false); }
        if (!on) CloseCard();
    }

    void OpenCard(PM_Hotspot h)
    {
        if (openHs == h && card.gameObject.activeSelf) { CloseCard(); return; }
        openHs = h;
        h.SetVisited(true);
        card.gameObject.SetActive(true);
        card.SetContent(h.info.title, h.info.body, "");
        card.SetAccent(h.info.accent);
        card.SetButtons(Btn(PM_Content.BtnClose, CloseCard), Btn(PM_Content.BtnRepeat, () => { if (PM_Audio.I != null) PM_Audio.I.PlayVoice("hs_" + h.info.id); }));
        Vector3 head = Camera.main != null ? Camera.main.transform.position : station.PlayerPos + Vector3.up * 1.6f;
        Vector3 pos = h.transform.position + Vector3.up * 0.32f - station.Forward * 0.18f;
        pos.y = Mathf.Clamp(pos.y, station.PlayerPos.y + 1.1f, station.PlayerPos.y + 1.75f);
        card.Place(pos, head);
        if (cardLink == null)
        {
            cardLink = PM_Util.Line(null, "PM_CardLink", new[] { Vector3.zero, Vector3.zero }, PM_Util.Cyan, 0.003f, false);
            cardLink.useWorldSpace = true;
        }
        cardLink.gameObject.SetActive(true);
        cardHighlight.Clear();
        if (h.info.target != PM_Target.Fabric && h.parts.Count > 0) cardHighlight.Show(h.parts, h.info.accent, false);
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("hs_" + h.info.id);
    }

    void CloseCard()
    {
        openHs = null;
        if (card != null) card.gameObject.SetActive(false);
        if (cardLink != null) cardLink.gameObject.SetActive(false);
        if (cardHighlight != null) { cardHighlight.Clear(); station.RefreshButtons(); }
    }

    static int CountVisited(List<PM_Hotspot> list)
    {
        int n = 0;
        foreach (PM_Hotspot h in list) if (h != null && h.Visited) n++;
        return n;
    }

    static KeyValuePair<string, Action> Btn(string label, Action a) { return new KeyValuePair<string, Action>(label, a); }

    void ClearStepVisuals()
    {
        highlight.Clear();
        station.RefreshButtons();
        station.ShowTools(false);
        ShowStationHotspots(false);
        RemoveFabric();
        if (PM_Audio.I != null) PM_Audio.I.StopVoice();
    }

    // ---------------- Learning ----------------
    void StartLearning()
    {
        state = State.Learning;
        steps = PM_Content.LearningSteps();
        EnterStep(0);
    }

    void EnterStep(int i)
    {
        stepIndex = Mathf.Clamp(i, 0, steps.Count - 1);
        PM_Step s = steps[stepIndex];
        stepDone = false;
        doneTimer = 0f;
        steamAirTime = 0f;
        modeClickedThisStep = false;

        highlight.Clear();
        station.RefreshButtons();
        station.ShowTools(s.group == PM_HotspotGroup.Tools);
        ShowStationHotspots(true);
        CloseCard();

        if (s.target == PM_Target.Fabric)
        {
            if (fabric == null || fabric.info.type != s.fabric) { SpawnFabric(s.fabric, stepIndex); AddFabricHotspots(); }
        }
        else RemoveFabric();

        if (s.target != PM_Target.None && s.target != PM_Target.Fabric)
            highlight.Show(station.Targets(s.target), PM_Util.Cyan, true);

        panel.SetContent(s.title, s.body, (stepIndex + 1) + "/" + steps.Count);
        Color accent = PM_Util.Cyan;
        if (s.target == PM_Target.Fabric) accent = PM_Util.ModeColor(PM_Content.Fabric(s.fabric).mode);
        else if (s.group == PM_HotspotGroup.Tools) accent = PM_Util.Violet;
        panel.SetAccent(accent);
        if (stepIndex == steps.Count - 1)
            panel.SetButtons(Btn(PM_Content.BtnExam, StartExam), Btn(PM_Content.BtnMenu, ShowMenu), Btn(PM_Content.BtnRepeat, RepeatVoice));
        else
            panel.SetButtons(Btn(PM_Content.BtnNext, NextStep), Btn(PM_Content.BtnRepeat, RepeatVoice), Btn(PM_Content.BtnMenu, ShowMenu));

        if (s.action == PM_Action.Power && station.Powered) Complete(PM_Content.StPressureOk);
        RepeatVoice();
    }

    void AddFabricHotspots()
    {
        fabricHs.Clear();
        if (fabric == null) return;
        List<PM_HotspotInfo> infos = PM_Content.FabricHotspots(fabric.info.type);
        Vector3 c = fabric.transform.position + Vector3.up * 0.14f + station.Forward * 0.14f;
        for (int i = 0; i < infos.Count; i++)
        {
            Vector3 pos = c + station.Right * (0.17f - 0.17f * i);
            fabricHs.Add(MakeHotspot(infos[i], pos, fabric.transform, new List<Transform> { fabric.transform }));
        }
    }

    void RepeatVoice()
    {
        if (PM_Audio.I == null) return;
        if (state == State.Learning) PM_Audio.I.PlayVoice(steps[stepIndex].id);
        else if (state == State.Menu) PM_Audio.I.PlayVoice("menu");
    }

    void NextStep()
    {
        if (state != State.Learning) return;
        if (stepIndex < steps.Count - 1) EnterStep(stepIndex + 1);
    }

    void PrevStep()
    {
        if (state != State.Learning) return;
        if (stepIndex > 0) EnterStep(stepIndex - 1);
    }

    void Complete(string message)
    {
        if (stepDone) return;
        stepDone = true;
        doneTimer = 0f;
        panel.SetStatus(message, PM_Util.Green);
        if (PM_Audio.I != null) PM_Audio.I.Play("ding", 0.7f);
        highlight.Clear();
        station.RefreshButtons();
    }

    void UpdateLearning(float dt)
    {
        PM_Step s = steps[stepIndex];
        if (stepDone)
        {
            doneTimer += dt;
            if (s.action != PM_Action.Next && doneTimer > 2.5f && stepIndex < steps.Count - 1) EnterStep(stepIndex + 1);
            return;
        }
        PM_Iron iron = station.Iron;
        switch (s.action)
        {
            case PM_Action.Power:
                if (station.Powered) Complete(PM_Content.StPressureLow);
                break;
            case PM_Action.WaitPressure:
                if (!station.Powered) panel.SetStatus(PM_Content.StNeedPower, PM_Util.Red);
                else if (station.PressureOk) Complete(PM_Content.StPressureOk);
                else panel.SetStatus(PM_Content.StPressureLow + "  " + station.Pressure.ToString("0.0") + " bar", PM_Util.Yellow);
                break;
            case PM_Action.GrabIron:
                if (iron != null && iron.Held) Complete(PM_Content.StGrabbed);
                break;
            case PM_Action.SteamInAir:
                if (!station.PressureOk) { panel.SetStatus(station.Powered ? PM_Content.StPressureLow : PM_Content.StNeedPower, PM_Util.Yellow); break; }
                bool steaming = (iron != null && iron.Steaming && iron.Touching == null) || KeyHeld(Key.LeftShift);
                if (steaming) steamAirTime += dt;
                panel.SetStatus(string.Format(PM_Content.StSteamAir, Mathf.RoundToInt(Mathf.Clamp01(steamAirTime / 2f) * 100)), PM_Util.Yellow);
                if (steamAirTime >= 2f) Complete(PM_Content.StDone);
                break;
            case PM_Action.PressTemp:
                if (modeClickedThisStep) Complete(string.Format(PM_Content.StModeSet, PM_Content.ModeDots[station.Mode], PM_Content.ModeTemp[station.Mode]));
                break;
            case PM_Action.Explore:
            {
                List<PM_Hotspot> list = s.group == PM_HotspotGroup.Tools ? toolHs : stationHs;
                int v = CountVisited(list);
                if (s.group == PM_HotspotGroup.Fibers)
                {
                    list = new List<PM_Hotspot>();
                    v = 0;
                    foreach (PM_FiberScreen f in fiberScreens) if (f != null && f.Visited) v++;
                    panel.SetStatus(string.Format(PM_Content.StExplored, v, fiberScreens.Count), PM_Util.Cyan);
                    if (v >= fiberScreens.Count && fiberScreens.Count > 0) Complete(PM_Content.StAllExplored);
                    break;
                }
                panel.SetStatus(string.Format(PM_Content.StExplored, v, list.Count), PM_Util.Cyan);
                if (list.Count > 0 && v >= list.Count) Complete(PM_Content.StAllExplored);
                break;
            }
            case PM_Action.IronFabric:
                if (!ProcessIroning(dt, false))
                    panel.SetStatus(string.Format(PM_Content.StExplored, CountVisited(fabricHs), fabricHs.Count), PM_Util.Cyan);
                if (fabric != null && fabric.Done) { Celebrate(); Complete(PM_Content.StDone); }
                break;
        }
    }

    // ---------------- Fabric / ironing ----------------
    void SpawnFabric(PM_FabricType t, int seed)
    {
        RemoveFabric();
        float lateral = Vector3.Dot(station.PlayerPos - station.BoardCenter, station.Right);
        lateral = Mathf.Clamp(lateral - 0.05f, -0.4f, 0.4f);
        Vector3 pos = new Vector3(station.BoardCenter.x, station.BoardTopY + 0.004f, station.BoardCenter.z) + station.Right * lateral;
        fabric = PM_Fabric.Create(PM_Content.Fabric(t), pos, station.BoardRotation, null, seed + 3);

        // Garment made of this fabric, shown as a rotating exhibit left of the board (name hidden in the exam).
        PM_FabricInfo info = PM_Content.Fabric(t);
        Vector3 gpos = new Vector3(station.BoardCenter.x, station.PlayerPos.y + 0.85f, station.BoardCenter.z) - station.Right * 1.3f + station.Forward * 0.05f;
        string label = state == State.Learning ? info.garment : "?";
        garment = PM_Garment.Show(t.ToString().ToLower(), gpos, 0.75f, station.PlayerPos + Vector3.up * 1.6f, label, info.smoothness, true);
    }

    void RemoveFabric()
    {
        if (fabric != null) Destroy(fabric.gameObject);
        fabric = null;
        if (garment != null) Destroy(garment.gameObject);
        garment = null;
    }

    // Returns true while ironing happens this frame.
    bool ProcessIroning(float dt, bool exam)
    {
        if (fabric == null) return false;
        PM_Iron iron = station.Iron;
        bool mouseIroning = Time.time - desktopTime < 0.15f;
        bool simulated = KeyHeld(Key.Space) || mouseIroning;
        bool touching = simulated || (iron != null && iron.Touching == fabric);
        if (!touching) return false;

        bool steaming = (iron != null && iron.Steaming) || (simulated && KeyHeld(Key.LeftShift) && station.PressureOk);
        Vector2 uv;
        if (mouseIroning) uv = desktopUV;
        else if (simulated)
        {
            float t = Time.time;
            uv = new Vector2(0.5f + 0.45f * Mathf.Sin(t * 1.7f), 0.5f + 0.42f * Mathf.Sin(t * 2.9f + 1f));
        }
        else uv = iron.TouchUV;
        float radius = iron != null ? Mathf.Max(0.07f, iron.SoleRadius) : 0.09f;

        PM_FabricInfo info = fabric.info;
        if (!station.Powered) { panel.SetStatus(PM_Content.StNeedPower, PM_Util.Red); return true; }
        int mode = station.Mode;
        if (mode == 0) { panel.SetStatus(PM_Content.StNoMode, PM_Util.Yellow); return true; }

        float rate = 1f, scorch = 0f, spots = 0f;
        string msg = null;
        Color msgColor = PM_Util.Yellow;
        if (mode < info.mode)
        {
            rate = 0f;
            msg = string.Format(PM_Content.StTooCold, PM_Content.ModeLabel(info.mode));
        }
        else if (mode > info.mode)
        {
            if (exam)
            {
                scorch = (mode - info.mode) * (info.type == PM_FabricType.Polyester ? 1.6f : 0.9f);
                msg = info.type == PM_FabricType.Polyester ? PM_Content.StMelted : PM_Content.StBurned;
                msgColor = PM_Util.Red;
                if (PM_Audio.I != null && UnityEngine.Random.value < dt * 2f) PM_Audio.I.Play("sizzle", 0.6f);
                if (iron != null) iron.Buzz(0.8f, 0.1f);
            }
            else
            {
                rate = 0f;
                msg = string.Format(PM_Content.StTooHotLearn, PM_Content.ModeLabel(info.mode));
                msgColor = PM_Util.Red;
            }
        }
        if (rate > 0f || exam)
        {
            if (info.steam == PM_Steam.Required && !steaming)
            {
                rate *= 0.25f;
                if (msg == null) msg = PM_Content.StNeedSteam;
            }
            else if (info.steam == PM_Steam.Forbidden && steaming)
            {
                if (exam) { spots = 1f; msg = PM_Content.StSpots; msgColor = PM_Util.Red; }
                else { rate = 0f; msg = string.Format(PM_Content.StNoSteam, info.name); msgColor = PM_Util.Red; }
            }
        }

        fabric.Iron(uv, radius, dt, rate, scorch, spots, mode / 3f);
        if (msg == null) { msg = string.Format(PM_Content.StProgress, Mathf.RoundToInt(fabric.Progress * 100f)); msgColor = PM_Util.Cyan; }
        string timeText = exam ? "   " + string.Format(PM_Content.StTime, Mathf.CeilToInt(Mathf.Max(0f, examTime))) : "";
        panel.SetStatus(msg + timeText, msgColor);
        return true;
    }

    void OnModeClicked(int m)
    {
        if (!station.Powered) { panel.SetStatus(PM_Content.StNeedPower, PM_Util.Red); if (PM_Audio.I != null) PM_Audio.I.Play("error", 0.6f); return; }
        station.SetMode(m);
        modeClickedThisStep = true;
        panel.SetStatus(string.Format(PM_Content.StModeSet, PM_Content.ModeDots[m], PM_Content.ModeTemp[m]), PM_Util.ModeColor(m));
    }

    // ---------------- Exam ----------------
    void StartExam()
    {
        state = State.ExamIntro;
        ClearStepVisuals();
        examList.Clear();
        examLines.Clear();
        examScore = 0;
        var plant = new[] { PM_FabricType.Cotton, PM_FabricType.Linen };
        var animal = new[] { PM_FabricType.Wool, PM_FabricType.Silk };
        examList.Add(plant[UnityEngine.Random.Range(0, 2)]);
        examList.Add(animal[UnityEngine.Random.Range(0, 2)]);
        examList.Add(PM_FabricType.Polyester);
        for (int i = examList.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            PM_FabricType tmp = examList[i]; examList[i] = examList[j]; examList[j] = tmp;
        }
        panel.SetContent(PM_Content.ExamTitle, PM_Content.ExamBody, "");
        panel.SetButtons(Btn(PM_Content.BtnNext, () => StartExamFabric(0)), Btn(PM_Content.BtnMenu, ShowMenu));
        highlight.Show(station.Targets(PM_Target.Power), PM_Util.Cyan, true);
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("exam_intro");
    }

    void StartExamFabric(int i)
    {
        highlight.Clear();
        station.RefreshButtons();
        state = State.ExamFabric;
        examIndex = i;
        examTime = 60f;
        examFinishedFabric = false;
        station.SetMode(0);
        PM_FabricInfo info = PM_Content.Fabric(examList[i]);
        SpawnFabric(info.type, 100 + i * 7);
        panel.SetContent(string.Format(PM_Content.ExamFabricTitle, i + 1),
            string.Format(PM_Content.ExamFabricBody, info.cue, info.burn), (i + 1) + "/" + examList.Count);
        panel.SetButtons(Btn(PM_Content.BtnMenu, ShowMenu));
    }

    void UpdateExam(float dt)
    {
        if (fabric == null) return;
        if (examFinishedFabric)
        {
            doneTimer += dt;
            if (doneTimer > 3f)
            {
                if (examIndex + 1 < examList.Count) StartExamFabric(examIndex + 1);
                else ShowExamResult();
            }
            return;
        }
        examTime -= dt;
        bool ironing = ProcessIroning(dt, true);
        if (!ironing) panel.SetStatus(string.Format(PM_Content.StTime, Mathf.CeilToInt(Mathf.Max(0f, examTime))), PM_Util.Cyan);

        PM_FabricInfo info = fabric.info;
        string n = (examIndex + 1).ToString();
        if (fabric.Damage >= 0.25f)
        {
            string why = info.type == PM_FabricType.Polyester && fabric.Damage > 0 ? PM_Content.StMelted : PM_Content.StBurned;
            examLines.Add(string.Format(PM_Content.ExamLineBurn, n, info.name, why));
            FinishExamFabric(why, PM_Util.Red, "error");
        }
        else if (fabric.Done)
        {
            examScore++;
            examLines.Add(string.Format(PM_Content.ExamLineOk, n, info.name, Mathf.RoundToInt(60f - examTime)));
            FinishExamFabric(PM_Content.StDone + " (" + info.name + ")", PM_Util.Green, "ding");
        }
        else if (examTime <= 0f)
        {
            examLines.Add(string.Format(PM_Content.ExamLineTime, n, info.name));
            FinishExamFabric(PM_Content.StTimeUp + " (" + info.name + ")", PM_Util.Red, "error");
        }
    }

    void Celebrate()
    {
        if (fabric == null) return;
        fabric.Flash();
        PM_Look.Burst(fabric.transform.position + Vector3.up * 0.05f, PM_Util.ModeColor(fabric.info.mode));
    }

    void FinishExamFabric(string msg, Color c, string sound)
    {
        if (sound == "ding") Celebrate();
        examFinishedFabric = true;
        doneTimer = 0f;
        panel.SetStatus(msg, c);
        if (PM_Audio.I != null) PM_Audio.I.Play(sound, 0.8f);
    }

    void ShowExamResult()
    {
        state = State.ExamResult;
        RemoveFabric();
        string body = string.Join("\n", examLines.ToArray()) + "\n\n" + PM_Content.Ranks[Mathf.Clamp(examScore, 0, 3)];
        panel.SetContent(PM_Content.ExamResultTitle, body, examScore + "/3");
        panel.SetButtons(Btn(PM_Content.BtnExam, StartExam), Btn(PM_Content.BtnMenu, ShowMenu));
        if (PM_Audio.I != null) PM_Audio.I.Play(examScore >= 2 ? "ding" : "error", 0.8f);
    }

    // ---------------- Loop ----------------
    void Update()
    {
        float dt = Time.deltaTime;
        HandleKeyboard();
        if (openHs != null && cardLink != null && card.gameObject.activeSelf)
        {
            cardLink.SetPosition(0, openHs.transform.position);
            cardLink.SetPosition(1, card.transform.position - card.transform.up * (0.26f));
        }
        if (station != null && station.Iron != null) station.Iron.simulateSteam = KeyHeld(Key.LeftShift);
        switch (state)
        {
            case State.Learning: UpdateLearning(dt); break;
            case State.ExamFabric: UpdateExam(dt); break;
        }
    }

    static bool KeyHeld(Key k)
    {
        Keyboard kb = Keyboard.current;
        return kb != null && kb[k].isPressed;
    }

    static bool KeyDown(Key k)
    {
        Keyboard kb = Keyboard.current;
        return kb != null && kb[k].wasPressedThisFrame;
    }

    void HandleKeyboard()
    {
        if (KeyDown(Key.N))
        {
            if (state == State.Learning) NextStep();
            else if (state == State.Menu) StartLearning();
            else if (state == State.ExamIntro) StartExamFabric(0);
        }
        if (KeyDown(Key.B)) PrevStep();
        if (KeyDown(Key.M)) ShowMenu();
        if (KeyDown(Key.X)) StartExam();
        if (KeyDown(Key.P)) station.onPowerClicked();
        if (KeyDown(Key.Digit1)) OnModeClicked(1);
        if (KeyDown(Key.Digit2)) OnModeClicked(2);
        if (KeyDown(Key.Digit3)) OnModeClicked(3);
    }
}
