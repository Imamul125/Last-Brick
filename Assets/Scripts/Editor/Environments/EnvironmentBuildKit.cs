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

    public static float Fbm(float x, float y, int octaves) => ProceduralMesh.Fbm(x, y, octaves);

    public static float SeamlessFbm(float u, float v, float period, int octaves, float seed) =>
        ProceduralMesh.SeamlessFbm(u, v, period, octaves, seed);

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

    public static Mesh Cylinder(string name, float rBottom, float rTop, float height, int segments, float uvScale, bool capTop = true, bool capBottom = false) =>
        ProceduralMesh.Cylinder(name, rBottom, rTop, height, segments, uvScale, capTop, capBottom);

    public static Mesh Ring(string name, float rInner, float rOuter, float height, int segments, float uvScale, bool bottom = false) =>
        ProceduralMesh.Ring(name, rInner, rOuter, height, segments, uvScale, bottom);

    public static Mesh FlutedShaft(string name, float rBottom, float rTop, float height, int flutes, float fluteDepth, float uvScale) =>
        ProceduralMesh.FlutedShaft(name, rBottom, rTop, height, flutes, fluteDepth, uvScale);

    public static Mesh Box(string name, Vector3 size, float uvScale) => ProceduralMesh.Box(name, size, uvScale);

    public static Mesh SaveMesh(Mesh mesh, string folder)
    {
        return SaveAsset(mesh, folder + "/" + mesh.name + ".asset");
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
