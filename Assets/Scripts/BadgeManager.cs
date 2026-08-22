using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BadgeConfig
{
    [Tooltip("The level number that unlocks this badge (e.g. 5)")]
    public int unlockLevel;
    [Tooltip("The UI RectTransform for the badge")]
    public RectTransform badgeUI;
    [Tooltip("The GPGS Achievement ID string (e.g. GPGSIds.achievement_rookie_escaper)")]
    public string playGamesAchievementId;
}

public class BadgeManager : MonoBehaviour
{
    public static BadgeManager Instance { get; private set; }

    [Header("Badges Setup")]
    public List<BadgeConfig> badges = new List<BadgeConfig>();

    [Header("Animation Settings")]
    [Tooltip("Optional: A RectTransform indicating the center of the screen where the badge should fly to. If left empty, it will use Vector2.zero.")]
    public RectTransform centerPoint;
    public float flyInDuration = 0.8f;
    public float rotateAmount = 720f; // 2 full rotations
    public float centerHoldDuration = 2.0f;
    public float flyOutDuration = 0.8f;
    [Tooltip("Scale multiplier when at the center")]
    public float centerScaleMultiplier = 1.5f;
    [Tooltip("Audio to play when a badge is revealed")]
    public AudioClip revealSound;

    private AudioSource audioSource;

    [Header("UI References")]
    [Tooltip("Optional: An image/text GameObject that will be enabled while the badge is revealing (e.g. 'Achievement Unlocked').")]
    public GameObject achievementUnlockedImage;

    [Header("Editor Testing")]
    [Tooltip("Check this box while the game is running to play the animation for the first badge in your list!")]
    public bool testPlayAnimation = false;

    [Tooltip("The button the user clicks to acknowledge the badge/quote")]
    public GameObject acknowledgeButton;

    [Tooltip("Optional: A parent container or background that should be enabled while ANY badge is showing.")]
    public GameObject badgeContainer;

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

        // Ensure all badges are hidden at start
        foreach (var badge in badges)
        {
            if (badge.badgeUI != null)
            {
                badge.badgeUI.gameObject.SetActive(false);
            }
        }

        if (achievementUnlockedImage != null)
        {
            achievementUnlockedImage.SetActive(false);
        }

