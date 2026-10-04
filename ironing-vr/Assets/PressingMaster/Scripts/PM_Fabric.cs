using UnityEngine;

// A piece of fabric lying on the ironing board. Wrinkles (normal map) disappear where the iron passes.
// Wrong settings in exam mode leave scorch marks or water spots.
public class PM_Fabric : MonoBehaviour
{
    const int Res = 256;

    public PM_FabricInfo info;
    public float sizeX = 0.5f, sizeZ = 0.38f;   // meters

    public float Progress { get; private set; }  // 0..1
    public float Damage { get; private set; }    // 0..1 (scorch / melt / spots)
    public bool Done { get { return Progress >= 0.96f; } }

    Texture2D albedoTex, normalTex;
    Color32[] baseAlbedo, albedo, wrinkleNormal, normal;
    float[] smooth, scorch, spots;
    float smoothSum;
    bool dirtyN, dirtyA;
    Material mat;

    public static PM_Fabric Create(PM_FabricInfo info, Vector3 center, Quaternion rot, Transform parent, int seed)
    {
        var go = new GameObject("PM_Fabric_" + info.type);
        go.transform.SetParent(parent, true);
        go.transform.position = center;
        go.transform.rotation = rot;
        var f = go.AddComponent<PM_Fabric>();
        f.info = info;
        f.Build(seed);
        return f;
    }

