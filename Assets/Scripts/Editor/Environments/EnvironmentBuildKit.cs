using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Shared helpers for the environment builders: procedural textures, meshes, materials and asset saving.
/// Every environment builder uses this so new environments can be added with only a layout script.
/// </summary>
public static partial class EnvironmentBuildKit
{
    public const string RootFolder = "Assets/Environments";

    // ---------------------------------------------------------------- Assets

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    /// <summary>Saves an asset, overwriting in place if it exists so GUID references stay valid.</summary>
    public static T SaveAsset<T>(T asset, string path) where T : Object
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(asset, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(asset);
            return existing;
        }
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>Finds the top of the gameplay floor (the collider tagged "Ground"), so visuals line up with physics.</summary>
    public static float FindGroundHeight(float fallback = -2.1f)
    {
        GameObject[] grounds;
        try { grounds = GameObject.FindGameObjectsWithTag("Ground"); }
        catch { return fallback; }

        float best = float.NegativeInfinity;
        foreach (GameObject g in grounds)
        {
            Collider c = g.GetComponent<Collider>();
            if (c != null && c.bounds.size.x > 50f) best = Mathf.Max(best, c.bounds.max.y);
        }
        return float.IsNegativeInfinity(best) ? fallback : best;
    }

    // ---------------------------------------------------------------- Textures

