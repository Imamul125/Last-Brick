using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using K = EnvironmentBuildKit;

/// <summary>
/// Environment 01 - "Golden Marble Sanctum".
/// Golden-hour sunset, polished Calacatta marble floor with brass inlays, emerald medallion under the tower,
/// sunken courtyard ringed by a fluted marble colonnade with glowing orb braziers and drifting gold motes.
/// Menu: Tools > Last Brick > Environments > Build 01 - Golden Marble Sanctum
/// </summary>
public static class GoldenMarbleSanctumBuilder
{
    private const string EnvName = "01_GoldenMarbleSanctum";
    private const string Folder = K.RootFolder + "/" + EnvName;

    // Layout (world units). The tower stands at the origin, gameplay camera orbits at radius ~19-30.
    private const float FloorRadius = 28f;       // Sunken courtyard floor
    private const float StepOuter = 31f;          // First step ring
    private const float TerraceOuter = 150f;      // Upper terrace (fades into fog)
    private const float StepHeight = 0.4f;
    private const float ColonnadeRadius = 38f;
    private const int ColumnCount = 12;
    private const float ColumnHeight = 14f;
    private const float FloorTileWorldSize = 5f; // One floor texture (2x2 tiles) covers 5 x 5 units

    private static readonly K.MarbleStyle Calacatta = new K.MarbleStyle
    {
        baseA = new Color(0.96f, 0.94f, 0.90f), baseB = new Color(0.88f, 0.85f, 0.80f),
        vein = new Color(0.56f, 0.47f, 0.35f), fineVein = new Color(0.74f, 0.70f, 0.64f),
        veinSharpness = 14f, fineVeinSharpness = 40f, fineVeinStrength = 0.6f, turbulence = 6f,
        veinFreqU = 1, veinFreqV = 1
    };

    private static readonly K.MarbleStyle Emerald = new K.MarbleStyle
    {
        baseA = new Color(0.03f, 0.22f, 0.15f), baseB = new Color(0.01f, 0.08f, 0.06f),
        vein = new Color(0.78f, 0.88f, 0.80f), fineVein = new Color(0.22f, 0.48f, 0.38f),
        veinSharpness = 18f, fineVeinSharpness = 30f, fineVeinStrength = 0.8f, turbulence = 7f,
        veinFreqU = 2, veinFreqV = 1
    };

    private static readonly K.MarbleStyle Statuario = new K.MarbleStyle
    {
        baseA = new Color(0.97f, 0.96f, 0.94f), baseB = new Color(0.90f, 0.89f, 0.87f),
        vein = new Color(0.62f, 0.62f, 0.64f), fineVein = new Color(0.80f, 0.80f, 0.81f),
        veinSharpness = 16f, fineVeinSharpness = 45f, fineVeinStrength = 0.5f, turbulence = 5f,
        veinFreqU = 1, veinFreqV = 2
    };

