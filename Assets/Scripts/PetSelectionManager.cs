using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PetSelectionManager : MonoBehaviour
{
    [Header("Pet Models (Place in order)")]
    [Tooltip("Drag the 3D models from the PetShowcasePodium here. Index 0 is the default Cat.")]
    public List<GameObject> petModels = new List<GameObject>();

    [Header("UI References")]
    public Button nextButton;
    public Button prevButton;
    public GameObject lockOverlay;
    public Button playButton; // Reference to main play button so we can disable it if pet is locked

    private int currentIndex = 0;

    private void Start()
    {
        // Add listeners to buttons
        if (nextButton != null) nextButton.onClick.AddListener(NextPet);
        if (prevButton != null) prevButton.onClick.AddListener(PrevPet);

        // Ensure only the first pet is visible initially
        UpdatePetDisplay();
    }

    public void NextPet()
    {
        if (petModels.Count == 0 || currentIndex >= petModels.Count - 1) return;

        currentIndex++;
        UpdatePetDisplay();
    }

    public void PrevPet()
    {
        if (petModels.Count == 0 || currentIndex <= 0) return;

        currentIndex--;
        UpdatePetDisplay();
    }

    private void UpdatePetDisplay()
    {
        // 1. Toggle visibility of 3D models on the podium
        for (int i = 0; i < petModels.Count; i++)
        {
            if (petModels[i] != null)
            {
                petModels[i].SetActive(i == currentIndex);
            }
        }

        // 2. Check unlock status
        bool isUnlocked = PlayerPrefs.GetInt("PetUnlocked_" + currentIndex, currentIndex == 0 ? 1 : 0) == 1;

        // 3. Update UI overlays
        if (lockOverlay != null)
        {
            lockOverlay.SetActive(!isUnlocked);
        }

        if (playButton != null)
        {
            playButton.interactable = isUnlocked;
        }

        // 4. Update Button Interactable States
        if (nextButton != null)
        {
            nextButton.interactable = (currentIndex < petModels.Count - 1);
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
}
