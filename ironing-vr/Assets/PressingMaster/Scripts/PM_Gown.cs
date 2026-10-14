using System.Collections.Generic;
using UnityEngine;

// Champagne satin gown worn by the dress form (mesh from tools/avatar/gown2.py, made in the form's own space,
// so it fits the form exactly). It is creased; steam from the iron (1–8 cm away) smooths the creases where
// it reaches. The gown stays on the form for the rest of the game.
public class PM_Gown : MonoBehaviour
{
    const float CreaseDepth = 0.011f;

    Mesh mesh;
    MeshCollider col;
    Vector3[] basePos, baseNrm, pos;
    float[] weight, crumple, amount;
    int[] front;                       // the vertices that count for the progress (the side facing the player)
    readonly Dictionary<int, List<int>> cells = new Dictionary<int, List<int>>();
    const float Cell = 0.05f;
    bool dirty;
    float lastBuild;
    float finishT = -1f;

    public float Progress { get; private set; }
    public bool Done { get { return Progress >= 0.92f; } }
    public Collider Collider { get { return col; } }
    public static PM_Gown Instance { get; private set; }

    public static PM_Gown Create(Transform formRoot)
    {
        TextAsset ta = Resources.Load<TextAsset>("PM_Garments/gown");
        if (ta == null) { Debug.LogWarning("[PM] Нет файла платья: Assets/Resources/PM_Garments/gown.bytes"); return null; }
        var go = new GameObject("PM_Gown");
        go.transform.SetParent(formRoot, false);
        var g = go.AddComponent<PM_Gown>();
        if (!g.Load(ta.bytes)) { Destroy(go); return null; }
        Instance = g;
        return g;
    }

