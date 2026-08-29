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
    public GameObject removeAdsPopup;
    public GameObject outOfMovesImage;
    public GameObject generalFailImage;

    [Header("Events")]
    public UnityEvent onCongrats;
    public UnityEvent onRetry;
    public UnityEvent onWinParticleStart;

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

    [Header("IAP Settings")]
    [Tooltip("Show the Remove Ads popup every N levels")]
    public int removeAdsPopupFrequency = 5;

    [Header("Debug Settings")]
    [Tooltip("Check this to override your saved level with the debug level on Start.")]
    public bool overrideLevelForTesting = false;
    [Tooltip("The level index to test (0 = Level 1, 1 = Level 2, etc.)")]
    public int debugLevelIndex = 0;

    [Header("State")]
    public int currentLevelIndex = 0;
    private bool levelEnded = false;
    public bool IsLevelEnded => levelEnded;
    private GameObject currentLevelInstance;
    [HideInInspector]
    public List<ProtectBrick> protectedBricksInLevel = new List<ProtectBrick>();
    private Coroutine _loadLevelCoroutine;

    private bool pendingRemoveAdsPopup = false;
    private bool waitingForAdsPopupToClose = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    private void Update()
    {
        if (waitingForAdsPopupToClose && removeAdsPopup != null && !removeAdsPopup.activeInHierarchy)
        {
            waitingForAdsPopupToClose = false;
            levelEnded = false;
        }
    }
    private void Start()
    {
#if UNITY_EDITOR
        if (overrideLevelForTesting)
        {
            PlayerPrefs.SetInt("SavedLevel", debugLevelIndex);
            PlayerPrefs.Save();
            overrideLevelForTesting = false;
        }
#endif
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
        int totalRetries = PlayerPrefs.GetInt("TotalRetries", 0);
        PlayerPrefs.SetInt("TotalRetries", totalRetries + 1);
        PlayerPrefs.Save();

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
        
        // Cancel any pending TriggerLossNoMoves or LoadNextLevel from previous attempts
        CancelInvoke();
        
        currentLevelIndex = index;
        LevelData currentLevel = levels[currentLevelIndex];
        
        isLoadingNextLevel = false;
        
        // Prevent any win/loss triggers or interactions while the level is loading
        levelEnded = true;

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

        // Check if there is a badge or quote to reveal for this level is now moved to WinSequenceRoutine

        // Level is fully loaded, check if we need to show the remove ads popup
        if (pendingRemoveAdsPopup)
        {
            pendingRemoveAdsPopup = false;
            if (removeAdsPopup != null)
            {
                removeAdsPopup.SetActive(true);
                waitingForAdsPopupToClose = true;
                levelEnded = true; // Keeps the timer paused
            }
            else
            {
                levelEnded = false;
            }
        }
        else
        {
            levelEnded = false;
        }

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
            
            ProtectBrick[] pbs = currentLevelInstance.GetComponentsInChildren<ProtectBrick>();
            protectedBricksInLevel = new List<ProtectBrick>(pbs);

            Debug.Log("[LevelManager] Loaded " + levelName);
        }
        else
        {
            Debug.LogError("[LevelManager] Could not find level prefab: " + levelName + " in Resources/Levels");
        }
    }

    public void CheckWinCondition()
    {
        if (levelEnded) return;

        foreach (var pb in protectedBricksInLevel)
        {
            if (!pb.isSafeAndDelayed)
            {
                return;
            }
        }
        
        // All safe!
        foreach (var pb in protectedBricksInLevel)
        {
            pb.TriggerWinEffect();
        }
        TriggerWin();
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

        CatController[] cats = FindObjectsByType<CatController>(FindObjectsSortMode.None);
        foreach (var cat in cats)
        {
            if (cat != null)
            {
                cat.RunAway();
            }
        }

        if (GooglePlayManager.Instance != null)
        {
            GooglePlayManager.Instance.PostScore();
        }

        StartCoroutine(WinSequenceRoutine());
    }

    private IEnumerator WinParticleRoutine()
    {
        yield return new WaitForSeconds(winParticleDelay);
        
        onWinParticleStart?.Invoke();

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
            int bonus = 0;
            if (ComboManager.Instance != null) {
                bonus = ComboManager.Instance.TotalBonusCoinsEarned;
            }
            CoinAnimator.Instance.AnimateCoins(bonus);
        }
        
        CompleteCurrentLevel();
        Invoke(nameof(LoadNextLevel), 4f); // Wait 4 seconds then start next level so coin animation finishes
    }

    public void TriggerLoss()
    {
        ExecuteLoss(false);
    }

    public void TriggerLossNoMoves()
    {
        ExecuteLoss(true);
    }

    private void ExecuteLoss(bool isOutOfMoves)
    {
        if (levelEnded) return;
        levelEnded = true;
        Debug.Log("Level lost!");

        if (homeButton != null) homeButton.SetActive(false);

        if (outOfMovesImage != null) outOfMovesImage.SetActive(isOutOfMoves);
        if (generalFailImage != null) generalFailImage.SetActive(!isOutOfMoves);

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

        if (retryUi != null) 
        {
            retryUi.SetActive(true);
        }
        onRetry?.Invoke();
    }

    public void ResumeAfterUndo()
    {
        levelEnded = false;
        
        if (retryUi != null) retryUi.SetActive(false);
        if (congratsUi != null) congratsUi.SetActive(false);
        
        if (UIManager.Instance != null) UIManager.Instance.ShowSceneUI();

        if (homeButton != null) homeButton.SetActive(true);

        if (freeLookCamera != null) freeLookCamera.gameObject.SetActive(true);
        if (orbitCamera != null) orbitCamera.gameObject.SetActive(false);
        if (congratsCamera != null) congratsCamera.gameObject.SetActive(false);
    }

    public void CompleteCurrentLevel()
    {
        levelEnded = true;

        if (congratsUi != null) 
        {
            congratsUi.SetActive(true);
        }

        if (removeAdsPopup != null && removeAdsPopupFrequency > 0)
        {
            if (PlayerPrefs.GetInt("NoAdsPurchased", 0) == 0)
            {
                int currentLevelDisplay = currentLevelIndex + 1;
                if (currentLevelDisplay > 0 && currentLevelDisplay % removeAdsPopupFrequency == 0)
                {
                    pendingRemoveAdsPopup = true;
                }
            }
        }
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideSceneUI();
        }

        if (homeButton != null) homeButton.SetActive(false);

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

    private bool isLoadingNextLevel = false;

    // Helper to easily progress to the next level in the list
    public void LoadNextLevel()
    {
        if (isLoadingNextLevel) return;
        isLoadingNextLevel = true;
        
        CancelInvoke(nameof(LoadNextLevel));
        StartCoroutine(LoadNextLevelWithBadgeCheckRoutine());
    }

    private IEnumerator LoadNextLevelWithBadgeCheckRoutine()
    {
        int completedLevelNumber = levels[currentLevelIndex].levelNumber;
        if (BadgeManager.Instance != null && BadgeManager.Instance.HasBadgeForLevel(completedLevelNumber))
        {
            // Hide congrats UI and popups to ensure they don't block clicks on the Acknowledge button
            if (congratsUi != null) congratsUi.SetActive(false);
            if (removeAdsPopup != null) removeAdsPopup.SetActive(false);

            yield return StartCoroutine(BadgeManager.Instance.RevealBadgeCoroutine(completedLevelNumber));
        }

        if (PowerUpUnlockManager.Instance != null && PowerUpUnlockManager.Instance.HasPowerUpForLevel(completedLevelNumber))
        {
            if (congratsUi != null) congratsUi.SetActive(false);
            if (removeAdsPopup != null) removeAdsPopup.SetActive(false);

            yield return StartCoroutine(PowerUpUnlockManager.Instance.RevealPowerUpCoroutine(completedLevelNumber));
        }

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

    /// <summary>
    /// Call this from a custom UI Button on the Game Over / Congrats screen.
    /// It plays a Rewarded Ad, then grants coins = (baseReward * multiplier).
    /// </summary>
    public void WatchAdForCoinMultiplier(int multiplier = 3)
    {
        if (GameAdManager.Instance != null)
        {
            bool earnedReward = false;

            GameAdManager.Instance.ShowRewardedAd(
                // 1. Reward Earned Callback (Fires when video finishes, before closing)
                () => {
                    earnedReward = true;
                    int baseReward = 50; 
                    int totalBonusCoins = (baseReward * multiplier) - baseReward; 

                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.AddCoin(totalBonusCoins);
                        if (SoundManager.Instance != null) SoundManager.Instance.PlayCoinSound();
                    }
                },
                // 2. Ad Closed Callback (Fires when the user clicks the 'X' to close the ad)
                () => {
                    if (earnedReward)
                    {
                        // Resume or close UI based on which panel is currently open
                        if (retryUi != null && retryUi.activeInHierarchy)
                        {
                            RetryCurrentLevel();
                        }
                        else if (congratsUi != null && congratsUi.activeInHierarchy)
                        {
                            CancelInvoke(nameof(LoadNextLevel));
                            LoadNextLevel();
                        }
                    }
                }
            );
        }
    }
}
