using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DynamicPowerUpButton : MonoBehaviour
{
    public enum PowerUpType { Hammer, Undo }
    
    [Header("Settings")]
    public PowerUpType type;
    public int unlockLevel = 5; // Level at which this feature unlocks (1-indexed)
    
    [Header("UI References")]
    public Image buttonImage;
    public TextMeshProUGUI buttonText;
    
    [Header("Sprites")]
    public Sprite coinModeSprite;
    public Sprite adModeSprite;

    private Button myButton;

    private void Awake()
    {
        myButton = GetComponent<Button>();
    }

    private void Update()
    {
        if (UIManager.Instance == null || PowerUpManager.Instance == null) return;
        
        bool isUnlocked = LevelManager.Instance != null && (LevelManager.Instance.currentLevelIndex + 1) >= unlockLevel;

        if (!isUnlocked)
        {
            if (buttonImage != null) buttonImage.enabled = false;
            if (buttonText != null) buttonText.gameObject.SetActive(false);
            if (myButton != null) myButton.interactable = false;
            return;
        }
        else
        {
            if (buttonImage != null) buttonImage.enabled = true;
            if (myButton != null) myButton.interactable = true;
        }

        int currentCoins = UIManager.Instance.currentCoins;
        int cost = type == PowerUpType.Hammer ? PowerUpManager.Instance.hammerCost : PowerUpManager.Instance.undoCost;

        if (currentCoins >= cost)
        {
            // Can afford with coins
            if (buttonImage != null && coinModeSprite != null) buttonImage.sprite = coinModeSprite;

            if (buttonText != null)
            {
                buttonText.gameObject.SetActive(true);
                buttonText.text = $"{type.ToString().ToUpper()}\n{cost} COINS";
                buttonText.color = Color.white;
            }
        }
        else
        {
            // Cannot afford, fallback to Ad
            if (buttonImage != null && adModeSprite != null) buttonImage.sprite = adModeSprite;

            if (buttonText != null)
            {
                // Disable text in ad mode as requested
                buttonText.gameObject.SetActive(false);
            }
        }
    }
}
