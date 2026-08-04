using UnityEngine;
using UnityEngine.UI;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using System;

public class GameAdManager : MonoBehaviour
{
    public static GameAdManager Instance { get; private set; }

    [Header("Global Ad Settings")]
    [Tooltip("If true, uses standard Google test ads. If false, uses your live ad IDs.")]
    public bool isTestMode = true;

    [Header("GDPR UI")]
    [Tooltip("Button to show GDPR privacy options. Hidden if not in EEA.")]
    public GameObject gdprPrivacyButton;

    [Header("Daily Reward Ad")]
    public Button daily_coins;
    public int dailyCoinRewardAmount = 50;
    private const string LastDailyAdTimeKey = "LastDailyAdTime";

    [Header("Level Complete Ad")]
    public int completeFrequency = 3;
    [Tooltip("Your real Interstitial Ad Unit ID for Level Complete")]
    public string liveCompleteAdIdAndroid = "ca-app-pub-1954957296482912/7380476731";
    private int levelsCompletedSinceLastAd = 0;
    private InterstitialAd completeAd;
    private Action currentCompleteAdClosedCallback;

    [Header("Retry Ad")]
    public int retryFrequency = 3;
    [Tooltip("Your real Interstitial Ad Unit ID for Retry")]
    public string liveRetryAdIdAndroid = "ca-app-pub-1954957296482912/1609275301";
    private int retriesSinceLastAd = 0;
    private InterstitialAd retryAd;
    private Action currentRetryAdClosedCallback;

    [Header("Rewarded Ad")]
    [Tooltip("Your real Rewarded Ad Unit ID")]
    public string liveRewardedAdIdAndroid = "ca-app-pub-1954957296482912/5516695335";
    private RewardedAd rewardedAd;
    private Action currentRewardedAdClosedCallback;
    private Action currentRewardEarnedCallback;

    // Standard Google Test IDs (Safe to use for testing)
    private string testInterstitialIdAndroid = "ca-app-pub-3940256099942544/1033173712";
    private string testRewardedIdAndroid = "ca-app-pub-3940256099942544/5224354917";

    private bool isAdMobInitialized = false;

    private void Awake()
    {
        // Singleton pattern to ensure only one Ad Manager exists and persists across scenes
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (daily_coins != null)
        {
            daily_coins.onClick.AddListener(OnDailyCoinsClicked);
            CheckDailyAdStatus();
        }

        // Request GDPR consent first (User Messaging Platform)
        ConsentRequestParameters request = new ConsentRequestParameters();

        ConsentInformation.Update(request, (FormError updateError) =>
        {
            if (updateError != null)
            {
                Debug.LogError("UMP Update Error: " + updateError.Message);
            }

            ConsentForm.LoadAndShowConsentFormIfRequired((FormError showError) =>
            {
                if (showError != null)
                {
                    Debug.LogError("UMP Show Error: " + showError.Message);
                }

                // Enable/disable the privacy button in UIManager if it exists
                UpdatePrivacyButton();

                // If consent is gathered or not required, init AdMob
                if (ConsentInformation.CanRequestAds())
                {
                    InitializeAdMob();
                }
            });
        });

        // Fallback: If we already had consent from a previous session, init immediately
        if (ConsentInformation.CanRequestAds())
        {
            InitializeAdMob();
        }
    }

    private void InitializeAdMob()
    {
        if (isAdMobInitialized) return;

        MobileAds.Initialize(initStatus => {
            Debug.Log("AdMob Initialized.");
            isAdMobInitialized = true;
            LoadCompleteAd();
            LoadRetryAd();
            LoadRewardedAd();
        });
    }

    public void UpdatePrivacyButton()
    {
        if (gdprPrivacyButton != null)
        {
            // Only show the button if the user is in a region that requires privacy options (like EEA)
            bool shouldShow = ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
            gdprPrivacyButton.SetActive(shouldShow);
        }
    }

    /// <summary>
    /// Call this from your GDPR Settings Button OnClick event.
    /// </summary>
    public void ShowPrivacyOptionsForm()
    {
        ConsentForm.ShowPrivacyOptionsForm((FormError formError) =>
        {
            if (formError != null)
            {
                Debug.LogError("Error showing privacy options form: " + formError.Message);
            }
        });
    }

    // ==========================================
    // LEVEL COMPLETE AD LOGIC
    // ==========================================

    public void OnLevelCompleted(Action onCompleteCallback = null)
    {
        levelsCompletedSinceLastAd++;
        Debug.Log($"Level completed. Progress to next ad: {levelsCompletedSinceLastAd} / {completeFrequency}");

        if (levelsCompletedSinceLastAd >= completeFrequency)
        {
            if (ShowCompleteAd(onCompleteCallback)) return;
        }
        
        onCompleteCallback?.Invoke();
    }

