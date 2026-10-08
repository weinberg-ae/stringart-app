using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

// MAIN SCRIPT. Put it on one empty object in the scene (for example _Game_Manager) and press Play.
// Keyboard (for testing without the headset): N = next, B = back, P = power, 1/2/3 = temperature,
// Space (hold) = iron the fabric, Left Shift (hold) = steam, M = menu, X = exam.
// Without headset: hold right mouse button to look around, W A S D Q E to move.
public class PM_Game : MonoBehaviour
{
    [Tooltip("Graphite + neon look for the table. Uncheck to keep the original blue model.")]
    public bool restyleTable = true;

    [Tooltip("Humanoid characters for the player's body (drag prefabs / FBX here). Rig must be Humanoid.")]
    public GameObject[] avatarCharacters;

    enum State { Avatar, Menu, Learning, ExamIntro, ExamFabric, ExamResult, Quiz, QuizResult }

    PM_Station station;
    public PM_WaterTank Tank { get { return station != null ? station.Tank : null; } }
    PM_Panel panel;
    PM_Panel practicePanel, menuHeader;   // main menu: theory = main panel on the right, practice on the left
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
    PM_Garment form;                 // dress form
    Vector3 formHome, formTask;
    LineRenderer cardLink;

    State state;
    bool recentered;   // the room is aligned to the player's real position once tracking starts
    List<PM_Step> steps;
    bool fullPath;   // the whole learning path (not a single topic)
    int stepIndex;
    bool stepDone;
    bool fibersBasicDone;
    float doneTimer;
    float steamAirTime;
    float dwell;     // seconds the iron stands still on the fabric
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
        // Dress form (tailor's mannequin) front-left of the player; it comes closer for the steaming task.
        formHome = station.PlayerPos + station.Forward * 1.3f - station.Right * 1.35f;
        formTask = station.PlayerPos + station.Forward * 0.5f - station.Right * 1.05f;
        form = PM_Garment.Show("mannequin", formHome, 1.6f, station.PlayerPos + Vector3.up * 1.6f, null, 0.3f, false);
        if (form != null)
        {
            form.spinSpeed = 0f;
            station.SetTarget(PM_Target.DressForm, new List<Transform> { form.transform });
        }
        BuildHotspots();
        PM_Look.PostFX();
        PM_Look.GridFloor(station.PlayerPos, station.Forward, station.Right);
        PM_Look.Dust(station.PlayerPos + station.Forward * 0.8f);
        if (restyleTable) station.Restyle();
        // Studio mirror on the right: the player sees the avatar (body, apron, head).
        Vector3 mpos = station.PlayerPos + station.Right * 1.7f + station.Forward * 0.1f;
        PM_Mirror.Create(mpos, station.PlayerPos - mpos);
        PM_Avatar.Characters = avatarCharacters ?? new GameObject[0];
        PM_Avatar.Init();
        ShowAvatarChoice();

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
    // Two windows side by side: theory on the right (read first in Hebrew), practice and exam on the left.
    void ShowMenu()
    {
        Remember();
        state = State.Menu;
        ClearStepVisuals();
        MenuLayout(true);

        panel.SetContent(PM_Content.TheoryTitle, PM_Content.TheoryBody, "");
        panel.SetAccent(PM_Util.Cyan);
        var theory = new List<KeyValuePair<string, Action>>();
        foreach (PM_Topic t in PM_Content.LearnTopics())
        {
            PM_Topic topic = t;
            theory.Add(Btn(topic.label, () => StartTopic(topic)));
        }
        panel.SetButtonGrid(2, 0, 0.4f, 0.02f, false, theory.ToArray());
        panel.SetButtonGrid(2, -1, 0.4f, -0.24f, true, Btn(PM_Content.BtnAvatar, ShowAvatarChoice), Btn(PM_Content.BtnRecenter, RecenterNow));

        practicePanel.SetContent(PM_Content.PracticeTitle, PM_Content.PracticeBody, "");
        practicePanel.SetAccent(PM_Util.Green);
        var prac = new List<KeyValuePair<string, Action>>();
        for (int i = 0; i < PM_Content.PracticeLabels.Length; i++)
        {
            int index = i;
            prac.Add(Btn(PM_Content.PracticeLabels[i], () => StartPractice(index)));
        }
        practicePanel.SetButtonGrid(4, -1, 0.21f, 0.02f, false, prac.ToArray());
        practicePanel.SetButtonGrid(3, -1, 0.29f, -0.24f, true,
            Btn(PM_Content.BtnExamIron, StartExam),
            Btn(PM_Content.BtnQuizTemp, () => StartQuiz(0)),
            Btn(PM_Content.BtnQuizFamily, () => StartQuiz(1)));
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("menu");
    }

