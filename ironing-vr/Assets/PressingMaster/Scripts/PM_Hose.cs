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

    public static PM_Hose Create(Transform table, Vector3 anchorWorld, Transform iron, Vector3 ironEndWorld)
    {
        var go = new GameObject("PM_Hose");
        go.transform.SetParent(table, false);
        var h = go.AddComponent<PM_Hose>();
        h.iron = iron;
        // the end goes a little into the iron's body, so the joint never shows a gap
        Bounds ib = PM_Util.MeshBounds(iron);
        Vector3 inside = Vector3.Lerp(ironEndWorld, ib.center, 0.12f);
        // A rubber sleeve on the iron where the hose goes in (it moves with the iron): the hose starts at its far end.
        Vector3 outDir = (anchorWorld - inside); outDir.y = Mathf.Max(outDir.y, outDir.magnitude * 0.6f); outDir.Normalize();
        var sleeve = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        sleeve.name = "PM_HoseSleeve";
        Object.Destroy(sleeve.GetComponent<Collider>());
        sleeve.transform.SetParent(iron, true);
        sleeve.transform.position = inside + outDir * 0.022f;
        sleeve.transform.rotation = Quaternion.FromToRotation(Vector3.up, outDir);
        sleeve.transform.localScale = Vector3.one;
        Vector3 ls = sleeve.transform.lossyScale;
        sleeve.transform.localScale = new Vector3(0.03f / ls.x, 0.025f / ls.y, 0.03f / ls.z);
        var sm = new Material(PM_Util.LitShader());
        Color rubber = new Color(0.16f, 0.17f, 0.2f);
        sm.SetColor("_BaseColor", rubber); sm.SetColor("_Color", rubber); sm.SetFloat("_Smoothness", 0.3f);
        sleeve.GetComponent<Renderer>().sharedMaterial = sm;
        h.ironLocalEnd = iron.InverseTransformPoint(inside + outDir * 0.045f);
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
