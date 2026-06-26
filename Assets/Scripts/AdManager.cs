using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using System;

public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    [Header("Ad Settings")]
    [Tooltip("Toggle OFF before launching to production!")]
    public bool useTestAds = true;

    // ─── Real Ad Unit IDs ───
    private const string REAL_REWARDED_ANDROID      = "ca-app-pub-1954957296482912/5363242581";
    private const string REAL_INTERSTITIAL_ANDROID   = "ca-app-pub-1954957296482912/9694540692";

    // ─── Google Test Ad Unit IDs ───
    private const string TEST_REWARDED_ANDROID      = "ca-app-pub-3940256099942544/5224354917";
    private const string TEST_INTERSTITIAL_ANDROID   = "ca-app-pub-3940256099942544/1033173712";
    private const string TEST_REWARDED_IOS          = "ca-app-pub-3940256099942544/1712485313";
    private const string TEST_INTERSTITIAL_IOS       = "ca-app-pub-3940256099942544/4411468910";

    private string rewardedAdUnitId;
    private string interstitialAdUnitId;
    private RewardedAd rewardedAd;
    private InterstitialAd interstitialAd;
    private bool _isMobileAdsInitialized = false;
    
    void Awake()
    {
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

        // ─── Select Ad Unit IDs based on toggle ───
#if UNITY_ANDROID
        rewardedAdUnitId      = useTestAds ? TEST_REWARDED_ANDROID : REAL_REWARDED_ANDROID;
        interstitialAdUnitId  = useTestAds ? TEST_INTERSTITIAL_ANDROID : REAL_INTERSTITIAL_ANDROID;
#elif UNITY_IPHONE
        rewardedAdUnitId      = useTestAds ? TEST_REWARDED_IOS : "unused";
        interstitialAdUnitId  = useTestAds ? TEST_INTERSTITIAL_IOS : "unused";
#else
        rewardedAdUnitId = "unused";
        interstitialAdUnitId = "unused";
#endif
        Debug.Log($"[AdManager] Mode: {(useTestAds ? "TEST ADS" : "REAL ADS")} | Rewarded: {rewardedAdUnitId} | Interstitial: {interstitialAdUnitId}");

        // ─── Start consent flow, then initialize ads ───
        RequestConsentAndInitialize();
    }

    /// <summary>
    /// Requests GDPR consent using UMP SDK before initializing ads.
    /// For non-EEA users, the consent form won't appear and ads initialize immediately.
    /// </summary>
    private void RequestConsentAndInitialize()
    {
        // Create consent request parameters
        var requestParameters = new ConsentRequestParameters();

        // Request the latest consent information
        ConsentInformation.Update(requestParameters, (FormError updateError) =>
        {
            if (updateError != null)
            {
                Debug.LogError("[AdManager] Consent update error: " + updateError.Message);
                // Still initialize ads even if consent fails (ads will be non-personalized)
                InitializeMobileAds();
                return;
            }

            Debug.Log($"[AdManager] Consent status: {ConsentInformation.ConsentStatus}");

            // Load and show the consent form if required
            ConsentForm.LoadAndShowConsentFormIfRequired((FormError formError) =>
            {
                if (formError != null)
                {
                    Debug.LogError("[AdManager] Consent form error: " + formError.Message);
                }

                Debug.Log($"[AdManager] Consent gathered. CanRequestAds: {ConsentInformation.CanRequestAds()}");

                // Initialize ads after consent is handled
                InitializeMobileAds();
            });
        });
    }

    /// <summary>
    /// Initializes the Google Mobile Ads SDK (only once).
    /// </summary>
    private void InitializeMobileAds()
    {
        if (_isMobileAdsInitialized)
            return;

        if (!ConsentInformation.CanRequestAds())
        {
            Debug.LogWarning("[AdManager] Cannot request ads (consent not granted). Skipping initialization.");
            return;
        }

        _isMobileAdsInitialized = true;

        MobileAds.Initialize(initStatus => {
            Debug.Log("[AdManager] MobileAds initialized successfully.");
            LoadRewardedAd();
            LoadInterstitialAd();
        });
    }

    /// <summary>
    /// Returns true if a "Privacy Settings" button should be shown in your UI.
    /// Only required for users in US states with privacy laws.
    /// </summary>
    public bool IsPrivacyOptionsRequired()
    {
        return ConsentInformation.PrivacyOptionsRequirementStatus ==
               PrivacyOptionsRequirementStatus.Required;
    }

    /// <summary>
    /// Opens the privacy/consent settings form so users can change their choices.
    /// Call this from a "Privacy Settings" button in your settings/pause menu.
    /// </summary>
    public void ShowPrivacyOptionsForm()
    {
        ConsentForm.ShowPrivacyOptionsForm((FormError error) =>
        {
            if (error != null)
            {
                Debug.LogError("[AdManager] Privacy options form error: " + error.Message);
            }
        });
    }

    public void LoadRewardedAd()
    {
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        var adRequest = new AdRequest();

        RewardedAd.Load(rewardedAdUnitId, adRequest,
            (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogError("Rewarded ad failed to load: " + error);
                    return;
                }
                rewardedAd = ad;
            });
    }

    public void LoadInterstitialAd()
    {
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }

        var adRequest = new AdRequest();

        InterstitialAd.Load(interstitialAdUnitId, adRequest,
            (InterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogError("Interstitial ad failed to load: " + error);
                    return;
                }
                interstitialAd = ad;
            });
    }

    // Call this from LevelManager for hints
    public void ShowRewardedAd(Action onSuccess, Action onClosed)
    {
        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            rewardedAd.OnAdFullScreenContentClosed += () => 
            {
                LoadRewardedAd();
                onClosed?.Invoke();
            };

            rewardedAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                LoadRewardedAd();
                onClosed?.Invoke();
            };

            rewardedAd.Show((Reward reward) =>
            {
                onSuccess?.Invoke();
            });
        }
        else
        {
            Debug.LogWarning("Rewarded ad is not ready yet.");
            onClosed?.Invoke();
            LoadRewardedAd();
        }
    }

    // Call this from LevelManager for between levels
    public void ShowInterstitialAd(Action onClosed)
    {
        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            interstitialAd.OnAdFullScreenContentClosed += () => 
            {
                LoadInterstitialAd();
                onClosed?.Invoke();
            };

            interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                LoadInterstitialAd();
                onClosed?.Invoke();
            };

            interstitialAd.Show();
        }
        else
        {
            Debug.LogWarning("Interstitial ad is not ready yet.");
            onClosed?.Invoke();
            LoadInterstitialAd();
        }
    }
}
