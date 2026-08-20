#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using VWS = VARCO_Workshop;

namespace VARCO_Workshop.Editor
{
    public class VARCOPrefabMakerWindow : EditorWindow
    {
        enum PrefabPresetKind
        {
            AdventurePlayer,
            PlatformPlayer,
            ChaserEnemy,
            SideDoor,
            VerticalDoor,
            MovingObject,
            JumpingObject,
            ItemPickup,
            HealthPickup,
            HazardZone,
            Checkpoint,
            Goal,
            FallGuysSpinnerObstacle,
            FallGuysBouncePad,
            FallGuysBreakawayPlatform,
            ArenaCover,
            BossEnemy,
            ZombieEnemy,
            TreasureCollectible,
            ExplorationLandmark,
            PuzzlePressurePlate,
            PuzzleMovableBox,
            LockedExitGoal,
            SafeRoomCheckpoint,
            SurvivalSpawnMarker
        }

        enum FunctionBundleKind
        {
            MoveLeftRight,
            MoveUpDown,
            DoorOpenSide,
            DoorOpenUp,
            JumpMotion,
            PlatformJumpAbility,
            CollectibleItem,
            DamageHazard,
            Checkpoint,
            Goal,
            SpinObstacle,
            BouncePad,
            BreakawayPlatform,
            ArenaCover,
            BossTuning,
            ZombieTuning,
            TreasureCollectible,
            ExplorationLandmark,
            PuzzlePressurePlate,
            PuzzleMovableBox,
            LockedExitGoal,
            SafeRoomCheckpoint,
            SurvivalSpawnMarker
        }

        static readonly GUIContent[] PresetLabels =
        {
            new GUIContent("플레이어 프리셋 - 이동/공격/체력"),
            new GUIContent("플랫폼 플레이어 프리셋 - 이동/달리기/점프/리스폰"),
            new GUIContent("적 프리셋 - 체력/추적/공격/NavMesh"),
            new GUIContent("문 프리셋 - 좌우로 열림"),
            new GUIContent("문 프리셋 - 위아래로 열림"),
            new GUIContent("움직이는 오브젝트 프리셋"),
            new GUIContent("점프/바운스 오브젝트 프리셋"),
            new GUIContent("수집 아이템 프리셋"),
            new GUIContent("회복 아이템 프리셋"),
            new GUIContent("위험 구역 프리셋"),
            new GUIContent("체크포인트 프리셋"),
            new GUIContent("목표 지점 프리셋"),
            new GUIContent("플랫폼 장애물 - 회전 해머/팬"),
            new GUIContent("플랫폼 장치 - 바운스 패드"),
            new GUIContent("플랫폼 장치 - 밟으면 사라지는 발판"),
            new GUIContent("아레나 오브젝트 - 엄폐물/기둥"),
            new GUIContent("아레나 적 - 보스 튜닝"),
            new GUIContent("좀비 생존 적 - 느린 추적"),
            new GUIContent("탐험 수집품 - 보물/열쇠"),
            new GUIContent("탐험 랜드마크 - 표식/목표물"),
            new GUIContent("퍼즐 장치 - 압력판"),
            new GUIContent("퍼즐 장치 - 밀 수 있는 박스"),
            new GUIContent("퍼즐 목표 - 잠금 출구"),
            new GUIContent("생존 루트 - 안전방 체크포인트"),
            new GUIContent("생존/아레나 - 스폰 마커")
        };

        static readonly GUIContent[] FunctionLabels =
        {
            new GUIContent("움직이기 - 좌우 왕복"),
            new GUIContent("움직이기 - 위아래 왕복"),
            new GUIContent("좌우로 열리기"),
            new GUIContent("위아래로 열리기"),
            new GUIContent("점프/바운스 움직임"),
            new GUIContent("플레이어 점프 기능"),
            new GUIContent("수집 아이템 기능"),
            new GUIContent("피해 구역 기능"),
            new GUIContent("체크포인트 기능"),
            new GUIContent("목표 지점 기능"),
            new GUIContent("회전 장애물 기능"),
            new GUIContent("바운스 패드 기능"),
            new GUIContent("사라지는 발판 기능"),
            new GUIContent("아레나 엄폐물 기능"),
            new GUIContent("보스 적 튜닝"),
            new GUIContent("좀비 적 튜닝"),
            new GUIContent("보물/열쇠 수집 기능"),
            new GUIContent("탐험 랜드마크 기능"),
            new GUIContent("압력판 기능"),
            new GUIContent("밀 수 있는 박스 기능"),
            new GUIContent("잠금 출구 기능"),
            new GUIContent("안전방 체크포인트 기능"),
            new GUIContent("스폰 마커 기능")
        };

        static readonly PrefabPresetKind[] ObjectPresetKinds =
        {
            PrefabPresetKind.SideDoor,
            PrefabPresetKind.VerticalDoor,
            PrefabPresetKind.MovingObject,
            PrefabPresetKind.JumpingObject,
            PrefabPresetKind.ItemPickup,
            PrefabPresetKind.HealthPickup,
            PrefabPresetKind.HazardZone,
            PrefabPresetKind.Checkpoint,
            PrefabPresetKind.Goal,
            PrefabPresetKind.FallGuysSpinnerObstacle,
            PrefabPresetKind.FallGuysBouncePad,
            PrefabPresetKind.FallGuysBreakawayPlatform,
            PrefabPresetKind.ArenaCover,
            PrefabPresetKind.TreasureCollectible,
            PrefabPresetKind.ExplorationLandmark,
            PrefabPresetKind.PuzzlePressurePlate,
            PrefabPresetKind.PuzzleMovableBox,
            PrefabPresetKind.LockedExitGoal,
            PrefabPresetKind.SafeRoomCheckpoint,
            PrefabPresetKind.SurvivalSpawnMarker
        };

        static readonly FunctionBundleKind[] ObjectFunctionKinds =
        {
            FunctionBundleKind.MoveLeftRight,
            FunctionBundleKind.MoveUpDown,
            FunctionBundleKind.DoorOpenSide,
            FunctionBundleKind.DoorOpenUp,
            FunctionBundleKind.JumpMotion,
            FunctionBundleKind.CollectibleItem,
            FunctionBundleKind.DamageHazard,
            FunctionBundleKind.Checkpoint,
            FunctionBundleKind.Goal,
            FunctionBundleKind.SpinObstacle,
            FunctionBundleKind.BouncePad,
            FunctionBundleKind.BreakawayPlatform,
            FunctionBundleKind.ArenaCover,
            FunctionBundleKind.TreasureCollectible,
            FunctionBundleKind.ExplorationLandmark,
            FunctionBundleKind.PuzzlePressurePlate,
            FunctionBundleKind.PuzzleMovableBox,
            FunctionBundleKind.LockedExitGoal,
            FunctionBundleKind.SafeRoomCheckpoint,
            FunctionBundleKind.SurvivalSpawnMarker
        };

        const string PresetKitRoot = "Assets/VARCOPresetKits";
        const string FunctionAppliedRoot = "Assets/Prefabs/VARCO_FunctionApplied";
        const string AnimationOutputFolder = "Assets/Animations/Generated";

        Vector2 scroll;
        Vector2 logScroll;
        readonly List<string> logLines = new List<string>();

        PrefabPresetKind prefabPreset = PrefabPresetKind.SideDoor;
        FunctionBundleKind functionBundle = FunctionBundleKind.MoveLeftRight;
        bool saveAsNewPrefab = true;
        bool reuseGeneratedPrefabPath = true;
        bool autoValidateAfterApply = true;
        bool repairValidationIssues = true;

        int playerDamage = 15;
        int enemyDamage = 2;
        int healthAmount = 25;
        int hazardDamagePerSecond = 15;
        int requiredItems;
        float moveDistance = 4f;
        float moveSpeed = 1.2f;
        float openDistance = 3f;
        float jumpHeight = 1.2f;
        float jumpSpeed = 1.5f;

        string animationSearchText = "";
        AnimationClip idleClip;
        AnimationClip walkClip;
        AnimationClip runClip;
        AnimationClip jumpClip;
        AnimationClip attackClip;
        AnimationClip deathClip;
        bool overwriteAnimationController = true;
        bool assignAnimationToGeneratedPrefab = true;

