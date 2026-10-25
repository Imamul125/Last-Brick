using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;

public static class EnvironmentSetupMenu
{
    private static readonly string[] LegacyRootNames = { "Environment", "Floor", "Lights" };
    private const string GeneratedFolder = EnvironmentBuildKit.RootFolder + "/Generated";

    /// <summary>
    /// One click: builds the shared library, creates/updates all 100 environment profiles from EnvironmentCatalog
    /// and registers them on the EnvironmentManager in the open scene.
    /// </summary>
    [MenuItem("Tools/Last Brick/Environments/Build All 100 Environments", priority = 0)]
    public static void BuildAll()
    {
        try
        {
            EnvironmentLibrary lib = EnvironmentLibraryBuilder.Build();

            EnvironmentBuildKit.EnsureFolder(GeneratedFolder);
            List<EnvironmentCatalog.Entry> entries = EnvironmentCatalog.BuildAll();
            var profiles = new List<EnvironmentProfile>();
            var keep = new HashSet<string>();

            for (int i = 0; i < entries.Count; i++)
            {
                EnvironmentCatalog.Entry e = entries[i];
                EditorUtility.DisplayProgressBar("Building environments", e.displayName, i / (float)entries.Count);

                string path = $"{GeneratedFolder}/{e.id}.asset";
                keep.Add(path);
                EnvironmentProfile profile = AssetDatabase.LoadAssetAtPath<EnvironmentProfile>(path);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<EnvironmentProfile>();
                    AssetDatabase.CreateAsset(profile, path);
                }
                profile.displayName = e.displayName;
                profile.useRecipe = true;
                profile.recipe = e.recipe;
                profile.environmentPrefab = null;
                profile.skybox = null;
                profile.postProcessProfile = null;
                EditorUtility.SetDirty(profile);
                profiles.Add(profile);
            }

            // Remove profiles left over from an older catalog.
            foreach (string guid in AssetDatabase.FindAssets("t:EnvironmentProfile", new[] { GeneratedFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!keep.Contains(path)) AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.SaveAssets();

            EnvironmentManager manager = Register(lib, profiles, replaceList: true);
            Debug.Log($"[Environments] Built {profiles.Count} environments in {GeneratedFolder} and registered them on '{manager.name}'. " +
                      "Save the scene and press Play. In Play Mode use [ and ] to step through them.");
            EditorUtility.DisplayDialog("Environments", $"Built {profiles.Count} environments and registered them in the scene.\n\n" +
                "Save the scene, press Play, then use the [ and ] keys to step through every environment.", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// Adds an EnvironmentManager to the open scene (or reuses the existing one), registers every
    /// EnvironmentProfile found under Assets/Environments and hides the old floor/props visuals.
    /// </summary>
    [MenuItem("Tools/Last Brick/Environments/Setup Environment Manager In Scene", priority = 20)]
    public static void Setup()
    {
        string[] guids = AssetDatabase.FindAssets("t:EnvironmentProfile", new[] { EnvironmentBuildKit.RootFolder });
        var profiles = guids
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .OrderBy(p => p)
            .Select(p => AssetDatabase.LoadAssetAtPath<EnvironmentProfile>(p))
            .Where(p => p != null)
            .ToList();

        EnvironmentManager manager = Register(FindLibrary(), profiles, replaceList: false);
        Debug.Log($"[Environments] EnvironmentManager ready with {manager.environments.Count} environment(s) and " +
                  $"{manager.legacyVisualRoots.Count} legacy root(s). Save the scene and press Play.");
    }

    private static EnvironmentManager Register(EnvironmentLibrary lib, List<EnvironmentProfile> profiles, bool replaceList)
    {
        EnvironmentManager manager = Object.FindFirstObjectByType<EnvironmentManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            manager = new GameObject("EnvironmentManager").AddComponent<EnvironmentManager>();
            Undo.RegisterCreatedObjectUndo(manager.gameObject, "Create EnvironmentManager");
        }
        Undo.RecordObject(manager, "Setup EnvironmentManager");

        if (lib != null) manager.library = lib;
        if (replaceList) manager.environments.Clear();
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
        return manager;
    }

    private static EnvironmentLibrary FindLibrary()
    {
        string guid = AssetDatabase.FindAssets("t:EnvironmentLibrary").FirstOrDefault();
        return guid == null ? null : AssetDatabase.LoadAssetAtPath<EnvironmentLibrary>(AssetDatabase.GUIDToAssetPath(guid));
    }

    /// <summary>Drops the selected environment into the scene so you can look at it without playing.</summary>
    [MenuItem("Tools/Last Brick/Environments/Preview Selected Profile In Scene", priority = 40)]
    public static void PreviewSelected()
    {
        EnvironmentProfile profile = Selection.activeObject as EnvironmentProfile;
        if (profile == null || (!profile.useRecipe && profile.environmentPrefab == null))
        {
            EditorUtility.DisplayDialog("Preview Environment", "Select an Environment Profile asset first.", "OK");
            return;
        }

        foreach (GeneratedEnvironment old in Object.FindObjectsByType<GeneratedEnvironment>(FindObjectsSortMode.None))
            Object.DestroyImmediate(old.gameObject);

        GameObject instance;
        if (profile.useRecipe)
        {
            EnvironmentLibrary lib = FindLibrary();
            if (lib == null)
            {
                EditorUtility.DisplayDialog("Preview Environment", "Run 'Build All 100 Environments' first to create the library.", "OK");
                return;
            }
            instance = EnvironmentGenerator.Build(profile, lib, EnvironmentBuildKit.FindGroundHeight()).gameObject;
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave; // never saved into the scene
        }
        else
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(profile.environmentPrefab);
            Undo.RegisterCreatedObjectUndo(instance, "Preview Environment");
        }

        Selection.activeGameObject = instance;
        Debug.Log("[Environments] Preview added (sky, fog and post-processing only show in Play Mode). " +
                  "Prefab previews must be deleted before saving; recipe previews are never saved.");
    }
}
