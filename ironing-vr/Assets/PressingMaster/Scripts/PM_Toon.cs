using System.Collections.Generic;
using UnityEngine;

// Cartoon characters built from parts (Assets/Resources/PM_Avatar/toon_*.bytes, made by tools/avatar/toon.py):
// a big-eyed head, a hairstyle, a hat, a top, a bottom, shoes and accessories. No arms: in the headset the hands
// float exactly on the controllers (like Rec Room). Every choice and color is remembered between sessions.
public static class PM_Toon
{
    // ----- options -----
    public static readonly string[] HairKeys = { "buzz", "crop", "quiff", "curly", "bob", "long", "ponytail", "spacebuns", "afro" };
    public static readonly string[] TopKeys = { "tee", "hoodie", "denim", "crop", "puffer", "bomber", "shirt", "bighoodie", "silkdress", "kimono" };
    public static readonly string[] BottomKeys = { "jeans", "cargo", "skirt", "shorts", "joggers", "tutu" };
    // Dresses and the kimono are one piece (the legs are part of the mesh): no bottom is drawn with them.
    public static bool OnePiece { get { string t = TopKeys[Top]; return t == "silkdress" || t == "kimono"; } }
    public static readonly string[] HatKeys = { null, "cap", "beanie", "bucket" };
    public static readonly string[] AccKeys = { "glasses", "sunglasses", "earrings", "necklace", "headphones", "apron" };
    static readonly bool[] HatFitsHair = { true, true, false, false, true, true, true, false, false };   // per hairstyle

    public static readonly Color[] Skins =
    {
        new Color(0.98f, 0.84f, 0.74f), new Color(0.93f, 0.74f, 0.60f), new Color(0.82f, 0.60f, 0.45f),
        new Color(0.66f, 0.45f, 0.31f), new Color(0.47f, 0.31f, 0.21f), new Color(0.31f, 0.20f, 0.14f),
    };
    public static readonly Color[] HairColors =
    {
        new Color(0.07f, 0.055f, 0.05f), new Color(0.30f, 0.17f, 0.08f), new Color(0.86f, 0.68f, 0.36f), new Color(0.72f, 0.24f, 0.08f),
        new Color(0.96f, 0.45f, 0.70f), new Color(0.30f, 0.55f, 0.98f), new Color(0.62f, 0.40f, 0.95f), new Color(0.88f, 0.88f, 0.92f),
    };
    public static readonly Color[] EyeColors =
    {
        new Color(0.22f, 0.52f, 0.92f), new Color(0.25f, 0.62f, 0.32f), new Color(0.42f, 0.25f, 0.12f), new Color(0.62f, 0.48f, 0.22f), new Color(0.55f, 0.35f, 0.85f),
    };
    // Clothes palettes: main, second (rib / inner tee), accent (zip, strings, logo)
    public static readonly Color[][] TopColors =
    {
        new[] { new Color(0.97f, 0.97f, 0.97f), new Color(0.12f, 0.12f, 0.14f), new Color(0.10f, 0.85f, 0.90f) },   // white
        new[] { new Color(0.10f, 0.10f, 0.12f), new Color(0.95f, 0.95f, 0.95f), new Color(0.98f, 0.36f, 0.58f) },   // black
        new[] { new Color(0.98f, 0.52f, 0.68f), new Color(0.98f, 0.95f, 0.92f), new Color(0.98f, 0.86f, 0.30f) },   // pink
        new[] { new Color(0.66f, 0.56f, 0.96f), new Color(0.97f, 0.97f, 0.99f), new Color(0.30f, 0.92f, 0.86f) },   // lilac
        new[] { new Color(0.08f, 0.66f, 0.70f), new Color(0.95f, 0.95f, 0.95f), new Color(0.98f, 0.62f, 0.20f) },   // studio teal
        new[] { new Color(0.98f, 0.55f, 0.18f), new Color(0.15f, 0.15f, 0.17f), new Color(0.95f, 0.95f, 0.95f) },   // orange
        new[] { new Color(0.36f, 0.45f, 0.26f), new Color(0.90f, 0.86f, 0.74f), new Color(0.98f, 0.80f, 0.30f) },   // olive
        new[] { new Color(0.80f, 0.12f, 0.18f), new Color(0.97f, 0.97f, 0.97f), new Color(0.98f, 0.86f, 0.30f) },   // red
    };
    public static readonly Color[] BottomColors =
    {
        new Color(0.25f, 0.36f, 0.62f), new Color(0.10f, 0.10f, 0.12f), new Color(0.80f, 0.72f, 0.56f),
        new Color(0.38f, 0.42f, 0.30f), new Color(0.95f, 0.95f, 0.95f), new Color(0.72f, 0.62f, 0.92f),
    };

