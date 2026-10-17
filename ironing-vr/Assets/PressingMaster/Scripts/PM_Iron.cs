using System.Collections.Generic;
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
    Vector3 handleLocal;              // the middle of the handle, where the fist goes around it (own space)
    Vector3 handleDir = Vector3.forward;   // along the handle (own space)
    bool handleKnown, warnedGrip;
    Bounds grabBounds;
    int grabBoundsFrame = -1;
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
        grab.selectFilters.Add(new PM_NearSelectFilter { iron = this });   // only a hand at the iron takes it (no grab from afar)
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
        // The handle slides into the fist (the hand stays where the real hand is; the iron comes to it).
        Vector3 hole;
        if (PM_Hand.HomeHole(PM_Clickable.IsLeft(holder), out hole))
        {
            Vector3 delta = hole - transform.TransformPoint(HandleLocal());
            if (delta.magnitude < 0.3f) grabPosOff = holder.InverseTransformPoint(transform.position + delta);
        }
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
        Vector3 upL = -it.localDown;
        pos = it.transform.TransformPoint(it.HandleLocal());
        Vector3 tip = it.transform.TransformDirection(it.tipLocal);
        Vector3 up = it.transform.TransformDirection(upL);
        rot = Quaternion.LookRotation(tip, Vector3.Cross(tip, left ? -up : up));
        return true;
    }

    // Direction of the handle (along its length), world space.
    public static Vector3 HandleAxis
    {
        get
        {
            PM_Iron it = Instance;
            if (it == null) return Vector3.forward;
            it.HandleLocal();
            return it.transform.TransformDirection(it.handleDir).normalized;
        }
    }

    // The handle is the highest long part of the iron (the body is tall; the hose spring is short): its length
    // is found from the part's own shape (the longest direction across it), not from the way the iron points.
    // The fist goes around it a little under its top. Without such a part: the middle, a little under the top.
    Vector3 HandleLocal()
    {
        if (handleKnown) return handleLocal;
        handleKnown = true;
        Vector3 up = (-localDown).normalized;
        Vector3 e1 = Vector3.Cross(up, Mathf.Abs(up.x) < 0.9f ? Vector3.right : Vector3.forward).normalized;
        Vector3 e2 = Vector3.Cross(up, e1);
        Vector3 c = (localMin + localMax) * 0.5f;
        float top = -99f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 k = new Vector3((i & 1) == 0 ? localMin.x : localMax.x, (i & 2) == 0 ? localMin.y : localMax.y, (i & 4) == 0 ? localMin.z : localMax.z);
            top = Mathf.Max(top, Vector3.Dot(k - c, up));
        }
        float H = top * 2f;
        handleLocal = c + up * (top - 0.03f);
        handleDir = tipLocal - Vector3.Dot(tipLocal, up) * up;
        MeshRenderer best = null;
        float bestTop = -99f, bestLong = 0f;
        var pts = new Vector3[8];
        var cand = new List<KeyValuePair<MeshRenderer, Vector4>>();   // part, (top, length, -, -)
        var candMid = new List<Vector3>();
        var candDir = new List<Vector3>();
        float maxTop = -99f;
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.name.StartsWith("PM_") || !r.enabled) continue;   // (the model's own hose pieces are switched off)
            Bounds lb = r.localBounds;
            float u0 = 99f, u1 = -99f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 k = new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x, (i & 2) == 0 ? lb.min.y : lb.max.y, (i & 4) == 0 ? lb.min.z : lb.max.z);
                pts[i] = transform.InverseTransformPoint(r.transform.TransformPoint(k)) - c;
                float u = Vector3.Dot(pts[i], up);
                u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u);
            }
            if (u1 - u0 > H * 0.6f) continue;
            // longest direction across the part (every 5 degrees) and its middle
            float bestLen = -1f, wid = 0f; Vector3 dir = e1, mid = Vector3.zero;
            for (int a = 0; a < 180; a += 5)
            {
                float rad = a * Mathf.Deg2Rad;
                Vector3 d = e1 * Mathf.Cos(rad) + e2 * Mathf.Sin(rad), n = Vector3.Cross(up, d);
                float d0 = 99f, d1 = -99f, n0 = 99f, n1 = -99f;
                for (int i = 0; i < 8; i++)
                {
                    float x = Vector3.Dot(pts[i], d), y = Vector3.Dot(pts[i], n);
                    d0 = Mathf.Min(d0, x); d1 = Mathf.Max(d1, x); n0 = Mathf.Min(n0, y); n1 = Mathf.Max(n1, y);
                }
                if (d1 - d0 > bestLen) { bestLen = d1 - d0; wid = n1 - n0; dir = d; mid = d * ((d0 + d1) * 0.5f) + n * ((n0 + n1) * 0.5f); }
            }
            if (bestLen < 0.06f || bestLen < wid * 1.5f) continue;   // not long enough to be the handle
            cand.Add(new KeyValuePair<MeshRenderer, Vector4>(r, new Vector4(u1, bestLen, u1 - u0, 0f)));
            candMid.Add(mid); candDir.Add(dir);
            maxTop = Mathf.Max(maxTop, u1);
        }
        // of the long parts at the top of the iron (within 4 cm), the longest: the handle, not the short spring
        for (int i = 0; i < cand.Count; i++)
        {
            Vector4 v = cand[i].Value;
            if (v.x < maxTop - 0.04f || v.y <= bestLong) continue;
            best = cand[i].Key; bestTop = v.x; bestLong = v.y;
            handleLocal = c + candMid[i] + up * (v.x - Mathf.Min(0.018f, v.z * 0.35f));
            handleDir = candDir[i];
        }
        if (handleDir.sqrMagnitude < 1e-6f) handleDir = Vector3.forward;
        handleDir.Normalize();
        if (Vector3.Dot(handleDir, tipLocal) < 0f) handleDir = -handleDir;   // to the tip
        Debug.Log(best != null ? "[PM] Ручка утюга: деталь " + best.name + ", длина " + (bestLong * 100f).ToString("0") + " см, высота над центром " + (bestTop * 100f).ToString("0") + " см"
                               : "[PM] Ручка утюга: отдельной детали нет — середина утюга");
        return handleLocal;
    }

    // Size of the iron for the hand checks (only its solid parts: the steam has no size while it is off).
    Bounds GrabBounds()
    {
        if (grabBoundsFrame != Time.frameCount)
        {
            grabBoundsFrame = Time.frameCount;
            grabBounds = PM_Util.MeshBounds(transform);
        }
        return grabBounds;
    }
    public bool HandAt(Vector3 p, float dist) { Bounds b = GrabBounds(); return Vector3.Distance(b.ClosestPoint(p), p) < dist; }

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
        KeepOutOfBody();
        KeepOutOfGown();
        KeepOutOfSolids();
        KeepAboveSurface();
    }

    bool IsSurface(Collider c)
    {
        if (c.isTrigger || c.transform.IsChildOf(transform)) return false;
        if (c.GetComponentInParent<PM_Clickable>() != null || c.GetComponentInParent<PM_Hotspot>() != null) return false;
        if (c.GetComponent<PM_Gown>() != null) return false;   // the gown is steamed from the side (see KeepOutOfGown)
        return c.GetComponent<CharacterController>() == null;
    }

    // Highest surface under the sole points: lift (meters, >0 = the sole is below it) and its mean normal.
    bool MeasureSurface(out float lift, out Vector3 normal)
    {
        lift = -1f; normal = Vector3.zero;
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
            lift = Mathf.Max(lift, best + soleExtra - wp.y);
            normal += bn; hitsCount++;
        }
        if (hitsCount == 0) return false;
        normal.Normalize();
        return true;
    }

    void KeepAboveSurface()
    {
        float lift; Vector3 normal;
        if (!MeasureSurface(out lift, out normal)) return;
        // Close to the surface: the soleplate turns flat onto it — fully flat when it touches,
        // so the whole sole lies on the fabric (no end hanging in the air).
        if (lift > -0.04f)
        {
            float k = Mathf.Clamp01(1f - (-lift - 0.005f) / 0.035f);
            Quaternion flat = Quaternion.FromToRotation(DownWorld, -normal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, flat, k);
            if (!MeasureSurface(out lift, out normal)) return;   // turning moved the sole: measure again
        }
        if (lift > 0f)
        {
            transform.position += Vector3.up * lift;
            followPos.y = Mathf.Max(followPos.y, transform.position.y);
        }
    }

    public float soleExtra;   // a silicone shoe under the sole lifts the iron a little

    // The iron cannot go into the station body, the water tank or a tool from the side either
    // (the board/fabric surface is handled by KeepAboveSurface).
    readonly Collider[] near = new Collider[24];
    void KeepOutOfSolids()
    {
        if (ownColliders == null) ownColliders = GetComponentsInChildren<Collider>(true);
        Vector3 c = transform.TransformPoint((localMin + localMax) * 0.5f);
        Vector3 half = (localMax - localMin) * 0.5f;
        int n = Physics.OverlapBoxNonAlloc(c, half, near, transform.rotation, ~0, QueryTriggerInteraction.Collide);
        for (int k = 0; k < n; k++)
        {
            Collider o = near[k];
            if (o == null) continue;
            bool solid = o.isTrigger ? o.GetComponent<PM_Solid>() != null : IsSurface(o);
            if (!solid || o.GetComponent<PM_Fabric>() != null || o.GetComponent<PM_Gown>() != null) continue;
            foreach (Collider mine in ownColliders)
            {
                if (mine == null || !mine.enabled || mine.isTrigger) continue;
                Vector3 dir; float dist;
                if (!Physics.ComputePenetration(mine, mine.transform.position, mine.transform.rotation, o, o.transform.position, o.transform.rotation, out dir, out dist)) continue;
                if (dir.y > 0.7f && !o.isTrigger) continue;   // lying on a real surface: KeepAboveSurface does that smoothly
                Vector3 push = dir * dist;
                transform.position += push;
                followPos += push;
            }
        }
    }

    // The iron cannot go into the gown on the dress form: pushed back out of the cloth.
    Collider[] ownColliders;
    void KeepOutOfGown()
    {
        PM_Gown g = PM_Gown.Instance;
        if (g == null || g.Collider == null || !g.isActiveAndEnabled) return;
        Collider gc = g.Collider;
        if (!PM_Util.WorldBounds(transform).Intersects(gc.bounds)) return;
        if (ownColliders == null) ownColliders = GetComponentsInChildren<Collider>(true);
        Vector3 total = Vector3.zero;
        foreach (Collider c in ownColliders)
        {
            if (c == null || !c.enabled || c.isTrigger) continue;
            Vector3 dir; float dist;
            if (Physics.ComputePenetration(c, c.transform.position + total, c.transform.rotation, gc, gc.transform.position, gc.transform.rotation, out dir, out dist))
                total += dir * dist;
        }
        if (total.sqrMagnitude > 0f)
        {
            transform.position += total;
            followPos += total;
        }
    }

    // The iron never goes into the player's own body (the cartoon body stands under the eyes):
    // all corners of the iron are checked (not only its centre), from the thighs up to the chest.
    void KeepOutOfBody()
    {
        Vector3 a, b; float r;
        if (!PM_ToonBody.Capsule(out a, out b, out r)) return;
        for (int pass = 0; pass < 3; pass++)
        {
            float worst = 0f; Vector3 push = Vector3.zero;
            for (int i = 0; i < 27; i++)   // corners, edge middles, face middles and the centre of the iron's box
            {
                Vector3 lp = new Vector3(Mathf.Lerp(localMin.x, localMax.x, (i % 3) * 0.5f),
                                         Mathf.Lerp(localMin.y, localMax.y, (i / 3 % 3) * 0.5f),
                                         Mathf.Lerp(localMin.z, localMax.z, (i / 9) * 0.5f));
                Vector3 p = transform.TransformPoint(lp);
                Vector3 ab = b - a;
                float t = Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f);
                if (t < -0.05f || t > 1.05f) continue;            // above the head or below the knees: free
                Vector3 q = a + ab * Mathf.Clamp01(t);
                Vector3 d = p - q; d.y = 0f;
                float dist = d.magnitude, pen = r - dist;
                if (pen <= worst) continue;
                worst = pen;
                push = (dist > 0.001f ? d / dist : PM_ToonBody.Forward) * pen;
            }
            if (worst <= 0f) return;
            transform.position += push;
            followPos += push;
        }
    }

    // Silicone shoe (an exam reward): a soft blue cover under the soleplate.
    // Silicone shoe: a cover in the shape of the soleplate (pointed front, straight back) with a low rim,
    // perforated all over so the steam still comes through.
    public GameObject AddSiliconeShoe()
    {
        Vector3 size = localMax - localMin;
        Vector3 side = Vector3.Cross(localDown, tipLocal).normalized;
        float L = Mathf.Abs(Vector3.Dot(size, tipLocal.normalized)) * 1.0f;
        float W = Mathf.Abs(Vector3.Dot(size, side)) * 1.02f;
        var go = new GameObject("PM_SiliconeShoe");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.LookRotation(-tipLocal, -localDown);   // the shoe's pointed end (+z) was at the back: turned to the tip
        go.transform.localPosition = localSole;
        go.AddComponent<MeshFilter>().sharedMesh = ShoeMesh(L, W);
        var m = new Material(PM_Util.LitShader());
        m.SetTexture("_BaseMap", ShoeTexture()); m.SetTexture("_MainTex", ShoeTexture());
        m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.45f);
        m.SetFloat("_Cull", 0f); m.doubleSidedGI = true;   // the rim is seen from both sides
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        soleExtra = 0.007f;
        return go;
    }

    // Outline of a soleplate (x = across, z = along: rear -L/2 .. tip +L/2): straight back with round corners,
    // narrowing to a point at the front. t = 0..1 goes once around.
    static Vector2 SolePoint(float t, float L, float W)
    {
        float s = t * 2f;                       // 0..1 right side rear->tip, 1..2 left side tip->rear
        float zn = s <= 1f ? s : 2f - s;
        float hw = W * 0.5f * Mathf.Min(1f, Mathf.Sqrt(Mathf.Max(0f, zn) / 0.06f));
        if (zn > 0.45f) hw *= Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow((zn - 0.45f) / 0.55f, 1.7f)), 0.6f);
        float x = s <= 1f ? hw : -hw;
        return new Vector2(x, -L * 0.5f + zn * L);
    }

    static Mesh ShoeMesh(float L, float W)
    {
        const int N = 64;
        const float bottom = -0.006f, lip = 0.008f, grow = 0.003f;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        // bottom: centre + outline (faces down)
        v.Add(new Vector3(0f, bottom, 0f)); uv.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i < N; i++)
        {
            Vector2 p = SolePoint(i / (float)N, L, W);
            v.Add(new Vector3(p.x, bottom, p.y)); uv.Add(new Vector2(p.x / W + 0.5f, p.y / L + 0.5f));
        }
        for (int i = 0; i < N; i++) { tri.Add(0); tri.Add(1 + i); tri.Add(1 + (i + 1) % N); }
        // rim: from the bottom edge up around the sole sides
        int b0 = v.Count;
        for (int i = 0; i < N; i++)
        {
            Vector2 p = SolePoint(i / (float)N, L, W);
            float pm = Mathf.Sqrt(p.x * p.x + p.y * p.y);
            Vector2 o = pm > 1e-5f ? new Vector2(p.x / pm * grow, p.y / pm * grow) : new Vector2(0f, 0f);
            v.Add(new Vector3(p.x + o.x, bottom, p.y + o.y)); uv.Add(new Vector2(0.02f, 0.02f));
            v.Add(new Vector3(p.x + o.x, lip, p.y + o.y)); uv.Add(new Vector2(0.02f, 0.02f));
        }
        for (int i = 0; i < N; i++)
        {
            int a0 = b0 + i * 2, a1 = a0 + 1, c0 = b0 + ((i + 1) % N) * 2, c1 = c0 + 1;
            // one side only (the material is double-sided): the same triangles twice with opposite turns made the
            // rim's normals cancel to zero -> broken pixels that the glow blew up into a big white disc
            tri.Add(a0); tri.Add(a1); tri.Add(c0); tri.Add(c0); tri.Add(a1); tri.Add(c1);
        }
        var m = new Mesh { name = "PM_SiliconeShoe" };
        m.vertices = v.ToArray(); m.uv = uv.ToArray(); m.SetTriangles(tri.ToArray(), 0);
        // the bottom must face down
        Vector3 n = Vector3.Cross(v[tri[1]] - v[tri[0]], v[tri[2]] - v[tri[0]]);
        if (n.y > 0f) { for (int i = 0; i < N * 3; i += 3) { int t = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = t; } m.SetTriangles(tri.ToArray(), 0); }
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static Texture2D shoeTex;
    // Light blue silicone with steam holes in rows all over the surface.
    static Texture2D ShoeTexture()
    {
        if (shoeTex != null) return shoeTex;
        const int w = 256, h = 512;
        var px = new Color32[w * h];
        Color32 sil = new Color32(70, 160, 220, 255), hole = new Color32(18, 28, 40, 255), rim = new Color32(50, 130, 190, 255);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = (x % 22) - 11f, cy = (y % 22) - 11f;
                if (((y / 22) & 1) == 1) cx = ((x + 11) % 22) - 11f;   // staggered rows
                bool isHole = cx * cx + cy * cy < 16f && x > 14 && x < w - 14 && y > 14 && y < h - 14;
                px[y * w + x] = isHole ? hole : (cx * cx + cy * cy < 30f ? rim : sil);
            }
        shoeTex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        shoeTex.SetPixels32(px); shoeTex.Apply(true);
        return shoeTex;
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
        Bounds b = GrabBounds();
        foreach (Transform c in controllers)
            if (c != null && Vector3.Distance(b.ClosestPoint(c.position), c.position) < 0.10f) HandNear = true;   // the hand is at the iron (not just near)
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
                if (!warnedGrip) Debug.LogWarning("[PM] Grip рядом с утюгом, но в руке нет подходящего Interactor — утюг не взят.");
                warnedGrip = true;
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
            // the one that really has an active interactor wins (a rig may have both "Controller" and "Hand"); then the shortest name
            bool live = c.GetComponentInChildren<XRBaseInteractor>(false) != null;
            bool curLive = res[side] != null && res[side].GetComponentInChildren<XRBaseInteractor>(false) != null;
            if (res[side] == null || (live && !curLive) || (live == curLive && c.name.Length < res[side].name.Length)) res[side] = c;
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
        // Steam = the trigger of the hand that holds the iron, read straight from the controller
        // (does not depend on the rig's "activate" setup).
        bool trig = TriggerDown;
        if (Held && holder != null)
        {
            var dev = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(PM_Clickable.IsLeft(holder) ? UnityEngine.XR.XRNode.LeftHand : UnityEngine.XR.XRNode.RightHand);
            float tv;
            if (dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out tv) && tv > 0.55f) trig = true;
        }
        bool shouldSteam = ((Held && trig) || simulateSteam) && steamAllowed;
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
        if (Vector3.Dot(down, Vector3.down) < 0.45f) return; // sole must face the board (a curved pad tilts it a little)
        // The middle of the sole first, then four points around it: on a small curved pad (chest, collar)
        // the iron still irons when it is a little off the middle or moves diagonally.
        for (int k = 0; k < 5 && Touching == null; k++)
        {
            Vector3 lp = localSole;
            if (k > 0) lp += new Vector3(((k & 1) == 0 ? -1 : 1) * soleHalf.x * 0.75f, 0f, (k < 3 ? -1 : 1) * soleHalf.y * 0.75f);
            Vector3 wp = transform.TransformPoint(lp);
            foreach (RaycastHit h in Physics.RaycastAll(wp - down * 0.05f, down, 0.10f))
            {
                var f = h.collider.GetComponent<PM_Fabric>();
                if (f == null) continue;
                Touching = f;
                TouchUV = h.textureCoord;
                break;
            }
        }
        if (Touching != null) PM_Clickable.Haptic(holder, Steaming ? 0.3f : 0.15f, 0.05f);
    }

    public void Buzz(float amplitude, float duration)
    {
        if (holder != null) PM_Clickable.Haptic(holder, amplitude, duration);
    }
}

// The iron is taken only by a hand that is really at it (about 18 cm): no grabbing from afar with the pointing
// ray — that made the hand jump to the iron and the iron hang far from the real hand.
public class PM_NearSelectFilter : UnityEngine.XR.Interaction.Toolkit.Filtering.IXRSelectFilter
{
    public PM_Iron iron;
    public bool canProcess { get { return iron != null && iron.isActiveAndEnabled; } }

    public bool Process(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor,
                        UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable interactable)
    {
        if (iron.Held) return true;   // keeps a hold that already started (pressing on the board may pull the hand away a bit)
        return iron.HandAt(interactor.transform.position, 0.18f);
    }
}
