using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 1) Guard: every camera that draws to the screen / headset gets URP "Stop NaN" (checked twice a second).
//    A broken pixel (NaN / infinity from some mesh or material) otherwise turns, through the glow (bloom),
//    into a huge white disc with black specks in the middle.
// 2) Diagnostics (yellow lines "[PM диагностика]" in the Console, 5 and 15 s after Play): which object has
//    broken numbers (NaN / infinity) in its position, mesh, line or material, plus what is next to the camera.
public class PM_Diag : MonoBehaviour
{
    readonly float[] at = { 5f, 15f };
    int next;
    float nextGuard;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("PM_Diag");
        DontDestroyOnLoad(go);
        go.AddComponent<PM_Diag>();
    }

    void Update()
    {
        if (Time.unscaledTime > nextGuard)
        {
            nextGuard = Time.unscaledTime + 0.5f;
            foreach (Camera c in Camera.allCameras)
            {
                if (c == null || c.targetTexture != null) continue;
                var d = c.GetComponent<UniversalAdditionalCameraData>();
                if (d != null && !d.stopNaN) d.stopNaN = true;
            }
        }
        if (next < at.Length && Time.timeSinceLevelLoad > at[next]) { next++; Report(); }
    }

    static string Path(Transform t)
    {
        string s = t.name;
        for (int i = 0; i < 4 && t.parent != null; i++) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    static bool Bad(float f) { return float.IsNaN(f) || float.IsInfinity(f); }
    static bool Bad(Vector3 v) { return Bad(v.x) || Bad(v.y) || Bad(v.z); }
    static bool Bad(Color c) { return Bad(c.r) || Bad(c.g) || Bad(c.b) || Bad(c.a); }

    static bool BadArray(Vector3[] a)
    {
        if (a == null) return false;
        for (int i = 0; i < a.Length; i++) if (Bad(a[i])) return true;
        return false;
    }

    void Report()
    {
        Camera cam = Camera.main;
        if (cam == null)
            foreach (Camera c in Camera.allCameras) if (c != null && c.targetTexture == null) { cam = c; break; }
        Debug.LogWarning("[PM диагностика] ===== проверка битых чисел, время " + Time.timeSinceLevelLoad.ToString("0") + " c =====");
        int found = 0;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            string why = null;
            Transform t = r.transform;
            if (Bad(t.position) || Bad(t.lossyScale)) why = "позиция/масштаб";
            Mesh m = null;
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null) m = mf.sharedMesh;
            var sk = r as SkinnedMeshRenderer;
            if (sk != null) m = sk.sharedMesh;
            if (why == null && m != null && m.isReadable)
            {
                if (BadArray(m.vertices)) why = "точки сетки";
                else if (BadArray(m.normals)) why = "нормали сетки";
            }
            var lr = r as LineRenderer;
            if (why == null && lr != null)
                for (int i = 0; i < lr.positionCount; i++) if (Bad(lr.GetPosition(i))) { why = "точки линии"; break; }
            if (why == null)
                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    if ((mat.HasProperty("_BaseColor") && Bad(mat.GetColor("_BaseColor"))) || (mat.HasProperty("_EmissionColor") && Bad(mat.GetColor("_EmissionColor"))))
                    { why = "цвет материала " + mat.name; break; }
                    if (mat.HasProperty("_EmissionColor") && mat.IsKeywordEnabled("_EMISSION") && mat.GetColor("_EmissionColor").maxColorComponent > 20f)
                    { why = "слишком сильное свечение " + mat.GetColor("_EmissionColor").maxColorComponent.ToString("0") + " в " + mat.name; break; }
                }
            if (why == null) continue;
            found++;
            if (found <= 15)
                Debug.LogWarning("[PM диагностика] БИТЫЙ: " + Path(r.transform) + " | " + r.GetType().Name + " | " + why
                                 + (cam != null ? " | " + Vector3.Distance(r.bounds.center, cam.transform.position).ToString("0.00") + " м от камеры" : ""));
        }
        Debug.LogWarning("[PM диагностика] битых объектов: " + found);
        PM_Iron iron = PM_Iron.Instance;
        if (iron != null && cam != null)
            Debug.LogWarning("[PM диагностика] утюг: " + Vector3.Distance(iron.transform.position, cam.transform.position).ToString("0.00") + " м от камеры, в руке=" + iron.Held
                             + ", пар=" + iron.Steaming + ", нагрев=" + iron.heat.ToString("0.00"));
    }
}
