using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using K = EnvironmentBuildKit;

/// <summary>
/// Environment 02 - "Sakura Zen Garden".
/// Sunset over a Japanese temple garden: granite plaza under the tower, raked zen gravel ring with stones,
/// vermilion torii gates, glowing stone lanterns, cherry blossom grove, a dense pine forest on rolling hills,
/// three layers of misty mountains, falling petals and drifting sky lanterns.
/// Menu: Tools > Last Brick > Environments > Build 02 - Sakura Zen Garden
/// </summary>
public static class SakuraZenGardenBuilder
{
    private const string EnvName = "02_SakuraZenGarden";
    private const string Folder = K.RootFolder + "/" + EnvName;

    // Layout (world units). Tower at origin, gameplay camera orbits at radius ~19-30, so nothing tall inside r = 31.
    private const float PlazaRadius = 13.5f;
    private const float GravelOuter = 29.6f;
    private const float GrassInner = 30.2f;
    private const float WorldRadius = 620f;
    private const int Seed = 2024;

    private static float groundY;

    [MenuItem("Tools/Last Brick/Environments/Build 02 - Sakura Zen Garden")]
    public static void Build()
    {
        try
        {
            groundY = K.FindGroundHeight();
            System.Random rng = new System.Random(Seed);

            K.EnsureFolder(Folder + "/Textures");
            K.EnsureFolder(Folder + "/Materials");
            K.EnsureFolder(Folder + "/Meshes");
            string tex = Folder + "/Textures/";
            string m = Folder + "/Materials/";
            string f = Folder + "/Meshes";

            // ---------------- Textures
            Progress("Generating granite plaza...", 0.05f);
            Texture2D graniteTiles = K.SaveProceduralTexture(tex + "T_GraniteTiles.png", 1024, GraniteTileColor);
            Texture2D graniteTilesN = K.SaveProceduralNormal(tex + "T_GraniteTiles_Normal.png", 1024, GraniteTileHeight, 5f);
            Texture2D granite = K.SaveProceduralTexture(tex + "T_Granite.png", 512, (u, v) => Granite(u, v, 3.3f));

            Progress("Generating raked gravel...", 0.2f);
            Texture2D gravel = K.SaveProceduralTexture(tex + "T_RakedGravel.png", 512, GravelColor);
            Texture2D gravelN = K.SaveProceduralNormal(tex + "T_RakedGravel_Normal.png", 512, GravelHeight, 7f);

            Progress("Generating grass...", 0.3f);
            Texture2D grass = K.SaveProceduralTexture(tex + "T_Grass.png", 512, GrassColor);
            Texture2D petal = K.SavePetalTexture(tex + "T_Petal.png");
            Texture2D dot = K.SaveSoftDotTexture(tex + "T_SoftDot.png");

            // ---------------- Materials
            Progress("Creating materials...", 0.38f);
            Material plazaMat = K.LitMaterial(m + "M_GranitePlaza.mat", Color.white, graniteTiles, graniteTilesN, 1f, null, 0f, 0.55f);
            Material graniteMat = K.LitMaterial(m + "M_Granite.mat", Color.white, granite, null, 1f, null, 0f, 0.3f);
            Material basaltMat = K.LitMaterial(m + "M_Basalt.mat", new Color(0.16f, 0.16f, 0.17f), null, null, 1f, null, 0f, 0.7f);
            Material gravelMat = K.LitMaterial(m + "M_RakedGravel.mat", Color.white, gravel, gravelN, 1.2f, null, 0f, 0.15f);
            Material grassMat = K.LitMaterial(m + "M_Grass.mat", Color.white, grass, null, 1f, null, 0f, 0.08f);
            Material rockMat = K.LitMaterial(m + "M_ZenRock.mat", new Color(0.36f, 0.35f, 0.34f), granite, null, 1f, null, 0f, 0.2f);
            Material barkMat = K.LitMaterial(m + "M_Bark.mat", new Color(0.24f, 0.15f, 0.12f), null, null, 1f, null, 0f, 0.15f);
            Material blossomA = K.LitMaterial(m + "M_BlossomPink.mat", new Color(1f, 0.70f, 0.80f), null, null, 1f, null, 0f, 0.25f,
                new Color(0.16f, 0.05f, 0.08f));
            Material blossomB = K.LitMaterial(m + "M_BlossomDeep.mat", new Color(0.93f, 0.52f, 0.68f), null, null, 1f, null, 0f, 0.25f,
                new Color(0.12f, 0.03f, 0.06f));
            Material pineA = K.LitMaterial(m + "M_PineDark.mat", new Color(0.09f, 0.20f, 0.14f), null, null, 1f, null, 0f, 0.1f);
            Material pineB = K.LitMaterial(m + "M_PineLight.mat", new Color(0.15f, 0.29f, 0.17f), null, null, 1f, null, 0f, 0.1f);
            Material mountainNear = K.LitMaterial(m + "M_MountainNear.mat", new Color(0.20f, 0.28f, 0.27f), null, null, 1f, null, 0f, 0f);
            Material mountainMid = K.LitMaterial(m + "M_MountainMid.mat", new Color(0.36f, 0.38f, 0.48f), null, null, 1f, null, 0f, 0f);
            Material mountainFar = K.LitMaterial(m + "M_MountainFar.mat", new Color(0.58f, 0.54f, 0.66f), null, null, 1f, null, 0f, 0f);
            Material lacquerRed = K.LitMaterial(m + "M_VermilionLacquer.mat", new Color(0.80f, 0.12f, 0.05f), null, null, 1f, null, 0f, 0.6f);
            Material lacquerBlack = K.LitMaterial(m + "M_BlackLacquer.mat", new Color(0.04f, 0.04f, 0.04f), null, null, 1f, null, 0f, 0.55f);
            Material lanternGlow = K.LitMaterial(m + "M_LanternGlow.mat", new Color(1f, 0.85f, 0.6f), null, null, 1f, null, 0f, 0.3f,
                new Color(1f, 0.55f, 0.2f) * 4.5f);
            Material petalMat = K.AlphaParticleMaterial(m + "M_Petals.mat", petal);
            Material skyLanternMat = K.AdditiveParticleMaterial(m + "M_SkyLanterns.mat", dot);

            Material sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunDisk", 2f);
            sky.DisableKeyword("_SUNDISK_NONE");
            sky.DisableKeyword("_SUNDISK_SIMPLE");
            sky.EnableKeyword("_SUNDISK_HIGH_QUALITY");
            sky.SetFloat("_SunSize", 0.05f);
            sky.SetFloat("_SunSizeConvergence", 3f);
            sky.SetFloat("_AtmosphereThickness", 1.65f);
            sky.SetColor("_SkyTint", new Color(0.78f, 0.52f, 0.64f));
            sky.SetColor("_GroundColor", new Color(0.52f, 0.40f, 0.42f));
            sky.SetFloat("_Exposure", 1.3f);
            sky = K.SaveAsset(sky, m + "M_Sky_SakuraSunset.mat");

            GameObject root = new GameObject("ENV_" + EnvName);
            Transform t = root.transform;

            // ---------------- Ground layers
            Progress("Building ground...", 0.45f);
            Transform ground = Group("Ground", t);
            K.Part("GranitePlaza", ground, K.SaveMesh(K.Cylinder("SM_Plaza", PlazaRadius, PlazaRadius, 0.5f, 128, 6f), f), plazaMat,
                new Vector3(0f, groundY - 0.5f, 0f));
            K.Part("BasaltInlay", ground, K.SaveMesh(K.Ring("SM_BasaltInlay", 6f, 6.45f, 0.03f, 128, 4f), f), basaltMat,
                new Vector3(0f, groundY, 0f), null, false);
            K.Part("BasaltInlayOuter", ground, K.SaveMesh(K.Ring("SM_BasaltInlayOuter", 12.6f, 12.8f, 0.03f, 128, 4f), f), basaltMat,
                new Vector3(0f, groundY, 0f), null, false);
            K.Part("PlazaCurb", ground, K.SaveMesh(K.Ring("SM_PlazaCurb", PlazaRadius, PlazaRadius + 0.5f, 0.14f, 128, 2f), f), graniteMat,
                new Vector3(0f, groundY - 0.02f, 0f));
            K.Part("RakedGravel", ground, K.SaveMesh(K.PolarGrid("SM_RakedGravel", PlazaRadius + 0.45f, GravelOuter, 192, 4,
                    (r, a) => 0f, 1f, true, 4f, 96f, 2.2f), f), gravelMat, new Vector3(0f, groundY - 0.03f, 0f), null, false);
            K.Part("GardenCurb", ground, K.SaveMesh(K.Ring("SM_GardenCurb", GravelOuter - 0.05f, GrassInner + 0.1f, 0.18f, 160, 2f), f), graniteMat,
                new Vector3(0f, groundY - 0.03f, 0f));
            K.Part("Hills", ground, K.SaveMesh(K.PolarGrid("SM_Hills", GrassInner, WorldRadius, 200, 70,
                    (r, a) => TerrainHeight(r, a), 2.4f, false, 7f), f), grassMat, new Vector3(0f, groundY, 0f));

            // ---------------- Distant mountains (fog gives the layered, misty look)
            Progress("Raising mountains...", 0.55f);
            Transform mountains = Group("Mountains", t);
            K.Part("Range_Near", mountains, K.SaveMesh(K.PolarGrid("SM_Range_Near", 170f, 260f, 256, 10,
                K.MountainRange(170f, 260f, groundY, 18f, 62f, 3.1f)), f), mountainNear, Vector3.zero, null, false);
            K.Part("Range_Mid", mountains, K.SaveMesh(K.PolarGrid("SM_Range_Mid", 260f, 400f, 256, 10,
                K.MountainRange(260f, 400f, groundY, 40f, 120f, 9.7f)), f), mountainMid, Vector3.zero, null, false);
            K.Part("Range_Far", mountains, K.SaveMesh(K.PolarGrid("SM_Range_Far", 400f, 590f, 256, 10,
                K.MountainRange(400f, 590f, groundY, 80f, 190f, 21.4f)), f), mountainFar, Vector3.zero, null, false);

            // ---------------- Trees
            Progress("Growing cherry blossoms and pines...", 0.65f);
            BuildTrees(t, rng, f, barkMat, blossomA, blossomB, pineA, pineB);

            // ---------------- Zen stones in the gravel
            Progress("Placing zen stones...", 0.75f);
            Mesh[] rockVariants =
            {
                K.SaveMesh(K.Blob("SM_Rock_A", 1, 0.35f, 1, new Vector3(1.6f, 0.8f, 1.1f)), f),
                K.SaveMesh(K.Blob("SM_Rock_B", 1, 0.4f, 2, new Vector3(1.0f, 1.1f, 0.9f)), f),
                K.SaveMesh(K.Blob("SM_Rock_C", 1, 0.3f, 3, new Vector3(1.4f, 0.6f, 1.3f)), f),
            };
            var rocks = new MeshBatch();
            float[] rockAngles = { 20f, 95f, 160f, 235f, 300f };
            foreach (float ang in rockAngles)
            {
                float rad = 20f + (float)rng.NextDouble() * 5f;
                Vector3 c = Polar(rad, ang);
                int count = 1 + rng.Next(3);
                for (int i = 0; i < count; i++)
                {
                    float s = i == 0 ? 0.9f + (float)rng.NextDouble() * 0.3f : 0.4f + (float)rng.NextDouble() * 0.3f;
                    Vector3 offset = i == 0 ? Vector3.zero : Polar(1.4f + (float)rng.NextDouble(), (float)rng.NextDouble() * 360f);
                    rocks.Add(rockVariants[rng.Next(rockVariants.Length)], c + offset + Vector3.up * (groundY - 0.15f * s),
                        Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), Vector3.one * s);
                }
            }
            for (int i = 0; i < 40; i++)
            {
                // Scattered boulders on the hills add mid-ground detail.
                float rad = 34f + (float)rng.NextDouble() * 90f;
                float ang = (float)rng.NextDouble() * 360f;
                float s = 0.8f + (float)rng.NextDouble() * 1.8f;
                Vector3 p = Polar(rad, ang);
                p.y = groundY + TerrainHeight(rad, ang * Mathf.Deg2Rad) - 0.3f * s;
                rocks.Add(rockVariants[rng.Next(rockVariants.Length)], p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), Vector3.one * s);
            }
            K.Part("ZenStones", t, K.SaveMesh(rocks.Build("SM_ZenStones"), f), rockMat, Vector3.zero);

