using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

public static class EnvironmentSetupMenu
{
    private static readonly string[] LegacyRootNames = { "Environment", "Floor", "Lights" };

    /// <summary>
    /// Adds an EnvironmentManager to the open scene (or reuses the existing one), registers every
    /// EnvironmentProfile found under Assets/Environments and hides the old floor/props visuals.
    /// </summary>
    [MenuItem("Tools/Last Brick/Environments/Setup Environment Manager In Scene")]
    public static void Setup()
    {
        EnvironmentManager manager = Object.FindFirstObjectByType<EnvironmentManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            manager = new GameObject("EnvironmentManager").AddComponent<EnvironmentManager>();
            Undo.RegisterCreatedObjectUndo(manager.gameObject, "Create EnvironmentManager");
        }
        Undo.RecordObject(manager, "Setup EnvironmentManager");

        string[] guids = AssetDatabase.FindAssets("t:EnvironmentProfile", new[] { EnvironmentBuildKit.RootFolder });
        var profiles = guids
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .OrderBy(p => p)
            .Select(p => AssetDatabase.LoadAssetAtPath<EnvironmentProfile>(p));
        foreach (EnvironmentProfile p in profiles)
        {
            if (p != null && !manager.environments.Contains(p)) manager.environments.Add(p);
        }

        foreach (GameObject root in manager.gameObject.scene.GetRootGameObjects())
        {
            if (LegacyRootNames.Contains(root.name) && !manager.legacyVisualRoots.Contains(root))
            {
                manager.legacyVisualRoots.Add(root);
            }
        }

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeObject = manager;

        Debug.Log($"[Environments] EnvironmentManager ready with {manager.environments.Count} environment(s) and " +
                  $"{manager.legacyVisualRoots.Count} legacy root(s). Save the scene and press Play.");
    }

    /// <summary>Drops the selected environment's prefab into the scene so you can look at it without playing.</summary>
    [MenuItem("Tools/Last Brick/Environments/Preview Selected Profile In Scene")]
    public static void PreviewSelected()
    {
        EnvironmentProfile profile = Selection.activeObject as EnvironmentProfile;
        if (profile == null || profile.environmentPrefab == null)
        {
            EditorUtility.DisplayDialog("Preview Environment", "Select an Environment Profile asset first.", "OK");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(profile.environmentPrefab);
        Undo.RegisterCreatedObjectUndo(instance, "Preview Environment");
        Selection.activeGameObject = instance;
        Debug.Log("[Environments] Preview instance added. Delete it before saving the scene - the EnvironmentManager spawns it at runtime. Press Play to see sky, fog and post-processing.");
    }
}
