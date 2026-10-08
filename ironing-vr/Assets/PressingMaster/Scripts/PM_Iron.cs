using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;

// The professional iron: grab with Grip, steam with Trigger, returns to its rest when released.
// While held, the iron follows the hand by our own code (a little behind = weight), and it can never sink into
// the board or the fabric: it is lifted onto the surface and lies flat on it when close.
public class PM_Iron : MonoBehaviour
{
    public bool Held { get { return grab != null && grab.isSelected; } }
    public bool TriggerDown { get; private set; }
    public bool Steaming { get; private set; }
    public bool steamAllowed;
    public bool spitting;                  // set by the station: wet steam with water drops (and limescale)
    public bool simulateSteam;             // keyboard test (Left Shift) without the headset              // set by the game (power on + pressure ok)

    // Contact with fabric (updated every frame).
    public PM_Fabric Touching { get; private set; }
    public Vector2 TouchUV { get; private set; }
    public float Speed { get; private set; }
    public float SoleRadius { get; private set; }

    public System.Action onGrab;
    public static Transform CurrentHolder { get; private set; }   // the hand (interactor) holding the iron
    public static PM_Iron Instance { get; private set; }
    public static bool HandNear { get; private set; }             // a hand is close: points of light step aside
    public float heat;                     // 0..1 set by the station (temperature button)
    Material soleMat;
    float hapticTimer, secondPulse = -1f;
    readonly bool[] gripWas = new bool[2];
    Transform[] controllers;
    float desktopUntil = -1f;
    bool desktopHolding;

    XRGrabInteractable grab;
    Transform holder;
    Vector3 restLocalPos;
    Quaternion restLocalRot;
    Vector3 localDown, localSole;
    float returnT = -1f;
    Vector3 returnFromPos;
    Quaternion returnFromRot;
    Vector3 lastSole;
    Vector2 soleHalf;                 // half size of the soleplate (local x, z)
    Vector3 localMin, localMax;       // iron size in its own space
    Vector3 tipLocal = Vector3.forward;   // direction of the iron's tip (local)
    Vector3 grabPosOff, followPos;
    Quaternion grabRotOff, followRot;
    ParticleSystem steam, spit;
    bool spitOn;
    AudioSource steamAudio;

