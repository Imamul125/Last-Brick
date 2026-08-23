using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PowerUpUnlockConfig
{
    [Tooltip("The level number after which this power-up unlocks (e.g. if the power up becomes available at Level 5, this should be 4, as you unlock it by beating Level 4)")]
    public int unlockAfterLevel;
    [Tooltip("The UI RectTransform for the power-up unlock screen")]
    public RectTransform powerUpUI;
}

public class PowerUpUnlockManager : MonoBehaviour
{
    public static PowerUpUnlockManager Instance { get; private set; }

    [Header("Power-Ups Setup")]
    public List<PowerUpUnlockConfig> powerUps = new List<PowerUpUnlockConfig>();

    [Header("Animation Settings")]
    [Tooltip("Optional: A RectTransform indicating the center of the screen where the power-up should fly to. If left empty, it will use Vector2.zero.")]
    public RectTransform centerPoint;
    public float flyInDuration = 0.8f;
    public float rotateAmount = 720f; // 2 full rotations
    public float centerHoldDuration = 2.0f;
    public float flyOutDuration = 0.8f;
    [Tooltip("Scale multiplier when at the center")]
    public float centerScaleMultiplier = 1.5f;
    [Tooltip("Audio to play when a power-up is revealed")]
    public AudioClip revealSound;

    private AudioSource audioSource;

    [Header("UI References")]
    [Tooltip("Optional: An image/text GameObject that will be enabled while the power-up is revealing (e.g. 'Power-Up Unlocked!').")]
    public GameObject powerUpUnlockedTitle;

    [Header("Editor Testing")]
    [Tooltip("Check this box while the game is running to play the animation for the first power-up in your list!")]
    public bool testPlayAnimation = false;

    [Tooltip("The button the user clicks to acknowledge the power-up unlock")]
    public GameObject acknowledgeButton;

    [Tooltip("Optional: A parent container or background that should be enabled while ANY power-up is showing.")]
    public GameObject powerUpContainer;

    private bool isAnimating = false;
    private bool isAcknowledged = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // Ensure all power-up UIs are hidden at start
        foreach (var p in powerUps)
        {
            if (p.powerUpUI != null)
            {
                p.powerUpUI.gameObject.SetActive(false);
            }
        }

        if (powerUpUnlockedTitle != null)
        {
            powerUpUnlockedTitle.SetActive(false);
        }