        bool kitOpen = true;
        bool presetOpen = true;
        bool bundleOpen = true;
        bool validationOpen = true;

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/오브젝트 프리팹 생성기", priority = -100)]
        public static void OpenObjectGenerator()
        {
            var window = GetWindow<VARCOPrefabMakerWindow>("VARCO 오브젝트 프리팹 생성기");
            window.minSize = new Vector2(580f, 680f);
            window.Focus();
        }

        public static void Open()
        {
            OpenObjectGenerator();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(10f);

            EditorGUILayout.LabelField("VARCO 오브젝트 프리팹 생성기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "애니메이션이 없는 모델링 에셋을 게임용 오브젝트 프리팹으로 바꾸는 작업실입니다. 캐릭터 모델과 애니메이션 묶음은 VARCO > 캐릭터 프리팹 생성기에서 처리합니다.",
                MessageType.Info);

            DrawSelectionSummary();

            kitOpen = EditorGUILayout.Foldout(kitOpen, "1. 프리셋 키트 준비", true);
            if (kitOpen)
                DrawKitTools();

            presetOpen = EditorGUILayout.Foldout(presetOpen, "2. 프리팹 프리셋 적용", true);
            if (presetOpen)
                DrawPrefabPresetTools();

            bundleOpen = EditorGUILayout.Foldout(bundleOpen, "3. 프리팹 기능 추가", true);
            if (bundleOpen)
                DrawFunctionBundleTools();

            validationOpen = EditorGUILayout.Foldout(validationOpen, "4. 프리팹 검증 / 자동 수정", true);
            if (validationOpen)
                DrawValidationTools();

            GUILayout.Space(10f);
            DrawLog();

            EditorGUILayout.EndScrollView();
        }

        void DrawSelectionSummary()
        {
            var selected = CurrentSelectionGameObject();
            var label = selected ? selected.name : "없음";
            var path = selected ? AssetDatabase.GetAssetPath(selected) : "";
            if (string.IsNullOrWhiteSpace(path) && selected)
                path = "Hierarchy 오브젝트";

            EditorGUILayout.HelpBox("현재 선택: " + label + (selected ? "\n위치: " + path : "\nProject 또는 Hierarchy에서 모델/프리팹을 선택하세요."), selected ? MessageType.None : MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("현재 선택 다시 읽기", GUILayout.Height(24f)))
                    Repaint();
                if (GUILayout.Button("캐릭터 프리팹 생성기 열기", GUILayout.Height(24f)))
                    VARCOCharacterPrefabMakerWindow.Open();
            }
        }

