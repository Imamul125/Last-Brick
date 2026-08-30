using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class AchievementBadge
{
    [Tooltip("The level required to unlock this badge")]
    public int unlockLevel;
    [Tooltip("The UI GameObject representing the badge in the achievements grid/list")]
    public GameObject badgeUI;

    // Used internally to swap between locked and unlocked sprites
    [HideInInspector] public Sprite unlockedSprite;
    [HideInInspector] public Image badgeImage;
}

public class AchievementsMenu : MonoBehaviour
{
    [Header("Badges Config")]
    public List<AchievementBadge> badges = new List<AchievementBadge>();

    [Header("Locked State")]
    [Tooltip("The sprite to display when a badge has not been unlocked yet. It will replace the image of the Badge UI.")]
    public Sprite lockedSprite;

    [Header("Animation Settings")]
    [Tooltip("The sound to play for the badge pop-up")]
    public AudioClip popSound;
    [Tooltip("If true, the pop sound plays exactly once per menu open. If false, it plays for every single badge that pops up.")]
    public bool playAudioOnlyOnce = false;
    public float popAnimationDuration = 0.5f;
    [Tooltip("Delay in seconds between each badge pop-up")]
    public float staggerDelay = 0.2f;

    [Header("Scroll View Settings")]
    [Tooltip("Drag your Scroll View's ScrollRect component here to automatically scroll to the top when opened.")]
    public ScrollRect scrollRect;

    private AudioSource audioSource;
    private Coroutine currentAnimationRoutine;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    /// <summary>
    /// Call this method from your Achievements Button OnClick event.
    /// It checks PlayerPrefs and triggers the staggered reveal animation.
    /// </summary>
    public void ShowAchievements()
    {
        // Stop any ongoing animations if the user rapidly clicks the button
        if (currentAnimationRoutine != null)
        {
            StopCoroutine(currentAnimationRoutine);
        }

        int savedLevel = PlayerPrefs.GetInt("SavedLevel", 0);

#if UNITY_EDITOR
        // For testing purposes in the editor, if overrideLevelForTesting is enabled, simulate the level
        if (LevelManager.Instance != null && LevelManager.Instance.overrideLevelForTesting)
        {
            int testIndex = Mathf.Max(0, LevelManager.Instance.debugLevelIndex - 1);
            savedLevel = Mathf.Max(LevelManager.Instance.currentLevelIndex, testIndex);
        }
#endif

        currentAnimationRoutine = StartCoroutine(AnimateBadges(savedLevel));
    }

    private IEnumerator AnimateBadges(int savedLevel)
    {
        // Force scroll to top instantly when opened
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }

        // 1. Prepare all badges immediately
        for (int i = 0; i < badges.Count; i++)
        {
            var badge = badges[i];
            if (badge.badgeUI != null)
            {
                // Reliably get the image even if it's on a child or inactive
                Image img = badge.badgeUI.GetComponent<Image>();
                if (img == null) img = badge.badgeUI.GetComponentInChildren<Image>(true);

                if (img != null)
                {
                    // Cache the original sprite the first time we see it
                    if (badge.unlockedSprite == null)
                    {
                        badge.unlockedSprite = img.sprite;
                    }

                    // If unlockLevel > savedLevel, it means the player HAS NOT reached it yet (Locked)
                    if (badge.unlockLevel > savedLevel)
                    {
                        if (lockedSprite != null)
                        {
                            img.sprite = lockedSprite;
                            badge.badgeUI.SetActive(true);
                            badge.badgeUI.transform.localScale = Vector3.one; 
                        }
                        else
                        {
                            badge.badgeUI.SetActive(false);
                        }
                    }
                    else
                    {
                        // Unlocked!
                        img.sprite = badge.unlockedSprite;
                        badge.badgeUI.SetActive(true); 
                        badge.badgeUI.transform.localScale = Vector3.zero;
                    }
                }
            }
        }

        bool hasPlayedAudio = false;

        // 2. Animate the unlocked badges one by one
        foreach (var badge in badges)
        {
            if (badge.unlockLevel <= savedLevel && badge.badgeUI != null)
            {
                badge.badgeUI.SetActive(true);
                StartCoroutine(PopAnimation(badge.badgeUI.transform));
                
                if (popSound != null && audioSource != null)
                {
                    if (!playAudioOnlyOnce || !hasPlayedAudio)
                    {
                        audioSource.PlayOneShot(popSound);
                        hasPlayedAudio = true;
                    }
                }

                yield return new WaitForSeconds(staggerDelay);
            }
        }
    }

    private IEnumerator PopAnimation(Transform target)
    {
        float elapsed = 0f;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one;

        // Save original rotation in case the badge isn't perfectly straight
        Quaternion originalRotation = target.localRotation;

        while (elapsed < popAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popAnimationDuration;
            
            // Apply a bouncy elastic tween
            float ease = ElasticEaseOut(t);
            target.localScale = Vector3.LerpUnclamped(startScale, endScale, ease);
            
            // Add a fun rotational wobble that decays over time
            float decay = 1f - t; 
            float wobbleAngle = Mathf.Sin(t * Mathf.PI * 4f) * 15f * decay; // Wobbles back and forth up to 15 degrees
            target.localRotation = originalRotation * Quaternion.Euler(0, 0, wobbleAngle);
            
            yield return null;
        }

        target.localScale = endScale;
        target.localRotation = originalRotation;
    }

    // A much bouncier, premium-feeling easing function
    private float ElasticEaseOut(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        
        float p = 0.3f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p) + 1f;
    }
}
