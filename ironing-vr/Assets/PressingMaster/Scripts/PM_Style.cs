using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

// The look of a loaded scene avatar (Genies): outfit colors, skin tone, hair color and a tattoo on the right forearm.
// It changes the avatar's own materials (MegaSkin _SkinColor, MegaHair _Color*, URP Lit clothes _BaseColor);
// the tattoo is a thin skinned copy of the forearm skin with our picture (Resources/PM_Avatar/gtattoo_*.png).
// Choices are remembered between sessions and applied again whenever the loader rebuilds the avatar.
public static class PM_Style
{
    public static int Look { get; private set; }
    public static int Skin { get; private set; }
    public static int Hair { get; private set; }
    public static int Tattoo { get; private set; }

    // Outfits: top, bottom, shoes (index 0 = the avatar's own colors)
    static readonly Color[][] Looks =
    {
        null,
        new[] { new Color(0.95f, 0.45f, 0.40f), new Color(0.20f, 0.30f, 0.52f), new Color(0.96f, 0.96f, 0.96f) },   // jeans
        new[] { new Color(0.07f, 0.07f, 0.08f), new Color(0.10f, 0.10f, 0.11f), new Color(0.55f, 0.04f, 0.10f) },   // evening black
        new[] { new Color(0.08f, 0.62f, 0.68f), new Color(0.17f, 0.17f, 0.20f), new Color(0.96f, 0.96f, 0.96f) },   // studio (station colors)
        new[] { new Color(0.97f, 0.55f, 0.72f), new Color(0.68f, 0.58f, 0.92f), new Color(0.98f, 0.98f, 0.98f) },   // pink
    };
    // Skin tones (index 2 = the avatar's own tone)
    static readonly Color[] Skins =
    {
        new Color(0.93f, 0.77f, 0.66f), new Color(0.82f, 0.63f, 0.50f), new Color(0.624f, 0.467f, 0.369f),
        new Color(0.45f, 0.31f, 0.22f), new Color(0.27f, 0.17f, 0.12f),
    };
    // Hair (index 0 = the avatar's own color)
    static readonly Color[] Hairs =
    {
        Color.clear, new Color(0.04f, 0.035f, 0.035f), new Color(0.78f, 0.62f, 0.30f), new Color(0.58f, 0.13f, 0.04f), new Color(0.88f, 0.30f, 0.58f),
    };
    static readonly string[] TattooFiles = { null, "gtattoo_needle", "gtattoo_rose", "gtattoo_geo", "gtattoo_iron" };
    public const int Count = 5;

