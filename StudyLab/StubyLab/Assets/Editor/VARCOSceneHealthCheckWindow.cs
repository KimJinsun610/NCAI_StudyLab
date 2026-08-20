#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using VWS = VARCO_Workshop;
using Object = UnityEngine.Object;

namespace VARCO_Workshop.Editor
{
    public class VARCOSceneHealthCheckWindow : EditorWindow
    {
        const string DefaultSoundRegistryPath = "Assets/ScriptableObjects/SoundEvents/WorkshopSoundEventRegistry.asset";

        readonly List<Finding> findings = new List<Finding>();
        Vector2 scroll;
        string lastActionSummary = "";
        VARCOEditorRepaintGate repaintGate;

        enum FindingLevel
        {
            Pass,
            Info,
            Warning,
            Error
        }

        class Finding
        {
            public FindingLevel level;
            public string title;
            public string detail;
            public Object context;
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/검증/현재 씬 건강 검사", priority = -30)]
        public static void Open()
        {
            var window = GetWindow<VARCOSceneHealthCheckWindow>("씬 건강 검사");
            window.minSize = new Vector2(520, 520);
            window.Scan();
        }

        void OnEnable()
        {
            repaintGate = new VARCOEditorRepaintGate(this);
            Scan();
        }

        void OnDisable()
        {
            if (repaintGate != null)
                repaintGate.Dispose();
            repaintGate = null;
        }

