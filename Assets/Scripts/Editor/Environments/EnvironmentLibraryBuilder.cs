using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using System.Collections.Generic;
using K = EnvironmentBuildKit;
using PM = ProceduralMesh;

/// <summary>
/// Generates the shared EnvironmentLibrary: neutral (tintable) 512px surface textures, prop meshes and material
/// templates. All 100 recipe environments share these, so the whole set adds only a few MB to the build.
/// </summary>
public static class EnvironmentLibraryBuilder
{
    public const string Folder = K.RootFolder + "/Shared";
    public const string LibraryPath = Folder + "/EnvironmentLibrary.asset";
    private const int Size = 512;

    public static EnvironmentLibrary Build()
    {
        K.EnsureFolder(Folder + "/Textures");
        K.EnsureFolder(Folder + "/Materials");
        K.EnsureFolder(Folder + "/Meshes");

        EnvironmentLibrary lib = AssetDatabase.LoadAssetAtPath<EnvironmentLibrary>(LibraryPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<EnvironmentLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
        }

        BuildTextures(lib);
        BuildTemplates(lib);
        BuildMeshes(lib);

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        return lib;
    }

    // ================================================================ Textures

    private static void BuildTextures(EnvironmentLibrary lib)
    {
        string t = Folder + "/Textures/";
        var sets = new List<SurfaceTextureSet>();

        Progress("Stone tiles", 0.02f);
        sets.Add(new SurfaceTextureSet
        {
            type = SurfaceTexture.StoneTiles,
            albedo = K.SaveProceduralTexture(t + "T_StoneTiles.png", Size, StoneTilesColor),
            normal = K.SaveProceduralNormal(t + "T_StoneTiles_N.png", Size, (u, v) => JointHeight(u, v, 3, 0.012f), 5f)
        });

        Progress("Marble tiles", 0.06f);
        sets.Add(new SurfaceTextureSet
        {
            type = SurfaceTexture.MarbleTiles,
            albedo = K.SaveProceduralTexture(t + "T_MarbleTiles.png", Size, MarbleTilesColor),
            normal = K.SaveProceduralNormal(t + "T_MarbleTiles_N.png", Size, (u, v) => JointHeight(u, v, 2, 0.006f), 4f)
        });

        Progress("Flagstones", 0.1f);
        sets.Add(new SurfaceTextureSet
        {
            type = SurfaceTexture.Flagstones,
            albedo = K.SaveProceduralTexture(t + "T_Flagstones.png", Size, FlagstoneColor),
            normal = K.SaveProceduralNormal(t + "T_Flagstones_N.png", Size, FlagstoneHeight, 6f)
        });

        Progress("Wood planks", 0.14f);
        sets.Add(new SurfaceTextureSet
        {
            type = SurfaceTexture.Planks,
            albedo = K.SaveProceduralTexture(t + "T_Planks.png", Size, PlankColor),
            normal = K.SaveProceduralNormal(t + "T_Planks_N.png", Size, PlankHeight, 5f)
        });

        Progress("Raked gravel", 0.18f);
        sets.Add(new SurfaceTextureSet
        {
            type = SurfaceTexture.RakedGravel,
            polarUV = true,
            albedo = Soften(K.SaveProceduralTexture(t + "T_RakedGravel.png", Size, (u, v) => Gray(Mathf.Lerp(0.8f, 0.93f, GravelHeight(u, v)) * Speck(u, v, 0.94f)))),
            normal = Soften(K.SaveProceduralNormal(t + "T_RakedGravel_N.png", Size, GravelHeight, 2.5f))
        });

        Progress("Sand ripples", 0.22f);
        sets.Add(new SurfaceTextureSet
        {
            type = SurfaceTexture.SandRipples,
            polarUV = true,
            albedo = Soften(K.SaveProceduralTexture(t + "T_SandRipples.png", Size, (u, v) => Gray(Mathf.Lerp(0.86f, 0.96f, SandHeight(u, v)) * Speck(u, v, 0.97f)))),
            normal = Soften(K.SaveProceduralNormal(t + "T_SandRipples_N.png", Size, SandHeight, 2f))
        });

        Progress("Organic ground", 0.26f);
        sets.Add(new SurfaceTextureSet
        {
            type = SurfaceTexture.Noise,
            albedo = K.SaveProceduralTexture(t + "T_Ground.png", Size, (u, v) =>
            {
                float patch = PM.SeamlessFbm(u, v, 4f, 4, 2.2f);
                float fine = PM.SeamlessFbm(u, v, 32f, 2, 6.6f);
                return Gray(0.78f + (patch - 0.5f) * 0.35f + (fine - 0.5f) * 0.25f);
            })
        });

        Progress("Rock", 0.3f);
        sets.Add(new SurfaceTextureSet
        {
            type = SurfaceTexture.Rock,
            albedo = K.SaveProceduralTexture(t + "T_Rock.png", Size, (u, v) => Gray(Mathf.Lerp(0.6f, 0.92f, RockHeight(u, v)) * Speck(u, v, 0.9f))),
            normal = K.SaveProceduralNormal(t + "T_Rock_N.png", Size, RockHeight, 8f)
        });

        Progress("Lava cracks", 0.34f);
        sets.Add(new SurfaceTextureSet
        {
            type = SurfaceTexture.LavaCracks,
            albedo = K.SaveProceduralTexture(t + "T_Basalt.png", Size, (u, v) => Gray(Mathf.Lerp(0.55f, 0.85f, RockHeight(u, v)) * (1f - CrackMask(u, v) * 0.5f))),
            normal = K.SaveProceduralNormal(t + "T_Basalt_N.png", Size, (u, v) => RockHeight(u, v) * 0.5f + (1f - CrackMask(u, v)) * 0.5f, 7f),
            emission = K.SaveProceduralTexture(t + "T_LavaCracks_E.png", Size, (u, v) => Gray(CrackMask(u, v)))
        });

        Progress("Water", 0.38f);
        sets.Add(new SurfaceTextureSet
        {
            type = SurfaceTexture.Water,
            normal = K.SaveProceduralNormal(t + "T_Water_N.png", Size, (u, v) =>
                PM.SeamlessFbm(u, v, 6f, 3, 1.1f) * 0.6f + PM.SeamlessFbm(u, v, 18f, 2, 7.7f) * 0.4f, 3f)
        });

        Progress("Snow", 0.42f);
        sets.Add(new SurfaceTextureSet
        {
            type = SurfaceTexture.Snow,
            albedo = K.SaveProceduralTexture(t + "T_Snow.png", Size, (u, v) => Gray(0.9f + (PM.SeamlessFbm(u, v, 6f, 3, 4.2f) - 0.5f) * 0.12f)),
            normal = K.SaveProceduralNormal(t + "T_Snow_N.png", Size, (u, v) => PM.SeamlessFbm(u, v, 5f, 4, 9.3f), 2.5f)
        });

        lib.surfaces = sets.ToArray();
        lib.softDot = K.SaveSoftDotTexture(t + "T_SoftDot.png");
        lib.petal = K.SavePetalTexture(t + "T_Petal.png");
    }