    void Build(int seed)
    {
        // Mesh: flat quad, slightly bigger in the middle to look like cloth.
        var mesh = new Mesh();
        int n = 16;
        var v = new Vector3[(n + 1) * (n + 1)];
        var uv = new Vector2[v.Length];
        var tri = new int[n * n * 6];
        for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++)
            {
                float u = x / (float)n, w = z / (float)n;
                v[z * (n + 1) + x] = new Vector3((u - 0.5f) * sizeX, 0, (w - 0.5f) * sizeZ);
                uv[z * (n + 1) + x] = new Vector2(u, w);
            }
        int k = 0;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * (n + 1) + x;
                tri[k++] = i; tri[k++] = i + n + 1; tri[k++] = i + 1;
                tri[k++] = i + 1; tri[k++] = i + n + 1; tri[k++] = i + n + 2;
            }
        mesh.vertices = v;
        mesh.uv = uv;
        mesh.triangles = tri;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = gameObject.AddComponent<MeshRenderer>();
        var mc = gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;

        MakeTextures(seed);
        mat = new Material(PM_Util.LitShader());
        mat.SetTexture("_BaseMap", albedoTex);
        mat.SetTexture("_MainTex", albedoTex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_BumpMap", normalTex);
        mat.SetFloat("_BumpScale", 1.6f);
        mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_Smoothness", info.smoothness);
        mat.SetFloat("_Metallic", 0f);
        mr.material = mat;
    }

    void MakeTextures(int seed)
    {
        int count = Res * Res;
        baseAlbedo = new Color32[count];
        albedo = new Color32[count];
        wrinkleNormal = new Color32[count];
        normal = new Color32[count];
        smooth = new float[count];
        scorch = new float[count];
        spots = new float[count];

        // Height field of wrinkles: ridged noise + diagonal folds.
        float ox = seed * 13.1f, oz = seed * 7.7f;
        float depth = info.type == PM_FabricType.Linen ? 1.3f : (info.type == PM_FabricType.Silk ? 0.7f : 1f);
        var h = new float[count];
        for (int y = 0; y < Res; y++)
            for (int x = 0; x < Res; x++)
            {
                float u = x / (float)Res, w = y / (float)Res;
                float n1 = Mathf.PerlinNoise(ox + u * 3.2f, oz + w * 2.6f);
                float ridge = 1f - Mathf.Abs(n1 * 2f - 1f);
                float n2 = Mathf.PerlinNoise(ox + 40 + u * 7f, oz + 40 + w * 6f);
                float ridge2 = 1f - Mathf.Abs(n2 * 2f - 1f);
                float folds = Mathf.Pow(ridge, 6f) * 0.9f + Mathf.Pow(ridge2, 8f) * 0.5f;
                float fine = Mathf.PerlinNoise(ox + u * 22f, oz + w * 22f) * 0.08f;
                h[y * Res + x] = (folds + fine) * depth;
            }
        for (int y = 0; y < Res; y++)
            for (int x = 0; x < Res; x++)
            {
                float hl = h[y * Res + Mathf.Max(x - 1, 0)], hr = h[y * Res + Mathf.Min(x + 1, Res - 1)];
                float hd = h[Mathf.Max(y - 1, 0) * Res + x], hu = h[Mathf.Min(y + 1, Res - 1) * Res + x];
                Vector3 nn = new Vector3((hl - hr) * 6f, (hd - hu) * 6f, 1f).normalized;
                wrinkleNormal[y * Res + x] = new Color32((byte)((nn.x * 0.5f + 0.5f) * 255), (byte)((nn.y * 0.5f + 0.5f) * 255), (byte)((nn.z * 0.5f + 0.5f) * 255), 255);
                normal[y * Res + x] = wrinkleNormal[y * Res + x];
            }

        // Weave pattern (albedo).
        Color c = info.color;
        for (int y = 0; y < Res; y++)
            for (int x = 0; x < Res; x++)
            {
                float m = 1f;
                switch (info.weave)
                {
                    case PM_Weave.Plain:
                        m = ((x / 2 + y / 2) % 2 == 0) ? 1.04f : 0.94f;
                        break;
                    case PM_Weave.Slub:
                        m = ((x / 3 + y / 3) % 2 == 0) ? 1.05f : 0.92f;
                        m *= 0.9f + 0.2f * Mathf.PerlinNoise(ox + y * 0.35f, x * 0.02f);
                        break;
                    case PM_Weave.Twill:
                        m = (((x + y) / 3) % 2 == 0) ? 1.08f : 0.9f;
                        m *= 0.9f + 0.2f * Mathf.PerlinNoise(x * 0.5f, y * 0.5f);
                        break;
                    case PM_Weave.Satin:
                        m = 0.97f + 0.06f * Mathf.Sin(y * 0.6f);
                        break;
                    case PM_Weave.Smooth:
                        m = 0.98f + 0.04f * ((x + y) % 2);
                        break;
                }
                // Wrinkle shading baked lightly into the color.
                float shade = 1f - h[y * Res + x] * 0.12f;
                Color col = c * m * shade;
                col.a = 1f;
                baseAlbedo[y * Res + x] = col;
                albedo[y * Res + x] = baseAlbedo[y * Res + x];
            }

        albedoTex = new Texture2D(Res, Res, TextureFormat.RGBA32, true);
        albedoTex.wrapMode = TextureWrapMode.Clamp;
        albedoTex.SetPixels32(albedo);
        albedoTex.Apply(true);
        normalTex = new Texture2D(Res, Res, TextureFormat.RGBA32, true, true);
        normalTex.wrapMode = TextureWrapMode.Clamp;
        normalTex.SetPixels32(normal);
        normalTex.Apply(true);
    }

    // Called by the iron every frame while it touches the fabric.
    // smoothRate: 0..1 multiplier of smoothing speed. scorchRate / spotRate: damage per second (0 = none).
    public void Iron(Vector2 uv, float radiusMeters, float dt, float smoothRate, float scorchRate, float spotRate)
    {
        int cx = Mathf.RoundToInt(uv.x * (Res - 1));
        int cy = Mathf.RoundToInt(uv.y * (Res - 1));
        int rx = Mathf.Max(2, Mathf.RoundToInt(radiusMeters / sizeX * Res));
        int ry = Mathf.Max(2, Mathf.RoundToInt(radiusMeters / sizeZ * Res));
        float sStep = dt / Mathf.Max(0.05f, info.ironSeconds) * smoothRate;
        for (int y = cy - ry; y <= cy + ry; y++)
        {
            if (y < 0 || y >= Res) continue;
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                if (x < 0 || x >= Res) continue;
                float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
                float d2 = dx * dx + dy * dy;
                if (d2 > 1f) continue;
                float fall = 1f - d2 * 0.5f;
                int i = y * Res + x;
                if (sStep > 0f && smooth[i] < 1f)
                {
                    float old = smooth[i];
                    smooth[i] = Mathf.Min(1f, old + sStep * fall);
                    smoothSum += smooth[i] - old;
                    Color32 a = wrinkleNormal[i];
                    float s = smooth[i];
                    normal[i] = new Color32((byte)Mathf.Lerp(a.r, 128, s), (byte)Mathf.Lerp(a.g, 128, s), (byte)Mathf.Lerp(a.b, 255, s), 255);
                    dirtyN = true;
                }
                bool hurt = false;
                if (scorchRate > 0f) { scorch[i] = Mathf.Min(1f, scorch[i] + scorchRate * dt * fall); hurt = true; }
                if (spotRate > 0f && Random.value < spotRate * dt * 3f) { spots[i] = 1f; hurt = true; }
                if (hurt) { albedo[i] = Damaged(i); dirtyA = true; }
            }
        }
        Progress = Mathf.Clamp01(smoothSum / (Res * Res) / 0.97f);
        if (scorchRate > 0f || spotRate > 0f) Damage = Mathf.Clamp01(Damage + (scorchRate + spotRate * 0.5f) * dt * 0.5f);
    }

    Color32 Damaged(int i)
    {
        Color c = baseAlbedo[i];
        if (spots[i] > 0f) c = Color.Lerp(c, c * 0.7f, 0.6f);
        float s = scorch[i];
        if (s > 0f)
        {
            Color burn = info.type == PM_FabricType.Polyester ? new Color(0.08f, 0.07f, 0.07f) : new Color(0.35f, 0.2f, 0.08f);
            c = Color.Lerp(c, burn, Mathf.SmoothStep(0f, 1f, s));
        }
        c.a = 1f;
        return c;
    }

    void LateUpdate()
    {
        if (dirtyN) { normalTex.SetPixels32(normal); normalTex.Apply(true); dirtyN = false; }
        if (dirtyA) { albedoTex.SetPixels32(albedo); albedoTex.Apply(true); dirtyA = false; }
        if (info != null && info.type == PM_FabricType.Polyester && Damage > 0.05f)
            mat.SetFloat("_Smoothness", Mathf.Lerp(info.smoothness, 0.95f, Damage));
    }

    void OnDestroy()
    {
        if (albedoTex != null) Destroy(albedoTex);
        if (normalTex != null) Destroy(normalTex);
        if (mat != null) Destroy(mat);
    }
}
