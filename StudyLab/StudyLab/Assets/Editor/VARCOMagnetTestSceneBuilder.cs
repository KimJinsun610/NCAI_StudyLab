#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VARCO_Workshop.Editor
{
    /// <summary>자석(MagnetTarget/MagnetAimController) 기능을 빠르게 테스트하기 위한 1회성 씬 빌더.
    /// 학생용 게임 만들기 파이프라인과는 별개의 개발자 테스트 도구입니다.</summary>
    public static class VARCOMagnetTestSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/VARCO_Magnet/VARCO_Magnet_Test.unity";
        const string HazardCubePrefabPath = "Assets/Prefabs/Magnet/VARCO_HazardCube.prefab";
        const string ClearCubePrefabPath = "Assets/Prefabs/Magnet/VARCO_ClearCube.prefab";

        // 곰돌이(TeddyBear) 모델 + 애니메이터가 이미 구성된 캐릭터 프리팹(VARCOCharacterPrefabMakerWindow 등으로 생성됨).
        // 이 프리팹을 복제해서 자석 기능만 추가합니다 — 모델/아바타/애니메이터를 직접 다시 만들지 않습니다.
        const string SourceCharacterPrefabPath = "Assets/Prefabs/VARCO_Characters/test_character/Prefabs/test_character.prefab";
        const string MagnetPlayerPrefabPath = "Assets/Prefabs/Magnet/VARCO_MagnetPlayer_TeddyBear.prefab";

        // 원본 컨트롤러엔 Idle/Walk/Jump만 연결돼 있고 Attack 스테이트는 비어있음(모션도, 트랜지션도 없음).
        // 자석 전용 프리팹에서만 이 Attack 스테이트에 자석 사용(Aim) 애니메이션을 연결합니다.
        const string SourceControllerPath = "Assets/Prefabs/VARCO_Characters/test_character/test_character_Controller.controller";
        const string MagnetControllerPath = "Assets/Prefabs/Magnet/VARCO_MagnetPlayer_TeddyBear_Controller.controller";
        const string TeddyAimFbxPath = "Assets/Study_Lab/resouse/TeddyBear_Aim.fbx";
        const string AimClipPath = "Assets/Animations/Generated/test_character/test_character_Aim.anim";
        const string TeddyDrowningFbxPath = "Assets/Study_Lab/resouse/TeddyBear_Drowning.fbx";
        const string DrowningClipPath = "Assets/Animations/Generated/test_character/test_character_Drowning.anim";

        [MenuItem("VARCO/테스트 씬/자석 퍼즐 테스트 씬 생성")]
        public static void BuildMagnetTestScene()
        {
            EnsureGameplayPrefabs();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "VARCO_Magnet_Test";

            CreateGameManager();
            CreateLight();
            CreateFloor();

            var player = CreatePlayer(new Vector3(0f, 1f, 0f));
            CreateCamera(player.transform);
            CreateMagnetItemPickup(new Vector3(0f, 0.6f, 2f));

            CreateMagnetCube("Magnet_CubeA_Light", new Vector3(2f, 0.5f, 4f), weight: 1f, maxRange: 8f);
            CreateMagnetSphere("Magnet_SphereB_Heavy", new Vector3(-2f, 0.5f, 6f), weight: 8f, maxRange: 8f);
            CreateMagnetCube("Magnet_CubeC_ShortRange", new Vector3(0f, 0.5f, 9f), weight: 1f, maxRange: 7f);

            InstantiatePrefabAt(HazardCubePrefabPath, new Vector3(3.5f, 0.5f, 2f));
            InstantiatePrefabAt(ClearCubePrefabPath, new Vector3(0f, 0.5f, 12f));

            SaveScene(scene, ScenePath);
            Debug.Log("[VARCO 자석] 테스트 씬을 생성했습니다: " + ScenePath);
        }

        /// <summary>기존에 만들어둔 곰돌이 캐릭터 프리팹(test_character)을 복제해서 자석 기능이 있는
        /// 플레이어 프리팹으로 만듭니다. 모델·아바타·애니메이터는 원본 그대로 재사용합니다.</summary>
        [MenuItem("VARCO/테스트 씬/자석용 곰돌이 플레이어 프리팹 생성")]
        public static void BuildMagnetTeddyBearPlayerPrefab()
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(SourceCharacterPrefabPath))
            {
                Debug.LogError("[VARCO 자석] 원본 캐릭터 프리팹을 찾지 못했습니다: " + SourceCharacterPrefabPath +
                    " (곰돌이 캐릭터 프리팹을 먼저 만들어 주세요)");
                return;
            }

            var dir = System.IO.Path.GetDirectoryName(MagnetPlayerPrefabPath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir.Replace("\\", "/")))
                System.IO.Directory.CreateDirectory(dir);

            AssetDatabase.DeleteAsset(MagnetPlayerPrefabPath);
            if (!AssetDatabase.CopyAsset(SourceCharacterPrefabPath, MagnetPlayerPrefabPath))
            {
                Debug.LogError("[VARCO 자석] 캐릭터 프리팹 복제에 실패했습니다.");
                return;
            }
            AssetDatabase.Refresh();

            var root = PrefabUtility.LoadPrefabContents(MagnetPlayerPrefabPath);
            root.name = "VARCO_MagnetPlayer_TeddyBear";

            // 좌클릭이 자석 선택/릴리즈로 쓰이므로 근접 공격(좌클릭)과 충돌하지 않게 제거합니다.
            var attack = root.GetComponent<PlayerAttack>();
            if (attack) Object.DestroyImmediate(attack);

            var health = GetOrAdd<PlayerHealth>(root);
            health.maxHP = 3;

            var magnet = GetOrAdd<MagnetAimController>(root);
            magnet.aimRange = 10f;

            var aimClip = BuildAimClip();
            var drowningClip = BuildDrowningClip();
            WireMagnetAnimator(root, aimClip, drowningClip);

            PrefabUtility.SaveAsPrefabAsset(root, MagnetPlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[VARCO 자석] 곰돌이 자석 플레이어 프리팹을 생성했습니다: " + MagnetPlayerPrefabPath);
        }

        /// <summary>TeddyBear_Aim.fbx에 들어있는 애니메이션 클립을 test_character_Idle/Walk/Jump.anim과
        /// 같은 방식(복제 후 Assets/Animations/Generated/test_character에 저장)으로 뽑아냅니다.</summary>
        static AnimationClip BuildAimClip()
        {
            var sourceClip = AssetDatabase.LoadAllAssetsAtPath(TeddyAimFbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (!sourceClip)
            {
                Debug.LogError("[VARCO 자석] TeddyBear_Aim.fbx에서 애니메이션 클립을 찾지 못했습니다: " + TeddyAimFbxPath);
                return null;
            }

            var dir = System.IO.Path.GetDirectoryName(AimClipPath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir.Replace("\\", "/")))
                System.IO.Directory.CreateDirectory(dir);

            AssetDatabase.DeleteAsset(AimClipPath);

            var copy = new AnimationClip();
            EditorUtility.CopySerialized(sourceClip, copy);
            copy.name = "test_character_Aim";

            // 자석 사용은 버튼을 누르는 순간이 아니라 부착돼 있는 동안 계속 유지되는 상태라
            // Idle/Walk처럼 반복 재생시킵니다(Attack 롤의 기본값인 1회 재생과는 다르게 의도적으로 선택).
            copy.wrapMode = WrapMode.Loop;
            var clipSettings = AnimationUtility.GetAnimationClipSettings(copy);
            clipSettings.loopTime = true;
            clipSettings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(copy, clipSettings);

            AssetDatabase.CreateAsset(copy, AimClipPath);
            return copy;
        }

        /// <summary>TeddyBear_Drowning.fbx에 들어있는 애니메이션 클립을 같은 방식으로 뽑아냅니다.</summary>
        static AnimationClip BuildDrowningClip()
        {
            var sourceClip = AssetDatabase.LoadAllAssetsAtPath(TeddyDrowningFbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (!sourceClip)
            {
                Debug.LogError("[VARCO 자석] TeddyBear_Drowning.fbx에서 애니메이션 클립을 찾지 못했습니다: " + TeddyDrowningFbxPath);
                return null;
            }

            var dir = System.IO.Path.GetDirectoryName(DrowningClipPath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir.Replace("\\", "/")))
                System.IO.Directory.CreateDirectory(dir);

            AssetDatabase.DeleteAsset(DrowningClipPath);

            var copy = new AnimationClip();
            EditorUtility.CopySerialized(sourceClip, copy);
            copy.name = "test_character_Drowning";

            // 물에 있는 동안 계속 유지되는 상태라 반복 재생시킵니다.
            copy.wrapMode = WrapMode.Loop;
            var clipSettings = AnimationUtility.GetAnimationClipSettings(copy);
            clipSettings.loopTime = true;
            clipSettings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(copy, clipSettings);

            AssetDatabase.CreateAsset(copy, DrowningClipPath);
            return copy;
        }

        /// <summary>원본 test_character_Controller를 복제해서 자석 프리팹 전용 컨트롤러로 쓰고,
        /// 비어있던 Attack 스테이트에 자석 사용 애니메이션을, 새 Drowning 스테이트에 물에 빠졌을 때
        /// 애니메이션을 연결합니다. 원본 컨트롤러(다른 씬에서도 쓰일 수 있음)는 건드리지 않기 위해
        /// 반드시 복제본에서만 작업합니다.</summary>
        static void WireMagnetAnimator(GameObject root, AnimationClip aimClip, AnimationClip drowningClip)
        {
            var animator = root.GetComponentInChildren<Animator>(true);
            if (!animator)
            {
                Debug.LogWarning("[VARCO 자석] Animator를 찾지 못해 애니메이션 연결을 건너뜁니다.");
                return;
            }

            AssetDatabase.DeleteAsset(MagnetControllerPath);
            if (!AssetDatabase.CopyAsset(SourceControllerPath, MagnetControllerPath))
            {
                Debug.LogError("[VARCO 자석] 애니메이터 컨트롤러 복제에 실패했습니다: " + SourceControllerPath);
                return;
            }
            AssetDatabase.Refresh();

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MagnetControllerPath);
            if (!controller)
            {
                Debug.LogError("[VARCO 자석] 복제된 컨트롤러를 불러오지 못했습니다: " + MagnetControllerPath);
                return;
            }

            animator.runtimeAnimatorController = controller;

            // IsAttack을 Trigger(1회성)에서 Bool로 바꿔서 "부착돼 있는 동안 계속 유지" 상태를 표현합니다.
            // 이 컨트롤러는 방금 만든 복제본이라 원본 test_character의 IsAttack(Trigger, PlayerAttack이 사용)에는 영향 없습니다.
            var parameters = controller.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == "IsAttack")
                {
                    parameters[i].type = AnimatorControllerParameterType.Bool;
                    break;
                }
            }
            controller.parameters = parameters;

            var sm = controller.layers[0].stateMachine;
            var idleState = sm.states.FirstOrDefault(s => s.state.name == "Idle").state;
            var attackState = sm.states.FirstOrDefault(s => s.state.name == "Attack").state;
            if (!idleState || !attackState)
            {
                Debug.LogWarning("[VARCO 자석] 컨트롤러에서 Idle/Attack 스테이트를 찾지 못해 애니메이션 연결을 건너뜁니다.");
                return;
            }

            attackState.motion = aimClip;

            var enter = sm.AddAnyStateTransition(attackState);
            enter.hasExitTime = false;
            enter.hasFixedDuration = true;
            enter.duration = 0.12f;
            enter.offset = 0f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "IsAttack");

            // 지속 상태이므로(반대 애니메이션인 Jump처럼) 정해진 시간에 빠져나가지 않고,
            // IsAttack이 false가 되는 즉시(Walk<->Idle처럼) Idle로 복귀합니다.
            var exit = attackState.AddTransition(idleState);
            exit.hasExitTime = false;
            exit.hasFixedDuration = true;
            exit.duration = 0.12f;
            exit.offset = 0f;
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAttack");

            if (drowningClip)
                WireDrowningState(controller, sm, idleState, drowningClip);

            EditorUtility.SetDirty(controller);
        }

        /// <summary>물(위험 구역)에 들어가면 재생되는 Drowning 스테이트를 새로 만들어 연결합니다.
        /// 다른 애니메이션(점프/자석 사용/걷기)보다 후순위여야 하므로 AnyState 목록 맨 뒤에 추가하고,
        /// 조건에도 그 상태들이 아닐 때만(IfNot) 켜지도록 명시적으로 제외합니다.</summary>
        static void WireDrowningState(AnimatorController controller, AnimatorStateMachine sm, AnimatorState idleState, AnimationClip drowningClip)
        {
            if (!controller.parameters.Any(p => p.name == "InWater"))
                controller.AddParameter("InWater", AnimatorControllerParameterType.Bool);

            var drowningState = sm.states.FirstOrDefault(s => s.state.name == "Drowning").state;
            if (!drowningState)
                drowningState = sm.AddState("Drowning", new Vector3(520, 500, 0));
            drowningState.motion = drowningClip;

            var enter = sm.AddAnyStateTransition(drowningState);
            enter.hasExitTime = false;
            enter.hasFixedDuration = true;
            enter.duration = 0.15f;
            enter.offset = 0f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "InWater");
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsJump");
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAttack");
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsWalk");

            var exit = drowningState.AddTransition(idleState);
            exit.hasExitTime = false;
            exit.hasFixedDuration = true;
            exit.duration = 0.15f;
            exit.offset = 0f;
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "InWater");
        }

        static void CreateGameManager()
        {
            var core = new GameObject("VW_Bootstrap");
            var gm = core.AddComponent<GameManager>();
            gm.loadResultScenes = false;
            // 이 프로젝트는 별도 Clear/GameOver 씬 없이 같은 씬을 재시작하는 구조라서,
            // GameManager가 DontDestroyOnLoad로 살아남으면 재시작 후에도 state가 GameOver에 고정된 채라
            // 두 번째 죽음부터는 TriggerGameOver()가 다시 발동하지 않습니다. 씬 재시작 시 항상 새로 만들어지게.
            gm.persistAcrossScenes = false;
        }

        /// <summary>HazardZone/GoalTrigger를 이미 가진 게임플레이 프리팹 두 개를 (없으면) 생성/갱신합니다.
        /// 메쉬는 우선 큐브 — 나중에 실제 모델로 교체하려면 프리팹을 열어 자식 오브젝트만 바꾸면 됩니다.
        /// 씬은 전혀 건드리지 않습니다 — 프리팹만 다시 만들고 싶을 때 이 메뉴를 쓰세요.</summary>
        [MenuItem("VARCO/테스트 씬/자석 게임플레이 프리팹만 갱신 (씬 건드리지 않음)")]
        public static void EnsureGameplayPrefabs()
        {
            SavePrefab(HazardCubePrefabPath, BuildHazardCubeTemplate);
            SavePrefab(ClearCubePrefabPath, BuildClearCubeTemplate);
        }

        static GameObject BuildHazardCubeTemplate()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "VARCO_HazardCube";
            SetHazardSeaMaterial(go);

            var collider = go.GetComponent<Collider>();
            collider.isTrigger = true;

            var hazard = go.AddComponent<HazardZone>();
            hazard.damagePerSecond = 1;
            go.AddComponent<WaterAnimationTrigger>();
            return go;
        }

        static GameObject BuildClearCubeTemplate()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "VARCO_ClearCube";
            SetClearShimmerMaterial(go);

            var collider = go.GetComponent<Collider>();
            collider.isTrigger = true;

            var goal = go.AddComponent<GoalTrigger>();
            goal.requiredItems = 0;
            return go;
        }

        static void SavePrefab(string path, System.Func<GameObject> buildTemplate)
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir.Replace("\\", "/")))
                System.IO.Directory.CreateDirectory(dir);

            var temp = buildTemplate();
            PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
        }

        static void InstantiatePrefabAt(string prefabPath, Vector3 pos)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab)
            {
                Debug.LogWarning("[VARCO 자석] 프리팹을 찾지 못했습니다: " + prefabPath);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = pos;
        }

        static void CreateLight()
        {
            // 단색(Flat) 대신 하늘/수평선/바닥 세 색을 쓰는 Trilight — 단조롭지 않고 입체감 있는 앰비언트
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.62f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.46f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.22f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.956f, 0.839f); // 살짝 따뜻한 햇빛 색온도
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.8f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        static void CreateFloor()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground";
            go.transform.position = Vector3.zero;
            go.transform.localScale = new Vector3(4f, 1f, 4f);
            SetObjectColor(go, new Color(0.4f, 0.42f, 0.45f));
        }

        static GameObject CreatePlayer(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Magnet_Player";
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.tag = "Player";
            SetObjectColor(go, new Color(0.25f, 0.55f, 0.9f));

            var rb = GetOrAdd<Rigidbody>(go);
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var health = GetOrAdd<PlayerHealth>(go);
            health.maxHP = 3;

            var ctrl = GetOrAdd<PlayerController_ThirdPerson>(go);
            ctrl.moveSpeed = 5f;
            ctrl.turnSpeed = 12f;
            ctrl.useCameraSpace = true;

            var magnet = GetOrAdd<MagnetAimController>(go);
            magnet.aimRange = 10f;

            return go;
        }

        static void CreateCamera(Transform target)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = target.position + new Vector3(0f, 2.5f, -5f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();

            var follow = camGo.AddComponent<ThirdPersonCamera>();
            follow.target = target;
            follow.distance = 5f;
            follow.pivotOffset = new Vector3(0f, 1.25f, 0f);
            follow.shoulderView = true;
            follow.shoulderSideOffset = 0.55f;
            follow.lockCursorOnStart = true;
        }

        static void CreateMagnetItemPickup(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Magnet_Item_Pickup";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.5f;
            SetObjectColor(go, new Color(1f, 0.15f, 0.75f));

            var collider = go.GetComponent<Collider>();
            collider.isTrigger = true;
            go.AddComponent<MagnetItemPickup>();
        }

        static void CreateMagnetCube(string name, Vector3 pos, float weight, float maxRange)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SetupMagnetObject(go, name, pos, weight, maxRange);
        }

        static void CreateMagnetSphere(string name, Vector3 pos, float weight, float maxRange)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            SetupMagnetObject(go, name, pos, weight, maxRange);
        }

        static void SetupMagnetObject(GameObject go, string name, Vector3 pos, float weight, float maxRange)
        {
            go.name = name;
            go.transform.position = pos;
            SetObjectColor(go, new Color(0.75f, 0.75f, 0.78f));

            GetOrAdd<Rigidbody>(go);

            var magnet = GetOrAdd<MagnetTarget>(go);
            magnet.maxRange = maxRange;
            magnet.weight = weight; // Awake에서 Rigidbody.mass에 그대로 반영됨
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
            var mat = GetOrCreateMaterial($"Assets/Materials/Magnet/{go.name}_Mat.mat", shader, m =>
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                else if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            });
            renderer.sharedMaterial = mat;
        }

        /// <summary>검은 바다처럼 표면이 출렁이는 위험 큐브 전용 머티리얼(Assets/Shaders/VARCO_HazardBlackSea.shader).
        /// 메쉬 자체는 그대로 큐브 — 정점을 살짝 흔들고, 프래그먼트에서 물결 하이라이트/Fresnel로 움직임을 표현합니다.</summary>
        static void SetHazardSeaMaterial(GameObject go)
        {
            var renderer = go.GetComponentInChildren<Renderer>();
            if (!renderer) return;

            var shader = Shader.Find("VARCO/HazardBlackSea");
            if (!shader)
            {
                Debug.LogWarning("[VARCO 자석] VARCO/HazardBlackSea 셰이더를 찾지 못해 기본 색으로 대체합니다.");
                SetObjectColor(go, new Color(0.02f, 0.02f, 0.04f));
                return;
            }

            var mat = GetOrCreateMaterial($"Assets/Materials/Magnet/{go.name}_Mat.mat", shader, null);
            renderer.sharedMaterial = mat;
        }

        /// <summary>반투명하게 일렁이는 클리어 큐브 전용 머티리얼(Assets/Shaders/VARCO_ClearShimmer.shader).
        /// HazardBlackSea와 같은 파도 패턴을 쓰지만 투명 블렌딩 + 밝은 금색 톤으로 반대 느낌을 냅니다.</summary>
        static void SetClearShimmerMaterial(GameObject go)
        {
            var renderer = go.GetComponentInChildren<Renderer>();
            if (!renderer) return;

            var shader = Shader.Find("VARCO/ClearShimmer");
            if (!shader)
            {
                Debug.LogWarning("[VARCO 자석] VARCO/ClearShimmer 셰이더를 찾지 못해 기본 색으로 대체합니다.");
                SetObjectColor(go, new Color(1f, 0.82f, 0.08f));
                return;
            }

            // 이 경로에 예전 셰이더로 저장된 머티리얼이 이미 있으면 _BaseColor 등 값이 그대로 남아있을 수 있어
            // (예: 알파 1로 불투명) 반투명 기본값을 명시적으로 다시 지정합니다.
            var mat = GetOrCreateMaterial($"Assets/Materials/Magnet/{go.name}_Mat.mat", shader, m =>
            {
                m.SetColor("_BaseColor", new Color(1f, 0.85f, 0.3f, 0.5f));
                m.SetColor("_ShimmerColor", new Color(1f, 0.98f, 0.82f, 0.85f));
            });
            renderer.sharedMaterial = mat;
        }

        /// <summary>머티리얼을 실제 .mat 에셋 파일로 저장해서 반환합니다. new Material()을 만들어서
        /// Renderer에만 대입하고 끝내면(과거 방식) PrefabUtility.SaveAsPrefabAsset 시 참조가 저장되지 않고
        /// 비어버립니다(fileID: 0) — 그래서 색이 하나도 안 먹은 것처럼 보였던 원인입니다.
        /// 반드시 이 함수처럼 디스크에 저장된 진짜 에셋을 참조하게 해야 합니다.</summary>
        static Material GetOrCreateMaterial(string path, Shader shader, System.Action<Material> configure)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing)
            {
                if (existing.shader != shader)
                    existing.shader = shader; // 이전에 다른 셰이더로 저장된 동일 경로 머티리얼을 재사용하는 경우 갱신
                configure?.Invoke(existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir.Replace("\\", "/")))
                System.IO.Directory.CreateDirectory(dir);

            var mat = new Material(shader);
            configure?.Invoke(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void SaveScene(Scene scene, string path)
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir.Replace("\\", "/")))
                System.IO.Directory.CreateDirectory(dir);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
