using UnityEngine;

// Studio mirror: a camera behind the glass renders the room as seen in the mirror (the player's avatar included).
// The image is shown 1:1 on the glass (off-axis projection), so it lines up with the real reflection.
public class PM_Mirror : MonoBehaviour
{
    public float width = 1.0f, height = 1.9f;
    Camera cam;
    RenderTexture rt;
    Mesh quad;
    readonly Vector3[] corners = new Vector3[4];

    public static PM_Mirror Create(Vector3 bottomCenter, Vector3 facing)
    {
        var go = new GameObject("PM_Mirror");
        facing.y = 0;
        go.transform.position = bottomCenter;
        go.transform.rotation = Quaternion.LookRotation(-facing.normalized, Vector3.up);   // +Z goes into the glass
        return go.AddComponent<PM_Mirror>();
    }

    void Start()
    {
        rt = new RenderTexture(768, Mathf.RoundToInt(768 * height / width), 24);
        rt.name = "PM_MirrorRT";
        var cgo = new GameObject("MirrorCamera");
        cgo.transform.SetParent(transform, false);
        cam = cgo.AddComponent<Camera>();
        cam.targetTexture = rt;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.01f, 0.015f, 0.03f);
        cam.cullingMask = ~0;   // also the mirror-only layer (the player's head)

        // Glass
        var glass = new GameObject("Glass");
        glass.transform.SetParent(transform, false);
        quad = new Mesh();
        quad.vertices = new[] { new Vector3(-width / 2, 0, 0), new Vector3(width / 2, 0, 0), new Vector3(width / 2, height, 0), new Vector3(-width / 2, height, 0) };
        quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        quad.RecalculateBounds();
        glass.AddComponent<MeshFilter>().sharedMesh = quad;
        var mr = glass.AddComponent<MeshRenderer>();
        var m = PM_Util.TransparentMaterial(Color.white);
        m.mainTexture = rt;
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Neon frame + dark back
        Vector3[] f = { new Vector3(-width / 2 - 0.03f, -0.02f, -0.005f), new Vector3(width / 2 + 0.03f, -0.02f, -0.005f), new Vector3(width / 2 + 0.03f, height + 0.03f, -0.005f), new Vector3(-width / 2 - 0.03f, height + 0.03f, -0.005f) };
        PM_Util.Line(transform, "Frame", f, PM_Util.Cyan, 0.02f, true);
        var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(back.GetComponent<Collider>());
        back.transform.SetParent(transform, false);
        back.transform.localPosition = new Vector3(0, height / 2, 0.03f);
        back.transform.localScale = new Vector3(width + 0.08f, height + 0.08f, 0.04f);
        back.GetComponent<Renderer>().material = PM_Util.NeonMaterial(new Color(0.05f, 0.06f, 0.08f), 0.05f);
    }

    void LateUpdate()
    {
        Camera main = Camera.main;
        if (main == null || cam == null) return;
        Vector3 n = -transform.forward;                  // normal towards the room
        Vector3 p0 = transform.position;
        Vector3 eye = main.transform.position;
        float dist = Vector3.Dot(eye - p0, n);
        cam.enabled = dist > 0.05f;                      // nobody in front of the mirror: nothing to draw
        if (!cam.enabled) return;
        Vector3 reflected = eye - 2f * dist * n;
        cam.transform.position = reflected;
        cam.transform.rotation = Quaternion.LookRotation(n, Vector3.up);

        // Off-axis frustum exactly through the glass corners.
        corners[0] = transform.TransformPoint(new Vector3(-width / 2, 0, 0));
        corners[1] = transform.TransformPoint(new Vector3(width / 2, 0, 0));
        corners[2] = transform.TransformPoint(new Vector3(width / 2, height, 0));
        corners[3] = transform.TransformPoint(new Vector3(-width / 2, height, 0));
        float l = float.MaxValue, r = float.MinValue, b = float.MaxValue, t = float.MinValue;
        var uv = new Vector2[4];
        var local = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            local[i] = cam.transform.InverseTransformPoint(corners[i]);
            l = Mathf.Min(l, local[i].x); r = Mathf.Max(r, local[i].x);
            b = Mathf.Min(b, local[i].y); t = Mathf.Max(t, local[i].y);
        }
        for (int i = 0; i < 4; i++) uv[i] = new Vector2((local[i].x - l) / (r - l), (local[i].y - b) / (t - b));
        quad.uv = uv;
        float near = Mathf.Max(0.02f, dist);
        cam.projectionMatrix = Matrix4x4.Frustum(l, r, b, t, near, 60f);
    }

    void OnDestroy()
    {
        if (rt != null) rt.Release();
    }
}
