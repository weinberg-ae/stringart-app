using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

// Player avatar in the headset: human hands instead of the controllers (fingers follow Grip and Trigger)
// and a simple body with a work apron and arms. Style (skin tone / hologram) is chosen at the start.
public static class PM_Avatar
{
    public static int Style { get; private set; }

    static readonly Color[] Skin = { new Color(0.96f, 0.80f, 0.69f), new Color(0.80f, 0.60f, 0.45f), new Color(0.45f, 0.30f, 0.21f) };
    static readonly Color[] Shirt = { new Color(0.12f, 0.13f, 0.16f), new Color(0.9f, 0.9f, 0.92f), new Color(0.42f, 0.1f, 0.16f) };

    static Material skinMat, shirtMat, apronMat, pantsMat;
    static PM_Hand leftHand, rightHand;
    static Transform preview;

    static Material Lit(Color c, float smooth)
    {
        var m = new Material(PM_Util.LitShader());
        m.SetColor("_BaseColor", c);
        m.SetColor("_Color", c);
        m.SetFloat("_Smoothness", smooth);
        return m;
    }

    public static void Init()
    {
        skinMat = Lit(Skin[0], 0.35f);
        shirtMat = Lit(Shirt[0], 0.2f);
        apronMat = Lit(new Color(0.08f, 0.09f, 0.11f), 0.3f);
        pantsMat = Lit(new Color(0.1f, 0.1f, 0.12f), 0.15f);
        Style = Mathf.Clamp(PlayerPrefs.GetInt("PM_AvatarStyle", 0), 0, 3);
        ApplyMaterials();
        if (XRSettings.isDeviceActive) BuildInHeadset();
    }

    public static void SetStyle(int style)
    {
        Style = Mathf.Clamp(style, 0, 3);
        PlayerPrefs.SetInt("PM_AvatarStyle", Style);
        ApplyMaterials();
    }

    static void ApplyMaterials()
    {
        if (skinMat == null) return;
        bool holo = Style == 3;
        Color skin = holo ? new Color(0.05f, 0.35f, 0.4f) : Skin[Style];
        Color shirt = holo ? new Color(0.03f, 0.15f, 0.2f) : Shirt[Style];
        skinMat.SetColor("_BaseColor", skin); skinMat.SetColor("_Color", skin);
        shirtMat.SetColor("_BaseColor", shirt); shirtMat.SetColor("_Color", shirt);
        foreach (Material m in new[] { skinMat, shirtMat, apronMat, pantsMat })
        {
            if (holo) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", PM_Util.Cyan * (m == skinMat ? 0.9f : 0.35f)); }
            else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
        }
    }

    // Hands on the controllers + body. Controller models are hidden (the pointing rays stay).
    static void BuildInHeadset()
    {
        XROrigin origin = Object.FindAnyObjectByType<XROrigin>();
        if (origin == null || origin.CameraFloorOffsetObject == null) return;
        Transform offset = origin.CameraFloorOffsetObject.transform;
        Transform left = null, right = null;
        for (int i = 0; i < offset.childCount; i++)
        {
            Transform c = offset.GetChild(i);
            string n = c.name.ToLower();
            if (!n.Contains("controller")) continue;
            if (n.Contains("left")) left = c;
            else if (n.Contains("right")) right = c;
        }
        if (left == null || right == null) { Debug.Log("[PM] Контроллеры не найдены — руки не созданы."); return; }
        HideControllerModel(left);
        HideControllerModel(right);
        leftHand = PM_Hand.Create(left, true, skinMat, shirtMat, true);
        rightHand = PM_Hand.Create(right, false, skinMat, shirtMat, true);
        PM_Body.Create(origin.transform, leftHand, rightHand, shirtMat, apronMat, pantsMat);
    }

    static void HideControllerModel(Transform controller)
    {
        foreach (Renderer r in controller.GetComponentsInChildren<Renderer>(true))
        {
            if (r is LineRenderer || r is ParticleSystemRenderer) continue;          // keep the pointing ray
            if (r.GetComponentInParent<PM_Hand>() != null) continue;
            r.enabled = false;
        }
    }