    // Builds the iron from model parts. Must be called while the iron lies on its rest.
    public static PM_Iron Assemble(Transform[] parts, Transform tableRoot)
    {
        Bounds b = PM_Util.WorldBounds(parts);
        var go = new GameObject("PM_Iron");
        go.transform.position = b.center;
        go.transform.rotation = tableRoot.rotation;
        go.transform.SetParent(tableRoot, true);
        foreach (Transform p in parts)
        {
            p.SetParent(go.transform, true);
            foreach (MeshRenderer r in p.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponent<Collider>() == null) r.gameObject.AddComponent<BoxCollider>();
            }
        }
        var iron = go.AddComponent<PM_Iron>();
        iron.Init(b);
        return iron;
    }

    void Init(Bounds worldBounds)
    {
        restLocalPos = transform.localPosition;
        restLocalRot = transform.localRotation;
        localDown = transform.InverseTransformDirection(Vector3.down);
        localSole = transform.InverseTransformPoint(new Vector3(worldBounds.center.x, worldBounds.min.y, worldBounds.center.z));
        SoleRadius = Mathf.Min(worldBounds.size.x, worldBounds.size.z) * 0.6f;

        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;          // always kinematic: the iron is moved by this script
        rb.useGravity = false;
        rb.mass = 1.6f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        grab = gameObject.AddComponent<XRGrabInteractable>();
        // XRI only tells us who holds the iron; the movement is ours (see Follow).
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.trackPosition = false;
        grab.trackRotation = false;
        grab.throwOnDetach = false;
        // Sole outline in the iron's own space (for the surface check).
        Vector3 lmin = Vector3.one * 99f, lmax = -Vector3.one * 99f;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            Bounds rb2 = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = new Vector3((i & 1) == 0 ? rb2.min.x : rb2.max.x, (i & 2) == 0 ? rb2.min.y : rb2.max.y, (i & 4) == 0 ? rb2.min.z : rb2.max.z);
                Vector3 l = transform.InverseTransformPoint(c);
                lmin = Vector3.Min(lmin, l); lmax = Vector3.Max(lmax, l);
            }
        }
        soleHalf = new Vector2((lmax.x - lmin.x) * 0.42f, (lmax.z - lmin.z) * 0.42f);
        localMin = lmin; localMax = lmax;

        // The player's body must not push the iron around.
        foreach (CharacterController cc in Resources.FindObjectsOfTypeAll<CharacterController>())
        {
            if (!cc.gameObject.scene.IsValid()) continue;
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) Physics.IgnoreCollision(c, cc);
        }
        grab.interactionLayers = -1;   // any hand / any interactor may take the iron
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
        grab.activated.AddListener(a => TriggerDown = true);
        grab.deactivated.AddListener(a => TriggerDown = false);

        BuildSteam();

        // The lowest part of the iron is the soleplate: it glows when hot.
        Renderer sole = null;
        float minY = float.MaxValue;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is LineRenderer) continue;
            if (r.bounds.min.y < minY) { minY = r.bounds.min.y; sole = r; }
        }
        if (sole != null) { soleMat = sole.material; soleMat.EnableKeyword("_EMISSION"); }
    }

    void BuildSteam()
    {
        var go = new GameObject("PM_Steam");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localSole;
        go.transform.rotation = Quaternion.LookRotation(Vector3.down);
        steam = go.AddComponent<ParticleSystem>();
        steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = steam.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 1.5f;
        main.startSpeed = 0.06f;
        main.startSize = 0.08f;
        main.startColor = new Color(1f, 1f, 1f, 0.7f);
        main.gravityModifier = -0.35f;   // steam rises around the iron
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var em = steam.emission;
        em.rateOverTime = 130f;
        var shape = steam.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.2f, 0.3f, 0.02f);
        var sol = steam.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 4f));
        var col = steam.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0.8f, 0), new GradientAlphaKey(0f, 1) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        var pr = go.GetComponent<ParticleSystemRenderer>();
        var m = PM_Util.TransparentMaterial(Color.white);
        m.mainTexture = PM_Util.SoftDot(64);
        pr.material = m;

        // Water drops (some brown = limescale) when the station is not ready or the tank is overfilled.
        var sgo = new GameObject("PM_Spit");
        sgo.transform.SetParent(transform, false);
        sgo.transform.localPosition = localSole + localDown * 0.02f;   // just under the sole, so drops don't hit the iron itself
        sgo.transform.rotation = Quaternion.LookRotation(Vector3.down);
        spit = sgo.AddComponent<ParticleSystem>();
        spit.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var sm = spit.main;
        sm.loop = true;
        sm.playOnAwake = false;
        sm.startLifetime = 0.9f;
        sm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
        var dg = new Gradient();
        Color w = new Color(0.75f, 0.9f, 1f, 0.9f), b = new Color(0.5f, 0.33f, 0.14f, 1f);
        dg.SetKeys(new[] { new GradientColorKey(w, 0f), new GradientColorKey(w, 0.84f), new GradientColorKey(b, 0.86f), new GradientColorKey(b, 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        sm.startColor = new ParticleSystem.MinMaxGradient(dg) { mode = ParticleSystemGradientMode.RandomColor };
        sm.gravityModifier = 1f;
        sm.simulationSpace = ParticleSystemSimulationSpace.World;
        var sem = spit.emission;
        sem.rateOverTime = 70f;
        var ssh = spit.shape;
        ssh.shapeType = ParticleSystemShapeType.Cone;
        ssh.angle = 25f;
        ssh.radius = 0.03f;
        // Drops stop on the board / fabric and leave wet marks (PM_Drops).
        var scol = spit.collision;
        scol.enabled = true;
        scol.type = ParticleSystemCollisionType.World;
        scol.mode = ParticleSystemCollisionMode.Collision3D;
        scol.bounce = 0f;
        scol.dampen = 1f;
        scol.lifetimeLoss = 1f;
        scol.sendCollisionMessages = true;
        sgo.AddComponent<PM_Drops>();
        var spr = sgo.GetComponent<ParticleSystemRenderer>();
        var spm = PM_Util.TransparentMaterial(Color.white);
        spm.mainTexture = PM_Util.SoftDot(16);
        spr.material = spm;

        steamAudio = go.AddComponent<AudioSource>();
        steamAudio.loop = true;
        steamAudio.playOnAwake = false;
        steamAudio.spatialBlend = 0.8f;
        steamAudio.volume = 0.6f;
        if (PM_Audio.I != null) steamAudio.clip = PM_Audio.I.Clip("steam_loop");
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        holder = args.interactorObject.transform;
        CurrentHolder = holder;
        returnT = -1f;
        grabPosOff = holder.InverseTransformPoint(transform.position);
        grabRotOff = Quaternion.Inverse(holder.rotation) * transform.rotation;
        followPos = transform.position;
        followRot = transform.rotation;
        PM_Clickable.Haptic(holder, 0.7f, 0.08f);   // "thunk" — first pulse
        secondPulse = Time.time + 0.12f;              // second, softer pulse
        if (onGrab != null) onGrab();
    }

    void OnRelease(SelectExitEventArgs args)
    {
        holder = null;
        CurrentHolder = null;
        TriggerDown = false;
        returnT = 0f;
        returnFromPos = transform.localPosition;
        returnFromRot = transform.localRotation;
    }

    // Mouse test: put the iron on the fabric under the cursor.
    public void PlaceAt(Vector3 point)
    {
        if (Held) return;
        if (!desktopHolding) { desktopHolding = true; transform.localRotation = restLocalRot; }
        returnT = -1f;
        transform.position += point + Vector3.up * 0.004f - SoleWorld;
        desktopUntil = Time.time + 0.2f;
    }

    // The station tells which way the tip points (at the rest it points to the player).
    public void SetTipDirection(Vector3 worldDir)
    {
        Vector3 l = transform.InverseTransformDirection(worldDir);
        l -= Vector3.Dot(l, localDown) * localDown;   // horizontal in the iron's own frame
        if (l.sqrMagnitude > 0.0001f) tipLocal = l.normalized;
    }

    // Where the hand holding the iron should be: around the handle, fist along the handle, palm down.
    // Returns the "grip frame" (Z along the handle to the tip, Y = thumb side), like a controller grip pose.
    public static bool HandleGrip(bool left, out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero; rot = Quaternion.identity;
        PM_Iron it = Instance;
        if (it == null || !it.Held || CurrentHolder == null || PM_Clickable.IsLeft(CurrentHolder) != left) return false;
        Vector3 c = (it.localMin + it.localMax) * 0.5f;
        Vector3 upL = -it.localDown;
        float top = 0f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 k = new Vector3((i & 1) == 0 ? it.localMin.x : it.localMax.x, (i & 2) == 0 ? it.localMin.y : it.localMax.y, (i & 4) == 0 ? it.localMin.z : it.localMax.z);
            top = Mathf.Max(top, Vector3.Dot(k - c, upL));
        }
        pos = it.transform.TransformPoint(c + upL * (top - 0.03f));
        Vector3 tip = it.transform.TransformDirection(it.tipLocal);
        Vector3 up = it.transform.TransformDirection(upL);
        rot = Quaternion.LookRotation(tip, Vector3.Cross(tip, left ? -up : up));
        return true;
    }

    // Follow the hand with a slight delay (weight), then keep the sole above the board / fabric.
    void LateUpdate()
    {
        if (!Held || holder == null) return;
        float dt = Time.deltaTime;
        Vector3 tp = holder.TransformPoint(grabPosOff);
        Quaternion tr = holder.rotation * grabRotOff;
        followPos = Vector3.Lerp(followPos, tp, 1f - Mathf.Exp(-dt * 22f));
        followRot = Quaternion.Slerp(followRot, tr, 1f - Mathf.Exp(-dt * 18f));
        transform.position = followPos;
        transform.rotation = followRot;
        KeepAboveSurface();
    }

    bool IsSurface(Collider c)
    {
        if (c.isTrigger || c.transform.IsChildOf(transform)) return false;
        if (c.GetComponentInParent<PM_Clickable>() != null || c.GetComponentInParent<PM_Hotspot>() != null) return false;
        return c.GetComponent<CharacterController>() == null;
    }

    void KeepAboveSurface()
    {
        float lift = -1f;
        Vector3 normal = Vector3.zero;
        int hitsCount = 0;
        for (int i = 0; i < 5; i++)
        {
            Vector3 lp = localSole;
            if (i > 0) lp += new Vector3(((i & 1) == 0 ? -1 : 1) * soleHalf.x, 0f, (i < 3 ? -1 : 1) * soleHalf.y);
            Vector3 wp = transform.TransformPoint(lp);
            float best = float.MinValue; Vector3 bn = Vector3.up;
            foreach (RaycastHit h in Physics.RaycastAll(wp + Vector3.up * 0.3f, Vector3.down, 0.6f))
            {
                if (!IsSurface(h.collider)) continue;
                if (h.point.y > best) { best = h.point.y; bn = h.normal; }
            }
            if (best == float.MinValue) continue;
            lift = Mathf.Max(lift, best - wp.y);
            normal += bn; hitsCount++;
        }
        if (hitsCount == 0) return;
        normal.Normalize();
        // Close to the surface: the soleplate turns flat onto it (stable gliding, no tipping over).
        if (lift > -0.03f)
        {
            float k = Mathf.Clamp01(1f - (-lift) / 0.03f) * 0.85f;
            Quaternion flat = Quaternion.FromToRotation(DownWorld, -normal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, flat, k);
        }
        if (lift > 0f)
        {
            transform.position += Vector3.up * lift;
            followPos.y = Mathf.Max(followPos.y, transform.position.y);
        }
    }

    public Vector3 SoleWorld { get { return transform.TransformPoint(localSole); } }
    public Vector3 DownWorld { get { return transform.TransformDirection(localDown); } }

    // Backup grab: Grip pressed with the hand close to the iron takes it, even if the hand's own
    // detection missed it (the console tells which interactor was used).
    void ManualGrab()
    {
        Instance = this;
        HandNear = false;
        if (!UnityEngine.XR.XRSettings.isDeviceActive) return;
        if (controllers == null || controllers[0] == null || controllers[1] == null) controllers = FindControllers();
        if (controllers == null) return;
        Bounds b = PM_Util.WorldBounds(transform);
        foreach (Transform c in controllers)
            if (c != null && Vector3.Distance(b.ClosestPoint(c.position), c.position) < 0.25f) HandNear = true;
        if (Held) return;
        for (int i = 0; i < 2; i++)
        {
            var dev = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(i == 0 ? UnityEngine.XR.XRNode.LeftHand : UnityEngine.XR.XRNode.RightHand);
            float g = 0f;
            bool down = dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out g) && g > 0.6f;
            bool pressed = down && !gripWas[i];
            gripWas[i] = down;
            if (!pressed || controllers[i] == null) continue;
            float dist = Vector3.Distance(b.ClosestPoint(controllers[i].position), controllers[i].position);
            if (dist > 0.14f) continue;
            XRBaseInteractor best = null;
            int bestScore = -1;
            foreach (XRBaseInteractor it in controllers[i].GetComponentsInChildren<XRBaseInteractor>(false))
            {
                if (!it.isActiveAndEnabled) continue;
                string n = it.GetType().Name + " " + it.name;
                if (n.Contains("Teleport") || n.Contains("Poke") || n.Contains("Socket")) continue;
                int score = n.Contains("NearFar") || n.Contains("Direct") ? 2 : n.Contains("Ray") ? 1 : 0;
                if (score > bestScore) { bestScore = score; best = it; }
            }
            if (best == null || grab.interactionManager == null)
            {
                Debug.LogWarning("[PM] Grip рядом с утюгом, но в руке нет подходящего Interactor — утюг не взят.");
                continue;
            }
            Debug.Log("[PM] Утюг взят рукой через " + best.GetType().Name + " (" + best.name + ")");
            grab.interactionManager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)best, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grab);
        }
    }

    static Transform[] FindControllers()
    {
        XROrigin origin = FindAnyObjectByType<XROrigin>();
        if (origin == null || origin.CameraFloorOffsetObject == null) return null;
        Transform offset = origin.CameraFloorOffsetObject.transform;
        var res = new Transform[2];
        for (int k = 0; k < offset.childCount; k++)
        {
            Transform c = offset.GetChild(k);
            if (!c.gameObject.activeInHierarchy) continue;
            string n = c.name.ToLower();
            if (!n.Contains("controller") && !n.Contains("hand")) continue;
            int side = n.Contains("left") ? 0 : n.Contains("right") ? 1 : -1;
            if (side < 0) continue;
            if (res[side] == null || c.name.Length < res[side].name.Length) res[side] = c;
        }
        return res;
    }

    void Update()
    {
        ManualGrab();
        // Hot soleplate glow (orange -> white-hot with temperature).
        if (soleMat != null)
        {
            Color hot = Color.Lerp(new Color(1f, 0.25f, 0.02f), new Color(1f, 0.75f, 0.4f), heat);
            soleMat.SetColor("_EmissionColor", hot * (heat * 2.2f * (0.9f + 0.1f * Mathf.Sin(Time.time * 4f))));
        }
        // Haptics in the hand: double "thunk" on grab, warm hum while held, hiss while steaming.
        if (holder != null)
        {
            if (secondPulse > 0f && Time.time >= secondPulse) { PM_Clickable.Haptic(holder, 0.3f, 0.06f); secondPulse = -1f; }
            hapticTimer -= Time.deltaTime;
            if (hapticTimer <= 0f && Touching == null)
            {
                hapticTimer = 0.1f;
                float amp = Steaming ? 0.22f + Random.value * 0.1f : heat * 0.08f * (0.7f + 0.3f * Mathf.Sin(Time.time * 6f));
                if (amp > 0.01f) PM_Clickable.Haptic(holder, amp, 0.11f);
            }
        }
        if (desktopHolding && Time.time > desktopUntil)
        {
            desktopHolding = false;
            returnT = 0f;
            returnFromPos = transform.localPosition;
            returnFromRot = transform.localRotation;
        }
        // Return to the rest when released.
        if (returnT >= 0f && !Held)
        {
            returnT += Time.deltaTime / 0.5f;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(returnT));
            transform.localPosition = Vector3.Lerp(returnFromPos, restLocalPos, t);
            transform.localRotation = Quaternion.Slerp(returnFromRot, restLocalRot, t);
            if (returnT >= 1f) returnT = -1f;
        }

        if (spitting != spitOn)
        {
            spitOn = spitting;
            if (spitting) { spit.Play(); Buzz(0.5f, 0.08f); } else spit.Stop();
        }
        bool shouldSteam = ((Held && TriggerDown) || simulateSteam) && steamAllowed;
        if (shouldSteam != Steaming)
        {
            Steaming = shouldSteam;
            if (Steaming) { steam.Play(); if (steamAudio.clip != null) steamAudio.Play(); }
            else { steam.Stop(); steamAudio.Stop(); }
        }

        // Contact with fabric.
        Vector3 sole = SoleWorld;
        Speed = Time.deltaTime > 0f ? (sole - lastSole).magnitude / Time.deltaTime : 0f;
        lastSole = sole;
        Touching = null;
        if (!Held) return;
        Vector3 down = DownWorld;
        if (Vector3.Dot(down, Vector3.down) < 0.6f) return; // sole must face the board
        RaycastHit[] hits = Physics.RaycastAll(sole - down * 0.05f, down, 0.09f);
        foreach (RaycastHit h in hits)
        {
            var f = h.collider.GetComponent<PM_Fabric>();
            if (f == null) continue;
            Touching = f;
            TouchUV = h.textureCoord;
            break;
        }
        if (Touching != null) PM_Clickable.Haptic(holder, Steaming ? 0.3f : 0.15f, 0.05f);
    }

    public void Buzz(float amplitude, float duration)
    {
        if (holder != null) PM_Clickable.Haptic(holder, amplitude, duration);
    }
}
