using System;
using System.Collections.Generic;
using UnityEngine;

public enum PetMovementType
{
    Walk,
    Fly
}

[Serializable]
public class PetSpawnData
{
    [Tooltip("The prefab of the pet to spawn")]
    public GameObject petPrefab;
    
    [Tooltip("The empty transform specifying where the pet should be placed")]
    public Transform spawnPoint;
    
    [Tooltip("Movement type (Walk for dog/cat, Fly for birds)")]
    public PetMovementType movementType;
}

public class ProtectedBrickPetSpawner : MonoBehaviour
{
    [Header("Pet Spawn Settings")]
    [Tooltip("List of all available pets, make sure their indices match PetSelectionManager")]
    public List<PetSpawnData> pets = new List<PetSpawnData>();

    private void Start()
    {
        SpawnSelectedPet();
    }

    private void SpawnSelectedPet()
    {
        // 1. Get the currently selected pet index from PlayerPrefs
        // Default is 0 (first pet)
        int selectedPetIndex = PlayerPrefs.GetInt("SelectedPet", 0);

        // 2. Validate index
        if (pets == null || pets.Count == 0)
        {
            Debug.LogWarning("ProtectedBrickPetSpawner: No pets configured in the list.");
            return;
        }

        if (selectedPetIndex < 0 || selectedPetIndex >= pets.Count)
        {
            Debug.LogWarning($"ProtectedBrickPetSpawner: Invalid SelectedPet index {selectedPetIndex}. Defaulting to 0.");
            selectedPetIndex = 0;
        }

        PetSpawnData data = pets[selectedPetIndex];

        // 3. Ensure we have the necessary data
        if (data.petPrefab != null && data.spawnPoint != null)
        {
            // 4. Instantiate and set up the pet
            GameObject spawnedPet = Instantiate(data.petPrefab);
            
            // Make it a child of the protected brick
            spawnedPet.transform.SetParent(this.transform);
            
            // Counteract the parent's scale so the pet doesn't get stretched
            Vector3 pScale = this.transform.lossyScale;
            spawnedPet.transform.localScale = new Vector3(1f / pScale.x, 1f / pScale.y, 1f / pScale.z);
            
            // Set position and rotation to match the empty transform
            spawnedPet.transform.position = data.spawnPoint.position;
            spawnedPet.transform.rotation = data.spawnPoint.rotation;
            
            // TODO: In the future, pass data.movementType to the pet's logic/controller
            // e.g., spawnedPet.GetComponent<PetLogic>().Initialize(data.movementType);
        }
        else
        {
            Debug.LogWarning($"ProtectedBrickPetSpawner: Pet Prefab or Spawn Point is missing for index {selectedPetIndex}");
        }
    }
}