    [MenuItem("Tools/Last Brick/Environments/Build 01 - Golden Marble Sanctum")]
    public static void Build()
    {
        try
        {
            float groundY = K.FindGroundHeight();

            K.EnsureFolder(Folder + "/Textures");
            K.EnsureFolder(Folder + "/Materials");
            K.EnsureFolder(Folder + "/Meshes");

            // ---------------- Textures
            EditorUtility.DisplayProgressBar("Golden Marble Sanctum", "Generating marble floor textures...", 0.1f);
            BuildFloorTextures(out Texture2D floorAlbedo, out Texture2D floorNormal, out Texture2D floorMask);

            EditorUtility.DisplayProgressBar("Golden Marble Sanctum", "Generating emerald marble...", 0.35f);
            Texture2D emeraldTex = BuildSeamlessMarble(Emerald, 512, 3.1f, Folder + "/Textures/T_EmeraldMarble.png");

            EditorUtility.DisplayProgressBar("Golden Marble Sanctum", "Generating statuario marble...", 0.5f);
            Texture2D statuarioTex = BuildSeamlessMarble(Statuario, 512, 7.7f, Folder + "/Textures/T_StatuarioMarble.png");

            Texture2D dotTex = K.SaveSoftDotTexture(Folder + "/Textures/T_SoftDot.png");

            // ---------------- Materials
            EditorUtility.DisplayProgressBar("Golden Marble Sanctum", "Creating materials...", 0.6f);
            string m = Folder + "/Materials/";
            Material floorMat = K.LitMaterial(m + "M_MarbleFloor.mat", Color.white, floorAlbedo, floorNormal, 1f, floorMask, 1f, 1f);
            Material emeraldMat = K.LitMaterial(m + "M_EmeraldMarble.mat", Color.white, emeraldTex, null, 1f, null, 0f, 0.93f);
            Material statuarioMat = K.LitMaterial(m + "M_StatuarioMarble.mat", Color.white, statuarioTex, null, 1f, null, 0f, 0.8f);
            Material goldMat = K.LitMaterial(m + "M_PolishedGold.mat", new Color(1f, 0.76f, 0.36f), null, null, 1f, null, 1f, 0.85f);
            Material orbMat = K.LitMaterial(m + "M_GlowOrb.mat", new Color(0.1f, 0.05f, 0.02f), null, null, 1f, null, 0f, 0.9f,
                new Color(1f, 0.5f, 0.16f) * 6f);
            Material motesMat = K.AdditiveParticleMaterial(m + "M_GoldMotes.mat", dotTex);

            Material sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunDisk", 2f); // High quality
            sky.DisableKeyword("_SUNDISK_NONE");
            sky.DisableKeyword("_SUNDISK_SIMPLE");
            sky.EnableKeyword("_SUNDISK_HIGH_QUALITY");
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 4f);
            sky.SetFloat("_AtmosphereThickness", 1.35f);
            sky.SetColor("_SkyTint", new Color(0.62f, 0.55f, 0.58f));
            sky.SetColor("_GroundColor", new Color(0.42f, 0.34f, 0.28f));
            sky.SetFloat("_Exposure", 1.2f);
            sky = K.SaveAsset(sky, m + "M_Sky_GoldenHour.mat");

            // ---------------- Meshes
            EditorUtility.DisplayProgressBar("Golden Marble Sanctum", "Building meshes...", 0.7f);
            string f = Folder + "/Meshes";
            Mesh floorMesh = K.SaveMesh(K.Cylinder("SM_CourtyardFloor", FloorRadius, FloorRadius, 0.5f, 128, FloorTileWorldSize), f);
            Mesh stepMesh = K.SaveMesh(K.Ring("SM_Step", FloorRadius, StepOuter, StepHeight, 128, FloorTileWorldSize), f);
            Mesh terraceMesh = K.SaveMesh(K.Ring("SM_Terrace", StepOuter, TerraceOuter, StepHeight * 2f, 128, FloorTileWorldSize), f);
            Mesh medallionMesh = K.SaveMesh(K.Cylinder("SM_Medallion", 6.5f, 6.5f, 0.02f, 128, 6f), f);
            Mesh goldRingWide = K.SaveMesh(K.Ring("SM_GoldRing_Wide", 6.5f, 6.85f, 0.03f, 128, 4f), f);
            Mesh goldRingThin = K.SaveMesh(K.Ring("SM_GoldRing_Thin", 10f, 10.12f, 0.03f, 128, 4f), f);
            Mesh emeraldBand = K.SaveMesh(K.Ring("SM_EmeraldBand", 10.12f, 10.9f, 0.02f, 128, 6f), f);
            Mesh goldRingOuter = K.SaveMesh(K.Ring("SM_GoldRing_Outer", 10.9f, 11.02f, 0.03f, 128, 4f), f);
            Mesh rayLong = K.SaveMesh(K.Box("SM_SunRay_Long", new Vector3(0.09f, 0.03f, 3.15f), 4f), f);
            Mesh rayShort = K.SaveMesh(K.Box("SM_SunRay_Short", new Vector3(0.06f, 0.03f, 1.6f), 4f), f);
            Mesh nosingMesh = K.SaveMesh(K.Ring("SM_GoldNosing", FloorRadius - 0.02f, FloorRadius + 0.14f, 0.05f, 128, 4f), f);
            Mesh nosing2Mesh = K.SaveMesh(K.Ring("SM_GoldNosing_Upper", StepOuter - 0.02f, StepOuter + 0.14f, 0.05f, 128, 4f), f);

            Mesh plinth = K.SaveMesh(K.Box("SM_Column_Plinth", new Vector3(2.4f, 0.5f, 2.4f), 3f), f);
            Mesh torus = K.SaveMesh(K.Cylinder("SM_Column_Base", 1.12f, 0.96f, 0.4f, 48, 3f), f);
            Mesh baseRing = K.SaveMesh(K.Cylinder("SM_Column_GoldBand", 1.0f, 1.0f, 0.12f, 48, 3f), f);
            Mesh shaft = K.SaveMesh(K.FlutedShaft("SM_Column_Shaft", 0.88f, 0.76f, ColumnHeight - 2.15f, 20, 0.06f, 3f), f);
            Mesh capital = K.SaveMesh(K.Cylinder("SM_Column_Capital", 0.8f, 1.2f, 0.6f, 48, 3f), f);
            Mesh abacus = K.SaveMesh(K.Box("SM_Column_Abacus", new Vector3(2.5f, 0.35f, 2.5f), 3f), f);
            Mesh abacusTrim = K.SaveMesh(K.Box("SM_Column_AbacusTrim", new Vector3(2.58f, 0.08f, 2.58f), 3f), f);

            Mesh architrave = K.SaveMesh(K.Ring("SM_Architrave", ColonnadeRadius - 1.4f, ColonnadeRadius + 1.4f, 1.4f, 160, 3f, true), f);
            Mesh friezeBand = K.SaveMesh(K.Ring("SM_FriezeGold", ColonnadeRadius - 1.52f, ColonnadeRadius - 1.38f, 0.2f, 160, 3f, true), f);
            Mesh cornice = K.SaveMesh(K.Ring("SM_Cornice", ColonnadeRadius - 1.65f, ColonnadeRadius + 1.7f, 0.35f, 160, 3f, true), f);

            Mesh brazierStand = K.SaveMesh(K.Cylinder("SM_Brazier_Stand", 0.38f, 0.14f, 1.6f, 32, 2f), f);
            Mesh brazierBowl = K.SaveMesh(K.Cylinder("SM_Brazier_Bowl", 0.28f, 0.75f, 0.42f, 32, 2f, true, true), f);
            Mesh brazierFoot = K.SaveMesh(K.Cylinder("SM_Brazier_Foot", 0.6f, 0.5f, 0.15f, 32, 2f), f);

            // ---------------- Prefab layout
            EditorUtility.DisplayProgressBar("Golden Marble Sanctum", "Assembling prefab...", 0.85f);
            GameObject root = new GameObject("ENV_" + EnvName);
            Transform t = root.transform;

            Transform ground = new GameObject("Ground").transform;
            ground.SetParent(t, false);
            K.Part("CourtyardFloor", ground, floorMesh, floorMat, new Vector3(0f, groundY - 0.5f, 0f));
            K.Part("Step", ground, stepMesh, floorMat, new Vector3(0f, groundY, 0f));
            K.Part("Terrace", ground, terraceMesh, floorMat, new Vector3(0f, groundY, 0f));
            K.Part("GoldNosing", ground, nosingMesh, goldMat, new Vector3(0f, groundY + StepHeight - 0.03f, 0f), null, false);
            K.Part("GoldNosingUpper", ground, nosing2Mesh, goldMat, new Vector3(0f, groundY + StepHeight * 2f - 0.03f, 0f), null, false);

            Transform medallion = new GameObject("Medallion").transform;
            medallion.SetParent(t, false);
            medallion.localPosition = new Vector3(0f, groundY, 0f);
            K.Part("EmeraldMedallion", medallion, medallionMesh, emeraldMat, new Vector3(0f, 0.005f, 0f), null, false);
            K.Part("GoldRing_Wide", medallion, goldRingWide, goldMat, Vector3.zero, null, false);
            K.Part("GoldRing_Thin", medallion, goldRingThin, goldMat, Vector3.zero, null, false);
            K.Part("EmeraldBand", medallion, emeraldBand, emeraldMat, new Vector3(0f, 0.005f, 0f), null, false);
            K.Part("GoldRing_Outer", medallion, goldRingOuter, goldMat, Vector3.zero, null, false);
            for (int i = 0; i < 16; i++)
            {
                bool isLong = i % 2 == 0;
                float angle = i * 22.5f;
                float len = isLong ? 3.15f : 1.6f;
                float centerR = 6.85f + len * 0.5f;
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                K.Part("SunRay_" + i, medallion, isLong ? rayLong : rayShort, goldMat,
                    rot * new Vector3(0f, 0.015f, centerR), rot, false);
            }

            Transform colonnade = new GameObject("Colonnade").transform;
            colonnade.SetParent(t, false);
            colonnade.localPosition = new Vector3(0f, groundY + StepHeight * 2f, 0f);
            for (int i = 0; i < ColumnCount; i++)
            {
                float angle = i * 360f / ColumnCount;
                Quaternion rot = Quaternion.Euler(0f, -angle, 0f);
                Transform col = new GameObject("Column_" + i).transform;
                col.SetParent(colonnade, false);
                col.localPosition = rot * new Vector3(ColonnadeRadius, 0f, 0f);
                col.localRotation = rot;

                float y = 0f;
                K.Part("Plinth", col, plinth, statuarioMat, new Vector3(0f, y + 0.25f, 0f)); y += 0.5f;
                K.Part("Base", col, torus, statuarioMat, new Vector3(0f, y, 0f)); y += 0.4f;
                K.Part("GoldBand", col, baseRing, goldMat, new Vector3(0f, y - 0.06f, 0f));
                K.Part("Shaft", col, shaft, statuarioMat, new Vector3(0f, y, 0f)); y += ColumnHeight - 2.15f;
                K.Part("Capital", col, capital, goldMat, new Vector3(0f, y, 0f)); y += 0.6f;
                K.Part("AbacusTrim", col, abacusTrim, goldMat, new Vector3(0f, y + 0.04f, 0f));
                K.Part("Abacus", col, abacus, statuarioMat, new Vector3(0f, y + 0.08f + 0.175f, 0f));
            }
            float entablatureY = ColumnHeight - 0.22f; // top of the abacus
            K.Part("Architrave", colonnade, architrave, statuarioMat, new Vector3(0f, entablatureY, 0f));
            K.Part("FriezeGold", colonnade, friezeBand, goldMat, new Vector3(0f, entablatureY + 0.6f, 0f));
            K.Part("Cornice", colonnade, cornice, statuarioMat, new Vector3(0f, entablatureY + 1.4f, 0f));

            Transform braziers = new GameObject("Braziers").transform;
            braziers.SetParent(t, false);
            braziers.localPosition = new Vector3(0f, groundY + StepHeight * 2f, 0f);
            for (int i = 0; i < ColumnCount; i++)
            {
                float angle = (i + 0.5f) * 360f / ColumnCount;
                Quaternion rot = Quaternion.Euler(0f, -angle, 0f);
                Transform b = new GameObject("Brazier_" + i).transform;
                b.SetParent(braziers, false);
                b.localPosition = rot * new Vector3(ColonnadeRadius - 3f, 0f, 0f);

                K.Part("Foot", b, brazierFoot, goldMat, Vector3.zero);
                K.Part("Stand", b, brazierStand, goldMat, new Vector3(0f, 0.15f, 0f));
                K.Part("Bowl", b, brazierBowl, goldMat, new Vector3(0f, 1.72f, 0f));

                GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(orb.GetComponent<Collider>());
                orb.name = "GlowOrb";
                orb.transform.SetParent(b, false);
                orb.transform.localPosition = new Vector3(0f, 2.45f, 0f);
                orb.transform.localScale = Vector3.one * 0.75f;
                MeshRenderer orbRenderer = orb.GetComponent<MeshRenderer>();
                orbRenderer.sharedMaterial = orbMat;
                orbRenderer.shadowCastingMode = ShadowCastingMode.Off;

                // Real lights on every third brazier only - keeps mobile per-object light count low.
                if (i % 3 == 1)
                {
                    Light pl = new GameObject("OrbLight").AddComponent<Light>();
                    pl.transform.SetParent(b, false);
                    pl.transform.localPosition = new Vector3(0f, 2.6f, 0f);
                    pl.type = LightType.Point;
                    pl.color = new Color(1f, 0.6f, 0.3f);
                    pl.intensity = 8f;
                    pl.range = 16f;
                    pl.shadows = LightShadows.None;
                }
            }

            // Sun - low golden-hour angle for long soft shadows.
            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(t, false);
            sun.transform.rotation = Quaternion.Euler(26f, -38f, 0f);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.8f, 0.6f);
            sun.intensity = 1.9f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;