    // Two hands floating next to the avatar menu, so the style can be seen also without the headset.
    public static void ShowPreview(bool on, Vector3 pos, Vector3 viewer)
    {
        if (!on)
        {
            if (preview != null) Object.Destroy(preview.gameObject);
            preview = null;
            return;
        }
        if (preview != null) return;
        preview = new GameObject("PM_AvatarPreview").transform;
        preview.position = pos;
        Vector3 d = pos - viewer; d.y = 0;
        preview.rotation = Quaternion.LookRotation(d.sqrMagnitude > 0.001f ? d : Vector3.forward, Vector3.up);   // looks away from the viewer
        var spin = preview.gameObject.AddComponent<PM_Float>();
        PM_Hand l = PM_Hand.Create(preview, true, skinMat, shirtMat, false);
        PM_Hand r = PM_Hand.Create(preview, false, skinMat, shirtMat, false);
        // Fingers up, backs of the hands towards the viewer.
        l.transform.localPosition = new Vector3(-0.09f, 0, 0);
        r.transform.localPosition = new Vector3(0.09f, 0, 0);
        l.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.right);
        r.transform.localRotation = Quaternion.LookRotation(Vector3.up, -Vector3.right);
        preview.localScale = Vector3.one * 1.8f;
        spin.amplitude = 0.02f;
    }
}

// Procedural hand around a controller (grip pose): fingers forward (+Z), thumb up (+Y), palm facing inwards.
public class PM_Hand : MonoBehaviour
{
    public bool left;
    public Transform Wrist { get; private set; }
    bool live;
    readonly Transform[][] fingers = new Transform[5][];   // 0 thumb, 1 index, 2 middle, 3 ring, 4 pinky
    float grip, trigger;

    public static PM_Hand Create(Transform parent, bool left, Material skin, Material sleeve, bool live)
    {
        var go = new GameObject(left ? "PM_LeftHand" : "PM_RightHand");
        go.transform.SetParent(parent, false);
        var h = go.AddComponent<PM_Hand>();
        h.left = left;
        h.live = live;
        h.Build(skin, sleeve);
        return h;
    }

    float S { get { return left ? -1f : 1f; } }   // mirror X for the left hand

