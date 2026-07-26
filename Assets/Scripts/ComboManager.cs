using System.Collections;
using UnityEngine;
using TMPro;

public class ComboManager : MonoBehaviour
{
    public static ComboManager Instance { get; private set; }

    [Header("Settings")]
    public float comboTimeout = 2.0f;
    public int bonusCoinsPerComboMultiplier = 5; // e.g., x2 = 10 coins, x3 = 15 coins

    [Header("State")]
    private int currentCombo = 0;
    private float comboTimer = 0f;
    public int TotalBonusCoinsEarned { get; private set; } = 0;

    [Header("UI Reference")]
    public GameObject comboTextPrefab; // A text prefab that pops up
    private Canvas mainCanvas;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    private void Start()
    {
        mainCanvas = FindAnyObjectByType<Canvas>();
        BrickInteractor.OnBrickRemoved += HandleBrickRemoved;
        
        // Listen to level start to reset combo
        LevelManager.Instance?.levels.ForEach(l => l.onLevelStart.AddListener(ResetComboState));
    }

    private void OnDestroy()
    {
        BrickInteractor.OnBrickRemoved -= HandleBrickRemoved;
    }

    private void ResetComboState()
    {
        currentCombo = 0;
        comboTimer = 0f;
        TotalBonusCoinsEarned = 0;
    }

    private void Update()
    {
        if (currentCombo > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0)
            {
                currentCombo = 0; // Combo dropped
            }
        }
    }

    private void HandleBrickRemoved()
    {
        currentCombo++;
        comboTimer = comboTimeout;

        if (currentCombo > 1)
        {
            // Award bonus coins
            int bonus = currentCombo * bonusCoinsPerComboMultiplier;
            TotalBonusCoinsEarned += bonus;

            // Show UI Popup
            ShowComboPopup(currentCombo);
            
            // Record max combo for quests
            if (QuestManager.Instance != null) {
                QuestManager.Instance.RecordCombo(currentCombo);
            }
        }
    }

    private GameObject currentComboPopup;

    private void ShowComboPopup(int comboMultiplier)
    {
        if (comboTextPrefab != null && mainCanvas != null)
        {
            GameObject popup = Instantiate(comboTextPrefab, mainCanvas.transform);
            
            // Randomize position widely to avoid overlap
            RectTransform rt = popup.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(Random.Range(-250f, 250f), Random.Range(150f, 450f));
            }

            TextMeshProUGUI txt = popup.GetComponent<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = $"Combo x{comboMultiplier}!";
            }

            // Simple animation
            StartCoroutine(AnimatePopup(popup));
        }
        else
        {
            // Fallback dynamic generation if prefab not assigned
            CreateDynamicComboPopup(comboMultiplier);
        }
    }

    private void CreateDynamicComboPopup(int combo)
    {
        if (mainCanvas == null) return;

        GameObject popup = new GameObject("ComboPopup");
        popup.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform rt = popup.AddComponent<RectTransform>();
        // Wide spread so they don't overlap as much
        rt.anchoredPosition = new Vector2(Random.Range(-300f, 300f), Random.Range(150f, 450f));
        rt.sizeDelta = new Vector2(400, 100);

        TextMeshProUGUI txt = popup.AddComponent<TextMeshProUGUI>();
        txt.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF"); // Default font
        txt.text = $"COMBO x{combo}!";
        txt.fontSize = 40 + (combo * 2); // Smaller text
        txt.color = new Color(1f, 0.7f, 0f, 1f); // Orange/Gold
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontStyle = FontStyles.Bold;

        StartCoroutine(AnimatePopup(popup));
    }

    private IEnumerator AnimatePopup(GameObject popup)
    {
        RectTransform rt = popup.GetComponent<RectTransform>();
        TextMeshProUGUI txt = popup.GetComponent<TextMeshProUGUI>();
        
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0, 100f); // Float up
        
        float duration = 1.0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            
            if (txt != null)
            {
                Color c = txt.color;
                c.a = 1.0f - (t * t); // Fade out
                txt.color = c;
            }

            yield return null;
        }

        Destroy(popup);
    }
}
