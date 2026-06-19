using UnityEngine;
using UnityEditor;

public class JengaLevelGeneratorWindow : EditorWindow
{
    private GameObject brickPrefab;
    private int numberOfRows = 15;
    private int numberOfColumns = 3;
    private int missingBricks = 5;
    private bool forceJengaProportions = true;
    private bool highlightProtectBrick = true;

    [MenuItem("Tools/Jenga Level Generator")]
    public static void ShowWindow()
    {
        GetWindow<JengaLevelGeneratorWindow>("Jenga Level Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("1. Setup", EditorStyles.boldLabel);
        brickPrefab = (GameObject)EditorGUILayout.ObjectField("Brick Prefab", brickPrefab, typeof(GameObject), true);

        EditorGUILayout.Space();
        GUILayout.Label("2. Generate Level", EditorStyles.boldLabel);
        
        numberOfRows = EditorGUILayout.IntSlider("Number of Rows", numberOfRows, 3, 30);
        numberOfColumns = EditorGUILayout.IntSlider("Number of Columns", numberOfColumns, 2, 10);
        
        int maxMissing = (numberOfRows - 2); // 1 per middle row
        missingBricks = EditorGUILayout.IntSlider("Missing Bricks", missingBricks, 0, maxMissing);

        EditorGUILayout.Space();
        forceJengaProportions = EditorGUILayout.ToggleLeft("Force Jenga Proportions (Fixes collapsing)", forceJengaProportions);
        EditorGUILayout.HelpBox("If your custom brick is not exactly 3x as long as it is wide, the tower will collapse. Keep this checked to automatically stretch it to perfect Jenga proportions!", MessageType.Info);

        EditorGUILayout.Space();
        highlightProtectBrick = EditorGUILayout.ToggleLeft("Highlight Protect Brick (Gold)", highlightProtectBrick);
        EditorGUILayout.HelpBox("Picks a random brick in the upper half of the tower, tints it Gold, and sets it as the Protect Brick for this level.", MessageType.Info);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Tower", GUILayout.Height(40)))
        {
            GenerateTower();
        }
    }

    private void GenerateTower()
    {
        if (brickPrefab == null)
        {
            Debug.LogError("Please assign a Brick Prefab!");
            return;
        }

        int newLevelNumber = 1;
        LevelManager lm = FindObjectOfType<LevelManager>();
        if (lm != null)
        {
            newLevelNumber = lm.levels.Count + 1;
            LevelData newData = new LevelData();
            newData.levelNumber = newLevelNumber;
            lm.levels.Add(newData);
            UnityEditor.EditorUtility.SetDirty(lm);
        }

        // Clean up previous tower if exists (for rapid prototyping)
        // Now we just look for anything starting with "Tower_" or "Generated_Tower"
        // Wait, we shouldn't destroy previous towers if we are generating multiple levels in the scene!
        // But if they are just prototyping one tower at a time, we probably should.
        // Let's assume they want them all in the scene but we disable previous ones or just let them overlap?
        // "instead of Generated_Tower, Name should be Tower_LevelNumber".
        // If they want to keep previous levels, we shouldn't destroy them. We will just disable the old ones.
        GameObject[] oldTowers = FindObjectsOfType<GameObject>();
        foreach(var obj in oldTowers)
        {
            if (obj.name.StartsWith("Tower_") || obj.name == "Generated_Tower")
            {
                obj.SetActive(false);
            }
        }

        GameObject levelRoot = new GameObject("Tower_" + newLevelNumber);
        
        int totalBricks = numberOfRows * numberOfColumns;
        
        System.Collections.Generic.List<int> availableIndices = new System.Collections.Generic.List<int>();
        // Only allow missing bricks in middle rows (exclude bottom row and top row for stability)
        // And ONLY pick the center-ish brick to be missing, so the layer above is supported by the edges!
        for (int y = 1; y < numberOfRows - 1; y++) {
            availableIndices.Add(y * numberOfColumns + (numberOfColumns / 2));
        }

        System.Collections.Generic.List<int> missingIndices = new System.Collections.Generic.List<int>();
        for(int i = 0; i < missingBricks && availableIndices.Count > 0; i++)
        {
            int r = Random.Range(0, availableIndices.Count);
            missingIndices.Add(availableIndices[r]);
            availableIndices.RemoveAt(r);
        }

        GameObject tempBrick = Instantiate(brickPrefab);
        if (tempBrick.GetComponent<Collider>() == null) tempBrick.AddComponent<BoxCollider>();
        Vector3 size = tempBrick.GetComponent<Collider>().bounds.size;
        DestroyImmediate(tempBrick);

        bool isSideways = size.x > size.z;
        float bWidth = isSideways ? size.z : size.x;
        float bLength = isSideways ? size.x : size.z;
        float bHeight = size.y;

        float horizontalGap = 0.02f; // Tiny gap so physics don't freak out
        float spacing = bWidth + horizontalGap;
        Quaternion baseRot = isSideways ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;

        System.Collections.Generic.List<GameObject> upperHalfBricks = new System.Collections.Generic.List<GameObject>();

        int brickCount = 0;
        for (int y = 0; y < numberOfRows; y++)
        {
            bool isRotated = y % 2 == 1;
            for (int i = 0; i < numberOfColumns; i++)
            {
                float offset = (i - (numberOfColumns - 1) / 2f) * spacing;
                bool isMissing = missingIndices.Contains(brickCount);

                if (!isMissing)
                {
                    Vector3 position;
                    Quaternion rotation;

                    if (isRotated)
                    {
                        position = new Vector3(0, y * bHeight + (bHeight / 2f), offset);
                        rotation = baseRot * Quaternion.Euler(0, 90, 0);
                    }
                    else
                    {
                        position = new Vector3(offset, y * bHeight + (bHeight / 2f), 0);
                        rotation = baseRot;
                    }

                    GameObject brick;
                    if (PrefabUtility.IsPartOfPrefabAsset(brickPrefab)) {
                        brick = (GameObject)PrefabUtility.InstantiatePrefab(brickPrefab);
                    } else {
                        brick = Instantiate(brickPrefab);
                    }
                    
                    brick.transform.position = position;
                    brick.transform.rotation = rotation;
                    brick.transform.parent = levelRoot.transform;

                    if (forceJengaProportions)
                    {
                        // The required length for the row to be a perfect square is (Width * Columns)
                        float requiredLength = bWidth * numberOfColumns;
                        // To stretch the brick's length to match the required length:
                        Vector3 scale = brick.transform.localScale;
                        if (isSideways) {
                            // X is length
                            scale.x = scale.x * (requiredLength / bLength);
                        } else {
                            // Z is length
                            scale.z = scale.z * (requiredLength / bLength);
                        }
                        brick.transform.localScale = scale;
                    }
                    
                    // Ensure it has physics
                    if (brick.GetComponent<Rigidbody>() == null) {
                        brick.AddComponent<Rigidbody>();
                    }
                    if (brick.GetComponent<Collider>() == null) {
                        brick.AddComponent<BoxCollider>();
                    }
                    if (brick.GetComponent<BrickCollisionSound>() == null) {
                        brick.AddComponent<BrickCollisionSound>();
                    }

                    if (y >= numberOfRows / 2) {
                        upperHalfBricks.Add(brick);
                    }
                }
                brickCount++;
            }
        }

        // Apply Protect Brick Logic
        if (upperHalfBricks.Count > 0)
        {
            int randomIndex = Random.Range(0, upperHalfBricks.Count);
            GameObject protectBrick = upperHalfBricks[randomIndex];
            protectBrick.AddComponent<ProtectBrick>();

            if (highlightProtectBrick)
            {
                Material goldMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GoldProtectBrick.mat");
                if (goldMat == null)
                {
                    if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
                    goldMat = new Material(Shader.Find("Standard"));
                    goldMat.color = new Color(1f, 0.84f, 0f);
                    goldMat.SetFloat("_Metallic", 0.6f);
                    goldMat.SetFloat("_Glossiness", 0.8f);
                    AssetDatabase.CreateAsset(goldMat, "Assets/Materials/GoldProtectBrick.mat");
                }
                
                Renderer[] rends = protectBrick.GetComponentsInChildren<Renderer>();
                foreach (var r in rends)
                {
                    r.sharedMaterial = goldMat;
                }
            }

            if (lm != null && lm.levels.Count > 0)
            {
                lm.levels[lm.levels.Count - 1].protectBrick = protectBrick;
                UnityEditor.EditorUtility.SetDirty(lm);
            }
        }

        Debug.Log("Tower generated in scene! You can inspect it and save it as a prefab manually.");
    }
}
