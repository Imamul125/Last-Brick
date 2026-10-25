using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Generates a batch of new towers (Resources/Levels/Tower_N.prefab) that follow the same building rules as the
/// existing 50 (Jenga Level Generator shapes, interlocking layers, protected bricks in the upper half, a few
/// bricks pushed out like the hand-tuned levels), with a smooth difficulty curve.
///
/// Every tower is physics-tested in an isolated scene before it is saved:
///  1. it must stand still on its own, and
///  2. an automatic solver must be able to get every protected brick onto the pedestal.
/// The solver's move count sets Max Moves (plus slack) and Time Limit. Levels are then added to the
/// LevelManager in the open scene, and the camera config is extended to cover them.
///
/// Menu: Tools > Last Brick > Levels > Tower Batch Generator
/// </summary>
public class TowerBatchGenerator : EditorWindow
{
    private const string BrickPath = "Assets/Prefabs/Perfect_Brick_LowPoly.prefab";
    private const string ProtectedPath = "Assets/Prefabs/Protected_Brick.prefab";
    private const string LevelsFolder = "Assets/Resources/Levels";

    /// <summary>Added on top of the calculated Max Moves / Time Limit so levels aren't too tight.</summary>
    private const int ExtraMoves = 5;
    private const float ExtraSeconds = 10f;

    private enum Shape { Standard, SolidHouse, HollowHouse, Pyramid, Stairs }

    private int adjustFrom = 51;
    private int adjustTo = 150;
    private int adjustMoves = ExtraMoves;
    private float adjustSeconds = ExtraSeconds;

    private int startLevel = 51;
    private int count = 100;
    private bool physicsTest = true;
    private bool overwrite = true;
    private bool addToLevelManager = true;
    private float pedestalWidth = 3f;
    private float pedestalDepth = 3f;
    private float pedestalTop = 0f;

    [MenuItem("Tools/Last Brick/Levels/Tower Batch Generator")]
    public static void Open()
    {
        var w = GetWindow<TowerBatchGenerator>("Tower Batch Generator");
        w.DetectDefaults();
    }