    // ----- the current character -----
    public static bool Woman = true;
    public static int Hair = 5, HairColor = 1, Hat, HatColor = 1, Top = 1, TopColor = 2, Bottom, BottomColor, Skin = 1, Eyes;
    public static int Accs = 1 << 2;   // bit i = AccKeys[i]

    public struct Preset
    {
        public string name; public bool woman; public int hair, hairColor, hat, hatColor, top, topColor, bottom, bottomColor, skin, eyes, accs, shoes;
    }
    public static readonly Preset[] Presets =
    {
        new Preset { name = "נויה",  woman = true,  hair = 5, hairColor = 0, hat = 0, top = 8, topColor = 7, bottom = 2, bottomColor = 1, skin = 1, eyes = 2, accs = (1 << 2) | (1 << 3), shoes = 0 },
        new Preset { name = "שירה",  woman = true,  hair = 7, hairColor = 4, hat = 0, top = 7, topColor = 3, bottom = 5, bottomColor = 5, skin = 0, eyes = 0, accs = 1 << 4, shoes = 0 },
        new Preset { name = "מאיה",  woman = true,  hair = 8, hairColor = 0, hat = 0, top = 9, topColor = 7, bottom = 1, bottomColor = 2, skin = 4, eyes = 2, accs = (1 << 1) | (1 << 2), shoes = 1 },
        new Preset { name = "איתי",  woman = false, hair = 2, hairColor = 1, hat = 0, top = 6, topColor = 0, bottom = 0, bottomColor = 1, skin = 2, eyes = 2, accs = 1 << 3, shoes = 0 },
        new Preset { name = "דניאל", woman = false, hair = 1, hairColor = 0, hat = 1, hatColor = 4, top = 1, topColor = 6, bottom = 1, bottomColor = 2, skin = 3, eyes = 2, accs = 1 << 0, shoes = 0 },
        new Preset { name = "עומר",  woman = false, hair = 0, hairColor = 2, hat = 2, hatColor = 1, top = 4, topColor = 5, bottom = 4, bottomColor = 1, skin = 1, eyes = 1, accs = 1 << 4, shoes = 1 },
    };