    // Menu: main panel moves right, the practice panel and the title appear. Otherwise one panel in the middle.
    void MenuLayout(bool menu)
    {
        Vector3 head = station.PlayerPos + Vector3.up * 1.6f;
        if (practicePanel == null)
        {
            practicePanel = PM_Panel.Create(null, "PM_PracticePanel", 1000, 700, 52, 34, 50, false);
            menuHeader = PM_Panel.Create(null, "PM_MenuHeader", 1400, 190, 60, 32, 80, false);
            menuHeader.SetContent(PM_Content.MenuTitle, PM_Content.MenuHeaderBody, "");
            menuHeader.SetAccent(PM_Util.Cyan);
        }
        practicePanel.gameObject.SetActive(menu);
        menuHeader.gameObject.SetActive(menu);
        if (menu)
        {
            panel.Place(station.PanelPos + station.Right * 0.53f, head);
            practicePanel.Place(station.PanelPos - station.Right * 0.53f, head);
            menuHeader.Place(station.PanelPos + Vector3.up * 0.5f, head);
        }
        else panel.Place(station.PanelPos, head);
    }

    void StartTopic(PM_Topic t)
    {
        Remember();
        List<PM_Step> all = PM_Content.LearningSteps();
        fullPath = t.steps == null;
        steps = new List<PM_Step>();
        if (fullPath) steps.AddRange(all);
        else foreach (string id in t.steps) foreach (PM_Step st in all) if (st.id == id) steps.Add(st);
        if (steps.Count == 0) return;
        if (t.needsPower) station.MakeReady();
        MenuLayout(false);
        state = State.Learning;
        EnterStep(0);
    }

    void StartPractice(int index)
    {
        Remember();
        List<PM_Step> all = PM_Content.LearningSteps();
        fullPath = false;
        steps = new List<PM_Step>();
        foreach (string id in PM_Content.PracticeSteps) foreach (PM_Step st in all) if (st.id == id) steps.Add(st);
        if (steps.Count == 0) return;
        station.MakeReady();   // practice starts with a hot station (no waiting)
        MenuLayout(false);
        state = State.Learning;
        EnterStep(Mathf.Clamp(index, 0, steps.Count - 1));
    }

    // ---------------- Recenter ----------------
    // Wherever the player stands in the real room, the virtual room moves so that the player is in the floor
    // circle in front of the board, facing it. (The station never ends up outside the play area.)
    bool Recenter()
    {
        XROrigin origin = FindAnyObjectByType<XROrigin>();
        if (origin == null || origin.Camera == null) return false;
        Transform cam = origin.Camera.transform;
        if (cam.localPosition.sqrMagnitude < 0.0001f) return false;   // tracking not started yet
        Vector3 f = cam.forward; f.y = 0;
        if (f.sqrMagnitude > 0.001f) origin.RotateAroundCameraUsingOriginUp(Vector3.SignedAngle(f.normalized, station.Forward, Vector3.up));
        origin.MoveCameraToWorldLocation(new Vector3(station.PlayerPos.x, cam.position.y, station.PlayerPos.z));
        Debug.Log("[PM] Позиция игрока выровнена по станции.");
        return true;
    }

    void RecenterNow()
    {
        Recenter();
        PM_Look.PulseRing(station.PlayerPos + Vector3.up * 0.01f, PM_Util.Cyan);
    }

