using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class PetData
{
    [Tooltip("The name of this pet")]
    public string petName;
    [Tooltip("The 3D model for this pet")]
    public GameObject model;
    [Tooltip("The required coins to unlock this pet")]
    public int cost;
    [Tooltip("The Unity IAP Product ID for this pet (if any)")]
    public string iapProductID;
}

public class PetSelectionManager : MonoBehaviour
{
    public static PetSelectionManager Instance { get; private set; }

    [Header("Pet Settings (Place in order)")]
    [Tooltip("Configure the 3D model and required coins for each pet. Index 0 is the default Cat.")]
    public List<PetData> pets = new List<PetData>();

    [Header("UI References")]
    public GameObject petNameParent;
    public TextMeshProUGUI petNameText;
    public Button nextButton;
    public Button prevButton;
    public GameObject lockOverlay;
    public Button playButton; // Reference to main play button so we can disable it if pet is locked
    public Button invisibleButton;
    
    [Header("New UI & Settings")]
    public Button unlockButton;
    public Button customizeButton;
    public bool useCustomization = true;

    [Header("Shop & Coins")]
    public GameObject buyPanel;
    public TextMeshProUGUI requiredCoinsText;

    [Header("IAP Pet Buying")]
    public GameObject iapBuyDialog;
    public Button iapBuyButton;
    public TextMeshProUGUI iapPriceText;

    private int currentIndex = 0;

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
        // Add listeners to buttons
        if (nextButton != null) nextButton.onClick.AddListener(NextPet);
        if (prevButton != null) prevButton.onClick.AddListener(PrevPet);
        if (playButton != null) playButton.onClick.AddListener(SaveSelectedPet);
        if (unlockButton != null) unlockButton.onClick.AddListener(OnUnlockButtonClicked);

