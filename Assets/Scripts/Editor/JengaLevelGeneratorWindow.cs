using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class JengaLevelGeneratorWindow : EditorWindow
{
    public enum TowerShape { Standard, SolidHouse, HollowHouse, Pyramid, Stairs }

    private TowerShape selectedShape = TowerShape.Standard;
    private GameObject brickPrefab;
    private GameObject protectBrickPrefab;
    
    private int numberOfRows = 15;
    private int numberOfColumns = 3; // base width
    private int missingBricks = 5;
    private int numberOfProtectBricks = 1;
    private bool forceJengaProportions = true;

    [MenuItem("Tools/Jenga Level Generator")]
    public static void ShowWindow()
    {
        GetWindow<JengaLevelGeneratorWindow>("Jenga Level Generator");
    }

    private void OnEnable()
    {
        selectedShape = (TowerShape)EditorPrefs.GetInt("JengaGen_Shape", 0);
        numberOfRows = EditorPrefs.GetInt("JengaGen_Rows", 15);
        numberOfColumns = EditorPrefs.GetInt("JengaGen_Cols", 3);
        missingBricks = EditorPrefs.GetInt("JengaGen_Missing", 5);
        numberOfProtectBricks = EditorPrefs.GetInt("JengaGen_ProtectCount", 1);
        forceJengaProportions = EditorPrefs.GetBool("JengaGen_ForceProportions", true);

        string prefabPath = EditorPrefs.GetString("JengaGen_PrefabPath", "");
        if (!string.IsNullOrEmpty(prefabPath)) brickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        string protectPath = EditorPrefs.GetString("JengaGen_ProtectPrefabPath", "");
        if (!string.IsNullOrEmpty(protectPath)) protectBrickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(protectPath);
    }

    private void OnDisable()
    {
        SaveSettings();
    }

    private void SaveSettings()
    {
        EditorPrefs.SetInt("JengaGen_Shape", (int)selectedShape);
        EditorPrefs.SetInt("JengaGen_Rows", numberOfRows);
        EditorPrefs.SetInt("JengaGen_Cols", numberOfColumns);
        EditorPrefs.SetInt("JengaGen_Missing", missingBricks);
        EditorPrefs.SetInt("JengaGen_ProtectCount", numberOfProtectBricks);
        EditorPrefs.SetBool("JengaGen_ForceProportions", forceJengaProportions);

        EditorPrefs.SetString("JengaGen_PrefabPath", brickPrefab != null ? AssetDatabase.GetAssetPath(brickPrefab) : "");
        EditorPrefs.SetString("JengaGen_ProtectPrefabPath", protectBrickPrefab != null ? AssetDatabase.GetAssetPath(protectBrickPrefab) : "");
    }

    private void OnGUI()
    {
        GUILayout.Label("1. Setup", EditorStyles.boldLabel);
        brickPrefab = (GameObject)EditorGUILayout.ObjectField("Normal Brick Prefab", brickPrefab, typeof(GameObject), true);
        protectBrickPrefab = (GameObject)EditorGUILayout.ObjectField("Protected Brick Prefab", protectBrickPrefab, typeof(GameObject), true);

        EditorGUILayout.Space();
        GUILayout.Label("2. Tower Settings", EditorStyles.boldLabel);
        
        selectedShape = (TowerShape)EditorGUILayout.EnumPopup("Tower Shape", selectedShape);
        
        if (selectedShape == TowerShape.Pyramid || selectedShape == TowerShape.Stairs)
        {
            EditorGUILayout.HelpBox("For Pyramids and Stairs, the height is automatically determined by the Base Width.", MessageType.Info);
        }
        else
        {
            numberOfRows = EditorGUILayout.IntSlider("Number of Rows (Height)", numberOfRows, 3, 40);
        }
        
        numberOfColumns = EditorGUILayout.IntSlider("Base Width (Columns)", numberOfColumns, 2, 15);
        
        if (selectedShape == TowerShape.Standard)
        {
            int maxMissing = Mathf.Max(0, numberOfRows - 2);
            missingBricks = EditorGUILayout.IntSlider("Missing Bricks", missingBricks, 0, maxMissing);
        }

        numberOfProtectBricks = EditorGUILayout.IntSlider("Protected Bricks Count", numberOfProtectBricks, 0, 10);

        EditorGUILayout.Space();
        forceJengaProportions = EditorGUILayout.ToggleLeft("Force Jenga Proportions (Fixes collapsing)", forceJengaProportions);
        EditorGUILayout.HelpBox("Automatically stretches blocks so they perfectly interlock and don't fall over. Required for 3D shapes to work properly!", MessageType.Info);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Tower", GUILayout.Height(40)))
        {
            SaveSettings();
            GenerateTower();
        }
    }

    private void GenerateTower()
    {
        if (brickPrefab == null)
        {
            Debug.LogError("Please assign a Normal Brick Prefab!");
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

        GameObject[] oldTowers = FindObjectsOfType<GameObject>();
        foreach(var obj in oldTowers)
        {
            if (obj.name.StartsWith("Tower_") || obj.name == "Generated_Tower")
                obj.SetActive(false);
        }

        GameObject levelRoot = new GameObject("Tower_" + newLevelNumber);
        
        GameObject tempBrick = Instantiate(brickPrefab);
        if (tempBrick.GetComponent<Collider>() == null) tempBrick.AddComponent<BoxCollider>();
        Vector3 size = tempBrick.GetComponent<Collider>().bounds.size;
        DestroyImmediate(tempBrick);

        bool isSideways = size.x > size.z;
        float bWidth = isSideways ? size.z : size.x;
        float bLength = isSideways ? size.x : size.z;
        float bHeight = size.y;

        float horizontalGap = 0.02f;
        float spacing = bWidth + horizontalGap;
        Quaternion baseRot = isSideways ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;

        List<GameObject> allBricks = new List<GameObject>();
        List<GameObject> upperHalfBricks = new List<GameObject>();

        if (selectedShape == TowerShape.Standard)
            GenerateStandard(levelRoot, bWidth, bLength, bHeight, spacing, baseRot, isSideways, ref allBricks, ref upperHalfBricks);
        else if (selectedShape == TowerShape.SolidHouse)
            GenerateSolidHouse(levelRoot, bWidth, bLength, bHeight, spacing, baseRot, isSideways, ref allBricks, ref upperHalfBricks);
        else if (selectedShape == TowerShape.HollowHouse)
            GenerateHollowHouse(levelRoot, bWidth, bLength, bHeight, spacing, baseRot, isSideways, ref allBricks, ref upperHalfBricks);
        else if (selectedShape == TowerShape.Pyramid)
            GeneratePyramid(levelRoot, bWidth, bLength, bHeight, spacing, baseRot, isSideways, ref allBricks, ref upperHalfBricks);
        else if (selectedShape == TowerShape.Stairs)
            GenerateStairs(levelRoot, bWidth, bLength, bHeight, spacing, baseRot, isSideways, ref allBricks, ref upperHalfBricks);

        // Apply Protected Bricks
        if (upperHalfBricks.Count > 0 && numberOfProtectBricks > 0)
        {
            int toProtect = Mathf.Min(numberOfProtectBricks, upperHalfBricks.Count);
            for (int i = 0; i < toProtect; i++)
            {
                int r = Random.Range(0, upperHalfBricks.Count);
                GameObject target = upperHalfBricks[r];
                upperHalfBricks.RemoveAt(r);
                
                if (protectBrickPrefab != null)
                {
                    Vector3 pos = target.transform.position;
                    Quaternion rot = target.transform.rotation;
                    Vector3 scale = target.transform.localScale;
                    
                    DestroyImmediate(target);
                    
                    GameObject newBrick;
                    if (PrefabUtility.IsPartOfPrefabAsset(protectBrickPrefab)) {
                        newBrick = (GameObject)PrefabUtility.InstantiatePrefab(protectBrickPrefab);
                    } else {
                        newBrick = Instantiate(protectBrickPrefab);
                    }
                    
                    newBrick.transform.position = pos;
                    newBrick.transform.rotation = rot;
                    newBrick.transform.localScale = scale;
                    newBrick.transform.parent = levelRoot.transform;
                    EnsurePhysics(newBrick);
                    if (newBrick.GetComponent<ProtectBrick>() == null) newBrick.AddComponent<ProtectBrick>();
                }
                else
                {
                    target.AddComponent<ProtectBrick>();
                    TintGold(target);
                }
            }
        }

        Debug.Log("Tower generated in scene! You can inspect it and save it as a prefab manually.");
    }

    private void EnsurePhysics(GameObject brick)
    {
        if (brick.GetComponent<Rigidbody>() == null) brick.AddComponent<Rigidbody>();
        if (brick.GetComponent<Collider>() == null) brick.AddComponent<BoxCollider>();
        if (brick.GetComponent<BrickCollisionSound>() == null) brick.AddComponent<BrickCollisionSound>();
    }

    private void TintGold(GameObject brick)
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
        Renderer[] rends = brick.GetComponentsInChildren<Renderer>();
        foreach (var r in rends) r.sharedMaterial = goldMat;
    }

    private void ScaleBrick(GameObject brick, bool isSideways, float bLength, float bWidth, int cols)
    {
        if (!forceJengaProportions) return;
        float requiredLength = bWidth * cols;
        Vector3 scale = brick.transform.localScale;
        if (isSideways) scale.x = scale.x * (requiredLength / bLength);
        else scale.z = scale.z * (requiredLength / bLength);
        brick.transform.localScale = scale;
    }

    private GameObject SpawnBrick(GameObject root, Vector3 pos, Quaternion rot)
    {
        GameObject brick;
        if (PrefabUtility.IsPartOfPrefabAsset(brickPrefab)) {
            brick = (GameObject)PrefabUtility.InstantiatePrefab(brickPrefab);
        } else {
            brick = Instantiate(brickPrefab);
        }
        brick.transform.position = pos;
        brick.transform.rotation = rot;
        brick.transform.parent = root.transform;
        EnsurePhysics(brick);
        return brick;
    }

    private void GenerateStandard(GameObject root, float bW, float bL, float bH, float space, Quaternion baseRot, bool sideways, ref List<GameObject> all, ref List<GameObject> upper)
    {
        List<int> avail = new List<int>();
        for (int y = 1; y < numberOfRows - 1; y++) avail.Add(y * numberOfColumns + (numberOfColumns / 2));
        List<int> missing = new List<int>();
        for(int i = 0; i < missingBricks && avail.Count > 0; i++)
        {
            int r = Random.Range(0, avail.Count);
            missing.Add(avail[r]);
            avail.RemoveAt(r);
        }

        int count = 0;
        for (int y = 0; y < numberOfRows; y++)
        {
            bool rotated = y % 2 == 1;
            for (int i = 0; i < numberOfColumns; i++)
            {
                if (!missing.Contains(count))
                {
                    float offset = (i - (numberOfColumns - 1) / 2f) * space;
                    Vector3 pos = rotated ? new Vector3(0, y * bH + (bH / 2f), offset) : new Vector3(offset, y * bH + (bH / 2f), 0);
                    Quaternion rot = rotated ? baseRot * Quaternion.Euler(0, 90, 0) : baseRot;
                    
                    GameObject b = SpawnBrick(root, pos, rot);
                    ScaleBrick(b, sideways, bL, bW, numberOfColumns);
                    all.Add(b);
                    if (y >= numberOfRows / 2) upper.Add(b);
                }
                count++;
            }
        }
    }

    private void GenerateSolidHouse(GameObject root, float bW, float bL, float bH, float space, Quaternion baseRot, bool sideways, ref List<GameObject> all, ref List<GameObject> upper)
    {
        int baseRows = Mathf.Max(2, numberOfRows - (numberOfColumns / 2));
        for (int y = 0; y < numberOfRows; y++)
        {
            bool rotated = y % 2 == 1;
            int currentCols = numberOfColumns;
            if (y >= baseRows) currentCols = Mathf.Max(1, numberOfColumns - ((y - baseRows + 1) * 2));

            for (int i = 0; i < currentCols; i++)
            {
                float offset = (i - (currentCols - 1) / 2f) * space;
                Vector3 pos = rotated ? new Vector3(0, y * bH + (bH / 2f), offset) : new Vector3(offset, y * bH + (bH / 2f), 0);
                Quaternion rot = rotated ? baseRot * Quaternion.Euler(0, 90, 0) : baseRot;
                
                GameObject b = SpawnBrick(root, pos, rot);
                ScaleBrick(b, sideways, bL, bW, currentCols); // House roof scales inward to form a true pyramid roof
                all.Add(b);
                if (y >= numberOfRows / 2) upper.Add(b);
            }
        }
    }

    private void GenerateHollowHouse(GameObject root, float bW, float bL, float bH, float space, Quaternion baseRot, bool sideways, ref List<GameObject> all, ref List<GameObject> upper)
    {
        int baseRows = Mathf.Max(2, numberOfRows - (numberOfColumns / 2));
        for (int y = 0; y < numberOfRows; y++)
        {
            bool rotated = y % 2 == 1;
            int currentCols = numberOfColumns;
            bool isRoof = y >= baseRows;
            if (isRoof) currentCols = Mathf.Max(1, numberOfColumns - ((y - baseRows + 1) * 2));

            for (int i = 0; i < currentCols; i++)
            {
                bool isOuter = (i == 0 || i == currentCols - 1);
                bool skip = (!isRoof && !isOuter && currentCols >= 3);

                if (!skip)
                {
                    float offset = (i - (currentCols - 1) / 2f) * space;
                    Vector3 pos = rotated ? new Vector3(0, y * bH + (bH / 2f), offset) : new Vector3(offset, y * bH + (bH / 2f), 0);
                    Quaternion rot = rotated ? baseRot * Quaternion.Euler(0, 90, 0) : baseRot;
                    
                    GameObject b = SpawnBrick(root, pos, rot);
                    ScaleBrick(b, sideways, bL, bW, currentCols);
                    all.Add(b);
                    if (y >= numberOfRows / 2) upper.Add(b);
                }
            }
        }
    }

    private void GeneratePyramid(GameObject root, float bW, float bL, float bH, float space, Quaternion baseRot, bool sideways, ref List<GameObject> all, ref List<GameObject> upper)
    {
        int y = 0;
        while (true)
        {
            int tier = y / 2; // Step inward every 2 rows so they interlock securely
            int currentCols = numberOfColumns - (tier * 2);
            if (currentCols < 1) break;

            bool rotated = y % 2 == 1;
            for (int i = 0; i < currentCols; i++)
            {
                float offset = (i - (currentCols - 1) / 2f) * space;
                Vector3 pos = rotated ? new Vector3(0, y * bH + (bH / 2f), offset) : new Vector3(offset, y * bH + (bH / 2f), 0);
                Quaternion rot = rotated ? baseRot * Quaternion.Euler(0, 90, 0) : baseRot;
                
                GameObject b = SpawnBrick(root, pos, rot);
                ScaleBrick(b, sideways, bL, bW, currentCols);
                all.Add(b);
                if (tier >= (numberOfColumns/4)) upper.Add(b); // Roughly upper half
            }
            y++;
        }
    }

    private void GenerateStairs(GameObject root, float bW, float bL, float bH, float space, Quaternion baseRot, bool sideways, ref List<GameObject> all, ref List<GameObject> upper)
    {
        int y = 0;
        float leftmostX = -(numberOfColumns - 1) / 2f * space;
        float maxLength = bW * numberOfColumns;

        while (true)
        {
            int tier = y / 2;
            int currentCols = numberOfColumns - tier;
            if (currentCols < 1) break;

            bool rotated = y % 2 == 1;
            float currentLength = bW * currentCols;
            float lengthOffset = (maxLength / 2f) - (currentLength / 2f);

            for (int i = 0; i < currentCols; i++)
            {
                float offset = leftmostX + (i * space);
                
                Vector3 pos;
                if (rotated) pos = new Vector3(-lengthOffset, y * bH + (bH / 2f), offset); // Align back edge
                else pos = new Vector3(offset, y * bH + (bH / 2f), -lengthOffset);

                Quaternion rot = rotated ? baseRot * Quaternion.Euler(0, 90, 0) : baseRot;
                
                GameObject b = SpawnBrick(root, pos, rot);
                ScaleBrick(b, sideways, bL, bW, currentCols);
                all.Add(b);
                if (tier >= (numberOfColumns/4)) upper.Add(b);
            }
            y++;
        }
    }
}
