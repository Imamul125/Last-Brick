using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;

[System.Serializable]
public class LevelCameraConfig
{
    public int startLevel;
    public int endLevel;
    public Transform cameraTarget;
    public float orbitalRadius;
}

[System.Serializable]
public class LevelData
{
    public int levelNumber;
    public GameObject protectBrick; // Reference to the unique brick
    public UnityEvent onLevelStart;
    public UnityEvent onLevelComplete;
}

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Levels Setup")]
    public List<LevelData> levels = new List<LevelData>();
    [Tooltip("Delay before loading the level prefab, allowing particles to play first.")]
    public float levelLoadDelay = 1.5f;

    [Header("UI References")]
    public GameObject congratsUi;
    public GameObject retryUi;
    public GameObject congratsFbx;

    [Header("Camera Settings")]
    public List<LevelCameraConfig> cameraConfigs = new List<LevelCameraConfig>();
    public CinemachineCamera orbitCamera; // The cinematic Vcamera
    public CinemachineCamera freeLookCamera; // The gameplay Vcamera
    public float cinematicRotationAmount = 360f;
    public float cinematicRotationDuration = 2f;
    private Coroutine cinematicCoroutine;

    [Header("State")]
    public int currentLevelIndex = 0;
    private bool levelEnded = false;
    private GameObject currentLevelInstance;
    private Coroutine _loadLevelCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Comment out the next line if you don't want the level to start automatically on load!
        // if (levels.Count > 0) StartLevel(0);
    }

    // Connect your UI 'Play' Button to this method!
    public void PlayCurrentLevel()
    {
        if (levels.Count > 0)
        {
            StartLevel(currentLevelIndex);
        }
    }

    public void StartLevel(int index)
    {
        if (index < 0 || index >= levels.Count) 
        {
            Debug.LogWarning("Level index out of range!");
            return;
        }
        
        currentLevelIndex = index;
        LevelData currentLevel = levels[currentLevelIndex];
        levelEnded = false;

        if (congratsUi != null) congratsUi.SetActive(false);
        if (congratsFbx != null) congratsFbx.SetActive(false);
        if (retryUi != null) retryUi.SetActive(false);
        
        // Destroy the previous level immediately so the screen is clear for the particle
        if (currentLevelInstance != null)
        {
            Destroy(currentLevelInstance);
            currentLevelInstance = null;
        }

        Debug.Log("Level " + currentLevel.levelNumber + " Started!");
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayLevelStartSound();
        }

        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlayLevelStartParticle();
        }

        if (_loadLevelCoroutine != null)
        {
            StopCoroutine(_loadLevelCoroutine);
        }
        _loadLevelCoroutine = StartCoroutine(LoadLevelRoutine(currentLevel));
    }

    private IEnumerator LoadLevelRoutine(LevelData currentLevel)
    {
        if (levelLoadDelay > 0)
        {
            yield return new WaitForSeconds(levelLoadDelay);
        }

        LoadLevelPrefab(currentLevel.levelNumber);

        SetupCameraForLevel(currentLevel.levelNumber);

        currentLevel.onLevelStart?.Invoke();
    }

    private void LoadLevelPrefab(int levelNum)
    {
        if (currentLevelInstance != null)
        {
            Destroy(currentLevelInstance);
        }

        string levelName = "Tower_" + levelNum;

        // Clean up any existing editor level with this name just in case
        GameObject existingEditorLevel = GameObject.Find(levelName);
        if (existingEditorLevel != null)
        {
            Destroy(existingEditorLevel);
        }

        GameObject levelPrefab = Resources.Load<GameObject>("Levels/" + levelName);

        if (levelPrefab != null)
        {
            currentLevelInstance = Instantiate(levelPrefab, Vector3.zero, Quaternion.identity);
            Debug.Log("[LevelManager] Loaded " + levelName);
        }
        else
        {
            Debug.LogError("[LevelManager] Could not find level prefab: " + levelName + " in Resources/Levels");
        }
    }

    public void TriggerWin()
    {
        if (levelEnded) return;
        levelEnded = true;
        Debug.Log("Protect Brick reached the pedestal! YOU WIN!");
        
        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlayPlayerWinParticle();
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayPlayerWinSound();
        }
        
        if (congratsUi != null) congratsUi.SetActive(true);
        if (congratsFbx != null) congratsFbx.SetActive(true);
        
        CompleteCurrentLevel();
        Invoke(nameof(LoadNextLevel), 3f); // Wait 3 seconds then start next level
    }

    public void TriggerLoss()
    {
        if (levelEnded) return;
        levelEnded = true;
        Debug.Log("Protect Brick hit the ground! YOU LOSE!");
        
        if (retryUi != null) retryUi.SetActive(true);
    }

    public void CompleteCurrentLevel()
    {
        if (currentLevelIndex < 0 || currentLevelIndex >= levels.Count) return;

        LevelData currentLevel = levels[currentLevelIndex];
        currentLevel.onLevelComplete?.Invoke();
    }

    // Helper to easily progress to the next level in the list
    public void LoadNextLevel()
    {
        if (currentLevelIndex + 1 < levels.Count)
        {
            StartLevel(currentLevelIndex + 1);
        }
        else
        {
            Debug.Log("All levels completed!");
        }
    }

    private void SetupCameraForLevel(int levelNum)
    {
        LevelCameraConfig config = cameraConfigs.Find(c => levelNum >= c.startLevel && levelNum <= c.endLevel);
        if (config != null)
        {
            Debug.Log($"[LevelManager] Found camera config for Level {levelNum}. Starting cinematic transition.");
            // Prepare cameras for the cinematic phase
            if (orbitCamera != null) orbitCamera.gameObject.SetActive(true);
            if (freeLookCamera != null) freeLookCamera.gameObject.SetActive(false);

            if (orbitCamera != null)
            {
                orbitCamera.Follow = config.cameraTarget;
                orbitCamera.LookAt = config.cameraTarget; // Re-enabled so it looks at target while rotating
                
                var orbitalFollow = orbitCamera.GetComponent<CinemachineOrbitalFollow>();
                if (orbitalFollow != null)
                {
                    orbitalFollow.Radius = config.orbitalRadius;

                    if (cinematicCoroutine != null) StopCoroutine(cinematicCoroutine);
                    cinematicCoroutine = StartCoroutine(DoCinematicRotation(orbitalFollow, config));
                }
                else
                {
                    Debug.LogWarning("[LevelManager] orbitCamera is missing the CinemachineOrbitalFollow component!");
                }
            }
        }
        else
        {
            Debug.LogWarning($"[LevelManager] No Camera Config found for Level {levelNum}! The cinematic rotation will not play. Please add a config in the inspector.");
        }
    }

    private IEnumerator DoCinematicRotation(CinemachineOrbitalFollow orbitalFollow, LevelCameraConfig config)
    {
        float elapsedTime = 0f;
        float startRotation = orbitalFollow.HorizontalAxis.Value;
        float endRotation = startRotation + cinematicRotationAmount;

        while (elapsedTime < cinematicRotationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / cinematicRotationDuration;
            
            // Smooth step for nicer cinematic ease-in/ease-out
            t = t * t * (3f - 2f * t);

            orbitalFollow.HorizontalAxis.Value = Mathf.Lerp(startRotation, endRotation, t);
            yield return null;
        }

        orbitalFollow.HorizontalAxis.Value = endRotation;

        // Intro is done, switch to the gameplay camera
        if (orbitCamera != null) orbitCamera.gameObject.SetActive(false);
        if (freeLookCamera != null)
        {
            freeLookCamera.gameObject.SetActive(true);
            freeLookCamera.Follow = config.cameraTarget;

            var freeLookOrbital = freeLookCamera.GetComponent<CinemachineOrbitalFollow>();
            if (freeLookOrbital != null)
            {
                freeLookOrbital.Radius = config.orbitalRadius;
                // Sync the rotation so there is no snap when switching cameras
                freeLookOrbital.HorizontalAxis.Value = endRotation;
            }
        }
    }
}