        if (acknowledgeButton != null)
        {
            acknowledgeButton.SetActive(false);
            
            // Auto-hook the button click to avoid Unity Inspector reference issues
            UnityEngine.UI.Button btn = acknowledgeButton.GetComponent<UnityEngine.UI.Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(AcknowledgePowerUp);
                btn.onClick.AddListener(AcknowledgePowerUp);
            }
        }

        if (powerUpContainer != null)
        {
            powerUpContainer.SetActive(false);
        }
    }

    void Update()
    {
        if (testPlayAnimation)
        {
            testPlayAnimation = false;
            if (powerUps.Count > 0)
            {
                if (!isAnimating)
                {
                    StartCoroutine(RevealPowerUpCoroutine(powerUps[0].unlockAfterLevel));
                }
                else
                {
                    Debug.LogWarning("Animation is already playing! Please wait for it to finish.");
                }
            }
            else
            {
                Debug.LogWarning("Please add at least one power-up to the Setup list to test the animation!");
            }
        }
    }

    public void AcknowledgePowerUp()
    {
        isAcknowledged = true;
    }

    public bool HasPowerUpForLevel(int completedLevelNumber)
    {
        PowerUpUnlockConfig powerUpToReveal = powerUps.Find(p => p.unlockAfterLevel == completedLevelNumber);
        if (powerUpToReveal == null || powerUpToReveal.powerUpUI == null) return false;

        bool isTestMode = false;
        if (LevelManager.Instance != null && LevelManager.Instance.overrideLevelForTesting)
        {
            isTestMode = true;
        }

        int previouslySavedLevel = PlayerPrefs.GetInt("SavedLevel", 0);
        
        // Show only if it hasn't been shown before
        if (completedLevelNumber < previouslySavedLevel && !isTestMode)
        {
            return false;
        }
        return true;
    }

    public IEnumerator RevealPowerUpCoroutine(int completedLevelNumber)
    {
        if (isAnimating) yield break;

        PowerUpUnlockConfig powerUpToReveal = powerUps.Find(p => p.unlockAfterLevel == completedLevelNumber);

        if (powerUpToReveal == null || powerUpToReveal.powerUpUI == null)
        {
            yield break;
        }

        bool isTestMode = false;
        if (LevelManager.Instance != null && LevelManager.Instance.overrideLevelForTesting)
        {
            isTestMode = true;
        }

        int previouslySavedLevel = PlayerPrefs.GetInt("SavedLevel", 0);
        
        if (completedLevelNumber < previouslySavedLevel && !isTestMode)
        {
            yield break; 
        }

        isAnimating = true;
        isAcknowledged = false;
        RectTransform uiRect = powerUpToReveal.powerUpUI;

        Vector2 originalAnchoredPos = uiRect.anchoredPosition;
        Vector3 originalScale = uiRect.localScale;
        Quaternion originalRotation = uiRect.localRotation;

        uiRect.gameObject.SetActive(true);
        if (powerUpUnlockedTitle != null)
        {
            powerUpUnlockedTitle.SetActive(true);
        }
        if (powerUpContainer != null)
        {
            powerUpContainer.SetActive(true);
        }

        Vector2 targetCenter = Vector2.zero;
        if (centerPoint != null)
        {
            targetCenter = centerPoint.anchoredPosition;
        }

        Vector3 targetScale = originalScale * centerScaleMultiplier;

        if (revealSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(revealSound);
        }

        float elapsed = 0f;
        while (elapsed < flyInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyInDuration);
            
            float easePos = OutBack(t);
            float easeScale = ElasticEaseOut(t);
            float easeRot = OutCubic(t);

            uiRect.anchoredPosition = Vector2.LerpUnclamped(originalAnchoredPos, targetCenter, easePos);
            uiRect.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, easeScale); 
            
            float macroSpin = Mathf.LerpUnclamped(rotateAmount, 0f, easeRot);
            
            float decay = 1f - t;
            float microWobble = Mathf.Sin(t * Mathf.PI * 6f) * 12f * decay; 
            
            float currentZRotation = macroSpin + microWobble;
            uiRect.localRotation = originalRotation * Quaternion.Euler(0, 0, currentZRotation);

            yield return null;
        }

        uiRect.anchoredPosition = targetCenter;
        uiRect.localScale = targetScale;
        uiRect.localRotation = originalRotation;

        if (acknowledgeButton != null)
        {
            acknowledgeButton.SetActive(true);
        }

        elapsed = 0f;
        while (!isAcknowledged)
        {
            elapsed += Time.deltaTime;
            float breathScale = 1f + Mathf.Sin(elapsed * Mathf.PI * 2f) * 0.05f;
            uiRect.localScale = targetScale * breathScale;
            yield return null;
        }

        elapsed = 0f;
        Quaternion startOutRotation = uiRect.localRotation;
        while (elapsed < flyOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyOutDuration);
            
            float easeOut = InBack(t); 
            
            uiRect.anchoredPosition = Vector2.LerpUnclamped(targetCenter, originalAnchoredPos, easeOut);
            uiRect.localScale = Vector3.LerpUnclamped(targetScale, originalScale, easeOut);
            uiRect.localRotation = Quaternion.LerpUnclamped(startOutRotation, originalRotation, easeOut);

            yield return null;
        }

        uiRect.anchoredPosition = originalAnchoredPos;
        uiRect.localScale = originalScale;
        uiRect.localRotation = originalRotation;

        uiRect.gameObject.SetActive(false);
        if (powerUpUnlockedTitle != null)
        {
            powerUpUnlockedTitle.SetActive(false);
        }
        if (powerUpContainer != null)
        {
            powerUpContainer.SetActive(false);
        }
        isAnimating = false;
    }

    private float ElasticEaseOut(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        float p = 0.3f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p) + 1f;
    }

    private float OutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private float InBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return c3 * t * t * t - c1 * t * t;
    }

    private float OutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}
