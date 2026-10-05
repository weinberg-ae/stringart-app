using UnityEngine;

// Small calm "narrator" orb next to the screen: breathes softly while the voice speaks.
public class PM_Narrator : MonoBehaviour
{
    Transform core;
    Material mat;
    LineRenderer[] rings = new LineRenderer[3];
    float level;   // smoothed voice level, so the orb does not flicker
    public Color color = new Color(0.1f, 0.95f, 1f);

    public static PM_Narrator Create(Transform parent, Vector3 localPos)
    {
        var go = new GameObject("PM_Narrator");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.AddComponent<PM_Narrator>();
    }

    void Start()
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(s.GetComponent<Collider>());
        s.transform.SetParent(transform, false);
        s.transform.localScale = Vector3.one * 0.03f;
        mat = PM_Util.NeonMaterial(color, 0.5f);
        s.GetComponent<Renderer>().material = mat;
        core = s.transform;
        for (int i = 0; i < rings.Length; i++)
        {
            rings[i] = PM_Util.Line(transform, "Ring" + i, PM_Util.Circle(0.035f + i * 0.014f, 48, Vector3.right, Vector3.up, Vector3.zero), color, 0.002f, true);
            rings[i].transform.localRotation = Quaternion.Euler(i * 60f, i * 45f, 0);
        }
    }

    void Update()
    {
        float target = PM_Audio.I != null ? PM_Audio.I.VoiceLevel : 0f;
        level = Mathf.Lerp(level, target, Time.deltaTime * 3f);
        core.localScale = Vector3.one * (0.03f + level * 0.008f);
        mat.SetColor("_EmissionColor", color * (0.45f + level * 0.5f));
        for (int i = 0; i < rings.Length; i++)
        {
            rings[i].transform.Rotate((i + 1) * 8f * Time.deltaTime, (i + 2) * 6f * Time.deltaTime, 0, Space.Self);
            Color c = color; c.a = 0.18f + level * 0.2f;
            rings[i].startColor = c; rings[i].endColor = c;
        }
    }
}