        void OnGUI()
        {
            DrawToolbar();

            var errors = findings.Count(f => f.level == FindingLevel.Error);
            var warnings = findings.Count(f => f.level == FindingLevel.Warning);
            var passes = findings.Count(f => f.level == FindingLevel.Pass);

            EditorGUILayout.HelpBox(
                $"현재 씬 점검 결과: 오류 {errors} / 확인 {warnings} / 통과 {passes}",
                errors > 0 ? MessageType.Error : warnings > 0 ? MessageType.Warning : MessageType.Info);

            if (!string.IsNullOrWhiteSpace(lastActionSummary))
                EditorGUILayout.HelpBox(lastActionSummary, MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var finding in findings)
                DrawFinding(finding);
            EditorGUILayout.EndScrollView();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(72)))
                    Scan();
                if (GUILayout.Button("자동 보정", EditorStyles.toolbarButton, GUILayout.Width(96)))
                    RunAutoFix();
                if (GUILayout.Button("게임 만들기", EditorStyles.toolbarButton, GUILayout.Width(96)))
                {
                    VARCOGameMakerWindow.RunBestGameFromMenu();
                    Scan();
                }
                if (GUILayout.Button("게임 메이커", EditorStyles.toolbarButton, GUILayout.Width(92)))
                {
                    VARCOGameMakerWindow.Open();
                }
                if (GUILayout.Button("초기 씬 설정", EditorStyles.toolbarButton, GUILayout.Width(104)))
                {
                    VARCOAutoConnectorWindow.CreateInitialSceneSetup();
                    Scan();
                }
                if (GUILayout.Button("사운드 연결", EditorStyles.toolbarButton, GUILayout.Width(92)))
                    VARCOSoundConnectorWindow.Open();
                if (GUILayout.Button("리포트 복사", EditorStyles.toolbarButton, GUILayout.Width(92)))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildReport();
                    lastActionSummary = "현재 검사 리포트를 클립보드에 복사했습니다.";
                }
                GUILayout.FlexibleSpace();
            }
        }

        void RunAutoFix()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("씬 건강 검사", "자동 보정 전에 Unity Play 모드를 먼저 꺼주세요.", "확인");
                return;
            }

            var log = new List<string>();
            Undo.SetCurrentGroupName("VARCO 씬 자동 보정");
            int undoGroup = Undo.GetCurrentGroup();

            VARCOBlockCodingBuilderWindow.RunSafetyPassForCurrentScene(log, saveScene: false);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);

            lastActionSummary = log.Count == 0
                ? "자동 보정이 끝났습니다. 새로 만들거나 수정할 항목은 없었습니다."
                : "자동 보정 완료:\n- " + string.Join("\n- ", log);

            Debug.Log("[VARCO 씬 자동 보정]\n" + (log.Count == 0 ? "수정할 항목 없음" : string.Join("\n", log)));
            Scan();
        }

        void DrawFinding(Finding finding)
        {
            var messageType = finding.level == FindingLevel.Error
                ? MessageType.Error
                : finding.level == FindingLevel.Warning
                    ? MessageType.Warning
                    : finding.level == FindingLevel.Info
                        ? MessageType.None
                        : MessageType.Info;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox($"{finding.title}\n{finding.detail}", messageType);
                using (new EditorGUI.DisabledScope(finding.context == null))
                {
                    if (GUILayout.Button("선택", GUILayout.Width(56), GUILayout.Height(38)))
                    {
                        Selection.activeObject = finding.context;
                        EditorGUIUtility.PingObject(finding.context);
                    }
                }
            }
        }

        void Scan()
        {
            findings.Clear();

            CheckGameManager();
            CheckPlayer();
            CheckCamera();
            CheckNavMesh();
            CheckSound();
            CheckBuildSettings();
            CheckMissingScripts();

            if (findings.Count == 0)
                Add(FindingLevel.Info, "검사 항목 없음", "현재 씬에서 점검할 수 있는 항목을 찾지 못했습니다.");

            RequestRepaint();
        }

        void RequestRepaint(bool immediate = false)
        {
            if (repaintGate == null)
                repaintGate = new VARCOEditorRepaintGate(this);
            repaintGate.Request(immediate);
        }

        void CheckGameManager()
        {
            var managers = Object.FindObjectsByType<VWS.GameManager>(FindObjectsSortMode.None);
            if (managers.Length == 0)
            {
                Add(FindingLevel.Error, "게임 매니저 없음", "`VARCO/프리셋 만들기` 또는 `VARCO/레거시/간략 자동 제작/간략 버전 열기`로 기본 시스템을 생성하세요.");
                return;
            }

            if (managers.Length > 1)
            {
                Add(FindingLevel.Error, "게임 매니저 중복", $"현재 씬에 게임 매니저가 {managers.Length}개 있습니다. 하나만 남기세요.", managers[0]);
                return;
            }

            var gm = managers[0];
            Add(FindingLevel.Pass, "게임 매니저 확인", gm.name, gm);

            if (!gm.loadResultScenes)
                return;

            CheckResultScene(gm.clearSceneName, "클리어", gm);
            CheckResultScene(gm.gameOverSceneName, "게임 오버", gm);
        }

        void CheckResultScene(string sceneName, string label, Object context)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            if (!BuildSettingsCanLoad(sceneName))
                Add(FindingLevel.Warning, $"{label} 결과 씬 미등록", $"게임 매니저가 `{sceneName}` 씬을 로드하도록 설정되어 있지만 빌드 설정에 없습니다.", context);
        }

        void CheckPlayer()
        {
            GameObject[] players;
            try
            {
                players = GameObject.FindGameObjectsWithTag("Player");
            }
            catch
            {
                Add(FindingLevel.Error, "플레이어 태그 없음", "Unity 태그 설정에서 `Player` 태그를 만들고 플레이어 오브젝트에 지정하세요.");
                return;
            }

            if (players.Length == 0)
            {
                Add(FindingLevel.Error, "플레이어 오브젝트 없음", "`VARCO/프리셋 만들기` 또는 `VARCO/레거시/간략 자동 제작/간략 버전 열기`를 실행하거나 플레이어 오브젝트에 `Player` 태그를 지정하세요.");
                return;
            }

            if (players.Length > 1)
                Add(FindingLevel.Warning, "플레이어 태그 중복", $"`Player` 태그 오브젝트가 {players.Length}개 있습니다. 의도한 구성이 맞는지 확인하세요.", players[0]);

            foreach (var player in players)
            {
                var hasThirdPerson = player.GetComponent<VWS.PlayerController_ThirdPerson>() != null;
                var hasPlatform = player.GetComponent<VWS.PlayerController_Platform>() != null;
                var hasHealth = player.GetComponent<VWS.PlayerHealth>() != null;
                var hasCollider = player.GetComponentInChildren<Collider>() != null;

                Add(FindingLevel.Pass, "플레이어 확인", player.name, player);

                if (!hasThirdPerson && !hasPlatform)
                    Add(FindingLevel.Error, "플레이어 컨트롤러 없음", "`VARCO/레거시/세부 자동 제작/현재 씬 자동 보정`을 실행해 장르에 맞는 컨트롤러를 연결하세요.", player);
                if (!hasHealth)
                    Add(FindingLevel.Warning, "플레이어 체력 없음", "`자동 보정`으로 피격/회복/게임 오버 흐름을 연결하세요.", player);
                if (!hasCollider)
                    Add(FindingLevel.Warning, "플레이어 충돌 영역 없음", "`자동 보정`으로 아이템, 목표, 위험 구역 트리거가 동작하도록 보정하세요.", player);
            }

            var itemGoals = Object.FindObjectsByType<VWS.GoalTrigger>(FindObjectsSortMode.None)
                .Any(g => g.requiredItems > 0);
            if (itemGoals && players.All(p => p.GetComponent<VWS.CollectibleCounter>() == null))
                Add(FindingLevel.Warning, "수집 카운터 없음", "`자동 보정`으로 수집 카운터를 플레이어에 연결하세요.", players[0]);
        }

        void CheckCamera()
        {
            var mainCamera = Camera.main;
            if (!mainCamera)
            {
                Add(FindingLevel.Error, "메인 카메라 없음", "카메라에 `MainCamera` 태그를 지정하거나 `자동 보정`을 실행하세요.");
                return;
            }

            Add(FindingLevel.Pass, "메인 카메라 확인", mainCamera.name, mainCamera);

            var followCamera = mainCamera.GetComponent<VWS.ThirdPersonCamera>();
            if (!followCamera)
            {
                Add(FindingLevel.Warning, "3인칭 카메라 추적 없음", "워크숍 기본 카메라 추적을 쓰려면 메인 카메라에 3인칭 카메라 컴포넌트를 추가하세요.", mainCamera);
                return;
            }

            if (!followCamera.target && GameObject.FindGameObjectWithTag("Player") != null)
                Add(FindingLevel.Warning, "카메라 대상 미지정", "플레이 시작 시 `Player` 태그를 자동 검색하지만, 수업 전에는 명시 연결을 권장합니다.", followCamera);
            else
                Add(FindingLevel.Pass, "3인칭 카메라 확인", followCamera.name, followCamera);
        }

        void CheckNavMesh()
        {
            var agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            var enemyAi = Object.FindObjectsByType<VWS.EnemyAI_NavMesh>(FindObjectsSortMode.None);
            var waves = Object.FindObjectsByType<VWS.WaveManager>(FindObjectsSortMode.None);
            if (agents.Length == 0 && enemyAi.Length == 0 && waves.Length == 0)
                return;

            var surfaceCount = CountNavMeshSurfaces();
            var triangulation = NavMesh.CalculateTriangulation();
            var hasBakedData = triangulation.vertices != null && triangulation.vertices.Length > 0;

            if (surfaceCount == 0)
                Add(FindingLevel.Warning, "내비메시 표면 없음", "적 AI 또는 적 웨이브 매니저가 있습니다. AI Navigation의 NavMeshSurface를 확인하세요.");
            else
                Add(FindingLevel.Pass, "내비메시 표면 확인", $"{surfaceCount}개 발견");

            if (!hasBakedData)
                Add(FindingLevel.Warning, "내비메시 베이크 확인 필요", "현재 에디터 내비메시 데이터를 확인하지 못했습니다. AI Navigation 창에서 Bake 상태를 확인하세요.");

            foreach (var wave in waves)
                CheckWaveManager(wave);
        }

        void CheckWaveManager(VWS.WaveManager wave)
        {
            if (wave.waves == null || wave.waves.Length == 0)
            {
                Add(FindingLevel.Warning, "적 웨이브 설정 없음", "전투/탐험 적 웨이브를 쓰려면 waves 배열을 설정하세요.", wave);
                return;
            }

            var missing = wave.waves.Count(w => w == null || w.enemyPrefab == null);
            if (missing > 0)
                Add(FindingLevel.Warning, "적 프리팹 누락", $"{missing}개 웨이브에 적 프리팹이 없습니다.", wave);
            else
                Add(FindingLevel.Pass, "적 웨이브 매니저 확인", $"{wave.waves.Length}개 웨이브 설정됨", wave);
        }

        void CheckSound()
        {
            var defaultRegistry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(DefaultSoundRegistryPath);
            if (!defaultRegistry)
                Add(FindingLevel.Warning, "기본 사운드 목록 없음", $"`{DefaultSoundRegistryPath}`가 없습니다. `자동 보정` 또는 `VARCO/게임 메이커`에서 생성하세요.");
            else
                CheckRegistryEntries(defaultRegistry);

            var triggers = Object.FindObjectsByType<VWS.SoundEventTrigger>(FindObjectsSortMode.None);
            if (triggers.Length == 0)
                return;

            foreach (var trigger in triggers)
            {
                if (string.IsNullOrWhiteSpace(trigger.eventId))
                {
                    Add(FindingLevel.Error, "사운드 트리거 이벤트 ID 없음", "이벤트 ID를 지정하세요.", trigger);
                    continue;
                }

                var registry = trigger.registry ? trigger.registry : defaultRegistry;
                if (!registry)
                {
                    var level = trigger.fallbackClip ? FindingLevel.Warning : FindingLevel.Error;
                    Add(level, "사운드 목록 연결 없음", $"{trigger.name}: `{trigger.eventId}`가 사운드 목록 없이 사용됩니다.", trigger);
                    continue;
                }

                var hasClip = RegistryHasUsableClip(registry, trigger.eventId);
                if (!hasClip)
                {
                    var level = trigger.fallbackClip ? FindingLevel.Warning : FindingLevel.Error;
                    var detail = trigger.fallbackClip
                        ? $"{trigger.name}: `{trigger.eventId}`가 사운드 목록에는 없지만 예비 오디오 클립이 있습니다."
                        : $"{trigger.name}: `{trigger.eventId}`가 사운드 목록에 없고 예비 오디오 클립도 없습니다.";
                    Add(level, "사운드 이벤트 누락", detail, trigger);
                }
            }
        }

        void CheckRegistryEntries(VWS.SoundEventRegistry registry)
        {
            var emptyIds = registry.events.Where(e => e == null || string.IsNullOrWhiteSpace(e.id)).ToList();
            if (emptyIds.Count > 0)
                Add(FindingLevel.Warning, "사운드 이벤트 ID 비어 있음", $"{emptyIds.Count}개 항목에 ID가 없습니다.", registry);

            var emptyClips = registry.events.Where(e => e != null && !string.IsNullOrWhiteSpace(e.id) && e.clip == null).ToList();
            if (emptyClips.Count > 0)
                Add(FindingLevel.Warning, "사운드 오디오 클립 누락", $"{emptyClips.Count}개 항목에 오디오 클립이 없습니다.", registry);

            var duplicates = registry.events
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.id))
                .GroupBy(e => e.id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
                Add(FindingLevel.Warning, "사운드 목록 중복 ID", string.Join(", ", duplicates), registry);
            else
                Add(FindingLevel.Pass, "사운드 목록 확인", $"{registry.events.Count}개 이벤트", registry);
        }

        void CheckBuildSettings()
        {
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (string.IsNullOrWhiteSpace(scene.path))
                    continue;

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
                if (!sceneAsset)
                    Add(FindingLevel.Error, "빌드 설정 씬 누락", $"등록된 씬 파일이 없습니다: {scene.path}");
            }

            var activePath = SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(activePath) && !EditorBuildSettings.scenes.Any(s => s.enabled && s.path == activePath))
                Add(FindingLevel.Warning, "현재 씬 빌드 설정 미등록", activePath);
        }

        void CheckMissingScripts()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var missingObjects = new List<GameObject>();

            foreach (var root in roots)
                CollectMissingScriptObjects(root.transform, missingObjects);

            if (missingObjects.Count == 0)
                Add(FindingLevel.Pass, "잃어버린 스크립트 없음", "현재 씬의 게임 오브젝트 기준");
            else
                Add(FindingLevel.Error, "잃어버린 스크립트 발견", $"{missingObjects.Count}개 게임 오브젝트에 잃어버린 스크립트가 있습니다.", missingObjects[0]);
        }

        static void CollectMissingScriptObjects(Transform root, List<GameObject> results)
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root.gameObject) > 0)
                results.Add(root.gameObject);

            foreach (Transform child in root)
                CollectMissingScriptObjects(child, results);
        }

        static int CountNavMeshSurfaces()
        {
            var type = System.Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (type == null)
                return 0;

            return Object.FindObjectsByType(type, FindObjectsSortMode.None).Length;
        }

        static bool RegistryHasUsableClip(VWS.SoundEventRegistry registry, string id)
        {
            return registry.events.Any(e => e != null && e.id == id && e.clip != null);
        }

        static bool BuildSettingsCanLoad(string sceneName)
        {
            return EditorBuildSettings.scenes.Any(scene =>
            {
                if (!scene.enabled)
                    return false;

                var name = System.IO.Path.GetFileNameWithoutExtension(scene.path);
                return name == sceneName || scene.path == sceneName;
            });
        }

        void Add(FindingLevel level, string title, string detail, Object context = null)
        {
            findings.Add(new Finding
            {
                level = level,
                title = title,
                detail = detail,
                context = context
            });
        }

        string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"VARCO 씬 건강 검사 - {SceneManager.GetActiveScene().path}");
            foreach (var finding in findings)
                sb.AppendLine($"[{LevelLabel(finding.level)}] {finding.title} - {finding.detail}");
            return sb.ToString();
        }

        static string LevelLabel(FindingLevel level)
        {
            switch (level)
            {
                case FindingLevel.Pass: return "통과";
                case FindingLevel.Info: return "정보";
                case FindingLevel.Warning: return "확인";
                case FindingLevel.Error: return "오류";
                default: return level.ToString();
            }
        }
    }
}
#endif
