#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

// One-click builder for the boss arena ("Odrealnienie").
//
// This exists because a .unity/.prefab authored as raw text outside the Editor
// can't reference brand-new scripts or imported FBX models (their GUIDs/fileIDs
// only exist after Unity imports them). Running this menu item creates and wires
// everything by type, so all references resolve correctly.
//
// Menu:  Psychedelia > Build Boss Arena
//
// It is idempotent-ish: re-running recreates the prefabs/scene and re-applies
// the player-prefab and Pustynia changes without duplicating components.
public static class BossArenaBuilder
{
    const string ArenaSceneName = "Odrealnienie";
    const string ArenaScenePath = "Assets/Scenes/Odrealnienie.unity";
    const string PustyniaScenePath = "Assets/Scenes/Pustynia.unity";

    const string BossPrefabPath = "Assets/Objects/Boss.prefab";
    const string BallPrefabPath = "Assets/Objects/ShotgunBall.prefab";

    const string HumanFbxPath = "Assets/Objects/human/source/Human.fbx";
    const string ShotgunFbxPath = "Assets/Objects/shotgun/source/shotgun.fbx";

    const string HostPlayerPath = "Assets/Objects/Player - host.prefab";
    const string ClientPlayerPath = "Assets/Objects/Player - client.prefab";

    const string NetworkPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";
    const string KaleidoShaderName = "Custom/KaleidoscopeTextureMorph";

    const string GeneratedFolder = "Assets/Materials/Generated";
    const string ArenaMaterialPath = "Assets/Materials/Generated/ArenaKaleido.mat";
    const string BallMaterialPath = "Assets/Materials/Generated/ShotgunBall.mat";
    const string ShotgunMaterialPath = "Assets/Materials/Generated/ShotgunMat.mat";

    // Boss placement tuning.
    const float BossScale = 4f;        // currently imported ~3x too small; bump it up
    const float BossStandPitch = -90f; // stand a face-down import upright; flip to +90 if it ends up on its back
    const float BossYaw = 180f;        // face the players (boss at +Z, players at -Z)

    [MenuItem("Psychedelia/Build Boss Arena")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        try
        {
            EnsureFolders();

            Material arenaMat = CreateArenaMaterial();
            Material ballMat = CreateBallMaterial();
            Material shotgunMat = CreateShotgunMaterial();

            GameObject ballPrefab = BuildBallPrefab(ballMat);
            RegisterNetworkPrefab(ballPrefab);

            GameObject bossPrefab = BuildBossPrefab(arenaMat);

            UpdatePlayerPrefab(HostPlayerPath, ballPrefab, shotgunMat);
            UpdatePlayerPrefab(ClientPlayerPath, ballPrefab, shotgunMat);

            BuildArenaScene(bossPrefab, arenaMat);
            UpdatePustyniaScene();

            AddScenesToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Psychedelia: Boss arena zbudowana. Otwórz scenę '" + ArenaSceneName +
                      "', a w 'Pustynia' host wciska P, aby przenieść graczy do bossa.");
            EditorUtility.DisplayDialog("Build Boss Arena",
                "Gotowe!\n\n- Scena: " + ArenaScenePath +
                "\n- Boss: " + BossPrefabPath +
                "\n- Pocisk: " + BallPrefabPath +
                "\n\nW 'Pustynia' host wciska P, aby rozpocząć walkę.\n" +
                "Dostrój pozę modelu shotguna i skalę bossa w razie potrzeby.", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError("BossArenaBuilder: błąd budowania areny: " + e);
            EditorUtility.DisplayDialog("Build Boss Arena", "Błąd: " + e.Message, "OK");
        }
    }

