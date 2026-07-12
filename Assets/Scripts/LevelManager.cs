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
    public int maxMoves = 15;
    public float timeLimit = 60f;
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
    public GameObject homeButton;

    [Header("Events")]
    public UnityEvent onCongrats;
    public UnityEvent onRetry;

    [Tooltip("Delay before playing the win particle")]
    public float winParticleDelay = 0.5f;
    [Tooltip("Delay before playing the win sound")]
    public float winSoundDelay = 0.5f;
    [Tooltip("Delay before showing congrats UI")]
    public float winCongratsDelay = 1.5f;

    [Header("Camera Settings")]
    public List<LevelCameraConfig> cameraConfigs = new List<LevelCameraConfig>();
    public CinemachineCamera orbitCamera; // The cinematic Vcamera
    public CinemachineCamera freeLookCamera; // The gameplay Vcamera
    public CinemachineCamera congratsCamera; // The camera for congrats
    public float cinematicRotationAmount = 360f;
    public float cinematicRotationDuration = 2f;
    private Coroutine cinematicCoroutine;

    [Header("State")]
    public int currentLevelIndex = 0;
    private bool levelEnded = false;
    public bool IsLevelEnded => levelEnded;
    private GameObject currentLevelInstance;
    private GameObject currentProtectBrick;
    private Coroutine _loadLevelCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    private void Start()
    {
        currentLevelIndex = PlayerPrefs.GetInt("SavedLevel", 0);
        // Comment out the next line if you don't want the level to start automatically on load!
        // if (levels.Count > 0) StartLevel(currentLevelIndex);
    }

    // Connect your UI 'Play' Button to this method!
    public void PlayCurrentLevel()
    {
        currentLevelIndex = PlayerPrefs.GetInt("SavedLevel", 0);
        if (levels.Count > 0)
        {
            StartLevel(currentLevelIndex);
        }
    }

    // Connect your UI 'Retry' Button to this method!
    public void RetryCurrentLevel()
    {
        if (GameAdManager.Instance != null)
        {
            GameAdManager.Instance.OnLevelRetry(() => {
                StartLevel(currentLevelIndex);
            });
        }
        else
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

        if (FirebaseManager.Instance != null)
        {
            FirebaseManager.Instance.LogLevelStarted(currentLevelIndex);
        }

        if (congratsUi != null) congratsUi.SetActive(false);
        if (retryUi != null) retryUi.SetActive(false);
        if (homeButton != null) homeButton.SetActive(true);
        if (congratsCamera != null) congratsCamera.gameObject.SetActive(false);
        
        // Destroy the previous level immediately so the screen is clear for the particle
        if (currentLevelInstance != null)
        {
            Destroy(currentLevelInstance);
            currentLevelInstance = null;
        }

        Debug.Log("Level " + currentLevel.levelNumber + " Started!");
        
        // Setup UI for this level (moves and time)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetupLevelLimits(currentLevel.maxMoves, currentLevel.timeLimit);
            UIManager.Instance.ShowSceneUI();
        }

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

        // Clean up any old cats left behind from previous levels (since they unparent themselves)
        CatController[] oldCats = FindObjectsByType<CatController>(FindObjectsSortMode.None);
        foreach (var c in oldCats)
        {
            Destroy(c.gameObject);
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
            
            ProtectBrick pb = currentLevelInstance.GetComponentInChildren<ProtectBrick>();
            if (pb != null)
            {
                currentProtectBrick = pb.gameObject;
            }

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

        if (homeButton != null) homeButton.SetActive(false);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideSceneUI();
        }

        if (cinematicCoroutine != null)
        {
            StopCoroutine(cinematicCoroutine);
            cinematicCoroutine = null;
        }
        
        StartCoroutine(WinParticleRoutine());
        StartCoroutine(WinSoundRoutine());

        if (congratsCamera != null)
        {
            if (orbitCamera != null) orbitCamera.gameObject.SetActive(false);
            if (freeLookCamera != null) freeLookCamera.gameObject.SetActive(false);
            congratsCamera.gameObject.SetActive(true);
        }

        if (currentLevelInstance != null)
        {
            PedestalAnimator pedestal = currentLevelInstance.GetComponentInChildren<PedestalAnimator>();
            if (pedestal != null)
            {
                pedestal.AnimateDown();
            }
        }

        CatController cat = FindAnyObjectByType<CatController>();
        if (cat != null)
        {
            cat.RunAway();
        }

        StartCoroutine(WinSequenceRoutine());
    }

    private IEnumerator WinParticleRoutine()
    {
        yield return new WaitForSeconds(winParticleDelay);
        
        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlayPlayerWinParticle();
        }
    }

    private IEnumerator WinSoundRoutine()
    {
        yield return new WaitForSeconds(winSoundDelay);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayPlayerWinSound();
        }
    }

    private IEnumerator WinSequenceRoutine()
    {
        yield return new WaitForSeconds(winCongratsDelay);
        
        if (congratsUi != null) congratsUi.SetActive(true);
        onCongrats?.Invoke();

        if (CoinAnimator.Instance != null)
        {
            CoinAnimator.Instance.AnimateCoins();
        }
        
        CompleteCurrentLevel();
        Invoke(nameof(LoadNextLevel), 4f); // Wait 4 seconds then start next level so coin animation finishes
    }

    public void TriggerLoss()
    {
        if (levelEnded) return;
        levelEnded = true;
        Debug.Log("Protect Brick hit the ground! YOU LOSE!");

        if (homeButton != null) homeButton.SetActive(false);

        if (FirebaseManager.Instance != null)
        {
            FirebaseManager.Instance.LogLevelFailed(currentLevelIndex);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideSceneUI();
        }

        if (cinematicCoroutine != null)
        {
            StopCoroutine(cinematicCoroutine);
            cinematicCoroutine = null;
        }

        if (orbitCamera != null) orbitCamera.gameObject.SetActive(false);
        if (freeLookCamera != null) freeLookCamera.gameObject.SetActive(false);
        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayRetrySound();
        }

        if (currentLevelInstance != null)
        {
            PedestalAnimator pedestal = currentLevelInstance.GetComponentInChildren<PedestalAnimator>();
            if (pedestal != null)
            {
                pedestal.AnimateDown();
            }
        }

        if (retryUi != null) retryUi.SetActive(true);
        onRetry?.Invoke();
    }

    public void CompleteCurrentLevel()
    {
        if (currentLevelIndex < 0 || currentLevelIndex >= levels.Count) return;

        LevelData currentLevel = levels[currentLevelIndex];
        currentLevel.onLevelComplete?.Invoke();
        
        if (FirebaseManager.Instance != null)
        {
            FirebaseManager.Instance.LogLevelCompleted(currentLevelIndex);
        }
        
        int nextLevel = currentLevelIndex + 1;
        if (nextLevel < levels.Count)
        {
            PlayerPrefs.SetInt("SavedLevel", nextLevel);
            PlayerPrefs.Save();
        }
    }

    // Helper to easily progress to the next level in the list
    public void LoadNextLevel()
    {
        if (GameAdManager.Instance != null)
        {
            GameAdManager.Instance.OnLevelCompleted(() => {
                ProceedToLoadNextLevel();
            });
        }
        else
        {
            ProceedToLoadNextLevel();
        }
    }

    private void ProceedToLoadNextLevel()
    {
        currentLevelIndex = PlayerPrefs.GetInt("SavedLevel", 0);
        if (currentLevelIndex < levels.Count)
        {
            StartLevel(currentLevelIndex);
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

    // Connect this to your 'Home' or 'Back to Main' button
    public void BackToMain()
    {
        // Pause logic without timescale
        levelEnded = true;

        // Destroy the current level prefab
        if (currentLevelInstance != null)
        {
            Destroy(currentLevelInstance);
            currentLevelInstance = null;
        }

        // Stop timer and hide UI
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideSceneUI();
        }

        // Stop any running level coroutines to be safe
        if (_loadLevelCoroutine != null)
        {
            StopCoroutine(_loadLevelCoroutine);
        }

        if (cinematicCoroutine != null)
        {
            StopCoroutine(cinematicCoroutine);
            cinematicCoroutine = null;
        }
    }
}
