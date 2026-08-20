#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VWS = VARCO_Workshop;
using Object = UnityEngine.Object;

namespace VARCO_Workshop.Editor
{
    public class VARCOBlockCodingBuilderWindow : EditorWindow
    {
        const string RegistryPath = "Assets/ScriptableObjects/SoundEvents/WorkshopSoundEventRegistry.asset";

        static readonly Dictionary<VWS.GenreType, string> ProfileByGenre = new Dictionary<VWS.GenreType, string>
        {
            { VWS.GenreType.Platform, "Assets/ScriptableObjects/GameProfiles/VARCO_Platform_Profile.asset" },
            { VWS.GenreType.Arena, "Assets/ScriptableObjects/GameProfiles/VARCO_Arena_Profile.asset" },
            { VWS.GenreType.Exploration, "Assets/ScriptableObjects/GameProfiles/VARCO_Exploration_Profile.asset" },
            { VWS.GenreType.Puzzle, "Assets/ScriptableObjects/GameProfiles/VARCO_Puzzle_Profile.asset" }
        };

        enum BlockGenre
        {
            Platform,
            Arena,
            Exploration,
            Puzzle,
            Common
        }

        enum BlockAction
        {
            AutoConnectSelected,
            ConnectPlayer,
            ConnectPlatformPlayer,
            ConnectEnemy,
            ConnectItemPickup,
            ConnectHealthPickup,
            ConnectGoal,
            ConnectDoor,
            ConnectPressurePlate,
            ConnectHazardZone,
            ConnectMovingPlatform,
            ConnectMovableBox,
            ConnectCheckpoint,
            ConnectArenaCover,
            AddCountdownTimer,
            CreateStarterBlocks
        }

        readonly List<string> logLines = new List<string>();
        Vector2 scroll;
        BlockGenre genre = BlockGenre.Platform;
        BlockAction action = BlockAction.AutoConnectSelected;
        GameObject selectedObject;
        RuntimeAnimatorController animatorController;
        bool connectEnemyToWave = true;
        bool saveEnemyAsPrefab = true;
        int waveIndex;
        int requiredItems;
        int healAmount = 25;
        int hazardDps = 15;
        float movingPlatformDistance = 4f;
        float movingPlatformSpeed = 1.2f;
        float countdownSeconds = 90f;
        VARCOAutoConnectorWindow.CameraViewPreset cameraView = VARCOAutoConnectorWindow.CameraViewPreset.SideView;

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/블록코딩/한글 블록 조립기", priority = -8)]
        public static void Open()
        {
            var window = GetWindow<VARCOBlockCodingBuilderWindow>("한글 블록 조립기");
            window.minSize = new Vector2(520, 680);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/블록코딩/선택 모델들 자동 판단 연결", priority = -10)]
        public static void AutoConnectSelectionMenu()
        {
            var targets = GetSelectedRootSceneObjects();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("선택 모델 자동 판단", "하이어라키에서 연결할 모델을 하나 이상 선택하세요.", "확인");
                return;
            }

            var window = GetWindow<VARCOBlockCodingBuilderWindow>("한글 블록 조립기");
            window.minSize = new Vector2(520, 680);
            window.genre = ToBlockGenre(GuessGenreFromScene(SceneManager.GetActiveScene().path));
            window.cameraView = GetDefaultCameraView(window.genre);
            window.ExecuteSelectedModelsAutoConnect(targets);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/블록코딩/선택 에셋 배치 후 자동 판단 연결", priority = -11)]
        public static void PlaceAndAutoConnectSelectedAssetsMenu()
        {
            var assets = GetSelectedProjectGameObjectAssets();
            if (assets.Count == 0)
            {
                EditorUtility.DisplayDialog("선택 에셋 자동 배치", "프로젝트 창에서 배치할 프리팹, 모델 에셋, 또는 그 에셋이 들어 있는 폴더를 하나 이상 선택하세요.", "확인");
                return;
            }

            var window = GetWindow<VARCOBlockCodingBuilderWindow>("한글 블록 조립기");
            window.minSize = new Vector2(520, 680);
            window.genre = ToBlockGenre(GuessGenreFromScene(SceneManager.GetActiveScene().path));
            window.cameraView = GetDefaultCameraView(window.genre);
            window.ExecuteProjectAssetsPlaceAndAutoConnect(assets);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/블록코딩/현재 씬 안전 보정", priority = -7)]
        public static void RunSafetyPassMenu()
        {
            var log = new List<string>();
            RunSafetyPassForBuildScenes(log);
            Debug.Log("[VARCO 블록코딩 안전 보정]\n" + string.Join("\n", log));
        }

        public static void RunSafetyPassForBuildScenes(List<string> log)
        {
            var originalScene = SceneManager.GetActiveScene().path;
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrWhiteSpace(s.path) && File.Exists(s.path))
                .Select(s => s.path)
                .Distinct()
                .ToList();

            foreach (var scenePath in scenes)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                RunSafetyPassForCurrentScene(log, saveScene: false);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AddLog(log, "안전 보정 저장: " + scenePath);
            }

            if (!string.IsNullOrWhiteSpace(originalScene) && File.Exists(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
        }

        public static void RunSafetyPassForCurrentScene(List<string> log, bool saveScene)
        {
            var scene = SceneManager.GetActiveScene();
            var genreType = GuessGenreFromScene(scene.path);

            EnsureSceneBasics(genreType, log);
            EnsureSoundTriggers(log);
            EnsureGoalCounters(log);
            EnsureMovingPlatformWaypoints(log);
            EnsurePressurePlateTargets(log);
            EnsureCheckpointDeathZones(log);
            EnsureBgmAudioSource(genreType, log);

            if (saveScene)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        void OnEnable()
        {
            cameraView = GetDefaultCameraView(genre);
            logLines.Clear();
            logLines.Add("준비됨. 장르와 블록을 고른 뒤 선택한 모델에 기능을 연결하거나 기본 블록을 만들 수 있습니다.");
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(8);
            EditorGUILayout.LabelField("VARCO 한글 블록 조립기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "하이어라키에서 모델을 선택하고 블록을 고르면 플레이어, 적, 아이템, 목표, 퍼즐, 이동 발판 같은 기능을 바로 붙입니다. 모델이 없을 때는 기본 블록 만들기로 샘플 구성을 생성할 수 있습니다.",
                MessageType.Info);

            DrawGenreAndAction();
            DrawTarget();
            DrawOptions();
            DrawActions();
            DrawLog();

            EditorGUILayout.EndScrollView();
        }

        void DrawGenreAndAction()
        {
            DrawHeader("1. 장르와 블록");

            EditorGUI.BeginChangeCheck();
            genre = DrawKoreanEnumPopup("장르", genre, BlockGenreLabel);
            if (EditorGUI.EndChangeCheck())
            {
                cameraView = GetDefaultCameraView(genre);
                action = GetDefaultAction(genre);
            }

            action = DrawKoreanEnumPopup("블록", action, BlockActionLabel);
            EditorGUILayout.HelpBox(GetActionHint(action), MessageType.None);
        }

        void DrawTarget()
        {
            DrawHeader("2. 대상 모델");
            using (new EditorGUI.DisabledScope(!RequiresSelection(action)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    selectedObject = (GameObject)EditorGUILayout.ObjectField(
                        "선택한 모델",
                        selectedObject ? selectedObject : Selection.activeGameObject,
                        typeof(GameObject),
                        true);

                    if (GUILayout.Button("현재 선택 사용", GUILayout.Width(120)))
                        selectedObject = Selection.activeGameObject;
                }
            }

            if (RequiresSelection(action) && !selectedObject)
                EditorGUILayout.HelpBox("이 블록은 하이어라키에서 모델을 먼저 선택해야 연결할 수 있습니다.", MessageType.Warning);
        }

        void DrawOptions()
        {
            DrawHeader("3. 블록 옵션");

            if (action == BlockAction.AutoConnectSelected || action == BlockAction.ConnectPlayer || action == BlockAction.ConnectPlatformPlayer || action == BlockAction.ConnectEnemy)
            {
                animatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
                    "애니메이터 컨트롤러",
                    animatorController,
                    typeof(RuntimeAnimatorController),
                    false);
            }

            if (action == BlockAction.AutoConnectSelected || action == BlockAction.ConnectPlayer || action == BlockAction.ConnectPlatformPlayer)
                cameraView = VARCOAutoConnectorWindow.DrawCameraViewPopup("카메라 시점", cameraView);

            if (action == BlockAction.AutoConnectSelected || action == BlockAction.ConnectEnemy)
            {
                connectEnemyToWave = EditorGUILayout.ToggleLeft("웨이브 매니저에 연결", connectEnemyToWave);
                saveEnemyAsPrefab = EditorGUILayout.ToggleLeft("먼저 프리팹으로 저장/갱신", saveEnemyAsPrefab);
                waveIndex = EditorGUILayout.IntSlider("웨이브 번호", waveIndex, 0, 5);
            }

            if (action == BlockAction.ConnectGoal || action == BlockAction.CreateStarterBlocks)
                requiredItems = EditorGUILayout.IntSlider("필요 아이템 수", requiredItems, 0, 8);

            if (action == BlockAction.ConnectHealthPickup || action == BlockAction.CreateStarterBlocks)
                healAmount = EditorGUILayout.IntSlider("회복량", healAmount, 5, 100);

            if (action == BlockAction.ConnectHazardZone || action == BlockAction.CreateStarterBlocks)
                hazardDps = EditorGUILayout.IntSlider("위험 구역 초당 피해", hazardDps, 1, 50);

            if (action == BlockAction.ConnectMovingPlatform || action == BlockAction.CreateStarterBlocks)
            {
                movingPlatformDistance = EditorGUILayout.Slider("이동 거리", movingPlatformDistance, 1f, 12f);
                movingPlatformSpeed = EditorGUILayout.Slider("이동 속도", movingPlatformSpeed, 0.2f, 6f);
            }

            if (action == BlockAction.AddCountdownTimer || action == BlockAction.CreateStarterBlocks)
                countdownSeconds = EditorGUILayout.Slider("제한시간(초)", countdownSeconds, 10f, 300f);
        }

        void DrawActions()
        {
            DrawHeader("4. 실행");

            using (new EditorGUI.DisabledScope(RequiresSelection(action) && selectedObject == null))
            {
                var buttonLabel = action == BlockAction.AutoConnectSelected
                    ? "선택 모델 자동 판단해서 연결"
                    : RequiresSelection(action) ? "선택 모델에 블록 연결" : "블록 만들기";
                if (GUILayout.Button(buttonLabel, GUILayout.Height(38)))
                    ExecuteSelectedBlock();
            }

            var multiTargets = GetSelectedRootSceneObjects();
            using (new EditorGUI.DisabledScope(multiTargets.Count == 0))
            {
                if (GUILayout.Button("선택한 모델 " + multiTargets.Count + "개 모두 자동 판단 연결", GUILayout.Height(32)))
                    ExecuteSelectedModelsAutoConnect(multiTargets);
            }

            var selectedAssets = GetSelectedProjectGameObjectAssets();
            using (new EditorGUI.DisabledScope(selectedAssets.Count == 0))
            {
                if (GUILayout.Button("선택 에셋/폴더 " + selectedAssets.Count + "개 배치하고 자동 연결", GUILayout.Height(32)))
                    ExecuteProjectAssetsPlaceAndAutoConnect(selectedAssets);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("현재 씬 안전 보정", GUILayout.Height(30)))
                {
                    logLines.Clear();
                    RunSafetyPassForCurrentScene(logLines, saveScene: false);
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }

                if (GUILayout.Button("씬 건강 검사", GUILayout.Height(30)))
                    VARCOSceneHealthCheckWindow.Open();
            }

            if (GUILayout.Button("선택 모델 기능 자동 연결 열기", GUILayout.Height(26)))
                VARCOAutoConnectorWindow.Open();
        }

        void DrawLog()
        {
            if (logLines.Count == 0)
                return;

            DrawHeader("작업 로그");
            EditorGUILayout.TextArea(string.Join("\n", logLines), GUILayout.MinHeight(120));
        }

        void ExecuteSelectedBlock()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("한글 블록 조립기", "씬 블록을 바꾸기 전에 Unity Play 모드를 먼저 꺼주세요.", "확인");
                return;
            }