    static readonly Dictionary<Material, Color> original = new Dictionary<Material, Color>();
    static readonly Dictionary<Material, Color[]> originalHair = new Dictionary<Material, Color[]>();
    static readonly string[] HairProps = { "_ColorBase", "_ColorR", "_ColorG", "_ColorB" };
    static bool loaded;
    static int version = 1, appliedVersion;
    static GameObject appliedTo;
    static float nextApply;

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        Look = Mathf.Clamp(PlayerPrefs.GetInt("PM_GLook", 0), 0, Count - 1);
        Skin = Mathf.Clamp(PlayerPrefs.GetInt("PM_GSkin", 2), 0, Count - 1);
        Hair = Mathf.Clamp(PlayerPrefs.GetInt("PM_GHair", 0), 0, Count - 1);
        Tattoo = Mathf.Clamp(PlayerPrefs.GetInt("PM_GTattoo", 0), 0, Count - 1);
    }

    static void Changed()
    {
        PlayerPrefs.SetInt("PM_GLook", Look); PlayerPrefs.SetInt("PM_GSkin", Skin);
        PlayerPrefs.SetInt("PM_GHair", Hair); PlayerPrefs.SetInt("PM_GTattoo", Tattoo);
        version++;
        nextApply = 0f;
        if (PM_Avatar.Character != PM_Avatar.SceneCharacter) PM_Avatar.ChooseCharacter(PM_Avatar.SceneCharacter);
    }

    public static void ChooseLook(int i) { Load(); Look = Mathf.Clamp(i, 0, Count - 1); Changed(); }
    public static void ChooseSkin(int i) { Load(); Skin = Mathf.Clamp(i, 0, Count - 1); Changed(); }
    public static void ChooseHair(int i) { Load(); Hair = Mathf.Clamp(i, 0, Count - 1); Changed(); }
    public static void ChooseTattoo(int i) { Load(); Tattoo = Mathf.Clamp(i, 0, Count - 1); Changed(); }

    // Is a Genies (or other) avatar loader in the scene? Then the style screen replaces the hands screen.
    public static bool LoaderPresent()
    {
        if (PM_Avatar.SceneCharacterAvailable) return true;
        foreach (MonoBehaviour mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (mb == null || !mb.gameObject.scene.IsValid() || !mb.isActiveAndEnabled) continue;
            System.Type t = mb.GetType();
            if (t.Name == "DemoMode" || (t.Namespace != null && t.Namespace.StartsWith("Genies"))) return true;
        }
        return false;
    }

    static Animator Target()
    {
        Animator a = PM_Avatar.AdoptedAnimator;
        if (a != null && a.gameObject.activeInHierarchy) return a;
        return PM_Humanoid.FindSceneCharacter();
    }

    // Called every frame by the avatar rig.
    public static void Tick()
    {
        Load();
        if (Time.unscaledTime < nextApply) return;
        nextApply = Time.unscaledTime + 1f;   // the loader may rebuild materials and meshes while loading
        Animator a = Target();
        if (a == null) return;
        Apply(a.gameObject);
        if (parkPending && !XRSettings.isDeviceActive) Park(a);
    }

    static void Apply(GameObject avatar)
    {
        foreach (SkinnedMeshRenderer r in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.name.StartsWith("PM_Tattoo")) continue;
            int cloth = 0;
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || m.shader == null) continue;
                string sh = m.shader.name;
                if (sh.Contains("MegaSkin") && m.HasProperty("_SkinColor"))
                {
                    Color o = Orig(m, "_SkinColor");
                    Color c = Skins[Skin]; c.a = o.a;
                    m.SetColor("_SkinColor", c);
                }
                else if (sh.Contains("MegaHair"))
                {
                    Color[] o;
                    if (!originalHair.TryGetValue(m, out o))
                    {
                        o = new Color[HairProps.Length];
                        for (int k = 0; k < HairProps.Length; k++) o[k] = m.HasProperty(HairProps[k]) ? m.GetColor(HairProps[k]) : Color.black;
                        originalHair[m] = o;
                    }
                    for (int k = 0; k < HairProps.Length; k++)
                    {
                        if (!m.HasProperty(HairProps[k])) continue;
                        Color c = Hair == 0 ? o[k] : Hairs[Hair] * (0.85f + 0.1f * k);
                        c.a = o[k].a;
                        m.SetColor(HairProps[k], c);
                    }
                }
                else if (m.name.StartsWith("URPMaterial") && m.HasProperty("_BaseColor"))   // the outfit pieces
                {
                    Color o = Orig(m, "_BaseColor");
                    m.SetColor("_BaseColor", Look == 0 ? o : Looks[Look][Mathf.Min(cloth, 2)]);
                    cloth++;
                }
            }
        }
        UpdateTattoo(avatar);
        if (appliedTo != avatar || appliedVersion != version)
        {
            appliedTo = avatar; appliedVersion = version;
            Debug.Log("[PM] Стиль аватара: образ " + Look + ", кожа " + Skin + ", волосы " + Hair + ", тату " + Tattoo);
        }
    }

    static Color Orig(Material m, string prop)
    {
        Color o;
        if (!original.TryGetValue(m, out o)) { o = m.GetColor(prop); original[m] = o; }
        return o;
    }

    // ----- tattoo on the right forearm -----
    static SkinnedMeshRenderer tattooRenderer;
    static Mesh tattooSource;
    static int tattooDesign;

    static void UpdateTattoo(GameObject avatar)
    {
        SkinnedMeshRenderer body = BodyRenderer(avatar);
        bool want = Tattoo > 0 && body != null;
        if (!want)
        {
            if (tattooRenderer != null) Object.Destroy(tattooRenderer.gameObject);
            tattooRenderer = null; tattooSource = null;
            return;
        }
        if (tattooRenderer != null && tattooSource == body.sharedMesh && tattooDesign == Tattoo && tattooRenderer.transform.parent == body.transform.parent) return;
        if (tattooRenderer != null) Object.Destroy(tattooRenderer.gameObject);
        tattooRenderer = BuildTattoo(body, TattooFiles[Tattoo]);
        tattooSource = body.sharedMesh; tattooDesign = Tattoo;
    }

    // The renderer whose skin material covers the arms (the largest skinned mesh with a skin material).
    static SkinnedMeshRenderer BodyRenderer(GameObject avatar)
    {
        SkinnedMeshRenderer best = null; int bestBones = -1;
        foreach (SkinnedMeshRenderer r in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.name.StartsWith("PM_") || r.name.EndsWith("_PMHead") || r.sharedMesh == null || !r.sharedMesh.isReadable) continue;
            if (SkinSubmesh(r) < 0) continue;
            int n = r.bones != null ? r.bones.Length : 0;
            if (n > bestBones) { best = r; bestBones = n; }
        }
        return best;
    }

    static int SkinSubmesh(SkinnedMeshRenderer r)
    {
        Material[] ms = r.sharedMaterials;
        for (int i = 0; i < ms.Length; i++)
            if (ms[i] != null && ms[i].shader != null && (ms[i].shader.name.Contains("Skin") || ms[i].name.ToLower().Contains("skin"))) return i;
        return -1;
    }

    static SkinnedMeshRenderer BuildTattoo(SkinnedMeshRenderer body, string file)
    {
        Texture2D tex = Resources.Load<Texture2D>("PM_Avatar/" + file);
        Mesh src = body.sharedMesh;
        Transform[] bones = body.bones;
        Matrix4x4[] bp = src.bindposes;
        int sub = SkinSubmesh(body);
        if (tex == null || bones == null || bp == null || bp.Length != bones.Length || sub < 0 || sub >= src.subMeshCount) return null;
        int fore = -1, hand = -1;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null) continue;
            string n = bones[i].name;
            if (n.EndsWith("RightForeArm")) fore = i;
            else if (n.EndsWith("RightHand") || n.EndsWith("RightHandBind")) hand = i;
        }
        if (fore < 0 || hand < 0) { Debug.LogWarning("[PM] Тату: не найдены кости предплечья."); return null; }

        // Forearm frame in the mesh (bind pose): axis elbow → wrist, "up" = the back of the forearm.
        Vector3 a = bp[fore].inverse.MultiplyPoint3x4(Vector3.zero);
        Vector3 b = bp[hand].inverse.MultiplyPoint3x4(Vector3.zero);
        Vector3 axis = b - a; float len = axis.magnitude;
        if (len < 1e-4f) return null;
        axis /= len;
        Vector3 up = Vector3.up - Vector3.Dot(Vector3.up, axis) * axis;
        if (up.sqrMagnitude < 0.01f) up = Vector3.forward - Vector3.Dot(Vector3.forward, axis) * axis;
        up.Normalize();
        Vector3 side = Vector3.Cross(axis, up);
        float h = len * 0.62f, w = h * tex.width / (float)tex.height, d0 = len * 0.2f;

        Vector3[] v = src.vertices, nrm = src.normals;
        BoneWeight[] bw = src.boneWeights;
        if (v == null || bw == null || bw.Length != v.Length) return null;
        var isFore = new bool[bones.Length];
        for (int i = 0; i < bones.Length; i++) isFore[i] = bones[i] != null && bones[i].name.Contains("RightForeArm");

        var map = new Dictionary<int, int>();
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nuv = new List<Vector2>(); var nbw = new List<BoneWeight>();
        var tris = new List<int>();
        int[] t = src.GetTriangles(sub);
        var uv = new Vector2[3]; var ang = new float[3];
        for (int k = 0; k + 2 < t.Length; k += 3)
        {
            bool ok = true, inside = false;
            for (int j = 0; j < 3; j++)
            {
                int vi = t[k + j];
                if (!isFore[Dominant(bw[vi])]) { ok = false; break; }
                Vector3 p = v[vi] - a;
                float d = Vector3.Dot(p, axis);
                Vector3 r = p - d * axis;
                ang[j] = Mathf.Atan2(Vector3.Dot(r, side), Vector3.Dot(r, up));
                if (Mathf.Abs(ang[j]) > 1.9f) { ok = false; break; }
                uv[j] = new Vector2(0.5f + ang[j] * r.magnitude / w, (d - d0) / h);
                if (uv[j].x > 0f && uv[j].x < 1f && uv[j].y > 0f && uv[j].y < 1f) inside = true;
            }
            if (!ok || !inside) continue;
            for (int j = 0; j < 3; j++)
            {
                int vi = t[k + j], ni;
                if (!map.TryGetValue(vi, out ni))
                {
                    ni = nv.Count; map[vi] = ni;
                    Vector3 n = nrm != null && nrm.Length == v.Length ? nrm[vi] : Vector3.zero;
                    nv.Add(v[vi] + n * (len * 0.006f));   // just above the skin
                    nn.Add(n); nuv.Add(uv[j]); nbw.Add(bw[vi]);
                }
                tris.Add(ni);
            }
        }
        if (tris.Count == 0) { Debug.LogWarning("[PM] Тату: на предплечье не нашлось кожи."); return null; }

        var mesh = new Mesh();
        mesh.name = "PM_TattooMesh";
        mesh.vertices = nv.ToArray(); mesh.normals = nn.ToArray(); mesh.uv = nuv.ToArray();
        mesh.boneWeights = nbw.ToArray(); mesh.bindposes = bp;
        mesh.triangles = tris.ToArray();
        tex.wrapMode = TextureWrapMode.Clamp;
        var mat = new Material(PM_Util.UnlitShader());
        mat.mainTexture = tex;
        mat.color = new Color(0.9f, 0.9f, 1f, 0.92f);
        var go = new GameObject("PM_Tattoo");
        go.transform.SetParent(body.transform.parent, false);
        go.transform.localPosition = body.transform.localPosition;
        go.transform.localRotation = body.transform.localRotation;
        go.transform.localScale = body.transform.localScale;
        var sr = go.AddComponent<SkinnedMeshRenderer>();
        sr.sharedMesh = mesh; sr.bones = bones; sr.rootBone = body.rootBone;
        sr.sharedMaterial = mat; sr.updateWhenOffscreen = true;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Debug.Log("[PM] Тату на предплечье: " + file + " (" + tris.Count / 3 + " треуг.)");
        return sr;
    }

    static int Dominant(BoneWeight b)
    {
        int bi = b.boneIndex0; float w = b.weight0;
        if (b.weight1 > w) { bi = b.boneIndex1; w = b.weight1; }
        if (b.weight2 > w) { bi = b.boneIndex2; w = b.weight2; }
        if (b.weight3 > w) { bi = b.boneIndex3; }
        return bi;
    }

    // ----- without the headset: the avatar stands next to the menu, facing the viewer -----
    static bool parkPending;
    static Vector3 parkPos, parkViewer;

    public static void ParkNextToMenu(Vector3 pos, Vector3 viewer)
    {
        parkPos = pos; parkViewer = viewer; parkPending = true; nextApply = 0f;
    }

    static void Park(Animator a)
    {
        if (a.GetComponent<PM_Humanoid>() != null) { parkPending = false; return; }
        PM_Humanoid.StopLoaderControl(a.gameObject);
        a.applyRootMotion = false;
        Vector3 p = new Vector3(parkPos.x, parkViewer.y - 1.6f, parkPos.z);
        Vector3 f = parkViewer - p; f.y = 0;
        a.transform.position = p;
        if (f.sqrMagnitude > 0.001f) a.transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
        parkPending = false;
    }
}
