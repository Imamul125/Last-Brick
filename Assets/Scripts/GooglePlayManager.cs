using UnityEngine;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#elif UNITY_IOS
using UnityEngine.SocialPlatforms;
using UnityEngine.SocialPlatforms.GameCenter;
#endif

/// <summary>
/// Leaderboard sign-in, score posting and UI.
/// Android uses Google Play Games; iOS uses Game Center through Unity's Social API.
/// </summary>
public class GooglePlayManager : MonoBehaviour
{
    public static GooglePlayManager Instance { get; private set; }

    [Tooltip("Leaderboard button; hidden on platforms without a leaderboard service")]
    public GameObject leaderboardButton;

    [Tooltip("Game Center leaderboard ID (App Store Connect > Game Center > Leaderboards)")]
    public string gameCenterLeaderboardId = "lastbrick_best_score";

    void Awake()
    {
#if !UNITY_ANDROID && !UNITY_IOS
        if (leaderboardButton != null) leaderboardButton.SetActive(false);
#endif

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
#elif UNITY_IOS
        AuthenticateGameCenter(null);
#endif
    }

#if UNITY_IOS
    private void AuthenticateGameCenter(System.Action onSuccess)
    {
        Social.localUser.Authenticate((bool success, string error) =>
        {
            if (success)
            {
                Debug.Log("[GooglePlayManager] Signed in to Game Center.");
                if (BadgeManager.Instance != null) BadgeManager.Instance.SyncEarnedAchievements();
                PostScore();
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogWarning("[GooglePlayManager] Game Center sign in failed: " + error);
            }
        });
    }
#endif

    /// <summary>
    /// Score = (Highest Level * 100,000,000,000) + ((99,999 - Retries) * 1,000,000) + Lifetime Coins
    /// </summary>
    private long GetPackedScore()
    {
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

        return (currentLevel * 100000000000L) + ((maxRetriesTracked - clampedRetries) * 1000000L) + clampedCoins;
    }

    public void PostScore()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (!Social.localUser.authenticated)
        {
            Debug.LogWarning("[GooglePlayManager] Cannot post score, user not authenticated.");
            return;
        }

        long packedScore = GetPackedScore();
#if UNITY_ANDROID
        string leaderboardId = GPGSIds.leaderboard_best_score;
#else
        string leaderboardId = gameCenterLeaderboardId;
#endif

        Social.ReportScore(packedScore, leaderboardId, (bool success) =>
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
#elif UNITY_IOS
        if (Social.localUser.authenticated)
        {
            ShowGameCenterLeaderboard();
        }
        else
        {
            // iOS only shows the Game Center sign-in sheet once per app launch; after that the
            // player has to sign in from Settings > Game Center.
            AuthenticateGameCenter(ShowGameCenterLeaderboard);
        }
#endif
    }

#if UNITY_IOS
    private void ShowGameCenterLeaderboard()
    {
        // Submit the latest score first so the player sees themselves on the board.
        PostScore();
        GameCenterPlatform.ShowLeaderboardUI(gameCenterLeaderboardId, TimeScope.AllTime);
    }
#endif
}
