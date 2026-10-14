using TMPro;
using UnityEngine;

// The exam score as a glass test tube next to the screen: it fills with glowing liquid as points come in.
// The pale part above the liquid = the points the current task can still bring. Marks at 55 / 75 / 90 (prize).
public class PM_ScoreTube : MonoBehaviour
{
    const float H = 0.46f, R = 0.042f;
    Transform liquid, ghost, surface;
    Material liquidMat, ghostMat;
    TextMeshProUGUI label;
    readonly Transform[] bubbles = new Transform[5];
    float shown, shownGhost, target, targetGhost, pulse;

    public static PM_ScoreTube Create(Transform panel, Vector3 local)
    {
        var go = new GameObject("PM_ScoreTube");
        go.transform.SetParent(panel, false);
        go.transform.localPosition = local;
        var t = go.AddComponent<PM_ScoreTube>();
        t.Build();
        return t;
    }

    void Build()
    {
        // glass
        var glass = Part(PrimitiveType.Cylinder, new Vector3(0, H * 0.5f, 0), new Vector3(R * 2f, H * 0.5f, R * 2f));
        glass.GetComponent<Renderer>().sharedMaterial = PM_Util.TransparentMaterial(new Color(0.75f, 0.95f, 1f, 0.16f));
        var bottom = Part(PrimitiveType.Sphere, Vector3.zero, Vector3.one * R * 2f);
        bottom.GetComponent<Renderer>().sharedMaterial = PM_Util.TransparentMaterial(new Color(0.75f, 0.95f, 1f, 0.16f));
        var rim = Part(PrimitiveType.Cylinder, new Vector3(0, H, 0), new Vector3(R * 2.5f, 0.006f, R * 2.5f));
        rim.GetComponent<Renderer>().sharedMaterial = PM_Util.NeonMaterial(PM_Util.Cyan, 1.2f);
        // liquid (a scaled cylinder from the bottom up) + the pale "still possible" part
        liquidMat = PM_Util.NeonMaterial(PM_Util.Cyan, 1.6f);
        ghostMat = PM_Util.TransparentMaterial(new Color(0.6f, 1f, 1f, 0.28f));
        var drop = Part(PrimitiveType.Sphere, Vector3.zero, Vector3.one * R * 1.7f);
        drop.GetComponent<Renderer>().sharedMaterial = liquidMat;
        liquid = Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(R * 1.7f, 0.001f, R * 1.7f)).transform;
        liquid.GetComponent<Renderer>().sharedMaterial = liquidMat;
        ghost = Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(R * 1.66f, 0.001f, R * 1.66f)).transform;
        ghost.GetComponent<Renderer>().sharedMaterial = ghostMat;
        surface = Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(R * 1.75f, 0.002f, R * 1.75f)).transform;
        surface.GetComponent<Renderer>().sharedMaterial = PM_Util.NeonMaterial(Color.white, 1.4f);
        // marks: 55, 75 and the prize line at 90
        float[] marks = { 0.55f, 0.75f, 0.9f };
        for (int i = 0; i < marks.Length; i++)
        {
            Color c = i == 2 ? PM_Util.Yellow : new Color(1f, 1f, 1f, 0.8f);
            var m = Part(PrimitiveType.Cube, new Vector3(R * 1.25f, marks[i] * H, -R * 0.2f), new Vector3(i == 2 ? 0.05f : 0.03f, 0.004f, 0.004f));
            m.GetComponent<Renderer>().sharedMaterial = PM_Util.NeonMaterial(c, i == 2 ? 2f : 1f);
        }
        var star = Part(PrimitiveType.Sphere, new Vector3(R * 1.25f + 0.035f, 0.9f * H, -R * 0.2f), Vector3.one * 0.016f);
        star.GetComponent<Renderer>().sharedMaterial = PM_Util.NeonMaterial(PM_Util.Yellow, 2.2f);
        for (int i = 0; i < bubbles.Length; i++)
        {
            bubbles[i] = Part(PrimitiveType.Sphere, Vector3.zero, Vector3.one * (0.006f + 0.003f * (i % 2))).transform;
            bubbles[i].GetComponent<Renderer>().sharedMaterial = PM_Util.NeonMaterial(Color.white, 1.2f);
        }
        // number above the tube
        var lab = new GameObject("Label", typeof(RectTransform));
        lab.transform.SetParent(transform, false);
        lab.transform.localPosition = new Vector3(0, H + 0.06f, 0);
        var canvas = lab.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = lab.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300, 90);
        rt.localScale = Vector3.one * 0.001f;
        label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(lab.transform, false);
        label.rectTransform.sizeDelta = new Vector2(300, 90);
        PM_Panel.Prepare(label);
        label.fontSize = 56;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        Set(0f, 0f);
    }

    GameObject Part(PrimitiveType type, Vector3 pos, Vector3 scale)
    {
        var g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(transform, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    // banked = points already earned (0..1 of the maximum); possible = what the current task can still add.
    public void Set(float banked, float possible)
    {
        if (banked > target + 0.001f) pulse = 1f;
        target = Mathf.Clamp01(banked);
        targetGhost = Mathf.Clamp01(banked + possible);
    }

    public void Show(bool on) { gameObject.SetActive(on); }

    void Update()
    {
        float dt = Time.deltaTime;
        shown = Mathf.MoveTowards(shown, target, dt * 0.35f);
        shownGhost = Mathf.MoveTowards(shownGhost, Mathf.Max(targetGhost, shown), dt * 0.6f);
        float h = shown * H, hg = Mathf.Max(shownGhost * H, h);
        liquid.localScale = new Vector3(liquid.localScale.x, Mathf.Max(0.0005f, h * 0.5f), liquid.localScale.z);
        liquid.localPosition = new Vector3(0, h * 0.5f, 0);
        ghost.localScale = new Vector3(ghost.localScale.x, Mathf.Max(0.0005f, (hg - h) * 0.5f), ghost.localScale.z);
        ghost.localPosition = new Vector3(0, (h + hg) * 0.5f, 0);
        surface.localPosition = new Vector3(0, h + 0.001f * Mathf.Sin(Time.time * 6f), 0);
        Color c = shown >= 0.9f ? PM_Util.Green : shown >= 0.75f ? PM_Util.Cyan : shown >= 0.55f ? PM_Util.Yellow : new Color(1f, 0.55f, 0.2f);
        pulse = Mathf.MoveTowards(pulse, 0f, dt * 1.5f);
        liquidMat.SetColor("_EmissionColor", c * (1.4f + pulse * 1.6f));
        liquidMat.SetColor("_BaseColor", c * 0.25f);
        for (int i = 0; i < bubbles.Length; i++)
        {
            float t = Mathf.Repeat(Time.time * (0.25f + 0.07f * i) + i * 0.37f, 1f);
            bool on = h > 0.03f;
            bubbles[i].gameObject.SetActive(on);
            if (!on) continue;
            float ang = i * 2.1f + Time.time * 0.5f;
            bubbles[i].localPosition = new Vector3(Mathf.Cos(ang) * R * 0.45f, t * h, Mathf.Sin(ang) * R * 0.45f);
        }
        label.text = Mathf.RoundToInt(shown * 100f).ToString();
        label.color = c;
    }
}