    private void DetectDefaults()
    {
        LevelManager lm = FindFirstObjectByType<LevelManager>(FindObjectsInactive.Include);
        if (lm != null) startLevel = Mathf.Max(startLevel, lm.levels.Count + 1);

        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Pedestal"))
        {
            Collider c = go.GetComponent<Collider>();
            if (c == null || c.bounds.size.x < 0.5f) continue;
            pedestalWidth = c.bounds.size.x;
            pedestalDepth = c.bounds.size.z;
            pedestalTop = c.bounds.max.y;
            break;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Creates new Tower_N prefabs in Resources/Levels, physics-tests each one, and adds them to the LevelManager in the open scene with Max Moves and Time Limit.", MessageType.Info);
        startLevel = EditorGUILayout.IntField("First Level Number", startLevel);
        count = EditorGUILayout.IntSlider("How Many Towers", count, 1, 200);
        physicsTest = EditorGUILayout.Toggle(new GUIContent("Physics Test (recommended)", "Rejects towers that fall over or cannot be solved. Takes a few minutes for 100 towers."), physicsTest);
        overwrite = EditorGUILayout.Toggle("Overwrite Existing Prefabs", overwrite);
        addToLevelManager = EditorGUILayout.Toggle("Add To LevelManager", addToLevelManager);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pedestal (used by the physics test)", EditorStyles.boldLabel);
        pedestalWidth = EditorGUILayout.FloatField("Width (X)", pedestalWidth);
        pedestalDepth = EditorGUILayout.FloatField("Depth (Z)", pedestalDepth);
        pedestalTop = EditorGUILayout.FloatField("Top Height", pedestalTop);
        if (GUILayout.Button("Detect From Scene")) DetectDefaults();

        EditorGUILayout.Space();
        if (GUILayout.Button($"Generate Levels {startLevel} - {startLevel + count - 1}", GUILayout.Height(40)))
        {
            Generate();
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Adjust Existing Levels", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Adds moves and seconds to levels already in the LevelManager (e.g. towers generated before the +5 / +10s bonus). Click once - every click adds again. Ctrl+Z undoes it.", MessageType.None);
        adjustFrom = EditorGUILayout.IntField("From Level", adjustFrom);
        adjustTo = EditorGUILayout.IntField("To Level", adjustTo);
        adjustMoves = EditorGUILayout.IntField("Add Moves", adjustMoves);
        adjustSeconds = EditorGUILayout.FloatField("Add Seconds", adjustSeconds);
        if (GUILayout.Button($"Add +{adjustMoves} Moves / +{adjustSeconds}s To Levels {adjustFrom} - {adjustTo}"))
        {
            AdjustExisting();
        }
    }

    private void AdjustExisting()
    {
        LevelManager lm = FindFirstObjectByType<LevelManager>(FindObjectsInactive.Include);
        if (lm == null)
        {
            EditorUtility.DisplayDialog("Adjust Levels", "Open the LastBrick scene first (no LevelManager found).", "OK");
            return;
        }

        Undo.RecordObject(lm, "Adjust level moves and time");
        int changed = 0;
        foreach (LevelData l in lm.levels)
        {
            if (l.levelNumber < adjustFrom || l.levelNumber > adjustTo) continue;
            l.maxMoves += adjustMoves;
            l.timeLimit += adjustSeconds;
            changed++;
        }
        EditorUtility.SetDirty(lm);
        EditorSceneManager.MarkSceneDirty(lm.gameObject.scene);
        EditorUtility.DisplayDialog("Adjust Levels", $"Updated {changed} levels (+{adjustMoves} moves, +{adjustSeconds}s). Save the scene to keep it.", "OK");
    }

    // ================================================================ Plan

    private struct BrickSpec
    {
        public Vector3 pos;
        public Quaternion rot;
        public int cols;      // columns of its layer (sets brick length)
        public int layer;
        public int index;
        public bool isProtected;
    }

    private struct Design
    {
        public Shape shape;
        public int rows, cols, missing, protect, pushOut;
    }

    private struct BrickSize
    {
        public float width, length, height, spacing;
        public bool sideways;
        public Quaternion baseRot;
        public Vector3 baseScale;
    }

    /// <summary>
    /// Difficulty never goes down. Level 51 starts at Tower_50's difficulty (4 columns, 12 rows, 3 cats) and every
    /// following level adds rows, columns, cats or missing bricks - all values are monotonic in the level number.
    /// </summary>
    private static Design DesignFor(int level)
    {
        int k = Mathf.Max(0, level - 51);            // 0 for level 51
        var d = new Design { shape = Shape.Standard };
        // Max 5 columns: a 6-column tower is ~5.6 wide and its outer bricks hang past the 3-wide pedestal and fall.
        d.rows = Mathf.Min(17, 12 + k / 18);          // 12 -> 17 rows (one more every 18 levels)
        d.cols = 4 + (k >= 35 ? 1 : 0);                // 4 -> 5 columns (from level 86)
        d.protect = 3 + (k >= 30 ? 1 : 0) + (k >= 70 ? 1 : 0); // 3 -> 4 (lvl 81) -> 5 (lvl 121) cats
        d.missing = Mathf.Min(4 + k / 8, d.rows - 3);   // 4 -> 14 missing bricks
        d.pushOut = 2 + k / 25;                        // 2 -> 5 pushed-out bricks
        return d;
    }

    private static List<BrickSpec> Plan(Design d, BrickSize b, System.Random rng)
    {
        var list = new List<BrickSpec>();
        float h = b.height;

        void Add(int y, int i, int layerCols, Vector3 pos)
        {
            bool rotated = y % 2 == 1;
            list.Add(new BrickSpec
            {
                pos = pos,
                rot = rotated ? b.baseRot * Quaternion.Euler(0, 90, 0) : b.baseRot,
                cols = layerCols, layer = y, index = i
            });
        }

        Vector3 Centered(int y, int i, int layerCols)
        {
            float offset = (i - (layerCols - 1) / 2f) * b.spacing;
            return y % 2 == 1 ? new Vector3(0, y * h + h / 2f, offset) : new Vector3(offset, y * h + h / 2f, 0);
        }

        switch (d.shape)
        {
            case Shape.Standard:
            {
                var avail = new List<int>();
                for (int y = 1; y < d.rows - 1; y++) avail.Add(y * d.cols + d.cols / 2);
                var missing = new HashSet<int>();
                for (int k = 0; k < d.missing && avail.Count > 0; k++)
                {
                    int r = rng.Next(avail.Count);
                    missing.Add(avail[r]);
                    avail.RemoveAt(r);
                }
                int n = 0;
                for (int y = 0; y < d.rows; y++)
                    for (int i = 0; i < d.cols; i++, n++)
                        if (!missing.Contains(n)) Add(y, i, d.cols, Centered(y, i, d.cols));
                break;
            }
            case Shape.SolidHouse:
            case Shape.HollowHouse:
            {
                int baseRows = Mathf.Max(2, d.rows - d.cols / 2);
                for (int y = 0; y < d.rows; y++)
                {
                    bool roof = y >= baseRows;
                    int c = roof ? Mathf.Max(1, d.cols - (y - baseRows + 1) * 2) : d.cols;
                    for (int i = 0; i < c; i++)
                    {
                        bool outer = i == 0 || i == c - 1;
                        if (d.shape == Shape.HollowHouse && !roof && !outer && c >= 3) continue;
                        Add(y, i, c, Centered(y, i, c));
                    }
                }
                break;
            }
            case Shape.Pyramid:
            {
                for (int y = 0; ; y++)
                {
                    int c = d.cols - (y / 2) * 2;
                    if (c < 1) break;
                    for (int i = 0; i < c; i++) Add(y, i, c, Centered(y, i, c));
                }
                break;
            }
            case Shape.Stairs:
            {
                float leftmost = -(d.cols - 1) / 2f * b.spacing;
                float maxLen = b.width * d.cols;
                for (int y = 0; ; y++)
                {
                    int c = d.cols - y / 2;
                    if (c < 1) break;
                    float lengthOffset = maxLen / 2f - b.width * c / 2f;
                    for (int i = 0; i < c; i++)
                    {
                        float offset = leftmost + i * b.spacing;
                        Vector3 pos = y % 2 == 1 ? new Vector3(-lengthOffset, y * h + h / 2f, offset) : new Vector3(offset, y * h + h / 2f, -lengthOffset);
                        Add(y, i, c, pos);
                    }
                }
                break;
            }
        }

        // Protected bricks: upper half, preferring the middle of a layer so a clean drop lands on the pedestal.
        // Only full-width layers, so a cat brick is always full size (never shortened like roof bricks).
        int topLayer = list.Max(s => s.layer);
        int minLayer = Mathf.Max(2, (topLayer + 1) / 2);
        int fullCols = list.Max(s => s.cols);
        var candidates = Enumerable.Range(0, list.Count)
            .Where(k => list[k].layer >= minLayer && list[k].cols == fullCols)
            .OrderBy(k => Mathf.Abs(list[k].index - (list[k].cols - 1) / 2f) + (float)rng.NextDouble() * 1.2f)
            .ToList();
        var usedLayers = new HashSet<int>();
        int placed = 0;
        foreach (int k in candidates)
        {
            if (placed >= d.protect) break;
            if (usedLayers.Contains(list[k].layer)) continue;
            BrickSpec s = list[k];
            s.isProtected = true;
            list[k] = s;
            usedLayers.Add(s.layer);
            placed++;
        }

        // Push a few normal bricks out along their length, like the hand-tuned levels.
        for (int p = 0; p < d.pushOut; p++)
        {
            int k = rng.Next(list.Count);
            BrickSpec s = list[k];
            if (s.isProtected || s.layer == 0) continue;
            float amount = (0.5f + (float)rng.NextDouble() * 0.6f) * (rng.NextDouble() < 0.5 ? -1f : 1f);
            s.pos += s.rot * Vector3.forward * amount;
            list[k] = s;
        }
        return list;
    }

    // ================================================================ Build

    private static BrickSize Measure(GameObject brickPrefab)
    {
        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(brickPrefab);
        Vector3 size;
        BoxCollider box = temp.GetComponent<BoxCollider>();
        if (box != null) size = Vector3.Scale(box.size, temp.transform.lossyScale);
        else size = temp.GetComponentInChildren<Renderer>().bounds.size;
        Vector3 baseScale = temp.transform.localScale;
        DestroyImmediate(temp);

        var b = new BrickSize { sideways = size.x > size.z, baseScale = baseScale };
        b.width = b.sideways ? size.z : size.x;
        b.length = b.sideways ? size.x : size.z;
        b.height = size.y;
        b.spacing = b.width + 0.02f;
        b.baseRot = b.sideways ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
        return b;
    }

    private static GameObject Build(string name, List<BrickSpec> plan, BrickSize b, GameObject brick, GameObject protectedBrick)
    {
        GameObject root = new GameObject(name);
        int fullCols = plan.Max(s => s.cols);
        foreach (BrickSpec s in plan)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(s.isProtected ? protectedBrick : brick);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = s.pos;
            go.transform.localRotation = s.rot;
            Vector3 scale = b.baseScale;
            float ratio = b.width * (s.isProtected ? fullCols : s.cols) / b.length;
            if (b.sideways) scale.x *= ratio; else scale.z *= ratio;
            go.transform.localScale = scale;
        }
        return root;
    }