    /// <summary>Blurs distant mip levels slightly so fine line patterns don't shimmer or look jagged.</summary>
    private static Texture2D Soften(Texture2D tex)
    {
        string path = AssetDatabase.GetAssetPath(tex);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.mipMapBias = 0.5f;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Color Gray(float g)
    {
        g = Mathf.Clamp01(g);
        return new Color(g, g, g, 1f);
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    private static float Speck(float u, float v, float dark)
    {
        float s = PM.SeamlessFbm(u, v, 96f, 1, 4.4f);
        return s > 0.62f ? dark : 1f;
    }

    private static float JointDistance(float u, float v, int tiles)
    {
        float tu = u * tiles, tv = v * tiles;
        float du = Mathf.Abs(tu - Mathf.Round(tu)), dv = Mathf.Abs(tv - Mathf.Round(tv));
        return Mathf.Min(du, dv) / tiles;
    }

    private static float JointHeight(float u, float v, int tiles, float halfWidth)
    {
        float d = JointDistance(u, v, tiles);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfWidth * 0.6f, halfWidth * 1.8f, d));
    }

    private static Color StoneTilesColor(float u, float v)
    {
        int tile = Mathf.FloorToInt(u * 3) + Mathf.FloorToInt(v * 3) * 7;
        float n = PM.SeamlessFbm(u, v, 8f, 4, 1.7f);
        float g = Mathf.Lerp(0.66f, 0.86f, n) * Speck(u, v, 0.8f) * (0.92f + 0.1f * Hash(tile, 1, 3));
        return JointDistance(u, v, 3) < 0.012f ? Gray(0.38f) : Gray(g);
    }

