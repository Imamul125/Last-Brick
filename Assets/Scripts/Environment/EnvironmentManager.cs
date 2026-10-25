using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Swaps the environment (floor, props, lights, skybox, fog, post-processing) based on the level number.
/// With changeFrequency = 2: levels 1-2 use environment 0, levels 3-4 use environment 1, and so on.
/// LevelManager calls ApplyForLevel() whenever a level starts.
/// Recipe environments are generated on demand and fully freed when replaced, so only one is ever in memory.
/// </summary>
public class EnvironmentManager : MonoBehaviour
{
    public static EnvironmentManager Instance { get; private set; }

    [Header("Environments")]
    [Tooltip("All saved environments, in the order they should appear.")]
    public List<EnvironmentProfile> environments = new List<EnvironmentProfile>();

    [Tooltip("Load a new environment every N levels. Example: 2 = new environment after every 2 levels.")]
    [Min(1)]
    public int changeFrequency = 2;

    [Tooltip("When we run out of environments, start again from the first one. If off, the last environment stays.")]
    public bool loopEnvironments = true;

    [Tooltip("Shared textures, meshes and material templates used by recipe environments.")]
    public EnvironmentLibrary library;

    [Header("Ground")]
    [Tooltip("Top of the gameplay floor. Detected automatically from the collider tagged 'Ground' when possible.")]
    public float groundHeight = -2.1f;
    public bool autoDetectGroundHeight = true;

    [Header("Legacy Scene Environment")]
    [Tooltip("Old scene objects whose visuals (renderers, lights, particles) are hidden while a profile is active. Colliders are kept, so the Ground collider keeps working.")]
    public List<GameObject> legacyVisualRoots = new List<GameObject>();

    [Header("Post Processing")]
    [Tooltip("Priority of the Volume this manager creates. Must be higher than your Global Volume so environment grading wins.")]
    public float volumePriority = 10f;

    [Header("Debug")]
    [Tooltip("Editor only: force a specific environment index for testing look and feel. -1 = use level based selection. In Play Mode press [ and ] to step through environments.")]
    public int debugForceEnvironmentIndex = -1;

    public int CurrentEnvironmentIndex { get; private set; } = -1;
    public EnvironmentProfile CurrentProfile => CurrentEnvironmentIndex >= 0 && CurrentEnvironmentIndex < environments.Count ? environments[CurrentEnvironmentIndex] : null;

    private const string SavedEnvironmentKey = "CurrentEnvironmentIndex";

    private GameObject currentInstance;
    private Volume environmentVolume;
    private bool legacyHidden;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        environmentVolume = gameObject.AddComponent<Volume>();
        environmentVolume.isGlobal = true;
        environmentVolume.priority = volumePriority;
        environmentVolume.weight = 1f;

        if (autoDetectGroundHeight) DetectGroundHeight();
    }

    private void Start()
    {
        // Show the saved environment on the home screen before any level starts.
        if (CurrentEnvironmentIndex < 0 && environments.Count > 0)
        {
            ApplyEnvironment(Mathf.Clamp(PlayerPrefs.GetInt(SavedEnvironmentKey, 0), 0, environments.Count - 1));
        }
    }

#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || environments.Count == 0) return;
        if (kb.rightBracketKey.wasPressedThisFrame) ApplyEnvironment((CurrentEnvironmentIndex + 1) % environments.Count);
        if (kb.leftBracketKey.wasPressedThisFrame) ApplyEnvironment((CurrentEnvironmentIndex - 1 + environments.Count) % environments.Count);
    }