    // ================================================================ Physics test

    private class Sim
    {
        public Scene scene;
        public PhysicsScene physics;
        public List<Rigidbody> bodies = new List<Rigidbody>();
        public List<bool> isProtected = new List<bool>();
        public float pedTop;
        public Rect pedRect;

        private struct BodyState { public bool active; public Vector3 pos, vel, ang; public Quaternion rot; }

        public Sim(GameObject template, float width, float depth, float top)
        {
            scene = EditorSceneManager.NewPreviewScene();
            physics = scene.GetPhysicsScene();
            pedTop = top;
            pedRect = new Rect(-width / 2f, -depth / 2f, width, depth);

            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.transform.position = new Vector3(0, top - 1f, 0);
            pedestal.transform.localScale = new Vector3(width, 2f, depth);
            SceneManager.MoveGameObjectToScene(pedestal, scene);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, top - 2.6f, 0);
            ground.transform.localScale = new Vector3(200f, 1f, 200f);
            SceneManager.MoveGameObjectToScene(ground, scene);

            GameObject tower = Object.Instantiate(template);
            SceneManager.MoveGameObjectToScene(tower, scene);
            foreach (Rigidbody rb in tower.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.None;
                bodies.Add(rb);
                isProtected.Add(rb.GetComponent<ProtectBrick>() != null);
            }
        }

