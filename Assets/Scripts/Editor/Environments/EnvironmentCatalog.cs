using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// The 100 environment designs: 20 hand-tuned biomes x 5 variations (time of day + colour palette).
/// Order interleaves biomes, so every consecutive environment the player sees is a different world.
/// Edit a biome here and re-run Tools > Last Brick > Environments > Build All 100 Environments.
/// </summary>
public static class EnvironmentCatalog
{
    public struct Entry
    {
        public string id;
        public string displayName;
        public EnvironmentRecipe recipe;
    }

    private delegate EnvironmentRecipe Biome(int variant, out string variantName);

    private static readonly (string name, Biome build)[] Biomes =
    {
        ("SakuraGarden", Sakura),
        ("AlpineSnow", Alpine),
        ("AutumnForest", Autumn),
        ("TropicalLagoon", Lagoon),
        ("DesertDunes", Desert),
        ("CrystalCavern", Cavern),
        ("VolcanicForge", Volcanic),
        ("EnchantedForest", Enchanted),
        ("AegeanTemple", Aegean),
        ("BambooGrove", Bamboo),
        ("HighlandMeadow", Meadow),
        ("IceKingdom", IceKingdom),
        ("JungleRuins", Jungle),
        ("CanyonMesa", Canyon),
        ("MushroomGlade", Mushrooms),
        ("RoyalGarden", Royal),
        ("MistyFjord", Fjord),
        ("SavannaSunset", Savanna),
        ("CoralReef", Reef),
        ("CloudKingdom", Clouds),
    };

    public const int Count = 100;

    public static List<Entry> BuildAll()
    {
        var list = new List<Entry>();
        for (int i = 0; i < Count; i++)
        {
            int biome = i % Biomes.Length;
            int variant = i / Biomes.Length;
            EnvironmentRecipe r = Biomes[biome].build(variant, out string variantName);
            r.seed = 1000 + i * 7919;
            r.sunYaw = -60f + (i * 47) % 120;
            list.Add(new Entry
            {
                id = $"{i + 1:000}_{Biomes[biome].name}_{variantName.Replace(" ", "")}",
                displayName = $"{Nice(Biomes[biome].name)} - {variantName}",
                recipe = r
            });
        }
        return list;
    }