#endif

    private void DetectGroundHeight()
    {
        GameObject[] grounds;
        try { grounds = GameObject.FindGameObjectsWithTag("Ground"); }
        catch { return; }

        float best = float.NegativeInfinity;
        foreach (GameObject g in grounds)
        {
            Collider col = g.GetComponent<Collider>();
            if (col != null && col.bounds.size.x > 50f) best = Mathf.Max(best, col.bounds.max.y);
        }
        if (!float.IsNegativeInfinity(best)) groundHeight = best;
    }

    /// <summary>Returns which environment index a level (1-based level number) should use.</summary>
    public int GetEnvironmentIndexForLevel(int levelNumber)
    {
        if (environments.Count == 0) return -1;

        int block = Mathf.Max(0, levelNumber - 1) / Mathf.Max(1, changeFrequency);
        return loopEnvironments ? block % environments.Count : Mathf.Min(block, environments.Count - 1);
    }

    public void ApplyForLevel(int levelNumber)
    {
        int index = GetEnvironmentIndexForLevel(levelNumber);
#if UNITY_EDITOR
        if (debugForceEnvironmentIndex >= 0 && debugForceEnvironmentIndex < environments.Count)
        {
            index = debugForceEnvironmentIndex;
        }
#endif
        ApplyEnvironment(index);
    }

    public void ApplyEnvironment(int index)
    {
        if (index < 0 || index >= environments.Count || environments[index] == null) return;
        if (index == CurrentEnvironmentIndex && currentInstance != null) return;

        EnvironmentProfile profile = environments[index];
        if (profile.useRecipe && library == null)
        {
            Debug.LogError("[EnvironmentManager] Recipe environments need the EnvironmentLibrary assigned.");
            return;
        }

        CurrentEnvironmentIndex = index;
        PlayerPrefs.SetInt(SavedEnvironmentKey, index);

        HideLegacyVisuals();

        if (currentInstance != null)
        {
            // GeneratedEnvironment.OnDestroy frees the runtime meshes, materials and profiles.
            Destroy(currentInstance);
            currentInstance = null;
        }

        if (profile.useRecipe)
        {
            GeneratedEnvironment generated = EnvironmentGenerator.Build(profile, library, groundHeight);
            currentInstance = generated.gameObject;

            RenderSettings.skybox = generated.skybox;
            EnvironmentGenerator.ApplyRenderSettings(profile.recipe);
            SetSun();
            environmentVolume.sharedProfile = generated.volume;
            environmentVolume.enabled = true;
        }
        else
        {
            if (profile.environmentPrefab != null)
            {
                currentInstance = Instantiate(profile.environmentPrefab, Vector3.zero, Quaternion.identity);
                currentInstance.name = profile.environmentPrefab.name;
            }

            ApplyRenderSettings(profile);
            environmentVolume.sharedProfile = profile.postProcessProfile;
            environmentVolume.enabled = profile.postProcessProfile != null;
        }

        RefreshReflectionProbes();

        Debug.Log($"[EnvironmentManager] Loaded environment {index}: {profile.displayName}");
    }

    private void ApplyRenderSettings(EnvironmentProfile profile)
    {
        if (profile.skybox != null) RenderSettings.skybox = profile.skybox;

        RenderSettings.ambientMode = profile.ambientMode;
        RenderSettings.ambientSkyColor = profile.ambientSkyColor;
        RenderSettings.ambientEquatorColor = profile.ambientEquatorColor;
        RenderSettings.ambientGroundColor = profile.ambientGroundColor;
        RenderSettings.ambientLight = profile.ambientSkyColor;
        RenderSettings.reflectionIntensity = profile.reflectionIntensity;

        RenderSettings.fog = profile.fog;
        RenderSettings.fogMode = profile.fogMode;
        RenderSettings.fogColor = profile.fogColor;
        RenderSettings.fogDensity = profile.fogDensity;
        RenderSettings.fogStartDistance = profile.fogStart;
        RenderSettings.fogEndDistance = profile.fogEnd;

        SetSun();
        if (profile.ambientMode == AmbientMode.Skybox) DynamicGI.UpdateEnvironment();
    }

    private void SetSun()
    {
        // The procedural skybox takes its sun direction from RenderSettings.sun.
        RenderSettings.sun = null;
        if (currentInstance == null) return;
        foreach (Light l in currentInstance.GetComponentsInChildren<Light>())
        {
            if (l.type == LightType.Directional) { RenderSettings.sun = l; break; }
        }
    }

    private void RefreshReflectionProbes()
    {
        if (currentInstance == null) return;

        // Realtime probes set to "Via Scripting" are rendered once here, giving glossy floors
        // correct reflections without a per-frame cost.
        foreach (ReflectionProbe probe in currentInstance.GetComponentsInChildren<ReflectionProbe>())
        {
            if (probe.mode == ReflectionProbeMode.Realtime &&
                probe.refreshMode == ReflectionProbeRefreshMode.ViaScripting)
            {
                probe.RenderProbe();
            }
        }
    }

    private void HideLegacyVisuals()
    {
        if (legacyHidden) return;
        legacyHidden = true;

        foreach (GameObject root in legacyVisualRoots)
        {
            if (root == null) continue;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (Light l in root.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Preview Next Environment (Play Mode)")]
    private void PreviewNextEnvironment()
    {
        if (!Application.isPlaying || environments.Count == 0) return;
        ApplyEnvironment((CurrentEnvironmentIndex + 1) % environments.Count);
    }
#endif
}