        public void Dispose() => EditorSceneManager.ClosePreviewScene(scene);

        public void Step(float seconds)
        {
            int n = Mathf.CeilToInt(seconds / 0.02f);
            for (int i = 0; i < n; i++) physics.Simulate(0.02f);
        }

        public object Save()
        {
            var s = new BodyState[bodies.Count];
            for (int i = 0; i < bodies.Count; i++)
            {
                Rigidbody rb = bodies[i];
                s[i] = new BodyState { active = rb.gameObject.activeSelf, pos = rb.position, rot = rb.rotation, vel = rb.linearVelocity, ang = rb.angularVelocity };
            }
            return s;
        }

        public void Load(object state)
        {
            var s = (BodyState[])state;
            for (int i = 0; i < bodies.Count; i++)
            {
                Rigidbody rb = bodies[i];
                rb.gameObject.SetActive(s[i].active);
                rb.transform.SetPositionAndRotation(s[i].pos, s[i].rot);
                rb.position = s[i].pos;
                rb.rotation = s[i].rot;
                if (s[i].active)
                {
                    rb.linearVelocity = s[i].vel;
                    rb.angularVelocity = s[i].ang;
                }
            }
        }

        public float MaxSpeed()
        {
            float m = 0f;
            foreach (Rigidbody rb in bodies) if (rb.gameObject.activeSelf) m = Mathf.Max(m, rb.linearVelocity.magnitude);
            return m;
        }

        public bool Fallen(int i) => bodies[i].position.y < pedTop - 0.2f;

        public bool AnyProtectedFallen()
        {
            for (int i = 0; i < bodies.Count; i++) if (isProtected[i] && Fallen(i)) return true;
            return false;
        }

