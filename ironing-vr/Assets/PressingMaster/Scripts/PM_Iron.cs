using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// The professional iron: grab with Grip, steam with Trigger, returns to its rest when released.
// It is a physical body while held (velocity tracking): it lies ON the board and the fabric, it does not pass through.
public class PM_Iron : MonoBehaviour
{
    public bool Held { get { return grab != null && grab.isSelected; } }
    public bool TriggerDown { get; private set; }
    public bool Steaming { get; private set; }
    public bool steamAllowed;
    public bool simulateSteam;             // keyboard test (Left Shift) without the headset              // set by the game (power on + pressure ok)

    // Contact with fabric (updated every frame).
    public PM_Fabric Touching { get; private set; }
    public Vector2 TouchUV { get; private set; }
    public float Speed { get; private set; }
    public float SoleRadius { get; private set; }

    public System.Action onGrab;
    public static Transform CurrentHolder { get; private set; }   // the hand (interactor) holding the iron
    public float heat;                     // 0..1 set by the station (temperature button)
    Material soleMat;
    float hapticTimer, secondPulse = -1f;
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
    ParticleSystem steam;
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
        rb.isKinematic = true;          // on the rest: kinematic; XRI makes it physical while held
        rb.useGravity = false;
        rb.mass = 1.6f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        grab = gameObject.AddComponent<XRGrabInteractable>();
        // Velocity tracking = real physics: the board and the fabric stop the iron (no passing through).
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.smoothPosition = true;
        grab.smoothPositionAmount = 12f;
        grab.smoothRotation = true;
        grab.smoothRotationAmount = 10f;

        // The player's body must not push the iron around.
        foreach (CharacterController cc in Resources.FindObjectsOfTypeAll<CharacterController>())
        {
            if (!cc.gameObject.scene.IsValid()) continue;
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) Physics.IgnoreCollision(c, cc);
        }
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

    public Vector3 SoleWorld { get { return transform.TransformPoint(localSole); } }
    public Vector3 DownWorld { get { return transform.TransformDirection(localDown); } }

    void Update()
    {
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