    static bool loaded;
    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        string s = PlayerPrefs.GetString("PM_Toon", "");
        if (string.IsNullOrEmpty(s)) { ApplyPreset(0, false); return; }
        string[] a = s.Split(',');
        int[] v = new int[13];
        for (int i = 0; i < v.Length && i < a.Length; i++) int.TryParse(a[i], out v[i]);
        Woman = v[0] == 1;
        Hair = Clamp(v[1], HairKeys.Length); HairColor = Clamp(v[2], HairColors.Length);
        Hat = Clamp(v[3], HatKeys.Length); HatColor = Clamp(v[4], TopColors.Length);
        Top = Clamp(v[5], TopKeys.Length); TopColor = Clamp(v[6], TopColors.Length);
        Bottom = Clamp(v[7], BottomKeys.Length); BottomColor = Clamp(v[8], BottomColors.Length);
        Skin = Clamp(v[9], Skins.Length); Eyes = Clamp(v[10], EyeColors.Length); Accs = v[11];
    }

    static int Clamp(int v, int n) { return Mathf.Clamp(v, 0, n - 1); }

    public static void Save()
    {
        PlayerPrefs.SetString("PM_Toon", string.Join(",", new[] { Woman ? 1 : 0, Hair, HairColor, Hat, HatColor, Top, TopColor, Bottom, BottomColor, Skin, Eyes, Accs, 0 }));
        PM_Avatar.ToonChanged();
    }

    public static void ApplyPreset(int i, bool save = true)
    {
        Preset p = Presets[Mathf.Clamp(i, 0, Presets.Length - 1)];
        Woman = p.woman; Hair = p.hair; HairColor = p.hairColor; Hat = p.hat; HatColor = p.hatColor;
        Top = p.top; TopColor = p.topColor; Bottom = p.bottom; BottomColor = p.bottomColor; Skin = p.skin; Eyes = p.eyes; Accs = p.accs;
        PM_Avatar.SetShoesQuiet(p.shoes);
        if (save) Save();
    }

    public static bool HasAcc(int i) { return (Accs & (1 << i)) != 0; }
    public static void ToggleAcc(int i)
    {
        Accs ^= 1 << i;
        if (i == 0 && HasAcc(0)) Accs &= ~(1 << 1);   // glasses or sunglasses, not both
        if (i == 1 && HasAcc(1)) Accs &= ~(1 << 0);
        Save();
    }
    public static bool HatHidden { get { return Hat > 0 && !HatFitsHair[Hair]; } }
    public static Color SkinColor { get { Load(); return Skins[Skin]; } }

    // ----- building -----
    static readonly Vector3 HeadCenter = new Vector3(0f, 1.645f, -0.01f);
    // Cartoon proportions (same as tools/avatar/toon.py): the body is 0.9 tall, the head group is 1.18x around the neck base.
    public const float BodyY = 0.9f, HeadScale = 1.18f, NeckY = 1.40f;
    public static readonly float EyeHeight = NeckY * BodyY + (1.607f - NeckY) * HeadScale;   // 1.50 m in the model
    static Material skin, hair, iris, mouth, white, dark, top1, top2, top3, top4, bot1, bot2, bot3, bot4, bot5, hat1, hat2, accMain, accSecond, gold, black;

    static Material Mat(Color c, float smooth, float metal = 0f)
    {
        var m = new Material(PM_Util.LitShader());
        m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        return m;
    }

    static void Col(Material m, Color c) { m.SetColor("_BaseColor", c); m.SetColor("_Color", c); }

    static void Tex(Material m, string name, float tiling)
    {
        Texture2D t = name != null ? Resources.Load<Texture2D>("PM_Avatar/" + name) : null;
        if (t != null) t.wrapMode = TextureWrapMode.Repeat;
        m.SetTexture("_BaseMap", t); m.SetTexture("_MainTex", t);
        m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling)); m.SetTextureScale("_MainTex", new Vector2(tiling, tiling));
    }

    static void Materials()
    {
        if (skin == null)
        {
            skin = Mat(Color.white, 0.35f); hair = Mat(Color.white, 0.45f); iris = Mat(Color.white, 0.92f); mouth = Mat(new Color(0.72f, 0.24f, 0.30f), 0.6f);
            white = Mat(Color.white, 0.92f); dark = Mat(new Color(0.03f, 0.03f, 0.04f), 0.9f);
            top1 = Mat(Color.white, 0.25f); top2 = Mat(Color.white, 0.2f); top3 = Mat(Color.white, 0.55f); top4 = Mat(new Color(0.1f, 0.09f, 0.08f), 0.4f);
            bot1 = Mat(Color.white, 0.2f); bot2 = Mat(Color.white, 0.2f); bot3 = Mat(new Color(0.85f, 0.70f, 0.32f), 0.8f, 0.7f); bot4 = Mat(new Color(0.12f, 0.10f, 0.09f), 0.45f); bot5 = Mat(Color.white, 0.3f);
            hat1 = Mat(Color.white, 0.25f); hat2 = Mat(Color.white, 0.25f);
            accMain = Mat(new Color(0.95f, 0.35f, 0.45f), 0.6f); accSecond = Mat(new Color(0.08f, 0.08f, 0.10f), 0.95f);
            gold = Mat(new Color(0.96f, 0.78f, 0.36f), 0.85f, 0.9f); black = Mat(new Color(0.06f, 0.06f, 0.07f), 0.7f);
        }
        Col(skin, Skins[Skin]);
        Col(hair, HairColors[HairColor]);
        Col(iris, EyeColors[Eyes]);
        Color lip = Color.Lerp(Skins[Skin], new Color(0.78f, 0.22f, 0.32f), Woman ? 0.75f : 0.45f);
        Col(mouth, lip);
        Color[] tc = TopColors[TopColor];
        Col(top1, tc[0]); Col(top2, tc[1]); Col(top3, tc[2]);
        string tk = TopKeys[Top];
        Col(top4, new Color(0.1f, 0.09f, 0.08f));
        Tex(top1, tk == "denim" ? "tex_denim" : tk == "hoodie" || tk == "bighoodie" || tk == "crop" ? "tex_knit" : tk == "kimono" ? "tex_sakura" : null, tk == "denim" ? 1.2f : tk == "kimono" ? 1f : 2f);
        top1.SetFloat("_Smoothness", tk == "puffer" ? 0.65f : tk == "bomber" ? 0.5f : tk == "silkdress" ? 0.88f : tk == "kimono" ? 0.5f : 0.22f);
        top1.SetFloat("_Metallic", tk == "silkdress" ? 0.15f : 0f);
        if (tk == "denim") Col(top1, Color.Lerp(tc[0], new Color(0.35f, 0.48f, 0.72f), 0.65f));   // denim stays denim-ish
        if (tk == "shirt") Col(top3, tc[1]);                                                          // buttons
        if (tk == "kimono")
        {
            Col(top1, Color.white);   // the sakura picture keeps its own colors
            Col(top2, tc[1]);         // collar
            Col(top3, tc[0]);         // obi
            Col(top4, tc[2]);         // cord
        }
        Col(bot1, BottomColors[BottomColor]);
        string bk = BottomKeys[Bottom];
        Tex(bot1, bk == "jeans" || bk == "shorts" ? "tex_denim" : bk == "skirt" ? "tex_plaid" : bk == "cargo" ? "tex_chino" : "tex_knit", bk == "skirt" ? 1.5f : 1.2f);
        Col(bot2, new Color(0.97f, 0.97f, 0.97f));
        bool joggers = bk == "joggers";   // drawstrings instead of a metal buckle
        Col(bot3, joggers ? new Color(0.97f, 0.97f, 0.97f) : new Color(0.85f, 0.70f, 0.32f));
        bot3.SetFloat("_Metallic", joggers ? 0f : 0.7f);
        if (bk == "joggers") Col(bot2, Color.Lerp(BottomColors[BottomColor], Color.black, 0.15f));
        if (bk == "tutu")   // three tulle layers in three colors
        {
            Tex(bot1, null, 1f);
            Color c = BottomColors[BottomColor];
            float h, sat, v;
            Color.RGBToHSV(c, out h, out sat, out v);
            // pastel neighbours of the chosen color (white/black/beige: pink and mint)
            Color c2 = sat < 0.25f ? new Color(0.98f, 0.66f, 0.80f) : Color.HSVToRGB((h + 0.14f) % 1f, 0.42f, 1f);
            Color c3 = sat < 0.25f ? new Color(0.66f, 0.92f, 0.86f) : Color.HSVToRGB((h + 0.86f) % 1f, 0.42f, 1f);
            Col(bot3, c2); bot3.SetFloat("_Metallic", 0f);
            Col(bot5, c3);
        }
        Color[] hc = TopColors[HatColor];
        Col(hat1, hc[0]); Col(hat2, hc[1]);
    }

    // Sleeves of the current top for the arms: end (0 = bare arm, 0.38 = short sleeve, 1 = long), looseness, cuff.
    public static void SleeveInfo(out Material skinMat, out Material sleeve, out Material cuff, out float end, out float extra, out float cuffLen, out bool wide)
    {
        Load(); Materials();
        skinMat = skin; sleeve = top1; cuff = top2;
        string t = TopKeys[Top];
        wide = t == "kimono";
        cuffLen = t == "hoodie" || t == "bighoodie" || t == "bomber" || t == "puffer" ? 0.08f : 0.05f;
        if (t == "crop" || t == "silkdress") { end = 0f; extra = 0f; }
        else if (t == "tee") { end = 0.36f; extra = 0.012f; }
        else { end = 1f; extra = t == "puffer" ? 0.022f : t == "bighoodie" ? 0.018f : 0.009f; }
        if (t == "shirt" || t == "denim" || t == "kimono") cuff = top1;
    }

    // Builds the character under parent (feet at the parent's origin, facing +Z). live: in the headset
    // (head parts only in the mirror). Returns the head group (moved by the body follower).
    public static Transform Build(Transform parent, bool live)
    {
        Load();
        Materials();
        string g = Woman ? "f" : "m";
        int headLayer = live ? PM_Avatar.MirrorLayer : 0;
        var headGroup = new GameObject("ToonHead").transform;
        headGroup.SetParent(parent, false);
        headGroup.localScale = Vector3.one * HeadScale;
        headGroup.localPosition = new Vector3(0f, NeckY * BodyY - NeckY * HeadScale, 0f);
        var bodyGroup = new GameObject("ToonBody").transform;
        bodyGroup.SetParent(parent, false);
        bodyGroup.localScale = new Vector3(1f, BodyY, 1f);
        Part(headGroup, "toon_head_" + g, headLayer, false, skin, iris, mouth, white, dark, hair);
        Part(headGroup, "toon_hair_" + HairKeys[Hair], headLayer, Woman, hair, accMain);
        if (Hat > 0 && !HatHidden) Part(headGroup, "toon_hat_" + HatKeys[Hat], headLayer, Woman, hat1, hat1, hat2, gold, black);
        if (HasAcc(0)) Part(headGroup, "toon_acc_glasses", headLayer, Woman, black, black, accSecond, gold, black);
        if (HasAcc(1)) Part(headGroup, "toon_acc_sunglasses", headLayer, Woman, black, black, accSecond, gold, black);
        if (HasAcc(2)) Part(headGroup, "toon_acc_earrings", headLayer, Woman, gold, gold, gold, gold, gold);
        Part(bodyGroup, "toon_top_" + TopKeys[Top] + "_" + g, 0, false, skin, top1, top2, top3, top4);
        if (!OnePiece) Part(bodyGroup, "toon_bottom_" + BottomKeys[Bottom] + "_" + g, 0, false, skin, bot1, bot2, bot3, bot4, bot5);
        Material[] shoes = PM_Avatar.ShoeMaterials();
        Part(parent, PM_Avatar.ShoeKey(), 0, false, shoes);
        if (HasAcc(3)) Part(bodyGroup, "toon_acc_necklace", 0, false, gold, gold, gold, gold, gold);
        if (HasAcc(4)) Part(bodyGroup, "toon_acc_headphones", 0, false, black, accMain, accMain, gold, black);
        if (HasAcc(5)) Part(bodyGroup, "toon_acc_apron_" + g, 0, false, skin, PM_Avatar.ApronMaterial(), top2, gold, black);
        return headGroup;
    }

    public static string HandKey { get { return Woman ? "toonf" : "toonm"; } }

    // Preview figure: arms hanging down, the cartoon hands at the hips.
    public static void AddStaticArms(Transform figure)
    {
        float sx = Woman ? 0.112f : 0.135f;
        float s = figure.lossyScale.x;
        for (int i = 0; i < 2; i++)
        {
            bool left = i == 0;
            float sg = left ? -1f : 1f;
            PM_Hand h = PM_Hand.Create(figure, left, HandKey, false);
            if (h == null) continue;
            h.transform.localPosition = new Vector3(sg * (sx + 0.045f), 0.66f, 0.02f);
            h.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            PM_ToonArm arm = PM_ToonArm.Create(figure, left);
            Vector3 sh = figure.TransformPoint(new Vector3(sg * sx, 1.325f * BodyY - 0.012f, -0.01f));
            arm.Pose(sh, h.Wrist, figure.right * sg, figure.forward, s);
        }
    }

    static void Part(Transform parent, string mesh, int layer, bool headScale, params Material[] mats)
    {
        Mesh m = PM_Body.LoadMesh(mesh);
        if (m == null) return;
        var go = new GameObject(mesh);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        if (headScale)   // hair, hats and glasses are made for the bigger (man's) head
        {
            go.transform.localScale = Vector3.one * 0.97f;
            go.transform.localPosition = HeadCenter * (1f - 0.97f);
        }
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>();
        var use = new Material[m.subMeshCount];
        for (int i = 0; i < use.Length; i++) use[i] = mats[Mathf.Min(i, mats.Length - 1)];
        r.sharedMaterials = use;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}

