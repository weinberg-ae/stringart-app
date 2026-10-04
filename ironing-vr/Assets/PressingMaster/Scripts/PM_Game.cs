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
    enum State { Menu, Learning, ExamIntro, ExamFabric, ExamResult }

    PM_Station station;
    PM_Panel panel;
    PM_Highlight highlight;
    PM_Fabric fabric;

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
        panel.SetButtons(Btn(PM_Content.BtnLearn, StartLearning), Btn(PM_Content.BtnExam, StartExam));
        if (PM_Audio.I != null) PM_Audio.I.PlayVoice("menu");
    }

    static KeyValuePair<string, Action> Btn(string label, Action a) { return new KeyValuePair<string, Action>(label, a); }

    void ClearStepVisuals()
    {
        highlight.Clear();
        station.RefreshButtons();
        station.ShowTool(PM_Target.None);
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
        station.ShowTool(IsTool(s.target) ? s.target : PM_Target.None);

        if (s.target == PM_Target.Fabric)
        {
            if (fabric == null || fabric.info.type != s.fabric) SpawnFabric(s.fabric, stepIndex);
        }
        else RemoveFabric();

        if (s.target != PM_Target.None && s.target != PM_Target.Fabric)
            highlight.Show(station.Targets(s.target), PM_Util.Cyan, true);

        panel.SetContent(s.title, s.body, (stepIndex + 1) + "/" + steps.Count);
        if (stepIndex == steps.Count - 1)
            panel.SetButtons(Btn(PM_Content.BtnExam, StartExam), Btn(PM_Content.BtnMenu, ShowMenu), Btn(PM_Content.BtnRepeat, RepeatVoice));
        else
            panel.SetButtons(Btn(PM_Content.BtnNext, NextStep), Btn(PM_Content.BtnRepeat, RepeatVoice), Btn(PM_Content.BtnMenu, ShowMenu));

        if (s.action == PM_Action.Power && station.Powered) Complete(PM_Content.StPressureOk);
        RepeatVoice();
    }

    static bool IsTool(PM_Target t)
    {
        return t == PM_Target.Ham || t == PM_Target.PointPresser || t == PM_Target.Cloth || t == PM_Target.Fusible;
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
            case PM_Action.IronFabric:
                ProcessIroning(dt, false);
                if (fabric != null && fabric.Done) Complete(PM_Content.StDone);
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
    }

    void RemoveFabric()
    {
        if (fabric != null) Destroy(fabric.gameObject);
        fabric = null;
    }

    // Returns true while ironing happens this frame.
    bool ProcessIroning(float dt, bool exam)
    {
        if (fabric == null) return false;
        PM_Iron iron = station.Iron;
        bool simulated = KeyHeld(Key.Space);
        bool touching = simulated || (iron != null && iron.Touching == fabric);
        if (!touching) return false;

        bool steaming = (iron != null && iron.Steaming) || (simulated && KeyHeld(Key.LeftShift) && station.PressureOk);
        Vector2 uv;
        if (simulated)
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

        fabric.Iron(uv, radius, dt, rate, scorch, spots);
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

    void FinishExamFabric(string msg, Color c, string sound)
    {
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
