using UnityEngine;
using UnityEditor;

public class JengaLevelGeneratorWindow : EditorWindow
{
    private int numberOfRows = 15;
    private int numberOfColumns = 3;
    private int missingBricks = 5;
    private GameObject sourceBrick;

    [MenuItem("Tools/Jenga Level Generator")]
    public static void ShowWindow()
    {
        GetWindow<JengaLevelGeneratorWindow>("Jenga Level Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("1. Setup", EditorStyles.boldLabel);
        sourceBrick = (GameObject)EditorGUILayout.ObjectField("Brick Object/Prefab", sourceBrick, typeof(GameObject), true);

        EditorGUILayout.Space();
        GUILayout.Label("2. Generate Level", EditorStyles.boldLabel);
        
        numberOfRows = EditorGUILayout.IntSlider("Number of Rows", numberOfRows, 3, 30);
        numberOfColumns = EditorGUILayout.IntSlider("Number of Columns", numberOfColumns, 2, 10);
        
        int maxMissing = (numberOfRows - 2); // 1 per middle row
        missingBricks = EditorGUILayout.IntSlider("Missing Bricks", missingBricks, 0, maxMissing);

        if (GUILayout.Button("Generate Tower in Scene", GUILayout.Height(40)))
        {
            if (sourceBrick == null)
            {
                EditorUtility.DisplayDialog("Error", "Please drag a Brick GameObject or Prefab into the field!", "OK");
                return;
            }
            GenerateLevel();
        }
    }

    private void GenerateLevel()
    {
        // Clean up previous generated tower so they don't overlap and confuse the user!
        GameObject existingTower = GameObject.Find("Generated_Tower");
        if (existingTower != null)
        {
            DestroyImmediate(existingTower);
        }

        GameObject levelRoot = new GameObject("Generated_Tower");
        
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

        int brickCount = 0;
        for (int y = 0; y < numberOfRows; y++)
        {
            bool isRotated = y % 2 == 1;
            for (int i = 0; i < numberOfColumns; i++)
            {
                float xPos = i - (numberOfColumns - 1) / 2f;
                bool isMissing = missingIndices.Contains(brickCount);

                if (!isMissing)
                {
                    Vector3 position;
                    Quaternion rotation;

                    // Exact 1.0 vertical spacing.
                    if (isRotated)
                    {
                        position = new Vector3(0, y * 1.0f + 0.5f, xPos * 1.0f);
                        rotation = Quaternion.Euler(0, 90, 0);
                    }
                    else
                    {
                        position = new Vector3(xPos * 1.0f, y * 1.0f + 0.5f, 0);
                        rotation = Quaternion.identity;
                    }

                    GameObject brick;
                    if (PrefabUtility.IsPartOfPrefabAsset(sourceBrick)) {
                        brick = (GameObject)PrefabUtility.InstantiatePrefab(sourceBrick);
                    } else {
                        brick = Instantiate(sourceBrick);
                    }
                    
                    brick.transform.position = position;
                    brick.transform.rotation = rotation;
                    brick.transform.parent = levelRoot.transform;

                    // Force the scale so the math works perfectly without falling!
                    // Length must perfectly match the number of columns to create a square layer.
                    brick.transform.localScale = new Vector3(0.98f, 1.0f, numberOfColumns - 0.02f);
                    
                    // Ensure it has physics
                    if (brick.GetComponent<Rigidbody>() == null) {
                        brick.AddComponent<Rigidbody>();
                    }
                    if (brick.GetComponent<Collider>() == null) {
                        brick.AddComponent<BoxCollider>();
                    }
                }
                brickCount++;
            }
        }

        Debug.Log("Tower generated in scene! You can inspect it and save it as a prefab manually.");
    }
}