// The player's toon body in the headset: stands under the eyes, turns after the head; the head (seen only in
// the mirror) stays exactly where the eyes are. The hands are separate (PM_Hand on the controllers).
public class PM_ToonBody : MonoBehaviour
{
    Transform cam, origin, figure, headGroup;
    float yaw, eyeReal = 1.0f, scale = 1f;
    PM_Hand lh, rh;
    PM_ToonArm armL, armR;
    static PM_ToonBody instance;

    // The torso as a vertical capsule (world space) — the iron is kept out of it.
    public static bool Capsule(out Vector3 a, out Vector3 b, out float r)
    {
        a = b = Vector3.zero; r = 0f;
        if (instance == null || !instance.isActiveAndEnabled) return false;
        Transform f = instance.figure;
        a = f.TransformPoint(new Vector3(0f, 0.92f * PM_Toon.BodyY, 0f));
        b = f.TransformPoint(new Vector3(0f, 1.32f * PM_Toon.BodyY, 0f));
        r = (PM_Toon.Woman ? 0.13f : 0.15f) * instance.scale;
        return true;
    }
    public static Vector3 Forward { get { return instance != null ? instance.figure.forward : Vector3.forward; } }

    public static PM_ToonBody Create(Transform xrOrigin, PM_Hand left, PM_Hand right)
    {
        var go = new GameObject("PM_ToonBody");
        var b = go.AddComponent<PM_ToonBody>();
        instance = b;
        b.lh = left; b.rh = right;
        b.origin = xrOrigin;
        b.figure = new GameObject("Figure").transform;
        b.figure.SetParent(go.transform, false);
        b.headGroup = PM_Toon.Build(b.figure, true);
        b.armL = PM_ToonArm.Create(go.transform, true);
        b.armR = PM_ToonArm.Create(go.transform, false);
        Camera main = Camera.main;
        if (main != null)
        {
            b.cam = main.transform;
            main.cullingMask &= ~(1 << PM_Avatar.MirrorLayer);
        }
        return b;
    }

