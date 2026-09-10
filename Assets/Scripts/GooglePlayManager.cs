// Google Play Games is an Android-only plugin, so the real implementation cannot compile for the
// WebGL target. Web builds (the Playworks playable ad, and any plain WebGL build) get an inert
// shell instead, which keeps the LevelManager call sites valid without any change to Android.
#if UNITY_WEBGL
using UnityEngine;

public class GooglePlayManager : MonoBehaviour
{
    public static GooglePlayManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void PostScore() { }
}
#else
using UnityEngine;
using GooglePlayGames;
using GooglePlayGames.BasicApi;

public class GooglePlayManager : MonoBehaviour
{
    public static GooglePlayManager Instance { get; private set; }

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

        InitializeGPGS();
    }

    void Start()
    {
        SignInToGooglePlay();
    }

    private void InitializeGPGS()
    {
#if UNITY_ANDROID
        PlayGamesPlatform.DebugLogEnabled = true;
        PlayGamesPlatform.Activate();
#endif
    }

    public void SignInToGooglePlay()
    {
#if UNITY_ANDROID
        PlayGamesPlatform.Instance.Authenticate((SignInStatus status) =>
        {
            if (status == SignInStatus.Success)
            {
                Debug.Log("[GooglePlayManager] Successfully Signed In to Google Play Games!");
            }
            else
            {
                Debug.LogWarning("[GooglePlayManager] Failed to Sign In to Google Play Games. Status: " + status);
            }
        });
#endif
    }

    public void PostScore()
    {
#if UNITY_ANDROID
        if (!Social.localUser.authenticated)
        {
            Debug.LogWarning("[GooglePlayManager] Cannot post score, user not authenticated.");
            return;
        }

        // 1. Get Highest Level (from PlayerPrefs, LevelManager saves it as "SavedLevel")
        int currentLevel = PlayerPrefs.GetInt("SavedLevel", 0);
        
        // 2. Get Total Retries
        long totalRetries = PlayerPrefs.GetInt("TotalRetries", 0);
        
        // 3. Get Lifetime Coins
        long lifetimeCoins = PlayerPrefs.GetInt("LifetimeCoins", 0);

        // Clamp values just to be safe
        long maxRetriesTracked = 99999;
        long clampedRetries = System.Math.Min(totalRetries, maxRetriesTracked);
        long clampedCoins = System.Math.Min(lifetimeCoins, 9999999L);

        // Formula: Score = (Highest Level * 100,000,000,000) + ((99,999 - Retries) * 1,000,000) + Lifetime Coins
        long packedScore = (currentLevel * 100000000000L) + ((maxRetriesTracked - clampedRetries) * 1000000L) + clampedCoins;

        Social.ReportScore(packedScore, GPGSIds.leaderboard_best_score, (bool success) =>
        {
            if (success)
            {
                Debug.Log($"[GooglePlayManager] Successfully posted packed score: {packedScore}");
            }
            else
            {
                Debug.LogWarning("[GooglePlayManager] Failed to post score.");
            }
        });
#endif
    }

    public void ShowLeaderboardUI()
    {
#if UNITY_ANDROID
        if (Social.localUser.authenticated)
        {
            PlayGamesPlatform.Instance.ShowLeaderboardUI(GPGSIds.leaderboard_best_score);
        }
        else
        {
            PlayGamesPlatform.Instance.ManuallyAuthenticate((SignInStatus status) =>
            {
                if (status == SignInStatus.Success)
                {
                    PlayGamesPlatform.Instance.ShowLeaderboardUI(GPGSIds.leaderboard_best_score);
                }
                else
                {
                    Debug.LogWarning("[GooglePlayManager] Failed manual sign in for leaderboard. Status: " + status);
                }
            });
        }
#endif
    }
}

#endif
