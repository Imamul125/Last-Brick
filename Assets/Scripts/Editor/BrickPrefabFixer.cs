using UnityEngine;
using UnityEditor;

public class BrickPrefabFixer : EditorWindow
{
    private GameObject rawFbx;

    [MenuItem("Tools/Fix Custom Brick Prefab")]
    public static void ShowWindow()
    {
        GetWindow<BrickPrefabFixer>("Brick Fixer");
    }

    private void OnGUI()
    {
        GUILayout.Label("AI Model Fixer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Drag your raw AI-generated FBX or mesh into the slot below, and click Fix. It will automatically center, rescale, and wrap it into a perfect Jenga physics prefab!", MessageType.Info);
        
        EditorGUILayout.Space();
        rawFbx = (GameObject)EditorGUILayout.ObjectField("Raw FBX / Model", rawFbx, typeof(GameObject), false);

        EditorGUILayout.Space();
        if (GUILayout.Button("Fix & Generate Prefab", GUILayout.Height(40)))
        {
            FixBrick();
        }
    }

    private void FixBrick()
    {
        if (rawFbx == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a raw FBX or Prefab into the slot first!", "OK");
            return;
        }

        // Create the clean root wrapper
        string prefabName = "Perfect_" + rawFbx.name;
        GameObject root = new GameObject(prefabName);
        root.tag = "Untagged"; // Interactor uses Rigidbody now, so no tag needed

        // Standard Jenga block dimensions
        Vector3 targetSize = new Vector3(0.92f, 1.0f, 2.92f);

        // Add correct physics to the root
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.size = targetSize;
        root.AddComponent<Rigidbody>();
        
        // Ensure folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Scripts")) AssetDatabase.CreateFolder("Assets", "Scripts");
        
        // Attach sound
        if (System.Type.GetType("BrickCollisionSound, Assembly-CSharp") != null)
        {
            root.AddComponent(System.Type.GetType("BrickCollisionSound, Assembly-CSharp"));
        }

        // Instantiate the messy FBX as a child
        GameObject child = (GameObject)PrefabUtility.InstantiatePrefab(rawFbx);
        child.transform.SetParent(root.transform);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        // Calculate the raw bounds of the FBX
        Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            // 1. Center the pivot
            Vector3 centerOffset = b.center - child.transform.position;
            child.transform.localPosition = -centerOffset;

            // Recalculate bounds after shifting
            b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            // 2. Rotate if the longest axis is X instead of Z
            bool rotated = false;
            if (b.size.x > b.size.z)
            {
                child.transform.RotateAround(root.transform.position, Vector3.up, 90f);
                b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                rotated = true;
            }

            // 3. Scale to perfectly fit the target size (0.92, 1.0, 2.92)
            Vector3 scaleMult = new Vector3(
                targetSize.x / b.size.x,
                targetSize.y / b.size.y,
                targetSize.z / b.size.z
            );

            Vector3 currentScale = child.transform.localScale;
            if (rotated)
            {
                child.transform.localScale = new Vector3(
                    currentScale.x * scaleMult.z, // Local X is now World Z
                    currentScale.y * scaleMult.y,
                    currentScale.z * scaleMult.x  // Local Z is now World X
                );
            }
            else
            {
                child.transform.localScale = new Vector3(
                    currentScale.x * scaleMult.x,
                    currentScale.y * scaleMult.y,
                    currentScale.z * scaleMult.z
                );
            }
        }

        // Save as a clean prefab
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        string prefabPath = "Assets/Prefabs/" + prefabName + ".prefab";
        prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        DestroyImmediate(root);

        Debug.Log("Successfully created perfect Jenga prefab at " + prefabPath);
        EditorUtility.DisplayDialog("Success", "Fixed prefab saved at:\n" + prefabPath + "\n\nYou can now use this in the Level Generator!", "Awesome!");
    }
}
