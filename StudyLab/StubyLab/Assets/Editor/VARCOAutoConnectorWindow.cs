#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using VWS = VARCO_Workshop;

namespace VARCO_Workshop.Editor
{
    /// <summary>
    /// 선택한 씬 모델에 맞는 기능 컴포넌트를 붙이고 기존 워크숍 시스템에 연결합니다.
    /// </summary>
    public class VARCOAutoConnectorWindow : EditorWindow
    {
        public static void CreateInitialSceneSetup()
        {
            var connector = CreateInstance<VARCOAutoConnectorWindow>();
            connector.EnsureSystemObjects();
            connector.EnsureMainCamera();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            DestroyImmediate(connector);
        }

        public enum Role
        {
            Player,
            PlatformPlayer,
            Enemy,
            ItemPickup,
            HealthPickup,
            Goal,
            Door,
            PressurePlate,
            HazardZone,
            MovingPlatform,
            MovableBox,
            Checkpoint,
            ArenaCover
        }

        public enum CameraViewPreset
        {
            ThirdPerson,
            QuarterView,
            TopDown,
            SideView
        }

        public static readonly GUIContent[] CameraViewLabels =
        {
            new GUIContent("3인칭 추적뷰", "뒤에서 캐릭터를 따라가는 일반 3D 액션 카메라"),
            new GUIContent("쿼터뷰", "전투 아레나/탐험/퍼즐에 어울리는 비스듬한 상단 카메라"),
            new GUIContent("탑다운", "수집/전략형 플레이에 어울리는 높은 시점 카메라"),
            new GUIContent("사이드뷰", "플랫폼 장르에 어울리는 옆면 카메라")
        };

        static readonly Role[] RoleOptions =
        {
            Role.Player,
            Role.PlatformPlayer,
            Role.Enemy,
            Role.ItemPickup,
            Role.HealthPickup,
            Role.Goal,
            Role.Door,
            Role.PressurePlate,
            Role.HazardZone,
            Role.MovingPlatform,
            Role.MovableBox,
            Role.Checkpoint,
            Role.ArenaCover
        };

        static readonly GUIContent[] RoleLabels =
        {
            new GUIContent("플레이어"),
            new GUIContent("플랫폼 플레이어"),
            new GUIContent("적 / 좀비"),
            new GUIContent("수집 아이템"),
            new GUIContent("회복 아이템"),
            new GUIContent("목표 지점"),
            new GUIContent("문"),
            new GUIContent("압력판 / 스위치"),
            new GUIContent("위험 구역"),
            new GUIContent("이동 발판"),
            new GUIContent("밀 수 있는 상자"),
            new GUIContent("체크포인트"),
            new GUIContent("환경 소품 / 엄폐물")
        };

        Role _role = Role.Enemy;
        GameObject _selected;
        RuntimeAnimatorController _animatorController;
        bool _connectEnemyToWave = true;
        bool _saveEnemyAsPrefab = true;
        int _waveIndex;
        int _requiredItems;
        int _healAmount = 25;
        int _hazardDps = 15;
        float _movingPlatformDistance = 4f;
        float _movingPlatformSpeed = 1.2f;
        bool _moveInFacingDirectionForPlayer;
        CameraViewPreset _cameraViewPreset = CameraViewPreset.ThirdPerson;
        string _log = "";
        VARCOEditorRepaintGate repaintGate;

        const string AutoPrefabFolder = "Assets/Prefabs/Characters/AutoConnected";
        const string DefaultHealthDropPrefabPath = "Assets/VARCO3DImports/ARENA_Healingpotion/model.fbx";
        const string DefaultSoundRegistryPath = "Assets/ScriptableObjects/SoundEvents/WorkshopSoundEventRegistry.asset";

        public static void Open()
        {
            var window = GetWindow<VARCOAutoConnectorWindow>("선택 모델 기능 연결");
            window.minSize = new Vector2(430f, 560f);
            window._selected = Selection.activeGameObject;
            if (window._selected)
                window._role = GuessRoleFromName(window._selected.name);
        }

        void OnDisable()
        {
            if (repaintGate != null)
                repaintGate.Dispose();
            repaintGate = null;
        }

        public static void ConnectFromFeatureBuilder(
            GameObject selected,
            Role role,
            RuntimeAnimatorController animatorController,
            bool connectEnemyToWave,
            bool saveEnemyAsPrefab,
            int waveIndex,
            int requiredItems,
            int healAmount,
            int hazardDps,
            float movingPlatformDistance,
            float movingPlatformSpeed,
            bool moveInFacingDirectionForPlayer,
            CameraViewPreset cameraViewPreset)
        {
            var connector = CreateInstance<VARCOAutoConnectorWindow>();
            connector._selected = selected;
            connector._role = role;
            connector._animatorController = animatorController;
            connector._connectEnemyToWave = connectEnemyToWave;
            connector._saveEnemyAsPrefab = saveEnemyAsPrefab;
            connector._waveIndex = waveIndex;
            connector._requiredItems = requiredItems;
            connector._healAmount = healAmount;
            connector._hazardDps = hazardDps;
            connector._movingPlatformDistance = movingPlatformDistance;
            connector._movingPlatformSpeed = movingPlatformSpeed;
            connector._moveInFacingDirectionForPlayer = moveInFacingDirectionForPlayer;
            connector._cameraViewPreset = cameraViewPreset;
            connector.ConnectSelected();
            DestroyImmediate(connector);
        }

