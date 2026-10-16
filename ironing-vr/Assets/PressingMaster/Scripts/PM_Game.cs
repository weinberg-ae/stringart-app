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

    [Tooltip("If a Humanoid avatar appears in the scene (for example loaded by the Genies Avatar SDK), it becomes the player's body.")]
    public bool useSceneAvatar = true;

    enum State { Avatar, Menu, Learning, ExamIntro, ExamFabric, ExamQuiz, ExamResult, Quiz, QuizResult }

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
    readonly List<int> examPoints = new List<int>();
    float examTime;
    bool examFinishedFabric, examSuccess;
    const float ExamSeconds = 75f;
    const int ExamQuestions = 8;   // bonus questions; together they are worth 100 points
    PM_ScoreTube tube;
    class ExamQ { public int kind; public PM_HotspotInfo fiber; public int tool; public int[] order; }
    readonly List<ExamQ> examQuiz = new List<ExamQ>();
    int examQuizIndex, examQuizRight;
    bool examQuizAnswered;

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
        PM_Rewards.Apply(station);

        highlight = PM_Highlight.Create("PM_Highlight");
        panel = PM_Panel.Create(null);
        panel.Place(station.PanelPos, station.PlayerPos + Vector3.up * 1.6f);
        cardHighlight = PM_Highlight.Create("PM_CardHighlight");
        PM_Narrator.Create(panel.transform, new Vector3(-0.6f, 0.22f, -0.02f));
        card = PM_Panel.Create(null, "PM_InfoCard", 620, 520, 40, 28, 36, false);
        card.gameObject.SetActive(false);
        // Dress form (tailor's mannequin) wearing a satin gown. At home it stands front-left, out of the way;
        // for the steaming task it comes right next to the narrow end of the board (within reach, not touching it).
        Bounds bb = station.BoardBounds;
        if (bb.size.sqrMagnitude < 1e-4f && station.Table != null) bb = PM_Util.WorldBounds(station.Table);
        float boardLeft = 0f, boardFwd = 0f;
        for (int i = 0; i < 4; i++)
        {
            Vector3 c = new Vector3((i & 1) == 0 ? bb.min.x : bb.max.x, bb.center.y, (i & 2) == 0 ? bb.min.z : bb.max.z);
            boardLeft = Mathf.Min(boardLeft, Vector3.Dot(c - station.PlayerPos, station.Right));
        }
        boardFwd = Vector3.Dot(bb.center - station.PlayerPos, station.Forward);
        boardLeft = Mathf.Clamp(boardLeft, -1.2f, -0.3f);
        formTask = station.PlayerPos + station.Forward * Mathf.Clamp(boardFwd - 0.02f, 0.30f, 0.75f) + station.Right * (boardLeft - 0.31f);
        formHome = station.PlayerPos + station.Forward * 1.15f + station.Right * (boardLeft - 0.70f);
        form = PM_Garment.Show("mannequin", formHome, 1.6f, station.PlayerPos + Vector3.up * 1.6f, null, 0.3f, false);
        if (form != null)
        {
            form.spinSpeed = 0f;
            FaceForm();
            gown = PM_Gown.Create(form.transform);
            if (gown != null) { gown.Wrinkle(1f); gown.gameObject.SetActive(false); }
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
        PM_Avatar.AdoptSceneCharacter = useSceneAvatar;
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
        panel.SetAccent(PM_Util.Fuchsia);    // theory: fuchsia pink
        var theory = new List<KeyValuePair<string, Action>>();
        foreach (PM_Topic t in PM_Content.LearnTopics())
        {
            PM_Topic topic = t;
            theory.Add(Btn(topic.label, () => StartTopic(topic)));
        }
        panel.SetButtonGrid(2, 0, 0.4f, 0.02f, false, theory.ToArray());
        ShowPracticeMain();
        BuildWorldButtons();
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("menu");
    }

    // Practice window: the fabrics, one "practical exercises" group, the quizzes and a big glowing exam button.
    void ShowPracticeMain()
    {
        practicePanel.SetContent(PM_Content.PracticeTitle, PM_Content.PracticeBody, "");
        practicePanel.SetAccent(PM_Util.Green);
        practicePanel.SetButtonGrid(1, -1, 0.62f, 0.04f, false, Btn(PM_Content.BtnPracticeFabrics, ShowPracticeFabrics));
        practicePanel.SetButtonGrid(1, -1, 0.62f, -0.07f, true, Btn(PM_Content.BtnPracticeTools, ShowPracticeGroup));
        practicePanel.SetButtonGrid(2, -1, 0.36f, -0.18f, true,
            Btn(PM_Content.BtnQuizTemp, () => StartQuiz(0)),
            Btn(PM_Content.BtnQuizFamily, () => StartQuiz(1)));
        practicePanel.AddPillButton(PM_Content.BtnExamIron, StartExam, new Vector3(0f, -0.29f, -0.03f), 0.46f, 0.085f, PM_Util.Gold);
    }

    // Ironing practice by fabric: cotton, linen, wool, silk, polyester.
    void ShowPracticeFabrics()
    {
        practicePanel.SetContent(PM_Content.BtnPracticeFabrics, PM_Content.PracticeFabricsBody, "");
        practicePanel.SetAccent(PM_Util.Green);
        var items = new List<KeyValuePair<string, Action>>();
        for (int i = 0; i < 5; i++) { int index = i; items.Add(Btn(PM_Content.PracticeLabels[i], () => StartPractice(index))); }
        practicePanel.SetButtonGrid(3, -1, 0.26f, 0.04f, false, items.ToArray());
        practicePanel.SetButtonGrid(1, -1, 0.3f, -0.3f, true, Btn(PM_Content.BtnBack, ShowPracticeMain));
    }

    // Practice with the pressing tools: collar, chest, steaming the dress on the form.
    void ShowPracticeGroup()
    {
        practicePanel.SetContent(PM_Content.BtnPracticeTools, PM_Content.PracticeGroupBody, "");
        practicePanel.SetAccent(PM_Util.Green);
        int[] idx = { 5, 6, 7 };
        var items = new List<KeyValuePair<string, Action>>();
        foreach (int i in idx) { int index = i; items.Add(Btn(PM_Content.PracticeLabels[i], () => StartPractice(index))); }
        practicePanel.SetButtonGrid(3, -1, 0.28f, 0.04f, false, items.ToArray());
        practicePanel.SetButtonGrid(1, -1, 0.3f, -0.3f, true, Btn(PM_Content.BtnBack, ShowPracticeMain));
    }

    // "בחירת דמות" above the mirror and "מרכוז מקום" in the circle on the floor (they do not belong to the menu).
    PM_Panel mirrorBtn, resetBtn;
    float resetArmed = -10f;

    void ResetRewards()
    {
        if (Time.time - resetArmed > 5f)
        {
            resetArmed = Time.time;
            panel.SetStatus(PM_Content.StResetConfirm, PM_Util.Yellow);
            return;
        }
        resetArmed = -10f;
        PM_Rewards.ResetAll(station);
        panel.SetStatus(PM_Content.StResetDone, PM_Util.Green);
    }
    GameObject floorBtn;
    void BuildWorldButtons()
    {
        Vector3 head = station.PlayerPos + Vector3.up * 1.6f;
        if (mirrorBtn == null)
        {
            mirrorBtn = PM_Panel.Create(null, "PM_MirrorButton", 520, 120, 40, 28, 30, false);
            mirrorBtn.SetContent("", "", "");
            mirrorBtn.SetAccent(PM_Util.Violet);
            mirrorBtn.AddPillButton(PM_Content.BtnAvatar, ShowAvatarChoice, new Vector3(0f, 0f, -0.03f), 0.44f, 0.085f, PM_Util.Violet);
            Vector3 mpos = station.PlayerPos + station.Right * 1.7f + station.Forward * 0.1f;
            mirrorBtn.Place(mpos + Vector3.up * 2.12f, head);
            // beside it: reset the achievements (asks for a second press)
            resetBtn = PM_Panel.Create(null, "PM_ResetButton", 420, 120, 40, 28, 30, false);
            resetBtn.SetContent("", "", "");
            resetBtn.SetAccent(PM_Util.Violet);
            resetBtn.AddPillButton(PM_Content.BtnResetRewards, ResetRewards, new Vector3(0f, 0f, -0.03f), 0.34f, 0.075f, PM_Util.Violet);
            Vector3 toHead = head - mpos; toHead.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, toHead.sqrMagnitude > 1e-4f ? toHead.normalized : -station.Forward);
            resetBtn.Place(mpos + Vector3.up * 2.12f + side * 0.48f, head);
        }
        if (floorBtn == null)
        {
            floorBtn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floorBtn.name = "PM_RecenterButton";
            floorBtn.transform.position = station.PlayerPos + Vector3.up * 0.006f;
            floorBtn.transform.localScale = new Vector3(0.34f, 0.004f, 0.34f);
            var m = PM_Util.NeonMaterial(PM_Util.Cyan, 0.6f);
            floorBtn.GetComponent<Renderer>().sharedMaterial = m;
            var click = PM_Clickable.Add(floorBtn, RecenterNow);
            click.onHover = h => m.SetColor("_EmissionColor", PM_Util.Cyan * (h ? 2.2f : 0.6f));
            // label lying on the floor, readable from where the player stands
            var lab = new GameObject("Label", typeof(RectTransform));
            lab.transform.SetParent(null, false);
            lab.transform.position = station.PlayerPos + Vector3.up * 0.012f + station.Forward * 0.25f;
            lab.transform.rotation = Quaternion.LookRotation(Vector3.down, station.Forward);
            var canvas = lab.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = lab.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 80);
            rt.localScale = Vector3.one * 0.001f;
            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
            t.transform.SetParent(lab.transform, false);
            t.rectTransform.sizeDelta = new Vector2(400, 80);
            PM_Panel.Prepare(t);
            t.fontSize = 44; t.alignment = TMPro.TextAlignmentOptions.Center; t.color = PM_Util.Cyan; t.raycastTarget = false;
            t.text = PM_Hebrew.VisualLine(PM_Content.BtnRecenter);
        }
    }

    // While an info window of a point of light or a big fiber screen is open, the main windows step aside
    // (no window shows through another one).
    bool menuShown;
    void UpdateCover()
    {
        bool cover = openHs != null && card != null && card.gameObject.activeSelf;
        if (!cover) foreach (PM_FiberScreen f in fiberScreens) if (f != null && f.gameObject.activeSelf && f.Expanded) { cover = true; break; }
        if (panel != null && panel.gameObject.activeSelf == cover) panel.gameObject.SetActive(!cover);
        if (practicePanel != null && practicePanel.gameObject.activeSelf != (menuShown && !cover)) practicePanel.gameObject.SetActive(menuShown && !cover);
        if (menuHeader != null && menuHeader.gameObject.activeSelf != (menuShown && !cover)) menuHeader.gameObject.SetActive(menuShown && !cover);
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
        menuShown = menu;
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
        if (PM_Avatar.IsToon) { ShowGenderChoice(); return; }
        if (PM_Avatar.Character == PM_Avatar.SceneCharacter && PM_Style.LoaderPresent()) { ShowStyleChoice(); return; }
        ShowHandsChoice();
    }

    // ---- first: woman, man or child (a child gets the safety rule of the sewing room) ----
    void ShowGenderChoice()
    {
        Remember();
        avatarPage = 5;
        state = State.Avatar;
        ClearStepVisuals();
        PM_Toon.Load();
        panel.SetContent(PM_Content.GenderTitle, PM_Content.GenderBody, "");
        panel.SetAccent(PM_Util.Violet);
        panel.SetButtonGrid(3, -1, 0.28f, -0.06f, false,
            Btn(PM_Content.GenderLabels[0], () => PickGender(true)),
            Btn(PM_Content.GenderLabels[1], () => PickGender(false)),
            Btn(PM_Content.GenderLabels[2], ShowChildWarning));
        AvatarPreview(3);
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("avatar");
    }

    void PickGender(bool woman)
    {
        PM_Toon.Load();
        if (PM_Toon.Woman != woman || !PM_Avatar.IsToon)
            for (int i = 0; i < PM_Toon.Presets.Length; i++)
                if (PM_Toon.Presets[i].woman == woman) { PM_Toon.ApplyPreset(i); break; }
        ShowToonChoice(0);
    }

    // ---- our cartoon characters: tabs (ready characters, hair, clothes, accessories, skin & eyes) ----
    int toonTab;
    void ShowToonChoice(int tab) { ShowToonChoice(tab, true); }

    void ShowToonChoice(int tab, bool remember)
    {
        if (remember) Remember();
        toonTab = tab;
        avatarPage = 4;
        state = State.Avatar;
        ClearStepVisuals();
        PM_Toon.Load();
        panel.SetContent(PM_Content.ToonTitle, "", "");   // no body text: the rows of choices fill the screen
        panel.SetAccent(PM_Util.Violet);
        var tabs = new List<KeyValuePair<string, Action>>();
        for (int i = 0; i < PM_Content.ToonTabs.Length; i++)
        {
            int k = i;
            tabs.Add(Btn((i == tab ? "• " : "") + PM_Content.ToonTabs[i], () => ShowToonChoice(k)));
        }
        panel.SetButtonGrid(5, tab, 0.18f, 0.16f, false, tabs.ToArray());
        float[] y = tab == 2 ? new[] { 0.05f, -0.05f, -0.15f, -0.25f, -0.35f } : new[] { 0.04f, -0.07f, -0.18f, -0.29f };
        int row = 0;
        Action<string[], int, int, Action<int>> Row = (labels, from, count, pick) =>
        {
            var items = new List<KeyValuePair<string, Action>>();
            for (int i = from; i < from + count && i < labels.Length; i++)
            {
                int k = i;
                items.Add(Btn(labels[i], () => { pick(k); ShowToonChoice(toonTab, false); }));
            }
            int per = Mathf.Max(items.Count, 1);
            float w = Mathf.Min(0.2f, (1.05f - (per - 1) * 0.04f) / per);   // the row fits the screen width
            if (row < y.Length) panel.SetButtonGrid(per, -1, w, y[row], true, items.ToArray());
            row++;
        };
        Func<string[], int, string[]> Mark = (labels, sel) =>
        {
            var m = new string[labels.Length];
            for (int i = 0; i < labels.Length; i++) m[i] = (i == sel ? "• " : "") + labels[i];
            return m;
        };
        switch (tab)
        {
            case 0:
                {
                    // only the characters of the chosen gender
                    var idx = new List<int>();
                    for (int i = 0; i < PM_Toon.Presets.Length; i++) if (PM_Toon.Presets[i].woman == PM_Toon.Woman) idx.Add(i);
                    var names = new string[idx.Count];
                    for (int i = 0; i < names.Length; i++) names[i] = PM_Toon.Presets[idx[i]].name;
                    Row(names, 0, names.Length, i => PM_Toon.ApplyPreset(idx[i]));
                    panel.SetButtonGrid(1, -1, 0.3f, y[row++], true, Btn(PM_Content.BtnChangeGender, ShowGenderChoice));
                    break;
                }
            case 1:
                Row(Mark(PM_Content.ToonHair, PM_Toon.Hair), 0, 5, i => { PM_Toon.Hair = i; PM_Toon.Save(); });
                Row(Mark(PM_Content.ToonHair, PM_Toon.Hair), 5, 4, i => { PM_Toon.Hair = i; PM_Toon.Save(); });
                Row(Mark(PM_Content.ToonHairColors, PM_Toon.HairColor), 0, 4, i => { PM_Toon.HairColor = i; PM_Toon.Save(); });
                Row(Mark(PM_Content.ToonHairColors, PM_Toon.HairColor), 4, 4, i => { PM_Toon.HairColor = i; PM_Toon.Save(); });
                break;
            case 2:
                Row(Mark(PM_Content.ToonTops, PM_Toon.Top), 0, 5, i => { PM_Toon.Top = i; PM_Toon.Save(); });
                Row(Mark(PM_Content.ToonTops, PM_Toon.Top), 5, 5, i => { PM_Toon.Top = i; PM_Toon.Save(); });
                Row(Mark(PM_Content.ToonTopColors, PM_Toon.TopColor), 0, 8, i => { PM_Toon.TopColor = i; PM_Toon.Save(); });
                Row(Mark(PM_Content.ToonBottoms, PM_Toon.Bottom), 0, 6, i => { PM_Toon.Bottom = i; PM_Toon.Save(); });
                Row(Mark(PM_Content.ToonBottomColors, PM_Toon.BottomColor), 0, 6, i => { PM_Toon.BottomColor = i; PM_Toon.Save(); });
                break;
            case 3:
                {
                    var accs = new string[PM_Content.ToonAccs.Length];
                    for (int i = 0; i < accs.Length; i++) accs[i] = (PM_Toon.HasAcc(i) ? "• " : "") + PM_Content.ToonAccs[i];
                    if (PM_Toon.HatAllowed)
                    {
                        Row(Mark(PM_Content.ToonHats, PM_Toon.Hat), 0, 4, i => { PM_Toon.Hat = i; PM_Toon.Save(); });
                        Row(Mark(PM_Content.ToonTopColors, PM_Toon.HatColor), 0, 8, i => { PM_Toon.HatColor = i; PM_Toon.Save(); });
                    }
                    else
                    {
                        // this hairstyle does not take a hat: no hat buttons at all
                        if (PM_Toon.Hat != 0) { PM_Toon.Hat = 0; PM_Toon.Save(); }
                        row += 2;
                    }
                    Row(accs, 0, 6, i => PM_Toon.ToggleAcc(i));
                    Row(Mark(PM_Content.ToonShoes, PM_Avatar.Shoes), 0, 3, i => PM_Avatar.ChooseShoes(i));
                    break;
                }
            default:
                Row(Mark(PM_Content.ToonSkins, PM_Toon.Skin), 0, 6, i => { PM_Toon.Skin = i; PM_Toon.Save(); });
                Row(Mark(PM_Content.ToonEyes, PM_Toon.Eyes), 0, 5, i => { PM_Toon.Eyes = i; PM_Toon.Save(); });
                Row(Mark(PM_Content.TattooLabels, PM_Avatar.Tattoo), 0, 4, i => PM_Avatar.ChooseTattoo(i));
                break;
        }
        panel.SetButtonGrid(3, 2, 0.26f, tab == 2 ? -0.47f : -0.42f, true,
            Btn(PM_Content.BtnBackToGender, ShowGenderChoice),
            Btn(PM_Content.BtnHandsOnly, () => { PM_Avatar.ChooseCharacter(-1); ShowHandsChoice(); }),
            Btn(PM_Content.BtnContinue, ShowMenu));
        if (tab == 3 && !PM_Toon.HatAllowed) panel.SetStatus(PM_Content.ToonNoHat, new Color(1f, 0.63f, 0.25f));
        else if (tab == 2 && PM_Toon.OnePiece) panel.SetStatus(PM_Content.ToonOnePiece, PM_Util.Cyan);
        AvatarPreview(3);
        if (remember && PM_Audio.I != null && tab == 0) PM_Audio.I.PlayVoice("avatar");
    }

    // A loaded avatar (Genies): outfit, skin tone, hair and a forearm tattoo. Seen in the studio mirror.
    void ShowStyleChoice()
    {
        Remember();
        avatarPage = 3;
        state = State.Avatar;
        ClearStepVisuals();
        panel.SetContent(PM_Content.StyleTitle, PM_Content.StyleBody, "");
        panel.SetAccent(PM_Util.Violet);
        var rows = new List<KeyValuePair<string, Action>>[4];
        for (int r = 0; r < 4; r++) rows[r] = new List<KeyValuePair<string, Action>>();
        for (int i = 0; i < PM_Style.Count; i++)
        {
            int k = i;
            rows[0].Add(Btn(PM_Content.LookLabels[i], () => PM_Style.ChooseLook(k)));
            rows[1].Add(Btn(PM_Content.SkinLabels[i], () => PM_Style.ChooseSkin(k)));
            rows[2].Add(Btn(PM_Content.HairLabels[i], () => PM_Style.ChooseHair(k)));
            rows[3].Add(Btn(PM_Content.StyleTattooLabels[i], () => PM_Style.ChooseTattoo(k)));
        }
        for (int r = 0; r < 4; r++) panel.SetButtonGrid(5, -1, 0.17f, 0.05f - r * 0.11f, r > 0, rows[r].ToArray());
        panel.SetButtonGrid(2, 1, 0.3f, -0.42f, true,
            Btn(PM_Content.BtnHandsOnly, () => { PM_Avatar.ChooseCharacter(-1); ShowHandsChoice(); }),
            Btn(PM_Content.BtnContinue, ShowMenu));
        PM_Avatar.ShowPreview(0, Vector3.zero, Vector3.zero);
        PM_Style.ParkNextToMenu(panel.transform.position - panel.transform.right * 0.95f, station.PlayerPos + Vector3.up * 1.6f);
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("avatar");
    }

    void ShowHandsChoice()
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
        panel.SetButtons(Btn(PM_Avatar.IsToon ? PM_Content.BtnBackToChoice : PM_Content.BtnHands, PM_Avatar.IsToon ? (Action)ShowGenderChoice : ShowHandsChoice));
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
        bool genies = PM_Avatar.SceneCharacterAvailable;
        panel.SetContent(PM_Content.BodyTitle, n > 0 || genies ? PM_Content.BodyBody : PM_Content.BodyNone, "");
        panel.SetAccent(PM_Util.Violet);
        var items = new List<KeyValuePair<string, Action>>();
        if (genies) items.Add(Btn(PM_Content.BtnSceneAvatar, () => PM_Avatar.ChooseCharacter(PM_Avatar.SceneCharacter)));
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
        panel.SetButtonGrid(2, 1, 0.3f, -0.42f, true, Btn(PM_Content.BtnHands, ShowHandsChoice), Btn(PM_Content.BtnContinue, ShowMenu));
        AvatarPreview(0);
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("avatar_body");
    }

    void AvatarPreview(int mode, int handOverride = -1)
    {
        if (mode == 0) { PM_Avatar.ShowPreview(0, Vector3.zero, Vector3.zero); return; }
        Vector3 pos = panel.transform.position - panel.transform.right * (mode == 3 ? 0.85f : 0.78f) + (mode == 2 ? Vector3.down * 0.4f : mode == 3 ? Vector3.down * 0.5f : Vector3.zero);
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
        // Points of the pressing tools: one row in the air behind the board (the tools themselves wait on the
        // shelf under it); a click puts that tool alone on the board, so the board is never crowded.
        List<PM_HotspotInfo> toolInfos = PM_Content.ToolHotspots();
        Vector3 rowC = new Vector3(station.BoardCenter.x, station.BoardTopY, station.BoardCenter.z) + station.Forward * 0.42f + Vector3.up * 0.38f;
        for (int i = 0; i < toolInfos.Count; i++)
        {
            PM_HotspotInfo info = toolInfos[i];
            List<Transform> parts = station.Targets(info.target);
            if (parts.Count == 0) continue;
            Vector3 pos = rowC + station.Right * (((toolInfos.Count - 1) * 0.5f - i) * 0.16f);   // first one on the right (Hebrew order)
            toolHs.Add(MakeHotspot(info, pos, station.Table, parts));
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
        SetToolHotspots(false);
        foreach (PM_FiberScreen f in fiberScreens) if (f != null) { f.gameObject.SetActive(fibers); f.SetExpanded(false); }
        if (!station) CloseCard();
    }

    // The points of light of the pressing tools: only in the tools lesson (not in the menu, not in the exercises).
    void SetToolHotspots(bool on)
    {
        foreach (PM_Hotspot h in toolHs)
            if (h != null) h.gameObject.SetActive(on && (h.info.target != PM_Target.Clapper || PM_Rewards.Has(PM_Rewards.Clapper)));   // the clapper only once won
    }

    void OpenCard(PM_Hotspot h)
    {
        if (openHs == h && card.gameObject.activeSelf) { CloseCard(); return; }
        foreach (PM_FiberScreen f in fiberScreens) if (f != null) f.SetExpanded(false);   // one window at a time
        openHs = h;
        spitCardReason = 0;
        h.SetVisited(true);
        // a pressing tool: it alone goes up onto the board (the shoulder presser stays on the floor, it is tall)
        if (h.info.group == PM_HotspotGroup.Tools)
            station.ShowTools(false, h.info.target == PM_Target.Shoulder ? PM_Target.None : h.info.target);
        card.gameObject.SetActive(true);
        card.SetContent(h.info.title, h.info.body, "");
        card.SetAccent(h.info.accent);
        card.SetButtons(Btn(PM_Content.BtnClose, CloseCard), Btn(PM_Content.BtnRepeat, () => { if (PM_Audio.I != null) PM_Audio.I.PlayVoice("hs_" + h.info.id); }));
        Vector3 head = Camera.main != null ? Camera.main.transform.position : station.PlayerPos + Vector3.up * 1.6f;
        card.Place(ArcSlot(h.transform.position), head);
        if (cardLink == null)
        {
            cardLink = PM_Util.Line(null, "PM_CardLink", new[] { Vector3.zero, Vector3.zero }, PM_Util.Cyan, 0.003f, false);
            cardLink.useWorldSpace = true;
        }
        cardLink.gameObject.SetActive(false);   // no light thread from the point to its window (it looked like a stray ray)
        cardHighlight.Clear();
        if (h.info.target != PM_Target.Fabric && h.parts.Count > 0) cardHighlight.Show(h.parts, h.info.accent, false);
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("hs_" + h.info.id);
    }

    // Info windows of the points of light open on a half circle around the player ("half sun"),
    // in the direction of their point, so they never pile up; the main window hides meanwhile.
    Vector3 ArcSlot(Vector3 target)
    {
        Vector3 d = target - station.PlayerPos; d.y = 0f;
        if (d.sqrMagnitude < 1e-4f) d = station.Forward;
        float ang = Vector3.SignedAngle(station.Forward, d.normalized, Vector3.up);
        ang = Mathf.Clamp(Mathf.Round(ang / 20f) * 20f, -60f, -20f);   // left of the iron's water hose and the water tank window (right side)
        Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * station.Forward;
        return station.PlayerPos + dir * 1.05f + Vector3.up * 1.42f;
    }

    // Where info windows open: right of the main screen and a little in front of it.
    Vector3 CardSlot()
    {
        return station.PanelPos + station.Right * 0.88f - station.Forward * 0.25f + Vector3.down * 0.04f;
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
        if (gown != null) gown.gameObject.SetActive(false);
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
        station.ShowTools(false, s.toolTask == 1 ? PM_Target.PointPresser : s.toolTask == 2 ? PM_Target.ChestPad : PM_Target.None);   // tools lesson too: on the shelf until one is chosen
        CloseCard();
        // Only what belongs to this step is shown: fiber screens in the fibers step, points of light at the station steps.
        bool stationStep = System.Array.IndexOf(StationStepIds, s.id) >= 0;
        // In the fibers step the main window goes lower, so the fiber screens above stay visible.
        panel.Place(station.PanelPos + (s.group == PM_HotspotGroup.Fibers ? Vector3.up * 0.06f : Vector3.zero), station.PlayerPos + Vector3.up * 1.6f);
        ShowStationHotspots(stationStep, s.group == PM_HotspotGroup.Fibers);
        SetToolHotspots(s.group == PM_HotspotGroup.Tools);

        if (s.target == PM_Target.Fabric)
        {
            if (s.toolTask > 0) SpawnToolFabric(s.fabric, s.toolTask);
            else if (fabric == null || fabric.info.type != s.fabric || fabric.dome > 0f || fabric.sizeZ < 0.2f) { SpawnFabric(s.fabric, stepIndex); AddFabricHotspots(); }
        }
        else RemoveFabric();
        if (form != null) { form.transform.position = s.action == PM_Action.SteamOnForm ? formTask : formHome; FaceForm(); }
        // the gown is on the form only in the steaming exercise; otherwise the form stands bare
        if (gown != null) gown.gameObject.SetActive(s.action == PM_Action.SteamOnForm);
        if (s.action == PM_Action.SteamOnForm && gown != null) gown.Wrinkle(1f);

        // (the dress form is not lit up: the glow hid the fabric)
        if (s.target != PM_Target.None && s.target != PM_Target.Fabric && s.target != PM_Target.DressForm)
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
        // behind the board (beyond its far edge), a little above it: out of the iron's way and not over the fabric
        Vector3 c = fabric.transform.position + Vector3.up * 0.30f + station.Forward * 0.42f;
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
        if (s.action == PM_Action.SteamOnForm) { if (gown != null) gown.Wrinkle(1f); panel.SetStatus("", Color.white); return; }
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
            case State.Avatar: { int t = toonTab; return avatarPage == 2 ? (Action)ShowBodyChoice : avatarPage == 3 ? (Action)ShowStyleChoice : avatarPage == 4 ? (Action)(() => ShowToonChoice(t)) : avatarPage == 5 ? ShowGenderChoice : (Action)ShowHandsChoice; }
            case State.Quiz: { int k = quizKind; return () => StartQuiz(k); }
            case State.ExamIntro: case State.ExamFabric: case State.ExamQuiz: case State.ExamResult: return StartExam;
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
                if (gown != null && gown.Done) { gown.FinishSmooth(); Celebrate(); Complete(PM_Content.StDone); }
                else if (gown == null) Complete(PM_Content.StDone);
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
                bool steaming = (iron != null && iron.Steaming && iron.Touching == null) || DeskKey(Key.LeftShift);
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
        Vector3 gpos = GarmentPos();
        string label = state == State.Learning ? info.garment : "?";
        garment = PM_Garment.Show(t.ToString().ToLower(), gpos, 0.75f, station.PlayerPos + Vector3.up * 1.6f, label, info.smoothness, true);
    }

    // Fabric lying on a pressing tool: 1 = shirt collar on the point presser, 2 = dress bodice on the ham.
    // The floating garment of a fabric: to the right, behind the board, between the main screen and the water tank
    // (away from the dress form on the left).
    Vector3 GarmentPos()
    {
        return new Vector3(station.PlayerPos.x, station.PlayerPos.y + 0.8f, station.PlayerPos.z) + station.Right * 0.95f + station.Forward * 1.35f;
    }

    void SpawnToolFabric(PM_FabricType t, int task)
    {
        RemoveFabric();
        PM_FabricInfo info = PM_Content.Fabric(t);
        List<Transform> tool = station.Targets(task == 1 ? PM_Target.PointPresser : PM_Target.ChestPad);
        if (tool.Count == 0) { SpawnFabric(t, 7); return; }
        Transform tr = tool[0];
        if (task == 1)
        {
            // A real collar (a curved band with pointed ends) lying on the collar board. It is not flat, so the
            // other hand can take it and turn / move it on the board to reach every part.
            Quaternion rot = tr.rotation;
            float sc = tr.lossyScale.x;
            Vector3 c0 = tr.TransformPoint(new Vector3(-0.11f, 0.143f, 0f));
            float R = 0.11f * sc, W = 0.07f * sc;
            System.Func<float, float, Vector3> shape = (u, w) =>
            {
                float a = Mathf.Lerp(-66f, 66f, u) * Mathf.Deg2Rad;
                float r = R - W * 0.5f + W * w;
                float tip = Mathf.Max(0f, Mathf.Abs(u - 0.5f) * 2f - 0.85f) / 0.15f;     // the collar points
                a += Mathf.Sign(u - 0.5f) * tip * w * 0.18f;
                float y = (0.003f + 0.007f * Mathf.Sin(Mathf.PI * w) + 0.004f * w) * sc;   // a soft roll along the fold line
                return rot * new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            };
            fabric = PM_Fabric.CreateShaped(info, c0, shape, 0.26f * sc, W, 41);
            PM_CollarHandle.Attach(fabric, tr);
        }
        else if (!SpawnCup(info, tr))
        {
            // (fallback if the cup file is missing) A bra cup: a half-moon shaped shell of thin fabric — a rounded lower edge
            // (the underwire line) and an almost straight upper edge with points at both ends — lying on the dome
            // of the cup pad and following its curve. One seam across, a firmer band along the lower edge.
            float sc = tr.lossyScale.x;
            Quaternion rot = tr.rotation;
            Vector3 c0 = tr.TransformPoint(new Vector3(-0.10f, 0.205f, 0f));   // centre of the dome of the pad (tools3d.py CUP_C)
            Vector3 R = new Vector3(0.085f, 0.105f, 0.080f) * sc;               // its radii (CUP_R)
            float A = 0.074f * sc, B = 0.066f * sc, C = 0.018f * sc;
            System.Func<float, float, Vector3> shape = (u, w) =>
            {
                float X = Mathf.Lerp(-A, A, u);
                float k = Mathf.Sqrt(Mathf.Max(0f, 1f - (X / A) * (X / A)));
                float zLow = -B * k, zUp = C * k;                           // lower curved edge .. upper edge
                float Z = Mathf.Lerp(zLow, zUp, w) + 0.012f * sc;
                float q = 1f - (X / R.x) * (X / R.x) - (Z / R.z) * (Z / R.z);
                float Y = R.y * Mathf.Sqrt(Mathf.Max(0.02f, q));           // on the dome
                float tx = Mathf.Clamp01(X / A);
                float off = 0.004f * sc + 0.016f * sc * tx * tx                                  // over the join with the tail
                          + 0.0025f * sc * Mathf.Clamp01(1f - w / 0.12f)          // band at the lower edge
                          + 0.0015f * sc * Mathf.Exp(-Mathf.Pow((w - 0.5f) / 0.04f, 2f));      // seam across
                Vector3 n = new Vector3(X / (R.x * R.x), Y / (R.y * R.y), Z / (R.z * R.z)).normalized;
                return rot * (new Vector3(X, Y, Z) + n * off);
            };
            fabric = PM_Fabric.CreateShaped(info, c0, shape, 0.16f * sc, 0.09f * sc, 43);
            fabric.LaceLook();
            var fr = fabric.GetComponent<Renderer>();
            if (fr != null) fr.material.SetFloat("_Cull", 0f);   // thin cloth: both sides visible
        }
        Vector3 gpos = GarmentPos();
        garment = PM_Garment.Show(t.ToString().ToLower(), gpos, 0.75f, station.PlayerPos + Vector3.up * 1.6f, info.garment, info.smoothness, true);
    }

    // The bra cup drawn by the user (tools/avatar/cupexact.py, baked by cupbake.py into PM_Garments/cup.bytes):
    // three panels — a tall upper panel rising to the strap, two lower panels — joined by a seam across and a seam
    // down; sheer tulle with opaque tapes, lying on the dome of the cup pad like real cloth (the file holds the
    // grid in the pad's own space).
    bool SpawnCup(PM_FabricInfo info, Transform tr)
    {
        TextAsset ta = Resources.Load<TextAsset>("PM_Garments/cup");
        if (ta == null) { Debug.LogWarning("[PM] Нет файла чашки: Assets/Resources/PM_Garments/cup.bytes"); return false; }
        byte[] b = ta.bytes;
        if (b.Length < 16 || System.BitConverter.ToInt32(b, 0) != 0x31505543) return false;
        int n = System.BitConverter.ToInt32(b, 4), res = System.BitConverter.ToInt32(b, 8);
        float side = System.BitConverter.ToSingle(b, 12);
        int count = (n + 1) * (n + 1);
        if (res != 256 || b.Length < 16 + count * 12 + res * res * 4) return false;
        var grid = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            int o = 16 + i * 12;
            grid[i] = new Vector3(System.BitConverter.ToSingle(b, o), System.BitConverter.ToSingle(b, o + 4), System.BitConverter.ToSingle(b, o + 8));
        }
        Vector3 c0 = tr.TransformPoint(new Vector3(-0.10f, 0.205f, 0f));   // centre of the dome of the pad
        System.Func<float, float, Vector3> shape = (u, w) =>
        {
            float fu = Mathf.Clamp(u * n, 0f, n), fw = Mathf.Clamp(w * n, 0f, n);
            int i0 = Mathf.Min((int)fu, n - 1), j0 = Mathf.Min((int)fw, n - 1);
            float tu = fu - i0, tw = fw - j0;
            Vector3 p = Vector3.Lerp(Vector3.Lerp(grid[j0 * (n + 1) + i0], grid[j0 * (n + 1) + i0 + 1], tu),
                                     Vector3.Lerp(grid[(j0 + 1) * (n + 1) + i0], grid[(j0 + 1) * (n + 1) + i0 + 1], tu), tw);
            return tr.TransformPoint(p) - c0;
        };
        float sc = tr.lossyScale.x;
        fabric = PM_Fabric.CreateShaped(info, c0, shape, side * sc, side * sc, 43, n);
        fabric.CupLook(b, 16 + count * 12);
        var fr = fabric.GetComponent<Renderer>();
        if (fr != null) fr.material.SetFloat("_Cull", 0f);   // thin cloth: both sides visible
        return true;
    }

    PM_Gown gown;

    // The form always turns its front to the player.
    void FaceForm()
    {
        if (form == null) return;
        Vector3 d = station.PlayerPos - form.transform.position; d.y = 0f;
        if (d.sqrMagnitude > 0.0001f) form.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
    }

    // Keyboard/mouse test: Shift + left mouse on the gown steams it.
    public void DesktopSteam(PM_Gown g, Vector3 point)
    {
        if (g != gown || state != State.Learning || steps[stepIndex].action != PM_Action.SteamOnForm) return;
        if (DeskKey(Key.LeftShift)) gown.Steam(point, 0.09f, Time.deltaTime);
        desktopTime = Time.time;
        panel.SetStatus(DeskKey(Key.LeftShift) ? string.Format(PM_Content.StProgress, Mathf.RoundToInt(gown.Progress * 100f)) : PM_Content.StFormSteam, PM_Util.Cyan);
    }

    // Vertical steaming: steam from 1–8 cm smooths the gown where it reaches; touching it does nothing (and is wrong).
    void ProcessFormSteam(float dt)
    {
        if (gown == null) return;
        if (Time.time - desktopTime < 0.15f) return;   // the mouse test is handled in DesktopSteam
        PM_Iron iron = station.Iron;
        if (iron == null || !iron.Held) return;
        Vector3 dir = iron.DownWorld;
        RaycastHit hit = default(RaycastHit);
        bool found = false;
        float best = float.MaxValue;
        foreach (RaycastHit h in Physics.RaycastAll(iron.SoleWorld - dir * 0.03f, dir, 0.25f))
            if (h.collider == gown.Collider && h.distance < best) { hit = h; best = h.distance; found = true; }
        float gap = found ? hit.distance - 0.03f : 9f;
        if (!found || gap > 0.12f)
        {
            // the sole does not point at the dress: the side of the dress that faces the iron counts too
            Vector3 axis = form.transform.position; axis.y = iron.SoleWorld.y;
            Vector3 to = axis - iron.SoleWorld;
            if (to.sqrMagnitude > 1e-6f)
                foreach (RaycastHit h in Physics.RaycastAll(iron.SoleWorld - to.normalized * 0.02f, to.normalized, 0.4f))
                    if (h.collider == gown.Collider && h.distance - 0.02f < gap) { hit = h; gap = h.distance - 0.02f; found = true; }
        }
        if (!found || gap > 0.12f) { Status(PM_Content.StFormFar, PM_Util.Yellow); return; }
        if (gap < 0.006f) { Status(PM_Content.StFormPress, PM_Util.Yellow); iron.Buzz(0.2f, 0.05f); return; }
        if (!iron.Steaming) { Status(PM_Content.StFormSteam, PM_Util.Yellow); return; }
        // the steam cloud widens with the distance
        gown.Steam(hit.point, 0.08f + gap * 0.8f, dt);
        Status(string.Format(PM_Content.StProgress, Mathf.RoundToInt(gown.Progress * 100f)), PM_Util.Cyan);
    }

    void RemoveFabric()
    {
        if (fabric != null) Destroy(fabric.gameObject);
        fabric = null;
        if (garment != null) Destroy(garment.gameObject);
        garment = null;
    }

    // Water / limescale spat by the iron lands on the fabric under it. Returns true while spitting.
    // The status line under the screen changes at most every half second (no flicker between two messages
    // when the iron touches and leaves the fabric many times a second).
    string statusText; float statusAt = -9f;
    void Status(string text, Color c)
    {
        if (text == statusText && Time.time - statusAt < 3f) return;
        if (Time.time - statusAt < 0.5f) return;
        statusText = text; statusAt = Time.time;
        panel.SetStatus(text, c);
    }

    bool HandleSpit(float dt)
    {
        PM_Iron iron = station.Iron;
        if (iron == null || !iron.spitting) return false;
        PM_WaterTank t = station.Tank;
        int reason = t != null && t.Overfilled ? 1 : !station.PressureOk ? 2 : 3;
        if (reason == 3)
            Status(string.Format(PM_Content.StSpitCold, Mathf.RoundToInt(station.IronTemp), Mathf.RoundToInt(station.TargetTemp)), PM_Util.Red);
        else Status(reason == 1 ? PM_Content.StSpitOverfill : PM_Content.StSpitNotReady, PM_Util.Red);
        if (spitCardReason != reason) ShowSpitCard(reason);
        spitOk = 0f;
        return true;   // the drops themselves stain the fabric where they land (PM_Drops)
    }

    // Why the iron spits water (and leaves brown marks): an explanation window while it happens.
    int spitCardReason;
    float spitOk;
    void ShowSpitCard(int reason)
    {
        spitCardReason = reason;
        CloseCard();
        card.gameObject.SetActive(true);
        card.SetContent(PM_Content.SpitTitle, PM_Content.SpitBodies[reason - 1], "");
        card.SetAccent(PM_Util.Red);
        card.SetButtons(Btn(PM_Content.BtnClose, () => { CloseCard(); }));
        Vector3 head = Camera.main != null ? Camera.main.transform.position : station.PlayerPos + Vector3.up * 1.6f;
        card.Place(CardSlot(), head);
        if (PM_Audio.I != null) PM_Audio.I.Play("error", 0.7f);
    }

    // The iron is ready again: the explanation closes by itself.
    void UpdateSpitCard(float dt)
    {
        if (spitCardReason == 0) return;
        if (station.WillSpit) { spitOk = 0f; return; }
        spitOk += dt;
        if (spitOk < 1.5f) return;
        spitCardReason = 0;
        if (openHs == null) CloseCard();
        Status(PM_Content.StSpitFixed, PM_Util.Green);
    }

    // Returns true while ironing happens this frame.
    bool ProcessIroning(float dt, bool exam)
    {
        if (fabric == null) return false;
        PM_Iron iron = station.Iron;
        bool mouseIroning = Time.time - desktopTime < 0.15f;
        bool simulated = DeskKey(Key.Space) || mouseIroning;
        bool touching = simulated || (iron != null && iron.Touching == fabric);
        if (!touching) { dwell = 0f; return false; }

        bool steaming = (iron != null && iron.Steaming) || (simulated && DeskKey(Key.LeftShift) && station.PressureOk);
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
        if (!station.Powered) { Status(PM_Content.StNeedPower, PM_Util.Red); return true; }
        if (station.Mode == 0) { Status(PM_Content.StNoMode, PM_Util.Yellow); return true; }
        // The real soleplate temperature counts, not only the button: a cold iron does not smooth,
        // and after switching to a lower setting the iron is still too hot for a while.
        int mode = station.EffectiveMode;
        if (mode == 0) { Status(string.Format(PM_Content.StHeating, Mathf.RoundToInt(station.IronTemp)), PM_Util.Yellow); return true; }

        // Learning: a ruined fabric must be replaced ("בד חדש").
        if (!exam && fabric.Damage >= 0.25f) { Status(PM_Content.StRuined, PM_Util.Red); return true; }

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
            scorch = (mode - info.mode) * (poly ? 1.6f : 0.9f) * PM_Rewards.TooHotFactor;   // a silicone shoe protects
            msg = exam ? (poly ? PM_Content.StMelted : PM_Content.StBurned) : string.Format(PM_Content.StTooHotLearn, PM_Content.ModeLabel(info.mode));
            msgColor = PM_Util.Red;
        }

        // Holding the iron still on one spot: first a warning, then a scorch mark (sensitive fabrics sooner).
        bool still = !simulated || mouseIroning;
        if (still && iron != null && iron.Speed < 0.04f && mode >= info.mode) dwell += dt;
        else dwell = Mathf.Max(0f, dwell - dt * 3f);
        float dwellLimit = info.dwellSeconds * PM_Rewards.DwellFactor;
        if (mode >= info.mode && dwell > dwellLimit)
        {
            scorch += 0.8f + (dwell - dwellLimit) * 0.6f * (poly ? 1.6f : 1f);
            msg = PM_Content.StDwellBurn;
            msgColor = PM_Util.Red;
        }
        else if (mode >= info.mode && dwell > dwellLimit * 0.6f && msg == null)
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

        fabric.Iron(uv, radius, dt, rate, scorch, spots, mode / 3f, !steaming && mode >= 1);
        if (fabric.DriedNow && msg == null) { msg = PM_Content.StDrying; msgColor = PM_Util.Cyan; }
        if (msg == null) { msg = string.Format(PM_Content.StProgress, Mathf.RoundToInt(fabric.Progress * 100f)); msgColor = PM_Util.Cyan; }
        Status(msg, msgColor);
        return true;
    }

    void OnModeClicked(int m)
    {
        // Temperature quiz: the station buttons answer the question.
        if (state == State.ExamQuiz && quizKind == 0)
        {
            PM_Look.ButtonPulse(station.ButtonCenter(m), PM_Util.ModeColor(m), -station.Forward);
            AnswerExamQuestion(m - 1);
            return;
        }
        if (state == State.Quiz && quizKind == 0)
        {
            PM_Look.ButtonPulse(station.ButtonCenter(m), PM_Util.ModeColor(m), -station.Forward);
            AnswerQuiz(m - 1);
            return;
        }
        if (!station.Powered) { panel.SetStatus(PM_Content.StNeedPower, PM_Util.Red); if (PM_Audio.I != null) PM_Audio.I.Play("error", 0.6f); return; }
        station.SetMode(m);
        // In the exam only the CHOICE is tested: the iron is at the chosen temperature at once
        // (no hidden waiting for it to cool down from the previous fabric).
        if (state == State.ExamFabric) station.MakeReady();
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
        examPoints.Clear();
        examQuizRight = 0;
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
        if (tube == null) tube = PM_ScoreTube.Create(panel.transform, new Vector3(0.66f, -0.30f, 0f));
        tube.Set(0f, 0f);
        panel.SetContent(PM_Content.ExamTitle, PM_Content.ExamBody, "");
        panel.SetButtons(1, Btn(PM_Content.BtnMenu, ShowMenu), Btn(PM_Content.BtnNext, () => StartExamFabric(0)));
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("exam_intro");
    }

    // The most points of the whole exam: 100 for each fabric + the bonus questions.
    int ExamMax { get { return examList.Count * 100 + 100; } }
    int QuizPts { get { return Mathf.RoundToInt(examQuizRight * 100f / ExamQuestions); } }
    int ExamSum { get { int s = 0; foreach (int p in examPoints) s += p; return s + QuizPts; } }
    void UpdateTube(float possible)
    {
        if (tube != null) tube.Set(ExamSum / (float)ExamMax, possible / ExamMax);
    }

    void StartExamFabric(int i)
    {
        highlight.Clear();
        station.RefreshButtons();
        state = State.ExamFabric;
        examIndex = i;
        examTime = ExamSeconds;
        examFinishedFabric = false;
        examSuccess = false;
        station.MakeReady();     // hot, water and pressure ready: only the choice of temperature is tested
        station.SetMode(0);
        dwell = 0f;
        PM_FabricInfo info = PM_Content.Fabric(examList[i]);
        SpawnFabric(info.type, 100 + i * 7);
        panel.SetContent(string.Format(PM_Content.ExamFabricTitle, i + 1),
            string.Format(PM_Content.ExamFabricBody, info.cue, info.burn), (i + 1) + "/" + examList.Count);
        ExamButtons();
        UpdateTube(0f);
    }

    void ExamButtons()
    {
        panel.SetButtons(examFinishedFabric ? 1 : -1, Btn(PM_Content.BtnMenu, ShowMenu), Btn(PM_Content.BtnNextTask, NextExamTask));
    }

    // "למשימה הבאה": after a result, or to give up on this fabric (its progress still counts a little).
    void NextExamTask()
    {
        if (state != State.ExamFabric) return;
        if (!examFinishedFabric && fabric != null)
        {
            int pts = Mathf.RoundToInt(50f * fabric.Progress);
            examPoints.Add(pts);
            examLines.Add(string.Format(PM_Content.ExamLineSkip, examIndex + 1, fabric.info.name) + string.Format(PM_Content.ExamPts, pts));
        }
        if (examIndex + 1 < examList.Count) StartExamFabric(examIndex + 1);
        else StartExamQuiz();
    }

    void UpdateExam(float dt)
    {
        if (fabric == null) return;
        if (examFinishedFabric)
        {
            // a perfect fabric goes on by itself; after a mistake the player reads the result and presses "next"
            doneTimer += dt;
            if (examSuccess && doneTimer > 2.5f) NextExamTask();
            return;
        }
        examTime -= dt;
        bool ironing = HandleSpit(dt) || ProcessIroning(dt, true);
        if (!ironing) Status(PM_Content.StExamGo, PM_Util.Cyan);
        // the time is shown in the corner of the screen, apart from the messages
        panel.SetCounter(string.Format(PM_Content.StTime, Mathf.CeilToInt(Mathf.Max(0f, examTime))) + "   " + (examIndex + 1) + "/" + examList.Count);
        UpdateTube(60f * fabric.Progress);

        PM_FabricInfo info = fabric.info;
        string n = (examIndex + 1).ToString();
        if (fabric.Damage >= 0.25f)
        {
            string why = info.type == PM_FabricType.Polyester && fabric.Damage > 0 ? PM_Content.StMelted : PM_Content.StBurned;
            int pts = Mathf.RoundToInt(20f * fabric.Progress);
            examPoints.Add(pts);
            examLines.Add(string.Format(PM_Content.ExamLineBurn, n, info.name, why) + string.Format(PM_Content.ExamPts, pts));
            FinishExamFabric(why + " — " + string.Format(PM_Content.ExamTip, PM_Content.ModeLabel(info.mode)), PM_Util.Red, "error", false);
        }
        else if (fabric.Done)
        {
            examScore++;
            // 70 for a finished fabric + up to 20 for speed (full if done in the first half of the time) + up to 10 for care
            int pts = Mathf.Clamp(Mathf.RoundToInt(70f + 20f * Mathf.Clamp01(examTime / (ExamSeconds * 0.5f)) + 10f * (1f - Mathf.Clamp01(fabric.Damage / 0.25f))), 0, 100);
            examPoints.Add(pts);
            examLines.Add(string.Format(PM_Content.ExamLineOk, n, info.name, Mathf.RoundToInt(ExamSeconds - examTime)) + string.Format(PM_Content.ExamPts, pts));
            FinishExamFabric(PM_Content.StDone + " (" + info.name + ") +" + pts, PM_Util.Green, "ding", true);
        }
        else if (examTime <= 0f)
        {
            int pts = Mathf.RoundToInt(60f * fabric.Progress);
            examPoints.Add(pts);
            examLines.Add(string.Format(PM_Content.ExamLineTime, n, info.name) + string.Format(PM_Content.ExamPts, pts));
            FinishExamFabric(PM_Content.StTimeUp + " (" + info.name + ")", PM_Util.Red, "error", false);
        }
    }

    // ---- bonus questions at the end of the exam (temperature / fiber family): points into the same tube ----
    void StartExamQuiz()
    {
        RemoveFabric();
        examQuiz.Clear();
        var fibers = new List<PM_HotspotInfo>(PM_Content.FiberHotspots());
        Shuffle(fibers);
        var tools = new List<int>();
        for (int i = 0; i < PM_Content.ToolQuestions.Length; i++) tools.Add(i);
        Shuffle(tools);
        // 3 temperature, 2 fiber family, 3 tools & station questions
        int[] kinds = { 0, 2, 1, 0, 2, 1, 0, 2 };
        int fi = 0, ti = 0;
        foreach (int k in kinds)
        {
            var q = new ExamQ { kind = k };
            if (k == 2) { q.tool = tools[ti++ % tools.Count]; var o = new List<int> { 0, 1, 2 }; Shuffle(o); q.order = o.ToArray(); }
            else q.fiber = fibers[fi++ % fibers.Count];
            examQuiz.Add(q);
        }
        examQuizIndex = 0;
        ShowExamQuestion();
    }

    static void Shuffle<T>(List<T> l)
    {
        for (int i = l.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); T t = l[i]; l[i] = l[j]; l[j] = t; }
    }

    string[] ExamOptions(ExamQ q)
    {
        if (q.kind == 0) return PM_Content.QuizTempOptions;
        if (q.kind == 1) return PM_Content.QuizFamilyOptions;
        string[] all = PM_Content.ToolQuestions[q.tool].options;
        return new[] { all[q.order[0]], all[q.order[1]], all[q.order[2]] };
    }

    int ExamCorrect(ExamQ q)
    {
        if (q.kind == 0) return Mathf.Clamp(q.fiber.mode - 1, 0, 2);
        if (q.kind == 1) return FamilyIndex(q.fiber.fiberGroup);
        return System.Array.IndexOf(q.order, 0);   // option 0 of the bank is the right one
    }

    void ShowExamQuestion()
    {
        state = State.ExamQuiz;
        examQuizAnswered = false;
        ExamQ q = examQuiz[examQuizIndex];
        quizKind = q.kind == 2 ? 1 : q.kind;   // (the station buttons answer only temperature questions)
        string text = q.kind == 2 ? PM_Content.ToolQuestions[q.tool].question : (q.kind == 0 ? string.Format(PM_Content.QuizTempQ, q.fiber.name) : string.Format(PM_Content.QuizFamilyQ, q.fiber.name));
        panel.SetContent(string.Format(PM_Content.ExamQuizTitle, examQuizIndex + 1, Mathf.RoundToInt(100f / ExamQuestions)),
            text, (examQuizIndex + 1) + "/" + examQuiz.Count);
        panel.SetAccent(PM_Util.Yellow);
        string[] o = ExamOptions(q);
        panel.SetButtonGrid(1, -1, 0.75f, 0.0f, false, Btn(o[0], () => AnswerExamQuestion(0)), Btn(o[1], () => AnswerExamQuestion(1)), Btn(o[2], () => AnswerExamQuestion(2)));
        UpdateTube(0f);
    }

    void AnswerExamQuestion(int choice)
    {
        if (state != State.ExamQuiz || examQuizAnswered) return;
        examQuizAnswered = true;
        ExamQ q = examQuiz[examQuizIndex];
        int correct = ExamCorrect(q);
        string[] o = ExamOptions(q);
        bool right = choice == correct;
        if (right) examQuizRight++;
        statusAt = -9f;
        panel.SetStatus(right ? PM_Content.QuizRight + " +" + Mathf.RoundToInt(100f / ExamQuestions) : string.Format(PM_Content.QuizWrong, o[correct]), right ? PM_Util.Green : PM_Util.Red);
        if (PM_Audio.I != null) PM_Audio.I.Play(right ? "ding" : "error", 0.7f);
        if (right) PM_Look.ButtonPulse(panel.transform.position - panel.transform.up * 0.3f, PM_Util.Green, -panel.transform.forward);
        UpdateTube(0f);
        bool last = examQuizIndex + 1 >= examQuiz.Count;
        panel.SetButtons(0, Btn(last ? PM_Content.BtnExamResult : PM_Content.BtnNext, () =>
        {
            if (state != State.ExamQuiz) return;
            examQuizIndex++;
            if (examQuizIndex < examQuiz.Count) ShowExamQuestion(); else ShowExamResult();
        }));
    }

    void Celebrate()
    {
        if (fabric == null) return;
        fabric.Flash();
        PM_Look.Burst(fabric.transform.position + Vector3.up * 0.05f, PM_Util.ModeColor(fabric.info.mode));
    }

    void FinishExamFabric(string msg, Color c, string sound, bool success)
    {
        if (sound == "ding") Celebrate();
        examFinishedFabric = true;
        examSuccess = success;
        doneTimer = 0f;
        statusText = msg; statusAt = Time.time + 1.5f;   // keeps the result on screen
        panel.SetStatus(msg, c);
        ExamButtons();
        UpdateTube(0f);
        if (PM_Audio.I != null) PM_Audio.I.Play(sound, 0.8f);
    }

    int ExamGrade()
    {
        if (ExamMax <= 0) return 0;
        return Mathf.Clamp(Mathf.RoundToInt(ExamSum * 100f / ExamMax), 0, 100);
    }

    void ShowExamResult()
    {
        state = State.ExamResult;
        RemoveFabric();
        int grade = ExamGrade();
        int rank = grade >= 90 ? 3 : grade >= 75 ? 2 : grade >= 55 ? 1 : 0;
        string body = string.Join("\n", examLines.ToArray()) + "\n" + string.Format(PM_Content.ExamQuizLine, examQuizRight, ExamQuestions, QuizPts)
                      + "\n\n" + string.Format(PM_Content.ExamGrade, grade) + "\n" + PM_Content.Ranks[rank];
        UpdateTube(0f);
        if (grade >= 90) body += "\n" + PM_Content.ExamRewardLine;
        panel.SetContent(PM_Content.ExamResultTitle, body, grade + "/100");
        if (grade >= 90)
            panel.SetButtons(1, Btn(PM_Content.BtnAgain, StartExam), Btn(PM_Content.BtnReward, () => ShowRewardChoice(grade)), Btn(PM_Content.BtnMenu, ShowMenu));
        else
            panel.SetButtons(Btn(PM_Content.BtnAgain, StartExam), Btn(PM_Content.BtnMenu, ShowMenu));
        if (grade >= 90) { PM_Look.Burst(panel.transform.position, PM_Util.Green); PM_Look.PulseRing(station.PlayerPos + Vector3.up * 0.01f, PM_Util.Green); }
        if (PM_Audio.I != null) PM_Audio.I.Play(grade >= 75 ? "ding" : "error", 0.8f);
    }

    // A grade of 90+: pick one new pressing item (silicone shoe, automatic water filling, wooden clapper).
    void ShowRewardChoice(int grade)
    {
        Remember();
        state = State.ExamResult;
        var items = new List<KeyValuePair<string, Action>>();
        string body = "";
        for (int i = 0; i < PM_Rewards.All.Length; i++)
        {
            int r = PM_Rewards.All[i];
            if (PM_Rewards.Has(r)) continue;
            int k = i;
            body += "• " + PM_Content.RewardNames[i] + ": " + PM_Content.RewardTexts[i] + "\n";
            items.Add(Btn(PM_Content.RewardNames[i], () => TakeReward(k)));
        }
        if (items.Count == 0)
        {
            panel.SetContent(string.Format(PM_Content.RewardTitle, grade), PM_Content.RewardAll, "");
            panel.SetButtons(Btn(PM_Content.BtnMenu, ShowMenu));
            return;
        }
        panel.SetContent(string.Format(PM_Content.RewardTitle, grade), body.TrimEnd('\n'), "");
        panel.SetAccent(PM_Util.Green);
        items.Add(Btn(PM_Content.BtnMenu, ShowMenu));
        panel.SetButtonGrid(items.Count, -1, 0.26f, items.ToArray());
    }

    void TakeReward(int i)
    {
        int r = PM_Rewards.All[i];
        PM_Rewards.Unlock(r);
        PM_Rewards.Apply(station);
        panel.SetContent(PM_Content.RewardNames[i], PM_Content.RewardTexts[i] + "\n\n" + PM_Content.RewardGot, "");
        panel.SetButtons(Btn(PM_Content.BtnPractice, ShowMenu));
        Vector3 at = r == PM_Rewards.SiliconeShoe && station.Iron != null ? station.Iron.transform.position
                   : r == PM_Rewards.AutoFill && station.Tank != null ? station.Tank.transform.position
                   : new Vector3(station.BoardCenter.x, station.BoardTopY, station.BoardCenter.z);
        PM_Look.Burst(at, PM_Util.Green);
        if (PM_Audio.I != null) PM_Audio.I.Play("ding", 0.9f);
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
        if (station != null && station.Iron != null) station.Iron.simulateSteam = DeskKey(Key.LeftShift);
        PM_Rewards.Tick(station, dt);
        if (form != null)
        {
            bool exam = state == State.ExamIntro || state == State.ExamFabric || state == State.ExamQuiz || state == State.ExamResult;
            if (form.gameObject.activeSelf == exam) form.gameObject.SetActive(!exam);   // no dress form in the exam
        }
        if (tube != null)
        {
            bool showTube = state == State.ExamIntro || state == State.ExamFabric || state == State.ExamQuiz || state == State.ExamResult;
            if (tube.gameObject.activeSelf != showTube) tube.Show(showTube);
        }
        UpdateSpitCard(dt);
        UpdateCover();
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

    // Keyboard test keys for ironing / steam (Space, Left Shift) work only without a headset: in VR a Shift that
    // Windows keeps "pressed" (e.g. after Alt+Shift to switch the language) would steam non-stop.
    static bool DeskKey(Key k)
    {
        return !UnityEngine.XR.XRSettings.isDeviceActive && KeyHeld(k);
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