    void LateUpdate()
    {
        if (cam == null) { cam = Camera.main != null ? Camera.main.transform : null; if (cam == null) return; }
        float floorY = origin != null ? origin.position.y : cam.position.y - 1.6f;
        eyeReal = Mathf.Max(eyeReal, Mathf.Clamp(cam.position.y - floorY, 1.0f, 1.95f));
        float s = eyeReal / PM_Toon.EyeHeight;
        Vector3 f = cam.forward; f.y = 0;
        if (f.sqrMagnitude > 0.001f)
        {
            float target = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            float diff = Mathf.DeltaAngle(yaw, target);
            if (Mathf.Abs(diff) > 25f) yaw += diff * Mathf.Min(1f, Time.deltaTime * 4f);
        }
        Quaternion rot = Quaternion.Euler(0, yaw, 0);
        figure.localScale = Vector3.one * s;
        figure.rotation = rot;
        // The body stands a bit behind the eyes, so looking down at the board the chest does not hide it.
        const float back = 0.17f;
        figure.position = cam.position - Vector3.up * PM_Toon.EyeHeight * s - rot * new Vector3(0, 0, back * s);
        // The head (seen in the mirror) turns and nods with the real head, around the neck base;
        // its eyes stay at the camera.
        Quaternion look = Quaternion.Inverse(rot) * cam.rotation;
        Vector3 neck = new Vector3(0f, PM_Toon.NeckY * PM_Toon.BodyY, back - 0.10f);
        headGroup.localRotation = look;
        headGroup.localPosition = neck - look * new Vector3(0f, PM_Toon.NeckY * PM_Toon.HeadScale, 0f);
        scale = s;
        // Arms: from the shoulders to the wrists of the hands on the controllers (they never come apart).
        float sx = PM_Toon.Woman ? 0.112f : 0.135f;
        Vector3 shL = figure.TransformPoint(new Vector3(-sx, 1.325f * PM_Toon.BodyY - 0.012f, -0.01f));
        Vector3 shR = figure.TransformPoint(new Vector3(sx, 1.325f * PM_Toon.BodyY - 0.012f, -0.01f));
        Vector3 right = rot * Vector3.right, fwd = rot * Vector3.forward;
        if (armL != null) armL.Pose(shL, lh != null && lh.isActiveAndEnabled ? lh.Wrist : null, -right, fwd, s);
        if (armR != null) armR.Pose(shR, rh != null && rh.isActiveAndEnabled ? rh.Wrist : null, right, fwd, s);
    }
}