            // Reflection probe rendered once by EnvironmentManager -> mirror-like marble and gold.
            ReflectionProbe probe = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(t, false);
            probe.transform.localPosition = new Vector3(0f, groundY + 4f, 0f);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(320f, 100f, 320f);
            probe.resolution = 256;
            probe.hdr = true;
            probe.boxProjection = false;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.cullingMask = ~(1 << 5); // Everything except UI
            probe.importance = 1;

            BuildGoldMotes(t, groundY, motesMat);

            SetStaticRecursive(root);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/ENV_" + EnvName + ".prefab");
            Object.DestroyImmediate(root);

            // ---------------- Post processing
            EditorUtility.DisplayProgressBar("Golden Marble Sanctum", "Creating post-processing profile...", 0.92f);
            VolumeProfile volume = BuildVolumeProfile(Folder + "/PP_" + EnvName + ".asset");

            // ---------------- Environment profile
            string profilePath = Folder + "/EnvProfile_" + EnvName + ".asset";
            EnvironmentProfile profile = AssetDatabase.LoadAssetAtPath<EnvironmentProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<EnvironmentProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.displayName = "Golden Marble Sanctum";
            profile.environmentPrefab = prefab;
            profile.skybox = sky;
            profile.ambientMode = AmbientMode.Trilight;
            profile.ambientSkyColor = new Color(0.62f, 0.64f, 0.78f);
            profile.ambientEquatorColor = new Color(0.66f, 0.54f, 0.44f);
            profile.ambientGroundColor = new Color(0.26f, 0.2f, 0.15f);
            profile.reflectionIntensity = 1f;
            profile.fog = true;
            profile.fogMode = FogMode.ExponentialSquared;
            profile.fogColor = new Color(0.87f, 0.72f, 0.58f);
            profile.fogDensity = 0.0105f;
            profile.postProcessProfile = volume;
            EditorUtility.SetDirty(profile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(profile);
            Selection.activeObject = profile;
            Debug.Log($"[Environments] Built '{profile.displayName}' at {Folder} (floor height {groundY}). " +
                      "Run Tools > Last Brick > Environments > Setup Environment Manager In Scene to hook it up.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void BuildFloorTextures(out Texture2D albedo, out Texture2D normal, out Texture2D mask)
    {
        // 2x2 marble tiles per texture, separated by thin polished brass inlay strips.
        const int size = 1024;
        const int tilePx = size / 2;
        const float inlay = 3.5f;   // half-width of the brass strip in pixels (strip sits on the texture edges, so tiling is seamless)
        const float bevel = 4f;

        Color brass = new Color(0.86f, 0.66f, 0.36f);
        Color[] albedoPx = new Color[size * size];
        Color[] maskPx = new Color[size * size];
        float[] height = new float[size * size];

        for (int y = 0; y < size; y++)
        {
            int ty = y / tilePx;
            float ly = y % tilePx + 0.5f;
            for (int x = 0; x < size; x++)
            {
                int tx = x / tilePx;
                float lx = x % tilePx + 0.5f;
                float edge = Mathf.Min(Mathf.Min(lx, tilePx - lx), Mathf.Min(ly, tilePx - ly));
                int i = y * size + x;

                if (edge < inlay)
                {
                    float grain = K.Fbm(x * 0.02f, y * 0.6f, 2) * 0.12f;
                    albedoPx[i] = brass * (0.94f + grain);
                    albedoPx[i].a = 1f;
                    maskPx[i] = new Color(1f, 0f, 0f, 0.78f);
                    height[i] = 0.75f;
                    continue;
                }

                // Each tile gets its own slab of marble; alternate the vein direction like book-matched slabs.
                K.MarbleStyle style = Calacatta;
                if ((tx + ty) % 2 == 1) { style.veinFreqU = 1; style.veinFreqV = -1; }
                float seed = 11.3f + tx * 37.1f + ty * 91.7f;
                Color c = K.SampleMarble(lx / tilePx, ly / tilePx, style, seed, false, out float veinMask);

                float b = Mathf.Clamp01((edge - inlay) / bevel);
                albedoPx[i] = c;
                maskPx[i] = new Color(0f, 0f, 0f, Mathf.Lerp(0.95f, 0.85f, veinMask) * Mathf.Lerp(0.85f, 1f, b));
                height[i] = Mathf.Lerp(0.75f, 1f, Mathf.SmoothStep(0f, 1f, b)) - veinMask * 0.01f;
            }
        }

        string tex = Folder + "/Textures/";
        albedo = K.SaveTexture(albedoPx, size, tex + "T_MarbleFloor_Albedo.png", false, true);
        normal = K.SaveTexture(K.HeightToNormal(height, size, 6f), size, tex + "T_MarbleFloor_Normal.png", true, false);
        mask = K.SaveTexture(maskPx, size, tex + "T_MarbleFloor_MetalSmooth.png", false, false);
    }

    private static Texture2D BuildSeamlessMarble(K.MarbleStyle style, int size, float seed, string path)
    {
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                px[y * size + x] = K.SampleMarble((x + 0.5f) / size, (y + 0.5f) / size, style, seed, true, out _);
            }
        }
        return K.SaveTexture(px, size, path, false, true, 512);
    }

    private static void BuildGoldMotes(Transform parent, float groundY, Material mat)
    {
        GameObject go = new GameObject("GoldMotes");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, groundY + 6f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 13f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.45f, 0.9f), new Color(1f, 0.62f, 0.25f, 0.7f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        main.gravityModifier = -0.004f;

        var emission = ps.emission;
        emission.rateOverTime = 26f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(70f, 12f, 70f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.1f;

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = mat;
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;
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

        Tonemapping tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.72f);
        bloom.tint.Override(new Color(1f, 0.88f, 0.72f));
        bloom.highQualityFiltering.Override(false);

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.25f);
        color.contrast.Override(14f);
        color.saturation.Override(8f);

        WhiteBalance wb = profile.Add<WhiteBalance>(true);
        wb.temperature.Override(8f);
        wb.tint.Override(2f);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.24f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(new Color(0.12f, 0.06f, 0.02f));

        foreach (VolumeComponent c in profile.components)
        {
            c.name = c.GetType().Name;
            AssetDatabase.AddObjectToAsset(c, profile);
        }
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void SetStaticRecursive(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
        foreach (Transform child in go.transform) SetStaticRecursive(child.gameObject);
    }
}
