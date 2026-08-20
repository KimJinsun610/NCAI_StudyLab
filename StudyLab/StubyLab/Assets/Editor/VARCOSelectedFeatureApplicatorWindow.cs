#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using VWS = VARCO_Workshop;

namespace VARCO_Workshop.Editor
{
    public class VARCOSelectedFeatureApplicatorWindow : EditorWindow
    {
        public enum FeatureKind
        {
            Auto,
            PlayerCore,
            PlayerAttack,
            EnemyCore,
            EnemyAttack,
            DoorOpen,
            ItemPickup,
            HealthPickup,
            HazardZone,
            Checkpoint,
            Goal
        }

        static readonly GUIContent[] FeatureLabels =
        {
            new GUIContent("이름으로 자동 판단"),
            new GUIContent("플레이어 기본 기능"),
            new GUIContent("플레이어 공격 기능"),
            new GUIContent("적 기본 기능"),
            new GUIContent("적 공격 기능"),
            new GUIContent("문 열림 기능"),
            new GUIContent("수집 아이템 기능"),
            new GUIContent("회복 아이템 기능"),
            new GUIContent("위험 구역 기능"),
            new GUIContent("체크포인트 기능"),
            new GUIContent("목표 지점 기능")
        };

        const string GeneratedPrefabFolder = "Assets/Prefabs/VARCO_FunctionApplied";
        const string DoorTriggerName = "VARCO_DoorOpenTrigger";

        readonly List<string> logLines = new List<string>();
        Vector2 scroll;
        FeatureKind feature = FeatureKind.Auto;
        int playerDamage = 15;
        int enemyDamage = 2;
        int healthAmount = 25;
        int hazardDamagePerSecond = 15;
        int requiredItems;

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/블록코딩/선택 프리팹에 기능 추가", priority = -9)]
        public static void Open()
        {
            var window = GetWindow<VARCOSelectedFeatureApplicatorWindow>("선택 프리팹 기능 추가");
            window.minSize = new Vector2(460f, 560f);
            window.Focus();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(8f);

            EditorGUILayout.LabelField("선택 프리팹 기능 추가", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "프로젝트 창의 프리팹 또는 하이어라키의 오브젝트를 선택한 뒤 필요한 게임 기능만 추가합니다. 모델, 머티리얼, 배치 상태는 유지됩니다.",
                MessageType.Info);

            var selected = GetSelectedTargets().ToList();
            EditorGUILayout.LabelField("선택 항목", selected.Count == 0 ? "없음" : $"{selected.Count}개");

            feature = (FeatureKind)EditorGUILayout.Popup(new GUIContent("추가할 기능"), (int)feature, FeatureLabels);
            DrawFeatureOptions();

            GUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(selected.Count == 0))
            {
                if (GUILayout.Button("선택 항목에 기능 추가", GUILayout.Height(36f)))
                    ApplyToCurrentSelection();
            }

            if (GUILayout.Button("로그 지우기", GUILayout.Height(24f)))
                logLines.Clear();

            GUILayout.Space(10f);
            DrawLog();

            EditorGUILayout.EndScrollView();
        }

        void DrawFeatureOptions()
        {
            switch (feature)
            {
                case FeatureKind.PlayerAttack:
                    playerDamage = EditorGUILayout.IntSlider("플레이어 공격력", playerDamage, 1, 100);
                    break;

                case FeatureKind.EnemyAttack:
                    enemyDamage = EditorGUILayout.IntSlider("적 공격력", enemyDamage, 1, 100);
                    break;

                case FeatureKind.HealthPickup:
                    healthAmount = EditorGUILayout.IntSlider("회복량", healthAmount, 1, 100);
                    break;

                case FeatureKind.HazardZone:
                    hazardDamagePerSecond = EditorGUILayout.IntSlider("초당 피해", hazardDamagePerSecond, 1, 100);
                    break;

                case FeatureKind.Goal:
                    requiredItems = EditorGUILayout.IntSlider("필요 수집 아이템 수", requiredItems, 0, 20);
                    break;
            }
        }