        void DrawKitTools()
        {
            DrawKitStatus();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("프리셋 키트 폴더 준비", GUILayout.Height(30f)))
                    VARCOGameMakerWindow.CreateAllPresetKitFoldersFromMenu();
                if (GUILayout.Button("현재 에셋 기준으로 역할 프리팹 채우기", GUILayout.Height(30f)))
                    VARCOGameMakerWindow.FillAllPresetKitPrefabsFromMenu();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("프리셋 키트 폴더 열기", GUILayout.Height(24f)))
                    RevealPresetKitRoot();
                if (GUILayout.Button("프리셋 만들기 열기", GUILayout.Height(24f)))
                    VARCOPresetMakerWindow.Open();
            }
        }

        void DrawPrefabPresetTools()
        {
            prefabPreset = DrawObjectPresetPopup("오브젝트 프리셋", prefabPreset);
            DrawCommonOptions();
            DrawTuningOptions();

            var selected = CurrentSelectionGameObject();
            var blockedCharacter = IsCharacterLikeSelection(selected);
            if (blockedCharacter)
                EditorGUILayout.HelpBox("캐릭터 에셋은 오브젝트 프리팹 생성기에서 제외됩니다. VARCO > 캐릭터 프리팹 생성기를 사용하세요.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(!selected || blockedCharacter))
            {
                if (GUILayout.Button("선택 모델에 프리팹 프리셋 적용", GUILayout.Height(34f)))
                    ApplyPresetToSelection();
            }
        }

        void DrawFunctionBundleTools()
        {
            functionBundle = DrawObjectFunctionPopup("오브젝트 기능", functionBundle);
            DrawCommonOptions();
            DrawMotionOptions();
            DrawTuningOptions();

            var selected = CurrentSelectionGameObject();
            var blockedCharacter = IsCharacterLikeSelection(selected);
            if (blockedCharacter)
                EditorGUILayout.HelpBox("캐릭터 에셋에는 오브젝트 기능을 추가하지 않습니다. 캐릭터 프리팹 생성기에서 처리하세요.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(!selected || blockedCharacter))
            {
                if (GUILayout.Button("선택 프리팹에 기능 추가", GUILayout.Height(34f)))
                    ApplyFunctionBundleToSelection();
            }

            if (GUILayout.Button("레거시 세부 기능 추가 창 열기", GUILayout.Height(24f)))
                VARCOSelectedFeatureApplicatorWindow.Open();
        }

        void DrawAnimationTools()
        {
            var selected = CurrentSelectionGameObject();
            if (selected && string.IsNullOrWhiteSpace(animationSearchText))
                animationSearchText = selected.name;

            animationSearchText = EditorGUILayout.TextField("클립 검색어", animationSearchText);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("클립 자동 찾기", GUILayout.Height(24f)))
                    AutoDetectAnimationClips();
                if (GUILayout.Button("애니메이션 폴더 열기", GUILayout.Height(24f)))
                    RevealFolder(AnimationOutputFolder);
            }

            idleClip = (AnimationClip)EditorGUILayout.ObjectField("대기 Idle", idleClip, typeof(AnimationClip), false);
            walkClip = (AnimationClip)EditorGUILayout.ObjectField("걷기 Walk", walkClip, typeof(AnimationClip), false);
            runClip = (AnimationClip)EditorGUILayout.ObjectField("뛰기 Run / Shift", runClip, typeof(AnimationClip), false);
            jumpClip = (AnimationClip)EditorGUILayout.ObjectField("점프 Jump", jumpClip, typeof(AnimationClip), false);
            attackClip = (AnimationClip)EditorGUILayout.ObjectField("공격 Attack", attackClip, typeof(AnimationClip), false);
            deathClip = (AnimationClip)EditorGUILayout.ObjectField("죽기 Death", deathClip, typeof(AnimationClip), false);

            overwriteAnimationController = EditorGUILayout.ToggleLeft("같은 이름의 컨트롤러가 있으면 갱신", overwriteAnimationController);
            assignAnimationToGeneratedPrefab = EditorGUILayout.ToggleLeft("생성 후 선택 프리팹/생성 프리팹에 바로 연결", assignAnimationToGeneratedPrefab);

            using (new EditorGUI.DisabledScope(!idleClip))
            {
                if (GUILayout.Button("AnimatorController 생성 / 선택 프리팹에 연결", GUILayout.Height(34f)))
                    CreateAnimationControllerForSelection();
            }
        }

        void DrawValidationTools()
        {
            repairValidationIssues = EditorGUILayout.ToggleLeft("검증 중 발견한 일반 문제 자동 수정", repairValidationIssues);
            using (new EditorGUI.DisabledScope(!CurrentSelectionGameObject()))
            {
                if (GUILayout.Button("선택 프리팹 검증", GUILayout.Height(30f)))
                    ValidateSelection(repair: false);
                if (GUILayout.Button("선택 프리팹 검증 + 자동 수정", GUILayout.Height(34f)))
                    ValidateSelection(repair: true);
            }
        }

        void DrawCommonOptions()
        {
            reuseGeneratedPrefabPath = EditorGUILayout.ToggleLeft("같은 이름 산출물은 덮어써서 중복 줄이기", reuseGeneratedPrefabPath);
            saveAsNewPrefab = EditorGUILayout.ToggleLeft("원본 보존 후 기능 적용 프리팹으로 저장", saveAsNewPrefab);
            autoValidateAfterApply = EditorGUILayout.ToggleLeft("적용 후 검증/자동 수정 실행", autoValidateAfterApply);
        }

        void DrawMotionOptions()
        {
            if (functionBundle == FunctionBundleKind.MoveLeftRight || functionBundle == FunctionBundleKind.MoveUpDown)
            {
                moveDistance = EditorGUILayout.Slider("이동 거리", moveDistance, 0.5f, 12f);
                moveSpeed = EditorGUILayout.Slider("이동 속도", moveSpeed, 0.1f, 5f);
            }

            if (functionBundle == FunctionBundleKind.DoorOpenSide || functionBundle == FunctionBundleKind.DoorOpenUp)
                openDistance = EditorGUILayout.Slider("열림 거리", openDistance, 0.5f, 8f);

            if (functionBundle == FunctionBundleKind.JumpMotion)
            {
                jumpHeight = EditorGUILayout.Slider("점프 높이", jumpHeight, 0.2f, 6f);
                jumpSpeed = EditorGUILayout.Slider("점프 속도", jumpSpeed, 0.1f, 6f);
            }
        }

        void DrawTuningOptions()
        {
            playerDamage = EditorGUILayout.IntSlider("플레이어 공격력", playerDamage, 1, 100);
            enemyDamage = EditorGUILayout.IntSlider("적 공격력", enemyDamage, 1, 100);
            healthAmount = EditorGUILayout.IntSlider("회복량", healthAmount, 1, 100);
            hazardDamagePerSecond = EditorGUILayout.IntSlider("초당 피해", hazardDamagePerSecond, 1, 100);
            requiredItems = EditorGUILayout.IntSlider("목표 필요 수집 수", requiredItems, 0, 20);
        }

        void DrawKitStatus()
        {
            var fullRoot = Path.GetFullPath(PresetKitRoot);
            if (!Directory.Exists(fullRoot))
            {
                EditorGUILayout.HelpBox("프리셋 키트 폴더가 아직 없습니다.", MessageType.Warning);
                return;
            }

            var kitCount = Directory.GetDirectories(fullRoot).Length;
            var prefabs = Directory.GetFiles(fullRoot, "*.prefab", SearchOption.AllDirectories);
            var placeholderCount = prefabs.Count(path => Path.GetFileNameWithoutExtension(path).Contains("_SLOT_PLACEHOLDER"));
            var realCount = prefabs.Length - placeholderCount;

            EditorGUILayout.HelpBox(
                "키트 폴더: " + kitCount + "개\n"
                    + "실제 역할 프리팹: " + realCount + "개\n"
                    + "안내용 슬롯 프리팹: " + placeholderCount + "개",
                MessageType.None);
        }

        void DrawLog()
        {
            EditorGUILayout.LabelField("작업 기록", EditorStyles.boldLabel);
            if (logLines.Count == 0)
            {
                EditorGUILayout.HelpBox("아직 실행 내역이 없습니다.", MessageType.None);
                return;
            }

            logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(220f));
            foreach (var line in logLines.TakeLast(24))
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("로그 지우기", GUILayout.Height(22f)))
                logLines.Clear();
        }

        void ApplyPresetToSelection()
        {
            var selected = CurrentSelectionGameObject();
            if (!selected)
            {
                Log("오류: 선택된 모델 또는 프리팹이 없습니다.");
                return;
            }

            if (IsCharacterLikeSelection(selected))
            {
                Log("중단: 캐릭터 에셋은 오브젝트 프리팹 생성기에서 제외됩니다. 캐릭터 프리팹 생성기를 사용하세요.");
                return;
            }
            if (IsCharacterPreset(prefabPreset))
            {
                Log("중단: 캐릭터 프리셋은 오브젝트 프리팹 생성기에서 사용할 수 없습니다.");
                return;
            }

            EditSelectedAsPrefab(selected, PresetSuffix(prefabPreset), editable =>
            {
                ApplyPrefabPreset(editable, prefabPreset);
                if (autoValidateAfterApply)
                    ValidateAndRepairPrefab(editable, repairValidationIssues, logLines);
            });
        }

        void ApplyFunctionBundleToSelection()
        {
            var selected = CurrentSelectionGameObject();
            if (!selected)
            {
                Log("오류: 선택된 모델 또는 프리팹이 없습니다.");
                return;
            }

            if (IsCharacterLikeSelection(selected))
            {
                Log("중단: 캐릭터 에셋에는 오브젝트 기능을 추가하지 않습니다. 캐릭터 프리팹 생성기를 사용하세요.");
                return;
            }
            if (IsCharacterFunctionBundle(functionBundle))
            {
                Log("중단: 캐릭터 기능은 오브젝트 프리팹 생성기에서 사용할 수 없습니다.");
                return;
            }

            EditSelectedAsPrefab(selected, FunctionSuffix(functionBundle), editable =>
            {
                ApplyFunctionBundle(editable, functionBundle);
                if (autoValidateAfterApply)
                    ValidateAndRepairPrefab(editable, repairValidationIssues, logLines);
            });
        }

        void CreateAnimationControllerForSelection()
        {
            var controller = CreateAnimationController();
            if (!controller)
                return;

            if (!assignAnimationToGeneratedPrefab)
                return;

            var selected = CurrentSelectionGameObject();
            if (!selected)
            {
                Log("확인 필요: 선택 프리팹이 없어 AnimatorController만 생성했습니다.");
                return;
            }

            EditSelectedAsPrefab(selected, "Animated", editable =>
            {
                AssignAnimatorController(editable, controller);
                MarkFeature(editable, "Animation", "AnimatorController", "AnimationReady");
                if (autoValidateAfterApply)
                    ValidateAndRepairPrefab(editable, repairValidationIssues, logLines);
            });
        }

        void ValidateSelection(bool repair)
        {
            var selected = CurrentSelectionGameObject();
            if (!selected)
            {
                Log("오류: 선택된 모델 또는 프리팹이 없습니다.");
                return;
            }

            EditSelectedAsPrefab(selected, "Validated", editable => ValidateAndRepairPrefab(editable, repair, logLines));
        }

        void EditSelectedAsPrefab(GameObject selected, string suffix, Action<GameObject> edit)
        {
            logLines.Clear();
            var path = AssetDatabase.GetAssetPath(selected);
            var isPrefabAsset = !string.IsNullOrWhiteSpace(path)
                && Path.GetExtension(path).Equals(".prefab", StringComparison.OrdinalIgnoreCase)
                && AssetDatabase.Contains(selected);

            if (isPrefabAsset && !saveAsNewPrefab)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    edit(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Log("완료: 원본 프리팹에 저장 " + path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return;
            }

            var instance = PrefabUtility.InstantiatePrefab(selected) as GameObject;
            if (!instance)
                instance = Instantiate(selected);

            try
            {
                instance.name = SafeFileName(selected.name + "_" + suffix);
                edit(instance);
                var folder = FunctionAppliedRoot + "/" + SafeFileName(suffix);
                EnsureFolder(folder);
                var prefabPath = folder + "/" + SafeFileName(instance.name) + ".prefab";
                if (!reuseGeneratedPrefabPath)
                    prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                if (prefab)
                {
                    Selection.activeObject = prefab;
                    EditorGUIUtility.PingObject(prefab);
                }
                Log("완료: 기능 적용 프리팹 생성 " + prefabPath);
            }
            finally
            {
                if (instance)
                    DestroyImmediate(instance);
            }
        }

        void ApplyPrefabPreset(GameObject target, PrefabPresetKind preset)
        {
            switch (preset)
            {
                case PrefabPresetKind.AdventurePlayer:
                    ApplyAdventurePlayer(target);
                    break;
                case PrefabPresetKind.PlatformPlayer:
                    ApplyPlatformPlayer(target);
                    break;
                case PrefabPresetKind.ChaserEnemy:
                    ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.EnemyAttack);
                    MarkFeature(target, "ChaserEnemy", "Enemy", "EnemyCore", "EnemyAttack", "NavMesh");
                    break;
                case PrefabPresetKind.SideDoor:
                    ConfigureDoor(target, Vector3.right * openDistance, "SideDoor");
                    break;
                case PrefabPresetKind.VerticalDoor:
                    ConfigureDoor(target, Vector3.up * openDistance, "VerticalDoor");
                    break;
                case PrefabPresetKind.MovingObject:
                    ConfigurePingPong(target, Vector3.right * moveDistance, "MovingObject");
                    break;
                case PrefabPresetKind.JumpingObject:
                    ConfigureJumpMotion(target, "JumpingObject");
                    break;
                case PrefabPresetKind.ItemPickup:
                    ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.ItemPickup);
                    MarkFeature(target, "ItemPickup", "ItemPickup", "ItemPickup");
                    break;
                case PrefabPresetKind.HealthPickup:
                    ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.HealthPickup);
                    MarkFeature(target, "HealthPickup", "HealthPickup", "HealthPickup");
                    break;
                case PrefabPresetKind.HazardZone:
                    ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.HazardZone);
                    MarkFeature(target, "HazardZone", "HazardZone", "HazardZone");
                    break;
                case PrefabPresetKind.Checkpoint:
                    ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.Checkpoint);
                    MarkFeature(target, "Checkpoint", "Checkpoint", "Checkpoint");
                    break;
                case PrefabPresetKind.Goal:
                    ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.Goal);
                    MarkFeature(target, "Goal", "Goal", "Goal");
                    break;
                case PrefabPresetKind.FallGuysSpinnerObstacle:
                    ConfigureSpinObstacle(target, "FallGuysSpinnerObstacle");
                    break;
                case PrefabPresetKind.FallGuysBouncePad:
                    ConfigureBouncePad(target, "FallGuysBouncePad");
                    break;
                case PrefabPresetKind.FallGuysBreakawayPlatform:
                    ConfigureBreakawayPlatform(target, "FallGuysBreakawayPlatform");
                    break;
                case PrefabPresetKind.ArenaCover:
                    ConfigureArenaCover(target, "ArenaCover");
                    break;
                case PrefabPresetKind.BossEnemy:
                    ConfigureBossEnemy(target, "BossEnemy");
                    break;
                case PrefabPresetKind.ZombieEnemy:
                    ConfigureZombieEnemy(target, "ZombieEnemy");
                    break;
                case PrefabPresetKind.TreasureCollectible:
                    ConfigureTreasureCollectible(target, "TreasureCollectible");
                    break;
                case PrefabPresetKind.ExplorationLandmark:
                    ConfigureExplorationLandmark(target, "ExplorationLandmark");
                    break;
                case PrefabPresetKind.PuzzlePressurePlate:
                    ConfigurePuzzlePressurePlate(target, "PuzzlePressurePlate");
                    break;
                case PrefabPresetKind.PuzzleMovableBox:
                    ConfigurePuzzleMovableBox(target, "PuzzleMovableBox");
                    break;
                case PrefabPresetKind.LockedExitGoal:
                    ConfigureLockedExitGoal(target, "LockedExitGoal");
                    break;
                case PrefabPresetKind.SafeRoomCheckpoint:
                    ConfigureSafeRoomCheckpoint(target, "SafeRoomCheckpoint");
                    break;
                case PrefabPresetKind.SurvivalSpawnMarker:
                    ConfigureSurvivalSpawnMarker(target, "SurvivalSpawnMarker");
                    break;
            }
        }

        void ApplyFunctionBundle(GameObject target, FunctionBundleKind bundle)
        {
            switch (bundle)
            {
                case FunctionBundleKind.MoveLeftRight:
                    ConfigurePingPong(target, Vector3.right * moveDistance, "MoveLeftRight");
                    break;
                case FunctionBundleKind.MoveUpDown:
                    ConfigurePingPong(target, Vector3.up * moveDistance, "MoveUpDown");
                    break;
                case FunctionBundleKind.DoorOpenSide:
                    ConfigureDoor(target, Vector3.right * openDistance, "DoorOpenSide");
                    break;
                case FunctionBundleKind.DoorOpenUp:
                    ConfigureDoor(target, Vector3.up * openDistance, "DoorOpenUp");
                    break;
                case FunctionBundleKind.JumpMotion:
                    ConfigureJumpMotion(target, "JumpMotion");
                    break;
                case FunctionBundleKind.PlatformJumpAbility:
                    ApplyPlatformPlayer(target);
                    break;
                case FunctionBundleKind.CollectibleItem:
                    ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.ItemPickup);
                    MarkFeature(target, "CollectibleItem", "ItemPickup", "ItemPickup");
                    break;
                case FunctionBundleKind.DamageHazard:
                    ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.HazardZone);
                    MarkFeature(target, "DamageHazard", "HazardZone", "HazardZone");
                    break;
                case FunctionBundleKind.Checkpoint:
                    ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.Checkpoint);
                    MarkFeature(target, "Checkpoint", "Checkpoint", "Checkpoint");
                    break;
                case FunctionBundleKind.Goal:
                    ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.Goal);
                    MarkFeature(target, "Goal", "Goal", "Goal");
                    break;
                case FunctionBundleKind.SpinObstacle:
                    ConfigureSpinObstacle(target, "SpinObstacle");
                    break;
                case FunctionBundleKind.BouncePad:
                    ConfigureBouncePad(target, "BouncePad");
                    break;
                case FunctionBundleKind.BreakawayPlatform:
                    ConfigureBreakawayPlatform(target, "BreakawayPlatform");
                    break;
                case FunctionBundleKind.ArenaCover:
                    ConfigureArenaCover(target, "ArenaCover");
                    break;
                case FunctionBundleKind.BossTuning:
                    ConfigureBossEnemy(target, "BossTuning");
                    break;
                case FunctionBundleKind.ZombieTuning:
                    ConfigureZombieEnemy(target, "ZombieTuning");
                    break;
                case FunctionBundleKind.TreasureCollectible:
                    ConfigureTreasureCollectible(target, "TreasureCollectible");
                    break;
                case FunctionBundleKind.ExplorationLandmark:
                    ConfigureExplorationLandmark(target, "ExplorationLandmark");
                    break;
                case FunctionBundleKind.PuzzlePressurePlate:
                    ConfigurePuzzlePressurePlate(target, "PuzzlePressurePlate");
                    break;
                case FunctionBundleKind.PuzzleMovableBox:
                    ConfigurePuzzleMovableBox(target, "PuzzleMovableBox");
                    break;
                case FunctionBundleKind.LockedExitGoal:
                    ConfigureLockedExitGoal(target, "LockedExitGoal");
                    break;
                case FunctionBundleKind.SafeRoomCheckpoint:
                    ConfigureSafeRoomCheckpoint(target, "SafeRoomCheckpoint");
                    break;
                case FunctionBundleKind.SurvivalSpawnMarker:
                    ConfigureSurvivalSpawnMarker(target, "SurvivalSpawnMarker");
                    break;
            }
        }

        void ApplyAdventurePlayer(GameObject target)
        {
            RemoveComponent<VWS.PlayerController_Platform>(target);
            ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.PlayerCore);
            var controller = EnsureComponent<VWS.PlayerController_ThirdPerson>(target);
            controller.moveSpeed = Mathf.Max(controller.moveSpeed, 5f);
            controller.runMultiplier = Mathf.Max(controller.runMultiplier, 1.35f);
            ConfigureCharacterVisualSafety(target, isPlayer: true, usesNavMesh: false, allowVerticalMotion: false);
            MarkFeature(target, "AdventurePlayer", "Player", "PlayerCore", "PlayerAttack", "RunShift", "GroundYAnchor");
        }

        void ApplyPlatformPlayer(GameObject target)
        {
            RemoveComponent<VWS.PlayerController_ThirdPerson>(target);
            RemoveComponent<Rigidbody>(target);
            EnsureTagExists("Player");
            target.tag = "Player";

            var bounds = CalculateLocalBounds(target);
            var cc = EnsureComponent<CharacterController>(target);
            cc.center = bounds.center;
            cc.height = Mathf.Max(1.6f, bounds.size.y);
            cc.radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.35f, 0.25f, 0.7f);

            var controller = EnsureComponent<VWS.PlayerController_Platform>(target);
            controller.moveSpeed = Mathf.Max(controller.moveSpeed, 6f);
            controller.runMultiplier = Mathf.Max(controller.runMultiplier, 1.3f);
            controller.jumpForce = Mathf.Max(controller.jumpForce, 8f);
            controller.respawnAtStartOnFall = true;

            EnsureComponent<VWS.PlayerHealth>(target);
            EnsureComponent<VWS.CollectibleCounter>(target);
            ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.PlayerAttack);
            ConfigureCharacterVisualSafety(target, isPlayer: true, usesNavMesh: false, allowVerticalMotion: true);
            MarkFeature(target, "PlatformPlayer", "Player", "PlatformMove", "RunShift", "Jump", "Respawn", "PlayerAttack");
        }

        void ConfigureDoor(GameObject target, Vector3 offset, string presetName)
        {
            ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.DoorOpen);
            var door = EnsureComponent<VWS.DoorController>(target);
            door.openOffset = offset;
            MarkFeature(target, presetName, "Door", offset.y > Mathf.Abs(offset.x) ? "DoorOpenUp" : "DoorOpenSide");
        }

        void ConfigurePingPong(GameObject target, Vector3 offset, string presetName)
        {
            EnsureSolidBoxCollider(target);
            var motion = EnsureComponent<VWS.PrefabPingPongMotion>(target);
            motion.localOffset = offset;
            motion.speed = moveSpeed;
            motion.carryCharacterControllers = true;
            MarkFeature(target, presetName, "MovingObject", "PingPongMotion");
        }

        void ConfigureJumpMotion(GameObject target, string presetName)
        {
            EnsureSolidBoxCollider(target);
            var jump = EnsureComponent<VWS.PrefabJumpMotion>(target);
            jump.height = jumpHeight;
            jump.speed = jumpSpeed;
            jump.playOnStart = true;
            jump.useLocalSpace = true;
            MarkFeature(target, presetName, "JumpingObject", "JumpMotion");
        }

        void ConfigureSpinObstacle(GameObject target, string presetName)
        {
            EnsureSolidBoxCollider(target);
            var spin = EnsureComponent<VWS.PrefabSpinMotion>(target);
            spin.localAxis = Vector3.up;
            spin.degreesPerSecond = Mathf.Max(90f, moveSpeed * 120f);
            spin.useLocalSpace = true;
            spin.pauseWhenGamePaused = true;
            MarkFeature(target, presetName, "PlatformObstacle", "SpinObstacle", "FallGuysReference");
        }

        void ConfigureBouncePad(GameObject target, string presetName)
        {
            EnsureBoxTrigger(target);
            var bounce = EnsureComponent<VWS.BouncePad>(target);
            bounce.bounceVelocity = Mathf.Max(8f, jumpHeight * 7f);
            bounce.horizontalBoost = Mathf.Max(1.5f, moveSpeed * 1.25f);
            bounce.usePadForwardForBoost = true;
            MarkFeature(target, presetName, "BouncePad", "BouncePad", "FallGuysReference");
        }

        void ConfigureBreakawayPlatform(GameObject target, string presetName)
        {
            EnsureSolidBoxCollider(target);
            var breakaway = EnsureComponent<VWS.BreakawayPlatform>(target);
            breakaway.breakDelay = Mathf.Clamp(breakaway.breakDelay <= 0f ? 0.5f : breakaway.breakDelay, 0.1f, 3f);
            breakaway.respawnDelay = Mathf.Clamp(breakaway.respawnDelay <= 0f ? 2.5f : breakaway.respawnDelay, 0.5f, 10f);
            breakaway.respawn = true;
            breakaway.reactToPlayerOnly = true;
            MarkFeature(target, presetName, "BreakawayPlatform", "BreakawayPlatform", "CrashFallGuysReference");
        }

        void ConfigureArenaCover(GameObject target, string presetName)
        {
            EnsureSolidBoxCollider(target);
            MarkFeature(target, presetName, "ArenaCover", "SolidCover", "HadesDoomReference");
        }

        void ConfigureBossEnemy(GameObject target, string presetName)
        {
            ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.EnemyAttack);

            var health = EnsureComponent<VWS.EnemyHealth>(target);
            health.maxHP = Mathf.Max(health.maxHP, 160);
            health.healthDropChance = Mathf.Clamp01(Mathf.Max(health.healthDropChance, 0.35f));

            var ai = EnsureComponent<VWS.EnemyAI_NavMesh>(target);
            ai.detectionRange = Mathf.Max(ai.detectionRange, 18f);
            ai.stopDistance = Mathf.Max(ai.stopDistance, 2f);
            ai.attackReach = Mathf.Max(ai.attackReach, 2.3f);
            ai.contactDamage = Mathf.Max(ai.contactDamage, Mathf.Max(12, enemyDamage * 4));
            ai.attackSpeed = Mathf.Clamp(ai.attackSpeed <= 0f ? 0.7f : ai.attackSpeed, 0.45f, 0.85f);
            ai.attackAnimationSpeed = Mathf.Clamp(ai.attackAnimationSpeed <= 0f ? 1.15f : ai.attackAnimationSpeed, 0.9f, 1.4f);
            ai.contactInterval = 1f / Mathf.Max(0.05f, ai.attackSpeed);

            var agent = EnsureComponent<NavMeshAgent>(target);
            agent.radius = Mathf.Max(agent.radius, 0.7f);
            agent.height = Mathf.Max(agent.height, 2.2f);
            agent.speed = Mathf.Clamp(agent.speed <= 0f ? 2.4f : agent.speed, 1.6f, 3.2f);
            agent.stoppingDistance = Mathf.Max(agent.stoppingDistance, ai.stopDistance);

            target.transform.localScale = Vector3.Max(target.transform.localScale, Vector3.one * 1.25f);
            ConfigureCharacterVisualSafety(target, isPlayer: false, usesNavMesh: true, allowVerticalMotion: false);
            MarkFeature(target, presetName, "BossEnemy", "EnemyCore", "BossTuning", "ArenaReference", "NavMesh");
        }

        void ConfigureZombieEnemy(GameObject target, string presetName)
        {
            ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.EnemyAttack);

            var health = EnsureComponent<VWS.EnemyHealth>(target);
            health.maxHP = Mathf.Clamp(health.maxHP <= 0 ? 35 : health.maxHP, 25, 60);
            health.healthDropChance = Mathf.Clamp01(Mathf.Min(health.healthDropChance, 0.45f));

            var ai = EnsureComponent<VWS.EnemyAI_NavMesh>(target);
            ai.detectionRange = Mathf.Max(ai.detectionRange, 14f);
            ai.stopDistance = Mathf.Max(ai.stopDistance, 1.15f);
            ai.attackReach = Mathf.Max(ai.attackReach, 1.6f);
            ai.contactDamage = Mathf.Clamp(ai.contactDamage <= 0 ? enemyDamage : ai.contactDamage, 2, 8);
            ai.attackSpeed = Mathf.Clamp(ai.attackSpeed <= 0f ? 0.45f : ai.attackSpeed, 0.25f, 0.6f);
            ai.attackAnimationSpeed = Mathf.Clamp(ai.attackAnimationSpeed <= 0f ? 0.9f : ai.attackAnimationSpeed, 0.7f, 1.1f);
            ai.contactInterval = 1f / Mathf.Max(0.05f, ai.attackSpeed);

            var agent = EnsureComponent<NavMeshAgent>(target);
            agent.speed = Mathf.Clamp(agent.speed <= 0f ? 1.8f : agent.speed, 1.2f, 2.2f);
            agent.angularSpeed = Mathf.Max(agent.angularSpeed, 180f);
            agent.stoppingDistance = Mathf.Max(agent.stoppingDistance, ai.stopDistance);

            ConfigureCharacterVisualSafety(target, isPlayer: false, usesNavMesh: true, allowVerticalMotion: false);
            MarkFeature(target, presetName, "ZombieEnemy", "EnemyCore", "ZombieTuning", "Left4DeadReference", "NavMesh");
        }

        void ConfigureTreasureCollectible(GameObject target, string presetName)
        {
            ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.ItemPickup);
            var bob = EnsureComponent<VWS.PickupBob>(target);
            bob.rotateSpeed = Mathf.Max(60f, bob.rotateSpeed);
            bob.bobHeight = Mathf.Max(0.08f, bob.bobHeight);
            MarkFeature(target, presetName, "TreasureCollectible", "ItemPickup", "PickupBob", "ExplorationReference");
        }

        void ConfigureExplorationLandmark(GameObject target, string presetName)
        {
            EnsureSolidBoxCollider(target);
            var spin = EnsureComponent<VWS.PrefabSpinMotion>(target);
            spin.localAxis = Vector3.up;
            spin.degreesPerSecond = 18f;
            spin.useLocalSpace = false;
            MarkFeature(target, presetName, "ExplorationLandmark", "Landmark", "SlowSpin", "ZeldaPikminReference");
        }

        void ConfigurePuzzlePressurePlate(GameObject target, string presetName)
        {
            EnsureBoxTrigger(target);
            var plate = EnsureComponent<VWS.PressurePlate>(target);
            if (plate.targets == null)
                plate.targets = Array.Empty<VWS.DoorController>();
            MarkFeature(target, presetName, "PressurePlate", "PuzzlePressurePlate", "PortalReference");
        }

        void ConfigurePuzzleMovableBox(GameObject target, string presetName)
        {
            EnsureSolidBoxCollider(target);
            var body = EnsureComponent<Rigidbody>(target);
            body.useGravity = true;
            body.mass = Mathf.Max(1f, body.mass);
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var box = EnsureComponent<VWS.MovableBox>(target);
            box.pushForce = Mathf.Max(3f, box.pushForce);
            MarkFeature(target, presetName, "MovableBox", "PuzzleMovableBox", "RigidbodyPush", "PortalReference");
        }

        void ConfigureLockedExitGoal(GameObject target, string presetName)
        {
            ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.Goal);
            var goal = EnsureComponent<VWS.GoalTrigger>(target);
            goal.requiredItems = Mathf.Max(1, requiredItems);
            MarkFeature(target, presetName, "LockedExitGoal", "Goal", "RequiresCollectible", "PuzzleReference");
        }

        void ConfigureSafeRoomCheckpoint(GameObject target, string presetName)
        {
            ApplySelectedFeature(target, VARCOSelectedFeatureApplicatorWindow.FeatureKind.Checkpoint);
            MarkFeature(target, presetName, "SafeRoomCheckpoint", "Checkpoint", "SafeRoom", "ZombieSurvivalReference");
        }

        void ConfigureSurvivalSpawnMarker(GameObject target, string presetName)
        {
            EnsureBoxTrigger(target);
            MarkFeature(target, presetName, "SpawnMarker", "SpawnMarker", "WaveManagerTarget", "ArenaSurvivalReference");
        }

        void ApplySelectedFeature(GameObject target, VARCOSelectedFeatureApplicatorWindow.FeatureKind feature)
        {
            VARCOSelectedFeatureApplicatorWindow.ApplyFeature(target, feature, CreateFeatureOptions(), useUndo: false);
        }

        FeatureOptions CreateFeatureOptions()
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

        void ConfigureCharacterVisualSafety(GameObject target, bool isPlayer, bool usesNavMesh, bool allowVerticalMotion)
        {
            var anchor = EnsureComponent<VWS.CharacterInitialYAnchor>(target);
            if (!anchor.HasStoredInitialY)
                anchor.CaptureCurrentYAsInitial();
            anchor.ConfigureForRole(isPlayer, usesNavMesh, allowVerticalMotion);

            var align = EnsureComponent<VWS.RuntimeGroundAlign>(target);
            align.alignOnEnable = true;
            align.alignVisualChildrenOnly = true;
            align.continuous = false;
            align.useRootY = true;
            align.alignDuration = 0f;
            align.alignFramesAfterEnable = isPlayer ? 12 : 6;
            align.footClearance = isPlayer ? 0.08f : 0.05f;

            var animator = target.GetComponentInChildren<Animator>(true);
            if (animator)
                animator.applyRootMotion = false;

            VARCOPrefabRepairUtility.RepairGameplayPrefab(
                target,
                isPlayer ? "Player" : usesNavMesh ? "Enemy" : "",
                AssetDatabase.GetAssetPath(target),
                logLines);
        }

        AnimatorController CreateAnimationController()
        {
            if (!idleClip)
            {
                Log("오류: Idle 클립은 반드시 필요합니다.");
                return null;
            }

            EnsureFolder(AnimationOutputFolder);
            var prefix = string.IsNullOrWhiteSpace(animationSearchText)
                ? "VARCO_Prefab"
                : SafeFileName(animationSearchText.Trim());
            var controllerPath = AnimationOutputFolder + "/" + prefix + "_PrefabController.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) && overwriteAnimationController)
                AssetDatabase.DeleteAsset(controllerPath);
            controllerPath = overwriteAnimationController ? controllerPath : AssetDatabase.GenerateUniqueAssetPath(controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            ConfigureAnimatorController(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("완료: AnimatorController 생성 " + controllerPath);
            return controller;
        }

        void ConfigureAnimatorController(AnimatorController controller)
        {
            EnsureAnimatorParameter(controller, "IsWalk", AnimatorControllerParameterType.Bool);
            EnsureAnimatorParameter(controller, "IsRun", AnimatorControllerParameterType.Bool);
            EnsureAnimatorParameter(controller, "IsJump", AnimatorControllerParameterType.Bool);
            EnsureAnimatorParameter(controller, "IsAttack", AnimatorControllerParameterType.Trigger);
            EnsureAnimatorParameter(controller, "IsDead", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            ClearStateMachine(sm);

            var idle = AddState(sm, "Idle", idleClip, new Vector3(240f, 80f, 0f));
            sm.defaultState = idle;

            AnimatorState walk = null;
            if (walkClip)
            {
                walk = AddState(sm, "Walk", walkClip, new Vector3(500f, 80f, 0f));
                AddBoolTransition(idle, walk, "IsWalk", true);
                AddBoolTransition(walk, idle, "IsWalk", false);
            }

            AnimatorState run = null;
            if (runClip)
            {
                run = AddState(sm, "Run", runClip, new Vector3(740f, 80f, 0f));
                AddBoolTransition(idle, run, "IsRun", true);
                AddBoolTransition(run, idle, "IsRun", false);
                if (walk != null)
                {
                    AddBoolTransition(walk, run, "IsRun", true);
                    AddBoolTransition(run, walk, "IsRun", false);
                }
            }

            if (jumpClip)
                AddBoolAction(sm, idle, "Jump", jumpClip, "IsJump", new Vector3(240f, 280f, 0f));
            if (attackClip)
                AddTriggeredAction(sm, idle, "Attack", attackClip, "IsAttack", new Vector3(500f, 280f, 0f));
            if (deathClip)
            {
                var death = AddState(sm, "Death", deathClip, new Vector3(500f, 470f, 0f));
                var transition = sm.AddAnyStateTransition(death);
                transition.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
                transition.hasExitTime = false;
                transition.duration = 0.05f;
                transition.canTransitionToSelf = false;
            }
        }

        void AssignAnimatorController(GameObject target, RuntimeAnimatorController controller)
        {
            var animator = target.GetComponentInChildren<Animator>(true);
            if (!animator)
                animator = target.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            Log("완료: " + target.name + "에 AnimatorController 연결");
        }

        void AutoDetectAnimationClips()
        {
            var selected = CurrentSelectionGameObject();
            var prefix = !string.IsNullOrWhiteSpace(animationSearchText)
                ? animationSearchText
                : selected ? selected.name : "";
            if (string.IsNullOrWhiteSpace(prefix))
            {
                Log("확인 필요: 선택 모델 또는 검색어가 필요합니다.");
                return;
            }

            animationSearchText = prefix;
            var clips = LoadAnimationClips();
            idleClip = FindBestClip(clips, prefix, "idle", "stand", "대기");
            walkClip = FindBestClip(clips, prefix, "walk", "걷");
            runClip = FindBestClip(clips, prefix, "run", "sprint", "dash", "뛰");
            jumpClip = FindBestClip(clips, prefix, "jump", "점프");
            attackClip = FindBestClip(clips, prefix, "attack", "atk", "공격");
            deathClip = FindBestClip(clips, prefix, "death", "die", "dead", "죽");
            Log("완료: '" + prefix + "' 기준 애니메이션 클립 자동 검색");
        }

        void ValidateAndRepairPrefab(GameObject target, bool repair, List<string> output)
        {
            var issues = new List<string>();
            var marker = EnsureComponent<VWS.PrefabFeatureMarker>(target);

            if (target.GetComponentsInChildren<Renderer>(true).Length == 0)
                issues.Add("WARN: Renderer가 없어 화면에 보이지 않을 수 있습니다.");

            var animator = target.GetComponentInChildren<Animator>(true);
            if (animator && animator.applyRootMotion)
            {
                issues.Add("FIX: Animator Root Motion이 켜져 있어 Y축/이동 충돌 가능성이 있습니다.");
                if (repair)
                    animator.applyRootMotion = false;
            }

            var thirdPerson = target.GetComponent<VWS.PlayerController_ThirdPerson>();
            var platform = target.GetComponent<VWS.PlayerController_Platform>();
            if (thirdPerson && platform)
            {
                issues.Add("FIX: 3인칭 플레이어와 플랫폼 플레이어 컨트롤러가 동시에 있습니다.");
                if (repair)
                {
                    if ((marker.prefabPreset ?? "").Contains("Platform"))
                        RemoveComponent<VWS.PlayerController_ThirdPerson>(target);
                    else
                        RemoveComponent<VWS.PlayerController_Platform>(target);
                }
            }

            if ((thirdPerson || platform) && target.GetComponent<NavMeshAgent>())
            {
                issues.Add("FIX: 플레이어에 NavMeshAgent가 붙어 있어 입력 이동과 충돌할 수 있습니다.");
                if (repair)
                    RemoveComponent<NavMeshAgent>(target);
            }

            if (platform)
            {
                var rb = target.GetComponent<Rigidbody>();
                if (rb)
                {
                    issues.Add("FIX: 플랫폼 플레이어에 Rigidbody가 있어 CharacterController와 충돌할 수 있습니다.");
                    if (repair)
                        RemoveComponent<Rigidbody>(target);
                }
            }

            if (thirdPerson)
                ConfigureCharacterVisualSafety(target, isPlayer: true, usesNavMesh: false, allowVerticalMotion: false);
            if (platform)
                ConfigureCharacterVisualSafety(target, isPlayer: true, usesNavMesh: false, allowVerticalMotion: true);

            var enemy = target.GetComponent<VWS.EnemyAI_NavMesh>() || target.GetComponent<VWS.EnemyHealth>();
            if (enemy)
            {
                if (!target.GetComponent<NavMeshAgent>())
                {
                    issues.Add("FIX: 적 기능에는 NavMeshAgent가 필요합니다.");
                    if (repair)
                        EnsureComponent<NavMeshAgent>(target);
                }
                ConfigureCharacterVisualSafety(target, isPlayer: false, usesNavMesh: true, allowVerticalMotion: false);
            }

            if (repair)
            {
                var roleHint = marker.role;
                if (string.IsNullOrWhiteSpace(roleHint))
                    roleHint = thirdPerson || platform ? "Player" : enemy ? "Enemy" : "";
                if (!string.IsNullOrWhiteSpace(roleHint)
                    && VARCOPrefabRepairUtility.RepairGameplayPrefab(target, roleHint, AssetDatabase.GetAssetPath(target), output))
                {
                    issues.Add("FIX: 애니메이션/이동 안정성 기본값을 자동 보정했습니다.");
                }
            }

            var door = target.GetComponent<VWS.DoorController>();
            if (door)
            {
                if (door.openOffset == Vector3.zero)
                {
                    issues.Add("FIX: 문 열림 offset이 0입니다.");
                    if (repair)
                        door.openOffset = Vector3.up * Mathf.Max(0.5f, openDistance);
                }
                EnsureSolidBoxCollider(target);
            }

            ValidateTriggerComponent<VWS.ItemPickup>(target, "수집 아이템", repair, issues);
            ValidateTriggerComponent<VWS.HealthPickup>(target, "회복 아이템", repair, issues);
            ValidateTriggerComponent<VWS.HazardZone>(target, "위험 구역", repair, issues);
            ValidateTriggerComponent<VWS.Checkpoint>(target, "체크포인트", repair, issues);
            ValidateTriggerComponent<VWS.GoalTrigger>(target, "목표 지점", repair, issues);
            ValidateTriggerComponent<VWS.PressurePlate>(target, "압력판", repair, issues);
            ValidateTriggerComponent<VWS.BouncePad>(target, "바운스 패드", repair, issues);

            if (target.GetComponent<VWS.PrefabSpinMotion>())
                ValidateSolidCollider(target, "회전 장애물", repair, issues);

            if (target.GetComponent<VWS.BreakawayPlatform>())
                ValidateSolidCollider(target, "사라지는 발판", repair, issues);

            if (target.GetComponent<VWS.MovableBox>())
            {
                ValidateSolidCollider(target, "밀 수 있는 박스", repair, issues);
                if (!target.GetComponent<Rigidbody>())
                {
                    issues.Add("FIX: 밀 수 있는 박스 기능에는 Rigidbody가 필요합니다.");
                    if (repair)
                    {
                        var body = EnsureComponent<Rigidbody>(target);
                        body.useGravity = true;
                        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                    }
                }
            }

            if (target.GetComponent<VWS.DoorController>() && target.GetComponent<VWS.PrefabPingPongMotion>())
                issues.Add("CONFIRM: 문 열림과 왕복 이동이 동시에 있어 수업용 예제에서는 의도가 불명확할 수 있습니다.");

            if (target.GetComponent<VWS.BouncePad>() && target.GetComponent<VWS.BreakawayPlatform>())
                issues.Add("CONFIRM: 바운스 패드와 사라지는 발판이 동시에 있어 학생이 의도를 이해하기 어려울 수 있습니다.");

            if (target.GetComponent<VWS.PressurePlate>() && target.GetComponent<VWS.MovableBox>())
                issues.Add("CONFIRM: 압력판과 이동 박스가 같은 오브젝트에 있어 퍼즐 구조가 불명확할 수 있습니다. 보통 별도 프리팹으로 나눕니다.");

            marker.validationPassed = !issues.Any(line => line.StartsWith("FIX", StringComparison.Ordinal) || line.StartsWith("WARN", StringComparison.Ordinal));
            marker.validationSummary = issues.Count == 0 ? "PASS: 충돌 가능성이 큰 문제를 찾지 못했습니다." : string.Join("\n", issues.ToArray());
            if (!marker.validationPassed && repair)
                marker.validationSummary += "\n자동 수정이 가능한 항목은 처리했습니다.";

            output.Add("검증 결과: " + target.name);
            if (issues.Count == 0)
                output.Add("PASS: 충돌 가능성이 큰 문제를 찾지 못했습니다.");
            else
                output.AddRange(issues);
        }

        void ValidateTriggerComponent<T>(GameObject target, string label, bool repair, List<string> issues) where T : Component
        {
            if (!target.GetComponent<T>())
                return;

            var box = target.GetComponent<BoxCollider>();
            if (!box || !box.isTrigger)
            {
                issues.Add("FIX: " + label + " 기능에는 Trigger BoxCollider가 필요합니다.");
                if (repair)
                    EnsureBoxTrigger(target);
            }
        }

        void ValidateSolidCollider(GameObject target, string label, bool repair, List<string> issues)
        {
            var collider = target.GetComponent<Collider>();
            if (!collider || collider.isTrigger)
            {
                issues.Add("FIX: " + label + " 기능에는 단단한 Collider가 필요합니다.");
                if (repair)
                    EnsureSolidBoxCollider(target);
            }
        }

        void MarkFeature(GameObject target, string preset, string role, params string[] features)
        {
            var marker = EnsureComponent<VWS.PrefabFeatureMarker>(target);
            marker.prefabPreset = preset;
            marker.role = role;
            foreach (var feature in features)
                marker.AddFeature(feature);
        }

        T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component ? component : target.AddComponent<T>();
        }

        void RemoveComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component)
                DestroyImmediate(component);
        }

        BoxCollider EnsureSolidBoxCollider(GameObject target)
        {
            var box = EnsureComponent<BoxCollider>(target);
            var bounds = CalculateLocalBounds(target);
            box.isTrigger = false;
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size, Vector3.one * 0.5f);
            return box;
        }

        BoxCollider EnsureBoxTrigger(GameObject target)
        {
            var box = EnsureComponent<BoxCollider>(target);
            var bounds = CalculateLocalBounds(target);
            box.isTrigger = true;
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size, Vector3.one * 0.75f);
            return box;
        }

        Bounds CalculateLocalBounds(GameObject target)
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

                var min = renderer.bounds.min;
                var max = renderer.bounds.max;
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

        GameObject CurrentSelectionGameObject()
        {
            if (Selection.activeGameObject)
                return Selection.activeGameObject;
            if (Selection.activeObject is Component component)
                return component.gameObject;
            return Selection.activeObject as GameObject;
        }

        static PrefabPresetKind DrawObjectPresetPopup(string label, PrefabPresetKind current)
        {
            if (IsCharacterPreset(current))
                current = ObjectPresetKinds[0];

            var labels = ObjectPresetKinds.Select(kind => PresetLabels[(int)kind]).ToArray();
            var index = Mathf.Max(0, Array.IndexOf(ObjectPresetKinds, current));
            index = EditorGUILayout.Popup(new GUIContent(label), index, labels);
            return ObjectPresetKinds[Mathf.Clamp(index, 0, ObjectPresetKinds.Length - 1)];
        }

        static FunctionBundleKind DrawObjectFunctionPopup(string label, FunctionBundleKind current)
        {
            if (IsCharacterFunctionBundle(current))
                current = ObjectFunctionKinds[0];

            var labels = ObjectFunctionKinds.Select(kind => FunctionLabels[(int)kind]).ToArray();
            var index = Mathf.Max(0, Array.IndexOf(ObjectFunctionKinds, current));
            index = EditorGUILayout.Popup(new GUIContent(label), index, labels);
            return ObjectFunctionKinds[Mathf.Clamp(index, 0, ObjectFunctionKinds.Length - 1)];
        }

        static bool IsCharacterPreset(PrefabPresetKind preset)
        {
            return preset == PrefabPresetKind.AdventurePlayer
                || preset == PrefabPresetKind.PlatformPlayer
                || preset == PrefabPresetKind.ChaserEnemy
                || preset == PrefabPresetKind.BossEnemy
                || preset == PrefabPresetKind.ZombieEnemy;
        }

        static bool IsCharacterFunctionBundle(FunctionBundleKind bundle)
        {
            return bundle == FunctionBundleKind.PlatformJumpAbility
                || bundle == FunctionBundleKind.BossTuning
                || bundle == FunctionBundleKind.ZombieTuning;
        }

        static bool IsCharacterLikeSelection(GameObject target)
        {
            if (!target)
                return false;

            if (target.GetComponent<VWS.PlayerController_ThirdPerson>()
                || target.GetComponent<VWS.PlayerController_Platform>()
                || target.GetComponent<VWS.PlayerHealth>()
                || target.GetComponent<VWS.EnemyAI_NavMesh>()
                || target.GetComponent<VWS.EnemyHealth>())
                return true;

            var marker = target.GetComponent<VWS.PrefabFeatureMarker>();
            if (marker && (string.Equals(marker.role, "Player", StringComparison.OrdinalIgnoreCase)
                || string.Equals(marker.role, "Enemy", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (target.GetComponentInChildren<SkinnedMeshRenderer>(true))
                return true;

            var path = AssetDatabase.GetAssetPath(target).ToLowerInvariant();
            var name = target.name.ToLowerInvariant();
            return ContainsCharacterToken(path) || ContainsCharacterToken(name);
        }

        static bool ContainsCharacterToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.Contains("character")
                || value.Contains("player")
                || value.Contains("enemy")
                || value.Contains("zombie")
                || value.Contains("avatar")
                || value.Contains("humanoid");
        }

        static PrefabPresetKind DrawEnumPopup(string label, PrefabPresetKind current, GUIContent[] labels)
        {
            var index = EditorGUILayout.Popup(new GUIContent(label), (int)current, labels);
            return (PrefabPresetKind)Mathf.Clamp(index, 0, labels.Length - 1);
        }

        static FunctionBundleKind DrawEnumPopup(string label, FunctionBundleKind current, GUIContent[] labels)
        {
            var index = EditorGUILayout.Popup(new GUIContent(label), (int)current, labels);
            return (FunctionBundleKind)Mathf.Clamp(index, 0, labels.Length - 1);
        }

        string PresetSuffix(PrefabPresetKind preset)
        {
            return preset.ToString();
        }

        string FunctionSuffix(FunctionBundleKind bundle)
        {
            return bundle.ToString();
        }

        void EnsureTagExists(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || tag == "Player")
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

        static void EnsureAnimatorParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(parameter => parameter.name == name))
                return;
            controller.AddParameter(name, type);
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion, Vector3 position)
        {
            var state = sm.AddState(name, position);
            state.motion = motion;
            state.writeDefaultValues = true;
            return state;
        }

        static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value)
        {
            var transition = from.AddTransition(to);
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
            transition.hasExitTime = false;
            transition.duration = 0.08f;
        }

        static void AddTriggeredAction(AnimatorStateMachine sm, AnimatorState idle, string name, Motion motion, string trigger, Vector3 position)
        {
            var state = AddState(sm, name, motion, position);
            state.speed = SpeedForTargetDuration(motion, 0.9f);
            var enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = false;

            var exit = state.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.duration = 0.08f;
        }

        static void AddBoolAction(AnimatorStateMachine sm, AnimatorState idle, string name, Motion motion, string parameter, Vector3 position)
        {
            var state = AddState(sm, name, motion, position);
            state.speed = SpeedForTargetDuration(motion, 1.1f);
            var enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, parameter);
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = false;

            var exit = state.AddTransition(idle);
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter);
            exit.hasExitTime = false;
            exit.duration = 0.08f;
        }

        static float SpeedForTargetDuration(Motion motion, float targetDuration)
        {
            if (motion is AnimationClip clip && clip.length > targetDuration)
                return Mathf.Clamp(clip.length / Mathf.Max(0.05f, targetDuration), 1f, 8f);
            return 1f;
        }

        static void ClearStateMachine(AnimatorStateMachine sm)
        {
            foreach (var state in sm.states.ToArray())
                sm.RemoveState(state.state);
            foreach (var transition in sm.anyStateTransitions.ToArray())
                sm.RemoveAnyStateTransition(transition);
        }

        static List<ClipCandidate> LoadAnimationClips()
        {
            var result = new List<ClipCandidate>();
            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                        result.Add(new ClipCandidate(path, clip));
                }
            }
            return result;
        }

        static AnimationClip FindBestClip(List<ClipCandidate> clips, string prefix, params string[] keywords)
        {
            var normalizedPrefix = Normalize(prefix);
            var prefixTokens = normalizedPrefix.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            ClipCandidate best = null;
            var bestScore = int.MinValue;

            foreach (var candidate in clips)
            {
                var haystack = Normalize(candidate.path + "/" + candidate.clip.name);
                var score = 0;
                if (!string.IsNullOrEmpty(normalizedPrefix) && haystack.Contains(normalizedPrefix))
                    score += 80;
                foreach (var token in prefixTokens)
                {
                    if (token.Length >= 3 && haystack.Contains(token))
                        score += 15;
                }
                foreach (var keyword in keywords.Select(Normalize))
                {
                    if (haystack.Contains(keyword))
                        score += 100;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return bestScore >= 100 ? best?.clip : null;
        }

        static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value)
                ? ""
                : value.ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
        }

        static void EnsureFolder(string folder)
        {
            folder = folder.Replace("\\", "/").Trim('/');
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parts = folder.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static void RevealPresetKitRoot()
        {
            EnsureFolder(PresetKitRoot);
            EditorUtility.RevealInFinder(Path.GetFullPath(PresetKitRoot));
        }

        static void RevealFolder(string folder)
        {
            EnsureFolder(folder);
            EditorUtility.RevealInFinder(Path.GetFullPath(folder));
        }

        static string SafeFileName(string value)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace(" ", "_");
        }

        void Log(string message)
        {
            logLines.Add(message);
            Debug.Log("[VARCO 프리팹 만들기] " + message);
        }

        class ClipCandidate
        {
            public readonly string path;
            public readonly AnimationClip clip;

            public ClipCandidate(string path, AnimationClip clip)
            {
                this.path = path;
                this.clip = clip;
            }
        }
    }
}
#endif