// A smooth arm (one tube mesh, rebuilt every frame) from the shoulder through a bent elbow to the wrist.
// Sleeves follow the top: short (tee), none (crop top, silk dress), long with a cuff, or the wide kimono sleeve.
public class PM_ToonArm : MonoBehaviour
{
    const int Rings = 26, Sides = 14;
    Mesh mesh;
    MeshRenderer mr;
    Vector3[] verts;
    bool left;

    public static PM_ToonArm Create(Transform parent, bool left)
    {
        var go = new GameObject(left ? "PM_ArmL" : "PM_ArmR");
        go.transform.SetParent(parent, false);
        var a = go.AddComponent<PM_ToonArm>();
        a.left = left;
        a.mesh = new Mesh { name = "PM_Arm" };
        a.mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = a.mesh;
        a.mr = go.AddComponent<MeshRenderer>();
        a.mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        a.Setup();
        return a;
    }

    float sleeveEnd, sleeveExtra, cuffLen;
    bool wide;

    void Setup()
    {
        Material skin, sleeve, cuff;
        PM_Toon.SleeveInfo(out skin, out sleeve, out cuff, out sleeveEnd, out sleeveExtra, out cuffLen, out wide);
        mr.sharedMaterials = new[] { skin, sleeve, cuff };
        // Rings: 0..Rings-1 along the arm, plus one extra ring at the sleeve end (the step from cloth to skin).
        int rc = Rings + 1;
        verts = new Vector3[rc * Sides];
        mesh.vertices = verts;
        var tri = new List<int>[3]; for (int i = 0; i < 3; i++) tri[i] = new List<int>();
        for (int r = 0; r < rc - 1; r++)
        {
            float t = RingT(r), t2 = RingT(r + 1);
            int sub = SubAt((t + t2) * 0.5f);
            if (r == SleeveRing()) sub = 1;   // the sleeve hem
            for (int k = 0; k < Sides; k++)
            {
                int a = r * Sides + k, b = r * Sides + (k + 1) % Sides, c = a + Sides, d = b + Sides;
                // cross(b - a, c - a) points outwards (Unity front face) for both arms: the ring frame is not mirrored
                tri[sub].Add(a); tri[sub].Add(b); tri[sub].Add(c); tri[sub].Add(b); tri[sub].Add(d); tri[sub].Add(c);
            }
        }
        mesh.subMeshCount = 3;
        for (int i = 0; i < 3; i++) mesh.SetTriangles(tri[i], i);
    }

