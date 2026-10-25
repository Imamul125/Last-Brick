using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

/// <summary>
/// Builds an environment from an EnvironmentRecipe at runtime.
/// Mobile budget: everything is merged per material (~25-40 draw calls total), only props near the tower cast
/// shadows, distant mountains use a cheap vertex-colour shader, and 4 point lights max.
/// </summary>
public static class EnvironmentGenerator
{
    public const float PlazaRadius = 13.5f;
    public const float RingOuter = 29.6f;
    public const float GrassInner = 30.2f;
    public const float WorldRadius = 620f;
    private const float ShadowRadius = 60f;
    private const int MaxPointLights = 4;

    private class Context
    {
        public EnvironmentRecipe r;
        public EnvironmentLibrary lib;
        public float groundY;
        public System.Random rng;
        public Transform root;
        public GeneratedEnvironment env;
        public readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        public readonly Dictionary<Material, MeshBatch> near = new Dictionary<Material, MeshBatch>();
        public readonly Dictionary<Material, MeshBatch> far = new Dictionary<Material, MeshBatch>();
        public readonly List<float> gateAngles = new List<float>();
        public int lights;

        public float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        public bool Chance(float p) => rng.NextDouble() < p;
    }

    // ================================================================ Entry points

    public static GeneratedEnvironment Build(EnvironmentProfile profile, EnvironmentLibrary lib, float groundY)
    {
        EnvironmentRecipe recipe = profile.recipe;
        GameObject rootGo = new GameObject("ENV_" + profile.name);
        var c = new Context
        {
            r = recipe,
            lib = lib,
            groundY = groundY,
            rng = new System.Random(recipe.seed),
            root = rootGo.transform,
            env = rootGo.AddComponent<GeneratedEnvironment>()
        };

        Light sun = BuildSun(c);
        BuildGround(c);
        BuildMountains(c);
        BuildStructures(c);
        BuildProps(c);
        FlushBatches(c);
        BuildParticles(c);
        BuildReflectionProbe(c);

        c.env.skybox = BuildSky(c, sun);
        c.env.volume = BuildVolume(c);

        if (!Application.isPlaying)
        {
            foreach (Object o in c.env.owned) if (o != null) o.hideFlags = HideFlags.DontSave;
        }
        return c.env;
    }

    /// <summary>Fog and ambient light derived from the recipe sky so the horizon always blends.</summary>
    public static void ApplyRenderSettings(EnvironmentRecipe r)
    {
        float k = r.ambientIntensity;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(r.skyTop, r.skyHorizon, 0.35f) * k;
        RenderSettings.ambientEquatorColor = r.skyHorizon * (0.8f * k);
        RenderSettings.ambientGroundColor = Color.Lerp(r.skyBottom, r.terrain.color, 0.5f) * (0.55f * k);
        RenderSettings.reflectionIntensity = 1f;

        RenderSettings.fog = r.fogDensity > 0f;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.Lerp(r.skyHorizon, r.skyBottom, 0.12f);
        RenderSettings.fogDensity = r.fogDensity;
    }

    // ================================================================ Materials

    private static Material NewMaterial(Context c, string key, Material template, Color color, float smoothness,
        float metallic = 0f, Texture tex = null, Texture normal = null, Color? emission = null, Texture emissionMap = null)
    {
        if (c.materials.TryGetValue(key, out Material cached)) return cached;
        Material m = new Material(template) { name = key };
        m.SetColor("_BaseColor", color);
        if (tex != null) m.SetTexture("_BaseMap", tex);
        if (normal != null) m.SetTexture("_BumpMap", normal);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (emission.HasValue) m.SetColor("_EmissionColor", emission.Value);
        if (emissionMap != null) m.SetTexture("_EmissionMap", emissionMap);
        c.materials[key] = m;
        c.env.owned.Add(m);
        return m;
    }

    private static Material SurfaceMaterial(Context c, SurfaceLayer s, string name)
    {
        SurfaceTextureSet set = c.lib.GetSurface(s.texture);
        string key = name + "_" + s.texture;
        bool glows = s.emission.maxColorComponent > 0.001f;

        if (set == null)
        {
            return glows
                ? NewMaterial(c, key, c.lib.litEmissive, s.color, s.smoothness, s.metallic, null, null, s.emission)
                : NewMaterial(c, key, c.lib.litPlain, s.color, s.smoothness, s.metallic);
        }
        if (set.emission != null)
        {
            return NewMaterial(c, key, c.lib.litTextureNormalEmissive, s.color, s.smoothness, s.metallic,
                set.albedo, set.normal, s.emission, set.emission);
        }
        if (set.normal != null)
        {
            return NewMaterial(c, key, c.lib.litTextureNormal, s.color, s.smoothness, s.metallic, set.albedo, set.normal);
        }
        return NewMaterial(c, key, c.lib.litTexture, s.color, s.smoothness, s.metallic, set.albedo);
    }

    private static Material Solid(Context c, Color color, float smoothness, float metallic = 0f)
    {
        string key = $"Solid_{ColorUtility.ToHtmlStringRGB(color)}_{smoothness:0.00}_{metallic:0.0}";
        return NewMaterial(c, key, c.lib.litPlain, color, smoothness, metallic);
    }

    private static Material Glow(Context c, Color baseColor, Color hdr)
    {
        if (hdr.maxColorComponent < 0.001f) return Solid(c, baseColor, 0.6f);
        string key = $"Glow_{ColorUtility.ToHtmlStringRGB(baseColor)}_{hdr.r:0.0}_{hdr.g:0.0}_{hdr.b:0.0}";
        return NewMaterial(c, key, c.lib.litEmissive, baseColor, 0.85f, 0f, null, null, hdr);
    }

    // ================================================================ Batching

    private static void Add(Context c, Material m, Mesh mesh, Matrix4x4 matrix, bool near)
    {
        var dict = near ? c.near : c.far;
        if (!dict.TryGetValue(m, out MeshBatch batch))
        {
            batch = new MeshBatch();
            dict[m] = batch;
        }
        batch.Add(mesh, matrix);
    }

    private static void Add(Context c, Material m, Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale, bool near)
    {
        Add(c, m, mesh, Matrix4x4.TRS(pos, rot, scale), near);
    }

    private static void Limb(Context c, Material m, Vector3 a, Vector3 b, float radius, bool near)
    {
        Vector3 d = b - a;
        if (d.sqrMagnitude < 1e-6f) return;
        Add(c, m, c.lib.limb, a, Quaternion.FromToRotation(Vector3.up, d.normalized), new Vector3(radius, d.magnitude, radius), near);
    }

    private static void FlushBatches(Context c)
    {
        foreach (var kv in c.near)
        {
            if (kv.Value.Count == 0) continue;
            Part(c, "Near_" + kv.Key.name, Own(c, kv.Value.Build("Near_" + kv.Key.name)), kv.Key, true);
        }
        foreach (var kv in c.far)
        {
            if (kv.Value.Count == 0) continue;
            Part(c, "Far_" + kv.Key.name, Own(c, kv.Value.Build("Far_" + kv.Key.name)), kv.Key, false);
        }
    }

    private static Mesh Own(Context c, Mesh m)
    {
        c.env.owned.Add(m);
        return m;
    }