            // ---------------- Torii gates and stone lanterns
            Progress("Building torii gates and lanterns...", 0.82f);
            BuildToriiAndLanterns(t, f, lacquerRed, lacquerBlack, graniteMat, lanternGlow);

            // ---------------- Lighting
            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(t, false);
            sun.transform.rotation = Quaternion.Euler(16f, 58f, 0f);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.72f, 0.55f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;

            ReflectionProbe probe = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(t, false);
            probe.transform.localPosition = new Vector3(0f, groundY + 4f, 0f);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(1300f, 600f, 1300f);
            probe.resolution = 128;
            probe.hdr = true;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.cullingMask = ~(1 << 5);

            // ---------------- Atmosphere particles
            BuildPetals(t, petalMat);
            BuildSkyLanterns(t, skyLanternMat);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/ENV_" + EnvName + ".prefab");
            Object.DestroyImmediate(root);

            // ---------------- Post processing + profile
            Progress("Saving profile...", 0.95f);
            VolumeProfile volume = BuildVolumeProfile(Folder + "/PP_" + EnvName + ".asset");

            string profilePath = Folder + "/EnvProfile_" + EnvName + ".asset";
            EnvironmentProfile profile = AssetDatabase.LoadAssetAtPath<EnvironmentProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<EnvironmentProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.displayName = "Sakura Zen Garden";
            profile.environmentPrefab = prefab;
            profile.skybox = sky;
            profile.ambientMode = AmbientMode.Trilight;
            profile.ambientSkyColor = new Color(0.70f, 0.62f, 0.80f);
            profile.ambientEquatorColor = new Color(0.86f, 0.62f, 0.58f);
            profile.ambientGroundColor = new Color(0.26f, 0.22f, 0.20f);
            profile.reflectionIntensity = 0.9f;
            profile.fog = true;
            profile.fogMode = FogMode.Linear;
            profile.fogColor = new Color(0.94f, 0.72f, 0.68f);
            profile.fogStart = 30f;
            profile.fogEnd = 620f;
            profile.postProcessProfile = volume;
            EditorUtility.SetDirty(profile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
            Debug.Log($"[Environments] Built '{profile.displayName}' at {Folder} (floor height {groundY}). " +
                      "Run Tools > Last Brick > Environments > Setup Environment Manager In Scene to register it.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ================================================================ Layout helpers

    private static void Progress(string msg, float p) => EditorUtility.DisplayProgressBar("Sakura Zen Garden", msg, p);

    private static Transform Group(string name, Transform parent)
    {
        Transform g = new GameObject(name).transform;
        g.SetParent(parent, false);
        return g;
    }

    private static Vector3 Polar(float radius, float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
    }

    /// <summary>Rolling hills: flat next to the garden, rising gently towards the forest.</summary>
    private static float TerrainHeight(float r, float angle)
    {
        float x = Mathf.Cos(angle) * r, z = Mathf.Sin(angle) * r;
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GrassInner + 2f, 75f, r));
        float hills = (K.Fbm(x * 0.018f + 50f, z * 0.018f + 50f, 4) - 0.35f) * 9f;
        float far = Mathf.InverseLerp(120f, WorldRadius, r) * 6f;
        return rise * (Mathf.Max(hills, -1f) + 1.2f) + far;
    }