    // Ring parameter along the arm (the extra ring repeats the sleeve end).
    int SleeveRing() { return sleeveEnd > 0f && sleeveEnd < 1f ? Mathf.RoundToInt(sleeveEnd * (Rings - 1)) : -1; }
    float RingT(int r)
    {
        int sr = SleeveRing();
        if (sr < 0) return Mathf.Clamp01(r / (float)(Rings - 1));
        if (r <= sr) return r / (float)(Rings - 1);
        return (r - 1) / (float)(Rings - 1);
    }
    int SubAt(float t)
    {
        if (t > sleeveEnd) return 0;
        if (sleeveEnd >= 1f && t > 1f - cuffLen) return 2;
        return 1;
    }

    float Radius(int r, float t, float s)
    {
        float baseR = Mathf.Lerp(0.050f, 0.040f, Mathf.Clamp01(t / 0.5f));
        if (t > 0.5f) baseR = Mathf.Lerp(0.040f, 0.031f, (t - 0.5f) / 0.5f);
        bool cloth = t <= sleeveEnd && !(SleeveRing() >= 0 && r == SleeveRing() + 1);   // the extra ring starts the bare arm
        float rad = baseR + (cloth ? sleeveExtra : 0f);
        if (cloth && wide) rad += Mathf.Clamp01((t - 0.35f) / 0.65f) * 0.05f;            // kimono sleeve opens towards the wrist
        if (cloth && sleeveEnd >= 1f && t > 1f - cuffLen) rad += 0.004f;                    // rib cuff
        return rad * s;
    }