    private static GameObject Part(Context c, string name, Mesh mesh, Material mat, bool castShadows, Vector3 pos = default)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(c.root, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        mr.lightProbeUsage = LightProbeUsage.Off;
        return go;
    }

    // ================================================================ Lighting

    private static Light BuildSun(Context c)
    {
        Light sun = new GameObject("Sun").AddComponent<Light>();
        sun.transform.SetParent(c.root, false);
        sun.transform.rotation = Quaternion.Euler(c.r.sunPitch, c.r.sunYaw, 0f);
        sun.type = LightType.Directional;
        sun.color = c.r.sunColor;
        sun.intensity = c.r.sunIntensity;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = c.r.shadowStrength;
        sun.shadowBias = 0.05f;
        sun.shadowNormalBias = 0.4f;
        return sun;
    }

    private static void PointLight(Context c, Vector3 pos, float range)
    {
        if (c.lights >= MaxPointLights || c.r.accentLightIntensity <= 0f) return;
        c.lights++;
        Light l = new GameObject("AccentLight").AddComponent<Light>();
        l.transform.SetParent(c.root, false);
        l.transform.localPosition = pos;
        l.type = LightType.Point;
        l.color = c.r.accentLightColor;
        l.intensity = c.r.accentLightIntensity;
        l.range = range;
        l.shadows = LightShadows.None;
    }

    private static void BuildReflectionProbe(Context c)
    {
        ReflectionProbe probe = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
        probe.transform.SetParent(c.root, false);
        probe.transform.localPosition = new Vector3(0f, c.groundY + 4f, 0f);
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = new Vector3(1300f, 600f, 1300f);
        probe.resolution = 64;
        probe.hdr = true;
        probe.clearFlags = ReflectionProbeClearFlags.Skybox;
        probe.cullingMask = ~(1 << 5);
    }

    private static Material BuildSky(Context c, Light sun)
    {
        EnvironmentRecipe r = c.r;
        Material sky = new Material(c.lib.sky) { name = "Sky" };
        sky.SetColor("_TopColor", r.skyTop);
        sky.SetColor("_HorizonColor", r.skyHorizon);
        sky.SetColor("_BottomColor", r.skyBottom);
        sky.SetFloat("_HorizonSharpness", r.horizonSharpness);
        sky.SetColor("_SunColor", r.sunDiscColor);
        sky.SetVector("_SunDirection", -sun.transform.forward);
        sky.SetFloat("_SunSize", r.sunSize);
        sky.SetFloat("_SunGlow", r.sunGlow);
        sky.SetFloat("_StarIntensity", r.stars);
        sky.SetColor("_CloudColor", r.cloudColor);
        sky.SetFloat("_CloudCoverage", r.cloudCoverage);
        c.env.owned.Add(sky);
        return sky;
    }

    private static VolumeProfile BuildVolume(Context c)
    {
        EnvironmentRecipe r = c.r;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "PP_Runtime";

        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1f);
        bloom.intensity.Override(r.bloom);
        bloom.scatter.Override(0.72f);

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(r.postExposure);
        color.contrast.Override(r.contrast);
        color.saturation.Override(r.saturation);

        WhiteBalance wb = profile.Add<WhiteBalance>(true);
        wb.temperature.Override(r.temperature);
        wb.tint.Override(r.tint);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(r.vignette);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(r.vignetteColor);

