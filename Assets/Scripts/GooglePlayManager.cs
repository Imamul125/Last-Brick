using UnityEngine;

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
    }

    void Start()
    {
        SignInToGooglePlay();
    }

    public void SignInToGooglePlay()
    {
        Debug.Log("[GooglePlayManager] Leaderboard not set up yet. Using PlayerPrefs.");
    }

    public void PostScore(int score)
    {
        int currentHighScore = PlayerPrefs.GetInt("HighScore", 0);
        if (score > currentHighScore)
        {
            PlayerPrefs.SetInt("HighScore", score);
            PlayerPrefs.Save();
            Debug.Log($"[GooglePlayManager] New High Score {score} saved to PlayerPrefs.");
        }
        else
        {
            Debug.Log($"[GooglePlayManager] Score {score} posted (HighScore is {currentHighScore}).");
        }
    }

    public void ShowLeaderboardUI()
    {
        Debug.Log("[GooglePlayManager] Leaderboard UI not implemented yet.");
    }
}