        void OnGUI()
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField("선택한 모델을 게임 기능에 자동 연결", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Hierarchy에서 VARCO 모델 또는 프리팹 인스턴스를 선택한 뒤 역할을 고르세요. 모델 임포트/배치는 그대로 두고, 필요한 Collider/Rigidbody/게임 스크립트와 웨이브/목표 연결을 자동으로 처리합니다.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _selected = (GameObject)EditorGUILayout.ObjectField("선택 모델", _selected ? _selected : Selection.activeGameObject, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck() && _selected)
            {
                Selection.activeGameObject = _selected;
                _role = GuessRoleFromName(_selected.name);
            }

            if (!_selected && Selection.activeGameObject)
                _selected = Selection.activeGameObject;

            _role = DrawRolePopup("연결할 기능", _role);
            if (_selected != null)
            {
                var guessed = GuessRoleFromName(_selected.name);
                if (guessed != _role)
                    EditorGUILayout.HelpBox($"오브젝트 이름 \"{_selected.name}\" 기준 추천 기능: {RoleLabel(guessed)}", MessageType.None);
            }
            _animatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField("애니메이션 컨트롤러", _animatorController, typeof(RuntimeAnimatorController), false);

            DrawRoleOptions();

            GUILayout.Space(10);
            using (new EditorGUI.DisabledScope(_selected == null))
            {
                GUI.backgroundColor = new Color(0.45f, 0.85f, 0.55f);
                if (GUILayout.Button("선택 모델에 기능 연결", GUILayout.Height(42)))
                    ConnectSelected();
                GUI.backgroundColor = Color.white;
            }

            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("현재 선택 사용"))
                    _selected = Selection.activeGameObject;
                if (GUILayout.Button("기록 지우기"))
                    _log = "";
            }