    private void LoadCompleteAd()
    {
        if (completeAd != null)
        {
            completeAd.Destroy();
            completeAd = null;
        }

        var adRequest = new AdRequest();
        string adUnitId = isTestMode ? testInterstitialIdAndroid : liveCompleteAdIdAndroid;

        InterstitialAd.Load(adUnitId, adRequest, (InterstitialAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null) return;
            completeAd = ad;
            
            completeAd.OnAdFullScreenContentClosed += () =>
            {
                levelsCompletedSinceLastAd = 0;
                LoadCompleteAd();
                currentCompleteAdClosedCallback?.Invoke();
                currentCompleteAdClosedCallback = null;
            };

            completeAd.OnAdFullScreenContentFailed += (AdError e) =>
            {
                LoadCompleteAd();
                currentCompleteAdClosedCallback?.Invoke();
                currentCompleteAdClosedCallback = null;
            };
        });
    }

    private bool ShowCompleteAd(Action onCompleteCallback)
    {
        if (completeAd != null && completeAd.CanShowAd())
        {
            currentCompleteAdClosedCallback = onCompleteCallback;
            completeAd.Show();
            return true;
        }
        LoadCompleteAd();
        return false;
    }

    // ==========================================
    // RETRY AD LOGIC
    // ==========================================

    public void OnLevelRetry(Action onCompleteCallback = null)
    {
        retriesSinceLastAd++;
        Debug.Log($"Level retried. Progress to next ad: {retriesSinceLastAd} / {retryFrequency}");

        if (retriesSinceLastAd >= retryFrequency)
        {
            if (ShowRetryAd(onCompleteCallback)) return;
        }
        
        onCompleteCallback?.Invoke();
    }

    private void LoadRetryAd()
    {
        if (retryAd != null)
        {
            retryAd.Destroy();
            retryAd = null;
        }

        var adRequest = new AdRequest();
        string adUnitId = isTestMode ? testInterstitialIdAndroid : liveRetryAdIdAndroid;

        InterstitialAd.Load(adUnitId, adRequest, (InterstitialAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null) return;
            retryAd = ad;
            
            retryAd.OnAdFullScreenContentClosed += () =>
            {
                retriesSinceLastAd = 0;
                LoadRetryAd();
                currentRetryAdClosedCallback?.Invoke();
                currentRetryAdClosedCallback = null;
            };

            retryAd.OnAdFullScreenContentFailed += (AdError e) =>
            {
                LoadRetryAd();
                currentRetryAdClosedCallback?.Invoke();
                currentRetryAdClosedCallback = null;
            };
        });
    }

    private bool ShowRetryAd(Action onCompleteCallback)
    {
        if (retryAd != null && retryAd.CanShowAd())
        {
            currentRetryAdClosedCallback = onCompleteCallback;
            retryAd.Show();
            return true;
        }
        LoadRetryAd();
        return false;
    }

    // ==========================================
    // REWARDED AD LOGIC
    // ==========================================

    /// <summary>
    /// Call this when a user clicks a button to watch an ad for a reward.
    /// Example: GameAdManager.Instance.ShowRewardedAd(() => { GivePlayerCoins(50); });
    /// </summary>
    public void ShowRewardedAd(Action onRewardEarned, Action onAdClosed = null)
    {
        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            Debug.Log("Showing rewarded ad.");
            currentRewardEarnedCallback = onRewardEarned;
            currentRewardedAdClosedCallback = onAdClosed;
            
            rewardedAd.Show((Reward reward) =>
            {
                Debug.Log($"Reward earned! Amount: {reward.Amount}, Type: {reward.Type}");
                // Fire the callback so the calling script can give the reward
                currentRewardEarnedCallback?.Invoke();
            });
        }
        else
        {
            Debug.LogError("Rewarded ad is not ready yet.");
            LoadRewardedAd(); // Try loading one for next time
            
            // Tell the UI it failed so they can resume or show an error
            onAdClosed?.Invoke();
        }
    }

    private void LoadRewardedAd()
    {
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        var adRequest = new AdRequest();
        string adUnitId = isTestMode ? testRewardedIdAndroid : liveRewardedAdIdAndroid;

        RewardedAd.Load(adUnitId, adRequest, (RewardedAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogError("Rewarded ad failed to load with error : " + error);
                return;
            }

            Debug.Log("Rewarded ad loaded with response : " + ad.GetResponseInfo());
            rewardedAd = ad;
            
            rewardedAd.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("Rewarded Ad full screen content closed.");
                LoadRewardedAd();
                currentRewardedAdClosedCallback?.Invoke();
                
                // Clean up callbacks
                currentRewardedAdClosedCallback = null;
                currentRewardEarnedCallback = null;
            };

            rewardedAd.OnAdFullScreenContentFailed += (AdError e) =>
            {
                Debug.LogError("Rewarded ad failed to open full screen content with error : " + e);
                LoadRewardedAd();
                currentRewardedAdClosedCallback?.Invoke();
                
                // Clean up callbacks
                currentRewardedAdClosedCallback = null;
                currentRewardEarnedCallback = null;
            };
        });
    }

    private void CheckDailyAdStatus()
    {
        if (daily_coins == null) return;
        
        string lastTimeString = PlayerPrefs.GetString(LastDailyAdTimeKey, "");
        if (string.IsNullOrEmpty(lastTimeString))
        {
            daily_coins.gameObject.SetActive(true);
            daily_coins.interactable = true;
        }
        else
        {
            DateTime lastTime;
            if (DateTime.TryParse(lastTimeString, out lastTime))
            {
                if ((DateTime.Now - lastTime).TotalHours >= 24)
                {
                    daily_coins.gameObject.SetActive(true);
                    daily_coins.interactable = true;
                }
                else
                {
                    daily_coins.gameObject.SetActive(false);
                }
            }
            else
            {
                daily_coins.gameObject.SetActive(true);
                daily_coins.interactable = true;
            }
        }
    }

    private void OnDailyCoinsClicked()
    {
        if (daily_coins != null)
            daily_coins.interactable = false;

        ShowRewardedAd(() => 
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddCoin(dailyCoinRewardAmount);
            }
            
            PlayerPrefs.SetString(LastDailyAdTimeKey, DateTime.Now.ToString());
            PlayerPrefs.Save();
        }, 
        () => 
        {
            CheckDailyAdStatus();
        });
    }
}
