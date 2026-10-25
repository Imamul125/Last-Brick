using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

/// <summary>
/// Editor helpers for organic environments (geometry itself lives in the runtime ProceduralMesh class).
/// </summary>
public static partial class EnvironmentBuildKit
{
    public static Mesh FlatShade(Mesh source, float uvScale = 2f) => ProceduralMesh.FlatShade(source, uvScale);

    public static Mesh Blob(string name, int subdivisions, float roughness, int seed, Vector3 scale) =>
        ProceduralMesh.Blob(name, subdivisions, roughness, seed, scale);

    public static Mesh PolarGrid(string name, float rInner, float rOuter, int segments, int rings,
        System.Func<float, float, float> height, float radialPower = 1f,
        bool polarUV = false, float uvScale = 4f, float uRepeats = 1f, float vUnit = 1f) =>
        ProceduralMesh.PolarGrid(name, rInner, rOuter, segments, rings, height, radialPower, polarUV, uvScale, uRepeats, vUnit);

    public static float CircularNoise(float angle, float frequency, float seed) => ProceduralMesh.CircularNoise(angle, frequency, seed);

    public static System.Func<float, float, float> MountainRange(float rInner, float rOuter, float baseY, float minHeight, float maxHeight, float seed) =>
        ProceduralMesh.MountainRange(rInner, rOuter, baseY, minHeight, maxHeight, seed);

    public static Material AlphaParticleMaterial(string path, Texture2D texture)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f); // Alpha
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", 0f); // Double sided petals
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        return SaveAsset(mat, path);
    }

    /// <summary>Soft petal shape (white, tint it with particle colour).</summary>
    public static Texture2D SavePetalTexture(string path, int size = 64)
    {
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float e = (u * u) / (0.5f * 0.5f) + (v * v) / (0.88f * 0.88f);
                float body = Mathf.Clamp01((1f - e) * 6f);
                float nd = Mathf.Sqrt(u * u + (v - 0.95f) * (v - 0.95f));
                float notch = Mathf.Clamp01((nd - 0.22f) * 12f);
                float a = body * notch;
                float shade = Mathf.Lerp(0.82f, 1f, (v + 1f) * 0.5f);
                px[y * size + x] = new Color(shade, shade, shade, a);
            }
        }
        Texture2D tex = SaveTexture(px, size, path, false, true, size);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return tex;
    }

    /// <summary>Seamless texture from a colour function of (u, v) on 0..1.</summary>
    public static Texture2D SaveProceduralTexture(string path, int size, System.Func<float, float, Color> color, bool sRGB = true)
    {
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = color((x + 0.5f) / size, (y + 0.5f) / size);
        return SaveTexture(px, size, path, false, sRGB, size);
    }

    public static Texture2D SaveProceduralNormal(string path, int size, System.Func<float, float, float> height, float strength)
    {
        float[] h = new float[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                h[y * size + x] = height((x + 0.5f) / size, (y + 0.5f) / size);
        return SaveTexture(HeightToNormal(h, size, strength), size, path, true, false, size);
    }
}