            if (!string.IsNullOrEmpty(_log))
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("작업 기록", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_log, GUILayout.MinHeight(110));
            }
        }

        static Role GuessRoleFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return Role.Player;
            var n = name.ToLowerInvariant();
            if (ContainsAny(n, "movingplatform", "moving_platform", "lift", "elevator", "moving", "이동발판", "움직이는발판", "리프트", "엘리베이터")) return Role.MovingPlatform;
            if (ContainsAny(n, "pressureplate", "pressure_plate", "plate", "switch", "button", "압력판", "발판", "스위치", "버튼")) return Role.PressurePlate;
            if (ContainsAny(n, "movablebox", "movable_box", "pushbox", "box", "crate", "push", "상자", "박스", "밀기", "밀수있는")) return Role.MovableBox;
            if (ContainsAny(n, "health", "heal", "healing", "potion", "hp", "회복", "힐", "포션", "체력")) return Role.HealthPickup;
            if (ContainsAny(n, "boss", "enemy", "monster", "mob", "orc", "zombie", "undead", "goblin", "demon", "drone", "보스", "적", "몬스터", "오크", "좀비", "언데드", "드론")) return Role.Enemy;
            if (ContainsAny(n, "platformplayer", "platform_player", "플랫폼플레이어")) return Role.PlatformPlayer;
            if (ContainsAny(n, "player", "hero", "knight", "warrior", "astronaut", "explorer", "플레이어", "주인공", "영웅", "기사", "전사", "우주인", "탐험가")) return Role.Player;
            if (ContainsAny(n, "goal", "flag", "crystal", "orb", "exit", "portal", "목표", "깃발", "크리스탈", "탈출", "포탈")) return Role.Goal;
            if (ContainsAny(n, "door", "gate", "문", "게이트", "출입문")) return Role.Door;
            if (ContainsAny(n, "checkpoint", "savepoint", "respawn", "체크포인트", "세이브", "리스폰")) return Role.Checkpoint;
            if (ContainsAny(n, "hazard", "trap", "spike", "lava", "damage", "위험", "함정", "가시", "용암", "데미지", "피해")) return Role.HazardZone;
            if (ContainsAny(n,
                    "cover", "wall", "barrier", "rock", "obstacle", "tree", "plant", "bush", "grass", "pillar", "ruin", "debris", "prop", "scenery", "environment",
                    "엄폐", "벽", "장애물", "바위", "나무", "식물", "수풀", "풀", "기둥", "폐허", "잔해", "소품", "배경", "환경")) return Role.ArenaCover;
            if (ContainsAny(n, "item", "pickup", "key", "coin", "gem", "treasure", "아이템", "열쇠", "키", "코인", "보석", "수집", "보물")) return Role.ItemPickup;
            return Role.Player;
        }

        static bool ContainsAny(string value, params string[] keywords)
        {
            foreach (var keyword in keywords)
                if (value.Contains(keyword))
                    return true;
            return false;
        }

        static Role DrawRolePopup(string label, Role current)
        {
            var index = System.Array.IndexOf(RoleOptions, current);
            if (index < 0)
                index = 0;

            index = EditorGUILayout.Popup(new GUIContent(label), index, RoleLabels);
            return RoleOptions[Mathf.Clamp(index, 0, RoleOptions.Length - 1)];
        }

        static string RoleLabel(Role role)
        {
            var index = System.Array.IndexOf(RoleOptions, role);
            return index >= 0 ? RoleLabels[index].text : role.ToString();
        }

        void DrawRoleOptions()
        {
            switch (_role)
            {
                case Role.Player:
                    _cameraViewPreset = DrawCameraViewPopup("카메라 시점", _cameraViewPreset);
                    break;
                case Role.PlatformPlayer:
                    _cameraViewPreset = DrawCameraViewPopup("카메라 시점", _cameraViewPreset);
                    break;
                case Role.Enemy:
                    _saveEnemyAsPrefab = EditorGUILayout.ToggleLeft("웨이브 연결 전에 프리팹으로 저장/갱신", _saveEnemyAsPrefab);
                    _connectEnemyToWave = EditorGUILayout.ToggleLeft("첫 번째 웨이브 매니저에 연결", _connectEnemyToWave);
                    if (_connectEnemyToWave)
                        _waveIndex = EditorGUILayout.IntField("웨이브 번호", _waveIndex);
                    EditorGUILayout.HelpBox("전투 아레나/탐험용 적: 내비게이션 이동, 적 체력, 적 AI, 충돌 영역을 보장하고 웨이브 매니저에 연결합니다.", MessageType.None);
                    break;
                case Role.Goal:
                    _requiredItems = EditorGUILayout.IntSlider("필요 아이템 수", _requiredItems, 0, 12);
                    break;
                case Role.HealthPickup:
                    _healAmount = EditorGUILayout.IntSlider("회복량", _healAmount, 5, 100);
                    break;
                case Role.HazardZone:
                    _hazardDps = EditorGUILayout.IntSlider("초당 피해", _hazardDps, 1, 50);
                    break;
                case Role.MovingPlatform:
                    _movingPlatformDistance = EditorGUILayout.Slider("이동 거리", _movingPlatformDistance, 1f, 12f);
                    _movingPlatformSpeed = EditorGUILayout.Slider("속도", _movingPlatformSpeed, 0.2f, 6f);
                    break;
            }
        }

        void ConnectSelected()
        {
            _selected = _selected ? _selected : Selection.activeGameObject;
            if (!_selected)
            {
                Log("오류: Hierarchy에서 모델을 먼저 선택하세요.");
                return;
            }

            Undo.SetCurrentGroupName("VARCO Auto Connector");
            int undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterFullObjectHierarchyUndo(_selected, "Connect Selected");

            EnsureSystemObjects();

            switch (_role)
            {
                case Role.Player: ConnectPlayer(_selected, platform: false); break;
                case Role.PlatformPlayer: ConnectPlayer(_selected, platform: true); break;
                case Role.Enemy: ConnectEnemy(_selected); break;
                case Role.ItemPickup: ConnectItem(_selected); break;
                case Role.HealthPickup: ConnectHealthPickup(_selected); break;
                case Role.Goal: ConnectGoal(_selected); break;
                case Role.Door: ConnectDoor(_selected); break;
                case Role.PressurePlate: ConnectPressurePlate(_selected); break;
                case Role.HazardZone: ConnectHazard(_selected); break;
                case Role.MovingPlatform: ConnectMovingPlatform(_selected); break;
                case Role.MovableBox: ConnectMovableBox(_selected); break;
                case Role.Checkpoint: ConnectCheckpoint(_selected); break;
                case Role.ArenaCover: ConnectArenaCover(_selected); break;
            }

            ApplyAnimatorIfNeeded(_selected, _role);
            EditorUtility.SetDirty(_selected);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
        }

        void ConnectPlayer(GameObject go, bool platform)
        {
            EnsureTag("Player");
            go.tag = "Player";

            if (platform)
            {
                DestroyImmediateIfExists<VWS.PlayerController_ThirdPerson>(go);
                DestroyImmediateIfExists<Rigidbody>(go);
                DestroyImmediateIfExists<CapsuleCollider>(go);
                var cc = EnsureCharacterController(go);
                FitCharacterControllerToRenderers(go, cc);
                EnsureComponent<VWS.PlayerController_Platform>(go);
            }
            else
            {
                DestroyImmediateIfExists<VWS.PlayerController_Platform>(go);
                ApplyThirdPersonPhysics(go);
                var ctrl = EnsureComponent<VWS.PlayerController_ThirdPerson>(go);
                ctrl.modelRoot = FindVisualRoot(go);
                if (ctrl.modelRoot == go.transform)
                    ctrl.modelRoot = null;
                ctrl.moveSpeed = 4.2f;
                ctrl.turnSpeed = 14f;
                ctrl.gravity = -28f;
                ctrl.useCameraSpace = !_moveInFacingDirectionForPlayer;
                ctrl.moveInFacingDirection = _moveInFacingDirectionForPlayer;
                ctrl.facingTurnSpeed = 180f;
                ctrl.applyRootMotionFromAnimation = false;
                ctrl.groundCheckExtra = 0.14f;
                ctrl.groundedDownVel = -0.35f;
                var attack = EnsureComponent<VWS.PlayerAttack>(go);
                attack.range = 2f;
                attack.radius = 0.75f;
                attack.keyboardAttackKey = KeyCode.None;
                attack.lockMovementDuringAttack = true;
                attack.movementLockTime = 0.55f;
            }

            EnsureComponent<VWS.PlayerHealth>(go);
            EnsureComponent<VWS.CollectibleCounter>(go);
            var footstep = EnsureComponent<VWS.PlayerFootstepSound>(go);
            if (!footstep.soundRegistry)
                footstep.soundRegistry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(DefaultSoundRegistryPath);
            EnsureCombatHud();
            ConnectMainCamera(go.transform, _cameraViewPreset);
            Log($"완료: {go.name}을(를) {(platform ? "플랫폼 플레이어" : "3인칭 플레이어")}로 연결했습니다.");
        }

        void ConnectEnemy(GameObject go)
        {
            EnsureTag("Enemy");
            go.tag = "Enemy";
            var agent = EnsureComponent<NavMeshAgent>(go);
            agent.stoppingDistance = 1.45f;
            agent.speed = Mathf.Max(agent.speed, 3.5f);
            FitNavMeshAgentToRenderers(go, agent);
            ConfigureNavMeshAlignment(go, agent);
            AlignTopLevelVisualsToRootFloor(go, 0.025f);
            FitNavMeshAgentToRenderers(go, agent);
            var health = EnsureComponent<VWS.EnemyHealth>(go);
            health.healthDropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultHealthDropPrefabPath);
            health.healthDropChance = 1f;
            health.healthDropHealAmount = 25;
            var ai = EnsureComponent<VWS.EnemyAI_NavMesh>(go);
            ai.stopDistance = 1.55f;
            ai.attackReach = 1.7f;
            ai.contactDamage = 2;
            ai.attackSpeed = 0.55f;
            ai.attackAnimationSpeed = 1f;
            ai.contactInterval = 1.8f;
            EnsureCapsuleCollider(go, trigger: false);
            EnsureCombatHud();

            GameObject waveObject = go;
            if (_saveEnemyAsPrefab)
                waveObject = SaveAsPrefab(go, AutoPrefabFolder);

            if (_connectEnemyToWave)
                ConnectEnemyToWave(waveObject);

            Log($"완료: {go.name}을(를) 적/좀비로 연결했습니다.");
        }

        static void AlignTopLevelVisualsToRootFloor(GameObject go, float clearance)
        {
            if (!go || go.transform.childCount == 0)
                return;
            if (!TryGetWorldRendererBounds(go, out var bounds))
                return;

            var targetBottom = go.transform.position.y + Mathf.Max(0f, clearance);
            var deltaY = targetBottom - bounds.min.y;
            if (Mathf.Abs(deltaY) <= 0.025f)
                return;

            var worldDelta = Vector3.up * deltaY;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i);
                if (!child || !ChildContainsVisibleRenderer(child))
                    continue;

                Undo.RecordObject(child, "VARCO visual floor align");
                child.localPosition += child.parent.InverseTransformVector(worldDelta);
                EditorUtility.SetDirty(child);
            }
        }

        static bool ChildContainsVisibleRenderer(Transform child)
        {
            var renderers = child.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer && !(renderer is ParticleSystemRenderer))
                    return true;
            }

            return false;
        }

        static bool TryGetWorldRendererBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            if (!go)
                return false;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var hasAny = false;
            foreach (var renderer in renderers)
            {
                if (!renderer || renderer is ParticleSystemRenderer)
                    continue;

                var rendererBounds = renderer.bounds;
                if (rendererBounds.size.sqrMagnitude <= 0.0001f)
                    continue;

                if (!hasAny)
                {
                    bounds = rendererBounds;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasAny;
        }

        void ConnectItem(GameObject go)
        {
            EnsureTriggerCollider(go);
            EnsureComponent<VWS.ItemPickup>(go);
            EnsurePlayerCounter();
            Log($"완료: {go.name}을(를) 수집 아이템으로 연결했습니다.");
        }

        void ConnectHealthPickup(GameObject go)
        {
            RemoveAnimators(go);
            EnsureTriggerCollider(go);
            var hp = EnsureComponent<VWS.HealthPickup>(go);
            hp.healAmount = _healAmount;
            Log($"완료: {go.name}을(를) 회복 아이템으로 연결했습니다. 회복량: {_healAmount}");
        }

        void ConnectGoal(GameObject go)
        {
            EnsureTriggerCollider(go);
            var goal = EnsureComponent<VWS.GoalTrigger>(go);
            goal.requiredItems = _requiredItems;
            if (_requiredItems > 0) EnsurePlayerCounter();
            Log($"완료: {go.name}을(를) 목표 지점으로 연결했습니다. 필요 아이템: {_requiredItems}");
        }

        void ConnectDoor(GameObject go)
        {
            EnsureBoxCollider(go, trigger: false);
            var door = EnsureComponent<VWS.DoorController>(go);
            ConnectNearestPlateToDoor(door);
            Log($"완료: {go.name}을(를) 문으로 연결했습니다.");
        }

        void ConnectPressurePlate(GameObject go)
        {
            EnsureTriggerCollider(go);
            var plate = EnsureComponent<VWS.PressurePlate>(go);
            var door = FindObjectsByType<VWS.DoorController>(FindObjectsSortMode.None)
                .OrderBy(d => Vector3.Distance(d.transform.position, go.transform.position))
                .FirstOrDefault();
            if (door)
                plate.targets = new[] { door };
            Log(door ? $"완료: {go.name} 압력판을 {door.name} 문에 연결했습니다." : $"완료: {go.name}을(를) 압력판으로 연결했습니다. 문은 직접 지정이 필요합니다.");
        }

        void ConnectHazard(GameObject go)
        {
            EnsureTriggerCollider(go);
            var hazard = EnsureComponent<VWS.HazardZone>(go);
            hazard.damagePerSecond = _hazardDps;
            Log($"완료: {go.name}을(를) 위험 구역으로 연결했습니다. 초당 피해: {_hazardDps}");
        }

        void ConnectMovingPlatform(GameObject go)
        {
            EnsureBoxCollider(go, trigger: false);
            var root = new GameObject($"{go.name}_Path");
            Undo.RegisterCreatedObjectUndo(root, "Create Moving Platform Path");
            root.transform.position = go.transform.position;

            var a = new GameObject("PointA");
            var b = new GameObject("PointB");
            Undo.RegisterCreatedObjectUndo(a, "Create PointA");
            Undo.RegisterCreatedObjectUndo(b, "Create PointB");
            a.transform.SetParent(root.transform);
            b.transform.SetParent(root.transform);
            a.transform.position = go.transform.position;
            b.transform.position = go.transform.position + Vector3.right * _movingPlatformDistance;

            var mp = EnsureComponent<VWS.MovingPlatform>(go);
            mp.a = a.transform;
            mp.b = b.transform;
            mp.speed = _movingPlatformSpeed;
            Log($"완료: {go.name}을(를) 이동 발판으로 연결했습니다.");
        }

        void ConnectMovableBox(GameObject go)
        {
            EnsureBoxCollider(go, trigger: false);
            var rb = EnsureComponent<Rigidbody>(go);
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            EnsureComponent<VWS.MovableBox>(go);
            Log($"완료: {go.name}을(를) 밀 수 있는 상자로 연결했습니다.");
        }

        void ConnectCheckpoint(GameObject go)
        {
            EnsureTriggerCollider(go);
            EnsureComponent<VWS.Checkpoint>(go);
            Log($"완료: {go.name}을(를) 체크포인트로 연결했습니다.");
        }

        void ConnectArenaCover(GameObject go)
        {
            EnsureBoxCollider(go, trigger: false);
            go.isStatic = true;
            Log($"완료: {go.name}을(를) 환경 소품/엄폐물로 연결했습니다.");
        }

        void EnsureSystemObjects()
        {
            var gm = FindFirstObjectByType<VWS.GameManager>();
            if (!gm)
            {
                var root = new GameObject("VW_Bootstrap");
                Undo.RegisterCreatedObjectUndo(root, "Create VW_Bootstrap");
                root.AddComponent<VWS.GameManager>();
                root.AddComponent<VWS.SceneBootstrap>();
                Log("완료: VW_Bootstrap을 생성했습니다.");
            }
            EnsureCombatHud();
        }

        void EnsureCombatHud()
        {
            if (FindFirstObjectByType<VWS.CombatHealthUI>(FindObjectsInactive.Include)) return;
            var go = new GameObject("VW_CombatHUD");
            Undo.RegisterCreatedObjectUndo(go, "Create VW_CombatHUD");
            go.AddComponent<VWS.CombatHealthUI>();
            Log("완료: VW_CombatHUD를 생성했습니다.");
        }

        void EnsurePlayerCounter()
        {
            var player = GameObject.FindWithTag("Player");
            if (player && !player.GetComponent<VWS.CollectibleCounter>())
            {
                Undo.AddComponent<VWS.CollectibleCounter>(player);
                Log("완료: 플레이어에 수집 카운터를 추가했습니다.");
            }
        }

        void ConnectMainCamera(Transform target, CameraViewPreset preset)
        {
            var camera = EnsureMainCamera();
            var cam = camera.GetComponent<VWS.ThirdPersonCamera>();
            if (!cam) cam = Undo.AddComponent<VWS.ThirdPersonCamera>(camera.gameObject);
            var so = new SerializedObject(cam);
            ApplyCameraPreset(camera.transform, so, target, preset);
            so.ApplyModifiedProperties();
        }

        void ApplyCameraPreset(Transform cameraTransform, SerializedObject so, Transform target, CameraViewPreset preset)
        {
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("minDistance").floatValue = 1.2f;
            so.FindProperty("sensX").floatValue = 2f;
            so.FindProperty("sensY").floatValue = 1.5f;
            so.FindProperty("useWallClipping").boolValue = true;
            so.FindProperty("wallSkin").floatValue = 0.28f;
            so.FindProperty("collisionRadius").floatValue = 0.25f;
            so.FindProperty("positionSharpness").floatValue = 18f;
            so.FindProperty("pivotSharpness").floatValue = 22f;
            so.FindProperty("distanceSharpness").floatValue = 9f;

            switch (preset)
            {
                case CameraViewPreset.QuarterView:
                    so.FindProperty("pivotOffset").vector3Value = new Vector3(0f, 1.4f, 0f);
                    so.FindProperty("distance").floatValue = 7.2f;
                    so.FindProperty("minPitch").floatValue = 30f;
                    so.FindProperty("maxPitch").floatValue = 55f;
                    so.FindProperty("orbitWhileRightMouseButtonOnly").boolValue = false;
                    cameraTransform.position = target.position + new Vector3(0f, 5.5f, -5.5f);
                    cameraTransform.rotation = Quaternion.Euler(42f, 0f, 0f);
                    break;
                case CameraViewPreset.TopDown:
                    so.FindProperty("pivotOffset").vector3Value = new Vector3(0f, 1f, 0f);
                    so.FindProperty("distance").floatValue = 9f;
                    so.FindProperty("minPitch").floatValue = 65f;
                    so.FindProperty("maxPitch").floatValue = 80f;
                    so.FindProperty("orbitWhileRightMouseButtonOnly").boolValue = true;
                    cameraTransform.position = target.position + new Vector3(0f, 9f, -1.5f);
                    cameraTransform.rotation = Quaternion.Euler(78f, 0f, 0f);
                    break;
                case CameraViewPreset.SideView:
                    so.FindProperty("pivotOffset").vector3Value = new Vector3(0f, 1.4f, 0f);
                    so.FindProperty("distance").floatValue = 8f;
                    so.FindProperty("minPitch").floatValue = 0f;
                    so.FindProperty("maxPitch").floatValue = 20f;
                    so.FindProperty("orbitWhileRightMouseButtonOnly").boolValue = true;
                    cameraTransform.position = target.position + new Vector3(0f, 2.2f, -8f);
                    cameraTransform.rotation = Quaternion.Euler(12f, 0f, 0f);
                    break;
                default:
                    so.FindProperty("pivotOffset").vector3Value = new Vector3(0f, 1.25f, 0f);
                    so.FindProperty("distance").floatValue = 5.2f;
                    so.FindProperty("minPitch").floatValue = -5f;
                    so.FindProperty("maxPitch").floatValue = 45f;
                    so.FindProperty("orbitWhileRightMouseButtonOnly").boolValue = false;
                    cameraTransform.position = target.position + new Vector3(0f, 3f, -5.2f);
                    cameraTransform.rotation = Quaternion.Euler(18f, 0f, 0f);
                    break;
            }
        }

        public static CameraViewPreset DrawCameraViewPopup(string label, CameraViewPreset value)
        {
            return (CameraViewPreset)EditorGUILayout.Popup(new GUIContent(label), (int)value, CameraViewLabels);
        }

        Camera EnsureMainCamera()
        {
            if (Camera.main) return Camera.main;

            var existing = FindFirstObjectByType<Camera>();
            if (existing)
            {
                Undo.RecordObject(existing.gameObject, "Set Main Camera");
                existing.tag = "MainCamera";
                if (!existing.GetComponent<AudioListener>())
                    Undo.AddComponent<AudioListener>(existing.gameObject);
                Log($"완료: {existing.name}을(를) 메인 카메라로 설정했습니다.");
                return existing;
            }

            var go = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(go, "Create Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 3f, -6f);
            go.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            var camera = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            Log("완료: Main Camera를 생성했습니다.");
            return camera;
        }

        void ConnectEnemyToWave(GameObject prefab)
        {
            var wm = EnsureWaveManager();

            var so = new SerializedObject(wm);
            var waves = so.FindProperty("waves");
            if (waves.arraySize <= _waveIndex)
                waves.arraySize = _waveIndex + 1;

            var wave = waves.GetArrayElementAtIndex(_waveIndex);
            wave.FindPropertyRelative("enemyPrefab").objectReferenceValue = prefab;
            if (wave.FindPropertyRelative("enemyCount").intValue <= 0)
                wave.FindPropertyRelative("enemyCount").intValue = 3;
            if (wave.FindPropertyRelative("spawnInterval").floatValue <= 0f)
                wave.FindPropertyRelative("spawnInterval").floatValue = 0.6f;
            so.ApplyModifiedProperties();
            Log($"완료: 웨이브 {_waveIndex}번에 {prefab.name} 적 프리팹을 연결했습니다.");
        }

        VWS.WaveManager EnsureWaveManager()
        {
            var wm = FindFirstObjectByType<VWS.WaveManager>();
            if (wm) return wm;

            var root = new GameObject("FB_EnemyWave");
            Undo.RegisterCreatedObjectUndo(root, "Create FB_EnemyWave");
            root.transform.position = new Vector3(-6f, 0f, 0f);

            var areaGo = new GameObject("SpawnArea");
            Undo.RegisterCreatedObjectUndo(areaGo, "Create SpawnArea");
            areaGo.transform.SetParent(root.transform, false);
            var areaBox = Undo.AddComponent<BoxCollider>(areaGo);
            areaBox.size = new Vector3(8f, 1f, 8f);
            areaBox.isTrigger = true;

            wm = Undo.AddComponent<VWS.WaveManager>(root);
            var so = new SerializedObject(wm);
            so.FindProperty("delayBetweenWaves").floatValue = 1.5f;
            so.FindProperty("clearWhenAllWavesCleared").boolValue = true;
            so.FindProperty("randomSpawnArea").objectReferenceValue = areaBox;
            so.FindProperty("waves").arraySize = Mathf.Max(1, _waveIndex + 1);
            for (int i = 0; i < so.FindProperty("waves").arraySize; i++)
            {
                var wave = so.FindProperty("waves").GetArrayElementAtIndex(i);
                wave.FindPropertyRelative("enemyCount").intValue = 3;
                wave.FindPropertyRelative("spawnInterval").floatValue = 0.8f;
            }
            so.ApplyModifiedProperties();

            Log("완료: 적 웨이브, 웨이브 매니저, 스폰 구역을 생성했습니다.");
            return wm;
        }

        void ConnectNearestPlateToDoor(VWS.DoorController door)
        {
            var plate = FindObjectsByType<VWS.PressurePlate>(FindObjectsSortMode.None)
                .OrderBy(p => Vector3.Distance(p.transform.position, door.transform.position))
                .FirstOrDefault();
            if (!plate) return;
            plate.targets = new[] { door };
            EditorUtility.SetDirty(plate);
            Log($"완료: 가장 가까운 압력판을 {door.name} 문에 연결했습니다.");
        }

        void ApplyAnimatorIfNeeded(GameObject go, Role role)
        {
            if (!_animatorController) return;
            if (!RoleUsesAnimator(role)) return;
            var animator = go.GetComponentInChildren<Animator>(true);
            if (!animator) animator = Undo.AddComponent<Animator>(go);
            animator.runtimeAnimatorController = _animatorController;
            animator.applyRootMotion = false;
            Log($"완료: 애니메이션 컨트롤러를 연결했습니다: {_animatorController.name}");
        }

        static bool RoleUsesAnimator(Role role)
        {
            return role == Role.Player || role == Role.PlatformPlayer || role == Role.Enemy;
        }

        static void RemoveAnimators(GameObject go)
        {
            if (!go) return;
            var animators = go.GetComponentsInChildren<Animator>(true);
            foreach (var animator in animators)
            {
                if (animator)
                    Undo.DestroyObjectImmediate(animator);
            }
        }

        GameObject SaveAsPrefab(GameObject go, string folder)
        {
            EnsureFolder(folder);
            string path = $"{folder}/{SanitizeFileName(go.name)}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log($"완료: 프리팹을 저장했습니다: {path}");
            return prefab ? prefab : go;
        }

        static Transform FindVisualRoot(GameObject go)
        {
            var modelSlot = go.transform.Find("ModelSlot");
            if (modelSlot) return modelSlot;
            var animator = go.GetComponentInChildren<Animator>(true);
            return animator ? animator.transform : go.transform;
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            return comp ? comp : Undo.AddComponent<T>(go);
        }

        static CharacterController EnsureCharacterController(GameObject go)
        {
            var controller = go.GetComponent<CharacterController>();
            if (controller)
                return controller;

            var originalScale = go.transform.localScale;
            var adjustedScale = new Vector3(
                Mathf.Abs(originalScale.x) < 1f ? Mathf.Sign(originalScale.x == 0f ? 1f : originalScale.x) : originalScale.x,
                Mathf.Abs(originalScale.y) < 1f ? Mathf.Sign(originalScale.y == 0f ? 1f : originalScale.y) : originalScale.y,
                Mathf.Abs(originalScale.z) < 1f ? Mathf.Sign(originalScale.z == 0f ? 1f : originalScale.z) : originalScale.z);
            var changedScale = adjustedScale != originalScale;

            if (changedScale)
                go.transform.localScale = adjustedScale;

            controller = Undo.AddComponent<CharacterController>(go);
            controller.stepOffset = 0.01f;

            if (changedScale)
                go.transform.localScale = originalScale;

            ClampCharacterControllerStepOffset(controller);
            return controller;
        }

        static void DestroyImmediateIfExists<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp) Undo.DestroyObjectImmediate(comp);
        }

        static Collider EnsureTriggerCollider(GameObject go)
        {
            var trigger = go.GetComponents<Collider>().FirstOrDefault(col => col && col.isTrigger && !(col is MeshCollider));
            if (trigger)
                return trigger;

            var reusable = go.GetComponents<Collider>().FirstOrDefault(col => col && !(col is MeshCollider));
            if (reusable)
            {
                reusable.isTrigger = true;
                return reusable;
            }

            return EnsureBoxCollider(go, true);
        }

        static BoxCollider EnsureBoxCollider(GameObject go, bool trigger)
        {
            var box = go.GetComponent<BoxCollider>();
            if (!box) box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = trigger;
            FitBoxToRenderers(go, box);
            return box;
        }

        static CapsuleCollider EnsureCapsuleCollider(GameObject go, bool trigger)
        {
            var cap = go.GetComponent<CapsuleCollider>();
            if (!cap) cap = Undo.AddComponent<CapsuleCollider>(go);
            FitCapsuleToRenderers(go, cap);
            cap.isTrigger = trigger;
            return cap;
        }

        static void FitCharacterControllerToRenderers(GameObject go, CharacterController cc)
        {
            if (!TryGetLocalRendererBounds(go, out var center, out var size))
            {
                cc.height = 1.6f;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, 0.8f, 0f);
                ClampCharacterControllerStepOffset(cc);
                return;
            }

            float height = Mathf.Max(size.y * 0.92f, 1.6f);
            float radius = Mathf.Max(Mathf.Max(size.x, size.z) * 0.28f, 0.3f);
            radius = Mathf.Min(radius, height * 0.45f);
            cc.center = center;
            cc.height = height;
            cc.radius = radius;
            ClampCharacterControllerStepOffset(cc);
        }

        static void ClampCharacterControllerStepOffset(CharacterController cc)
        {
            if (!cc)
                return;

            var scale = cc.transform.lossyScale;
            var verticalScale = Mathf.Max(0.001f, Mathf.Abs(scale.y));
            var radiusScale = Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            var maxScaledHeight = cc.height * verticalScale + cc.radius * radiusScale * 2f;
            cc.stepOffset = Mathf.Clamp(cc.stepOffset, 0.005f, Mathf.Max(0.005f, maxScaledHeight - 0.005f));
            cc.slopeLimit = Mathf.Clamp(cc.slopeLimit, 1f, 89f);
            cc.minMoveDistance = 0f;
        }

        static void FitNavMeshAgentToRenderers(GameObject go, NavMeshAgent agent)
        {
            if (!agent) return;
            if (!TryGetLocalRendererBounds(go, out var center, out var size))
            {
                agent.height = 1.7f;
                agent.radius = 0.35f;
                agent.baseOffset = 0f;
                return;
            }

            float height = Mathf.Max(size.y * 0.9f, 1.2f);
            float radius = Mathf.Max(Mathf.Max(size.x, size.z) * 0.22f, 0.25f);
            radius = Mathf.Min(radius, height * 0.45f);
            float bottom = center.y - size.y * 0.5f;

            agent.height = height;
            agent.radius = radius;
            agent.baseOffset = Mathf.Abs(bottom) < 0.08f ? 0f : bottom;
        }

        static void ConfigureNavMeshAlignment(GameObject go, NavMeshAgent agent)
        {
            if (!go)
                return;

            var align = EnsureComponent<VWS.NavMeshEditPlayAlign>(go);
            align.sampleMaxDistance = Mathf.Max(align.sampleMaxDistance, 20f);
            align.alignInPlayMode = true;
            align.alignInEditMode = false;
            EditorUtility.SetDirty(align);

            if (!agent)
                agent = go.GetComponent<NavMeshAgent>();

            if (!NavMesh.SamplePosition(go.transform.position, out var hit, align.sampleMaxDistance, NavMesh.AllAreas))
                return;

            Undo.RecordObject(go.transform, "VARCO enemy NavMesh align");
            if (agent && agent.enabled && agent.Warp(hit.position))
            {
                EditorUtility.SetDirty(agent);
                return;
            }

            var wasEnabled = agent && agent.enabled;
            if (agent)
                agent.enabled = false;
            go.transform.position = hit.position;
            if (agent)
            {
                agent.enabled = wasEnabled;
                if (wasEnabled)
                    agent.Warp(hit.position);
                EditorUtility.SetDirty(agent);
            }
            EditorUtility.SetDirty(go.transform);
        }

        static void FitBoxToRenderers(GameObject go, BoxCollider box)
        {
            if (!TryGetLocalRendererBounds(go, out var center, out var size))
            {
                box.center = Vector3.zero;
                box.size = Vector3.one;
                return;
            }

            box.center = center;
            box.size = new Vector3(
                Mathf.Max(size.x, 0.2f),
                Mathf.Max(size.y, 0.2f),
                Mathf.Max(size.z, 0.2f));
        }

        static bool TryGetLocalRendererBounds(GameObject go, out Vector3 center, out Vector3 size)
        {
            center = Vector3.zero;
            size = Vector3.one;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;

            bool hasAny = false;
            Vector3 localMin = Vector3.zero;
            Vector3 localMax = Vector3.zero;
            foreach (var renderer in renderers)
            {
                if (!renderer || renderer is ParticleSystemRenderer) continue;
                if (!TryGetStableWorldBounds(renderer, out var b))
                    b = renderer.bounds;
                Vector3 bMin = b.min;
                Vector3 bMax = b.max;
                Vector3[] corners =
                {
                    new Vector3(bMin.x, bMin.y, bMin.z),
                    new Vector3(bMin.x, bMin.y, bMax.z),
                    new Vector3(bMin.x, bMax.y, bMin.z),
                    new Vector3(bMin.x, bMax.y, bMax.z),
                    new Vector3(bMax.x, bMin.y, bMin.z),
                    new Vector3(bMax.x, bMin.y, bMax.z),
                    new Vector3(bMax.x, bMax.y, bMin.z),
                    new Vector3(bMax.x, bMax.y, bMax.z)
                };

                foreach (var corner in corners)
                {
                    var local = go.transform.InverseTransformPoint(corner);
                    if (!hasAny)
                    {
                        localMin = local;
                        localMax = local;
                        hasAny = true;
                    }
                    else
                    {
                        localMin = Vector3.Min(localMin, local);
                        localMax = Vector3.Max(localMax, local);
                    }
                }
            }

            if (!hasAny) return false;
            center = (localMin + localMax) * 0.5f;
            size = localMax - localMin;
            return true;
        }

        static bool TryGetStableWorldBounds(Renderer renderer, out Bounds worldBounds)
        {
            worldBounds = default;
            Bounds localBounds;
            Transform boundsTransform = renderer.transform;

            if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh)
                localBounds = skinned.sharedMesh.bounds;
            else if (renderer.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh)
                localBounds = meshFilter.sharedMesh.bounds;
            else
                return false;

            var min = localBounds.min;
            var max = localBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            worldBounds = new Bounds(boundsTransform.TransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
                worldBounds.Encapsulate(boundsTransform.TransformPoint(corners[i]));
            return true;
        }

        static void ApplyThirdPersonPhysics(GameObject go)
        {
            if (!go) return;
            DestroyImmediateIfExists<CharacterController>(go);

            var capsule = EnsureComponent<CapsuleCollider>(go);
            FitCapsuleToRenderers(go, capsule);

            var rb = EnsureComponent<Rigidbody>(go);
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        }

        static void FitCapsuleToRenderers(GameObject go, CapsuleCollider capsule)
        {
            if (!go || !capsule) return;
            if (!TryGetLocalRendererBounds(go, out var center, out var size))
            {
                capsule.height = 1.6f;
                capsule.radius = 0.3f;
                capsule.center = new Vector3(0f, 0.8f, 0f);
                capsule.direction = 1;
                capsule.isTrigger = false;
                return;
            }

            float height = Mathf.Max(size.y * 0.88f, 1.6f);
            float radius = Mathf.Max(Mathf.Max(size.x, size.z) * 0.22f, 0.28f);
            radius = Mathf.Min(radius, height * 0.45f);

            capsule.direction = 1;
            float bottom = center.y - size.y * 0.5f;
            capsule.center = new Vector3(center.x, bottom + height * 0.5f + size.y * 0.03f, center.z);
            capsule.height = height;
            capsule.radius = radius;
            capsule.isTrigger = false;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static void EnsureTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || IsBuiltInUnityTag(tag))
                return;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var tags = so.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
            tags.arraySize++;
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            so.ApplyModifiedProperties();
        }

        static bool IsBuiltInUnityTag(string tag)
        {
            switch (tag)
            {
                case "Untagged":
                case "Respawn":
                case "Finish":
                case "EditorOnly":
                case "MainCamera":
                case "Player":
                case "GameController":
                    return true;
                default:
                    return false;
            }
        }

        static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "AutoConnected" : name;
        }

        void Log(string message)
        {
            _log += message + "\n";
            Debug.Log("[VARCO 선택 모델 기능 연결] " + message);
            RequestRepaint();
        }

        void RequestRepaint(bool immediate = false)
        {
            if (repaintGate == null)
                repaintGate = new VARCOEditorRepaintGate(this);
            repaintGate.Request(immediate);
        }
    }
}
#endif
