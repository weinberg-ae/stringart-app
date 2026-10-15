using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// The collar in the collar exercise can be taken with the free hand (Grip) and moved / turned.
// When it is let go, it lies down flat on the collar board again (keeping its new direction).
public class PM_CollarHandle : MonoBehaviour
{
    Transform board;
    float restY;
    XRGrabInteractable grab;

    public static void Attach(PM_Fabric f, Transform collarBoard)
    {
        if (f == null) return;
        var h = f.gameObject.AddComponent<PM_CollarHandle>();
        h.board = collarBoard;
        h.restY = f.transform.position.y;
        var rb = f.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        h.grab = f.gameObject.AddComponent<XRGrabInteractable>();
        h.grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        h.grab.throwOnDetach = false;
        h.grab.useDynamicAttach = true;   // held where the hand took it (not by its centre)
        h.grab.interactionLayers = -1;
        h.grab.selectExited.AddListener(a => h.LayDown());
    }

    // Flat on the board again: only the turn around the vertical axis is kept, and it stays near the board.
    void LayDown()
    {
        Vector3 f = transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
        transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
        Vector3 p = transform.position;
        if (board != null)
        {
            Vector3 c = board.position;
            Vector3 d = p - c; d.y = 0f;
            if (d.magnitude > 0.16f) d = d.normalized * 0.16f;
            p = c + d;
        }
        p.y = restY;
        transform.position = p;
    }
}
