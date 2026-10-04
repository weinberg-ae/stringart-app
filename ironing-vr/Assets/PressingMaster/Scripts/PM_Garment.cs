using TMPro;
using UnityEngine;

// Shows a 3D garment model from Resources/PM_Garments (<key>.fbx + <key>_albedo texture)
// as a slowly rotating exhibit on a neon pedestal. Size and orientation are normalized automatically.
public class PM_Garment : MonoBehaviour
{
    public float spinSpeed = 20f;
    Transform model;

    public static PM_Garment Show(string key, Vector3 basePos, float height, Vector3 viewer, string label, float smoothness, bool pedestal)
    {
        if (key == "linen") return ShowImages(key, basePos, height, viewer, label);
        GameObject prefab = Resources.Load<GameObject>("PM_Garments/" + key);
        if (prefab == null)
        {
            Debug.LogWarning("[PM] Модель не найдена: Assets/Resources/PM_Garments/" + key);
            return null;
        }
        var root = new GameObject("PM_Garment_" + key);
        root.transform.position = basePos;
        var g = root.AddComponent<PM_Garment>();

        // Pivot that spins; the model inside is centered on it.
        var spin = new GameObject("Spin").transform;
        spin.SetParent(root.transform, false);
        GameObject inst = Instantiate(prefab, spin);
        g.model = spin;
        foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) Destroy(c);

        // Material with the model's texture.
        Texture2D albedo = Resources.Load<Texture2D>("PM_Garments/" + key + "_albedo");
        var mat = new Material(PM_Util.LitShader());
        if (albedo != null) { mat.SetTexture("_BaseMap", albedo); mat.SetTexture("_MainTex", albedo); }
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", smoothness);
        foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }

        // Stand the model up: its longest side becomes vertical.
        Bounds b = PM_Util.WorldBounds(inst.transform);
        if (b.size.z > b.size.y && b.size.z >= b.size.x) inst.transform.Rotate(-90f, 0f, 0f, Space.World);
        else if (b.size.x > b.size.y && b.size.x > b.size.z) inst.transform.Rotate(0f, 0f, 90f, Space.World);

        // Scale to the wanted height and put its bottom on the base point.
        b = PM_Util.WorldBounds(inst.transform);
        if (b.size.y > 0.0001f) inst.transform.localScale *= height / b.size.y;
        b = PM_Util.WorldBounds(inst.transform);
        inst.transform.position += new Vector3(basePos.x - b.center.x, basePos.y - b.min.y, basePos.z - b.center.z);

        if (pedestal)
        {
            PM_Util.Line(root.transform, "Ring", PM_Util.Circle(0.28f, 64, Vector3.right, Vector3.forward, Vector3.zero), PM_Util.Cyan, 0.008f, true);
            PM_Util.Line(root.transform, "Ring2", PM_Util.Circle(0.33f, 64, Vector3.right, Vector3.forward, Vector3.zero), new Color(0.1f, 0.95f, 1f, 0.35f), 0.004f, true);
        }
        if (!string.IsNullOrEmpty(label)) g.AddLabel(label, viewer);
        return g;
    }

    // Linen: a framed picture of the flax plant and its flower instead of a 3D model.
    static PM_Garment ShowImages(string key, Vector3 basePos, float height, Vector3 viewer, string label)
    {
        Texture2D plant = Resources.Load<Texture2D>("PM_Garments/" + key + "_plant");
        Texture2D flower = Resources.Load<Texture2D>("PM_Garments/" + key + "_flower");
        var root = new GameObject("PM_Garment_" + key);
        root.transform.position = basePos;
        Vector3 d = basePos - viewer;
        d.y = 0;
        if (d.sqrMagnitude > 0.0001f) root.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        var g = root.AddComponent<PM_Garment>();
        g.spinSpeed = 0f;
        float h = height;
        Picture(root.transform, plant, new Vector3(0.2f, h * 0.5f, 0), h);
        Picture(root.transform, flower, new Vector3(-0.2f, h * 0.55f, 0), h * 0.5f);
        Vector3[] frame =
        {
            new Vector3(-0.47f, -0.02f, 0.01f), new Vector3(0.47f, -0.02f, 0.01f),
            new Vector3(0.47f, h + 0.02f, 0.01f), new Vector3(-0.47f, h + 0.02f, 0.01f)
        };
        PM_Util.Line(root.transform, "Frame", frame, PM_Util.Cyan, 0.006f, true);
        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(bg.GetComponent<Collider>());
        bg.transform.SetParent(root.transform, false);
        bg.transform.localPosition = new Vector3(0, h * 0.5f, 0.02f);
        bg.transform.localScale = new Vector3(0.94f, h + 0.04f, 1f);
        bg.GetComponent<Renderer>().material = PM_Util.TransparentMaterial(new Color(0.01f, 0.03f, 0.07f, 0.85f));
        if (!string.IsNullOrEmpty(label)) g.AddLabel(label, viewer);
        return g;
    }

    static void Picture(Transform parent, Texture2D tex, Vector3 pos, float size)
    {
        if (tex == null) return;
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localScale = new Vector3(size * tex.width / (float)tex.height, size, 1f);
        var m = PM_Util.TransparentMaterial(Color.white);
        m.mainTexture = tex;
        q.GetComponent<Renderer>().material = m;
    }

    void AddLabel(string text, Vector3 viewer)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0, -0.07f, 0);
        Vector3 d = go.transform.position - viewer;
        d.y = 0;
        if (d.sqrMagnitude > 0.0001f) go.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1100, 60);
        rt.localScale = Vector3.one * 0.0007f;
        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(go.transform, false);
        t.rectTransform.sizeDelta = new Vector2(1100, 60);
        t.font = PM_Panel.Font();
        t.fontSize = 36;
        t.alignment = TextAlignmentOptions.Center;
        t.color = PM_Util.Cyan;
        t.raycastTarget = false;
        t.text = PM_Hebrew.VisualLine(text);
    }

    void Update()
    {
        if (model != null && spinSpeed != 0f) model.RotateAround(transform.position, Vector3.up, spinSpeed * Time.deltaTime);
    }
}
