using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Top Bar UI")]
    public List<GameObject> sceneUiElements = new List<GameObject>();
    public GameObject coinsUiTarget;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI movesText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI timerText2;
    public TextMeshProUGUI coinsText;
    public TextMeshProUGUI anotherCoinsText;

    [Header("Objective UI")]
    public TextMeshProUGUI objectiveProgressText;

    [Header("Debug Settings")]
    [Tooltip("Check this to override your saved coins with the debug amount on Start.")]
    public bool overrideCoinsForTesting = false;
    public int debugCoinAmount = 60000;

    [Header("State Values")]
    public int currentMoves = 0;
    public int maxMovesForLevel = 15;
    public int MovesRemaining => Mathf.Max(0, maxMovesForLevel - currentMoves);

    public int currentCoins = 0;
    public int targetObjective = 15;
    public int currentObjectiveProgress = 0;

    [Header("Animation Settings")]
    [Tooltip("How much the moves text scales up when running out of moves.")]
    public float movesTextAnimScale = 1.5f;
    [Tooltip("How fast the moves text zooms in and out.")]
    public float movesTextAnimDuration = 0.15f;

    private float timeRemaining;
    private bool timerRunning = false;
    private int lastTimerSecond = -1;
    
    private Coroutine movesAnimCoroutine;
    private Vector3 originalMovesTextScale = Vector3.one;

    private void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
        }
        else 
        {
            Destroy(this);
        }
    }

    private void Start()
    {
#if UNITY_EDITOR
        if (overrideCoinsForTesting)
        {
            PlayerPrefs.SetInt("SavedCoins", debugCoinAmount);
            PlayerPrefs.Save();
            overrideCoinsForTesting = false; // Reset to avoid constant overriding if not wanted
        }
#endif

        currentCoins = PlayerPrefs.GetInt("SavedCoins", 0);
        if (movesText != null)
        {
            originalMovesTextScale = movesText.transform.localScale;
        }
        UpdateAllUI();
    }

    public void ShowSceneUI()
    {
        foreach (var ui in sceneUiElements)
        {
            if (ui != null) ui.SetActive(true);
        }
    }

    public void HideSceneUI()
    {
        foreach (var ui in sceneUiElements)
        {
            if (ui != null) ui.SetActive(false);
        }
    }

    public void ActivateCoinsUI()
    {
        if (coinsUiTarget != null) coinsUiTarget.SetActive(true);
    }

    private void Update()
    {
        if (!timerRunning || LevelManager.Instance == null || LevelManager.Instance.IsLevelEnded) return;

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0)
        {
            timeRemaining = 0;
            timerRunning = false;
            UpdateTimerUI();
            LevelManager.Instance.TriggerLoss();
            return;
        }

        UpdateTimerUI();

        if (timeRemaining <= 10f)
        {
            int currentSecond = Mathf.CeilToInt(timeRemaining);
            if (currentSecond != lastTimerSecond)
            {
                lastTimerSecond = currentSecond;
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlayTimerTickSound();
                }
            }
        }
    }

    public void SetupLevelLimits(int maxMoves, float timeLimit)
    {
        maxMovesForLevel = maxMoves;
        currentMoves = 0;
        
        timeRemaining = timeLimit;
        timerRunning = timeLimit > 0;
        lastTimerSecond = -1;
        
        UpdateAllUI();
    }

    public void AddMove()
    {
        currentMoves++;
        UpdateAllUI();

        if (maxMovesForLevel > 0 && MovesRemaining < 4)
        {
            if (movesAnimCoroutine != null) StopCoroutine(movesAnimCoroutine);
            movesAnimCoroutine = StartCoroutine(AnimateMovesText());
        }
    }

    private System.Collections.IEnumerator AnimateMovesText()
    {
        if (movesText == null) yield break;

        float duration = movesTextAnimDuration;
        float elapsed = 0f;
        Vector3 targetScale = originalMovesTextScale * movesTextAnimScale;

        Debug.Log($"[UIManager] AnimateMovesText started! Original Scale: {originalMovesTextScale}, Target Scale: {targetScale}, Duration: {duration}");

        // Zoom In
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            movesText.transform.localScale = Vector3.Lerp(originalMovesTextScale, targetScale, t);
            yield return null;
        }
        
        movesText.transform.localScale = targetScale;
        elapsed = 0f;

        // Zoom Out
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            movesText.transform.localScale = Vector3.Lerp(targetScale, originalMovesTextScale, t);
            yield return null;
        }

        movesText.transform.localScale = originalMovesTextScale;
    }

    public void AddCoin(int amount)
    {
        currentCoins += amount;
        PlayerPrefs.SetInt("SavedCoins", currentCoins);

        // Track lifetime coins (only for additions, not when buying things)
        if (amount > 0)
        {
            int lifetimeCoins = PlayerPrefs.GetInt("LifetimeCoins", 0);
            lifetimeCoins += amount;
            PlayerPrefs.SetInt("LifetimeCoins", lifetimeCoins);
        }

        PlayerPrefs.Save();
        UpdateAllUI();
    }

    public void AddObjectiveProgress()
    {
        currentObjectiveProgress++;
        UpdateAllUI();
    }

    public void SetLevel(int level)
    {
        if (levelText != null)
            levelText.text = level.ToString();
    }

    public void UpdateAllUI()
    {
        if (levelText != null)
        {
            // Level is index + 1
            int currentLevelDisplay = PlayerPrefs.GetInt("SavedLevel", 0) + 1;
            levelText.text = currentLevelDisplay.ToString();
        }

        if (movesText != null) 
        {
            if (maxMovesForLevel > 0)
                movesText.text = MovesRemaining.ToString();
            else
                movesText.text = "∞";
        }

        UpdateTimerUI();

        if (coinsText != null) coinsText.text = currentCoins.ToString();
        if (anotherCoinsText != null) anotherCoinsText.text = currentCoins.ToString();
        if (objectiveProgressText != null) objectiveProgressText.text = currentObjectiveProgress + "/" + targetObjective;
    }

    private void UpdateTimerUI()
    {
        string textToDisplay = "";

        if (!timerRunning && maxMovesForLevel > 0 && timeRemaining <= 0) // if timeLimit is 0 it's infinite, if time remaining is 0 it's game over
        {
            textToDisplay = "<color=#FF0000>00:00</color>";
        }
        else if (!timerRunning)
        {
            textToDisplay = "∞";
        }
        else
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60F);
            int seconds = Mathf.FloorToInt(timeRemaining - minutes * 60);
            string timeString = string.Format("{0:00}:{1:00}", minutes, seconds);

            if (timeRemaining <= 10f)
            {
                textToDisplay = "<color=#FF0000>" + timeString + "</color>";
            }
            else
            {
                textToDisplay = timeString;
            }
        }

        if (timerText != null) timerText.text = textToDisplay;
        if (timerText2 != null) timerText2.text = textToDisplay;
    }
}
