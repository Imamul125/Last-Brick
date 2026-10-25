using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One saved environment: visual prefab (floor, props, lights) plus the scene-wide
/// render settings (skybox, ambient, fog, post-processing) that go with it.
/// Create via Assets > Create > Last Brick > Environment Profile, or generate one
/// with Tools > Last Brick > Environments.
/// </summary>
[CreateAssetMenu(fileName = "EnvironmentProfile", menuName = "Last Brick/Environment Profile")]
public class EnvironmentProfile : ScriptableObject
{
    [Header("Info")]
    public string displayName = "New Environment";

    [Header("Recipe (generated at runtime - tiny build size)")]
    [Tooltip("When on, the environment is generated from the recipe below using the shared EnvironmentLibrary, and the prefab/sky/ambient/fog/post fields are ignored.")]
    public bool useRecipe = false;
    public EnvironmentRecipe recipe = new EnvironmentRecipe();

    [Header("Visuals")]
    [Tooltip("Floor, props and lights for this environment. Spawned at world origin. Should contain its own Directional Light (sun).")]
    public GameObject environmentPrefab;
    public Material skybox;

    [Header("Ambient Light")]
    public AmbientMode ambientMode = AmbientMode.Trilight;
    [ColorUsage(false, true)] public Color ambientSkyColor = new Color(0.55f, 0.6f, 0.75f);
    [ColorUsage(false, true)] public Color ambientEquatorColor = new Color(0.55f, 0.45f, 0.38f);
    [ColorUsage(false, true)] public Color ambientGroundColor = new Color(0.2f, 0.16f, 0.12f);
    [Range(0f, 2f)] public float reflectionIntensity = 1f;

    [Header("Fog")]
    public bool fog = true;
    public FogMode fogMode = FogMode.ExponentialSquared;
    public Color fogColor = new Color(0.86f, 0.72f, 0.58f);
    public float fogDensity = 0.01f;
    public float fogStart = 40f;
    public float fogEnd = 160f;

    [Header("Post Processing")]
    [Tooltip("Optional URP Volume Profile (bloom, tonemapping, color grading) used while this environment is active.")]
    public VolumeProfile postProcessProfile;
}