    private static bool NearTorii(float degrees)
    {
        float d = Mathf.Repeat(degrees - 45f, 90f);
        return d < 12f || d > 78f;
    }

    private static void BuildTrees(Transform parent, System.Random rng, string f,
        Material bark, Material blossomA, Material blossomB, Material pineA, Material pineB)
    {
        Mesh limb = K.SaveMesh(K.Cylinder("SM_Limb", 1f, 0.72f, 1f, 8, 2f, true, false), f);
        Mesh[] blobs =
        {
            K.SaveMesh(K.Blob("SM_Canopy_A", 1, 0.25f, 11, Vector3.one), f),
            K.SaveMesh(K.Blob("SM_Canopy_B", 1, 0.3f, 12, new Vector3(1.1f, 0.85f, 1.1f)), f),
            K.SaveMesh(K.Blob("SM_Canopy_C", 1, 0.28f, 13, new Vector3(0.95f, 0.8f, 1.05f)), f),
        };
        Mesh[] cones =
        {
            K.SaveMesh(K.FlatShade(K.Cylinder("SM_PineTier_0", 2.3f, 0f, 3.4f, 8, 2f, false, true)), f),
            K.SaveMesh(K.FlatShade(K.Cylinder("SM_PineTier_1", 1.8f, 0f, 3.0f, 8, 2f, false, true)), f),
            K.SaveMesh(K.FlatShade(K.Cylinder("SM_PineTier_2", 1.25f, 0f, 2.6f, 8, 2f, false, true)), f),
        };

        var barkBatch = new MeshBatch();
        var pinkA = new MeshBatch();
        var pinkB = new MeshBatch();
        var greenA = new MeshBatch();
        var greenB = new MeshBatch();

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // Cherry blossom grove: two staggered rings around the garden, leaving the torii gates open.
        int cherryCount = 0;
        for (int ring = 0; ring < 2; ring++)
        {
            int count = ring == 0 ? 22 : 26;
            for (int i = 0; i < count; i++)
            {
                float ang = (i + (ring == 0 ? 0.5f : 0f)) * 360f / count + Rand(-4f, 4f);
                if (ring == 0 && NearTorii(ang)) continue;
                float rad = ring == 0 ? Rand(35f, 40f) : Rand(45f, 56f);
                float s = ring == 0 ? Rand(0.85f, 1.15f) : Rand(1.0f, 1.4f);
                Vector3 basePos = Polar(rad, ang);
                basePos.y = groundY + TerrainHeight(rad, ang * Mathf.Deg2Rad) - 0.2f;
                AddCherryTree(basePos, s, rng, limb, blobs, barkBatch, pinkA, pinkB);
                cherryCount++;
            }
        }

        // Pine forest on the hills behind - dense enough to fill every camera angle.
        for (int i = 0; i < 230; i++)
        {
            float rad = Mathf.Lerp(62f, 165f, Mathf.Pow((float)rng.NextDouble(), 0.8f));
            float ang = Rand(0f, 360f);
            float s = Mathf.Lerp(1.1f, 2.6f, Mathf.InverseLerp(62f, 165f, rad)) * Rand(0.8f, 1.2f);
            Vector3 p = Polar(rad, ang);
            p.y = groundY + TerrainHeight(rad, ang * Mathf.Deg2Rad) - 0.2f;
            Quaternion rot = Quaternion.Euler(0f, Rand(0f, 360f), 0f);

            barkBatch.AddLimb(limb, p, p + Vector3.up * 2f * s, 0.3f * s);
            MeshBatch g = rng.NextDouble() < 0.5 ? greenA : greenB;
            float y = 1.3f;
            for (int k = 0; k < cones.Length; k++)
            {
                g.Add(cones[k], p + Vector3.up * y * s, rot, Vector3.one * s);
                y += 1.75f - k * 0.15f;
            }
        }

        Transform trees = Group("Trees", parent);
        K.Part("Trunks", trees, K.SaveMesh(barkBatch.Build("SM_Trunks"), f), bark, Vector3.zero);
        K.Part("Blossoms_Pink", trees, K.SaveMesh(pinkA.Build("SM_Blossoms_Pink"), f), blossomA, Vector3.zero);
        K.Part("Blossoms_Deep", trees, K.SaveMesh(pinkB.Build("SM_Blossoms_Deep"), f), blossomB, Vector3.zero);
        K.Part("Pines_Dark", trees, K.SaveMesh(greenA.Build("SM_Pines_Dark"), f), pineA, Vector3.zero);
        K.Part("Pines_Light", trees, K.SaveMesh(greenB.Build("SM_Pines_Light"), f), pineB, Vector3.zero);
        Debug.Log($"[Environments] Sakura Zen Garden: {cherryCount} cherry trees, 230 pines.");
    }

