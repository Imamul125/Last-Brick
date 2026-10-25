using UnityEngine;
using System.Collections.Generic;

public enum SurfaceTexture { None, StoneTiles, MarbleTiles, Flagstones, Planks, RakedGravel, SandRipples, Noise, Rock, LavaCracks, Water, Snow }

public enum MountainStyle { None, Peaks, Mesas, Dunes, Walls }

public enum PropKind
{
    RoundTree, PineTree, SnowPine, Cypress, Palm, Acacia, DeadTree, Bamboo, Bush, Rock, Crystal, Mushroom,
    Cactus, RuinColumn, Coral, Kelp, Topiary, Flowers, IceSpike, CloudPuff, FloatingIsland
}

public enum StructureKind { None, Torii, Columns, StoneLanterns, Obelisks, Braziers, Monoliths, CrystalPylons, Arches }

public enum ParticleKind { None, Snow, Petals, Leaves, Fireflies, Embers, GoldMotes, Bubbles, Spores, Dust }

[System.Serializable]
public class SurfaceLayer
{
    public SurfaceTexture texture = SurfaceTexture.StoneTiles;
    public Color color = Color.white;
    [Range(0f, 1f)] public float smoothness = 0.3f;
    [Range(0f, 1f)] public float metallic = 0f;
    [Tooltip("World units covered by one texture repeat.")]
    public float textureScale = 6f;
    [ColorUsage(false, true)] public Color emission = Color.black;

    public SurfaceLayer() { }

    public SurfaceLayer(SurfaceTexture texture, Color color, float smoothness, float textureScale = 6f)
    {
        this.texture = texture;
        this.color = color;
        this.smoothness = smoothness;
        this.textureScale = textureScale;
    }
}

[System.Serializable]
public class PropLayer
{
    public PropKind kind = PropKind.RoundTree;
    public int count = 20;
    public float minRadius = 36f;
    public float maxRadius = 60f;
    public float minScale = 0.9f;
    public float maxScale = 1.3f;
    public Color colorA = new Color(0.3f, 0.6f, 0.3f);
    public Color colorB = new Color(0.25f, 0.5f, 0.25f);
    [Tooltip("Trunks, stems, rock caps, snow - depends on the prop.")]
    public Color secondary = new Color(0.3f, 0.2f, 0.15f);
    [ColorUsage(false, true)] public Color glow = Color.black;
    [Tooltip("Extra height above the terrain (floating islands, clouds).")]
    public float heightOffset = 0f;

    public PropLayer() { }

    public PropLayer(PropKind kind, int count, float minRadius, float maxRadius, float minScale, float maxScale,
        Color colorA, Color colorB, Color secondary)
    {
        this.kind = kind;
        this.count = count;
        this.minRadius = minRadius;
        this.maxRadius = maxRadius;
        this.minScale = minScale;
        this.maxScale = maxScale;
        this.colorA = colorA;
        this.colorB = colorB;
        this.secondary = secondary;
    }

    public PropLayer WithGlow(Color hdrGlow) { glow = hdrGlow; return this; }
    public PropLayer Floating(float height) { heightOffset = height; return this; }
}

/// <summary>
/// Data-only description of an environment. The runtime EnvironmentGenerator turns it into geometry using the
/// shared EnvironmentLibrary, so 100 environments cost almost nothing in build size or memory.
/// </summary>
[System.Serializable]
public class EnvironmentRecipe
{
    public int seed = 1;

    [Header("Sky")]
    public Color skyTop = new Color(0.25f, 0.5f, 0.95f);
    public Color skyHorizon = new Color(0.8f, 0.9f, 1f);
    public Color skyBottom = new Color(0.4f, 0.45f, 0.5f);
    [Range(0.5f, 8f)] public float horizonSharpness = 2.5f;
    [ColorUsage(false, true)] public Color sunDiscColor = new Color(1f, 0.9f, 0.7f);
    [Range(0f, 0.3f)] public float sunSize = 0.05f;
    [Range(0f, 4f)] public float sunGlow = 1f;
    [Range(0f, 3f)] public float stars = 0f;
    public Color cloudColor = new Color(1f, 1f, 1f, 0.8f);
    [Range(0f, 1f)] public float cloudCoverage = 0.3f;

    [Header("Atmosphere")]
    [Tooltip("Exponential squared fog. ~0.0035 shows layered mountains, higher = mistier.")]
    public float fogDensity = 0.0035f;
    public float ambientIntensity = 1f;

    [Header("Sun Light")]
    public float sunPitch = 35f;
    public float sunYaw = -40f;
    public Color sunColor = new Color(1f, 0.95f, 0.85f);
    public float sunIntensity = 1.5f;
    [Range(0f, 1f)] public float shadowStrength = 0.8f;

    [Header("Ground")]
    public SurfaceLayer plaza = new SurfaceLayer(SurfaceTexture.StoneTiles, new Color(0.8f, 0.78f, 0.74f), 0.4f);
    public SurfaceLayer inlay = new SurfaceLayer(SurfaceTexture.None, new Color(0.2f, 0.2f, 0.2f), 0.6f);
    public SurfaceLayer ring = new SurfaceLayer(SurfaceTexture.RakedGravel, new Color(0.9f, 0.88f, 0.85f), 0.15f, 2.2f);
    public SurfaceLayer curb = new SurfaceLayer(SurfaceTexture.Rock, new Color(0.6f, 0.58f, 0.55f), 0.3f, 2f);
    public SurfaceLayer terrain = new SurfaceLayer(SurfaceTexture.Noise, new Color(0.35f, 0.5f, 0.25f), 0.08f, 8f);
    public float hillHeight = 9f;

    [Header("Mountains")]
    public MountainStyle mountains = MountainStyle.Peaks;
    public Color mountainNear = new Color(0.25f, 0.32f, 0.32f);
    public Color mountainFar = new Color(0.55f, 0.6f, 0.7f);
    public bool snowCaps = false;
    public Color snowColor = new Color(0.95f, 0.97f, 1f);
    public float mountainHeight = 1f;

    [Header("Props")]
    public List<PropLayer> props = new List<PropLayer>();

    [Header("Structures (ring around the garden)")]
    public StructureKind structure = StructureKind.None;
    public int structureCount = 8;
    public float structureRadius = 33f;
    public Color structurePrimary = new Color(0.8f, 0.78f, 0.74f);
    public Color structureSecondary = new Color(0.85f, 0.65f, 0.3f);
    [ColorUsage(false, true)] public Color structureGlow = Color.black;
    public Color accentLightColor = new Color(1f, 0.6f, 0.3f);
    public float accentLightIntensity = 0f;

    [Header("Particles")]
    public ParticleKind particles = ParticleKind.None;
    [ColorUsage(true, true)] public Color particleColor = Color.white;
    public float particleRate = 1f;

    [Header("Post Processing")]
    public float bloom = 0.7f;
    public float postExposure = 0.2f;
    public float contrast = 10f;
    public float saturation = 8f;
    public float temperature = 0f;
    public float tint = 0f;
    public float vignette = 0.22f;
    public Color vignetteColor = new Color(0.1f, 0.05f, 0.05f);
}