    static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(euler);
        g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }

    void Build(Material skin, Material sleeve)
    {
        float s = S;
        // Palm (rounded block) and wrist with a cuff of the sleeve.
        Part(transform, PrimitiveType.Sphere, new Vector3(0.012f * s, -0.004f, -0.035f), Vector3.zero, new Vector3(0.032f, 0.088f, 0.1f), skin);
        Part(transform, PrimitiveType.Cube, new Vector3(0.012f * s, -0.004f, -0.032f), Vector3.zero, new Vector3(0.026f, 0.072f, 0.075f), skin);
        Wrist = new GameObject("Wrist").transform;
        Wrist.SetParent(transform, false);
        Wrist.localPosition = new Vector3(0.012f * s, -0.012f, -0.095f);
        Part(transform, PrimitiveType.Capsule, new Vector3(0.012f * s, -0.01f, -0.09f), new Vector3(90, 0, 0), new Vector3(0.05f, 0.03f, 0.05f), skin);
        Part(transform, PrimitiveType.Cylinder, new Vector3(0.012f * s, -0.012f, -0.125f), new Vector3(90, 0, 0), new Vector3(0.062f, 0.025f, 0.062f), sleeve);

        // Fingers: base height (Y), segment lengths, radius.
        float[] baseY = { 0f, 0.03f, 0.009f, -0.012f, -0.032f };
        float[][] len =
        {
            new[] { 0.036f, 0.03f, 0.025f },
            new[] { 0.04f, 0.026f, 0.021f },
            new[] { 0.045f, 0.029f, 0.023f },
            new[] { 0.042f, 0.027f, 0.021f },
            new[] { 0.033f, 0.021f, 0.018f },
        };
        float[] rad = { 0.0105f, 0.0092f, 0.0095f, 0.009f, 0.008f };
        for (int f = 0; f < 5; f++)
        {
            fingers[f] = new Transform[3];
            Transform parentJ = transform;
            for (int j = 0; j < 3; j++)
            {
                var joint = new GameObject("F" + f + "_" + j).transform;
                joint.SetParent(parentJ, false);
                if (j == 0)
                {
                    if (f == 0) { joint.localPosition = new Vector3(-0.004f * s, 0.035f, -0.055f); joint.localRotation = Quaternion.Euler(-28f, -32f * s, 0); }
                    else joint.localPosition = new Vector3(0.006f * s, baseY[f], 0.012f);
                }
                else joint.localPosition = new Vector3(0, 0, len[f][j - 1]);
                float L = len[f][j], r = rad[f] * (1f - j * 0.08f);
                Part(joint, PrimitiveType.Capsule, new Vector3(0, 0, L * 0.5f), new Vector3(90, 0, 0), new Vector3(r * 2f, (L + r) * 0.5f, r * 2f), skin);
                fingers[f][j] = joint;
                parentJ = joint;
            }
        }
        Pose(0.25f, 0.15f);
    }

    // curl 0 = open, 1 = fist. Fingers bend towards the palm (inwards = -X for the right hand).
    void Pose(float g, float t)
    {
        float s = S;
        for (int f = 1; f < 5; f++)
        {
            float c = f == 1 ? t : g;
            float[] maxA = { 70f, 95f, 70f };
            for (int j = 0; j < 3; j++)
            {
                float a = Mathf.Lerp(8f + j * 6f, maxA[j], c);
                fingers[f][j].localRotation = Quaternion.Euler(0, -a * s, 0);
            }
        }
        // Thumb rests on the top; with a strong grip it closes a bit.
        for (int j = 1; j < 3; j++) fingers[0][j].localRotation = Quaternion.Euler(0, -Mathf.Lerp(10f, 35f, g) * s, 0);
    }

    void Update()
    {
        if (!live) return;
        InputDevice d = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
        float g = 0f, t = 0f;
        if (d.isValid)
        {
            d.TryGetFeatureValue(CommonUsages.grip, out g);
            d.TryGetFeatureValue(CommonUsages.trigger, out t);
        }
        grip = Mathf.Lerp(grip, g, Time.deltaTime * 18f);
        trigger = Mathf.Lerp(trigger, t, Time.deltaTime * 18f);
        Pose(0.2f + grip * 0.8f, 0.1f + trigger * 0.9f);
    }
}

// Simple body under the head: torso with a work apron, legs and arms reaching the hands.
public class PM_Body : MonoBehaviour
{
    Transform head, torso, apron, neck, legL, legR;
    PM_Hand lh, rh;
    Transform[] armParts = new Transform[4];
    Transform origin;
    float yaw;

    public static PM_Body Create(Transform xrOrigin, PM_Hand l, PM_Hand r, Material shirt, Material apronM, Material pants)
    {
        var go = new GameObject("PM_Body");
        var b = go.AddComponent<PM_Body>();
        b.origin = xrOrigin;
        b.lh = l; b.rh = r;
        b.head = Camera.main != null ? Camera.main.transform : null;
        b.torso = Prim(go.transform, PrimitiveType.Capsule, shirt);
        b.neck = Prim(go.transform, PrimitiveType.Capsule, shirt);
        b.apron = Prim(go.transform, PrimitiveType.Cube, apronM);
        b.legL = Prim(go.transform, PrimitiveType.Capsule, pants);
        b.legR = Prim(go.transform, PrimitiveType.Capsule, pants);
        for (int i = 0; i < 4; i++) b.armParts[i] = Prim(go.transform, PrimitiveType.Capsule, shirt);
        // Neon trim of the apron.
        var trim = PM_Util.Line(b.apron, "Trim", new[] { new Vector3(-0.5f, 0.5f, -0.6f), new Vector3(0.5f, 0.5f, -0.6f), new Vector3(0.5f, -0.5f, -0.6f), new Vector3(-0.5f, -0.5f, -0.6f) }, PM_Util.Cyan, 0.004f, true);
        trim.useWorldSpace = false;
        return b;
    }

    static Transform Prim(Transform parent, PrimitiveType t, Material m)
    {
        var g = GameObject.CreatePrimitive(t);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }

