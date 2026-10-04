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
        rt.sizeDelta = new Vector2(600, 60);
        rt.localScale = Vector3.one * 0.0007f;
        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(go.transform, false);
        t.rectTransform.sizeDelta = new Vector2(600, 60);
        t.font = PM_Panel.Font();
        t.fontSize = 40;
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
