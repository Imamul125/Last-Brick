using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>Collects mesh instances and merges them into one mesh (one draw call per material).</summary>
public class MeshBatch
{
    private readonly List<CombineInstance> items = new List<CombineInstance>();
    public int Count => items.Count;

    public void Add(Mesh mesh, Matrix4x4 matrix)
    {
        if (mesh != null) items.Add(new CombineInstance { mesh = mesh, transform = matrix });
    }

    public void Add(Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Add(mesh, Matrix4x4.TRS(pos, rot, scale));
    }

    /// <summary>Adds a tapered limb (unit cylinder along +Y) stretched from a to b.</summary>
    public void AddLimb(Mesh unitCylinder, Vector3 a, Vector3 b, float radius)
    {
        Vector3 d = b - a;
        if (d.sqrMagnitude < 1e-6f) return;
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

/// <summary>
/// Procedural geometry and noise shared by the editor builders and the runtime environment generator.
/// </summary>
public static class ProceduralMesh
{
    // ---------------------------------------------------------------- Noise

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

    /// <summary>Noise that wraps around the circle (angle in radians).</summary>
    public static float CircularNoise(float angle, float frequency, float seed)
    {
        return Mathf.PerlinNoise(Mathf.Cos(angle) * frequency + seed, Mathf.Sin(angle) * frequency + seed * 0.37f);
    }

    // ---------------------------------------------------------------- Primitives

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

        if (capTop && rTop > 0f) AddCap(verts, normals, uvs, tris, rTop, height, segments, uvScale, true);
        if (capBottom && rBottom > 0f) AddCap(verts, normals, uvs, tris, rBottom, 0f, segments, uvScale, false);

        return BuildMesh(name, verts, normals, uvs, tris);
    }

    /// <summary>Flat ring (annulus) with height: top face plus inner and outer walls (and optional underside).</summary>
    public static Mesh Ring(string name, float rInner, float rOuter, float height, int segments, float uvScale, bool bottom = false)
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        AddAnnulus(verts, normals, uvs, tris, rInner, rOuter, height, segments, uvScale, true);
        if (bottom) AddAnnulus(verts, normals, uvs, tris, rInner, rOuter, 0f, segments, uvScale, false);
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

    // ---------------------------------------------------------------- Organic shapes

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

    /// <summary>Noise-displaced icosphere. scale stretches it before shading so normals stay correct.</summary>
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

    // ---------------------------------------------------------------- Landscape

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
                float y = height(radius, a);
                if (float.IsNaN(y) || float.IsInfinity(y)) y = 0f;
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
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

    /// <summary>Height function for a ring of mountains: jagged ridge line, peak in the middle of the band.</summary>
    public static System.Func<float, float, float> MountainRange(float rInner, float rOuter, float baseY, float minHeight, float maxHeight, float seed)
    {
        return (r, a) =>
        {
            float t = Mathf.InverseLerp(rInner, rOuter, r);
            // Clamp: sin(PI) is a tiny negative number and Pow(negative, 1.3) is NaN.
            float profile = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 1.3f);
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

    /// <summary>Writes per-vertex colours (used by the vertex-colour shader for mountains, clouds etc.).</summary>
    public static void Colorize(Mesh mesh, System.Func<Vector3, Color> color)
    {
        Vector3[] v = mesh.vertices;
        var c = new Color[v.Length];
        for (int i = 0; i < v.Length; i++) c[i] = color(v[i]);
        mesh.colors = c;
    }

    // ---------------------------------------------------------------- Internals

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

    private static void AddAnnulus(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris,
        float rInner, float rOuter, float y, int segments, float uvScale, bool up)
    {
        int start = verts.Count;
        Vector3 n = up ? Vector3.up : Vector3.down;
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            Vector3 pi = new Vector3(cos * rInner, y, sin * rInner);
            Vector3 po = new Vector3(cos * rOuter, y, sin * rOuter);
            verts.Add(pi); normals.Add(n); uvs.Add(new Vector2(pi.x, pi.z) / uvScale);
            verts.Add(po); normals.Add(n); uvs.Add(new Vector2(po.x, po.z) / uvScale);
        }
        for (int i = 0; i < segments; i++)
        {
            int in0 = start + i * 2, out0 = in0 + 1, in1 = in0 + 2, out1 = in0 + 3;
            if (up)
            {
                tris.Add(in0); tris.Add(in1); tris.Add(out0);
                tris.Add(in1); tris.Add(out1); tris.Add(out0);
            }
            else
            {
                tris.Add(in0); tris.Add(out0); tris.Add(in1);
                tris.Add(in1); tris.Add(out0); tris.Add(out1);
            }
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
}
