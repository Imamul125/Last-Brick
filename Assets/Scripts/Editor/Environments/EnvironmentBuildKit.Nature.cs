using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Organic geometry helpers: low-poly blobs, rocks, terrain, mountain ranges and mesh batching.
/// </summary>
public static partial class EnvironmentBuildKit
{
    /// <summary>Collects mesh instances and merges them into one mesh (one draw call per material).</summary>
    public class MeshBatch
    {
        private readonly List<CombineInstance> items = new List<CombineInstance>();

        public void Add(Mesh mesh, Matrix4x4 matrix)
        {
            items.Add(new CombineInstance { mesh = mesh, transform = matrix });
        }

        public void Add(Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            Add(mesh, Matrix4x4.TRS(pos, rot, scale));
        }

        /// <summary>Adds a tapered limb (unit cylinder along +Y) stretched from a to b.</summary>
        public void AddLimb(Mesh unitCylinder, Vector3 a, Vector3 b, float radius)
        {
            Vector3 d = b - a;
            Add(unitCylinder, a, Quaternion.FromToRotation(Vector3.up, d.normalized), new Vector3(radius, d.magnitude, radius));
        }

        public Mesh Build(string name)
        {
            Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(items.ToArray(), true, true);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Re-builds a mesh with unshared vertices and per-face normals (crisp low-poly look).</summary>
    public static Mesh FlatShade(Mesh source, float uvScale = 2f)
    {
        Vector3[] v = source.vertices;
        int[] t = source.triangles;
        var verts = new Vector3[t.Length];
        var normals = new Vector3[t.Length];
        var uvs = new Vector2[t.Length];
        var tris = new int[t.Length];
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            Vector3 an = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            for (int k = 0; k < 3; k++)
            {
                Vector3 p = k == 0 ? a : k == 1 ? b : c;
                verts[i + k] = p;
                normals[i + k] = n;
                // Tri-planar style projection so textures don't stretch.
                uvs[i + k] = (an.y > an.x && an.y > an.z ? new Vector2(p.x, p.z) : an.x > an.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y)) / uvScale;
                tris[i + k] = i + k;
            }
        }
        Mesh mesh = new Mesh { name = source.name };
        if (verts.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Noise-displaced icosphere. scale stretches it before shading so normals stay correct (good for rocks).</summary>
    public static Mesh Blob(string name, int subdivisions, float roughness, int seed, Vector3 scale)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var verts = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
        var tris = new List<int>
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;

        for (int s = 0; s < subdivisions; s++)
        {
            var cache = new Dictionary<long, int>();
            var next = new List<int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int idx)) return idx;
                verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
                cache[key] = verts.Count - 1;
                return verts.Count - 1;
            }
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            tris = next;
        }

        float o = seed * 7.31f;
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 p = verts[i];
            float n = (Mathf.PerlinNoise(p.x * 1.3f + o, p.y * 1.3f + o) +
                       Mathf.PerlinNoise(p.y * 1.3f + o * 2f, p.z * 1.3f) +
                       Mathf.PerlinNoise(p.z * 1.3f, p.x * 1.3f + o * 3f)) / 3f;
            verts[i] = Vector3.Scale(p * (1f + (n - 0.5f) * 2f * roughness), scale);
        }

        // Make sure every face points outward.
        for (int i = 0; i < tris.Count; i += 3)
        {
            Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f)
            {
                int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp;
            }
        }

        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        return FlatShade(mesh);
    }

    /// <summary>
    /// Polar grid between two radii, displaced by height(r, angle). Used for terrain, gravel rings and mountain ranges.
    /// radialPower > 1 packs rings near the inner edge (more detail close to the camera).
    /// polarUV: u wraps around the circle (uRepeats times), v runs outward in vUnit world units.
    /// </summary>
    public static Mesh PolarGrid(string name, float rInner, float rOuter, int segments, int rings,
        System.Func<float, float, float> height, float radialPower = 1f,
        bool polarUV = false, float uvScale = 4f, float uRepeats = 1f, float vUnit = 1f)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        int stride = segments + 1;

        for (int r = 0; r <= rings; r++)
        {
            float tr = Mathf.Pow(r / (float)rings, radialPower);
            float radius = Mathf.Lerp(rInner, rOuter, tr);
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, height(radius, a), Mathf.Sin(a) * radius);
                verts.Add(p);
                uvs.Add(polarUV ? new Vector2(s / (float)segments * uRepeats, (radius - rInner) / vUnit) : new Vector2(p.x, p.z) / uvScale);
            }
        }
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int i0 = r * stride + s, i1 = i0 + 1, o0 = i0 + stride, o1 = o0 + 1;
                tris.Add(i0); tris.Add(i1); tris.Add(o0);
                tris.Add(i1); tris.Add(o1); tris.Add(o0);
            }
        }

        Mesh mesh = new Mesh { name = name };
        if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();

        // Weld the normals across the angular seam so there is no lighting crease.
        Vector3[] normals = mesh.normals;
        for (int r = 0; r <= rings; r++)
        {
            int first = r * stride, last = first + segments;
            Vector3 n = (normals[first] + normals[last]).normalized;
            normals[first] = n;
            normals[last] = n;
        }
        mesh.normals = normals;
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Noise that wraps around the circle (angle in radians).</summary>
    public static float CircularNoise(float angle, float frequency, float seed)
    {
        return Mathf.PerlinNoise(Mathf.Cos(angle) * frequency + seed, Mathf.Sin(angle) * frequency + seed * 0.37f);
    }

    /// <summary>Height function for a ring of mountains: jagged ridge line, peak in the middle of the band.</summary>
    public static System.Func<float, float, float> MountainRange(float rInner, float rOuter, float baseY, float minHeight, float maxHeight, float seed)
    {
        return (r, a) =>
        {
            float t = Mathf.InverseLerp(rInner, rOuter, r);
            float profile = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 1.3f);
            float ridge = 0f, amp = 1f, freq = 2.2f, norm = 0f;
            for (int o = 0; o < 4; o++)
            {
                float n = CircularNoise(a, freq, seed + o * 11.1f + t * 0.6f);
                ridge += (1f - Mathf.Abs(n * 2f - 1f)) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.1f;
            }
            ridge /= norm;
            float h = Mathf.Lerp(minHeight, maxHeight, ridge * ridge);
            return baseY - 3f + profile * h;
        };
    }

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