    // Capsule from a to b with the given radius.
    static void Bone(Transform cap, Vector3 a, Vector3 b, float radius)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        cap.position = (a + b) * 0.5f;
        cap.rotation = len > 0.0001f ? Quaternion.FromToRotation(Vector3.up, d / len) : Quaternion.identity;
        cap.localScale = new Vector3(radius * 2f, (len + radius * 2f) * 0.5f, radius * 2f);
    }

    void LateUpdate()
    {
        if (head == null) { head = Camera.main != null ? Camera.main.transform : null; if (head == null) return; }
        // The body turns after the head (with a little delay), not with every glance.
        Vector3 f = head.forward; f.y = 0;
        if (f.sqrMagnitude > 0.001f)
        {
            float target = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            float diff = Mathf.DeltaAngle(yaw, target);
            if (Mathf.Abs(diff) > 25f) yaw += diff * Mathf.Min(1f, Time.deltaTime * 4f);
        }
        Quaternion rot = Quaternion.Euler(0, yaw, 0);
        Vector3 fwd = rot * Vector3.forward, right = rot * Vector3.right;
        float floorY = origin != null ? origin.position.y : head.position.y - 1.6f;

        Vector3 neckTop = head.position - Vector3.up * 0.12f - fwd * 0.09f;
        Vector3 chest = neckTop - Vector3.up * 0.2f;
        Vector3 waist = neckTop - Vector3.up * 0.55f;
        Bone(neck, neckTop, neckTop - Vector3.up * 0.08f, 0.05f);
        torso.rotation = rot;
        torso.position = (chest + waist) * 0.5f;
        torso.localScale = new Vector3(0.36f, (Vector3.Distance(chest, waist) + 0.2f) * 0.5f, 0.22f);

        // Apron: from the chest to the knees, in front of the body.
        Vector3 apronTop = chest - Vector3.up * 0.02f + fwd * 0.115f;
        float apronH = Mathf.Max(0.3f, apronTop.y - (floorY + 0.45f));
        apron.position = apronTop - Vector3.up * apronH * 0.5f;
        apron.rotation = rot;
        apron.localScale = new Vector3(0.38f, apronH, 0.012f);

        // Legs.
        Vector3 hipL = waist - right * 0.09f, hipR = waist + right * 0.09f;
        Bone(legL, hipL, new Vector3(hipL.x, floorY + 0.05f, hipL.z), 0.065f);
        Bone(legR, hipR, new Vector3(hipR.x, floorY + 0.05f, hipR.z), 0.065f);

        // Arms: two bones from the shoulder to the wrist, elbow down and out.
        Arm(neckTop - Vector3.up * 0.05f - right * 0.19f, lh, -right, fwd, 0, 1);
        Arm(neckTop - Vector3.up * 0.05f + right * 0.19f, rh, right, fwd, 2, 3);
    }

    void Arm(Vector3 shoulder, PM_Hand hand, Vector3 outward, Vector3 fwd, int upper, int lower)
    {
        bool on = hand != null && hand.Wrist != null && hand.isActiveAndEnabled;
        armParts[upper].gameObject.SetActive(on);
        armParts[lower].gameObject.SetActive(on);
        if (!on) return;
        const float L1 = 0.3f, L2 = 0.28f;
        Vector3 wrist = hand.Wrist.position;
        Vector3 to = wrist - shoulder;
        float dist = Mathf.Clamp(to.magnitude, 0.05f, L1 + L2 - 0.001f);
        Vector3 dir = to.normalized;
        Vector3 pole = Vector3.down + outward * 0.6f - fwd * 0.3f;
        pole = (pole - Vector3.Dot(pole, dir) * dir).normalized;
        float a = (L1 * L1 - L2 * L2 + dist * dist) / (2f * dist);
        float h = Mathf.Sqrt(Mathf.Max(0f, L1 * L1 - a * a));
        Vector3 elbow = shoulder + dir * a + pole * h;
        Bone(armParts[upper], shoulder, elbow, 0.045f);
        Bone(armParts[lower], elbow, wrist, 0.037f);
    }
}
