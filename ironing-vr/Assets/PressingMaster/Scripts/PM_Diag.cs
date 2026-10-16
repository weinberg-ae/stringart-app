using System.Collections.Generic;
using UnityEngine;

// Diagnostics only (changes nothing in the game): 5 and 15 seconds after Play it writes to the Console
// (yellow lines "[PM диагностика]") what is right next to the headset camera — every object the camera draws
// within 1.2 m (nearest first), live particles (steam / drops) within 3 m, and the iron's state.
// Used to find what covers the view. Can be deleted later.
public class PM_Diag : MonoBehaviour
{
    readonly float[] at = { 5f, 15f };
    int next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("PM_Diag");
        DontDestroyOnLoad(go);
        go.AddComponent<PM_Diag>();
    }

    void Update()
    {
        if (next < at.Length && Time.timeSinceLevelLoad > at[next]) { next++; Report(); }
    }

    static string Path(Transform t)
    {
        string s = t.name;
        for (int i = 0; i < 4 && t.parent != null; i++) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    void Report()
    {
        Camera cam = Camera.main;
        if (cam == null)
            foreach (Camera c in Camera.allCameras) if (c != null && c.targetTexture == null) { cam = c; break; }
        if (cam == null) { Debug.LogWarning("[PM диагностика] камера не найдена"); return; }
        Vector3 p = cam.transform.position;
        Debug.LogWarning("[PM диагностика] ===== камера: " + Path(cam.transform) + ", время " + Time.timeSinceLevelLoad.ToString("0") + " c =====");

        var near = new List<KeyValuePair<float, Renderer>>();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (((cam.cullingMask >> r.gameObject.layer) & 1) == 0) continue;   // this camera does not draw it
            float d = Mathf.Sqrt(r.bounds.SqrDistance(p));
            if (d < 1.2f) near.Add(new KeyValuePair<float, Renderer>(d, r));
        }
        near.Sort((a, b) => a.Key.CompareTo(b.Key));
        for (int i = 0; i < near.Count && i < 12; i++)
        {
            Renderer r = near[i].Value;
            Material m = r.sharedMaterial;
            string mat = m != null ? m.name : "-";
            if (m != null && m.HasProperty("_EmissionColor") && m.IsKeywordEnabled("_EMISSION"))
                mat += " (свечение " + m.GetColor("_EmissionColor").maxColorComponent.ToString("0.0") + ")";
            Debug.LogWarning("[PM диагностика] " + near[i].Key.ToString("0.00") + " м — " + Path(r.transform) + " | " + r.GetType().Name
                             + " | слой " + r.gameObject.layer + " | размер " + r.bounds.size.ToString("0.00") + " | " + mat);
        }
        foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (ps == null || ps.particleCount == 0) continue;
            float d = Vector3.Distance(ps.transform.position, p);
            if (d < 3f) Debug.LogWarning("[PM диагностика] частицы " + Path(ps.transform) + ": " + ps.particleCount + " шт., " + d.ToString("0.00") + " м, идёт=" + ps.isPlaying);
        }
        PM_Iron iron = PM_Iron.Instance;
        if (iron != null)
            Debug.LogWarning("[PM диагностика] утюг: " + Vector3.Distance(iron.transform.position, p).ToString("0.00") + " м от камеры, в руке=" + iron.Held
                             + ", пар=" + iron.Steaming + ", нагрев=" + iron.heat.ToString("0.00") + ", пар с клавиатуры=" + iron.simulateSteam);
    }
}
