using UnityEngine;

// Glowing "narrator" orb next to the screen: pulses with the voice.
public class PM_Narrator : MonoBehaviour
{
    Transform core;
    Material mat;
    LineRenderer[] rings = new LineRenderer[3];
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
        s.transform.localScale = Vector3.one * 0.05f;
        mat = PM_Util.NeonMaterial(color, 2f);
        s.GetComponent<Renderer>().material = mat;
        core = s.transform;
        for (int i = 0; i < rings.Length; i++)
        {
            rings[i] = PM_Util.Line(transform, "Ring" + i, PM_Util.Circle(0.045f + i * 0.02f, 48, Vector3.right, Vector3.up, Vector3.zero), color, 0.003f, true);
            rings[i].transform.localRotation = Quaternion.Euler(i * 60f, i * 45f, 0);
        }
    }

    void Update()
    {
        float lvl = PM_Audio.I != null ? PM_Audio.I.VoiceLevel : 0f;
        float idle = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.5f);
        core.localScale = Vector3.one * (0.045f + lvl * 0.05f + idle * 0.005f);
        mat.SetColor("_EmissionColor", color * (1.2f + lvl * 5f));
        for (int i = 0; i < rings.Length; i++)
        {
            rings[i].transform.Rotate((i + 1) * 20f * Time.deltaTime, (i + 2) * 15f * Time.deltaTime, 0, Space.Self);
            Color c = color; c.a = 0.35f + lvl * 0.6f;
            rings[i].startColor = c; rings[i].endColor = c;
            rings[i].transform.localScale = Vector3.one * (1f + lvl * 0.6f);
        }
    }
}
