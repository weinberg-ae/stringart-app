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

    bool Setup(Animator an)
    {
        root = transform;
        head = an.GetBoneTransform(HumanBodyBones.Head);
        HumanBodyBones[] ab = { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                                HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand };
        for (int i = 0; i < 6; i++) { arm[i] = an.GetBoneTransform(ab[i]); if (arm[i] == null) return false; armRest[i] = arm[i].localRotation; }
        if (head == null) return false;
        // Eyes: a bit above and in front of the head bone.
        Vector3 eye = head.position + Vector3.up * 0.09f + Vector3.forward * 0.09f;
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
        an.enabled = false;   // bones are driven by this script
        cam = Camera.main != null ? Camera.main.transform : null;
        if (Camera.main != null) Camera.main.nearClipPlane = Mathf.Max(Camera.main.nearClipPlane, 0.07f);   // don't see inside the own head
        return true;
    }

    void LateUpdate()
    {
        if (cam == null) { cam = Camera.main != null ? Camera.main.transform : null; if (cam == null) return; }
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