    private static readonly K.MarbleStyle GreyMarble = new K.MarbleStyle
    {
        baseA = new Color(0.97f, 0.97f, 0.97f), baseB = new Color(0.9f, 0.9f, 0.9f),
        vein = new Color(0.58f, 0.58f, 0.6f), fineVein = new Color(0.78f, 0.78f, 0.79f),
        veinSharpness = 14f, fineVeinSharpness = 40f, fineVeinStrength = 0.6f, turbulence = 6f,
        veinFreqU = 1, veinFreqV = 1
    };

    private static Color MarbleTilesColor(float u, float v)
    {
        if (JointDistance(u, v, 2) < 0.006f) return Gray(0.55f);
        int tx = Mathf.FloorToInt(u * 2), ty = Mathf.FloorToInt(v * 2);
        K.MarbleStyle style = GreyMarble;
        if ((tx + ty) % 2 == 1) style.veinFreqV = -1;
        Color c = K.SampleMarble(u * 2 - tx, v * 2 - ty, style, 11.3f + tx * 37.1f + ty * 91.7f, false, out _);
        float g = c.grayscale;
        return Gray(g);
    }

    // Seamless jittered Voronoi (6x6 cells) for flagstones and lava cracks.
    private static void Voronoi(float u, float v, int cells, int seed, out float f1, out float f2, out int id)
    {
        float x = u * cells, y = v * cells;
        int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
        f1 = f2 = 10f;
        id = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = cx + dx, ny = cy + dy;
                int wx = (nx % cells + cells) % cells, wy = (ny % cells + cells) % cells;
                float px = nx + 0.15f + 0.7f * Hash(wx, wy, seed);
                float py = ny + 0.15f + 0.7f * Hash(wx, wy, seed + 1);
                float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < f1) { f2 = f1; f1 = d; id = wx + wy * cells; }
                else if (d < f2) f2 = d;
            }
        }
    }

    private static float FlagstoneHeight(float u, float v)
    {
        Voronoi(u, v, 6, 5, out float f1, out float f2, out _);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.12f, f2 - f1)) * 0.85f + PM.SeamlessFbm(u, v, 16f, 2, 3.3f) * 0.15f;
    }

    private static Color FlagstoneColor(float u, float v)
    {
        Voronoi(u, v, 6, 5, out float f1, out float f2, out int id);
        if (f2 - f1 < 0.04f) return Gray(0.4f);
        float n = PM.SeamlessFbm(u, v, 10f, 3, 8.8f);
        return Gray((0.68f + 0.18f * Hash(id, 7, 9)) * Mathf.Lerp(0.88f, 1.06f, n) * Speck(u, v, 0.86f));
    }

    private static float CrackMask(float u, float v)
    {
        Voronoi(u, v, 6, 21, out float f1, out float f2, out _);
        float wobble = (PM.SeamlessFbm(u, v, 12f, 2, 1.9f) - 0.5f) * 0.05f;
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.015f, 0.07f, f2 - f1 + wobble));
    }

    private static float RockHeight(float u, float v)
    {
        float n = PM.SeamlessFbm(u, v, 6f, 5, 12.1f);
        return 1f - Mathf.Abs(n * 2f - 1f);
    }

    private const int Boards = 6;

    private static float PlankHeight(float u, float v)
    {
        int b = Mathf.FloorToInt(v * Boards);
        float lv = v * Boards - b;
        float seam = Mathf.Min(lv, 1f - lv);
        float joint = Mathf.Abs(Mathf.Repeat(u - Hash(b, 3, 2) + 0.5f, 1f) - 0.5f);
        float h = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.01f, 0.05f, seam));
        h = Mathf.Min(h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.001f, 0.006f, joint)));
        return h;
    }

    private static Color PlankColor(float u, float v)
    {
        int b = Mathf.FloorToInt(v * Boards);
        float lv = v * Boards - b;
        float grain = 0.5f + 0.5f * Mathf.Sin(Mathf.PI * 2f * (lv * 3f + PM.SeamlessFbm(u, v, 4f, 3, b * 3.1f) * 2.5f));
        float tone = 0.72f + 0.22f * Hash(b, 9, 4);
        float g = tone * Mathf.Lerp(0.86f, 1.04f, grain) * Mathf.Lerp(0.55f, 1f, PlankHeight(u, v));
        return Gray(g);
    }

    // Few, wide, soft grooves: thin high-contrast lines alias badly on phone screens.
    private static float GravelHeight(float u, float v)
    {
        float grooves = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * 4f);
        return grooves * 0.8f + PM.SeamlessFbm(u, v, 24f, 2, 9.1f) * 0.2f;
    }

    private static float SandHeight(float u, float v)
    {
        float warp = PM.SeamlessFbm(u, v, 3f, 3, 5.5f);
        float ripple = 0.5f + 0.5f * Mathf.Sin(Mathf.PI * 2f * (v * 4f + warp * 1.2f));
        return ripple * 0.85f + PM.SeamlessFbm(u, v, 20f, 2, 2.7f) * 0.15f;
    }

    // ================================================================ Material templates

    private static void BuildTemplates(EnvironmentLibrary lib)
    {
        Progress("Material templates", 0.5f);
        string m = Folder + "/Materials/";
        lib.litPlain = K.LitMaterial(m + "TPL_Lit_Plain.mat", Color.white);
        lib.litTexture = K.LitMaterial(m + "TPL_Lit_Texture.mat", Color.white, lib.GetSurface(SurfaceTexture.Noise).albedo);
        lib.litTextureNormal = K.LitMaterial(m + "TPL_Lit_TextureNormal.mat", Color.white,
            lib.GetSurface(SurfaceTexture.StoneTiles).albedo, lib.GetSurface(SurfaceTexture.StoneTiles).normal);
        lib.litTextureNormalEmissive = K.LitMaterial(m + "TPL_Lit_TextureNormalEmissive.mat", Color.white,
            lib.GetSurface(SurfaceTexture.LavaCracks).albedo, lib.GetSurface(SurfaceTexture.LavaCracks).normal, 1f, null, 0f, 0.5f, Color.black);
        lib.litTextureNormalEmissive.SetTexture("_EmissionMap", lib.GetSurface(SurfaceTexture.LavaCracks).emission);
        lib.litEmissive = K.LitMaterial(m + "TPL_Lit_Emissive.mat", Color.white, null, null, 1f, null, 0f, 0.8f, Color.black);

        Material vc = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
        vc.SetColor("_BaseColor", Color.white);
        vc.SetFloat("_Surface", 0f);
        vc.enableInstancing = false;
        lib.vertexColorLit = K.SaveAsset(vc, m + "TPL_VertexColorLit.mat");

        lib.particleAdditive = K.AdditiveParticleMaterial(m + "TPL_ParticleAdditive.mat", lib.softDot);
        lib.particleAlpha = K.AlphaParticleMaterial(m + "TPL_ParticleAlpha.mat", lib.petal);

        Shader skyShader = Shader.Find("LastBrick/GradientSky");
        if (skyShader == null) Debug.LogError("[Environments] Shader 'LastBrick/GradientSky' not found - check Assets/Shaders.");
        else lib.sky = K.SaveAsset(new Material(skyShader), m + "TPL_GradientSky.mat");

        lib.postProcessTemplate = BuildPostTemplate(Folder + "/PP_Template.asset");
        EditorUtility.SetDirty(lib.litTextureNormalEmissive);
    }

    private static VolumeProfile BuildPostTemplate(string path)
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        else
        {
            foreach (VolumeComponent c in profile.components) Object.DestroyImmediate(c, true);
            profile.components.Clear();
        }
        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
        profile.Add<Bloom>(true).intensity.Override(0.7f);
        profile.Add<ColorAdjustments>(true).contrast.Override(10f);
        profile.Add<WhiteBalance>(true).temperature.Override(0f);
        profile.Add<Vignette>(true).intensity.Override(0.2f);
        foreach (VolumeComponent c in profile.components)
        {
            c.name = c.GetType().Name;
            AssetDatabase.AddObjectToAsset(c, profile);
        }
        EditorUtility.SetDirty(profile);
        return profile;
    }

    // ================================================================ Meshes

    private static void BuildMeshes(EnvironmentLibrary lib)
    {
        Progress("Prop meshes", 0.7f);
        string f = Folder + "/Meshes";
        lib.limb = K.SaveMesh(PM.Cylinder("Lib_Limb", 1f, 0.72f, 1f, 8, 2f, true, false), f);
        lib.cylinder = K.SaveMesh(PM.Cylinder("Lib_Cylinder", 1f, 1f, 1f, 16, 2f, true, true), f);
        lib.hexCylinder = K.SaveMesh(PM.FlatShade(PM.Cylinder("Lib_HexCylinder", 1f, 1f, 1f, 6, 2f, true, true)), f);
        lib.cone = K.SaveMesh(PM.FlatShade(PM.Cylinder("Lib_Cone", 1f, 0f, 1f, 8, 2f, false, true)), f);
        lib.hexCone = K.SaveMesh(PM.FlatShade(PM.Cylinder("Lib_HexCone", 1f, 0f, 1f, 6, 2f, false, true)), f);
        lib.pyramid = K.SaveMesh(PM.FlatShade(PM.Cylinder("Lib_Pyramid", 1f, 0f, 1f, 4, 2f, false, true)), f);
        lib.squareFrustum = K.SaveMesh(PM.FlatShade(PM.Cylinder("Lib_SquareFrustum", 1f, 0.7f, 1f, 4, 2f, true, true)), f);
        lib.box = K.SaveMesh(PM.Box("Lib_Box", Vector3.one, 2f), f);
        lib.tinyBlob = K.SaveMesh(PM.Blob("Lib_TinyBlob", 0, 0.15f, 5, Vector3.one), f);
        lib.cloudBlob = K.SaveMesh(PM.Blob("Lib_CloudBlob", 1, 0.1f, 6, Vector3.one), f);
        lib.flatCanopy = K.SaveMesh(PM.Blob("Lib_FlatCanopy", 1, 0.25f, 7, new Vector3(1.6f, 0.45f, 1.6f)), f);
        lib.frond = K.SaveMesh(PM.Blob("Lib_Frond", 1, 0.1f, 8, new Vector3(1.6f, 0.1f, 0.35f)), f);
        lib.mushroomCap = K.SaveMesh(PM.Blob("Lib_MushroomCap", 1, 0.08f, 9, new Vector3(1f, 0.45f, 1f)), f);
        lib.flutedShaft = K.SaveMesh(PM.FlutedShaft("Lib_FlutedShaft", 1f, 0.87f, 1f, 16, 0.06f, 3f), f);
        lib.canopyBlobs = new[]
        {
            K.SaveMesh(PM.Blob("Lib_Canopy_A", 1, 0.25f, 11, Vector3.one), f),
            K.SaveMesh(PM.Blob("Lib_Canopy_B", 1, 0.3f, 12, new Vector3(1.1f, 0.85f, 1.1f)), f),
            K.SaveMesh(PM.Blob("Lib_Canopy_C", 1, 0.28f, 13, new Vector3(0.95f, 0.8f, 1.05f)), f),
        };
        lib.pineTiers = new[]
        {
            K.SaveMesh(PM.FlatShade(PM.Cylinder("Lib_PineTier_0", 2.3f, 0f, 3.4f, 8, 2f, false, true)), f),
            K.SaveMesh(PM.FlatShade(PM.Cylinder("Lib_PineTier_1", 1.8f, 0f, 3.0f, 8, 2f, false, true)), f),
            K.SaveMesh(PM.FlatShade(PM.Cylinder("Lib_PineTier_2", 1.25f, 0f, 2.6f, 8, 2f, false, true)), f),
        };
        lib.rocks = new[]
        {
            K.SaveMesh(PM.Blob("Lib_Rock_A", 1, 0.35f, 1, new Vector3(1.6f, 0.8f, 1.1f)), f),
            K.SaveMesh(PM.Blob("Lib_Rock_B", 1, 0.4f, 2, new Vector3(1.0f, 1.1f, 0.9f)), f),
            K.SaveMesh(PM.Blob("Lib_Rock_C", 1, 0.3f, 3, new Vector3(1.4f, 0.6f, 1.3f)), f),
        };
    }

    private static void Progress(string msg, float p) =>
        EditorUtility.DisplayProgressBar("Environment Library", msg, p);
}
