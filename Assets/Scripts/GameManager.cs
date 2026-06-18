using UnityEngine;

public class GameManager : MonoBehaviour
{
    public int currentLevel = 1;
    private GameObject currentLevelInstance;

    void Start()
    {
        LoadLevel(currentLevel);
    }

    public void LoadLevel(int level)
    {
        if (currentLevelInstance != null)
        {
            Destroy(currentLevelInstance);
        }

        string levelName = "Level_" + level;

        // If there's an existing level generated from the Editor, destroy it to prevent overlaps!
        GameObject existingEditorLevel = GameObject.Find(levelName);
        if (existingEditorLevel != null)
        {
            Destroy(existingEditorLevel);
        }

        GameObject levelPrefab = Resources.Load<GameObject>("Levels/" + levelName);

        if (levelPrefab != null)
        {
            currentLevelInstance = Instantiate(levelPrefab, Vector3.zero, Quaternion.identity);
            Debug.Log("Loaded " + levelName);
        }
        else
        {
            Debug.LogError("Could not find level prefab: " + levelName + " in Resources/Levels");
        }
    }
}
