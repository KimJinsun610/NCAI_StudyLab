#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace VARCO_Workshop.Editor
{
    public static class VARCOExampleSceneBuilder
    {
        const string AutoRunRequestPath = "Temp/VARCO_BUILD_EXAMPLES.request";
        const string SpaceAutoRunRequestPath = "Temp/VARCO_BUILD_SPACE_PLATFORM.request";
        const string PlayerPrefabPath = "Assets/Prefabs/Characters/ARENA_Player.prefab";
        const string EnemyPrefabPath = "Assets/Prefabs/Characters/ARENA_Boss.prefab";
        const string PlayerControllerPath = "Assets/Animations/ARENA_Player.controller";
        const string EnemyControllerPath = "Assets/Animations/ARENA_Boss.controller";
        const string SpacePlatformScenePath = "Assets/Scenes/VARCO_Platform/VARCO_Platform_Space3D.unity";
        const string PlatformPlayerModelPath = "Assets/VARCO3DImports/PLATFORM_Player_Idle/model.fbx";
        const string PlatformDroneModelPath = "Assets/VARCO3DImports/PLATFORM_Drone/model.fbx";

        static readonly string[] ScenePaths =
        {
            "Assets/Scenes/VARCO_Arena/VARCO_Arena_Example.unity",
            "Assets/Scenes/VARCO_Exploration/VARCO_Exploration_Example.unity",
            "Assets/Scenes/VARCO_Platform/VARCO_Platform_Example.unity",
            "Assets/Scenes/VARCO_Puzzle/VARCO_Puzzle_Example.unity"
        };

        [InitializeOnLoadMethod]
        static void BuildWhenRequested()
        {
            if (System.IO.File.Exists(AutoRunRequestPath))
            {
                System.IO.File.Delete(AutoRunRequestPath);
                EditorApplication.delayCall += BuildAllExamples;
            }

            if (System.IO.File.Exists(SpaceAutoRunRequestPath))
            {
                System.IO.File.Delete(SpaceAutoRunRequestPath);
                EditorApplication.delayCall += BuildSpacePlatformer;
            }
        }

        public static void BuildAllExamples()
        {
            var previousSerializationMode = EditorSettings.serializationMode;
            EditorSettings.serializationMode = SerializationMode.ForceText;

            try
            {
                BuildArena();
                BuildExploration();
                BuildPlatform();
                BuildPuzzle();
                AddScenesToBuildSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.ForceReserializeAssets(new List<string>(ScenePaths));
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorSettings.serializationMode = previousSerializationMode;
            }

            Debug.Log("[VARCO 예제] 아레나, 탐험, 플랫폼, 퍼즐 예제 씬을 생성했습니다.");
        }

        public static void BuildSpacePlatformer()
        {
            var previousSerializationMode = EditorSettings.serializationMode;
            EditorSettings.serializationMode = SerializationMode.ForceText;

            try
            {
                var scene = CreateScene("VARCO_Platform_Space3D");
                SetupCore("우주 플랫폼: 궤도 발판을 건너 도킹 게이트에 도착하세요.");
                RenderSettings.ambientLight = new Color(0.06f, 0.08f, 0.16f);
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.015f, 0.02f, 0.05f);
                RenderSettings.fogDensity = 0.012f;

                var player = CreateSpacePlatformPlayer(new Vector3(0f, 1.15f, 0f));
                SetupSpacePlatformCamera(player.transform);

                CreateStarfield();
                CreateSpaceFloor("01_Start_Airlock", new Vector3(0f, 0f, 0f), new Vector3(9f, 0.45f, 9f));
                CreateSpaceFloor("02_Launch_Pad", new Vector3(9f, 0.65f, 1f), new Vector3(5f, 0.38f, 5f));
                CreateSpaceFloor("03_Solar_Pad_A", new Vector3(16f, 1.05f, 5f), new Vector3(4.4f, 0.35f, 4.4f));
                CreateSpaceFloor("04_Solar_Pad_B", new Vector3(23f, 1.35f, 1f), new Vector3(4.2f, 0.35f, 4.2f));
                CreateMovingPlatform3D("05_Orbital_Shuttle_A", new Vector3(29f, 1.55f, 1f), new Vector3(36f, 2.15f, 7f), new Vector3(4.5f, 0.35f, 4.5f), 0.28f);
                CreateSpaceFloor("06_Checkpoint_Ring_A", new Vector3(42f, 2.15f, 7f), new Vector3(7f, 0.42f, 7f));
                CreateCheckpoint(new Vector3(42f, 2.85f, 7f));

                CreateSpaceFloor("07_Asteroid_Step_01", new Vector3(50f, 2.55f, 11f), new Vector3(4f, 0.45f, 4f));
                CreateSpaceFloor("08_Asteroid_Step_02", new Vector3(57f, 3f, 7f), new Vector3(3.8f, 0.45f, 3.8f));
                CreateSpaceFloor("09_Asteroid_Step_03", new Vector3(64f, 3.45f, 12f), new Vector3(4.2f, 0.45f, 4.2f));
                CreateSpaceFloor("10_Service_Bridge", new Vector3(73f, 3.7f, 12f), new Vector3(12f, 0.34f, 3.3f));
                CreateHazard(new Vector3(70f, 4.05f, 12f), new Vector3(1.1f, 0.2f, 3.4f), 18);
                CreateHazard(new Vector3(75.5f, 4.05f, 12f), new Vector3(1.1f, 0.2f, 3.4f), 18);
                CreateSpaceFloor("11_Elevator_Dock", new Vector3(83f, 3.95f, 9f), new Vector3(6f, 0.4f, 6f));
                CreateMovingPlatform3D("12_Gravity_Lift", new Vector3(90f, 4.15f, 9f), new Vector3(90f, 8.5f, 9f), new Vector3(5f, 0.35f, 5f), 0.22f);

                CreateSpaceFloor("13_Upper_Array", new Vector3(98f, 8.5f, 9f), new Vector3(7f, 0.4f, 7f));
                CreateCheckpoint(new Vector3(98f, 9.2f, 9f));
                CreateSpaceFloor("14_Comet_Step_A", new Vector3(106f, 9f, 14f), new Vector3(4.2f, 0.42f, 4.2f));
                CreateSpaceFloor("15_Comet_Step_B", new Vector3(114f, 9.55f, 8f), new Vector3(4.2f, 0.42f, 4.2f));
                CreateSpaceFloor("16_Comet_Step_C", new Vector3(122f, 10.1f, 14f), new Vector3(4.4f, 0.42f, 4.4f));
                CreateMovingPlatform3D("17_Orbital_Shuttle_B", new Vector3(130f, 10.35f, 14f), new Vector3(139f, 10.85f, 6f), new Vector3(4.8f, 0.35f, 4.8f), 0.24f);
                CreateSpaceFloor("18_Relay_Tower", new Vector3(147f, 10.85f, 6f), new Vector3(7f, 0.42f, 7f));
                CreateHazard(new Vector3(147f, 11.18f, 3.4f), new Vector3(5.4f, 0.2f, 0.7f), 20);

                CreateSpaceFloor("19_Final_Runway_A", new Vector3(157f, 11.2f, 8f), new Vector3(10f, 0.34f, 3.2f));
                CreateSpaceFloor("20_Final_Runway_B", new Vector3(169f, 11.6f, 11f), new Vector3(10f, 0.34f, 3.2f));
                CreateMovingPlatform3D("21_Final_Shuttle", new Vector3(178f, 11.85f, 11f), new Vector3(188f, 12.3f, 16f), new Vector3(5.2f, 0.35f, 5.2f), 0.2f);
                CreateSpaceFloor("22_Docking_Station", new Vector3(198f, 12.35f, 16f), new Vector3(12f, 0.5f, 10f));
                CreateGoal(new Vector3(202f, 14f, 16f), new Vector3(2.8f, 3.2f, 2.8f), 0, "Docking_Gate_Goal");
                CreateCheckpoint(new Vector3(196f, 13.05f, 16f));
                CreateDeathZone(new Vector3(100f, -8f, 8f), new Vector3(240f, 12f, 90f));

                CreateCollectible(new Vector3(16f, 2f, 5f), "Energy_Cell_01");
                CreateCollectible(new Vector3(57f, 3.95f, 7f), "Energy_Cell_02");
                CreateCollectible(new Vector3(114f, 10.5f, 8f), "Energy_Cell_03");
                CreateCollectible(new Vector3(169f, 12.5f, 11f), "Energy_Cell_04");
                CreateHealthPickup(new Vector3(83f, 4.75f, 9f), 30);
                CreateHealthPickup(new Vector3(147f, 11.65f, 6f), 30);

                CreateSpaceDrone(new Vector3(38f, 5.8f, 10f), new Vector3(0f, -35f, 0f), 1.4f, "Guide_Drone_A");
                CreateSpaceDrone(new Vector3(144f, 15f, 4f), new Vector3(0f, 120f, 0f), 1.7f, "Guide_Drone_B");
                CreateDockingRings(new Vector3(202f, 14f, 16f));
                CreateSceneNote(new Vector3(0f, 0.35f, -4.8f), "우주 플랫폼: WASD로 이동, Space로 점프, 체크포인트를 지나 도킹 게이트에 도착하세요.");

                SaveScene(scene, SpacePlatformScenePath);
                AddSceneToBuildSettings(SpacePlatformScenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ForceReserializeAssets(new List<string> { SpacePlatformScenePath });
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorSettings.serializationMode = previousSerializationMode;
            }

            Debug.Log("[VARCO 예제] 우주 플랫폼 씬을 생성했습니다.");
        }

        public static void BuildArena()
        {
            var scene = CreateScene("VARCO_Arena_Example");
            SetupCore("Combat Arena - defeat all waves");
            var player = CreateWorkshopPlayer(new Vector3(0f, 0.05f, -7f), "Arena_Player", true);
            SetupThirdPersonCamera(player.transform);

            CreateFloor("Arena_Ground", Vector3.zero, new Vector3(18f, 0.3f, 18f), Color.gray);
            CreateWall("Arena_Wall_N", new Vector3(0f, 1.5f, 9f), new Vector3(18f, 3f, 0.4f));
            CreateWall("Arena_Wall_S", new Vector3(0f, 1.5f, -9f), new Vector3(18f, 3f, 0.4f));
            CreateWall("Arena_Wall_E", new Vector3(9f, 1.5f, 0f), new Vector3(0.4f, 3f, 18f));
            CreateWall("Arena_Wall_W", new Vector3(-9f, 1.5f, 0f), new Vector3(0.4f, 3f, 18f));
            CreateCover("Cover_Left", new Vector3(-4.5f, 0.8f, -0.5f), new Vector3(1.4f, 1.6f, 4f));
            CreateCover("Cover_Right", new Vector3(4.4f, 0.8f, 2.4f), new Vector3(4f, 1.6f, 1.2f));
            CreateHealthPickup(new Vector3(-6.5f, 0.65f, 6f), 35);

            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            var wmGo = new GameObject("VW_WaveManager");
            var wm = wmGo.AddComponent<WaveManager>();
            wm.clearWhenAllWavesCleared = true;
            wm.delayBetweenWaves = 1f;
            wm.waves = new[]
            {
                new WaveDataVW { enemyCount = 2, spawnInterval = 0.8f, enemyPrefab = enemyPrefab },
                new WaveDataVW { enemyCount = 3, spawnInterval = 0.7f, enemyPrefab = enemyPrefab }
            };
            wm.spawnPoints = new[]
            {
                CreateMarker("Spawn_A", new Vector3(-5f, 0f, 5.5f)).transform,
                CreateMarker("Spawn_B", new Vector3(5f, 0f, 5.5f)).transform,
                CreateMarker("Spawn_C", new Vector3(0f, 0f, 2f)).transform
            };

            CreateSceneNote(new Vector3(0f, 0.05f, -8.6f), "Arena: defeat 2 waves. Use mouse/space attack.");
            BakeNavMesh();
            SaveScene(scene, ScenePaths[0]);
        }

        public static void BuildExploration()
        {
            var scene = CreateScene("VARCO_Exploration_Example");
            SetupCore("Exploration - collect 3 relics and reach the gate");
            var player = CreateWorkshopPlayer(new Vector3(0f, 0.05f, -10f), "Explorer_Player", true);
            SetupThirdPersonCamera(player.transform);

            CreateFloor("Exploration_Ground", new Vector3(0f, 0f, 0f), new Vector3(14f, 0.3f, 24f), new Color(0.34f, 0.38f, 0.42f));
            CreateWall("Left_Ruin", new Vector3(-7f, 1.3f, 0f), new Vector3(0.35f, 2.6f, 24f));
            CreateWall("Right_Ruin", new Vector3(7f, 1.3f, 0f), new Vector3(0.35f, 2.6f, 24f));
            CreateWall("Center_Block_A", new Vector3(-2f, 1f, -2f), new Vector3(4f, 2f, 0.7f));
            CreateWall("Center_Block_B", new Vector3(2.5f, 1f, 4f), new Vector3(4f, 2f, 0.7f));

            CreateCollectible(new Vector3(-4.8f, 0.75f, -4f), "Relic_01");
            CreateCollectible(new Vector3(4.8f, 0.75f, 1.5f), "Relic_02");
            CreateCollectible(new Vector3(-4.5f, 0.75f, 6.5f), "Relic_03");
            CreateHazard(new Vector3(0f, 0.12f, 2.6f), new Vector3(4.5f, 0.2f, 2.4f), 10);
            CreateCheckpoint(new Vector3(0f, 0.5f, -3.5f));
            CreateGoal(new Vector3(0f, 1f, 10.6f), new Vector3(3f, 2f, 0.5f), 3, "Exit_Gate_Goal");
            CreateSceneNote(new Vector3(0f, 0.05f, -11.5f), "Exploration: collect 3 relics, avoid hazard, reach gate.");

            BakeNavMesh();
            SaveScene(scene, ScenePaths[1]);
        }

        public static void BuildPlatform()
        {
            var scene = CreateScene("VARCO_Platform_Example");
            SetupCore("Platform - ride moving platforms to the goal");
            var player = CreatePlatformPlayer(new Vector3(-8f, 1.1f, 0f));
            SetupSideCamera(player.transform);

            CreateFloor("Start_Platform", new Vector3(-8f, 0f, 0f), new Vector3(4f, 0.4f, 4f), new Color(0.36f, 0.42f, 0.48f));
            CreateFloor("Middle_Island", new Vector3(0f, 2.2f, 0f), new Vector3(4f, 0.4f, 4f), new Color(0.36f, 0.42f, 0.48f));
            CreateFloor("Goal_Platform", new Vector3(8f, 3.5f, 0f), new Vector3(4f, 0.4f, 4f), new Color(0.36f, 0.42f, 0.48f));
            CreateMovingPlatform("Moving_Platform_A", new Vector3(-4f, 0.9f, 0f), new Vector3(-1.2f, 2f, 0f));
            CreateMovingPlatform("Moving_Platform_B", new Vector3(2f, 2.8f, 0f), new Vector3(5.2f, 3.5f, 0f));
            CreateCheckpoint(new Vector3(0f, 2.9f, 0f));
            CreateDeathZone(new Vector3(0f, -3.5f, 0f), new Vector3(30f, 1f, 10f));
            CreateGoal(new Vector3(9.2f, 4.5f, 0f), new Vector3(1.4f, 2f, 2f), 0, "Platform_Goal");
            CreateSceneNote(new Vector3(-8f, 0.35f, -2.2f), "Platform: move A/D, Space jump, reach yellow goal.");

            SaveScene(scene, ScenePaths[2]);
        }

        public static void BuildPuzzle()
        {
            var scene = CreateScene("VARCO_Puzzle_Example");
            SetupCore("Puzzle - push the box onto the plate to open the door");
            var player = CreateWorkshopPlayer(new Vector3(0f, 0.05f, -7f), "Puzzle_Player", true);
            SetupThirdPersonCamera(player.transform);

            CreateFloor("Puzzle_Ground", Vector3.zero, new Vector3(14f, 0.3f, 18f), new Color(0.36f, 0.34f, 0.32f));
            CreateWall("Puzzle_Wall_L", new Vector3(-7f, 1.4f, 0f), new Vector3(0.35f, 2.8f, 18f));
            CreateWall("Puzzle_Wall_R", new Vector3(7f, 1.4f, 0f), new Vector3(0.35f, 2.8f, 18f));
            CreateWall("Puzzle_Back", new Vector3(0f, 1.4f, 9f), new Vector3(14f, 2.8f, 0.35f));

            var door = CreateDoor(new Vector3(0f, 1.5f, 4.8f));
            CreatePressurePlate(new Vector3(3.2f, 0.18f, 0.5f), door);
            CreateMovableBox(new Vector3(-3.2f, 0.8f, 0.5f));
            CreateGoal(new Vector3(0f, 1f, 7.4f), new Vector3(2.5f, 2f, 0.6f), 0, "Puzzle_Goal");
            CreateSceneNote(new Vector3(0f, 0.05f, -8.5f), "Puzzle: push box onto plate, pass through opened door.");

            BakeNavMesh();
            SaveScene(scene, ScenePaths[3]);
        }

        static Scene CreateScene(string name)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = name;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            return scene;
        }

        static void SetupCore(string label)
        {
            var core = new GameObject("VW_Bootstrap");
            core.AddComponent<SceneBootstrap>();
            var gm = core.AddComponent<GameManager>();
            gm.loadResultScenes = false;
            core.AddComponent<PlaytestTuningOverlay>();

            var note = new GameObject("Scene Goal");
            note.transform.position = new Vector3(0f, 2.5f, 0f);
            note.name = "Scene Goal - " + label;
        }

        static GameObject CreateWorkshopPlayer(Vector3 position, string name, bool combatReady, bool moveInFacingDirection = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var go = prefab ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.tag = "Player";
            EnsureCapsule(go, 0.38f, 1.8f, new Vector3(0f, 0.9f, 0f));
            var rb = GetOrAdd<Rigidbody>(go);
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var health = GetOrAdd<PlayerHealth>(go);
            health.maxHP = 100;
            var counter = GetOrAdd<CollectibleCounter>(go);
            _ = counter;
            var ctrl = GetOrAdd<PlayerController_ThirdPerson>(go);
            ctrl.moveSpeed = 5f;
            ctrl.turnSpeed = 12f;
            ctrl.useCameraSpace = !moveInFacingDirection;
            ctrl.moveInFacingDirection = moveInFacingDirection;
            ctrl.facingTurnSpeed = 180f;
            ctrl.modelRoot = moveInFacingDirection ? null : FindAnimatorTransform(go);

            if (combatReady)
            {
                var attack = GetOrAdd<PlayerAttack>(go);
                attack.damage = 15;
                attack.cooldown = 0.38f;
                attack.range = 2.1f;
                attack.radius = 0.7f;
            }

            AssignAnimator(go, PlayerControllerPath);
            return go;
        }

        static GameObject CreatePlatformPlayer(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Platform_Player";
            go.transform.position = position;
            go.tag = "Player";
            SetObjectColor(go, new Color(0.25f, 0.55f, 0.9f));
            var collider = go.GetComponent<CapsuleCollider>();
            UnityEngine.Object.DestroyImmediate(collider);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            go.AddComponent<PlayerHealth>().maxHP = 100;
            var controller = go.AddComponent<PlayerController_Platform>();
            controller.moveSpeed = 6f;
            controller.jumpForce = 8f;
            controller.lockZAxis = true;
            controller.useCameraSpace = false;
            return go;
        }

        static GameObject CreateSpacePlatformPlayer(Vector3 position)
        {
            var go = new GameObject("Space_Platform_Player");
            go.transform.position = position;
            go.tag = "Player";

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.85f;
            cc.radius = 0.38f;
            cc.center = new Vector3(0f, 0.92f, 0f);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;

            go.AddComponent<PlayerHealth>().maxHP = 100;
            go.AddComponent<CollectibleCounter>();
            var controller = go.AddComponent<PlayerController_Platform>();
            controller.moveSpeed = 6.2f;
            controller.jumpForce = 8.6f;
            controller.gravity = -24f;
            controller.lockZAxis = false;
            controller.useCameraSpace = true;

            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlatformPlayerModelPath);
            if (modelPrefab)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
                model.name = "PLATFORM_Player_Model";
                model.transform.SetParent(go.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                var anim = model.GetComponentInChildren<Animator>(true);
                if (anim) anim.applyRootMotion = false;
                controller.modelRoot = model.transform;
            }
            else
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Fallback_Player_Model";
                body.transform.SetParent(go.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.92f, 0f);
                body.transform.localScale = new Vector3(0.75f, 0.95f, 0.75f);
                UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
                SetObjectColor(body, new Color(0.25f, 0.65f, 1f));
                controller.modelRoot = body.transform;
            }

            GetOrAdd<PlayerFootstepSound>(go);
            return go;
        }

        static void SetupThirdPersonCamera(Transform target)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = target.position + new Vector3(0f, 3f, -6f);
            camGo.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 62f;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<ThirdPersonCamera>();
            follow.target = target;
            follow.distance = 5.6f;
            follow.pivotOffset = new Vector3(0f, 1.25f, 0f);
            follow.positionSharpness = 12f;
            follow.pivotSharpness = 14f;
            follow.distanceSharpness = 9f;
        }

        static void SetupSideCamera(Transform target)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 6f, -15f);
            camGo.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 48f;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<ThirdPersonCamera>();
            follow.target = target;
            follow.distance = 13f;
            follow.pivotOffset = new Vector3(0f, 1.5f, 0f);
            follow.orbitWhileRightMouseButtonOnly = true;
        }

        static void SetupSpacePlatformCamera(Transform target)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = target.position + new Vector3(0f, 4.2f, -7.2f);
            camGo.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 58f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.014f, 0.035f);
            camGo.AddComponent<AudioListener>();

            var follow = camGo.AddComponent<ThirdPersonCamera>();
            follow.target = target;
            follow.distance = 7.4f;
            follow.minDistance = 3.2f;
            follow.pivotOffset = new Vector3(0f, 1.45f, 0f);
            follow.minPitch = 8f;
            follow.maxPitch = 42f;
            follow.positionSharpness = 18f;
            follow.pivotSharpness = 18f;
            follow.distanceSharpness = 12f;
            follow.orbitWhileRightMouseButtonOnly = false;
            follow.useWallClipping = true;
        }

        static GameObject CreateFloor(string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            SetObjectColor(go, color);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static GameObject CreateSpaceFloor(string name, Vector3 pos, Vector3 scale)
        {
            var go = CreateFloor(name, pos, scale, new Color(0.12f, 0.2f, 0.32f));
            var trimA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trimA.name = name + "_Cyan_Trim_A";
            trimA.transform.position = pos + new Vector3(0f, scale.y * 0.55f + 0.03f, scale.z * 0.5f - 0.08f);
            trimA.transform.localScale = new Vector3(scale.x, 0.06f, 0.08f);
            SetObjectColor(trimA, new Color(0.05f, 0.85f, 1f));
            UnityEngine.Object.DestroyImmediate(trimA.GetComponent<Collider>());

            var trimB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trimB.name = name + "_Cyan_Trim_B";
            trimB.transform.position = pos + new Vector3(0f, scale.y * 0.55f + 0.03f, -scale.z * 0.5f + 0.08f);
            trimB.transform.localScale = new Vector3(scale.x, 0.06f, 0.08f);
            SetObjectColor(trimB, new Color(0.05f, 0.85f, 1f));
            UnityEngine.Object.DestroyImmediate(trimB.GetComponent<Collider>());
            return go;
        }

        static GameObject CreateWall(string name, Vector3 pos, Vector3 scale)
        {
            var go = CreateFloor(name, pos, scale, new Color(0.24f, 0.27f, 0.31f));
            return go;
        }

        static void CreateCover(string name, Vector3 pos, Vector3 scale)
        {
            CreateFloor(name, pos, scale, new Color(0.18f, 0.22f, 0.26f));
        }

        static void CreateHealthPickup(Vector3 pos, int healAmount)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "HealthPickup";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.7f;
            SetObjectColor(go, new Color(0.9f, 0.15f, 0.18f));
            go.GetComponent<Collider>().isTrigger = true;
            go.AddComponent<HealthPickup>().healAmount = healAmount;
        }

        static void CreateCollectible(Vector3 pos, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.55f;
            SetObjectColor(go, new Color(0.2f, 0.8f, 0.35f));
            go.GetComponent<Collider>().isTrigger = true;
            go.AddComponent<ItemPickup>();
        }

        static void CreateHazard(Vector3 pos, Vector3 scale, int dps)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "HazardZone";
            go.transform.position = pos;
            go.transform.localScale = scale;
            SetObjectColor(go, new Color(1f, 0.35f, 0.05f));
            go.GetComponent<Collider>().isTrigger = true;
            go.AddComponent<HazardZone>().damagePerSecond = dps;
        }

        static void CreateCheckpoint(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Checkpoint";
            go.transform.position = pos;
            go.transform.localScale = new Vector3(1f, 0.25f, 1f);
            SetObjectColor(go, new Color(0.8f, 0.35f, 0.95f));
            go.GetComponent<Collider>().isTrigger = true;
            go.AddComponent<Checkpoint>();
        }

        static void CreateDeathZone(Vector3 pos, Vector3 scale)
        {
            var go = new GameObject("DeathZone");
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider>();
            box.size = scale;
            box.isTrigger = true;
            go.AddComponent<DeathZone>();
        }

        static GameObject CreateGoal(Vector3 pos, Vector3 scale, int requiredItems, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            SetObjectColor(go, new Color(1f, 0.82f, 0.08f));
            go.GetComponent<Collider>().isTrigger = true;
            go.AddComponent<GoalTrigger>().requiredItems = requiredItems;
            return go;
        }

        static DoorController CreateDoor(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Puzzle_Door";
            go.transform.position = pos;
            go.transform.localScale = new Vector3(3f, 3f, 0.45f);
            SetObjectColor(go, new Color(0.42f, 0.26f, 0.16f));
            var door = go.AddComponent<DoorController>();
            door.openOffset = new Vector3(0f, 3.4f, 0f);
            return door;
        }

        static void CreatePressurePlate(Vector3 pos, DoorController door)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "PressurePlate";
            go.transform.position = pos;
            go.transform.localScale = new Vector3(2f, 0.18f, 2f);
            SetObjectColor(go, new Color(0.95f, 0.8f, 0.15f));
            go.GetComponent<Collider>().isTrigger = true;
            go.AddComponent<PressurePlate>().targets = new[] { door };
        }

        static void CreateMovableBox(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "MovableBox";
            go.transform.position = pos;
            go.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            SetObjectColor(go, new Color(0.55f, 0.42f, 0.25f));
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            go.AddComponent<MovableBox>();
        }

        static void CreateMovingPlatform(string name, Vector3 aPos, Vector3 bPos)
        {
            var a = CreateMarker(name + "_A", aPos).transform;
            var b = CreateMarker(name + "_B", bPos).transform;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = aPos;
            go.transform.localScale = new Vector3(2.6f, 0.35f, 2.6f);
            SetObjectColor(go, new Color(0.2f, 0.48f, 0.9f));
            var mp = go.AddComponent<MovingPlatform>();
            mp.a = a;
            mp.b = b;
            mp.speed = 0.65f;
        }

        static void CreateMovingPlatform3D(string name, Vector3 aPos, Vector3 bPos, Vector3 scale, float speed)
        {
            var a = CreateMarker(name + "_A", aPos).transform;
            var b = CreateMarker(name + "_B", bPos).transform;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = aPos;
            go.transform.localScale = scale;
            SetObjectColor(go, new Color(0.08f, 0.52f, 0.86f));

            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = name + "_Light_Stripe";
            stripe.transform.SetParent(go.transform, false);
            stripe.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            stripe.transform.localScale = new Vector3(0.9f, 0.12f, 0.12f);
            SetObjectColor(stripe, new Color(0.95f, 0.95f, 0.18f));
            UnityEngine.Object.DestroyImmediate(stripe.GetComponent<Collider>());

            var mp = go.AddComponent<MovingPlatform>();
            mp.a = a;
            mp.b = b;
            mp.speed = speed;
        }

        static void CreateStarfield()
        {
            var root = new GameObject("Space_Backdrop_Stars");
            var positions = new[]
            {
                new Vector3(-18f, 16f, 28f), new Vector3(12f, 21f, 34f), new Vector3(36f, 18f, -18f),
                new Vector3(58f, 26f, 30f), new Vector3(82f, 18f, -24f), new Vector3(110f, 27f, 32f),
                new Vector3(134f, 20f, -22f), new Vector3(165f, 28f, 30f), new Vector3(206f, 24f, -12f),
                new Vector3(60f, 34f, 54f), new Vector3(150f, 36f, 48f), new Vector3(196f, 30f, 40f)
            };

            for (var i = 0; i < positions.Length; i++)
            {
                var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                star.name = "Star_" + (i + 1).ToString("00");
                star.transform.SetParent(root.transform);
                star.transform.position = positions[i];
                star.transform.localScale = Vector3.one * (i % 3 == 0 ? 0.7f : 0.35f);
                SetObjectColor(star, i % 4 == 0 ? new Color(0.45f, 0.75f, 1f) : Color.white);
                UnityEngine.Object.DestroyImmediate(star.GetComponent<Collider>());
            }

            var planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "Distant_Blue_Planet";
            planet.transform.position = new Vector3(132f, 28f, 54f);
            planet.transform.localScale = Vector3.one * 12f;
            SetObjectColor(planet, new Color(0.08f, 0.25f, 0.62f));
            UnityEngine.Object.DestroyImmediate(planet.GetComponent<Collider>());
        }

        static void CreateSpaceDrone(Vector3 position, Vector3 euler, float scale, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlatformDroneModelPath);
            GameObject go;
            if (prefab)
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            else
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            go.name = name;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = Vector3.one * scale;
            foreach (var collider in go.GetComponentsInChildren<Collider>())
                UnityEngine.Object.DestroyImmediate(collider);
        }

        static void CreateDockingRings(Vector3 center)
        {
            for (var i = 0; i < 3; i++)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "Docking_Ring_" + (i + 1).ToString("00");
                ring.transform.position = center + new Vector3(i * 1.25f - 1.25f, 0f, 0f);
                ring.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                ring.transform.localScale = new Vector3(2.4f - i * 0.35f, 0.08f, 2.4f - i * 0.35f);
                SetObjectColor(ring, new Color(0.95f, 0.82f, 0.12f));
                UnityEngine.Object.DestroyImmediate(ring.GetComponent<Collider>());
            }
        }

        static GameObject CreateMarker(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            return go;
        }

        static void CreateSceneNote(Vector3 pos, string text)
        {
            var go = new GameObject("Guide - " + text);
            go.transform.position = pos;
        }

        static void EnsureCapsule(GameObject go, float radius, float height, Vector3 center)
        {
            var cap = go.GetComponent<CapsuleCollider>();
            if (!cap) cap = go.AddComponent<CapsuleCollider>();
            cap.radius = radius;
            cap.height = height;
            cap.center = center;
        }

        static Transform FindAnimatorTransform(GameObject go)
        {
            var anim = go.GetComponentInChildren<Animator>(true);
            if (anim && anim.transform != go.transform)
                return anim.transform;
            return null;
        }

        static void AssignAnimator(GameObject go, string controllerPath)
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            var anim = go.GetComponentInChildren<Animator>(true);
            if (anim && controller)
            {
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
            }
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component ? component : go.AddComponent<T>();
        }

        static void SetObjectColor(GameObject go, Color color)
        {
            var renderer = go.GetComponentInChildren<Renderer>();
            if (!renderer) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = go.name + "_Mat" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            renderer.sharedMaterial = mat;
        }

        static void BakeNavMesh()
        {
            var navGo = new GameObject("NavMesh");
            var surfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (surfaceType == null)
            {
                Debug.LogWarning("[VARCO 예제] NavMeshSurface 패키지를 찾지 못했습니다. 적이 움직이지 않으면 씬을 열고 NavMesh를 직접 베이크하세요.");
                return;
            }

            var surface = navGo.AddComponent(surfaceType);
            SetFieldOrProperty(surface, "m_CollectObjects", 0);
            SetFieldOrProperty(surface, "m_UseGeometry", 0);
            SetFieldOrProperty(surface, "m_IgnoreNavMeshAgent", true);
            SetFieldOrProperty(surface, "m_IgnoreNavMeshObstacle", true);

            // Pre-place the baking surface, but do not bake here. Baking in batchmode embeds
            // generated NavMesh data in these Unity 6 scenes as binary, which makes the example
            // scenes look broken in source control and harder to recover in class.
            navGo.name = "NavMesh - Open AI Navigation and Bake";
        }

        static void SetFieldOrProperty(object target, string name, object value)
        {
            var type = target.GetType();
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite)
                prop.SetValue(target, value);
        }

        static void SaveScene(Scene scene, string path)
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir.Replace("\\", "/")))
                System.IO.Directory.CreateDirectory(dir);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        static void AddScenesToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var path in ScenePaths)
            {
                AddSceneToList(scenes, path);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            AddSceneToList(scenes, path);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void AddSceneToList(List<EditorBuildSettingsScene> scenes, string path)
        {
            var existing = scenes.FindIndex(s => s.path == path);
            if (existing >= 0)
                scenes[existing] = new EditorBuildSettingsScene(path, true);
            else
                scenes.Add(new EditorBuildSettingsScene(path, true));
        }
    }
}
#endif
