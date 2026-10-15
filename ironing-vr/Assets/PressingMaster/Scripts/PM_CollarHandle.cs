using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// The collar in the collar exercise: soft cloth that lies on the collar board and hangs down where the board
// ends. The free hand (Grip) can take it, move and turn it; let go, it lies down again on the board (keeping
// its new direction) and drapes over the edges again. Both sides of the cloth are visible.
public class PM_CollarHandle : MonoBehaviour
{
    Transform board;
    float restY;
    XRGrabInteractable grab;
    Mesh mesh;
    MeshCollider col;
    Vector3[] flat;

    public static void Attach(PM_Fabric f, Transform collarBoard)
    {
        if (f == null) return;
        var h = f.gameObject.AddComponent<PM_CollarHandle>();
        h.board = collarBoard;
        h.restY = f.transform.position.y;
        var mf = f.GetComponent<MeshFilter>();
        h.mesh = mf != null ? mf.sharedMesh : null;
        h.col = f.GetComponent<MeshCollider>();
        if (h.mesh != null) { h.mesh.MarkDynamic(); h.flat = h.mesh.vertices; }
        var r = f.GetComponent<Renderer>();
        if (r != null) { r.material.SetFloat("_Cull", 0f); r.material.doubleSidedGI = true; }   // cloth seen from both sides
        var rb = f.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        h.grab = f.gameObject.AddComponent<XRGrabInteractable>();
        h.grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        h.grab.throwOnDetach = false;
        h.grab.useDynamicAttach = true;   // held where the hand took it (not by its centre)
        h.grab.interactionLayers = -1;
        h.grab.selectExited.AddListener(a => h.LayDown());
        h.Drape();
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
            float lim = 0.16f * board.lossyScale.x;
            if (d.magnitude > lim) d = d.normalized * lim;
            p = c + d;
        }
        p.y = restY;
        transform.position = p;
        Drape();
    }

    // Distance (board units) outside the padded top of the collar board (two ellipses, as in tools3d.py).
    static float Outside(Vector3 l)
    {
        float d1 = (Mathf.Sqrt((l.x + 0.06f) * (l.x + 0.06f) / (0.13f * 0.13f) + l.z * l.z / (0.075f * 0.075f)) - 1f) * 0.075f;
        float d2 = (Mathf.Sqrt((l.x - 0.10f) * (l.x - 0.10f) / (0.09f * 0.09f) + l.z * l.z / (0.06f * 0.06f)) - 1f) * 0.06f;
        return Mathf.Min(d1, d2);
    }

    // Where nothing holds it, the cloth bends down over the edge of the board.
    void Drape()
    {
        if (mesh == null || flat == null || board == null) return;
        var v = new Vector3[flat.Length];
        float s = board.lossyScale.y;
        for (int i = 0; i < flat.Length; i++)
        {
            Vector3 w = transform.TransformPoint(flat[i]);
            float d = Outside(board.InverseTransformPoint(w));
            if (d > 0f) w.y -= Mathf.Min(0.08f, 1.1f * d + 4f * d * d) * s;
            v[i] = transform.InverseTransformPoint(w);
        }
        mesh.vertices = v;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (col != null) { col.sharedMesh = null; col.sharedMesh = mesh; }
    }
}
