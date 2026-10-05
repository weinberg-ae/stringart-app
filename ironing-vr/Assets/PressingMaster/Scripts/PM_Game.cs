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

    enum State { Menu, TopicsLearn, TopicsExam, Learning, ExamIntro, ExamFabric, ExamResult, Quiz, QuizResult }

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
    bool fullPath;   // the whole learning path (not a single topic)
    int stepIndex;
    bool stepDone;
    bool fibersBasicDone;
    float doneTimer;
    float steamAirTime;
    bool modeClickedThisStep;

    // Exam
    readonly List<PM_FabricType> examList = new List<PM_FabricType>();
    readonly List<string> examLines = new List<string>();
    int examIndex, examScore;
    float examTime;
    bool examFinishedFabric;

    // Quizzes
    int quizKind;    // 0 = temperatures, 1 = fiber families
    readonly List<PM_HotspotInfo> quizItems = new List<PM_HotspotInfo>();
    int quizIndex, quizScore;
    bool quizAnswered;

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
        PM_Narrator.Create(panel.transform, new Vector3(-0.6f, 0.22f, -0.02f));
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
        panel.SetButtons(Btn(PM_Content.BtnLearn, ShowLearnTopics), Btn(PM_Content.BtnExam, ShowExamTopics));
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("menu");
    }

    // ---------------- Topic menus ----------------
    void ShowLearnTopics()
    {
        state = State.TopicsLearn;
        ClearStepVisuals();
        panel.SetContent(PM_Content.TopicsLearnTitle, PM_Content.TopicsLearnBody, "");
        panel.SetAccent(PM_Util.Cyan);
        var items = new List<KeyValuePair<string, Action>>();
        foreach (PM_Topic t in PM_Content.LearnTopics())
        {
            PM_Topic topic = t;
            items.Add(Btn(topic.label, () => StartTopic(topic)));
        }
        items.Add(Btn(PM_Content.BtnMenu, ShowMenu));
        panel.SetButtonGrid(3, 0, 0.3f, items.ToArray());
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("topics_learn");
    }

    void ShowExamTopics()
    {
        state = State.TopicsExam;
        ClearStepVisuals();
        panel.SetContent(PM_Content.TopicsExamTitle, PM_Content.TopicsExamBody, "");
        panel.SetAccent(PM_Util.Cyan);
        panel.SetButtonGrid(3, 0, 0.3f,
            Btn(PM_Content.BtnExamIron, StartExam),
            Btn(PM_Content.BtnQuizTemp, () => StartQuiz(0)),
            Btn(PM_Content.BtnQuizFamily, () => StartQuiz(1)),
            Btn(PM_Content.BtnMenu, ShowMenu));
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("topics_exam");
    }

    void StartTopic(PM_Topic t)
    {
        List<PM_Step> all = PM_Content.LearningSteps();
        fullPath = t.steps == null;
        steps = new List<PM_Step>();
        if (fullPath) steps.AddRange(all);
        else foreach (string id in t.steps) foreach (PM_Step st in all) if (st.id == id) steps.Add(st);
        if (steps.Count == 0) return;
        if (t.needsPower && !station.Powered) station.SetPower(true);
        state = State.Learning;
        EnterStep(0);
    }

    // ---------------- Quizzes ----------------
    void StartQuiz(int kind)
    {
        state = State.Quiz;
        ClearStepVisuals();
        quizKind = kind;
        quizItems.Clear();
        quizItems.AddRange(PM_Content.FiberHotspots());
        for (int i = quizItems.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            PM_HotspotInfo tmp = quizItems[i]; quizItems[i] = quizItems[j]; quizItems[j] = tmp;
        }
        if (quizItems.Count > 8) quizItems.RemoveRange(8, quizItems.Count - 8);
        quizIndex = 0;
        quizScore = 0;
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice(kind == 0 ? "quiz_temp" : "quiz_family");
        ShowQuizQuestion();
    }

    static int FamilyIndex(string family)
    {
        if (family != null && family.StartsWith("טבעי")) return 0;
        if (family != null && family.StartsWith("מלאכותי")) return 1;
        return 2;
    }

    string QuizQuestion(PM_HotspotInfo f)
    {
        return string.Format(quizKind == 0 ? PM_Content.QuizTempQ : PM_Content.QuizFamilyQ, f.name);
    }

    void ShowQuizQuestion()
    {
        PM_HotspotInfo f = quizItems[quizIndex];
        quizAnswered = false;
        panel.SetContent(quizKind == 0 ? PM_Content.QuizTempTitle : PM_Content.QuizFamilyTitle,
            QuizQuestion(f) + (quizKind == 0 ? "\n\n" + PM_Content.QuizTempHint : ""), (quizIndex + 1) + "/" + quizItems.Count);
        panel.SetAccent(PM_Util.Cyan);
        string[] o = quizKind == 0 ? PM_Content.QuizTempOptions : PM_Content.QuizFamilyOptions;
        panel.SetButtons(-1, Btn(o[0], () => AnswerQuiz(0)), Btn(o[1], () => AnswerQuiz(1)), Btn(o[2], () => AnswerQuiz(2)),
            Btn(PM_Content.BtnMenu, ShowMenu));
    }

    void AnswerQuiz(int choice)
    {
        if (state != State.Quiz || quizAnswered) return;
        quizAnswered = true;
        PM_HotspotInfo f = quizItems[quizIndex];
        int correct = quizKind == 0 ? Mathf.Clamp(f.mode - 1, 0, 2) : FamilyIndex(f.fiberGroup);
        string[] o = quizKind == 0 ? PM_Content.QuizTempOptions : PM_Content.QuizFamilyOptions;
        bool right = choice == correct;
        if (right) quizScore++;

        // Show the explanation line of this fiber ("גיהוץ:" for temperatures, "מקור:" for families).
        string key = quizKind == 0 ? "גיהוץ:" : "מקור:";
        string explain = "";
        foreach (string line in f.body.Split('\n')) if (line.StartsWith(key)) explain = line;
        panel.SetContent(quizKind == 0 ? PM_Content.QuizTempTitle : PM_Content.QuizFamilyTitle,
            QuizQuestion(f) + "\n\n" + f.name + "\n" + explain, (quizIndex + 1) + "/" + quizItems.Count);
        panel.SetAccent(quizKind == 0 ? PM_Util.ModeColor(f.mode) : PM_Util.FamilyColor(f.fiberGroup));
        panel.SetStatus(right ? PM_Content.QuizRight : string.Format(PM_Content.QuizWrong, o[correct]), right ? PM_Util.Green : PM_Util.Red);
        if (PM_Audio.I != null) PM_Audio.I.Play(right ? "ding" : "error", 0.7f);
        if (right) PM_Look.ButtonPulse(panel.transform.position - panel.transform.up * 0.3f, PM_Util.Green, -panel.transform.forward);
        panel.SetButtons(Btn(PM_Content.BtnNext, NextQuiz), Btn(PM_Content.BtnMenu, ShowMenu));
    }

    void NextQuiz()
    {
        if (state != State.Quiz || !quizAnswered) return;
        quizIndex++;
        if (quizIndex < quizItems.Count) ShowQuizQuestion();
        else ShowQuizResult();
    }

    void ShowQuizResult()
    {
        state = State.QuizResult;
        int n = quizItems.Count;
        int rank = quizScore >= n ? 3 : quizScore >= n * 3 / 4 ? 2 : quizScore >= n / 2 ? 1 : 0;
        panel.SetContent(PM_Content.QuizResultTitle,
            string.Format(PM_Content.QuizResultBody, quizScore, n) + "\n\n" + PM_Content.Ranks[rank], quizScore + "/" + n);
        panel.SetAccent(rank >= 2 ? PM_Util.Green : PM_Util.Cyan);
        int kind = quizKind;
        panel.SetButtons(Btn(PM_Content.BtnAgain, () => StartQuiz(kind)), Btn(PM_Content.BtnTopics, ShowExamTopics), Btn(PM_Content.BtnMenu, ShowMenu));
        if (PM_Audio.I != null) PM_Audio.I.Play(rank >= 2 ? "ding" : "error", 0.8f);
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
        // Floating fiber screens in two arcs above and behind the station:
        // lower row = the five fabrics of the game, upper row = more fibers.
        List<PM_HotspotInfo> fibers = PM_Content.FiberHotspots();
        int basicCount = 0, extraCount = 0;
        foreach (PM_HotspotInfo info in fibers) if (info.extraFiber) extraCount++; else basicCount++;
        int bi = 0, ei = 0;
        foreach (PM_HotspotInfo info in fibers)
        {
            bool extra = info.extraFiber;
            int i = extra ? ei++ : bi++;
            int n = extra ? extraCount : basicCount;
            float span = extra ? 60f : 56f;
            float ang = n > 1 ? Mathf.Lerp(span, -span, i / (float)(n - 1)) : 0f;
            Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * station.Forward;
            Vector3 pos = station.PlayerPos + dir * (extra ? 2.6f : 2.4f) + Vector3.up * (extra ? 2.85f : 2.15f);
            PM_FiberScreen fs = PM_FiberScreen.Create(info, info.fiberGroup, pos);
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
        StartTopic(PM_Content.LearnTopics()[0]);   // whole path
    }

    void EnterStep(int i)
    {
        stepIndex = Mathf.Clamp(i, 0, steps.Count - 1);
        PM_Step s = steps[stepIndex];
        stepDone = false;
        fibersBasicDone = false;
        doneTimer = 0f;
        steamAirTime = 0f;
        modeClickedThisStep = false;

        highlight.Clear();
        station.RefreshButtons();
        station.ShowTools(s.group == PM_HotspotGroup.Tools || s.toolTask > 0);
        ShowStationHotspots(true);
        CloseCard();

        if (s.target == PM_Target.Fabric)
        {
            if (s.toolTask > 0) SpawnToolFabric(s.fabric, s.toolTask);
            else if (fabric == null || fabric.info.type != s.fabric || fabric.dome > 0f || fabric.sizeZ < 0.2f) { SpawnFabric(s.fabric, stepIndex); AddFabricHotspots(); }
        }
        else RemoveFabric();

        if (s.target != PM_Target.None && s.target != PM_Target.Fabric)
            highlight.Show(station.Targets(s.target), PM_Util.Cyan, true);

        panel.SetContent(s.title, s.body, (stepIndex + 1) + "/" + steps.Count);
        Color accent = PM_Util.Cyan;
        if (s.target == PM_Target.Fabric) accent = PM_Util.ModeColor(PM_Content.Fabric(s.fabric).mode);
        else if (s.group == PM_HotspotGroup.Tools) accent = PM_Util.Violet;
        panel.SetAccent(accent);
        // Navigation (right to left): menu, repeat, back, next. Forward is on the left, as Hebrew reads.
        bool first = stepIndex == 0, last = stepIndex == steps.Count - 1;
        var nav = new List<KeyValuePair<string, Action>>();
        nav.Add(Btn(PM_Content.BtnMenu, ShowMenu));
        nav.Add(Btn(PM_Content.BtnRepeat, RepeatVoice));
        if (!first) nav.Add(Btn(PM_Content.BtnBack, PrevStep));
        else if (!last) nav.Add(Btn(PM_Content.BtnTopics, ShowLearnTopics));
        if (!last) nav.Add(Btn(PM_Content.BtnNext, NextStep));
        else if (fullPath) nav.Add(Btn(PM_Content.BtnExam, ShowExamTopics));
        else nav.Add(Btn(PM_Content.BtnTopics, ShowLearnTopics));
        panel.SetButtons(nav.Count - 1, nav.ToArray());

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
        else if (state == State.TopicsLearn) PM_Audio.I.PlayVoice("topics_learn");
        else if (state == State.TopicsExam) PM_Audio.I.PlayVoice("topics_exam");
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
                    int basic = 0, basicSeen = 0;
                    foreach (PM_FiberScreen f in fiberScreens)
                    {
                        if (f == null) continue;
                        if (f.Visited) v++;
                        if (!f.info.extraFiber) { basic++; if (f.Visited) basicSeen++; }
                    }
                    // The five basic fibers are required; the extra ones are for curious learners.
                    if (v >= fiberScreens.Count && fiberScreens.Count > 0) Complete(PM_Content.StAllExplored);
                    else if (basic > 0 && basicSeen >= basic)
                    {
                        // No auto-advance here: the learner may keep exploring the upper row, "Next" continues.
                        if (!fibersBasicDone && PM_Audio.I != null) PM_Audio.I.Play("ding", 0.7f);
                        fibersBasicDone = true;
                        panel.SetStatus(string.Format(PM_Content.StFibersBasic, v, fiberScreens.Count), PM_Util.Green);
                    }
                    else panel.SetStatus(string.Format(PM_Content.StExplored, v, fiberScreens.Count), PM_Util.Cyan);
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

    // Fabric lying on a pressing tool: 1 = shirt collar on the point presser, 2 = dress bodice on the ham.
    void SpawnToolFabric(PM_FabricType t, int task)
    {
        RemoveFabric();
        PM_FabricInfo info = PM_Content.Fabric(t);
        List<Transform> tool = station.Targets(task == 1 ? PM_Target.PointPresser : PM_Target.Ham);
        if (tool.Count == 0) { SpawnFabric(t, 7); return; }
        Transform tr = tool[0];
        if (task == 1)
            fabric = PM_Fabric.Create(info, tr.TransformPoint(new Vector3(0f, 0.142f, 0f)), station.BoardRotation, null, 41, 0.28f, 0.075f, 0.008f);
        else
            fabric = PM_Fabric.Create(info, tr.TransformPoint(new Vector3(0f, 0.015f, 0f)), station.BoardRotation, null, 43, 0.27f, 0.17f, 0.06f);
        Vector3 gpos = new Vector3(station.BoardCenter.x, station.PlayerPos.y + 0.85f, station.BoardCenter.z) - station.Right * 1.3f + station.Forward * 0.05f;
        garment = PM_Garment.Show(t.ToString().ToLower(), gpos, 0.75f, station.PlayerPos + Vector3.up * 1.6f, info.garment, info.smoothness, true);
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
        // Temperature quiz: the station buttons answer the question.
        if (state == State.Quiz && quizKind == 0)
        {
            PM_Look.ButtonPulse(station.ButtonCenter(m), PM_Util.ModeColor(m), -station.Forward);
            AnswerQuiz(m - 1);
            return;
        }
        if (!station.Powered) { panel.SetStatus(PM_Content.StNeedPower, PM_Util.Red); if (PM_Audio.I != null) PM_Audio.I.Play("error", 0.6f); return; }
        station.SetMode(m);
        modeClickedThisStep = true;
        PM_Look.ButtonPulse(station.ButtonCenter(m), PM_Util.ModeColor(m), -station.Forward);
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
        panel.SetButtons(1, Btn(PM_Content.BtnTopics, ShowExamTopics), Btn(PM_Content.BtnNext, () => StartExamFabric(0)), Btn(PM_Content.BtnMenu, ShowMenu));
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
        panel.SetButtons(Btn(PM_Content.BtnAgain, StartExam), Btn(PM_Content.BtnTopics, ShowExamTopics), Btn(PM_Content.BtnMenu, ShowMenu));
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
            else if (state == State.Menu) ShowLearnTopics();
            else if (state == State.TopicsLearn) StartLearning();
            else if (state == State.ExamIntro) StartExamFabric(0);
            else if (state == State.Quiz) NextQuiz();
        }
        if (KeyDown(Key.B)) PrevStep();
        if (KeyDown(Key.M)) ShowMenu();
        if (KeyDown(Key.X)) StartExam();
        if (KeyDown(Key.P)) station.onPowerClicked();
        for (int d = 1; d <= 3; d++)
        {
            if (!KeyDown(d == 1 ? Key.Digit1 : d == 2 ? Key.Digit2 : Key.Digit3)) continue;
            if (state == State.Quiz) AnswerQuiz(d - 1);
            else OnModeClicked(d);
        }
    }
}
