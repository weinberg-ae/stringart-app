using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// A pressing tool shown on the board in the tools lesson: the hand (Grip) can take it, move and turn it to
// look at it from every side. Let go, it stands on the board again (keeping its new direction).
// Only active while the tool is shown in the lesson (not on the shelf, not in the exercises).
public class PM_ToolHandle : MonoBehaviour
{
    XRGrabInteractable grab;
    Transform board;          // the board point the tool stands on (its height and centre)
    bool layDown;

    public static PM_ToolHandle Get(Transform tool)
    {
        var h = tool.GetComponent<PM_ToolHandle>();
        if (h != null) return h;
        h = tool.gameObject.AddComponent<PM_ToolHandle>();
        h.Setup();
        return h;
    }

    void Setup()
    {
        // sheets (pressing cloth, fusible) have no collider: a box around what is drawn
        if (GetComponentInChildren<Collider>(true) == null)
        {
            Bounds b = PM_Util.WorldBounds(transform);
            var bc = gameObject.AddComponent<BoxCollider>();
            Vector3 s = transform.lossyScale;
            bc.center = transform.InverseTransformPoint(b.center);
            bc.size = new Vector3(b.size.x / Mathf.Max(1e-4f, s.x), Mathf.Max(0.02f, b.size.y) / Mathf.Max(1e-4f, s.y), b.size.z / Mathf.Max(1e-4f, s.z));
        }
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        grab = gameObject.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;   // held where the hand took it
        grab.interactionLayers = -1;
        grab.selectExited.AddListener(a => layDown = true);
        grab.enabled = false;
    }

    public void SetOn(bool on, Transform boardPoint)
    {
        board = boardPoint;
        if (grab != null) grab.enabled = on;
    }

    // After the hand lets go (next frame, when the grab has put the parent back): upright on the board again.
    void LateUpdate()
    {
        if (!layDown || (grab != null && grab.isSelected)) return;
        layDown = false;
        Vector3 f = transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
        transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
        if (board == null) return;
        Vector3 c = board.position, p = transform.position;
        Vector3 d = p - c; d.y = 0f;
        if (d.magnitude > 0.35f) d = d.normalized * 0.35f;   // stays on the board
        transform.position = c + d;
    }
}