    public static Texture2D SaveTexture(Color[] pixels, int size, string path, bool isNormal, bool sRGB, int maxSize = 1024)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = sRGB && !isNormal;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8;
        importer.filterMode = FilterMode.Trilinear;
        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    public static float Fbm(float x, float y, int octaves)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Mathf.PerlinNoise(x, y) * amp;
            norm += amp;
            x = x * 2.03f + 17.17f;
            y = y * 2.03f + 31.31f;
            amp *= 0.5f;
        }
        return sum / norm;
    }

    /// <summary>Fbm that wraps perfectly on the 0..1 square (for tiling textures).</summary>
    public static float SeamlessFbm(float u, float v, float period, int octaves, float seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            float px = u * period + seed, py = v * period + seed * 1.7f;
            float a = Mathf.PerlinNoise(px, py);
            float b = Mathf.PerlinNoise(px - period, py);
            float c = Mathf.PerlinNoise(px, py - period);
            float d = Mathf.PerlinNoise(px - period, py - period);
            sum += Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v) * amp;
            norm += amp;
            period *= 2f;
            seed += 13.7f;
            amp *= 0.5f;
        }
        // Bilinear blending flattens contrast, stretch it back out.
        return Mathf.Clamp01(((sum / norm) - 0.5f) * 1.6f + 0.5f);
    }

    [System.Serializable]
    public struct MarbleStyle
    {
        public Color baseA, baseB, vein, fineVein;
        public float veinSharpness, fineVeinSharpness, fineVeinStrength, turbulence;
        public int veinFreqU, veinFreqV;
    }

    /// <summary>Marble colour at uv. veinMask returns 0..1 vein amount (used for smoothness/height).</summary>
    public static Color SampleMarble(float u, float v, MarbleStyle s, float seed, bool seamless, out float veinMask)
    {
        System.Func<float, float, float, int, float> noise = seamless
            ? (System.Func<float, float, float, int, float>)((x, y, p, o) => SeamlessFbm(x, y, p, o, seed + p))
            : (x, y, p, o) => Fbm(x * p + seed, y * p + seed * 1.3f, o);

        float cloud = noise(u, v, 3f, 5);
        float turb = (noise(u, v, 2f, 6) - 0.5f) * 2f;
        float turb2 = (noise(u, v, 5f, 5) - 0.5f) * 2f;
        float fade = Mathf.SmoothStep(0.3f, 0.7f, noise(u, v, 1.5f, 3));

        float t1 = Mathf.PI * 2f * (s.veinFreqU * u + s.veinFreqV * v) + turb * s.turbulence;
        float vein = Mathf.Pow(1f - Mathf.Abs(Mathf.Sin(t1)), s.veinSharpness) * (0.35f + 0.65f * fade);

        float t2 = Mathf.PI * 2f * (s.veinFreqV * 3 * u - s.veinFreqU * 2 * v) + turb2 * s.turbulence * 1.4f;
        float fine = Mathf.Pow(1f - Mathf.Abs(Mathf.Sin(t2)), s.fineVeinSharpness) * s.fineVeinStrength;

        Color c = Color.Lerp(s.baseA, s.baseB, Mathf.SmoothStep(0f, 1f, cloud));
        c = Color.Lerp(c, s.fineVein, Mathf.Clamp01(fine));
        c = Color.Lerp(c, s.vein, Mathf.Clamp01(vein));
        veinMask = Mathf.Clamp01(vein + fine * 0.5f);
        c.a = 1f;
        return c;
    }

    public static Color[] HeightToNormal(float[] height, int size, float strength)
    {
        Color[] result = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float l = height[y * size + (x - 1 + size) % size];
                float r = height[y * size + (x + 1) % size];
                float d = height[((y - 1 + size) % size) * size + x];
                float u = height[((y + 1) % size) * size + x];
                Vector3 n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                result[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        }
        return result;
    }

    public static Texture2D SaveSoftDotTexture(string path, int size = 64)
    {
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a * (3f - 2f * a);
                px[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
        }
        Texture2D tex = SaveTexture(px, size, path, false, true, size);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return tex;
    }

    // ---------------------------------------------------------------- Materials

    public static Material LitMaterial(string path, Color baseColor, Texture2D baseMap = null,
        Texture2D normalMap = null, float normalScale = 1f, Texture2D metallicGlossMap = null,
        float metallic = 0f, float smoothness = 0.5f, Color? emission = null)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", baseColor);
        if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);

        if (normalMap != null)
        {
            mat.SetTexture("_BumpMap", normalMap);
            mat.SetFloat("_BumpScale", normalScale);
            mat.EnableKeyword("_NORMALMAP");
        }

        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (metallicGlossMap != null)
        {
            mat.SetTexture("_MetallicGlossMap", metallicGlossMap);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (emission.HasValue)
        {
            mat.SetColor("_EmissionColor", emission.Value);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        mat.enableInstancing = true;
        return SaveAsset(mat, path);
    }

    public static Material AdditiveParticleMaterial(string path, Texture2D texture)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f); // Additive
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.One);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        return SaveAsset(mat, path);
    }

    // ---------------------------------------------------------------- Meshes

    /// <summary>Cylinder / cone frustum with smooth sides. UVs are in world units / uvScale so textures tile evenly.</summary>
    public static Mesh Cylinder(string name, float rBottom, float rTop, float height, int segments, float uvScale, bool capTop = true, bool capBottom = false)
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        float slope = (rBottom - rTop) / height;
        float circumference = Mathf.PI * (rBottom + rTop);
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            Vector3 n = new Vector3(cos, slope, sin).normalized;
            float u = i / (float)segments * circumference / uvScale;
            verts.Add(new Vector3(cos * rBottom, 0f, sin * rBottom)); normals.Add(n); uvs.Add(new Vector2(u, 0f));
            verts.Add(new Vector3(cos * rTop, height, sin * rTop)); normals.Add(n); uvs.Add(new Vector2(u, height / uvScale));
        }
        for (int i = 0; i < segments; i++)
        {
            int b0 = i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
            tris.Add(b0); tris.Add(t0); tris.Add(b1);
            tris.Add(b1); tris.Add(t0); tris.Add(t1);
        }

        if (capTop) AddCap(verts, normals, uvs, tris, rTop, height, segments, uvScale, true);
        if (capBottom) AddCap(verts, normals, uvs, tris, rBottom, 0f, segments, uvScale, false);

        return BuildMesh(name, verts, normals, uvs, tris);
    }

    /// <summary>Flat ring (annulus) with height: top face plus inner and outer walls (and optional underside).</summary>
    public static Mesh Ring(string name, float rInner, float rOuter, float height, int segments, float uvScale, bool bottom = false)
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        // Top
        int start = verts.Count;
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            Vector3 pi = new Vector3(cos * rInner, height, sin * rInner);
            Vector3 po = new Vector3(cos * rOuter, height, sin * rOuter);
            verts.Add(pi); normals.Add(Vector3.up); uvs.Add(new Vector2(pi.x, pi.z) / uvScale);
            verts.Add(po); normals.Add(Vector3.up); uvs.Add(new Vector2(po.x, po.z) / uvScale);
        }
        for (int i = 0; i < segments; i++)
        {
            int in0 = start + i * 2, out0 = in0 + 1, in1 = in0 + 2, out1 = in0 + 3;
            tris.Add(in0); tris.Add(in1); tris.Add(out0);
            tris.Add(in1); tris.Add(out1); tris.Add(out0);
        }

        if (bottom)
        {
            start = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                Vector3 pi = new Vector3(cos * rInner, 0f, sin * rInner);
                Vector3 po = new Vector3(cos * rOuter, 0f, sin * rOuter);
                verts.Add(pi); normals.Add(Vector3.down); uvs.Add(new Vector2(pi.x, pi.z) / uvScale);
                verts.Add(po); normals.Add(Vector3.down); uvs.Add(new Vector2(po.x, po.z) / uvScale);
            }
            for (int i = 0; i < segments; i++)
            {
                int in0 = start + i * 2, out0 = in0 + 1, in1 = in0 + 2, out1 = in0 + 3;
                tris.Add(in0); tris.Add(out0); tris.Add(in1);
                tris.Add(in1); tris.Add(out0); tris.Add(out1);
            }
        }

        AddWall(verts, normals, uvs, tris, rOuter, height, segments, uvScale, true);
        AddWall(verts, normals, uvs, tris, rInner, height, segments, uvScale, false);

        return BuildMesh(name, verts, normals, uvs, tris);
    }

    /// <summary>Classical fluted column shaft with a gentle taper.</summary>
    public static Mesh FlutedShaft(string name, float rBottom, float rTop, float height, int flutes, float fluteDepth, float uvScale)
    {
        const int perFlute = 6;
        int segments = flutes * perFlute;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            float frac = (i % perFlute) / (float)perFlute;
            float k = 1f - fluteDepth * Mathf.Sin(frac * Mathf.PI);
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            float u = i / (float)segments * Mathf.PI * (rBottom + rTop) / uvScale;
            verts.Add(new Vector3(cos * rBottom * k, 0f, sin * rBottom * k)); uvs.Add(new Vector2(u, 0f));
            verts.Add(new Vector3(cos * rTop * k, height, sin * rTop * k)); uvs.Add(new Vector2(u, height / uvScale));
        }
        for (int i = 0; i < segments; i++)
        {
            int b0 = i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
            tris.Add(b0); tris.Add(t0); tris.Add(b1);
            tris.Add(b1); tris.Add(t0); tris.Add(t1);
        }

        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Mesh Box(string name, Vector3 size, float uvScale)
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        Vector3 h = size * 0.5f;

        void Face(Vector3 n, Vector3 right, Vector3 up, float w, float hgt)
        {
            int s = verts.Count;
            Vector3 c = Vector3.Scale(n, h);
            verts.Add(c - right * w * 0.5f - up * hgt * 0.5f);
            verts.Add(c - right * w * 0.5f + up * hgt * 0.5f);
            verts.Add(c + right * w * 0.5f + up * hgt * 0.5f);
            verts.Add(c + right * w * 0.5f - up * hgt * 0.5f);
            for (int i = 0; i < 4; i++) normals.Add(n);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, hgt / uvScale));
            uvs.Add(new Vector2(w / uvScale, hgt / uvScale)); uvs.Add(new Vector2(w / uvScale, 0));
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
        }

        Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
        Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
        Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
        Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
        Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
        Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);

        return BuildMesh(name, verts, normals, uvs, tris);
    }

    public static Mesh SaveMesh(Mesh mesh, string folder)
    {
        return SaveAsset(mesh, folder + "/" + mesh.name + ".asset");
    }

    private static void AddCap(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris,
        float radius, float y, int segments, float uvScale, bool top)
    {
        int center = verts.Count;
        Vector3 n = top ? Vector3.up : Vector3.down;
        verts.Add(new Vector3(0f, y, 0f)); normals.Add(n); uvs.Add(Vector2.zero);
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
            verts.Add(p); normals.Add(n); uvs.Add(new Vector2(p.x, p.z) / uvScale);
        }
        for (int i = 0; i < segments; i++)
        {
            int p0 = center + 1 + i, p1 = center + 1 + (i + 1) % segments;
            if (top) { tris.Add(center); tris.Add(p1); tris.Add(p0); }
            else { tris.Add(center); tris.Add(p0); tris.Add(p1); }
        }
    }

    private static void AddWall(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris,
        float radius, float height, int segments, float uvScale, bool outward)
    {
        int start = verts.Count;
        float circumference = Mathf.PI * 2f * radius;
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 n = outward ? dir : -dir;
            float u = i / (float)segments * circumference / uvScale;
            verts.Add(dir * radius); normals.Add(n); uvs.Add(new Vector2(u, 0f));
            verts.Add(dir * radius + Vector3.up * height); normals.Add(n); uvs.Add(new Vector2(u, height / uvScale));
        }
        for (int i = 0; i < segments; i++)
        {
            int b0 = start + i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
            if (outward)
            {
                tris.Add(b0); tris.Add(t0); tris.Add(b1);
                tris.Add(b1); tris.Add(t0); tris.Add(t1);
            }
            else
            {
                tris.Add(b0); tris.Add(b1); tris.Add(t0);
                tris.Add(b1); tris.Add(t1); tris.Add(t0);
            }
        }
    }

    private static Mesh BuildMesh(string name, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris)
    {
        Mesh mesh = new Mesh { name = name };
        if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------------------------------------------------------- Scene objects

    public static GameObject Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos,
        Quaternion? rot = null, bool castShadows = true)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
    }
}