        /// <summary>Resting on the pedestal, or on a protected brick that is itself safe.</summary>
        public bool AllProtectedSafe()
        {
            var safe = new bool[bodies.Count];
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < bodies.Count; i++)
                {
                    if (!isProtected[i] || safe[i]) continue;
                    Rigidbody rb = bodies[i];
                    if (rb.linearVelocity.magnitude > 0.3f) continue;
                    float bottom = rb.position.y - 0.5f;
                    Vector2 xz = new Vector2(rb.position.x, rb.position.z);
                    bool onPedestal = Mathf.Abs(bottom - pedTop) < 0.15f && pedRect.Contains(xz);
                    bool onSafe = false;
                    for (int j = 0; j < bodies.Count && !onPedestal; j++)
                    {
                        if (!safe[j]) continue;
                        Vector3 o = bodies[j].position;
                        if (Mathf.Abs(bottom - (o.y + 0.5f)) < 0.15f && Vector2.Distance(xz, new Vector2(o.x, o.z)) < 1.2f) onSafe = true;
                    }
                    if (onPedestal || onSafe) { safe[i] = true; changed = true; }
                }
            }
            for (int i = 0; i < bodies.Count; i++) if (isProtected[i] && !safe[i]) return false;
            return true;
        }

        /// <summary>Same rule as BrickInteractor: a brick can slide out unless both ends are blocked.</summary>
        public bool CanRemove(int i)
        {
            Rigidbody rb = bodies[i];
            BoxCollider box = rb.GetComponent<BoxCollider>();
            if (box == null) return true;
            float length = Mathf.Max(box.size.x, box.size.y, box.size.z) * rb.transform.localScale.z;
            Vector3 half = box.size * 0.45f;
            box.enabled = false;
            bool fwd = physics.BoxCast(rb.position, half, rb.transform.forward, out _, rb.rotation, length * 0.6f);
            bool back = physics.BoxCast(rb.position, half, -rb.transform.forward, out _, rb.rotation, length * 0.6f);
            box.enabled = true;
            return !(fwd && back);
        }

        public void Remove(int i) => bodies[i].gameObject.SetActive(false);

        /// <summary>Lower is better: protected bricks closer to the pedestal, huge penalty for losing one.</summary>
        public float Score()
        {
            float s = 0f;
            for (int i = 0; i < bodies.Count; i++)
            {
                if (!isProtected[i]) continue;
                if (Fallen(i)) return 100000f;
                Vector3 p = bodies[i].position;
                s += Mathf.Max(0f, p.y - 0.5f - pedTop);
                Vector2 xz = new Vector2(p.x, p.z);
                if (!pedRect.Contains(xz)) s += 3f;
            }
            return s;
        }
    }

    private struct TestResult { public bool stable, solved; public int moves; }

    private TestResult Test(GameObject tower, int moveLimit)
    {
        var sim = new Sim(tower, pedestalWidth, pedestalDepth, pedestalTop);
        try
        {
            // Stability: nothing may drift far, nothing may fall to the ground.
            var startPositions = new List<Vector3>();
            foreach (Rigidbody rb in sim.bodies) startPositions.Add(rb.position);
            sim.Step(4f);
            for (int i = 0; i < sim.bodies.Count; i++)
            {
                if (Vector3.Distance(sim.bodies[i].position, startPositions[i]) > 0.25f || sim.Fallen(i))
                    return new TestResult { stable = false };
            }

            // Greedy solver with one-step look-ahead.
            object state = sim.Save();
            for (int move = 1; move <= moveLimit; move++)
            {
                var targets = new List<int>();
                for (int i = 0; i < sim.bodies.Count; i++)
                {
                    if (sim.isProtected[i] || !sim.bodies[i].gameObject.activeSelf) continue;
                    Vector3 p = sim.bodies[i].position;
                    float below = float.MaxValue;
                    for (int j = 0; j < sim.bodies.Count; j++)
                    {
                        if (!sim.isProtected[j]) continue;
                        Vector3 q = sim.bodies[j].position;
                        if (p.y < q.y - 0.5f && Mathf.Abs(p.x - q.x) < 1.9f && Mathf.Abs(p.z - q.z) < 1.9f) below = Mathf.Min(below, q.y - p.y);
                    }
                    if (below < float.MaxValue && sim.CanRemove(i)) targets.Add(i);
                }
                if (targets.Count == 0) break;

                // Evaluate the bricks closest under a protected brick first.
                targets = targets.OrderByDescending(i => sim.bodies[i].position.y).Take(6).ToList();
                int best = -1;
                float bestScore = float.MaxValue;
                foreach (int i in targets)
                {
                    sim.Load(state);
                    sim.Remove(i);
                    sim.Step(2f);
                    float sc = sim.Score();
                    if (sim.AllProtectedSafe()) sc -= 1000f;
                    if (sc < bestScore) { bestScore = sc; best = i; }
                }
                if (best < 0 || bestScore >= 100000f) return new TestResult { stable = true, solved = false };

                sim.Load(state);
                sim.Remove(best);
                sim.Step(2.5f);
                state = sim.Save();
                if (sim.AnyProtectedFallen()) return new TestResult { stable = true, solved = false };
                if (sim.AllProtectedSafe()) return new TestResult { stable = true, solved = true, moves = move };
            }
            return new TestResult { stable = true, solved = false };
        }
        finally
        {
            sim.Dispose();
        }
    }

    // ================================================================ Main

    private void Generate()
    {
        GameObject brick = AssetDatabase.LoadAssetAtPath<GameObject>(BrickPath);
        GameObject protectedBrick = AssetDatabase.LoadAssetAtPath<GameObject>(ProtectedPath);
        if (brick == null || protectedBrick == null)
        {
            EditorUtility.DisplayDialog("Tower Batch Generator", $"Could not find {BrickPath} or {ProtectedPath}.", "OK");
            return;
        }

        LevelManager lm = FindFirstObjectByType<LevelManager>(FindObjectsInactive.Include);
        if (addToLevelManager && lm == null)
        {
            EditorUtility.DisplayDialog("Tower Batch Generator", "Open the LastBrick scene first (no LevelManager found).", "OK");
            return;
        }

        BrickSize size = Measure(brick);
        var results = new List<(int level, int moves, float time, string info)>();
        var report = new System.Text.StringBuilder();
        int unverified = 0;
        int prevMoves = 0;
        float prevTime = 0f;
        LevelData last = lm != null ? lm.levels.Where(l => l.levelNumber < startLevel).OrderBy(l => l.levelNumber).LastOrDefault() : null;
        if (last != null) { prevMoves = last.maxMoves; prevTime = last.timeLimit; }

        try
        {
            for (int n = 0; n < count; n++)
            {
                int level = startLevel + n;
                string path = $"{LevelsFolder}/Tower_{level}.prefab";
                if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                {
                    report.AppendLine($"Level {level}: prefab exists, skipped (enable Overwrite to replace).");
                    continue;
                }
                if (EditorUtility.DisplayCancelableProgressBar("Generating towers", $"Level {level} ({n + 1}/{count})", n / (float)count)) break;

                GameObject bestTower = null;
                TestResult bestResult = default;
                Design bestDesign = default;

                // Retries only re-roll WHERE the gaps, cats and pushed-out bricks go - never the difficulty
                // (rows, columns, cat count, missing count). Pushed-out bricks are cosmetic and dropped last.
                // Last resort (only if no full-difficulty tower could even stand): fewer gaps instead of skipping the level.
                for (int attempt = 0; attempt < (physicsTest ? 16 : 1); attempt++)
                {
                    if (attempt >= 10 && bestTower != null) break;
                    var rng = new System.Random(level * 7919 + attempt * 104729);
                    Design d = DesignFor(level);
                    if (attempt >= 7) d.pushOut = 0;
                    if (attempt >= 10) d.missing /= 2;
                    if (attempt >= 14) d.missing = 0;

                    GameObject tower = Build($"Tower_{level}", Plan(d, size, rng), size, brick, protectedBrick);
                    int bricks = tower.GetComponentsInChildren<Rigidbody>().Length;
                    TestResult r = physicsTest ? Test(tower, bricks) : new TestResult { stable = true, solved = false };

                    if (!physicsTest || (r.stable && r.solved))
                    {
                        if (bestTower != null) DestroyImmediate(bestTower);
                        bestTower = tower; bestResult = r; bestDesign = d;
                        break;
                    }
                    if (bestTower == null && r.stable) { bestTower = tower; bestResult = r; bestDesign = d; }
                    else DestroyImmediate(tower);
                }

                if (bestTower == null)
                {
                    report.AppendLine($"Level {level}: FAILED - no stable tower found.");
                    continue;
                }

                int brickCount = bestTower.GetComponentsInChildren<Rigidbody>().Length;
                int moves;
                if (bestResult.solved)
                {
                    moves = Mathf.Max(bestResult.moves + 3, Mathf.CeilToInt(bestResult.moves * 1.35f));
                }
                else
                {
                    moves = Mathf.RoundToInt(brickCount * 0.65f);
                    unverified++;
                }
                // Bigger towers need more moves/time; never allow fewer than the previous level.
                moves = Mathf.Clamp(Mathf.Max(moves, prevMoves), 25, 80);
                float time = Mathf.Clamp(Mathf.Round((moves * 2.6f + 10f) / 5f) * 5f, 80f, 200f);
                time = Mathf.Max(time, prevTime);
                prevMoves = moves;
                prevTime = time;

                // Extra breathing room on top of the calculation (not fed back into prevMoves, so it doesn't stack).
                moves += ExtraMoves;
                time += ExtraSeconds;

                PrefabUtility.SaveAsPrefabAsset(bestTower, path);
                DestroyImmediate(bestTower);

                string info = $"{bestDesign.shape}, {bestDesign.cols} cols, {bestDesign.rows} rows, {bestDesign.protect} protected, {bestDesign.missing} missing, {brickCount} bricks";
                if (bestDesign.missing < DesignFor(level).missing) info += " (gaps reduced so it can stand)";
                info += bestResult.solved ? $", solved in {bestResult.moves}" : (physicsTest ? ", NOT auto-solved - please play-test" : "");
                results.Add((level, moves, time, info));
                report.AppendLine($"Level {level}: {info} -> Max Moves {moves}, Time {time}s");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
        }

        if (addToLevelManager && lm != null && results.Count > 0) RegisterLevels(lm, results);

        string summary = $"Created {results.Count} towers." + (unverified > 0 ? $" {unverified} could not be auto-solved and use an estimated move count - play-test those (listed in the Console)." : "");
        Debug.Log("[TowerBatchGenerator] " + summary + "\n" + report);
        EditorUtility.DisplayDialog("Tower Batch Generator", summary + "\n\nSave the scene to keep the new LevelManager entries.", "OK");
    }

    private static void RegisterLevels(LevelManager lm, List<(int level, int moves, float time, string info)> results)
    {
        Undo.RecordObject(lm, "Add generated levels");
        foreach (var r in results)
        {
            LevelData data = lm.levels.FirstOrDefault(l => l.levelNumber == r.level);
            if (data == null)
            {
                data = new LevelData { levelNumber = r.level };
                lm.levels.Add(data);
            }
            data.maxMoves = r.moves;
            data.timeLimit = r.time;
        }
        lm.levels = lm.levels.OrderBy(l => l.levelNumber).ToList();

        // Taller/wider towers need the camera further back: add a config for levels past the last one.
        int maxLevel = lm.levels.Max(l => l.levelNumber);
        if (lm.cameraConfigs.Count > 0 && !lm.cameraConfigs.Any(c => maxLevel >= c.startLevel && maxLevel <= c.endLevel))
        {
            LevelCameraConfig last = lm.cameraConfigs.OrderBy(c => c.endLevel).Last();
            lm.cameraConfigs.Add(new LevelCameraConfig
            {
                startLevel = last.endLevel + 1,
                endLevel = maxLevel,
                cameraTarget = last.cameraTarget,
                orbitalRadius = last.orbitalRadius + 5f
            });
        }

        EditorUtility.SetDirty(lm);
        EditorSceneManager.MarkSceneDirty(lm.gameObject.scene);
    }
}