            selectedObject = selectedObject ? selectedObject : Selection.activeGameObject;
            if (RequiresSelection(action) && !selectedObject)
                return;

            logLines.Clear();
            Undo.SetCurrentGroupName("VARCO 한글 블록 조립기");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureSceneBasics(ToGenreType(genre), logLines);

            if (RequiresSelection(action))
                ConnectSelectedModel();
            else if (action == BlockAction.AddCountdownTimer)
                CreateOrUpdateCountdown();
            else if (action == BlockAction.CreateStarterBlocks)
                CreateStarterBlocks();

            RunSafetyPassForCurrentScene(logLines, saveScene: false);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
        }

        void ConnectSelectedModel()
        {
            var effectiveAction = action;
            var reason = "";
            if (action == BlockAction.AutoConnectSelected)
                effectiveAction = GuessBlockActionForSelectedModel(selectedObject, genre, out reason);

            ConnectModelWithAction(selectedObject, effectiveAction);

            if (action == BlockAction.AutoConnectSelected)
                AddLog(logLines, selectedObject.name + " 모델을 '" + BlockActionLabel(effectiveAction) + "' 블록으로 자동 판단했습니다. 이유: " + reason);
            AddLog(logLines, selectedObject.name + " 모델에 '" + BlockActionLabel(effectiveAction) + "' 블록을 연결했습니다.");
        }