        // Ensure only the first pet is visible initially
        UpdatePetDisplay();
    }

    public void NextPet()
    {
        if (pets.Count == 0 || currentIndex >= pets.Count - 1) return;

        currentIndex++;
        UpdatePetDisplay();
    }

    public void PrevPet()
    {
        if (pets.Count == 0 || currentIndex <= 0) return;

        currentIndex--;
        UpdatePetDisplay();
    }

    private void UpdatePetDisplay()
    {
        // 1. Toggle visibility of 3D models on the podium
        for (int i = 0; i < pets.Count; i++)
        {
            if (pets[i] != null && pets[i].model != null)
            {
                pets[i].model.SetActive(i == currentIndex);
            }
        }

        // 2. Check unlock status
        bool isUnlocked = PlayerPrefs.GetInt("PetUnlocked_" + currentIndex, currentIndex == 0 ? 1 : 0) == 1;

        if (petNameParent != null)
        {
            petNameParent.SetActive(!isUnlocked);
        }

        if (petNameText != null && currentIndex < pets.Count)
        {
            petNameText.text = pets[currentIndex].petName;
        }

        // 3. Update UI overlays and Buttons
        if (lockOverlay != null)
        {
            lockOverlay.SetActive(!isUnlocked);
        }

        if (playButton != null)
        {
            playButton.gameObject.SetActive(isUnlocked);
        }

        if (unlockButton != null)
        {
            unlockButton.gameObject.SetActive(!isUnlocked);
        }

        if (customizeButton != null)
        {
            customizeButton.gameObject.SetActive(isUnlocked && useCustomization);
        }

        if (invisibleButton != null)
        {
            invisibleButton.interactable = isUnlocked;
        }

        // 4. Update Button Interactable States
        if (nextButton != null)
        {
            nextButton.interactable = (currentIndex < pets.Count - 1);
        }
        
        if (prevButton != null)
        {
            prevButton.interactable = (currentIndex > 0);
        }
    }

    /// <summary>
    /// Call this method from your shop or reward script when a player buys a pet!
    /// Example: UnlockPet(1); // Unlocks the Dog
    /// </summary>
    public void UnlockPet(int index)
    {
        PlayerPrefs.SetInt("PetUnlocked_" + index, 1);
        PlayerPrefs.Save();
        
        // Refresh UI if we are currently looking at the pet we just unlocked
        if (index == currentIndex)
        {
            UpdatePetDisplay();
        }
    }

    /// <summary>
    /// Call this from the Play button to get the currently selected pet index.
    /// Useful for deciding which character to spawn in the level.
    /// </summary>
    public int GetSelectedPetIndex()
    {
        return currentIndex;
    }

    /// <summary>
    /// Saves the currently selected pet to PlayerPrefs when Play is clicked.
    /// </summary>
    public void SaveSelectedPet()
    {
        bool isUnlocked = PlayerPrefs.GetInt("PetUnlocked_" + currentIndex, currentIndex == 0 ? 1 : 0) == 1;
        if (isUnlocked)
        {
            PlayerPrefs.SetInt("SelectedPet", currentIndex);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Checks if user has enough coins to unlock the pet. If yes, unlocks it. Else shows buy panel.
    /// </summary>
    public void OnUnlockButtonClicked()
    {
        int requiredCoins = 0;
        if (currentIndex < pets.Count)
        {
            requiredCoins = pets[currentIndex].cost;
        }

        int currentCoins = PlayerPrefs.GetInt("SavedCoins", 0);

        if (currentCoins >= requiredCoins)
        {
            // Deduct coins
            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddCoin(-requiredCoins);
            }
            else
            {
                PlayerPrefs.SetInt("SavedCoins", currentCoins - requiredCoins);
                PlayerPrefs.Save();
            }

            UnlockPet(currentIndex);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayUnlockSound();
            }
        }
        else
        {
            // Not enough coins, check if we can buy with IAP
            bool hasIAP = !string.IsNullOrEmpty(pets[currentIndex].iapProductID);

            if (hasIAP && iapBuyDialog != null)
            {
                iapBuyDialog.SetActive(true);
                if (iapPriceText != null && IAPManager.Instance != null)
                {
                    iapPriceText.text = IAPManager.Instance.GetLocalizedPriceString(pets[currentIndex].iapProductID);
                }
                if (iapBuyButton != null)
                {
                    iapBuyButton.onClick.RemoveAllListeners();
                    int petToBuy = currentIndex;
                    iapBuyButton.onClick.AddListener(() => {
                        if (IAPManager.Instance != null)
                        {
                            IAPManager.Instance.BuyProductID(pets[petToBuy].iapProductID);
                            // Optionally close the dialog
                            iapBuyDialog.SetActive(false);
                        }
                    });
                }
            }
            else
            {
                // Fallback to coins buy panel
                if (buyPanel != null)
                {
                    buyPanel.SetActive(true);
                }

                if (requiredCoinsText != null)
                {
                    int missingCoins = requiredCoins - currentCoins;
                    requiredCoinsText.text = missingCoins.ToString();
                }
            }
        }
    }

    /// <summary>
    /// Call this from a single invisible UI Button to interact with whichever pet is currently visible on the podium.
    /// </summary>
    public void InteractWithCurrentPet()
    {
        if (currentIndex >= 0 && currentIndex < pets.Count)
        {
            GameObject currentModel = pets[currentIndex].model;
            if (currentModel != null)
            {
                PetInteract petInteract = currentModel.GetComponent<PetInteract>();
                if (petInteract != null)
                {
                    petInteract.Interact();
                }
            }
        }
    }

    public void StoreAndDisableNavButtons()
    {
        if (prevButton != null)
        {
            prevButton.interactable = false;
        }
        
        if (nextButton != null)
        {
            nextButton.interactable = false;
        }

        if (playButton != null)
        {
            playButton.gameObject.SetActive(false);
        }

        if (unlockButton != null)
        {
            unlockButton.gameObject.SetActive(false);
        }
    }

    public void RestoreNavButtons()
    {
        if (prevButton != null)
        {
            prevButton.interactable = (currentIndex > 0);
        }
        
        if (nextButton != null)
        {
            nextButton.interactable = (currentIndex < pets.Count - 1);
        }

        bool isUnlocked = PlayerPrefs.GetInt("PetUnlocked_" + currentIndex, currentIndex == 0 ? 1 : 0) == 1;

        if (playButton != null)
        {
            playButton.gameObject.SetActive(isUnlocked);
        }

        if (unlockButton != null)
        {
            unlockButton.gameObject.SetActive(!isUnlocked);
        }
    }
}