    private static void AddCherryTree(Vector3 basePos, float s, System.Random rng, Mesh limb, Mesh[] blobs,
        MeshBatch bark, MeshBatch pinkA, MeshBatch pinkB)
    {
        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // Gently curving trunk in two segments.
        Vector3 p0 = basePos;
        Vector3 p1 = p0 + new Vector3(Rand(-0.6f, 0.6f), 2.3f, Rand(-0.6f, 0.6f)) * s;
        Vector3 p2 = p1 + new Vector3(Rand(-0.7f, 0.7f), 1.8f, Rand(-0.7f, 0.7f)) * s;
        bark.AddLimb(limb, p0, p1, 0.42f * s);
        bark.AddLimb(limb, p1 - Vector3.up * 0.1f * s, p2, 0.31f * s);

        int branches = 4 + rng.Next(2);
        float start = Rand(0f, 360f);
        for (int b = 0; b < branches; b++)
        {
            float ang = (start + b * 360f / branches + Rand(-20f, 20f)) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(ang), Rand(0.5f, 1.0f), Mathf.Sin(ang)).normalized;
            Vector3 from = b % 2 == 0 ? p2 : Vector3.Lerp(p1, p2, 0.5f);
            Vector3 end = from + dir * Rand(2.0f, 3.0f) * s;
            bark.AddLimb(limb, from, end, 0.2f * s);

            AddBlossom(end + Vector3.up * 0.4f * s, Rand(1.5f, 2.1f) * s, rng, blobs, pinkA, pinkB);
            AddBlossom(Vector3.Lerp(from, end, 0.55f) + Vector3.up * 0.6f * s, Rand(1.1f, 1.5f) * s, rng, blobs, pinkA, pinkB);
        }
        AddBlossom(p2 + Vector3.up * 1.4f * s, Rand(2.0f, 2.5f) * s, rng, blobs, pinkA, pinkB);
    }

    private static void AddBlossom(Vector3 pos, float size, System.Random rng, Mesh[] blobs, MeshBatch a, MeshBatch b)
    {
        MeshBatch batch = rng.NextDouble() < 0.6 ? a : b;
        Quaternion rot = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);
        batch.Add(blobs[rng.Next(blobs.Length)], pos, rot, Vector3.one * size);
    }

    private static void BuildToriiAndLanterns(Transform parent, string f, Material red, Material black, Material stone, Material glow)
    {
        // ---- Torii parts (built facing +Z, pillars along X)
        Mesh pillar = K.SaveMesh(K.Cylinder("SM_Torii_Pillar", 0.4f, 0.33f, 8.6f, 24, 2f), f);
        Mesh pillarBase = K.SaveMesh(K.Cylinder("SM_Torii_Base", 0.52f, 0.46f, 0.7f, 24, 2f), f);
        Mesh nuki = K.SaveMesh(K.Box("SM_Torii_Nuki", new Vector3(8.6f, 0.45f, 0.34f), 2f), f);
        Mesh strut = K.SaveMesh(K.Box("SM_Torii_Strut", new Vector3(0.36f, 1.1f, 0.3f), 2f), f);
        Mesh beamCenter = K.SaveMesh(K.Box("SM_Torii_Kasagi", new Vector3(6.4f, 0.55f, 0.72f), 2f), f);
        Mesh beamWing = K.SaveMesh(K.Box("SM_Torii_KasagiWing", new Vector3(2.7f, 0.55f, 0.72f), 2f), f);
        Mesh capCenter = K.SaveMesh(K.Box("SM_Torii_Cap", new Vector3(6.6f, 0.3f, 0.86f), 2f), f);
        Mesh capWing = K.SaveMesh(K.Box("SM_Torii_CapWing", new Vector3(2.9f, 0.3f, 0.86f), 2f), f);

        var redBatch = new MeshBatch();
        var blackBatch = new MeshBatch();
        for (int i = 0; i < 4; i++)
        {
            float ang = 45f + i * 90f;
            Vector3 pos = Polar(33.5f, ang) + Vector3.up * groundY;
            Quaternion rot = Quaternion.LookRotation(new Vector3(-pos.x, 0f, -pos.z).normalized); // gate faces the tower
            Matrix4x4 gate = Matrix4x4.TRS(pos, rot, Vector3.one);

            foreach (float x in new[] { -3.3f, 3.3f })
            {
                redBatch.Add(pillar, gate * Matrix4x4.Translate(new Vector3(x, 0f, 0f)));
                blackBatch.Add(pillarBase, gate * Matrix4x4.Translate(new Vector3(x, 0f, 0f)));
            }
            redBatch.Add(nuki, gate * Matrix4x4.Translate(new Vector3(0f, 6.5f, 0f)));
            redBatch.Add(strut, gate * Matrix4x4.Translate(new Vector3(0f, 7.3f, 0f)));
            redBatch.Add(beamCenter, gate * Matrix4x4.Translate(new Vector3(0f, 8.05f, 0f)));
            blackBatch.Add(capCenter, gate * Matrix4x4.Translate(new Vector3(0f, 8.45f, 0f)));
            foreach (float side in new[] { -1f, 1f })
            {
                // Upswept ends of the top beam - the signature torii silhouette.
                Matrix4x4 wing = gate * Matrix4x4.TRS(new Vector3(side * 4.35f, 8.2f, 0f), Quaternion.Euler(0f, 0f, side * 7f), Vector3.one);
                redBatch.Add(beamWing, wing);
                blackBatch.Add(capWing, wing * Matrix4x4.Translate(new Vector3(0f, 0.4f, 0f)));
            }
        }

        // ---- Stone lanterns (toro), between the gates
        Mesh lBase = K.SaveMesh(K.FlatShade(K.Cylinder("SM_Toro_Base", 0.75f, 0.65f, 0.35f, 6, 2f, true, false)), f);
        Mesh lPole = K.SaveMesh(K.FlatShade(K.Cylinder("SM_Toro_Pole", 0.28f, 0.24f, 1.1f, 6, 2f, false, false)), f);
        Mesh lDeck = K.SaveMesh(K.FlatShade(K.Cylinder("SM_Toro_Deck", 0.55f, 0.7f, 0.25f, 6, 2f, true, true)), f);
        Mesh lLight = K.SaveMesh(K.FlatShade(K.Cylinder("SM_Toro_Light", 0.42f, 0.42f, 0.65f, 6, 2f, false, false)), f);
        Mesh lRoof = K.SaveMesh(K.FlatShade(K.Cylinder("SM_Toro_Roof", 1.05f, 0.12f, 0.6f, 6, 2f, true, true)), f);
        Mesh lFinial = K.SaveMesh(K.Blob("SM_Toro_Finial", 1, 0.05f, 4, Vector3.one * 0.2f), f);

        var stoneBatch = new MeshBatch();
        var glowBatch = new MeshBatch();
        Transform lights = Group("LanternLights", parent);
        for (int i = 0; i < 8; i++)
        {
            float ang = i * 45f + 22.5f;
            Vector3 p = Polar(32.3f, ang) + Vector3.up * (groundY - 0.03f);
            Quaternion rot = Quaternion.Euler(0f, ang, 0f);
            float y = 0f;
            stoneBatch.Add(lBase, p, rot, Vector3.one); y += 0.35f;
            stoneBatch.Add(lPole, p + Vector3.up * y, rot, Vector3.one); y += 1.1f;
            stoneBatch.Add(lDeck, p + Vector3.up * y, rot, Vector3.one); y += 0.25f;
            glowBatch.Add(lLight, p + Vector3.up * y, rot, Vector3.one); y += 0.65f;
            stoneBatch.Add(lRoof, p + Vector3.up * y, rot, Vector3.one); y += 0.6f;
            stoneBatch.Add(lFinial, p + Vector3.up * (y + 0.1f), rot, Vector3.one);

            // Real point lights on every other lantern - mobile keeps a 4-light-per-object budget.
            if (i % 2 == 0)
            {
                Light l = new GameObject("LanternLight_" + i).AddComponent<Light>();
                l.transform.SetParent(lights, false);
                l.transform.localPosition = p + Vector3.up * 2.0f;
                l.type = LightType.Point;
                l.color = new Color(1f, 0.6f, 0.3f);
                l.intensity = 5f;
                l.range = 9f;
                l.shadows = LightShadows.None;
            }
        }

        Transform structures = Group("Structures", parent);
        K.Part("Torii_Red", structures, K.SaveMesh(redBatch.Build("SM_Torii_Red"), f), red, Vector3.zero);
        K.Part("Torii_Black", structures, K.SaveMesh(blackBatch.Build("SM_Torii_Black"), f), black, Vector3.zero);
        K.Part("Lanterns_Stone", structures, K.SaveMesh(stoneBatch.Build("SM_Lanterns_Stone"), f), stone, Vector3.zero);
        K.Part("Lanterns_Glow", structures, K.SaveMesh(glowBatch.Build("SM_Lanterns_Glow"), f), glow, Vector3.zero, null, false);
    }

    private static void BuildPetals(Transform parent, Material mat)
    {
        GameObject go = new GameObject("FallingPetals");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, groundY + 16f, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 12f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 16f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.80f, 0.87f), new Color(0.98f, 0.62f, 0.76f));
        main.gravityModifier = 0.012f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 450;

        var emission = ps.emission;
        emission.rateOverTime = 32f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(80f, 4f, 80f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.4f, 0.9f); // gentle breeze
        vel.y = new ParticleSystem.MinMaxCurve(-0.2f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = mat;
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;
    }

    private static void BuildSkyLanterns(Transform parent, Material mat)
    {
        GameObject go = new GameObject("SkyLanterns");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, groundY + 30f, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 30f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(35f, 50f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.62f, 0.25f), new Color(1f, 0.45f, 0.15f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 140;

        var emission = ps.emission;
        emission.rateOverTime = 3.5f;

        // Hollow ring so lanterns float over the forest, never in front of the tower.
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = 140f;
        shape.donutRadius = 70f;
        shape.rotation = new Vector3(90f, 0f, 0f);
        shape.scale = new Vector3(1f, 1f, 0.25f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        vel.y = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.1f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.9f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = mat;
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;
    }

    // ================================================================ Procedural textures

    private static Color Granite(float u, float v, float seed)
    {
        float n = K.SeamlessFbm(u, v, 8f, 4, seed);
        float speck = K.SeamlessFbm(u, v, 64f, 2, seed + 5f);
        float dark = speck > 0.62f ? 0.55f : speck < 0.3f ? 1.12f : 1f;
        float g = Mathf.Lerp(0.50f, 0.64f, n) * dark;
        Color c = new Color(g * 1.02f, g, g * 0.97f, 1f);
        return c;
    }

    // 3x3 tiles per texture with recessed mortar joints. Texture covers 6 world units -> 2 m tiles.
    private const int GraniteTilesPerSide = 3;
    private const float JointHalfWidth = 0.012f;

    private static float JointDistance(float u, float v)
    {
        float tu = u * GraniteTilesPerSide, tv = v * GraniteTilesPerSide;
        float du = Mathf.Abs(tu - Mathf.Round(tu)), dv = Mathf.Abs(tv - Mathf.Round(tv));
        return Mathf.Min(du, dv) / GraniteTilesPerSide;
    }

    private static Color GraniteTileColor(float u, float v)
    {
        int tile = Mathf.FloorToInt(u * GraniteTilesPerSide) + Mathf.FloorToInt(v * GraniteTilesPerSide) * 7;
        Color c = Granite(u, v, 1.7f) * (0.92f + 0.12f * Mathf.PerlinNoise(tile * 3.1f, 0.5f));
        c.a = 1f;
        float d = JointDistance(u, v);
        return d < JointHalfWidth ? new Color(0.22f, 0.21f, 0.2f, 1f) : c;
    }

    private static float GraniteTileHeight(float u, float v)
    {
        float d = JointDistance(u, v);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(JointHalfWidth * 0.6f, JointHalfWidth * 1.8f, d));
    }

    // Gravel: 8 rake grooves across the texture (v runs outward on the ring -> concentric lines).
    private static float GravelHeight(float u, float v)
    {
        float grooves = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * 8f);
        float grain = K.SeamlessFbm(u, v, 48f, 2, 9.1f);
        return grooves * 0.75f + grain * 0.25f;
    }

    private static Color GravelColor(float u, float v)
    {
        float h = GravelHeight(u, v);
        float speck = K.SeamlessFbm(u, v, 96f, 1, 4.4f);
        float g = Mathf.Lerp(0.70f, 0.90f, h) * (speck > 0.6f ? 0.85f : 1f);
        return new Color(g, g * 0.98f, g * 0.95f, 1f);
    }

    private static Color GrassColor(float u, float v)
    {
        float patch = K.SeamlessFbm(u, v, 4f, 4, 2.2f);
        float fine = K.SeamlessFbm(u, v, 40f, 2, 6.6f);
        Color lush = new Color(0.30f, 0.46f, 0.20f);
        Color dry = new Color(0.52f, 0.54f, 0.28f);
        Color moss = new Color(0.20f, 0.33f, 0.16f);
        Color c = Color.Lerp(lush, dry, Mathf.SmoothStep(0.45f, 0.75f, patch));
        c = Color.Lerp(c, moss, Mathf.SmoothStep(0.5f, 0.2f, patch) * 0.6f);
        c *= 0.85f + fine * 0.3f;
        c.a = 1f;
        return c;
    }

    private static VolumeProfile BuildVolumeProfile(string path)
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        else
        {
            foreach (VolumeComponent c in profile.components) Object.DestroyImmediate(c, true);
            profile.components.Clear();
        }

        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.8f);
        bloom.scatter.Override(0.75f);
        bloom.tint.Override(new Color(1f, 0.82f, 0.78f));

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.3f);
        color.contrast.Override(10f);
        color.saturation.Override(12f);

        WhiteBalance wb = profile.Add<WhiteBalance>(true);
        wb.temperature.Override(5f);
        wb.tint.Override(6f);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.22f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(new Color(0.15f, 0.04f, 0.08f));

        foreach (VolumeComponent c in profile.components)
        {
            c.name = c.GetType().Name;
            AssetDatabase.AddObjectToAsset(c, profile);
        }
        EditorUtility.SetDirty(profile);
        return profile;
    }
}