    // ---------------- Avatar ----------------
    void ShowAvatarChoice()
    {
        Remember();
        avatarPage = 1;
        state = State.Avatar;
        ClearStepVisuals();
        panel.SetContent(PM_Content.AvatarTitle, PM_Content.AvatarBody, "");
        panel.SetAccent(PM_Util.Violet);
        var hands = new List<KeyValuePair<string, Action>>();
        for (int i = 0; i < 4; i++)
        {
            int k = i;
            hands.Add(Btn(PM_Content.HandLabels[i], () => { if (k == 2) ShowChildWarning(); else { PM_Avatar.ChooseHands(k); AvatarPreview(1); } }));
        }
        var tones = new List<KeyValuePair<string, Action>>();
        for (int i = 0; i < 4; i++) { int k = i; tones.Add(Btn(PM_Content.ToneLabels[i], () => PM_Avatar.ChooseTone(k))); }
        var tattoos = new List<KeyValuePair<string, Action>>();
        for (int i = 0; i < 4; i++) { int k = i; tattoos.Add(Btn(PM_Content.TattooLabels[i], () => PM_Avatar.ChooseTattoo(k))); }
        panel.SetButtonGrid(4, -1, 0.22f, 0.04f, false, hands.ToArray());
        panel.SetButtonGrid(4, -1, 0.22f, -0.07f, true, tones.ToArray());
        panel.SetButtonGrid(4, -1, 0.22f, -0.18f, true, tattoos.ToArray());
        panel.SetButtonGrid(1, 0, 0.3f, -0.42f, true, Btn(PM_Content.BtnContinue, ShowBodyChoice));
        AvatarPreview(1);
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("avatar");
    }

    // Choosing a child: a safety rule of the sewing room — children do not operate an ironing station.
    void ShowChildWarning()
    {
        state = State.Avatar;
        panel.SetContent(PM_Content.ChildTitle, PM_Content.ChildBody, "");
        panel.SetAccent(PM_Util.Red);
        panel.SetButtons(Btn(PM_Content.BtnHands, ShowAvatarChoice));
        AvatarPreview(1, 2);
        PM_Look.PulseRing(station.PlayerPos + Vector3.up * 0.01f, PM_Util.Red);
        if (PM_Audio.I != null) { PM_Audio.I.Play("error", 0.8f); PM_Audio.I.PlayVoice("child_warning"); }
    }

    // Full body: a Humanoid character from the Inspector list (PM_Game → Avatar Characters), or hands only.
    void ShowBodyChoice()
    {
        Remember();
        avatarPage = 2;
        state = State.Avatar;
        ClearStepVisuals();
        int n = avatarCharacters != null ? avatarCharacters.Length : 0;
        panel.SetContent(PM_Content.BodyTitle, n > 0 ? PM_Content.BodyBody : PM_Content.BodyNone, "");
        panel.SetAccent(PM_Util.Violet);
        var items = new List<KeyValuePair<string, Action>>();
        items.Add(Btn(PM_Content.BtnHandsOnly, () => PM_Avatar.ChooseCharacter(-1)));
        for (int i = 0; i < n && i < 7; i++)
        {
            if (avatarCharacters[i] == null) continue;
            int k = i;
            string label = avatarCharacters[i].name;
            if (label.Length > 14) label = label.Substring(0, 14);
            items.Add(Btn(label, () => PM_Avatar.ChooseCharacter(k)));
        }
        panel.SetButtonGrid(4, -1, 0.22f, -0.02f, false, items.ToArray());
        panel.SetButtonGrid(2, 1, 0.3f, -0.42f, true, Btn(PM_Content.BtnHands, ShowAvatarChoice), Btn(PM_Content.BtnContinue, ShowMenu));
        AvatarPreview(0);
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("avatar_body");
    }

    void AvatarPreview(int mode, int handOverride = -1)
    {
        if (mode == 0) { PM_Avatar.ShowPreview(0, Vector3.zero, Vector3.zero); return; }
        Vector3 pos = panel.transform.position - panel.transform.right * 0.78f + (mode == 2 ? Vector3.down * 0.4f : Vector3.zero);
        PM_Avatar.ShowPreview(mode, pos, station.PlayerPos + Vector3.up * 1.6f, handOverride);
    }