        void DrawLog()
        {
            if (logLines.Count == 0)
            {
                EditorGUILayout.HelpBox("아직 실행 내역이 없습니다.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("실행 내역", EditorStyles.boldLabel);
            foreach (var line in logLines.TakeLast(14))
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
        }

        void ApplyToCurrentSelection()
        {
            logLines.Clear();
            var options = CreateOptions();
            var targets = GetSelectedTargets().ToList();
            foreach (var target in targets)
                ApplySelectionObject(target, feature, options, logLines);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        FeatureOptions CreateOptions()
        {
            return new FeatureOptions
            {
                playerDamage = playerDamage,
                enemyDamage = enemyDamage,
                healthAmount = healthAmount,
                hazardDamagePerSecond = hazardDamagePerSecond,
                requiredItems = requiredItems
            };
        }

        static IEnumerable<Object> GetSelectedTargets()
        {
            foreach (var selected in Selection.objects)
            {
                if (selected is GameObject || selected is Component)
                    yield return selected;
            }
        }

        public static void ApplySelectionObject(Object selected, FeatureKind requestedFeature, FeatureOptions options, List<string> log)
        {
            if (!TryGetGameObject(selected, out var selectedGameObject))
            {
                log.Add($"건너뜀: {selected.name}은 게임 오브젝트가 아닙니다.");
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(selectedGameObject);
            if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.Contains(selectedGameObject))
            {
                if (Path.GetExtension(assetPath).Equals(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyToPrefabAsset(assetPath, requestedFeature, options, log);
                    return;
                }

                CreateFunctionAppliedPrefab(selectedGameObject, requestedFeature, options, log);
                return;
            }

            var featureToApply = ResolveFeature(selectedGameObject, requestedFeature);
            if (featureToApply == FeatureKind.Auto)
            {
                log.Add($"확인 필요: {selectedGameObject.name}에서 추가할 기능을 판단하지 못했습니다.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(selectedGameObject, "VARCO 기능 추가");
            ApplyFeature(selectedGameObject, featureToApply, options, useUndo: true);
            MarkSceneObjectDirty(selectedGameObject);
            log.Add($"완료: 씬 오브젝트 {selectedGameObject.name}에 {FeatureName(featureToApply)} 적용");
        }

        public static void ApplyFeature(GameObject target, FeatureKind featureToApply, FeatureOptions options, bool useUndo)
        {
            if (!target)
                return;

            switch (featureToApply)
            {
                case FeatureKind.PlayerCore:
                    ApplyPlayerCore(target, options, useUndo);
                    break;

                case FeatureKind.PlayerAttack:
                    ApplyPlayerAttack(target, options, useUndo);
                    break;

                case FeatureKind.EnemyCore:
                    ApplyEnemyCore(target, options, useUndo);
                    break;

                case FeatureKind.EnemyAttack:
                    ApplyEnemyAttack(target, options, useUndo);
                    break;

                case FeatureKind.DoorOpen:
                    ApplyDoorOpen(target, useUndo);
                    break;

                case FeatureKind.ItemPickup:
                    ApplyItemPickup(target, useUndo);
                    break;

                case FeatureKind.HealthPickup:
                    ApplyHealthPickup(target, options, useUndo);
                    break;

                case FeatureKind.HazardZone:
                    ApplyHazardZone(target, options, useUndo);
                    break;

                case FeatureKind.Checkpoint:
                    ApplyCheckpoint(target, useUndo);
                    break;

                case FeatureKind.Goal:
                    ApplyGoal(target, options, useUndo);
                    break;
            }
        }

        public static string GetRegisteredVarcoMenusForVerification()
        {
            var menuItems = new SortedSet<string>(StringComparer.Ordinal);
            var menuAttributeType = typeof(MenuItem);
            var menuField = menuAttributeType.GetField("menuItem", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var menuProperty = menuAttributeType.GetProperty("menuItem", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types = null;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                if (types == null)
                    continue;

                foreach (var type in types)
                {
                    if (type == null)
                        continue;

                    var methods = type.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    foreach (var method in methods)
                    {
                        foreach (var attribute in method.GetCustomAttributes(menuAttributeType, false))
                        {
                            var rawMenu = menuField != null
                                ? menuField.GetValue(attribute)
                                : menuProperty != null
                                    ? menuProperty.GetValue(attribute, null)
                                    : null;

                            var menu = rawMenu as string;
                            if (!string.IsNullOrEmpty(menu) && menu.StartsWith("VARCO/", StringComparison.Ordinal))
                                menuItems.Add(menu);
                        }
                    }
                }
            }

            return "count=" + menuItems.Count + "\n" + string.Join("\n", menuItems.ToArray());
        }

        public static string RunComponentSmokeTest()
        {
            var results = new List<string>();
            var roots = new List<GameObject>();
            var options = new FeatureOptions
            {
                playerDamage = 21,
                enemyDamage = 2,
                healthAmount = 30,
                hazardDamagePerSecond = 17,
                requiredItems = 2
            };

            try
            {
                var player = new GameObject("VARCO_Smoke_Player");
                roots.Add(player);
                ApplyFeature(player, FeatureKind.PlayerCore, options, useUndo: false);
                AddSmokeResult(results, "player_core",
                    player.GetComponent<VWS.PlayerController_ThirdPerson>() &&
                    player.GetComponent<VWS.PlayerAttack>() &&
                    player.GetComponent<VWS.PlayerHealth>() &&
                    player.GetComponent<Rigidbody>());

                var enemy = new GameObject("VARCO_Smoke_Enemy");
                roots.Add(enemy);
                ApplyFeature(enemy, FeatureKind.EnemyAttack, options, useUndo: false);
                AddSmokeResult(results, "enemy_attack",
                    enemy.GetComponent<VWS.EnemyAI_NavMesh>() &&
                    enemy.GetComponent<VWS.EnemyHealth>() &&
                    enemy.GetComponent<NavMeshAgent>());

                var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
                door.name = "VARCO_Smoke_Door";
                roots.Add(door);
                ApplyFeature(door, FeatureKind.DoorOpen, options, useUndo: false);
                var trigger = door.transform.Find(DoorTriggerName);
                AddSmokeResult(results, "door_open",
                    door.GetComponent<VWS.DoorController>() &&
                    trigger &&
                    trigger.GetComponent<VWS.PressurePlate>() &&
                    HasTriggerBox(trigger.gameObject));

                var item = new GameObject("VARCO_Smoke_Item");
                roots.Add(item);
                ApplyFeature(item, FeatureKind.ItemPickup, options, useUndo: false);
                AddSmokeResult(results, "item_pickup",
                    item.GetComponent<VWS.ItemPickup>() &&
                    HasTriggerBox(item));

                var heal = new GameObject("VARCO_Smoke_HealthPickup");
                roots.Add(heal);
                ApplyFeature(heal, FeatureKind.HealthPickup, options, useUndo: false);
                var healPickup = heal.GetComponent<VWS.HealthPickup>();
                AddSmokeResult(results, "health_pickup",
                    healPickup && healPickup.healAmount == options.healthAmount && HasTriggerBox(heal));

                var hazard = new GameObject("VARCO_Smoke_HazardZone");
                roots.Add(hazard);
                ApplyFeature(hazard, FeatureKind.HazardZone, options, useUndo: false);
                var hazardZone = hazard.GetComponent<VWS.HazardZone>();
                AddSmokeResult(results, "hazard_zone",
                    hazardZone && hazardZone.damagePerSecond == options.hazardDamagePerSecond && HasTriggerBox(hazard));

                var checkpoint = new GameObject("VARCO_Smoke_Checkpoint");
                roots.Add(checkpoint);
                ApplyFeature(checkpoint, FeatureKind.Checkpoint, options, useUndo: false);
                AddSmokeResult(results, "checkpoint",
                    checkpoint.GetComponent<VWS.Checkpoint>() && HasTriggerBox(checkpoint));

                var goal = new GameObject("VARCO_Smoke_Goal");
                roots.Add(goal);
                ApplyFeature(goal, FeatureKind.Goal, options, useUndo: false);
                var goalTrigger = goal.GetComponent<VWS.GoalTrigger>();
                AddSmokeResult(results, "goal",
                    goalTrigger && goalTrigger.requiredItems == options.requiredItems && HasTriggerBox(goal));
            }
            catch (Exception exception)
            {
                results.Add("exception=FAIL " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                foreach (var root in roots)
                {
                    if (root)
                        DestroyImmediate(root);
                }
            }

            return string.Join("\n", results);
        }

        static void ApplyToPrefabAsset(string prefabPath, FeatureKind requestedFeature, FeatureOptions options, List<string> log)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var featureToApply = ResolveFeature(root, requestedFeature);
                if (featureToApply == FeatureKind.Auto)
                {
                    log.Add($"확인 필요: {prefabPath}에서 추가할 기능을 판단하지 못했습니다.");
                    return;
                }

                ApplyFeature(root, featureToApply, options, useUndo: false);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                log.Add($"완료: 프리팹 {prefabPath}에 {FeatureName(featureToApply)} 저장");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void CreateFunctionAppliedPrefab(GameObject sourceAsset, FeatureKind requestedFeature, FeatureOptions options, List<string> log)
        {
            EnsureGeneratedPrefabFolder();

            var featureToApply = ResolveFeature(sourceAsset, requestedFeature);
            if (featureToApply == FeatureKind.Auto)
            {
                log.Add($"확인 필요: {sourceAsset.name}에서 추가할 기능을 판단하지 못했습니다.");
                return;
            }

            var instance = PrefabUtility.InstantiatePrefab(sourceAsset) as GameObject;
            if (!instance)
                instance = Instantiate(sourceAsset);

            try
            {
                instance.name = $"{sourceAsset.name}_{FeatureSuffix(featureToApply)}";
                ApplyFeature(instance, featureToApply, options, useUndo: false);
                var prefabPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{GeneratedPrefabFolder}/{SafeFileName(instance.name)}.prefab");
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                log.Add($"완료: 원본 모델을 유지하고 새 프리팹 생성 {prefabPath}");
            }
            finally
            {
                DestroyImmediate(instance);
            }
        }

        static void ApplyPlayerCore(GameObject target, FeatureOptions options, bool useUndo)
        {
            EnsureTagExists("Player");
            target.tag = "Player";
            EnsureCapsuleCollider(target, useUndo);
            var rb = EnsureComponent<Rigidbody>(target, useUndo);
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var controller = EnsureComponent<VWS.PlayerController_ThirdPerson>(target, useUndo);
            controller.useCameraSpace = true;
            controller.moveInFacingDirection = false;
            controller.moveSpeed = Mathf.Max(4f, controller.moveSpeed);
            controller.turnSpeed = Mathf.Max(8f, controller.turnSpeed);

            EnsureComponent<VWS.PlayerHealth>(target, useUndo);
            EnsureComponent<VWS.CollectibleCounter>(target, useUndo);
            ApplyPlayerAttack(target, options, useUndo);
        }

        static void ApplyPlayerAttack(GameObject target, FeatureOptions options, bool useUndo)
        {
            EnsureComponent<VWS.PlayerHealth>(target, useUndo);
            var attack = EnsureComponent<VWS.PlayerAttack>(target, useUndo);
            attack.damage = Mathf.Max(1, options.playerDamage);
            attack.range = Mathf.Max(1.8f, attack.range);
            attack.radius = Mathf.Max(0.65f, attack.radius);
            attack.cooldown = Mathf.Clamp(attack.cooldown <= 0f ? 0.45f : attack.cooldown, 0.05f, 3f);
        }

        static void ApplyEnemyCore(GameObject target, FeatureOptions options, bool useUndo)
        {
            EnsureTagExists("Enemy");
            target.tag = "Enemy";
            EnsureCapsuleCollider(target, useUndo);
            var health = EnsureComponent<VWS.EnemyHealth>(target, useUndo);
            health.maxHP = Mathf.Max(20, health.maxHP);

            var agent = EnsureComponent<NavMeshAgent>(target, useUndo);
            agent.radius = Mathf.Clamp(agent.radius <= 0f ? 0.4f : agent.radius, 0.25f, 1.2f);
            agent.height = Mathf.Max(1.5f, agent.height);
            agent.speed = Mathf.Max(2.5f, agent.speed);
            agent.angularSpeed = Mathf.Max(240f, agent.angularSpeed);
            agent.stoppingDistance = Mathf.Max(1.2f, agent.stoppingDistance);

            var align = EnsureComponent<VWS.NavMeshEditPlayAlign>(target, useUndo);
            align.sampleMaxDistance = Mathf.Max(align.sampleMaxDistance, 20f);
            align.alignInPlayMode = true;
            align.alignInEditMode = false;
            EditorUtility.SetDirty(align);
        }

        static void ApplyEnemyAttack(GameObject target, FeatureOptions options, bool useUndo)
        {
            ApplyEnemyCore(target, options, useUndo);
            var ai = EnsureComponent<VWS.EnemyAI_NavMesh>(target, useUndo);
            ai.contactDamage = Mathf.Max(1, options.enemyDamage);
            ai.attackReach = Mathf.Max(1.7f, ai.attackReach);
            ai.stopDistance = Mathf.Max(1.2f, ai.stopDistance);
            ai.attackSpeed = Mathf.Clamp(ai.attackSpeed <= 0f ? 0.55f : ai.attackSpeed, 0.35f, 0.55f);
            ai.attackAnimationSpeed = Mathf.Clamp(ai.attackAnimationSpeed <= 0f ? 1f : ai.attackAnimationSpeed, 0.7f, 1.2f);
            ai.contactInterval = 1f / Mathf.Max(0.05f, ai.attackSpeed);
        }

        static void ApplyDoorOpen(GameObject target, bool useUndo)
        {
            EnsureSolidBoxCollider(target, useUndo);
            var door = EnsureComponent<VWS.DoorController>(target, useUndo);
            if (door.openOffset == Vector3.zero)
                door.openOffset = new Vector3(0f, 3f, 0f);

            var trigger = FindOrCreateChild(target, DoorTriggerName, useUndo);
            var bounds = CalculateLocalBounds(target);
            trigger.transform.localPosition = new Vector3(0f, Mathf.Max(1f, bounds.center.y), Mathf.Max(1.5f, bounds.extents.z + 1.25f));
            trigger.transform.localRotation = Quaternion.identity;
            trigger.transform.localScale = Vector3.one;

            var box = EnsureComponent<BoxCollider>(trigger, useUndo);
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = new Vector3(Mathf.Max(2f, bounds.size.x + 1f), Mathf.Max(2f, bounds.size.y), 1.4f);

            var plate = EnsureComponent<VWS.PressurePlate>(trigger, useUndo);
            plate.targets = new[] { door };
        }

        static void ApplyItemPickup(GameObject target, bool useUndo)
        {
            EnsureBoxTrigger(target, useUndo);
            EnsureComponent<VWS.ItemPickup>(target, useUndo);
        }

        static void ApplyHealthPickup(GameObject target, FeatureOptions options, bool useUndo)
        {
            EnsureBoxTrigger(target, useUndo);
            var pickup = EnsureComponent<VWS.HealthPickup>(target, useUndo);
            pickup.healAmount = Mathf.Max(1, options.healthAmount);
            pickup.destroyOnPickup = true;
        }

        static void ApplyHazardZone(GameObject target, FeatureOptions options, bool useUndo)
        {
            EnsureBoxTrigger(target, useUndo);
            var hazard = EnsureComponent<VWS.HazardZone>(target, useUndo);
            hazard.damagePerSecond = Mathf.Max(1, options.hazardDamagePerSecond);
        }

        static void ApplyCheckpoint(GameObject target, bool useUndo)
        {
            EnsureBoxTrigger(target, useUndo);
            EnsureComponent<VWS.Checkpoint>(target, useUndo);
        }

        static void ApplyGoal(GameObject target, FeatureOptions options, bool useUndo)
        {
            EnsureBoxTrigger(target, useUndo);
            var goal = EnsureComponent<VWS.GoalTrigger>(target, useUndo);
            goal.requiredItems = Mathf.Max(0, options.requiredItems);
        }

        static FeatureKind ResolveFeature(GameObject target, FeatureKind requestedFeature)
        {
            if (requestedFeature != FeatureKind.Auto)
                return requestedFeature;

            var normalized = target.name.ToLowerInvariant();
            if (ContainsAny(normalized, "player", "hero", "knight", "warrior", "astronaut", "character"))
                return FeatureKind.PlayerCore;
            if (ContainsAny(normalized, "enemy", "zombie", "monster", "boss", "creature"))
                return FeatureKind.EnemyAttack;
            if (ContainsAny(normalized, "door", "gate", "portal"))
                return FeatureKind.DoorOpen;
            if (ContainsAny(normalized, "health", "heal", "potion", "medicine"))
                return FeatureKind.HealthPickup;
            if (ContainsAny(normalized, "hazard", "trap", "spike", "fire", "lava", "damage"))
                return FeatureKind.HazardZone;
            if (ContainsAny(normalized, "checkpoint", "spawn", "respawn"))
                return FeatureKind.Checkpoint;
            if (ContainsAny(normalized, "goal", "exit", "finish", "clear"))
                return FeatureKind.Goal;
            if (ContainsAny(normalized, "item", "coin", "key", "gem", "treasure", "collect"))
                return FeatureKind.ItemPickup;

            return FeatureKind.Auto;
        }

        static bool ContainsAny(string text, params string[] tokens)
        {
            return tokens.Any(text.Contains);
        }

        static void AddSmokeResult(List<string> results, string key, bool passed)
        {
            results.Add(key + "=" + (passed ? "PASS" : "FAIL"));
        }

        static bool HasTriggerBox(GameObject target)
        {
            var box = target ? target.GetComponent<BoxCollider>() : null;
            return box && box.isTrigger;
        }

        static bool TryGetGameObject(Object selected, out GameObject gameObject)
        {
            gameObject = selected switch
            {
                GameObject go => go,
                Component component => component.gameObject,
                _ => null
            };
            return gameObject != null;
        }

        static T EnsureComponent<T>(GameObject target, bool useUndo) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component)
                return component;

            return useUndo ? Undo.AddComponent<T>(target) : target.AddComponent<T>();
        }

        static GameObject FindOrCreateChild(GameObject target, string childName, bool useUndo)
        {
            var child = target.transform.Find(childName);
            if (child)
                return child.gameObject;

            var childObject = new GameObject(childName);
            if (useUndo)
                Undo.RegisterCreatedObjectUndo(childObject, "VARCO 기능 트리거 생성");

            childObject.transform.SetParent(target.transform, false);
            return childObject;
        }

        static CapsuleCollider EnsureCapsuleCollider(GameObject target, bool useUndo)
        {
            var collider = EnsureComponent<CapsuleCollider>(target, useUndo);
            var bounds = CalculateLocalBounds(target);
            collider.isTrigger = false;
            collider.center = bounds.center;
            collider.height = Mathf.Max(1.6f, bounds.size.y);
            collider.radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.35f, 0.25f, 0.7f);
            return collider;
        }

        static BoxCollider EnsureSolidBoxCollider(GameObject target, bool useUndo)
        {
            var collider = EnsureComponent<BoxCollider>(target, useUndo);
            var bounds = CalculateLocalBounds(target);
            collider.isTrigger = false;
            collider.center = bounds.center;
            collider.size = Vector3.Max(bounds.size, Vector3.one);
            return collider;
        }

        static BoxCollider EnsureBoxTrigger(GameObject target, bool useUndo)
        {
            var collider = EnsureComponent<BoxCollider>(target, useUndo);
            var bounds = CalculateLocalBounds(target);
            collider.isTrigger = true;
            collider.center = bounds.center;
            collider.size = Vector3.Max(bounds.size, Vector3.one * 0.75f);
            return collider;
        }

        static Bounds CalculateLocalBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(1f, 1.8f, 1f));

            var localMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var localMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            foreach (var renderer in renderers)
            {
                if (!renderer)
                    continue;

                var worldBounds = renderer.bounds;
                var min = worldBounds.min;
                var max = worldBounds.max;
                var corners = new[]
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

                foreach (var corner in corners)
                {
                    var local = target.transform.InverseTransformPoint(corner);
                    localMin = Vector3.Min(localMin, local);
                    localMax = Vector3.Max(localMax, local);
                }
            }

            var size = localMax - localMin;
            if (float.IsInfinity(size.x) || size.sqrMagnitude <= 0.0001f)
                return new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(1f, 1.8f, 1f));

            return new Bounds((localMin + localMax) * 0.5f, Vector3.Max(size, Vector3.one * 0.1f));
        }

        static void EnsureGeneratedPrefabFolder()
        {
            if (AssetDatabase.IsValidFolder(GeneratedPrefabFolder))
                return;

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Prefabs", "VARCO_FunctionApplied");
        }

        static void EnsureTagExists(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || IsBuiltInUnityTag(tag))
                return;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tags = tagManager.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                    return;
            }

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
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

        static void MarkSceneObjectDirty(GameObject gameObject)
        {
            EditorUtility.SetDirty(gameObject);
            foreach (var component in gameObject.GetComponentsInChildren<Component>(true))
            {
                if (component)
                {
                    EditorUtility.SetDirty(component);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
            }

            if (gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        static string FeatureName(FeatureKind featureKind)
        {
            var index = Mathf.Clamp((int)featureKind, 0, FeatureLabels.Length - 1);
            return FeatureLabels[index].text;
        }

        static string FeatureSuffix(FeatureKind featureKind)
        {
            return featureKind switch
            {
                FeatureKind.PlayerCore => "Player",
                FeatureKind.PlayerAttack => "PlayerAttack",
                FeatureKind.EnemyCore => "Enemy",
                FeatureKind.EnemyAttack => "EnemyAttack",
                FeatureKind.DoorOpen => "DoorOpen",
                FeatureKind.ItemPickup => "ItemPickup",
                FeatureKind.HealthPickup => "HealthPickup",
                FeatureKind.HazardZone => "HazardZone",
                FeatureKind.Checkpoint => "Checkpoint",
                FeatureKind.Goal => "Goal",
                _ => "Feature"
            };
        }

        static string SafeFileName(string value)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value;
        }
    }

    [Serializable]
    public struct FeatureOptions
    {
        public int playerDamage;
        public int enemyDamage;
        public int healthAmount;
        public int hazardDamagePerSecond;
        public int requiredItems;
    }
}
#endif