        c.env.owned.Add(profile);
        foreach (VolumeComponent comp in profile.components) c.env.owned.Add(comp);
        return profile;
    }

    // ================================================================ Ground

    private static float TerrainHeight(Context c, float r, float angle)
    {
        float hill = c.r.hillHeight;
        float x = Mathf.Cos(angle) * r, z = Mathf.Sin(angle) * r;
        float o = (c.r.seed % 1000) * 0.37f;
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GrassInner + 2f, 75f, r));
        float hills = (ProceduralMesh.Fbm(x * 0.018f + o, z * 0.018f + o, 4) - 0.35f) * hill;
        float far = Mathf.InverseLerp(120f, WorldRadius, r) * 6f * Mathf.Clamp01(hill / 9f + 0.3f);
        return rise * (Mathf.Max(hills, -1f) + (hill > 0f ? 1.2f : 0f)) + far;
    }

    private static Vector3 GroundPoint(Context c, float radius, float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a) * radius, c.groundY + TerrainHeight(c, radius, a), Mathf.Sin(a) * radius);
    }

    private static Vector3 Polar(float radius, float degrees, float y)
    {
        float a = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
    }

    private static void BuildGround(Context c)
    {
        EnvironmentRecipe r = c.r;
        float gy = c.groundY;

        Material plaza = SurfaceMaterial(c, r.plaza, "Plaza");
        Part(c, "Plaza", Own(c, ProceduralMesh.Cylinder("Plaza", PlazaRadius, PlazaRadius, 0.5f, 96, r.plaza.textureScale)),
            plaza, false, new Vector3(0f, gy - 0.5f, 0f));

        Material inlay = SurfaceMaterial(c, r.inlay, "Inlay");
        Part(c, "Inlay", Own(c, ProceduralMesh.Ring("Inlay", 6f, 6.45f, 0.03f, 96, 4f)), inlay, false, new Vector3(0f, gy, 0f));
        Part(c, "InlayOuter", Own(c, ProceduralMesh.Ring("InlayOuter", 12.6f, 12.8f, 0.03f, 96, 4f)), inlay, false, new Vector3(0f, gy, 0f));

        Material curb = SurfaceMaterial(c, r.curb, "Curb");
        Part(c, "PlazaCurb", Own(c, ProceduralMesh.Ring("PlazaCurb", PlazaRadius, PlazaRadius + 0.5f, 0.14f, 96, r.curb.textureScale)),
            curb, false, new Vector3(0f, gy - 0.02f, 0f));
        Part(c, "GardenCurb", Own(c, ProceduralMesh.Ring("GardenCurb", RingOuter - 0.05f, GrassInner + 0.1f, 0.18f, 128, r.curb.textureScale)),
            curb, false, new Vector3(0f, gy - 0.03f, 0f));

        SurfaceTextureSet ringSet = c.lib.GetSurface(r.ring.texture);
        bool polar = ringSet != null && ringSet.polarUV;
        float midR = (PlazaRadius + RingOuter) * 0.5f;
        float uRepeats = Mathf.Round(Mathf.PI * 2f * midR / Mathf.Max(0.5f, r.ring.textureScale));
        Mesh ringMesh = ProceduralMesh.PolarGrid("Ring", PlazaRadius + 0.45f, RingOuter, 160, 4, (rad, a) => 0f, 1f,
            polar, r.ring.textureScale, uRepeats, r.ring.textureScale);
        float ringY = r.ring.texture == SurfaceTexture.Water ? gy - 0.1f : gy - 0.03f;
        Part(c, "Ring", Own(c, ringMesh), SurfaceMaterial(c, r.ring, "Ring"), false, new Vector3(0f, ringY, 0f));

        Mesh terrain = ProceduralMesh.PolarGrid("Terrain", GrassInner, WorldRadius, 160, 56,
            (rad, a) => TerrainHeight(c, rad, a), 2.4f, false, r.terrain.textureScale);
        Part(c, "Terrain", Own(c, terrain), SurfaceMaterial(c, r.terrain, "Terrain"), false, new Vector3(0f, gy, 0f));
    }

    private static void BuildMountains(Context c)
    {
        EnvironmentRecipe r = c.r;
        if (r.mountains == MountainStyle.None) return;

        Vector4[] ranges = r.mountains == MountainStyle.Walls
            ? new[] { new Vector4(50f, 120f, 45f, 95f), new Vector4(120f, 230f, 70f, 140f), new Vector4(230f, 420f, 100f, 200f) }
            : new[] { new Vector4(170f, 260f, 18f, 62f), new Vector4(260f, 400f, 40f, 120f), new Vector4(400f, 590f, 80f, 190f) };

        var batch = new MeshBatch();
        var temp = new List<Mesh>();
        float gy = c.groundY;
        for (int i = 0; i < ranges.Length; i++)
        {
            float r0 = ranges[i].x, r1 = ranges[i].y;
            float hMin = ranges[i].z * r.mountainHeight, hMax = ranges[i].w * r.mountainHeight;
            float seed = r.seed * 0.013f + i * 9.7f;
            System.Func<float, float, float> height = MountainHeight(r.mountains, r0, r1, gy, hMin, hMax, seed);
            Mesh m = ProceduralMesh.PolarGrid("Range" + i, r0, r1, 192, 10, height);

            float k = i / (float)(ranges.Length - 1);
            Color baseColor = Color.Lerp(r.mountainNear, r.mountainFar, k);
            Color snow = Color.Lerp(r.snowColor, r.mountainFar, k * 0.35f);
            float top = hMax;
            ProceduralMesh.Colorize(m, p =>
            {
                float h = Mathf.InverseLerp(gy, gy + top, p.y);
                Color col = baseColor * Mathf.Lerp(0.72f, 1.1f, h);
                if (r.snowCaps)
                {
                    float line = 0.5f + (Mathf.PerlinNoise(p.x * 0.02f, p.z * 0.02f) - 0.5f) * 0.25f;
                    col = Color.Lerp(col, snow, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(line, line + 0.1f, h)));
                }
                col.a = 1f;
                return col;
            });
            temp.Add(m);
            batch.Add(m, Matrix4x4.identity);
        }

        Mesh mountains = Own(c, batch.Build("Mountains"));
        foreach (Mesh m in temp) Object.DestroyImmediate(m);

        Material mat = NewMaterial(c, "Mountains", c.lib.vertexColorLit, Color.white, 0f);
        Part(c, "Mountains", mountains, mat, false);
    }

    private static System.Func<float, float, float> MountainHeight(MountainStyle style, float r0, float r1, float gy, float hMin, float hMax, float seed)
    {
        switch (style)
        {
            case MountainStyle.Mesas:
                return (rad, a) =>
                {
                    float t = Mathf.InverseLerp(r0, r1, rad);
                    float profile = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.35f);
                    float n = ProceduralMesh.CircularNoise(a, 3f, seed) * 0.7f + ProceduralMesh.CircularNoise(a, 9f, seed + 3f) * 0.3f;
                    float h = Mathf.Lerp(hMin, hMax, n) * 0.75f;
                    float plateau = Mathf.Round(n * 4f) / 4f * h + hMin * 0.3f;
                    return gy - 3f + Mathf.Min(profile * h * 1.8f, plateau);
                };
            case MountainStyle.Dunes:
                return (rad, a) =>
                {
                    float t = Mathf.InverseLerp(r0, r1, rad);
                    float profile = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI));
                    float n = ProceduralMesh.CircularNoise(a, 2.5f, seed + t * 1.5f) * 0.6f + ProceduralMesh.CircularNoise(a, 7f, seed + 5f + t) * 0.4f;
                    return gy - 3f + profile * Mathf.Lerp(hMin, hMax, n) * 0.4f;
                };
            default:
                return ProceduralMesh.MountainRange(r0, r1, gy, hMin, hMax, seed);
        }
    }

    // ================================================================ Props

    private static bool BlockedByGate(Context c, float radius, float degrees)
    {
        if (radius > c.r.structureRadius + 14f) return false;
        foreach (float g in c.gateAngles)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(degrees, g)) < 13f) return true;
        }
        return false;
    }

    private static void BuildProps(Context c)
    {
        foreach (PropLayer layer in c.r.props)
        {
            for (int i = 0; i < layer.count; i++)
            {
                float rad = 0f, ang = 0f;
                for (int tries = 0; tries < 6; tries++)
                {
                    rad = Mathf.Lerp(layer.minRadius, layer.maxRadius, Mathf.Sqrt((float)c.rng.NextDouble()));
                    ang = c.Rand(0f, 360f);
                    if (!BlockedByGate(c, rad, ang)) break;
                }
                Vector3 p = GroundPoint(c, rad, ang);
                if (layer.heightOffset > 0f) p.y += layer.heightOffset * c.Rand(0.5f, 1.5f);
                float s = c.Rand(layer.minScale, layer.maxScale);
                bool near = rad < ShadowRadius;
                AddProp(c, layer, p, s, near);
            }
        }
    }

    private static Quaternion Yaw(Context c) => Quaternion.Euler(0f, c.Rand(0f, 360f), 0f);

    private static void AddProp(Context c, PropLayer L, Vector3 p, float s, bool near)
    {
        EnvironmentLibrary lib = c.lib;
        Material mA = Solid(c, L.colorA, 0.2f);
        Material mB = Solid(c, L.colorB, 0.2f);
        Material mS = Solid(c, L.secondary, 0.25f);
        Material Pick() => c.Chance(0.6f) ? mA : mB;
        Vector3 up = Vector3.up;

        switch (L.kind)
        {
            case PropKind.RoundTree:
            {
                Vector3 p1 = p + new Vector3(c.Rand(-0.5f, 0.5f), 2.2f, c.Rand(-0.5f, 0.5f)) * s;
                Vector3 p2 = p1 + new Vector3(c.Rand(-0.6f, 0.6f), 1.7f, c.Rand(-0.6f, 0.6f)) * s;
                Limb(c, mS, p - up * 0.2f * s, p1, 0.4f * s, near);
                Limb(c, mS, p1 - up * 0.1f * s, p2, 0.3f * s, near);
                int branches = 3 + c.rng.Next(2);
                float start = c.Rand(0f, 360f);
                for (int b = 0; b < branches; b++)
                {
                    float a = (start + b * 360f / branches + c.Rand(-20f, 20f)) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(a), c.Rand(0.5f, 1f), Mathf.Sin(a)).normalized;
                    Vector3 from = b % 2 == 0 ? p2 : Vector3.Lerp(p1, p2, 0.5f);
                    Vector3 end = from + dir * c.Rand(1.8f, 2.6f) * s;
                    Limb(c, mS, from, end, 0.18f * s, near);
                    Add(c, Pick(), lib.canopyBlobs[c.rng.Next(lib.canopyBlobs.Length)], end + up * 0.4f * s, Random3(c), Vector3.one * c.Rand(1.4f, 2f) * s, near);
                }
                Add(c, Pick(), lib.canopyBlobs[c.rng.Next(lib.canopyBlobs.Length)], p2 + up * 1.3f * s, Random3(c), Vector3.one * c.Rand(1.9f, 2.4f) * s, near);
                break;
            }
            case PropKind.PineTree:
            case PropKind.SnowPine:
            {
                Limb(c, mS, p - up * 0.2f * s, p + up * 2f * s, 0.3f * s, near);
                Material leaves = Pick();
                Material snow = Solid(c, new Color(0.93f, 0.96f, 1f), 0.45f);
                Quaternion rot = Yaw(c);
                float y = 1.3f;
                for (int k = 0; k < lib.pineTiers.Length; k++)
                {
                    Add(c, leaves, lib.pineTiers[k], p + up * y * s, rot, Vector3.one * s, near);
                    if (L.kind == PropKind.SnowPine)
                        Add(c, snow, lib.pineTiers[k], p + up * (y + 0.75f) * s, rot, Vector3.one * 0.72f * s, near);
                    y += 1.75f - k * 0.15f;
                }
                break;
            }
            case PropKind.Cypress:
            {
                Limb(c, mS, p - up * 0.2f * s, p + up * 1.4f * s, 0.25f * s, near);
                Material leaves = Pick();
                Add(c, leaves, lib.canopyBlobs[0], p + up * 3.6f * s, Yaw(c), new Vector3(1.1f, 3.0f, 1.1f) * s, near);
                Add(c, leaves, lib.canopyBlobs[1], p + up * 6.6f * s, Yaw(c), new Vector3(0.75f, 1.8f, 0.75f) * s, near);
                break;
            }
            case PropKind.Palm:
            {
                float lean = c.Rand(0f, 360f) * Mathf.Deg2Rad;
                Vector3 d = new Vector3(Mathf.Cos(lean), 0f, Mathf.Sin(lean));
                Vector3 prev = p - up * 0.2f * s;
                for (int k = 1; k <= 4; k++)
                {
                    Vector3 next = p + (up * k * 1.6f + d * k * k * 0.12f) * s;
                    Limb(c, mS, prev, next, Mathf.Lerp(0.26f, 0.17f, k / 4f) * s, near);
                    prev = next;
                }
                int fronds = 7;
                for (int j = 0; j < fronds; j++)
                {
                    Quaternion rot = Quaternion.Euler(0f, j * 360f / fronds + c.Rand(-10f, 10f), 0f) * Quaternion.Euler(0f, 0f, -c.Rand(18f, 38f));
                    Add(c, Pick(), lib.frond, prev + rot * new Vector3(1.4f * s, 0f, 0f), rot, Vector3.one * s, near);
                }
                for (int j = 0; j < 3; j++)
                    Add(c, mS, lib.tinyBlob, prev + Polar(0.3f * s, j * 120f, -0.25f * s), Random3(c), Vector3.one * 0.22f * s, near);
                break;
            }
            case PropKind.Acacia:
            {
                Vector3 p1 = p + new Vector3(c.Rand(-0.6f, 0.6f), 2.8f, c.Rand(-0.6f, 0.6f)) * s;
                Limb(c, mS, p - up * 0.2f * s, p1, 0.32f * s, near);
                for (int b = 0; b < 3; b++)
                {
                    float a = (b * 120f + c.Rand(-25f, 25f)) * Mathf.Deg2Rad;
                    Vector3 end = p1 + new Vector3(Mathf.Cos(a) * 2f, 1.3f, Mathf.Sin(a) * 2f) * s;
                    Limb(c, mS, p1, end, 0.15f * s, near);
                    Add(c, Pick(), lib.flatCanopy, end + up * 0.2f * s, Yaw(c), Vector3.one * c.Rand(0.8f, 1.1f) * s, near);
                }
                Add(c, Pick(), lib.flatCanopy, p1 + up * 1.8f * s, Yaw(c), Vector3.one * 1.3f * s, near);
                break;
            }
            case PropKind.DeadTree:
            {
                Vector3 p1 = p + new Vector3(c.Rand(-0.5f, 0.5f), 2.5f, c.Rand(-0.5f, 0.5f)) * s;
                Vector3 p2 = p1 + new Vector3(c.Rand(-0.8f, 0.8f), 1.8f, c.Rand(-0.8f, 0.8f)) * s;
                Limb(c, mS, p - up * 0.2f * s, p1, 0.32f * s, near);
                Limb(c, mS, p1, p2, 0.22f * s, near);
                for (int b = 0; b < 5; b++)
                {
                    float a = c.Rand(0f, 360f) * Mathf.Deg2Rad;
                    Vector3 from = Vector3.Lerp(p1, p2, c.Rand(0f, 1f));
                    Vector3 end = from + new Vector3(Mathf.Cos(a), c.Rand(0.3f, 0.9f), Mathf.Sin(a)).normalized * c.Rand(1.4f, 2.4f) * s;
                    Limb(c, mS, from, end, 0.12f * s, near);
                    Vector3 twig = end + new Vector3(c.Rand(-0.6f, 0.6f), c.Rand(0.4f, 0.9f), c.Rand(-0.6f, 0.6f)) * s;
                    Limb(c, mS, end, twig, 0.07f * s, near);
                }
                break;
            }
            case PropKind.Bamboo:
            {
                int stalks = 5 + c.rng.Next(4);
                for (int k = 0; k < stalks; k++)
                {
                    Vector3 b = p + Polar(c.Rand(0f, 1.3f) * s, c.Rand(0f, 360f), 0f);
                    float h = c.Rand(7f, 11f) * s;
                    Vector3 top = b + new Vector3(c.Rand(-0.6f, 0.6f), h, c.Rand(-0.6f, 0.6f));
                    Limb(c, mA, b - up * 0.2f, top, 0.11f * s, near);
                    for (float y = 1.4f * s; y < h; y += 1.4f * s)
                    {
                        Vector3 node = Vector3.Lerp(b, top, y / h);
                        Add(c, mS, lib.cylinder, node, Quaternion.FromToRotation(up, (top - b).normalized), new Vector3(0.135f * s, 0.07f * s, 0.135f * s), near);
                    }
                    for (int j = 0; j < 3; j++)
                    {
                        Quaternion rot = Quaternion.Euler(0f, c.Rand(0f, 360f), 0f) * Quaternion.Euler(0f, 0f, -c.Rand(10f, 35f));
                        Vector3 at = Vector3.Lerp(b, top, c.Rand(0.7f, 1f));
                        Add(c, mB, lib.frond, at + rot * new Vector3(0.6f * s, 0f, 0f), rot, Vector3.one * 0.45f * s, near);
                    }
                }
                break;
            }
            case PropKind.Bush:
            {
                int n = 2 + c.rng.Next(3);
                for (int k = 0; k < n; k++)
                {
                    Vector3 o = Polar(c.Rand(0f, 0.9f) * s, c.Rand(0f, 360f), c.Rand(0.3f, 0.7f) * s);
                    Add(c, Pick(), lib.canopyBlobs[c.rng.Next(lib.canopyBlobs.Length)], p + o, Random3(c), Vector3.one * c.Rand(0.7f, 1.2f) * s, near);
                }
                break;
            }
            case PropKind.Rock:
            {
                Quaternion rot = Quaternion.Euler(c.Rand(-8f, 8f), c.Rand(0f, 360f), c.Rand(-8f, 8f));
                Add(c, Solid(c, L.colorA, 0.25f), lib.rocks[c.rng.Next(lib.rocks.Length)], p - up * 0.25f * s, rot, Vector3.one * s, near);
                break;
            }
            case PropKind.Crystal:
                AddCrystalCluster(c, p, s, L.colorA, L.glow, mS, near);
                break;
            case PropKind.Mushroom:
            {
                float h = c.Rand(1.8f, 2.8f) * s;
                Add(c, mS, lib.cylinder, p - up * 0.2f, Yaw(c), new Vector3(0.28f * s, h, 0.28f * s), near);
                float cap = c.Rand(1f, 1.4f) * s;
                Vector3 top = p + up * h;
                Add(c, Pick(), lib.mushroomCap, top, Yaw(c), Vector3.one * cap, near);
                Material spots = Glow(c, Color.white, L.glow);
                for (int k = 0; k < 5; k++)
                    Add(c, spots, lib.tinyBlob, top + Polar(0.55f * cap, k * 72f + c.Rand(-15f, 15f), 0.3f * cap), Random3(c), Vector3.one * 0.13f * cap, near);
                break;
            }
            case PropKind.Cactus:
            {
                float h = c.Rand(2.8f, 4.2f) * s;
                Material green = Solid(c, L.colorA, 0.3f);
                Add(c, green, lib.cylinder, p - up * 0.2f, Yaw(c), new Vector3(0.38f * s, h, 0.38f * s), near);
                Add(c, green, lib.tinyBlob, p + up * h, Random3(c), Vector3.one * 0.38f * s, near);
                int arms = 1 + c.rng.Next(2);
                for (int k = 0; k < arms; k++)
                {
                    float a = c.Rand(0f, 360f);
                    Vector3 from = p + up * c.Rand(1.2f, 2f) * s;
                    Vector3 elbow = from + Polar(0.9f * s, a, 0f);
                    Vector3 tip = elbow + up * c.Rand(0.9f, 1.4f) * s;
                    Limb(c, green, from, elbow, 0.24f * s, near);
                    Limb(c, green, elbow, tip, 0.24f * s, near);
                    Add(c, green, lib.tinyBlob, tip, Random3(c), Vector3.one * 0.18f * s, near);
                }
                break;
            }
            case PropKind.RuinColumn:
            {
                Material stone = Solid(c, L.colorA, 0.35f);
                Quaternion yaw = Yaw(c);
                Add(c, stone, lib.box, p + up * 0.1f * s, yaw, new Vector3(2f, 0.6f, 2f) * s, near);
                float h = c.Rand(1.5f, 7f) * s;
                Add(c, stone, lib.flutedShaft, p + up * 0.4f * s, yaw, new Vector3(0.75f * s, h, 0.75f * s), near);
                if (h > 6f * s) Add(c, stone, lib.box, p + up * (0.4f * s + h + 0.2f * s), yaw, new Vector3(2.1f, 0.4f, 2.1f) * s, near);
                Add(c, mS, lib.tinyBlob, p + up * (0.4f * s + h), Random3(c), new Vector3(0.7f, 0.25f, 0.7f) * s, near);
                if (c.Chance(0.4f))
                {
                    Quaternion lying = yaw * Quaternion.Euler(90f, 0f, 0f);
                    Add(c, stone, lib.flutedShaft, p + yaw * new Vector3(2.2f, 0.55f, -0.8f) * s, lying, new Vector3(0.7f * s, 1.6f * s, 0.7f * s), near);
                }
                break;
            }
            case PropKind.Coral:
            {
                int arms = 4 + c.rng.Next(3);
                for (int k = 0; k < arms; k++)
                {
                    Material m = k % 2 == 0 ? mA : mB;
                    float a = c.Rand(0f, 360f) * Mathf.Deg2Rad;
                    Vector3 mid = p + new Vector3(Mathf.Cos(a) * 0.6f, 1.2f, Mathf.Sin(a) * 0.6f) * s;
                    Vector3 tip = mid + new Vector3(Mathf.Cos(a) * c.Rand(0.4f, 0.9f), c.Rand(0.8f, 1.5f), Mathf.Sin(a) * c.Rand(0.4f, 0.9f)) * s;
                    Limb(c, m, p - up * 0.1f, mid, 0.2f * s, near);
                    Limb(c, m, mid, tip, 0.14f * s, near);
                    Add(c, Glow(c, L.colorA, L.glow), lib.tinyBlob, tip, Random3(c), Vector3.one * 0.2f * s, near);
                }
                Add(c, mB, lib.canopyBlobs[0], p + Polar(1.4f * s, c.Rand(0f, 360f), 0.2f * s), Random3(c), new Vector3(0.9f, 0.6f, 0.9f) * s, near);
                break;
            }
            case PropKind.Kelp:
            {
                int strands = 3 + c.rng.Next(3);
                for (int k = 0; k < strands; k++)
                {
                    Vector3 prev = p + Polar(c.Rand(0f, 0.8f) * s, c.Rand(0f, 360f), -0.2f);
                    float phase = c.Rand(0f, 6.28f);
                    for (int seg = 1; seg <= 5; seg++)
                    {
                        Vector3 next = prev + new Vector3(Mathf.Sin(phase + seg) * 0.35f, 1.6f, Mathf.Cos(phase + seg * 0.7f) * 0.35f) * s;
                        Limb(c, k % 2 == 0 ? mA : mB, prev, next, 0.1f * s, near);
                        Quaternion rot = Quaternion.Euler(0f, c.Rand(0f, 360f), 0f) * Quaternion.Euler(0f, 0f, c.Rand(20f, 50f));
                        Add(c, mB, lib.frond, next + rot * new Vector3(0.4f * s, 0f, 0f), rot, Vector3.one * 0.3f * s, near);
                        prev = next;
                    }
                }
                break;
            }
            case PropKind.Topiary:
            {
                if (c.Chance(0.5f))
                {
                    Vector3 dir = new Vector3(p.x, 0f, p.z).normalized;
                    Quaternion facing = Quaternion.LookRotation(dir);
                    Add(c, Pick(), lib.box, p + up * 0.6f * s, facing, new Vector3(c.Rand(2.5f, 4f), 1.5f, 0.9f) * s, near);
                }
                else
                {
                    Add(c, mS, lib.box, p + up * 0.2f * s, Yaw(c), new Vector3(0.9f, 0.7f, 0.9f) * s, near);
                    Add(c, mS, lib.cylinder, p + up * 0.5f * s, Quaternion.identity, new Vector3(0.08f, 0.9f, 0.08f) * s, near);
                    Add(c, Pick(), lib.cloudBlob, p + up * 2f * s, Random3(c), Vector3.one * c.Rand(0.9f, 1.15f) * s, near);
                }
                break;
            }
            case PropKind.Flowers:
            {
                int n = 8 + c.rng.Next(7);
                Material leaf = mS;
                for (int k = 0; k < 3; k++)
                    Add(c, leaf, lib.tinyBlob, p + Polar(c.Rand(0f, 0.8f) * s, c.Rand(0f, 360f), 0.12f * s), Random3(c), new Vector3(0.5f, 0.25f, 0.5f) * s, near);
                for (int k = 0; k < n; k++)
                    Add(c, k % 2 == 0 ? mA : mB, lib.tinyBlob, p + Polar(c.Rand(0f, 1.6f) * s, c.Rand(0f, 360f), c.Rand(0.25f, 0.5f) * s),
                        Random3(c), Vector3.one * c.Rand(0.13f, 0.22f) * s, near);
                break;
            }
            case PropKind.IceSpike:
            {
                Material ice = Solid(c, L.colorA, 0.95f);
                int n = 3 + c.rng.Next(3);
                for (int k = 0; k < n; k++)
                {
                    Quaternion rot = Quaternion.Euler(c.Rand(-15f, 15f), c.Rand(0f, 360f), c.Rand(-15f, 15f));
                    float w = c.Rand(0.4f, 0.75f) * s;
                    Add(c, ice, lib.cone, p + Polar(c.Rand(0f, 1.2f) * s, c.Rand(0f, 360f), -0.3f), rot, new Vector3(w, c.Rand(3f, 8f) * s, w), near);
                }
                Add(c, mS, lib.rocks[c.rng.Next(lib.rocks.Length)], p - up * 0.3f * s, Yaw(c), Vector3.one * 0.8f * s, near);
                break;
            }
            case PropKind.CloudPuff:
            {
                Material lit = Solid(c, L.colorA, 0.1f);
                Material shade = Solid(c, L.colorB, 0.1f);
                int n = 4 + c.rng.Next(4);
                for (int k = 0; k < n; k++)
                {
                    float y = c.Rand(0f, 1.4f) * s;
                    Add(c, y > 0.6f * s ? lit : shade, lib.cloudBlob, p + Polar(c.Rand(0f, 3.2f) * s, c.Rand(0f, 360f), y), Random3(c), Vector3.one * c.Rand(2f, 3.6f) * s, false);
                }
                break;
            }
            case PropKind.FloatingIsland:
            {
                Add(c, mS, lib.cone, p, Quaternion.Euler(180f, c.Rand(0f, 360f), 0f), new Vector3(3f, 4.5f, 3f) * s, near);
                Add(c, mA, lib.flatCanopy, p + up * 0.1f * s, Yaw(c), new Vector3(1.9f, 0.7f, 1.9f) * s, near);
                Vector3 top = p + up * 0.4f * s;
                Limb(c, mS, top, top + up * 1.6f * s, 0.18f * s, near);
                Add(c, mB, lib.canopyBlobs[c.rng.Next(lib.canopyBlobs.Length)], top + up * 2.2f * s, Random3(c), Vector3.one * 1.2f * s, near);
                for (int k = 0; k < 3; k++)
                    Add(c, mS, lib.tinyBlob, p + Polar(c.Rand(1f, 2.5f) * s, c.Rand(0f, 360f), -c.Rand(1.5f, 4f) * s), Random3(c), Vector3.one * c.Rand(0.3f, 0.6f) * s, near);
                break;
            }
        }
    }

    private static Quaternion Random3(Context c) => Quaternion.Euler(c.Rand(0f, 360f), c.Rand(0f, 360f), c.Rand(0f, 360f));

    private static void AddCrystalCluster(Context c, Vector3 p, float s, Color color, Color glow, Material baseMat, bool near)
    {
        Material crystal = glow.maxColorComponent > 0.001f ? Glow(c, color, glow) : Solid(c, color, 0.92f);
        int n = 3 + c.rng.Next(4);
        for (int k = 0; k < n; k++)
        {
            Quaternion rot = Quaternion.Euler(c.Rand(-25f, 25f), c.Rand(0f, 360f), c.Rand(-25f, 25f));
            float h = c.Rand(1.2f, 3f) * s * (k == 0 ? 1.4f : 1f);
            float w = c.Rand(0.25f, 0.45f) * s * (k == 0 ? 1.3f : 1f);
            Vector3 b = p + Polar(c.Rand(0f, 0.7f) * s, c.Rand(0f, 360f), -0.2f);
            Add(c, crystal, c.lib.hexCylinder, b, rot, new Vector3(w, h, w), near);
            Add(c, crystal, c.lib.hexCone, b + rot * Vector3.up * h, rot, new Vector3(w, w * 1.8f, w), near);
        }
        Add(c, baseMat, c.lib.rocks[c.rng.Next(c.lib.rocks.Length)], p - Vector3.up * 0.3f * s, Quaternion.Euler(0f, c.Rand(0f, 360f), 0f), Vector3.one * 0.6f * s, near);
    }

    // ================================================================ Structures

    private static void BuildStructures(Context c)
    {
        EnvironmentRecipe r = c.r;
        if (r.structure == StructureKind.None) return;
        EnvironmentLibrary lib = c.lib;

        bool gates = r.structure == StructureKind.Torii || r.structure == StructureKind.Arches;
        int count = gates ? 4 : Mathf.Max(1, r.structureCount);
        float offset = gates ? 45f : 22.5f;
        Material P = Solid(c, r.structurePrimary, 0.5f);
        Material S = Solid(c, r.structureSecondary, 0.65f, r.structureSecondary.r > r.structureSecondary.b * 1.6f ? 0.8f : 0f);
        Material G = Glow(c, Color.white, r.structureGlow);
        int lightEvery = Mathf.Max(1, count / MaxPointLights);

        for (int i = 0; i < count; i++)
        {
            float ang = offset + i * 360f / count;
            if (gates) c.gateAngles.Add(ang);
            Vector3 pos = Polar(r.structureRadius, ang, c.groundY);
            Quaternion rot = Quaternion.LookRotation(new Vector3(-pos.x, 0f, -pos.z).normalized);
            Matrix4x4 m = Matrix4x4.TRS(pos, rot, Vector3.one);
            float lightY = 2f;

            switch (r.structure)
            {
                case StructureKind.Torii:
                    foreach (float x in new[] { -3.3f, 3.3f })
                    {
                        Add(c, P, lib.cylinder, m * Matrix4x4.TRS(new Vector3(x, 0f, 0f), Quaternion.identity, new Vector3(0.4f, 8.6f, 0.4f)), true);
                        Add(c, S, lib.cylinder, m * Matrix4x4.TRS(new Vector3(x, 0f, 0f), Quaternion.identity, new Vector3(0.52f, 0.7f, 0.52f)), true);
                    }
                    Add(c, P, lib.box, m * Matrix4x4.TRS(new Vector3(0f, 6.5f, 0f), Quaternion.identity, new Vector3(8.6f, 0.45f, 0.34f)), true);
                    Add(c, P, lib.box, m * Matrix4x4.TRS(new Vector3(0f, 7.3f, 0f), Quaternion.identity, new Vector3(0.36f, 1.1f, 0.3f)), true);
                    Add(c, P, lib.box, m * Matrix4x4.TRS(new Vector3(0f, 8.05f, 0f), Quaternion.identity, new Vector3(6.4f, 0.55f, 0.72f)), true);
                    Add(c, S, lib.box, m * Matrix4x4.TRS(new Vector3(0f, 8.45f, 0f), Quaternion.identity, new Vector3(6.6f, 0.3f, 0.86f)), true);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Matrix4x4 wing = m * Matrix4x4.TRS(new Vector3(side * 4.35f, 8.2f, 0f), Quaternion.Euler(0f, 0f, side * 7f), Vector3.one);
                        Add(c, P, lib.box, wing * Matrix4x4.Scale(new Vector3(2.7f, 0.55f, 0.72f)), true);
                        Add(c, S, lib.box, wing * Matrix4x4.TRS(new Vector3(0f, 0.4f, 0f), Quaternion.identity, new Vector3(2.9f, 0.3f, 0.86f)), true);
                    }
                    lightY = 0f;
                    break;

                case StructureKind.Columns:
                    Add(c, P, lib.box, m * Matrix4x4.TRS(new Vector3(0f, 0.25f, 0f), Quaternion.identity, new Vector3(2.4f, 0.5f, 2.4f)), true);
                    Add(c, P, lib.cylinder, m * Matrix4x4.TRS(new Vector3(0f, 0.5f, 0f), Quaternion.identity, new Vector3(1.1f, 0.4f, 1.1f)), true);
                    Add(c, S, lib.cylinder, m * Matrix4x4.TRS(new Vector3(0f, 0.86f, 0f), Quaternion.identity, new Vector3(0.98f, 0.12f, 0.98f)), true);
                    Add(c, P, lib.flutedShaft, m * Matrix4x4.TRS(new Vector3(0f, 0.9f, 0f), Quaternion.identity, new Vector3(0.85f, 11.8f, 0.85f)), true);
                    Add(c, S, lib.squareFrustum, m * Matrix4x4.TRS(new Vector3(0f, 13.3f, 0f), Quaternion.Euler(180f, 45f, 0f), new Vector3(1.6f, 0.6f, 1.6f)), true);
                    Add(c, P, lib.box, m * Matrix4x4.TRS(new Vector3(0f, 13.47f, 0f), Quaternion.identity, new Vector3(2.5f, 0.35f, 2.5f)), true);
                    if (r.structureGlow.maxColorComponent > 0.001f)
                        Add(c, G, lib.cloudBlob, m * Matrix4x4.TRS(new Vector3(0f, 14.2f, 0f), Quaternion.identity, Vector3.one * 0.45f), true);
                    lightY = 14f;
                    break;

                case StructureKind.StoneLanterns:
                {
                    float y = 0f;
                    Add(c, P, lib.hexCylinder, m * Matrix4x4.TRS(new Vector3(0f, y, 0f), Quaternion.identity, new Vector3(0.75f, 0.35f, 0.75f)), true); y += 0.35f;
                    Add(c, P, lib.hexCylinder, m * Matrix4x4.TRS(new Vector3(0f, y, 0f), Quaternion.identity, new Vector3(0.28f, 1.1f, 0.28f)), true); y += 1.1f;
                    Add(c, P, lib.hexCylinder, m * Matrix4x4.TRS(new Vector3(0f, y, 0f), Quaternion.identity, new Vector3(0.7f, 0.25f, 0.7f)), true); y += 0.25f;
                    Add(c, G, lib.hexCylinder, m * Matrix4x4.TRS(new Vector3(0f, y, 0f), Quaternion.identity, new Vector3(0.42f, 0.65f, 0.42f)), true); y += 0.65f;
                    Add(c, P, lib.hexCone, m * Matrix4x4.TRS(new Vector3(0f, y, 0f), Quaternion.identity, new Vector3(1.05f, 0.6f, 1.05f)), true); y += 0.6f;
                    Add(c, P, lib.tinyBlob, m * Matrix4x4.TRS(new Vector3(0f, y + 0.1f, 0f), Quaternion.identity, Vector3.one * 0.2f), true);
                    lightY = 2f;
                    break;
                }

                case StructureKind.Obelisks:
                    Add(c, S, lib.box, m * Matrix4x4.TRS(new Vector3(0f, 0.3f, 0f), Quaternion.identity, new Vector3(2.4f, 0.6f, 2.4f)), true);
                    Add(c, P, lib.squareFrustum, m * Matrix4x4.TRS(new Vector3(0f, 0.6f, 0f), Quaternion.Euler(0f, 45f, 0f), new Vector3(1.1f, 9f, 1.1f)), true);
                    Add(c, r.structureGlow.maxColorComponent > 0.001f ? G : S, lib.pyramid,
                        m * Matrix4x4.TRS(new Vector3(0f, 9.6f, 0f), Quaternion.Euler(0f, 45f, 0f), new Vector3(0.77f, 1.3f, 0.77f)), true);
                    lightY = 10f;
                    break;

                case StructureKind.Braziers:
                    Add(c, S, lib.cylinder, m * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.6f, 0.15f, 0.6f)), true);
                    Add(c, S, lib.limb, m * Matrix4x4.TRS(new Vector3(0f, 0.15f, 0f), Quaternion.identity, new Vector3(0.3f, 1.8f, 0.3f)), true);
                    Add(c, S, lib.hexCone, m * Matrix4x4.TRS(new Vector3(0f, 2.45f, 0f), Quaternion.Euler(180f, 0f, 0f), new Vector3(0.85f, 0.5f, 0.85f)), true);
                    Add(c, G, lib.cloudBlob, m * Matrix4x4.TRS(new Vector3(0f, 2.75f, 0f), Quaternion.identity, Vector3.one * 0.42f), true);
                    lightY = 3f;
                    break;

                case StructureKind.Monoliths:
                    foreach (float x in new[] { -1.6f, 1.6f })
                    {
                        Matrix4x4 stone = m * Matrix4x4.TRS(new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 0f, -x * 1.2f), Vector3.one);
                        Add(c, P, lib.box, stone * Matrix4x4.TRS(new Vector3(0f, 2.8f, 0f), Quaternion.identity, new Vector3(1.4f, 6f, 0.9f)), true);
                        if (r.structureGlow.maxColorComponent > 0.001f)
                            Add(c, G, lib.box, stone * Matrix4x4.TRS(new Vector3(0f, 3.2f, 0.46f), Quaternion.identity, new Vector3(0.45f, 1.6f, 0.04f)), true);
                    }
                    Add(c, P, lib.box, m * Matrix4x4.TRS(new Vector3(0f, 6.2f, 0f), Quaternion.identity, new Vector3(4.6f, 0.9f, 1.0f)), true);
                    lightY = 3f;
                    break;

                case StructureKind.CrystalPylons:
                    AddCrystalCluster(c, pos, 2.2f, r.structurePrimary, r.structureGlow, S, true);
                    lightY = 3f;
                    break;

                case StructureKind.Arches:
                    foreach (float x in new[] { -3f, 3f })
                        Add(c, P, lib.box, m * Matrix4x4.TRS(new Vector3(x, 3.5f, 0f), Quaternion.identity, new Vector3(1.1f, 7f, 1.1f)), true);
                    for (int k = 0; k <= 6; k++)
                    {
                        float t = Mathf.PI * k / 6f;
                        Vector3 at = new Vector3(Mathf.Cos(t) * 3f, 7f + Mathf.Sin(t) * 3f, 0f);
                        Add(c, k == 3 ? S : P, lib.box, m * Matrix4x4.TRS(at, Quaternion.Euler(0f, 0f, t * Mathf.Rad2Deg + 90f), new Vector3(1.1f, 1.5f, 1.1f)), true);
                    }
                    lightY = 0f;
                    break;
            }

            if (lightY > 0f && i % lightEvery == 0)
                PointLight(c, pos + Vector3.up * lightY + new Vector3(-pos.x, 0f, -pos.z).normalized * 0.8f, 10f);
        }
    }

    // ================================================================ Particles

    private static ParticleSystem NewParticles(Context c, string name, Material mat, Vector3 boxSize, float centerY,
        Vector2 lifetime, Vector2 size, float rate, int max)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(c.root, false);
        go.transform.localPosition = new Vector3(0f, c.groundY + centerY, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.startColor = c.r.particleColor;

        var emission = ps.emission;
        emission.rateOverTime = rate * Mathf.Max(0f, c.r.particleRate);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = boxSize;

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = mat;
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;
        return ps;
    }

    private static void Velocity(ParticleSystem ps, Vector2 x, Vector2 y, Vector2 z)
    {
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(x.x, x.y);
        vel.y = new ParticleSystem.MinMaxCurve(y.x, y.y);
        vel.z = new ParticleSystem.MinMaxCurve(z.x, z.y);
    }

    private static void Noise(ParticleSystem ps, float strength, float frequency)
    {
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = strength;
        noise.frequency = frequency;
        noise.scrollSpeed = 0.15f;
    }

    private static void Fade(ParticleSystem ps, params float[] alphaKeys)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var keys = new GradientAlphaKey[alphaKeys.Length / 2];
        for (int i = 0; i < keys.Length; i++) keys[i] = new GradientAlphaKey(alphaKeys[i * 2 + 1], alphaKeys[i * 2]);
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, keys);
        col.color = g;
    }

    private static void Spin(ParticleSystem ps)
    {
        var main = ps.main;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
    }

    private static void BuildParticles(Context c)
    {
        EnvironmentRecipe r = c.r;
        if (r.particles == ParticleKind.None) return;

        Material add = NewMaterial(c, "ParticlesAdd", c.lib.particleAdditive, Color.white, 0f, 0f, c.lib.softDot);
        Material alphaDot = NewMaterial(c, "ParticlesAlphaDot", c.lib.particleAlpha, Color.white, 0f, 0f, c.lib.softDot);
        Material alphaPetal = NewMaterial(c, "ParticlesPetal", c.lib.particleAlpha, Color.white, 0f, 0f, c.lib.petal);
        ParticleSystem ps;

        switch (r.particles)
        {
            case ParticleKind.Snow:
                ps = NewParticles(c, "Snow", add, new Vector3(90f, 2f, 90f), 22f, new Vector2(14f, 18f), new Vector2(0.07f, 0.16f), 70f, 500);
                Velocity(ps, new Vector2(0.2f, 0.5f), new Vector2(-1.4f, -0.9f), new Vector2(-0.1f, 0.2f));
                Noise(ps, 0.5f, 0.3f);
                Fade(ps, 0f, 0f, 0.1f, 1f, 0.9f, 1f, 1f, 0f);
                break;
            case ParticleKind.Petals:
            case ParticleKind.Leaves:
            {
                bool leaves = r.particles == ParticleKind.Leaves;
                ps = NewParticles(c, leaves ? "Leaves" : "Petals", alphaPetal, new Vector3(80f, 4f, 80f), 16f, new Vector2(12f, 16f),
                    leaves ? new Vector2(0.2f, 0.34f) : new Vector2(0.12f, 0.24f), leaves ? 22f : 32f, 400);
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(r.particleColor, r.particleColor * 0.8f);
                main.gravityModifier = 0.012f;
                Velocity(ps, new Vector2(0.4f, 0.9f), new Vector2(-0.3f, 0f), new Vector2(0.1f, 0.4f));
                Spin(ps);
                Noise(ps, 0.6f, 0.3f);
                Fade(ps, 0f, 0f, 0.1f, 1f, 0.85f, 1f, 1f, 0f);
                break;
            }
            case ParticleKind.Fireflies:
                ps = NewParticles(c, "Fireflies", add, new Vector3(70f, 5f, 70f), 2.5f, new Vector2(6f, 10f), new Vector2(0.12f, 0.22f), 25f, 220);
                Velocity(ps, new Vector2(-0.15f, 0.15f), new Vector2(-0.05f, 0.1f), new Vector2(-0.15f, 0.15f));
                Noise(ps, 0.8f, 0.4f);
                Fade(ps, 0f, 0f, 0.15f, 1f, 0.3f, 0.2f, 0.5f, 1f, 0.7f, 0.3f, 1f, 0f);
                break;
            case ParticleKind.Embers:
                ps = NewParticles(c, "Embers", add, new Vector3(70f, 1f, 70f), 0.3f, new Vector2(5f, 9f), new Vector2(0.06f, 0.14f), 40f, 300);
                Velocity(ps, new Vector2(-0.2f, 0.4f), new Vector2(0.6f, 1.6f), new Vector2(-0.2f, 0.3f));
                Noise(ps, 0.6f, 0.5f);
                Fade(ps, 0f, 0f, 0.1f, 1f, 0.7f, 0.8f, 1f, 0f);
                break;
            case ParticleKind.GoldMotes:
                ps = NewParticles(c, "Motes", add, new Vector3(70f, 12f, 70f), 6f, new Vector2(8f, 13f), new Vector2(0.05f, 0.16f), 26f, 300);
                Velocity(ps, new Vector2(-0.1f, 0.1f), new Vector2(0f, 0.12f), new Vector2(-0.1f, 0.1f));
                Noise(ps, 0.35f, 0.25f);
                Fade(ps, 0f, 0f, 0.25f, 1f, 0.7f, 1f, 1f, 0f);
                break;
            case ParticleKind.Bubbles:
                ps = NewParticles(c, "Bubbles", add, new Vector3(70f, 2f, 70f), 0f, new Vector2(10f, 16f), new Vector2(0.1f, 0.3f), 30f, 300);
                Velocity(ps, new Vector2(-0.1f, 0.1f), new Vector2(0.5f, 1.2f), new Vector2(-0.1f, 0.1f));
                Noise(ps, 0.3f, 0.6f);
                Fade(ps, 0f, 0f, 0.1f, 1f, 0.8f, 1f, 1f, 0f);
                break;
            case ParticleKind.Spores:
                ps = NewParticles(c, "Spores", add, new Vector3(70f, 12f, 70f), 6f, new Vector2(8f, 12f), new Vector2(0.06f, 0.15f), 30f, 280);
                Velocity(ps, new Vector2(-0.12f, 0.12f), new Vector2(-0.05f, 0.15f), new Vector2(-0.12f, 0.12f));
                Noise(ps, 0.4f, 0.3f);
                Fade(ps, 0f, 0f, 0.2f, 1f, 0.8f, 1f, 1f, 0f);
                break;
            case ParticleKind.Dust:
                ps = NewParticles(c, "Dust", alphaDot, new Vector3(90f, 8f, 90f), 4f, new Vector2(10f, 14f), new Vector2(0.3f, 0.8f), 25f, 250);
                Velocity(ps, new Vector2(0.8f, 1.6f), new Vector2(-0.05f, 0.1f), new Vector2(0.1f, 0.4f));
                Noise(ps, 0.4f, 0.2f);
                Fade(ps, 0f, 0f, 0.2f, 1f, 0.8f, 1f, 1f, 0f);
                break;
        }
    }
}
