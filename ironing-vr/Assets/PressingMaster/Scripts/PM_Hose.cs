using UnityEngine;

// Steam hose from the boom to the iron. The hose of the model is static, so it is hidden
// and replaced by this flexible one that always stays connected to the iron and sags naturally.
public class PM_Hose : MonoBehaviour
{
    Transform iron;
    Vector3 ironLocalEnd;   // where the hose enters the iron (iron local space)
    Vector3 anchor;         // top end at the boom (table local space)
    float length;
    LineRenderer core, shine;
    const int Segments = 28;

    public static PM_Hose Create(Transform table, Vector3 anchorWorld, Transform iron, Vector3 ironEndWorld)
    {
        var go = new GameObject("PM_Hose");
        go.transform.SetParent(table, false);
        var h = go.AddComponent<PM_Hose>();
        h.iron = iron;
        h.ironLocalEnd = iron.InverseTransformPoint(ironEndWorld);
        h.anchor = table.InverseTransformPoint(anchorWorld);
        h.length = Vector3.Distance(anchorWorld, ironEndWorld) * 1.25f + 0.35f;
        var pts = new Vector3[Segments];
        h.core = PM_Util.Line(go.transform, "Hose", pts, new Color(0.11f, 0.12f, 0.14f), 0.017f, false);
        h.shine = PM_Util.Line(go.transform, "HoseShine", pts, new Color(0.45f, 0.5f, 0.58f, 0.55f), 0.005f, false);
        h.core.useWorldSpace = true;
        h.shine.useWorldSpace = true;
        h.core.numCapVertices = 4;
        h.LateUpdate();
        return h;
    }

    void LateUpdate()
    {
        if (iron == null) return;
        Vector3 a = transform.parent != null ? transform.parent.TransformPoint(anchor) : anchor;
        Vector3 b = iron.TransformPoint(ironLocalEnd);
        float d = Vector3.Distance(a, b);
        // Slack hose hangs down; a stretched hose is straight.
        float sag = d < length ? Mathf.Min(0.45f, Mathf.Sqrt(length * length - d * d) * 0.35f) : 0f;
        Vector3 ctrl = (a + b) * 0.5f + Vector3.down * sag * 2f;
        for (int i = 0; i < Segments; i++)
        {
            float t = i / (float)(Segments - 1);
            Vector3 p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * ctrl + t * t * b;
            core.SetPosition(i, p);
            shine.SetPosition(i, p + Vector3.up * 0.005f);
        }
    }
}