    bool Load(byte[] b)
    {
        var r = new System.IO.BinaryReader(new System.IO.MemoryStream(b));
        if (r.ReadInt32() != 0x31474D50) return false;
        int vc = r.ReadInt32(), subs = r.ReadInt32(); r.ReadInt32();
        basePos = new Vector3[vc]; baseNrm = new Vector3[vc]; weight = new float[vc]; crumple = new float[vc]; amount = new float[vc];
        for (int i = 0; i < vc; i++) basePos[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        for (int i = 0; i < vc; i++) baseNrm[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        for (int i = 0; i < vc; i++) weight[i] = r.ReadSingle();
        for (int i = 0; i < vc; i++) crumple[i] = r.ReadSingle();
        mesh = new Mesh { name = "PM_Gown" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.MarkDynamic();
        pos = (Vector3[])basePos.Clone();
        mesh.vertices = pos;
        mesh.normals = baseNrm;
        mesh.subMeshCount = subs;
        var all = new List<int>();
        for (int s = 0; s < subs; s++)
        {
            int n = r.ReadInt32();
            var t = new int[n];
            for (int i = 0; i < n; i++) t[i] = r.ReadInt32();
            mesh.SetTriangles(t, s);
            if (s == 0) all.AddRange(t);
        }
        mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterials = Materials();
        // Collider = the smooth outer satin (the steam rays hit it).
        var cm = new Mesh { name = "PM_GownCollider" };
        cm.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        cm.vertices = basePos;
        cm.triangles = all.ToArray();
        col = gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = cm;
        // Spatial cells for the steam, and the front half for the progress.
        var fr = new List<int>();
        for (int i = 0; i < vc; i++)
        {
            if (weight[i] <= 0f) continue;
            int key = Key(basePos[i]);
            List<int> l;
            if (!cells.TryGetValue(key, out l)) { l = new List<int>(); cells[key] = l; }
            l.Add(i);
            if (baseNrm[i].z > -0.15f && basePos[i].z > 0.03f) fr.Add(i);
        }
        front = fr.ToArray();
        return true;
    }

    static int Key(Vector3 p)
    {
        int x = Mathf.FloorToInt(p.x / Cell) + 64, y = Mathf.FloorToInt(p.y / Cell) + 64, z = Mathf.FloorToInt(p.z / Cell) + 64;
        return (x * 256 + y) * 256 + z;
    }

    static Material[] mats;
    static Material[] Materials()
    {
        if (mats != null) return mats;
        Color champagne = new Color(0.93f, 0.84f, 0.74f);
        var satin = new Material(PM_Util.LitShader());
        satin.SetColor("_BaseColor", champagne); satin.SetColor("_Color", champagne);
        satin.SetFloat("_Smoothness", 0.86f); satin.SetFloat("_Metallic", 0.08f);
        var lining = new Material(satin);
        lining.SetFloat("_Smoothness", 0.7f);
        var jewel = new Material(PM_Util.LitShader());
        Color silver = new Color(0.82f, 0.85f, 0.9f);
        jewel.SetColor("_BaseColor", silver); jewel.SetColor("_Color", silver);
        jewel.SetFloat("_Metallic", 0.9f); jewel.SetFloat("_Smoothness", 0.95f);
        mats = new[] { satin, lining, jewel };
        return mats;
    }

    // All creases back (a fresh, crumpled gown) — 1 = fully creased.
    public void Wrinkle(float a)
    {
        finishT = -1f;
        for (int i = 0; i < amount.Length; i++) amount[i] = a;
        Progress = 1f - a;
        Rebuild();
    }

    // Steam reaching the gown at a world point: smooths the creases around it. Returns true if it hit cloth.
    public bool Steam(Vector3 worldPoint, float radius, float dt)
    {
        Vector3 p = transform.InverseTransformPoint(worldPoint);
        float r = radius / Mathf.Max(0.001f, transform.lossyScale.x);
        int x0 = Mathf.FloorToInt((p.x - r) / Cell), x1 = Mathf.FloorToInt((p.x + r) / Cell);
        int y0 = Mathf.FloorToInt((p.y - r) / Cell), y1 = Mathf.FloorToInt((p.y + r) / Cell);
        int z0 = Mathf.FloorToInt((p.z - r) / Cell), z1 = Mathf.FloorToInt((p.z + r) / Cell);
        bool any = false;
        float r2 = r * r;
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                {
                    List<int> l;
                    if (!cells.TryGetValue(((x + 64) * 256 + (y + 64)) * 256 + (z + 64), out l)) continue;
                    foreach (int i in l)
                    {
                        float d2 = (basePos[i] - p).sqrMagnitude;
                        if (d2 > r2) continue;
                        float k = 1f - d2 / r2;
                        amount[i] = Mathf.Max(0f, amount[i] - dt * 1.6f * k);
                        any = true;
                    }
                }
        if (any) { dirty = true; UpdateProgress(); }
        return any;
    }

    void UpdateProgress()
    {
        float s = 0f;
        foreach (int i in front) s += amount[i];
        Progress = front.Length > 0 ? 1f - s / front.Length : 1f;
    }

    // After the task: the rest of the gown relaxes smoothly too (it stays perfect on the form).
    public void FinishSmooth() { finishT = 0f; }

    void LateUpdate()
    {
        if (finishT >= 0f)
        {
            finishT += Time.deltaTime;
            float k = Time.deltaTime / 1.5f;
            for (int i = 0; i < amount.Length; i++) amount[i] = Mathf.Max(0f, amount[i] - k);
            dirty = true;
            if (finishT > 1.6f) { finishT = -1f; Progress = 1f; }
        }
        if (dirty && Time.time - lastBuild > 0.06f) Rebuild();
    }

    void Rebuild()
    {
        dirty = false;
        lastBuild = Time.time;
        for (int i = 0; i < pos.Length; i++)
        {
            float a = weight[i] * amount[i];
            pos[i] = a > 0f ? basePos[i] + baseNrm[i] * (crumple[i] * a * CreaseDepth) : basePos[i];
        }
        mesh.vertices = pos;
        mesh.RecalculateNormals();
    }
}
