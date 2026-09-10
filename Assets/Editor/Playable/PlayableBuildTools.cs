// Editor-only tooling for producing the Unity Playworks (Luna) playable-ad scene.
//
// Everything here is scripted rather than hand-authored so the playable can be regenerated from
// Assets/Scenes/LastBrick.unity after the game scene changes, instead of drifting out of sync.
//
// Headless usage:
//   Unity.exe -batchmode -quit -projectPath . -executeMethod PlayableBuildTools.BuildPlayableScene
//   Unity.exe -batchmode -quit -projectPath . -executeMethod PlayableBuildTools.BuildWebGL
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PlayableBuildTools
{
    public const string SourceScene   = "Assets/Scenes/LastBrick.unity";
    public const string PlayableScene = "Assets/Scenes/WebGL/PlayableAds.unity";
    public const string WebGLOutput   = "Builds/WebGL_Playable";

    /// <summary>Root objects kept in the playable. Everything else in the scene is deleted.</summary>
    private static readonly string[] KeepRoots =
    {
        "Main Camera",
        "Directional Light",
        "Global Volume",
        "Floor",
        "EventSystem",
        "Environment",
        "Lights",
    };

    /// <summary>Project scripts allowed to survive. Every other Assembly-CSharp behaviour is stripped.</summary>
    private static readonly HashSet<string> KeepScripts = new HashSet<string>
    {
        "PlayableAdController",
        "PlayableBrickInteractor",
        "PlayableEndCard",
        "PlayableTutorialHand",
        "PlayableCameraRig",
    };

    /// <summary>
    /// Level to build the playable around, loaded from Resources exactly as LevelManager does.
    /// The Tower_N objects sitting in the shipped scene are stale editor placeholders — the game
    /// deletes them at runtime and instantiates this prefab instead, and the placeholders are not
    /// stable under physics. Tower_5 is 32 bricks: big enough to survive three pulls.
    /// </summary>
    private const string LevelResourcePath = "Levels/Tower_5";

    private static string ReportDir
    {
        get
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "PlayableReports");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    // ---------------------------------------------------------------- hierarchy dump

    /// <summary>Writes the hierarchy of a scene to PlayableReports/hierarchy.txt.</summary>
    public static void DumpHierarchy()
    {
        string scenePath = ArgOr("-scene", SourceScene);
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var sb = new StringBuilder();
        sb.AppendLine("SCENE: " + scenePath);
        var roots = scene.GetRootGameObjects();
        sb.AppendLine("ROOT COUNT: " + roots.Length);
        sb.AppendLine();

        foreach (var root in roots)
        {
            Describe(root.transform, sb, 0, maxDepth: 2);
            sb.AppendLine();
        }

        string outPath = Path.Combine(ReportDir, Path.GetFileNameWithoutExtension(scenePath) + "-hierarchy.txt");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[Playable] Hierarchy written to " + outPath);
    }

    private static void Describe(Transform t, StringBuilder sb, int depth, int maxDepth)
    {
        string indent = new string(' ', depth * 2);
        var comps = t.GetComponents<Component>()
                     .Where(c => c != null)
                     .Select(c => c.GetType().Name)
                     .Where(n => n != "Transform" && n != "RectTransform")
                     .ToArray();

        int descendants = t.GetComponentsInChildren<Transform>(true).Length - 1;
        sb.Append(indent).Append(t.name)
          .Append("  [kids:").Append(t.childCount)
          .Append(" desc:").Append(descendants)
          .Append(t.gameObject.activeSelf ? "" : " INACTIVE")
          .Append("]");
        if (comps.Length > 0) sb.Append("  {").Append(string.Join(", ", comps)).Append("}");
        sb.AppendLine();

        if (depth >= maxDepth) return;
        foreach (Transform child in t) Describe(child, sb, depth + 1, maxDepth);
    }

    // ---------------------------------------------------------------- scene generation

    [MenuItem("Tools/Playable Ad/1. Build Playable Scene")]
    public static void BuildPlayableScene()
    {
        var log = new StringBuilder();

        EnsureArtAssets(log);

        // Copy the source scene to the playable path FIRST, then edit the copy. Editing the
        // source scene in place and saving a copy leaves the real game scene open and dirty, and
        // anything that later saves open scenes (Unity on quit, or the Playworks build) writes
        // the stripped version straight over Assets/Scenes/LastBrick.unity.
        Directory.CreateDirectory(Path.GetDirectoryName(PlayableScene));
        var source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
        int rootsBefore = source.GetRootGameObjects().Length;
        if (!EditorSceneManager.SaveScene(source, PlayableScene, saveAsCopy: true))
            throw new Exception("[Playable] Could not copy the source scene to " + PlayableScene);

        var scene = EditorSceneManager.OpenScene(PlayableScene, OpenSceneMode.Single);
        log.AppendLine("Source: " + SourceScene + " (copied, never modified)");
        log.AppendLine("Roots before: " + rootsBefore);

        int deleted = StripRoots(scene, log);

        GameObject tower = InstantiateTower(scene, log);

        // Strip after the tower exists so its brick scripts are cleaned up in the same pass.
        int strippedComponents = StripForeignComponents(scene, log);

        StripBuiltinMeshes(scene, log);
        EnsureDirectionalLight(scene, log);
        Camera cam = SetupCamera(scene, tower, log);
        var ui = BuildUI(cam, log);
        WireController(tower, cam, ui, log);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        log.AppendLine("Saved: " + saved + " -> " + PlayableScene);
        log.AppendLine("Roots deleted: " + deleted + ", components stripped: " + strippedComponents);

        ScrubDefaultSpotCookie(log);

        AssetDatabase.Refresh();

        var info = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), PlayableScene));
        if (info.Exists) log.AppendLine("Scene file size: " + (info.Length / 1024) + " KB");

        File.WriteAllText(Path.Combine(ReportDir, "build-scene.log"), log.ToString());
        Debug.Log("[Playable] Scene built.\n" + log);
    }

    /// <summary>
    /// Clears RenderSettings.m_SpotCookie, the one built-in reference Unity writes into every
    /// scene by default and exposes no API to clear. It is the last thing that would send the
    /// Playworks exporter down the BuiltinResource reflection path that fails with LP1025.
    /// Nothing in the playable uses a spot cookie, so dropping it changes no visuals.
    /// </summary>
    private static void ScrubDefaultSpotCookie(StringBuilder log)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), PlayableScene);
        string[] lines = File.ReadAllLines(fullPath);
        bool changed = false;

        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].TrimStart().StartsWith("m_SpotCookie:")) continue;
            if (lines[i].Contains("fileID: 0}")) continue;

            int indent = lines[i].Length - lines[i].TrimStart().Length;
            lines[i] = new string(' ', indent) + "m_SpotCookie: {fileID: 0}";
            changed = true;
            break; // Only RenderSettings carries one.
        }

        if (changed)
        {
            File.WriteAllLines(fullPath, lines);
            log.AppendLine("Default spot cookie cleared (last built-in reference removed).");
        }
        else log.AppendLine("No default spot cookie to clear.");
    }

    private static GameObject InstantiateTower(Scene scene, StringBuilder log)
    {
        var prefab = Resources.Load<GameObject>(LevelResourcePath);
        if (prefab == null)
            throw new Exception("[Playable] Level prefab not found: Resources/" + LevelResourcePath);

        var tower = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        tower.transform.position = Vector3.zero;
        tower.SetActive(true);

        // Break the prefab link so the generated scene never writes back to the shipped level.
        PrefabUtility.UnpackPrefabInstance(tower, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        log.AppendLine("Tower instantiated: " + LevelResourcePath +
                       " (bricks: " + tower.GetComponentsInChildren<Rigidbody>(true).Length + ")");
        return tower;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    private static int StripRoots(Scene scene, StringBuilder log)
    {
        var keep = new HashSet<string>(KeepRoots);
        var doomed = scene.GetRootGameObjects().Where(go => !keep.Contains(go.name)).ToList();

        foreach (var go in doomed)
        {
            log.AppendLine("  delete root: " + go.name);
            UnityEngine.Object.DestroyImmediate(go);
        }
        return doomed.Count;
    }

    /// <summary>
    /// Removes leftover behaviours that the playable cannot carry: every Assembly-CSharp script
    /// outside KeepScripts (they pull in Firebase, AdMob, GPGS, IAP and the manager stack) and
    /// every Cinemachine component (its input axis controller needs the Input System package).
    /// </summary>
    private static int StripForeignComponents(Scene scene, StringBuilder log)
    {
        int removed = 0;
        var counts = new Dictionary<string, int>();

        foreach (var root in scene.GetRootGameObjects())
        {
            var behaviours = root.GetComponentsInChildren<Component>(true);
            foreach (var c in behaviours)
            {
                if (c == null) continue;

                Type type = c.GetType();
                string typeName = type.Name;
                bool isProjectScript = type.Assembly.GetName().Name == "Assembly-CSharp";
                bool isCinemachine = type.Namespace != null && type.Namespace.StartsWith("Unity.Cinemachine");

                if (!isCinemachine && !(isProjectScript && !KeepScripts.Contains(typeName))) continue;

                try
                {
                    UnityEngine.Object.DestroyImmediate(c);
                    removed++;
                    counts[typeName] = counts.ContainsKey(typeName) ? counts[typeName] + 1 : 1;
                }
                catch (Exception e)
                {
                    // Usually a RequireComponent dependency; the owner is stripped in the same pass.
                    log.AppendLine("  ! could not strip " + typeName + ": " + e.Message);
                }
            }
        }

        foreach (var kv in counts.OrderByDescending(k => k.Value))
            log.AppendLine("  strip component: " + kv.Key + " x" + kv.Value);

        return removed;
    }

    /// <summary>
    /// Replaces Unity's built-in meshes with generated project assets of identical geometry.
    /// Playworks 7.2.0 reflects into UnityEditor.BuiltinResource to collect built-ins, and that
    /// reflection fails on Unity versions it does not know, aborting the export with LP1025:
    /// "Field 'UnityEditor.BuiltinResource.m_InstanceID' not found".
    ///
    /// The Floor is a built-in Cube and it IS the visible ground under the tower, so its renderer
    /// cannot simply be deleted — a swap for an identical generated cube keeps the visuals and
    /// the collider exactly as they were. Anything built-in with no known replacement falls back
    /// to having its renderer stripped.
    /// </summary>
    private static void StripBuiltinMeshes(Scene scene, StringBuilder log)
    {
        Mesh cube = EnsureCubeMesh();
        int replaced = 0;
        int removed = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var filter in filters)
            {
                if (filter == null || filter.sharedMesh == null) continue;

                string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                // Built-in assets live at these two virtual paths, or at no path at all.
                bool isBuiltin = path == "Library/unity default resources" ||
                                 path == "Resources/unity_builtin_extra" ||
                                 string.IsNullOrEmpty(path);
                if (!isBuiltin) continue;

                var go = filter.gameObject;
                string meshName = filter.sharedMesh.name;

                if (meshName == "Cube")
                {
                    filter.sharedMesh = cube;
                    log.AppendLine("  swap builtin mesh: " + go.name + " (Cube -> generated)");
                    replaced++;
                    continue;
                }

                log.AppendLine("  strip builtin mesh: " + go.name + " (" + meshName + ", no replacement)");
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
                UnityEngine.Object.DestroyImmediate(filter);
                removed++;
            }
        }

        log.AppendLine("Builtin meshes replaced: " + replaced + ", renderers stripped: " + removed);
    }

    private const string CubeMeshPath = ArtDir + "/PlayableCube.asset";

    /// <summary>Builds a 1x1x1 cube matching Unity's built-in Cube: 24 verts, per-face UVs.</summary>
    private static Mesh EnsureCubeMesh()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(CubeMeshPath);
        if (existing != null) return existing;

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        AddCubeFace(Vector3.up,      Vector3.right,   Vector3.forward, verts, norms, uvs, tris);
        AddCubeFace(Vector3.down,    Vector3.right,   Vector3.back,    verts, norms, uvs, tris);
        AddCubeFace(Vector3.forward, Vector3.left,    Vector3.up,      verts, norms, uvs, tris);
        AddCubeFace(Vector3.back,    Vector3.right,   Vector3.up,      verts, norms, uvs, tris);
        AddCubeFace(Vector3.right,   Vector3.forward, Vector3.up,      verts, norms, uvs, tris);
        AddCubeFace(Vector3.left,    Vector3.back,    Vector3.up,      verts, norms, uvs, tris);

        var mesh = new Mesh { name = "PlayableCube" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        Directory.CreateDirectory(ArtDir);
        AssetDatabase.CreateAsset(mesh, CubeMeshPath);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Mesh>(CubeMeshPath);
    }

    private static void AddCubeFace(Vector3 n, Vector3 r, Vector3 u,
        List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
    {
        int b = verts.Count;
        Vector3 c = n * 0.5f;

        verts.Add(c - r * 0.5f - u * 0.5f);
        verts.Add(c + r * 0.5f - u * 0.5f);
        verts.Add(c + r * 0.5f + u * 0.5f);
        verts.Add(c - r * 0.5f + u * 0.5f);

        for (int i = 0; i < 4; i++) norms.Add(n);

        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(1f, 1f));
        uvs.Add(new Vector2(0f, 1f));

        // Unity treats clockwise-from-the-front as the front face.
        tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
        tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
    }

    private static void EnsureDirectionalLight(Scene scene, StringBuilder log)
    {
        var light = FindRoot(scene, "Directional Light");
        if (light == null) return;

        // The shipped scene keeps this off and lights from the Stone_Glow set; the playable needs a
        // dependable key light because most of that set is bound to deleted level dressing.
        if (!light.activeSelf)
        {
            light.SetActive(true);
            var l = light.GetComponent<Light>();
            if (l != null) l.intensity = Mathf.Max(l.intensity, 0.9f);
            log.AppendLine("Directional Light enabled as key light.");
        }
    }

    private static Camera SetupCamera(Scene scene, GameObject tower, StringBuilder log)
    {
        var camGo = FindRoot(scene, "Main Camera");
        if (camGo == null) throw new Exception("[Playable] Main Camera not found.");

        var cam = camGo.GetComponent<Camera>();

        Bounds b = ComputeBounds(tower);
        var pivot = new GameObject("CameraPivot");
        pivot.transform.position = new Vector3(b.center.x, b.center.y, b.center.z);
        SceneManager.MoveGameObjectToScene(pivot, scene);

        var rig = camGo.GetComponent<PlayableCameraRig>();
        if (rig == null) rig = camGo.AddComponent<PlayableCameraRig>();
        rig.target = pivot.transform;

        // Frame the whole tower with headroom for the end card, from its diagonal extent.
        float radius = Mathf.Max(b.extents.magnitude, 0.5f);
        rig.distance = radius * 2.6f;
        rig.height = b.extents.y * 0.35f;
        rig.portraitDistanceBoost = radius * 0.9f;

        cam.transform.position = pivot.transform.position + new Vector3(0f, rig.height, rig.distance);
        cam.transform.LookAt(pivot.transform.position);

        log.AppendLine("Camera framed: bounds=" + b + " distance=" + rig.distance.ToString("F2") +
                       " height=" + rig.height.ToString("F2"));
        return cam;
    }

    private static Bounds ComputeBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    // ---------------------------------------------------------------- UI

    private class PlayableUI
    {
        public PlayableEndCard EndCard;
        public PlayableTutorialHand Hand;
    }

    private static PlayableUI BuildUI(Camera cam, StringBuilder log)
    {
        var canvasGo = new GameObject("PlayableCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        // 0.5 keeps the layout usable in both orientations, which ad networks both serve.
        scaler.matchWidthOrHeight = 0.5f;

        var ui = new PlayableUI();
        ui.Hand = BuildHand(canvasGo.transform, cam);
        ui.EndCard = BuildEndCard(canvasGo.transform);

        log.AppendLine("UI built: canvas 1080x1920, hand + end card.");
        return ui;
    }

    private const string ArtDir = "Assets/Playable/Art";
    private const string HandSpritePath = ArtDir + "/PlayableHand.png";
    private const string PanelSpritePath = ArtDir + "/PlayablePanel.png";

    /// <summary>
    /// Generates the two UI sprites the playable needs as real project assets.
    /// They used to come from AssetDatabase.GetBuiltinExtraResource("UI/Skin/..."), but every
    /// built-in reference sends the Playworks exporter down its BuiltinResource reflection path,
    /// which is what fails with LP1025 on unsupported Unity versions.
    /// </summary>
    private static void EnsureArtAssets(StringBuilder log)
    {
        Directory.CreateDirectory(ArtDir);

        bool wrote = false;
        wrote |= WriteSprite(HandSpritePath, BuildCircleTexture(128), border: Vector4.zero);
        wrote |= WriteSprite(PanelSpritePath, BuildRoundedRectTexture(64, 16), border: new Vector4(18, 18, 18, 18));

        if (wrote) AssetDatabase.Refresh();
        log.AppendLine("Art assets ready (generated, no built-in sprites): " + ArtDir);
    }

    private static bool WriteSprite(string assetPath, Texture2D texture, Vector4 border)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (File.Exists(fullPath))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            return false;
        }

        File.WriteAllBytes(fullPath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return true;
    }

    private static Texture2D BuildCircleTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                // One pixel of feather so the hint does not look aliased against the tower.
                float a = Mathf.Clamp01(r - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D BuildRoundedRectTexture(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance outside the rounded-rect body, measured from the nearest corner arc.
                float dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius), 0f);
                float dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        return tex;
    }

    private static Sprite LoadSprite(string assetPath)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null) throw new Exception("[Playable] Sprite not found: " + assetPath);
        return sprite;
    }

    private static PlayableTutorialHand BuildHand(Transform parent, Camera cam)
    {
        var go = new GameObject("TutorialHand", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(140f, 140f);

        var image = go.GetComponent<Image>();
        image.sprite = LoadSprite(HandSpritePath);
        image.color = new Color(1f, 1f, 1f, 0.85f);
        image.raycastTarget = false;

        var hand = go.AddComponent<PlayableTutorialHand>();
        hand.worldCamera = cam;
        go.SetActive(false);
        return hand;
    }

    private static PlayableEndCard BuildEndCard(Transform parent)
    {
        var root = new GameObject("EndCard", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());

        // Full-screen dim that is itself a button, so any tap converts.
        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
        dim.transform.SetParent(root.transform, false);
        Stretch(dim.GetComponent<RectTransform>());
        var dimImage = dim.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.72f);

        var title = CreateText(root.transform, "Title", "TOWER MASTER", 110f);
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.62f);
        titleRect.anchorMax = new Vector2(0.5f, 0.62f);
        titleRect.sizeDelta = new Vector2(1000f, 260f);
        title.color = Color.white;

        var subtitle = CreateText(root.transform, "Subtitle", "Can you pull them all?", 56f);
        var subRect = subtitle.rectTransform;
        subRect.anchorMin = new Vector2(0.5f, 0.52f);
        subRect.anchorMax = new Vector2(0.5f, 0.52f);
        subRect.sizeDelta = new Vector2(900f, 120f);
        subtitle.color = new Color(1f, 0.92f, 0.72f);

        // CTA
        var ctaGo = new GameObject("CTAButton", typeof(RectTransform), typeof(Image), typeof(Button));
        ctaGo.transform.SetParent(root.transform, false);
        var ctaRect = ctaGo.GetComponent<RectTransform>();
        ctaRect.anchorMin = new Vector2(0.5f, 0.3f);
        ctaRect.anchorMax = new Vector2(0.5f, 0.3f);
        ctaRect.sizeDelta = new Vector2(680f, 180f);

        var ctaImage = ctaGo.GetComponent<Image>();
        ctaImage.sprite = LoadSprite(PanelSpritePath);
        ctaImage.type = Image.Type.Sliced;
        ctaImage.color = new Color(0.16f, 0.78f, 0.31f);

        var ctaLabel = CreateText(ctaGo.transform, "Label", "PLAY NOW", 72f);
        Stretch(ctaLabel.rectTransform);
        ctaLabel.color = Color.white;

        var endCard = root.AddComponent<PlayableEndCard>();
        // Diagnostics off for shipping. Flip to true to trace pointer handling in a build.
        endCard.logTaps = false;
        endCard.root = root;
        endCard.fullScreenButton = dim.GetComponent<Button>();
        endCard.ctaButton = ctaGo.GetComponent<Button>();

        root.SetActive(false);
        return endCard;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string content, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        var font = TMP_Settings.defaultFontAsset;
        if (font != null) text.font = font;

        return text;
    }

    // ---------------------------------------------------------------- wiring

    private static void WireController(GameObject tower, Camera cam, PlayableUI ui, StringBuilder log)
    {
        var go = new GameObject("PlayableAd");
        var interactor = go.AddComponent<PlayableBrickInteractor>();
        // Diagnostics off for shipping. Flip to true to trace pointer handling in a build.
        interactor.logInput = false;
        var controller = go.AddComponent<PlayableAdController>();

        interactor.physicsRoot = tower.transform;

        controller.interactor = interactor;
        controller.endCard = ui.EndCard;
        controller.tutorialHand = ui.Hand;
        controller.towerRoot = tower.transform;

        Bounds b = ComputeBounds(tower);
        // A brick landing a body-length below the tower base is unambiguously a collapse.
        controller.collapseY = b.min.y - b.size.y * 0.5f - 1f;

        log.AppendLine("Controller wired: bricksToWin=" + controller.bricksToWin +
                       " collapseY=" + controller.collapseY.ToString("F2"));
    }

    // ---------------------------------------------------------------- player settings + build

    [MenuItem("Tools/Playable Ad/2. Configure Player Settings For Web")]
    public static void ConfigureForWeb()
    {
        var log = new StringBuilder();

        // Playworks converts C# to JS and cannot carry the Input System package's native device
        // bindings, so the legacy Input Manager has to be compiled in. "Both" leaves the existing
        // Android build, which uses the Input System, working exactly as before.
        SetActiveInputHandler(2, log);

        PlayerSettings.SetApiCompatibilityLevel(
            UnityEditor.Build.NamedBuildTarget.WebGL, ApiCompatibilityLevel.NET_Unity_4_8);
        log.AppendLine("WebGL API compatibility: .NET Framework (required by the Luna compiler).");

        // Without this the player pauses whenever the canvas loses focus, which freezes Time and
        // stalls every timer. A playable ad runs inside an iframe that frequently does not hold
        // focus, so pausing there would strand the player on a dead frame.
        PlayerSettings.runInBackground = true;
        log.AppendLine("Run In Background enabled (playables rarely hold focus).");

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.dataCaching = false;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.SetIl2CppCompilerConfiguration(
            UnityEditor.Build.NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Master);
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetManagedStrippingLevel(
            UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
        log.AppendLine("WebGL: uncompressed, no data caching, exceptions off, high stripping.");

        // Build Settings is deliberately left alone. It must keep Assets/Scenes/LastBrick.unity
        // as the game's startup scene, or an Android build ships without the game in it.
        // Nothing here needs it: BuildWebGL passes its scene list explicitly, and Playworks reads
        // the scene list from luna.json.
        log.AppendLine("Build settings left untouched (LastBrick stays the game's startup scene).");

        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(ReportDir, "configure.log"), log.ToString());
        Debug.Log("[Playable] Configured.\n" + log);
    }

    /// <summary>
    /// Sets Active Input Handling. There is no public API, so this edits the serialized
    /// ProjectSettings property directly: 0 = legacy, 1 = Input System, 2 = both.
    /// </summary>
    private static void SetActiveInputHandler(int value, StringBuilder log)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets == null || assets.Length == 0)
        {
            log.AppendLine("! ProjectSettings.asset unreadable; set Active Input Handling to Both by hand.");
            return;
        }

        var so = new SerializedObject(assets[0]);
        var prop = so.FindProperty("activeInputHandler");
        if (prop == null)
        {
            log.AppendLine("! activeInputHandler property missing; set Active Input Handling by hand.");
            return;
        }

        if (prop.intValue == value)
        {
            log.AppendLine("Active Input Handling already " + value + " (Both).");
            return;
        }

        log.AppendLine("Active Input Handling: " + prop.intValue + " -> " + value + " (Both).");
        prop.intValue = value;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Switches the active build target to WebGL. This must happen in its own Editor session:
    /// building straight to WebGL while the Editor assemblies are still compiled for Android
    /// fails with "script class layout is incompatible between the editor and the player".
    /// </summary>
    public static void SwitchToWebGL()
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
        {
            Debug.Log("[Playable] Active build target already WebGL.");
            return;
        }

        bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(
            UnityEditor.Build.NamedBuildTarget.WebGL, BuildTarget.WebGL);
        Debug.Log("[Playable] Switched active build target to WebGL: " + ok);
        if (!ok) throw new Exception("[Playable] Could not switch active build target to WebGL.");
    }

    [MenuItem("Tools/Playable Ad/3. Build WebGL Preview")]
    public static void BuildWebGL()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            throw new Exception("[Playable] Active build target is " +
                EditorUserBuildSettings.activeBuildTarget +
                ". Run PlayableBuildTools.SwitchToWebGL in a separate Editor session first.");

        string output = Path.Combine(Directory.GetCurrentDirectory(), WebGLOutput);
        Directory.CreateDirectory(output);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { PlayableScene },
            locationPathName = output,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        var log = new StringBuilder();
        log.AppendLine("Result: " + summary.result);
        log.AppendLine("Output: " + output);
        log.AppendLine("Total size: " + (summary.totalSize / 1024 / 1024) + " MB");
        log.AppendLine("Errors: " + summary.totalErrors + ", warnings: " + summary.totalWarnings);
        log.AppendLine("Duration: " + summary.totalTime);

        File.WriteAllText(Path.Combine(ReportDir, "build-webgl.log"), log.ToString());
        Debug.Log("[Playable] WebGL build.\n" + log);

        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("[Playable] WebGL build failed: " + summary.result);
    }

    // ---------------------------------------------------------------- helpers

    private static string ArgOr(string flag, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == flag) return args[i + 1];
        return fallback;
    }
}