    private static string Nice(string id)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < id.Length; i++)
        {
            if (i > 0 && char.IsUpper(id[i])) sb.Append(' ');
            sb.Append(id[i]);
        }
        return sb.ToString();
    }

    // ================================================================ Helpers

    private static Color C(string hex)
    {
        ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out Color c);
        return c;
    }

    private static Color C(string hex, float intensity) => C(hex) * intensity;

    private static SurfaceLayer S(SurfaceTexture t, string hex, float smooth, float scale = 6f) =>
        new SurfaceLayer(t, C(hex), smooth, scale);

    private static PropLayer P(PropKind k, int n, float r0, float r1, float s0, float s1, string a, string b, string sec) =>
        new PropLayer(k, n, r0, r1, s0, s1, C(a), C(b), C(sec));

    private struct Mood
    {
        public string name;
        public string top, horizon, bottom, disc, light, cloud;
        public float discIntensity, intensity, pitch, stars, glow, ambient, temperature, clouds, sunSize, bloom, cloudAlpha;
        public bool night;
    }

    private static readonly Mood Dawn = new Mood
    {
        name = "Dawn", top = "#5B7FC4", horizon = "#FFC1A1", bottom = "#6E5F6A", disc = "#FFD9B0", light = "#FFC9A0", cloud = "#FFE2D2",
        discIntensity = 2f, intensity = 1.35f, pitch = 12f, stars = 0.1f, glow = 1.3f, ambient = 0.95f, temperature = -2f, clouds = 0.35f, sunSize = 0.05f, bloom = 0.8f, cloudAlpha = 0.8f
    };

    private static readonly Mood Day = new Mood
    {
        name = "Day", top = "#2F6FE0", horizon = "#BFE0FF", bottom = "#6F8090", disc = "#FFF6E0", light = "#FFF4E2", cloud = "#FFFFFF",
        discIntensity = 2f, intensity = 1.6f, pitch = 52f, stars = 0f, glow = 0.6f, ambient = 1.05f, temperature = 0f, clouds = 0.4f, sunSize = 0.045f, bloom = 0.5f, cloudAlpha = 0.9f
    };

    private static readonly Mood Golden = new Mood
    {
        name = "Golden Hour", top = "#4C70B8", horizon = "#FFC57A", bottom = "#7A6450", disc = "#FFD27A", light = "#FFCB8A", cloud = "#FFE6C4",
        discIntensity = 2.5f, intensity = 1.75f, pitch = 22f, stars = 0f, glow = 1.4f, ambient = 1f, temperature = 6f, clouds = 0.3f, sunSize = 0.055f, bloom = 0.75f, cloudAlpha = 0.85f
    };

    private static readonly Mood Dusk = new Mood
    {
        name = "Dusk", top = "#2B2557", horizon = "#F27A5E", bottom = "#3F2C3E", disc = "#FF8A5C", light = "#FF9A6A", cloud = "#E8A3A0",
        discIntensity = 2.5f, intensity = 1.15f, pitch = 7f, stars = 0.4f, glow = 1.8f, ambient = 0.8f, temperature = 4f, clouds = 0.35f, sunSize = 0.065f, bloom = 0.9f, cloudAlpha = 0.8f
    };

    private static readonly Mood Night = new Mood
    {
        name = "Night", top = "#050A1E", horizon = "#1B2A52", bottom = "#0A0D16", disc = "#CFE0FF", light = "#9DB6FF", cloud = "#39456E",
        discIntensity = 1.5f, intensity = 0.5f, pitch = 38f, stars = 1.2f, glow = 0.7f, ambient = 0.7f, temperature = -6f, clouds = 0.2f, sunSize = 0.035f, bloom = 1.1f, cloudAlpha = 0.7f, night = true
    };

    private static EnvironmentRecipe New(Mood m)
    {
        var r = new EnvironmentRecipe();
        ApplyMood(r, m);
        return r;
    }

    private static void ApplyMood(EnvironmentRecipe r, Mood m)
    {
        r.skyTop = C(m.top);
        r.skyHorizon = C(m.horizon);
        r.skyBottom = C(m.bottom);
        r.sunDiscColor = C(m.disc, m.discIntensity);
        r.sunColor = C(m.light);
        r.sunIntensity = m.intensity;
        r.sunPitch = m.pitch;
        r.stars = m.stars;
        r.sunGlow = m.glow;
        r.sunSize = m.sunSize;
        r.ambientIntensity = m.ambient;
        r.temperature = m.temperature;
        r.cloudCoverage = m.clouds;
        r.cloudColor = C(m.cloud);
        r.cloudColor.a = m.cloudAlpha;
        r.bloom = m.bloom;
        r.vignetteColor = m.night ? C("#02040A") : C("#1A0E08");
        r.shadowStrength = m.night ? 0.55f : 0.8f;
    }

    /// <summary>Blend the mood sky towards a biome tint (keeps every time of day on-theme).</summary>
    private static void TintSky(EnvironmentRecipe r, string hex, float amount)
    {
        Color t = C(hex);
        r.skyTop = Color.Lerp(r.skyTop, t * r.skyTop.maxColorComponent * 1.2f, amount * 0.5f);
        r.skyHorizon = Color.Lerp(r.skyHorizon, t * Mathf.Max(0.25f, r.skyHorizon.maxColorComponent), amount);
    }

    private static Mood Pick(int v, params Mood[] moods) => moods[v % moods.Length];

    private static T Pick<T>(int v, params T[] items) => items[v % items.Length];

    // ================================================================ Biomes

    private static EnvironmentRecipe Sakura(int v, out string name)
    {
        Mood m = Pick(v, Dawn, Day, Golden, Dusk, Night);
        var pal = Pick(v, ("#FFB3CC", "#F28DB2", "Pink"), ("#FFF0F5", "#F7D6E4", "White"), ("#FFC9B0", "#F7A58A", "Peach"),
            ("#E6B8F0", "#C994E0", "Lilac"), ("#FF8FC7", "#E0609E", "Magenta"));
        name = m.name;
        var r = New(m);
        TintSky(r, "#FFB8C8", 0.25f);
        r.plaza = S(SurfaceTexture.StoneTiles, "#A29E98", 0.45f);
        r.inlay = S(SurfaceTexture.None, "#2A2A2C", 0.7f);
        r.ring = S(SurfaceTexture.RakedGravel, "#ECE8E0", 0.15f, 2.2f);
        r.curb = S(SurfaceTexture.Rock, "#8A8783", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.Noise, "#6E8F4E", 0.08f, 8f);
        r.mountainNear = C("#39504F"); r.mountainFar = C("#8A92AA");
        r.props.Add(P(PropKind.RoundTree, 40, 35f, 58f, 0.9f, 1.4f, pal.Item1, pal.Item2, "#3B2A22"));
        r.props.Add(P(PropKind.PineTree, 200, 62f, 165f, 1.1f, 2.6f, "#1E3A2A", "#2B4A30", "#3B2A22"));
        r.props.Add(P(PropKind.Rock, 20, 33f, 120f, 0.6f, 1.8f, "#7A7570", "#7A7570", "#7A7570"));
        if (m.night)
        {
            r.structure = StructureKind.StoneLanterns;
            r.structurePrimary = C("#8E8A84");
            r.structureGlow = C("#FF9A4A", 4.5f);
            r.accentLightIntensity = 5f;
            r.particles = ParticleKind.Fireflies;
            r.particleColor = C("#FFD27A", 3f);
        }
        else
        {
            r.structure = StructureKind.Torii;
            r.structurePrimary = C("#C8321E");
            r.structureSecondary = C("#121212");
            r.particles = ParticleKind.Petals;
            r.particleColor = C(pal.Item1);
        }
        r.saturation = 12f; r.tint = 5f;
        name = $"{pal.Item3} {m.name}";
        return r;
    }

    private static EnvironmentRecipe Alpine(int v, out string name)
    {
        Mood m = Pick(v, Day, Golden, Dawn, Night, Dusk);
        var pal = Pick(v, ("#1F3B33", "#28473B", "#FFB060"), ("#22402F", "#2F5038", "#FF9A4A"), ("#1C3640", "#27434B", "#FFC27A"),
            ("#1A3330", "#24403A", "#7AC8FF"), ("#233A2C", "#304B36", "#FF8A5A"));
        var r = New(m);
        TintSky(r, "#CFE4FF", 0.2f);
        r.plaza = S(SurfaceTexture.Flagstones, "#A9B4C2", 0.45f);
        r.inlay = S(SurfaceTexture.None, "#3B4A63", 0.7f);
        r.ring = S(SurfaceTexture.Snow, "#F2F6FF", 0.5f, 4f);
        r.curb = S(SurfaceTexture.Rock, "#6C7480", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.Snow, "#EEF3FB", 0.35f, 8f);
        r.hillHeight = 12f;
        r.mountainNear = C("#4A5868"); r.mountainFar = C("#9AA8C0");
        r.snowCaps = true; r.mountainHeight = 1.3f;
        r.props.Add(P(PropKind.SnowPine, 60, 34f, 70f, 0.9f, 1.5f, pal.Item1, pal.Item2, "#3A2A22"));
        r.props.Add(P(PropKind.SnowPine, 190, 70f, 170f, 1.3f, 2.8f, pal.Item1, pal.Item2, "#3A2A22"));
        r.props.Add(P(PropKind.Rock, 25, 33f, 120f, 0.6f, 2f, "#6F7884", "#6F7884", "#6F7884"));
        r.props.Add(P(PropKind.IceSpike, 8, 36f, 60f, 0.6f, 1f, "#BFE6FF", "#BFE6FF", "#6F7884"));
        r.structure = StructureKind.StoneLanterns;
        r.structurePrimary = C("#8C96A3");
        r.structureGlow = C(pal.Item3, 4f);
        r.accentLightColor = C(pal.Item3);
        r.accentLightIntensity = m.night ? 5f : 2.5f;
        r.particles = ParticleKind.Snow;
        r.particleColor = C("#FFFFFF", 1.2f);
        r.fogDensity = 0.005f;
        r.temperature -= 4f;
        name = m.name;
        return r;
    }

    private static EnvironmentRecipe Autumn(int v, out string name)
    {
        Mood m = Pick(v, Golden, Day, Dusk, Dawn, Night);
        var pal = Pick(v, ("#E8742A", "#C8461E", "Ember"), ("#F2B233", "#D9822B", "Amber"), ("#C8382E", "#8E2A2A", "Crimson"),
            ("#E89A3C", "#B5622C", "Copper"), ("#D9A640", "#9C6B2E", "Harvest"));
        var r = New(m);
        TintSky(r, "#FFCC88", 0.2f);
        r.plaza = S(SurfaceTexture.Planks, "#8A6A4E", 0.35f, 5f);
        r.inlay = S(SurfaceTexture.None, "#3A2A20", 0.5f);
        r.ring = S(SurfaceTexture.Noise, "#9C5A2E", 0.1f, 3f);
        r.curb = S(SurfaceTexture.Rock, "#6E655C", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.Noise, "#7E7A3E", 0.08f, 8f);
        r.mountainNear = C("#4E4234"); r.mountainFar = C("#9A8A86");
        r.props.Add(P(PropKind.RoundTree, 42, 34f, 62f, 0.9f, 1.4f, pal.Item1, pal.Item2, "#3A2A20"));
        r.props.Add(P(PropKind.RoundTree, 110, 62f, 150f, 1.3f, 2.2f, pal.Item2, "#6E5A2A", "#3A2A20"));
        r.props.Add(P(PropKind.Bush, 30, 31f, 60f, 0.7f, 1.2f, pal.Item1, "#7A6A2E", "#3A2A20"));
        r.props.Add(P(PropKind.Rock, 20, 33f, 120f, 0.6f, 1.8f, "#7A7068", "#7A7068", "#7A7068"));
        r.structure = StructureKind.StoneLanterns;
        r.structurePrimary = C("#7E7870");
        r.structureGlow = C("#FFA04A", 4f);
        r.accentLightIntensity = m.night ? 5f : 2f;
        r.particles = ParticleKind.Leaves;
        r.particleColor = C(pal.Item1);
        r.saturation = 14f;
        name = $"{pal.Item3} {m.name}";
        return r;
    }

    private static EnvironmentRecipe Lagoon(int v, out string name)
    {
        Mood m = Pick(v, Day, Golden, Dusk, Dawn, Night);
        var pal = Pick(v, ("#2FB8C8", "#FF5E8A"), ("#27A8C0", "#FFC23A"), ("#3AC0B0", "#FF7A3A"), ("#2E9ED8", "#C86AFF"), ("#1E7A9A", "#FFE04A"));
        var r = New(m);
        TintSky(r, "#9AF0FF", 0.2f);
        r.plaza = S(SurfaceTexture.Planks, "#C8A57A", 0.35f, 5f);
        r.inlay = S(SurfaceTexture.None, "#2C6E7A", 0.8f);
        r.ring = S(SurfaceTexture.Water, pal.Item1, 0.95f, 8f);
        r.curb = S(SurfaceTexture.Rock, "#E0CFA8", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.Noise, "#E6D2A2", 0.2f, 6f);
        r.hillHeight = 3f;
        r.mountainNear = C("#2E5E4A"); r.mountainFar = C("#7FA3A8"); r.mountainHeight = 0.8f;
        r.props.Add(P(PropKind.Palm, 40, 33f, 60f, 0.9f, 1.3f, "#3C8A3A", "#2E6E2E", "#8A6A48"));
        r.props.Add(P(PropKind.Palm, 60, 60f, 130f, 1.1f, 1.8f, "#3C8A3A", "#2E6E2E", "#8A6A48"));
        r.props.Add(P(PropKind.Bush, 25, 31f, 60f, 0.7f, 1.2f, "#3E9A4A", "#2F7A3A", "#2F7A3A"));
        r.props.Add(P(PropKind.Flowers, 25, 31f, 50f, 0.9f, 1.3f, pal.Item2, "#FFFFFF", "#2F7A3A"));
        r.props.Add(P(PropKind.Rock, 15, 33f, 100f, 0.6f, 1.6f, "#9C8C7A", "#9C8C7A", "#9C8C7A"));
        r.structure = StructureKind.Braziers;
        r.structurePrimary = C("#5A4030"); r.structureSecondary = C("#4A3426");
        r.structureGlow = C("#FF8A3A", 4f);
        r.accentLightIntensity = m.night ? 5f : 1.5f;
        r.particles = m.night ? ParticleKind.Fireflies : ParticleKind.GoldMotes;
        r.particleColor = m.night ? C("#C8FF8A", 3f) : C("#FFF0C0", 1.5f);
        r.fogDensity = 0.003f;
        r.saturation = 14f;
        name = m.name;
        return r;
    }

    private static EnvironmentRecipe Desert(int v, out string name)
    {
        Mood m = Pick(v, Golden, Day, Dusk, Night, Dawn);
        var r = New(m);
        TintSky(r, "#FFD9A0", 0.3f);
        r.plaza = S(SurfaceTexture.Flagstones, "#C9A27A", 0.35f);
        r.inlay = S(SurfaceTexture.None, Pick(v, "#2E7A8A", "#6B3A22", "#C9A04A", "#2E4A8A", "#8A2E2E"), 0.7f);
        r.ring = S(SurfaceTexture.SandRipples, "#E8C690", 0.15f, 2.5f);
        r.curb = S(SurfaceTexture.Rock, "#B08A62", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.Noise, "#D9B47E", 0.12f, 8f);
        r.hillHeight = 6f;
        r.mountains = MountainStyle.Dunes;
        r.mountainNear = C("#C99A62"); r.mountainFar = C("#E0B88A");
        r.props.Add(P(PropKind.Cactus, 28, 34f, 90f, 0.8f, 1.4f, "#4E7A3E", "#4E7A3E", "#4E7A3E"));
        r.props.Add(P(PropKind.Rock, 40, 33f, 170f, 1f, 3f, "#A87850", "#A87850", "#A87850"));
        r.props.Add(P(PropKind.DeadTree, 10, 40f, 110f, 0.9f, 1.4f, "#6E5038", "#6E5038", "#6E5038"));
        r.props.Add(P(PropKind.RuinColumn, 12, 36f, 70f, 0.9f, 1.3f, "#D8C3A0", "#D8C3A0", "#8A7A50"));
        r.structure = StructureKind.Obelisks;
        r.structurePrimary = C("#D9BE8C"); r.structureSecondary = C("#C99A3A");
        r.structureGlow = C("#FFC060", 3f);
        r.accentLightIntensity = m.night ? 5f : 0f;
        r.particles = ParticleKind.Dust;
        r.particleColor = new Color(0.91f, 0.79f, 0.63f, 0.35f);
        r.fogDensity = 0.004f;
        r.temperature += 4f;
        name = m.name;
        return r;
    }

    private static EnvironmentRecipe Cavern(int v, out string name)
    {
        var pal = Pick(v, ("#5FE0FF", "Azure"), ("#B07CFF", "Amethyst"), ("#4CFFA8", "Emerald"), ("#FF6FB5", "Rose Quartz"), ("#FFB347", "Amber"));
        Color glow = C(pal.Item1);
        var r = New(Night);
        r.skyTop = C("#05070D"); r.skyHorizon = Color.Lerp(C("#10283A"), glow * 0.2f, 0.4f); r.skyBottom = C("#030406");
        r.stars = 0.6f; r.cloudCoverage = 0f; r.sunGlow = 0.2f; r.sunDiscColor = Color.black;
        r.sunColor = Color.Lerp(C("#6FA8FF"), glow, 0.4f); r.sunIntensity = 0.35f; r.sunPitch = 60f;
        r.ambientIntensity = 0.65f;
        r.plaza = S(SurfaceTexture.Flagstones, "#4A4F5A", 0.55f);
        r.inlay = new SurfaceLayer(SurfaceTexture.None, glow, 0.8f) { emission = glow * 2.5f };
        r.ring = S(SurfaceTexture.Rock, "#2E333C", 0.35f, 3f);
        r.curb = S(SurfaceTexture.Rock, "#3A3F48", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.Rock, "#343842", 0.25f, 10f);
        r.hillHeight = 6f;
        r.mountains = MountainStyle.Walls;
        r.mountainNear = C("#1E242C"); r.mountainFar = C("#2C3644");
        r.props.Add(P(PropKind.Crystal, 34, 33f, 48f, 0.8f, 1.8f, pal.Item1, pal.Item1, "#3A4048").WithGlow(glow * 2.2f));
        r.props.Add(P(PropKind.Rock, 30, 32f, 48f, 0.8f, 2.4f, "#3A4048", "#3A4048", "#3A4048"));
        r.props.Add(P(PropKind.IceSpike, 14, 34f, 48f, 0.8f, 1.6f, "#3E444E", "#3E444E", "#30353E"));
        r.structure = StructureKind.CrystalPylons;
        r.structureCount = 6;
        r.structurePrimary = glow; r.structureSecondary = C("#2E333C");
        r.structureGlow = glow * 3f;
        r.accentLightColor = glow; r.accentLightIntensity = 6f;
        r.particles = ParticleKind.Spores;
        r.particleColor = glow * 2.5f;
        r.fogDensity = 0.008f;
        r.bloom = 1.2f; r.saturation = 15f;
        name = pal.Item2;
        return r;
    }

    private static EnvironmentRecipe Volcanic(int v, out string name)
    {
        var pal = Pick(v, ("#FF5A14", "Inferno"), ("#FF2A2A", "Crimson Forge"), ("#3AA0FF", "Sapphire Flame"), ("#7AFF3A", "Toxic Rift"), ("#FF3AA0", "Magenta Core"));
        Color lava = C(pal.Item1);
        var r = New(Dusk);
        r.skyTop = C("#120606"); r.skyHorizon = Color.Lerp(C("#5A1A0E"), lava * 0.5f, 0.5f); r.skyBottom = C("#1A0704");
        r.sunDiscColor = lava * 2f; r.sunColor = Color.Lerp(C("#FF7A4A"), lava, 0.5f); r.sunIntensity = 0.9f; r.sunPitch = 15f;
        r.cloudColor = new Color(0.23f, 0.1f, 0.08f, 0.9f); r.cloudCoverage = 0.5f; r.stars = 0.2f;
        r.ambientIntensity = 0.75f;
        r.plaza = S(SurfaceTexture.Flagstones, "#2A2624", 0.45f);
        r.inlay = new SurfaceLayer(SurfaceTexture.None, lava, 0.8f) { emission = lava * 3f };
        r.ring = new SurfaceLayer(SurfaceTexture.LavaCracks, C("#2A2220"), 0.35f, 5f) { emission = lava * 3.5f };
        r.curb = S(SurfaceTexture.Rock, "#3A3430", 0.3f, 2f);
        r.terrain = new SurfaceLayer(SurfaceTexture.LavaCracks, C("#2E2826"), 0.3f, 14f) { emission = lava * 1.8f };
        r.mountainNear = C("#1E1614"); r.mountainFar = Color.Lerp(C("#4A2A22"), lava * 0.3f, 0.3f); r.mountainHeight = 1.2f;
        r.props.Add(P(PropKind.DeadTree, 25, 34f, 120f, 0.9f, 1.6f, "#2A1E18", "#2A1E18", "#2A1E18"));
        r.props.Add(P(PropKind.Rock, 40, 33f, 170f, 1f, 3f, "#3A322E", "#3A322E", "#3A322E"));
        r.props.Add(P(PropKind.Crystal, 12, 34f, 70f, 0.8f, 1.5f, "#2A1A1A", "#2A1A1A", "#2A2220").WithGlow(lava * 2f));
        r.structure = StructureKind.Obelisks;
        r.structurePrimary = C("#1E1A1A"); r.structureSecondary = C("#2A2422");
        r.structureGlow = lava * 4f;
        r.accentLightColor = lava; r.accentLightIntensity = 6f;
        r.particles = ParticleKind.Embers;
        r.particleColor = lava * 3f;
        r.fogDensity = 0.006f;
        r.bloom = 1.2f; r.contrast = 14f;
        name = pal.Item2;
        return r;
    }

    private static EnvironmentRecipe Enchanted(int v, out string name)
    {
        var pal = Pick(v, ("#4CFFC8", "Moonlit"), ("#4C9CFF", "Bluebell"), ("#C06CFF", "Violet Dream"), ("#FFD24C", "Golden Glow"), ("#FF6CC8", "Faerie"));
        Color glow = C(pal.Item1);
        var r = New(v == 2 ? Dusk : Night);
        TintSky(r, pal.Item1, 0.25f);
        r.plaza = S(SurfaceTexture.Flagstones, "#5A6A6A", 0.4f);
        r.inlay = new SurfaceLayer(SurfaceTexture.None, glow, 0.8f) { emission = glow * 2f };
        r.ring = S(SurfaceTexture.Noise, "#2E4A34", 0.15f, 3f);
        r.curb = S(SurfaceTexture.Rock, "#4A5550", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.Noise, "#2E4A30", 0.1f, 8f);
        r.hillHeight = 10f;
        r.mountainNear = C("#18242C"); r.mountainFar = C("#3A4A66");
        r.props.Add(P(PropKind.Mushroom, 25, 31f, 55f, 0.8f, 1.6f, pal.Item1, "#E0E0F0", "#D8D0C0").WithGlow(glow * 2.5f));
        r.props.Add(P(PropKind.RoundTree, 45, 36f, 70f, 1f, 1.5f, "#1E4A44", "#2A5A4E", "#2A2A2A"));
        r.props.Add(P(PropKind.PineTree, 160, 70f, 160f, 1.2f, 2.6f, "#12261E", "#1A3226", "#2A2A2A"));
        r.props.Add(P(PropKind.Flowers, 20, 31f, 45f, 0.8f, 1.2f, pal.Item1, "#FFFFFF", "#2E4A34"));
        r.structure = StructureKind.StoneLanterns;
        r.structurePrimary = C("#6A7470");
        r.structureGlow = glow * 3.5f;
        r.accentLightColor = glow; r.accentLightIntensity = 5f;
        r.particles = ParticleKind.Fireflies;
        r.particleColor = glow * 3f;
        r.fogDensity = 0.006f;
        r.bloom = 1.2f; r.saturation = 14f;
        name = pal.Item2;
        return r;
    }

    private static EnvironmentRecipe Aegean(int v, out string name)
    {
        Mood m = Pick(v, Day, Golden, Dusk, Dawn, Night);
        var pal = Pick(v, ("#2C5A8A", "#FF6A9A"), ("#C9A04A", "#FFFFFF"), ("#2C7A8A", "#FF8A3A"), ("#8A2C5A", "#FFD24A"), ("#2C4A8A", "#C8A0FF"));
        var r = New(m);
        TintSky(r, "#A8D8FF", 0.15f);
        r.plaza = S(SurfaceTexture.MarbleTiles, "#EDE8E0", 0.85f, 5f);
        r.inlay = S(SurfaceTexture.None, pal.Item1, 0.85f);
        r.ring = S(SurfaceTexture.StoneTiles, "#D8D0C2", 0.4f, 4f);
        r.curb = S(SurfaceTexture.MarbleTiles, "#E6E0D6", 0.7f, 3f);
        r.terrain = S(SurfaceTexture.Noise, "#9AA86A", 0.08f, 8f);
        r.mountainNear = C("#5E6A5A"); r.mountainFar = C("#A0AEC0");
        r.props.Add(P(PropKind.Cypress, 34, 40f, 65f, 0.9f, 1.4f, "#2E4A2E", "#3A5A34", "#4A3A2A"));
        r.props.Add(P(PropKind.RoundTree, 30, 45f, 90f, 0.8f, 1.2f, "#8A9A6A", "#6E7E54", "#5A4A3A"));
        r.props.Add(P(PropKind.Cypress, 80, 90f, 160f, 1.4f, 2.4f, "#2E4A2E", "#3A5A34", "#4A3A2A"));
        r.props.Add(P(PropKind.Flowers, 16, 31f, 40f, 0.9f, 1.3f, pal.Item2, "#FFFFFF", "#3A6A3A"));
        r.structure = StructureKind.Columns;
        r.structureCount = 12; r.structureRadius = 36f;
        r.structurePrimary = C("#F0EBE3"); r.structureSecondary = C("#D4AF5A");
        if (m.night) { r.structureGlow = C("#FFB060", 4f); r.accentLightIntensity = 5f; }
        r.particles = ParticleKind.GoldMotes;
        r.particleColor = C("#FFE0A0", 2f);
        name = m.name;
        return r;
    }

    private static EnvironmentRecipe Bamboo(int v, out string name)
    {
        Mood m = Pick(v, Day, Dawn, Golden, Dusk, Night);
        var pal = Pick(v, ("#7EA84A", "#5E7E36", "#6E9A3E", "Jade"), ("#8AB45A", "#6A8A40", "#7AA64A", "Morning Mist"),
            ("#C8B24A", "#9A8A36", "#8AA03E", "Golden Bamboo"), ("#5E8A44", "#3E5E2E", "#5A8A3A", "Shadow"), ("#4A6A3A", "#2E3A2A", "#4A7A3A", "Moonlit"));
        var r = New(m);
        TintSky(r, "#D8F0D0", 0.2f);
        r.plaza = S(SurfaceTexture.Planks, "#9C7A54", 0.35f, 5f);
        r.inlay = S(SurfaceTexture.None, "#2E2E2E", 0.6f);
        r.ring = S(SurfaceTexture.RakedGravel, "#DCD8CC", 0.15f, 2.2f);
        r.curb = S(SurfaceTexture.Rock, "#7A7A72", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.Noise, "#5E7E3E", 0.08f, 8f);
        r.mountainNear = C("#3A5244"); r.mountainFar = C("#8AA0A0"); r.mountainHeight = 1.15f;
        r.props.Add(P(PropKind.Bamboo, 60, 33f, 70f, 0.9f, 1.3f, pal.Item1, pal.Item3, pal.Item2));
        r.props.Add(P(PropKind.Bamboo, 70, 70f, 125f, 1.1f, 1.6f, pal.Item1, pal.Item3, pal.Item2));
        r.props.Add(P(PropKind.Rock, 15, 33f, 80f, 0.6f, 1.6f, "#7A7A72", "#7A7A72", "#7A7A72"));
        r.props.Add(P(PropKind.Bush, 20, 31f, 50f, 0.7f, 1.1f, pal.Item3, pal.Item1, "#3A3A2A"));
        r.structure = StructureKind.StoneLanterns;
        r.structurePrimary = C("#86847C");
        r.structureGlow = C("#FFB46A", 4f);
        r.accentLightIntensity = m.night ? 5f : 2f;
        r.particles = m.night ? ParticleKind.Fireflies : ParticleKind.Leaves;
        r.particleColor = m.night ? C("#D8FF8A", 3f) : C("#9ACB5A");
        r.particleRate = 0.6f;
        r.fogDensity = 0.006f;
        name = pal.Item4;
        return r;
    }

    private static EnvironmentRecipe Meadow(int v, out string name)
    {
        Mood m = Pick(v, Day, Golden, Dawn, Dusk, Night);
        var pal = Pick(v, ("#FF5E7E", "#FFD23A"), ("#9A6AFF", "#FFFFFF"), ("#FF8A3A", "#FF3A5A"), ("#3AA0FF", "#FFE14A"), ("#FF6AC8", "#C8FF6A"));
        var r = New(m);
        r.plaza = S(SurfaceTexture.StoneTiles, "#B8B2A6", 0.4f);
        r.inlay = S(SurfaceTexture.None, "#5A5448", 0.6f);
        r.ring = S(SurfaceTexture.Noise, "#6E9E4A", 0.08f, 3f);
        r.curb = S(SurfaceTexture.Rock, "#8A847A", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.Noise, "#6E9A44", 0.08f, 8f);
        r.hillHeight = 14f;
        r.mountainNear = C("#4E6070"); r.mountainFar = C("#A8B4C8");
        r.snowCaps = true; r.mountainHeight = 1.2f;
        r.props.Add(P(PropKind.RoundTree, 22, 38f, 80f, 0.9f, 1.4f, "#4E8A34", "#3E7A2C", "#3A2A20"));
        r.props.Add(P(PropKind.Flowers, 60, 31f, 85f, 0.9f, 1.4f, pal.Item1, pal.Item2, "#3E7A2C"));
        r.props.Add(P(PropKind.Bush, 30, 31f, 70f, 0.7f, 1.2f, "#4E8A34", "#5E9A3A", "#3A2A20"));
        r.props.Add(P(PropKind.Rock, 25, 33f, 140f, 0.6f, 2f, "#8A867E", "#8A867E", "#8A867E"));
        r.props.Add(P(PropKind.PineTree, 120, 85f, 170f, 1.3f, 2.6f, "#24452E", "#2E5236", "#3A2A20"));
        r.structure = StructureKind.Monoliths;
        r.structureCount = 8; r.structureRadius = 34f;
        r.structurePrimary = C("#8E8A80");
        if (m.night) { r.structureGlow = C(pal.Item1, 3f); r.accentLightColor = C(pal.Item1); r.accentLightIntensity = 4f; }
        r.particles = m.night ? ParticleKind.Fireflies : ParticleKind.GoldMotes;
        r.particleColor = m.night ? C("#E8FF8A", 3f) : C("#FFF8D0", 1.5f);
        r.saturation = 14f;
        name = m.name;
        return r;
    }

    private static EnvironmentRecipe IceKingdom(int v, out string name)
    {
        Mood m = Pick(v, Night, Day, Dusk, Dawn, Golden);
        var pal = Pick(v, ("#6AD0FF", "Aurora"), ("#9AE8FF", "Glacier"), ("#B08AFF", "Frostfire"), ("#5AFFE0", "Polar"), ("#FFD08A", "Frozen Sun"));
        Color glow = C(pal.Item1);
        var r = New(m);
        TintSky(r, "#BFE8FF", 0.3f);
        r.plaza = S(SurfaceTexture.MarbleTiles, "#DDEBFA", 0.9f, 5f);
        r.inlay = new SurfaceLayer(SurfaceTexture.None, glow, 0.9f) { emission = glow * 2f };
        r.ring = S(SurfaceTexture.Water, "#A8D8F0", 0.98f, 10f);
        r.curb = S(SurfaceTexture.MarbleTiles, "#CFE2F5", 0.8f, 3f);
        r.terrain = S(SurfaceTexture.Snow, "#E6F0FA", 0.4f, 8f);
        r.hillHeight = 8f;
        r.mountainNear = C("#6E8AA8"); r.mountainFar = C("#B8CCE6");
        r.snowCaps = true; r.mountainHeight = 1.4f;
        r.props.Add(P(PropKind.IceSpike, 36, 33f, 70f, 0.8f, 1.5f, "#A8E0FF", "#A8E0FF", "#8AA0B8"));
        r.props.Add(P(PropKind.Crystal, 16, 34f, 60f, 0.8f, 1.4f, "#BFF0FF", "#BFF0FF", "#8AA0B8").WithGlow(glow * 2f));
        r.props.Add(P(PropKind.SnowPine, 120, 70f, 160f, 1.3f, 2.6f, "#1E3A40", "#27474E", "#3A2A22"));
        r.props.Add(P(PropKind.Rock, 15, 33f, 100f, 0.6f, 1.8f, "#8AA0B8", "#8AA0B8", "#8AA0B8"));
        r.structure = StructureKind.CrystalPylons;
        r.structureCount = 6;
        r.structurePrimary = C("#BFEFFF"); r.structureSecondary = C("#8AA0B8");
        r.structureGlow = glow * 3f;
        r.accentLightColor = glow; r.accentLightIntensity = m.night ? 6f : 3f;
        r.particles = ParticleKind.Snow;
        r.particleColor = C("#FFFFFF", 1.3f);
        r.temperature -= 6f;
        r.fogDensity = 0.0045f;
        name = pal.Item2;
        return r;
    }

    private static EnvironmentRecipe Jungle(int v, out string name)
    {
        Mood m = Pick(v, Day, Golden, Dawn, Dusk, Night);
        var pal = Pick(v, ("#FF4A6A", "#FFB43A"), ("#FF7A2A", "#FFE04A"), ("#C84AFF", "#FF4AA0"), ("#FF3A3A", "#FFFFFF"), ("#4AC8FF", "#FFD04A"));
        var r = New(m);
        TintSky(r, "#C8FFC0", 0.2f);
        r.plaza = S(SurfaceTexture.Flagstones, "#8A8A72", 0.35f);
        r.inlay = S(SurfaceTexture.None, "#C9A04A", 0.8f);
        r.inlay.metallic = 0.8f;
        r.ring = S(SurfaceTexture.Noise, "#4A6A34", 0.12f, 3f);
        r.curb = S(SurfaceTexture.Rock, "#6A6A5A", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.Noise, "#3E6A2E", 0.1f, 8f);
        r.hillHeight = 10f;
        r.mountainNear = C("#2A4A34"); r.mountainFar = C("#6E8A80");
        r.props.Add(P(PropKind.RoundTree, 40, 36f, 64f, 1f, 1.6f, "#2E6A2A", "#3E7E30", "#4A3A2A"));
        r.props.Add(P(PropKind.Palm, 25, 36f, 75f, 1f, 1.5f, "#3E8A34", "#2E6A2A", "#6A5A3A"));
        r.props.Add(P(PropKind.Bush, 40, 31f, 60f, 0.8f, 1.4f, "#3E7E30", "#2E6A2A", "#2E6A2A"));
        r.props.Add(P(PropKind.RuinColumn, 16, 33f, 55f, 0.9f, 1.3f, "#9A9A86", "#9A9A86", "#4A7A34"));
        r.props.Add(P(PropKind.Flowers, 16, 31f, 45f, 0.9f, 1.3f, pal.Item1, pal.Item2, "#2E6A2A"));
        r.props.Add(P(PropKind.RoundTree, 90, 64f, 150f, 1.4f, 2.4f, "#24561E", "#2E6626", "#4A3A2A"));
        r.structure = StructureKind.Arches;
        r.structurePrimary = C("#8A8676"); r.structureSecondary = C("#C9A04A");
        r.particles = m.night ? ParticleKind.Fireflies : ParticleKind.Spores;
        r.particleColor = m.night ? C("#C8FF6A", 3f) : C("#E0FFB0", 1.2f);
        r.fogDensity = 0.006f;
        r.saturation = 14f;
        name = m.name;
        return r;
    }

    private static EnvironmentRecipe Canyon(int v, out string name)
    {
        Mood m = Pick(v, Golden, Day, Dusk, Dawn, Night);
        var r = New(m);
        TintSky(r, "#FFB080", 0.25f);
        r.plaza = S(SurfaceTexture.Flagstones, "#B07A54", 0.35f);
        r.inlay = S(SurfaceTexture.None, Pick(v, "#2E8A9A", "#1E6E7A", "#C9A04A", "#3A9A8A", "#4A6AC8"), 0.8f);
        r.ring = S(SurfaceTexture.SandRipples, "#D29A64", 0.15f, 2.5f);
        r.curb = S(SurfaceTexture.Rock, "#9A6440", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.Noise, "#C07E4E", 0.1f, 8f);
        r.hillHeight = 8f;
        r.mountains = MountainStyle.Mesas;
        r.mountainNear = C("#A0522D"); r.mountainFar = C("#D9946A");
        r.props.Add(P(PropKind.Cactus, 20, 34f, 90f, 0.8f, 1.3f, "#5A7A3E", "#5A7A3E", "#5A7A3E"));
        r.props.Add(P(PropKind.Rock, 50, 33f, 180f, 1f, 3.5f, "#A0603A", "#A0603A", "#A0603A"));
        r.props.Add(P(PropKind.DeadTree, 8, 40f, 100f, 0.9f, 1.3f, "#5A4030", "#5A4030", "#5A4030"));
        r.props.Add(P(PropKind.Bush, 24, 31f, 70f, 0.6f, 1f, "#8A8A4A", "#6E7A3A", "#6E7A3A"));
        r.structure = StructureKind.Braziers;
        r.structurePrimary = C("#6A4A3A"); r.structureSecondary = C("#4A3428");
        r.structureGlow = C("#FF8A3A", 4f);
        r.accentLightIntensity = m.night ? 5f : 1.5f;
        r.particles = ParticleKind.Dust;
        r.particleColor = new Color(0.88f, 0.63f, 0.44f, 0.3f);
        r.fogDensity = 0.003f;
        r.temperature += 3f;
        name = m.name;
        return r;
    }

    private static EnvironmentRecipe Mushrooms(int v, out string name)
    {
        Mood m = Pick(v, Dusk, Night, Day, Golden, Dawn);
        var pal = Pick(v, ("#E04A4A", "#FFFFFF", "Toadstool"), ("#9A4AE0", "#E0B0FF", "Mystic"), ("#4AA0E0", "#A0F0FF", "Azure Glade"),
            ("#E0A04A", "#FFE0A0", "Amber Glade"), ("#E04AA0", "#FFB0E0", "Bloom"));
        Color glow = C(pal.Item2);
        var r = New(m);
        TintSky(r, "#D0A0FF", 0.3f);
        r.plaza = S(SurfaceTexture.Flagstones, "#7A6E8A", 0.4f);
        r.inlay = new SurfaceLayer(SurfaceTexture.None, glow, 0.8f) { emission = glow * 1.5f };
        r.ring = S(SurfaceTexture.Noise, "#4A6A3E", 0.12f, 3f);
        r.curb = S(SurfaceTexture.Rock, "#5A5468", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.Noise, "#4E6E3A", 0.1f, 8f);
        r.hillHeight = 9f;
        r.mountainNear = C("#2E2A44"); r.mountainFar = C("#6A6090");
        r.props.Add(P(PropKind.Mushroom, 32, 33f, 60f, 1.2f, 2.4f, pal.Item1, "#F2ECE0", "#E8E0D0").WithGlow(glow * 2.5f));
        r.props.Add(P(PropKind.Mushroom, 36, 60f, 115f, 2.2f, 4f, pal.Item1, pal.Item1, "#E8E0D0").WithGlow(glow * 2f));
        r.props.Add(P(PropKind.Flowers, 25, 31f, 50f, 0.8f, 1.2f, pal.Item2, pal.Item1, "#3E6A44"));
        r.props.Add(P(PropKind.Bush, 20, 31f, 60f, 0.7f, 1.2f, "#3E6A44", "#4E7A4A", "#3E6A44"));
        r.structure = StructureKind.Obelisks;
        r.structurePrimary = C("#4A4458"); r.structureSecondary = C("#3A3448");
        r.structureGlow = glow * 3f;
        r.accentLightColor = glow; r.accentLightIntensity = 4f;
        r.particles = ParticleKind.Spores;
        r.particleColor = glow * 2.5f;
        r.fogDensity = 0.0055f;
        r.bloom = Mathf.Max(r.bloom, 0.9f); r.saturation = 15f;
        name = pal.Item3;
        return r;
    }

    private static EnvironmentRecipe Royal(int v, out string name)
    {
        Mood m = Pick(v, Day, Golden, Dusk, Dawn, Night);
        var pal = Pick(v, ("#E0304A", "#FFFFFF"), ("#FFFFFF", "#FFC0D0"), ("#FF8A3A", "#FFE04A"), ("#C04AE0", "#FFFFFF"), ("#E0304A", "#FFD04A"));
        var r = New(m);
        r.plaza = S(SurfaceTexture.MarbleTiles, "#F2EEE6", 0.85f, 5f);
        r.inlay = S(SurfaceTexture.None, "#D4AF37", 0.85f);
        r.inlay.metallic = 0.9f;
        r.ring = S(SurfaceTexture.RakedGravel, "#ECE6DA", 0.15f, 2.2f);
        r.curb = S(SurfaceTexture.MarbleTiles, "#E6E0D6", 0.7f, 3f);
        r.terrain = S(SurfaceTexture.Noise, "#5E8E3E", 0.08f, 6f);
        r.hillHeight = 3f;
        r.mountainNear = C("#4E6A5A"); r.mountainFar = C("#A0B0C0"); r.mountainHeight = 0.7f;
        r.props.Add(P(PropKind.Topiary, 40, 33f, 55f, 0.9f, 1.2f, "#2E6A2E", "#3A7A34", "#E6E0D6"));
        r.props.Add(P(PropKind.Flowers, 40, 31f, 55f, 0.9f, 1.3f, pal.Item1, pal.Item2, "#2E5A2E"));
        r.props.Add(P(PropKind.Cypress, 30, 55f, 90f, 1f, 1.6f, "#2E4A2E", "#3A5A34", "#4A3A2A"));
        r.props.Add(P(PropKind.RoundTree, 60, 90f, 160f, 1.3f, 2.2f, "#3E6E34", "#4A7A3A", "#4A3A2A"));
        r.structure = StructureKind.Braziers;
        r.structurePrimary = C("#E6E0D6"); r.structureSecondary = C("#D4AF37");
        r.structureGlow = C("#FFC060", 4f);
        r.accentLightIntensity = m.night ? 5f : 2f;
        r.particles = ParticleKind.GoldMotes;
        r.particleColor = C("#FFD27A", 2f);
        name = m.name;
        return r;
    }

    private static EnvironmentRecipe Fjord(int v, out string name)
    {
        Mood m = Pick(v, Dawn, Day, Dusk, Golden, Night);
        var r = New(m);
        TintSky(r, "#C0D8E8", 0.3f);
        r.plaza = S(SurfaceTexture.Planks, "#6E5A48", 0.35f, 5f);
        r.inlay = S(SurfaceTexture.None, "#2A2A2A", 0.6f);
        r.ring = S(SurfaceTexture.Water, Pick(v, "#3E6A7A", "#2E6A8A", "#4A5A7A", "#4A6A6A", "#1E3A4A"), 0.96f, 8f);
        r.curb = S(SurfaceTexture.Rock, "#5A6066", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.Noise, "#4E6A44", 0.08f, 8f);
        r.hillHeight = 12f;
        r.mountainNear = C("#2E3E4A"); r.mountainFar = C("#7A8AA0");
        r.snowCaps = true; r.mountainHeight = 1.5f;
        r.props.Add(P(PropKind.PineTree, 60, 34f, 80f, 1f, 1.6f, "#1E3A30", "#2A4A38", "#3A2A22"));
        r.props.Add(P(PropKind.PineTree, 200, 80f, 170f, 1.4f, 2.8f, "#1E3A30", "#2A4A38", "#3A2A22"));
        r.props.Add(P(PropKind.Rock, 30, 33f, 140f, 0.8f, 2.4f, "#6A7078", "#6A7078", "#6A7078"));
        r.props.Add(P(PropKind.DeadTree, 6, 36f, 70f, 0.9f, 1.3f, "#4A4440", "#4A4440", "#4A4440"));
        r.structure = StructureKind.Braziers;
        r.structurePrimary = C("#3A3A3A"); r.structureSecondary = C("#2A2A2A");
        r.structureGlow = C("#FF9A4A", 4f);
        r.accentLightIntensity = m.night ? 5f : 2.5f;
        r.particles = m.night ? ParticleKind.Fireflies : ParticleKind.GoldMotes;
        r.particleColor = m.night ? C("#FFD08A", 2.5f) : C("#FFFFFF", 0.8f);
        r.particleRate = 0.6f;
        r.fogDensity = 0.007f;
        name = m.name;
        return r;
    }

    private static EnvironmentRecipe Savanna(int v, out string name)
    {
        Mood m = Pick(v, Golden, Dusk, Day, Dawn, Night);
        var r = New(m);
        TintSky(r, "#FFB060", 0.3f);
        r.sunSize = 0.09f;
        r.plaza = S(SurfaceTexture.Flagstones, "#A88A60", 0.35f);
        r.inlay = S(SurfaceTexture.None, "#6A3A22", 0.6f);
        r.ring = S(SurfaceTexture.Noise, "#C8A060", 0.1f, 3f);
        r.curb = S(SurfaceTexture.Rock, "#8A6A4A", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.Noise, "#C29A58", 0.08f, 8f);
        r.hillHeight = 5f;
        r.mountainNear = C("#8A5A3E"); r.mountainFar = C("#C08A6A"); r.mountainHeight = 0.6f;
        r.props.Add(P(PropKind.Acacia, 26, 36f, 130f, 1.2f, 2f, Pick(v, "#6E7A34", "#7A8A3A", "#5E6E2E", "#8A8A3E", "#4E5E2A"), "#5A6A2E", "#4A3628"));
        r.props.Add(P(PropKind.Bush, 25, 31f, 90f, 0.7f, 1.2f, "#8A8A4A", "#7A7A3E", "#7A7A3E"));
        r.props.Add(P(PropKind.Rock, 20, 33f, 150f, 0.8f, 2.5f, "#9A7A5A", "#9A7A5A", "#9A7A5A"));
        r.props.Add(P(PropKind.DeadTree, 6, 40f, 120f, 1f, 1.5f, "#5A4434", "#5A4434", "#5A4434"));
        r.structure = StructureKind.Braziers;
        r.structurePrimary = C("#5A3A2A"); r.structureSecondary = C("#3A2A20");
        r.structureGlow = C("#FF9A3A", 4f);
        r.accentLightIntensity = m.night ? 5f : 1.5f;
        r.particles = ParticleKind.Dust;
        r.particleColor = new Color(0.91f, 0.69f, 0.44f, 0.25f);
        r.fogDensity = 0.0035f;
        r.temperature += 5f;
        name = m.name;
        return r;
    }

    private static EnvironmentRecipe Reef(int v, out string name)
    {
        var pal = Pick(v, ("#FF6A5A", "#FFB03A", "Sunlit Reef"), ("#FF5AB0", "#B05AFF", "Neon Reef"), ("#5AFFB0", "#FFE05A", "Lagoon Reef"),
            ("#FF8A3A", "#5AB0FF", "Deep Reef"), ("#E05AFF", "#FF5A8A", "Abyss Glow"));
        bool deep = v >= 3;
        var r = New(Day);
        r.skyTop = deep ? C("#2A7AA8") : C("#6AD0F0");
        r.skyHorizon = deep ? C("#0E3A60") : C("#1E7AA8");
        r.skyBottom = C("#062A44");
        r.sunDiscColor = C("#CFF6FF", 1.2f); r.sunColor = C("#9ADFFF"); r.sunIntensity = deep ? 0.8f : 1.15f; r.sunPitch = 70f;
        r.cloudColor = new Color(0.75f, 0.94f, 1f, 0.4f); r.cloudCoverage = 0.5f; r.stars = 0f; r.sunGlow = 1.5f;
        r.ambientIntensity = 1f;
        r.plaza = S(SurfaceTexture.Flagstones, "#8AA8A0", 0.4f);
        r.inlay = S(SurfaceTexture.None, "#E0C060", 0.8f);
        r.ring = S(SurfaceTexture.SandRipples, "#D8D0B0", 0.15f, 2.5f);
        r.curb = S(SurfaceTexture.Rock, "#7A8A86", 0.3f, 2f);
        r.terrain = S(SurfaceTexture.SandRipples, "#C8C0A0", 0.12f, 6f);
        r.hillHeight = 6f;
        r.mountainNear = C("#1E4A5A"); r.mountainFar = C("#2E6A80"); r.mountainHeight = 0.8f;
        Color glow = deep ? C(pal.Item1, 1.5f) : Color.black;
        r.props.Add(P(PropKind.Coral, 45, 32f, 70f, 0.9f, 1.6f, pal.Item1, pal.Item2, "#C8C0A0").WithGlow(glow));
        r.props.Add(P(PropKind.Kelp, 40, 34f, 90f, 0.9f, 1.4f, "#3A7A3A", "#5A8A34", "#3A7A3A"));
        r.props.Add(P(PropKind.Rock, 30, 33f, 120f, 0.8f, 2.4f, "#6A7A76", "#6A7A76", "#6A7A76"));
        r.props.Add(P(PropKind.Coral, 50, 70f, 140f, 1.4f, 2.4f, pal.Item2, pal.Item1, "#C8C0A0"));
        r.structure = StructureKind.Arches;
        r.structurePrimary = C("#8AA8A0"); r.structureSecondary = C("#C9A04A");
        r.particles = ParticleKind.Bubbles;
        r.particleColor = C("#BFF6FF", 1.5f);
        r.fogDensity = deep ? 0.016f : 0.012f;
        r.temperature = -4f; r.saturation = 12f; r.bloom = deep ? 1f : 0.6f;
        name = pal.Item3;
        return r;
    }

    private static EnvironmentRecipe Clouds(int v, out string name)
    {
        Mood m = Pick(v, Day, Golden, Dusk, Dawn, Night);
        var r = New(m);
        r.skyBottom = Color.Lerp(r.skyHorizon, Color.white, m.night ? 0.1f : 0.6f);
        r.plaza = S(SurfaceTexture.MarbleTiles, "#F5F2EA", 0.85f, 5f);
        r.inlay = S(SurfaceTexture.None, "#D4AF37", 0.85f);
        r.inlay.metallic = 0.9f;
        r.ring = S(SurfaceTexture.Noise, "#F4F6FA", 0.1f, 4f);
        r.curb = S(SurfaceTexture.MarbleTiles, "#EEE9E0", 0.7f, 3f);
        r.terrain = S(SurfaceTexture.Noise, "#F2F4F8", 0.1f, 10f);
        r.hillHeight = 4f;
        r.mountainNear = C("#E8EEF8"); r.mountainFar = C("#F4F7FC"); r.mountainHeight = 0.5f;
        r.props.Add(P(PropKind.CloudPuff, 40, 34f, 90f, 0.8f, 1.4f, "#FFFFFF", "#DCE4F0", "#FFFFFF"));
        r.props.Add(P(PropKind.CloudPuff, 40, 90f, 200f, 2f, 4f, "#FFFFFF", "#DCE4F0", "#FFFFFF"));
        r.props.Add(P(PropKind.FloatingIsland, 14, 40f, 110f, 0.9f, 1.6f, "#6EAA4A", Pick(v, "#FFB0CC", "#FFD27A", "#B0E0FF", "#C8A0FF", "#A0FFC8"), "#8A7A6A").Floating(14f));
        r.structure = StructureKind.Columns;
        r.structureCount = 8; r.structureRadius = 36f;
        r.structurePrimary = C("#F5F2EA"); r.structureSecondary = C("#D4AF37");
        r.structureGlow = C("#FFD27A", m.night ? 4f : 2f);
        r.accentLightIntensity = m.night ? 5f : 0f;
        r.particles = ParticleKind.GoldMotes;
        r.particleColor = C("#FFE8B0", 2f);
        r.fogDensity = 0.003f;
        name = m.name;
        return r;
    }
}