    // ------------------------------------------------------------------
    // Assets / materials
    // ------------------------------------------------------------------

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        if (!AssetDatabase.IsValidFolder(GeneratedFolder))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "Generated");
        }
    }

    static Material CreateArenaMaterial()
    {
        Shader shader = Shader.Find(KaleidoShaderName);
        if (shader == null)
        {
            Debug.LogWarning("BossArenaBuilder: nie znaleziono shadera " + KaleidoShaderName + ", używam URP/Lit.");
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(ArenaMaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, ArenaMaterialPath);
        }
        else
        {
            mat.shader = shader;
        }

        AssignKaleidoChannels(mat);

        // Kaleidoscope shader sliders, tuned for an overwhelming arena look.
        SetFloatSafe(mat, "_AnimationSpeed", 1.8f);
        SetFloatSafe(mat, "_Scale", 2.5f);
        SetFloatSafe(mat, "_FractalNormalInfluence", 1f);
        SetFloatSafe(mat, "_FractalOpacity", 0.7f);
        SetFloatSafe(mat, "_BlendMode", 2f); // Lerp
        SetFloatSafe(mat, "_WaveTexEnable", 1f);
        SetFloatSafe(mat, "_WaveNormEnable", 1f);
        SetFloatSafe(mat, "_WaveStrength", 1.6f);
        SetFloatSafe(mat, "_WaveSpeed", 1.3f);
        SetFloatSafe(mat, "_WaveFreqMin", 5f);
        SetFloatSafe(mat, "_WaveFreqMax", 18f);
        SetFloatSafe(mat, "_WaveAmpMin", 0.01f);
        SetFloatSafe(mat, "_WaveAmpMax", 0.05f);
        SetFloatSafe(mat, "_ShadowWaveStrength", 0.12f);
        SetFloatSafe(mat, "_ShadowWaveSpeed", 1f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void SetFloatSafe(Material mat, string property, float value)
    {
        if (mat != null && mat.HasProperty(property))
        {
            mat.SetFloat(property, value);
        }
    }

    static Material CreateBallMaterial()
    {
        Shader shader = Shader.Find(KaleidoShaderName);
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(BallMaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, BallMaterialPath);
        }
        else
        {
            mat.shader = shader;
        }

        AssignKaleidoChannels(mat);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 0.5f, 0.95f, 1f));
        if (mat.HasProperty("_FractalOpacity")) mat.SetFloat("_FractalOpacity", 0.9f);
        if (mat.HasProperty("_AnimationSpeed")) mat.SetFloat("_AnimationSpeed", 3f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateShotgunMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(ShotgunMaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, ShotgunMaterialPath);
        }
        else
        {
            mat.shader = shader;
        }

        const string texDir = "Assets/Objects/shotgun/textures";
        Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/shotgun_l_MainMat.006_BaseColor.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/shotgun_l_MainMat.006_Normal.png");
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/shotgun_l_MainMat.006_Metallic.png");
        Texture2D occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/shotgun_l_MainMat.006_AO.png");

        if (baseColor != null && mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", baseColor);
        }
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", Color.white);
        }

        if (normal != null)
        {
            MarkAsNormalMap(normal);
            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
        }

        if (metallic != null && mat.HasProperty("_MetallicGlossMap"))
        {
            mat.SetTexture("_MetallicGlossMap", metallic);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (occlusion != null && mat.HasProperty("_OcclusionMap"))
        {
            mat.SetTexture("_OcclusionMap", occlusion);
            mat.EnableKeyword("_OCCLUSIONMAP");
        }

        SetFloatSafe(mat, "_Smoothness", 0.4f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void MarkAsNormalMap(Texture2D normal)
    {
        string path = AssetDatabase.GetAssetPath(normal);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }

    static void AssignKaleidoChannels(Material mat)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Shaders/PsychedelicTextures" });
        List<Texture2D> textures = new List<Texture2D>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                textures.Add(tex);
            }
        }

        if (textures.Count == 0)
        {
            return;
        }

        string[] channels = { "_Channel0", "_Channel1", "_Channel2", "_Channel3" };
        for (int i = 0; i < channels.Length; i++)
        {
            if (mat.HasProperty(channels[i]))
            {
                mat.SetTexture(channels[i], textures[i % textures.Count]);
            }
        }

        if (mat.HasProperty("_MainTex"))
        {
            mat.SetTexture("_MainTex", textures[0]);
        }
    }

    // ------------------------------------------------------------------
    // Prefabs
    // ------------------------------------------------------------------

    static GameObject BuildBallPrefab(Material ballMat)
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        temp.name = "ShotgunBall";
        temp.transform.localScale = Vector3.one * 0.8f;

        // We resolve hits with a manual SphereCast, so drop the physics collider.
        Collider col = temp.GetComponent<Collider>();
        if (col != null)
        {
            Object.DestroyImmediate(col);
        }

        Renderer rend = temp.GetComponent<Renderer>();
        if (rend != null && ballMat != null)
        {
            rend.sharedMaterial = ballMat;
        }

        // Trail so the fast projectile reads as a clear streak.
        TrailRenderer trail = temp.AddComponent<TrailRenderer>();
        trail.time = 0.25f;
        trail.startWidth = 0.7f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.05f;
        trail.autodestruct = false;
        if (ballMat != null)
        {
            trail.sharedMaterial = ballMat;
        }

        // No PsychedeliaNetworkTransform: the ball moves deterministically on
        // every client (ShotgunBall), so it doesn't need transform replication.
        // The initial spawn position is still synchronized by the NetworkObject.
        temp.AddComponent<NetworkObject>();
        temp.AddComponent<ShotgunBall>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, BallPrefabPath);
        Object.DestroyImmediate(temp);
        return prefab;
    }

    static GameObject BuildBossPrefab(Material bossMat)
    {
        GameObject humanFbx = AssetDatabase.LoadAssetAtPath<GameObject>(HumanFbxPath);
        GameObject root;

        if (humanFbx != null)
        {
            root = (GameObject)PrefabUtility.InstantiatePrefab(humanFbx);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        else
        {
            Debug.LogWarning("BossArenaBuilder: brak " + HumanFbxPath + " — tworzę zastępczego bossa (kapsuła).");
            root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.transform.localScale = new Vector3(2f, 3f, 2f);
        }

        root.name = "Boss";
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;

        ApplyMaterialToRenderers(root, bossMat);

        // Auto-fit a box collider to the model so projectiles can hit it.
        AddFittedBoxCollider(root);

        root.AddComponent<NetworkObject>();
        BossController boss = root.AddComponent<BossController>();

        // Muzzle near the top of the model for the laser origin.
        Bounds bounds = GetHierarchyBounds(root);
        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        boss.muzzle = muzzle.transform;

        root.AddComponent<BossIdleMotion>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void UpdatePlayerPrefab(string playerPath, GameObject ballPrefab, Material shotgunMat)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(playerPath);
        if (root == null)
        {
            Debug.LogWarning("BossArenaBuilder: nie można otworzyć " + playerPath);
            return;
        }

        try
        {
            Transform camTransform = FindChildByName(root.transform, "Main Camera");
            Camera cam = camTransform != null ? camTransform.GetComponent<Camera>() : root.GetComponentInChildren<Camera>(true);
            if (camTransform == null && cam != null)
            {
                camTransform = cam.transform;
            }

            PlayerDownState down = root.GetComponent<PlayerDownState>();
            if (down == null)
            {
                down = root.AddComponent<PlayerDownState>();
            }

            ShotgunWeapon weapon = root.GetComponent<ShotgunWeapon>();
            if (weapon == null)
            {
                weapon = root.AddComponent<ShotgunWeapon>();
            }

            // (Re)build the shotgun viewmodel under the camera.
            if (camTransform != null)
            {
                Transform existingModel = FindChildByName(camTransform, "ShotgunModel");
                if (existingModel != null)
                {
                    Object.DestroyImmediate(existingModel.gameObject);
                }

                GameObject shotgunFbx = AssetDatabase.LoadAssetAtPath<GameObject>(ShotgunFbxPath);
                GameObject model;
                if (shotgunFbx != null)
                {
                    model = (GameObject)Object.Instantiate(shotgunFbx);
                }
                else
                {
                    Debug.LogWarning("BossArenaBuilder: brak " + ShotgunFbxPath + " — używam zastępczego modelu.");
                    model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                }

                model.name = "ShotgunModel";
                model.transform.SetParent(camTransform, false);
                // Barrel points forward (camera +Z). Was 180 which aimed it back
                // at the holder. Flip to (0,180,0) if your FBX faces the other way.
                model.transform.localRotation = Quaternion.identity;

                // Give the gun a proper textured material (the raw FBX import can
                // be material-less and render invisible/magenta).
                if (shotgunMat != null)
                {
                    ApplyMaterialToRenderers(model, shotgunMat);
                }

                // Normalize to a sane viewmodel size regardless of FBX import scale
                // (a model authored in cm can otherwise engulf the camera and look
                // "invisible"). Then place it as a first-person viewmodel.
                NormalizeViewmodelSize(model, 0.45f);
                model.transform.localPosition = new Vector3(0.22f, -0.2f, 0.4f);
                StripColliders(model);
                model.SetActive(false);

                weapon.shotgunModel = model;
                weapon.muzzle = model.transform;
                weapon.playerCamera = cam;
            }

            weapon.projectilePrefab = ballPrefab;

            PrefabUtility.SaveAsPrefabAsset(root, playerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------------------------------
    // Scenes
    // ------------------------------------------------------------------

    static void BuildArenaScene(GameObject bossPrefab, Material arenaMat)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Lighting / mood (saved with the scene) ---
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.18f, 0.02f, 0.28f);
        RenderSettings.fogDensity = 0.015f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.35f, 0.2f, 0.5f);

        // --- Directional key light ---
        GameObject sun = new GameObject("Directional Light");
        Light sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.color = new Color(1f, 0.7f, 0.95f);
        sunLight.intensity = 1.1f;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // --- Floor ---
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(60f, 1f, 60f);
        floor.transform.position = new Vector3(0f, -0.5f, 0f);
        ApplyMaterialToRenderers(floor, arenaMat);

        // --- Ring of pillars + animated colored lights for the overwhelming look ---
        GameObject decorRoot = new GameObject("ArenaDecor");
        int pillars = 12;
        float radius = 22f;
        for (int i = 0; i < pillars; i++)
        {
            float angle = (i / (float)pillars) * Mathf.PI * 2f;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 6f, Mathf.Sin(angle) * radius);

            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Pillar_" + i;
            pillar.transform.SetParent(decorRoot.transform, false);
            pillar.transform.position = pos;
            pillar.transform.localScale = new Vector3(2f, 14f, 2f);
            ApplyMaterialToRenderers(pillar, arenaMat);

            GameObject lightGo = new GameObject("PillarLight_" + i);
            lightGo.transform.SetParent(decorRoot.transform, false);
            lightGo.transform.position = pos + Vector3.up * 4f;
            Light pl = lightGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.range = 28f;
            pl.intensity = 2.4f;
            pl.color = Color.HSVToRGB(i / (float)pillars, 0.85f, 1f);
        }

        // --- Boss spawn + in-scene boss instance ---
        Vector3 bossPos = new Vector3(0f, 0f, 14f);
        GameObject boss = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab);
        boss.transform.localScale = boss.transform.localScale * BossScale;
        boss.transform.rotation = Quaternion.Euler(BossStandPitch, BossYaw, 0f); // stand upright, face the players
        boss.transform.position = bossPos;

        // Drop the boss so its feet rest on the floor regardless of model pivot/scale.
        Bounds bossBounds = GetHierarchyBounds(boss);
        boss.transform.position += new Vector3(0f, bossPos.y - bossBounds.min.y, 0f);

        BossController bossController = boss.GetComponent<BossController>();

        // --- Player spawn points (facing the boss) ---
        GameObject spawnRoot = new GameObject("SpawnPoints");
        Transform[] spawns = new Transform[2];
        Vector3[] spawnPositions =
        {
            new Vector3(-2.5f, 1f, -10f),
            new Vector3(2.5f, 1f, -10f)
        };
        for (int i = 0; i < spawnPositions.Length; i++)
        {
            GameObject sp = new GameObject("PlayerSpawn_" + i);
            sp.transform.SetParent(spawnRoot.transform, false);
            sp.transform.position = spawnPositions[i];
            Vector3 toBoss = bossPos - spawnPositions[i];
            toBoss.y = 0f;
            sp.transform.rotation = Quaternion.LookRotation(toBoss.normalized, Vector3.up);
            sp.AddComponent<NetworkSpawnPoint>();
            spawns[i] = sp.transform;
        }

        // --- Arena controller ---
        GameObject controllerGo = new GameObject("BossArenaController");
        controllerGo.AddComponent<NetworkObject>();
        BossArenaController controller = controllerGo.AddComponent<BossArenaController>();
        controller.arenaSceneName = ArenaSceneName;
        controller.boss = bossController;
        controller.bossLookTarget = boss.transform;
        controller.playerSpawnPoints = spawns;

        // --- HUD ---
        GameObject hudGo = new GameObject("BossArenaHud");
        hudGo.AddComponent<BossArenaHud>();

        // --- Psychedelic particles ---
        AddArenaParticles();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ArenaScenePath);
    }

    static void AddArenaParticles()
    {
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null)
        {
            particleShader = Shader.Find("Sprites/Default");
        }

        const string particleMatPath = "Assets/Materials/Generated/ArenaParticles.mat";
        Material particleMat = AssetDatabase.LoadAssetAtPath<Material>(particleMatPath);
        if (particleMat == null && particleShader != null)
        {
            particleMat = new Material(particleShader);
            AssetDatabase.CreateAsset(particleMat, particleMatPath);
        }

        GameObject go = new GameObject("PsychedelicParticles");
        go.transform.position = new Vector3(0f, 9f, 2f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop();

        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = 9f;
        main.startSpeed = 0.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 1.4f);
        main.maxParticles = 2000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.3f, 0.9f), new Color(0.3f, 0.9f, 1f))
        {
            mode = ParticleSystemGradientMode.TwoColors
        };
        main.gravityModifier = -0.02f;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 120f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(54f, 18f, 54f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.2f, 0.8f), 0f),
                new GradientColorKey(new Color(0.4f, 1f, 0.7f), 0.5f),
                new GradientColorKey(new Color(0.5f, 0.6f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.8f, 0.2f),
                new GradientAlphaKey(0.8f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        if (particleMat != null)
        {
            renderer.sharedMaterial = particleMat;
        }

        ParticleSystem.MainModule mainPlay = ps.main;
        mainPlay.playOnAwake = true;
        ps.Play();
    }

    static void UpdatePustyniaScene()
    {
        if (!File.Exists(PustyniaScenePath))
        {
            Debug.LogWarning("BossArenaBuilder: brak sceny " + PustyniaScenePath + " — pomijam dodanie teleportu.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(PustyniaScenePath, OpenSceneMode.Single);

        BossArenaTeleport existing = Object.FindFirstObjectByType<BossArenaTeleport>(FindObjectsInactive.Include);
        if (existing == null)
        {
            GameObject go = new GameObject("BossArenaTeleport");
            BossArenaTeleport teleport = go.AddComponent<BossArenaTeleport>();
            teleport.arenaSceneName = ArenaSceneName;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    static void AddScenesToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        AddSceneIfMissing(scenes, PustyniaScenePath);
        AddSceneIfMissing(scenes, ArenaScenePath);

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void AddSceneIfMissing(List<EditorBuildSettingsScene> scenes, string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (EditorBuildSettingsScene s in scenes)
        {
            if (s.path == path)
            {
                s.enabled = true;
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(path, true));
    }

    // ------------------------------------------------------------------
    // Network prefab registration
    // ------------------------------------------------------------------

    static void RegisterNetworkPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return;
        }

        NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsListPath);
        if (list == null)
        {
            Debug.LogWarning("BossArenaBuilder: brak " + NetworkPrefabsListPath + " — zarejestruj pocisk ręcznie.");
            return;
        }

        foreach (NetworkPrefab existing in list.PrefabList)
        {
            if (existing != null && existing.Prefab == prefab)
            {
                return;
            }
        }

        list.Add(new NetworkPrefab { Prefab = prefab });
        EditorUtility.SetDirty(list);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    static void ApplyMaterialToRenderers(GameObject root, Material mat)
    {
        if (mat == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = mat;
            }
            r.sharedMaterials = mats;
        }
    }

    static void NormalizeViewmodelSize(GameObject model, float targetSize)
    {
        model.transform.localScale = Vector3.one;
        Bounds bounds = GetHierarchyBounds(model);
        float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxDim > 0.0001f)
        {
            float factor = targetSize / maxDim;
            model.transform.localScale = Vector3.one * factor;
        }
    }

    static void StripColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (Collider c in colliders)
        {
            Object.DestroyImmediate(c);
        }
    }

    static void AddFittedBoxCollider(GameObject root)
    {
        Bounds bounds = GetHierarchyBounds(root);
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = root.transform.InverseTransformPoint(bounds.center);
        box.size = bounds.size;
    }

    static Bounds GetHierarchyBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position + Vector3.up, new Vector3(1f, 2f, 1f));
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    static Transform FindChildByName(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildByName(parent.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
#endif
