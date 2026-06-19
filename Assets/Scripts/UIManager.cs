using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Top Bar UI")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI movesText;
    public TextMeshProUGUI coinsText;

    [Header("Objective UI")]
    public TextMeshProUGUI objectiveProgressText;

    [Header("State Values")]
    public int currentMoves = 0;
    public int currentCoins = 0;
    public int targetObjective = 15;
    public int currentObjectiveProgress = 0;

    private void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
        }
        else 
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        UpdateAllUI();
    }

    public void AddMove()
    {
        currentMoves++;
        UpdateAllUI();
    }

    public void AddCoin(int amount)
    {
        currentCoins += amount;
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
            levelText.text = "LEVEL <color=#FFB700>" + level.ToString() + "</color>";
    }

    private void UpdateAllUI()
    {
        if (movesText != null) movesText.text = "MOVES: <color=#FFB700>" + currentMoves.ToString() + "</color>";
        if (coinsText != null) coinsText.text = currentCoins.ToString();
        if (objectiveProgressText != null) objectiveProgressText.text = "<color=#FFB700>" + currentObjectiveProgress + "</color>/" + targetObjective;
    }
}