    public void Pose(Vector3 shoulder, Transform wrist, Vector3 outward, Vector3 fwd, float s)
    {
        mr.enabled = wrist != null;
        if (wrist == null) return;
        Vector3 w = wrist.position;
        float L1 = 0.26f * s, L2 = 0.24f * s;
        Vector3 to = w - shoulder;
        float dist = to.magnitude;
        Vector3 dir = dist > 1e-4f ? to / dist : Vector3.down;
        float dc = Mathf.Clamp(dist, 0.05f, L1 + L2 - 0.001f);
        Vector3 pole = Vector3.down * 0.7f + outward * 0.6f - fwd * 0.4f;
        pole = (pole - Vector3.Dot(pole, dir) * dir).normalized;
        float a = (L1 * L1 - L2 * L2 + dc * dc) / (2f * dc);
        float h = Mathf.Sqrt(Mathf.Max(0f, L1 * L1 - a * a));
        Vector3 elbow = shoulder + dir * (a * Mathf.Min(1f, dist / dc)) + pole * h;   // a fully stretched arm stretches a little (cartoon)
        // Smooth curve through the elbow (quadratic Bezier with its middle on the elbow).
        Vector3 ctrl = elbow * 2f - (shoulder + w) * 0.5f;
        Vector3 prevN = Vector3.Cross(dir, pole).normalized;
        if (prevN.sqrMagnitude < 0.01f) prevN = outward;
        Matrix4x4 toLocal = transform.worldToLocalMatrix;
        int rc = Rings + 1;
        for (int r = 0; r < rc; r++)
        {
            float t = RingT(r);
            Vector3 p = (1 - t) * (1 - t) * shoulder + 2 * (1 - t) * t * ctrl + t * t * w;
            Vector3 tan = (2 * (1 - t) * (ctrl - shoulder) + 2 * t * (w - ctrl)).normalized;
            Vector3 n = (prevN - Vector3.Dot(prevN, tan) * tan).normalized;   // parallel transport: no twisting
            prevN = n;
            Vector3 b = Vector3.Cross(tan, n);
            float rad = Radius(r, t, s);
            for (int k = 0; k < Sides; k++)
            {
                float ang = k * Mathf.PI * 2f / Sides;
                verts[r * Sides + k] = toLocal.MultiplyPoint3x4(p + (n * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * rad);
            }
        }
        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