        void ExecuteSelectedModelsAutoConnect(List<GameObject> targets)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("한글 블록 조립기", "씬 블록을 바꾸기 전에 Unity Play 모드를 먼저 꺼주세요.", "확인");
                return;
            }

            if (targets == null || targets.Count == 0)
                return;

            logLines.Clear();
            Undo.SetCurrentGroupName("VARCO 선택 모델 자동 판단 연결");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureSceneBasics(ToGenreType(genre), logLines);

            int connected = 0;
            foreach (var target in targets)
            {
                if (!target)
                    continue;

                var effectiveAction = GuessBlockActionForSelectedModel(target, genre, out var reason);
                ConnectModelWithAction(target, effectiveAction);
                AddLog(logLines, target.name + " → " + BlockActionLabel(effectiveAction) + " / " + reason);
                connected++;
            }

            RunSafetyPassForCurrentScene(logLines, saveScene: false);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            selectedObject = connected > 0 ? targets[0] : selectedObject;
            AddLog(logLines, "선택 모델 자동 판단 연결 완료: " + connected + "개");
        }

        void ExecuteProjectAssetsPlaceAndAutoConnect(List<GameObject> assets)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("한글 블록 조립기", "에셋을 배치하기 전에 Unity Play 모드를 먼저 꺼주세요.", "확인");
                return;
            }

            if (assets == null || assets.Count == 0)
                return;

            logLines.Clear();
            Undo.SetCurrentGroupName("VARCO 선택 에셋 배치 후 자동 연결");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureSceneBasics(ToGenreType(genre), logLines);

            var root = GameObject.Find("BC_AutoPlacedAssets");
            if (!root)
            {
                root = new GameObject("BC_AutoPlacedAssets");
                Undo.RegisterCreatedObjectUndo(root, "자동 배치 에셋 루트 생성");
            }

            var counts = new Dictionary<BlockAction, int>();
            var placed = new List<GameObject>();
            foreach (var asset in assets.OrderBy(AutoConnectSortKey).ThenBy(asset => asset.name))
            {
                var instance = InstantiateProjectAsset(asset);
                if (!instance)
                    continue;

                Undo.RegisterCreatedObjectUndo(instance, "선택 에셋 배치");
                instance.name = MakePlacedAssetName(asset.name);
                instance.transform.SetParent(root.transform, true);

                var effectiveAction = GuessBlockActionForSelectedModel(instance, genre, out var reason);
                var index = NextActionIndex(counts, effectiveAction);
                PlaceAutoInstantiatedObject(instance, effectiveAction, index);
                ConnectModelWithAction(instance, effectiveAction);

                placed.Add(instance);
                AddLog(logLines, asset.name + " 배치 → " + BlockActionLabel(effectiveAction) + " / " + reason);
            }

            RunSafetyPassForCurrentScene(logLines, saveScene: false);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);

            if (placed.Count > 0)
            {
                selectedObject = placed[0];
                Selection.objects = placed.Cast<Object>().ToArray();
            }

            AddLog(logLines, "선택 에셋 배치 및 자동 연결 완료: " + placed.Count + "개");
        }

        void ConnectModelWithAction(GameObject target, BlockAction effectiveAction)
        {
            var role = ResolveRole(effectiveAction);
            var platformPlayer = effectiveAction == BlockAction.ConnectPlatformPlayer;

            VARCOAutoConnectorWindow.ConnectFromFeatureBuilder(
                target,
                role,
                animatorController,
                connectEnemyToWave,
                saveEnemyAsPrefab,
                waveIndex,
                requiredItems,
                healAmount,
                hazardDps,
                movingPlatformDistance,
                movingPlatformSpeed,
                moveInFacingDirectionForPlayer: platformPlayer,
                cameraViewPreset: cameraView);
        }

        void CreateStarterBlocks()
        {
            var root = new GameObject("BC_" + genre + "_StarterBlocks");
            Undo.RegisterCreatedObjectUndo(root, "블록코딩 기본 블록 만들기");

            switch (genre)
            {
                case BlockGenre.Arena:
                    CreateArenaCover(root.transform);
                    CreateHealthPickup(root.transform, new Vector3(-4f, 0f, -3f));
                    CreateHazard(root.transform, new Vector3(4f, 0f, 3f), new Vector3(3f, 0.12f, 3f));
                    CreateOrUpdateCountdown();
                    break;
                case BlockGenre.Exploration:
                    CreateItemRing(root.transform, Mathf.Max(3, requiredItems), new Vector3(0f, 0f, 4f));
                    CreateGoal(root.transform, new Vector3(0f, 0.75f, 10f), Mathf.Max(1, requiredItems));
                    CreateHazard(root.transform, new Vector3(-4f, 0f, 6f), new Vector3(3f, 0.12f, 4f));
                    CreateCheckpoint(root.transform, new Vector3(4f, 0f, 2f));
                    CreateArenaCover(root.transform);
                    break;
                case BlockGenre.Puzzle:
                    CreateDoorAndPlate(root.transform, new Vector3(0f, 0f, 6f));
                    CreateMovableBox(root.transform, new Vector3(-3f, 0f, 2f));
                    CreateGoal(root.transform, new Vector3(0f, 0.75f, 11f), 0);
                    CreateArenaCover(root.transform);
                    break;
                default:
                    CreateItemRing(root.transform, Mathf.Max(3, requiredItems), new Vector3(0f, 0f, 4f));
                    CreateMovingPlatform(root.transform, new Vector3(0f, 1f, -2f));
                    CreateHazard(root.transform, new Vector3(-4f, 0f, 3f), new Vector3(3f, 0.12f, 3f));
                    CreateCheckpoint(root.transform, new Vector3(4f, 0f, 1f));
                    CreateGoal(root.transform, new Vector3(0f, 0.75f, 10f), 0);
                    CreateArenaCover(root.transform);
                    break;
            }

            AddLog(logLines, BlockGenreLabel(genre) + " 장르용 기본 블록을 만들었습니다.");
        }

        void CreateOrUpdateCountdown()
        {
            var gm = Object.FindFirstObjectByType<VWS.GameManager>();
            var target = gm ? gm.gameObject : new GameObject("BC_CountdownTimer");
            if (!gm)
                Undo.RegisterCreatedObjectUndo(target, "제한시간 타이머 생성");

            var timer = Object.FindFirstObjectByType<VWS.CountdownTimer>();
            if (!timer)
                timer = Undo.AddComponent<VWS.CountdownTimer>(target);

            Undo.RecordObject(timer, "제한시간 타이머 설정");
            timer.totalSeconds = countdownSeconds;
            timer.pauseWhenNotPlaying = true;
            EditorUtility.SetDirty(timer);
            AddLog(logLines, "제한시간을 " + Mathf.RoundToInt(countdownSeconds) + "초로 설정했습니다.");
        }

        static void EnsureSceneBasics(VWS.GenreType genreType, List<string> log)
        {
            EnsureTag("Player");

            var gm = Object.FindFirstObjectByType<VWS.GameManager>();
            if (!gm)
            {
                var root = new GameObject("VW_Bootstrap");
                Undo.RegisterCreatedObjectUndo(root, "Create VW_Bootstrap");
                gm = root.AddComponent<VWS.GameManager>();
                root.AddComponent<VWS.SceneBootstrap>();
                AddLog(log, "기본 게임 매니저와 씬 부트스트랩을 만들었습니다.");
            }

            Undo.RecordObject(gm, "Configure GameManager");
            gm.loadResultScenes = false;
            gm.clearSceneName = "Clear";
            gm.gameOverSceneName = "GameOver";

            if (ProfileByGenre.TryGetValue(genreType, out var profilePath))
            {
                var profile = AssetDatabase.LoadAssetAtPath<VWS.GameProfile>(profilePath);
                if (profile && gm.profile != profile)
                    gm.profile = profile;
            }

            EditorUtility.SetDirty(gm);
            EnsureMainCamera(log);
            EnsureDirectionalLight(log);
            EnsureWorkshopHud(log);
        }

        static void EnsureMainCamera(List<string> log)
        {
            var camera = Camera.main;
            if (!camera)
                camera = Object.FindFirstObjectByType<Camera>();

            if (camera)
            {
                Undo.RecordObject(camera.gameObject, "Configure Main Camera");
                camera.tag = "MainCamera";
                if (!camera.GetComponent<AudioListener>())
                    Undo.AddComponent<AudioListener>(camera.gameObject);
                EditorUtility.SetDirty(camera.gameObject);
                return;
            }

            var go = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(go, "Create Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 4f, -8f);
            go.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            AddLog(log, "메인 카메라를 만들었습니다.");
        }

        static void EnsureDirectionalLight(List<string> log)
        {
            if (Object.FindFirstObjectByType<Light>())
                return;

            var go = new GameObject("Directional Light");
            Undo.RegisterCreatedObjectUndo(go, "Create Directional Light");
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            AddLog(log, "기본 방향 조명을 만들었습니다.");
        }

        static void EnsureWorkshopHud(List<string> log)
        {
            var hud = Object.FindFirstObjectByType<VWS.VARCOGameHUD>();
            if (!hud)
            {
                var go = new GameObject("VARCO_GameHUD");
                Undo.RegisterCreatedObjectUndo(go, "Create VARCO Game HUD");
                hud = go.AddComponent<VWS.VARCOGameHUD>();
                AddLog(log, "VARCO 게임 HUD를 만들었습니다.");
            }

            var sceneGenre = GuessGenreFromScene(SceneManager.GetActiveScene().path);
            hud.fallbackGenre = sceneGenre;
            hud.hideWorkshopHud = true;
            hud.showHud = true;
            EditorUtility.SetDirty(hud);

            foreach (var legacy in Object.FindObjectsByType<VWS.WorkshopHUD>(FindObjectsSortMode.None))
            {
                legacy.showDuringPlay = false;
                EditorUtility.SetDirty(legacy);
            }
        }

        static void EnsureSoundTriggers(List<string> log)
        {
            var registry = EnsureRegistry();
            foreach (var trigger in Object.FindObjectsByType<VWS.SoundEventTrigger>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(trigger, "Fix Sound Event Trigger");
                if (!trigger.GetComponent<AudioSource>())
                    Undo.AddComponent<AudioSource>(trigger.gameObject);

                if (!trigger.registry)
                    trigger.registry = registry;

                if (trigger.fallbackClip && string.IsNullOrWhiteSpace(trigger.eventId))
                    trigger.eventId = BuildSoundId(AssetDatabase.GetAssetPath(trigger.fallbackClip));

                EditorUtility.SetDirty(trigger);
            }
            AddLog(log, "사운드 트리거를 점검했습니다.");
        }

        static void EnsureGoalCounters(List<string> log)
        {
            var needsCounter = Object.FindObjectsByType<VWS.GoalTrigger>(FindObjectsSortMode.None)
                .Any(goal => goal && goal.requiredItems > 0);
            if (!needsCounter)
                return;

            foreach (var player in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (!player.GetComponent<VWS.CollectibleCounter>())
                {
                    Undo.AddComponent<VWS.CollectibleCounter>(player);
                    AddLog(log, player.name + "에 수집 카운터를 추가했습니다.");
                }
            }
        }

        static void EnsureMovingPlatformWaypoints(List<string> log)
        {
            foreach (var platform in Object.FindObjectsByType<VWS.MovingPlatform>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(platform);
                var aProp = so.FindProperty("a");
                var bProp = so.FindProperty("b");
                if (aProp.objectReferenceValue != null && bProp.objectReferenceValue != null)
                    continue;

                var root = new GameObject(platform.name + "_Path");
                Undo.RegisterCreatedObjectUndo(root, "Create Moving Platform Path");
                root.transform.position = platform.transform.position;
                var a = new GameObject("PointA");
                var b = new GameObject("PointB");
                Undo.RegisterCreatedObjectUndo(a, "Create PointA");
                Undo.RegisterCreatedObjectUndo(b, "Create PointB");
                a.transform.SetParent(root.transform);
                b.transform.SetParent(root.transform);
                a.transform.position = platform.transform.position;
                b.transform.position = platform.transform.position + Vector3.right * 4f;
                aProp.objectReferenceValue = a.transform;
                bProp.objectReferenceValue = b.transform;
                so.ApplyModifiedProperties();
                AddLog(log, platform.name + " 이동 발판에 빠진 이동 지점을 만들었습니다.");
            }
        }

        static void EnsurePressurePlateTargets(List<string> log)
        {
            var doors = Object.FindObjectsByType<VWS.DoorController>(FindObjectsSortMode.None);
            foreach (var plate in Object.FindObjectsByType<VWS.PressurePlate>(FindObjectsSortMode.None))
            {
                if (plate.targets != null && plate.targets.Any(target => target != null))
                    continue;

                var door = doors.OrderBy(d => Vector3.Distance(d.transform.position, plate.transform.position)).FirstOrDefault();
                if (!door)
                    continue;

                Undo.RecordObject(plate, "Connect Pressure Plate");
                plate.targets = new[] { door };
                EditorUtility.SetDirty(plate);
                AddLog(log, plate.name + " 압력판을 " + door.name + " 문에 연결했습니다.");
            }
        }

        static void EnsureCheckpointDeathZones(List<string> log)
        {
            var checkpoints = Object.FindObjectsByType<VWS.Checkpoint>(FindObjectsSortMode.None);
            if (checkpoints.Length == 0)
                return;

            var zones = Object.FindObjectsByType<VWS.DeathZone>(FindObjectsSortMode.None);
            if (zones.Length == 0)
            {
                var go = new GameObject("BC_FallRespawnZone");
                Undo.RegisterCreatedObjectUndo(go, "Create Fall Respawn Zone");
                go.transform.position = RecommendedFallRespawnCenter(checkpoints);
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = RecommendedFallRespawnSize(checkpoints);
                go.AddComponent<VWS.DeathZone>();
                AddLog(log, "체크포인트용 낙사 리스폰 안전망을 만들었습니다.");
            }

            foreach (var zone in Object.FindObjectsByType<VWS.DeathZone>(FindObjectsSortMode.None))
                ConfigureDeathZoneCollider(zone, checkpoints);

            foreach (var player in Object.FindObjectsByType<VWS.PlayerController_Platform>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(player, "Configure Fall Respawn");
                player.respawnAtStartOnFall = true;
                player.fallRespawnY = RecommendedFallRespawnCenter(checkpoints).y;
                EditorUtility.SetDirty(player);
            }
        }

        static void ConfigureDeathZoneCollider(VWS.DeathZone zone, VWS.Checkpoint[] checkpoints)
        {
            if (!zone)
                return;

            var collider = zone.GetComponent<Collider>();
            if (!collider)
                collider = Undo.AddComponent<BoxCollider>(zone.gameObject);

            Undo.RecordObject(collider, "Configure DeathZone Collider");
            collider.isTrigger = true;

            var box = collider as BoxCollider;
            if (box)
            {
                box.size = RecommendedFallRespawnSize(checkpoints);
                box.center = Vector3.zero;
            }

            if (zone.gameObject.name.Contains("FallRespawn") || zone.gameObject.name.Contains("DeathZone"))
            {
                Undo.RecordObject(zone.transform, "Place DeathZone");
                zone.transform.position = RecommendedFallRespawnCenter(checkpoints);
                zone.transform.rotation = Quaternion.identity;
            }

            EditorUtility.SetDirty(zone.gameObject);
        }

        static Vector3 RecommendedFallRespawnCenter(VWS.Checkpoint[] checkpoints)
        {
            if (checkpoints == null || checkpoints.Length == 0)
                return new Vector3(0f, -10f, 0f);

            var center = Vector3.zero;
            foreach (var checkpoint in checkpoints)
                center += checkpoint.transform.position;
            center /= checkpoints.Length;
            center.y = -10f;
            return center;
        }

        static Vector3 RecommendedFallRespawnSize(VWS.Checkpoint[] checkpoints)
        {
            var center = RecommendedFallRespawnCenter(checkpoints);
            var radius = 40f;
            if (checkpoints != null)
            {
                foreach (var checkpoint in checkpoints)
                {
                    var delta = checkpoint.transform.position - center;
                    delta.y = 0f;
                    radius = Mathf.Max(radius, delta.magnitude + 30f);
                }
            }

            var diameter = Mathf.Clamp(radius * 2f, 80f, 220f);
            return new Vector3(diameter, 2f, diameter);
        }

        static void EnsureBgmAudioSource(VWS.GenreType genreType, List<string> log)
        {
            var existing = GameObject.Find("VW_Audio_BGM");
            var source = existing ? existing.GetComponent<AudioSource>() : null;
            if (source && source.clip)
                return;

            var registry = EnsureRegistry();
            var clip = FindBgmClip(registry, genreType);
            if (!clip)
                return;

            var go = existing ? existing : new GameObject("VW_Audio_BGM");
            if (!source)
                source = go.AddComponent<AudioSource>();

            Undo.RecordObject(source, "Configure BGM");
            source.clip = clip;
            source.playOnAwake = true;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0.45f;
            EditorUtility.SetDirty(go);
            AddLog(log, "BGM을 연결했습니다: " + clip.name);
        }

        static VWS.SoundEventRegistry EnsureRegistry()
        {
            EnsureFolder("Assets/ScriptableObjects");
            EnsureFolder("Assets/ScriptableObjects/SoundEvents");
            var registry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(RegistryPath);
            if (registry)
                return registry;

            registry = CreateInstance<VWS.SoundEventRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
            AssetDatabase.SaveAssets();
            return registry;
        }

        static AudioClip FindBgmClip(VWS.SoundEventRegistry registry, VWS.GenreType genreType)
        {
            foreach (var id in PreferredBgmIds(genreType))
            {
                if (registry && registry.TryGet(id, out var clip, out _) && clip)
                    return clip;

                var guid = AssetDatabase.FindAssets(id + " t:AudioClip", new[] { "Assets/Audio/BGM" }).FirstOrDefault();
                if (!string.IsNullOrEmpty(guid))
                {
                    var assetClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                    if (assetClip)
                        return assetClip;
                }
            }

            return null;
        }

        static IEnumerable<string> PreferredBgmIds(VWS.GenreType genreType)
        {
            if (genreType == VWS.GenreType.Arena)
            {
                yield return "bgm_arena_battle_loop";
                yield return "bgm_battle_loop";
                yield return "bgm_arena_bgm1";
            }
            else if (genreType == VWS.GenreType.Exploration)
            {
                yield return "bgm_exploration_loop";
            }
            else if (genreType == VWS.GenreType.Puzzle)
            {
                yield return "bgm_puzzle_loop";
            }
            else
            {
                yield return "bgm_platform_space_loop";
            }
        }

        void CreateItemRing(Transform parent, int count, Vector3 center)
        {
            var root = new GameObject("BC_ItemRing");
            Undo.RegisterCreatedObjectUndo(root, "Create Item Ring");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = center;

            count = Mathf.Clamp(count, 1, 12);
            for (int i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count;
                var item = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Undo.RegisterCreatedObjectUndo(item, "Create Item");
                item.name = "BC_Item_" + (i + 1).ToString("00");
                item.transform.SetParent(root.transform, false);
                item.transform.localPosition = new Vector3(Mathf.Cos(angle) * 2f, 0.55f, Mathf.Sin(angle) * 2f);
                item.transform.localScale = Vector3.one * 0.45f;
                item.GetComponent<Collider>().isTrigger = true;
                item.AddComponent<VWS.ItemPickup>();
                SetColor(item, new Color(0.2f, 0.75f, 0.3f));
            }
        }

        void CreateGoal(Transform parent, Vector3 localPosition, int itemRequirement)
        {
            var goal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(goal, "Create Goal");
            goal.name = "BC_Goal";
            goal.transform.SetParent(parent, false);
            goal.transform.localPosition = localPosition;
            goal.transform.localScale = new Vector3(2.2f, 1.5f, 0.5f);
            goal.GetComponent<Collider>().isTrigger = true;
            var trigger = goal.AddComponent<VWS.GoalTrigger>();
            trigger.requiredItems = Mathf.Max(0, itemRequirement);
            SetColor(goal, new Color(1f, 0.85f, 0.15f));
        }

        void CreateHazard(Transform parent, Vector3 localPosition, Vector3 localScale)
        {
            var hazard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(hazard, "Create Hazard");
            hazard.name = "BC_HazardZone";
            hazard.transform.SetParent(parent, false);
            hazard.transform.localPosition = localPosition;
            hazard.transform.localScale = localScale;
            hazard.GetComponent<Collider>().isTrigger = true;
            var zone = hazard.AddComponent<VWS.HazardZone>();
            zone.damagePerSecond = hazardDps;
            SetColor(hazard, new Color(1f, 0.42f, 0.12f));
        }

        void CreateMovingPlatform(Transform parent, Vector3 localPosition)
        {
            var root = new GameObject("BC_MovingPlatform_Path");
            Undo.RegisterCreatedObjectUndo(root, "Create Moving Platform Path");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            var pointA = new GameObject("PointA");
            var pointB = new GameObject("PointB");
            Undo.RegisterCreatedObjectUndo(pointA, "Create PointA");
            Undo.RegisterCreatedObjectUndo(pointB, "Create PointB");
            pointA.transform.SetParent(root.transform, false);
            pointB.transform.SetParent(root.transform, false);
            pointA.transform.localPosition = Vector3.zero;
            pointB.transform.localPosition = Vector3.right * movingPlatformDistance;

            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(platform, "Create Moving Platform");
            platform.name = "BC_MovingPlatform";
            platform.transform.SetParent(root.transform, false);
            platform.transform.localPosition = pointA.transform.localPosition;
            platform.transform.localScale = new Vector3(3f, 0.35f, 3f);
            var moving = platform.AddComponent<VWS.MovingPlatform>();
            moving.a = pointA.transform;
            moving.b = pointB.transform;
            moving.speed = movingPlatformSpeed;
            SetColor(platform, new Color(0.2f, 0.52f, 0.9f));
        }

        void CreateCheckpoint(Transform parent, Vector3 localPosition)
        {
            var checkpoint = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(checkpoint, "Create Checkpoint");
            checkpoint.name = "BC_Checkpoint";
            checkpoint.transform.SetParent(parent, false);
            checkpoint.transform.localPosition = localPosition + Vector3.up;
            checkpoint.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
            checkpoint.GetComponent<Collider>().isTrigger = true;
            checkpoint.AddComponent<VWS.Checkpoint>();
            SetColor(checkpoint, new Color(0.8f, 0.35f, 0.9f));
        }

        void CreateHealthPickup(Transform parent, Vector3 localPosition)
        {
            var pickup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(pickup, "Create Health Pickup");
            pickup.name = "BC_HealthPickup";
            pickup.transform.SetParent(parent, false);
            pickup.transform.localPosition = localPosition + Vector3.up * 0.55f;
            pickup.transform.localScale = new Vector3(0.8f, 0.25f, 0.8f);
            pickup.GetComponent<Collider>().isTrigger = true;
            var hp = pickup.AddComponent<VWS.HealthPickup>();
            hp.healAmount = healAmount;
            SetColor(pickup, new Color(0.9f, 0.1f, 0.15f));
        }

        void CreateDoorAndPlate(Transform parent, Vector3 localPosition)
        {
            var root = new GameObject("BC_DoorAndPlate");
            Undo.RegisterCreatedObjectUndo(root, "Create Door And Plate");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            var doorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(doorGo, "Create Door");
            doorGo.name = "BC_Door";
            doorGo.transform.SetParent(root.transform, false);
            doorGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            doorGo.transform.localScale = new Vector3(2.4f, 3f, 0.35f);
            var door = doorGo.AddComponent<VWS.DoorController>();
            SetColor(doorGo, new Color(0.45f, 0.28f, 0.16f));

            var plateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(plateGo, "Create Pressure Plate");
            plateGo.name = "BC_PressurePlate";
            plateGo.transform.SetParent(root.transform, false);
            plateGo.transform.localPosition = new Vector3(0f, 0.08f, -3f);
            plateGo.transform.localScale = new Vector3(2f, 0.16f, 2f);
            plateGo.GetComponent<Collider>().isTrigger = true;
            var plate = plateGo.AddComponent<VWS.PressurePlate>();
            plate.targets = new[] { door };
            SetColor(plateGo, new Color(0.9f, 0.78f, 0.18f));
        }

        void CreateMovableBox(Transform parent, Vector3 localPosition)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(box, "Create Movable Box");
            box.name = "BC_MovableBox";
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition + Vector3.up * 0.65f;
            box.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
            var rb = box.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            box.AddComponent<VWS.MovableBox>();
            SetColor(box, new Color(0.55f, 0.42f, 0.24f));
        }

        void CreateArenaCover(Transform parent)
        {
            var positions = new[]
            {
                new Vector3(-3.5f, 0.75f, 2.5f),
                new Vector3(3.5f, 0.75f, 2.0f),
                new Vector3(0f, 0.75f, -2.8f)
            };
            var scales = new[]
            {
                new Vector3(1.2f, 1.5f, 2.8f),
                new Vector3(2.5f, 1.5f, 1.1f),
                new Vector3(3.2f, 1.5f, 1.0f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(cover, "Create Arena Cover");
                cover.name = "BC_ArenaCover_" + (i + 1).ToString("00");
                cover.transform.SetParent(parent, false);
                cover.transform.localPosition = positions[i];
                cover.transform.localScale = scales[i];
                cover.isStatic = true;
                SetColor(cover, new Color(0.22f, 0.25f, 0.3f));
            }
        }

        static void SetColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (!renderer)
                return;

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");
            if (!shader)
                return;

            var material = new Material(shader) { name = go.name + "_mat" };
            if (material.HasColor("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else if (material.HasColor("_Color"))
                material.SetColor("_Color", color);
            renderer.sharedMaterial = material;
        }

        static bool RequiresSelection(BlockAction blockAction)
        {
            return blockAction != BlockAction.AddCountdownTimer && blockAction != BlockAction.CreateStarterBlocks;
        }

        static T DrawKoreanEnumPopup<T>(string label, T value, Func<T, string> labelFor) where T : struct
        {
            var values = (T[])Enum.GetValues(typeof(T));
            var index = Array.IndexOf(values, value);
            if (index < 0)
                index = 0;

            var labels = values.Select(labelFor).ToArray();
            var next = EditorGUILayout.Popup(label, index, labels);
            return values[Mathf.Clamp(next, 0, values.Length - 1)];
        }

        static string BlockGenreLabel(BlockGenre value)
        {
            switch (value)
            {
                case BlockGenre.Arena:
                    return "아레나 전투";
                case BlockGenre.Exploration:
                    return "탐험";
                case BlockGenre.Puzzle:
                    return "퍼즐";
                case BlockGenre.Common:
                    return "공통";
                default:
                    return "플랫폼";
            }
        }

        static string BlockActionLabel(BlockAction value)
        {
            switch (value)
            {
                case BlockAction.AutoConnectSelected:
                    return "선택 모델 자동 판단";
                case BlockAction.ConnectPlayer:
                    return "3인칭 플레이어 연결";
                case BlockAction.ConnectPlatformPlayer:
                    return "플랫폼 플레이어 연결";
                case BlockAction.ConnectEnemy:
                    return "적 웨이브 연결";
                case BlockAction.ConnectItemPickup:
                    return "수집 아이템 연결";
                case BlockAction.ConnectHealthPickup:
                    return "회복 아이템 연결";
                case BlockAction.ConnectGoal:
                    return "목표 지점 연결";
                case BlockAction.ConnectDoor:
                    return "문 연결";
                case BlockAction.ConnectPressurePlate:
                    return "압력판 연결";
                case BlockAction.ConnectHazardZone:
                    return "위험 구역 연결";
                case BlockAction.ConnectMovingPlatform:
                    return "이동 발판 연결";
                case BlockAction.ConnectMovableBox:
                    return "밀 수 있는 상자 연결";
                case BlockAction.ConnectCheckpoint:
                    return "체크포인트 연결";
                case BlockAction.ConnectArenaCover:
                    return "환경 소품/엄폐물 연결";
                case BlockAction.AddCountdownTimer:
                    return "제한시간 타이머 추가";
                default:
                    return "기본 블록 만들기";
            }
        }

        static VARCOAutoConnectorWindow.Role ResolveRole(BlockAction blockAction)
        {
            switch (blockAction)
            {
                case BlockAction.ConnectPlatformPlayer: return VARCOAutoConnectorWindow.Role.PlatformPlayer;
                case BlockAction.ConnectEnemy: return VARCOAutoConnectorWindow.Role.Enemy;
                case BlockAction.ConnectItemPickup: return VARCOAutoConnectorWindow.Role.ItemPickup;
                case BlockAction.ConnectHealthPickup: return VARCOAutoConnectorWindow.Role.HealthPickup;
                case BlockAction.ConnectGoal: return VARCOAutoConnectorWindow.Role.Goal;
                case BlockAction.ConnectDoor: return VARCOAutoConnectorWindow.Role.Door;
                case BlockAction.ConnectPressurePlate: return VARCOAutoConnectorWindow.Role.PressurePlate;
                case BlockAction.ConnectHazardZone: return VARCOAutoConnectorWindow.Role.HazardZone;
                case BlockAction.ConnectMovingPlatform: return VARCOAutoConnectorWindow.Role.MovingPlatform;
                case BlockAction.ConnectMovableBox: return VARCOAutoConnectorWindow.Role.MovableBox;
                case BlockAction.ConnectCheckpoint: return VARCOAutoConnectorWindow.Role.Checkpoint;
                case BlockAction.ConnectArenaCover: return VARCOAutoConnectorWindow.Role.ArenaCover;
                default: return VARCOAutoConnectorWindow.Role.Player;
            }
        }

        static BlockAction GetDefaultAction(BlockGenre blockGenre)
        {
            return BlockAction.AutoConnectSelected;
        }

        static VARCOAutoConnectorWindow.CameraViewPreset GetDefaultCameraView(BlockGenre blockGenre)
        {
            switch (blockGenre)
            {
                case BlockGenre.Arena: return VARCOAutoConnectorWindow.CameraViewPreset.QuarterView;
                case BlockGenre.Exploration: return VARCOAutoConnectorWindow.CameraViewPreset.QuarterView;
                case BlockGenre.Platform: return VARCOAutoConnectorWindow.CameraViewPreset.SideView;
                default: return VARCOAutoConnectorWindow.CameraViewPreset.ThirdPerson;
            }
        }

        static VWS.GenreType ToGenreType(BlockGenre blockGenre)
        {
            switch (blockGenre)
            {
                case BlockGenre.Arena: return VWS.GenreType.Arena;
                case BlockGenre.Exploration: return VWS.GenreType.Exploration;
                case BlockGenre.Puzzle: return VWS.GenreType.Puzzle;
                default: return VWS.GenreType.Platform;
            }
        }

        static BlockGenre ToBlockGenre(VWS.GenreType genreType)
        {
            switch (genreType)
            {
                case VWS.GenreType.Arena: return BlockGenre.Arena;
                case VWS.GenreType.Exploration: return BlockGenre.Exploration;
                case VWS.GenreType.Puzzle: return BlockGenre.Puzzle;
                default: return BlockGenre.Platform;
            }
        }

        static VWS.GenreType GuessGenreFromScene(string scenePath)
        {
            var lower = (scenePath ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("arena")) return VWS.GenreType.Arena;
            if (lower.Contains("exploration")) return VWS.GenreType.Exploration;
            if (lower.Contains("puzzle")) return VWS.GenreType.Puzzle;
            return VWS.GenreType.Platform;
        }

        static List<GameObject> GetSelectedRootSceneObjects()
        {
            var selected = Selection.gameObjects
                .Where(IsSceneObject)
                .Distinct()
                .ToList();

            return selected
                .Where(go => !HasSelectedAncestor(go, selected))
                .OrderBy(go => AutoConnectSortKey(go))
                .ThenBy(go => go.name)
                .ToList();
        }

        static List<GameObject> GetSelectedProjectGameObjectAssets()
        {
            var results = new List<GameObject>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var selected in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(selected);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { path }))
                        AddProjectGameObjectAsset(AssetDatabase.GUIDToAssetPath(guid), results, seenPaths);
                    continue;
                }

                AddProjectGameObjectAsset(path, results, seenPaths, selected as GameObject);
            }

            return results
                .Where(go => go && !IsSceneObject(go))
                .Distinct()
                .OrderBy(AutoConnectSortKey)
                .ThenBy(go => go.name)
                .ToList();
        }

        static void AddProjectGameObjectAsset(string path, List<GameObject> results, HashSet<string> seenPaths, GameObject selectedAsset = null)
        {
            if (string.IsNullOrWhiteSpace(path) || !seenPaths.Add(path))
                return;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset && selectedAsset)
                asset = selectedAsset;
            if (!asset || IsSceneObject(asset))
                return;

            results.Add(asset);
        }

        static bool IsSceneObject(GameObject go)
        {
            return go && go.scene.IsValid() && go.scene.isLoaded;
        }

        static bool HasSelectedAncestor(GameObject go, List<GameObject> selected)
        {
            var current = go ? go.transform.parent : null;
            while (current)
            {
                if (selected.Contains(current.gameObject))
                    return true;
                current = current.parent;
            }

            return false;
        }

        static int AutoConnectSortKey(GameObject go)
        {
            var evidence = BuildSelectedModelEvidence(go);
            if (ContainsAny(evidence, "player", "hero", "character", "astronaut", "explorer", "플레이어", "캐릭터"))
                return 0;
            if (ContainsAny(evidence, "enemy", "zombie", "boss", "orc", "monster", "drone", "적", "좀비", "보스"))
                return 1;
            if (ContainsAny(evidence, "checkpoint", "respawn", "체크포인트", "리스폰"))
                return 2;
            if (ContainsAny(evidence, "goal", "finish", "exit", "portal", "dock", "docking", "목표", "도착", "출구"))
                return 3;
            return 4;
        }

        static GameObject InstantiateProjectAsset(GameObject asset)
        {
            if (!asset)
                return null;

            var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (instance)
                return instance;

            return Object.Instantiate(asset);
        }

        static string MakePlacedAssetName(string assetName)
        {
            return "BC_" + (string.IsNullOrWhiteSpace(assetName) ? "VARCO_Asset" : assetName);
        }

        static int NextActionIndex(Dictionary<BlockAction, int> counts, BlockAction action)
        {
            counts.TryGetValue(action, out var index);
            counts[action] = index + 1;
            return index;
        }

        static void PlaceAutoInstantiatedObject(GameObject go, BlockAction action, int index)
        {
            if (!go)
                return;

            Undo.RecordObject(go.transform, "자동 배치 위치 설정");
            go.transform.position = RecommendedAutoPlacement(action, index);
            go.transform.rotation = RecommendedAutoRotation(action);
            NormalizePlacedAssetScale(go, action);
        }

        static Vector3 RecommendedAutoPlacement(BlockAction action, int index)
        {
            switch (action)
            {
                case BlockAction.ConnectPlayer:
                case BlockAction.ConnectPlatformPlayer:
                    return new Vector3(index * 2f, 0f, 0f);
                case BlockAction.ConnectEnemy:
                    return new Vector3(-6f + index * 2.5f, 0f, 6f);
                case BlockAction.ConnectItemPickup:
                    return RingPosition(index, 3f, new Vector3(0f, 0.5f, 4f));
                case BlockAction.ConnectHealthPickup:
                    return new Vector3(-4f + index * 2f, 0.5f, 2f);
                case BlockAction.ConnectGoal:
                    return new Vector3(0f, 0.75f, 11f + index * 2f);
                case BlockAction.ConnectDoor:
                    return new Vector3(0f, 1.5f, 8f + index * 3f);
                case BlockAction.ConnectPressurePlate:
                    return new Vector3(0f, 0.08f, 5f + index * 2f);
                case BlockAction.ConnectHazardZone:
                    return new Vector3(4f + index * 2.5f, 0.05f, 3f);
                case BlockAction.ConnectMovingPlatform:
                    return new Vector3(index * 5f, 1f, -4f);
                case BlockAction.ConnectMovableBox:
                    return new Vector3(-3f + index * 2f, 0.65f, 2f);
                case BlockAction.ConnectCheckpoint:
                    return new Vector3(4f + index * 3f, 1f, 1f);
                case BlockAction.ConnectArenaCover:
                    return new Vector3(-5f + index * 3f, 0.75f, -2f);
                default:
                    return new Vector3(index * 2f, 0f, 0f);
            }
        }

        static Vector3 RingPosition(int index, float radius, Vector3 center)
        {
            var angle = index * Mathf.PI * 2f / 8f;
            return center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        static Quaternion RecommendedAutoRotation(BlockAction action)
        {
            switch (action)
            {
                case BlockAction.ConnectDoor:
                case BlockAction.ConnectGoal:
                    return Quaternion.identity;
                default:
                    return Quaternion.identity;
            }
        }

        static void NormalizePlacedAssetScale(GameObject go, BlockAction action)
        {
            if (!TryGetWorldRendererBounds(go, out var bounds))
                return;

            var maxAxis = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxAxis <= 0.001f)
                return;

            var targetMax = TargetAutoPlacedMaxSize(action);
            if (maxAxis > targetMax * 2.2f || maxAxis < targetMax * 0.25f)
            {
                Undo.RecordObject(go.transform, "자동 배치 크기 보정");
                var scale = Mathf.Clamp(targetMax / maxAxis, 0.05f, 20f);
                go.transform.localScale *= scale;
            }
        }

        static float TargetAutoPlacedMaxSize(BlockAction action)
        {
            switch (action)
            {
                case BlockAction.ConnectPlayer:
                case BlockAction.ConnectPlatformPlayer:
                case BlockAction.ConnectEnemy:
                    return 2.2f;
                case BlockAction.ConnectDoor:
                case BlockAction.ConnectGoal:
                case BlockAction.ConnectArenaCover:
                    return 3f;
                case BlockAction.ConnectMovingPlatform:
                    return 4f;
                case BlockAction.ConnectHazardZone:
                    return 5f;
                default:
                    return 1.5f;
            }
        }

        static bool TryGetWorldRendererBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var renderers = go.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer && renderer.enabled)
                .ToList();
            if (renderers.Count == 0)
                return false;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Count; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        static string GetActionHint(BlockAction blockAction)
        {
            switch (blockAction)
            {
                case BlockAction.AutoConnectSelected: return "선택한 모델의 이름, 프리팹 경로, 자식 오브젝트, 머티리얼, 애니메이터 단서를 보고 가장 알맞은 기능 블록을 자동으로 붙입니다.";
                case BlockAction.ConnectPlayer: return "선택한 모델을 마우스 시점 카메라가 따라가는 3인칭 플레이어로 만듭니다.";
                case BlockAction.ConnectPlatformPlayer: return "선택한 모델을 점프, 낙사 리스폰, 플랫폼 이동에 맞는 플레이어로 만듭니다.";
                case BlockAction.ConnectEnemy: return "선택한 모델에 적 AI, 체력, 내비게이션 이동, 충돌 영역을 붙이고 웨이브에 연결합니다.";
                case BlockAction.ConnectItemPickup: return "선택한 모델을 닿으면 수집되는 아이템으로 만듭니다.";
                case BlockAction.ConnectHealthPickup: return "선택한 모델을 플레이어 HP를 회복하는 아이템으로 만듭니다.";
                case BlockAction.ConnectGoal: return "선택한 모델을 게임 클리어 지점으로 만들고, 필요 아이템 수도 설정합니다.";
                case BlockAction.ConnectDoor: return "선택한 모델을 열리고 닫히는 문으로 만듭니다.";
                case BlockAction.ConnectPressurePlate: return "선택한 모델을 압력판으로 만들고 가까운 문과 자동 연결합니다.";
                case BlockAction.ConnectHazardZone: return "선택한 모델을 플레이어에게 지속 피해를 주는 위험 구역으로 만듭니다.";
                case BlockAction.ConnectMovingPlatform: return "선택한 모델을 두 지점 사이를 움직이는 이동 발판으로 만듭니다.";
                case BlockAction.ConnectMovableBox: return "선택한 모델을 밀 수 있는 퍼즐 상자로 만듭니다.";
                case BlockAction.ConnectCheckpoint: return "선택한 모델을 통과하면 리스폰 위치가 저장되는 체크포인트로 만들고, 안전 보정 때 낙사 리스폰 구역도 함께 준비합니다.";
                case BlockAction.ConnectArenaCover: return "선택한 모델을 전투/탐험/퍼즐 공간에서 쓸 환경 소품이나 엄폐물로 만듭니다.";
                case BlockAction.AddCountdownTimer: return "시간이 0이 되면 실패 처리되는 제한시간 타이머를 추가하거나 갱신합니다.";
                default: return "선택한 장르에 맞는 기본 샘플 블록을 씬에 만듭니다.";
            }
        }

        static BlockAction GuessBlockActionForSelectedModel(GameObject go, BlockGenre blockGenre, out string reason)
        {
            reason = "단서가 부족해 장르와 현재 씬 상태를 기준으로 판단했습니다.";
            if (!go)
                return blockGenre == BlockGenre.Platform ? BlockAction.ConnectPlatformPlayer : BlockAction.ConnectPlayer;

            var evidence = BuildSelectedModelEvidence(go);
            if (ContainsAny(evidence, "docking", "dock", "finish_gate", "clear_gate"))
                return WithReason(BlockAction.ConnectGoal, "도킹/도착 게이트 단어를 찾아 목표 지점으로 판단했습니다.", out reason);
            if (ContainsAny(evidence, "checkpoint", "check_point", "savepoint", "save_point", "respawn", "리스폰", "체크포인트"))
                return WithReason(BlockAction.ConnectCheckpoint, "체크포인트/리스폰 단어를 찾았습니다.", out reason);
            if (ContainsAny(evidence, "pressure", "plate", "switch", "button", "lever", "발판", "스위치", "압력판"))
                return WithReason(BlockAction.ConnectPressurePlate, "압력판/스위치 단어를 찾았습니다.", out reason);
            if (ContainsAny(evidence, "door", "gate", "lockeddoor", "lock_door", "문", "게이트"))
                return WithReason(BlockAction.ConnectDoor, "문/게이트 단어를 찾았습니다.", out reason);
            if (ContainsAny(evidence, "hazard", "trap", "lava", "spike", "damage", "fire", "poison", "위험", "함정"))
                return WithReason(BlockAction.ConnectHazardZone, "위험 구역/피해 단어를 찾았습니다.", out reason);
            if (ContainsAny(evidence, "health", "heal", "hp", "potion", "medkit", "회복", "포션"))
                return WithReason(BlockAction.ConnectHealthPickup, "회복/체력 아이템 단어를 찾았습니다.", out reason);
            if (ContainsAny(evidence, "coin", "gem", "key", "item", "collect", "pickup", "treasure", "crystal", "수집", "아이템", "열쇠", "보물"))
                return WithReason(BlockAction.ConnectItemPickup, "수집 아이템 단어를 찾았습니다.", out reason);
            if (ContainsAny(evidence, "goal", "finish", "exit", "portal", "dock", "docking", "clear", "목표", "도착", "출구"))
                return WithReason(BlockAction.ConnectGoal, "목표/출구 단어를 찾았습니다.", out reason);
            if (ContainsAny(evidence, "box", "crate", "movable", "push", "pushable", "상자", "밀기"))
                return WithReason(BlockAction.ConnectMovableBox, "밀 수 있는 상자 단어를 찾았습니다.", out reason);
            if (ContainsAny(evidence, "movingplatform", "move_platform", "moving_platform", "lift", "elevator", "shuttle", "bridge", "platform_moving", "이동발판", "엘리베이터"))
                return WithReason(BlockAction.ConnectMovingPlatform, "이동 발판/리프트 단어를 찾았습니다.", out reason);
            if (ContainsAny(evidence,
                    "cover", "barricade", "obstacle", "wall", "pillar", "rock", "tree", "plant", "bush", "grass", "ruin", "debris", "prop", "scenery", "environment",
                    "엄폐", "장애물", "벽", "바위", "나무", "식물", "수풀", "풀", "폐허", "잔해", "소품", "배경", "환경"))
                return WithReason(BlockAction.ConnectArenaCover, "환경 소품/엄폐물 단어를 찾았습니다.", out reason);

            if (ContainsAny(evidence, "enemy", "zombie", "boss", "orc", "monster", "drone", "ai_", "_ai", "적", "좀비", "보스", "몬스터"))
                return WithReason(BlockAction.ConnectEnemy, "적/좀비/보스 단어를 찾았습니다.", out reason);
            if (ContainsAny(evidence, "player", "hero", "character", "knight", "warrior", "astronaut", "explorer", "avatar", "플레이어", "캐릭터", "영웅"))
            {
                var playerAction = blockGenre == BlockGenre.Platform
                    ? BlockAction.ConnectPlatformPlayer
                    : BlockAction.ConnectPlayer;
                return WithReason(playerAction, "플레이어/캐릭터 단어를 찾았습니다.", out reason);
            }

            var hasCharacterShape = go.GetComponentInChildren<Animator>(true) || go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (hasCharacterShape)
            {
                if (SceneHasPlayer())
                    return WithReason(BlockAction.ConnectEnemy, "애니메이터 또는 스킨드 메시가 있고 이미 플레이어가 있어 적으로 판단했습니다.", out reason);

                var playerAction = blockGenre == BlockGenre.Platform
                    ? BlockAction.ConnectPlatformPlayer
                    : BlockAction.ConnectPlayer;
                return WithReason(playerAction, "애니메이터 또는 스킨드 메시가 있어 플레이어 캐릭터로 판단했습니다.", out reason);
            }

            if (blockGenre == BlockGenre.Puzzle)
                return WithReason(BlockAction.ConnectMovableBox, "퍼즐 장르의 알 수 없는 오브젝트라 밀 수 있는 상자로 판단했습니다.", out reason);
            if (blockGenre == BlockGenre.Arena || blockGenre == BlockGenre.Exploration)
                return WithReason(BlockAction.ConnectArenaCover, "전투/탐험 장르의 알 수 없는 오브젝트라 환경 소품/엄폐물로 판단했습니다.", out reason);

            return WithReason(BlockAction.ConnectItemPickup, "알 수 없는 작은 오브젝트는 수집 아이템으로 판단했습니다.", out reason);
        }

        static BlockAction WithReason(BlockAction action, string value, out string reason)
        {
            reason = value;
            return action;
        }

        static bool ContainsAny(string text, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            foreach (var keyword in keywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword) && text.Contains(keyword.ToLowerInvariant()))
                    return true;
            }

            return false;
        }

        static string BuildSelectedModelEvidence(GameObject go)
        {
            var parts = new List<string> { go.name };
            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (!string.IsNullOrWhiteSpace(prefabPath))
                parts.Add(prefabPath);

            foreach (var transform in go.GetComponentsInChildren<Transform>(true).Take(80))
                parts.Add(transform.name);

            foreach (var animator in go.GetComponentsInChildren<Animator>(true).Take(12))
            {
                parts.Add(animator.name);
                if (animator.runtimeAnimatorController)
                    parts.Add(animator.runtimeAnimatorController.name);
            }

            foreach (var mesh in go.GetComponentsInChildren<MeshFilter>(true).Take(24))
            {
                if (mesh.sharedMesh)
                    parts.Add(mesh.sharedMesh.name);
            }

            foreach (var skinned in go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Take(24))
            {
                parts.Add(skinned.name);
                if (skinned.sharedMesh)
                    parts.Add(skinned.sharedMesh.name);
            }

            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true).Take(30))
            {
                parts.Add(renderer.name);
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material)
                        parts.Add(material.name);
                }
            }

            return string.Join(" ", parts).ToLowerInvariant();
        }

        static bool SceneHasPlayer()
        {
            try
            {
                return GameObject.FindGameObjectWithTag("Player") != null;
            }
            catch
            {
                return Object.FindFirstObjectByType<VWS.PlayerController_ThirdPerson>() != null
                    || Object.FindFirstObjectByType<VWS.PlayerController_Platform>() != null;
            }
        }

        static string BuildSoundId(string assetPath)
        {
            var file = Path.GetFileNameWithoutExtension(assetPath);
            var id = Regex.Replace(file.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
            id = Regex.Replace(id, @"_+", "_");
            if (id.StartsWith("sfx_") || id.StartsWith("bgm_") || id.StartsWith("amb_"))
                return id;
            return "sfx_" + id;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

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
            if (assets == null || assets.Length == 0)
                return;

            var so = new SerializedObject(assets[0]);
            var tags = so.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                    return;
            }

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

        static void DrawHeader(string text)
        {
            GUILayout.Space(12);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        static void AddLog(List<string> log, string message)
        {
            if (log != null)
                log.Add(message);
        }
    }
}
#endif
