using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Quest Data")]
    public int totalBricksRemoved = 0;
    public int totalHammersUsed = 0;
    public int highestCombo = 0;

    // Thresholds
    public int bricksQuestTarget = 50;
    public int hammerQuestTarget = 2;
    public int comboQuestTarget = 5;

    public int questRewardCoins = 200;

    [Header("UI Reference")]
    // Since UI is dynamically built, we'll try to find these or build a basic debug UI if null
    private GameObject questPanel;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
        
        LoadQuests();
    }

    private void Start()
    {
        BrickInteractor.OnBrickRemoved += HandleBrickRemoved;
    }

    private void OnDestroy()
    {
        BrickInteractor.OnBrickRemoved -= HandleBrickRemoved;
    }

    private void LoadQuests()
    {
        totalBricksRemoved = PlayerPrefs.GetInt("Quest_Bricks", 0);
        totalHammersUsed = PlayerPrefs.GetInt("Quest_Hammers", 0);
        highestCombo = PlayerPrefs.GetInt("Quest_Combo", 0);
    }

    private void SaveQuests()
    {
        PlayerPrefs.SetInt("Quest_Bricks", totalBricksRemoved);
        PlayerPrefs.SetInt("Quest_Hammers", totalHammersUsed);
        PlayerPrefs.SetInt("Quest_Combo", highestCombo);
        PlayerPrefs.Save();
    }

    private void HandleBrickRemoved()
    {
        totalBricksRemoved++;
        SaveQuests();
        CheckQuestCompletion();
    }

    public void RecordHammerUsed()
    {
        totalHammersUsed++;
        SaveQuests();
        CheckQuestCompletion();
    }

    public void RecordCombo(int combo)
    {
        if (combo > highestCombo)
        {
            highestCombo = combo;
            SaveQuests();
            CheckQuestCompletion();
        }
    }

    private void CheckQuestCompletion()
    {
        // Simple logic: if reached target and not claimed (using PlayerPrefs bools)
        if (totalBricksRemoved >= bricksQuestTarget && PlayerPrefs.GetInt("Quest_Bricks_Claimed", 0) == 0)
        {
            ShowQuestCompletedPopup("Removed " + bricksQuestTarget + " Bricks!");
            PlayerPrefs.SetInt("Quest_Bricks_Claimed", 1);
        }

        if (totalHammersUsed >= hammerQuestTarget && PlayerPrefs.GetInt("Quest_Hammers_Claimed", 0) == 0)
        {
            ShowQuestCompletedPopup("Used " + hammerQuestTarget + " Hammers!");
            PlayerPrefs.SetInt("Quest_Hammers_Claimed", 1);
        }

        if (highestCombo >= comboQuestTarget && PlayerPrefs.GetInt("Quest_Combo_Claimed", 0) == 0)
        {
            ShowQuestCompletedPopup("Reached x" + comboQuestTarget + " Combo!");
            PlayerPrefs.SetInt("Quest_Combo_Claimed", 1);
        }
    }

    private void ShowQuestCompletedPopup(string questName)
    {
        // Give Reward immediately
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddCoin(questRewardCoins);
        }

        // Show a temporary UI popup
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            GameObject popup = new GameObject("QuestPopup");
            popup.transform.SetParent(canvas.transform, false);
            
            RectTransform rt = popup.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -300);
            rt.sizeDelta = new Vector2(800, 150);

            Image bg = popup.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.8f);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(popup.transform, false);
            TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
            txt.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            txt.text = $"QUEST COMPLETED!\n<size=40>{questName}</size>\n<color=#FFD700>+{questRewardCoins} COINS</color>";
            txt.fontSize = 50;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontStyle = FontStyles.Bold;
            textObj.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 150);

            Destroy(popup, 3.5f);
            
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayPlayerWinSound();
            }
        }
    }
}