        if (acknowledgeButton != null)
        {
            acknowledgeButton.SetActive(false);
            
            // Auto-hook the button click to avoid Unity Inspector reference issues
            UnityEngine.UI.Button btn = acknowledgeButton.GetComponent<UnityEngine.UI.Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(AcknowledgeBadge);
                btn.onClick.AddListener(AcknowledgeBadge);
            }
        }

        if (badgeContainer != null)
        {
            badgeContainer.SetActive(false);
        }
    }

    void Update()
    {
        // Simple trick to let the developer test the animation by clicking a checkbox in the inspector
        if (testPlayAnimation)
        {
            testPlayAnimation = false;
            if (badges.Count > 0)
            {
                if (!isAnimating)
                {
                    // Play the animation for the first badge in the list
                    StartCoroutine(RevealBadgeCoroutine(badges[0].unlockLevel));
                }
                else
                {
                    Debug.LogWarning("Animation is already playing! Please wait for it to finish.");
                }
            }
            else
            {
                Debug.LogWarning("Please add at least one badge to the Badges Setup list to test the animation!");
            }
        }
    }

    public void AcknowledgeBadge()
    {
        isAcknowledged = true;
    }

    public bool HasBadgeForLevel(int levelNumber)
    {
        BadgeConfig badgeToReveal = badges.Find(b => b.unlockLevel == levelNumber);
        if (badgeToReveal == null || badgeToReveal.badgeUI == null) return false;

        bool isTestMode = false;
        if (LevelManager.Instance != null && LevelManager.Instance.overrideLevelForTesting)
        {
            isTestMode = true;
        }

        int previouslySavedLevel = PlayerPrefs.GetInt("SavedLevel", 0);
        
        // Show only if it hasn't been shown before
        if (levelNumber < previouslySavedLevel && !isTestMode)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if there is a badge for this level. If so, returns the coroutine to play it.
    /// Returns null if no badge exists for this level.
    /// </summary>
    public IEnumerator RevealBadgeCoroutine(int completedLevelNumber)
    {
        if (isAnimating) yield break;

        BadgeConfig badgeToReveal = badges.Find(b => b.unlockLevel == completedLevelNumber);

        if (badgeToReveal == null || badgeToReveal.badgeUI == null)
        {
            yield break; // No badge for this level
        }

        // Only reveal if we haven't already unlocked it, UNLESS we are in test mode
        bool isTestMode = false;
        if (LevelManager.Instance != null && LevelManager.Instance.overrideLevelForTesting)
        {
            isTestMode = true;
        }

        int previouslySavedLevel = PlayerPrefs.GetInt("SavedLevel", 0);
        
        // If we already passed this level in a previous session, and we aren't testing, skip the animation
        // Note: Using strictly less than (<) because LevelManager saves the level BEFORE calling this!
        if (completedLevelNumber < previouslySavedLevel && !isTestMode)
        {
            // Even if we skip the animation, try to unlock the achievement just in case they were offline previously
            UnlockGPGSAchievement(badgeToReveal.playGamesAchievementId);
            yield break; 
        }

        isAnimating = true;
        isAcknowledged = false;
        RectTransform badgeRect = badgeToReveal.badgeUI;

        // Unlock the achievement for this badge
        UnlockGPGSAchievement(badgeToReveal.playGamesAchievementId);

        // Save original transform state
        Vector2 originalAnchoredPos = badgeRect.anchoredPosition;
        Vector3 originalScale = badgeRect.localScale;
        Quaternion originalRotation = badgeRect.localRotation;

        badgeRect.gameObject.SetActive(true);
        if (achievementUnlockedImage != null)
        {
            achievementUnlockedImage.SetActive(true);
        }
        if (badgeContainer != null)
        {
            badgeContainer.SetActive(true);
        }

        // Calculate target center position
        Vector2 targetCenter = Vector2.zero;
        if (centerPoint != null)
        {
            targetCenter = centerPoint.anchoredPosition;
        }

        Vector3 targetScale = originalScale * centerScaleMultiplier;

        // Play Sound immediately as the badge appears!
        if (revealSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(revealSound);
        }

        // --- Fly In Animation ---
        float elapsed = 0f;
        while (elapsed < flyInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyInDuration);
            
            float easePos = OutBack(t); // Pop past the center and settle
            float easeScale = ElasticEaseOut(t); // Very bouncy, premium scale
            float easeRot = OutCubic(t); // Smooth deceleration for the spin

            badgeRect.anchoredPosition = Vector2.LerpUnclamped(originalAnchoredPos, targetCenter, easePos);
            badgeRect.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, easeScale); 
            
            // --- DYNAMIC ROTATION ---
            // 1. Macro Spin: Starts at rotateAmount and smoothly spins down to exactly 0.
            //    This guarantees it always spins in one continuous direction and lands upright.
            float macroSpin = Mathf.LerpUnclamped(rotateAmount, 0f, easeRot);
            
            // 2. Micro Wobble: Adds a subtle back-and-forth wobble that decays as the animation finishes.
            float decay = 1f - t;
            float microWobble = Mathf.Sin(t * Mathf.PI * 6f) * 12f * decay; 
            
            float currentZRotation = macroSpin + microWobble;
            badgeRect.localRotation = originalRotation * Quaternion.Euler(0, 0, currentZRotation);

            yield return null;
        }

        // Ensure it's exactly at target before hold
        badgeRect.anchoredPosition = targetCenter;
        badgeRect.localScale = targetScale;
        badgeRect.localRotation = originalRotation; // Always perfectly upright

        // Show the acknowledge button
        if (acknowledgeButton != null)
        {
            acknowledgeButton.SetActive(true);
        }

        // --- Hold at Center with Breathing Animation until Acknowledged ---
        elapsed = 0f;
        while (!isAcknowledged)
        {
            elapsed += Time.deltaTime;
            
            // Breathing effect: gently scales up and down by 5%
            float breathScale = 1f + Mathf.Sin(elapsed * Mathf.PI * 2f) * 0.05f;
            badgeRect.localScale = targetScale * breathScale;
            
            yield return null;
        }

        // --- Fly Out Animation ---
        elapsed = 0f;
        Quaternion startOutRotation = badgeRect.localRotation;
        while (elapsed < flyOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyOutDuration);
            
            // InBack pulls the badge backwards slightly (anticipation) before flying out fast
            float easeOut = InBack(t); 
            
            badgeRect.anchoredPosition = Vector2.LerpUnclamped(targetCenter, originalAnchoredPos, easeOut);
            badgeRect.localScale = Vector3.LerpUnclamped(targetScale, originalScale, easeOut);
            
            badgeRect.localRotation = Quaternion.LerpUnclamped(startOutRotation, originalRotation, easeOut);

            yield return null;
        }

        // Restore exact original state
        badgeRect.anchoredPosition = originalAnchoredPos;
        badgeRect.localScale = originalScale;
        badgeRect.localRotation = originalRotation;

        badgeRect.gameObject.SetActive(false);
        if (achievementUnlockedImage != null)
        {
            achievementUnlockedImage.SetActive(false);
        }
        if (badgeContainer != null)
        {
            badgeContainer.SetActive(false);
        }
        isAnimating = false;
    }

    // --- Easing Functions for Premium Animation Feel ---

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

    private void UnlockGPGSAchievement(string achievementId)
    {
        if (string.IsNullOrEmpty(achievementId)) return;

#if UNITY_ANDROID
        if (Social.localUser.authenticated)
        {
            Social.ReportProgress(achievementId, 100.0f, (bool success) =>
            {
                if (success)
                {
                    Debug.Log($"[BadgeManager] Successfully unlocked achievement: {achievementId}");
                }
                else
                {
                    Debug.LogWarning($"[BadgeManager] Failed to unlock achievement: {achievementId}");
                }
            });
        }
#endif
    }
}
