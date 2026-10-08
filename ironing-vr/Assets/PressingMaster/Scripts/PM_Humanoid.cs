using UnityEngine;
using UnityEngine.XR;

// A Humanoid character (Asset Store / Mixamo, Rig = Humanoid) as the player's body:
// it stands under the eyes, turns with the head, and its arms reach the controllers (two-bone IK);
// fingers follow Grip / Trigger. While holding the iron, the hand sits on the iron's handle.
[DefaultExecutionOrder(210)]
public class PM_Humanoid : MonoBehaviour
{
    Transform root, head, cam, origin;
    readonly Transform[] arm = new Transform[6];          // L upper, L lower, L hand, R upper, R lower, R hand
    readonly Quaternion[] armRest = new Quaternion[6];
    readonly Quaternion[] handOffset = new Quaternion[2]; // hand bone relative to the grip frame
    Transform[][] fingers = new Transform[2][];
    Quaternion[][] fingerRest = new Quaternion[2][];
    Vector3[][] fingerAxis = new Vector3[2][];
    Transform[] controllers = new Transform[2];
    Vector3 eyeLocal;
    float eyeModel = 1.6f, eyeReal = 1.0f, yaw;
    readonly float[] grip = new float[2], trig = new float[2];

    public static GameObject Create(GameObject prefab, Transform xrOrigin, Transform leftCtrl, Transform rightCtrl)
    {
        if (prefab == null) return null;
        GameObject inst = Instantiate(prefab);
        inst.name = "PM_Character";
        Animator an = inst.GetComponentInChildren<Animator>();
        if (an == null || !an.isHuman)
        {
            Debug.LogWarning("[PM] Персонаж «" + prefab.name + "» не Humanoid: выберите модель → Inspector → Rig → Animation Type = Humanoid → Apply.");
            Destroy(inst);
            return null;
        }
        foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) Destroy(c);
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
        var h = inst.AddComponent<PM_Humanoid>();
        h.origin = xrOrigin;
        h.controllers[0] = leftCtrl; h.controllers[1] = rightCtrl;
        if (!h.Setup(an)) { Destroy(inst); return null; }
        Debug.Log("[PM] Персонаж-аватар: " + prefab.name);
        return inst;
    }

    // A character that is already in the scene (for example a Genies avatar spawned by its loader).
    public static GameObject Adopt(Animator an, Transform xrOrigin, Transform leftCtrl, Transform rightCtrl)
    {
        if (an == null || !an.isHuman) return null;
        GameObject go = an.gameObject;
        if (go.GetComponent<PM_Humanoid>() != null) return go;
        // The loader's own character controller (walking, gravity, touch joystick) must not move the body.
        Transform spawn = null;
        for (Transform p = go.transform; p != null; p = p.parent)
        {
            bool ctrl = false;
            foreach (CharacterController cc in p.GetComponents<CharacterController>()) { cc.enabled = false; ctrl = true; }
            foreach (Rigidbody rb in p.GetComponents<Rigidbody>()) rb.isKinematic = true;
            foreach (MonoBehaviour mb in p.GetComponents<MonoBehaviour>())
            {
                if (mb == null || mb is PM_Humanoid) continue;
                string n = mb.GetType().Name;
                if (!n.Contains("Controller") && !n.Contains("Locomotion") && !n.Contains("Movement")) continue;
                mb.enabled = false; ctrl = true;
                Debug.Log("[PM] Управление персонажем из демо отключено: " + n);
            }
            if (ctrl) spawn = p;
        }
        foreach (Light l in (spawn != null ? spawn : go.transform).GetComponentsInChildren<Light>(true)) l.enabled = false;
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;   // the iron must not land on the body
        var h = go.AddComponent<PM_Humanoid>();
        h.origin = xrOrigin;
        h.controllers[0] = leftCtrl; h.controllers[1] = rightCtrl;
        Quaternion r0 = go.transform.rotation; Vector3 p0 = go.transform.position;
        go.transform.rotation = Quaternion.identity;   // measure the rest pose facing +Z
        go.transform.position = Vector3.zero;
        an.applyRootMotion = false;
        if (!h.Setup(an, true)) { Destroy(h); go.transform.SetPositionAndRotation(p0, r0); return null; }
        Debug.Log("[PM] Аватар из сцены стал телом игрока: " + go.name);
        return go;
    }

    // Finds a Humanoid character in the scene that is not ours (Genies or any other loader).
    static readonly System.Collections.Generic.HashSet<Animator> reported = new System.Collections.Generic.HashSet<Animator>();
    public static Animator FindSceneCharacter()
    {
        foreach (Animator a in Resources.FindObjectsOfTypeAll<Animator>())
        {
            if (a == null || !a.gameObject.scene.IsValid() || !a.isActiveAndEnabled) continue;
            if (a.GetComponent<PM_Humanoid>() != null || a.GetComponentInParent<PM_Humanoid>() != null) continue;
            if (a.name.StartsWith("PM_")) continue;
            if (!a.isHuman)
            {
                if (a.GetComponentInChildren<SkinnedMeshRenderer>() != null && reported.Add(a))
                    Debug.LogWarning("[PM] Персонаж «" + a.name + "» в сцене не Humanoid (Avatar: " + (a.avatar != null ? a.avatar.name : "нет") + ") — телом игрока он стать не может.");
                continue;
            }
            return a;
        }
        return null;
    }

    // keepAnimator: a loaded avatar keeps its idle animation (and its body-shape rig); arms, hands and
    // fingers are overridden every frame after the animation.
    bool Setup(Animator an, bool keepAnimator = false)
    {
        root = transform;
        head = an.GetBoneTransform(HumanBodyBones.Head);
        HumanBodyBones[] ab = { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                                HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand };
        for (int i = 0; i < 6; i++) { arm[i] = an.GetBoneTransform(ab[i]); if (arm[i] == null) return false; armRest[i] = arm[i].localRotation; }
        if (head == null) return false;
        // Eyes: a bit above and in front of the head bone.
        Transform eyeL = an.GetBoneTransform(HumanBodyBones.LeftEye), eyeR = an.GetBoneTransform(HumanBodyBones.RightEye);
        Vector3 eye = eyeL != null && eyeR != null ? (eyeL.position + eyeR.position) * 0.5f + Vector3.forward * 0.02f
                                                   : head.position + Vector3.up * 0.09f + Vector3.forward * 0.09f;
        eyeLocal = root.InverseTransformPoint(eye);
        eyeModel = Mathf.Max(0.5f, eye.y - root.position.y);
        // Fingers + the hand's own frame (fingers direction, thumb side) in the rest pose.
        for (int s = 0; s < 2; s++)
        {
            bool left = s == 0;
            Transform hand = arm[s * 3 + 2];
            Transform mid = an.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            Transform idx = an.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            Transform lit = an.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            Vector3 f = mid != null ? (mid.position - hand.position).normalized : (hand.position - arm[s * 3 + 1].position).normalized;
            Vector3 t = idx != null && lit != null ? (idx.position - lit.position) : Vector3.forward;
            t = (t - Vector3.Dot(t, f) * f).normalized;
            if (t.sqrMagnitude < 0.01f) t = Vector3.up;
            Quaternion frame = Quaternion.LookRotation(f, t);
            handOffset[s] = Quaternion.Inverse(frame) * hand.rotation;
            Vector3 palm = left ? Vector3.Cross(t, f) : Vector3.Cross(f, t);
            Vector3 axisW = Vector3.Cross(f, palm);
            var list = new System.Collections.Generic.List<Transform>();
            HumanBodyBones first = left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal;
            for (int k = 0; k < 12; k++)
            {
                Transform b = an.GetBoneTransform(first + k);   // index, middle, ring, little × 3 joints
                list.Add(b);
            }
            fingers[s] = list.ToArray();
            fingerRest[s] = new Quaternion[12];
            fingerAxis[s] = new Vector3[12];
            for (int k = 0; k < 12; k++)
            {
                if (fingers[s][k] == null) continue;
                fingerRest[s][k] = fingers[s][k].localRotation;
                fingerAxis[s][k] = Quaternion.Inverse(fingers[s][k].rotation) * axisW;
            }
        }
        if (!keepAnimator) an.enabled = false;   // bones are driven by this script
        foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = true;
        cam = Camera.main != null ? Camera.main.transform : null;
        HideHead();
        return true;
    }

    void LateUpdate()
    {
        if (cam == null) { cam = Camera.main != null ? Camera.main.transform : null; if (cam == null) return; }
        if (Time.unscaledTime > nextHeadCheck) { nextHeadCheck = Time.unscaledTime + 1f; HideHead(); }   // loaders may swap meshes later
        SyncHeadCopies();
        float floorY = origin != null ? origin.position.y : cam.position.y - 1.6f;
        eyeReal = Mathf.Max(eyeReal, Mathf.Clamp(cam.position.y - floorY, 1.0f, 1.95f));
        float sc = eyeReal / eyeModel;
        Vector3 f = cam.forward; f.y = 0;
        if (f.sqrMagnitude > 0.001f)
        {
            float target = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            float diff = Mathf.DeltaAngle(yaw, target);
            if (Mathf.Abs(diff) > 25f) yaw += diff * Mathf.Min(1f, Time.deltaTime * 4f);
        }
        Quaternion rot = Quaternion.Euler(0, yaw, 0);
        root.localScale = Vector3.one * sc;
        root.rotation = rot;
        root.position = cam.position - rot * (eyeLocal * sc) - rot * Vector3.forward * 0.06f;

        for (int s = 0; s < 2; s++)
        {
            bool left = s == 0;
            for (int i = 0; i < 3; i++) arm[s * 3 + i].localRotation = armRest[s * 3 + i];
            Vector3 gp; Quaternion gr;
            if (!PM_Iron.HandleGrip(left, out gp, out gr))
            {
                if (controllers[s] == null) continue;
                gp = controllers[s].position; gr = controllers[s].rotation;
            }
            Vector3 wrist = gp + gr * new Vector3((left ? -0.03f : 0.03f) * sc, -0.01f * sc, -0.075f * sc);
            Vector3 outward = rot * (left ? Vector3.left : Vector3.right);
            Solve(arm[s * 3], arm[s * 3 + 1], arm[s * 3 + 2], wrist, Vector3.down + outward * 0.6f - rot * Vector3.forward * 0.3f);
            arm[s * 3 + 2].rotation = gr * handOffset[s];
            Fingers(s, left);
        }
    }

    // ----- the own head: the player looks out of it, so only the studio mirror draws it -----
    float nextHeadCheck;
    readonly System.Collections.Generic.HashSet<Mesh> splitMeshes = new System.Collections.Generic.HashSet<Mesh>();
    readonly System.Collections.Generic.Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer> headCopies =
        new System.Collections.Generic.Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer>();

    bool IsHeadPart(Transform b) { return b != null && head != null && b.IsChildOf(head); }

    void HideHead()
    {
        Camera main = cam != null ? cam.GetComponent<Camera>() : Camera.main;
        if (main != null)
        {
            main.cullingMask &= ~(1 << PM_Avatar.MirrorLayer);
            main.nearClipPlane = Mathf.Max(main.nearClipPlane, 0.05f);
        }
        foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.gameObject.layer == PM_Avatar.MirrorLayer || r.sharedMesh == null || splitMeshes.Contains(r.sharedMesh)) continue;
            SplitHead(r);
        }
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))   // hats, glasses, earrings on the head bone
            if (IsHeadPart(r.transform)) r.gameObject.layer = PM_Avatar.MirrorLayer;
    }

    // Moves the triangles that follow the head (face, hair, hat) into a copy drawn on the mirror-only layer.
    void SplitHead(SkinnedMeshRenderer r)
    {
        Mesh m = r.sharedMesh;
        splitMeshes.Add(m);
        Transform[] bones = r.bones;
        BoneWeight[] w = m.isReadable ? m.boneWeights : null;
        if (w == null || w.Length == 0 || bones == null || bones.Length == 0)
        {
            if (IsHeadPart(r.rootBone != null ? r.rootBone : r.transform)) r.gameObject.layer = PM_Avatar.MirrorLayer;
            return;
        }
        var headBone = new bool[bones.Length];
        for (int i = 0; i < bones.Length; i++) headBone[i] = IsHeadPart(bones[i]);
        var headVert = new bool[w.Length];
        for (int v = 0; v < w.Length; v++)
        {
            BoneWeight b = w[v];
            int bi = b.boneIndex0; float bw = b.weight0;
            if (b.weight1 > bw) { bi = b.boneIndex1; bw = b.weight1; }
            if (b.weight2 > bw) { bi = b.boneIndex2; bw = b.weight2; }
            if (b.weight3 > bw) { bi = b.boneIndex3; bw = b.weight3; }
            headVert[v] = bi >= 0 && bi < headBone.Length && headBone[bi];
        }
        int subs = m.subMeshCount, nHead = 0, nBody = 0;
        var headTris = new System.Collections.Generic.List<int>[subs];
        var bodyTris = new System.Collections.Generic.List<int>[subs];
        for (int s = 0; s < subs; s++)
        {
            headTris[s] = new System.Collections.Generic.List<int>();
            bodyTris[s] = new System.Collections.Generic.List<int>();
            int[] t = m.GetTriangles(s);
            for (int k = 0; k + 2 < t.Length; k += 3)
            {
                bool hd = headVert[t[k]] || headVert[t[k + 1]] || headVert[t[k + 2]];
                var list = hd ? headTris[s] : bodyTris[s];
                list.Add(t[k]); list.Add(t[k + 1]); list.Add(t[k + 2]);
                if (hd) nHead++; else nBody++;
            }
        }
        if (nHead == 0) return;
        if (nBody == 0) { r.gameObject.layer = PM_Avatar.MirrorLayer; return; }

        Mesh body = Instantiate(m); body.name = m.name + "_PMbody";
        Mesh hm = Instantiate(m); hm.name = m.name + "_PMhead";
        for (int s = 0; s < subs; s++) { body.SetTriangles(bodyTris[s], s); hm.SetTriangles(headTris[s], s); }
        splitMeshes.Add(body); splitMeshes.Add(hm);

        SkinnedMeshRenderer old;
        if (headCopies.TryGetValue(r, out old) && old != null) Destroy(old.gameObject);
        var go = new GameObject(r.name + "_PMHead");
        go.layer = PM_Avatar.MirrorLayer;
        go.transform.SetParent(r.transform.parent, false);
        go.transform.localPosition = r.transform.localPosition;
        go.transform.localRotation = r.transform.localRotation;
        go.transform.localScale = r.transform.localScale;
        var hr = go.AddComponent<SkinnedMeshRenderer>();
        hr.sharedMesh = hm;
        hr.bones = bones;
        hr.rootBone = r.rootBone;
        hr.sharedMaterials = r.sharedMaterials;
        hr.localBounds = r.localBounds;
        hr.updateWhenOffscreen = true;
        hr.shadowCastingMode = r.shadowCastingMode;
        var block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block); hr.SetPropertyBlock(block);
        r.sharedMesh = body;
        headCopies[r] = hr;
    }

    void SyncHeadCopies()
    {
        foreach (var kv in headCopies)
        {
            SkinnedMeshRenderer src = kv.Key, dst = kv.Value;
            if (src == null || dst == null) continue;
            dst.enabled = src.enabled && src.gameObject.activeInHierarchy;
            int n = dst.sharedMesh != null ? dst.sharedMesh.blendShapeCount : 0;
            for (int i = 0; i < n; i++) dst.SetBlendShapeWeight(i, src.GetBlendShapeWeight(i));   // blinking, expressions
        }
    }

    static void Solve(Transform u, Transform l, Transform h, Vector3 target, Vector3 poleHint)
    {
        float a = Vector3.Distance(u.position, l.position), b = Vector3.Distance(l.position, h.position);
        Vector3 to = target - u.position;
        float d = Mathf.Clamp(to.magnitude, 0.01f, a + b - 0.001f);
        Vector3 dir = to.normalized;
        Vector3 pole = (poleHint - Vector3.Dot(poleHint, dir) * dir).normalized;
        float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
        Vector3 elbow = u.position + dir * (a * cosA) + pole * (a * Mathf.Sqrt(1f - cosA * cosA));
        u.rotation = Quaternion.FromToRotation(l.position - u.position, elbow - u.position) * u.rotation;
        l.rotation = Quaternion.FromToRotation(h.position - l.position, u.position + dir * d - l.position) * l.rotation;
    }

    void Fingers(int s, bool left)
    {
        InputDevice dev = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
        float g = 0f, t = 0f;
        if (dev.isValid) { dev.TryGetFeatureValue(CommonUsages.grip, out g); dev.TryGetFeatureValue(CommonUsages.trigger, out t); }
        grip[s] = Mathf.Lerp(grip[s], g, Time.deltaTime * 18f);
        trig[s] = Mathf.Lerp(trig[s], t, Time.deltaTime * 18f);
        float[] maxA = { 65f, 90f, 60f };
        for (int k = 0; k < 12; k++)
        {
            Transform b = fingers[s][k];
            if (b == null) continue;
            float c = k < 3 ? 0.08f + trig[s] * 0.92f : 0.1f + grip[s] * 0.9f;
            b.localRotation = fingerRest[s][k] * Quaternion.AngleAxis(maxA[k % 3] * c, fingerAxis[s][k]);
        }
    }
}