    // ---------------- Quizzes ----------------
    void StartQuiz(int kind)
    {
        Remember();
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
        panel.SetButtons(Btn(PM_Content.BtnAgain, () => StartQuiz(kind)), Btn(PM_Content.BtnMenu, ShowMenu));
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
            Transform parent = info.target == PM_Target.Iron && station.Iron != null ? station.Iron.transform
                             : info.target == PM_Target.SleeveBoard && station.SleevePivot != null ? station.SleevePivot : station.Table;
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
        CloseCard();   // one window at a time
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

    static readonly string[] StationStepIds = { "learn_intro", "water", "power", "explore_station", "iron", "purge", "temp" };

    void ShowStationHotspots(bool on) { ShowStationHotspots(on, on); }

    void ShowStationHotspots(bool station, bool fibers)
    {
        foreach (PM_Hotspot h in stationHs) if (h != null) h.gameObject.SetActive(station);
        foreach (PM_FiberScreen f in fiberScreens) if (f != null) { f.gameObject.SetActive(fibers); f.SetExpanded(false); }
        if (!station) CloseCard();
    }

    void OpenCard(PM_Hotspot h)
    {
        if (openHs == h && card.gameObject.activeSelf) { CloseCard(); return; }
        foreach (PM_FiberScreen f in fiberScreens) if (f != null) f.SetExpanded(false);   // one window at a time
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
        PM_Avatar.ShowPreview(0, Vector3.zero, Vector3.zero);
        MenuLayout(false);
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
        dwell = 0f;
        doneTimer = 0f;
        steamAirTime = 0f;
        modeClickedThisStep = false;

        highlight.Clear();
        station.RefreshButtons();
        station.ShowTools(s.group == PM_HotspotGroup.Tools || s.toolTask > 0);
        CloseCard();
        // Only what belongs to this step is shown: fiber screens in the fibers step, points of light at the station steps.
        bool stationStep = System.Array.IndexOf(StationStepIds, s.id) >= 0;
        // In the fibers step the main window goes lower, so the fiber screens above stay visible.
        panel.Place(station.PanelPos - (s.group == PM_HotspotGroup.Fibers ? Vector3.up * 0.38f : Vector3.zero), station.PlayerPos + Vector3.up * 1.6f);
        ShowStationHotspots(stationStep, s.group == PM_HotspotGroup.Fibers);

        if (s.target == PM_Target.Fabric)
        {
            if (s.toolTask > 0) SpawnToolFabric(s.fabric, s.toolTask);
            else if (fabric == null || fabric.info.type != s.fabric || fabric.dome > 0f || fabric.sizeZ < 0.2f) { SpawnFabric(s.fabric, stepIndex); AddFabricHotspots(); }
        }
        else RemoveFabric();
        if (form != null) form.transform.position = s.action == PM_Action.SteamOnForm ? formTask : formHome;
        if (s.action == PM_Action.SteamOnForm) SpawnFormFabric(s.fabric);

        if (s.target != PM_Target.None && s.target != PM_Target.Fabric)
            highlight.Show(station.Targets(s.target), PM_Util.Cyan, true);

        panel.SetContent(s.title, s.body, (stepIndex + 1) + "/" + steps.Count);
        Color accent = PM_Util.Cyan;
        if (s.target == PM_Target.Fabric) accent = PM_Util.ModeColor(PM_Content.Fabric(s.fabric).mode);
        else if (s.group == PM_HotspotGroup.Tools) accent = PM_Util.Violet;
        panel.SetAccent(accent);
        // Navigation (right to left): menu, repeat, [new fabric], back, next. Forward is on the left, as Hebrew reads.
        bool last = stepIndex == steps.Count - 1;
        var nav = new List<KeyValuePair<string, Action>>();
        nav.Add(Btn(PM_Content.BtnMenu, ShowMenu));
        nav.Add(Btn(PM_Content.BtnRepeat, RepeatVoice));
        if (s.action == PM_Action.IronFabric || s.action == PM_Action.SteamOnForm) nav.Add(Btn(PM_Content.BtnNewFabric, NewFabric));
        if (history.Count > 0) nav.Add(Btn(PM_Content.BtnBack, GoBack));
        if (!last) nav.Add(Btn(PM_Content.BtnNext, NextStep));
        else if (fullPath) nav.Add(Btn(PM_Content.BtnExamIron, StartExam));
        else nav.Add(Btn(PM_Content.BtnFinish, ShowMenu));
        panel.SetButtonGrid(5, nav.Count - 1, nav.Count > 4 ? 0.18f : 0.22f, nav.ToArray());

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
        else if (state == State.Avatar) PM_Audio.I.PlayVoice("avatar");
    }

    // A fresh piece of the same fabric (after burning it, or to practise again).
    void NewFabric()
    {
        if (state != State.Learning) return;
        PM_Step s = steps[stepIndex];
        stepDone = false;
        dwell = 0f;
        if (s.action == PM_Action.SteamOnForm) { SpawnFormFabric(s.fabric); panel.SetStatus("", Color.white); return; }
        if (s.target != PM_Target.Fabric) return;
        if (s.toolTask > 0) SpawnToolFabric(s.fabric, s.toolTask);
        else { SpawnFabric(s.fabric, stepIndex + UnityEngine.Random.Range(1, 99)); AddFabricHotspots(); }
        panel.SetStatus("", Color.white);
    }

    void NextStep()
    {
        if (state != State.Learning) return;
        if (stepIndex < steps.Count - 1) { Remember(); EnterStep(stepIndex + 1); }
    }

    // ---------------- History: "הקודם" returns to the screen the player really came from ----------------
    readonly List<Action> history = new List<Action>();
    bool restoring, firstScreenShown;
    int avatarPage = 1;

    Action Snapshot()
    {
        switch (state)
        {
            case State.Learning:
            {
                List<PM_Step> st = steps; int idx = stepIndex; bool fp = fullPath;
                return () => { steps = st; fullPath = fp; MenuLayout(false); state = State.Learning; EnterStep(idx); };
            }
            case State.Avatar: return avatarPage == 2 ? (Action)ShowBodyChoice : ShowAvatarChoice;
            case State.Quiz: { int k = quizKind; return () => StartQuiz(k); }
            case State.ExamIntro: case State.ExamFabric: case State.ExamResult: return StartExam;
            default: return ShowMenu;
        }
    }

    void Remember()
    {
        if (restoring) return;
        if (!firstScreenShown) { firstScreenShown = true; return; }
        history.Add(Snapshot());
        if (history.Count > 40) history.RemoveAt(0);
    }

    void GoBack()
    {
        if (history.Count == 0) { ShowMenu(); return; }
        Action a = history[history.Count - 1];
        history.RemoveAt(history.Count - 1);
        restoring = true;
        try { a(); } finally { restoring = false; }
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
            // Ironing tasks wait for "הבא" (free choice); other steps move on by themselves.
            if (s.action != PM_Action.Next && s.action != PM_Action.IronFabric && s.action != PM_Action.SteamOnForm && doneTimer > 2.5f && stepIndex < steps.Count - 1) { Remember(); EnterStep(stepIndex + 1); }
            return;
        }
        PM_Iron iron = station.Iron;
        switch (s.action)
        {
            case PM_Action.SteamOnForm:
                if (HandleSpit(dt)) break;
                ProcessFormSteam(dt);
                if (fabric != null && fabric.Done) { Celebrate(); Complete(PM_Content.StDone); }
                break;
            case PM_Action.FillWater:
            {
                PM_WaterTank t = station.Tank;
                if (t == null) { Complete(PM_Content.StWaterOk); break; }
                if (t.Overfilled) panel.SetStatus(PM_Content.StWaterHigh, PM_Util.Red);
                else if (t.Low) panel.SetStatus(PM_Content.StWaterLow, PM_Util.Yellow);
                else Complete(PM_Content.StWaterOk);
                break;
            }
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
                if (HandleSpit(dt)) break;
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
                if (HandleSpit(dt)) break;
                if (station.Tank != null && station.Tank.Empty && station.Iron != null && station.Iron.TriggerDown) { panel.SetStatus(PM_Content.StNoWater, PM_Util.Yellow); break; }
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

    // A wrinkled cotton dress worn by the dress form: fitted to the form's torso, flared skirt below.
    void SpawnFormFabric(PM_FabricType t)
    {
        RemoveFabric();
        if (form == null) return;
        Vector3 fwd = station.PlayerPos - formTask; fwd.y = 0; fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        // Measure the form: half width (along right) and half depth (along fwd) in 24 height bands.
        const int Bands = 24;
        var rx = new float[Bands]; var rz = new float[Bands];
        Bounds fb = PM_Util.WorldBounds(form.transform);
        Vector3 axis = fb.center;
        bool measured = false;
        foreach (MeshFilter mf in form.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            measured = true;
            Vector3[] vs = mf.sharedMesh.vertices;
            Transform tr = mf.transform;
            for (int i = 0; i < vs.Length; i++)
            {
                Vector3 p = tr.TransformPoint(vs[i]);
                int bi = Mathf.Clamp(Mathf.FloorToInt((p.y - fb.min.y) / fb.size.y * Bands), 0, Bands - 1);
                Vector3 d = p - axis;
                rx[bi] = Mathf.Max(rx[bi], Mathf.Abs(Vector3.Dot(d, right)));
                rz[bi] = Mathf.Max(rz[bi], Mathf.Abs(Vector3.Dot(d, fwd)));
            }
        }
        if (!measured)   // mesh not readable: use the outer size of the form for the upper half
            for (int i = Bands / 2; i < Bands; i++) { rx[i] = fb.extents.x * 0.8f; rz[i] = fb.extents.z * 0.8f; }
        // Torso = the upper bands that are wide (the pole and the stand are thin).
        int top = Bands - 1, bottom = Bands - 1;
        while (top > 0 && rx[top] < 0.07f) top--;
        bottom = top;
        while (bottom > 0 && rx[bottom - 1] > 0.07f) bottom--;
        float band = fb.size.y / Bands;
        // The form's mesh is coarse: smooth the radii (max of neighbours, then average) so the dress has no rings.
        rx = SmoothProfile(rx); rz = SmoothProfile(rz);
        float yTop = fb.min.y + (top + 0.6f) * band, yTorso = fb.min.y + bottom * band;
        if (top - bottom < 3) { yTop = fb.min.y + fb.size.y * 0.92f; yTorso = fb.min.y + fb.size.y * 0.55f; }
        float yHem = Mathf.Max(fb.min.y + 0.1f, yTorso - 0.38f);
        float rxT = Mathf.Max(0.09f, rx[Mathf.Max(bottom, 0)]), rzT = Mathf.Max(0.07f, rz[Mathf.Max(bottom, 0)]);
        System.Func<float, float, Vector3> shape = (u, w) =>
        {
            float y = Mathf.Lerp(yHem, yTop, w);
            float ax, az;
            if (y >= yTorso)
            {
                int bi = Mathf.Clamp(Mathf.FloorToInt((y - fb.min.y) / band), 0, Bands - 1);
                ax = Mathf.Max(rx[bi], 0.06f) * 1.06f + 0.012f;
                az = Mathf.Max(rz[bi], 0.05f) * 1.06f + 0.012f;
                // Narrow shoulders at the very top (the neckline stays open).
                float k = Mathf.InverseLerp(yTop - 0.05f, yTop, y);
                ax = Mathf.Lerp(ax, ax * 0.75f, k); az = Mathf.Lerp(az, az * 0.7f, k);
            }
            else
            {
                float f = (yTorso - y) / Mathf.Max(0.01f, yTorso - yHem);   // skirt flares out
                ax = (rxT * 1.06f + 0.012f) + f * 0.13f;
                az = (rzT * 1.06f + 0.012f) + f * 0.11f;
            }
            float a = u * Mathf.PI * 2f;
            Vector3 world = new Vector3(axis.x, y, axis.z) + right * (Mathf.Cos(a) * ax) + fwd * (Mathf.Sin(a) * az);
            return world - new Vector3(axis.x, 0f, axis.z);
        };
        PM_FabricInfo info = PM_Content.Fabric(t).Clone();
        info.color = new Color(0.55f, 0.72f, 0.92f);   // light blue summer dress
        float circ = Mathf.PI * (rxT + rzT) * 1.2f;
        fabric = PM_Fabric.CreateShaped(info, new Vector3(axis.x, 0f, axis.z), shape, circ, yTop - yHem, 57);
    }

    static float[] SmoothProfile(float[] a)
    {
        int n = a.Length;
        var m = new float[n]; var r = new float[n];
        for (int i = 0; i < n; i++) m[i] = Mathf.Max(a[Mathf.Max(0, i - 1)], Mathf.Max(a[i], a[Mathf.Min(n - 1, i + 1)]));
        for (int i = 0; i < n; i++) r[i] = (m[Mathf.Max(0, i - 1)] + m[i] + m[Mathf.Min(n - 1, i + 1)]) / 3f;
        return r;
    }

    // Vertical steaming: steam from 1–6 cm smooths the garment; touching it does nothing (and is wrong).
    void ProcessFormSteam(float dt)
    {
        if (fabric == null) return;
        PM_Iron iron = station.Iron;
        bool mouse = Time.time - desktopTime < 0.15f;
        if (mouse)
        {
            if (KeyHeld(Key.LeftShift)) fabric.Iron(desktopUV, 0.08f, dt, 1.2f, 0f, 0f, 0.4f);
            panel.SetStatus(KeyHeld(Key.LeftShift) ? string.Format(PM_Content.StProgress, Mathf.RoundToInt(fabric.Progress * 100f)) : PM_Content.StFormSteam, PM_Util.Cyan);
            return;
        }
        if (iron == null || !iron.Held) return;
        Vector3 dir = iron.DownWorld;
        RaycastHit hit = default(RaycastHit);
        bool found = false;
        foreach (RaycastHit h in Physics.RaycastAll(iron.SoleWorld - dir * 0.02f, dir, 0.25f))
            if (h.collider.GetComponent<PM_Fabric>() == fabric) { hit = h; found = true; break; }
        if (!found) return;
        float gap = hit.distance - 0.02f;
        if (gap < 0.008f) { panel.SetStatus(PM_Content.StFormPress, PM_Util.Yellow); iron.Buzz(0.2f, 0.05f); return; }
        if (gap > 0.08f) { panel.SetStatus(PM_Content.StFormFar, PM_Util.Yellow); return; }
        if (!iron.Steaming) { panel.SetStatus(PM_Content.StFormSteam, PM_Util.Yellow); return; }
        fabric.Iron(hit.textureCoord, 0.08f, dt, 1.2f, 0f, 0f, 0.35f);
        panel.SetStatus(string.Format(PM_Content.StProgress, Mathf.RoundToInt(fabric.Progress * 100f)), PM_Util.Cyan);
    }

    void RemoveFabric()
    {
        if (fabric != null) Destroy(fabric.gameObject);
        fabric = null;
        if (garment != null) Destroy(garment.gameObject);
        garment = null;
    }

    // Water / limescale spat by the iron lands on the fabric under it. Returns true while spitting.
    bool HandleSpit(float dt)
    {
        PM_Iron iron = station.Iron;
        if (iron == null || !iron.spitting) return false;
        PM_WaterTank t = station.Tank;
        panel.SetStatus(t != null && t.Overfilled ? PM_Content.StSpitOverfill : PM_Content.StSpitNotReady, PM_Util.Red);
        return true;   // the drops themselves stain the fabric where they land (PM_Drops)
    }

    // Returns true while ironing happens this frame.
    bool ProcessIroning(float dt, bool exam)
    {
        if (fabric == null) return false;
        PM_Iron iron = station.Iron;
        bool mouseIroning = Time.time - desktopTime < 0.15f;
        bool simulated = KeyHeld(Key.Space) || mouseIroning;
        bool touching = simulated || (iron != null && iron.Touching == fabric);
        if (!touching) { dwell = 0f; return false; }

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
        if (station.Mode == 0) { panel.SetStatus(PM_Content.StNoMode, PM_Util.Yellow); return true; }
        // The real soleplate temperature counts, not only the button: a cold iron does not smooth,
        // and after switching to a lower setting the iron is still too hot for a while.
        int mode = station.EffectiveMode;
        if (mode == 0) { panel.SetStatus(string.Format(PM_Content.StHeating, Mathf.RoundToInt(station.IronTemp)), PM_Util.Yellow); return true; }

        // Learning: a ruined fabric must be replaced ("בד חדש").
        if (!exam && fabric.Damage >= 0.25f) { panel.SetStatus(PM_Content.StRuined, PM_Util.Red); return true; }

        float rate = 1f, scorch = 0f, spots = 0f;
        string msg = null;
        Color msgColor = PM_Util.Yellow;
        bool poly = info.type == PM_FabricType.Polyester;
        if (mode < info.mode)
        {
            rate = 0f;
            msg = string.Format(PM_Content.StTooCold, PM_Content.ModeLabel(info.mode));
        }
        else if (mode > info.mode)
        {
            // Too hot burns in the exam AND in learning — that's the lesson.
            rate = exam ? 1f : 0f;
            scorch = (mode - info.mode) * (poly ? 1.6f : 0.9f);
            msg = exam ? (poly ? PM_Content.StMelted : PM_Content.StBurned) : string.Format(PM_Content.StTooHotLearn, PM_Content.ModeLabel(info.mode));
            msgColor = PM_Util.Red;
        }

        // Holding the iron still on one spot: first a warning, then a scorch mark (sensitive fabrics sooner).
        bool still = !simulated || mouseIroning;
        if (still && iron != null && iron.Speed < 0.04f && mode >= info.mode) dwell += dt;
        else dwell = Mathf.Max(0f, dwell - dt * 3f);
        if (mode >= info.mode && dwell > info.dwellSeconds)
        {
            scorch += 0.8f + (dwell - info.dwellSeconds) * 0.6f * (poly ? 1.6f : 1f);
            msg = PM_Content.StDwellBurn;
            msgColor = PM_Util.Red;
        }
        else if (mode >= info.mode && dwell > info.dwellSeconds * 0.6f && msg == null)
        {
            msg = PM_Content.StDwellWarn;
            if (iron != null) iron.Buzz(0.35f, 0.05f);
        }
        if (scorch > 0f)
        {
            if (PM_Audio.I != null && UnityEngine.Random.value < dt * 2f) PM_Audio.I.Play("sizzle", 0.6f);
            if (iron != null) iron.Buzz(0.8f, 0.1f);
        }

        if (rate > 0f || exam || scorch > 0f)
        {
            if (info.steam == PM_Steam.Required && !steaming)
            {
                rate *= 0.25f;
                if (msg == null) msg = PM_Content.StNeedSteam;
            }
            else if (info.steam == PM_Steam.Forbidden && steaming)
            {
                // Water spots on silk — in the exam and in learning.
                spots = 1f;
                rate = exam ? rate : 0f;
                msg = exam ? PM_Content.StSpots : string.Format(PM_Content.StNoSteam, info.name);
                msgColor = PM_Util.Red;
            }
        }
        else if (info.steam == PM_Steam.Forbidden && steaming)
        {
            spots = 1f;
            msg = string.Format(PM_Content.StNoSteam, info.name);
            msgColor = PM_Util.Red;
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
        Remember();
        state = State.ExamIntro;
        ClearStepVisuals();
        station.MakeReady();
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
        panel.SetButtons(1, Btn(PM_Content.BtnMenu, ShowMenu), Btn(PM_Content.BtnNext, () => StartExamFabric(0)));
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("exam_intro");
    }

    void StartExamFabric(int i)
    {
        highlight.Clear();
        station.RefreshButtons();
        state = State.ExamFabric;
        examIndex = i;
        examTime = 40f;
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
        bool ironing = HandleSpit(dt) || ProcessIroning(dt, true);
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
        panel.SetButtons(Btn(PM_Content.BtnAgain, StartExam), Btn(PM_Content.BtnMenu, ShowMenu));
        if (PM_Audio.I != null) PM_Audio.I.Play(examScore >= 2 ? "ding" : "error", 0.8f);
    }

    // ---------------- Loop ----------------
    void Update()
    {
        float dt = Time.deltaTime;
        if (!recentered && Time.timeSinceLevelLoad > 1f && UnityEngine.XR.XRSettings.isDeviceActive) recentered = Recenter();
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
            else if (state == State.Avatar) ShowMenu();
            else if (state == State.ExamIntro) StartExamFabric(0);
            else if (state == State.Quiz) NextQuiz();
        }
        if (KeyDown(Key.B)) GoBack();
        if (KeyDown(Key.M)) ShowMenu();
        if (KeyDown(Key.R)) RecenterNow();
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
