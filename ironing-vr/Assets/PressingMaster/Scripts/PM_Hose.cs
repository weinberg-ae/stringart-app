using UnityEngine;

// Steam hose from the boom to the iron. The hose of the model is static, so it is hidden
// and replaced by this flexible one that always stays connected to the iron and sags naturally.
// Runs after the iron has moved this frame (otherwise the hose end lags behind the iron = looks disconnected).
[DefaultExecutionOrder(300)]
public class PM_Hose : MonoBehaviour
{
    Transform iron;
    Vector3 ironLocalEnd;   // where the hose enters the iron (iron local space)
    Vector3 anchor;         // top end at the boom (table local space)
    float length;
    LineRenderer core, shine;
    const int Segments = 28;

    Vector3 ironLocalOut;   // direction the hose leaves the iron (along the spring)

    public static PM_Hose Create(Transform table, Vector3 anchorWorld, Transform iron, Vector3 ironEndWorld, Vector3 outWorld)
    {
        var go = new GameObject("PM_Hose");
        go.transform.SetParent(table, false);
        var h = go.AddComponent<PM_Hose>();
        h.iron = iron;
        h.ironLocalEnd = iron.InverseTransformPoint(ironEndWorld - outWorld.normalized * 0.03f);   // well inside the top of the spring (no gap)
        h.ironLocalOut = iron.InverseTransformDirection(outWorld.normalized);
        h.anchor = table.InverseTransformPoint(anchorWorld);
        h.length = Vector3.Distance(anchorWorld, ironEndWorld) * 1.25f + 0.35f;
        var pts = new Vector3[Segments];
        h.core = PM_Util.Line(go.transform, "Hose", pts, new Color(0.32f, 0.34f, 0.4f), 0.022f, false);
        h.shine = PM_Util.Line(go.transform, "HoseShine", pts, new Color(0.1f, 0.95f, 1f, 0.7f), 0.005f, false);   // neon line along the hose
        h.core.useWorldSpace = true;
        h.shine.useWorldSpace = true;
        h.core.numCapVertices = 4;
        h.Shape();
        return h;
    }

    void LateUpdate() { Shape(); }
    void OnEnable() { Application.onBeforeRender += Shape; }
    void OnDisable() { Application.onBeforeRender -= Shape; }

    void Shape()
    {
        if (iron == null || core == null) return;
        Vector3 a = transform.parent != null ? transform.parent.TransformPoint(anchor) : anchor;
        Vector3 b = iron.TransformPoint(ironLocalEnd);
        float d = Vector3.Distance(a, b);
        // Slack hose hangs down; a stretched hose is straight.
        float sag = d < length ? Mathf.Min(0.45f, Mathf.Sqrt(length * length - d * d) * 0.35f) : 0f;
        // Cubic curve: it leaves the iron straight along the spring, then hangs down towards the boom.
        Vector3 outDir = iron.TransformDirection(ironLocalOut);
        Vector3 p1 = a + (b - a) * 0.25f + Vector3.down * sag * 1.4f;
        Vector3 p2 = b + outDir * Mathf.Clamp(d * 0.3f, 0.05f, 0.16f);
        for (int i = 0; i < Segments; i++)
        {
            float t = i / (float)(Segments - 1), u = 1f - t;
            Vector3 p = u * u * u * a + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * b;
            core.SetPosition(i, p);
            shine.SetPosition(i, p + Vector3.up * 0.005f);
        }
    }
}
