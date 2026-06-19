using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public class LevelData
{
    public int levelNumber;
    public GameObject protectBrick; // Reference to the unique brick
    public UnityEvent onLevelStart;
    public UnityEvent onLevelComplete;
}

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Levels Setup")]
    public List<LevelData> levels = new List<LevelData>();

    [Header("UI References")]
    public GameObject congratsUi;
    public GameObject retryUi;

    [Header("State")]
    public int currentLevelIndex = 0;
    private bool levelEnded = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Automatically start the first level if the list is populated
        if (levels.Count > 0)
        {
            StartLevel(0);
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

        if (congratsUi != null) congratsUi.SetActive(false);
        if (retryUi != null) retryUi.SetActive(false);
        
        Debug.Log("Level " + currentLevel.levelNumber + " Started!");
        currentLevel.onLevelStart?.Invoke();
    }

    public void TriggerWin()
    {
        if (levelEnded) return;
        levelEnded = true;
        Debug.Log("Protect Brick reached the pedestal! YOU WIN!");
        
        if (congratsUi != null) congratsUi.SetActive(true);
        
        CompleteCurrentLevel();
        Invoke(nameof(LoadNextLevel), 3f); // Wait 3 seconds then start next level
    }

    public void TriggerLoss()
    {
        if (levelEnded) return;
        levelEnded = true;
        Debug.Log("Protect Brick hit the ground! YOU LOSE!");
        
        if (retryUi != null) retryUi.SetActive(true);
    }

    public void CompleteCurrentLevel()
    {
        if (currentLevelIndex < 0 || currentLevelIndex >= levels.Count) return;

        LevelData currentLevel = levels[currentLevelIndex];
        currentLevel.onLevelComplete?.Invoke();
    }

    // Helper to easily progress to the next level in the list
    public void LoadNextLevel()
    {
        if (currentLevelIndex + 1 < levels.Count)
        {
            StartLevel(currentLevelIndex + 1);
        }
        else
        {
            Debug.Log("All levels completed!");
        }
    }
}
