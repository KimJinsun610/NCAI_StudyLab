#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public class VARCOMenuSmokeTestWindow : EditorWindow
    {
        const string ReportFolder = "Assets/VARCOReports";
        const int MaxRecommendedGameObjects = 4000;
        const int MaxRecommendedRenderers = 1200;
        const int MaxRecommendedLights = 80;
        const int MaxRecommendedActiveLights = 40;

        static readonly string[] EssentialMenus =
        {
            "VARCO/캐릭터 프리팹 생성기",
            "VARCO/오브젝트 프리팹 생성기",
            "VARCO/프리셋 만들기",
            "VARCO/레거시/간략 자동 제작/간략 버전 열기",
            "VARCO/레거시/간략 자동 제작/프리셋 키트 폴더 모두 만들기",
            "VARCO/레거시/간략 자동 제작/프리셋 키트 실제 프리팹 채우기",
            "VARCO/레거시/세부 자동 제작/세부 버전 열기",
            "VARCO/레거시/세부 자동 제작/현재 씬 자동 보정",
            "VARCO/레거시/세부 자동 제작/기본 플레이 설명서",
            "VARCO/레거시/세부 자동 제작/한글 UX 점검 리포트",
            "VARCO/레거시/세부 자동 제작/프로젝트 사용 매뉴얼",
            "VARCO/레거시/세부 자동 제작/추천 게임 기준 부족한 에셋 요청서",
            "VARCO/레거시/세부 자동 제작/에셋 자동 매칭 진단서",
            "VARCO/레거시/세부 자동 제작/프리셋 키트 폴더 모두 만들기",
            "VARCO/레거시/세부 자동 제작/프리셋 키트 실제 프리팹 채우기",
            "VARCO/레거시/세부 자동 제작/장르별/전투 아레나 만들기",
            "VARCO/레거시/세부 자동 제작/장르별/탐험 좀비 게임 만들기",
            "VARCO/레거시/세부 자동 제작/장르별/퍼즐 방 만들기",
            "VARCO/레거시/세부 자동 제작/장르별/플랫폼 코스 만들기",
            "VARCO/레거시/세부 자동 제작/장르별/전체 기능 샌드박스 만들기",
            "VARCO/레거시/세부 자동 제작/게임 프리셋/수집 후 탈출 만들기",
            "VARCO/레거시/세부 자동 제작/게임 프리셋/제한시간 생존 만들기",
            "VARCO/레거시/세부 자동 제작/게임 프리셋/아레나 보스전 만들기",
            "VARCO/레거시/세부 자동 제작/게임 프리셋/탐험 좀비 생존 만들기",
            "VARCO/레거시/세부 자동 제작/게임 프리셋/탐험 보물찾기 만들기",
            "VARCO/레거시/세부 자동 제작/게임 프리셋/퍼즐 탈출방 만들기",
            "VARCO/레거시/세부 자동 제작/게임 프리셋/플랫폼 장애물 코스 만들기",
            "VARCO/레거시/세부 자동 제작/블록코딩/한글 블록 조립기",
            "VARCO/레거시/세부 자동 제작/블록코딩/선택 모델들 자동 판단 연결",
            "VARCO/레거시/세부 자동 제작/블록코딩/선택 에셋 배치 후 자동 판단 연결",
            "VARCO/레거시/세부 자동 제작/블록코딩/현재 씬 안전 보정",
            "VARCO/레거시/세부 자동 제작/블록코딩/선택 프리팹에 기능 추가",
            "VARCO/레거시/세부 자동 제작/검증/현재 씬 건강 검사"
        };

        static readonly string[] VerificationMenus =
        {
            "VARCO/레거시/세부 자동 제작/검증/자동 제작 메뉴 스모크 테스트",
            "VARCO/레거시/세부 자동 제작/검증/대표 프리셋 실제 생성 검증"
        };

        static readonly string[] RemovedMenus =
        {
            "VARCO/게임 메이커",
            "VARCO/통합 스튜디오",
            "VARCO/수업용 자동 준비",
            "VARCO/자동 제작/가장 맞는 게임 만들기",
            "VARCO/자동 제작/처음 사용자용 게임 만들기",
            "VARCO/자동 제작/플레이 준비 검사 리포트",
            "VARCO/자동 제작/부족한 에셋 요청서",
            "VARCO/자동 제작/Windows EXE 빌드",
            "VARCO/자동 제작/수업용 전체 준비",
            "VARCO/자동 제작/현재 씬 자동 보정",
            "VARCO/자동 제작/초보자 플레이 설명서",
            "VARCO/자동 제작/한글 UX 점검 리포트",
            "VARCO/자동 제작/프로젝트 사용 매뉴얼",
            "VARCO/자동 제작/추천 게임 기준 부족한 에셋 요청서",
            "VARCO/자동 제작/에셋 자동 매칭 진단서",
            "VARCO/자동 제작/장르별/전투 아레나 만들기",
            "VARCO/자동 제작/장르별/탐험 좀비 게임 만들기",
            "VARCO/자동 제작/장르별/퍼즐 방 만들기",
            "VARCO/자동 제작/장르별/플랫폼 코스 만들기",
            "VARCO/자동 제작/장르별/전체 기능 샌드박스 만들기",
            "VARCO/자동 제작/게임 프리셋/수집 후 탈출 만들기",
            "VARCO/자동 제작/게임 프리셋/제한시간 생존 만들기",
            "VARCO/자동 제작/게임 프리셋/아레나 보스전 만들기",
            "VARCO/자동 제작/게임 프리셋/탐험 좀비 생존 만들기",
            "VARCO/자동 제작/게임 프리셋/탐험 보물찾기 만들기",
            "VARCO/자동 제작/게임 프리셋/퍼즐 탈출방 만들기",
            "VARCO/자동 제작/게임 프리셋/플랫폼 장애물 코스 만들기",
            "VARCO/블록코딩/한글 블록 조립기",
            "VARCO/블록코딩/선택 모델들 자동 판단 연결",
            "VARCO/블록코딩/선택 에셋 배치 후 자동 판단 연결",
            "VARCO/블록코딩/현재 씬 안전 보정",
            "VARCO/블록코딩/선택 프리팹에 기능 추가",
            "VARCO/검증/현재 씬 건강 검사",
            "VARCO/검증/자동 제작 메뉴 스모크 테스트",
            "VARCO/검증/대표 프리셋 실제 생성 검증",
            "VARCO/플레이 테스트/튜닝 오버레이 추가",
            "VARCO/간략 자동 제작/간략 버전 열기",
            "VARCO/간략 자동 제작/프리셋 키트 폴더 모두 만들기",
            "VARCO/간략 자동 제작/프리셋 키트 실제 프리팹 채우기",
            "VARCO/세부 자동 제작/세부 버전 열기",
            "VARCO/세부 자동 제작/현재 씬 자동 보정",
            "VARCO/세부 자동 제작/기본 플레이 설명서",
            "VARCO/세부 자동 제작/한글 UX 점검 리포트",
            "VARCO/세부 자동 제작/프로젝트 사용 매뉴얼",
            "VARCO/세부 자동 제작/추천 게임 기준 부족한 에셋 요청서",
            "VARCO/세부 자동 제작/에셋 자동 매칭 진단서",
            "VARCO/세부 자동 제작/프리셋 키트 폴더 모두 만들기",
            "VARCO/세부 자동 제작/프리셋 키트 실제 프리팹 채우기",
            "VARCO/세부 자동 제작/장르별/전투 아레나 만들기",
            "VARCO/세부 자동 제작/장르별/탐험 좀비 게임 만들기",
            "VARCO/세부 자동 제작/장르별/퍼즐 방 만들기",
            "VARCO/세부 자동 제작/장르별/플랫폼 코스 만들기",
            "VARCO/세부 자동 제작/장르별/전체 기능 샌드박스 만들기",
            "VARCO/세부 자동 제작/게임 프리셋/수집 후 탈출 만들기",
            "VARCO/세부 자동 제작/게임 프리셋/제한시간 생존 만들기",
            "VARCO/세부 자동 제작/게임 프리셋/아레나 보스전 만들기",
            "VARCO/세부 자동 제작/게임 프리셋/탐험 좀비 생존 만들기",
            "VARCO/세부 자동 제작/게임 프리셋/탐험 보물찾기 만들기",
            "VARCO/세부 자동 제작/게임 프리셋/퍼즐 탈출방 만들기",
            "VARCO/세부 자동 제작/게임 프리셋/플랫폼 장애물 코스 만들기",
            "VARCO/세부 자동 제작/블록코딩/한글 블록 조립기",
            "VARCO/세부 자동 제작/블록코딩/선택 모델들 자동 판단 연결",
            "VARCO/세부 자동 제작/블록코딩/선택 에셋 배치 후 자동 판단 연결",
            "VARCO/세부 자동 제작/블록코딩/현재 씬 안전 보정",
            "VARCO/세부 자동 제작/블록코딩/선택 프리팹에 기능 추가",
            "VARCO/세부 자동 제작/검증/현재 씬 건강 검사",
            "VARCO/세부 자동 제작/검증/자동 제작 메뉴 스모크 테스트",
            "VARCO/세부 자동 제작/검증/대표 프리셋 실제 생성 검증"
        };

        static readonly BuildCase[] RepresentativeCases =
        {
            new BuildCase("탐험 좀비 생존", "VARCO/레거시/세부 자동 제작/게임 프리셋/탐험 좀비 생존 만들기", VWS.GenreType.Exploration,
                new[] { SceneFeature.Player, SceneFeature.Enemy, SceneFeature.Item, SceneFeature.NavMesh }),
            new BuildCase("퍼즐 탈출방", "VARCO/레거시/세부 자동 제작/게임 프리셋/퍼즐 탈출방 만들기", VWS.GenreType.Puzzle,
                new[] { SceneFeature.Player, SceneFeature.Door, SceneFeature.Goal }),
            new BuildCase("플랫폼 장애물 코스", "VARCO/레거시/세부 자동 제작/게임 프리셋/플랫폼 장애물 코스 만들기", VWS.GenreType.Platform,
                new[] { SceneFeature.Player, SceneFeature.Checkpoint, SceneFeature.DeathZone, SceneFeature.MovingPlatform, SceneFeature.Goal })
        };

        enum FindingLevel
        {
            Pass,
            Warn,
            Fail
        }

        enum SceneFeature
        {
            Player,
            Enemy,
            Item,
            Goal,
            NavMesh,
            Door,
            Checkpoint,
            DeathZone,
            MovingPlatform
        }

        class Finding
        {
            public FindingLevel level;
            public string area;
            public string title;
            public string detail;
        }

        class BuildCase
        {
            public readonly string label;
            public readonly string menuPath;
            public readonly VWS.GenreType expectedGenre;
            public readonly SceneFeature[] requiredFeatures;

            public BuildCase(string label, string menuPath, VWS.GenreType expectedGenre, SceneFeature[] requiredFeatures)
            {
                this.label = label;
                this.menuPath = menuPath;
                this.expectedGenre = expectedGenre;
                this.requiredFeatures = requiredFeatures;
            }
        }

        class SceneSnapshot
        {
            public string sceneName;
            public string scenePath;
            public VWS.GenreType? profileGenre;
            public int gameObjects;
            public int renderers;
            public int lights;
            public int activeLights;
            public int audioSources;
            public int missingScripts;
            public int gameManagers;
            public int thirdPersonPlayers;
            public int platformPlayers;
            public int playerHealth;
            public int huds;
            public int workshopHuds;
            public int cameras;
            public int enemies;
            public int waveManagers;
            public int navMeshAgents;
            public int navMeshVertices;
            public int agentsNearNavMesh;
            public int items;
            public int healthPickups;
            public int goals;
            public int doors;
            public int pressurePlates;
            public int checkpoints;
            public int deathZones;
            public int movingPlatforms;
            public int largeRenderers;
            public string[] largestRenderers;
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/검증/자동 제작 메뉴 스모크 테스트", priority = -29)]
        public static void RunQuickSmokeFromMenu()
        {
            var path = RunQuickSmokeReport();
            OpenReport(path);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/검증/대표 프리셋 실제 생성 검증", priority = -28)]
        public static void RunRepresentativeBuildSmokeFromMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "대표 프리셋 실제 생성 검증",
                    "대표 프리셋 3개를 실제로 생성하며 현재 씬이 변경될 수 있습니다. 계속하시겠습니까?",
                    "실행",
                    "취소"))
                return;

            var path = RunRepresentativeBuildSmokeReport();
            OpenReport(path);
        }

        public static string RunQuickSmokeReport()
        {
            var findings = new List<Finding>();
            var registeredMenus = CollectRegisteredVarcoMenus();

            ValidateMenus(registeredMenus, findings);
            ValidateComponentSmoke(findings);

            var snapshot = CaptureCurrentScene();
            ValidateSceneSnapshot(snapshot, null, Array.Empty<SceneFeature>(), findings, "현재 씬");

            var path = WriteReport(
                "VARCO 자동 제작 메뉴 스모크 테스트 리포트",
                "빠른 검증",
                findings,
                registeredMenus,
                new[] { snapshot },
                Array.Empty<string>());

            Debug.Log("[VARCO 검증] 자동 제작 메뉴 스모크 테스트 완료: " + path);
            return path;
        }

        public static string RunRepresentativeBuildSmokeReport()
        {
            var findings = new List<Finding>();
            var registeredMenus = CollectRegisteredVarcoMenus();
            var snapshots = new List<SceneSnapshot>();
            var buildLogs = new List<string>();

            ValidateMenus(registeredMenus, findings);
            ValidateComponentSmoke(findings);

            try
            {
                for (int i = 0; i < RepresentativeCases.Length; i++)
                {
                    var buildCase = RepresentativeCases[i];
                    EditorUtility.DisplayProgressBar(
                        "VARCO 대표 프리셋 검증",
                        buildCase.label + " 생성 중",
                        (float)i / RepresentativeCases.Length);

                    if (!registeredMenus.Contains(buildCase.menuPath))
                    {
                        Add(findings, FindingLevel.Fail, buildCase.label, "메뉴 없음", buildCase.menuPath + " 메뉴가 등록되어 있지 않습니다.");
                        continue;
                    }

                    var startedAt = DateTime.Now;
                    try
                    {
                        if (!EditorApplication.ExecuteMenuItem(buildCase.menuPath))
                        {
                            Add(findings, FindingLevel.Fail, buildCase.label, "메뉴 실행 실패", buildCase.menuPath + " 실행 결과가 false입니다.");
                            continue;
                        }

                        AssetDatabase.SaveAssets();
                        EditorSceneManager.SaveOpenScenes();
                        var snapshot = CaptureCurrentScene();
                        snapshots.Add(snapshot);
                        ValidateSceneSnapshot(snapshot, buildCase.expectedGenre, buildCase.requiredFeatures, findings, buildCase.label);
                        buildLogs.Add("- " + buildCase.label + ": " + (DateTime.Now - startedAt).TotalSeconds.ToString("0.0") + "초, 씬 " + snapshot.sceneName);
                    }
                    catch (Exception exception)
                    {
                        Add(findings, FindingLevel.Fail, buildCase.label, "생성 중 예외", exception.GetType().Name + ": " + exception.Message);
                        buildLogs.Add("- " + buildCase.label + ": 예외 " + exception.GetType().Name);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var path = WriteReport(
                "VARCO 대표 프리셋 실제 생성 검증 리포트",
                "대표 프리셋 실제 생성 검증",
                findings,
                registeredMenus,
                snapshots,
                buildLogs);

            Debug.Log("[VARCO 검증] 대표 프리셋 실제 생성 검증 완료: " + path);
            return path;
        }

        static SortedSet<string> CollectRegisteredVarcoMenus()
        {
            var menuItems = new SortedSet<string>(StringComparer.Ordinal);
            var menuAttributeType = typeof(MenuItem);
            var menuField = menuAttributeType.GetField("menuItem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var menuProperty = menuAttributeType.GetProperty("menuItem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                if (types == null)
                    continue;

                foreach (var type in types)
                {
                    if (type == null)
                        continue;

                    var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
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

            return menuItems;
        }

        static void ValidateMenus(SortedSet<string> registeredMenus, List<Finding> findings)
        {
            foreach (var menu in EssentialMenus)
                Add(findings, registeredMenus.Contains(menu) ? FindingLevel.Pass : FindingLevel.Fail,
                    "메뉴 등록", menu, registeredMenus.Contains(menu) ? "필수 메뉴가 등록되어 있습니다." : "필수 메뉴가 누락되었습니다.");

            foreach (var menu in VerificationMenus)
                Add(findings, registeredMenus.Contains(menu) ? FindingLevel.Pass : FindingLevel.Fail,
                    "검증 메뉴", menu, registeredMenus.Contains(menu) ? "검증 메뉴가 등록되어 있습니다." : "검증 메뉴가 누락되었습니다.");

            foreach (var menu in RemovedMenus)
            {
                if (registeredMenus.Contains(menu))
                    Add(findings, FindingLevel.Fail, "메뉴 정리", menu, "삭제 대상 메뉴가 아직 등록되어 있습니다.");
            }

            var expected = new HashSet<string>(EssentialMenus.Concat(VerificationMenus), StringComparer.Ordinal);
            var extraMenus = registeredMenus.Where(menu => !expected.Contains(menu) && !RemovedMenus.Contains(menu)).ToArray();
            Add(findings, extraMenus.Length == 0 ? FindingLevel.Pass : FindingLevel.Warn,
                "메뉴 정리",
                "추가 VARCO 메뉴",
                extraMenus.Length == 0 ? "관리 대상 외 메뉴가 없습니다." : string.Join("\n", extraMenus));
        }

        static void ValidateComponentSmoke(List<Finding> findings)
        {
            string result;
            try
            {
                result = VARCOSelectedFeatureApplicatorWindow.RunComponentSmokeTest();
            }
            catch (Exception exception)
            {
                Add(findings, FindingLevel.Fail, "기능 부착", "스모크 테스트 예외", exception.GetType().Name + ": " + exception.Message);
                return;
            }

            var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                Add(findings, FindingLevel.Fail, "기능 부착", "스모크 테스트 결과 없음", "기능 부착 테스트가 결과를 반환하지 않았습니다.");
                return;
            }

            foreach (var line in lines)
            {
                var pass = line.IndexOf("=PASS", StringComparison.OrdinalIgnoreCase) >= 0;
                Add(findings, pass ? FindingLevel.Pass : FindingLevel.Fail,
                    "기능 부착",
                    line,
                    pass ? "선택 프리팹 기능 추가가 필요한 컴포넌트를 생성했습니다." : "기능 부착 결과를 확인해야 합니다.");
            }
        }

        static SceneSnapshot CaptureCurrentScene()
        {
            var scene = SceneManager.GetActiveScene();
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(IsSceneObject)
                .ToArray();
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            var navMesh = NavMesh.CalculateTriangulation();
            var largestRenderers = renderers
                .Where(renderer => renderer && renderer.enabled)
                .Select(renderer => new
                {
                    renderer.name,
                    size = Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y, renderer.bounds.size.z)
                })
                .OrderByDescending(row => row.size)
                .Take(5)
                .Select(row => row.name + " (" + row.size.ToString("0.0") + "m)")
                .ToArray();

            return new SceneSnapshot
            {
                sceneName = scene.name,
                scenePath = scene.path,
                profileGenre = CurrentProfileGenre(),
                gameObjects = allObjects.Length,
                renderers = renderers.Length,
                lights = lights.Length,
                activeLights = lights.Count(light => light && light.isActiveAndEnabled),
                audioSources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length,
                missingScripts = CountMissingScripts(allObjects),
                gameManagers = Object.FindObjectsByType<VWS.GameManager>(FindObjectsSortMode.None).Length,
                thirdPersonPlayers = Object.FindObjectsByType<VWS.PlayerController_ThirdPerson>(FindObjectsSortMode.None).Length,
                platformPlayers = Object.FindObjectsByType<VWS.PlayerController_Platform>(FindObjectsSortMode.None).Length,
                playerHealth = Object.FindObjectsByType<VWS.PlayerHealth>(FindObjectsSortMode.None).Length,
                huds = Object.FindObjectsByType<VWS.VARCOGameHUD>(FindObjectsSortMode.None).Length,
                workshopHuds = Object.FindObjectsByType<VWS.WorkshopHUD>(FindObjectsSortMode.None).Length,
                cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(camera => camera && camera.enabled),
                enemies = Object.FindObjectsByType<VWS.EnemyAI_NavMesh>(FindObjectsSortMode.None).Length,
                waveManagers = Object.FindObjectsByType<VWS.WaveManager>(FindObjectsSortMode.None).Length,
                navMeshAgents = agents.Length,
                navMeshVertices = navMesh.vertices == null ? 0 : navMesh.vertices.Length,
                agentsNearNavMesh = agents.Count(IsAgentNearNavMesh),
                items = Object.FindObjectsByType<VWS.ItemPickup>(FindObjectsSortMode.None).Length,
                healthPickups = Object.FindObjectsByType<VWS.HealthPickup>(FindObjectsSortMode.None).Length,
                goals = Object.FindObjectsByType<VWS.GoalTrigger>(FindObjectsSortMode.None).Length
                    + Object.FindObjectsByType<VWS.PuzzleGoal>(FindObjectsSortMode.None).Length
                    + Object.FindObjectsByType<VWS.PlatformGoal>(FindObjectsSortMode.None).Length,
                doors = Object.FindObjectsByType<VWS.DoorController>(FindObjectsSortMode.None).Length,
                pressurePlates = Object.FindObjectsByType<VWS.PressurePlate>(FindObjectsSortMode.None).Length,
                checkpoints = Object.FindObjectsByType<VWS.Checkpoint>(FindObjectsSortMode.None).Length,
                deathZones = Object.FindObjectsByType<VWS.DeathZone>(FindObjectsSortMode.None).Length,
                movingPlatforms = Object.FindObjectsByType<VWS.MovingPlatform>(FindObjectsSortMode.None).Length,
                largeRenderers = renderers.Count(renderer => renderer && Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y, renderer.bounds.size.z) > 40f),
                largestRenderers = largestRenderers
            };
        }

        static bool IsSceneObject(GameObject gameObject)
        {
            return gameObject &&
                gameObject.scene.IsValid() &&
                !EditorUtility.IsPersistent(gameObject) &&
                (gameObject.hideFlags & HideFlags.HideAndDontSave) == 0;
        }

        static int CountMissingScripts(GameObject[] objects)
        {
            var count = 0;
            foreach (var gameObject in objects)
            {
                var components = gameObject.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (!component)
                        count++;
                }
            }

            return count;
        }

        static VWS.GenreType? CurrentProfileGenre()
        {
            var manager = Object.FindFirstObjectByType<VWS.GameManager>();
            if (!manager || !manager.profile)
                return null;

            return manager.profile.genre;
        }

        static bool IsAgentNearNavMesh(NavMeshAgent agent)
        {
            return agent && NavMesh.SamplePosition(agent.transform.position, out _, 2f, NavMesh.AllAreas);
        }

        static void ValidateSceneSnapshot(SceneSnapshot snapshot, VWS.GenreType? expectedGenre, SceneFeature[] requiredFeatures, List<Finding> findings, string area)
        {
            Add(findings, snapshot.gameManagers > 0 ? FindingLevel.Pass : FindingLevel.Fail, area, "게임 매니저", snapshot.gameManagers + "개");
            Add(findings, snapshot.cameras > 0 ? FindingLevel.Pass : FindingLevel.Fail, area, "카메라", snapshot.cameras + "개");
            Add(findings, snapshot.huds > 0 || snapshot.workshopHuds > 0 ? FindingLevel.Pass : FindingLevel.Warn, area, "HUD", "VARCO HUD " + snapshot.huds + "개 / Workshop HUD " + snapshot.workshopHuds + "개");
            Add(findings, snapshot.missingScripts == 0 ? FindingLevel.Pass : FindingLevel.Fail, area, "누락 스크립트", snapshot.missingScripts + "개");

            var playerCount = snapshot.thirdPersonPlayers + snapshot.platformPlayers + snapshot.playerHealth;
            Add(findings, playerCount > 0 ? FindingLevel.Pass : FindingLevel.Fail, area, "플레이어", "3인칭 " + snapshot.thirdPersonPlayers + " / 플랫폼 " + snapshot.platformPlayers + " / 체력 " + snapshot.playerHealth);
            Add(findings, snapshot.thirdPersonPlayers > 0 && snapshot.platformPlayers > 0 ? FindingLevel.Fail : FindingLevel.Pass, area, "플레이어 컨트롤러 혼합", "3인칭 " + snapshot.thirdPersonPlayers + " / 플랫폼 " + snapshot.platformPlayers);

            if (expectedGenre.HasValue)
            {
                Add(findings, snapshot.profileGenre.HasValue && snapshot.profileGenre.Value == expectedGenre.Value ? FindingLevel.Pass : FindingLevel.Fail,
                    area,
                    "게임 장르",
                    snapshot.profileGenre.HasValue ? snapshot.profileGenre.Value + " / 기대 " + expectedGenre.Value : "프로필 없음 / 기대 " + expectedGenre.Value);
            }

            foreach (var feature in requiredFeatures)
                ValidateRequiredFeature(snapshot, feature, findings, area);

            var expectsEnemyWave = requiredFeatures != null && requiredFeatures.Contains(SceneFeature.Enemy);
            if (!expectsEnemyWave && expectedGenre.HasValue && (expectedGenre.Value == VWS.GenreType.Platform || expectedGenre.Value == VWS.GenreType.Puzzle))
            {
                var leftoverEnemySystems = snapshot.enemies + snapshot.waveManagers;
                Add(findings, leftoverEnemySystems == 0 ? FindingLevel.Pass : FindingLevel.Fail,
                    area,
                    "불필요한 적/웨이브",
                    "적 " + snapshot.enemies + " / 웨이브 " + snapshot.waveManagers);
            }

            Add(findings, snapshot.gameObjects <= MaxRecommendedGameObjects ? FindingLevel.Pass : FindingLevel.Warn,
                area, "오브젝트 수", snapshot.gameObjects + "개 / 권장 " + MaxRecommendedGameObjects + "개 이하");
            Add(findings, snapshot.renderers <= MaxRecommendedRenderers ? FindingLevel.Pass : FindingLevel.Warn,
                area, "렌더러 수", snapshot.renderers + "개 / 권장 " + MaxRecommendedRenderers + "개 이하");
            Add(findings, snapshot.lights <= MaxRecommendedLights ? FindingLevel.Pass : FindingLevel.Warn,
                area, "조명 수", snapshot.lights + "개 / 권장 " + MaxRecommendedLights + "개 이하");
            Add(findings, snapshot.activeLights <= MaxRecommendedActiveLights ? FindingLevel.Pass : FindingLevel.Warn,
                area, "활성 조명 수", snapshot.activeLights + "개 / 권장 " + MaxRecommendedActiveLights + "개 이하");
            Add(findings, snapshot.largeRenderers == 0 ? FindingLevel.Pass : FindingLevel.Warn,
                area, "대형 렌더러", snapshot.largeRenderers == 0 ? "시야를 크게 가릴 가능성이 낮습니다." : string.Join("\n", snapshot.largestRenderers));

            if (snapshot.navMeshAgents > 0)
            {
                Add(findings, snapshot.navMeshVertices > 0 ? FindingLevel.Pass : FindingLevel.Fail,
                    area, "NavMesh", "정점 " + snapshot.navMeshVertices + "개 / 에이전트 " + snapshot.navMeshAgents + "개");
                Add(findings, snapshot.agentsNearNavMesh == snapshot.navMeshAgents ? FindingLevel.Pass : FindingLevel.Warn,
                    area, "NavMesh Agent 배치", "NavMesh 근처 " + snapshot.agentsNearNavMesh + " / 전체 " + snapshot.navMeshAgents);
            }
        }

        static void ValidateRequiredFeature(SceneSnapshot snapshot, SceneFeature feature, List<Finding> findings, string area)
        {
            switch (feature)
            {
                case SceneFeature.Player:
                    Add(findings, snapshot.thirdPersonPlayers + snapshot.platformPlayers + snapshot.playerHealth > 0 ? FindingLevel.Pass : FindingLevel.Fail, area, "필수 기능: 플레이어", "플레이어 기능 확인");
                    break;
                case SceneFeature.Enemy:
                    Add(findings, snapshot.enemies > 0 || snapshot.waveManagers > 0 ? FindingLevel.Pass : FindingLevel.Fail, area, "필수 기능: 적", "적 " + snapshot.enemies + " / 웨이브 " + snapshot.waveManagers);
                    break;
                case SceneFeature.Item:
                    Add(findings, snapshot.items + snapshot.healthPickups > 0 ? FindingLevel.Pass : FindingLevel.Fail, area, "필수 기능: 아이템", "수집 " + snapshot.items + " / 회복 " + snapshot.healthPickups);
                    break;
                case SceneFeature.Goal:
                    Add(findings, snapshot.goals > 0 ? FindingLevel.Pass : FindingLevel.Fail, area, "필수 기능: 목표", snapshot.goals + "개");
                    break;
                case SceneFeature.NavMesh:
                    Add(findings, snapshot.navMeshVertices > 0 ? FindingLevel.Pass : FindingLevel.Fail, area, "필수 기능: NavMesh", snapshot.navMeshVertices + " 정점");
                    break;
                case SceneFeature.Door:
                    Add(findings, snapshot.doors > 0 || snapshot.pressurePlates > 0 ? FindingLevel.Pass : FindingLevel.Fail, area, "필수 기능: 문/스위치", "문 " + snapshot.doors + " / 스위치 " + snapshot.pressurePlates);
                    break;
                case SceneFeature.Checkpoint:
                    Add(findings, snapshot.checkpoints > 0 ? FindingLevel.Pass : FindingLevel.Fail, area, "필수 기능: 체크포인트", snapshot.checkpoints + "개");
                    break;
                case SceneFeature.DeathZone:
                    Add(findings, snapshot.deathZones > 0 ? FindingLevel.Pass : FindingLevel.Fail, area, "필수 기능: 낙하/위험 구역", snapshot.deathZones + "개");
                    break;
                case SceneFeature.MovingPlatform:
                    Add(findings, snapshot.movingPlatforms > 0 ? FindingLevel.Pass : FindingLevel.Fail, area, "필수 기능: 이동 발판", snapshot.movingPlatforms + "개");
                    break;
            }
        }

        static void Add(List<Finding> findings, FindingLevel level, string area, string title, string detail)
        {
            findings.Add(new Finding
            {
                level = level,
                area = area,
                title = title,
                detail = detail ?? ""
            });
        }

        static string WriteReport(string title, string mode, List<Finding> findings, SortedSet<string> registeredMenus, IReadOnlyList<SceneSnapshot> snapshots, IReadOnlyList<string> buildLogs)
        {
            EnsureReportFolder();

            var pass = findings.Count(finding => finding.level == FindingLevel.Pass);
            var warn = findings.Count(finding => finding.level == FindingLevel.Warn);
            var fail = findings.Count(finding => finding.level == FindingLevel.Fail);
            var fileName = mode.Replace(" ", "_").Replace("/", "_") + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".md";
            var path = ReportFolder + "/" + fileName;

            var builder = new StringBuilder();
            builder.AppendLine("# " + title);
            builder.AppendLine();
            builder.AppendLine("- 생성 시각: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine("- 검증 모드: " + mode);
            builder.AppendLine("- 활성 씬: " + SceneManager.GetActiveScene().name);
            builder.AppendLine("- 판정: PASS " + pass + " / WARN " + warn + " / FAIL " + fail);
            builder.AppendLine();

            builder.AppendLine("## 요약");
            if (fail > 0)
                builder.AppendLine("- 결과: 수정 필요. FAIL 항목을 우선 해결해야 합니다.");
            else if (warn > 0)
                builder.AppendLine("- 결과: 기본 동작은 가능하지만 품질 또는 성능 경고가 있습니다.");
            else
                builder.AppendLine("- 결과: 등록된 검증 기준을 모두 통과했습니다.");
            builder.AppendLine();

            if (buildLogs.Count > 0)
            {
                builder.AppendLine("## 대표 프리셋 생성 로그");
                foreach (var line in buildLogs)
                    builder.AppendLine(line);
                builder.AppendLine();
            }

            builder.AppendLine("## 판정 상세");
            foreach (var group in findings.GroupBy(finding => finding.area))
            {
                builder.AppendLine("### " + group.Key);
                builder.AppendLine("| 상태 | 항목 | 내용 |");
                builder.AppendLine("| --- | --- | --- |");
                foreach (var finding in group)
                    builder.AppendLine("| " + finding.level + " | " + EscapeCell(finding.title) + " | " + EscapeCell(finding.detail) + " |");
                builder.AppendLine();
            }

            builder.AppendLine("## 현재 등록된 VARCO 메뉴");
            foreach (var menu in registeredMenus)
                builder.AppendLine("- " + menu);
            builder.AppendLine();

            builder.AppendLine("## 씬 스냅샷");
            foreach (var snapshot in snapshots)
                AppendSceneSnapshot(builder, snapshot);

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(path);
            return path;
        }

        static void AppendSceneSnapshot(StringBuilder builder, SceneSnapshot snapshot)
        {
            builder.AppendLine("### " + snapshot.sceneName);
            builder.AppendLine("- 경로: " + (string.IsNullOrEmpty(snapshot.scenePath) ? "(저장되지 않음)" : snapshot.scenePath));
            builder.AppendLine("- 장르 프로필: " + (snapshot.profileGenre.HasValue ? snapshot.profileGenre.Value.ToString() : "(없음)"));
            builder.AppendLine("- 오브젝트/렌더러/조명: " + snapshot.gameObjects + " / " + snapshot.renderers + " / " + snapshot.lights + " (활성 " + snapshot.activeLights + ")");
            builder.AppendLine("- 플레이어: 3인칭 " + snapshot.thirdPersonPlayers + " / 플랫폼 " + snapshot.platformPlayers + " / 체력 " + snapshot.playerHealth);
            builder.AppendLine("- 전투/탐험: 적 " + snapshot.enemies + " / 웨이브 " + snapshot.waveManagers + " / 아이템 " + snapshot.items + " / 회복 " + snapshot.healthPickups);
            builder.AppendLine("- 퍼즐/플랫폼: 문 " + snapshot.doors + " / 스위치 " + snapshot.pressurePlates + " / 체크포인트 " + snapshot.checkpoints + " / 위험 구역 " + snapshot.deathZones + " / 이동 발판 " + snapshot.movingPlatforms);
            builder.AppendLine("- NavMesh: 정점 " + snapshot.navMeshVertices + " / 에이전트 " + snapshot.navMeshAgents + " / NavMesh 근처 " + snapshot.agentsNearNavMesh);
            builder.AppendLine("- HUD/카메라/오디오: HUD " + snapshot.huds + " / WorkshopHUD " + snapshot.workshopHuds + " / 카메라 " + snapshot.cameras + " / 오디오 " + snapshot.audioSources);
            builder.AppendLine("- 누락 스크립트: " + snapshot.missingScripts);
            if (snapshot.largestRenderers != null && snapshot.largestRenderers.Length > 0)
                builder.AppendLine("- 가장 큰 렌더러: " + string.Join(", ", snapshot.largestRenderers));
            builder.AppendLine();
        }

        static string EscapeCell(string value)
        {
            return (value ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", "<br>");
        }

        static void EnsureReportFolder()
        {
            if (AssetDatabase.IsValidFolder(ReportFolder))
                return;

            if (!AssetDatabase.IsValidFolder("Assets/VARCOReports"))
                AssetDatabase.CreateFolder("Assets", "VARCOReports");
        }

        static void OpenReport(string path)
        {
            AssetDatabase.Refresh();
            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            EditorUtility.RevealInFinder(path);
        }
    }
}
#endif
