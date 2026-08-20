#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using VWS = VARCO_Workshop;
using Object = UnityEngine.Object;

namespace VARCO_Workshop.Editor
{
    public class VARCOGameMakerWindow : EditorWindow
    {
        const string RegistryPath = "Assets/ScriptableObjects/SoundEvents/WorkshopSoundEventRegistry.asset";
        const string GeneratedAnimationFolder = "Assets/Animations/Generated";
        const string VisualProfileFolder = "Assets/ScriptableObjects/VisualProfiles";
        const string AutoConnectedPrefabFolder = "Assets/Prefabs/Characters/AutoConnected";
        const string ReportFolder = "Assets/VARCOReports";
        const string PresetFolder = "Assets/ScriptableObjects/GameMakerPresets";
        const string PresetKitRoot = "Assets/VARCOPresetKits";
        const string TtsAudioFolder = "Assets/Audio/TTS";
        const string BuildRoot = "Builds/Windows";
        const string AutoBuildLayoutRootName = "VARCO_AutoBuildLayout";
        const double EditorSummaryCacheSeconds = 2.0;

        static readonly string[] GameObjectRootCandidates =
        {
            "Assets/Prefabs",
            "Assets/VARCO3DImports",
            "Assets/Importing Assets",
            "Assets/Models",
            "Assets/Characters",
            "Assets/Environment",
            "Assets/Environments",
            "Assets/Art",
            "Assets/Generated",
            PresetKitRoot,
            "Assets/Resources"
        };

        static readonly string[] AudioRootCandidates =
        {
            "Assets/Audio",
            TtsAudioFolder,
            "Assets/Importing Assets",
            "Assets/VARCO3DImports",
            "Assets/Sounds",
            "Assets/Sound",
            "Assets/Music",
            "Assets/Generated",
            "Assets/Resources"
        };

        static readonly string[] AnimationRootCandidates =
        {
            "Assets/Animations",
            "Assets/VARCO3DImports",
            "Assets/Importing Assets",
            "Assets/Models",
            "Assets/Characters",
            "Assets/Generated",
            "Assets/Resources"
        };

        static readonly Dictionary<VWS.GenreType, string> ProfileByGenre = new Dictionary<VWS.GenreType, string>
        {
            { VWS.GenreType.Platform, "Assets/ScriptableObjects/GameProfiles/VARCO_Platform_Profile.asset" },
            { VWS.GenreType.Arena, "Assets/ScriptableObjects/GameProfiles/VARCO_Arena_Profile.asset" },
            { VWS.GenreType.Exploration, "Assets/ScriptableObjects/GameProfiles/VARCO_Exploration_Profile.asset" },
            { VWS.GenreType.Puzzle, "Assets/ScriptableObjects/GameProfiles/VARCO_Puzzle_Profile.asset" }
        };

        public enum SceneMode
        {
            CurrentScene,
            GenreScene
        }

        public enum GameRecipe
        {
            GenreDefault,
            CombatWave,
            ExplorationQuest,
            DoorPuzzle,
            PlatformCourse,
            CollectAndEscape,
            SurvivalTimer,
            BossBattle,
            ZombieSurvival,
            TreasureHunt,
            EscapeRoom,
            ObstacleRun
        }

        public enum BlockTemplate
        {
            Custom,
            ArenaCombatWave,
            ExplorationZombieQuest,
            PuzzleDoorRoom,
            PlatformSpaceCourse,
            CollectAndEscape,
            SurvivalTimer,
            ArenaBossBattle,
            ExplorationZombieSurvival,
            ExplorationTreasureHunt,
            PuzzleEscapeRoom,
            PlatformObstacleRun,
            FullFeatureSandbox
        }

        enum AssetRole
        {
            Unknown,
            Player,
            Enemy,
            Weapon,
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

        enum CharacterKind
        {
            None,
            Player,
            Boss,
            Zombie,
            Orc,
            Drone,
            Object
        }

        public enum EnemyCharacterChoice
        {
            Auto,
            Boss,
            Zombie,
            Orc,
            Drone,
            Any
        }

        public enum PlayerCharacterChoice
        {
            Auto,
            Arena,
            Exploration,
            Puzzle,
            Platform,
            Any
        }

        public enum DifficultyPreset
        {
            Story,
            Normal,
            Hard,
            Nightmare
        }

        public enum CameraPresetChoice
        {
            Auto,
            ThirdPerson,
            QuarterView,
            TopDown,
            SideView
        }

        public enum PlayerMovementChoice
        {
            Auto,
            CameraRelative,
            FacingDirection
        }

        enum RecommendedAction
        {
            None,
            EnableSafeAutomation,
            AutoDesign,
            BuildGame,
            BuildBestPreset,
            GenerateAssetRequest,
            BuildWindows
        }

        class AssetCandidate
        {
            public string path;
            public GameObject asset;
            public VWS.GenreType? genre;
            public AssetRole role;
            public CharacterKind characterKind;
            public int score;
            public string matchReason;
            public bool isPrefab;
            public int rendererCount;
            public int transformCount;
            public int lightCount;
            public int animatorCount;
            public bool hasSkinnedMesh;
            public bool hasVisuals;
            public int internalEvidenceCount;
            public bool usedInternalEvidence;
            public string normalizedText;
            public string pathNormalizedText;
            public bool fromPresetKit;
            public string presetKitKey;

            public string DisplayName => asset ? asset.name : Path.GetFileNameWithoutExtension(path);
        }

        class PresetReadinessScore
        {
            public VARCOGameMakerPreset preset;
            public int score;
            public int matchedRoles;
            public int fallbackRoles;
            public int activeRoles;
            public int characterWarnings;
            public string summary;
        }

        class AssetSlotStatus
        {
            public AssetRole role;
            public AssetCandidate selected;
            public int candidateCount;
            public bool preferredMatch;
            public string state;
            public string message;
        }

        class SoundSlotDefinition
        {
            public string id;
            public string label;
            public string primary;
            public string[] keywords;
            public float volume;
            public bool important;
            public VWS.GenreType? genre;
        }

        class SoundSlotStatus
        {
            public SoundSlotDefinition definition;
            public AudioClip clip;
            public string clipPath;
            public string state;
            public string reason;
            public int score;
            public bool fromRegistry;
        }

        class AudioCandidateMatch
        {
            public AudioClip clip;
            public string path;
            public int score;
            public string reason;
        }

        class AnimationSlotDefinition
        {
            public string ownerLabel;
            public string stateLabel;
            public AssetRole sourceRole;
            public string[] keywords;
            public bool important;
        }

        class AnimationSlotStatus
        {
            public AnimationSlotDefinition definition;
            public AnimationClip clip;
            public string clipPath;
            public string state;
            public string reason;
            public int score;
        }

        class AnimationCandidateMatch
        {
            public AnimationClip clip;
            public string path;
            public int score;
            public string reason;
        }

        class BlockAssemblyStatus
        {
            public string group;
            public string label;
            public string state;
            public string message;
            public bool active;
        }

        class AcceptanceFinding
        {
            public string state;
            public string area;
            public string message;
        }

        class OneClickRecommendation
        {
            public RecommendedAction action;
            public string title;
            public string detail;
            public string buttonLabel;
            public MessageType messageType;
        }

        public class UnifiedAssetMatchLine
        {
            public string stateCode;
            public string stateLabel;
            public string roleLabel;
            public string assetName;
            public string assetPath;
            public string detail;
            public int candidateCount;
            public bool hasAsset;
            public bool usedInternalEvidence;
        }

        public class OneClickCardReadinessLine
        {
            public BlockTemplate template;
            public string title;
            public string stateCode;
            public string stateLabel;
            public string summary;
            public string missingText;
            public string matchedText;
            public int readyRoles;
            public int warningRoles;
            public int fallbackRoles;
            public int totalRoles;
            public bool recommended;
        }

        readonly List<AssetCandidate> candidates = new List<AssetCandidate>();
        readonly List<string> log = new List<string>();
        readonly List<AssetRole> cachedActiveRoles = new List<AssetRole>();
        readonly List<AssetSlotStatus> cachedActiveAssetSlots = new List<AssetSlotStatus>();
        readonly List<BlockAssemblyStatus> cachedBlockAssemblyStatuses = new List<BlockAssemblyStatus>();
        readonly List<AcceptanceFinding> cachedAcceptanceFindings = new List<AcceptanceFinding>();
        readonly List<SoundSlotStatus> cachedSoundSlotStatuses = new List<SoundSlotStatus>();
        readonly List<AnimationSlotStatus> cachedAnimationSlotStatuses = new List<AnimationSlotStatus>();
        readonly List<AssetCandidate> cachedTopDetectedAssets = new List<AssetCandidate>();
        readonly List<AssetCandidate> cachedInternalMatches = new List<AssetCandidate>();
        readonly List<string> cachedOneClickNextSteps = new List<string>();

        Vector2 scroll;
        double editorSummaryCacheExpiresAt;
        string cachedRoleCountsText = "";
        string cachedAssetSlotSummaryText = "";
        string sceneObjectNameTokenCache;
        OneClickRecommendation cachedOneClickRecommendation;
        VWS.GenreType genre = VWS.GenreType.Arena;
        SceneMode sceneMode = SceneMode.CurrentScene;
        bool createMissingObjects = true;
        bool autoConnectPrefabs = true;
        bool autoAnimations = true;
        bool autoSounds = true;
        bool addModernHud = true;
        bool applyVisualPreset = true;
        bool runSafetyPass = true;
        bool addSceneToBuild = true;
        bool useOnlyActiveSceneForWindowsBuild = true;
        bool saveScene = true;
        bool acceptanceChecklistOpen = true;
        bool blockAssemblyOpen = true;
        bool assetMatchingGuideOpen = true;
        bool windowsBuildReadinessOpen = true;
        bool assetSummaryOpen;
        bool actionDiagnosticsOpen;
        int itemGoal = 3;
        int waveEnemyCount = 3;
        float countdownSeconds = 90f;
        bool advancedOpen;
        bool initializedBlockDefaults;
        string lastReportPath;
        string lastAutoDesignSummary;
        VARCOGameMakerPreset selectedPreset;

        GameRecipe recipe = GameRecipe.GenreDefault;
        BlockTemplate blockTemplate = BlockTemplate.Custom;
        PlayerCharacterChoice playerCharacter = PlayerCharacterChoice.Auto;
        EnemyCharacterChoice enemyCharacter = EnemyCharacterChoice.Auto;
        DifficultyPreset difficulty = DifficultyPreset.Normal;
        CameraPresetChoice cameraPreset = CameraPresetChoice.Auto;
        PlayerMovementChoice playerMovement = PlayerMovementChoice.Auto;
        bool blockPlayer = true;
        bool blockWeapon = true;
        bool blockEnemyWave;
        bool blockItems;
        bool blockGoal = true;
        bool blockHealthPickup;
        bool blockHazard;
        bool blockCheckpoint;
        bool blockFallRespawn;
        bool blockMovingPlatform;
        bool blockPuzzleDoor;
        bool blockMovableBox;
        bool blockCover;
        bool blockCountdown;
        bool blockHud = true;
        bool blockVisuals = true;
        bool blockSound = true;

        public static void Open()
        {
            var window = GetWindow<VARCOGameMakerWindow>("VARCO 게임 메이커");
            window.minSize = new Vector2(620f, 720f);
            window.Focus();
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/세부 버전 열기", priority = -99)]
        public static void OpenDetailedFromMenu()
        {
            Open();
        }

        public static void RunBestGameFromMenu()
        {
            var window = PrepareOneClickWindow();
            window.BuildBestPresetOneClick();
        }

        public static void BuildBeginnerGameFromMenu()
        {
            var window = PrepareOneClickWindow();
            window.RunBeginnerOneClick(buildWindows: false);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/장르별/전투 아레나 만들기", priority = -44)]
        public static void BuildArenaGameFromMenu()
        {
            BuildGenreOneClick(VWS.GenreType.Arena, BlockTemplate.ArenaCombatWave, buildWindows: false);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/장르별/탐험 좀비 게임 만들기", priority = -43)]
        public static void BuildExplorationGameFromMenu()
        {
            BuildGenreOneClick(VWS.GenreType.Exploration, BlockTemplate.ExplorationZombieQuest, buildWindows: false);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/장르별/퍼즐 방 만들기", priority = -42)]
        public static void BuildPuzzleGameFromMenu()
        {
            BuildGenreOneClick(VWS.GenreType.Puzzle, BlockTemplate.PuzzleDoorRoom, buildWindows: false);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/장르별/플랫폼 코스 만들기", priority = -41)]
        public static void BuildPlatformGameFromMenu()
        {
            BuildGenreOneClick(VWS.GenreType.Platform, BlockTemplate.PlatformSpaceCourse, buildWindows: false);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/장르별/전체 기능 샌드박스 만들기", priority = -40)]
        public static void BuildFullFeatureSandboxFromMenu()
        {
            BuildGenreOneClick(VWS.GenreType.Exploration, BlockTemplate.FullFeatureSandbox, buildWindows: false);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/게임 프리셋/수집 후 탈출 만들기", priority = -39)]
        public static void BuildCollectAndEscapeGameFromMenu()
        {
            BuildTemplateGame(BlockTemplate.CollectAndEscape);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/게임 프리셋/제한시간 생존 만들기", priority = -38)]
        public static void BuildSurvivalTimerGameFromMenu()
        {
            BuildTemplateGame(BlockTemplate.SurvivalTimer);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/게임 프리셋/아레나 보스전 만들기", priority = -37)]
        public static void BuildArenaBossBattleGameFromMenu()
        {
            BuildTemplateGame(BlockTemplate.ArenaBossBattle);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/게임 프리셋/탐험 좀비 생존 만들기", priority = -36)]
        public static void BuildExplorationZombieSurvivalGameFromMenu()
        {
            BuildTemplateGame(BlockTemplate.ExplorationZombieSurvival);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/게임 프리셋/탐험 보물찾기 만들기", priority = -35)]
        public static void BuildExplorationTreasureHuntGameFromMenu()
        {
            BuildTemplateGame(BlockTemplate.ExplorationTreasureHunt);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/게임 프리셋/퍼즐 탈출방 만들기", priority = -34)]
        public static void BuildPuzzleEscapeRoomGameFromMenu()
        {
            BuildTemplateGame(BlockTemplate.PuzzleEscapeRoom);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/게임 프리셋/플랫폼 장애물 코스 만들기", priority = -33)]
        public static void BuildPlatformObstacleRunGameFromMenu()
        {
            BuildTemplateGame(BlockTemplate.PlatformObstacleRun);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/현재 씬 자동 보정", priority = -49)]
        public static void FixCurrentSceneFromMenu()
        {
            var window = PrepareOneClickWindow();
            window.FixAllCurrentScene();
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/프리셋 검증/변경된 프리셋 재검증 + NavMesh 재베이크", priority = -50)]
        public static void RevalidateChangedPresetSceneFromMenu()
        {
            var window = PrepareRevalidationWindow();
            window.RevalidateChangedPresetScene();
        }

        public static void GeneratePlayReadyReportFromMenu()
        {
            var window = PrepareOneClickWindow();
            window.GeneratePlayReadyReport();
            window.OpenLastReport();
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/기본 플레이 설명서", priority = -48)]
        public static void GenerateBeginnerPlayGuideFromMenu()
        {
            var window = PrepareOneClickWindow();
            window.GenerateBeginnerPlayGuide();
            window.OpenLastReport();
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/한글 UX 점검 리포트", priority = -48)]
        public static void GenerateKoreanUxReportFromMenu()
        {
            var window = PrepareOneClickWindow();
            window.GenerateKoreanUxReport();
            window.OpenLastReport();
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/프로젝트 사용 매뉴얼", priority = -48)]
        public static void GenerateProjectManualFromMenu()
        {
            var window = PrepareOneClickWindow();
            window.GenerateProjectManual();
            window.OpenLastReport();
        }

        public static void GenerateAssetRequestFromMenu()
        {
            var window = PrepareOneClickWindow();
            window.GenerateAssetRequestSheet();
            window.OpenLastReport();
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/추천 게임 기준 부족한 에셋 요청서", priority = -47)]
        public static void GenerateBestAssetRequestFromMenu()
        {
            var window = PrepareOneClickWindow();
            if (!window.SelectBestReadyPreset())
                return;

            window.GenerateAssetRequestSheet();
            window.OpenLastReport();
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/에셋 자동 매칭 진단서", priority = -47)]
        public static void GenerateAssetMatchingReportFromMenu()
        {
            var window = PrepareOneClickWindow();
            window.GenerateAssetMatchingReport();
            window.OpenLastReport();
        }

        public static void BuildWindowsExeFromMenu()
        {
            var window = PrepareOneClickWindow();
            window.BuildBestPresetWindowsExe();
        }

        static VARCOGameMakerWindow PrepareOneClickWindow()
        {
            var window = GetWindow<VARCOGameMakerWindow>("VARCO 게임 메이커");
            window.minSize = new Vector2(620f, 720f);
            window.EnableSafeAutomationDefaults();
            window.ScanAssets();
            window.NormalizeBlockPlan();
            window.Focus();
            return window;
        }

        static VARCOGameMakerWindow PrepareRevalidationWindow()
        {
            var window = GetWindow<VARCOGameMakerWindow>("VARCO Game Maker");
            window.minSize = new Vector2(620f, 720f);
            window.createMissingObjects = true;
            window.runSafetyPass = true;
            window.addSceneToBuild = true;
            window.saveScene = true;
            window.addModernHud = true;
            window.autoSounds = false;
            window.Focus();
            return window;
        }

        public static List<UnifiedAssetMatchLine> BuildUnifiedAssetMatchSummary(int maxRows)
        {
            var worker = CreateInstance<VARCOGameMakerWindow>();
            try
            {
                worker.EnableSafeAutomationDefaults();
                worker.AutoDesignFromAssets();
                worker.NormalizeBlockPlan();

                var roles = worker.ActiveRolesForCurrentBlocks()
                    .Distinct()
                    .ToList();

                if (roles.Count == 0)
                {
                    return new List<UnifiedAssetMatchLine>
                    {
                        new UnifiedAssetMatchLine
                        {
                            stateCode = "WARN",
                            stateLabel = "확인",
                            roleLabel = "활성 블록",
                            assetName = "없음",
                            detail = "아직 켜진 기능 블록이 없어 에셋 매칭을 계산할 수 없습니다.",
                            candidateCount = 0,
                            hasAsset = false
                        }
                    };
                }

                return roles
                    .Take(Mathf.Max(1, maxRows))
                    .Select(worker.BuildUnifiedAssetMatchLine)
                    .ToList();
            }
            finally
            {
                DestroyImmediate(worker);
            }
        }

        public static List<OneClickCardReadinessLine> BuildOneClickCardReadinessSummaries(IEnumerable<BlockTemplate> templates)
        {
            var templateList = (templates ?? Enumerable.Empty<BlockTemplate>())
                .Where(template => template != BlockTemplate.Custom)
                .Distinct()
                .ToList();

            var results = new List<OneClickCardReadinessLine>();
            if (templateList.Count == 0)
                return results;

            var worker = CreateInstance<VARCOGameMakerWindow>();
            try
            {
                worker.EnableSafeAutomationDefaults();
                worker.ScanAssets();

                foreach (var template in templateList)
                    results.Add(worker.BuildOneClickCardReadinessLine(template));

                var recommended = results
                    .OrderByDescending(line => line.readyRoles)
                    .ThenBy(line => line.fallbackRoles)
                    .ThenBy(line => line.warningRoles)
                    .ThenBy(line => line.totalRoles)
                    .FirstOrDefault();

                if (recommended != null)
                    recommended.recommended = true;

                return results;
            }
            finally
            {
                DestroyImmediate(worker);
            }
        }

        OneClickCardReadinessLine BuildOneClickCardReadinessLine(BlockTemplate template)
        {
            ApplyBlockTemplate(template);
            NormalizeBlockPlan();

            var roles = ActiveRolesForCurrentBlocks()
                .Distinct()
                .ToList();
            var slots = roles
                .Select(BuildAssetSlotStatus)
                .ToList();

            var readyRoles = slots.Count(slot => slot.state == "PASS");
            var warningRoles = slots.Count(slot => slot.state == "WARN");
            var fallbackRoles = slots.Count(slot => slot.state == "FALLBACK");
            var state = fallbackRoles == 0 && warningRoles == 0
                ? "PASS"
                : readyRoles > 0 || warningRoles > 0
                    ? "WARN"
                    : "FALLBACK";

            var missing = slots
                .Where(slot => slot.state == "FALLBACK")
                .Select(slot => AssetRoleLabel(slot.role))
                .Distinct()
                .Take(5)
                .ToList();
            var matched = slots
                .Where(slot => slot.state == "PASS" || slot.state == "WARN")
                .Select(slot => AssetRoleLabel(slot.role))
                .Distinct()
                .Take(5)
                .ToList();

            return new OneClickCardReadinessLine
            {
                template = template,
                title = BlockTemplateLabel(template),
                stateCode = state,
                stateLabel = StateLabel(state),
                readyRoles = readyRoles,
                warningRoles = warningRoles,
                fallbackRoles = fallbackRoles,
                totalRoles = Mathf.Max(1, roles.Count),
                summary = "VARCO 에셋 직접 매칭 " + readyRoles + "/" + Mathf.Max(1, roles.Count)
                    + "개, 기본 생성 " + fallbackRoles + "개"
                    + (warningRoles > 0 ? ", 확인 필요 " + warningRoles + "개" : ""),
                missingText = missing.Count == 0 ? "부족 역할 없음" : "부족: " + string.Join(", ", missing),
                matchedText = matched.Count == 0 ? "직접 매칭 없음" : "매칭: " + string.Join(", ", matched)
            };
        }

        static void BuildGenreOneClick(VWS.GenreType targetGenre, BlockTemplate template, bool buildWindows)
        {
            var worker = CreateInstance<VARCOGameMakerWindow>();
            try
            {
                worker.BuildGenreOneClickInternal(targetGenre, template, buildWindows);
            }
            finally
            {
                DestroyImmediate(worker);
            }
        }

        public static void BuildTemplateGame(BlockTemplate template, bool buildWindows = false)
        {
            BuildGenreOneClick(GenreForTemplate(template), template, buildWindows);
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/간략 자동 제작/프리셋 키트 폴더 모두 만들기", priority = -78)]
        public static void CreateAllPresetKitFoldersFromMenu()
        {
            var created = CreateAllPresetKitFolders(createPlaceholderPrefabs: true);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "VARCO 프리셋 키트",
                "프리셋 키트 폴더를 준비했습니다.\n생성/확인한 폴더: " + created + "개\n\nAssets/VARCOPresetKits 아래의 Parts 폴더에서 역할별 프리팹을 교체하면 자동 제작이 해당 프리셋 폴더를 먼저 사용합니다.",
                "확인");
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/프리셋 키트 폴더 모두 만들기", priority = -78)]
        public static void CreateAllPresetKitFoldersDetailedFromMenu()
        {
            CreateAllPresetKitFoldersFromMenu();
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/간략 자동 제작/프리셋 키트 실제 프리팹 채우기", priority = -77)]
        public static void FillAllPresetKitPrefabsFromMenu()
        {
            var result = FillAllPresetKitsFromCurrentMatches();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "VARCO 프리셋 키트",
                "프리셋 키트의 실제 슬롯 프리팹을 준비했습니다.\n"
                    + "자동 매칭으로 저장한 프리팹: " + result.created + "개\n"
                    + "기본 형태로 저장한 슬롯: " + result.createdFallback + "개\n"
                    + "이미 준비된 프리팹: " + result.skippedExisting + "개\n"
                    + "저장하지 못한 역할: " + result.missingSource + "개\n\n"
                    + "Assets/VARCOPresetKits 아래의 Parts 폴더에서 같은 파일명으로 프리팹을 교체하면 해당 프리셋 조립에 우선 적용됩니다.",
                "확인");
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/세부 자동 제작/프리셋 키트 실제 프리팹 채우기", priority = -77)]
        public static void FillAllPresetKitPrefabsDetailedFromMenu()
        {
            FillAllPresetKitPrefabsFromMenu();
        }

        static VWS.GenreType GenreForTemplate(BlockTemplate template)
        {
            switch (template)
            {
                case BlockTemplate.ArenaCombatWave:
                case BlockTemplate.SurvivalTimer:
                case BlockTemplate.ArenaBossBattle:
                    return VWS.GenreType.Arena;
                case BlockTemplate.PuzzleDoorRoom:
                case BlockTemplate.PuzzleEscapeRoom:
                    return VWS.GenreType.Puzzle;
                case BlockTemplate.PlatformSpaceCourse:
                case BlockTemplate.PlatformObstacleRun:
                    return VWS.GenreType.Platform;
                default:
                    return VWS.GenreType.Exploration;
            }
        }

        static int CreateAllPresetKitFolders(bool createPlaceholderPrefabs)
        {
            EnsureFolder(PresetKitRoot);
            var count = 0;
            foreach (var definition in PresetKitDefinitions())
            {
                CreatePresetKitFolder(definition.template, definition.genre, definition.roles, createPlaceholderPrefabs);
                count++;
            }

            AssetDatabase.SaveAssets();
            return count;
        }

        static PresetKitFillResult FillAllPresetKitsFromCurrentMatches()
        {
            CreateAllPresetKitFolders(createPlaceholderPrefabs: true);

            var result = new PresetKitFillResult();
            var worker = CreateInstance<VARCOGameMakerWindow>();
            try
            {
                worker.EnableSafeAutomationDefaults();

                foreach (var definition in PresetKitDefinitions())
                {
                    var partsFolder = PresetKitPartsFolder(definition.genre, definition.template);
                    worker.genre = definition.genre;
                    worker.blockTemplate = definition.template;
                    worker.ApplyBlockTemplate(definition.template);
                    worker.NormalizeBlockPlan();
                    worker.ScanAssets();

                    foreach (var role in definition.roles.Distinct().OrderBy(PresetKitRoleOrder))
                    {
                        var targetPath = partsFolder + "/" + PresetKitSlotFileName(role);
                        if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPath))
                        {
                            result.skippedExisting++;
                            continue;
                        }

                        var source = worker.FindBestExternalCandidate(role, definition.genre);
                        if (source == null || !source.asset)
                        {
                            if (SavePresetKitRoleFallbackPrefab(role, targetPath))
                                result.createdFallback++;
                            else
                                result.missingSource++;
                            continue;
                        }

                        if (SavePresetKitRolePrefabFromCandidate(source, role, targetPath))
                            result.created++;
                        else
                            result.missingSource++;
                    }
                }
            }
            finally
            {
                DestroyImmediate(worker);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        struct PresetKitFillResult
        {
            public int created;
            public int createdFallback;
            public int skippedExisting;
            public int missingSource;
        }

        static void CreatePresetKitFolder(BlockTemplate template, VWS.GenreType targetGenre, AssetRole[] roles, bool createPlaceholderPrefabs)
        {
            var kitFolder = PresetKitFolder(targetGenre, template);
            var partsFolder = PresetKitPartsFolder(targetGenre, template);
            EnsureFolder(kitFolder);
            EnsureFolder(partsFolder);

            if (createPlaceholderPrefabs)
            {
                foreach (var role in roles.Distinct())
                    CreatePresetKitSlotPrefab(partsFolder, role);
            }

            WritePresetKitReadme(kitFolder, template, targetGenre, roles);
        }

        static IEnumerable<PresetKitDefinition> PresetKitDefinitions()
        {
            yield return new PresetKitDefinition(BlockTemplate.ArenaCombatWave, VWS.GenreType.Arena, new[] { AssetRole.Player, AssetRole.Weapon, AssetRole.Enemy, AssetRole.HealthPickup, AssetRole.ArenaCover });
            yield return new PresetKitDefinition(BlockTemplate.ExplorationZombieQuest, VWS.GenreType.Exploration, new[] { AssetRole.Player, AssetRole.Weapon, AssetRole.Enemy, AssetRole.ItemPickup, AssetRole.Goal, AssetRole.HealthPickup, AssetRole.HazardZone, AssetRole.Checkpoint, AssetRole.ArenaCover });
            yield return new PresetKitDefinition(BlockTemplate.PuzzleDoorRoom, VWS.GenreType.Puzzle, new[] { AssetRole.Player, AssetRole.Door, AssetRole.PressurePlate, AssetRole.MovableBox, AssetRole.Goal, AssetRole.Checkpoint, AssetRole.ArenaCover });
            yield return new PresetKitDefinition(BlockTemplate.PlatformSpaceCourse, VWS.GenreType.Platform, new[] { AssetRole.Player, AssetRole.ItemPickup, AssetRole.Goal, AssetRole.MovingPlatform, AssetRole.HazardZone, AssetRole.Checkpoint, AssetRole.ArenaCover });
            yield return new PresetKitDefinition(BlockTemplate.CollectAndEscape, VWS.GenreType.Exploration, new[] { AssetRole.Player, AssetRole.ItemPickup, AssetRole.Goal, AssetRole.Enemy, AssetRole.HealthPickup, AssetRole.Checkpoint, AssetRole.ArenaCover });
            yield return new PresetKitDefinition(BlockTemplate.SurvivalTimer, VWS.GenreType.Arena, new[] { AssetRole.Player, AssetRole.Weapon, AssetRole.Enemy, AssetRole.HealthPickup, AssetRole.ArenaCover });
            yield return new PresetKitDefinition(BlockTemplate.ArenaBossBattle, VWS.GenreType.Arena, new[] { AssetRole.Player, AssetRole.Weapon, AssetRole.Enemy, AssetRole.HealthPickup, AssetRole.ArenaCover });
            yield return new PresetKitDefinition(BlockTemplate.ExplorationZombieSurvival, VWS.GenreType.Exploration, new[] { AssetRole.Player, AssetRole.Weapon, AssetRole.Enemy, AssetRole.HealthPickup, AssetRole.HazardZone, AssetRole.Checkpoint, AssetRole.ArenaCover });
            yield return new PresetKitDefinition(BlockTemplate.ExplorationTreasureHunt, VWS.GenreType.Exploration, new[] { AssetRole.Player, AssetRole.ItemPickup, AssetRole.Goal, AssetRole.HealthPickup, AssetRole.Checkpoint, AssetRole.ArenaCover });
            yield return new PresetKitDefinition(BlockTemplate.PuzzleEscapeRoom, VWS.GenreType.Puzzle, new[] { AssetRole.Player, AssetRole.ItemPickup, AssetRole.Goal, AssetRole.Door, AssetRole.PressurePlate, AssetRole.MovableBox, AssetRole.Checkpoint, AssetRole.ArenaCover });
            yield return new PresetKitDefinition(BlockTemplate.PlatformObstacleRun, VWS.GenreType.Platform, new[] { AssetRole.Player, AssetRole.ItemPickup, AssetRole.Goal, AssetRole.MovingPlatform, AssetRole.HazardZone, AssetRole.Checkpoint, AssetRole.ArenaCover });
            yield return new PresetKitDefinition(BlockTemplate.FullFeatureSandbox, VWS.GenreType.Exploration, new[] { AssetRole.Player, AssetRole.Weapon, AssetRole.Enemy, AssetRole.ItemPickup, AssetRole.Goal, AssetRole.HealthPickup, AssetRole.Door, AssetRole.PressurePlate, AssetRole.HazardZone, AssetRole.MovingPlatform, AssetRole.MovableBox, AssetRole.Checkpoint, AssetRole.ArenaCover });
        }

        struct PresetKitDefinition
        {
            public readonly BlockTemplate template;
            public readonly VWS.GenreType genre;
            public readonly AssetRole[] roles;

            public PresetKitDefinition(BlockTemplate template, VWS.GenreType genre, AssetRole[] roles)
            {
                this.template = template;
                this.genre = genre;
                this.roles = roles;
            }
        }

        static string PresetKitFolder(VWS.GenreType targetGenre, BlockTemplate template)
        {
            return PresetKitRoot + "/" + PresetKitKey(targetGenre, template);
        }

        static string PresetKitPartsFolder(VWS.GenreType targetGenre, BlockTemplate template)
        {
            return PresetKitFolder(targetGenre, template) + "/Parts";
        }

        static string PresetKitKey(VWS.GenreType targetGenre, BlockTemplate template)
        {
            var templateKey = template == BlockTemplate.Custom ? targetGenre + "_Custom" : template.ToString();
            return SafeFileName(targetGenre + "_" + templateKey);
        }

        static bool IsPresetKitPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            return assetPath.Replace('\\', '/').StartsWith(PresetKitRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        static string PresetKitKeyFromPath(string assetPath)
        {
            if (!IsPresetKitPath(assetPath))
                return string.Empty;

            var normalized = assetPath.Replace('\\', '/');
            var rest = normalized.Substring((PresetKitRoot + "/").Length);
            var slash = rest.IndexOf('/');
            return slash >= 0 ? rest.Substring(0, slash) : rest;
        }

        static void CreatePresetKitSlotPrefab(string partsFolder, AssetRole role)
        {
            var realSlotPath = partsFolder + "/" + PresetKitSlotFileName(role);
            var placeholderPath = partsFolder + "/" + PresetKitPlaceholderFileName(role);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(realSlotPath) || AssetDatabase.LoadAssetAtPath<GameObject>(placeholderPath))
                return;

            var go = GameObject.CreatePrimitive(PresetKitPrimitive(role));
            go.name = Path.GetFileNameWithoutExtension(placeholderPath);
            go.transform.localScale = PresetKitDefaultScale(role);
            SetColor(go, PresetKitColor(role));
            PrefabUtility.SaveAsPrefabAsset(go, placeholderPath);
            Object.DestroyImmediate(go);
        }

        static bool SavePresetKitRolePrefabFromCandidate(AssetCandidate source, AssetRole role, string targetPath)
        {
            if (source == null || !source.asset || string.IsNullOrWhiteSpace(targetPath))
                return false;

            EnsureFolder(Path.GetDirectoryName(targetPath).Replace('\\', '/'));
            var instance = Object.Instantiate(source.asset);
            if (!instance)
                return false;

            try
            {
                instance.name = Path.GetFileNameWithoutExtension(targetPath);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;

                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                VARCOPrefabRepairUtility.RepairGameplayPrefab(
                    instance,
                    role.ToString(),
                    targetPath + " " + source.path,
                    null);

                PrefabUtility.SaveAsPrefabAsset(instance, targetPath);
                AssetDatabase.ImportAsset(targetPath);
                return AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        static bool SavePresetKitRoleFallbackPrefab(AssetRole role, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                return false;

            EnsureFolder(Path.GetDirectoryName(targetPath).Replace('\\', '/'));
            var go = GameObject.CreatePrimitive(PresetKitPrimitive(role));
            if (!go)
                return false;

            try
            {
                go.name = Path.GetFileNameWithoutExtension(targetPath);
                go.transform.localScale = PresetKitDefaultScale(role);
                SetColor(go, PresetKitColor(role));
                PrefabUtility.SaveAsPrefabAsset(go, targetPath);
                AssetDatabase.ImportAsset(targetPath);
                return AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static string PresetKitSlotFileName(AssetRole role)
        {
            return PresetKitRoleOrder(role).ToString("00") + "_" + role + ".prefab";
        }

        static string PresetKitPlaceholderFileName(AssetRole role)
        {
            return PresetKitRoleOrder(role).ToString("00") + "_" + role + "_SLOT_PLACEHOLDER.prefab";
        }

        static bool IsPresetKitPlaceholderPath(string path)
        {
            return Normalize(path).Contains("slot_placeholder");
        }

        static int PresetKitRoleOrder(AssetRole role)
        {
            switch (role)
            {
                case AssetRole.Player: return 1;
                case AssetRole.Enemy: return 2;
                case AssetRole.Weapon: return 3;
                case AssetRole.ItemPickup: return 4;
                case AssetRole.HealthPickup: return 5;
                case AssetRole.Goal: return 6;
                case AssetRole.Door: return 7;
                case AssetRole.PressurePlate: return 8;
                case AssetRole.HazardZone: return 9;
                case AssetRole.MovingPlatform: return 10;
                case AssetRole.MovableBox: return 11;
                case AssetRole.Checkpoint: return 12;
                case AssetRole.ArenaCover: return 13;
                default: return 99;
            }
        }

        static PrimitiveType PresetKitPrimitive(AssetRole role)
        {
            switch (role)
            {
                case AssetRole.Player:
                case AssetRole.Enemy:
                case AssetRole.Checkpoint:
                case AssetRole.HealthPickup:
                    return PrimitiveType.Capsule;
                case AssetRole.Weapon:
                case AssetRole.MovingPlatform:
                case AssetRole.Door:
                case AssetRole.PressurePlate:
                case AssetRole.HazardZone:
                case AssetRole.MovableBox:
                case AssetRole.ArenaCover:
                    return PrimitiveType.Cube;
                default:
                    return PrimitiveType.Sphere;
            }
        }

        static Vector3 PresetKitDefaultScale(AssetRole role)
        {
            switch (role)
            {
                case AssetRole.Player: return new Vector3(0.9f, 1.8f, 0.9f);
                case AssetRole.Enemy: return new Vector3(0.9f, 1.7f, 0.9f);
                case AssetRole.Weapon: return new Vector3(0.18f, 0.18f, 1.25f);
                case AssetRole.ItemPickup: return Vector3.one * 0.55f;
                case AssetRole.HealthPickup: return new Vector3(0.7f, 0.7f, 0.7f);
                case AssetRole.Goal: return new Vector3(1.4f, 1.4f, 1.4f);
                case AssetRole.Door: return new Vector3(2.6f, 3.0f, 0.35f);
                case AssetRole.PressurePlate: return new Vector3(2.0f, 0.16f, 2.0f);
                case AssetRole.HazardZone: return new Vector3(3.0f, 0.18f, 3.0f);
                case AssetRole.MovingPlatform: return new Vector3(2.8f, 0.3f, 2.8f);
                case AssetRole.MovableBox: return new Vector3(1.2f, 1.2f, 1.2f);
                case AssetRole.Checkpoint: return new Vector3(1.1f, 1.1f, 1.1f);
                case AssetRole.ArenaCover: return new Vector3(2.6f, 1.6f, 1.0f);
                default: return Vector3.one;
            }
        }

        static Color PresetKitColor(AssetRole role)
        {
            switch (role)
            {
                case AssetRole.Player: return new Color(0.2f, 0.55f, 1f);
                case AssetRole.Enemy: return new Color(0.78f, 0.2f, 0.16f);
                case AssetRole.Weapon: return new Color(0.78f, 0.78f, 0.82f);
                case AssetRole.ItemPickup: return new Color(1f, 0.86f, 0.16f);
                case AssetRole.HealthPickup: return new Color(0.9f, 0.1f, 0.18f);
                case AssetRole.Goal: return new Color(0.55f, 0.95f, 1f);
                case AssetRole.Door: return new Color(0.44f, 0.27f, 0.15f);
                case AssetRole.PressurePlate: return new Color(0.95f, 0.78f, 0.18f);
                case AssetRole.HazardZone: return new Color(1f, 0.35f, 0.1f);
                case AssetRole.MovingPlatform: return new Color(0.15f, 0.48f, 0.9f);
                case AssetRole.MovableBox: return new Color(0.5f, 0.36f, 0.18f);
                case AssetRole.Checkpoint: return new Color(0.7f, 0.35f, 0.9f);
                case AssetRole.ArenaCover: return new Color(0.35f, 0.38f, 0.42f);
                default: return Color.white;
            }
        }

        static void WritePresetKitReadme(string kitFolder, BlockTemplate template, VWS.GenreType targetGenre, AssetRole[] roles)
        {
            var path = kitFolder + "/README_KO.md";
            var fullPath = Path.GetFullPath(path);
            var lines = new List<string>
            {
                "# VARCO 프리셋 키트",
                "",
                "- 프리셋: " + template,
                "- 장르: " + targetGenre,
                "- 사용 방법: `Parts` 폴더 안의 역할별 프리팹을 같은 역할 이름으로 교체한 뒤 해당 프리셋 자동 제작 메뉴를 실행합니다.",
                "- 제작기는 이 폴더의 실제 역할 프리팹을 먼저 사용하고, 비어 있는 역할만 기존 자동 에셋 인식으로 보완합니다.",
                "- `_SLOT_PLACEHOLDER`가 붙은 파일은 안내용이며 자동 제작 후보에서 제외됩니다.",
                "",
                "## 역할 슬롯"
            };

            foreach (var role in roles.Distinct().OrderBy(PresetKitRoleOrder))
                lines.Add("- 실제 교체 이름 `" + PresetKitSlotFileName(role) + "` / 안내 파일 `" + PresetKitPlaceholderFileName(role) + "`: " + role);

            File.WriteAllText(fullPath, string.Join(Environment.NewLine, lines), System.Text.Encoding.UTF8);
            AssetDatabase.ImportAsset(path);
        }

        void OnEnable()
        {
            if (!initializedBlockDefaults)
            {
                ApplyGenreDefaults();
                initializedBlockDefaults = true;
            }

            if (log.Count == 0)
                log.Add("준비됨. 장르를 고르고 자동 제작 버튼을 누르세요.");
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("VARCO 게임 메이커", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "VARCO 에셋을 자동 인식해서 블록 조합, 캐릭터, HUD, 비주얼, 사운드, 애니메이션을 연결하는 한글 게임 제작 도구입니다.",
                MessageType.Info);

            DrawBeginnerOneClickPanel();
            DrawMainControls();
            DrawBlockPlan();

            assetSummaryOpen = EditorGUILayout.Foldout(assetSummaryOpen, "에셋 요약 / 자동 매칭", true);
            if (assetSummaryOpen)
                DrawAssetSummary();

            actionDiagnosticsOpen = EditorGUILayout.Foldout(actionDiagnosticsOpen, "제작 액션 / 진단", true);
            if (actionDiagnosticsOpen)
                DrawActions();

            DrawAdvanced();
            DrawLog();

            EditorGUILayout.EndScrollView();
        }

        void InvalidateEditorSummaryCache()
        {
            editorSummaryCacheExpiresAt = 0;
        }

        void EnsureEditorSummaryCache(bool force = false)
        {
            var now = EditorApplication.timeSinceStartup;
            if (!force && cachedOneClickRecommendation != null && now < editorSummaryCacheExpiresAt)
                return;

            cachedActiveRoles.Clear();
            cachedActiveRoles.AddRange(ActiveRolesForCurrentBlocks().Distinct());

            cachedActiveAssetSlots.Clear();
            foreach (var role in cachedActiveRoles)
                cachedActiveAssetSlots.Add(BuildAssetSlotStatus(role));

            cachedBlockAssemblyStatuses.Clear();
            cachedBlockAssemblyStatuses.AddRange(BuildBlockAssemblyStatuses());

            cachedAcceptanceFindings.Clear();
            cachedAcceptanceFindings.AddRange(BuildAcceptanceChecklist());

            cachedOneClickRecommendation = BuildOneClickRecommendation();

            cachedOneClickNextSteps.Clear();
            cachedOneClickNextSteps.AddRange(BuildOneClickNextSteps().Take(5));

            var roleCounts = candidates
                .Where(c => c.role != AssetRole.Unknown)
                .GroupBy(c => c.role)
                .OrderBy(g => g.Key.ToString())
                .Select(g => AssetRoleLabel(g.Key) + ": " + g.Count())
                .ToArray();
            cachedRoleCountsText = roleCounts.Length > 0 ? string.Join(" / ", roleCounts) : "아직 사용할 수 있는 에셋을 찾지 못했습니다.";
            cachedAssetSlotSummaryText = AssetSlotSummary();

            cachedTopDetectedAssets.Clear();
            cachedTopDetectedAssets.AddRange(candidates
                .Where(candidate => candidate.role != AssetRole.Unknown)
                .Take(6));

            cachedInternalMatches.Clear();
            cachedInternalMatches.AddRange(candidates
                .Where(candidate => candidate.usedInternalEvidence)
                .Take(5));

            cachedSoundSlotStatuses.Clear();
            if (blockSound)
            {
                var registry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(RegistryPath);
                cachedSoundSlotStatuses.AddRange(BuildSoundSlotStatuses(registry));
            }

            cachedAnimationSlotStatuses.Clear();
            if (blockPlayer && autoAnimations)
                cachedAnimationSlotStatuses.AddRange(BuildAnimationSlotStatuses());

            editorSummaryCacheExpiresAt = now + EditorSummaryCacheSeconds;
        }

        void DrawBeginnerOneClickPanel()
        {
            DrawHeader("처음 사용자용");
            EditorGUILayout.HelpBox(
                "프로그래밍이나 Unity 설정 없이 시작하려면 아래 버튼만 누르세요. 안전 자동화, 에셋 스캔, 프리셋 선택, 게임 오브젝트 생성, HUD/비주얼/사운드/애니메이션 연결을 한 번에 진행합니다.",
                MessageType.Info);

            DrawBeginnerReadinessSnapshot();

            GUI.backgroundColor = new Color(0.35f, 0.9f, 0.62f, 1f);
            if (GUILayout.Button("처음 사용자: 현재 에셋으로 게임 만들기", GUILayout.Height(42f)))
                RunBeginnerOneClick(buildWindows: false);
            GUI.backgroundColor = new Color(0.45f, 0.72f, 1f, 1f);
            if (GUILayout.Button("처음 사용자: 게임 만들고 Windows EXE까지", GUILayout.Height(36f)))
                RunBeginnerOneClick(buildWindows: true);
            GUI.backgroundColor = new Color(1f, 0.84f, 0.36f, 1f);
            if (GUILayout.Button("변경된 프리셋 재검증 + NavMesh 재베이크", GUILayout.Height(32f)))
                RevalidateChangedPresetScene();
            GUI.backgroundColor = Color.white;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("먼저 에셋 요청서 보기", GUILayout.Height(24f)))
                    GenerateAssetRequestSheet();
                if (GUILayout.Button("빌드 준비만 보기", GUILayout.Height(24f)))
                    GenerateBuildPreflightReport();
            }
        }

        void DrawBeginnerReadinessSnapshot()
        {
            if (cachedOneClickRecommendation == null || cachedAcceptanceFindings.Count == 0)
            {
                EditorGUILayout.HelpBox("상태 요약은 필요할 때만 새로고침합니다. 에디터 멈춤을 줄이기 위해 자동 전체 분석은 끕니다.", MessageType.None);
                if (GUILayout.Button("상태 요약 새로고침", GUILayout.Height(24f)))
                    EnsureEditorSummaryCache(true);
                return;
            }

            var recommendation = cachedOneClickRecommendation;
            var findings = cachedAcceptanceFindings;
            var roles = cachedActiveRoles;
            var slots = cachedActiveAssetSlots;
            var readyAssetCount = slots.Count(slot => slot.state == "PASS");
            var fallbackAssetCount = slots.Count(slot => slot.state == "FALLBACK");
            var failCount = findings.Count(finding => finding.state == "FAIL");
            var warnCount = findings.Count(finding => finding.state == "WARN");

            var statusText = "현재 상태: " + AcceptanceSummary(findings)
                + "\n추천 다음 행동: " + recommendation.title
                + "\n에셋 적용률: " + readyAssetCount + "/" + Mathf.Max(1, roles.Count) + "개 역할 매칭"
                + (fallbackAssetCount > 0 ? " / 기본 오브젝트 대체 " + fallbackAssetCount + "개" : "")
                + "\n선택된 게임: " + GenreLabel(genre) + " / " + BlockTemplateLabel() + " / " + GameRecipeLabel(recipe);
            EditorGUILayout.HelpBox(statusText, failCount > 0 ? MessageType.Warning : warnCount > 0 ? MessageType.Info : MessageType.None);

            var nextSteps = cachedOneClickNextSteps.Take(3).ToList();
            if (nextSteps.Count == 0)
                return;

            EditorGUILayout.LabelField("제작 전 확인", EditorStyles.miniBoldLabel);
            foreach (var step in nextSteps)
                EditorGUILayout.LabelField("- " + step, EditorStyles.wordWrappedMiniLabel);
        }

        void RunBeginnerOneClick(bool buildWindows)
        {
            EnableSafeAutomationDefaults();
            ScanAssets();

            if (buildWindows)
            {
                BuildBestPresetWindowsExe();
                return;
            }

            BuildBestPresetOneClick();
        }

        void BuildGenreOneClickInternal(VWS.GenreType targetGenre, BlockTemplate template, bool buildWindows)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("VARCO 게임 메이커", "자동 제작 전에 플레이 모드를 종료하세요.", "확인");
                return;
            }

            EnableSafeAutomationDefaults();
            ScanAssets();
            genre = targetGenre;
            ApplyBlockTemplate(template);
            NormalizeBlockPlan();
            ScanAssets();
            lastAutoDesignSummary = "장르별 자동 제작 시작: " + GenreLabel(genre) + " / " + BlockTemplateLabel();
            log.Add(lastAutoDesignSummary);

            BuildGameOneClick();
            if (buildWindows)
                BuildWindowsExe();
        }

        void DrawMainControls()
        {
            DrawHeader("1. 게임 선택");
            EditorGUI.BeginChangeCheck();
            genre = DrawKoreanEnumPopup("장르", genre, GenreLabel);
            if (EditorGUI.EndChangeCheck())
            {
                itemGoal = genre == VWS.GenreType.Exploration ? 3 : genre == VWS.GenreType.Platform ? 2 : 0;
                ApplyGenreDefaults();
                ScanAssets();
            }

            sceneMode = DrawKoreanEnumPopup("씬 대상", sceneMode, SceneModeLabel);
            blockTemplate = DrawKoreanEnumPopup("블록 템플릿", blockTemplate, BlockTemplateLabel);
            recipe = DrawKoreanEnumPopup("게임 방식", recipe, GameRecipeLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("템플릿 적용", GUILayout.Height(24f)))
                    ApplyBlockTemplate(blockTemplate);
                if (GUILayout.Button("방식 기본값 적용", GUILayout.Height(24f)))
                    ApplyRecipeDefaults();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("에셋 보고 자동 설계", GUILayout.Height(24f)))
                    AutoDesignFromAssets();
                if (GUILayout.Button("에셋에 맞는 템플릿 선택", GUILayout.Height(24f)))
                    ApplyBestTemplateFromAssets();
            }
            selectedPreset = (VARCOGameMakerPreset)EditorGUILayout.ObjectField("게임 프리셋", selectedPreset, typeof(VARCOGameMakerPreset), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("현재 설정 저장", GUILayout.Height(24f)))
                    SaveCurrentPreset();
                if (GUILayout.Button("프리셋 불러오기", GUILayout.Height(24f)))
                    LoadSelectedPreset();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("기본 프리셋 만들기", GUILayout.Height(24f)))
                    GenerateStarterPresets();
                if (GUILayout.Button("가장 준비된 프리셋 선택", GUILayout.Height(24f)))
                    SelectBestReadyPreset();
            }
            playerCharacter = DrawKoreanEnumPopup("플레이어 종류", playerCharacter, PlayerCharacterChoiceLabel);
            enemyCharacter = DrawKoreanEnumPopup("적 종류", enemyCharacter, EnemyCharacterChoiceLabel);
            difficulty = DrawKoreanEnumPopup("난이도", difficulty, DifficultyPresetLabel);
            cameraPreset = DrawKoreanEnumPopup("카메라", cameraPreset, CameraPresetChoiceLabel);
            playerMovement = DrawKoreanEnumPopup("이동 방식", playerMovement, PlayerMovementChoiceLabel);
            itemGoal = EditorGUILayout.IntSlider("수집 목표 개수", itemGoal, 0, 12);
            waveEnemyCount = EditorGUILayout.IntSlider("적 수", waveEnemyCount, 1, 12);
        }

        void DrawBlockPlan()
        {
            DrawHeader("2. 블록 조합");
            EditorGUILayout.HelpBox(
                "여기서 고른 블록이 게임 규칙이 됩니다. 자동 제작은 부족한 오브젝트를 만들고, VARCO 에셋을 적용하고, 컴포넌트를 연결합니다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("핵심 게임"))
                {
                    SelectCoreBlocks();
                    blockTemplate = BlockTemplate.Custom;
                }
                if (GUILayout.Button("모든 블록"))
                {
                    SelectAllBlocks();
                    blockTemplate = BlockTemplate.Custom;
                }
                if (GUILayout.Button("선택 블록 끄기"))
                {
                    ClearOptionalBlocks();
                    blockTemplate = BlockTemplate.Custom;
                }
            }

            EditorGUI.BeginChangeCheck();
            blockPlayer = EditorGUILayout.ToggleLeft("블록: 플레이어 조작 + 카메라", blockPlayer);
            blockWeapon = EditorGUILayout.ToggleLeft("블록: 무기 장착 / 공격 비주얼", blockWeapon);
            blockEnemyWave = EditorGUILayout.ToggleLeft("블록: 적 웨이브 / 선택한 적 캐릭터", blockEnemyWave);
            blockItems = EditorGUILayout.ToggleLeft("블록: 수집 아이템", blockItems);
            blockGoal = EditorGUILayout.ToggleLeft("블록: 목표 지점 / 클리어 트리거", blockGoal);
            blockHealthPickup = EditorGUILayout.ToggleLeft("블록: 회복 아이템", blockHealthPickup);
            blockHazard = EditorGUILayout.ToggleLeft("블록: 위험 구역 / 데미지", blockHazard);
            blockCheckpoint = EditorGUILayout.ToggleLeft("블록: 체크포인트 / 리스폰", blockCheckpoint);
            blockFallRespawn = EditorGUILayout.ToggleLeft("블록: 낙사 리스폰 안전망", blockFallRespawn);
            blockMovingPlatform = EditorGUILayout.ToggleLeft("블록: 이동 발판", blockMovingPlatform);
            blockPuzzleDoor = EditorGUILayout.ToggleLeft("블록: 문 + 압력판", blockPuzzleDoor);
            blockMovableBox = EditorGUILayout.ToggleLeft("블록: 밀 수 있는 상자", blockMovableBox);
            blockCover = EditorGUILayout.ToggleLeft("블록: 환경 소품 / 엄폐물", blockCover);
            blockCountdown = EditorGUILayout.ToggleLeft("블록: 제한시간 타이머", blockCountdown);
            blockHud = EditorGUILayout.ToggleLeft("블록: 게임 HUD", blockHud);
            blockVisuals = EditorGUILayout.ToggleLeft("블록: 비주얼 프리셋", blockVisuals);
            blockSound = EditorGUILayout.ToggleLeft("블록: 사운드 / BGM 연결", blockSound);
            if (EditorGUI.EndChangeCheck())
            {
                blockTemplate = BlockTemplate.Custom;
                InvalidateEditorSummaryCache();
            }

            DrawBlockAssemblyBoard();
            EditorGUILayout.HelpBox(BuildNoCodeRecipePreview(), MessageType.Info);
            if (GUILayout.Button("노코드 레시피 카드 만들기", GUILayout.Height(26f)))
                GenerateNoCodeRecipeCard();
        }

        void DrawBlockAssemblyBoard()
        {
            var statuses = cachedBlockAssemblyStatuses.Count > 0
                ? cachedBlockAssemblyStatuses
                : BuildBlockAssemblyStatuses().ToList();
            if (statuses.Count == 0)
                return;

            var summary = BlockAssemblySummary(statuses);
            blockAssemblyOpen = EditorGUILayout.Foldout(blockAssemblyOpen, "블록 조립판 - " + summary, true);
            if (!blockAssemblyOpen)
                return;

            EditorGUILayout.HelpBox(
                "켜진 블록이 자동 제작 때 어떤 오브젝트, 에셋, HUD, 사운드, 애니메이션으로 조립되는지 보여줍니다.",
                BlockAssemblyMessageType(statuses));

            foreach (var status in statuses.Where(item => item.active || item.state != "PASS").Take(18))
            {
                EditorGUILayout.LabelField(
                    StateLabel(status.state) + ": [" + status.group + "] " + status.label,
                    status.message,
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        List<BlockAssemblyStatus> BuildBlockAssemblyStatuses()
        {
            var statuses = new List<BlockAssemblyStatus>();
            AddBlockAssemblyStatus(statuses, "설계", "게임 방식", true,
                blockTemplate == BlockTemplate.Custom || recipe == GameRecipe.GenreDefault ? "WARN" : "PASS",
                BlockTemplateLabel() + " / " + GameRecipeLabel(recipe) + " / 클리어 조건 " + CompletionConditionLabel(PrimaryClearCondition()));

            AddBlockAssemblyStatus(statuses, "핵심", "플레이어 조작", blockPlayer, blockPlayer ? BuildAssetSlotStatus(AssetRole.Player).state : "FAIL",
                blockPlayer ? PlayerCharacterLabel() + " 캐릭터, " + PlayerMovementLabel() + ", " + CameraPresetLabel() + " 카메라 연결" : "자동 제작에는 플레이어 블록이 필요합니다.");
            AddAssetBackedAssemblyStatus(statuses, blockWeapon, "전투", "무기/공격", AssetRole.Weapon, "무기 모델을 플레이어 손과 공격 비주얼에 연결");
            AddAssetBackedAssemblyStatus(statuses, blockEnemyWave, "전투", "적 웨이브", AssetRole.Enemy, EnemyCharacterLabel() + " 적 " + EffectiveWaveEnemyCount() + "명과 웨이브 매니저 연결");
            AddAssetBackedAssemblyStatus(statuses, blockItems, "목표", "수집 아이템", AssetRole.ItemPickup, "수집 아이템 " + Mathf.Max(1, itemGoal) + "개 배치와 카운터 연결");
            AddAssetBackedAssemblyStatus(statuses, blockGoal, "목표", "클리어 지점", AssetRole.Goal, "목표 트리거와 클리어 조건 연결");
            AddAssetBackedAssemblyStatus(statuses, blockHealthPickup, "지원", "회복 아이템", AssetRole.HealthPickup, DifficultyLabel() + " 난이도 회복량 " + HealAmountForDifficulty() + " 적용");
            AddAssetBackedAssemblyStatus(statuses, blockHazard, "장애물", "위험 구역", AssetRole.HazardZone, "초당 피해 " + HazardDpsForDifficulty() + " 위험 구역 배치");
            AddAssetBackedAssemblyStatus(statuses, blockCheckpoint, "진행", "체크포인트", AssetRole.Checkpoint, "통과 시 리스폰 위치 저장");
            AddBlockAssemblyStatus(statuses, "진행", "낙사 리스폰 안전망", blockFallRespawn, createMissingObjects ? "PASS" : "WARN",
                createMissingObjects ? "맵 아래에 보이지 않는 낙사 감지 구역을 만들고 체크포인트/시작 위치로 되돌립니다." : "부족한 오브젝트 자동 생성이 꺼져 있어 안전망을 만들 수 없습니다.");
            AddAssetBackedAssemblyStatus(statuses, blockMovingPlatform, "장애물", "이동 발판", AssetRole.MovingPlatform, "속도 " + MovingPlatformSpeedForDifficulty().ToString("0.0") + " 이동 발판 생성");
            AddPuzzleDoorAssemblyStatus(statuses);
            AddAssetBackedAssemblyStatus(statuses, blockMovableBox, "퍼즐", "밀 수 있는 상자", AssetRole.MovableBox, "퍼즐 상호작용용 상자 배치");
            AddAssetBackedAssemblyStatus(statuses, blockCover, "공간", "환경 소품/엄폐물", AssetRole.ArenaCover, "장르 공간용 소품과 엄폐물 배치");
            AddBlockAssemblyStatus(statuses, "규칙", "제한시간", blockCountdown, blockCountdown ? "PASS" : "PASS",
                blockCountdown ? EffectiveCountdownSeconds().ToString("0") + "초 타이머와 실패 조건 연결" : "타이머 블록 꺼짐");
            AddBlockAssemblyStatus(statuses, "화면", "게임 HUD", blockHud, addModernHud ? "PASS" : "WARN",
                addModernHud ? "HP, 목표, 아이템, 타이머 한글 HUD 생성" : "HUD 자동 생성이 꺼져 있습니다.");
            AddBlockAssemblyStatus(statuses, "연출", "비주얼 프리셋", blockVisuals, applyVisualPreset ? "PASS" : "WARN",
                applyVisualPreset ? GenreLabel(genre) + " 분위기의 조명/볼륨/반사 설정 적용" : "비주얼 자동 적용이 꺼져 있습니다.");
            AddSoundAssemblyStatus(statuses);
            AddAnimationAssemblyStatus(statuses);

            AddBlockAssemblyStatus(statuses, "자동 제작", "부족한 오브젝트 생성", true, createMissingObjects ? "PASS" : "WARN",
                createMissingObjects ? "에셋이 없어도 기본 오브젝트를 만들어 게임을 완성합니다." : "부족한 오브젝트 자동 생성이 꺼져 있습니다.");
            AddBlockAssemblyStatus(statuses, "자동 제작", "VARCO 에셋 자동 적용", true, autoConnectPrefabs ? "PASS" : "WARN",
                autoConnectPrefabs ? "감지된 프리팹/모델을 역할 블록에 자동 적용합니다." : "에셋 자동 적용이 꺼져 있습니다.");
            AddBlockAssemblyStatus(statuses, "자동 제작", "안전 보정/저장", true, runSafetyPass && saveScene ? "PASS" : "WARN",
                "안전 보정 " + BoolLabel(runSafetyPass) + " / 씬 저장 " + BoolLabel(saveScene) + " / 빌드 설정 추가 " + BoolLabel(addSceneToBuild));

            return statuses;
        }

        void AddAssetBackedAssemblyStatus(List<BlockAssemblyStatus> statuses, bool active, string group, string label, AssetRole role, string action)
        {
            if (!active)
                return;

            var slot = BuildAssetSlotStatus(role);
            AddBlockAssemblyStatus(statuses, group, label, true, slot.state, action + " / 에셋: " + slot.message);
        }

        void AddPuzzleDoorAssemblyStatus(List<BlockAssemblyStatus> statuses)
        {
            if (!blockPuzzleDoor)
                return;

            var door = BuildAssetSlotStatus(AssetRole.Door);
            var plate = BuildAssetSlotStatus(AssetRole.PressurePlate);
            AddBlockAssemblyStatus(statuses, "퍼즐", "문 + 압력판", true, WorstState(door.state, plate.state),
                "문과 압력판을 만들고 트리거로 연결 / 문: " + door.message + " / 압력판: " + plate.message);
        }

        void AddSoundAssemblyStatus(List<BlockAssemblyStatus> statuses)
        {
            if (!blockSound)
                return;

            if (!autoSounds)
            {
                AddBlockAssemblyStatus(statuses, "소리", "사운드/BGM", true, "WARN", "사운드 자동 연결이 꺼져 있습니다.");
                return;
            }

            var registry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(RegistryPath);
            var soundSlots = BuildSoundSlotStatuses(registry).ToList();
            var ready = soundSlots.Count(slot => slot.state == "PASS");
            AddBlockAssemblyStatus(statuses, "소리", "사운드/BGM", true, ready == soundSlots.Count ? "PASS" : "WARN",
                "사운드 이벤트 슬롯 " + ready + "/" + soundSlots.Count + " 준비");
        }

        void AddAnimationAssemblyStatus(List<BlockAssemblyStatus> statuses)
        {
            if (!blockPlayer)
                return;

            if (!autoAnimations)
            {
                AddBlockAssemblyStatus(statuses, "애니메이션", "애니메이션 자동 연결", true, "WARN", "애니메이션 컨트롤러 자동 생성/연결이 꺼져 있습니다.");
                return;
            }

            var animationSlots = BuildAnimationSlotStatuses().ToList();
            var required = animationSlots.Count(slot => slot.definition.important);
            var readyRequired = animationSlots.Count(slot => slot.definition.important && slot.state == "PASS");
            AddBlockAssemblyStatus(statuses, "애니메이션", "애니메이션 자동 연결", true, readyRequired >= required ? "PASS" : "WARN",
                "필수 애니메이션 슬롯 " + readyRequired + "/" + required + " 준비");
        }

        static void AddBlockAssemblyStatus(List<BlockAssemblyStatus> statuses, string group, string label, bool active, string state, string message)
        {
            statuses.Add(new BlockAssemblyStatus
            {
                group = group,
                label = label,
                active = active,
                state = active ? state : "PASS",
                message = message
            });
        }

        static string BlockAssemblySummary(List<BlockAssemblyStatus> statuses)
        {
            var visible = statuses.Where(item => item.active || item.state != "PASS").ToList();
            var pass = visible.Count(item => item.state == "PASS");
            var warn = visible.Count(item => item.state == "WARN");
            var fail = visible.Count(item => item.state == "FAIL");
            var fallback = visible.Count(item => item.state == "FALLBACK");
            return "통과 " + pass + " / 확인 " + warn + " / 기본 생성 " + fallback + " / 실패 " + fail;
        }

        static MessageType BlockAssemblyMessageType(List<BlockAssemblyStatus> statuses)
        {
            if (statuses.Any(item => item.active && item.state == "FAIL"))
                return MessageType.Error;
            return statuses.Any(item => item.active && (item.state == "WARN" || item.state == "FALLBACK")) ? MessageType.Warning : MessageType.Info;
        }

        static string WorstState(params string[] states)
        {
            if (states.Any(state => state == "FAIL")) return "FAIL";
            if (states.Any(state => state == "WARN")) return "WARN";
            if (states.Any(state => state == "FALLBACK")) return "FALLBACK";
            return "PASS";
        }

        void ApplyGenreDefaults()
        {
            recipe = GameRecipe.GenreDefault;
            ApplyRecipeDefaults();
        }

        void ApplyRecipeDefaults()
        {
            SelectCoreBlocks();

            var selectedRecipe = recipe;
            if (selectedRecipe == GameRecipe.GenreDefault)
            {
                switch (genre)
                {
                    case VWS.GenreType.Arena:
                        selectedRecipe = GameRecipe.CombatWave;
                        break;
                    case VWS.GenreType.Exploration:
                        selectedRecipe = GameRecipe.ExplorationQuest;
                        break;
                    case VWS.GenreType.Puzzle:
                        selectedRecipe = GameRecipe.DoorPuzzle;
                        break;
                    default:
                        selectedRecipe = GameRecipe.PlatformCourse;
                        break;
                }
            }

            ClearOptionalBlocks();
            blockPlayer = true;
            blockWeapon = false;
            blockGoal = selectedRecipe != GameRecipe.CombatWave || genre != VWS.GenreType.Arena;
            blockHud = true;
            blockVisuals = true;
            blockSound = true;

            switch (selectedRecipe)
            {
                case GameRecipe.CombatWave:
                    blockWeapon = true;
                    blockEnemyWave = true;
                    blockHealthPickup = true;
                    blockCover = true;
                    blockCountdown = true;
                    blockGoal = false;
                    itemGoal = 0;
                    break;
                case GameRecipe.ExplorationQuest:
                    blockWeapon = true;
                    blockEnemyWave = true;
                    blockItems = true;
                    blockGoal = true;
                    blockHealthPickup = true;
                    blockHazard = true;
                    blockCheckpoint = true;
                    blockFallRespawn = true;
                    blockCover = true;
                    itemGoal = Mathf.Max(itemGoal, 3);
                    break;
                case GameRecipe.DoorPuzzle:
                    blockPuzzleDoor = true;
                    blockMovableBox = true;
                    blockGoal = true;
                    blockItems = false;
                    blockCover = true;
                    itemGoal = 0;
                    break;
                case GameRecipe.PlatformCourse:
                    blockItems = true;
                    blockGoal = true;
                    blockHealthPickup = true;
                    blockHazard = true;
                    blockCheckpoint = true;
                    blockFallRespawn = true;
                    blockMovingPlatform = true;
                    blockCover = true;
                    itemGoal = Mathf.Max(itemGoal, 2);
                    break;
                case GameRecipe.CollectAndEscape:
                    blockItems = true;
                    blockGoal = true;
                    blockHazard = true;
                    blockCheckpoint = true;
                    blockFallRespawn = true;
                    blockCover = true;
                    itemGoal = Mathf.Max(itemGoal, 5);
                    break;
                case GameRecipe.SurvivalTimer:
                    blockWeapon = true;
                    blockEnemyWave = true;
                    blockHealthPickup = true;
                    blockHazard = true;
                    blockCover = true;
                    blockCountdown = true;
                    blockGoal = false;
                    itemGoal = 0;
                    break;
                case GameRecipe.BossBattle:
                    blockWeapon = true;
                    blockEnemyWave = true;
                    blockHealthPickup = true;
                    blockHazard = true;
                    blockCover = true;
                    blockCountdown = true;
                    blockGoal = false;
                    itemGoal = 0;
                    waveEnemyCount = 1;
                    break;
                case GameRecipe.ZombieSurvival:
                    blockWeapon = true;
                    blockEnemyWave = true;
                    blockHealthPickup = true;
                    blockHazard = true;
                    blockCheckpoint = true;
                    blockFallRespawn = true;
                    blockCover = true;
                    blockCountdown = true;
                    blockGoal = false;
                    itemGoal = 0;
                    waveEnemyCount = Mathf.Max(waveEnemyCount, 5);
                    break;
                case GameRecipe.TreasureHunt:
                    blockItems = true;
                    blockGoal = true;
                    blockHealthPickup = true;
                    blockHazard = true;
                    blockCheckpoint = true;
                    blockFallRespawn = true;
                    blockCover = true;
                    itemGoal = Mathf.Max(itemGoal, 6);
                    break;
                case GameRecipe.EscapeRoom:
                    blockPuzzleDoor = true;
                    blockMovableBox = true;
                    blockItems = true;
                    blockGoal = true;
                    blockCountdown = true;
                    blockCover = true;
                    itemGoal = Mathf.Max(itemGoal, 2);
                    break;
                case GameRecipe.ObstacleRun:
                    blockItems = true;
                    blockGoal = true;
                    blockHealthPickup = true;
                    blockHazard = true;
                    blockCheckpoint = true;
                    blockFallRespawn = true;
                    blockMovingPlatform = true;
                    blockCountdown = true;
                    blockCover = true;
                    itemGoal = Mathf.Max(itemGoal, 4);
                    break;
            }
        }

        void ApplyReferenceTuningForCurrentRecipe()
        {
            switch (recipe)
            {
                case GameRecipe.CombatWave:
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    waveEnemyCount = Mathf.Clamp(waveEnemyCount, 3, 4);
                    countdownSeconds = Mathf.Clamp(countdownSeconds, 90f, 150f);
                    break;
                case GameRecipe.ExplorationQuest:
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = blockItems ? Mathf.Clamp(itemGoal, 5, 6) : 0;
                    waveEnemyCount = blockEnemyWave ? Mathf.Clamp(waveEnemyCount, 2, 3) : 1;
                    countdownSeconds = Mathf.Clamp(countdownSeconds, 210f, 280f);
                    break;
                case GameRecipe.DoorPuzzle:
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = 0;
                    waveEnemyCount = 1;
                    countdownSeconds = Mathf.Clamp(countdownSeconds, 90f, 140f);
                    break;
                case GameRecipe.PlatformCourse:
                    cameraPreset = CameraPresetChoice.ThirdPerson;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = blockItems ? Mathf.Clamp(itemGoal, 2, 3) : 0;
                    waveEnemyCount = 1;
                    countdownSeconds = Mathf.Clamp(countdownSeconds, 120f, 180f);
                    break;
                case GameRecipe.CollectAndEscape:
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    difficulty = DifficultyPreset.Story;
                    itemGoal = blockItems ? Mathf.Clamp(itemGoal, 5, 6) : 0;
                    waveEnemyCount = 1;
                    countdownSeconds = Mathf.Clamp(countdownSeconds, 150f, 210f);
                    break;
                case GameRecipe.SurvivalTimer:
                    cameraPreset = CameraPresetChoice.TopDown;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    difficulty = DifficultyPreset.Hard;
                    itemGoal = 0;
                    waveEnemyCount = blockEnemyWave ? Mathf.Clamp(waveEnemyCount, 5, 7) : 1;
                    countdownSeconds = Mathf.Clamp(countdownSeconds, 90f, 150f);
                    break;
                case GameRecipe.BossBattle:
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    difficulty = DifficultyPreset.Hard;
                    itemGoal = 0;
                    waveEnemyCount = 1;
                    countdownSeconds = Mathf.Clamp(countdownSeconds, 150f, 220f);
                    break;
                case GameRecipe.ZombieSurvival:
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    difficulty = DifficultyPreset.Hard;
                    itemGoal = 0;
                    waveEnemyCount = blockEnemyWave ? Mathf.Clamp(waveEnemyCount, 3, 4) : 1;
                    countdownSeconds = Mathf.Clamp(countdownSeconds, 150f, 220f);
                    break;
                case GameRecipe.TreasureHunt:
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    difficulty = DifficultyPreset.Story;
                    itemGoal = blockItems ? Mathf.Clamp(itemGoal, 6, 6) : 0;
                    waveEnemyCount = 1;
                    countdownSeconds = Mathf.Clamp(countdownSeconds, 180f, 240f);
                    break;
                case GameRecipe.EscapeRoom:
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = blockItems ? Mathf.Clamp(itemGoal, 2, 2) : 0;
                    waveEnemyCount = 1;
                    countdownSeconds = Mathf.Clamp(countdownSeconds, 160f, 220f);
                    break;
                case GameRecipe.ObstacleRun:
                    cameraPreset = CameraPresetChoice.ThirdPerson;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = blockItems ? Mathf.Clamp(itemGoal, 4, 4) : 0;
                    waveEnemyCount = 1;
                    countdownSeconds = Mathf.Clamp(countdownSeconds, 130f, 190f);
                    break;
            }

            if (blockTemplate == BlockTemplate.FullFeatureSandbox)
            {
                cameraPreset = CameraPresetChoice.QuarterView;
                playerMovement = PlayerMovementChoice.CameraRelative;
                difficulty = DifficultyPreset.Normal;
                itemGoal = blockItems ? Mathf.Clamp(itemGoal, 5, 5) : 0;
                waveEnemyCount = blockEnemyWave ? Mathf.Clamp(waveEnemyCount, 3, 4) : 1;
                countdownSeconds = Mathf.Clamp(countdownSeconds, 180f, 240f);
            }
        }

        void ApplyBestTemplateFromAssets()
        {
            ScanAssets();
            var detectedGenre = GuessBestGenreFromAssets();
            var detectedRecipe = GuessRecipeFromAssets(detectedGenre);
            blockTemplate = TemplateFor(detectedGenre, detectedRecipe);
            ApplyBlockTemplate(blockTemplate);
            lastAutoDesignSummary = "에셋 기준 최적 블록 템플릿: " + BlockTemplateLabel()
                + " (" + GenreLabel(detectedGenre) + " / " + GameRecipeLabel(detectedRecipe) + ")";
            log.Add(lastAutoDesignSummary);
        }

        void ApplyBlockTemplate(BlockTemplate template)
        {
            blockTemplate = template;
            if (template == BlockTemplate.Custom)
            {
                ApplyRecipeDefaults();
                NormalizeBlockPlan();
                log.Add("직접 고른 블록을 유지하고, 게임 방식 기본값을 적용했습니다.");
                return;
            }

            switch (template)
            {
                case BlockTemplate.ArenaCombatWave:
                    genre = VWS.GenreType.Arena;
                    recipe = GameRecipe.CombatWave;
                    playerCharacter = PlayerCharacterChoice.Arena;
                    enemyCharacter = EnemyCharacterChoice.Orc;
                    difficulty = DifficultyPreset.Normal;
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = 0;
                    waveEnemyCount = 3;
                    countdownSeconds = 90f;
                    break;
                case BlockTemplate.ExplorationZombieQuest:
                    genre = VWS.GenreType.Exploration;
                    recipe = GameRecipe.ExplorationQuest;
                    playerCharacter = PlayerCharacterChoice.Exploration;
                    enemyCharacter = EnemyCharacterChoice.Zombie;
                    difficulty = DifficultyPreset.Normal;
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = RecommendedItemGoal(5);
                    waveEnemyCount = RecommendedEnemyCount(3);
                    countdownSeconds = 240f;
                    break;
                case BlockTemplate.PuzzleDoorRoom:
                    genre = VWS.GenreType.Puzzle;
                    recipe = GameRecipe.DoorPuzzle;
                    playerCharacter = PlayerCharacterChoice.Puzzle;
                    enemyCharacter = EnemyCharacterChoice.Any;
                    difficulty = DifficultyPreset.Story;
                    cameraPreset = CameraPresetChoice.ThirdPerson;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = 0;
                    waveEnemyCount = 1;
                    countdownSeconds = 90f;
                    break;
                case BlockTemplate.PlatformSpaceCourse:
                    genre = VWS.GenreType.Platform;
                    recipe = GameRecipe.PlatformCourse;
                    playerCharacter = PlayerCharacterChoice.Platform;
                    enemyCharacter = EnemyCharacterChoice.Drone;
                    difficulty = DifficultyPreset.Normal;
                    cameraPreset = CameraPresetChoice.ThirdPerson;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = Mathf.Min(RecommendedItemGoal(3), 4);
                    waveEnemyCount = 1;
                    countdownSeconds = 150f;
                    break;
                case BlockTemplate.CollectAndEscape:
                    genre = VWS.GenreType.Exploration;
                    recipe = GameRecipe.CollectAndEscape;
                    playerCharacter = PlayerCharacterChoice.Exploration;
                    enemyCharacter = EnemyCharacterChoice.Any;
                    difficulty = DifficultyPreset.Story;
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = RecommendedItemGoal(5);
                    waveEnemyCount = 1;
                    countdownSeconds = 150f;
                    break;
                case BlockTemplate.SurvivalTimer:
                    genre = VWS.GenreType.Arena;
                    recipe = GameRecipe.SurvivalTimer;
                    playerCharacter = PlayerCharacterChoice.Arena;
                    enemyCharacter = EnemyCharacterChoice.Orc;
                    difficulty = DifficultyPreset.Hard;
                    cameraPreset = CameraPresetChoice.TopDown;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = 0;
                    waveEnemyCount = RecommendedEnemyCount(5);
                    countdownSeconds = 120f;
                    break;
                case BlockTemplate.ArenaBossBattle:
                    genre = VWS.GenreType.Arena;
                    recipe = GameRecipe.BossBattle;
                    playerCharacter = PlayerCharacterChoice.Arena;
                    enemyCharacter = EnemyCharacterChoice.Boss;
                    difficulty = DifficultyPreset.Hard;
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = 0;
                    waveEnemyCount = 1;
                    countdownSeconds = 180f;
                    break;
                case BlockTemplate.ExplorationZombieSurvival:
                    genre = VWS.GenreType.Exploration;
                    recipe = GameRecipe.ZombieSurvival;
                    playerCharacter = PlayerCharacterChoice.Exploration;
                    enemyCharacter = EnemyCharacterChoice.Zombie;
                    difficulty = DifficultyPreset.Hard;
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = 0;
                    waveEnemyCount = RecommendedEnemyCount(6);
                    countdownSeconds = 180f;
                    break;
                case BlockTemplate.ExplorationTreasureHunt:
                    genre = VWS.GenreType.Exploration;
                    recipe = GameRecipe.TreasureHunt;
                    playerCharacter = PlayerCharacterChoice.Exploration;
                    enemyCharacter = EnemyCharacterChoice.Any;
                    difficulty = DifficultyPreset.Story;
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = RecommendedItemGoal(6);
                    waveEnemyCount = 1;
                    countdownSeconds = 180f;
                    break;
                case BlockTemplate.PuzzleEscapeRoom:
                    genre = VWS.GenreType.Puzzle;
                    recipe = GameRecipe.EscapeRoom;
                    playerCharacter = PlayerCharacterChoice.Puzzle;
                    enemyCharacter = EnemyCharacterChoice.Any;
                    difficulty = DifficultyPreset.Normal;
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = RecommendedItemGoal(2);
                    waveEnemyCount = 1;
                    countdownSeconds = 180f;
                    break;
                case BlockTemplate.PlatformObstacleRun:
                    genre = VWS.GenreType.Platform;
                    recipe = GameRecipe.ObstacleRun;
                    playerCharacter = PlayerCharacterChoice.Platform;
                    enemyCharacter = EnemyCharacterChoice.Drone;
                    difficulty = DifficultyPreset.Hard;
                    cameraPreset = CameraPresetChoice.ThirdPerson;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = Mathf.Min(RecommendedItemGoal(4), 4);
                    waveEnemyCount = 1;
                    countdownSeconds = 150f;
                    break;
                case BlockTemplate.FullFeatureSandbox:
                    genre = VWS.GenreType.Exploration;
                    recipe = GameRecipe.ExplorationQuest;
                    playerCharacter = PlayerCharacterChoice.Exploration;
                    enemyCharacter = EnemyCharacterChoice.Auto;
                    difficulty = DifficultyPreset.Normal;
                    cameraPreset = CameraPresetChoice.QuarterView;
                    playerMovement = PlayerMovementChoice.CameraRelative;
                    itemGoal = RecommendedItemGoal(5);
                    waveEnemyCount = RecommendedEnemyCount(4);
                    countdownSeconds = 180f;
                    break;
            }

            ApplyRecipeDefaults();
            ApplyReferenceTuningForCurrentRecipe();
            if (template == BlockTemplate.FullFeatureSandbox)
            {
                SelectAllBlocks();
                ApplyReferenceTuningForCurrentRecipe();
            }

            NormalizeBlockPlan();
            lastAutoDesignSummary = "블록 템플릿 적용됨: " + BlockTemplateLabel();
            log.Add(lastAutoDesignSummary);
        }

        void AutoDesignFromAssets()
        {
            ScanAssets();
            if (candidates.Count == 0)
            {
                lastAutoDesignSummary = "VARCO 프리팹/모델을 찾지 못해 현재 선택을 유지했습니다.";
                log.Add(lastAutoDesignSummary);
                return;
            }

            var detectedGenre = GuessBestGenreFromAssets();
            var detectedRecipe = GuessRecipeFromAssets(detectedGenre);
            genre = detectedGenre;
            recipe = detectedRecipe;
            blockTemplate = TemplateFor(detectedGenre, detectedRecipe);
            playerCharacter = GuessPlayerChoiceFromAssets(detectedGenre);
            enemyCharacter = EnemyCharacterChoice.Auto;
            enemyCharacter = GuessEnemyChoiceFromAssets(detectedGenre);
            cameraPreset = CameraPresetChoice.Auto;
            playerMovement = PlayerMovementChoice.Auto;
            difficulty = DifficultyPreset.Normal;

            ApplyRecipeDefaults();
            TuneCountsFromAssets(detectedRecipe);
            ApplyReferenceTuningForCurrentRecipe();

            lastAutoDesignSummary = "에셋 기준 자동 설계: " + GenreLabel(genre)
                + " / " + GameRecipeLabel(recipe)
                + " / 템플릿=" + BlockTemplateLabel()
                + " / 플레이어=" + PlayerCharacterLabel()
                + " / 적=" + EnemyCharacterLabel()
                + " / 아이템=" + CountCandidates(AssetRole.ItemPickup)
                + " / 적 수=" + CountCandidates(AssetRole.Enemy)
                + " / 블록=" + ActiveBlockSummary();
            log.Add(lastAutoDesignSummary);
        }

        void SaveCurrentPreset()
        {
            EnsureFolder(PresetFolder);
            if (!selectedPreset)
            {
                var assetName = "VARCO_" + SafeFileName(genre.ToString()) + "_" + SafeFileName(recipe.ToString()) + "_Preset.asset";
                var path = AssetDatabase.GenerateUniqueAssetPath(PresetFolder + "/" + assetName);
                selectedPreset = CreateInstance<VARCOGameMakerPreset>();
                AssetDatabase.CreateAsset(selectedPreset, path);
            }

            Undo.RecordObject(selectedPreset, "VARCO 게임 프리셋 저장");
            WritePreset(selectedPreset);
            EditorUtility.SetDirty(selectedPreset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            log.Add("프리셋 저장됨: " + AssetDatabase.GetAssetPath(selectedPreset));
        }

        void LoadSelectedPreset()
        {
            if (!selectedPreset)
            {
                EditorUtility.DisplayDialog("VARCO 게임 메이커", "먼저 게임 프리셋을 선택하세요.", "확인");
                return;
            }

            ReadPreset(selectedPreset);
            NormalizeBlockPlan();
            ScanAssets();
            lastAutoDesignSummary = "프리셋 불러옴: " + AssetDatabase.GetAssetPath(selectedPreset);
            log.Add(lastAutoDesignSummary);
        }

        bool SelectBestReadyPreset()
        {
            var best = FindBestReadyPresetScore(createStarterPresets: true);
            if (best == null || !best.preset)
            {
                EditorUtility.DisplayDialog("VARCO 게임 메이커", "사용 가능한 게임 프리셋이 없습니다.", "확인");
                return false;
            }

            selectedPreset = best.preset;
            ReadPreset(best.preset);
            NormalizeBlockPlan();
            ScanAssets();
            lastAutoDesignSummary = "가장 준비된 프리셋 선택됨: " + best.preset.name + " / " + best.summary;
            log.Add(lastAutoDesignSummary);
            return true;
        }

        void BuildBestPresetOneClick()
        {
            if (!SelectBestReadyPreset())
                return;

            BuildGameOneClick();
        }

        void BuildBestPresetWindowsExe()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("VARCO 게임 메이커", "빌드하기 전에 플레이 모드를 종료하세요.", "확인");
                return;
            }

            if (!SelectBestReadyPreset())
                return;

            BuildGameOneClick();
            BuildWindowsExe();
        }

        void GenerateStarterPresets()
        {
            EnsureFolder(PresetFolder);
            ScanAssets();

            var snapshot = CreateInstance<VARCOGameMakerPreset>();
            WritePreset(snapshot);
            var originalPreset = selectedPreset;
            var originalDesignSummary = lastAutoDesignSummary;
            var generated = new List<VARCOGameMakerPreset>();

            try
            {
                generated.Add(GenerateStarterPreset("VARCO_Arena_CombatWave", VWS.GenreType.Arena, GameRecipe.CombatWave, PlayerCharacterChoice.Arena, EnemyCharacterChoice.Orc, DifficultyPreset.Normal, 0, 3, 90f));
                generated.Add(GenerateStarterPreset("VARCO_Exploration_ZombieQuest", VWS.GenreType.Exploration, GameRecipe.ExplorationQuest, PlayerCharacterChoice.Exploration, EnemyCharacterChoice.Zombie, DifficultyPreset.Normal, RecommendedItemGoal(3), RecommendedEnemyCount(3), 180f));
                generated.Add(GenerateStarterPreset("VARCO_Puzzle_DoorRoom", VWS.GenreType.Puzzle, GameRecipe.DoorPuzzle, PlayerCharacterChoice.Puzzle, EnemyCharacterChoice.Any, DifficultyPreset.Story, 0, 1, 90f));
                generated.Add(GenerateStarterPreset("VARCO_Platform_SpaceCourse", VWS.GenreType.Platform, GameRecipe.PlatformCourse, PlayerCharacterChoice.Platform, EnemyCharacterChoice.Drone, DifficultyPreset.Normal, RecommendedItemGoal(3), 1, 150f));
                generated.Add(GenerateStarterPreset("VARCO_Collect_And_Escape", VWS.GenreType.Exploration, GameRecipe.CollectAndEscape, PlayerCharacterChoice.Exploration, EnemyCharacterChoice.Any, DifficultyPreset.Story, RecommendedItemGoal(5), 1, 150f));
                generated.Add(GenerateStarterPreset("VARCO_Arena_SurvivalTimer", VWS.GenreType.Arena, GameRecipe.SurvivalTimer, PlayerCharacterChoice.Arena, EnemyCharacterChoice.Orc, DifficultyPreset.Hard, 0, RecommendedEnemyCount(5), 120f));
                generated.Add(GenerateStarterPreset("VARCO_Arena_BossBattle", VWS.GenreType.Arena, GameRecipe.BossBattle, PlayerCharacterChoice.Arena, EnemyCharacterChoice.Boss, DifficultyPreset.Hard, 0, 1, 180f));
                generated.Add(GenerateStarterPreset("VARCO_Exploration_ZombieSurvival", VWS.GenreType.Exploration, GameRecipe.ZombieSurvival, PlayerCharacterChoice.Exploration, EnemyCharacterChoice.Zombie, DifficultyPreset.Hard, 0, RecommendedEnemyCount(6), 180f));
                generated.Add(GenerateStarterPreset("VARCO_Exploration_TreasureHunt", VWS.GenreType.Exploration, GameRecipe.TreasureHunt, PlayerCharacterChoice.Exploration, EnemyCharacterChoice.Any, DifficultyPreset.Story, RecommendedItemGoal(6), 1, 180f));
                generated.Add(GenerateStarterPreset("VARCO_Puzzle_EscapeRoom", VWS.GenreType.Puzzle, GameRecipe.EscapeRoom, PlayerCharacterChoice.Puzzle, EnemyCharacterChoice.Any, DifficultyPreset.Normal, RecommendedItemGoal(2), 1, 180f));
                generated.Add(GenerateStarterPreset("VARCO_Platform_ObstacleRun", VWS.GenreType.Platform, GameRecipe.ObstacleRun, PlayerCharacterChoice.Platform, EnemyCharacterChoice.Drone, DifficultyPreset.Hard, RecommendedItemGoal(4), 1, 150f));
            }
            finally
            {
                ReadPreset(snapshot);
                DestroyImmediate(snapshot);
                lastAutoDesignSummary = originalDesignSummary;
                var firstGenerated = generated.FirstOrDefault(p => p);
                selectedPreset = firstGenerated ? firstGenerated : originalPreset;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            log.Add("기본 프리셋 " + generated.Count + "개 생성됨: " + PresetFolder);
        }

        VARCOGameMakerPreset GenerateStarterPreset(
            string assetName,
            VWS.GenreType presetGenre,
            GameRecipe presetRecipe,
            PlayerCharacterChoice presetPlayer,
            EnemyCharacterChoice presetEnemy,
            DifficultyPreset presetDifficulty,
            int presetItemGoal,
            int presetWaveEnemyCount,
            float presetCountdownSeconds)
        {
            var path = PresetFolder + "/" + assetName + ".asset";
            var preset = AssetDatabase.LoadAssetAtPath<VARCOGameMakerPreset>(path);
            if (!preset)
            {
                preset = CreateInstance<VARCOGameMakerPreset>();
                AssetDatabase.CreateAsset(preset, path);
            }

            genre = presetGenre;
            sceneMode = SceneMode.CurrentScene;
            recipe = presetRecipe;
            blockTemplate = TemplateFor(presetGenre, presetRecipe);
            playerCharacter = presetPlayer;
            enemyCharacter = presetEnemy;
            difficulty = presetDifficulty;
            cameraPreset = CameraPresetChoice.Auto;
            playerMovement = PlayerMovementChoice.Auto;
            ApplyRecipeDefaults();
            ApplyReferenceTuningForCurrentRecipe();

            itemGoal = blockItems ? Mathf.Clamp(presetItemGoal, 1, 12) : 0;
            waveEnemyCount = blockEnemyWave ? Mathf.Clamp(presetWaveEnemyCount, 1, 12) : 1;
            countdownSeconds = Mathf.Clamp(presetCountdownSeconds, 10f, 300f);
            ApplyReferenceTuningForCurrentRecipe();

            WritePreset(preset);
            EditorUtility.SetDirty(preset);
            return preset;
        }

        int RecommendedItemGoal(int fallback)
        {
            var detected = CountCandidates(AssetRole.ItemPickup);
            return Mathf.Clamp(detected > 0 ? detected : fallback, 1, 12);
        }

        int RecommendedEnemyCount(int fallback)
        {
            var detected = CountCandidates(AssetRole.Enemy);
            return Mathf.Clamp(detected > 0 ? detected : fallback, 1, 12);
        }

        PresetReadinessScore FindBestReadyPresetScore(bool createStarterPresets)
        {
            var snapshot = CreateInstance<VARCOGameMakerPreset>();
            WritePreset(snapshot);
            var originalPreset = selectedPreset;
            var originalDesignSummary = lastAutoDesignSummary;
            var originalLog = log.ToList();

            try
            {
                if (createStarterPresets)
                    GenerateStarterPresets();

                PresetReadinessScore best = null;
                foreach (var preset in FindGameMakerPresets())
                {
                    var score = EvaluatePresetReadiness(preset);
                    if (best == null || score.score > best.score)
                        best = score;
                }

                return best;
            }
            finally
            {
                ReadPreset(snapshot);
                DestroyImmediate(snapshot);
                selectedPreset = originalPreset;
                lastAutoDesignSummary = originalDesignSummary;
                log.Clear();
                log.AddRange(originalLog);
            }
        }

        PresetReadinessScore EvaluatePresetReadiness(VARCOGameMakerPreset preset)
        {
            selectedPreset = preset;
            ReadPreset(preset);
            NormalizeBlockPlan();
            ScanAssets();

            var activeRolesList = ActiveRolesForCurrentBlocks().Distinct().ToList();
            var matched = 0;
            var fallback = 0;
            var characterWarnings = 0;

            foreach (var role in activeRolesList)
            {
                var best = FindBest(role, genre);
                if (best != null)
                    matched++;
                else
                    fallback++;

                if (role == AssetRole.Player && best != null && !PlayerCandidateMatchesSelection(best))
                    characterWarnings++;
                if (role == AssetRole.Enemy && best != null)
                {
                    var preferred = PreferredCharacterKind(AssetRole.Enemy, genre);
                    if (preferred != CharacterKind.None && best.characterKind != preferred)
                        characterWarnings++;
                }
            }

            var activeCount = activeRolesList.Count;
            var scoreValue = matched * 100
                + activeCount * 8
                - fallback * 140
                - characterWarnings * 35;

            return new PresetReadinessScore
            {
                preset = preset,
                score = scoreValue,
                matchedRoles = matched,
                fallbackRoles = fallback,
                activeRoles = activeCount,
                characterWarnings = characterWarnings,
                summary = "score=" + scoreValue
                    + ", matched=" + matched + "/" + activeCount
                    + ", fallback=" + fallback
                    + ", characterWarnings=" + characterWarnings
            };
        }

        void WritePreset(VARCOGameMakerPreset preset)
        {
            preset.genre = genre;
            preset.sceneMode = sceneMode;
            preset.recipe = recipe;
            preset.blockTemplate = blockTemplate;
            preset.playerCharacter = playerCharacter;
            preset.enemyCharacter = enemyCharacter;
            preset.difficulty = difficulty;
            preset.cameraPreset = cameraPreset;
            preset.playerMovement = playerMovement;
            preset.itemGoal = itemGoal;
            preset.waveEnemyCount = waveEnemyCount;
            preset.countdownSeconds = countdownSeconds;
            preset.blockPlayer = blockPlayer;
            preset.blockWeapon = blockWeapon;
            preset.blockEnemyWave = blockEnemyWave;
            preset.blockItems = blockItems;
            preset.blockGoal = blockGoal;
            preset.blockHealthPickup = blockHealthPickup;
            preset.blockHazard = blockHazard;
            preset.blockCheckpoint = blockCheckpoint;
            preset.blockFallRespawn = blockFallRespawn;
            preset.blockMovingPlatform = blockMovingPlatform;
            preset.blockPuzzleDoor = blockPuzzleDoor;
            preset.blockMovableBox = blockMovableBox;
            preset.blockCover = blockCover;
            preset.blockCountdown = blockCountdown;
            preset.blockHud = blockHud;
            preset.blockVisuals = blockVisuals;
            preset.blockSound = blockSound;
            preset.createMissingObjects = createMissingObjects;
            preset.autoConnectPrefabs = autoConnectPrefabs;
            preset.autoAnimations = autoAnimations;
            preset.autoSounds = autoSounds;
            preset.addModernHud = addModernHud;
            preset.applyVisualPreset = applyVisualPreset;
            preset.runSafetyPass = runSafetyPass;
            preset.addSceneToBuild = addSceneToBuild;
            preset.saveScene = saveScene;
        }

        void ReadPreset(VARCOGameMakerPreset preset)
        {
            genre = preset.genre;
            sceneMode = preset.sceneMode;
            recipe = preset.recipe;
            blockTemplate = preset.blockTemplate;
            playerCharacter = preset.playerCharacter;
            enemyCharacter = preset.enemyCharacter;
            difficulty = preset.difficulty;
            cameraPreset = preset.cameraPreset;
            playerMovement = preset.playerMovement;
            itemGoal = preset.itemGoal;
            waveEnemyCount = preset.waveEnemyCount;
            countdownSeconds = preset.countdownSeconds;
            blockPlayer = preset.blockPlayer;
            blockWeapon = preset.blockWeapon;
            blockEnemyWave = preset.blockEnemyWave;
            blockItems = preset.blockItems;
            blockGoal = preset.blockGoal;
            blockHealthPickup = preset.blockHealthPickup;
            blockHazard = preset.blockHazard;
            blockCheckpoint = preset.blockCheckpoint;
            blockFallRespawn = preset.blockFallRespawn;
            blockMovingPlatform = preset.blockMovingPlatform;
            blockPuzzleDoor = preset.blockPuzzleDoor;
            blockMovableBox = preset.blockMovableBox;
            blockCover = preset.blockCover;
            blockCountdown = preset.blockCountdown;
            blockHud = preset.blockHud;
            blockVisuals = preset.blockVisuals;
            blockSound = preset.blockSound;
            createMissingObjects = preset.createMissingObjects;
            autoConnectPrefabs = preset.autoConnectPrefabs;
            autoAnimations = preset.autoAnimations;
            autoSounds = preset.autoSounds;
            addModernHud = preset.addModernHud;
            applyVisualPreset = preset.applyVisualPreset;
            runSafetyPass = preset.runSafetyPass;
            addSceneToBuild = preset.addSceneToBuild;
            saveScene = preset.saveScene;
        }

        VWS.GenreType GuessBestGenreFromAssets()
        {
            var scores = new Dictionary<VWS.GenreType, int>();
            foreach (VWS.GenreType value in Enum.GetValues(typeof(VWS.GenreType)))
                scores[value] = 0;

            foreach (var candidate in candidates)
            {
                if (!candidate.genre.HasValue)
                    continue;

                var weight = 1;
                if (candidate.role == AssetRole.Player || candidate.role == AssetRole.Enemy)
                    weight += 5;
                else if (candidate.role != AssetRole.Unknown)
                    weight += 2;
                if (candidate.isPrefab)
                    weight += 2;
                if (candidate.hasVisuals)
                    weight += 1;
                if (candidate.hasSkinnedMesh)
                    weight += 1;

                scores[candidate.genre.Value] += weight;
            }

            var best = scores.OrderByDescending(pair => pair.Value).First();
            return best.Value > 0 ? best.Key : genre;
        }

        GameRecipe GuessRecipeFromAssets(VWS.GenreType detectedGenre)
        {
            var enemyCount = CountCandidates(AssetRole.Enemy);
            var itemCount = CountCandidates(AssetRole.ItemPickup);
            var bossCount = candidates.Count(c => c.role == AssetRole.Enemy && c.characterKind == CharacterKind.Boss);
            var zombieCount = candidates.Count(c => c.role == AssetRole.Enemy && c.characterKind == CharacterKind.Zombie);
            var hazardCount = CountCandidates(AssetRole.HazardZone);
            var hasPuzzleSet = CountCandidates(AssetRole.Door) > 0
                || CountCandidates(AssetRole.PressurePlate) > 0
                || CountCandidates(AssetRole.MovableBox) > 0;
            var hasPlatformSet = CountCandidates(AssetRole.MovingPlatform) > 0;
            var hasExplorationSet = CountCandidates(AssetRole.Checkpoint) > 0
                || hazardCount > 0;

            if (detectedGenre == VWS.GenreType.Puzzle || hasPuzzleSet)
                return itemCount > 0 || CountCandidates(AssetRole.MovableBox) > 0 ? GameRecipe.EscapeRoom : GameRecipe.DoorPuzzle;
            if (detectedGenre == VWS.GenreType.Platform || hasPlatformSet)
                return hazardCount > 0 || itemCount >= 4 ? GameRecipe.ObstacleRun : GameRecipe.PlatformCourse;
            if (detectedGenre == VWS.GenreType.Arena && bossCount > 0)
                return GameRecipe.BossBattle;
            if (zombieCount > 0 && hazardCount > 0)
                return GameRecipe.ZombieSurvival;
            if (detectedGenre == VWS.GenreType.Exploration && enemyCount == 0 && itemCount > 0)
                return itemCount >= 5 ? GameRecipe.TreasureHunt : GameRecipe.CollectAndEscape;
            if (detectedGenre == VWS.GenreType.Exploration || (enemyCount > 0 && itemCount > 0 && hasExplorationSet))
                return GameRecipe.ExplorationQuest;
            if (enemyCount > 0 && hazardCount > 0)
                return GameRecipe.SurvivalTimer;
            if (enemyCount > 0)
                return GameRecipe.CombatWave;
            if (itemCount > 0)
                return GameRecipe.CollectAndEscape;

            switch (detectedGenre)
            {
                case VWS.GenreType.Exploration:
                    return GameRecipe.ExplorationQuest;
                case VWS.GenreType.Puzzle:
                    return GameRecipe.DoorPuzzle;
                case VWS.GenreType.Platform:
                    return GameRecipe.PlatformCourse;
                default:
                    return GameRecipe.CombatWave;
            }
        }

        PlayerCharacterChoice GuessPlayerChoiceFromAssets(VWS.GenreType detectedGenre)
        {
            var best = candidates
                .Where(c => c.role == AssetRole.Player)
                .OrderByDescending(c => c.genre.HasValue && c.genre.Value == detectedGenre ? 1 : 0)
                .ThenByDescending(c => c.hasSkinnedMesh ? 1 : 0)
                .ThenByDescending(c => c.isPrefab ? 1 : 0)
                .ThenByDescending(c => c.score)
                .FirstOrDefault();

            if (best == null || !best.genre.HasValue)
                return PlayerCharacterChoice.Auto;

            switch (best.genre.Value)
            {
                case VWS.GenreType.Arena:
                    return PlayerCharacterChoice.Arena;
                case VWS.GenreType.Exploration:
                    return PlayerCharacterChoice.Exploration;
                case VWS.GenreType.Puzzle:
                    return PlayerCharacterChoice.Puzzle;
                case VWS.GenreType.Platform:
                    return PlayerCharacterChoice.Platform;
                default:
                    return PlayerCharacterChoice.Auto;
            }
        }

        EnemyCharacterChoice GuessEnemyChoiceFromAssets(VWS.GenreType detectedGenre)
        {
            var preferred = DefaultEnemyKindForGenre(detectedGenre);
            var best = candidates
                .Where(c => c.role == AssetRole.Enemy)
                .OrderByDescending(c => preferred != CharacterKind.None && c.characterKind == preferred ? 1 : 0)
                .ThenByDescending(c => c.genre.HasValue && c.genre.Value == detectedGenre ? 1 : 0)
                .ThenByDescending(c => c.hasSkinnedMesh ? 1 : 0)
                .ThenByDescending(c => c.isPrefab ? 1 : 0)
                .ThenByDescending(c => c.score)
                .FirstOrDefault();

            if (best == null)
                return EnemyCharacterChoice.Auto;

            switch (best.characterKind)
            {
                case CharacterKind.Boss:
                    return EnemyCharacterChoice.Boss;
                case CharacterKind.Zombie:
                    return EnemyCharacterChoice.Zombie;
                case CharacterKind.Orc:
                    return EnemyCharacterChoice.Orc;
                case CharacterKind.Drone:
                    return EnemyCharacterChoice.Drone;
                default:
                    return EnemyCharacterChoice.Auto;
            }
        }

        static CharacterKind DefaultEnemyKindForGenre(VWS.GenreType targetGenre)
        {
            switch (targetGenre)
            {
                case VWS.GenreType.Exploration:
                    return CharacterKind.Zombie;
                case VWS.GenreType.Arena:
                    return CharacterKind.Boss;
                default:
                    return CharacterKind.None;
            }
        }

        void TuneCountsFromAssets(GameRecipe detectedRecipe)
        {
            var detectedItems = CountCandidates(AssetRole.ItemPickup);
            var detectedEnemies = CountCandidates(AssetRole.Enemy);

            if (blockItems)
                itemGoal = Mathf.Clamp(detectedItems > 0 ? detectedItems : itemGoal, 1, 12);
            else
                itemGoal = 0;

            if (blockEnemyWave)
                waveEnemyCount = Mathf.Clamp(detectedEnemies > 0 ? detectedEnemies : waveEnemyCount, 1, 12);
            else
                waveEnemyCount = 1;

            switch (detectedRecipe)
            {
                case GameRecipe.SurvivalTimer:
                case GameRecipe.ZombieSurvival:
                    countdownSeconds = 120f;
                    break;
                case GameRecipe.BossBattle:
                    countdownSeconds = 180f;
                    waveEnemyCount = 1;
                    break;
                case GameRecipe.PlatformCourse:
                case GameRecipe.ObstacleRun:
                    countdownSeconds = 150f;
                    break;
                case GameRecipe.ExplorationQuest:
                case GameRecipe.TreasureHunt:
                case GameRecipe.EscapeRoom:
                    countdownSeconds = 180f;
                    break;
                default:
                    countdownSeconds = 90f;
                    break;
            }
        }

        int CountCandidates(AssetRole role)
        {
            return candidates.Count(c => IsCandidateAllowedForCurrentPresetKit(c, genre) && IsCandidateUsableForRole(c, role, genre));
        }

        AssetSlotStatus BuildAssetSlotStatus(AssetRole role)
        {
            var selected = FindBest(role, genre);
            var count = candidates.Count(c => IsCandidateAllowedForCurrentPresetKit(c, genre) && IsCandidateUsableForRole(c, role, genre));
            var preferred = selected != null && AssetMatchesCurrentPreference(role, selected);
            var state = selected == null ? "FALLBACK" : preferred ? "PASS" : "WARN";
            var message = selected == null
                ? "맞는 VARCO 에셋 없음: 기본 오브젝트로 자동 생성"
                : BuildAssetLabel(selected);

            if (selected != null && !preferred)
                message += " / 선택한 캐릭터 또는 장르와 다름";
            if (selected != null && selected.role != role)
                message += " / " + AssetRoleLabel(selected.role) + " 후보를 " + AssetRoleLabel(role) + " 역할로 자동 해석";
            if (count > 1)
                message += " / 다른 후보 " + (count - 1) + "개";

            return new AssetSlotStatus
            {
                role = role,
                selected = selected,
                candidateCount = count,
                preferredMatch = preferred,
                state = state,
                message = message
            };
        }

        bool AssetMatchesCurrentPreference(AssetRole role, AssetCandidate candidate)
        {
            if (candidate == null)
                return false;

            if (role == AssetRole.Player)
                return PlayerCandidateMatchesSelection(candidate);

            if (role == AssetRole.Enemy)
            {
                var preferred = PreferredCharacterKind(role, genre);
                return preferred == CharacterKind.None || candidate.characterKind == preferred;
            }

            if (role == AssetRole.Weapon)
            {
                if (!candidate.genre.HasValue || candidate.genre.Value == genre)
                    return true;

                return !candidates.Any(other => other != candidate
                    && other.genre == genre
                    && IsCandidateUsableForRole(other, role, genre));
            }

            return true;
        }

        static bool IsCandidateUsableForRole(AssetCandidate candidate, AssetRole role, VWS.GenreType targetGenre)
        {
            if (candidate == null || !candidate.hasVisuals)
                return false;

            if (FailsStrictAssetRoleSignal(candidate, role))
                return false;

            return RoleFitScore(candidate, role, targetGenre) >= MinimumRoleFitScore(role);
        }

        static bool FailsStrictAssetRoleSignal(AssetCandidate candidate, AssetRole role)
        {
            if (candidate == null)
                return true;

            var text = candidate.normalizedText ?? Normalize(candidate.path + " " + candidate.DisplayName);
            var pathText = candidate.pathNormalizedText ?? Normalize(candidate.path + " " + candidate.DisplayName);

            switch (role)
            {
                case AssetRole.Weapon:
                    if (!HasWeaponRoleSignal(text))
                        return true;
                    return HasDecorativeOrEnvironmentBlocker(pathText)
                        && !HasStrongWeaponRoleSignal(pathText);

                case AssetRole.ItemPickup:
                    if (!HasItemPickupRoleSignal(text))
                        return true;
                    return HasDecorativeOrEnvironmentBlocker(pathText)
                        && !HasStrongItemPickupRoleSignal(pathText);

                case AssetRole.HealthPickup:
                    return !HasHealthPickupRoleSignal(text);

                case AssetRole.Goal:
                    return !HasGoalRoleSignal(text);

                case AssetRole.Checkpoint:
                    return !HasCheckpointRoleSignal(text);

                case AssetRole.HazardZone:
                    return !HasHazardRoleSignal(text);

                default:
                    return false;
            }
        }

        static bool HasWeaponRoleSignal(string text)
        {
            return HasStrongWeaponRoleSignal(text) || ContainsAny(text, "blade");
        }

        static bool HasStrongWeaponRoleSignal(string text)
        {
            return ContainsAny(text, "sword", "weapon", "axe", "gun", "rifle", "staff", "wand", "dagger", "mace", "spear", "hammer")
                || ContainsSegment(text, "bow");
        }

        static bool HasItemPickupRoleSignal(string text)
        {
            return HasStrongItemPickupRoleSignal(text)
                || ContainsSegment(text, "item");
        }

        static bool HasStrongItemPickupRoleSignal(string text)
        {
            return ContainsAny(text, "pickup", "collectible", "key", "coin", "gem", "jewel", "relic", "treasure", "loot", "token", "crystal", "orb");
        }

        static bool HasHealthPickupRoleSignal(string text)
        {
            return ContainsAny(text, "healing", "health", "potion", "medkit", "med_pack")
                || ContainsSegment(text, "hp")
                || ContainsSegment(text, "heal");
        }

        static bool HasGoalRoleSignal(string text)
        {
            return ContainsAny(text, "goal", "finish", "exit", "flag", "portal", "finish_line");
        }

        static bool HasCheckpointRoleSignal(string text)
        {
            return ContainsAny(text, "checkpoint", "checklpoint", "save_point", "respawn", "beacon");
        }

        static bool HasHazardRoleSignal(string text)
        {
            return ContainsAny(text, "hazard", "trap", "spike", "lava", "fire", "acid", "laser", "mine", "fireplace");
        }

        static bool HasDecorativeOrEnvironmentBlocker(string text)
        {
            return ContainsAny(text,
                "windmill", "food", "bowl", "bowler",
                "wall", "walls", "floor", "floors", "roof", "ceiling", "column", "pillar",
                "house", "hut", "building", "tower", "bridge",
                "tree", "plant", "grass", "mushroom", "stump", "log", "rock", "stone", "terrain",
                "environment", "environments", "scenery", "prop", "props", "town");
        }

        static int MinimumRoleFitScore(AssetRole role)
        {
            switch (role)
            {
                case AssetRole.Player:
                case AssetRole.Enemy:
                    return 95;
                case AssetRole.ArenaCover:
                    return 60;
                default:
                    return 75;
            }
        }

        static int RoleFitScore(AssetCandidate candidate, AssetRole desiredRole, VWS.GenreType targetGenre)
        {
            if (candidate == null)
                return int.MinValue;

            var text = candidate.normalizedText ?? Normalize(candidate.path + " " + candidate.DisplayName);
            var score = 0;

            if (candidate.role == desiredRole)
                score += 220;
            else if (candidate.role != AssetRole.Unknown)
                score += RelatedRoleScore(candidate.role, desiredRole, text);

            score += KeywordScoreForRole(text, desiredRole);

            if (candidate.genre == targetGenre)
                score += 26;
            else if (candidate.genre.HasValue && IsCharacterRole(desiredRole))
                score -= 12;

            if (candidate.isPrefab)
                score += 18;
            if (candidate.hasVisuals)
                score += 12;
            if (candidate.usedInternalEvidence)
                score += 14;

            if (IsComplexForSimpleFunction(desiredRole, candidate))
                score -= 180;
            else if (candidate.lightCount > 48 && !IsCharacterRole(desiredRole))
                score -= 60;

            switch (desiredRole)
            {
                case AssetRole.Player:
                    if (LooksLikeEnemyText(text) || candidate.role == AssetRole.Enemy)
                        score -= 180;
                    if (candidate.characterKind == CharacterKind.Player)
                        score += 110;
                    if (candidate.hasSkinnedMesh)
                        score += 45;
                    if (candidate.animatorCount > 0)
                        score += 30;
                    if (ContainsAny(text, "human", "humanoid", "knight", "warrior", "mage", "adventurer", "astronaut", "character",
                            "사람", "인간", "휴머노이드", "기사", "전사", "마법사", "모험가", "우주인", "캐릭터"))
                        score += 55;
                    break;
                case AssetRole.Enemy:
                    if (candidate.role == AssetRole.Player || candidate.characterKind == CharacterKind.Player)
                        score -= 180;
                    if (candidate.characterKind == CharacterKind.Boss
                        || candidate.characterKind == CharacterKind.Zombie
                        || candidate.characterKind == CharacterKind.Orc
                        || candidate.characterKind == CharacterKind.Drone)
                        score += 95;
                    if (targetGenre == VWS.GenreType.Exploration && candidate.characterKind == CharacterKind.Zombie)
                        score += 40;
                    if (targetGenre == VWS.GenreType.Arena && (candidate.characterKind == CharacterKind.Boss || candidate.characterKind == CharacterKind.Orc))
                        score += 35;
                    if (targetGenre == VWS.GenreType.Platform && candidate.characterKind == CharacterKind.Drone)
                        score += 35;
                    if (candidate.hasSkinnedMesh)
                        score += 25;
                    if (candidate.animatorCount > 0)
                        score += 24;
                    break;
                case AssetRole.Weapon:
                    if (IsCharacterRole(candidate.role))
                        score -= 120;
                    break;
                case AssetRole.ArenaCover:
                    if (candidate.role == AssetRole.Unknown && candidate.hasVisuals && !candidate.hasSkinnedMesh)
                        score += 42;
                    if (candidate.characterKind == CharacterKind.Object)
                        score += 28;
                    break;
                default:
                    if (candidate.hasSkinnedMesh && !IsCharacterRole(desiredRole))
                        score -= 90;
                    break;
            }

            return score;
        }

        static int RelatedRoleScore(AssetRole actualRole, AssetRole desiredRole, string text)
        {
            if (desiredRole == AssetRole.Goal && actualRole == AssetRole.Door
                && ContainsAny(text, "portal", "exit", "finish", "goal", "flag", "crystal", "orb",
                    "포탈", "포털", "출구", "도착", "목표", "깃발", "크리스탈", "수정", "오브"))
                return 105;

            if (desiredRole == AssetRole.Door && actualRole == AssetRole.Goal
                && ContainsAny(text, "door", "gate", "hatch", "portal",
                    "문", "게이트", "해치", "포탈", "포털"))
                return 95;

            if (desiredRole == AssetRole.ItemPickup && actualRole == AssetRole.HealthPickup)
                return 45;

            if (desiredRole == AssetRole.Goal && actualRole == AssetRole.ItemPickup
                && ContainsAny(text, "key", "relic", "treasure", "crystal", "orb",
                    "열쇠", "유물", "보물", "크리스탈", "수정", "오브"))
                return 60;

            if (desiredRole == AssetRole.ArenaCover && actualRole == AssetRole.MovableBox)
                return 65;

            return -90;
        }

        static int KeywordScoreForRole(string text, AssetRole role)
        {
            switch (role)
            {
                case AssetRole.Player:
                    return ContainsAny(text, "player", "hero", "explorer", "adventurer", "astronaut", "avatar", "knight", "warrior",
                        "플레이어", "주인공", "영웅", "탐험가", "모험가", "우주인", "아바타", "기사", "전사") ? 120 : 0;
                case AssetRole.Enemy:
                    return LooksLikeEnemyText(text) ? 130 : 0;
                case AssetRole.Weapon:
                    return ContainsAny(text, "sword", "weapon", "blade", "axe", "bow", "gun", "rifle", "staff", "wand",
                        "무기", "검", "칼", "블레이드", "도끼", "활", "총", "소총", "지팡이", "마법봉") ? 135 : 0;
                case AssetRole.ItemPickup:
                    return ContainsAny(text, "item", "pickup", "collectible", "key", "coin", "gem", "jewel", "relic", "treasure", "loot", "token",
                        "아이템", "수집", "수집품", "열쇠", "키", "동전", "코인", "보석", "유물", "보물", "전리품", "토큰") ? 120 : 0;
                case AssetRole.HealthPickup:
                    return ContainsAny(text, "healing", "health", "potion", "hp", "medkit", "med_pack",
                        "회복", "체력", "포션", "치료", "구급", "힐") ? 130 : 0;
                case AssetRole.Goal:
                    return ContainsAny(text, "goal", "finish", "exit", "flag", "portal", "crystal", "orb", "finish_line",
                        "목표", "도착", "탈출", "출구", "깃발", "포탈", "포털", "크리스탈", "수정", "오브", "완주") ? 125 : 0;
                case AssetRole.Door:
                    return ContainsAny(text, "door", "gate", "portal", "hatch",
                        "문", "게이트", "포탈", "포털", "해치") ? 125 : 0;
                case AssetRole.PressurePlate:
                    return ContainsAny(text, "pressure", "plate", "switch", "button", "lever", "trigger_pad",
                        "압력판", "스위치", "버튼", "레버", "발판스위치", "트리거발판") ? 125 : 0;
                case AssetRole.HazardZone:
                    return ContainsAny(text, "hazard", "trap", "spike", "lava", "fire", "acid", "laser", "mine",
                        "위험", "함정", "가시", "용암", "불", "화염", "산성", "레이저", "지뢰", "데미지", "피해") ? 125 : 0;
                case AssetRole.MovingPlatform:
                    return ContainsAny(text, "moving_platform", "platform_moving", "platform_lift", "lift", "elevator",
                        "이동발판", "이동_발판", "움직이는발판", "움직이는_발판", "리프트", "엘리베이터", "승강기") ? 130
                        : ContainsAny(text, "platform", "발판") ? 45 : 0;
                case AssetRole.MovableBox:
                    return ContainsAny(text, "box", "crate", "push", "barrel", "container",
                        "상자", "박스", "크레이트", "밀기", "밀수있는", "밀_수_있는", "통", "배럴", "컨테이너") ? 125 : 0;
                case AssetRole.Checkpoint:
                    return ContainsAny(text, "checkpoint", "checklpoint", "save_point", "respawn", "beacon",
                        "체크포인트", "저장지점", "저장_지점", "리스폰", "부활", "비콘") ? 130 : 0;
                case AssetRole.ArenaCover:
                    return ContainsAny(text,
                        "obstacle", "cover", "wall", "rock", "pillar", "column", "fence", "barricade", "ruin", "debris",
                        "tree", "plant", "bush", "grass", "mushroom", "stump", "log", "statue", "bench", "house", "hut",
                        "building", "tower", "bridge", "sign", "lamp", "lantern", "torch", "furniture", "prop", "scenery", "environment",
                        "장애물", "엄폐", "엄폐물", "벽", "바위", "기둥", "울타리", "바리케이드", "폐허", "잔해",
                        "나무", "식물", "수풀", "풀", "버섯", "그루터기", "통나무", "조각상", "벤치", "집", "오두막",
                        "건물", "탑", "다리", "표지판", "램프", "랜턴", "횃불", "가구", "소품", "배경", "환경") ? 115 : 0;
                default:
                    return 0;
            }
        }

        string AssetSlotSummary()
        {
            var slots = ActiveRolesForCurrentBlocks()
                .Distinct()
                .Select(BuildAssetSlotStatus)
                .ToList();
            if (slots.Count == 0)
                return "활성 에셋 슬롯 없음.";

            var pass = slots.Count(slot => slot.state == "PASS");
            var warn = slots.Count(slot => slot.state == "WARN");
            var fallback = slots.Count(slot => slot.state == "FALLBACK");
            return "에셋 슬롯: 통과 " + pass + " / 확인 " + warn + " / 기본 생성 " + fallback;
        }

        void SelectCoreBlocks()
        {
            blockPlayer = true;
            blockWeapon = false;
            blockGoal = true;
            blockHud = true;
            blockVisuals = true;
            blockSound = true;
        }

        void SelectAllBlocks()
        {
            blockPlayer = true;
            blockWeapon = true;
            blockEnemyWave = true;
            blockItems = true;
            blockGoal = true;
            blockHealthPickup = true;
            blockHazard = true;
            blockCheckpoint = true;
            blockFallRespawn = true;
            blockMovingPlatform = true;
            blockPuzzleDoor = true;
            blockMovableBox = true;
            blockCover = true;
            blockCountdown = true;
            blockHud = true;
            blockVisuals = true;
            blockSound = true;
            itemGoal = Mathf.Max(itemGoal, 3);
        }

        void ClearOptionalBlocks()
        {
            blockEnemyWave = false;
            blockWeapon = false;
            blockItems = false;
            blockHealthPickup = false;
            blockHazard = false;
            blockCheckpoint = false;
            blockFallRespawn = false;
            blockMovingPlatform = false;
            blockPuzzleDoor = false;
            blockMovableBox = false;
            blockCover = false;
            blockCountdown = false;
        }

        void DrawAssetSummary()
        {
            if (UseCachedEditorSummary())
            {
                DrawCachedAssetSummary();
                return;
            }

            DrawHeader("3. 감지된 에셋");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("에셋 다시 찾기", GUILayout.Height(28f)))
                    ScanAssets();
                GUILayout.Label("스캔: " + ScanRootSummary(GameObjectScanRoots()), EditorStyles.miniLabel);
            }

            var roleCounts = candidates
                .Where(c => c.role != AssetRole.Unknown)
                .GroupBy(c => c.role)
                .OrderBy(g => g.Key.ToString())
                .Select(g => AssetRoleLabel(g.Key) + ": " + g.Count())
                .ToArray();
            EditorGUILayout.HelpBox(roleCounts.Length > 0 ? string.Join(" / ", roleCounts) : "아직 사용할 수 있는 에셋을 찾지 못했습니다.", MessageType.None);
            EditorGUILayout.HelpBox(AssetSlotSummary(), MessageType.None);

            DrawAssetMatchingGuide();

            foreach (var role in ActiveRolesForCurrentBlocks())
            {
                var slot = BuildAssetSlotStatus(role);
                EditorGUILayout.LabelField(StateLabel(slot.state) + ": " + AssetRoleLabel(role), slot.message);
            }

            DrawTopDetectedAssets();
            DrawSoundSummary();
            DrawAnimationSummary();
        }

        static bool UseCachedEditorSummary()
        {
            return false;
        }

        void DrawCachedAssetSummary()
        {
            EnsureEditorSummaryCache();
            DrawHeader("3. 감지한 에셋");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("에셋 다시 찾기", GUILayout.Height(28f)))
                {
                    ScanAssets();
                    EnsureEditorSummaryCache(true);
                }

                if (GUILayout.Button("상태 새로고침", GUILayout.Height(28f)))
                    EnsureEditorSummaryCache(true);

                GUILayout.Label("스캔: " + ScanRootSummary(GameObjectScanRoots()), EditorStyles.miniLabel);
            }

            EditorGUILayout.HelpBox(cachedRoleCountsText, MessageType.None);
            EditorGUILayout.HelpBox(cachedAssetSlotSummaryText, MessageType.None);

            DrawCachedAssetMatchingGuide();

            foreach (var slot in cachedActiveAssetSlots)
                EditorGUILayout.LabelField(StateLabel(slot.state) + ": " + AssetRoleLabel(slot.role), slot.message);

            DrawCachedTopDetectedAssets();
            DrawCachedSoundSummary();
            DrawCachedAnimationSummary();
        }

        void DrawCachedAssetMatchingGuide()
        {
            var slots = cachedActiveAssetSlots;
            if (slots.Count == 0)
                return;

            var summary = AssetSlotProgressLabel(slots);
            assetMatchingGuideOpen = EditorGUILayout.Foldout(assetMatchingGuideOpen, "에셋 자동 매칭판 - " + summary, true);
            if (!assetMatchingGuideOpen)
                return;

            var detectedCount = candidates.Count(candidate => candidate.role != AssetRole.Unknown);
            var internalEvidenceCount = candidates.Count(candidate => candidate.usedInternalEvidence);
            EditorGUILayout.HelpBox(
                "현재 켜진 블록에 맞는 VARCO 에셋 매칭 상태입니다."
                + "\n감지 후보 " + detectedCount + "개 / 프리팹 내부 단서 사용 " + internalEvidenceCount + "개",
                AssetSlotMessageType(slots));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("에셋 기준 자동 설계", GUILayout.Height(24f)))
                    AutoDesignFromAssets();
                if (GUILayout.Button("에셋 매칭 진단서", GUILayout.Height(24f)))
                    GenerateAssetMatchingReport();
                if (GUILayout.Button("부족한 에셋 요청서", GUILayout.Height(24f)))
                    GenerateAssetRequestSheet();
            }

            foreach (var slot in slots)
            {
                EditorGUILayout.LabelField(
                    StateLabel(slot.state) + ": " + AssetRoleLabel(slot.role),
                    BuildAssetSlotGuideMessage(slot),
                    EditorStyles.wordWrappedMiniLabel);
            }

            if (cachedInternalMatches.Count == 0)
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("프리팹 내부 단서로 찾은 후보", EditorStyles.miniBoldLabel);
            foreach (var candidate in cachedInternalMatches)
            {
                EditorGUILayout.LabelField(
                    AssetRoleLabel(candidate.role) + " / " + candidate.DisplayName,
                    BuildAssetShortReason(candidate),
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        void DrawCachedTopDetectedAssets()
        {
            if (cachedTopDetectedAssets.Count == 0)
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("상위 감지 후보", EditorStyles.miniBoldLabel);
            foreach (var candidate in cachedTopDetectedAssets)
            {
                EditorGUILayout.LabelField(
                    AssetRoleLabel(candidate.role) + " / " + candidate.DisplayName,
                    BuildAssetShortReason(candidate),
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        void DrawCachedSoundSummary()
        {
            if (!blockSound || cachedSoundSlotStatuses.Count == 0)
                return;

            var connectedCount = cachedSoundSlotStatuses.Count(status => status.state == "PASS");
            var missingCount = cachedSoundSlotStatuses.Count - connectedCount;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("사운드/BGM 자동 연결", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "연결 " + connectedCount + "/" + cachedSoundSlotStatuses.Count
                + (missingCount > 0 ? " / 누락 " + missingCount : "")
                + " / 스캔: " + ScanRootSummary(AudioScanRoots()),
                missingCount > 0 ? MessageType.Warning : MessageType.None);

            foreach (var status in cachedSoundSlotStatuses.Take(6))
                EditorGUILayout.LabelField(
                    StateLabel(status.state) + ": " + status.definition.label,
                    SoundSlotMessage(status),
                    EditorStyles.wordWrappedMiniLabel);
        }

        void DrawCachedAnimationSummary()
        {
            if (!blockPlayer || !autoAnimations || cachedAnimationSlotStatuses.Count == 0)
                return;

            var readyCount = cachedAnimationSlotStatuses.Count(status => status.state == "PASS");
            var missingCount = cachedAnimationSlotStatuses.Count - readyCount;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("애니메이션 자동 연결", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "클립 준비 " + readyCount + "/" + cachedAnimationSlotStatuses.Count
                + (missingCount > 0 ? " / 누락 " + missingCount : "")
                + " / 스캔: " + ScanRootSummary(AnimationScanRoots()),
                missingCount > 0 ? MessageType.Warning : MessageType.None);

            foreach (var status in cachedAnimationSlotStatuses.Take(7))
                EditorGUILayout.LabelField(
                    StateLabel(status.state) + ": " + AnimationSlotLabel(status.definition),
                    AnimationSlotMessage(status),
                    EditorStyles.wordWrappedMiniLabel);
        }

        void DrawAssetMatchingGuide()
        {
            var roles = ActiveRolesForCurrentBlocks().Distinct().ToList();
            if (roles.Count == 0)
                return;

            var slots = roles.Select(BuildAssetSlotStatus).ToList();
            var summary = AssetSlotProgressLabel(slots);
            assetMatchingGuideOpen = EditorGUILayout.Foldout(assetMatchingGuideOpen, "에셋 자동 매칭판 - " + summary, true);
            if (!assetMatchingGuideOpen)
                return;

            var detectedCount = candidates.Count(candidate => candidate.role != AssetRole.Unknown);
            var internalEvidenceCount = candidates.Count(candidate => candidate.usedInternalEvidence);
            EditorGUILayout.HelpBox(
                "현재 켜진 블록에 어떤 VARCO 에셋이 붙는지 보여줍니다. 파일명뿐 아니라 프리팹 안의 모델, 머티리얼, 메시, Animator 이름까지 검사합니다."
                + "\n감지 후보 " + detectedCount + "개 / 프리팹 내부 단서 사용 " + internalEvidenceCount + "개",
                AssetSlotMessageType(slots));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("에셋 기준 자동 설계", GUILayout.Height(24f)))
                    AutoDesignFromAssets();
                if (GUILayout.Button("에셋 매칭 진단서", GUILayout.Height(24f)))
                    GenerateAssetMatchingReport();
                if (GUILayout.Button("부족한 에셋 요청서", GUILayout.Height(24f)))
                    GenerateAssetRequestSheet();
            }

            foreach (var slot in slots)
            {
                EditorGUILayout.LabelField(
                    StateLabel(slot.state) + ": " + AssetRoleLabel(slot.role),
                    BuildAssetSlotGuideMessage(slot),
                    EditorStyles.wordWrappedMiniLabel);
            }

            var internalMatches = candidates
                .Where(candidate => candidate.usedInternalEvidence)
                .Take(5)
                .ToList();
            if (internalMatches.Count == 0)
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("프리팹 내부 단서로 찾은 후보", EditorStyles.miniBoldLabel);
            foreach (var candidate in internalMatches)
            {
                EditorGUILayout.LabelField(
                    AssetRoleLabel(candidate.role) + " / " + candidate.DisplayName,
                    BuildAssetShortReason(candidate),
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        string BuildAssetSlotGuideMessage(AssetSlotStatus slot)
        {
            if (slot == null)
                return "슬롯 정보를 확인할 수 없습니다.";

            if (slot.selected == null)
            {
                return "추천 이름: " + SuggestedAssetName(slot.role)
                    + " / 넣을 위치: " + SuggestedImportTarget(slot.role)
                    + " / 자동 제작은 우선 기본 오브젝트로 게임 기능을 만듭니다.";
            }

            var candidate = slot.selected;
            var message = "사용: " + candidate.DisplayName
                + " / 후보 " + slot.candidateCount + "개"
                + " / 점수 " + candidate.score
                + " / " + (candidate.isPrefab ? "프리팹" : "모델")
                + " / " + (candidate.hasVisuals ? "비주얼 있음" : "비주얼 없음");

            if (candidate.usedInternalEvidence)
                message += " / 프리팹 내부 단서로 인식";
            if (!slot.preferredMatch)
                message += " / 선택 조건 확인: 현재 설정은 " + AssetPreferenceLabel(slot.role);

            return message;
        }

        UnifiedAssetMatchLine BuildUnifiedAssetMatchLine(AssetRole role)
        {
            var slot = BuildAssetSlotStatus(role);
            var selected = slot.selected;
            return new UnifiedAssetMatchLine
            {
                stateCode = slot.state,
                stateLabel = StateLabel(slot.state),
                roleLabel = AssetRoleLabel(role),
                assetName = selected != null ? selected.DisplayName : "기본 오브젝트 자동 생성",
                assetPath = selected != null ? selected.path : string.Empty,
                detail = BuildUnifiedAssetMatchDetail(slot),
                candidateCount = slot.candidateCount,
                hasAsset = selected != null,
                usedInternalEvidence = selected != null && selected.usedInternalEvidence
            };
        }

        string BuildUnifiedAssetMatchDetail(AssetSlotStatus slot)
        {
            if (slot == null)
                return "매칭 정보를 확인할 수 없습니다.";

            if (slot.selected == null)
            {
                return "맞는 VARCO 에셋을 찾지 못했습니다. 추천 이름 "
                    + SuggestedAssetName(slot.role)
                    + "으로 VarcoAI에서 만들면 이 역할에 자동 연결됩니다.";
            }

            var selected = slot.selected;
            var roleFit = RoleFitScore(selected, slot.role, genre);
            var details = new List<string>
            {
                "적합도 " + roleFit,
                AssetMatchesCurrentPreference(slot.role, selected) ? "현재 장르/선택 조건과 맞음" : "선택 조건 확인 필요"
            };

            if (selected.genre.HasValue)
                details.Add("장르 단서 " + GenreLabel(selected.genre.Value));
            else
                details.Add("장르 제한 없음");

            if (selected.hasSkinnedMesh)
                details.Add("리깅 메시");
            if (selected.animatorCount > 0)
                details.Add("Animator " + selected.animatorCount + "개");
            if (selected.usedInternalEvidence)
                details.Add("프리팹 내부 단서 사용");
            if (selected.role != slot.role)
                details.Add(AssetRoleLabel(selected.role) + " 후보를 " + AssetRoleLabel(slot.role) + "로 자동 해석");
            if (slot.candidateCount > 1)
                details.Add("예비 후보 " + (slot.candidateCount - 1) + "개");

            return string.Join(" / ", details);
        }

        string AssetPreferenceLabel(AssetRole role)
        {
            switch (role)
            {
                case AssetRole.Player:
                    return PlayerCharacterLabel();
                case AssetRole.Enemy:
                    return EnemyCharacterLabel();
                case AssetRole.Weapon:
                    return GenreLabel(genre) + " 무기";
                default:
                    return AssetRoleLabel(role);
            }
        }

        static string AssetSlotProgressLabel(List<AssetSlotStatus> slots)
        {
            if (slots == null || slots.Count == 0)
                return "활성 슬롯 없음";

            var pass = slots.Count(slot => slot.state == "PASS");
            var warn = slots.Count(slot => slot.state == "WARN");
            var fallback = slots.Count(slot => slot.state == "FALLBACK");
            var fail = slots.Count(slot => slot.state == "FAIL");
            return "통과 " + pass + " / 확인 " + warn + " / 기본 생성 " + fallback + " / 실패 " + fail;
        }

        static MessageType AssetSlotMessageType(List<AssetSlotStatus> slots)
        {
            if (slots != null && slots.Any(slot => slot.state == "FAIL"))
                return MessageType.Error;
            if (slots != null && slots.Any(slot => slot.state == "WARN" || slot.state == "FALLBACK"))
                return MessageType.Warning;
            return MessageType.Info;
        }

        void DrawTopDetectedAssets()
        {
            var top = candidates
                .Where(candidate => candidate.role != AssetRole.Unknown)
                .Take(6)
                .ToList();
            if (top.Count == 0)
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("상위 감지 후보", EditorStyles.miniBoldLabel);
            foreach (var candidate in top)
            {
                EditorGUILayout.LabelField(
                    AssetRoleLabel(candidate.role) + " / " + candidate.DisplayName,
                    BuildAssetShortReason(candidate),
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        void DrawSoundSummary()
        {
            if (!blockSound)
                return;

            var registry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(RegistryPath);
            var statuses = BuildSoundSlotStatuses(registry).ToList();
            if (statuses.Count == 0)
                return;

            var connectedCount = statuses.Count(status => status.state == "PASS");
            var missingCount = statuses.Count - connectedCount;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("사운드/BGM 자동 연결", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "연결 " + connectedCount + "/" + statuses.Count
                + (missingCount > 0 ? " / 누락 " + missingCount : "")
                + " / 스캔: " + ScanRootSummary(AudioScanRoots()),
                missingCount > 0 ? MessageType.Warning : MessageType.None);

            foreach (var status in statuses.Take(6))
                EditorGUILayout.LabelField(
                    StateLabel(status.state) + ": " + status.definition.label,
                    SoundSlotMessage(status),
                    EditorStyles.wordWrappedMiniLabel);
        }

        void DrawAnimationSummary()
        {
            if (!blockPlayer || !autoAnimations)
                return;

            var statuses = BuildAnimationSlotStatuses().ToList();
            if (statuses.Count == 0)
                return;

            var readyCount = statuses.Count(status => status.state == "PASS");
            var missingCount = statuses.Count - readyCount;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("애니메이션 자동 연결", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "클립 준비 " + readyCount + "/" + statuses.Count
                + (missingCount > 0 ? " / 누락 " + missingCount : "")
                + " / 스캔: " + ScanRootSummary(AnimationScanRoots()),
                missingCount > 0 ? MessageType.Warning : MessageType.None);

            foreach (var status in statuses.Take(7))
                EditorGUILayout.LabelField(
                    StateLabel(status.state) + ": " + AnimationSlotLabel(status.definition),
                    AnimationSlotMessage(status),
                    EditorStyles.wordWrappedMiniLabel);
        }

        void DrawActions()
        {
            DrawHeader("4. 자동 제작");
            DrawOneClickGuide();

            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.55f, 1f);
            if (GUILayout.Button("현재 VARCO 에셋으로 게임 만들기", GUILayout.Height(48f)))
                BuildGameOneClick();
            if (GUILayout.Button("가장 맞는 프리셋 선택 후 만들기", GUILayout.Height(40f)))
                BuildBestPresetOneClick();
            if (GUILayout.Button("가장 맞는 프리셋 선택 후 Windows EXE 빌드", GUILayout.Height(40f)))
                BuildBestPresetWindowsExe();
            GUI.backgroundColor = Color.white;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("현재 씬 자동 보정", GUILayout.Height(30f)))
                    FixAllCurrentScene();
                if (GUILayout.Button("씬 검사/보정", GUILayout.Height(30f)))
                    VARCOSceneHealthCheckWindow.Open();
                if (GUILayout.Button("Windows EXE 빌드", GUILayout.Height(30f)))
                    BuildWindowsExe();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("선택 에셋/폴더 배치+자동 연결", GUILayout.Height(30f)))
                    VARCOBlockCodingBuilderWindow.PlaceAndAutoConnectSelectedAssetsMenu();
                if (GUILayout.Button("선택 모델들 자동 연결", GUILayout.Height(30f)))
                    VARCOBlockCodingBuilderWindow.AutoConnectSelectionMenu();
            }

            DrawAcceptanceSummary();
            DrawWindowsBuildReadiness();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("준비 상태 리포트 만들기", GUILayout.Height(26f)))
                    GenerateReadinessReport();
                if (GUILayout.Button("최근 리포트 열기", GUILayout.Height(26f)))
                    OpenLastReport();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("완성 체크리스트 만들기", GUILayout.Height(26f)))
                    GenerateAcceptanceChecklistReport();
                if (GUILayout.Button("플레이 준비 검사", GUILayout.Height(26f)))
                    GeneratePlayReadyReport();
                if (GUILayout.Button("프리셋 준비도 표 만들기", GUILayout.Height(26f)))
                    GeneratePresetReadinessMatrix();
            }

            if (GUILayout.Button("VARCO 에셋 요청서 만들기", GUILayout.Height(26f)))
                GenerateAssetRequestSheet();
        }

        void DrawOneClickGuide()
        {
            var recommendation = BuildOneClickRecommendation();
            EditorGUILayout.HelpBox(
                "추천 다음 버튼: " + recommendation.title + "\n" + recommendation.detail,
                recommendation.messageType);

            if (recommendation.action != RecommendedAction.None && !string.IsNullOrWhiteSpace(recommendation.buttonLabel))
            {
                GUI.backgroundColor = new Color(0.55f, 0.75f, 1f, 1f);
                if (GUILayout.Button(recommendation.buttonLabel, GUILayout.Height(34f)))
                    ExecuteRecommendedAction(recommendation.action);
                GUI.backgroundColor = Color.white;
            }

            var steps = BuildOneClickNextSteps().Take(5).ToList();
            if (steps.Count == 0)
                return;

            EditorGUILayout.LabelField("진행 순서", EditorStyles.miniBoldLabel);
            foreach (var step in steps)
                EditorGUILayout.LabelField("- " + step, EditorStyles.wordWrappedMiniLabel);
        }

        OneClickRecommendation BuildOneClickRecommendation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return new OneClickRecommendation
                {
                    action = RecommendedAction.None,
                    title = "Play 모드 종료",
                    detail = "씬을 안전하게 바꾸려면 먼저 Unity Play 모드를 끄세요.",
                    buttonLabel = "",
                    messageType = MessageType.Warning
                };
            }

            var acceptance = BuildAcceptanceChecklist();
            var slots = ActiveRolesForCurrentBlocks()
                .Distinct()
                .Select(BuildAssetSlotStatus)
                .ToList();
            var fallbackCount = slots.Count(slot => slot.state == "FALLBACK");
            var failCount = acceptance.Count(finding => finding.state == "FAIL");
            var warnCount = acceptance.Count(finding => finding.state == "WARN");

            if (!SafeAutomationDefaultsEnabled())
            {
                return new OneClickRecommendation
                {
                    action = RecommendedAction.EnableSafeAutomation,
                    title = "초보자 안전 자동화 켜기",
                    detail = "부족한 오브젝트 생성, 에셋 자동 연결, HUD, 비주얼, 사운드, 씬 저장을 켜서 버튼 한 번 제작 흐름을 안전하게 만듭니다.",
                    buttonLabel = "추천 실행: 초보자 안전 자동화 켜기",
                    messageType = MessageType.Warning
                };
            }

            if (blockTemplate == BlockTemplate.Custom || recipe == GameRecipe.GenreDefault)
            {
                return new OneClickRecommendation
                {
                    action = RecommendedAction.AutoDesign,
                    title = "에셋 보고 자동 설계",
                    detail = "현재 프로젝트의 VARCO 에셋 이름과 역할을 읽어 장르, 게임 방식, 블록 템플릿, 캐릭터 선택을 자동으로 맞춥니다.",
                    buttonLabel = "추천 실행: 에셋 보고 자동 설계",
                    messageType = MessageType.Info
                };
            }

            if (candidates.Count == 0 || (slots.Count > 0 && fallbackCount >= Mathf.Max(2, Mathf.CeilToInt(slots.Count * 0.5f))))
            {
                return new OneClickRecommendation
                {
                    action = RecommendedAction.GenerateAssetRequest,
                    title = "부족한 VARCO 에셋 요청서 만들기",
                    detail = "활성 블록에 필요한 에셋 매칭이 부족합니다. VarcoAI에서 바로 만들 수 있는 이름과 생성 요청문을 먼저 뽑습니다.",
                    buttonLabel = "추천 실행: VARCO 에셋 요청서 만들기",
                    messageType = MessageType.Warning
                };
            }

            if (SceneNeedsOneClickBuild() || failCount > 0)
            {
                return new OneClickRecommendation
                {
                    action = RecommendedAction.BuildGame,
                    title = "현재 에셋으로 게임 만들기",
                    detail = "씬에 필요한 게임 매니저, 플레이어, HUD, 규칙 오브젝트를 만들고 감지된 에셋을 자동 연결합니다.",
                    buttonLabel = "추천 실행: 자동 제작 실행",
                    messageType = MessageType.Info
                };
            }

            if (warnCount > 0)
            {
                return new OneClickRecommendation
                {
                    action = RecommendedAction.BuildGame,
                    title = "자동 제작으로 남은 확인 항목 보정",
                    detail = "대부분 준비됐지만 몇 가지 확인 항목이 남아 있습니다. 자동 제작을 다시 실행하면 연결과 안전 보정을 한 번 더 맞춥니다.",
                    buttonLabel = "추천 실행: 자동 제작 다시 실행",
                    messageType = MessageType.Warning
                };
            }

            return new OneClickRecommendation
            {
                action = RecommendedAction.BuildWindows,
                title = "Windows EXE 빌드",
                detail = "현재 체크리스트가 통과 상태입니다. 실행 파일까지 필요하면 가장 준비된 프리셋 기준으로 빌드하세요.",
                buttonLabel = "추천 실행: Windows EXE 빌드",
                messageType = MessageType.Info
            };
        }

        IEnumerable<string> BuildOneClickNextSteps()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                yield return "Unity Play 모드를 먼저 끕니다.";
                yield break;
            }

            if (!SafeAutomationDefaultsEnabled())
                yield return "초보자 안전 자동화를 켭니다.";

            if (blockTemplate == BlockTemplate.Custom || recipe == GameRecipe.GenreDefault)
                yield return "에셋 보고 자동 설계로 장르와 블록 템플릿을 맞춥니다.";

            var slots = ActiveRolesForCurrentBlocks()
                .Distinct()
                .Select(BuildAssetSlotStatus)
                .ToList();
            var fallbackCount = slots.Count(slot => slot.state == "FALLBACK");
            if (fallbackCount > 0)
                yield return "부족한 에셋 " + fallbackCount + "개는 요청서로 VarcoAI 생성 목록을 뽑습니다.";

            if (SceneNeedsOneClickBuild())
                yield return "자동 제작으로 씬 오브젝트, HUD, 사운드, 애니메이션 연결을 생성합니다.";

            var acceptance = BuildAcceptanceChecklist();
            var warnCount = acceptance.Count(finding => finding.state == "WARN");
            var failCount = acceptance.Count(finding => finding.state == "FAIL");
            if (!SceneNeedsOneClickBuild() && (warnCount > 0 || failCount > 0))
                yield return "자동 제작을 다시 실행해 남은 확인 항목을 보정합니다.";

            if (warnCount == 0 && failCount == 0)
                yield return "완성 체크가 통과되면 Windows EXE 빌드를 실행합니다.";
        }

        void ExecuteRecommendedAction(RecommendedAction action)
        {
            switch (action)
            {
                case RecommendedAction.EnableSafeAutomation:
                    EnableSafeAutomationDefaults();
                    break;
                case RecommendedAction.AutoDesign:
                    AutoDesignFromAssets();
                    break;
                case RecommendedAction.BuildGame:
                    BuildGameOneClick();
                    break;
                case RecommendedAction.BuildBestPreset:
                    BuildBestPresetOneClick();
                    break;
                case RecommendedAction.GenerateAssetRequest:
                    GenerateAssetRequestSheet();
                    break;
                case RecommendedAction.BuildWindows:
                    BuildBestPresetWindowsExe();
                    break;
            }
        }

        bool SafeAutomationDefaultsEnabled()
        {
            return createMissingObjects
                && autoConnectPrefabs
                && autoAnimations
                && autoSounds
                && addModernHud
                && applyVisualPreset
                && runSafetyPass
                && addSceneToBuild
                && saveScene;
        }

        void EnableSafeAutomationDefaults()
        {
            createMissingObjects = true;
            autoConnectPrefabs = true;
            autoAnimations = true;
            autoSounds = true;
            addModernHud = true;
            applyVisualPreset = true;
            runSafetyPass = true;
            addSceneToBuild = true;
            saveScene = true;
            log.Add("초보자 안전 자동화를 켰습니다. 이제 자동 제작을 실행해도 됩니다.");
        }

        bool SceneNeedsOneClickBuild()
        {
            if (!FindFirstObjectByType<VWS.GameManager>())
                return true;
            if (blockPlayer && !GameObject.FindGameObjectWithTag("Player"))
                return true;
            if (blockHud && addModernHud && !FindFirstObjectByType<VWS.VARCOGameHUD>())
                return true;
            if (blockEnemyWave && !FindFirstObjectByType<VWS.WaveManager>())
                return true;
            if (blockItems && CountSceneComponents<VWS.ItemPickup>() < Mathf.Max(1, itemGoal))
                return true;
            if (blockGoal && !FindFirstObjectByType<VWS.GoalTrigger>())
                return true;
            if (blockHealthPickup && !FindFirstObjectByType<VWS.HealthPickup>())
                return true;
            if (blockHazard && !FindFirstObjectByType<VWS.HazardZone>())
                return true;
            if (blockCheckpoint && !FindFirstObjectByType<VWS.Checkpoint>())
                return true;
            if (blockFallRespawn && !FindFirstObjectByType<VWS.DeathZone>())
                return true;
            if (blockMovingPlatform && !FindFirstObjectByType<VWS.MovingPlatform>())
                return true;
            if (blockPuzzleDoor && (!FindFirstObjectByType<VWS.DoorController>() || !FindFirstObjectByType<VWS.PressurePlate>()))
                return true;
            if (blockMovableBox && !FindFirstObjectByType<VWS.MovableBox>())
                return true;
            if (blockCountdown && !FindFirstObjectByType<VWS.CountdownTimer>())
                return true;
            if (blockVisuals && applyVisualPreset && !FindFirstObjectByType<Volume>())
                return true;
            if (blockSound && autoSounds && !GameObject.Find("VW_Audio_BGM"))
                return true;
            if (SceneHasPlayReadyIssue())
                return true;

            return false;
        }

        void DrawAdvanced()
        {
            advancedOpen = EditorGUILayout.Foldout(advancedOpen, "고급 설정", true);
            if (!advancedOpen)
                return;

            createMissingObjects = EditorGUILayout.ToggleLeft("부족한 게임 오브젝트 자동 생성", createMissingObjects);
            autoConnectPrefabs = EditorGUILayout.ToggleLeft("감지된 프리팹/모델을 게임 기능에 자동 연결", autoConnectPrefabs);
            autoAnimations = EditorGUILayout.ToggleLeft("애니메이션 컨트롤러 자동 생성/연결", autoAnimations);
            autoSounds = EditorGUILayout.ToggleLeft("사운드 자동 동기화/연결", autoSounds);
            addModernHud = EditorGUILayout.ToggleLeft("게임 HUD 자동 추가", addModernHud);
            applyVisualPreset = EditorGUILayout.ToggleLeft("비주얼 프리셋 적용", applyVisualPreset);
            runSafetyPass = EditorGUILayout.ToggleLeft("안전 보정 실행", runSafetyPass);
            addSceneToBuild = EditorGUILayout.ToggleLeft("현재 씬을 빌드 설정에 추가", addSceneToBuild);
            useOnlyActiveSceneForWindowsBuild = EditorGUILayout.ToggleLeft("Windows 빌드는 현재 씬만 사용", useOnlyActiveSceneForWindowsBuild);
            saveScene = EditorGUILayout.ToggleLeft("제작 후 씬 저장", saveScene);
            countdownSeconds = EditorGUILayout.Slider("제한시간(초)", countdownSeconds, 10f, 300f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("한글 블록 조립기 열기"))
                    VARCOBlockCodingBuilderWindow.Open();
                if (GUILayout.Button("사운드 연결 열기"))
                    VARCOSoundConnectorWindow.Open();
                if (GUILayout.Button("애니메이션 설정 열기"))
                    VARCOAnimationSetupWindow.Open();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("선택 에셋/폴더 배치+연결"))
                    VARCOBlockCodingBuilderWindow.PlaceAndAutoConnectSelectedAssetsMenu();
                if (GUILayout.Button("선택 모델들 자동 연결"))
                    VARCOBlockCodingBuilderWindow.AutoConnectSelectionMenu();
            }
        }

        void DrawLog()
        {
            if (log.Count == 0)
                return;

            DrawHeader("작업 로그");
            EditorGUILayout.TextArea(string.Join("\n", log), GUILayout.MinHeight(150f));
        }

        void DrawWindowsBuildReadiness()
        {
            var findings = BuildWindowsReadinessFindings();
            windowsBuildReadinessOpen = EditorGUILayout.Foldout(
                windowsBuildReadinessOpen,
                "Windows 빌드 준비판 - " + AcceptanceSummary(findings),
                true);
            if (!windowsBuildReadinessOpen)
                return;

            var scenePath = SceneManager.GetActiveScene().path;
            EditorGUILayout.HelpBox(
                "출력 위치: " + WindowsBuildOutputPath().Replace("\\", "/")
                + "\n씬: " + (string.IsNullOrWhiteSpace(scenePath) ? "아직 저장되지 않음" : scenePath)
                + "\n빌드 전 자동 준비: 씬 저장 " + BoolLabel(saveScene)
                + " / 빌드 설정 추가 " + BoolLabel(addSceneToBuild)
                + " / 현재 씬만 사용 " + BoolLabel(useOnlyActiveSceneForWindowsBuild),
                AcceptanceMessageType(findings));

            foreach (var finding in findings.Where(item => item.state != "PASS").Take(8))
                EditorGUILayout.LabelField(StateLabel(finding.state) + ": " + finding.area + " - " + finding.message, EditorStyles.wordWrappedMiniLabel);

            if (findings.All(item => item.state == "PASS"))
                EditorGUILayout.LabelField("실행 파일 빌드 준비가 끝났습니다.", EditorStyles.wordWrappedMiniLabel);

            if (GUILayout.Button("Windows 빌드 전 점검 리포트 만들기", GUILayout.Height(24f)))
                GenerateBuildPreflightReport();
        }

        void BuildGameOneClick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("VARCO 게임 메이커", "씬을 변경하기 전에 플레이 모드를 종료하세요.", "확인");
                return;
            }

            log.Clear();
            ScanAssets();
            NormalizeBlockPlan();

            Undo.SetCurrentGroupName("VARCO One Click Game");
            var undoGroup = Undo.GetCurrentGroup();

            if (sceneMode == SceneMode.GenreScene)
                OpenGenreScene();

            EnsureSceneCanBeSaved();
            EnsureFolders();
            var shouldAutoSounds = blockSound && autoSounds;
            var shouldAddHud = blockHud && addModernHud;
            var shouldApplyVisuals = blockVisuals && applyVisualPreset;

            var registry = shouldAutoSounds ? SyncAudioRegistry() : EnsureRegistry();
            EnsureGameManagerAndProfile();
            EnsureBaseEnvironment();
            PrepareSceneForPresetBuild();

            if (autoConnectPrefabs)
            {
                ClearAutoBuildLayoutBeforePresetBuild();
                ClearPresetTransientRootsBeforeBuild();
                BuildGenreObjects(registry);
            }

            if (shouldAddHud)
                EnsureModernHud();

            if (shouldApplyVisuals)
                ApplyVisualSetup();

            if (shouldAutoSounds)
                ApplySoundBindings(registry);

            if (runSafetyPass)
                VARCOBlockCodingBuilderWindow.RunSafetyPassForCurrentScene(log, saveScene: false);

            EnsureSinglePlayerControllerForGenre();
            NormalizeRuntimeGroundAlignForActiveCharacters();
            if (shouldAddHud)
                EnsureModernHud();
            ConfigureWaveManagerForGenre();
            ApplyRecipeCombatTuningToEnemies();
            EnsureGameManagerProfileMatchesGenre();
            OptimizeGeneratedLayoutForPerformance();
            OptimizeSceneLightingForEditorPerformance();
            EnsurePlayableNavMesh();

            if (addSceneToBuild)
                AddActiveSceneToBuildSettings();

            AppendValidationReport(registry);

            if (saveScene)
                SaveActiveScene();

            log.Add("완료. Unity Play 버튼을 눌러 생성된 게임을 테스트하세요.");
            SaveOneClickReport("OneClick");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Undo.CollapseUndoOperations(undoGroup);
        }

        void FixAllCurrentScene()
        {
            log.Clear();
            AdoptGenreFromActiveScene();
            NormalizeBlockPlan();
            EnsureFolders();
            var shouldAutoSounds = blockSound && autoSounds;
            var shouldAddHud = blockHud && addModernHud;
            var shouldApplyVisuals = blockVisuals && applyVisualPreset;

            var registry = shouldAutoSounds ? SyncAudioRegistry() : EnsureRegistry();
            EnsureGameManagerAndProfile();
            EnsureBaseEnvironment();
            if (shouldAddHud)
                EnsureModernHud();
            if (shouldApplyVisuals)
                ApplyVisualSetup();
            if (shouldAutoSounds)
                ApplySoundBindings(registry);
            VARCOBlockCodingBuilderWindow.RunSafetyPassForCurrentScene(log, saveScene: false);
            EnsureSinglePlayerControllerForGenre();
            NormalizeRuntimeGroundAlignForActiveCharacters();
            ConfigureWaveManagerForGenre();
            ApplyRecipeCombatTuningToEnemies();
            EnsureGameManagerProfileMatchesGenre();
            OptimizeGeneratedLayoutForPerformance();
            OptimizeSceneLightingForEditorPerformance();
            EnsurePlayableNavMesh();
            if (!PrepareActiveSceneForBuild())
                log.Add("빌드 준비 경고: 현재 씬 저장 또는 빌드 설정 자동 준비를 완료하지 못했습니다.");
            AppendValidationReport(registry);
            log.Add("현재 씬 자동 보정을 완료했습니다.");
            SaveOneClickReport("FixAll");
        }

        void RevalidateChangedPresetScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("VARCO Game Maker", "Play 모드를 종료한 뒤 변경된 프리셋 재검증을 실행하세요.", "확인");
                return;
            }

            log.Clear();
            sceneObjectNameTokenCache = null;
            AdoptGenreFromActiveScene();
            AdoptChangedPresetStateFromScene();
            NormalizeBlockPlan();
            EnsureFolders();

            var registry = EnsureRegistry();

            Undo.SetCurrentGroupName("VARCO Changed Preset Revalidate");
            var undoGroup = Undo.GetCurrentGroup();

            EnsureGameManagerAndProfile();
            EnsureSceneEssentialsForRecheck();
            if (blockHud || addModernHud)
                EnsureModernHud();
            if (runSafetyPass)
                VARCOBlockCodingBuilderWindow.RunSafetyPassForCurrentScene(log, saveScene: false);

            EnsureSinglePlayerControllerForGenre();
            NormalizeRuntimeGroundAlignForActiveCharacters();
            ApplyRecipeCombatTuningToEnemies();
            EnsureGameManagerProfileMatchesGenre();
            OptimizeGeneratedLayoutForPerformance();
            OptimizeSceneLightingForEditorPerformance();
            EnsurePlayableNavMesh();

            if (addSceneToBuild)
                AddActiveSceneToBuildSettings();

            AppendChangedPresetRevalidationSummary();
            AppendValidationReport(registry);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene && !string.IsNullOrWhiteSpace(scene.path))
                SaveActiveScene();
            else if (saveScene)
                log.Add("저장 안내: 현재 씬 경로가 없어 자동 저장은 건너뛰었습니다. 씬을 먼저 저장하면 다음 재검증부터 자동 저장됩니다.");

            log.Add("변경된 프리셋 재검증 완료: 현재 배치를 유지한 채 검증, 안전 보정, NavMesh 재베이크를 수행했습니다.");
            SaveOneClickReport("ChangedPresetRecheck");
            AssetDatabase.SaveAssets();
            editorSummaryCacheExpiresAt = 0;
            Undo.CollapseUndoOperations(undoGroup);
        }

        void AdoptGenreFromActiveScene()
        {
            var gm = FindFirstObjectByType<VWS.GameManager>();
            if (gm && gm.profile)
            {
                genre = gm.profile.genre;
                return;
            }

            var hud = FindFirstObjectByType<VWS.VARCOGameHUD>();
            if (hud)
                genre = hud.fallbackGenre;
        }

        void AdoptChangedPresetStateFromScene()
        {
            genre = InferGenreFromCurrentScene();

            var sceneItemCount = CountSceneComponentsIncludingInactive<VWS.ItemPickup>();
            var sceneEnemyCount = CountSceneComponentsIncludingInactive<VWS.EnemyHealth>();
            var waveEnemyTotal = CountWaveEnemyTotalFromScene();

            blockPlayer = GameObject.FindGameObjectWithTag("Player") != null
                || HasSceneComponent<VWS.PlayerHealth>()
                || HasSceneComponent<VWS.PlayerController_ThirdPerson>()
                || HasSceneComponent<VWS.PlayerController_Platform>();
            blockWeapon = HasSceneComponent<VWS.PlayerAttack>() || HasSceneObjectNameToken("weapon", "sword", "gun", "blade");
            blockEnemyWave = HasSceneComponent<VWS.WaveManager>() || HasSceneComponent<VWS.EnemyAI_NavMesh>() || sceneEnemyCount > 0;
            blockItems = sceneItemCount > 0;
            blockGoal = HasSceneComponent<VWS.GoalTrigger>() || HasSceneComponent<VWS.PlatformGoal>() || HasSceneComponent<VWS.PuzzleGoal>();
            blockHealthPickup = HasSceneComponent<VWS.HealthPickup>();
            blockHazard = HasSceneComponent<VWS.HazardZone>();
            blockCheckpoint = HasSceneComponent<VWS.Checkpoint>();
            blockFallRespawn = HasSceneComponent<VWS.DeathZone>();
            blockMovingPlatform = HasSceneComponent<VWS.MovingPlatform>();
            blockPuzzleDoor = HasSceneComponent<VWS.DoorController>() || HasSceneComponent<VWS.PressurePlate>();
            blockMovableBox = HasSceneComponent<VWS.MovableBox>();
            blockCover = HasSceneObjectNameToken("cover", "wall", "pillar", "rock", "barricade", "obstacle");
            blockCountdown = HasSceneComponent<VWS.CountdownTimer>();
            blockHud = blockHud || HasSceneComponent<VWS.VARCOGameHUD>();
            blockVisuals = HasSceneComponent<Volume>();
            blockSound = HasSceneComponent<VWS.SoundEventEmitter>()
                || HasSceneComponent<VWS.SoundEventTrigger>()
                || HasSceneComponent<AudioSource>()
                || blockSound;

            itemGoal = blockItems ? Mathf.Clamp(Mathf.Max(1, Mathf.Max(sceneItemCount, MaxRequiredItemsFromGoals())), 1, 12) : 0;
            if (blockEnemyWave)
                waveEnemyCount = Mathf.Clamp(Mathf.Max(1, Mathf.Max(sceneEnemyCount, waveEnemyTotal)), 1, 12);

            recipe = InferRecipeFromCurrentScene();
            blockTemplate = TemplateFor(genre, recipe);
            lastAutoDesignSummary = "현재 씬 재인식: " + GenreLabel(genre)
                + " / " + GameRecipeLabel(recipe)
                + " / " + ActiveBlockSummary();
            log.Add(lastAutoDesignSummary);
        }

        VWS.GenreType InferGenreFromCurrentScene()
        {
            var gm = FindFirstObjectByType<VWS.GameManager>();
            if (gm && gm.profile)
                return gm.profile.genre;

            var hud = FindFirstObjectByType<VWS.VARCOGameHUD>();
            if (hud)
                return hud.fallbackGenre;

            if (HasSceneComponent<VWS.MovingPlatform>() || HasSceneComponent<VWS.DeathZone>() || HasSceneComponent<VWS.PlatformGoal>() || HasSceneObjectNameToken("platform", "jump", "course"))
                return VWS.GenreType.Platform;

            if (HasSceneComponent<VWS.DoorController>() || HasSceneComponent<VWS.PressurePlate>() || HasSceneComponent<VWS.MovableBox>() || HasSceneComponent<VWS.PuzzleGoal>() || HasSceneObjectNameToken("puzzle", "door", "switch"))
                return VWS.GenreType.Puzzle;

            var hasEnemies = HasSceneComponent<VWS.WaveManager>() || HasSceneComponent<VWS.EnemyAI_NavMesh>() || HasSceneComponent<VWS.EnemyHealth>();
            var hasExplorationFlow = HasSceneComponent<VWS.ItemPickup>() || HasSceneComponent<VWS.Checkpoint>() || HasSceneObjectNameToken("quest", "exploration", "treasure", "zombie");
            if (hasExplorationFlow)
                return VWS.GenreType.Exploration;

            return hasEnemies ? VWS.GenreType.Arena : genre;
        }

        GameRecipe InferRecipeFromCurrentScene()
        {
            var itemCount = CountSceneComponentsIncludingInactive<VWS.ItemPickup>();
            var hasEnemies = HasSceneComponent<VWS.WaveManager>() || HasSceneComponent<VWS.EnemyAI_NavMesh>() || HasSceneComponent<VWS.EnemyHealth>();
            var hasTimer = HasSceneComponent<VWS.CountdownTimer>();
            var hasHazard = HasSceneComponent<VWS.HazardZone>();
            var hasBossName = HasSceneObjectNameToken("boss", "elite", "king");
            var hasZombieName = HasSceneObjectNameToken("zombie", "undead");

            if (genre == VWS.GenreType.Puzzle)
                return itemCount > 0 || HasSceneComponent<VWS.MovableBox>() ? GameRecipe.EscapeRoom : GameRecipe.DoorPuzzle;

            if (genre == VWS.GenreType.Platform)
                return hasHazard || hasTimer || itemCount >= 4 ? GameRecipe.ObstacleRun : GameRecipe.PlatformCourse;

            if (genre == VWS.GenreType.Exploration)
            {
                if (hasEnemies && (hasTimer || hasZombieName) && itemCount == 0)
                    return GameRecipe.ZombieSurvival;
                if (!hasEnemies && itemCount >= 5)
                    return GameRecipe.TreasureHunt;
                if (!hasEnemies && itemCount > 0)
                    return GameRecipe.CollectAndEscape;
                return GameRecipe.ExplorationQuest;
            }

            if (hasBossName)
                return GameRecipe.BossBattle;
            if (hasEnemies && hasTimer && hasHazard)
                return GameRecipe.SurvivalTimer;
            if (hasEnemies)
                return GameRecipe.CombatWave;
            if (itemCount > 0)
                return GameRecipe.CollectAndEscape;

            return GameRecipe.CombatWave;
        }

        void EnsureSceneEssentialsForRecheck()
        {
            EnsureTag("Player");

            var camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (!camera)
            {
                var go = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(go, "VARCO recheck camera");
                go.tag = "MainCamera";
                go.transform.position = new Vector3(0f, 8f, -10f);
                go.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
                log.Add("재검증 보정: 메인 카메라가 없어 기본 카메라를 추가했습니다.");
            }
            else
            {
                Undo.RecordObject(camera.gameObject, "VARCO recheck camera tag");
                camera.tag = "MainCamera";
                if (!camera.GetComponent<AudioListener>())
                    Undo.AddComponent<AudioListener>(camera.gameObject);
                EditorUtility.SetDirty(camera.gameObject);
            }

            var light = FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(item => item && item.type == LightType.Directional);
            if (!light)
            {
                var go = new GameObject("Directional Light");
                Undo.RegisterCreatedObjectUndo(go, "VARCO recheck light");
                light = go.AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
                light.intensity = 1.15f;
                log.Add("재검증 보정: Directional Light가 없어 기본 조명을 추가했습니다.");
            }

            if (!HasGroundLikeObject())
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(ground, "VARCO recheck ground");
                ground.name = "VARCO_Ground";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = genre == VWS.GenreType.Platform ? new Vector3(8f, 0.35f, 8f) : new Vector3(18f, 0.3f, 18f);
                SetColor(ground, new Color(0.25f, 0.3f, 0.34f));
                ground.isStatic = true;
                log.Add("재검증 보정: 바닥 후보가 없어 기본 바닥을 추가했습니다.");
            }
        }

        void AppendChangedPresetRevalidationSummary()
        {
            log.Add("변경 프리셋 재검증 요약");
            log.Add(StateLabel("PASS") + ": 현재 씬 기준 " + GenreLabel(genre) + " / " + GameRecipeLabel(recipe) + " / " + BlockTemplateLabel());

            AddValidationLine("Player 태그 또는 플레이어 컴포넌트", FindPlayerObjectForControllerCleanup());
            AddValidationLine("메인 카메라", Camera.main != null ? Camera.main.gameObject : null);
            AddValidationLine("VARCO HUD", FindFirstObjectByType<VWS.VARCOGameHUD>());

            var needsNavMesh = blockEnemyWave || CountSceneComponentsIncludingInactive<NavMeshAgent>() > 0;
            if (needsNavMesh)
            {
                var triangulation = NavMesh.CalculateTriangulation();
                var vertexCount = triangulation.vertices == null ? 0 : triangulation.vertices.Length;
                log.Add(CheckLabel(vertexCount > 0) + ": NavMesh 재베이크 결과 정점 " + vertexCount + "개");

                foreach (var wave in FindObjectsByType<VWS.WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (!wave)
                        continue;
                    log.Add(CheckLabel(wave.randomSpawnArea != null) + ": WaveManager 랜덤 스폰 영역 - " + wave.name);
                    log.Add(CheckLabel(wave.waves != null && wave.waves.Length > 0) + ": WaveManager 웨이브 데이터 - " + wave.name);
                }
            }
            else
            {
                log.Add(StateLabel("PASS") + ": NavMesh 대상 AI가 없어 재베이크 검증을 생략했습니다.");
            }
        }

        static bool HasSceneComponent<T>() where T : Object
        {
            return CountSceneComponentsIncludingInactive<T>() > 0;
        }

        static int CountSceneComponentsIncludingInactive<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        int MaxRequiredItemsFromGoals()
        {
            var max = 0;
            foreach (var goal in FindObjectsByType<VWS.GoalTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (goal)
                    max = Mathf.Max(max, goal.requiredItems);
            }

            return max;
        }

        int CountWaveEnemyTotalFromScene()
        {
            var total = 0;
            foreach (var wave in FindObjectsByType<VWS.WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!wave || wave.waves == null)
                    continue;

                foreach (var data in wave.waves)
                {
                    if (data != null)
                        total += Mathf.Max(0, data.enemyCount);
                }
            }

            return total;
        }

        bool HasSceneObjectNameToken(params string[] tokens)
        {
            if (string.IsNullOrEmpty(sceneObjectNameTokenCache))
            {
                var names = new List<string>();
                foreach (var transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (transform)
                        names.Add(transform.name);
                }

                sceneObjectNameTokenCache = Normalize(string.Join(" ", names));
            }

            return ContainsAny(sceneObjectNameTokenCache, tokens);
        }

        void GenerateReadinessReport()
        {
            log.Clear();
            ScanAssets();
            NormalizeBlockPlan();
            log.Add("준비 상태 리포트를 만들었습니다. 씬 오브젝트는 생성하거나 연결하지 않았습니다.");
            var registry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(RegistryPath);
            AppendValidationReport(registry);
            SaveOneClickReport("Readiness");
        }

        void GenerateAcceptanceChecklistReport()
        {
            log.Clear();
            ScanAssets();
            NormalizeBlockPlan();
            log.Add("완성 체크리스트를 만들었습니다. 씬은 변경하지 않았습니다.");

            var scene = SceneManager.GetActiveScene();
            var sceneName = string.IsNullOrWhiteSpace(scene.name) ? "UnsavedScene" : scene.name;
            var path = ReportFolder + "/VARCO_AcceptanceChecklist_" + genre + "_" + SafeFileName(sceneName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".md";
            SaveReportFile(path, BuildAcceptanceChecklistLines("완성 체크리스트"));
        }

        void GeneratePlayReadyReport()
        {
            log.Clear();
            ScanAssets();
            NormalizeBlockPlan();
            log.Add("플레이 준비 검사를 실행했습니다. 씬은 변경하지 않았습니다.");
            var registry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(RegistryPath);
            AppendValidationReport(registry);
            SaveOneClickReport("PlayReady");
        }

        void GenerateBeginnerPlayGuide()
        {
            log.Clear();
            ScanAssets();
            NormalizeBlockPlan();
            log.Add("초보자 플레이 설명서를 만들었습니다. 씬은 변경하지 않았습니다.");

            var scene = SceneManager.GetActiveScene();
            var sceneName = string.IsNullOrWhiteSpace(scene.name) ? "UnsavedScene" : scene.name;
            var path = ReportFolder + "/VARCO_BeginnerPlayGuide_" + genre + "_" + SafeFileName(sceneName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".md";
            SaveReportFile(path, BuildBeginnerPlayGuideLines());
        }

        void GenerateKoreanUxReport()
        {
            log.Clear();
            ScanAssets();
            NormalizeBlockPlan();
            log.Add("한글 UX 점검 리포트를 만들었습니다. 씬은 변경하지 않았습니다.");

            var scene = SceneManager.GetActiveScene();
            var sceneName = string.IsNullOrWhiteSpace(scene.name) ? "UnsavedScene" : scene.name;
            var path = ReportFolder + "/VARCO_KoreanUX_" + genre + "_" + SafeFileName(sceneName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".md";
            SaveReportFile(path, BuildKoreanUxReportLines());
        }

        void GenerateProjectManual()
        {
            log.Clear();
            ScanAssets();
            NormalizeBlockPlan();
            log.Add("프로젝트 사용 매뉴얼을 만들었습니다. 씬은 변경하지 않았습니다.");

            var scene = SceneManager.GetActiveScene();
            var sceneName = string.IsNullOrWhiteSpace(scene.name) ? "UnsavedScene" : scene.name;
            var path = ReportFolder + "/VARCO_ProjectManual_" + genre + "_" + SafeFileName(sceneName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".md";
            SaveReportFile(path, BuildProjectManualLines());
        }

        void GenerateAssetMatchingReport()
        {
            log.Clear();
            ScanAssets();
            NormalizeBlockPlan();
            log.Add("에셋 자동 매칭 진단서를 만들었습니다. 씬은 변경하지 않았습니다.");
            SaveOneClickReport("AssetMatching");
        }

        void GenerateAssetRequestSheet()
        {
            log.Clear();
            ScanAssets();
            NormalizeBlockPlan();
            log.Add("현재 노코드 블록 조합을 기준으로 VARCO 에셋 요청서를 만들었습니다.");

            var scene = SceneManager.GetActiveScene();
            var sceneName = string.IsNullOrWhiteSpace(scene.name) ? "UnsavedScene" : scene.name;
            var path = ReportFolder + "/VARCO_AssetRequests_" + genre + "_" + SafeFileName(sceneName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".md";
            SaveReportFile(path, BuildAssetRequestSheetLines());
        }

        void GenerateNoCodeRecipeCard()
        {
            log.Clear();
            ScanAssets();
            NormalizeBlockPlan();

            var scene = SceneManager.GetActiveScene();
            var sceneName = string.IsNullOrWhiteSpace(scene.name) ? "UnsavedScene" : scene.name;
            var path = ReportFolder + "/VARCO_NoCodeRecipe_" + genre + "_" + SafeFileName(sceneName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".md";
            SaveReportFile(path, BuildNoCodeRecipeLines());
        }

        void GeneratePresetReadinessMatrix()
        {
            var snapshot = CreateInstance<VARCOGameMakerPreset>();
            WritePreset(snapshot);
            var originalPreset = selectedPreset;
            var originalDesignSummary = lastAutoDesignSummary;
            var originalLog = log.ToList();
            string reportPath = null;

            try
            {
                GenerateStarterPresets();
                var presets = FindGameMakerPresets().ToList();
                var registry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(RegistryPath);
                var lines = new List<string>
                {
                    "# VARCO 게임 메이커 프리셋 준비도 표",
                    "",
                    "- 생성 시간: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    "- 씬: " + SceneLabel(),
                    "- 프리셋 수: " + presets.Count,
                    "",
                    "| 프리셋 | 템플릿 | 장르 | 게임 방식 | 점수 | 블록 | 통과 | 확인 | 기본 생성 |",
                    "| --- | --- | --- | --- | ---: | --- | ---: | ---: | ---: |"
                };
                var details = new List<string>();

                foreach (var preset in presets)
                {
                    var readiness = EvaluatePresetReadiness(preset);
                    selectedPreset = preset;
                    ReadPreset(preset);
                    log.Clear();
                    ScanAssets();
                    NormalizeBlockPlan();
                    log.Add("프리셋 준비도를 계산했습니다. 씬 오브젝트는 생성하거나 연결하지 않았습니다.");
                    AppendValidationReport(registry);

                    var passCount = CountLogPrefix("PASS") + CountLogPrefix("ASSET");
                    var warnCount = CountLogPrefix("WARN");
                    var fallbackCount = CountLogPrefix("FALLBACK");
                    lines.Add("| " + MarkdownCell(preset.name)
                        + " | " + MarkdownCell(BlockTemplateLabel())
                        + " | " + MarkdownCell(GenreLabel(genre))
                        + " | " + MarkdownCell(GameRecipeLabel(recipe))
                        + " | " + readiness.score
                        + " | " + MarkdownCell(ActiveBlockSummary())
                        + " | " + passCount
                        + " | " + warnCount
                        + " | " + fallbackCount
                        + " |");

                    details.Add("");
                    details.Add("## " + preset.name);
                    details.Add("- 경로: " + AssetDatabase.GetAssetPath(preset));
                    details.Add("- 장르: " + GenreLabel(genre));
                    details.Add("- 템플릿: " + BlockTemplateLabel());
                    details.Add("- 게임 방식: " + GameRecipeLabel(recipe));
                    details.Add("- 블록: " + ActiveBlockSummary());
                    details.Add("- 플레이어: " + PlayerCharacterLabel());
                    details.Add("- 적: " + EnemyCharacterLabel());
                    details.Add("");
                    details.Add("### 에셋 매칭");
                    foreach (var role in ActiveRolesForCurrentBlocks())
                    {
                        var slot = BuildAssetSlotStatus(role);
                        details.Add("- " + StateLabel(slot.state) + " " + AssetRoleLabel(role) + ": " + slot.message);
                    }
                    details.Add("");
                    details.Add("### 검증 로그");
                    foreach (var entry in log)
                        details.Add("- " + entry);
                }

                lines.AddRange(details);
                reportPath = ReportFolder + "/VARCO_PresetReadinessMatrix_" + SafeFileName(SceneManager.GetActiveScene().name) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".md";
                SaveReportFile(reportPath, lines);
            }
            finally
            {
                ReadPreset(snapshot);
                DestroyImmediate(snapshot);
                selectedPreset = originalPreset;
                lastAutoDesignSummary = originalDesignSummary;
                log.Clear();
                log.AddRange(originalLog);
                log.Add(string.IsNullOrWhiteSpace(reportPath)
                    ? "프리셋 준비도 표 생성을 마쳤습니다."
                    : "프리셋 준비도 표 저장됨: " + reportPath);
            }
        }

        void SaveOneClickReport(string reportKind)
        {
            EnsureFolder(ReportFolder);
            var scene = SceneManager.GetActiveScene();
            var sceneName = string.IsNullOrWhiteSpace(scene.name) ? "UnsavedScene" : scene.name;
            var fileKind = reportKind == "OneClick" ? "AutoBuild" : reportKind;
            var path = ReportFolder + "/VARCO_" + SafeFileName(fileKind) + "_" + genre + "_" + SafeFileName(sceneName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".md";
            SaveReportFile(path, BuildReportLines(reportKind));
        }

        void SaveReportFile(string path, IEnumerable<string> lines)
        {
            EnsureFolder(ReportFolder);
            File.WriteAllLines(path, lines);
            AssetDatabase.ImportAsset(path);
            lastReportPath = path;
            log.Add("리포트 저장됨: " + path);
        }

        static string ReportKindLabel(string reportKind)
        {
            switch (reportKind)
            {
                case "OneClick":
                    return "자동 제작";
                case "FixAll":
                    return "현재 씬 자동 보정";
                case "Readiness":
                    return "준비 상태 점검";
                case "PlayReady":
                    return "플레이 준비 자동 검사";
                case "BuildPreflight":
                    return "Windows 빌드 전 점검";
                case "BuildWindows":
                    return "Windows EXE 빌드";
                case "AssetMatching":
                    return "에셋 자동 매칭 진단";
                default:
                    return reportKind;
            }
        }

        static string BoolLabel(bool value)
        {
            return value ? "예" : "아니오";
        }

        static string StateLabel(string state)
        {
            switch (state)
            {
                case "PASS":
                    return "통과";
                case "WARN":
                    return "확인 필요";
                case "FAIL":
                    return "실패";
                case "FALLBACK":
                    return "기본 생성";
                default:
                    return state;
            }
        }

        static string CheckLabel(bool passed)
        {
            return passed ? StateLabel("PASS") : StateLabel("WARN");
        }

        static string CompletionConditionLabel(VWS.CompletionCondition condition)
        {
            switch (condition)
            {
                case VWS.CompletionCondition.DefeatWaves:
                    return "적 웨이브 처치";
                case VWS.CompletionCondition.CollectItems:
                    return "아이템 수집";
                case VWS.CompletionCondition.ReachGoal:
                    return "목표 지점 도달";
                default:
                    return condition.ToString();
            }
        }

        List<string> BuildReportLines(string reportKind)
        {
            var scene = SceneManager.GetActiveScene();
            var acceptance = BuildAcceptanceChecklist();
            var lines = new List<string>
            {
                "# VARCO 게임 메이커 리포트",
                "",
                "- 종류: " + ReportKindLabel(reportKind),
                "- 생성 시간: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "- 씬: " + (string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path),
                "- 장르: " + GenreLabel(genre),
                "- 블록 템플릿: " + BlockTemplateLabel(),
                "- 게임 방식: " + GameRecipeLabel(recipe),
                "- 프리셋: " + PresetLabel(),
                "- 설계 기준: " + DesignSourceLabel(),
                "- 난이도: " + DifficultyLabel(),
                "- 카메라: " + CameraPresetLabel(),
                "- 이동 방식: " + PlayerMovementLabel(),
                "- 플레이어 캐릭터: " + PlayerCharacterLabel(),
                "- 적 캐릭터: " + EnemyCharacterLabel(),
                "- 에셋 스캔 범위: " + ScanRootSummary(GameObjectScanRoots()),
                "- 블록: " + ActiveBlockSummary(),
                "- 완성 점검: " + AcceptanceSummary(acceptance)
            };

            AppendGuidedNextActionSection(lines);
            AppendBlockAssemblyDetails(lines);
            AppendWindowsBuildReadinessDetails(lines);
            AppendAssetMatchingGuideSection(lines);

            lines.Add("");
            lines.Add("## 에셋 매칭");
            foreach (var role in ActiveRolesForCurrentBlocks())
            {
                var slot = BuildAssetSlotStatus(role);
                lines.Add("- " + StateLabel(slot.state) + " " + AssetRoleLabel(role) + ": " + slot.message);
            }
            AppendDetectedAssetDetails(lines);
            AppendSoundAssetDetails(lines);
            AppendAnimationAssetDetails(lines);

            AppendAcceptanceSection(lines, acceptance);

            lines.Add("");
            lines.Add("## 검증 로그");
            foreach (var entry in log)
                lines.Add("- " + entry);

            return lines;
        }

        List<string> BuildNoCodeRecipeLines()
        {
            var roles = ActiveRolesForCurrentBlocks().Distinct().ToList();
            var slots = roles.Select(BuildAssetSlotStatus).ToList();
            var acceptance = BuildAcceptanceChecklist();
            var lines = new List<string>
            {
                "# VARCO 노코드 블록 레시피",
                "",
                "- 생성 시간: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "- 씬: " + SceneLabel(),
                "- 장르: " + GenreLabel(genre),
                "- 블록 템플릿: " + BlockTemplateLabel(),
                "- 게임 방식: " + GameRecipeLabel(recipe),
                "- 프리셋: " + PresetLabel(),
                "- 설계 기준: " + DesignSourceLabel(),
                "- 난이도: " + DifficultyLabel(),
                "- 카메라: " + CameraPresetLabel(),
                "- 이동 방식: " + PlayerMovementLabel(),
                "- 클리어 조건: " + CompletionConditionLabel(PrimaryClearCondition()),
                "- 에셋 준비도: " + AssetSlotSummary(),
                "- 에셋 스캔 범위: " + ScanRootSummary(GameObjectScanRoots()),
                "- 완성 점검: " + AcceptanceSummary(acceptance),
                "- 블록: " + ActiveBlockSummary()
            };

            AppendGuidedNextActionSection(lines);
            AppendBlockAssemblyDetails(lines);
            AppendWindowsBuildReadinessDetails(lines);
            AppendAssetMatchingGuideSection(lines);

            lines.Add("");
            lines.Add("## 블록 단계");
            foreach (var step in BuildNoCodeRecipeSteps())
                lines.Add("- " + step);

            lines.Add("");
            lines.Add("## 에셋 연결");
            foreach (var slot in slots)
            {
                lines.Add("- " + StateLabel(slot.state) + " " + AssetRoleLabel(slot.role) + ": " + slot.message);
            }
            AppendDetectedAssetDetails(lines);
            AppendSoundAssetDetails(lines);
            AppendAnimationAssetDetails(lines);

            AppendAcceptanceSection(lines, acceptance);

            lines.Add("");
            lines.Add("## 자동 제작 설정");
            lines.Add("- 부족한 오브젝트 자동 생성: " + BoolLabel(createMissingObjects));
            lines.Add("- 감지된 프리팹/모델 자동 연결: " + BoolLabel(autoConnectPrefabs));
            lines.Add("- 애니메이션 컨트롤러 자동 생성/연결: " + BoolLabel(blockPlayer && autoAnimations));
            lines.Add("- 사운드/BGM 자동 연결: " + BoolLabel(blockSound && autoSounds));
            lines.Add("- 게임 HUD: " + BoolLabel(blockHud && addModernHud));
            lines.Add("- 비주얼 프리셋: " + BoolLabel(blockVisuals && applyVisualPreset));
            lines.Add("- 안전 보정: " + BoolLabel(runSafetyPass));
            lines.Add("- 현재 씬을 빌드 설정에 추가: " + BoolLabel(addSceneToBuild));
            lines.Add("- 제작 후 씬 저장: " + BoolLabel(saveScene));

            lines.Add("");
            lines.Add("## 사용 흐름");
            lines.Add("1. `VARCO/프리셋 만들기` 또는 `VARCO/레거시/세부 자동 제작/장르별`, `VARCO/레거시/세부 자동 제작/게임 프리셋`에서 원하는 제작 메뉴를 실행합니다.");
            lines.Add("2. Unity에서 Play를 눌러 테스트합니다.");
            lines.Add("3. 문제가 보이면 `VARCO/레거시/세부 자동 제작/현재 씬 자동 보정`을 실행합니다.");
            lines.Add("4. 필요한 프리팹만 수정하려면 `VARCO/레거시/세부 자동 제작/블록코딩/선택 프리팹에 기능 추가`를 사용합니다.");

            return lines;
        }

        List<string> BuildAcceptanceChecklistLines(string reportKind)
        {
            var findings = BuildAcceptanceChecklist();
            var scene = SceneManager.GetActiveScene();
            var lines = new List<string>
            {
                "# VARCO 게임 메이커 완성 체크리스트",
                "",
                "- 종류: " + reportKind,
                "- 생성 시간: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "- 씬: " + (string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path),
                "- 장르: " + GenreLabel(genre),
                "- 블록 템플릿: " + BlockTemplateLabel(),
                "- 게임 방식: " + GameRecipeLabel(recipe),
                "- 프리셋: " + PresetLabel(),
                "- 블록: " + ActiveBlockSummary(),
                "- 완성 점검: " + AcceptanceSummary(findings)
            };

            AppendGuidedNextActionSection(lines);
            AppendAcceptanceSection(lines, findings);
            return lines;
        }

        List<string> BuildProjectManualLines()
        {
            var scene = SceneManager.GetActiveScene();
            var acceptance = BuildAcceptanceChecklist();
            var roles = ActiveRolesForCurrentBlocks().Distinct().ToList();
            var slots = roles.Select(BuildAssetSlotStatus).ToList();
            var readyAssetCount = slots.Count(slot => slot.state == "PASS" || slot.state == "WARN");
            var fallbackAssetCount = slots.Count(slot => slot.state == "FALLBACK");

            var lines = new List<string>
            {
                "# VARCO 프로젝트 사용 매뉴얼",
                "",
                "- 생성 시간: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "- 현재 씬: " + (string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path),
                "- 현재 추천 장르: " + GenreLabel(genre),
                "- 현재 블록 템플릿: " + BlockTemplateLabel(),
                "- 에셋 직접 매칭: " + readyAssetCount + " / " + Mathf.Max(1, roles.Count),
                "- 기본 생성 대상: " + fallbackAssetCount,
                "- 완성 점검: " + AcceptanceSummary(acceptance),
                "",
                "## 가장 쉬운 사용 순서",
                "1. Unity 상단 메뉴에서 `VARCO/프리셋 만들기` 또는 `VARCO/레거시/세부 자동 제작/장르별`, `VARCO/레거시/세부 자동 제작/게임 프리셋` 중 하나를 선택합니다.",
                "2. 생성된 씬에서 Unity Play 버튼을 눌러 테스트합니다.",
                "3. HUD의 `목표`, `다음 행동`, `체력`, `시간` 안내를 따라 진행합니다.",
                "4. 문제가 보이면 Play 모드를 끄고 `VARCO/레거시/세부 자동 제작/현재 씬 자동 보정`을 실행합니다.",
                "5. 특정 프리팹에 기능만 추가하려면 프리팹을 선택한 뒤 `VARCO/레거시/세부 자동 제작/블록코딩/선택 프리팹에 기능 추가`를 실행합니다.",
                "",
                "## VarcoAI 에셋을 추가한 뒤",
                "1. VarcoAI에서 만든 프리팹, 모델, 애니메이션, 사운드를 프로젝트의 `Assets` 폴더 아래에 넣습니다.",
                "2. `VARCO/레거시/세부 자동 제작/에셋 자동 매칭 진단서`로 현재 에셋이 어떤 역할에 맞는지 확인합니다.",
                "3. 부족한 역할이 보이면 `VARCO/레거시/세부 자동 제작/추천 게임 기준 부족한 에셋 요청서`를 생성합니다.",
                "4. 선택한 모델만 기능으로 바꾸고 싶으면 `VARCO/레거시/세부 자동 제작/블록코딩/선택 모델들 자동 판단 연결` 또는 `VARCO/레거시/세부 자동 제작/블록코딩/선택 프리팹에 기능 추가`를 사용합니다.",
                "",
                "## 한글 블록의 의미",
                "- 플레이어: 사용자가 조작하는 캐릭터와 카메라를 만듭니다.",
                "- 적 웨이브: 좀비, 보스, 오크, 드론 같은 적을 생성하고 처치 목표를 연결합니다.",
                "- 수집 아이템: 아이템을 먹으면 HUD 진행도가 올라갑니다.",
                "- 목표 지점: 목표에 도착하면 클리어됩니다.",
                "- 체크포인트: 통과하면 이후 리스폰 위치가 그 지점으로 바뀝니다.",
                "- 낙사 안전망: 맵 아래로 떨어지면 마지막 체크포인트 또는 시작 지점으로 되돌립니다.",
                "- 퍼즐 장치: 문, 압력판, 밀 수 있는 상자를 연결합니다.",
                "- HUD: 체력, 목표, 진행도, 시간, 다음 행동을 한글로 표시합니다.",
                "- 사운드/BGM: 장르와 오브젝트 상황에 맞는 효과음과 배경음을 연결합니다.",
                "- 애니메이션: 플레이어와 적의 대기, 이동, 공격, 피격 애니메이션을 자동 연결합니다.",
                "",
                "## 현재 프로젝트에서 바로 확인할 것"
            };

            foreach (var slot in slots.Take(12))
                lines.Add("- " + StateLabel(slot.state) + " " + AssetRoleLabel(slot.role) + ": " + slot.message);

            lines.Add("");
            lines.Add("## 문제가 생겼을 때");
            lines.Add("1. Play 모드가 켜져 있으면 끄고 다시 실행합니다.");
            lines.Add("2. `VARCO/레거시/세부 자동 제작/현재 씬 자동 보정`을 실행합니다.");
            lines.Add("3. `VARCO/레거시/세부 자동 제작/검증/현재 씬 건강 검사`로 실패 항목을 확인합니다.");
            lines.Add("4. 에셋이 부족하면 `VARCO/레거시/세부 자동 제작/추천 게임 기준 부족한 에셋 요청서`를 보고 VarcoAI에서 필요한 에셋을 만듭니다.");
            lines.Add("5. 특정 오브젝트 기능만 빠졌다면 `VARCO/레거시/세부 자동 제작/블록코딩/선택 프리팹에 기능 추가`를 사용합니다.");

            AppendGuidedNextActionSection(lines);
            AppendAcceptanceSection(lines, acceptance);
            return lines;
        }

        List<string> BuildBeginnerPlayGuideLines()
        {
            var acceptance = BuildAcceptanceChecklist();
            var lines = new List<string>
            {
                "# VARCO 초보자 플레이 설명서",
                "",
                "- 생성 시간: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "- 씬: " + SceneLabel(),
                "- 게임: " + GenreLabel(genre) + " / " + GameRecipeLabel(recipe),
                "- 블록 템플릿: " + BlockTemplateLabel(),
                "- 클리어 조건: " + CompletionConditionLabel(PrimaryClearCondition()),
                "- 완성 점검: " + AcceptanceSummary(acceptance),
                "- 블록: " + ActiveBlockSummary(),
                "",
                "## 바로 플레이하기",
                "1. Unity 상단의 Play 버튼을 누릅니다.",
                "2. 화면에 플레이어가 보이면 아래 조작으로 움직입니다.",
                "3. 목표를 달성하면 HUD와 GameManager가 클리어 상태를 표시합니다.",
                "4. 이상하면 Play 모드를 끄고 `VARCO/레거시/세부 자동 제작/현재 씬 자동 보정`을 실행합니다.",
                "",
                "## 조작",
            };

            foreach (var line in BuildBeginnerControlLines())
                lines.Add("- " + line);

            lines.Add("");
            lines.Add("## 목표");
            foreach (var line in BuildBeginnerGoalLines())
                lines.Add("- " + line);

            lines.Add("");
            lines.Add("## 플레이 흐름");
            var flowIndex = 1;
            foreach (var line in BuildBeginnerPlayFlowLines())
                lines.Add((flowIndex++) + ". " + line);

            lines.Add("");
            lines.Add("## 현재 씬에 들어간 기능");
            foreach (var line in BuildBeginnerSceneStatusLines())
                lines.Add("- " + line);

            lines.Add("");
            lines.Add("## 블록 설명");
            foreach (var step in BuildNoCodeRecipeSteps())
                lines.Add("- " + step);

            lines.Add("");
            lines.Add("## 막혔을 때");
            foreach (var line in BuildBeginnerTroubleshootingLines(acceptance))
                lines.Add("- " + line);

            AppendGuidedNextActionSection(lines);
            AppendAcceptanceSection(lines, acceptance);
            return lines;
        }

        IEnumerable<string> BuildBeginnerControlLines()
        {
            yield return "W/A/S/D 또는 방향키: 이동";
            yield return "마우스 이동: 카메라와 시점 회전";
            yield return "Esc: 마우스 잠금 해제";

            if (genre == VWS.GenreType.Platform || blockMovingPlatform || blockCheckpoint || blockFallRespawn)
                yield return "Space: 점프";

            if (blockEnemyWave || blockWeapon)
                yield return "마우스 왼쪽 버튼: 공격";

            if (blockHud)
                yield return "F10: VARCO HUD 보이기/숨기기";
        }

        IEnumerable<string> BuildBeginnerGoalLines()
        {
            switch (PrimaryClearCondition())
            {
                case VWS.CompletionCondition.DefeatWaves:
                    yield return "등장하는 적 웨이브를 모두 처치하면 클리어됩니다.";
                    yield return "체력이 낮아지면 회복 아이템이나 안전한 위치를 활용합니다.";
                    break;
                case VWS.CompletionCondition.CollectItems:
                    yield return "수집 아이템 " + Mathf.Max(1, itemGoal) + "개를 모읍니다.";
                    yield return "목표 지점이나 출구가 있으면 마지막에 그곳으로 이동합니다.";
                    break;
                case VWS.CompletionCondition.ReachGoal:
                    yield return "목표 지점, 출구, 포탈 또는 플랫폼 끝 지점에 도착하면 클리어됩니다.";
                    break;
            }

            if (blockPuzzleDoor)
                yield return "문이 막혀 있으면 발판/스위치/상자를 찾아 문을 엽니다.";
            if (blockCheckpoint)
                yield return "체크포인트를 지나면 다음 리스폰 위치가 그 지점으로 바뀝니다.";
            if (blockFallRespawn)
                yield return "맵 아래로 떨어지면 마지막 체크포인트 또는 시작 위치로 돌아옵니다.";
        }

        IEnumerable<string> BuildBeginnerPlayFlowLines()
        {
            yield return "Play를 누른 뒤 플레이어가 보이면 마우스로 시야를 맞춥니다.";
            yield return "W/A/S/D로 이동하면서 화면의 HUD에 표시되는 목표를 확인합니다.";

            switch (genre)
            {
                case VWS.GenreType.Arena:
                    yield return "적이 등장하면 거리를 조절하면서 공격하고, 모든 웨이브를 끝내는 것을 목표로 합니다.";
                    break;
                case VWS.GenreType.Exploration:
                    yield return "길을 따라 이동하며 수집 아이템, 문, 좀비, 목표 지점을 차례대로 확인합니다.";
                    break;
                case VWS.GenreType.Puzzle:
                    yield return "문이 막혀 있으면 발판, 스위치, 밀 수 있는 상자를 먼저 찾아봅니다.";
                    break;
                case VWS.GenreType.Platform:
                    yield return "발판을 따라 이동하고 체크포인트를 지나 다음 리스폰 위치를 확보합니다.";
                    break;
            }

            if (blockCheckpoint)
                yield return "체크포인트를 지나면 이후 낙사나 사망 때 그 위치에서 다시 시작하는지 확인합니다.";
            if (blockSound)
                yield return "아이템 획득, 공격, 클리어 같은 행동에 효과음이나 BGM이 연결됐는지 확인합니다.";
            if (blockVisuals)
                yield return "조명과 카메라 분위기가 장르에 맞는지 Scene/Game 뷰에서 확인합니다.";
        }

        IEnumerable<string> BuildBeginnerSceneStatusLines()
        {
            yield return "플레이어: " + (CountSceneComponents<VWS.PlayerController_ThirdPerson>() + CountSceneComponents<VWS.PlayerController_Platform>()) + "개";
            yield return "적/전투: 적 " + CountSceneComponents<VWS.EnemyHealth>() + "개, 웨이브 매니저 " + CountSceneComponents<VWS.WaveManager>() + "개";
            yield return "수집 아이템: " + CountSceneComponents<VWS.ItemPickup>() + "개";
            yield return "목표/클리어 지점: " + (CountSceneComponents<VWS.GoalTrigger>() + CountSceneComponents<VWS.PlatformGoal>() + CountSceneComponents<VWS.PuzzleGoal>()) + "개";
            yield return "체크포인트: " + CountSceneComponents<VWS.Checkpoint>() + "개";
            yield return "낙사 안전망: " + CountSceneComponents<VWS.DeathZone>() + "개";
            yield return "문/발판/상자: " + (CountSceneComponents<VWS.DoorController>() + CountSceneComponents<VWS.PressurePlate>() + CountSceneComponents<VWS.MovableBox>()) + "개";
            yield return "HUD: " + CountSceneComponents<VWS.VARCOGameHUD>() + "개";
            yield return "사운드 반응: " + (CountSceneComponents<VWS.SoundEventEmitter>() + CountSceneComponents<VWS.SoundEventTrigger>()) + "개";
        }

        IEnumerable<string> BuildBeginnerTroubleshootingLines(List<AcceptanceFinding> acceptance)
        {
            var issues = acceptance.Where(finding => finding.state != "PASS").Take(5).ToList();
            if (issues.Count == 0)
            {
                yield return "완성 체크가 통과 상태입니다. 바로 Play 테스트를 진행해도 됩니다.";
            }
            else
            {
                yield return "`VARCO/레거시/세부 자동 제작/현재 씬 자동 보정`을 실행하면 기본 구성과 안전 보정을 다시 적용합니다.";
                foreach (var issue in issues)
                    yield return issue.area + ": " + issue.message;
            }

            if (SceneNeedsOneClickBuild())
                yield return "`VARCO/프리셋 만들기` 또는 `VARCO/레거시/세부 자동 제작/장르별`, `VARCO/레거시/세부 자동 제작/게임 프리셋` 메뉴에서 현재 목표에 맞는 제작 메뉴를 다시 실행합니다.";

            yield return "플레이어가 안 움직이면 Play 모드를 끄고 `현재 씬 자동 보정`을 실행한 뒤 다시 Play 합니다.";
            yield return "에셋이 기본 큐브/캡슐로 보이면 `부족한 에셋 요청서`를 생성해서 VarcoAI에서 필요한 프리팹을 만들면 됩니다.";
        }

        List<string> BuildKoreanUxReportLines()
        {
            var gm = FindFirstObjectByType<VWS.GameManager>();
            var profile = gm ? gm.profile : null;
            var hud = FindFirstObjectByType<VWS.VARCOGameHUD>();
            var playReady = BuildPlayReadyChecklist();
            var koreanHudTextCount = profile ? CountKoreanHudTexts(profile) : 0;
            var lines = new List<string>
            {
                "# VARCO 한글 UX 점검 리포트",
                "",
                "- 생성 시간: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "- 씬: " + SceneLabel(),
                "- 장르: " + GenreLabel(genre),
                "- 게임 방식: " + GameRecipeLabel(recipe),
                "- 블록 템플릿: " + BlockTemplateLabel(),
                "- 클리어 조건: " + CompletionConditionLabel(PrimaryClearCondition()),
                "- 블록: " + ActiveBlockSummary(),
                "",
                "## 한눈에 보기",
                "",
                "| 항목 | 상태 | 확인 내용 |",
                "| --- | --- | --- |"
            };

            AddKoreanUxRow(lines, profile != null, "게임 프로필", profile ? "장르/목표/HUD 문구를 담은 GameProfile이 연결되어 있습니다." : "GameProfile이 없습니다. `현재 씬 자동 보정` 또는 `자동 제작 실행`을 사용하세요.");
            AddKoreanUxRow(lines, hud != null, "VARCO 게임 HUD", hud ? "Play 중 체력, 목표, 진행도, 다음 행동을 한글로 표시합니다." : "VARCOGameHUD가 없습니다. `현재 씬 자동 보정`을 실행하세요.");
            AddKoreanUxRow(lines, koreanHudTextCount >= 4, "HUD 한글 문구", "목표/조작/클리어/실패 문구 " + koreanHudTextCount + " / 4개");
            AddKoreanUxRow(lines, HasDisplayText(BuildHudObjectiveText(genre)), "목표 자동 문구", BuildHudObjectiveText(genre));
            AddKoreanUxRow(lines, HasDisplayText(BuildHudControlGuideText(genre)), "조작 자동 문구", BuildHudControlGuideText(genre));
            AddKoreanUxRow(lines, blockHud && addModernHud, "HUD 자동 생성 설정", blockHud && addModernHud ? "자동 제작 시 HUD를 추가합니다." : "HUD 블록 또는 HUD 자동 추가가 꺼져 있습니다.");
            AddKoreanUxRow(lines, !playReady.Any(f => f.area == "HUD" && f.state == "FAIL"), "플레이 전 HUD 점검", AcceptanceSummary(playReady.Where(f => f.area == "HUD").ToList()));

            lines.Add("");
            lines.Add("## Play 중 사용자가 보게 될 안내");
            lines.Add("");
            lines.Add("- 제목: VARCO " + GenreLabel(genre) + " | " + GameRecipeLabel(recipe));
            lines.Add("- 목표: " + BuildHudObjectiveText(genre));
            lines.Add("- 조작: " + BuildHudControlGuideText(genre));
            lines.Add("- 클리어: " + BuildHudClearMessage(genre));
            lines.Add("- 실패: " + BuildHudGameOverMessage());
            lines.Add("- 다음 행동 예시: " + BuildKoreanUxNextActionPreview());

            lines.Add("");
            lines.Add("## 초보자 흐름");
            lines.Add("");
            lines.Add("1. `VARCO/프리셋 만들기` 또는 `VARCO/레거시/세부 자동 제작/장르별`, `VARCO/레거시/세부 자동 제작/게임 프리셋`에서 원하는 제작 메뉴를 실행합니다.");
            lines.Add("2. `한글 UX 점검 리포트`와 `초보자 플레이 설명서`를 생성합니다.");
            lines.Add("3. 필요하면 `VARCO/레거시/세부 자동 제작/블록코딩/선택 프리팹에 기능 추가`로 특정 프리팹의 기능을 보강합니다.");
            lines.Add("4. Unity Play 버튼을 누르고 HUD의 `목표`, `다음 행동`, `체력`, `시간`을 따라갑니다.");
            lines.Add("5. 문제가 있으면 Play를 끄고 `현재 씬 자동 보정`을 누릅니다.");

            lines.Add("");
            lines.Add("## 플레이 준비 중 HUD 관련 항목");
            lines.Add("");
            foreach (var finding in playReady.Where(f => f.area == "HUD" || f.area == "플레이 준비" || f.area == "입력").Take(12))
                lines.Add("- " + StateLabel(finding.state) + " " + finding.area + ": " + finding.message);

            AppendGuidedNextActionSection(lines);
            return lines;
        }

        static void AddKoreanUxRow(List<string> lines, bool ok, string label, string detail)
        {
            lines.Add("| " + MarkdownCell(label) + " | " + (ok ? "통과" : "확인 필요") + " | " + MarkdownCell(detail) + " |");
        }

        string BuildKoreanUxNextActionPreview()
        {
            switch (PrimaryClearCondition())
            {
                case VWS.CompletionCondition.DefeatWaves:
                    return "남은 적을 찾아 공격하세요. 체력이 낮으면 회복 아이템을 먼저 찾으세요.";
                case VWS.CompletionCondition.CollectItems:
                    return "아이템 " + Mathf.Max(1, itemGoal) + "개를 모은 뒤 목표 지점이나 포탈로 이동하세요.";
                default:
                    if (blockPuzzleDoor)
                        return "발판, 스위치, 상자로 문을 열고 목표 지점으로 이동하세요.";
                    if (genre == VWS.GenreType.Platform)
                        return "발판을 건너 체크포인트를 저장하고 목표 지점으로 이동하세요.";
                    return "목표 지점, 출구, 포탈로 이동하세요.";
            }
        }

        List<string> BuildAssetRequestSheetLines()
        {
            var roles = ActiveRolesForCurrentBlocks().Distinct().ToList();
            var slots = roles.Select(BuildAssetSlotStatus).ToList();
            var needs = slots.Where(slot => slot.state != "PASS").ToList();
            var lines = new List<string>
            {
                "# VARCO 에셋 요청서",
                "",
                "- 생성 시간: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "- 씬: " + SceneLabel(),
                "- 장르: " + GenreLabel(genre),
                "- 블록 템플릿: " + BlockTemplateLabel(),
                "- 게임 방식: " + GameRecipeLabel(recipe),
                "- 플레이어 캐릭터: " + PlayerCharacterLabel(),
                "- 적 캐릭터: " + EnemyCharacterLabel(),
                "- 에셋 스캔 범위: " + ScanRootSummary(GameObjectScanRoots()),
                "- 블록: " + ActiveBlockSummary(),
                "",
                "## 현재 에셋 슬롯",
                "",
                "| 역할 | 상태 | 현재 매칭 | 추천 에셋 이름 |",
                "| --- | --- | --- | --- |"
            };

            foreach (var slot in slots)
                lines.Add("| " + AssetRoleLabel(slot.role) + " | " + StateLabel(slot.state) + " | " + MarkdownCell(slot.message) + " | " + SuggestedAssetName(slot.role) + " |");

            AppendSoundAssetDetails(lines);
            AppendAnimationAssetDetails(lines);

            AppendAssetMatchingGuideSection(lines);
            AppendGuidedNextActionSection(lines);

            lines.Add("");
            lines.Add("## VarcoAI에서 만들거나 보강할 것");
            if (needs.Count == 0)
            {
                lines.Add("- 활성화된 게임플레이 슬롯은 모두 선호 에셋을 가지고 있습니다. 더 다듬고 싶을 때만 아래 사운드/애니메이션 요청을 사용하세요.");
            }
            else
            {
                foreach (var slot in needs)
                {
                    lines.Add("");
                    lines.Add("### " + AssetRoleLabel(slot.role));
                    lines.Add("- 추천 이름: " + SuggestedAssetName(slot.role));
                    lines.Add("- 넣을 위치: " + SuggestedImportTarget(slot.role));
                    lines.Add("- 생성 요청문: " + SuggestedAssetPrompt(slot.role));
                }
            }

            lines.Add("");
            lines.Add("## 사운드 요청");
            foreach (var soundPrompt in BuildSoundRequestPrompts())
                lines.Add("- " + soundPrompt);

            lines.Add("");
            lines.Add("## 애니메이션 요청");
            foreach (var animationPrompt in BuildAnimationRequestPrompts())
                lines.Add("- " + animationPrompt);

            lines.Add("");
            lines.Add("## 자동 인식을 위한 이름 규칙");
            lines.Add("- 완성된 프리팹은 `Assets/Prefabs`, 원본 FBX/모델은 `Assets/VARCO3DImports`, 외부 에셋팩은 `Assets/Importing Assets` 아래에 넣어도 자동 스캔됩니다.");
            lines.Add("- 이름은 한글만 써도 자동 인식됩니다. 장르 예: 아레나, 탐험, 퍼즐, 플랫폼, 우주.");
            lines.Add("- 역할 예: 플레이어, 적, 좀비, 보스, 무기, 아이템, 회복, 목표, 문, 압력판, 위험, 이동발판, 상자, 체크포인트, 엄폐물.");
            lines.Add("- 영어 에셋팩도 그대로 인식됩니다: Player, Enemy, Zombie, Boss, Weapon, Item, Health, Goal, Door, PressurePlate, Hazard, MovingPlatform, Box, Checkpoint, Cover, Tree, Prop, Environment.");
            lines.Add("- 캐릭터는 Humanoid Rig, 보이는 Mesh, Animator, +Z 정면 방향을 권장합니다.");

            AppendAcceptanceSection(lines, BuildAcceptanceChecklist());
            return lines;
        }

        void AppendAssetMatchingGuideSection(List<string> lines)
        {
            var roles = ActiveRolesForCurrentBlocks().Distinct().ToList();
            if (roles.Count == 0)
                return;

            var slots = roles.Select(BuildAssetSlotStatus).ToList();
            var detectedCount = candidates.Count(candidate => candidate.role != AssetRole.Unknown);
            var internalEvidenceCount = candidates.Count(candidate => candidate.usedInternalEvidence);

            lines.Add("");
            lines.Add("## 에셋 자동 매칭판");
            lines.Add("- 요약: " + AssetSlotProgressLabel(slots));
            lines.Add("- 감지된 역할 후보: " + detectedCount + "개");
            lines.Add("- 프리팹 내부 단서로 인식된 후보: " + internalEvidenceCount + "개");
            lines.Add("- 검사 기준: 파일명, 프리팹 자식 이름, 렌더러/재질, 메시, 애니메이션 컨트롤러 이름");
            lines.Add("");
            lines.Add("| 역할 | 상태 | 선택 후보 | 초보자용 안내 |");
            lines.Add("| --- | --- | --- | --- |");

            foreach (var slot in slots)
            {
                var selected = slot.selected != null ? slot.selected.DisplayName : "기본 오브젝트 자동 생성";
                lines.Add("| " + MarkdownCell(AssetRoleLabel(slot.role))
                    + " | " + StateLabel(slot.state)
                    + " | " + MarkdownCell(selected)
                    + " | " + MarkdownCell(BuildAssetSlotGuideMessage(slot))
                    + " |");
            }

            var internalMatches = candidates
                .Where(candidate => candidate.usedInternalEvidence)
                .Take(8)
                .ToList();
            if (internalMatches.Count == 0)
                return;

            lines.Add("");
            lines.Add("### 프리팹 내부 단서로 찾은 후보");
            lines.Add("| 역할 | 후보 | 판단 근거 | 경로 |");
            lines.Add("| --- | --- | --- | --- |");
            foreach (var candidate in internalMatches)
            {
                lines.Add("| " + MarkdownCell(AssetRoleLabel(candidate.role))
                    + " | " + MarkdownCell(candidate.DisplayName)
                    + " | " + MarkdownCell(BuildAssetShortReason(candidate))
                    + " | " + MarkdownCell(candidate.path)
                    + " |");
            }
        }

        void AppendDetectedAssetDetails(List<string> lines)
        {
            var roles = ActiveRolesForCurrentBlocks().Distinct().ToList();
            if (roles.Count == 0)
                return;

            lines.Add("");
            lines.Add("## 감지된 VARCO 에셋 후보");
            lines.Add("| 역할 | 후보 | 점수 | 판단 근거 | 경로 |");
            lines.Add("| --- | --- | ---: | --- | --- |");

            var wroteAny = false;
            foreach (var role in roles)
            {
                var roleCandidates = RankedCandidatesForRole(role, 3).ToList();
                if (roleCandidates.Count == 0)
                {
                    lines.Add("| " + MarkdownCell(AssetRoleLabel(role)) + " | 기본 오브젝트 자동 생성 | 0 | 맞는 VARCO 에셋을 찾지 못했습니다. |  |");
                    wroteAny = true;
                    continue;
                }

                foreach (var candidate in roleCandidates)
                {
                    lines.Add("| " + MarkdownCell(AssetRoleLabel(role))
                        + " | " + MarkdownCell(candidate.DisplayName)
                        + " | " + candidate.score
                        + " | " + MarkdownCell(BuildAssetShortReason(candidate))
                        + " | " + MarkdownCell(candidate.path)
                        + " |");
                    wroteAny = true;
                }
            }

            if (!wroteAny)
                lines.Add("- 감지된 후보가 없습니다.");
        }

        void AppendSoundAssetDetails(List<string> lines)
        {
            if (!blockSound)
                return;

            var registry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(RegistryPath);
            var statuses = BuildSoundSlotStatuses(registry).ToList();
            if (statuses.Count == 0)
                return;

            lines.Add("");
            lines.Add("## 사운드/BGM 자동 연결");
            lines.Add("| 슬롯 | 상태 | 연결된 클립 | 판단 근거 | 이벤트 ID |");
            lines.Add("| --- | --- | --- | --- | --- |");
            foreach (var status in statuses)
            {
                var clipLabel = status.clip ? status.clip.name : "누락";
                if (!string.IsNullOrWhiteSpace(status.clipPath))
                    clipLabel += " (" + status.clipPath + ")";

                lines.Add("| " + MarkdownCell(status.definition.label)
                    + " | " + StateLabel(status.state)
                    + " | " + MarkdownCell(clipLabel)
                    + " | " + MarkdownCell(status.reason)
                    + " | `" + status.definition.id + "` |");
            }
        }

        void AppendAnimationAssetDetails(List<string> lines)
        {
            if (!blockPlayer || !autoAnimations)
                return;

            var statuses = BuildAnimationSlotStatuses().ToList();
            if (statuses.Count == 0)
                return;

            lines.Add("");
            lines.Add("## 애니메이션 자동 연결");
            lines.Add("| 슬롯 | 상태 | 연결 후보 클립 | 판단 근거 |");
            lines.Add("| --- | --- | --- | --- |");
            foreach (var status in statuses)
            {
                var clipLabel = status.clip ? status.clip.name : "누락";
                if (!string.IsNullOrWhiteSpace(status.clipPath))
                    clipLabel += " (" + status.clipPath + ")";

                lines.Add("| " + MarkdownCell(AnimationSlotLabel(status.definition))
                    + " | " + StateLabel(status.state)
                    + " | " + MarkdownCell(clipLabel)
                    + " | " + MarkdownCell(status.reason)
                    + " |");
            }
        }

        void AppendBlockAssemblyDetails(List<string> lines)
        {
            var statuses = BuildBlockAssemblyStatuses()
                .Where(status => status.active || status.state != "PASS")
                .ToList();
            if (statuses.Count == 0)
                return;

            lines.Add("");
            lines.Add("## 블록 조립판");
            lines.Add("| 묶음 | 블록 | 상태 | 자동 제작 작업 |");
            lines.Add("| --- | --- | --- | --- |");
            foreach (var status in statuses)
            {
                lines.Add("| " + MarkdownCell(status.group)
                    + " | " + MarkdownCell(status.label)
                    + " | " + StateLabel(status.state)
                    + " | " + MarkdownCell(status.message)
                    + " |");
            }
        }

        void AppendWindowsBuildReadinessDetails(List<string> lines)
        {
            var findings = BuildWindowsReadinessFindings();
            lines.Add("");
            lines.Add("## Windows 빌드 준비판");
            lines.Add("- 출력 위치: `" + WindowsBuildOutputPath().Replace("\\", "/") + "`");
            lines.Add("- 준비 상태: " + AcceptanceSummary(findings));
            lines.Add("");
            lines.Add("| 상태 | 영역 | 내용 |");
            lines.Add("| --- | --- | --- |");
            foreach (var finding in findings)
                lines.Add("| " + StateLabel(finding.state) + " | " + MarkdownCell(finding.area) + " | " + MarkdownCell(finding.message) + " |");
        }

        void AppendGuidedNextActionSection(List<string> lines)
        {
            var recommendation = BuildOneClickRecommendation();
            lines.Add("");
            lines.Add("## 추천 다음 버튼");
            lines.Add("- 추천: " + recommendation.title);
            lines.Add("- 이유: " + recommendation.detail);
            if (!string.IsNullOrWhiteSpace(recommendation.buttonLabel))
                lines.Add("- 누를 버튼: `" + recommendation.buttonLabel.Replace("추천 실행: ", "") + "`");

            var steps = BuildOneClickNextSteps().ToList();
            if (steps.Count == 0)
                return;

            lines.Add("");
            lines.Add("## 진행 순서");
            for (var i = 0; i < steps.Count; i++)
                lines.Add((i + 1) + ". " + steps[i]);
        }

        void AppendAcceptanceSection(List<string> lines, List<AcceptanceFinding> findings)
        {
            lines.Add("");
            lines.Add("## 완성 체크리스트");
            lines.Add("");
            lines.Add("| 상태 | 영역 | 확인 내용 |");
            lines.Add("| --- | --- | --- |");
            foreach (var finding in findings)
                lines.Add("| " + StateLabel(finding.state) + " | " + MarkdownCell(finding.area) + " | " + MarkdownCell(finding.message) + " |");
        }

        void DrawAcceptanceSummary()
        {
            var findings = BuildAcceptanceChecklist();
            var issueCount = findings.Count(f => f.state != "PASS");
            EditorGUILayout.HelpBox("완성 점검: " + AcceptanceSummary(findings), AcceptanceMessageType(findings));

            acceptanceChecklistOpen = EditorGUILayout.Foldout(acceptanceChecklistOpen, "완성 체크리스트", true);
            if (!acceptanceChecklistOpen)
                return;

            var important = findings.Where(f => f.state != "PASS").Take(8).ToList();
            if (important.Count == 0)
            {
                EditorGUILayout.LabelField("통과: 자동 제작 준비가 끝났습니다.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            foreach (var finding in important)
                EditorGUILayout.LabelField(StateLabel(finding.state) + ": " + finding.area + " - " + finding.message, EditorStyles.wordWrappedMiniLabel);

            if (issueCount > important.Count)
                EditorGUILayout.LabelField("추가 확인 " + (issueCount - important.Count) + "개는 생성된 리포트에서 볼 수 있습니다.", EditorStyles.miniLabel);
        }

        List<AcceptanceFinding> BuildAcceptanceChecklist()
        {
            var findings = new List<AcceptanceFinding>();
            var activeRoles = ActiveRolesForCurrentBlocks().Distinct().ToList();
            var scene = SceneManager.GetActiveScene();
            var scenePath = scene.path;
            var currentSceneInBuild = !string.IsNullOrWhiteSpace(scenePath)
                && EditorBuildSettings.scenes.Any(s => s.enabled && s.path == scenePath);

            AddAcceptanceFinding(findings, blockPlayer ? "PASS" : "FAIL", "설계",
                blockPlayer ? "플레이어 블록이 켜져 있습니다." : "플레이어 블록이 꺼져 있습니다. 자동 제작에는 조작 가능한 플레이어가 필요합니다.");
            AddAcceptanceFinding(findings, blockGoal || blockEnemyWave ? "PASS" : "FAIL", "설계",
                "클리어 조건: " + CompletionConditionLabel(PrimaryClearCondition()) + ".");
            AddAcceptanceFinding(findings, blockTemplate == BlockTemplate.Custom ? "WARN" : "PASS", "설계",
                blockTemplate == BlockTemplate.Custom ? "직접 고른 블록입니다. 처음 사용자에게는 기본 템플릿이 더 안전합니다." : "선택된 템플릿: " + BlockTemplateLabel() + ".");
            AddAcceptanceFinding(findings, saveScene ? "PASS" : "WARN", "자동화",
                saveScene ? "씬 저장이 켜져 있습니다." : "씬 저장이 꺼져 있어 제작 결과를 잃기 쉽습니다.");
            AddAcceptanceFinding(findings, runSafetyPass ? "PASS" : "WARN", "자동화",
                runSafetyPass ? "안전 보정이 켜져 있습니다." : "안전 보정이 꺼져 있어 이전 연결이 남을 수 있습니다.");
            AddAcceptanceFinding(findings, autoConnectPrefabs ? "PASS" : "WARN", "자동화",
                autoConnectPrefabs ? "VARCO 에셋 자동 연결이 켜져 있습니다." : "VARCO 에셋 자동 연결이 꺼져 있습니다.");

            foreach (var role in activeRoles)
            {
                var slot = BuildAssetSlotStatus(role);
                var state = slot.state == "FALLBACK" ? "WARN" : slot.state;
                var message = slot.state == "FALLBACK"
                    ? AssetRoleLabel(role) + "에 맞는 VARCO 에셋이 없습니다. 자동 제작으로 기본 오브젝트는 만들 수 있지만 에셋 적용은 미완성입니다."
                    : AssetRoleLabel(role) + "에 " + slot.message + " 사용.";
                AddAcceptanceFinding(findings, state, "에셋", message);
            }

            AddSceneCheck(findings, FindFirstObjectByType<VWS.GameManager>(), "씬", "게임 매니저", true);
            AddSceneCheck(findings, Camera.main, "씬", "메인 카메라", true);
            AddSceneCheck(findings, GameObject.FindGameObjectWithTag("Player"), "씬", "Player 태그가 붙은 플레이어 오브젝트", createMissingObjects);
            findings.AddRange(BuildPlayReadyChecklist());

            if (blockWeapon)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                var equipped = FindEquippedWeapon(player);
                AddSceneCheck(findings, equipped, "씬", "장착된 무기 비주얼", createMissingObjects);
            }

            if (blockEnemyWave)
            {
                AddSceneCheck(findings, FindFirstObjectByType<VWS.WaveManager>(), "씬", "웨이브 매니저", true);
                AddSceneCheck(findings, FindFirstObjectByType<VWS.EnemyAI_NavMesh>(), "씬", "적 AI", createMissingObjects);
                AddEnemyWaveReadinessCheck(findings);
            }

            if (blockItems)
                AddCountCheck(findings, CountSceneComponents<VWS.ItemPickup>(), Mathf.Max(1, itemGoal), "씬", "수집 아이템", createMissingObjects);
            if (blockGoal)
                AddSceneCheck(findings, FindFirstObjectByType<VWS.GoalTrigger>(), "씬", "목표 트리거", createMissingObjects);
            if (blockHealthPickup)
                AddSceneCheck(findings, FindFirstObjectByType<VWS.HealthPickup>(), "씬", "회복 아이템", createMissingObjects);
            if (blockHazard)
                AddSceneCheck(findings, FindFirstObjectByType<VWS.HazardZone>(), "씬", "위험 구역", createMissingObjects);
            if (blockCheckpoint)
                AddSceneCheck(findings, FindFirstObjectByType<VWS.Checkpoint>(), "씬", "체크포인트", createMissingObjects);
            if (blockFallRespawn)
                AddSceneCheck(findings, FindFirstObjectByType<VWS.DeathZone>(), "씬", "낙사 리스폰 안전망", createMissingObjects);
            if (blockMovingPlatform)
                AddSceneCheck(findings, FindFirstObjectByType<VWS.MovingPlatform>(), "씬", "이동 발판", createMissingObjects);
            if (blockPuzzleDoor)
            {
                AddSceneCheck(findings, FindFirstObjectByType<VWS.DoorController>(), "씬", "문 컨트롤러", createMissingObjects);
                AddSceneCheck(findings, FindFirstObjectByType<VWS.PressurePlate>(), "씬", "압력판", createMissingObjects);
            }
            if (blockMovableBox)
                AddSceneCheck(findings, FindFirstObjectByType<VWS.MovableBox>(), "씬", "밀 수 있는 상자", createMissingObjects);
            if (blockCountdown)
                AddSceneCheck(findings, FindFirstObjectByType<VWS.CountdownTimer>(), "씬", "제한시간 타이머", createMissingObjects);

            if (blockHud)
                AddSceneCheck(findings, FindFirstObjectByType<VWS.VARCOGameHUD>(), "HUD", "VARCO 게임 HUD", addModernHud);

            if (blockVisuals)
                AddSceneCheck(findings, FindFirstObjectByType<Volume>(), "비주얼", "글로벌 볼륨", applyVisualPreset);

            if (blockSound)
            {
                var registry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(RegistryPath);
                AddSceneCheck(findings, registry, "사운드", "사운드 이벤트 목록", autoSounds);
                AddSceneCheck(findings, GameObject.Find("VW_Audio_BGM"), "사운드", "BGM 오디오 소스", autoSounds);
                var audioClipCount = CountProjectAssets("t:AudioClip", AudioScanRoots());
                AddAcceptanceFinding(findings, audioClipCount > 0 ? "PASS" : "WARN", "사운드",
                    audioClipCount > 0 ? "오디오 클립 " + audioClipCount + "개를 찾았습니다." : "스캔 범위에서 AudioClip을 찾지 못했습니다.");
            }

            if (blockPlayer && autoAnimations)
            {
                var animationClipCount = CountProjectAssets("t:AnimationClip", AnimationScanRoots());
                var animationSlots = BuildAnimationSlotStatuses().ToList();
                var readySlots = animationSlots.Count(slot => slot.state == "PASS");
                var requiredSlots = animationSlots.Count(slot => slot.definition.important);
                var readyRequiredSlots = animationSlots.Count(slot => slot.definition.important && slot.state == "PASS");
                AddAcceptanceFinding(findings, readyRequiredSlots >= requiredSlots ? "PASS" : animationClipCount > 0 ? "WARN" : "WARN", "애니메이션",
                    "Animator 자동 연결 슬롯 " + readySlots + "/" + animationSlots.Count + " 준비"
                    + " / 필수 " + readyRequiredSlots + "/" + requiredSlots
                    + " / 전체 클립 " + animationClipCount + "개");
            }
            else if (blockPlayer)
            {
                AddAcceptanceFinding(findings, "WARN", "애니메이션", "Animator 자동 설정이 꺼져 있습니다.");
            }

            AddAcceptanceFinding(findings, !string.IsNullOrWhiteSpace(scenePath) ? "PASS" : "WARN", "빌드",
                !string.IsNullOrWhiteSpace(scenePath) ? "씬이 저장되어 있습니다: " + scenePath + "." : "씬이 아직 저장되지 않았습니다. Windows EXE 빌드에는 씬 경로가 필요합니다.");
            AddAcceptanceFinding(findings, currentSceneInBuild ? "PASS" : addSceneToBuild ? "WARN" : "FAIL", "빌드",
                currentSceneInBuild ? "현재 씬이 이미 빌드 설정에 들어 있습니다." : addSceneToBuild ? "자동 제작 시 현재 씬을 빌드 설정에 추가합니다." : "현재 씬이 빌드 설정에 없고 자동 추가도 꺼져 있습니다.");

            return findings;
        }

        List<AcceptanceFinding> BuildPlayReadyChecklist()
        {
            var findings = new List<AcceptanceFinding>();
            var gm = FindFirstObjectByType<VWS.GameManager>();
            var profile = gm ? gm.profile : null;
            var player = GameObject.FindGameObjectWithTag("Player");
            var expectedClear = PrimaryClearCondition();

            AddAcceptanceFinding(findings, profile ? "PASS" : createMissingObjects ? "WARN" : "FAIL", "플레이 준비",
                profile ? "게임 매니저에 게임 프로필이 연결되어 있습니다." : "게임 매니저 프로필이 비어 있습니다. 자동 제작이 장르별 프로필을 연결합니다.");

            if (profile)
            {
                AddAcceptanceFinding(findings, profile.genre == genre ? "PASS" : "WARN", "플레이 준비",
                    profile.genre == genre ? "프로필 장르가 현재 선택과 일치합니다." : "프로필 장르(" + GenreLabel(profile.genre) + ")와 현재 선택(" + GenreLabel(genre) + ")이 다릅니다.");
                AddAcceptanceFinding(findings, profile.clearCondition == expectedClear ? "PASS" : "WARN", "플레이 준비",
                    profile.clearCondition == expectedClear ? "프로필 클리어 조건이 현재 블록과 일치합니다." : "프로필 클리어 조건(" + CompletionConditionLabel(profile.clearCondition) + ")과 현재 블록 기준(" + CompletionConditionLabel(expectedClear) + ")이 다릅니다.");

                var koreanHudTextCount = CountKoreanHudTexts(profile);
                AddAcceptanceFinding(findings, koreanHudTextCount >= 4 ? "PASS" : "WARN", "HUD",
                    koreanHudTextCount >= 4 ? "목표, 조작, 클리어, 실패 문구가 한글로 준비되어 있습니다." : "HUD 한글 문구 " + koreanHudTextCount + "/4개만 준비되어 있습니다. 자동 제작이 채웁니다.");
            }

            if (blockPlayer)
                AddPlayerPlayReadyChecks(findings, player);

            if (blockHud)
            {
                var hud = FindFirstObjectByType<VWS.VARCOGameHUD>();
                AddAcceptanceFinding(findings, hud && hud.fallbackGenre == genre ? "PASS" : addModernHud ? "WARN" : "FAIL", "HUD",
                    hud ? "VARCO 게임 HUD 장르가 " + GenreLabel(hud.fallbackGenre) + "로 설정되어 있습니다." : "VARCO 게임 HUD가 아직 없습니다. 자동 제작이 추가합니다.");
            }

            AddCameraPlayReadyChecks(findings, player);
            AddClearConditionPlayReadyChecks(findings, expectedClear);
            return findings;
        }

        void AddPlayerPlayReadyChecks(List<AcceptanceFinding> findings, GameObject player)
        {
            if (!player)
            {
                AddAcceptanceFinding(findings, createMissingObjects ? "WARN" : "FAIL", "입력",
                    "플레이어가 없어 컨트롤러와 입력 충돌을 검사할 수 없습니다.");
                return;
            }

            var platformController = player.GetComponent<VWS.PlayerController_Platform>();
            var thirdPersonController = player.GetComponent<VWS.PlayerController_ThirdPerson>();
            var health = player.GetComponent<VWS.PlayerHealth>();
            var counter = player.GetComponent<VWS.CollectibleCounter>();
            var attack = player.GetComponent<VWS.PlayerAttack>();

            if (genre == VWS.GenreType.Platform)
            {
                AddAcceptanceFinding(findings, platformController && player.GetComponent<CharacterController>() ? "PASS" : "WARN", "입력",
                    platformController ? "플랫폼용 플레이어 컨트롤러가 연결되어 있습니다." : "플랫폼 장르인데 플랫폼 컨트롤러가 없습니다. 자동 제작이 연결합니다.");
            }
            else
            {
                AddAcceptanceFinding(findings, thirdPersonController ? "PASS" : "WARN", "입력",
                    thirdPersonController ? "3인칭 플레이어 컨트롤러가 연결되어 있습니다." : GenreLabel(genre) + " 장르에는 3인칭 컨트롤러가 필요합니다.");
            }

            AddAcceptanceFinding(findings, health ? "PASS" : "WARN", "플레이 준비",
                health ? "플레이어 체력 컴포넌트가 연결되어 있습니다." : "플레이어 체력 컴포넌트가 없습니다. 자동 제작이 추가합니다.");

            if (blockItems)
                AddAcceptanceFinding(findings, counter ? "PASS" : "WARN", "플레이 준비",
                    counter ? "수집 카운터가 플레이어에 연결되어 있습니다." : "수집 아이템을 쓰지만 플레이어 수집 카운터가 없습니다.");

            if (blockWeapon && genre != VWS.GenreType.Platform)
                AddAcceptanceFinding(findings, attack ? "PASS" : "WARN", "입력",
                    attack ? "공격 컴포넌트가 플레이어에 연결되어 있습니다." : "무기 블록이 켜져 있지만 공격 컴포넌트가 없습니다.");

            if (attack)
                AddAcceptanceFinding(findings, attack.keyboardAttackKey != KeyCode.Space ? "PASS" : "WARN", "입력",
                    attack.keyboardAttackKey == KeyCode.Space ? "공격 키가 Space라서 점프/플랫폼 입력과 충돌할 수 있습니다." : "Space는 공격 키로 쓰지 않아 점프/공격 입력 충돌을 피합니다.");
        }

        void AddCameraPlayReadyChecks(List<AcceptanceFinding> findings, GameObject player)
        {
            var mainCamera = Camera.main;
            var followCamera = mainCamera ? mainCamera.GetComponent<VWS.ThirdPersonCamera>() : FindFirstObjectByType<VWS.ThirdPersonCamera>();
            var targetOk = !followCamera || !player || !followCamera.target || followCamera.target == player.transform;

            AddAcceptanceFinding(findings, mainCamera && followCamera && targetOk ? "PASS" : createMissingObjects ? "WARN" : "FAIL", "카메라",
                mainCamera && followCamera && targetOk ? "메인 카메라가 플레이어 추적 카메라로 준비되어 있습니다." : "메인 카메라 추적 대상 또는 ThirdPersonCamera 연결을 자동 제작으로 다시 맞춰야 합니다.");
        }

        void AddClearConditionPlayReadyChecks(List<AcceptanceFinding> findings, VWS.CompletionCondition expectedClear)
        {
            switch (expectedClear)
            {
                case VWS.CompletionCondition.DefeatWaves:
                    var wave = FindFirstObjectByType<VWS.WaveManager>();
                    AddAcceptanceFinding(findings, wave && wave.clearWhenAllWavesCleared ? "PASS" : createMissingObjects ? "WARN" : "FAIL", "클리어",
                        wave && wave.clearWhenAllWavesCleared ? "적 웨이브 처치 시 클리어되도록 설정되어 있습니다." : "웨이브 클리어 규칙이 현재 블록과 맞지 않습니다.");
                    break;
                case VWS.CompletionCondition.CollectItems:
                    var collectGoal = FindFirstObjectByType<VWS.GoalTrigger>();
                    var requiredItems = collectGoal ? collectGoal.requiredItems : 0;
                    AddAcceptanceFinding(findings, collectGoal && requiredItems >= Mathf.Max(1, itemGoal) ? "PASS" : createMissingObjects ? "WARN" : "FAIL", "클리어",
                        collectGoal ? "목표 트리거 요구 수집량: " + requiredItems + " / 기대 " + Mathf.Max(1, itemGoal) + "." : "수집 완료를 판정할 목표 트리거가 없습니다.");
                    break;
                default:
                    var goal = FindFirstObjectByType<VWS.GoalTrigger>();
                    AddAcceptanceFinding(findings, goal && goal.requiredItems <= 0 ? "PASS" : createMissingObjects ? "WARN" : "FAIL", "클리어",
                        goal ? "목표 지점 도달 클리어 트리거가 준비되어 있습니다." : "목표 지점 도달을 판정할 트리거가 없습니다.");
                    break;
            }
        }

        bool SceneHasPlayReadyIssue()
        {
            return BuildPlayReadyChecklist().Any(finding => finding.state == "WARN" || finding.state == "FAIL");
        }

        static int CountKoreanHudTexts(VWS.GameProfile profile)
        {
            if (!profile)
                return 0;

            var count = 0;
            if (HasDisplayText(profile.objectiveText)) count++;
            if (HasDisplayText(profile.controlGuideText)) count++;
            if (HasDisplayText(profile.clearMessage)) count++;
            if (HasDisplayText(profile.gameOverMessage)) count++;
            return count;
        }

        static bool HasDisplayText(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        static bool HasKoreanText(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, "[가-힣]");
        }

        void AddSceneCheck(List<AcceptanceFinding> findings, Object obj, string area, string label, bool canAutoCreate)
        {
            if (obj)
            {
                AddAcceptanceFinding(findings, "PASS", area, label + "이(가) 있습니다.");
                return;
            }

            AddAcceptanceFinding(findings, canAutoCreate ? "WARN" : "FAIL", area,
                canAutoCreate ? label + "이(가) 아직 없지만 자동 제작으로 만들거나 연결할 수 있습니다." : label + "이(가) 없고 자동 생성도 꺼져 있습니다.");
        }

        void AddCountCheck(List<AcceptanceFinding> findings, int count, int expected, string area, string label, bool canAutoCreate)
        {
            if (count >= expected)
            {
                AddAcceptanceFinding(findings, "PASS", area, label + " " + count + "/" + expected + ".");
                return;
            }

            AddAcceptanceFinding(findings, canAutoCreate ? "WARN" : "FAIL", area,
                canAutoCreate ? label + " " + count + "/" + expected + "; 부족한 오브젝트는 자동 제작으로 생성됩니다." : label + " " + count + "/" + expected + "이고 자동 생성이 꺼져 있습니다.");
        }

        void AddEnemyWaveReadinessCheck(List<AcceptanceFinding> findings)
        {
            var expected = Mathf.Max(1, EffectiveWaveEnemyCount());
            var sceneEnemies = CountSceneComponents<VWS.EnemyHealth>();
            var wave = FindFirstObjectByType<VWS.WaveManager>();
            var configuredWaveEnemies = CountConfiguredWaveEnemies(wave);
            if (configuredWaveEnemies >= expected)
            {
                AddAcceptanceFinding(findings, "PASS", "씬",
                    "적 웨이브 총합 " + configuredWaveEnemies + "/" + expected + " (씬 배치 " + sceneEnemies + "개, 나머지는 웨이브로 생성).");
                return;
            }

            if (sceneEnemies >= expected)
            {
                AddAcceptanceFinding(findings, "PASS", "씬", "적 체력 오브젝트 " + sceneEnemies + "/" + expected + ".");
                return;
            }

            AddAcceptanceFinding(findings, createMissingObjects ? "WARN" : "FAIL", "씬",
                createMissingObjects
                    ? "적 웨이브 총합 " + configuredWaveEnemies + "/" + expected + "; 자동 제작을 다시 실행하면 부족한 설정을 보정합니다."
                    : "적 웨이브 총합 " + configuredWaveEnemies + "/" + expected + "이고 자동 생성이 꺼져 있습니다.");
        }

        static int CountConfiguredWaveEnemies(VWS.WaveManager wave)
        {
            if (!wave || wave.waves == null)
                return 0;

            var total = 0;
            foreach (var data in wave.waves)
            {
                if (data == null || !data.enemyPrefab || !data.enemyPrefab.GetComponentInChildren<VWS.EnemyHealth>(true))
                    continue;

                total += Mathf.Max(0, data.enemyCount);
            }

            return total;
        }

        static void AddAcceptanceFinding(List<AcceptanceFinding> findings, string state, string area, string message)
        {
            findings.Add(new AcceptanceFinding
            {
                state = state,
                area = area,
                message = message
            });
        }

        static int CountSceneComponents<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;
        }

        static int CountProjectAssets(string filter, params string[] roots)
        {
            return AssetDatabase.FindAssets(filter, AssetScanRoots(roots)).Length;
        }

        static string[] GameObjectScanRoots()
        {
            return AssetScanRoots(GameObjectRootCandidates);
        }

        static string[] AudioScanRoots()
        {
            return AssetScanRoots(AudioRootCandidates);
        }

        static string[] AnimationScanRoots()
        {
            return AssetScanRoots(AnimationRootCandidates);
        }

        static string[] AssetScanRoots(params string[] roots)
        {
            var validRoots = roots
                .Where(root => !string.IsNullOrWhiteSpace(root) && AssetDatabase.IsValidFolder(root))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return validRoots.Length > 0 ? validRoots : new[] { "Assets" };
        }

        static string ScanRootSummary(IEnumerable<string> roots)
        {
            return string.Join(" / ", roots.Select(root => root == "Assets" ? root : root.Replace("Assets/", string.Empty)));
        }

        string SuggestedAssetName(AssetRole role)
        {
            var genreToken = SafeFileName(genre.ToString());
            switch (role)
            {
                case AssetRole.Player:
                    return "VARCO_" + genreToken + "_Player.prefab";
                case AssetRole.Enemy:
                    return "VARCO_" + genreToken + "_" + SafeFileName(EnemyCharacterLabel()) + "_Enemy.prefab";
                case AssetRole.Weapon:
                    return "VARCO_" + genreToken + "_Sword_Weapon.prefab";
                case AssetRole.ItemPickup:
                    return "VARCO_" + genreToken + "_Collectible_Item.prefab";
                case AssetRole.HealthPickup:
                    return "VARCO_" + genreToken + "_Health_Pickup.prefab";
                case AssetRole.Goal:
                    return "VARCO_" + genreToken + "_Goal_Crystal.prefab";
                case AssetRole.Door:
                    return "VARCO_" + genreToken + "_Door.prefab";
                case AssetRole.PressurePlate:
                    return "VARCO_" + genreToken + "_PressurePlate.prefab";
                case AssetRole.HazardZone:
                    return "VARCO_" + genreToken + "_Hazard_Trap.prefab";
                case AssetRole.MovingPlatform:
                    return "VARCO_" + genreToken + "_Moving_Platform.prefab";
                case AssetRole.MovableBox:
                    return "VARCO_" + genreToken + "_Push_Box.prefab";
                case AssetRole.Checkpoint:
                    return "VARCO_" + genreToken + "_Checkpoint.prefab";
                case AssetRole.ArenaCover:
                    return "VARCO_" + genreToken + "_Environment_Prop.prefab";
                default:
                    return "VARCO_" + genreToken + "_" + role + ".prefab";
            }
        }

        static string SuggestedImportTarget(AssetRole role)
        {
            switch (role)
            {
                case AssetRole.Player:
                case AssetRole.Enemy:
                case AssetRole.Weapon:
                    return "Assets/Prefabs/Characters, Assets/VARCO3DImports 또는 Assets/Importing Assets";
                default:
                    return "Assets/Prefabs, Assets/VARCO3DImports 또는 Assets/Importing Assets";
            }
        }

        string SuggestedAssetPrompt(AssetRole role)
        {
            var genreLabel = GenreLabel(genre);
            switch (role)
            {
                case AssetRole.Player:
                    return genreLabel + " 장르의 3인칭 Unity 게임용 리깅된 3D 플레이어 캐릭터를 만들어줘. 보이는 메시, 휴머노이드 리그, Animator에 바로 넣을 수 있는 idle, walk, run, jump, attack, hit, death 애니메이션 클립을 포함하고, 정면은 +Z 방향을 보게 해줘. 이름은 " + SuggestedAssetName(role) + "로 해줘.";
                case AssetRole.Enemy:
                    return genreLabel + " 게임용 3D " + EnemyCharacterLabel() + " 적을 만들어줘. 대기, 걷기, 추적, 공격, 피격, 사망 애니메이션과 알아보기 쉬운 실루엣, 적 이동 경로에 맞는 크기를 포함해줘. 이름은 " + SuggestedAssetName(role) + "로 해줘.";
                case AssetRole.Weapon:
                    return genreLabel + " 플레이어가 손에 들 수 있는 3D 무기를 만들어줘. 오른손 장착에 맞는 크기, 손잡이 근처 피벗, 비주얼용이므로 불필요한 물리 콜라이더는 빼줘. 이름은 " + SuggestedAssetName(role) + "로 해줘.";
                case AssetRole.ItemPickup:
                    return genreLabel + " 게임에서 멀리서도 잘 보이는 밝은 3D 수집 아이템을 만들어줘. 회전 애니메이션에 어울리는 단순하고 읽기 쉬운 형태로 해줘. 이름은 " + SuggestedAssetName(role) + "로 해줘.";
                case AssetRole.HealthPickup:
                    return "회복 아이템임을 한눈에 알 수 있는 3D 프롭을 만들어줘. 초록색 또는 빨간색 계열, 작은 수집 아이템 크기, 단순한 머티리얼로 해줘. 이름은 " + SuggestedAssetName(role) + "로 해줘.";
                case AssetRole.Goal:
                    return genreLabel + " 게임의 최종 목표 오브젝트를 만들어줘. 클리어 지점임이 명확하고, 글로우 효과와 어울리는 머티리얼을 사용해줘. 이름은 " + SuggestedAssetName(role) + "로 해줘.";
                case AssetRole.Door:
                    return "Unity에서 열리거나 슬라이드될 수 있는 퍼즐 문 또는 게이트 프롭을 만들어줘. 중심 피벗과 문 통과에 맞는 크기를 유지해줘. 이름은 " + SuggestedAssetName(role) + "로 해줘.";
                case AssetRole.PressurePlate:
                    return "낮고 평평한 압력판 스위치를 만들어줘. 활성/비활성 상태를 표현하기 쉬운 표면 디테일을 넣어줘. 이름은 " + SuggestedAssetName(role) + "로 해줘.";
                case AssetRole.HazardZone:
                    return genreLabel + " 게임플레이용 위험 구역 프롭을 만들어줘. 가시, 용암, 레이저, 데미지 필드처럼 위험하다는 것이 바로 보이게 해줘. 이름은 " + SuggestedAssetName(role) + "로 해줘.";
                case AssetRole.MovingPlatform:
                    return "이동 발판 게임플레이용 플랫폼 프롭을 만들어줘. 플레이어가 올라설 만큼 충분히 넓고, 가장자리가 명확하며 단순한 충돌 형태에 맞게 해줘. 이름은 " + SuggestedAssetName(role) + "로 해줘.";
                case AssetRole.MovableBox:
                    return "퍼즐에서 밀 수 있는 상자 또는 크레이트 프롭을 만들어줘. 플레이어 키 이하 크기, 무게감이 보이는 디자인, 단순한 박스형 충돌 형태에 맞게 해줘. 이름은 " + SuggestedAssetName(role) + "로 해줘.";
                case AssetRole.Checkpoint:
                    return "리스폰용 체크포인트 마커를 만들어줘. 안전한 저장 지점처럼 보이도록 빛, 깃발, 표식 느낌을 넣어줘. 이름은 " + SuggestedAssetName(role) + "로 해줘.";
                case AssetRole.ArenaCover:
                    return genreLabel + " 공간을 채울 수 있는 환경 소품 또는 엄폐물을 만들어줘. 나무, 바위, 기둥, 잔해, 가구, 박스처럼 장르 분위기가 바로 보이고 단순한 충돌 형태에 맞게 해줘. 이름은 " + SuggestedAssetName(role) + "로 해줘.";
                default:
                    return genreLabel + " 게임에서 " + AssetRoleLabel(role) + " 역할로 바로 쓸 수 있는 Unity용 3D 에셋을 만들어줘. 이름은 " + SuggestedAssetName(role) + "로 해줘.";
            }
        }

        IEnumerable<string> BuildSoundRequestPrompts()
        {
            if (!blockSound)
            {
                yield return "현재 레시피에서는 사운드 블록이 꺼져 있습니다.";
                yield break;
            }

            yield return "VARCO_" + genre + "_BGM: " + GenreLabel(genre) + " 게임플레이에 어울리는 반복 가능한 BGM, 60-120초, 자연스러운 루프 지점.";
            if (blockPlayer)
            {
                yield return "VARCO_Player_Footstep: 현재 환경과 어울리는 짧은 발소리 효과음.";
                yield return "VARCO_Player_Hit: 플레이어가 피해를 받을 때 쓰는 짧은 효과음.";
                yield return "VARCO_Game_Over: 플레이어가 쓰러지거나 실패했을 때 쓰는 짧은 게임 오버 효과음.";
            }
            if (blockPlayer || blockWeapon)
                yield return "VARCO_Player_Attack: 무기 휘두르기 또는 공격에 맞는 짧은 효과음.";
            if (blockEnemyWave)
            {
                yield return "VARCO_" + SafeFileName(EnemyCharacterLabel()) + "_Attack: 적 공격 효과음.";
                yield return "VARCO_" + SafeFileName(EnemyCharacterLabel()) + "_Hit: 적이 피해를 받을 때 쓰는 짧은 효과음.";
                yield return "VARCO_" + SafeFileName(EnemyCharacterLabel()) + "_Death: 적 처치/사망 효과음.";
            }
            if (blockItems)
                yield return "VARCO_Item_Pickup: 밝고 명확한 수집 아이템 획득 효과음.";
            if (blockHealthPickup)
                yield return "VARCO_Health_Pickup: 회복 아이템 획득 효과음.";
            if (blockCheckpoint)
                yield return "VARCO_Checkpoint: 리스폰 지점을 통과했을 때 쓰는 안전하고 명확한 저장 효과음.";
            if (blockPuzzleDoor)
            {
                yield return "VARCO_PressurePlate_Activate: 스위치/압력판 활성화 효과음.";
                yield return "VARCO_Door_Open: 무거운 문이 열리는 효과음.";
            }
            if (blockGoal)
                yield return "VARCO_Goal_Clear: 짧은 승리 또는 클리어 효과음.";
        }

        IEnumerable<string> BuildAnimationRequestPrompts()
        {
            if (!blockPlayer)
            {
                yield return "현재 레시피에서는 플레이어 블록이 꺼져 있습니다.";
                yield break;
            }

            yield return "플레이어 애니메이션 세트: idle, walk, run, jump, fall, land, attack, hit, death. 모두 " + SuggestedAssetName(AssetRole.Player) + "와 같은 리그 기준.";
            if (blockEnemyWave)
                yield return "적 애니메이션 세트: idle, walk, chase/run, attack, hit, death. 모두 " + SuggestedAssetName(AssetRole.Enemy) + "와 같은 리그 기준.";
            if (blockMovableBox)
                yield return "퍼즐 상호작용 애니메이션: 플레이어가 상자를 밀거나 힘을 주는 동작.";
            if (blockPuzzleDoor)
                yield return "문 애니메이션: " + SuggestedAssetName(AssetRole.Door) + "용 안정적인 피벗의 open/close 클립.";
            if (blockMovingPlatform)
                yield return "이동 발판 애니메이션은 선택 사항입니다. 게임 메이커가 스크립트로 발판을 움직일 수 있습니다.";
        }

        static int AcceptanceScore(List<AcceptanceFinding> findings)
        {
            if (findings == null || findings.Count == 0)
                return 0;

            var fail = findings.Count(f => f.state == "FAIL");
            var warn = findings.Count(f => f.state == "WARN");
            return Mathf.RoundToInt(Mathf.Clamp(100f - fail * 25f - warn * 6f, 0f, 100f));
        }

        static string AcceptanceSummary(List<AcceptanceFinding> findings)
        {
            var pass = findings.Count(f => f.state == "PASS");
            var warn = findings.Count(f => f.state == "WARN");
            var fail = findings.Count(f => f.state == "FAIL");
            return AcceptanceScore(findings) + "/100 | 통과 " + pass + " / 확인 " + warn + " / 실패 " + fail;
        }

        static MessageType AcceptanceMessageType(List<AcceptanceFinding> findings)
        {
            if (findings.Any(f => f.state == "FAIL"))
                return MessageType.Error;
            return findings.Any(f => f.state == "WARN") ? MessageType.Warning : MessageType.Info;
        }

        void OpenLastReport()
        {
            var path = lastReportPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = LatestReportPath();
                lastReportPath = path;
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                EditorUtility.DisplayDialog("VARCO 게임 메이커", "아직 생성된 리포트가 없습니다.", "확인");
                return;
            }

            AssetDatabase.Refresh();
            var projectPath = path.Replace("\\", "/");
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(projectPath);
            if (asset)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                EditorUtility.FocusProjectWindow();
                return;
            }

            Debug.Log("VARCO 리포트 생성됨: " + Path.GetFullPath(path));
        }

        string LatestReportPath()
        {
            if (!Directory.Exists(ReportFolder))
                return null;

            return Directory.GetFiles(ReportFolder, "VARCO_*.md")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        IEnumerable<VARCOGameMakerPreset> FindGameMakerPresets()
        {
            if (!AssetDatabase.IsValidFolder(PresetFolder))
                yield break;

            foreach (var guid in AssetDatabase.FindAssets("t:VARCOGameMakerPreset", new[] { PresetFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<VARCOGameMakerPreset>(path);
                if (preset)
                    yield return preset;
            }
        }

        int CountLogPrefix(string prefix)
        {
            return log.Count(entry => entry.StartsWith(prefix, StringComparison.Ordinal));
        }

        static string MarkdownCell(string value)
        {
            return (value ?? string.Empty).Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
        }

        string SceneLabel()
        {
            var scene = SceneManager.GetActiveScene();
            return string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path;
        }

        string DesignSourceLabel()
        {
            return string.IsNullOrWhiteSpace(lastAutoDesignSummary) ? "사용자 직접 선택" : lastAutoDesignSummary;
        }

        string PresetLabel()
        {
            return selectedPreset ? AssetDatabase.GetAssetPath(selectedPreset) : "없음";
        }

        static T DrawKoreanEnumPopup<T>(string label, T value, Func<T, string> labelFor) where T : struct
        {
            var values = (T[])Enum.GetValues(typeof(T));
            var labels = values.Select(labelFor).ToArray();
            var index = Array.IndexOf(values, value);
            if (index < 0)
                index = 0;

            var next = EditorGUILayout.Popup(label, index, labels);
            return values[Mathf.Clamp(next, 0, values.Length - 1)];
        }

        static string GenreLabel(VWS.GenreType value)
        {
            switch (value)
            {
                case VWS.GenreType.Arena:
                    return "아레나 전투";
                case VWS.GenreType.Exploration:
                    return "탐험";
                case VWS.GenreType.Puzzle:
                    return "퍼즐";
                case VWS.GenreType.Platform:
                    return "플랫폼/스페이스";
                default:
                    return value.ToString();
            }
        }

        static string SceneModeLabel(SceneMode value)
        {
            switch (value)
            {
                case SceneMode.GenreScene:
                    return "장르 샘플 씬 열기";
                default:
                    return "현재 씬에 만들기";
            }
        }

        static string GameRecipeLabel(GameRecipe value)
        {
            switch (value)
            {
                case GameRecipe.GenreDefault:
                    return "장르에 맞게 자동 추천";
                case GameRecipe.CombatWave:
                    return "전투 웨이브";
                case GameRecipe.ExplorationQuest:
                    return "탐험 퀘스트";
                case GameRecipe.DoorPuzzle:
                    return "문 열기 퍼즐";
                case GameRecipe.PlatformCourse:
                    return "스페이스 플랫폼 코스";
                case GameRecipe.CollectAndEscape:
                    return "수집 후 탈출";
                case GameRecipe.SurvivalTimer:
                    return "제한시간 생존";
                case GameRecipe.BossBattle:
                    return "보스전";
                case GameRecipe.ZombieSurvival:
                    return "좀비 생존";
                case GameRecipe.TreasureHunt:
                    return "보물찾기";
                case GameRecipe.EscapeRoom:
                    return "탈출방";
                case GameRecipe.ObstacleRun:
                    return "장애물 코스";
                default:
                    return value.ToString();
            }
        }

        string BlockTemplateLabel()
        {
            return BlockTemplateLabel(blockTemplate);
        }

        static string BlockTemplateLabel(BlockTemplate template)
        {
            switch (template)
            {
                case BlockTemplate.ArenaCombatWave:
                    return "아레나 전투 웨이브";
                case BlockTemplate.ExplorationZombieQuest:
                    return "탐험 좀비 퀘스트";
                case BlockTemplate.PuzzleDoorRoom:
                    return "문 열기 퍼즐방";
                case BlockTemplate.PlatformSpaceCourse:
                    return "스페이스 플랫폼 코스";
                case BlockTemplate.CollectAndEscape:
                    return "수집 후 탈출";
                case BlockTemplate.SurvivalTimer:
                    return "제한시간 생존";
                case BlockTemplate.ArenaBossBattle:
                    return "아레나 보스전";
                case BlockTemplate.ExplorationZombieSurvival:
                    return "탐험 좀비 생존";
                case BlockTemplate.ExplorationTreasureHunt:
                    return "탐험 보물찾기";
                case BlockTemplate.PuzzleEscapeRoom:
                    return "퍼즐 탈출방";
                case BlockTemplate.PlatformObstacleRun:
                    return "플랫폼 장애물 코스";
                case BlockTemplate.FullFeatureSandbox:
                    return "전체 기능 샌드박스";
                default:
                    return "직접 고른 블록";
            }
        }

        static string PlayerCharacterChoiceLabel(PlayerCharacterChoice value)
        {
            switch (value)
            {
                case PlayerCharacterChoice.Arena:
                    return "아레나 플레이어";
                case PlayerCharacterChoice.Exploration:
                    return "탐험 플레이어";
                case PlayerCharacterChoice.Puzzle:
                    return "퍼즐 플레이어";
                case PlayerCharacterChoice.Platform:
                    return "플랫폼/스페이스 플레이어";
                case PlayerCharacterChoice.Any:
                    return "아무 플레이어";
                default:
                    return "자동 선택";
            }
        }

        static string EnemyCharacterChoiceLabel(EnemyCharacterChoice value)
        {
            switch (value)
            {
                case EnemyCharacterChoice.Boss:
                    return "보스";
                case EnemyCharacterChoice.Zombie:
                    return "좀비";
                case EnemyCharacterChoice.Orc:
                    return "오크";
                case EnemyCharacterChoice.Drone:
                    return "드론";
                case EnemyCharacterChoice.Any:
                    return "아무 적";
                default:
                    return "자동 선택";
            }
        }

        static string DifficultyPresetLabel(DifficultyPreset value)
        {
            switch (value)
            {
                case DifficultyPreset.Story:
                    return "쉬움";
                case DifficultyPreset.Hard:
                    return "어려움";
                case DifficultyPreset.Nightmare:
                    return "악몽";
                default:
                    return "보통";
            }
        }

        static string CameraPresetChoiceLabel(CameraPresetChoice value)
        {
            switch (value)
            {
                case CameraPresetChoice.ThirdPerson:
                    return "3인칭";
                case CameraPresetChoice.QuarterView:
                    return "쿼터뷰";
                case CameraPresetChoice.TopDown:
                    return "탑다운";
                case CameraPresetChoice.SideView:
                    return "사이드뷰";
                default:
                    return "자동 선택";
            }
        }

        static string PlayerMovementChoiceLabel(PlayerMovementChoice value)
        {
            switch (value)
            {
                case PlayerMovementChoice.CameraRelative:
                    return "카메라 기준 이동";
                case PlayerMovementChoice.FacingDirection:
                    return "바라보는 방향 이동";
                default:
                    return "자동 선택";
            }
        }

        static string CharacterKindLabel(CharacterKind value)
        {
            switch (value)
            {
                case CharacterKind.Player:
                    return "플레이어";
                case CharacterKind.Boss:
                    return "보스";
                case CharacterKind.Zombie:
                    return "좀비";
                case CharacterKind.Orc:
                    return "오크";
                case CharacterKind.Drone:
                    return "드론";
                case CharacterKind.Object:
                    return "오브젝트";
                default:
                    return "자동";
            }
        }

        static string CameraViewPresetLabel(VARCOAutoConnectorWindow.CameraViewPreset value)
        {
            switch (value)
            {
                case VARCOAutoConnectorWindow.CameraViewPreset.ThirdPerson:
                    return "3인칭";
                case VARCOAutoConnectorWindow.CameraViewPreset.QuarterView:
                    return "쿼터뷰";
                case VARCOAutoConnectorWindow.CameraViewPreset.TopDown:
                    return "탑다운";
                case VARCOAutoConnectorWindow.CameraViewPreset.SideView:
                    return "사이드뷰";
                default:
                    return value.ToString();
            }
        }

        static string AssetRoleLabel(AssetRole value)
        {
            switch (value)
            {
                case AssetRole.Player:
                    return "플레이어";
                case AssetRole.Enemy:
                    return "적";
                case AssetRole.Weapon:
                    return "무기";
                case AssetRole.ItemPickup:
                    return "수집 아이템";
                case AssetRole.HealthPickup:
                    return "회복 아이템";
                case AssetRole.Goal:
                    return "목표 지점";
                case AssetRole.Door:
                    return "문";
                case AssetRole.PressurePlate:
                    return "압력판";
                case AssetRole.HazardZone:
                    return "위험 구역";
                case AssetRole.MovingPlatform:
                    return "이동 발판";
                case AssetRole.MovableBox:
                    return "밀 수 있는 상자";
                case AssetRole.Checkpoint:
                    return "체크포인트";
                case AssetRole.ArenaCover:
                    return "환경 소품/엄폐물";
                default:
                    return "알 수 없음";
            }
        }

        static BlockTemplate TemplateFor(VWS.GenreType targetGenre, GameRecipe targetRecipe)
        {
            switch (targetRecipe)
            {
                case GameRecipe.CombatWave:
                    return BlockTemplate.ArenaCombatWave;
                case GameRecipe.ExplorationQuest:
                    return BlockTemplate.ExplorationZombieQuest;
                case GameRecipe.DoorPuzzle:
                    return BlockTemplate.PuzzleDoorRoom;
                case GameRecipe.PlatformCourse:
                    return BlockTemplate.PlatformSpaceCourse;
                case GameRecipe.CollectAndEscape:
                    return BlockTemplate.CollectAndEscape;
                case GameRecipe.SurvivalTimer:
                    return BlockTemplate.SurvivalTimer;
                case GameRecipe.BossBattle:
                    return BlockTemplate.ArenaBossBattle;
                case GameRecipe.ZombieSurvival:
                    return BlockTemplate.ExplorationZombieSurvival;
                case GameRecipe.TreasureHunt:
                    return BlockTemplate.ExplorationTreasureHunt;
                case GameRecipe.EscapeRoom:
                    return BlockTemplate.PuzzleEscapeRoom;
                case GameRecipe.ObstacleRun:
                    return BlockTemplate.PlatformObstacleRun;
            }

            switch (targetGenre)
            {
                case VWS.GenreType.Exploration:
                    return BlockTemplate.ExplorationZombieQuest;
                case VWS.GenreType.Puzzle:
                    return BlockTemplate.PuzzleDoorRoom;
                case VWS.GenreType.Platform:
                    return BlockTemplate.PlatformSpaceCourse;
                default:
                    return BlockTemplate.ArenaCombatWave;
            }
        }

        void NormalizeBlockPlan()
        {
            if (blockItems && itemGoal < 1)
            {
                itemGoal = 1;
                log.Add("블록 자동 보정: 수집 목표 개수를 1개로 올렸습니다.");
            }

            if (blockWeapon && !blockPlayer)
            {
                blockPlayer = true;
                log.Add("블록 자동 보정: 무기를 장착할 수 있도록 플레이어 블록을 켰습니다.");
            }

            if (blockFallRespawn && !blockPlayer)
            {
                blockPlayer = true;
                log.Add("블록 자동 보정: 낙사 리스폰 안전망이 동작하도록 플레이어 블록을 켰습니다.");
            }

            if (blockItems && !blockGoal)
            {
                blockGoal = true;
                log.Add("블록 자동 보정: 아이템 수집 후 클리어되도록 목표 지점 블록을 켰습니다.");
            }

            if (blockPuzzleDoor && !blockGoal)
            {
                blockGoal = true;
                log.Add("블록 자동 보정: 퍼즐 완료 후 클리어되도록 목표 지점 블록을 켰습니다.");
            }

            if (!blockGoal && !blockEnemyWave)
            {
                blockGoal = true;
                log.Add("블록 자동 보정: 게임 클리어 조건을 만들기 위해 목표 지점 블록을 켰습니다.");
            }

            waveEnemyCount = Mathf.Max(1, waveEnemyCount);
        }

        void ScanAssets()
        {
            candidates.Clear();
            InvalidateEditorSummaryCache();
            var roots = GameObjectScanRoots();

            if (roots.Length == 0)
                return;

            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", roots))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".prefab" && ext != ".fbx")
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!asset)
                    continue;

                var candidate = ClassifyAsset(path, asset);
                if (candidate.role != AssetRole.Unknown || IsBroadVisualAssetCandidate(candidate))
                    candidates.Add(candidate);
            }

            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            InvalidateEditorSummaryCache();
        }

        AssetCandidate ClassifyAsset(string path, GameObject asset)
        {
            var pathText = Normalize(path + " " + asset.name);
            var internalEvidenceText = BuildAssetInternalEvidenceText(asset, out var internalEvidenceCount);
            var n = Normalize(path + " " + asset.name + " " + internalEvidenceText);
            var pathGenre = GuessGenreFromText(pathText);
            var pathCharacterKind = GuessCharacterKind(pathText);
            var pathRole = GuessRoleFromText(pathText);
            var candidate = new AssetCandidate
            {
                path = path,
                asset = asset,
                role = AssetRole.Unknown,
                genre = GuessGenreFromText(n),
                characterKind = GuessCharacterKind(n),
                isPrefab = Path.GetExtension(path).Equals(".prefab", StringComparison.OrdinalIgnoreCase),
                internalEvidenceCount = internalEvidenceCount,
                normalizedText = n,
                pathNormalizedText = pathText
            };

            var renderers = asset.GetComponentsInChildren<Renderer>(true)
                .Where(r => r && !(r is ParticleSystemRenderer))
                .ToArray();
            candidate.rendererCount = renderers.Length;
            candidate.transformCount = asset.GetComponentsInChildren<Transform>(true).Length;
            candidate.lightCount = asset.GetComponentsInChildren<Light>(true).Length;
            candidate.animatorCount = asset.GetComponentsInChildren<Animator>(true).Length;
            candidate.hasSkinnedMesh = asset.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r && r.sharedMesh);
            candidate.hasVisuals = candidate.rendererCount > 0;

            candidate.role = GuessRoleFromText(n);
            candidate.usedInternalEvidence = internalEvidenceCount > 0
                && ((candidate.role != AssetRole.Unknown && candidate.role != pathRole)
                    || (candidate.genre.HasValue && candidate.genre != pathGenre)
                    || (candidate.characterKind != CharacterKind.None && candidate.characterKind != pathCharacterKind));
            candidate.fromPresetKit = IsPresetKitPath(path);
            candidate.presetKitKey = candidate.fromPresetKit ? PresetKitKeyFromPath(path) : string.Empty;

            candidate.score = 0;
            if (candidate.isPrefab) candidate.score += 80;
            if (candidate.hasVisuals) candidate.score += 26;
            else candidate.score -= 90;
            if (candidate.internalEvidenceCount > 0) candidate.score += 6;
            if (candidate.usedInternalEvidence) candidate.score += 18;
            if (pathText.Contains("/prefabs/")) candidate.score += 22;
            if (pathText.Contains("/varco3dimports/")) candidate.score += 20;
            if (pathText.Contains("/importing_assets/")) candidate.score += 4;
            if (candidate.genre == genre) candidate.score += 50;
            if (candidate.genre.HasValue) candidate.score += 10;
            if (n.Contains("idle")) candidate.score += 12;
            if (n.Contains("walk") || n.Contains("attack") || n.Contains("death") || n.Contains("jump") || n.Contains("push")) candidate.score -= 14;
            if (IsComplexForSimpleFunction(candidate.role, candidate)) candidate.score -= 120;
            else if (candidate.lightCount > 48) candidate.score -= 45;
            if (candidate.role == AssetRole.Player && candidate.characterKind == CharacterKind.Player) candidate.score += 15;
            if (candidate.role == AssetRole.Enemy && candidate.characterKind != CharacterKind.None) candidate.score += 15;
            if (candidate.role == AssetRole.Weapon) candidate.score += n.Contains("sword") ? 30 : 12;
            if (IsCharacterRole(candidate.role))
            {
                if (candidate.animatorCount > 0) candidate.score += 18;
                if (candidate.hasSkinnedMesh) candidate.score += 22;
                if (!candidate.hasSkinnedMesh && candidate.isPrefab) candidate.score -= 8;
            }
            else if (candidate.characterKind == CharacterKind.Object)
            {
                candidate.score += 6;
            }

            candidate.matchReason = BuildBaseAssetReason(candidate, n);
            return candidate;
        }

        static bool IsBroadVisualAssetCandidate(AssetCandidate candidate)
        {
            if (candidate == null || !candidate.hasVisuals)
                return false;

            var text = candidate.normalizedText ?? string.Empty;
            if (candidate.hasSkinnedMesh || candidate.animatorCount > 0)
                return true;

            if (candidate.characterKind == CharacterKind.Object)
                return true;

            return KeywordScoreForRole(text, AssetRole.ArenaCover) >= 100
                || KeywordScoreForRole(text, AssetRole.Goal) >= 100
                || KeywordScoreForRole(text, AssetRole.ItemPickup) >= 100
                || KeywordScoreForRole(text, AssetRole.Checkpoint) >= 100
                || KeywordScoreForRole(text, AssetRole.MovingPlatform) >= 100;
        }

        static bool LooksLikeEnemyText(string text)
        {
            return ContainsAny(text,
                "boss", "zombie", "orc", "drone", "enemy", "monster", "creature", "skeleton", "guard", "robot", "alien", "mutant",
                "적", "몬스터", "괴물", "좀비", "보스", "오크", "드론", "로봇", "스켈레톤", "해골", "경비", "외계인", "뮤턴트");
        }

        static AssetRole GuessRoleFromText(string text)
        {
            var looksLikeEnemy = LooksLikeEnemyText(text);

            if (ContainsAny(text, "player", "hero", "explorer", "adventurer", "astronaut", "knight", "warrior", "avatar",
                    "플레이어", "주인공", "영웅", "탐험가", "우주인", "기사", "전사", "아바타", "캐릭터") && !looksLikeEnemy)
                return AssetRole.Player;
            if (looksLikeEnemy)
                return AssetRole.Enemy;
            if (ContainsAny(text, "healing", "health", "potion", "hp", "medkit", "med_pack",
                    "회복", "체력", "포션", "치료", "구급", "힐"))
                return AssetRole.HealthPickup;
            if (ContainsAny(text, "checkpoint", "checklpoint", "save_point", "respawn", "beacon",
                    "체크포인트", "저장지점", "저장_지점", "리스폰", "부활", "비콘"))
                return AssetRole.Checkpoint;
            if (ContainsAny(text, "item", "pickup", "collectible", "key", "coin", "gem", "jewel", "relic", "treasure", "loot", "token",
                    "아이템", "수집", "수집품", "열쇠", "키", "동전", "코인", "보석", "유물", "보물", "전리품", "토큰"))
                return AssetRole.ItemPickup;
            if (ContainsAny(text, "sword", "weapon", "blade", "axe", "bow", "gun", "rifle", "staff", "wand",
                    "무기", "검", "칼", "블레이드", "도끼", "활", "총", "소총", "지팡이", "마법봉"))
                return AssetRole.Weapon;
            if (ContainsAny(text, "pressure", "plate", "switch", "button", "lever", "trigger_pad",
                    "압력판", "스위치", "버튼", "레버", "발판스위치", "트리거발판"))
                return AssetRole.PressurePlate;
            if (ContainsAny(text, "door", "gate", "portal", "hatch",
                    "문", "게이트", "포탈", "포털", "해치"))
                return AssetRole.Door;
            if (ContainsAny(text, "goal", "finish", "exit", "flag", "crystal", "orb", "portal", "finish_line",
                    "목표", "도착", "탈출", "출구", "깃발", "크리스탈", "수정", "오브", "완주"))
                return AssetRole.Goal;
            if (ContainsAny(text, "hazard", "trap", "spike", "lava", "fire", "acid", "laser", "mine",
                    "위험", "함정", "가시", "용암", "불", "화염", "산성", "레이저", "지뢰", "데미지", "피해"))
                return AssetRole.HazardZone;
            if (ContainsAny(text, "moving", "moving_platform", "platform_moving", "lift", "elevator", "platform_lift",
                    "이동발판", "이동_발판", "움직이는발판", "움직이는_발판", "리프트", "엘리베이터", "승강기"))
                return AssetRole.MovingPlatform;
            if (ContainsAny(text, "box", "crate", "push", "barrel", "container",
                    "상자", "박스", "크레이트", "밀기", "밀수있는", "밀_수_있는", "통", "배럴", "컨테이너"))
                return AssetRole.MovableBox;
            if (ContainsAny(text,
                    "obstacle", "cover", "wall", "rock", "pillar", "column", "fence", "barricade", "ruin", "debris",
                    "tree", "plant", "bush", "grass", "mushroom", "stump", "log", "statue", "bench", "house", "hut",
                    "building", "tower", "bridge", "sign", "lamp", "lantern", "torch", "furniture", "prop", "scenery", "environment",
                    "장애물", "엄폐", "엄폐물", "벽", "바위", "기둥", "울타리", "바리케이드", "폐허", "잔해",
                    "나무", "식물", "수풀", "풀", "버섯", "그루터기", "통나무", "조각상", "벤치", "집", "오두막",
                    "건물", "탑", "다리", "표지판", "램프", "랜턴", "횃불", "가구", "소품", "배경", "환경"))
                return AssetRole.ArenaCover;

            return AssetRole.Unknown;
        }

        static string BuildAssetInternalEvidenceText(GameObject asset, out int evidenceCount)
        {
            var parts = new List<string>();
            evidenceCount = 0;

            if (!asset)
                return string.Empty;

            foreach (var child in asset.GetComponentsInChildren<Transform>(true))
            {
                if (!child || child == asset.transform)
                    continue;

                AddInternalEvidence(parts, child.name, 48);
            }

            foreach (var renderer in asset.GetComponentsInChildren<Renderer>(true).Where(r => r && !(r is ParticleSystemRenderer)).Take(24))
            {
                AddInternalEvidence(parts, renderer.name, 80);

                foreach (var material in renderer.sharedMaterials)
                {
                    if (material)
                        AddInternalEvidence(parts, material.name, 80);
                }
            }

            foreach (var meshFilter in asset.GetComponentsInChildren<MeshFilter>(true).Take(16))
            {
                if (meshFilter && meshFilter.sharedMesh)
                    AddInternalEvidence(parts, meshFilter.sharedMesh.name, 96);
            }

            foreach (var skinned in asset.GetComponentsInChildren<SkinnedMeshRenderer>(true).Take(16))
            {
                if (skinned && skinned.sharedMesh)
                    AddInternalEvidence(parts, skinned.sharedMesh.name, 112);
            }

            foreach (var animator in asset.GetComponentsInChildren<Animator>(true).Take(8))
            {
                AddInternalEvidence(parts, animator.name, 128);
                if (animator.runtimeAnimatorController)
                    AddInternalEvidence(parts, animator.runtimeAnimatorController.name, 128);
            }

            evidenceCount = parts.Count;
            return string.Join(" ", parts);
        }

        static void AddInternalEvidence(List<string> parts, string value, int maxCount)
        {
            if (parts == null || parts.Count >= maxCount || string.IsNullOrWhiteSpace(value))
                return;

            var normalized = Normalize(value);
            if (string.IsNullOrEmpty(normalized) || IsGenericInternalEvidenceName(normalized))
                return;

            if (parts.Any(existing => string.Equals(Normalize(existing), normalized, StringComparison.Ordinal)))
                return;

            parts.Add(value);
        }

        static bool IsGenericInternalEvidenceName(string normalizedName)
        {
            if (string.IsNullOrEmpty(normalizedName))
                return true;

            if (normalizedName.Contains("mixamorig")
                || normalizedName.Contains("hitbox")
                || normalizedName.Contains("hurtbox")
                || normalizedName.Contains("collider")
                || normalizedName.Contains("collision")
                || normalizedName.Contains("socket")
                || normalizedName.Contains("attach"))
                return true;

            var genericNames = new[]
            {
                "root", "armature", "rig", "skeleton", "bone", "joint", "hips", "spine", "neck", "head",
                "shoulder", "arm", "forearm", "hand", "finger", "leg", "upleg", "foot", "toe", "mesh",
                "body", "lod", "lod0", "lod1", "lod2", "pivot", "dummy", "locator", "target", "camera", "light"
            };

            return genericNames.Any(name => normalizedName == name
                || normalizedName.StartsWith(name + "_", StringComparison.Ordinal)
                || normalizedName.EndsWith("_" + name, StringComparison.Ordinal));
        }

        void BuildGenreObjects(VWS.SoundEventRegistry registry)
        {
            var root = EnsureLayoutRoot();
            if (blockPlayer)
            {
                var playerRole = AssetRole.Player;
                var player = EnsureGameplayObject(playerRole, root, PlayerPosition(), PlayerRotation(), "VARCO_Player", PrimitiveType.Capsule, new Vector3(1f, 1.9f, 1f), new Color(0.2f, 0.55f, 1f));
                if (player)
                {
                    ConnectObject(player, genre == VWS.GenreType.Platform ? VARCOAutoConnectorWindow.Role.PlatformPlayer : VARCOAutoConnectorWindow.Role.Player, FindBest(playerRole, genre), 0, registry);
                    AttachWeaponToPlayer(player);
                }
            }

            if (BuildRecipeSpecificLayout(root, registry))
            {
                ConfigureRecipeCamera();
            }
            else switch (genre)
            {
                case VWS.GenreType.Arena:
                    BuildArena(root, registry);
                    break;
                case VWS.GenreType.Exploration:
                    BuildExploration(root, registry);
                    break;
                case VWS.GenreType.Puzzle:
                    BuildPuzzle(root, registry);
                    break;
                case VWS.GenreType.Platform:
                    BuildPlatform(root, registry);
                    break;
            }

            if (blockCountdown)
                EnsureCountdownTimer();
            if (blockFallRespawn)
                EnsureFallRespawnSafety(root);

            FinalizeGeneratedGrounding(root);
            ConfigureRecipeCamera();
        }

        bool BuildRecipeSpecificLayout(Transform root, VWS.SoundEventRegistry registry)
        {
            if (blockTemplate == BlockTemplate.FullFeatureSandbox)
            {
                BuildFullFeatureSandboxLayout(root, registry);
                return true;
            }

            switch (recipe)
            {
                case GameRecipe.CombatWave:
                    BuildArenaCombatWaveLayout(root, registry);
                    return true;
                case GameRecipe.SurvivalTimer:
                    BuildSurvivalTimerLayout(root, registry);
                    return true;
                case GameRecipe.BossBattle:
                    BuildArenaBossBattleLayout(root, registry);
                    return true;
                case GameRecipe.CollectAndEscape:
                    BuildCollectAndEscapeLayout(root, registry);
                    return true;
                case GameRecipe.ExplorationQuest:
                    BuildExplorationQuestLayout(root, registry);
                    return true;
                case GameRecipe.ZombieSurvival:
                    BuildZombieSurvivalLayout(root, registry);
                    return true;
                case GameRecipe.TreasureHunt:
                    BuildTreasureHuntLayout(root, registry);
                    return true;
                case GameRecipe.DoorPuzzle:
                    BuildPuzzleDoorRoomLayout(root, registry);
                    return true;
                case GameRecipe.EscapeRoom:
                    BuildPuzzleEscapeRoomLayout(root, registry);
                    return true;
                case GameRecipe.PlatformCourse:
                    BuildPlatformCourseLayout(root, registry);
                    return true;
                case GameRecipe.ObstacleRun:
                    BuildPlatformObstacleRunLayout(root, registry);
                    return true;
                default:
                    return false;
            }
        }

        void BuildArena(Transform root, VWS.SoundEventRegistry registry)
        {
            if (blockEnemyWave)
            {
                var enemy = EnsureGameplayObject(AssetRole.Enemy, root, new Vector3(5f, 0.1f, 5f), Quaternion.Euler(0f, 210f, 0f), "VARCO_Enemy", PrimitiveType.Capsule, Vector3.one * 1.5f, new Color(0.78f, 0.2f, 0.16f));
                if (enemy)
                    ConnectObject(enemy, VARCOAutoConnectorWindow.Role.Enemy, FindBest(AssetRole.Enemy, genre), 0, registry);
            }

            if (blockHealthPickup)
                EnsureHealthPickup(root, registry, new Vector3(-4f, 0.6f, -3f));

            if (blockCover)
                EnsureEnvironmentProps(root, registry,
                    new[] { new Vector3(0f, 0.9f, 2.8f), new Vector3(-5.2f, 0.9f, 1.2f), new Vector3(4.6f, 0.9f, -1.8f) },
                    new[] { new Vector3(4.2f, 1.8f, 0.8f), new Vector3(2.4f, 1.5f, 1.2f), new Vector3(2.8f, 1.6f, 1.0f) });

            if (blockHazard)
                EnsureHazard(root, registry, new Vector3(3.5f, 0.08f, -2.8f));

            if (blockItems)
                EnsureItems(root, Mathf.Max(1, itemGoal), registry);

            if (blockGoal)
                EnsureGoal(root, Mathf.Max(0, itemGoal), registry, new Vector3(0f, 1f, 8f));
        }

        void BuildExploration(Transform root, VWS.SoundEventRegistry registry)
        {
            if (blockEnemyWave)
            {
                var enemy = EnsureGameplayObject(AssetRole.Enemy, root, new Vector3(4.5f, 0.1f, 6f), Quaternion.Euler(0f, 220f, 0f), "VARCO_Zombie", PrimitiveType.Capsule, Vector3.one * 1.45f, new Color(0.5f, 0.65f, 0.3f));
                if (enemy)
                    ConnectObject(enemy, VARCOAutoConnectorWindow.Role.Enemy, FindBest(AssetRole.Enemy, VWS.GenreType.Exploration), 0, registry);
            }

            if (blockItems)
                EnsureItems(root, Mathf.Max(1, itemGoal), registry);
            if (blockGoal)
                EnsureGoal(root, blockItems ? Mathf.Max(1, itemGoal) : 0, registry, new Vector3(0f, 1f, 12f));
            if (blockCheckpoint)
                EnsureCheckpoint(root, registry, new Vector3(-4f, 0.8f, 2f));
            if (blockHazard)
                EnsureHazard(root, registry, new Vector3(4f, 0.08f, 2.8f));
            if (blockHealthPickup)
                EnsureHealthPickup(root, registry, new Vector3(-4f, 0.6f, -3f));
            if (blockCover)
                EnsureEnvironmentProps(root, registry,
                    new[] { new Vector3(-5.5f, 0.8f, 5.5f), new Vector3(5.5f, 0.8f, 8.5f), new Vector3(-2.4f, 0.8f, 10.2f) },
                    new[] { new Vector3(2.2f, 2.2f, 2.2f), new Vector3(2.6f, 2.0f, 2.6f), new Vector3(1.8f, 1.8f, 1.8f) });
        }

        void BuildPuzzle(Transform root, VWS.SoundEventRegistry registry)
        {
            if (blockPuzzleDoor)
            {
                var door = EnsureGameplayObject(AssetRole.Door, root, new Vector3(0f, 1.5f, 6f), Quaternion.identity, "VARCO_Door", PrimitiveType.Cube, new Vector3(2.6f, 3f, 0.45f), new Color(0.44f, 0.27f, 0.15f));
                if (door)
                    ConnectObject(door, VARCOAutoConnectorWindow.Role.Door, FindBest(AssetRole.Door, genre), 0, registry);

                var plate = EnsureGameplayObject(AssetRole.PressurePlate, root, new Vector3(0f, 0.08f, 2.2f), Quaternion.identity, "VARCO_PressurePlate", PrimitiveType.Cube, new Vector3(2f, 0.16f, 2f), new Color(0.9f, 0.75f, 0.18f));
                if (plate)
                    ConnectObject(plate, VARCOAutoConnectorWindow.Role.PressurePlate, FindBest(AssetRole.PressurePlate, genre), 0, registry);
            }

            if (blockMovableBox)
            {
                var box = EnsureGameplayObject(AssetRole.MovableBox, root, new Vector3(-2.8f, 0.65f, 2.2f), Quaternion.identity, "VARCO_MovableBox", PrimitiveType.Cube, new Vector3(1.2f, 1.2f, 1.2f), new Color(0.5f, 0.36f, 0.18f));
                if (box)
                    ConnectObject(box, VARCOAutoConnectorWindow.Role.MovableBox, FindBest(AssetRole.MovableBox, genre), 0, registry);
            }

            if (blockItems)
                EnsureItems(root, Mathf.Max(1, itemGoal), registry);
            if (blockHazard)
                EnsureHazard(root, registry, new Vector3(4f, 0.08f, 0f));
            if (blockCheckpoint)
                EnsureCheckpoint(root, registry, new Vector3(-4f, 0.8f, -2f));
            if (blockCover)
                EnsureEnvironmentProps(root, registry,
                    new[] { new Vector3(-4.8f, 0.7f, 4.8f), new Vector3(4.8f, 0.7f, 5.2f) },
                    new[] { new Vector3(1.8f, 1.6f, 1.8f), new Vector3(1.6f, 1.4f, 1.6f) });
            if (blockGoal)
                EnsureGoal(root, blockItems ? Mathf.Max(1, itemGoal) : 0, registry, new Vector3(0f, 1f, 10f));
        }

        void BuildPlatform(Transform root, VWS.SoundEventRegistry registry)
        {
            BuildPlatformObstacleRunLayout(root, registry);
        }

        void BuildArenaCombatWaveLayout(Transform root, VWS.SoundEventRegistry registry)
        {
            EnsureArenaFloor(root, "VARCO_Arena_CombatWave_Floor", new Vector3(0f, -0.06f, 1f), new Vector3(22f, 0.18f, 24f), new Color(0.18f, 0.19f, 0.22f));
            CreateLayoutBlock(root, "VARCO_Arena_StartPad", new Vector3(0f, 0.04f, -7.2f), new Vector3(5.4f, 0.06f, 2.8f), new Color(0.16f, 0.4f, 0.82f), false);
            CreateLayoutBlock(root, "VARCO_Arena_MainLane", new Vector3(0f, 0.03f, 0.8f), new Vector3(3.2f, 0.04f, 13.6f), new Color(0.24f, 0.27f, 0.32f), false);
            CreateLayoutBlock(root, "VARCO_Arena_LeftCoverLane", new Vector3(-5.2f, 0.032f, 1.3f), new Vector3(2.2f, 0.04f, 10.2f), new Color(0.22f, 0.24f, 0.28f), false);
            CreateLayoutBlock(root, "VARCO_Arena_RightCoverLane", new Vector3(5.2f, 0.032f, 1.3f), new Vector3(2.2f, 0.04f, 10.2f), new Color(0.22f, 0.24f, 0.28f), false);
            EnsureGuidePads(root, "VARCO_Arena_Guide", new[] { new Vector3(0f, 0.08f, -4.5f), new Vector3(0f, 0.08f, -0.8f), new Vector3(0f, 0.08f, 3.0f), new Vector3(0f, 0.08f, 6.4f) }, new Color(1f, 0.88f, 0.22f));
            EnsureGoalFrame(root, "VARCO_Arena_GoalFrame", new Vector3(0f, 1.45f, 9.4f), 4.6f, 2.4f, new Color(1f, 0.78f, 0.18f));
            EnsureSpawnMarkers(root,
                new[] { new Vector3(-8f, 0.06f, 7.6f), new Vector3(8f, 0.06f, 7.6f), new Vector3(-8.2f, 0.06f, 1.2f), new Vector3(8.2f, 0.06f, 1.2f) },
                new Color(0.85f, 0.18f, 0.16f));

            if (blockEnemyWave)
                EnsureEnemy(root, registry, new Vector3(0f, 0.1f, 6.8f), Quaternion.Euler(0f, 180f, 0f), "VARCO_Arena_Enemy", Vector3.one * 1.45f, new Color(0.78f, 0.2f, 0.16f));
            if (blockHealthPickup)
                EnsureHealthPickup(root, registry, new Vector3(-6.8f, 0.6f, -5.8f));
            if (blockCover)
                EnsureEnvironmentProps(root, registry,
                    new[] { new Vector3(-5.8f, 0.8f, -1.6f), new Vector3(5.8f, 0.8f, -1.6f), new Vector3(-5.6f, 0.8f, 3.3f), new Vector3(5.6f, 0.8f, 3.3f) },
                    new[] { new Vector3(1.9f, 1.15f, 0.85f), new Vector3(1.9f, 1.15f, 0.85f), new Vector3(1.65f, 1.1f, 1.2f), new Vector3(1.65f, 1.1f, 1.2f) });
            if (blockHazard)
                EnsureHazard(root, registry, new Vector3(0f, 0.08f, 9.2f));
            if (blockItems)
                EnsureItemsAtPositions(root, BuildCirclePositions(new Vector3(0f, 0.6f, 1.2f), 4.4f, Mathf.Max(1, itemGoal), -25f, 210f), registry, "VARCO_Arena_Item");
            if (blockGoal)
                EnsureGoal(root, Mathf.Max(0, itemGoal), registry, new Vector3(0f, 1f, 9.4f));

            EnsureLayoutLight(root, "VARCO_Arena_KeyLight", new Vector3(0f, 5.8f, -3.4f), new Color(1f, 0.86f, 0.68f), 1.7f, 15f);
        }

        void BuildSurvivalTimerLayout(Transform root, VWS.SoundEventRegistry registry)
        {
            EnsureArenaFloor(root, "VARCO_Survival_Arena_Floor", Vector3.zero, new Vector3(28f, 0.16f, 28f), new Color(0.13f, 0.15f, 0.16f));
            CreateLayoutBlock(root, "VARCO_Survival_SafeCore", new Vector3(0f, 0.035f, 0f), new Vector3(5.8f, 0.05f, 5.8f), new Color(0.18f, 0.42f, 0.58f), false);
            CreateLayoutBlock(root, "VARCO_Survival_NorthLane", new Vector3(0f, 0.025f, 6.7f), new Vector3(3.2f, 0.04f, 7.6f), new Color(0.18f, 0.22f, 0.24f), false);
            CreateLayoutBlock(root, "VARCO_Survival_EastLane", new Vector3(6.7f, 0.025f, 0f), new Vector3(7.6f, 0.04f, 3.2f), new Color(0.18f, 0.22f, 0.24f), false);
            CreateLayoutBlock(root, "VARCO_Survival_SouthLane", new Vector3(0f, 0.025f, -6.7f), new Vector3(3.2f, 0.04f, 7.6f), new Color(0.18f, 0.22f, 0.24f), false);
            CreateLayoutBlock(root, "VARCO_Survival_WestLane", new Vector3(-6.7f, 0.025f, 0f), new Vector3(7.6f, 0.04f, 3.2f), new Color(0.18f, 0.22f, 0.24f), false);
            EnsureSpawnMarkers(root,
                new[] { new Vector3(0f, 0.06f, 12.2f), new Vector3(12.2f, 0.06f, 0f), new Vector3(0f, 0.06f, -12.2f), new Vector3(-12.2f, 0.06f, 0f) },
                new Color(0.95f, 0.24f, 0.2f));

            if (blockEnemyWave)
                EnsureEnemy(root, registry, new Vector3(9f, 0.1f, 8f), Quaternion.Euler(0f, 225f, 0f), "VARCO_Survival_Enemy", Vector3.one * 1.35f, new Color(0.76f, 0.2f, 0.18f));
            if (blockHealthPickup)
                EnsureHealthPickupsAtPositions(root, new[] { new Vector3(-7f, 0.6f, -6.5f), new Vector3(7f, 0.6f, 6.5f) }, registry, "VARCO_Survival_Health");
            if (blockCover)
                EnsureEnvironmentProps(root, registry,
                    new[] { new Vector3(-4.5f, 0.8f, 3.8f), new Vector3(4.6f, 0.8f, -3.6f), new Vector3(-2.2f, 0.8f, -6.6f), new Vector3(6.6f, 0.8f, 2.3f) },
                    new[] { new Vector3(2.4f, 1.5f, 1f), new Vector3(2.4f, 1.5f, 1f), new Vector3(1.8f, 1.4f, 1.8f), new Vector3(1.8f, 1.4f, 1.8f) });
            if (blockHazard)
                EnsureHazard(root, registry, new Vector3(0f, 0.08f, 11.4f));

            CreateLayoutBlock(root, "VARCO_Survival_TimerFocus", new Vector3(0f, 0.04f, 0f), new Vector3(4.5f, 0.05f, 4.5f), new Color(0.25f, 0.62f, 0.9f), false);
            EnsureLayoutLight(root, "VARCO_Survival_ArenaLight", new Vector3(0f, 6.2f, 0f), new Color(0.7f, 0.88f, 1f), 1.4f, 18f);
        }

        void BuildArenaBossBattleLayout(Transform root, VWS.SoundEventRegistry registry)
        {
            EnsureArenaFloor(root, "VARCO_Boss_Arena_Floor", new Vector3(0f, -0.06f, 0.5f), new Vector3(20f, 0.18f, 18f), new Color(0.16f, 0.13f, 0.15f));
            CreateLayoutBlock(root, "VARCO_Boss_StartPad", new Vector3(0f, 0.04f, -7.2f), new Vector3(4.2f, 0.06f, 2.2f), new Color(0.18f, 0.42f, 0.82f), false);
            CreateLayoutBlock(root, "VARCO_Boss_TargetPad", new Vector3(0f, 0.04f, 7.0f), new Vector3(5.8f, 0.06f, 3.2f), new Color(0.85f, 0.18f, 0.16f), false);
            CreateLayoutBlock(root, "VARCO_Boss_DodgeLane_Left", new Vector3(-5.6f, 0.035f, 0f), new Vector3(2.6f, 0.05f, 11.5f), new Color(0.24f, 0.23f, 0.30f), false);
            CreateLayoutBlock(root, "VARCO_Boss_DodgeLane_Right", new Vector3(5.6f, 0.035f, 0f), new Vector3(2.6f, 0.05f, 11.5f), new Color(0.24f, 0.23f, 0.30f), false);
            CreateLayoutBlock(root, "VARCO_Boss_Telegraph_Line_01", new Vector3(0f, 0.07f, 2.6f), new Vector3(10f, 0.05f, 0.32f), new Color(1f, 0.28f, 0.12f), false);
            CreateLayoutBlock(root, "VARCO_Boss_Telegraph_Line_02", new Vector3(0f, 0.07f, 5.0f), new Vector3(8.0f, 0.05f, 0.32f), new Color(1f, 0.28f, 0.12f), false);
            EnsureGoalFrame(root, "VARCO_Boss_ArenaFrame", new Vector3(0f, 1.55f, 7.8f), 5.8f, 2.8f, new Color(1f, 0.46f, 0.2f));

            GameObject boss = null;
            if (blockEnemyWave)
                boss = EnsureEnemy(root, registry, new Vector3(0f, 0.1f, 7.4f), Quaternion.Euler(0f, 180f, 0f), "VARCO_Boss_Enemy", Vector3.one * 2.35f, new Color(0.72f, 0.1f, 0.08f));
            ConfigureBossEnemy(boss);

            if (blockHealthPickup)
                EnsureHealthPickupsAtPositions(root, new[] { new Vector3(-7.5f, 0.6f, -5.6f), new Vector3(7.5f, 0.6f, -5.6f) }, registry, "VARCO_Boss_Health");
            if (blockCover)
                EnsureEnvironmentProps(root, registry,
                    new[] { new Vector3(-5.4f, 0.9f, -0.5f), new Vector3(5.4f, 0.9f, -0.5f), new Vector3(-3.4f, 0.9f, 3.4f), new Vector3(3.4f, 0.9f, 3.4f) },
                    new[] { new Vector3(2.0f, 1.5f, 1.0f), new Vector3(2.0f, 1.5f, 1.0f), new Vector3(1.5f, 1.4f, 1.5f), new Vector3(1.5f, 1.4f, 1.5f) });
            if (blockHazard)
                EnsureHazard(root, registry, new Vector3(0f, 0.08f, 4.3f));

            EnsureLayoutLight(root, "VARCO_Boss_BackLight", new Vector3(0f, 5.2f, 7f), new Color(1f, 0.52f, 0.24f), 2.1f, 12f);
        }

        void BuildCollectAndEscapeLayout(Transform root, VWS.SoundEventRegistry registry)
        {
            EnsureArenaFloor(root, "VARCO_Collect_Field_Floor", new Vector3(0f, -0.06f, 1.5f), new Vector3(20f, 0.16f, 21f), new Color(0.12f, 0.21f, 0.17f));
            CreateLayoutBlock(root, "VARCO_Collect_StartCamp", new Vector3(0f, 0.04f, -5.2f), new Vector3(5.2f, 0.08f, 3.4f), new Color(0.18f, 0.5f, 0.42f), false);
            CreateLayoutBlock(root, "VARCO_Collect_ExitRing", new Vector3(0f, 0.05f, 9.2f), new Vector3(5f, 0.08f, 2.8f), new Color(0.92f, 0.74f, 0.16f), false);
            CreateLayoutBlock(root, "VARCO_Collect_ReturnLane", new Vector3(0f, 0.03f, 2.2f), new Vector3(3.2f, 0.05f, 12.0f), new Color(0.18f, 0.34f, 0.28f), false);
            EnsureGuidePads(root, "VARCO_Collect_Guide", new[] { new Vector3(0f, 0.08f, -2.3f), new Vector3(-3.1f, 0.08f, 0.4f), new Vector3(3.2f, 0.08f, 2.6f), new Vector3(0f, 0.08f, 6.2f) }, new Color(1f, 0.85f, 0.2f));
            EnsureGoalFrame(root, "VARCO_Collect_ExitFrame", new Vector3(0f, 1.35f, 9.45f), 4.8f, 2.3f, new Color(1f, 0.82f, 0.18f));

            if (blockItems)
            {
                var positions = new[]
                {
                    new Vector3(-4.8f, 0.6f, -0.6f),
                    new Vector3(4.7f, 0.6f, -0.5f),
                    new Vector3(-5.5f, 0.6f, 3.2f),
                    new Vector3(0f, 0.6f, 3.9f),
                    new Vector3(5.5f, 0.6f, 3.1f),
                    new Vector3(0f, 0.6f, 6.2f)
                };
                EnsureItemsAtPositions(root, positions, registry, "VARCO_Collect_Item");
            }
            if (blockGoal)
                EnsureGoal(root, blockItems ? Mathf.Max(1, itemGoal) : 0, registry, new Vector3(0f, 1f, 9.4f));
            if (blockHazard)
                EnsureHazard(root, registry, new Vector3(2.7f, 0.08f, 2.1f));
            if (blockCheckpoint)
                EnsureCheckpoint(root, registry, new Vector3(0f, 0.8f, -1.8f));
            if (blockCover)
                EnsureEnvironmentProps(root, registry,
                    new[] { new Vector3(-7f, 0.9f, 1.2f), new Vector3(7f, 0.9f, 1.4f), new Vector3(-2.8f, 0.9f, 6.6f), new Vector3(3.1f, 0.9f, 6.4f) },
                    new[] { new Vector3(2.1f, 2.2f, 2.1f), new Vector3(2.1f, 2.2f, 2.1f), new Vector3(1.4f, 1.6f, 1.4f), new Vector3(1.4f, 1.6f, 1.4f) });
        }

        void BuildExplorationQuestLayout(Transform root, VWS.SoundEventRegistry registry)
        {
            EnsureArenaFloor(root, "VARCO_Exploration_Field_Floor", new Vector3(0f, -0.06f, 3.2f), new Vector3(22f, 0.16f, 34f), new Color(0.075f, 0.11f, 0.13f));
            CreateLayoutBlock(root, "VARCO_Exploration_StartCamp", new Vector3(0f, 0.065f, -11.1f), new Vector3(7.2f, 0.10f, 3.2f), new Color(0.16f, 0.56f, 0.44f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_Path_Near", new Vector3(0f, 0.07f, -7.0f), new Vector3(5.4f, 0.08f, 5.2f), new Color(0.30f, 0.46f, 0.30f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_Path_Mid", new Vector3(0f, 0.07f, -1.6f), new Vector3(5.0f, 0.08f, 5.8f), new Color(0.28f, 0.42f, 0.29f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_Path_Clearing", new Vector3(0f, 0.075f, 4.0f), new Vector3(8.0f, 0.09f, 5.2f), new Color(0.31f, 0.43f, 0.30f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_Path_Gate", new Vector3(0f, 0.07f, 9.4f), new Vector3(5.8f, 0.08f, 5.2f), new Color(0.32f, 0.43f, 0.29f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_GoalPlaza", new Vector3(0f, 0.075f, 15.3f), new Vector3(7.8f, 0.10f, 4.6f), new Color(0.58f, 0.44f, 0.16f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_LeftBoundary", new Vector3(-7.8f, 0.08f, 2.8f), new Vector3(0.18f, 0.16f, 29.5f), new Color(0.12f, 0.25f, 0.19f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_RightBoundary", new Vector3(7.8f, 0.08f, 2.8f), new Vector3(0.18f, 0.16f, 29.5f), new Color(0.12f, 0.25f, 0.19f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_SidePocket_L", new Vector3(-5.2f, 0.072f, 3.9f), new Vector3(3.8f, 0.08f, 3.2f), new Color(0.18f, 0.34f, 0.25f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_SidePocket_R", new Vector3(5.2f, 0.072f, 7.8f), new Vector3(3.8f, 0.08f, 3.2f), new Color(0.18f, 0.34f, 0.25f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_RouteLine_01", new Vector3(0f, 0.13f, -9.2f), new Vector3(0.34f, 0.06f, 1.1f), new Color(1f, 0.86f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_RouteLine_02", new Vector3(0f, 0.13f, -5.7f), new Vector3(0.34f, 0.06f, 1.1f), new Color(1f, 0.86f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_RouteLine_03", new Vector3(0f, 0.13f, -2.0f), new Vector3(0.34f, 0.06f, 1.1f), new Color(1f, 0.86f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_RouteLine_04", new Vector3(0f, 0.13f, 1.7f), new Vector3(0.34f, 0.06f, 1.1f), new Color(1f, 0.86f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_RouteLine_05", new Vector3(0f, 0.13f, 5.4f), new Vector3(0.34f, 0.06f, 1.1f), new Color(1f, 0.86f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_RouteLine_06", new Vector3(0f, 0.13f, 9.2f), new Vector3(0.34f, 0.06f, 1.1f), new Color(1f, 0.86f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_RouteLine_07", new Vector3(0f, 0.13f, 12.6f), new Vector3(0.34f, 0.06f, 1.1f), new Color(1f, 0.86f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_RouteLine_08", new Vector3(0f, 0.13f, 15.1f), new Vector3(0.34f, 0.06f, 1.1f), new Color(1f, 0.86f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_EnemyZone", new Vector3(2.8f, 0.12f, 8.4f), new Vector3(1.65f, 0.05f, 1.65f), new Color(0.86f, 0.18f, 0.11f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_EnemyZone_02", new Vector3(-3.2f, 0.12f, 12.4f), new Vector3(1.55f, 0.05f, 1.55f), new Color(0.74f, 0.16f, 0.10f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_LandmarkGate_L", new Vector3(-3.2f, 1.45f, 9.45f), new Vector3(0.24f, 2.9f, 0.24f), new Color(0.58f, 0.52f, 0.36f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_LandmarkGate_R", new Vector3(3.2f, 1.45f, 9.45f), new Vector3(0.24f, 2.9f, 0.24f), new Color(0.58f, 0.52f, 0.36f), false);
            CreateLayoutBlock(root, "VARCO_Exploration_LandmarkGate_Top", new Vector3(0f, 2.95f, 9.45f), new Vector3(6.6f, 0.22f, 0.24f), new Color(0.82f, 0.72f, 0.36f), false);
            EnsureGuidePads(root, "VARCO_Exploration_Guide", new[] { new Vector3(0f, 0.09f, -9.2f), new Vector3(-2.4f, 0.09f, -4.6f), new Vector3(2.4f, 0.09f, 0.6f), new Vector3(-2.8f, 0.09f, 4.2f), new Vector3(2.8f, 0.09f, 8.2f), new Vector3(0f, 0.09f, 12.2f), new Vector3(0f, 0.09f, 15.3f) }, new Color(0.96f, 0.78f, 0.18f));
            EnsureGoalFrame(root, "VARCO_Exploration_GoalFrame", new Vector3(0f, 1.35f, 15.45f), 5.4f, 2.5f, new Color(1f, 0.82f, 0.22f));

            var itemPositions = new[]
            {
                new Vector3(-2.6f, 0.6f, -5.0f),
                new Vector3(2.6f, 0.6f, -0.6f),
                new Vector3(-4.8f, 0.6f, 3.8f),
                new Vector3(4.9f, 0.6f, 7.8f),
                new Vector3(-2.8f, 0.6f, 11.6f),
                new Vector3(2.3f, 0.6f, 14.3f)
            };
            for (int i = 0; i < itemPositions.Length; i++)
                CreateLayoutBlock(root, "VARCO_Exploration_ItemPad_" + (i + 1).ToString("00"), itemPositions[i] + new Vector3(0f, -0.5f, 0f), new Vector3(1.35f, 0.07f, 1.35f), new Color(0.28f, 0.88f, 0.46f), false);

            if (blockEnemyWave)
                EnsureEnemy(root, registry, new Vector3(2.8f, 0.1f, 8.4f), Quaternion.Euler(0f, 205f, 0f), "VARCO_Zombie", Vector3.one * 1.35f, new Color(0.5f, 0.65f, 0.3f));
            if (blockItems)
                EnsureItemsAtPositions(root, itemPositions, registry, "VARCO_Exploration_Item");
            if (blockGoal)
                EnsureGoal(root, blockItems ? Mathf.Max(1, itemGoal) : 0, registry, new Vector3(0f, 1f, 15.45f));
            if (blockCheckpoint)
                EnsureCheckpoint(root, registry, new Vector3(0f, 0.8f, 4.0f));
            if (blockHazard)
                EnsureHazard(root, registry, new Vector3(4.8f, 0.08f, 3.1f));
            if (blockHealthPickup)
                EnsureHealthPickup(root, registry, new Vector3(-3.4f, 0.6f, -9.0f));
            if (blockCover)
                EnsureEnvironmentProps(root, registry,
                    new[] { new Vector3(-8.4f, 0.8f, -5.6f), new Vector3(8.2f, 0.8f, -2.2f), new Vector3(-8.0f, 0.8f, 4.4f), new Vector3(8.0f, 0.8f, 8.4f), new Vector3(-7.0f, 0.8f, 13.5f), new Vector3(7.2f, 0.8f, 14.2f) },
                    new[] { new Vector3(1.55f, 1.8f, 1.55f), new Vector3(1.45f, 1.7f, 1.45f), new Vector3(1.8f, 1.9f, 1.4f), new Vector3(1.5f, 1.8f, 1.8f), new Vector3(1.8f, 1.7f, 1.5f), new Vector3(1.7f, 1.8f, 1.7f) });
        }

        void BuildZombieSurvivalLayout(Transform root, VWS.SoundEventRegistry registry)
        {
            EnsureArenaFloor(root, "VARCO_Zombie_Route_Floor", new Vector3(0f, -0.06f, 0f), new Vector3(24f, 0.16f, 24f), new Color(0.09f, 0.105f, 0.1f));
            CreateLayoutBlock(root, "VARCO_Zombie_SafeStart", new Vector3(-8f, 0.05f, -8f), new Vector3(5f, 0.08f, 4f), new Color(0.18f, 0.4f, 0.34f), false);
            CreateLayoutBlock(root, "VARCO_Zombie_EscapeMarker", new Vector3(8.5f, 0.05f, 8.5f), new Vector3(4.5f, 0.08f, 3.2f), new Color(0.78f, 0.55f, 0.14f), false);
            CreateLayoutBlock(root, "VARCO_Zombie_CorridorHint", new Vector3(0f, 0.04f, 0f), new Vector3(4f, 0.05f, 20f), new Color(0.18f, 0.18f, 0.17f), false);
            CreateLayoutBlock(root, "VARCO_Zombie_SafeWall_A", new Vector3(-8f, 0.9f, -5.8f), new Vector3(5f, 1.6f, 0.24f), new Color(0.20f, 0.28f, 0.24f), false);
            CreateLayoutBlock(root, "VARCO_Zombie_SafeWall_B", new Vector3(-5.4f, 0.9f, -8f), new Vector3(0.24f, 1.6f, 4f), new Color(0.20f, 0.28f, 0.24f), false);
            EnsureGuidePads(root, "VARCO_Zombie_Guide", new[] { new Vector3(-5.5f, 0.08f, -5.5f), new Vector3(-2.8f, 0.08f, -2.8f), new Vector3(0.2f, 0.08f, 0.2f), new Vector3(3.4f, 0.08f, 3.4f), new Vector3(6.2f, 0.08f, 6.2f) }, new Color(0.98f, 0.74f, 0.18f));
            EnsureGoalFrame(root, "VARCO_Zombie_EscapeFrame", new Vector3(8.5f, 1.35f, 8.6f), 4.2f, 2.3f, new Color(1f, 0.78f, 0.18f));

            if (blockEnemyWave)
                EnsureEnemy(root, registry, new Vector3(5.8f, 0.1f, 5.8f), Quaternion.Euler(0f, 225f, 0f), "VARCO_Zombie_SurvivalEnemy", Vector3.one * 1.45f, new Color(0.42f, 0.58f, 0.28f));
            EnsureSpawnMarkers(root,
                new[] { new Vector3(3.5f, 0.06f, 8.5f), new Vector3(8.5f, 0.06f, 2.5f), new Vector3(-2.5f, 0.06f, 6.2f), new Vector3(6.5f, 0.06f, -2.2f) },
                new Color(0.65f, 0.12f, 0.1f));
            if (blockCheckpoint)
                EnsureCheckpoint(root, registry, new Vector3(-2.5f, 0.8f, -2.5f));
            if (blockHealthPickup)
                EnsureHealthPickup(root, registry, new Vector3(0f, 0.6f, 0.5f));
            if (blockHazard)
                EnsureHazard(root, registry, new Vector3(3.2f, 0.08f, 2.8f));
            if (blockCover)
                EnsureEnvironmentProps(root, registry,
                    new[] { new Vector3(-5.8f, 0.9f, -0.5f), new Vector3(-1.0f, 0.9f, 4.4f), new Vector3(4.2f, 0.9f, -4.4f), new Vector3(6.6f, 0.9f, 4.8f) },
                    new[] { new Vector3(2.2f, 2.2f, 1.2f), new Vector3(1.4f, 2.0f, 2.8f), new Vector3(2.5f, 2.0f, 1.2f), new Vector3(1.2f, 2.0f, 2.5f) });
        }

        void BuildTreasureHuntLayout(Transform root, VWS.SoundEventRegistry registry)
        {
            EnsureArenaFloor(root, "VARCO_Treasure_Field_Floor", new Vector3(0f, -0.06f, 2f), new Vector3(26f, 0.16f, 24f), new Color(0.11f, 0.2f, 0.15f));
            CreateLayoutBlock(root, "VARCO_Treasure_StartCamp", new Vector3(-8f, 0.05f, -6.2f), new Vector3(5.2f, 0.08f, 3.6f), new Color(0.18f, 0.42f, 0.36f), false);
            CreateLayoutBlock(root, "VARCO_Treasure_Landmark", new Vector3(0f, 1.5f, 2.8f), new Vector3(1.4f, 3.0f, 1.4f), new Color(0.36f, 0.34f, 0.30f));
            CreateLayoutBlock(root, "VARCO_Treasure_MainTrail", new Vector3(0f, 0.03f, 2.0f), new Vector3(4.2f, 0.05f, 16.0f), new Color(0.22f, 0.32f, 0.24f), false);
            CreateLayoutBlock(root, "VARCO_Treasure_StartGate_L", new Vector3(-10.4f, 1.15f, -4.2f), new Vector3(0.22f, 2.2f, 0.22f), new Color(0.78f, 0.68f, 0.34f), false);
            CreateLayoutBlock(root, "VARCO_Treasure_StartGate_R", new Vector3(-5.7f, 1.15f, -4.2f), new Vector3(0.22f, 2.2f, 0.22f), new Color(0.78f, 0.68f, 0.34f), false);
            CreateLayoutBlock(root, "VARCO_Treasure_StartGate_Top", new Vector3(-8.05f, 2.3f, -4.2f), new Vector3(4.9f, 0.22f, 0.22f), new Color(0.92f, 0.76f, 0.28f), false);
            CreateLayoutBlock(root, "VARCO_Treasure_Route_Segment_01", new Vector3(-5.4f, 0.09f, -2.5f), new Vector3(2.8f, 0.05f, 0.55f), new Color(0.94f, 0.72f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Treasure_Route_Segment_02", new Vector3(-2.6f, 0.09f, -0.2f), new Vector3(0.55f, 0.05f, 3.0f), new Color(0.94f, 0.72f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Treasure_Route_Segment_03", new Vector3(1.0f, 0.09f, 3.0f), new Vector3(4.2f, 0.05f, 0.55f), new Color(0.94f, 0.72f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Treasure_Route_Segment_04", new Vector3(5.9f, 0.09f, 6.7f), new Vector3(0.55f, 0.05f, 4.4f), new Color(0.94f, 0.72f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Treasure_LeftRuins", new Vector3(-8.7f, 0.5f, 3.0f), new Vector3(2.2f, 0.9f, 4.8f), new Color(0.22f, 0.28f, 0.24f), false);
            CreateLayoutBlock(root, "VARCO_Treasure_RightRuins", new Vector3(8.9f, 0.5f, 2.1f), new Vector3(2.2f, 0.9f, 5.0f), new Color(0.22f, 0.28f, 0.24f), false);
            CreateLayoutBlock(root, "VARCO_Treasure_LookoutPad", new Vector3(0f, 0.12f, 2.8f), new Vector3(3.8f, 0.08f, 3.8f), new Color(0.32f, 0.30f, 0.24f), false);
            CreateLayoutBlock(root, "VARCO_Treasure_ReturnBeaconBase", new Vector3(9f, 0.08f, 9.3f), new Vector3(4.8f, 0.08f, 3.8f), new Color(0.88f, 0.64f, 0.16f), false);
            EnsureGuidePads(root, "VARCO_Treasure_Guide", new[] { new Vector3(-6f, 0.08f, -3.5f), new Vector3(-3f, 0.08f, -1.2f), new Vector3(0f, 0.08f, 2.2f), new Vector3(3.2f, 0.08f, 5.2f), new Vector3(6.6f, 0.08f, 7.5f) }, new Color(0.96f, 0.78f, 0.2f));
            EnsureGoalFrame(root, "VARCO_Treasure_GoalFrame", new Vector3(9f, 1.45f, 9.3f), 4.6f, 2.5f, new Color(1f, 0.82f, 0.18f));

            var treasurePositions = new[]
            {
                new Vector3(-3.8f, 0.75f, -1.5f),
                new Vector3(4.8f, 0.75f, 0.7f),
                new Vector3(-6.5f, 0.75f, 5.8f),
                new Vector3(2.2f, 1.05f, 6.8f),
                new Vector3(7.7f, 0.75f, 8.6f),
                new Vector3(-0.5f, 1.2f, 9.0f)
            };
            for (int i = 0; i < treasurePositions.Length; i++)
            {
                CreateLayoutBlock(root, "VARCO_Treasure_Pedestal_" + (i + 1).ToString("00"), treasurePositions[i] + Vector3.down * 0.38f, new Vector3(1.2f, 0.35f, 1.2f), new Color(0.32f, 0.28f, 0.22f));
                CreateLayoutPrimitive(root, "VARCO_Treasure_BeaconRing_" + (i + 1).ToString("00"), PrimitiveType.Cylinder, treasurePositions[i] + Vector3.down * 0.58f, new Vector3(1.65f, 0.035f, 1.65f), new Color(0.95f, 0.74f, 0.18f), false);
            }
            if (blockItems)
                EnsureItemsAtPositions(root, treasurePositions, registry, "VARCO_Treasure_Item");
            if (blockGoal)
                EnsureGoal(root, blockItems ? Mathf.Max(1, itemGoal) : 0, registry, new Vector3(9f, 1f, 9.3f));
            if (blockCheckpoint)
                EnsureCheckpoint(root, registry, new Vector3(-2.5f, 0.8f, -3.0f));
            if (blockHealthPickup)
                EnsureHealthPickup(root, registry, new Vector3(3.8f, 0.6f, -4.2f));
            if (blockHazard)
                EnsureHazard(root, registry, new Vector3(5.8f, 0.08f, 4.5f));
            if (blockCover)
                EnsureEnvironmentProps(root, registry,
                    new[] { new Vector3(-8.5f, 0.9f, 2.5f), new Vector3(7.5f, 0.9f, 3.2f), new Vector3(-3.5f, 0.9f, 9.5f) },
                    new[] { new Vector3(2.4f, 2.4f, 2.4f), new Vector3(2.2f, 2.2f, 2.2f), new Vector3(2.0f, 2.0f, 2.0f) });
        }

        void BuildPuzzleDoorRoomLayout(Transform root, VWS.SoundEventRegistry registry)
        {
            EnsurePuzzleRoomShell(root, "VARCO_PuzzleRoom", new Vector3(0f, 0f, 0.5f), new Vector3(12f, 0.16f, 15f));
            CreateLayoutBlock(root, "VARCO_Puzzle_StartPad", new Vector3(-3.2f, 0.055f, -5.2f), new Vector3(3.1f, 0.05f, 2.1f), new Color(0.16f, 0.42f, 0.76f), false);
            CreateLayoutBlock(root, "VARCO_Puzzle_BoxBay", new Vector3(-3.5f, 0.055f, -1.3f), new Vector3(2.0f, 0.05f, 2.0f), new Color(0.46f, 0.34f, 0.20f), false);
            CreateLayoutBlock(root, "VARCO_Puzzle_BoxToPlateLane", new Vector3(-2.65f, 0.045f, 0.05f), new Vector3(0.6f, 0.045f, 2.7f), new Color(0.22f, 0.44f, 0.48f), false);
            CreateLayoutBlock(root, "VARCO_Puzzle_PlateGuide", new Vector3(-1.8f, 0.055f, 1.4f), new Vector3(2.7f, 0.05f, 2.7f), new Color(0.9f, 0.72f, 0.16f), false);
            CreateLayoutPrimitive(root, "VARCO_Puzzle_Plate_Ring", PrimitiveType.Cylinder, new Vector3(-1.8f, 0.13f, 1.4f), new Vector3(2.55f, 0.035f, 2.55f), new Color(1f, 0.9f, 0.22f), false);
            CreateLayoutBlock(root, "VARCO_Puzzle_DoorCable_Z", new Vector3(0.7f, 0.06f, 2.8f), new Vector3(0.18f, 0.05f, 3.0f), new Color(0.95f, 0.78f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Puzzle_DoorCable_X", new Vector3(1.9f, 0.06f, 4.2f), new Vector3(2.6f, 0.05f, 0.18f), new Color(0.95f, 0.78f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Puzzle_DoorThreshold", new Vector3(3.2f, 0.07f, 4.2f), new Vector3(3.8f, 0.06f, 1.2f), new Color(0.82f, 0.62f, 0.14f), false);
            CreateLayoutBlock(root, "VARCO_Puzzle_ExitLane", new Vector3(3.8f, 0.05f, 5.6f), new Vector3(1.5f, 0.05f, 2.2f), new Color(0.24f, 0.48f, 0.38f), false);
            EnsureDoorPuzzleElements(root, registry,
                new Vector3(3.2f, 1.5f, 4.2f),
                new Vector3(-1.8f, 0.08f, 1.4f),
                new Vector3(-3.5f, 0.65f, -1.3f),
                new Vector3(4.1f, 1f, 6.0f));
            if (blockCover)
            {
                CreateLayoutBlock(root, "VARCO_Puzzle_L_PathMarker", new Vector3(0.8f, 0.04f, 1.4f), new Vector3(4.2f, 0.05f, 1.0f), new Color(0.22f, 0.44f, 0.48f), false);
                CreateLayoutBlock(root, "VARCO_Puzzle_GuideRail_L", new Vector3(-4.9f, 0.8f, 0.0f), new Vector3(0.18f, 1.2f, 5.6f), new Color(0.25f, 0.28f, 0.30f), false);
                CreateLayoutBlock(root, "VARCO_Puzzle_GuideRail_R", new Vector3(1.5f, 0.8f, -0.9f), new Vector3(0.18f, 1.2f, 3.8f), new Color(0.25f, 0.28f, 0.30f), false);
            }
            EnsureLayoutLight(root, "VARCO_Puzzle_PlateLight", new Vector3(-1.8f, 3.2f, 1.4f), new Color(1f, 0.84f, 0.36f), 1.2f, 6.5f);
        }

        void BuildPuzzleEscapeRoomLayout(Transform root, VWS.SoundEventRegistry registry)
        {
            EnsurePuzzleRoomShell(root, "VARCO_EscapeRoom", new Vector3(0f, 0f, 0.8f), new Vector3(16f, 0.16f, 19f));
            CreateLayoutBlock(root, "VARCO_Escape_StartPad", new Vector3(0f, 0.055f, -5.8f), new Vector3(3.8f, 0.05f, 2.4f), new Color(0.16f, 0.40f, 0.74f), false);
            CreateLayoutBlock(root, "VARCO_Escape_CenterHub", new Vector3(0f, 0.06f, 0.8f), new Vector3(4.2f, 0.05f, 3.2f), new Color(0.24f, 0.24f, 0.28f), false);
            CreateLayoutBlock(root, "VARCO_Escape_LeftBranch", new Vector3(-3.4f, 0.055f, 1.6f), new Vector3(3.6f, 0.05f, 1.0f), new Color(0.18f, 0.36f, 0.48f), false);
            CreateLayoutBlock(root, "VARCO_Escape_RightBranch", new Vector3(3.6f, 0.055f, 2.0f), new Vector3(3.6f, 0.05f, 1.0f), new Color(0.18f, 0.36f, 0.48f), false);
            CreateLayoutBlock(root, "VARCO_Escape_InternalWall_L", new Vector3(-1.9f, 0.95f, 3.0f), new Vector3(0.28f, 1.7f, 3.3f), new Color(0.24f, 0.25f, 0.28f), false);
            CreateLayoutBlock(root, "VARCO_Escape_InternalWall_R", new Vector3(1.9f, 0.95f, 3.0f), new Vector3(0.28f, 1.7f, 3.3f), new Color(0.24f, 0.25f, 0.28f), false);
            CreateLayoutBlock(root, "VARCO_Escape_PlateGuide", new Vector3(2.8f, 0.055f, 1.0f), new Vector3(2.8f, 0.05f, 2.8f), new Color(0.9f, 0.72f, 0.16f), false);
            CreateLayoutBlock(root, "VARCO_Escape_SearchZone_L", new Vector3(-4.6f, 0.05f, 2.7f), new Vector3(2.2f, 0.06f, 2.2f), new Color(0.20f, 0.42f, 0.56f), false);
            CreateLayoutBlock(root, "VARCO_Escape_SearchZone_R", new Vector3(4.8f, 0.05f, 3.6f), new Vector3(2.2f, 0.06f, 2.2f), new Color(0.20f, 0.42f, 0.56f), false);
            CreateLayoutPrimitive(root, "VARCO_Escape_LeftKeyRing", PrimitiveType.Cylinder, new Vector3(-4.6f, 0.13f, 2.7f), new Vector3(1.85f, 0.035f, 1.85f), new Color(0.35f, 0.72f, 1f), false);
            CreateLayoutPrimitive(root, "VARCO_Escape_RightKeyRing", PrimitiveType.Cylinder, new Vector3(4.8f, 0.13f, 3.6f), new Vector3(1.85f, 0.035f, 1.85f), new Color(0.35f, 0.72f, 1f), false);
            CreateLayoutBlock(root, "VARCO_Escape_DoorCable", new Vector3(1.4f, 0.06f, 3.3f), new Vector3(0.18f, 0.05f, 5.0f), new Color(0.95f, 0.78f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Escape_DoorCable_Left", new Vector3(-2.8f, 0.065f, 3.8f), new Vector3(3.2f, 0.05f, 0.18f), new Color(0.95f, 0.78f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Escape_DoorCable_Right", new Vector3(3.7f, 0.065f, 4.2f), new Vector3(3.2f, 0.05f, 0.18f), new Color(0.95f, 0.78f, 0.18f), false);
            EnsureDoorPuzzleElements(root, registry,
                new Vector3(0f, 1.5f, 5.6f),
                new Vector3(2.8f, 0.08f, 1.0f),
                new Vector3(-2.9f, 0.65f, -1.3f),
                new Vector3(0f, 1f, 7.7f));
            if (blockItems)
                EnsureItemsAtPositions(root, new[] { new Vector3(-4.6f, 0.6f, 2.7f), new Vector3(4.8f, 0.6f, 3.6f) }, registry, "VARCO_Escape_Item");
            CreateLayoutBlock(root, "VARCO_Escape_ExitArrow", new Vector3(0f, 0.12f, 6.75f), new Vector3(3.2f, 0.06f, 0.45f), new Color(0.95f, 0.78f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Escape_ExitPad", new Vector3(0f, 0.06f, 7.7f), new Vector3(4.8f, 0.05f, 2.4f), new Color(0.72f, 0.54f, 0.18f), false);
            EnsureLayoutLight(root, "VARCO_Escape_DoorLight", new Vector3(0f, 4.2f, 5.4f), new Color(0.9f, 0.78f, 0.48f), 1.6f, 8f);
            EnsureLayoutLight(root, "VARCO_Escape_SearchLight_L", new Vector3(-4.6f, 3.2f, 2.7f), new Color(0.45f, 0.72f, 1f), 0.9f, 5.5f);
            EnsureLayoutLight(root, "VARCO_Escape_SearchLight_R", new Vector3(4.8f, 3.2f, 3.6f), new Color(0.45f, 0.72f, 1f), 0.9f, 5.5f);
        }

        void BuildPlatformCourseLayout(Transform root, VWS.SoundEventRegistry registry)
        {
            EnsureIntroPlatformCourse(root);
            if (blockMovingPlatform)
            {
                var moving = EnsureGameplayObject(AssetRole.MovingPlatform, root, new Vector3(2.8f, 1.28f, 0f), Quaternion.identity, "VARCO_MovingPlatform", PrimitiveType.Cube, new Vector3(2.4f, 0.32f, 3.0f), new Color(0.15f, 0.48f, 0.9f));
                if (moving)
                {
                    ConnectObject(moving, VARCOAutoConnectorWindow.Role.MovingPlatform, FindBest(AssetRole.MovingPlatform, genre), 0, registry);
                    ConfigurePlatformMovingPlatform(moving, root, new Vector3(2.8f, 1.28f, 0f), new Vector3(1.6f, 1.28f, 0f), new Vector3(4.0f, 1.28f, 0f), new Vector3(2.1f, 0.3f, 2.8f));
                }
            }
            if (blockCheckpoint)
                EnsureCheckpoint(root, registry, new Vector3(0.2f, 1.55f, 0f));
            if (blockHazard)
            {
                var hazard = CreatePrimitiveGameplayObject("VARCO_Platform_Intro_Hazard", PrimitiveType.Cube, new Vector3(-2.0f, 1.08f, 0f), Quaternion.identity, new Vector3(2.4f, 0.16f, 3.6f), new Color(1f, 0.08f, 0.03f));
                hazard.transform.SetParent(root, true);
                ConnectObject(hazard, VARCOAutoConnectorWindow.Role.HazardZone, null, 0, registry);
                ConfigurePlatformHazard(hazard, new Vector3(-2.0f, 1.08f, 0f), new Vector3(2.4f, 0.16f, 3.6f));
            }
            if (blockItems)
                EnsurePlatformItems(root, Mathf.Clamp(itemGoal, 1, 3), registry);
            if (blockHealthPickup)
                EnsureHealthPickup(root, registry, new Vector3(6.8f, 1.85f, 1.1f));
            if (blockCover)
                EnsureIntroPlatformMarkers(root);
            if (blockGoal)
                EnsureGoal(root, 0, registry, new Vector3(8.7f, 2.05f, 0f));

            ConfigurePlatformCamera();
        }

        void BuildPlatformObstacleRunLayout(Transform root, VWS.SoundEventRegistry registry)
        {
            EnsurePlatformCourse(root);

            if (blockMovingPlatform)
            {
                var moving = EnsureGameplayObject(AssetRole.MovingPlatform, root, new Vector3(4.2f, 1.45f, 0f), Quaternion.identity, "VARCO_MovingPlatform", PrimitiveType.Cube, new Vector3(3.2f, 0.35f, 3.2f), new Color(0.15f, 0.48f, 0.9f));
                if (moving)
                {
                    ConnectObject(moving, VARCOAutoConnectorWindow.Role.MovingPlatform, FindBest(AssetRole.MovingPlatform, genre), 0, registry);
                    ConfigurePlatformMovingPlatform(moving, root);
                }
            }

            if (blockCheckpoint)
                EnsureCheckpoint(root, registry, new Vector3(8.0f, 2.25f, 0f));
            if (blockHazard)
            {
                var hazard = EnsurePlatformHazard(root, registry);
                ConfigurePlatformHazard(hazard);
            }
            if (blockItems)
                EnsurePlatformItems(root, Mathf.Clamp(itemGoal, 1, 4), registry);
            if (blockHealthPickup)
                EnsureHealthPickup(root, registry, new Vector3(14.6f, 2.65f, 1.5f));
            if (blockCover)
                EnsurePlatformCourseMarkers(root);
            if (blockGoal)
                EnsureGoal(root, 0, registry, new Vector3(16.7f, 2.85f, 0f));

            ConfigurePlatformCamera();
        }

        void BuildFullFeatureSandboxLayout(Transform root, VWS.SoundEventRegistry registry)
        {
            EnsureArenaFloor(root, "VARCO_Sandbox_Floor", new Vector3(0f, -0.06f, 1f), new Vector3(30f, 0.16f, 22f), new Color(0.13f, 0.15f, 0.17f));
            CreateLayoutBlock(root, "VARCO_Sandbox_StartHub", new Vector3(-12f, 0.055f, -6f), new Vector3(4.6f, 0.06f, 3.8f), new Color(0.16f, 0.42f, 0.78f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_StartRail_L", new Vector3(-12f, 0.55f, -8.0f), new Vector3(4.6f, 0.18f, 0.24f), new Color(0.62f, 0.82f, 1f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_StartRail_R", new Vector3(-12f, 0.55f, -4.0f), new Vector3(4.6f, 0.18f, 0.24f), new Color(0.62f, 0.82f, 1f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_TourSpine", new Vector3(-4.0f, 0.055f, -5.8f), new Vector3(14.0f, 0.05f, 0.85f), new Color(0.84f, 0.70f, 0.22f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_CombatZone", new Vector3(-8f, 0.04f, 4f), new Vector3(8f, 0.05f, 7f), new Color(0.35f, 0.18f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_PuzzleZone", new Vector3(5.5f, 0.04f, 3.8f), new Vector3(9f, 0.05f, 7f), new Color(0.18f, 0.28f, 0.38f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_PlatformZone", new Vector3(0f, 0.04f, -6f), new Vector3(17f, 0.05f, 5f), new Color(0.20f, 0.36f, 0.24f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_CombatGate_L", new Vector3(-11.9f, 1.05f, 0.2f), new Vector3(0.22f, 2.0f, 0.22f), new Color(0.95f, 0.24f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_CombatGate_R", new Vector3(-4.1f, 1.05f, 0.2f), new Vector3(0.22f, 2.0f, 0.22f), new Color(0.95f, 0.24f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_CombatGate_Top", new Vector3(-8f, 2.1f, 0.2f), new Vector3(8.0f, 0.22f, 0.22f), new Color(0.95f, 0.24f, 0.18f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_PuzzleGate_L", new Vector3(1.0f, 1.05f, 0.2f), new Vector3(0.22f, 2.0f, 0.22f), new Color(0.36f, 0.72f, 1f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_PuzzleGate_R", new Vector3(10.0f, 1.05f, 0.2f), new Vector3(0.22f, 2.0f, 0.22f), new Color(0.36f, 0.72f, 1f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_PuzzleGate_Top", new Vector3(5.5f, 2.1f, 0.2f), new Vector3(9.2f, 0.22f, 0.22f), new Color(0.36f, 0.72f, 1f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_PlatformGate_L", new Vector3(-8.4f, 0.9f, -3.4f), new Vector3(0.22f, 1.7f, 0.22f), new Color(0.42f, 1f, 0.58f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_PlatformGate_R", new Vector3(8.4f, 0.9f, -3.4f), new Vector3(0.22f, 1.7f, 0.22f), new Color(0.42f, 1f, 0.58f), false);
            CreateLayoutBlock(root, "VARCO_Sandbox_PlatformGate_Top", new Vector3(0f, 1.78f, -3.4f), new Vector3(17f, 0.22f, 0.22f), new Color(0.42f, 1f, 0.58f), false);
            EnsureGuidePads(root, "VARCO_Sandbox_TourGuide", new[] { new Vector3(-10.8f, 0.08f, -5.5f), new Vector3(-5.8f, 0.08f, -5.6f), new Vector3(0f, 0.08f, -5.8f), new Vector3(5.6f, 0.08f, -1.8f), new Vector3(9.8f, 0.08f, 3.7f) }, new Color(1f, 0.86f, 0.22f));
            EnsureGoalFrame(root, "VARCO_Sandbox_GoalFrame", new Vector3(11.2f, 1.35f, -5.8f), 4.0f, 2.3f, new Color(1f, 0.82f, 0.18f));

            if (blockEnemyWave)
                ConfigureSandboxDemoEnemy(EnsureEnemy(root, registry, new Vector3(-8f, 0.1f, 6.8f), Quaternion.Euler(0f, 210f, 0f), "VARCO_Sandbox_Enemy", Vector3.one * 1.35f, new Color(0.78f, 0.2f, 0.16f)));
            if (blockItems)
                EnsureItemsAtPositions(root, new[] { new Vector3(-5.2f, 0.6f, -6f), new Vector3(-1.8f, 0.6f, -6f), new Vector3(1.8f, 0.6f, -6f), new Vector3(5.2f, 0.6f, -6f), new Vector3(8.6f, 0.6f, -6f) }, registry, "VARCO_Sandbox_Item");
            if (blockPuzzleDoor)
                EnsureDoorPuzzleElements(root, registry, new Vector3(7.8f, 1.5f, 5.6f), new Vector3(3.8f, 0.08f, 2.8f), new Vector3(2.0f, 0.65f, 1.0f), new Vector3(10.7f, 1f, 6.2f));
            if (blockGoal)
                EnsureGoal(root, blockItems ? Mathf.Max(1, itemGoal) : 0, registry, new Vector3(11.2f, 1f, -5.8f));
            if (blockHazard)
                EnsureHazard(root, registry, new Vector3(-8.2f, 0.08f, 1.2f));
            if (blockHealthPickup)
                EnsureHealthPickup(root, registry, new Vector3(-12f, 0.6f, 0.5f));
            if (blockCheckpoint)
                EnsureCheckpoint(root, registry, new Vector3(-4.8f, 0.8f, -5.8f));
            if (blockMovingPlatform)
            {
                var moving = EnsureGameplayObject(AssetRole.MovingPlatform, root, new Vector3(1.2f, 0.85f, -6f), Quaternion.identity, "VARCO_Sandbox_MovingPlatform", PrimitiveType.Cube, new Vector3(2.8f, 0.32f, 2.8f), new Color(0.15f, 0.48f, 0.9f));
                if (moving)
                {
                    ConnectObject(moving, VARCOAutoConnectorWindow.Role.MovingPlatform, FindBest(AssetRole.MovingPlatform, genre), 0, registry);
                    ConfigurePlatformMovingPlatform(moving, root, new Vector3(1.2f, 0.85f, -6f), new Vector3(-0.6f, 0.85f, -6f), new Vector3(3.0f, 0.85f, -6f), new Vector3(2.8f, 0.32f, 2.8f));
                }
            }
            if (blockCover)
                EnsureEnvironmentProps(root, registry,
                    new[] { new Vector3(-11f, 0.9f, 5.8f), new Vector3(-5.2f, 0.9f, 2.2f), new Vector3(5.5f, 0.9f, 0.4f), new Vector3(12.2f, 0.9f, 3.4f) },
                    new[] { new Vector3(1.5f, 1.5f, 1.5f), new Vector3(2.0f, 1.5f, 1.0f), new Vector3(1.5f, 1.5f, 1.5f), new Vector3(1.5f, 1.5f, 1.5f) });
            EnsureLayoutLight(root, "VARCO_Sandbox_StartLight", new Vector3(-12f, 3.4f, -6f), new Color(0.55f, 0.76f, 1f), 1.0f, 6f);
            EnsureLayoutLight(root, "VARCO_Sandbox_CombatLight", new Vector3(-8f, 3.8f, 5.8f), new Color(1f, 0.42f, 0.32f), 1.0f, 7f);
            EnsureLayoutLight(root, "VARCO_Sandbox_PuzzleLight", new Vector3(6.2f, 3.8f, 4.2f), new Color(0.45f, 0.72f, 1f), 1.0f, 7f);
        }

        void EnsureArenaFloor(Transform root, string name, Vector3 position, Vector3 scale, Color color)
        {
            if (!root || !createMissingObjects)
                return;

            CreateLayoutBlock(root, name, position, scale, color);
        }

        void EnsureGuidePads(Transform root, string prefix, Vector3[] positions, Color color)
        {
            if (!root || positions == null || !createMissingObjects)
                return;

            for (int i = 0; i < positions.Length; i++)
                CreateLayoutBlock(root, prefix + "_" + (i + 1).ToString("00"), positions[i], new Vector3(0.9f, 0.05f, 0.9f), color, false);
        }

        void EnsureGoalFrame(Transform root, string prefix, Vector3 center, float width, float height, Color color)
        {
            if (!root || !createMissingObjects)
                return;

            var halfWidth = Mathf.Max(0.4f, width * 0.5f);
            var postHeight = Mathf.Max(0.8f, height);
            CreateLayoutBlock(root, prefix + "_Left", center + new Vector3(-halfWidth, 0f, 0f), new Vector3(0.24f, postHeight, 0.24f), color, false);
            CreateLayoutBlock(root, prefix + "_Right", center + new Vector3(halfWidth, 0f, 0f), new Vector3(0.24f, postHeight, 0.24f), color, false);
            CreateLayoutBlock(root, prefix + "_Top", center + new Vector3(0f, postHeight * 0.5f, 0f), new Vector3(width + 0.24f, 0.24f, 0.24f), color, false);
        }

        void EnsurePuzzleRoomShell(Transform root, string prefix, Vector3 center, Vector3 floorScale)
        {
            if (!root || !createMissingObjects)
                return;

            CreateLayoutBlock(root, prefix + "_Floor", center + new Vector3(0f, -0.06f, 0f), floorScale, new Color(0.18f, 0.18f, 0.19f));

            var halfX = floorScale.x * 0.5f;
            var halfZ = floorScale.z * 0.5f;
            CreateLayoutBlock(root, prefix + "_Wall_Back", center + new Vector3(0f, 1.25f, halfZ), new Vector3(floorScale.x, 2.5f, 0.35f), new Color(0.32f, 0.32f, 0.34f));
            CreateLayoutBlock(root, prefix + "_Wall_Left", center + new Vector3(-halfX, 1.25f, 0f), new Vector3(0.35f, 2.5f, floorScale.z), new Color(0.28f, 0.29f, 0.31f));
            CreateLayoutBlock(root, prefix + "_Wall_Right", center + new Vector3(halfX, 1.25f, 0f), new Vector3(0.35f, 2.5f, floorScale.z), new Color(0.28f, 0.29f, 0.31f));
            CreateLayoutBlock(root, prefix + "_ExitFrame_L", center + new Vector3(-1.6f, 1.55f, halfZ - 0.35f), new Vector3(0.24f, 3.1f, 0.28f), new Color(0.86f, 0.68f, 0.16f));
            CreateLayoutBlock(root, prefix + "_ExitFrame_R", center + new Vector3(1.6f, 1.55f, halfZ - 0.35f), new Vector3(0.24f, 3.1f, 0.28f), new Color(0.86f, 0.68f, 0.16f));
            CreateLayoutBlock(root, prefix + "_ExitFrame_Top", center + new Vector3(0f, 3.1f, halfZ - 0.35f), new Vector3(3.4f, 0.24f, 0.28f), new Color(0.86f, 0.68f, 0.16f));
        }

        GameObject EnsureEnemy(Transform root, VWS.SoundEventRegistry registry, Vector3 position, Quaternion rotation, string fallbackName, Vector3 fallbackScale, Color fallbackColor)
        {
            var enemy = EnsureGameplayObject(AssetRole.Enemy, root, position, rotation, fallbackName, PrimitiveType.Capsule, fallbackScale, fallbackColor);
            if (enemy)
                ConnectObject(enemy, VARCOAutoConnectorWindow.Role.Enemy, FindBest(AssetRole.Enemy, genre), 0, registry);
            return enemy;
        }

        void ConfigureBossEnemy(GameObject boss)
        {
            if (!boss)
                return;

            Undo.RecordObject(boss.transform, "VARCO boss emphasis");
            boss.transform.localScale = Vector3.Max(boss.transform.localScale, Vector3.one * 1.35f);

            var health = boss.GetComponent<VWS.EnemyHealth>();
            if (health)
            {
                Undo.RecordObject(health, "VARCO boss health");
                health.maxHP = Mathf.Max(health.maxHP, 180);
                health.healthDropChance = 1f;
                health.healthDropHealAmount = Mathf.Max(health.healthDropHealAmount, 35);
                EditorUtility.SetDirty(health);
            }

            var ai = boss.GetComponent<VWS.EnemyAI_NavMesh>();
            if (ai)
            {
                Undo.RecordObject(ai, "VARCO boss attack");
                ai.detectionRange = Mathf.Max(ai.detectionRange, 18f);
                ai.contactDamage = Mathf.Max(ai.contactDamage, 14);
                ai.attackReach = Mathf.Max(ai.attackReach, 2.3f);
                EditorUtility.SetDirty(ai);
            }

            var agent = boss.GetComponent<NavMeshAgent>();
            if (agent)
            {
                Undo.RecordObject(agent, "VARCO boss agent");
                agent.speed = Mathf.Clamp(agent.speed, 2.2f, 3.4f);
                agent.stoppingDistance = Mathf.Max(agent.stoppingDistance, 2.1f);
                EditorUtility.SetDirty(agent);
            }

            EditorUtility.SetDirty(boss);
        }

        void ConfigureSandboxDemoEnemy(GameObject enemy)
        {
            if (!enemy)
                return;

            var ai = enemy.GetComponentInChildren<VWS.EnemyAI_NavMesh>(true);
            if (ai)
            {
                Undo.RecordObject(ai, "VARCO sandbox demo enemy");
                ai.enabled = false;
                EditorUtility.SetDirty(ai);
            }

            var agent = enemy.GetComponentInChildren<NavMeshAgent>(true);
            if (agent)
            {
                Undo.RecordObject(agent, "VARCO sandbox demo enemy");
                agent.enabled = false;
                EditorUtility.SetDirty(agent);
            }

            var health = enemy.GetComponentInChildren<VWS.EnemyHealth>(true);
            if (health)
            {
                Undo.RecordObject(health, "VARCO sandbox demo enemy");
                health.maxHP = Mathf.Clamp(health.maxHP <= 0 ? 60 : health.maxHP, 60, 90);
                health.healthDropChance = 0f;
                EditorUtility.SetDirty(health);
            }

            EditorUtility.SetDirty(enemy);
        }

        void EnsureItemsAtPositions(Transform root, Vector3[] positions, VWS.SoundEventRegistry registry, string prefix)
        {
            if (!root || positions == null || positions.Length == 0)
                return;

            var targetCount = Mathf.Clamp(blockItems ? Mathf.Max(1, itemGoal) : 0, 0, positions.Length);
            if (targetCount == 0)
                return;

            var existing = FindObjectsByType<VWS.ItemPickup>(FindObjectsSortMode.None)
                .Where(item => item && !EditorUtility.IsPersistent(item.gameObject))
                .Select(item => item.gameObject)
                .ToList();
            var candidate = FindBest(AssetRole.ItemPickup, genre);

            for (int i = 0; i < targetCount; i++)
            {
                GameObject item = i < existing.Count ? existing[i] : null;
                if (!item)
                {
                    if (!createMissingObjects)
                        continue;
                    item = InstantiateCandidateOrPrimitive(candidate, prefix + "_" + (i + 1).ToString("00"), PrimitiveType.Sphere, positions[i], Quaternion.identity, Vector3.one * 0.42f, new Color(0.2f, 0.76f, 0.32f));
                }

                if (!item)
                    continue;

                Undo.RecordObject(item.transform, "VARCO item layout");
                item.transform.SetParent(root, true);
                item.transform.SetPositionAndRotation(positions[i], Quaternion.identity);
                ConnectObject(item, VARCOAutoConnectorWindow.Role.ItemPickup, candidate, 0, registry);
                ConfigureCollectibleMarker(item, i);
            }
        }

        void ConfigureCollectibleMarker(GameObject item, int index)
        {
            if (!item)
                return;

            foreach (var collider in item.GetComponentsInChildren<Collider>(true))
            {
                if (!collider || collider is MeshCollider)
                    continue;

                Undo.RecordObject(collider, "VARCO collectible trigger");
                collider.isTrigger = true;
            }

            var bob = item.GetComponent<VWS.PickupBob>();
            if (!bob)
                bob = Undo.AddComponent<VWS.PickupBob>(item);
            bob.rotateSpeed = 80f;
            bob.bobHeight = 0.12f;
            bob.bobSpeed = 2.1f;
            bob.phase = index * 0.45f;
            EditorUtility.SetDirty(item);
        }

        void EnsureHealthPickupsAtPositions(Transform root, Vector3[] positions, VWS.SoundEventRegistry registry, string prefix)
        {
            if (!root || positions == null || positions.Length == 0)
                return;

            var existing = FindObjectsByType<VWS.HealthPickup>(FindObjectsSortMode.None)
                .Where(pickup => pickup && !EditorUtility.IsPersistent(pickup.gameObject))
                .Select(pickup => pickup.gameObject)
                .ToList();
            var candidate = FindBest(AssetRole.HealthPickup, genre);

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject pickup = i < existing.Count ? existing[i] : null;
                if (!pickup)
                {
                    if (!createMissingObjects)
                        continue;
                    pickup = InstantiateCandidateOrPrimitive(candidate, prefix + "_" + (i + 1).ToString("00"), PrimitiveType.Cylinder, positions[i], Quaternion.identity, new Vector3(0.8f, 0.25f, 0.8f), new Color(0.9f, 0.1f, 0.18f));
                }

                if (!pickup)
                    continue;

                Undo.RecordObject(pickup.transform, "VARCO health pickup layout");
                pickup.transform.SetParent(root, true);
                pickup.transform.SetPositionAndRotation(positions[i], Quaternion.identity);
                ConnectObject(pickup, VARCOAutoConnectorWindow.Role.HealthPickup, candidate, 0, registry);
            }
        }

        void EnsureDoorPuzzleElements(Transform root, VWS.SoundEventRegistry registry, Vector3 doorPosition, Vector3 platePosition, Vector3 boxPosition, Vector3 goalPosition)
        {
            GameObject door = null;
            GameObject plate = null;

            if (blockPuzzleDoor)
            {
                door = EnsureGameplayObject(AssetRole.Door, root, doorPosition, Quaternion.identity, "VARCO_Door", PrimitiveType.Cube, new Vector3(2.6f, 3f, 0.45f), new Color(0.44f, 0.27f, 0.15f));
                if (door)
                    ConnectObject(door, VARCOAutoConnectorWindow.Role.Door, FindBest(AssetRole.Door, genre), 0, registry);

                plate = EnsureGameplayObject(AssetRole.PressurePlate, root, platePosition, Quaternion.identity, "VARCO_PressurePlate", PrimitiveType.Cube, new Vector3(2f, 0.16f, 2f), new Color(0.95f, 0.78f, 0.18f));
                if (plate)
                    ConnectObject(plate, VARCOAutoConnectorWindow.Role.PressurePlate, FindBest(AssetRole.PressurePlate, genre), 0, registry);

                LinkPressurePlateToDoor(plate, door);
            }

            if (blockMovableBox)
            {
                var box = EnsureGameplayObject(AssetRole.MovableBox, root, boxPosition, Quaternion.identity, "VARCO_MovableBox", PrimitiveType.Cube, new Vector3(1.2f, 1.2f, 1.2f), new Color(0.5f, 0.36f, 0.18f));
                if (box)
                    ConnectObject(box, VARCOAutoConnectorWindow.Role.MovableBox, FindBest(AssetRole.MovableBox, genre), 0, registry);
            }

            if (blockGoal)
                EnsureGoal(root, blockItems ? Mathf.Max(1, itemGoal) : 0, registry, goalPosition);
        }

        void LinkPressurePlateToDoor(GameObject plateGo, GameObject doorGo)
        {
            if (!plateGo || !doorGo)
                return;

            var plate = plateGo.GetComponent<VWS.PressurePlate>();
            var door = doorGo.GetComponent<VWS.DoorController>();
            if (!plate || !door)
                return;

            Undo.RecordObject(plate, "VARCO pressure plate door link");
            plate.targets = new[] { door };
            EditorUtility.SetDirty(plate);
        }

        void EnsureSpawnMarkers(Transform root, Vector3[] positions, Color color)
        {
            if (!root || positions == null || !createMissingObjects)
                return;

            for (int i = 0; i < positions.Length; i++)
            {
                var marker = CreateLayoutPrimitive(root, "VARCO_SpawnMarker_" + (i + 1).ToString("00"), PrimitiveType.Cylinder, positions[i], new Vector3(1.3f, 0.08f, 1.3f), color, false);
                if (!marker)
                    continue;
                marker.transform.rotation = Quaternion.identity;
            }
        }

        Vector3[] BuildCirclePositions(Vector3 center, float radius, int count, float startAngle, float arcDegrees)
        {
            count = Mathf.Clamp(count, 1, 12);
            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var t = count == 1 ? 0.5f : (float)i / (count - 1);
                var angle = (startAngle + arcDegrees * t) * Mathf.Deg2Rad;
                positions[i] = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }

            return positions;
        }

        void EnsureLayoutLight(Transform root, string name, Vector3 position, Color color, float intensity, float range)
        {
            if (!root || !createMissingObjects)
                return;

            var go = GameObject.Find(name);
            if (!go)
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "VARCO layout light");
            }

            Undo.RecordObject(go.transform, "VARCO layout light");
            go.transform.SetParent(root, true);
            go.transform.position = position;

            var light = go.GetComponent<Light>();
            if (!light)
                light = Undo.AddComponent<Light>(go);
            Undo.RecordObject(light, "VARCO layout light");
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            EditorUtility.SetDirty(go);
        }

        void EnsureIntroPlatformCourse(Transform root)
        {
            if (!root || !createMissingObjects)
                return;

            CreateLayoutBlock(root, "VARCO_Intro_StartPad", new Vector3(-6.2f, 0.48f, 0f), new Vector3(4.8f, 0.5f, 4.8f), new Color(0.12f, 0.48f, 0.88f));
            CreateLayoutBlock(root, "VARCO_Intro_Step_01", new Vector3(-2.5f, 0.86f, 0f), new Vector3(2.5f, 0.32f, 3.2f), new Color(0.24f, 0.68f, 0.74f));
            CreateLayoutBlock(root, "VARCO_Intro_CheckpointPad", new Vector3(0.2f, 1.05f, 0f), new Vector3(2.7f, 0.34f, 3.5f), new Color(0.38f, 0.74f, 0.38f));
            CreateLayoutBlock(root, "VARCO_Intro_MovingDock_A", new Vector3(1.6f, 1.18f, 0f), new Vector3(1.2f, 0.32f, 3.3f), new Color(0.18f, 0.56f, 0.88f));
            CreateLayoutBlock(root, "VARCO_Intro_MovingDock_B", new Vector3(4.2f, 1.2f, 0f), new Vector3(1.2f, 0.32f, 3.3f), new Color(0.18f, 0.56f, 0.88f));
            CreateLayoutBlock(root, "VARCO_Intro_FinalPad", new Vector3(7.3f, 1.5f, 0f), new Vector3(4.2f, 0.42f, 4.5f), new Color(0.92f, 0.70f, 0.18f));
        }

        void EnsureIntroPlatformMarkers(Transform root)
        {
            if (!root || !createMissingObjects)
                return;

            CreateLayoutBlock(root, "VARCO_Intro_Arrow_01", new Vector3(-4.4f, 0.86f, 0f), new Vector3(0.9f, 0.08f, 0.2f), new Color(1f, 0.95f, 0.3f), false);
            CreateLayoutBlock(root, "VARCO_Intro_Arrow_02", new Vector3(0.2f, 1.42f, 0f), new Vector3(0.9f, 0.08f, 0.2f), new Color(1f, 0.95f, 0.3f), false);
            CreateLayoutBlock(root, "VARCO_Intro_GoalArch_L", new Vector3(8.6f, 2.52f, -2.3f), new Vector3(0.22f, 1.8f, 0.22f), new Color(1f, 0.85f, 0.18f));
            CreateLayoutBlock(root, "VARCO_Intro_GoalArch_R", new Vector3(8.6f, 2.52f, 2.3f), new Vector3(0.22f, 1.8f, 0.22f), new Color(1f, 0.85f, 0.18f));
            CreateLayoutBlock(root, "VARCO_Intro_GoalArch_Top", new Vector3(8.6f, 3.45f, 0f), new Vector3(0.22f, 0.22f, 4.8f), new Color(1f, 0.85f, 0.18f), false);
        }

        GameObject CreateLayoutBlock(Transform root, string name, Vector3 position, Vector3 scale, Color color, bool collidable = true)
        {
            return CreateCourseBlock(root, name, position, scale, color, collidable);
        }

        GameObject CreateLayoutPrimitive(Transform root, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Color color, bool collidable = true)
        {
            return CreateCoursePrimitive(root, name, primitive, position, scale, color, collidable);
        }

        void ConfigureRecipeCamera()
        {
            if (genre == VWS.GenreType.Platform)
            {
                ConfigurePlatformCamera();
                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            var camera = Camera.main ? Camera.main : FindFirstObjectByType<Camera>();
            if (!player || !camera)
                return;

            var follow = camera.GetComponent<VWS.ThirdPersonCamera>();
            if (!follow)
                follow = Undo.AddComponent<VWS.ThirdPersonCamera>(camera.gameObject);

            Undo.RecordObject(camera.transform, "VARCO recipe camera");
            Undo.RecordObject(camera, "VARCO recipe camera");
            Undo.RecordObject(follow, "VARCO recipe camera");

            follow.target = player.transform;
            follow.minDistance = 2.2f;
            follow.sensX = 2f;
            follow.sensY = 1.4f;
            follow.useWallClipping = recipe != GameRecipe.SurvivalTimer;
            follow.collisionRadius = 0.2f;

            var yaw = 0f;
            var pitch = 42f;
            var distance = 10f;
            var pivot = new Vector3(0f, 1.55f, 0f);
            var minPitch = 28f;
            var maxPitch = 58f;
            var rightMouseOnly = false;
            camera.fieldOfView = 62f;

            switch (recipe)
            {
                case GameRecipe.CombatWave:
                    yaw = 0f;
                    pitch = 48f;
                    distance = 12.4f;
                    pivot = new Vector3(0f, 1.6f, 0.45f);
                    minPitch = 34f;
                    maxPitch = 64f;
                    rightMouseOnly = false;
                    camera.fieldOfView = 66f;
                    break;
                case GameRecipe.SurvivalTimer:
                    yaw = 45f;
                    pitch = 66f;
                    distance = 14f;
                    pivot = new Vector3(0f, 1.2f, 0f);
                    minPitch = 55f;
                    maxPitch = 78f;
                    camera.fieldOfView = 68f;
                    break;
                case GameRecipe.BossBattle:
                    yaw = 0f;
                    pitch = 38f;
                    distance = 12f;
                    pivot = new Vector3(0f, 1.55f, 0.4f);
                    camera.fieldOfView = 66f;
                    break;
                case GameRecipe.ZombieSurvival:
                    yaw = 35f;
                    pitch = 38f;
                    distance = 10.5f;
                    camera.fieldOfView = 60f;
                    break;
                case GameRecipe.TreasureHunt:
                    yaw = 28f;
                    pitch = 43f;
                    distance = 12.2f;
                    camera.fieldOfView = 66f;
                    break;
                case GameRecipe.EscapeRoom:
                case GameRecipe.DoorPuzzle:
                    yaw = 0f;
                    pitch = 45f;
                    distance = 9.2f;
                    pivot = new Vector3(0f, 1.35f, 0.4f);
                    minPitch = 34f;
                    maxPitch = 62f;
                    camera.fieldOfView = 60f;
                    break;
                case GameRecipe.CollectAndEscape:
                case GameRecipe.ExplorationQuest:
                    yaw = 0f;
                    pitch = 31f;
                    distance = 8.7f;
                    pivot = new Vector3(0f, 1.42f, 0.55f);
                    minPitch = 18f;
                    maxPitch = 52f;
                    camera.fieldOfView = 60f;
                    break;
            }

            follow.ApplyViewPreset(yaw, pitch, distance, pivot, minPitch, maxPitch, rightMouseOnly, true);
            EditorUtility.SetDirty(camera.gameObject);
            EditorUtility.SetDirty(follow);
        }

        void EnsurePlatformCourse(Transform root)
        {
            if (!root || !createMissingObjects)
                return;

            CreateCourseBlock(root, "VARCO_Course_StartPad", new Vector3(-11.0f, 0.48f, 0f), new Vector3(5.6f, 0.5f, 5.4f), new Color(0.12f, 0.48f, 0.88f));
            CreateCourseBlock(root, "VARCO_Course_StartLine", new Vector3(-12.75f, 0.78f, 0f), new Vector3(0.14f, 0.08f, 5.0f), new Color(1f, 0.92f, 0.18f), false);
            CreateCourseBlock(root, "VARCO_Course_TutorialStep_01", new Vector3(-7.3f, 0.72f, -0.8f), new Vector3(2.6f, 0.34f, 3.0f), new Color(0.18f, 0.68f, 0.78f));
            CreateCourseBlock(root, "VARCO_Course_TutorialStep_02", new Vector3(-4.8f, 0.94f, 0.8f), new Vector3(2.2f, 0.34f, 3.0f), new Color(0.28f, 0.72f, 0.58f));
            CreateCourseBlock(root, "VARCO_Course_TutorialStep_03", new Vector3(-2.4f, 1.14f, 0f), new Vector3(2.3f, 0.34f, 2.6f), new Color(0.45f, 0.72f, 0.42f));
            CreateCourseBlock(root, "VARCO_Course_HazardDeck", new Vector3(0.6f, 1.28f, 0f), new Vector3(3.2f, 0.36f, 4.8f), new Color(0.38f, 0.70f, 0.56f));
            CreateCourseBlock(root, "VARCO_Course_MovingDock_A", new Vector3(3.2f, 1.42f, 0f), new Vector3(1.4f, 0.34f, 4.4f), new Color(0.18f, 0.56f, 0.88f));
            CreateCourseBlock(root, "VARCO_Course_MovingDock_B", new Vector3(6.4f, 1.46f, 0f), new Vector3(1.5f, 0.36f, 4.4f), new Color(0.18f, 0.56f, 0.88f));
            CreateCourseBlock(root, "VARCO_Course_CheckpointIsland", new Vector3(8.0f, 1.62f, 0f), new Vector3(3.6f, 0.48f, 5.0f), new Color(0.36f, 0.74f, 0.36f));
            CreateCourseBlock(root, "VARCO_Course_FinalStep_01", new Vector3(10.8f, 1.82f, -1.15f), new Vector3(1.7f, 0.36f, 2.4f), new Color(0.36f, 0.68f, 0.94f));
            CreateCourseBlock(root, "VARCO_Course_FinalStep_02", new Vector3(12.6f, 2.02f, 1.05f), new Vector3(1.7f, 0.36f, 2.4f), new Color(0.55f, 0.58f, 0.95f));
            CreateCourseBlock(root, "VARCO_Course_FinalStep_03", new Vector3(14.3f, 2.18f, 0f), new Vector3(1.8f, 0.36f, 2.8f), new Color(0.72f, 0.54f, 0.90f));
            CreateCourseBlock(root, "VARCO_Course_GoalPad", new Vector3(16.5f, 2.18f, 0f), new Vector3(4.8f, 0.5f, 5.2f), new Color(0.92f, 0.70f, 0.18f));
            CreateCourseBlock(root, "VARCO_Course_HazardMarker", new Vector3(0.6f, 1.55f, 0f), new Vector3(3.0f, 0.08f, 4.6f), new Color(1f, 0.12f, 0.05f), false);
        }

        void EnsurePlatformCourseMarkers(Transform root)
        {
            if (!root || !createMissingObjects)
                return;

            CreateCourseBlock(root, "VARCO_Course_Rail_Start_L", new Vector3(-11f, 1.02f, -2.8f), new Vector3(5.4f, 0.16f, 0.22f), new Color(0.65f, 0.82f, 1f));
            CreateCourseBlock(root, "VARCO_Course_Rail_Start_R", new Vector3(-11f, 1.02f, 2.8f), new Vector3(5.4f, 0.16f, 0.22f), new Color(0.65f, 0.82f, 1f));
            CreateCourseBlock(root, "VARCO_Course_Rail_Checkpoint_L", new Vector3(8.0f, 2.12f, -2.65f), new Vector3(3.6f, 0.16f, 0.22f), new Color(0.7f, 1f, 0.65f));
            CreateCourseBlock(root, "VARCO_Course_Rail_Checkpoint_R", new Vector3(8.0f, 2.12f, 2.65f), new Vector3(3.6f, 0.16f, 0.22f), new Color(0.7f, 1f, 0.65f));
            CreateCourseBlock(root, "VARCO_Course_Rail_Goal_L", new Vector3(16.5f, 2.78f, -2.75f), new Vector3(4.8f, 0.18f, 0.24f), new Color(1f, 0.88f, 0.42f));
            CreateCourseBlock(root, "VARCO_Course_Rail_Goal_R", new Vector3(16.5f, 2.78f, 2.75f), new Vector3(4.8f, 0.18f, 0.24f), new Color(1f, 0.88f, 0.42f));

            CreateCourseBlock(root, "VARCO_Course_Arrow_01", new Vector3(-9.7f, 0.84f, 0f), new Vector3(0.9f, 0.08f, 0.2f), new Color(1f, 0.95f, 0.3f), false);
            CreateCourseBlock(root, "VARCO_Course_Arrow_02", new Vector3(-6.2f, 1.08f, -0.65f), new Vector3(0.9f, 0.08f, 0.2f), new Color(1f, 0.95f, 0.3f), false);
            CreateCourseBlock(root, "VARCO_Course_Arrow_03", new Vector3(-3.55f, 1.28f, 0.55f), new Vector3(0.9f, 0.08f, 0.2f), new Color(1f, 0.95f, 0.3f), false);
            CreateCourseBlock(root, "VARCO_Course_Arrow_04", new Vector3(7.4f, 2.03f, 0f), new Vector3(0.9f, 0.08f, 0.2f), new Color(1f, 0.95f, 0.3f), false);
            CreateCourseBlock(root, "VARCO_Course_Arrow_05", new Vector3(13.5f, 2.54f, 0f), new Vector3(0.9f, 0.08f, 0.2f), new Color(1f, 0.95f, 0.3f), false);

            CreateCourseBlock(root, "VARCO_Course_CheckpointGate_L", new Vector3(8.0f, 2.65f, -2.35f), new Vector3(0.22f, 1.8f, 0.22f), new Color(0.48f, 1f, 0.78f));
            CreateCourseBlock(root, "VARCO_Course_CheckpointGate_R", new Vector3(8.0f, 2.65f, 2.35f), new Vector3(0.22f, 1.8f, 0.22f), new Color(0.48f, 1f, 0.78f));
            CreateCourseBlock(root, "VARCO_Course_CheckpointGate_Top", new Vector3(8.0f, 3.58f, 0f), new Vector3(0.22f, 0.22f, 4.9f), new Color(0.48f, 1f, 0.78f), false);

            CreateCourseBlock(root, "VARCO_Course_GoalArch_L", new Vector3(17.7f, 3.15f, -2.65f), new Vector3(0.26f, 2.1f, 0.26f), new Color(1f, 0.85f, 0.18f));
            CreateCourseBlock(root, "VARCO_Course_GoalArch_R", new Vector3(17.7f, 3.15f, 2.65f), new Vector3(0.26f, 2.1f, 0.26f), new Color(1f, 0.85f, 0.18f));
            CreateCourseBlock(root, "VARCO_Course_GoalArch_Top", new Vector3(17.7f, 4.28f, 0f), new Vector3(0.26f, 0.26f, 5.5f), new Color(1f, 0.85f, 0.18f), false);

            CreateCoursePrimitive(root, "VARCO_Course_BumperBall_01", PrimitiveType.Sphere, new Vector3(-0.2f, 1.94f, -1.75f), Vector3.one * 0.65f, new Color(1f, 0.15f, 0.12f), false);
            CreateCoursePrimitive(root, "VARCO_Course_BumperBall_02", PrimitiveType.Sphere, new Vector3(1.25f, 1.94f, 1.75f), Vector3.one * 0.65f, new Color(1f, 0.15f, 0.12f), false);
            CreateCoursePrimitive(root, "VARCO_Course_BumperBall_03", PrimitiveType.Sphere, new Vector3(12.45f, 2.66f, -1.1f), Vector3.one * 0.58f, new Color(1f, 0.2f, 0.42f), false);
            CreateCoursePrimitive(root, "VARCO_Course_BumperBall_04", PrimitiveType.Sphere, new Vector3(14.25f, 2.82f, 1.15f), Vector3.one * 0.58f, new Color(1f, 0.2f, 0.42f), false);
        }

        GameObject CreateCourseBlock(Transform root, string name, Vector3 position, Vector3 scale, Color color, bool collidable = true)
        {
            var block = GameObject.Find(name);
            if (!block)
            {
                block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(block, "VARCO platform course block");
                block.name = name;
            }

            Undo.RecordObject(block.transform, "VARCO platform course block");
            block.transform.SetParent(root, true);
            block.transform.SetPositionAndRotation(position, Quaternion.identity);
            block.transform.localScale = scale;
            block.isStatic = true;
            SetColor(block, color);
            foreach (var collider in block.GetComponentsInChildren<Collider>(true))
                collider.enabled = collidable;
            return block;
        }

        GameObject CreateCoursePrimitive(Transform root, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Color color, bool collidable = true)
        {
            var go = GameObject.Find(name);
            if (!go)
            {
                go = GameObject.CreatePrimitive(primitive);
                Undo.RegisterCreatedObjectUndo(go, "VARCO platform course primitive");
                go.name = name;
            }

            Undo.RecordObject(go.transform, "VARCO platform course primitive");
            go.transform.SetParent(root, true);
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            go.transform.localScale = scale;
            go.isStatic = true;
            SetColor(go, color);
            foreach (var collider in go.GetComponentsInChildren<Collider>(true))
                collider.enabled = collidable;
            return go;
        }

        void EnsurePlatformItems(Transform root, int count, VWS.SoundEventRegistry registry)
        {
            if (!root || !createMissingObjects)
                return;

            var positions = new[]
            {
                new Vector3(-7.3f, 1.25f, -0.8f),
                new Vector3(-4.8f, 1.47f, 0.8f),
                new Vector3(0.1f, 1.98f, -1.4f),
                new Vector3(4.8f, 2.24f, 0f),
                new Vector3(8.4f, 2.44f, 1.0f),
                new Vector3(10.8f, 2.45f, -1.15f),
                new Vector3(12.6f, 2.65f, 1.05f),
                new Vector3(14.3f, 2.84f, 0f)
            };

            var needed = Mathf.Clamp(count, 1, positions.Length);
            for (int i = 0; i < needed; i++)
            {
                var item = CreatePrimitiveGameplayObject("VARCO_Platform_Item_" + (i + 1).ToString("00"), PrimitiveType.Sphere, positions[i], Quaternion.identity, Vector3.one * 0.38f, new Color(0.16f, 0.95f, 0.35f));
                if (!item)
                    continue;

                item.transform.SetParent(root, true);
                ConnectObject(item, VARCOAutoConnectorWindow.Role.ItemPickup, null, 0, registry);
                ConfigurePlatformCollectible(item, i);
            }
        }

        GameObject EnsurePlatformHazard(Transform root, VWS.SoundEventRegistry registry)
        {
            if (!root || !createMissingObjects)
                return null;

            var hazard = CreatePrimitiveGameplayObject("VARCO_Platform_Hazard_Lane", PrimitiveType.Cube, new Vector3(0.6f, 1.58f, 0f), Quaternion.identity, new Vector3(3.0f, 0.18f, 4.6f), new Color(1f, 0.08f, 0.03f));
            if (!hazard)
                return null;

            hazard.transform.SetParent(root, true);
            ConnectObject(hazard, VARCOAutoConnectorWindow.Role.HazardZone, null, 0, registry);
            return hazard;
        }

        GameObject CreatePrimitiveGameplayObject(string name, PrimitiveType primitive, Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            var go = GameObject.Find(name);
            if (!go)
            {
                go = GameObject.CreatePrimitive(primitive);
                Undo.RegisterCreatedObjectUndo(go, "VARCO primitive gameplay object");
                go.name = name;
            }

            Undo.RecordObject(go.transform, "VARCO primitive gameplay object");
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            SetColor(go, color);
            return go;
        }

        void ConfigurePlatformCollectible(GameObject item, int index)
        {
            if (!item)
                return;

            foreach (var collider in item.GetComponentsInChildren<Collider>(true))
            {
                Undo.RecordObject(collider, "VARCO platform collectible trigger");
                collider.isTrigger = true;
            }

            var bob = item.GetComponent<VWS.PickupBob>();
            if (!bob)
                bob = Undo.AddComponent<VWS.PickupBob>(item);
            bob.rotateSpeed = 90f;
            bob.bobHeight = 0.12f;
            bob.bobSpeed = 2.2f;
            bob.phase = index * 0.55f;
            EditorUtility.SetDirty(item);
        }

        void ConfigurePlatformMovingPlatform(GameObject moving, Transform root)
        {
            ConfigurePlatformMovingPlatform(
                moving,
                root,
                new Vector3(4.8f, 1.78f, 0f),
                new Vector3(3.4f, 1.78f, 0f),
                new Vector3(6.2f, 1.78f, 0f),
                new Vector3(2.4f, 0.32f, 3.4f));
        }

        void ConfigurePlatformMovingPlatform(GameObject moving, Transform root, Vector3 platformPosition, Vector3 pointA, Vector3 pointB, Vector3 platformScale)
        {
            if (!moving || !root)
                return;

            RemoveMovingPlatformPathRoots();

            Undo.RecordObject(moving.transform, "VARCO platform moving platform");
            moving.transform.SetPositionAndRotation(platformPosition, Quaternion.identity);
            moving.transform.localScale = platformScale;

            var platform = moving.GetComponent<VWS.MovingPlatform>();
            if (!platform)
                return;

            var path = new GameObject("VARCO_PlatformMovingPath");
            Undo.RegisterCreatedObjectUndo(path, "VARCO platform moving path");
            path.transform.SetParent(root, false);
            path.transform.position = Vector3.zero;

            var a = new GameObject("PointA");
            var b = new GameObject("PointB");
            Undo.RegisterCreatedObjectUndo(a, "VARCO moving platform point");
            Undo.RegisterCreatedObjectUndo(b, "VARCO moving platform point");
            a.transform.SetParent(path.transform, false);
            b.transform.SetParent(path.transform, false);
            a.transform.position = pointA;
            b.transform.position = pointB;

            Undo.RecordObject(platform, "VARCO platform moving platform");
            platform.a = a.transform;
            platform.b = b.transform;
            platform.speed = Mathf.Clamp(MovingPlatformSpeedForDifficulty(), 0.7f, 1.5f);
            platform.carryCharacterControllers = true;
            EditorUtility.SetDirty(platform);
        }

        void ConfigurePlatformHazard(GameObject hazard)
        {
            ConfigurePlatformHazard(hazard, new Vector3(0.6f, 1.58f, 0f), new Vector3(3.0f, 0.18f, 4.6f));
        }

        void ConfigurePlatformHazard(GameObject hazard, Vector3 position, Vector3 scale)
        {
            if (!hazard)
                return;

            Undo.RecordObject(hazard.transform, "VARCO platform hazard");
            hazard.transform.SetPositionAndRotation(position, Quaternion.identity);
            hazard.transform.localScale = scale;

            foreach (var collider in hazard.GetComponentsInChildren<Collider>(true))
            {
                if (!collider)
                    continue;

                Undo.RecordObject(collider, "VARCO platform hazard collider cleanup");
                collider.enabled = false;
            }

            var box = hazard.GetComponent<BoxCollider>();
            if (!box)
                box = Undo.AddComponent<BoxCollider>(hazard);
            Undo.RecordObject(box, "VARCO platform hazard trigger");
            box.enabled = true;
            box.isTrigger = true;
            box.center = new Vector3(0f, 2.0f, 0f);
            box.size = new Vector3(1f, 4.5f, 1f);
            EditorUtility.SetDirty(hazard);
        }

        void ConfigurePlatformCamera()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var camera = Camera.main ? Camera.main : FindFirstObjectByType<Camera>();
            if (!player || !camera)
                return;

            var follow = camera.GetComponent<VWS.ThirdPersonCamera>();
            if (!follow)
                follow = Undo.AddComponent<VWS.ThirdPersonCamera>(camera.gameObject);

            Undo.RecordObject(camera.transform, "VARCO platform camera");
            Undo.RecordObject(camera, "VARCO platform camera");
            Undo.RecordObject(follow, "VARCO platform camera");
            camera.fieldOfView = 66f;
            follow.target = player.transform;
            follow.pivotOffset = new Vector3(0f, 1.85f, 0.25f);
            follow.distance = 9.6f;
            follow.minDistance = 3f;
            follow.minPitch = 18f;
            follow.maxPitch = 42f;
            follow.orbitWhileRightMouseButtonOnly = true;
            follow.sensX = 1.6f;
            follow.sensY = 1.2f;
            follow.useWallClipping = true;
            follow.collisionRadius = 0.18f;
            var pivotOffset = new Vector3(0f, 1.85f, 0.25f);
            follow.ApplyViewPreset(90f, 27f, 10.2f, pivotOffset, 18f, 42f, true, true);

            var pivot = player.transform.position + pivotOffset;
            var cameraPosition = player.transform.position + new Vector3(-9.4f, 4.9f, 0.25f);
            camera.transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(pivot - cameraPosition, Vector3.up));
            EditorUtility.SetDirty(camera.gameObject);
            EditorUtility.SetDirty(follow);
        }

        GameObject EnsureGameplayObject(AssetRole role, Transform parent, Vector3 position, Quaternion rotation, string fallbackName, PrimitiveType fallbackPrimitive, Vector3 fallbackScale, Color fallbackColor)
        {
            var existing = FindExistingObjectForRole(role);
            if (existing)
            {
                Undo.RecordObject(existing.transform, "VARCO preset object placement");
                existing.transform.SetParent(parent, true);
                existing.transform.SetPositionAndRotation(position, rotation);
                PrepareGeneratedAssetInstance(existing, role, fallbackScale);
                AlignGeneratedObjectToPlayableSurface(existing, role);
                return existing;
            }

            if (!createMissingObjects)
                return null;

            var candidate = FindBest(role, genre);
            GameObject go = null;
            if (candidate != null && candidate.asset)
            {
                go = PrefabUtility.InstantiatePrefab(candidate.asset) as GameObject;
                if (!go)
                    go = Instantiate(candidate.asset);
                Undo.RegisterCreatedObjectUndo(go, "VARCO 에셋 배치");
                go.name = "VARCO_" + role + "_" + candidate.DisplayName;
                log.Add(AssetRoleLabel(role) + " 에셋 배치됨: " + candidate.path);
            }
            else
            {
                go = GameObject.CreatePrimitive(fallbackPrimitive);
                Undo.RegisterCreatedObjectUndo(go, "VARCO 기본 대체물 생성");
                go.name = fallbackName;
                go.transform.localScale = fallbackScale;
                SetColor(go, fallbackColor);
                log.Add("기본 " + AssetRoleLabel(role) + " 오브젝트를 생성했습니다.");
            }

            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, rotation);
            if (candidate != null)
                PrepareGeneratedAssetInstance(go, role, fallbackScale);
            AlignGeneratedObjectToPlayableSurface(go, role);
            return go;
        }

        void AttachWeaponToPlayer(GameObject player)
        {
            if (!blockWeapon || !player)
                return;

            var previous = FindEquippedWeapon(player);
            if (previous)
                Undo.DestroyObjectImmediate(previous.gameObject);

            if (!createMissingObjects)
                return;

            var candidate = FindBest(AssetRole.Weapon, genre);
            GameObject weapon = null;
            if (candidate != null && candidate.asset)
            {
                weapon = PrefabUtility.InstantiatePrefab(candidate.asset) as GameObject;
                if (!weapon)
                    weapon = Instantiate(candidate.asset);
                Undo.RegisterCreatedObjectUndo(weapon, "VARCO 무기 장착");
                weapon.name = "VARCO_EquippedWeapon";
                log.Add("무기 에셋 장착됨: " + candidate.path);
            }
            else
            {
                weapon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(weapon, "VARCO 기본 무기 생성");
                weapon.name = "VARCO_EquippedWeapon";
                SetColor(weapon, new Color(0.78f, 0.78f, 0.82f));
                log.Add("기본 무기 비주얼을 생성했습니다.");
            }

            StripPhysicsForVisualAttachment(weapon);
            var mount = FindWeaponMount(player);
            weapon.transform.SetParent(mount, false);
            if (mount == player.transform || mount.GetComponent<Animator>())
            {
                weapon.transform.localPosition = new Vector3(0.45f, 1.05f, 0.42f);
                weapon.transform.localRotation = Quaternion.Euler(65f, 0f, -25f);
                weapon.transform.localScale = Vector3.one * 0.55f;
            }
            else
            {
                weapon.transform.localPosition = new Vector3(0.02f, 0.02f, 0.05f);
                weapon.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
                weapon.transform.localScale = Vector3.one * 0.35f;
            }

            PrepareGeneratedAssetInstance(weapon, AssetRole.Weapon, Vector3.one * 0.55f);
            EditorUtility.SetDirty(weapon);
        }

        static Transform FindWeaponMount(GameObject player)
        {
            var hand = FindChildByName(player.transform, "right", "hand")
                ?? FindChildByName(player.transform, "r", "hand")
                ?? FindChildByName(player.transform, "right", "wrist")
                ?? FindChildByName(player.transform, "weapon");
            if (hand)
                return hand;

            var animator = player.GetComponentInChildren<Animator>(true);
            return animator ? animator.transform : player.transform;
        }

        static Transform FindChildByName(Transform root, params string[] keywords)
        {
            if (!root)
                return null;

            var text = Normalize(root.name);
            if (keywords.All(keyword => text.Contains(keyword)))
                return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindChildByName(root.GetChild(i), keywords);
                if (found)
                    return found;
            }

            return null;
        }

        static void StripPhysicsForVisualAttachment(GameObject go)
        {
            if (!go)
                return;

            foreach (var collider in go.GetComponentsInChildren<Collider>(true))
                Undo.DestroyObjectImmediate(collider);
            foreach (var body in go.GetComponentsInChildren<Rigidbody>(true))
                Undo.DestroyObjectImmediate(body);
        }

        GameObject FindExistingObjectForRole(AssetRole role)
        {
            switch (role)
            {
                case AssetRole.Player:
                    var player = GameObject.FindGameObjectWithTag("Player");
                    if (player) return player;
                    var third = FindFirstObjectByType<VWS.PlayerController_ThirdPerson>();
                    if (third) return third.gameObject;
                    var platform = FindFirstObjectByType<VWS.PlayerController_Platform>();
                    return platform ? platform.gameObject : null;
                case AssetRole.Enemy:
                    var enemy = FindFirstObjectByType<VWS.EnemyHealth>();
                    return enemy ? enemy.gameObject : null;
                case AssetRole.ItemPickup:
                    var item = FindFirstObjectByType<VWS.ItemPickup>();
                    return item ? item.gameObject : null;
                case AssetRole.HealthPickup:
                    var hp = FindFirstObjectByType<VWS.HealthPickup>();
                    return hp ? hp.gameObject : null;
                case AssetRole.Goal:
                    var goal = FindFirstObjectByType<VWS.GoalTrigger>();
                    return goal ? goal.gameObject : null;
                case AssetRole.Door:
                    var door = FindFirstObjectByType<VWS.DoorController>();
                    return door ? door.gameObject : null;
                case AssetRole.PressurePlate:
                    var plate = FindFirstObjectByType<VWS.PressurePlate>();
                    return plate ? plate.gameObject : null;
                case AssetRole.HazardZone:
                    var hazard = FindFirstObjectByType<VWS.HazardZone>();
                    return hazard ? hazard.gameObject : null;
                case AssetRole.MovingPlatform:
                    var moving = FindFirstObjectByType<VWS.MovingPlatform>();
                    return moving ? moving.gameObject : null;
                case AssetRole.MovableBox:
                    var box = FindFirstObjectByType<VWS.MovableBox>();
                    return box ? box.gameObject : null;
                case AssetRole.Checkpoint:
                    var checkpoint = FindFirstObjectByType<VWS.Checkpoint>();
                    return checkpoint ? checkpoint.gameObject : null;
                default:
                    return null;
            }
        }

        void EnsureSinglePlayerControllerForGenre()
        {
            var player = FindPlayerObjectForControllerCleanup();
            if (!player)
                return;

            var changed = false;
            Undo.RegisterFullObjectHierarchyUndo(player, "VARCO 플레이어 컨트롤러 보정");
            EnsureTag("Player");
            player.tag = "Player";

            if (genre == VWS.GenreType.Platform)
            {
                DestroyComponentForControllerCleanup<VWS.PlayerController_ThirdPerson>(player, ref changed);
                DestroyComponentForControllerCleanup<Rigidbody>(player, ref changed);
                DestroyComponentForControllerCleanup<CapsuleCollider>(player, ref changed);

                var controller = EnsureComponentForControllerCleanup<CharacterController>(player, ref changed);
                if (controller.height <= 0f)
                {
                    controller.height = 1.7f;
                    controller.radius = 0.35f;
                    controller.center = new Vector3(0f, 0.85f, 0f);
                    changed = true;
                }

                var platform = EnsureComponentForControllerCleanup<VWS.PlayerController_Platform>(player, ref changed);
                platform.useCameraSpace = true;
                platform.lockZAxis = false;
            }
            else
            {
                DestroyComponentForControllerCleanup<VWS.PlayerController_Platform>(player, ref changed);
                DestroyComponentForControllerCleanup<CharacterController>(player, ref changed);

                var capsule = EnsureComponentForControllerCleanup<CapsuleCollider>(player, ref changed);
                if (capsule.height <= 0f)
                {
                    capsule.height = 1.7f;
                    capsule.radius = 0.35f;
                    capsule.center = new Vector3(0f, 0.85f, 0f);
                    changed = true;
                }

                var rb = EnsureComponentForControllerCleanup<Rigidbody>(player, ref changed);
                rb.useGravity = false;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

                var thirdPerson = EnsureComponentForControllerCleanup<VWS.PlayerController_ThirdPerson>(player, ref changed);
                thirdPerson.useCameraSpace = !MoveInFacingDirectionForPlayer();
                thirdPerson.moveInFacingDirection = MoveInFacingDirectionForPlayer();
                thirdPerson.applyRootMotionFromAnimation = false;
            }

            if (changed)
                log.Add("플레이어 컨트롤러 보정: 현재 장르에 맞는 조작 컴포넌트만 남겼습니다.");

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static GameObject FindPlayerObjectForControllerCleanup()
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged)
                return tagged;

            var third = FindFirstObjectByType<VWS.PlayerController_ThirdPerson>();
            if (third)
                return third.gameObject;

            var platform = FindFirstObjectByType<VWS.PlayerController_Platform>();
            if (platform)
                return platform.gameObject;

            var health = FindFirstObjectByType<VWS.PlayerHealth>();
            return health ? health.gameObject : null;
        }

        static T EnsureComponentForControllerCleanup<T>(GameObject go, ref bool changed) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component)
                return component;

            changed = true;
            return Undo.AddComponent<T>(go);
        }

        static void DestroyComponentForControllerCleanup<T>(GameObject go, ref bool changed) where T : Component
        {
            var component = go.GetComponent<T>();
            if (!component)
                return;

            changed = true;
            Undo.DestroyObjectImmediate(component);
        }

        void ConnectObject(GameObject go, VARCOAutoConnectorWindow.Role role, AssetCandidate source, int waveIndex, VWS.SoundEventRegistry registry)
        {
            if (!go)
                return;

            var controller = autoAnimations ? CreateAnimatorController(go, source, role) : ExistingController(go);
            VARCOAutoConnectorWindow.ConnectFromFeatureBuilder(
                go,
                role,
                controller,
                connectEnemyToWave: role == VARCOAutoConnectorWindow.Role.Enemy,
                saveEnemyAsPrefab: role == VARCOAutoConnectorWindow.Role.Enemy,
                waveIndex: waveIndex,
                requiredItems: itemGoal,
                healAmount: HealAmountForDifficulty(),
                hazardDps: HazardDpsForDifficulty(),
                movingPlatformDistance: 5f,
                movingPlatformSpeed: MovingPlatformSpeedForDifficulty(),
                moveInFacingDirectionForPlayer: MoveInFacingDirectionForPlayer(),
                cameraViewPreset: EffectiveCameraPreset());

            AssignRegistryToObject(go, registry);
        }

        static void AssignRegistryToObject(GameObject go, VWS.SoundEventRegistry registry)
        {
            if (!go || !registry)
                return;

            foreach (var emitter in go.GetComponentsInChildren<VWS.SoundEventEmitter>(true))
            {
                emitter.registry = registry;
                EditorUtility.SetDirty(emitter);
            }

            foreach (var trigger in go.GetComponentsInChildren<VWS.SoundEventTrigger>(true))
            {
                trigger.registry = registry;
                EditorUtility.SetDirty(trigger);
            }

            var attack = go.GetComponent<VWS.PlayerAttack>();
            if (attack)
            {
                attack.soundRegistry = registry;
                EditorUtility.SetDirty(attack);
            }

            var playerHealth = go.GetComponent<VWS.PlayerHealth>();
            if (playerHealth)
            {
                playerHealth.soundRegistry = registry;
                EditorUtility.SetDirty(playerHealth);
            }

            var enemyHealth = go.GetComponent<VWS.EnemyHealth>();
            if (enemyHealth)
            {
                enemyHealth.soundRegistry = registry;
                EditorUtility.SetDirty(enemyHealth);
            }

            var enemyAi = go.GetComponent<VWS.EnemyAI_NavMesh>();
            if (enemyAi)
            {
                enemyAi.soundRegistry = registry;
                EditorUtility.SetDirty(enemyAi);
            }

            var footstep = go.GetComponent<VWS.PlayerFootstepSound>();
            if (footstep)
            {
                footstep.soundRegistry = registry;
                EditorUtility.SetDirty(footstep);
            }
        }

        RuntimeAnimatorController ExistingController(GameObject go)
        {
            var animator = go ? go.GetComponentInChildren<Animator>(true) : null;
            return animator ? animator.runtimeAnimatorController : null;
        }

        AnimatorController CreateAnimatorController(GameObject go, AssetCandidate source, VARCOAutoConnectorWindow.Role role)
        {
            if (role != VARCOAutoConnectorWindow.Role.Player &&
                role != VARCOAutoConnectorWindow.Role.PlatformPlayer &&
                role != VARCOAutoConnectorWindow.Role.Enemy)
                return ExistingController(go) as AnimatorController;

            var idle = FindClip(source, "idle");
            if (!idle)
                return ExistingController(go) as AnimatorController;

            EnsureFolder(GeneratedAnimationFolder);
            var path = StableAnimatorControllerPath(go, source, role);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            var created = false;
            if (!controller)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                created = true;
            }

            EnsureAnimatorParameter(controller, "IsWalk", AnimatorControllerParameterType.Bool);
            EnsureAnimatorParameter(controller, "IsRun", AnimatorControllerParameterType.Bool);
            EnsureAnimatorParameter(controller, "IsAttack", AnimatorControllerParameterType.Trigger);
            EnsureAnimatorParameter(controller, "IsDead", AnimatorControllerParameterType.Trigger);
            EnsureAnimatorParameter(controller, "IsJump", AnimatorControllerParameterType.Bool);
            EnsureAnimatorParameter(controller, "IsPush", AnimatorControllerParameterType.Bool);

            var sm = controller.layers[0].stateMachine;
            ClearAnimatorStateMachine(sm);

            var idleState = AddState(sm, "Idle", idle, new Vector3(120f, 120f, 0f));
            sm.defaultState = idleState;

            var walk = FindClip(source, "walk");
            var run = FindClip(source, "run", "sprint", "dash");
            if (!walk && run)
                walk = run;
            if (walk)
            {
                var walkState = AddState(sm, "Walk", walk, new Vector3(360f, 120f, 0f));
                AddBoolTransition(idleState, walkState, "IsWalk", true);
                AddBoolTransition(walkState, idleState, "IsWalk", false);

                if (run)
                {
                    var runState = AddState(sm, "Run", run, new Vector3(600f, 120f, 0f));
                    AddBoolTransition(idleState, runState, "IsRun", true);
                    AddBoolTransition(runState, idleState, "IsRun", false);
                    AddBoolTransition(walkState, runState, "IsRun", true);
                    AddBoolTransition(runState, walkState, "IsRun", false);
                }
            }

            var attack = FindClip(source, "attack");
            if (attack)
                AddTriggeredState(sm, idleState, "Attack", attack, "IsAttack", new Vector3(360f, 280f, 0f));

            var death = FindClip(source, "death", "die");
            if (death)
            {
                var deathState = AddState(sm, "Death", death, new Vector3(600f, 280f, 0f));
                var t = sm.AddAnyStateTransition(deathState);
                t.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
                t.hasExitTime = false;
                t.duration = 0.05f;
            }

            var jump = FindClip(source, "jump");
            if (jump)
            {
                var jumpState = AddState(sm, "Jump", jump, new Vector3(120f, 300f, 0f));
                jumpState.speed = SpeedForTargetDuration(jump, 1.1f);
                AddBoolTransition(idleState, jumpState, "IsJump", true);
                AddBoolTransition(jumpState, idleState, "IsJump", false);
            }

            var animator = go.GetComponentInChildren<Animator>(true);
            if (!animator)
                animator = Undo.AddComponent<Animator>(go);
            Undo.RecordObject(animator, "VARCO 애니메이션 연결");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(controller);
            log.Add((created ? "애니메이터 컨트롤러 생성됨: " : "애니메이터 컨트롤러 재사용됨: ") + path);
            return controller;
        }

        string StableAnimatorControllerPath(GameObject go, AssetCandidate source, VARCOAutoConnectorWindow.Role role)
        {
            var displayName = source != null ? source.DisplayName : go ? go.name : "VARCO";
            var roleName = role.ToString();
            var guid = source != null && !string.IsNullOrWhiteSpace(source.path)
                ? AssetDatabase.AssetPathToGUID(source.path)
                : string.Empty;
            var suffix = string.IsNullOrWhiteSpace(guid) ? string.Empty : "_" + guid.Substring(0, Mathf.Min(8, guid.Length));
            var baseName = SafeFileName(displayName + "_" + roleName + suffix + "_AutoBuild");
            return GeneratedAnimationFolder + "/" + baseName + ".controller";
        }

        static void EnsureAnimatorParameter(AnimatorController controller, string parameterName, AnimatorControllerParameterType parameterType)
        {
            var existing = controller.parameters.FirstOrDefault(p => p.name == parameterName);
            if (existing != null && existing.type != parameterType)
                controller.RemoveParameter(existing);
            if (controller.parameters.Any(p => p.name == parameterName))
                return;
            controller.AddParameter(parameterName, parameterType);
        }

        static void ClearAnimatorStateMachine(AnimatorStateMachine sm)
        {
            foreach (var transition in sm.anyStateTransitions.ToArray())
                sm.RemoveAnyStateTransition(transition);
            foreach (var transition in sm.entryTransitions.ToArray())
                sm.RemoveEntryTransition(transition);
            foreach (var state in sm.states.ToArray())
                sm.RemoveState(state.state);
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion, Vector3 position)
        {
            var state = sm.AddState(name, position);
            state.motion = motion;
            return state;
        }

        static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value)
        {
            var t = from.AddTransition(to);
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
            t.hasExitTime = false;
            t.duration = 0.08f;
        }

        static void AddTriggeredState(AnimatorStateMachine sm, AnimatorState idle, string stateName, Motion motion, string trigger, Vector3 position)
        {
            var state = AddState(sm, stateName, motion, position);
            state.speed = SpeedForTargetDuration(motion, 0.9f);
            var enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.duration = 0.04f;

            var exit = state.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = 0.92f;
            exit.duration = 0.08f;
        }

        static float SpeedForTargetDuration(Motion motion, float targetDuration)
        {
            if (motion is AnimationClip clip && clip.length > targetDuration)
                return Mathf.Clamp(clip.length / Mathf.Max(0.05f, targetDuration), 1f, 8f);
            return 1f;
        }

        AnimationClip FindClip(AssetCandidate source, params string[] keywords)
        {
            var match = FindAnimationMatch(source, keywords);
            return match != null ? match.clip : null;
        }

        IEnumerable<AnimationSlotDefinition> AnimationSlotDefinitions()
        {
            if (!blockPlayer)
                yield break;

            yield return new AnimationSlotDefinition { ownerLabel = "플레이어", stateLabel = "대기", sourceRole = AssetRole.Player, keywords = new[] { "idle" }, important = true };
            yield return new AnimationSlotDefinition { ownerLabel = "플레이어", stateLabel = "이동", sourceRole = AssetRole.Player, keywords = new[] { "walk", "run" }, important = true };
            if (blockWeapon || blockEnemyWave)
                yield return new AnimationSlotDefinition { ownerLabel = "플레이어", stateLabel = "공격", sourceRole = AssetRole.Player, keywords = new[] { "attack", "slash", "shoot" }, important = true };
            if (genre == VWS.GenreType.Platform || blockMovingPlatform)
                yield return new AnimationSlotDefinition { ownerLabel = "플레이어", stateLabel = "점프", sourceRole = AssetRole.Player, keywords = new[] { "jump" }, important = true };
            yield return new AnimationSlotDefinition { ownerLabel = "플레이어", stateLabel = "사망", sourceRole = AssetRole.Player, keywords = new[] { "death", "die" }, important = false };

            if (!blockEnemyWave)
                yield break;

            yield return new AnimationSlotDefinition { ownerLabel = "적", stateLabel = "대기", sourceRole = AssetRole.Enemy, keywords = new[] { "idle" }, important = true };
            yield return new AnimationSlotDefinition { ownerLabel = "적", stateLabel = "이동/추격", sourceRole = AssetRole.Enemy, keywords = new[] { "walk", "run", "chase" }, important = true };
            yield return new AnimationSlotDefinition { ownerLabel = "적", stateLabel = "공격", sourceRole = AssetRole.Enemy, keywords = new[] { "attack" }, important = true };
            yield return new AnimationSlotDefinition { ownerLabel = "적", stateLabel = "사망", sourceRole = AssetRole.Enemy, keywords = new[] { "death", "die" }, important = true };
        }

        IEnumerable<AnimationSlotStatus> BuildAnimationSlotStatuses()
        {
            foreach (var definition in AnimationSlotDefinitions())
                yield return BuildAnimationSlotStatus(definition);
        }

        AnimationSlotStatus BuildAnimationSlotStatus(AnimationSlotDefinition definition)
        {
            var source = FindBest(definition.sourceRole, genre);
            var match = FindAnimationMatch(source, definition.keywords);
            if (match != null && match.clip)
            {
                return new AnimationSlotStatus
                {
                    definition = definition,
                    clip = match.clip,
                    clipPath = match.path,
                    state = "PASS",
                    reason = match.reason,
                    score = match.score
                };
            }

            return new AnimationSlotStatus
            {
                definition = definition,
                state = definition.important ? "WARN" : "FALLBACK",
                reason = "스캔 범위에서 " + string.Join("/", definition.keywords) + " 클립을 찾지 못했습니다."
            };
        }

        static string AnimationSlotLabel(AnimationSlotDefinition definition)
        {
            return definition.ownerLabel + " " + definition.stateLabel;
        }

        string AnimationSlotMessage(AnimationSlotStatus status)
        {
            if (status.clip)
            {
                var path = string.IsNullOrWhiteSpace(status.clipPath) ? "" : " / " + status.clipPath;
                return "자동 후보: " + status.clip.name + path + " / " + status.reason;
            }

            return (status.definition.important ? "필수 누락: " : "선택 누락: ") + status.reason;
        }

        AnimationCandidateMatch FindAnimationMatch(AssetCandidate source, params string[] keywords)
        {
            var sourceText = Normalize((source != null ? source.path + " " + source.DisplayName : genre.ToString()));
            var wantedGenre = source != null && source.genre.HasValue ? source.genre.Value : genre;
            var character = source != null ? source.characterKind : CharacterKind.None;
            var normalizedKeywords = (keywords ?? Array.Empty<string>())
                .Select(Normalize)
                .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                .ToArray();
            var bestScore = int.MinValue;
            AnimationCandidateMatch best = null;

            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", AnimationScanRoots()))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var clip = asset as AnimationClip;
                    if (!clip || clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                        continue;

                    var text = Normalize(path + " " + clip.name);
                    if (!normalizedKeywords.Any(text.Contains))
                        continue;

                    var score = 0;
                    var reasons = new List<string>();
                    foreach (var keyword in normalizedKeywords.Where(text.Contains))
                    {
                        score += 25;
                        reasons.Add("키워드 " + keyword);
                    }
                    if (GuessGenreFromText(text) == wantedGenre)
                    {
                        score += 40;
                        reasons.Add("장르 일치");
                    }
                    if (character != CharacterKind.None && GuessCharacterKind(text) == character)
                    {
                        score += 35;
                        reasons.Add("캐릭터 타입 일치");
                    }
                    if (sourceText.Contains("player") && text.Contains("player"))
                    {
                        score += 15;
                        reasons.Add("플레이어 이름 일치");
                    }
                    if (sourceText.Contains("zombie") && text.Contains("zombie"))
                    {
                        score += 20;
                        reasons.Add("좀비 이름 일치");
                    }
                    if (sourceText.Contains("boss") && text.Contains("boss"))
                    {
                        score += 20;
                        reasons.Add("보스 이름 일치");
                    }
                    if (sourceText.Contains("orc") && text.Contains("orc"))
                    {
                        score += 20;
                        reasons.Add("오크 이름 일치");
                    }
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = new AnimationCandidateMatch
                        {
                            clip = clip,
                            path = path,
                            score = score,
                            reason = string.Join(", ", reasons) + " / 점수 " + score
                        };
                    }
                }
            }

            return bestScore > 0 ? best : null;
        }

        void EnsureItems(Transform root, int count, VWS.SoundEventRegistry registry)
        {
            var existing = FindObjectsByType<VWS.ItemPickup>(FindObjectsSortMode.None);
            if (existing.Length >= count)
                return;

            var needed = Mathf.Clamp(count - existing.Length, 0, 12);
            var candidate = FindBest(AssetRole.ItemPickup, genre);
            for (int i = 0; i < needed; i++)
            {
                var angle = i * Mathf.PI * 2f / Mathf.Max(1, needed);
                var pos = new Vector3(Mathf.Cos(angle) * 2.4f, 0.6f, 4f + Mathf.Sin(angle) * 2.4f);
                var item = InstantiateCandidateOrPrimitive(candidate, "VARCO_Item_" + (i + 1).ToString("00"), PrimitiveType.Sphere, pos, Quaternion.identity, Vector3.one * 0.42f, new Color(0.2f, 0.76f, 0.32f));
                item.transform.SetParent(root, true);
                ConnectObject(item, VARCOAutoConnectorWindow.Role.ItemPickup, candidate, 0, registry);
            }
        }

        void EnsureGoal(Transform root, int requiredItems, VWS.SoundEventRegistry registry, Vector3 position)
        {
            var goal = EnsureGameplayObject(AssetRole.Goal, root, position, Quaternion.identity, "VARCO_Goal", PrimitiveType.Cube, new Vector3(2.4f, 1.8f, 0.6f), new Color(1f, 0.84f, 0.16f));
            if (!goal)
                return;

            var previousGoal = itemGoal;
            itemGoal = requiredItems;
            ConnectObject(goal, VARCOAutoConnectorWindow.Role.Goal, FindBest(AssetRole.Goal, genre), 0, registry);
            itemGoal = previousGoal;
        }

        void EnsureCheckpoint(Transform root, VWS.SoundEventRegistry registry, Vector3 position)
        {
            var checkpoint = EnsureGameplayObject(AssetRole.Checkpoint, root, position, Quaternion.identity, "VARCO_Checkpoint", PrimitiveType.Cylinder, new Vector3(1.15f, 1f, 1.15f), new Color(0.7f, 0.35f, 0.9f));
            if (checkpoint)
                ConnectObject(checkpoint, VARCOAutoConnectorWindow.Role.Checkpoint, FindBest(AssetRole.Checkpoint, genre), 0, registry);
        }

        GameObject EnsureHazard(Transform root, VWS.SoundEventRegistry registry, Vector3 position)
        {
            var hazard = EnsureGameplayObject(AssetRole.HazardZone, root, position, Quaternion.identity, "VARCO_Hazard", PrimitiveType.Cube, new Vector3(3f, 0.16f, 3f), new Color(1f, 0.35f, 0.1f));
            if (hazard)
                ConnectObject(hazard, VARCOAutoConnectorWindow.Role.HazardZone, FindBest(AssetRole.HazardZone, genre), 0, registry);
            return hazard;
        }

        void EnsureFallRespawnSafety(Transform root)
        {
            var existing = FindFirstObjectByType<VWS.DeathZone>();
            if (existing)
            {
                ConfigureFallRespawnZone(existing.gameObject);
                ConfigurePlatformFallRespawn();
                return;
            }

            if (!createMissingObjects)
                return;

            var zone = new GameObject("VARCO_FallRespawnZone");
            Undo.RegisterCreatedObjectUndo(zone, "낙하 리스폰 안전망 생성");
            zone.transform.SetParent(root, true);
            ConfigureFallRespawnZone(zone);
            Undo.AddComponent<VWS.DeathZone>(zone);
            ConfigurePlatformFallRespawn();
            log.Add("낙사 리스폰 안전망을 생성했습니다. 플레이어가 맵 아래로 떨어지면 체크포인트 또는 시작 위치로 돌아갑니다.");
        }

        void ConfigureFallRespawnZone(GameObject zone)
        {
            if (!zone)
                return;

            Undo.RecordObject(zone.transform, "낙하 리스폰 안전망 설정");
            zone.transform.position = new Vector3(0f, FallRespawnZoneY(), 0f);
            zone.transform.rotation = Quaternion.identity;
            zone.transform.localScale = Vector3.one;

            var collider = zone.GetComponent<BoxCollider>();
            if (!collider)
                collider = Undo.AddComponent<BoxCollider>(zone);
            Undo.RecordObject(collider, "낙하 리스폰 충돌 영역 설정");
            collider.isTrigger = true;
            collider.size = FallRespawnZoneSize();
            collider.center = Vector3.zero;

            foreach (var renderer in zone.GetComponentsInChildren<Renderer>(true))
                Undo.DestroyObjectImmediate(renderer);
            EditorUtility.SetDirty(zone);
        }

        void ConfigurePlatformFallRespawn()
        {
            var player = FindFirstObjectByType<VWS.PlayerController_Platform>();
            if (!player)
                return;

            Undo.RecordObject(player, "플랫폼 낙하 리스폰 설정");
            player.respawnAtStartOnFall = true;
            player.fallRespawnY = FallRespawnZoneY() - 1f;
            EditorUtility.SetDirty(player);
        }

        float FallRespawnZoneY()
        {
            switch (genre)
            {
                case VWS.GenreType.Platform:
                    return -8f;
                case VWS.GenreType.Puzzle:
                    return -5f;
                default:
                    return -6f;
            }
        }

        Vector3 FallRespawnZoneSize()
        {
            switch (genre)
            {
                case VWS.GenreType.Platform:
                    return new Vector3(90f, 2f, 90f);
                case VWS.GenreType.Puzzle:
                    return new Vector3(36f, 2f, 36f);
                case VWS.GenreType.Exploration:
                    return new Vector3(70f, 2f, 70f);
                default:
                    return new Vector3(58f, 2f, 58f);
            }
        }

        void EnsureHealthPickup(Transform root, VWS.SoundEventRegistry registry, Vector3 position)
        {
            var heal = EnsureGameplayObject(AssetRole.HealthPickup, root, position, Quaternion.identity, "VARCO_HealthPickup", PrimitiveType.Cylinder, new Vector3(0.8f, 0.25f, 0.8f), new Color(0.9f, 0.1f, 0.18f));
            if (heal)
                ConnectObject(heal, VARCOAutoConnectorWindow.Role.HealthPickup, FindBest(AssetRole.HealthPickup, genre), 0, registry);
        }

        void EnsureEnvironmentProps(Transform root, VWS.SoundEventRegistry registry, Vector3[] positions, Vector3[] fallbackScales)
        {
            if (positions == null || positions.Length == 0)
                return;

            if (!createMissingObjects)
                return;

            var candidatesForRole = RankedCandidatesForRole(AssetRole.ArenaCover, positions.Length).ToList();
            var created = 0;

            for (int i = 0; i < positions.Length; i++)
            {
                var objectName = "VARCO_EnvironmentProp_" + (i + 1).ToString("00");
                if (GameObject.Find(objectName))
                    continue;

                var candidate = candidatesForRole.Count > 0 ? candidatesForRole[i % candidatesForRole.Count] : null;
                var scale = fallbackScales != null && i < fallbackScales.Length ? fallbackScales[i] : new Vector3(2.4f, 1.8f, 2.4f);
                var rotation = Quaternion.Euler(0f, 25f + i * 53f, 0f);
                var prop = InstantiateCandidateOrPrimitive(candidate, objectName, PrimitiveType.Cube, positions[i], rotation, scale, new Color(0.35f, 0.38f, 0.42f));
                if (!prop)
                    continue;

                prop.transform.SetParent(root, true);
                ConnectObject(prop, VARCOAutoConnectorWindow.Role.ArenaCover, candidate, 0, registry);
                created++;
            }

            if (created > 0)
                log.Add("환경 소품/엄폐물 " + created + "개를 장르 공간에 자동 배치했습니다.");
        }

        void EnsureCover(Transform root, VWS.SoundEventRegistry registry, Vector3 position)
        {
            var cover = EnsureGameplayObject(AssetRole.ArenaCover, root, position, Quaternion.identity, "VARCO_Cover", PrimitiveType.Cube, new Vector3(4.2f, 1.8f, 0.8f), new Color(0.35f, 0.38f, 0.42f));
            if (cover)
                ConnectObject(cover, VARCOAutoConnectorWindow.Role.ArenaCover, FindBest(AssetRole.ArenaCover, genre), 0, registry);
        }

        GameObject InstantiateCandidateOrPrimitive(AssetCandidate candidate, string name, PrimitiveType primitive, Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            GameObject go = null;
            if (candidate != null && candidate.asset)
            {
                go = PrefabUtility.InstantiatePrefab(candidate.asset) as GameObject;
                if (!go)
                    go = Instantiate(candidate.asset);
                Undo.RegisterCreatedObjectUndo(go, "VARCO 에셋 배치");
            }
            else
            {
                go = GameObject.CreatePrimitive(primitive);
                Undo.RegisterCreatedObjectUndo(go, "기본 대체 에셋 생성");
                go.transform.localScale = scale;
                SetColor(go, color);
            }

            go.name = name;
            go.transform.SetPositionAndRotation(position, rotation);
            if (candidate != null)
            {
                PrepareGeneratedAssetInstance(go, candidate.role, scale);
                AlignGeneratedObjectToPlayableSurface(go, candidate.role);
            }
            return go;
        }

        void PrepareGeneratedAssetInstance(GameObject go, AssetRole role, Vector3 fallbackScale)
        {
            if (!go)
                return;

            NormalizePlacedAsset(go, role, fallbackScale);
            ClampGeneratedAssetFootprint(go, role, fallbackScale, logChange: true);
            LimitGeneratedLights(go.transform, MaxGeneratedLightsForRole(role), logChange: true);
            EnsureRuntimeGroundAlign(go, role);
        }

        void EnsureRuntimeGroundAlign(GameObject go, AssetRole role)
        {
            if (!go || (role != AssetRole.Player && role != AssetRole.Enemy))
                return;

            var align = go.GetComponent<VWS.RuntimeGroundAlign>();
            if (!align)
                align = Undo.AddComponent<VWS.RuntimeGroundAlign>(go);

            Undo.RecordObject(align, "VARCO runtime ground align");
            align.alignOnEnable = true;
            align.alignVisualChildrenOnly = true;
            align.continuous = false;
            align.useRootY = true;
            align.alignDuration = 0f;
            align.alignFramesAfterEnable = role == AssetRole.Player ? 12 : 6;
            align.footClearance = role == AssetRole.Player ? 0.08f : 0.05f;
            align.maxCorrectionPerCall = role == AssetRole.Player ? 3.0f : 2.5f;
            EditorUtility.SetDirty(align);

            EnsureCharacterInitialYAnchor(go, role);
        }

        void EnsureCharacterInitialYAnchor(GameObject go, AssetRole role)
        {
            if (!go || (role != AssetRole.Player && role != AssetRole.Enemy))
                return;

            var anchor = go.GetComponent<VWS.CharacterInitialYAnchor>();
            if (!anchor)
                anchor = Undo.AddComponent<VWS.CharacterInitialYAnchor>(go);

            Undo.RecordObject(anchor, "VARCO character initial Y anchor");
            anchor.syncInitialYFromSceneTransform = true;
            if (!anchor.HasStoredInitialY || Mathf.Abs(anchor.InitialY - go.transform.position.y) > 0.0001f)
                anchor.CaptureCurrentYAsInitial();

            var allowVerticalGameplayMotion = role == AssetRole.Player && genre == VWS.GenreType.Platform;
            anchor.ConfigureForRole(
                isPlayer: role == AssetRole.Player,
                usesNavMeshAgent: role == AssetRole.Enemy,
                allowVerticalGameplayMotion: allowVerticalGameplayMotion);
            anchor.makeVisualAlignUseInitialY = true;
            anchor.visualFootClearance = role == AssetRole.Player ? 0.08f : 0.05f;
            EditorUtility.SetDirty(anchor);
        }

        void NormalizeRuntimeGroundAlignForActiveCharacters()
        {
            var player = FindPlayerObjectForControllerCleanup();
            if (player)
                EnsureRuntimeGroundAlign(player, AssetRole.Player);

            var normalizedEnemies = 0;
            foreach (var enemyHealth in FindObjectsByType<VWS.EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!enemyHealth)
                    continue;

                var agent = enemyHealth.GetComponentInParent<NavMeshAgent>();
                var root = agent ? agent.gameObject : enemyHealth.gameObject;
                EnsureRuntimeGroundAlign(root, AssetRole.Enemy);
                normalizedEnemies++;
            }

            if (player || normalizedEnemies > 0)
                log.Add("Y축 안정화: 플레이어/몬스터 RuntimeGroundAlign을 시작 시 1회 보정 방식으로 전환했습니다.");
        }

        void AlignGeneratedObjectToPlayableSurface(GameObject go, AssetRole role)
        {
            if (!go || !ShouldAlignRoleToPlayableSurface(role))
                return;
            if (!TryGetGroundingRendererBounds(go, out var bounds))
                return;

            var surfaceY = 0f;
            if (!TrySamplePlayableSurfaceY(go, go.transform.position, out surfaceY))
                surfaceY = 0f;

            var targetBottom = surfaceY + SurfaceFootClearance(role);
            var deltaY = targetBottom - bounds.min.y;
            if (Mathf.Abs(deltaY) <= 0.025f)
                return;

            if ((role == AssetRole.Player || role == AssetRole.Enemy) && TryOffsetTopLevelVisualChildren(go, deltaY))
            {
                EditorUtility.SetDirty(go);
                return;
            }

            Undo.RecordObject(go.transform, "VARCO ground placement");
            go.transform.position += Vector3.up * deltaY;
            EditorUtility.SetDirty(go.transform);
        }

        static bool TryGetGroundingRendererBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
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

        void FinalizeGeneratedGrounding(Transform root)
        {
            if (!root)
                return;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player)
                AlignGeneratedObjectToPlayableSurface(player, AssetRole.Player);

            foreach (var enemy in FindObjectsByType<VWS.EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (enemy && !EditorUtility.IsPersistent(enemy.gameObject))
                    AlignGeneratedObjectToPlayableSurface(enemy.gameObject, AssetRole.Enemy);
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child && child.name.StartsWith("VARCO_EnvironmentProp_", StringComparison.Ordinal))
                    AlignGeneratedObjectToPlayableSurface(child.gameObject, AssetRole.ArenaCover);
            }
        }

        static bool ShouldAlignRoleToPlayableSurface(AssetRole role)
        {
            switch (role)
            {
                case AssetRole.Player:
                case AssetRole.Enemy:
                case AssetRole.ArenaCover:
                case AssetRole.Door:
                case AssetRole.PressurePlate:
                case AssetRole.MovableBox:
                case AssetRole.Checkpoint:
                    return true;
                default:
                    return false;
            }
        }

        static float SurfaceFootClearance(AssetRole role)
        {
            switch (role)
            {
                case AssetRole.PressurePlate:
                    return 0.012f;
                case AssetRole.ArenaCover:
                    return 0.018f;
                default:
                    return 0.025f;
            }
        }

        static bool TryOffsetTopLevelVisualChildren(GameObject go, float worldDeltaY)
        {
            if (!go || go.transform.childCount == 0)
                return false;

            var moved = false;
            var worldDelta = Vector3.up * worldDeltaY;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i);
                if (!child || !ChildContainsVisibleRenderer(child))
                    continue;

                Undo.RecordObject(child, "VARCO visual ground placement");
                child.localPosition += child.parent.InverseTransformVector(worldDelta);
                EditorUtility.SetDirty(child);
                moved = true;
            }

            return moved;
        }

        static bool ChildContainsVisibleRenderer(Transform child)
        {
            if (!child)
                return false;

            var renderers = child.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer && !(renderer is ParticleSystemRenderer))
                    return true;
            }
            return false;
        }

        static bool TrySamplePlayableSurfaceY(GameObject ignoreRoot, Vector3 position, out float surfaceY)
        {
            surfaceY = 0f;
            var found = false;
            var bestY = float.NegativeInfinity;
            var colliders = FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var collider in colliders)
            {
                if (!collider || collider.isTrigger || EditorUtility.IsPersistent(collider))
                    continue;
                if (ignoreRoot && collider.transform.IsChildOf(ignoreRoot.transform))
                    continue;

                var bounds = collider.bounds;
                if (!ContainsXZ(bounds, position, 0.7f))
                    continue;
                if (!LooksLikePlayableSurface(collider, bounds))
                    continue;
                if (bounds.max.y < -10f || bounds.max.y > position.y + 8f)
                    continue;

                if (!found || bounds.max.y > bestY)
                {
                    found = true;
                    bestY = bounds.max.y;
                }
            }

            if (!found)
                return false;

            surfaceY = bestY;
            return true;
        }

        static bool ContainsXZ(Bounds bounds, Vector3 position, float margin)
        {
            return position.x >= bounds.min.x - margin
                && position.x <= bounds.max.x + margin
                && position.z >= bounds.min.z - margin
                && position.z <= bounds.max.z + margin;
        }

        static bool LooksLikePlayableSurface(Collider collider, Bounds bounds)
        {
            var name = Normalize(collider.name + " " + collider.transform.root.name);
            if (ContainsAny(name, "wall", "frame", "goal", "door", "enemy", "player", "prop", "cover", "hazard", "spawn"))
                return false;
            if (bounds.size.y <= 0.75f && Mathf.Min(bounds.size.x, bounds.size.z) >= 0.35f)
                return true;

            return ContainsAny(name, "floor", "ground", "pad", "lane", "platform", "course", "path", "room", "field", "safe", "core", "start", "trail");
        }

        void NormalizePlacedAsset(GameObject go, AssetRole role, Vector3 fallbackScale)
        {
            if (!go || !TryGetWorldRendererBounds(go, out var bounds))
                return;

            var targetSize = TargetReferenceSize(role, fallbackScale);
            var currentSize = CurrentReferenceSize(role, bounds.size);
            if (targetSize <= 0.01f || currentSize <= 0.01f)
                return;

            var ratio = targetSize / currentSize;
            if (ratio > 0.72f && ratio < 1.42f)
                return;

            var appliedRatio = Mathf.Clamp(ratio, 0.02f, 50f);
            Undo.RecordObject(go.transform, "VARCO 에셋 크기 정리");
            go.transform.localScale *= appliedRatio;
            EditorUtility.SetDirty(go);
            log.Add(AssetRoleLabel(role) + " 비주얼 크기 자동 보정: " + currentSize.ToString("0.00") + " -> " + targetSize.ToString("0.00") + " (" + appliedRatio.ToString("0.00") + "x)");
        }

        bool ClampGeneratedAssetFootprint(GameObject go, AssetRole role, Vector3 fallbackScale, bool logChange)
        {
            if (!go || !TryGetWorldRendererBounds(go, out var bounds))
                return false;

            var maxAllowed = MaxGeneratedFootprintForRole(role, fallbackScale);
            var maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxAllowed <= 0.01f || maxSize <= maxAllowed * 1.35f)
                return false;

            var ratio = Mathf.Clamp(maxAllowed / maxSize, 0.01f, 1f);
            Undo.RecordObject(go.transform, "VARCO 생성 에셋 크기 제한");
            go.transform.localScale *= ratio;
            EditorUtility.SetDirty(go);

            if (logChange)
                log.Add(AssetRoleLabel(role) + " 대형 에셋 크기 제한: " + maxSize.ToString("0.0") + "m -> " + maxAllowed.ToString("0.0") + "m");

            return true;
        }

        static float MaxGeneratedFootprintForRole(AssetRole role, Vector3 fallbackScale)
        {
            switch (role)
            {
                case AssetRole.Player: return 2.8f;
                case AssetRole.Enemy: return 3f;
                case AssetRole.Weapon: return 1.8f;
                case AssetRole.ItemPickup: return 1.2f;
                case AssetRole.HealthPickup: return 1.3f;
                case AssetRole.Goal: return 3.5f;
                case AssetRole.Door: return 4.5f;
                case AssetRole.PressurePlate: return 2.5f;
                case AssetRole.HazardZone: return 4f;
                case AssetRole.MovingPlatform: return 4.5f;
                case AssetRole.MovableBox: return 1.8f;
                case AssetRole.Checkpoint: return 1.8f;
                case AssetRole.ArenaCover: return 3.2f;
                default: return Mathf.Max(4f, Mathf.Max(fallbackScale.x, Mathf.Max(fallbackScale.y, fallbackScale.z)) * 1.4f);
            }
        }

        int LimitGeneratedLights(Transform root, int maxLights, bool logChange)
        {
            if (!root)
                return 0;

            var lights = root.GetComponentsInChildren<Light>(true)
                .Where(light => light && light.type != LightType.Directional)
                .OrderByDescending(light => light.intensity * Mathf.Max(1f, light.range))
                .ToArray();
            var removeCount = Mathf.Max(0, lights.Length - Mathf.Max(0, maxLights));
            if (removeCount == 0)
                return 0;

            for (int i = Mathf.Max(0, maxLights); i < lights.Length; i++)
                Undo.DestroyObjectImmediate(lights[i]);

            if (logChange)
                log.Add("생성 에셋 조명 정리: " + root.name + "에서 추가 조명 " + removeCount + "개를 제거했습니다.");

            return removeCount;
        }

        static int MaxGeneratedLightsForRole(AssetRole role)
        {
            switch (role)
            {
                case AssetRole.Goal:
                case AssetRole.HazardZone:
                case AssetRole.Checkpoint:
                    return 2;
                case AssetRole.Door:
                case AssetRole.MovingPlatform:
                case AssetRole.ArenaCover:
                    return 1;
                default:
                    return 0;
            }
        }

        static float TargetReferenceSize(AssetRole role, Vector3 fallbackScale)
        {
            switch (role)
            {
                case AssetRole.Player: return 1.85f;
                case AssetRole.Enemy: return 1.75f;
                case AssetRole.Weapon: return 1.25f;
                case AssetRole.ItemPickup: return 0.6f;
                case AssetRole.HealthPickup: return 0.75f;
                case AssetRole.Goal: return 1.8f;
                case AssetRole.Door: return 3f;
                case AssetRole.PressurePlate: return 2f;
                case AssetRole.HazardZone: return 3f;
                case AssetRole.MovingPlatform: return 3.4f;
                case AssetRole.MovableBox: return 1.2f;
                case AssetRole.Checkpoint: return 1.1f;
                case AssetRole.ArenaCover: return 2.35f;
                default: return Mathf.Max(fallbackScale.x, fallbackScale.y, fallbackScale.z);
            }
        }

        static float CurrentReferenceSize(AssetRole role, Vector3 boundsSize)
        {
            switch (role)
            {
                case AssetRole.PressurePlate:
                case AssetRole.HazardZone:
                case AssetRole.MovingPlatform:
                case AssetRole.ArenaCover:
                    return Mathf.Max(boundsSize.x, boundsSize.z);
                case AssetRole.ItemPickup:
                case AssetRole.HealthPickup:
                case AssetRole.MovableBox:
                case AssetRole.Weapon:
                    return Mathf.Max(boundsSize.x, boundsSize.y, boundsSize.z);
                default:
                    return boundsSize.y;
            }
        }

        static bool TryGetWorldRendererBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var hasAny = false;
            foreach (var renderer in renderers)
            {
                if (!renderer || renderer is ParticleSystemRenderer)
                    continue;

                if (!TryGetStableWorldBounds(renderer, out var rendererBounds))
                    rendererBounds = renderer.bounds;
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

        Transform EnsureLayoutRoot()
        {
            var existing = FindAutoBuildLayoutRoot();
            if (existing)
                return existing.transform;

            var root = new GameObject(AutoBuildLayoutRootName);
            Undo.RegisterCreatedObjectUndo(root, "VARCO 자동 제작 배치 생성");
            return root.transform;
        }

        void ClearAutoBuildLayoutBeforePresetBuild()
        {
            var existing = FindAutoBuildLayoutRoot();
            if (!existing)
                return;

            var removedCount = Mathf.Max(1, existing.GetComponentsInChildren<Transform>(true).Length);
            Undo.DestroyObjectImmediate(existing);
            log.Add("Cleared previous auto build layout before preset build: " + removedCount + " object(s).");
        }

        void PrepareSceneForPresetBuild()
        {
            RemoveLegacyGuideRoots();
            ConfigureLegacyArenaWallsForGenre();
            ConfigureLegacyArenaPropsForGenre();
            ConfigureLegacyGroundForGenre();
        }

        void ClearPresetTransientRootsBeforeBuild()
        {
            RemoveMovingPlatformPathRoots();
        }

        void RemoveLegacyGuideRoots()
        {
            var removed = 0;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!root)
                    continue;

                if (root.name.StartsWith("Guide - ", StringComparison.Ordinal) ||
                    root.name.StartsWith("Scene Goal - ", StringComparison.Ordinal))
                {
                    Undo.DestroyObjectImmediate(root);
                    removed++;
                }
            }

            if (removed > 0)
                log.Add("Removed legacy guide object(s): " + removed + ".");
        }

        void ConfigureLegacyArenaWallsForGenre()
        {
            var keepArenaWalls = genre == VWS.GenreType.Arena;
            var changed = 0;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!root || !root.name.StartsWith("Arena_Wall_", StringComparison.Ordinal))
                    continue;

                if (root.activeSelf == keepArenaWalls)
                    continue;

                Undo.RecordObject(root, "VARCO arena wall visibility");
                root.SetActive(keepArenaWalls);
                changed++;
            }

            if (changed > 0)
                log.Add((keepArenaWalls ? "Enabled" : "Disabled") + " legacy arena wall object(s): " + changed + ".");
        }

        void ConfigureLegacyArenaPropsForGenre()
        {
            var keepCover = genre == VWS.GenreType.Arena;
            var keepSpawnMarkers = genre == VWS.GenreType.Arena || genre == VWS.GenreType.Exploration;
            var coverChanged = 0;
            var spawnChanged = 0;

            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!root)
                    continue;

                if (root.name.Equals("Cover_Left", StringComparison.Ordinal) ||
                    root.name.Equals("Cover_Right", StringComparison.Ordinal))
                {
                    if (root.activeSelf != keepCover)
                    {
                        Undo.RecordObject(root, "VARCO arena cover visibility");
                        root.SetActive(keepCover);
                        coverChanged++;
                    }
                    continue;
                }

                if (root.name.Equals("Spawn_A", StringComparison.Ordinal) ||
                    root.name.Equals("Spawn_B", StringComparison.Ordinal) ||
                    root.name.Equals("Spawn_C", StringComparison.Ordinal))
                {
                    if (root.activeSelf != keepSpawnMarkers)
                    {
                        Undo.RecordObject(root, "VARCO spawn marker visibility");
                        root.SetActive(keepSpawnMarkers);
                        spawnChanged++;
                    }
                }
            }

            if (coverChanged > 0)
                log.Add((keepCover ? "Enabled" : "Disabled") + " legacy arena cover object(s): " + coverChanged + ".");
            if (spawnChanged > 0)
                log.Add((keepSpawnMarkers ? "Enabled" : "Disabled") + " legacy spawn marker object(s): " + spawnChanged + ".");
        }

        void ConfigureLegacyGroundForGenre()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!root)
                    continue;

                var isKnownGround =
                    root.name.Equals("Arena_Ground", StringComparison.Ordinal) ||
                    root.name.Equals("VARCO_Ground", StringComparison.Ordinal);
                if (!isKnownGround)
                    continue;

                var isPlatform = genre == VWS.GenreType.Platform;
                Undo.RecordObject(root.transform, "VARCO genre ground");
                Undo.RecordObject(root, "VARCO genre ground visibility");
                root.SetActive(true);
                root.transform.position = isPlatform ? new Vector3(2.5f, -2.2f, 0f) : Vector3.zero;
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = isPlatform ? new Vector3(34f, 0.08f, 15f) : new Vector3(18f, 0.3f, 18f);
                root.isStatic = true;
                SetColor(root, isPlatform ? new Color(0.12f, 0.16f, 0.24f) : new Color(0.25f, 0.3f, 0.34f));

                foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                {
                    Undo.RecordObject(collider, "VARCO genre ground collision");
                    collider.enabled = !isPlatform;
                }
            }
        }

        void RemoveMovingPlatformPathRoots()
        {
            var removed = 0;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!root)
                    continue;

                if (root.name.EndsWith("_Path", StringComparison.Ordinal) ||
                    root.name.Equals("VARCO_PlatformMovingPath", StringComparison.Ordinal))
                {
                    Undo.DestroyObjectImmediate(root);
                    removed++;
                }
            }

            if (removed > 0)
                log.Add("Removed stale moving platform path object(s): " + removed + ".");
        }

        static GameObject FindAutoBuildLayoutRoot()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return null;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root && string.Equals(root.name, AutoBuildLayoutRootName, StringComparison.Ordinal))
                    return root;
            }

            return null;
        }

        void EnsureBaseEnvironment()
        {
            EnsureTag("Player");
            EnsureMainCamera();
            EnsureDirectionalLight();

            if (HasGroundLikeObject())
                return;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(ground, "VARCO 기본 바닥 생성");
            ground.name = "VARCO_Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = genre == VWS.GenreType.Platform ? new Vector3(5f, 0.35f, 5f) : new Vector3(18f, 0.3f, 18f);
            SetColor(ground, new Color(0.25f, 0.3f, 0.34f));
            ground.isStatic = true;
            log.Add("기본 바닥을 생성했습니다.");
        }

        bool HasGroundLikeObject()
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                var lower = root.name.ToLowerInvariant();
                if (lower.Contains("ground") || lower.Contains("floor") || lower.Contains("platform") || lower.Contains("terrain"))
                    return true;
            }
            return false;
        }

        void EnsureMainCamera()
        {
            var camera = Camera.main ? Camera.main : FindFirstObjectByType<Camera>();
            if (!camera)
            {
                var go = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(go, "메인 카메라 생성");
                go.tag = "MainCamera";
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            else
            {
                Undo.RecordObject(camera.gameObject, "메인 카메라 설정");
                camera.tag = "MainCamera";
                if (!camera.GetComponent<AudioListener>())
                    Undo.AddComponent<AudioListener>(camera.gameObject);
            }

            camera.fieldOfView = 58f;
            EditorUtility.SetDirty(camera.gameObject);
        }

        void EnsureDirectionalLight()
        {
            var light = FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (!light)
            {
                var go = new GameObject("Directional Light");
                Undo.RegisterCreatedObjectUndo(go, "기본 조명 생성");
                light = go.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            Undo.RecordObject(light, "기본 조명 설정");
            light.transform.rotation = Quaternion.Euler(GenreSunAngle(), -35f, 0f);
            light.intensity = genre == VWS.GenreType.Platform ? 1.0f : 1.35f;
            light.color = genre == VWS.GenreType.Puzzle ? new Color(1f, 0.82f, 0.58f) : Color.white;
            light.shadows = LightShadows.Soft;
            EditorUtility.SetDirty(light);
        }

        void OptimizeSceneLightingForEditorPerformance()
        {
            const int maxActiveAdditionalLights = 64;
            const int maxAdditionalShadowLights = 4;
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            var activeAdditionalLights = new HashSet<Light>(
                lights
                    .Where(light => light && light.type != LightType.Directional)
                    .OrderByDescending(light => light.intensity * Mathf.Max(1f, light.range))
                    .Take(maxActiveAdditionalLights));
            var additionalShadowLights = 0;
            var disabledLights = 0;
            var disabledShadows = 0;

            foreach (var light in lights)
            {
                if (!light)
                    continue;

                Undo.RecordObject(light, "VARCO Optimize Lighting");
                if (light.type == LightType.Directional)
                {
                    light.shadows = LightShadows.Soft;
                    EditorUtility.SetDirty(light);
                    continue;
                }

                if (!activeAdditionalLights.Contains(light))
                {
                    if (light.enabled)
                        disabledLights++;
                    light.enabled = false;
                    light.shadows = LightShadows.None;
                    EditorUtility.SetDirty(light);
                    continue;
                }

                light.enabled = true;
                light.renderMode = LightRenderMode.Auto;
                if (light.shadows != LightShadows.None)
                {
                    if (additionalShadowLights < maxAdditionalShadowLights && light.intensity >= 0.35f)
                    {
                        additionalShadowLights++;
                    }
                    else
                    {
                        light.shadows = LightShadows.None;
                        disabledShadows++;
                    }
                }

                EditorUtility.SetDirty(light);
            }

            if (disabledLights > 0 || disabledShadows > 0)
                log.Add("조명 최적화: 추가 조명 " + disabledLights + "개를 끄고 " + disabledShadows + "개의 그림자를 꺼서 편집기 성능을 개선했습니다.");
        }

        void OptimizeGeneratedLayoutForPerformance()
        {
            var root = FindAutoBuildLayoutRoot();
            if (!root)
                return;

            var clamped = 0;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (!child)
                    continue;
                if (ShouldSkipGeneratedFootprintClamp(child.name))
                    continue;

                var role = GuessGeneratedRoleFromName(child.name);
                if (ClampGeneratedAssetFootprint(child.gameObject, role, Vector3.one, logChange: false))
                    clamped++;
            }

            var removedLights = LimitGeneratedLights(root.transform, 32, logChange: false);
            if (clamped > 0 || removedLights > 0)
                log.Add("생성 배치 최적화: 대형 에셋 " + clamped + "개 크기 제한, 추가 조명 " + removedLights + "개 제거.");
        }

        static bool ShouldSkipGeneratedFootprintClamp(string name)
        {
            var text = Normalize(name);
            return ContainsAny(text,
                "floor", "field", "path", "guide", "routeline", "itempad", "startcamp", "goalplaza",
                "boundary", "sidepocket", "clearing", "enemyzone", "landmarkgate", "goalframe", "exitframe", "escapeframe", "returnlane",
                "corridorhint", "safestart", "safewall", "escapemarker", "markerbase",
                "course", "intro", "rail", "arrow", "step", "deck", "dock", "island", "bumper",
                "wall", "room", "shell", "pedestal");
        }

        static AssetRole GuessGeneratedRoleFromName(string name)
        {
            var text = Normalize(name);
            if (ContainsAny(text, "player", "hero", "knight", "warrior", "astronaut", "explorer", "플레이어", "주인공")) return AssetRole.Player;
            if (ContainsAny(text, "enemy", "zombie", "boss", "orc", "drone", "monster", "적", "좀비")) return AssetRole.Enemy;
            if (ContainsAny(text, "weapon", "sword", "gun", "무기", "검")) return AssetRole.Weapon;
            if (ContainsAny(text, "healthpickup", "heal", "potion", "회복")) return AssetRole.HealthPickup;
            if (ContainsAny(text, "item", "collect", "coin", "gem", "treasure", "아이템", "보물")) return AssetRole.ItemPickup;
            if (ContainsAny(text, "checkpoint", "체크")) return AssetRole.Checkpoint;
            if (ContainsAny(text, "hazard", "deathzone", "fire", "trap", "위험", "장애물")) return AssetRole.HazardZone;
            if (ContainsAny(text, "pressureplate", "switch", "button", "압력판", "스위치")) return AssetRole.PressurePlate;
            if (ContainsAny(text, "door", "gate", "문", "게이트")) return AssetRole.Door;
            if (ContainsAny(text, "movingplatform", "platform", "발판")) return AssetRole.MovingPlatform;
            if (ContainsAny(text, "movablebox", "box", "crate", "상자")) return AssetRole.MovableBox;
            if (ContainsAny(text, "goal", "exit", "portal", "finish", "목표", "출구")) return AssetRole.Goal;
            return AssetRole.ArenaCover;
        }

        void EnsureGameManagerAndProfile()
        {
            var gm = FindFirstObjectByType<VWS.GameManager>();
            if (!gm)
            {
                var root = new GameObject("VW_Bootstrap");
                Undo.RegisterCreatedObjectUndo(root, "게임 시작 시스템 생성");
                gm = root.AddComponent<VWS.GameManager>();
                root.AddComponent<VWS.SceneBootstrap>();
            }

            var profile = EnsureProfile(genre);
            Undo.RecordObject(gm, "게임 매니저 설정");
            gm.profile = profile;
            gm.loadResultScenes = false;
            gm.clearSceneName = "Clear";
            gm.gameOverSceneName = "GameOver";
            EditorUtility.SetDirty(gm);
        }

        void EnsureGameManagerProfileMatchesGenre()
        {
            var gm = FindFirstObjectByType<VWS.GameManager>();
            if (!gm)
                return;

            var expectedProfile = EnsureProfile(genre);
            if (gm.profile == expectedProfile && expectedProfile && expectedProfile.genre == genre)
                return;

            Undo.RecordObject(gm, "게임 매니저 프로필 장르 보정");
            gm.profile = expectedProfile;
            EditorUtility.SetDirty(gm);
            log.Add("게임 프로필 보정: 현재 자동 제작 장르와 GameManager 프로필을 일치시켰습니다.");
        }

        VWS.GameProfile EnsureProfile(VWS.GenreType targetGenre)
        {
            EnsureFolder("Assets/ScriptableObjects");
            EnsureFolder("Assets/ScriptableObjects/GameProfiles");
            var path = ProfileByGenre[targetGenre];
            var profile = AssetDatabase.LoadAssetAtPath<VWS.GameProfile>(path);
            if (!profile)
            {
                profile = CreateInstance<VWS.GameProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            Undo.RecordObject(profile, "VARCO 게임 프로필 설정");
            profile.genre = targetGenre;
            profile.playerMaxHP = PlayerMaxHpForDifficulty();
            profile.waveCount = blockEnemyWave ? 1 : 0;
            var clearCondition = PrimaryClearCondition();
            profile.itemGoal = clearCondition == VWS.CompletionCondition.CollectItems && blockItems ? Mathf.Max(1, itemGoal) : 0;
            profile.clearCondition = clearCondition;
            profile.objectiveText = BuildHudObjectiveText(targetGenre);
            profile.controlGuideText = BuildHudControlGuideText(targetGenre);
            profile.clearMessage = BuildHudClearMessage(targetGenre);
            profile.gameOverMessage = BuildHudGameOverMessage();
            profile.designNotes = "VARCO 게임 메이커로 생성됨. 블록: " + ActiveBlockSummary();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        string BuildHudObjectiveText(VWS.GenreType targetGenre)
        {
            switch (recipe)
            {
                case GameRecipe.BossBattle:
                    return "보스를 처치하고 아레나를 클리어하세요.";
                case GameRecipe.ZombieSurvival:
                    return "좀비를 피하고 적 웨이브를 모두 처치하세요.";
                case GameRecipe.TreasureHunt:
                    return "보물 " + Mathf.Max(1, itemGoal) + "개를 모으고 목표 지점에 도달하세요.";
                case GameRecipe.EscapeRoom:
                    return "상자와 압력판으로 문 퍼즐을 풀고 탈출하세요.";
                case GameRecipe.ObstacleRun:
                    return "위험 구역과 발판을 지나 목표 지점에 도달하세요.";
                case GameRecipe.CollectAndEscape:
                    return "아이템 " + Mathf.Max(1, itemGoal) + "개를 모으고 탈출 지점으로 이동하세요.";
                case GameRecipe.SurvivalTimer:
                    return Mathf.CeilToInt(countdownSeconds) + "초 동안 살아남고 적을 처치하세요.";
                case GameRecipe.CombatWave:
                    return "적 웨이브를 모두 처치하세요.";
                case GameRecipe.ExplorationQuest:
                    return "탐험하며 아이템을 모으고 목표 지점에 도달하세요.";
                case GameRecipe.DoorPuzzle:
                    return "압력판을 작동시켜 문을 열고 목표 지점에 도달하세요.";
                case GameRecipe.PlatformCourse:
                    return "발판을 건너 목표 지점에 도달하세요.";
            }

            switch (PrimaryClearCondition())
            {
                case VWS.CompletionCondition.DefeatWaves:
                    return "적 웨이브를 모두 처치하세요.";
                case VWS.CompletionCondition.CollectItems:
                    return "아이템 " + Mathf.Max(1, itemGoal) + "개를 모으세요.";
                default:
                    return GenreLabel(targetGenre) + " 목표 지점에 도달하세요.";
            }
        }

        string BuildHudClearMessage(VWS.GenreType targetGenre)
        {
            return GenreLabel(targetGenre) + " 게임을 클리어했습니다. 새 에셋이나 블록을 바꿔 다른 버전도 만들어보세요.";
        }

        string BuildHudControlGuideText(VWS.GenreType targetGenre)
        {
            var parts = new List<string>();

            if (targetGenre == VWS.GenreType.Platform)
            {
                parts.Add("이동: A/D 또는 방향키");
                parts.Add("점프: Space");
            }
            else
            {
                parts.Add("이동: WASD");
                parts.Add("시점: 마우스");
            }

            if (blockWeapon && targetGenre != VWS.GenreType.Platform)
                parts.Add("공격: 마우스 왼쪽");
            if (blockItems)
                parts.Add("수집: 아이템에 닿기");
            if (blockPuzzleDoor || blockMovableBox)
                parts.Add("퍼즐: 상자 밀기/압력판");
            if (blockCheckpoint)
                parts.Add("체크포인트: 마커 통과");

            return string.Join(" | ", parts);
        }

        string BuildHudGameOverMessage()
        {
            if (blockCheckpoint)
                return "실패했습니다. 체크포인트 위치와 위험 구역 배치를 확인하고 다시 도전하세요.";
            if (blockHealthPickup)
                return "실패했습니다. 회복 아이템 위치나 난이도를 조정해 다시 도전하세요.";
            return "실패했습니다. 난이도, 적 수, 제한시간을 조정해 다시 도전하세요.";
        }

        VWS.CompletionCondition PrimaryClearCondition()
        {
            if (genre == VWS.GenreType.Platform)
                return VWS.CompletionCondition.ReachGoal;
            if (blockEnemyWave && !blockGoal)
                return VWS.CompletionCondition.DefeatWaves;
            if (blockItems && itemGoal > 0)
                return VWS.CompletionCondition.CollectItems;
            if (blockEnemyWave && genre == VWS.GenreType.Arena)
                return VWS.CompletionCondition.DefeatWaves;
            return VWS.CompletionCondition.ReachGoal;
        }

        string ActiveBlockSummary()
        {
            var blocks = new List<string>();
            blocks.Add("난이도(" + DifficultyLabel() + ")");
            blocks.Add("카메라(" + CameraPresetLabel() + ")");
            blocks.Add("이동(" + PlayerMovementLabel() + ")");
            if (blockPlayer) blocks.Add("플레이어(" + PlayerCharacterLabel() + ")");
            if (blockWeapon) blocks.Add("무기");
            if (blockEnemyWave) blocks.Add("적 웨이브(" + EffectiveWaveEnemyCount() + ", " + EnemyCharacterLabel() + ")");
            if (blockItems) blocks.Add("수집 아이템(" + Mathf.Max(1, itemGoal) + ")");
            if (blockGoal) blocks.Add("목표 지점");
            if (blockHealthPickup) blocks.Add("회복 아이템");
            if (blockHazard) blocks.Add("위험 구역");
            if (blockCheckpoint) blocks.Add("체크포인트");
            if (blockFallRespawn) blocks.Add("낙사 리스폰 안전망");
            if (blockMovingPlatform) blocks.Add("이동 발판");
            if (blockPuzzleDoor) blocks.Add("문 퍼즐");
            if (blockMovableBox) blocks.Add("밀 수 있는 상자");
            if (blockCover) blocks.Add("환경 소품/엄폐물");
            if (blockCountdown) blocks.Add("타이머(" + EffectiveCountdownSeconds().ToString("0") + "초)");
            return blocks.Count > 0 ? string.Join(", ", blocks) : "없음";
        }

        string BuildNoCodeRecipePreview()
        {
            return "노코드 레시피: " + GenreLabel(genre) + " / " + GameRecipeLabel(recipe)
                + "\n템플릿: " + BlockTemplateLabel()
                + "\n클리어 조건: " + CompletionConditionLabel(PrimaryClearCondition())
                + "\n" + AssetSlotSummary()
                + "\n조립 상태: " + BlockAssemblySummary(BuildBlockAssemblyStatuses())
                + "\n블록: " + ActiveBlockSummary();
        }

        IEnumerable<string> BuildNoCodeRecipeSteps()
        {
            yield return "게임 매니저: 현재 장르 프로필과 클리어 조건(" + CompletionConditionLabel(PrimaryClearCondition()) + ")을 만들거나 갱신합니다.";
            if (blockPlayer)
                yield return "플레이어 블록: " + PlayerCharacterLabel() + " 캐릭터, " + CameraPresetLabel() + " 카메라, " + PlayerMovementLabel() + "을 연결합니다.";
            if (blockWeapon)
                yield return "무기 블록: 감지된 무기 모델을 플레이어 손/공격 비주얼에 장착합니다.";
            if (blockEnemyWave)
                yield return "적 웨이브 블록: " + EnemyCharacterLabel() + " 적 " + EffectiveWaveEnemyCount() + "명을 만들고 웨이브 매니저에 연결합니다.";
            if (blockItems)
                yield return "수집 블록: 아이템 " + Mathf.Max(1, itemGoal) + "개를 배치하고 수집 카운터에 연결합니다.";
            if (blockGoal)
                yield return "목표 블록: 선택한 클리어 조건에 맞는 목표 트리거를 만듭니다.";
            if (blockHealthPickup)
                yield return "회복 블록: " + DifficultyLabel() + " 난이도에 맞춘 회복 아이템을 배치합니다.";
            if (blockHazard)
                yield return "위험 구역 블록: " + DifficultyLabel() + " 난이도에 맞춘 데미지 구역을 추가합니다.";
            if (blockCheckpoint)
                yield return "체크포인트 블록: 플랫폼/탐험 경로에 리스폰 위치를 배치합니다.";
            if (blockFallRespawn)
                yield return "낙사 리스폰 블록: 플레이어가 맵 아래로 떨어지면 체크포인트 또는 시작 위치로 되돌리는 안전망을 추가합니다.";
            if (blockMovingPlatform)
                yield return "이동 발판 블록: 난이도에 맞춘 속도의 움직이는 발판을 추가합니다.";
            if (blockPuzzleDoor)
                yield return "문 퍼즐 블록: 문과 압력판 퍼즐을 만듭니다.";
            if (blockMovableBox)
                yield return "밀기 상자 블록: 퍼즐 상호작용용 상자를 추가합니다.";
            if (blockCover)
                yield return "환경 소품 블록: 장르 분위기에 맞는 소품과 엄폐물을 2~3개 배치합니다.";
            if (blockCountdown)
                yield return "타이머 블록: " + EffectiveCountdownSeconds().ToString("0") + "초 제한시간을 추가합니다.";
            if (blockHud && addModernHud)
                yield return "HUD 블록: HP, 목표, 아이템, 타이머 표시를 추가합니다.";
            if (blockVisuals && applyVisualPreset)
                yield return "비주얼 블록: 조명, 볼륨, 카메라에 맞는 장면 분위기를 적용합니다.";
            if (blockSound && autoSounds)
                yield return "사운드 블록: 오디오 클립을 동기화하고 효과음/BGM을 연결합니다.";
            if (runSafetyPass)
                yield return "안전 점검: 생성 후 빠진 콜라이더, 리지드바디, 태그, 연결 상태를 보정합니다.";
        }

        int PlayerMaxHpForDifficulty()
        {
            switch (difficulty)
            {
                case DifficultyPreset.Story:
                    return 150;
                case DifficultyPreset.Hard:
                    return 80;
                case DifficultyPreset.Nightmare:
                    return 60;
                default:
                    return 100;
            }
        }

        int HealAmountForDifficulty()
        {
            switch (difficulty)
            {
                case DifficultyPreset.Story:
                    return 35;
                case DifficultyPreset.Hard:
                    return 20;
                case DifficultyPreset.Nightmare:
                    return 15;
                default:
                    return 25;
            }
        }

        int HazardDpsForDifficulty()
        {
            switch (difficulty)
            {
                case DifficultyPreset.Story:
                    return 8;
                case DifficultyPreset.Hard:
                    return 22;
                case DifficultyPreset.Nightmare:
                    return 30;
                default:
                    return 15;
            }
        }

        float MovingPlatformSpeedForDifficulty()
        {
            switch (difficulty)
            {
                case DifficultyPreset.Story:
                    return 0.9f;
                case DifficultyPreset.Hard:
                    return 1.45f;
                case DifficultyPreset.Nightmare:
                    return 1.7f;
                default:
                    return 1.2f;
            }
        }

        int EffectiveWaveEnemyCount()
        {
            var baseCount = Mathf.Max(1, waveEnemyCount);
            var maxCount = genre == VWS.GenreType.Exploration && recipe != GameRecipe.ZombieSurvival ? 4 : 12;
            switch (difficulty)
            {
                case DifficultyPreset.Story:
                    return Mathf.Clamp(baseCount - 1, 1, maxCount);
                case DifficultyPreset.Hard:
                    return Mathf.Clamp(baseCount + 1, 1, maxCount);
                case DifficultyPreset.Nightmare:
                    return Mathf.Clamp(baseCount + 2, 1, maxCount);
                default:
                    return Mathf.Clamp(baseCount, 1, maxCount);
            }
        }

        float EffectiveCountdownSeconds()
        {
            var seconds = Mathf.Max(10f, countdownSeconds);
            switch (difficulty)
            {
                case DifficultyPreset.Story:
                    seconds *= 1.25f;
                    break;
                case DifficultyPreset.Hard:
                    seconds *= 0.85f;
                    break;
                case DifficultyPreset.Nightmare:
                    seconds *= 0.7f;
                    break;
            }

            return Mathf.Clamp(seconds, 10f, 300f);
        }

        string DifficultyLabel()
        {
            return DifficultyPresetLabel(difficulty);
        }

        string CameraPresetLabel()
        {
            return cameraPreset == CameraPresetChoice.Auto
                ? "자동 (" + CameraViewPresetLabel(CameraForGenre()) + ")"
                : CameraPresetChoiceLabel(cameraPreset);
        }

        bool MoveInFacingDirectionForPlayer()
        {
            switch (playerMovement)
            {
                case PlayerMovementChoice.FacingDirection:
                    return true;
                case PlayerMovementChoice.CameraRelative:
                    return false;
                default:
                    return false;
            }
        }

        string PlayerMovementLabel()
        {
            if (playerMovement == PlayerMovementChoice.Auto)
                return "자동 (카메라 기준 이동)";
            return PlayerMovementChoiceLabel(playerMovement);
        }

        void EnsureModernHud()
        {
            var hud = FindFirstObjectByType<VWS.VARCOGameHUD>();
            if (!hud)
            {
                var go = new GameObject("VARCO_GameHUD");
                Undo.RegisterCreatedObjectUndo(go, "VARCO HUD 생성");
                hud = go.AddComponent<VWS.VARCOGameHUD>();
            }

            Undo.RecordObject(hud, "VARCO HUD 설정");
            hud.fallbackGenre = genre;
            hud.modeLabelOverride = GameRecipeLabel(recipe);
            hud.hideWorkshopHud = true;
            hud.showHud = true;
            hud.objectiveOverride = "";
            EditorUtility.SetDirty(hud);

            var showCombatHud = genre == VWS.GenreType.Arena || (genre == VWS.GenreType.Exploration && blockEnemyWave);
            VWS.CombatHealthUI primaryCombatHud = null;
            foreach (var combatHud in FindObjectsByType<VWS.CombatHealthUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!combatHud)
                    continue;
                if (!primaryCombatHud)
                {
                    primaryCombatHud = combatHud;
                }
                else
                {
                    Undo.DestroyObjectImmediate(combatHud.gameObject);
                    continue;
                }

                Undo.RecordObject(combatHud, "VARCO Combat HUD");
                Undo.RecordObject(combatHud.gameObject, "VARCO Combat HUD visibility");
                combatHud.gameObject.SetActive(showCombatHud);
                combatHud.maxVisibleEnemyBars = genre == VWS.GenreType.Exploration ? 1 : 6;
                combatHud.enemyBarOffset = genre == VWS.GenreType.Exploration ? new Vector3(0f, 1.35f, 0f) : new Vector3(0f, 2.2f, 0f);
                combatHud.enemyBarSize = genre == VWS.GenreType.Exploration ? new Vector2(58f, 6f) : new Vector2(72f, 8f);
                EditorUtility.SetDirty(combatHud);
            }

            foreach (var old in FindObjectsByType<VWS.WorkshopHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(old, "기존 워크숍 HUD 숨김");
                old.showDuringPlay = false;
                Undo.RecordObject(old.gameObject, "기존 워크숍 HUD 비활성화");
                old.gameObject.SetActive(false);
                EditorUtility.SetDirty(old);
            }

            ConfigurePlaytestTuningOverlayVisibility(false);
        }

        void ConfigurePlaytestTuningOverlayVisibility(bool visible)
        {
            foreach (var overlay in FindObjectsByType<VWS.PlaytestTuningOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(overlay, "VARCO playtest tuning overlay visibility");
                overlay.visible = visible;
                EditorUtility.SetDirty(overlay);
            }
        }

        void EnsureCountdownTimer()
        {
            var timer = FindFirstObjectByType<VWS.CountdownTimer>();
            if (!timer)
            {
                var gm = FindFirstObjectByType<VWS.GameManager>();
                var target = gm ? gm.gameObject : new GameObject("VARCO_Countdown");
                timer = Undo.AddComponent<VWS.CountdownTimer>(target);
            }

            Undo.RecordObject(timer, "제한 시간 타이머 설정");
            timer.totalSeconds = EffectiveCountdownSeconds();
            timer.pauseWhenNotPlaying = true;
            EditorUtility.SetDirty(timer);
        }

        void ConfigureWaveManagerForGenre()
        {
            if (blockTemplate == BlockTemplate.FullFeatureSandbox)
            {
                RemoveWaveManagersForNonEnemyPreset();
                log.Add("Full feature sandbox uses a placed combat demo enemy; automatic wave spawning is disabled for classroom safety.");
                return;
            }

            if (!blockEnemyWave)
            {
                RemoveWaveManagersForNonEnemyPreset();
                return;
            }

            var wave = FindFirstObjectByType<VWS.WaveManager>();
            if (!wave)
                wave = CreateWaveManagerRoot();
            if (!wave)
                return;

            Undo.RecordObject(wave, "웨이브 매니저 설정");
            Undo.RecordObject(wave.transform, "VARCO wave root align");
            wave.transform.position = Vector3.zero;
            wave.transform.rotation = Quaternion.identity;
            wave.transform.localScale = Vector3.one;
            wave.clearWhenAllWavesCleared = PrimaryClearCondition() == VWS.CompletionCondition.DefeatWaves;
            wave.delayBetweenWaves = Mathf.Max(0.5f, wave.delayBetweenWaves);
            EnsureWaveSpawnArea(wave);

            var enemyPrefab = FindOrCreateWaveEnemyPrefab();
            if (wave.waves == null || wave.waves.Length == 0)
                wave.waves = new[] { new VWS.WaveDataVW() };

            var enemyCount = EffectiveWaveEnemyCount();
            foreach (var data in wave.waves)
            {
                if (data == null)
                    continue;
                data.enemyCount = enemyCount;
                data.spawnInterval = Mathf.Max(MinimumWaveSpawnInterval(), data.spawnInterval > 0f ? data.spawnInterval : 0.7f);
                if (enemyPrefab && (!data.enemyPrefab || !data.enemyPrefab.GetComponentInChildren<VWS.EnemyHealth>(true) || !EnemyPrefabMatchesSelection(data.enemyPrefab)))
                    data.enemyPrefab = enemyPrefab;
            }

            EditorUtility.SetDirty(wave);
            log.Add("웨이브 매니저 준비 완료: 웨이브 " + wave.waves.Length + "개, 적 " + enemyCount + "명, 웨이브 클리어=" + BoolLabel(wave.clearWhenAllWavesCleared));
        }

        float MinimumWaveSpawnInterval()
        {
            switch (recipe)
            {
                case GameRecipe.CombatWave:
                    return 1.6f;
                case GameRecipe.SurvivalTimer:
                    return 0.85f;
                case GameRecipe.ZombieSurvival:
                case GameRecipe.ExplorationQuest:
                    return 1.0f;
                default:
                    return 0.6f;
            }
        }

        void ApplyRecipeCombatTuningToEnemies()
        {
            if (!blockEnemyWave)
                return;

            var tunedSceneEnemies = 0;
            foreach (var enemy in FindObjectsByType<VWS.EnemyHealth>(FindObjectsSortMode.None))
            {
                if (!enemy || EditorUtility.IsPersistent(enemy.gameObject))
                    continue;

                if (ApplyEnemyTuningForCurrentRecipe(enemy.gameObject))
                    tunedSceneEnemies++;
            }

            var tunedPrefabs = 0;
            var wave = FindFirstObjectByType<VWS.WaveManager>();
            if (wave && wave.waves != null)
            {
                foreach (var data in wave.waves)
                {
                    if (data == null || !data.enemyPrefab)
                        continue;

                    if (ApplyEnemyTuningForCurrentRecipe(data.enemyPrefab))
                        tunedPrefabs++;
                }
            }

            if (tunedSceneEnemies > 0 || tunedPrefabs > 0)
                log.Add("전투 난이도 보정 완료: 씬 적 " + tunedSceneEnemies + "개, 웨이브 프리팹 " + tunedPrefabs + "개");
        }

        bool ApplyEnemyTuningForCurrentRecipe(GameObject enemy)
        {
            if (!enemy)
                return false;

            switch (recipe)
            {
                case GameRecipe.CombatWave:
                    return ApplyArenaCombatWaveEnemyTuning(enemy);
                case GameRecipe.BossBattle:
                    return ApplyRecipeEnemyTuning(enemy, 160, 220, 40, 18f, 2.0f, 2.4f, 6, 10, 0.35f, 0.5f, 2.0f, 2.8f, "boss");
                case GameRecipe.SurvivalTimer:
                    return ApplyRecipeEnemyTuning(enemy, 28, 36, 20, 17f, 1.35f, 1.7f, 1, 2, 0.35f, 0.55f, 1.7f, 2.15f, "survival");
                case GameRecipe.ZombieSurvival:
                    return ApplyRecipeEnemyTuning(enemy, 45, 65, 25, 16f, 1.5f, 1.9f, 2, 4, 0.3f, 0.5f, 1.45f, 1.95f, "zombie");
                case GameRecipe.ExplorationQuest:
                    return ApplyRecipeEnemyTuning(enemy, 35, 55, 25, 14f, 1.45f, 1.8f, 2, 3, 0.35f, 0.55f, 1.55f, 2.05f, "exploration");
            }

            return false;
        }

        bool ApplyRecipeEnemyTuning(
            GameObject enemy,
            int minHp,
            int maxHp,
            int healAmount,
            float detectionRange,
            float minStopDistance,
            float maxAttackReach,
            int minDamage,
            int maxDamage,
            float minAttackSpeed,
            float maxAttackSpeed,
            float minAgentSpeed,
            float maxAgentSpeed,
            string label)
        {
            var changed = false;
            var health = enemy.GetComponentInChildren<VWS.EnemyHealth>(true);
            if (health)
            {
                RecordObjectForTuning(health, "VARCO " + label + " enemy health");
                var hp = health.maxHP <= 0 ? minHp : health.maxHP;
                health.maxHP = Mathf.Clamp(hp, minHp, maxHp);
                health.healthDropHealAmount = Mathf.Clamp(health.healthDropHealAmount <= 0 ? healAmount : health.healthDropHealAmount, Mathf.Max(1, healAmount - 10), healAmount + 10);
                EditorUtility.SetDirty(health);
                changed = true;
            }

            var ai = enemy.GetComponentInChildren<VWS.EnemyAI_NavMesh>(true);
            if (ai)
            {
                RecordObjectForTuning(ai, "VARCO " + label + " enemy attack");
                ai.detectionRange = Mathf.Max(ai.detectionRange, detectionRange);
                ai.stopDistance = Mathf.Clamp(ai.stopDistance <= 0f ? minStopDistance : ai.stopDistance, minStopDistance, maxAttackReach);
                ai.attackReach = Mathf.Clamp(ai.attackReach <= 0f ? maxAttackReach : ai.attackReach, minStopDistance, maxAttackReach);
                ai.contactDamage = Mathf.Clamp(ai.contactDamage <= 0 ? minDamage : ai.contactDamage, minDamage, maxDamage);
                ai.attackSpeed = Mathf.Clamp(ai.attackSpeed <= 0f ? maxAttackSpeed : ai.attackSpeed, minAttackSpeed, maxAttackSpeed);
                ai.attackAnimationSpeed = Mathf.Clamp(ai.attackAnimationSpeed <= 0f ? 1f : ai.attackAnimationSpeed, 0.7f, 1.15f);
                ai.contactInterval = 1f / Mathf.Max(0.05f, ai.attackSpeed);
                EditorUtility.SetDirty(ai);
                changed = true;
            }

            var agent = enemy.GetComponentInChildren<NavMeshAgent>(true);
            if (agent)
            {
                RecordObjectForTuning(agent, "VARCO " + label + " enemy navigation");
                agent.speed = Mathf.Clamp(agent.speed <= 0f ? minAgentSpeed : agent.speed, minAgentSpeed, maxAgentSpeed);
                agent.acceleration = Mathf.Clamp(agent.acceleration <= 0f ? 5f : agent.acceleration, 3.5f, 6f);
                agent.angularSpeed = Mathf.Clamp(agent.angularSpeed <= 0f ? 360f : agent.angularSpeed, 240f, 420f);
                agent.stoppingDistance = Mathf.Max(agent.stoppingDistance, minStopDistance);
                EditorUtility.SetDirty(agent);
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(enemy);
            return changed;
        }

        bool ApplyArenaCombatWaveEnemyTuning(GameObject enemy)
        {
            var changed = false;
            var health = enemy.GetComponentInChildren<VWS.EnemyHealth>(true);
            if (health)
            {
                RecordObjectForTuning(health, "VARCO arena enemy health");
                var hp = health.maxHP <= 0 ? 40 : health.maxHP;
                health.maxHP = Mathf.Clamp(hp, 30, 45);
                health.healthDropHealAmount = Mathf.Clamp(health.healthDropHealAmount <= 0 ? 25 : health.healthDropHealAmount, 20, 30);
                EditorUtility.SetDirty(health);
                changed = true;
            }

            var ai = enemy.GetComponentInChildren<VWS.EnemyAI_NavMesh>(true);
            if (ai)
            {
                RecordObjectForTuning(ai, "VARCO arena enemy attack");
                ai.detectionRange = Mathf.Max(ai.detectionRange, 14f);
                ai.stopDistance = Mathf.Clamp(ai.stopDistance <= 0f ? 1.55f : ai.stopDistance, 1.45f, 1.75f);
                ai.attackReach = Mathf.Clamp(ai.attackReach <= 0f ? 1.65f : ai.attackReach, 1.55f, 1.85f);
                ai.contactDamage = Mathf.Clamp(ai.contactDamage <= 0 ? 2 : ai.contactDamage, 1, 2);
                ai.attackSpeed = Mathf.Clamp(ai.attackSpeed <= 0f ? 0.55f : ai.attackSpeed, 0.35f, 0.55f);
                ai.attackAnimationSpeed = Mathf.Clamp(ai.attackAnimationSpeed <= 0f ? 1f : ai.attackAnimationSpeed, 0.7f, 1.2f);
                ai.contactInterval = 1f / Mathf.Max(0.05f, ai.attackSpeed);
                EditorUtility.SetDirty(ai);
                changed = true;
            }

            var agent = enemy.GetComponentInChildren<NavMeshAgent>(true);
            if (agent)
            {
                RecordObjectForTuning(agent, "VARCO arena enemy navigation");
                agent.speed = Mathf.Clamp(agent.speed <= 0f ? 2.1f : agent.speed, 1.8f, 2.2f);
                agent.acceleration = Mathf.Clamp(agent.acceleration <= 0f ? 5f : agent.acceleration, 3.5f, 6f);
                agent.angularSpeed = Mathf.Clamp(agent.angularSpeed <= 0f ? 360f : agent.angularSpeed, 240f, 420f);
                agent.stoppingDistance = Mathf.Max(agent.stoppingDistance, 1.55f);
                EditorUtility.SetDirty(agent);
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(enemy);
            return changed;
        }

        static void RecordObjectForTuning(Object target, string undoName)
        {
            if (!target || EditorUtility.IsPersistent(target))
                return;

            Undo.RecordObject(target, undoName);
        }

        void RemoveWaveManagersForNonEnemyPreset()
        {
            var removed = 0;
            foreach (var wave in FindObjectsByType<VWS.WaveManager>(FindObjectsSortMode.None))
            {
                if (!wave)
                    continue;

                Undo.DestroyObjectImmediate(wave.gameObject);
                removed++;
            }

            if (removed > 0)
                log.Add("Removed leftover wave manager(s) for this non-enemy preset: " + removed + ".");
        }

        VWS.WaveManager CreateWaveManagerRoot()
        {
            var root = GameObject.Find("FB_EnemyWave");
            if (!root)
            {
                root = new GameObject("FB_EnemyWave");
                Undo.RegisterCreatedObjectUndo(root, "웨이브 매니저 생성");
                root.transform.position = Vector3.zero;
            }

            var wave = root.GetComponent<VWS.WaveManager>();
            if (!wave)
                wave = Undo.AddComponent<VWS.WaveManager>(root);
            return wave;
        }

        void EnsureWaveSpawnArea(VWS.WaveManager wave)
        {
            var area = wave.randomSpawnArea;
            if (!area)
            {
                area = wave.GetComponentsInChildren<BoxCollider>(true)
                    .FirstOrDefault(c => c && c.name.IndexOf("SpawnArea", StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (!area)
            {
                var areaGo = new GameObject("SpawnArea");
                Undo.RegisterCreatedObjectUndo(areaGo, "웨이브 생성 영역 생성");
                areaGo.transform.SetParent(wave.transform, false);
                area = Undo.AddComponent<BoxCollider>(areaGo);
            }

            Undo.RecordObject(area, "웨이브 생성 영역 설정");
            area.isTrigger = true;
            area.center = Vector3.zero;
            area.size = WaveSpawnAreaSize();
            var spawnCenter = WaveSpawnAreaCenter();
            if (area.transform.parent == wave.transform)
            {
                area.transform.localPosition = spawnCenter;
                area.transform.localRotation = Quaternion.identity;
                area.transform.localScale = Vector3.one;
            }
            else
            {
                area.transform.position = spawnCenter;
                area.transform.rotation = Quaternion.identity;
                area.transform.localScale = Vector3.one;
            }

            wave.randomSpawnArea = area;
            EditorUtility.SetDirty(area);
        }

        Vector3 WaveSpawnAreaSize()
        {
            if (blockTemplate == BlockTemplate.FullFeatureSandbox)
                return new Vector3(7.5f, 1.2f, 5.5f);

            switch (recipe)
            {
                case GameRecipe.SurvivalTimer:
                    return new Vector3(24f, 1.2f, 24f);
                case GameRecipe.BossBattle:
                    return new Vector3(7f, 1.2f, 4f);
                case GameRecipe.CombatWave:
                    return new Vector3(14f, 1.2f, 6.5f);
                case GameRecipe.ZombieSurvival:
                    return new Vector3(10f, 1.2f, 14f);
                case GameRecipe.ExplorationQuest:
                    return new Vector3(13f, 1.2f, 11f);
            }

            switch (genre)
            {
                case VWS.GenreType.Exploration:
                    return new Vector3(12f, 1.2f, 12f);
                case VWS.GenreType.Platform:
                    return new Vector3(10f, 1.2f, 7f);
                case VWS.GenreType.Puzzle:
                    return new Vector3(8f, 1.2f, 8f);
                default:
                    return new Vector3(14f, 1.2f, 14f);
            }
        }

        Vector3 WaveSpawnAreaCenter()
        {
            if (blockTemplate == BlockTemplate.FullFeatureSandbox)
                return new Vector3(-8f, 0.6f, 5.0f);

            switch (recipe)
            {
                case GameRecipe.SurvivalTimer:
                    return new Vector3(0f, 0.6f, 0f);
                case GameRecipe.BossBattle:
                    return new Vector3(0f, 0.6f, 7f);
                case GameRecipe.CombatWave:
                    return new Vector3(0f, 0.6f, 6.8f);
                case GameRecipe.ZombieSurvival:
                    return new Vector3(4.5f, 0.6f, 4.5f);
                case GameRecipe.ExplorationQuest:
                    return new Vector3(1.4f, 0.6f, 9.4f);
            }

            switch (genre)
            {
                case VWS.GenreType.Exploration:
                    return new Vector3(0f, 0.6f, 4f);
                case VWS.GenreType.Platform:
                    return new Vector3(4f, 1.4f, 0f);
                case VWS.GenreType.Puzzle:
                    return new Vector3(0f, 0.6f, 3f);
                default:
                    return new Vector3(0f, 0.6f, 3f);
            }
        }

        GameObject FindOrCreateWaveEnemyPrefab()
        {
            var wave = FindFirstObjectByType<VWS.WaveManager>();
            GameObject existingWavePrefab = null;
            if (wave && wave.waves != null)
            {
                foreach (var data in wave.waves)
                {
                    if (data == null || !data.enemyPrefab || !data.enemyPrefab.GetComponentInChildren<VWS.EnemyHealth>(true))
                        continue;

                    if (EnemyPrefabMatchesSelection(data.enemyPrefab))
                        return data.enemyPrefab;
                    if (!existingWavePrefab)
                        existingWavePrefab = data.enemyPrefab;
                }
            }

            GameObject sceneEnemyFallback = null;
            var sceneEnemy = FindFirstObjectByType<VWS.EnemyHealth>();
            if (sceneEnemy)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(sceneEnemy.gameObject);
                if (source && source.GetComponentInChildren<VWS.EnemyHealth>(true))
                {
                    if (EnemyPrefabMatchesSelection(source))
                        return source;
                    sceneEnemyFallback = source;
                }

                if (!sceneEnemyFallback && EnemyPrefabMatchesSelection(sceneEnemy.gameObject))
                {
                    EnsureFolder(AutoConnectedPrefabFolder);
                    var path = AutoConnectedPrefabFolder + "/" + SafeFileName(sceneEnemy.gameObject.name) + ".prefab";
                    var prefab = PrefabUtility.SaveAsPrefabAsset(sceneEnemy.gameObject, path);
                    if (prefab)
                    {
                        log.Add("웨이브 적 프리팹 저장됨: " + path);
                        return prefab;
                    }
                }

                if (!sceneEnemyFallback)
                {
                    EnsureFolder(AutoConnectedPrefabFolder);
                    var fallbackPath = AutoConnectedPrefabFolder + "/" + SafeFileName(sceneEnemy.gameObject.name) + ".prefab";
                    sceneEnemyFallback = PrefabUtility.SaveAsPrefabAsset(sceneEnemy.gameObject, fallbackPath);
                    if (sceneEnemyFallback)
                        log.Add("웨이브 적 프리팹 저장됨: " + fallbackPath);
                }
            }

            var connectedPrefabs = FindConnectedEnemyPrefabs().ToList();
            foreach (var prefab in connectedPrefabs)
            {
                if (EnemyPrefabMatchesSelection(prefab))
                    return prefab;
            }

            foreach (var prefab in connectedPrefabs)
                return prefab;

            return sceneEnemyFallback ? sceneEnemyFallback : existingWavePrefab;
        }

        IEnumerable<GameObject> FindConnectedEnemyPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(AutoConnectedPrefabFolder))
                yield break;

            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { AutoConnectedPrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab && prefab.GetComponentInChildren<VWS.EnemyHealth>(true))
                    yield return prefab;
            }
        }

        bool EnemyPrefabMatchesSelection(GameObject prefab)
        {
            if (!prefab || !prefab.GetComponentInChildren<VWS.EnemyHealth>(true))
                return false;

            var desired = EnemyChoiceToCharacterKind();
            var targetKind = desired.HasValue ? desired.Value : PreferredCharacterKind(AssetRole.Enemy, genre);
            if (targetKind == CharacterKind.None)
                return true;

            var path = AssetDatabase.GetAssetPath(prefab);
            var internalEvidenceText = BuildAssetInternalEvidenceText(prefab, out _);
            var text = Normalize(path + " " + prefab.name + " " + internalEvidenceText);
            return GuessCharacterKind(text) == targetKind;
        }

        void ApplyVisualSetup()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = genre == VWS.GenreType.Platform ? 0.006f : 0.018f;
            RenderSettings.fogColor = GenreFogColor();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = GenreAmbientSky();
            RenderSettings.ambientEquatorColor = GenreAmbientEquator();
            RenderSettings.ambientGroundColor = new Color(0.03f, 0.035f, 0.04f);

            EnsureVolumeProfile();
            EnsureReflectionProbe();
            EnsureNavMeshSurfaceHint();
            log.Add("비주얼 프리셋을 적용했습니다.");
        }

        void EnsureVolumeProfile()
        {
            EnsureFolder("Assets/ScriptableObjects");
            EnsureFolder(VisualProfileFolder);
            var path = VisualProfileFolder + "/VARCO_" + genre + "_VolumeProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile)
            {
                profile = CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            if (!profile.TryGet(out Bloom bloom))
                bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(genre == VWS.GenreType.Platform ? 0.55f : 0.35f);
            bloom.threshold.Override(1.1f);

            if (!profile.TryGet(out ColorAdjustments color))
                color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(genre == VWS.GenreType.Puzzle ? 18f : 10f);
            color.saturation.Override(genre == VWS.GenreType.Platform ? 4f : -2f);
            color.postExposure.Override(0.05f);

            if (!profile.TryGet(out Vignette vignette))
                vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(genre == VWS.GenreType.Platform ? 0.18f : 0.25f);
            vignette.smoothness.Override(0.55f);

            if (!profile.TryGet(out Tonemapping tone))
                tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            EditorUtility.SetDirty(profile);

            var volumeGo = GameObject.Find("VARCO_GlobalVolume") ?? new GameObject("VARCO_GlobalVolume");
            Undo.RegisterCompleteObjectUndo(volumeGo, "VARCO 비주얼 볼륨 설정");
            var volume = volumeGo.GetComponent<Volume>();
            if (!volume)
                volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 1f;
            volume.profile = profile;
            EditorUtility.SetDirty(volumeGo);
        }

        void EnsureReflectionProbe()
        {
            if (FindFirstObjectByType<ReflectionProbe>())
                return;

            var go = new GameObject("VARCO_ReflectionProbe");
            Undo.RegisterCreatedObjectUndo(go, "반사 프로브 생성");
            go.transform.position = new Vector3(0f, 3f, 0f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.size = new Vector3(28f, 12f, 28f);
            probe.intensity = 0.55f;
        }

        void EnsureNavMeshSurfaceHint()
        {
            if (genre != VWS.GenreType.Arena && genre != VWS.GenreType.Exploration && genre != VWS.GenreType.Puzzle)
                return;

            var type = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (type == null || FindFirstObjectByType(type))
                return;

            var go = new GameObject("VARCO_NavMeshSurface_BakeMe");
            Undo.RegisterCreatedObjectUndo(go, "내비게이션 표면 생성");
            go.AddComponent(type);
            log.Add("적 이동용 내비게이션 표면을 생성했습니다. 적이 움직이지 않으면 내비게이션 창에서 굽기를 실행하세요.");
        }

        void EnsurePlayableNavMesh()
        {
            if (genre != VWS.GenreType.Arena && genre != VWS.GenreType.Exploration && genre != VWS.GenreType.Puzzle)
                return;

            if (!FindFirstObjectByType<VWS.EnemyAI_NavMesh>() && !FindFirstObjectByType<VWS.WaveManager>())
                return;

            var surfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (surfaceType == null)
            {
                DisableWaveSpawningWithoutNavMesh("NavMeshSurface 패키지를 찾지 못해 적 웨이브를 보호 모드로 전환했습니다.");
                return;
            }

            var surface = FindFirstObjectByType(surfaceType) as Component;
            if (surface == null)
            {
                var go = new GameObject("VARCO_NavMeshSurface_AutoBuild");
                Undo.RegisterCreatedObjectUndo(go, "VARCO NavMeshSurface 생성");
                surface = go.AddComponent(surfaceType);
            }

            ConfigureNavMeshSurface(surface);
            var buildMethod = surfaceType.GetMethod("BuildNavMesh", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (buildMethod == null)
            {
                DisableWaveSpawningWithoutNavMesh("NavMeshSurface 베이크 함수를 찾지 못해 적 웨이브를 보호 모드로 전환했습니다.");
                return;
            }

            try
            {
                buildMethod.Invoke(surface, null);
                EditorUtility.SetDirty((Object)surface);
            }
            catch (Exception ex)
            {
                DisableWaveSpawningWithoutNavMesh("내비메시 자동 베이크 실패: " + ex.GetBaseException().Message);
                return;
            }

            var triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices == null || triangulation.vertices.Length == 0)
            {
                DisableWaveSpawningWithoutNavMesh("내비메시가 생성되지 않아 적 웨이브를 보호 모드로 전환했습니다.");
                return;
            }

            foreach (var agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            {
                ConfigureNavMeshAligner(agent);

                if (!agent || !agent.enabled || agent.isOnNavMesh)
                    continue;

                if (NavMesh.SamplePosition(agent.transform.position, out var hit, 20f, NavMesh.AllAreas))
                {
                    var wasEnabled = agent.enabled;
                    agent.enabled = false;
                    agent.transform.position = hit.position;
                    agent.enabled = wasEnabled;
                    agent.Warp(hit.position);
                    EditorUtility.SetDirty(agent);
                }
            }

            log.Add("내비메시 자동 베이크 완료: 정점 " + triangulation.vertices.Length + "개");
        }

        void ConfigureNavMeshAligner(NavMeshAgent agent)
        {
            if (!agent)
                return;

            var align = agent.GetComponent<VWS.NavMeshEditPlayAlign>();
            if (!align)
                align = Undo.AddComponent<VWS.NavMeshEditPlayAlign>(agent.gameObject);

            Undo.RecordObject(align, "VARCO enemy NavMesh aligner");
            align.sampleMaxDistance = Mathf.Max(align.sampleMaxDistance, 20f);
            align.alignInPlayMode = true;
            align.alignInEditMode = false;
            EditorUtility.SetDirty(align);
        }

        void ConfigureNavMeshSurface(Component surface)
        {
            if (!surface)
                return;

            Undo.RecordObject(surface, "VARCO NavMeshSurface 설정");
            SetFieldOrProperty(surface, "m_CollectObjects", 0);
            SetFieldOrProperty(surface, "m_UseGeometry", 0);
            SetFieldOrProperty(surface, "m_IgnoreNavMeshAgent", true);
            SetFieldOrProperty(surface, "m_IgnoreNavMeshObstacle", true);
            EditorUtility.SetDirty(surface);
        }

        void DisableWaveSpawningWithoutNavMesh(string reason)
        {
            foreach (var wave in FindObjectsByType<VWS.WaveManager>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(wave, "VARCO Wave NavMesh Guard");
                wave.disableWhenNavMeshMissing = true;
                EditorUtility.SetDirty(wave);
            }

            log.Add(reason);
        }

        static void SetFieldOrProperty(object target, string name, object value)
        {
            if (target == null)
                return;

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

        VWS.SoundEventRegistry SyncAudioRegistry()
        {
            var registry = EnsureRegistry();
            var clips = AssetDatabase.FindAssets("t:AudioClip", AudioScanRoots())
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Select(path => new { path, clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path) })
                .Where(item => item.clip)
                .ToList();

            Undo.RecordObject(registry, "VARCO 사운드 목록 동기화");
            foreach (var item in clips)
                AddOrUpdateSound(registry, BuildSoundId(item.path), item.clip, DefaultVolumeForPath(item.path));

            foreach (var slot in CanonicalSoundSlots())
                AddCanonicalSound(registry, slot.id, FindAudioClip(slot.primary, slot.genre, slot.keywords), slot.volume);

            EditorUtility.SetDirty(registry);
            log.Add("사운드 이벤트 목록 동기화됨: " + clips.Count + "개 클립");
            return registry;
        }

        VWS.SoundEventRegistry EnsureRegistry()
        {
            EnsureFolder("Assets/ScriptableObjects");
            EnsureFolder("Assets/ScriptableObjects/SoundEvents");
            var registry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(RegistryPath);
            if (registry)
                return registry;

            registry = CreateInstance<VWS.SoundEventRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
            log.Add("사운드 이벤트 목록을 생성했습니다.");
            return registry;
        }

        static void AddCanonicalSound(VWS.SoundEventRegistry registry, string id, AudioClip clip, float volume)
        {
            if (!registry || !clip)
                return;
            AddOrUpdateSound(registry, id, clip, volume);
        }

        static void AddOrUpdateSound(VWS.SoundEventRegistry registry, string id, AudioClip clip, float volume)
        {
            if (!registry || string.IsNullOrWhiteSpace(id) || !clip)
                return;

            var entry = registry.events.FirstOrDefault(e => e != null && e.id == id);
            if (entry == null)
            {
                entry = new VWS.SoundEventRegistry.Entry { id = id };
                registry.events.Add(entry);
            }

            entry.clip = clip;
            entry.volume = Mathf.Clamp01(volume);
        }

        void ApplySoundBindings(VWS.SoundEventRegistry registry)
        {
            if (!registry)
                return;

            foreach (var player in FindObjectsByType<VWS.PlayerHealth>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(player, "플레이어 사운드 연결");
                player.soundRegistry = registry;
                player.fallbackHitSound = GetClip(registry, "sfx_player_hit");
                player.fallbackDeathSound = GetClip(registry, "sfx_game_over");
                EnsureAudioAndEmitter(player.gameObject, registry);
                EditorUtility.SetDirty(player);
            }

            foreach (var attack in FindObjectsByType<VWS.PlayerAttack>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(attack, "공격 사운드 연결");
                attack.soundRegistry = registry;
                attack.fallbackAttackSound = GetClip(registry, "sfx_player_attack");
                EnsureAudioAndEmitter(attack.gameObject, registry);
                EditorUtility.SetDirty(attack);
            }

            foreach (var footstep in FindObjectsByType<VWS.PlayerFootstepSound>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(footstep, "발소리 연결");
                footstep.soundRegistry = registry;
                footstep.fallbackFootstepSound = GetClip(registry, "sfx_player_footstep");
                EnsureAudioAndEmitter(footstep.gameObject, registry);
                EditorUtility.SetDirty(footstep);
            }

            foreach (var enemy in FindObjectsByType<VWS.EnemyHealth>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(enemy, "적 사운드 연결");
                enemy.soundRegistry = registry;
                enemy.fallbackHitSound = GetClip(registry, "sfx_enemy_hit");
                enemy.fallbackDeathSound = GetClip(registry, "sfx_enemy_death");
                EnsureAudioAndEmitter(enemy.gameObject, registry);
                EditorUtility.SetDirty(enemy);
            }

            foreach (var enemyAi in FindObjectsByType<VWS.EnemyAI_NavMesh>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(enemyAi, "적 공격 사운드 연결");
                enemyAi.soundRegistry = registry;
                enemyAi.fallbackAttackSound = GetClip(registry, "sfx_enemy_attack");
                EnsureAudioAndEmitter(enemyAi.gameObject, registry);
                EditorUtility.SetDirty(enemyAi);
            }

            foreach (var item in FindObjectsByType<VWS.ItemPickup>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(item, "아이템 사운드 연결");
                item.pickupClip = GetClip(registry, "sfx_collect_item");
                EditorUtility.SetDirty(item);
            }

            foreach (var health in FindObjectsByType<VWS.HealthPickup>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(health, "회복 사운드 연결");
                health.pickupClip = GetClip(registry, "sfx_pickup_health") ?? GetClip(registry, "sfx_collect_item");
                EditorUtility.SetDirty(health);
            }

            foreach (var goal in FindObjectsByType<VWS.GoalTrigger>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(goal, "목표 사운드 연결");
                goal.clearClip = GetClip(registry, "sfx_clear");
                EditorUtility.SetDirty(goal);
            }

            foreach (var checkpoint in FindObjectsByType<VWS.Checkpoint>(FindObjectsSortMode.None))
                EnsureTriggerSound(checkpoint.gameObject, registry, "sfx_checkpoint", true);

            foreach (var plate in FindObjectsByType<VWS.PressurePlate>(FindObjectsSortMode.None))
                EnsureTriggerSound(plate.gameObject, registry, "sfx_door_open", false);

            EnsureSceneBgm(registry);
            log.Add("사운드/BGM 연결을 적용했습니다.");
        }

        void EnsureSceneBgm(VWS.SoundEventRegistry registry)
        {
            var id = BgmIdForGenre(genre);
            if (!registry.TryGet(id, out var clip, out var volume) || !clip)
                return;

            var go = GameObject.Find("VW_Audio_BGM") ?? new GameObject("VW_Audio_BGM");
            Undo.RegisterCompleteObjectUndo(go, "배경 음악 설정");
            var source = go.GetComponent<AudioSource>();
            if (!source)
                source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = true;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = Mathf.Clamp01(volume);
            EditorUtility.SetDirty(go);
        }

        static AudioClip GetClip(VWS.SoundEventRegistry registry, string id)
        {
            return registry && registry.TryGet(id, out var clip, out _) ? clip : null;
        }

        static void EnsureAudioAndEmitter(GameObject go, VWS.SoundEventRegistry registry)
        {
            if (!go.GetComponent<AudioSource>())
                Undo.AddComponent<AudioSource>(go);
            var emitter = go.GetComponent<VWS.SoundEventEmitter>();
            if (!emitter)
                emitter = Undo.AddComponent<VWS.SoundEventEmitter>(go);
            emitter.registry = registry;
            EditorUtility.SetDirty(emitter);
        }

        static void EnsureTriggerSound(GameObject go, VWS.SoundEventRegistry registry, string id, bool playOnce)
        {
            if (!go)
                return;
            var trigger = go.GetComponent<VWS.SoundEventTrigger>();
            if (!trigger)
                trigger = Undo.AddComponent<VWS.SoundEventTrigger>(go);
            trigger.registry = registry;
            trigger.eventId = id;
            trigger.triggerMode = VWS.SoundTriggerMode.OnTriggerEnter;
            trigger.onlyPlayer = true;
            trigger.playOnce = playOnce;
            trigger.fallbackClip = GetClip(registry, id);
            if (!go.GetComponent<AudioSource>())
                Undo.AddComponent<AudioSource>(go);
            EditorUtility.SetDirty(trigger);
        }

        AudioClip FindAudioClip(params string[] keywords)
        {
            return FindAudioClip(null, null, keywords);
        }

        AudioClip FindAudioClip(string primary, VWS.GenreType? targetGenre, params string[] keywords)
        {
            var match = FindAudioMatch(primary, targetGenre, keywords);
            return match != null ? match.clip : null;
        }

        IEnumerable<SoundSlotDefinition> CanonicalSoundSlots()
        {
            var slots = new Dictionary<string, SoundSlotDefinition>(StringComparer.OrdinalIgnoreCase);
            AddSoundSlot(slots, BgmIdForGenre(genre), GenreLabel(genre) + " BGM", "bgm", 0.45f, true, "music", "loop", genre.ToString());

            if (blockPlayer || blockWeapon)
                AddSoundSlot(slots, "sfx_player_attack", "플레이어 공격", "attack", 1f, true, "player", "weapon", "sword", "swing", "slash", "shoot");
            if (blockPlayer)
            {
                AddSoundSlot(slots, "sfx_player_footstep", "플레이어 발소리", "footstep", 0.75f, false, "step", "walk", "run", "player");
                AddSoundSlot(slots, "sfx_player_hit", "플레이어 피격", "hit", 1f, true, "damage", "hurt", "player");
                AddSoundSlot(slots, "sfx_game_over", "게임 오버", "game", 1f, true, "over", "fail", "death");
            }
            if (blockEnemyWave)
            {
                AddSoundSlot(slots, "sfx_enemy_attack", "적 공격", "attack", 1f, true, "enemy", "zombie", "boss", "orc");
                AddSoundSlot(slots, "sfx_enemy_hit", "적 피격", "hit", 1f, true, "enemy", "damage", "hurt");
                AddSoundSlot(slots, "sfx_enemy_death", "적 사망", "death", 1f, true, "enemy", "zombie", "boss", "die");
            }
            if (blockItems)
                AddSoundSlot(slots, "sfx_collect_item", "아이템 획득", "collect", 1f, true, "item", "pickup", "coin");
            if (blockHealthPickup)
                AddSoundSlot(slots, "sfx_pickup_health", "회복 아이템", "health", 1f, true, "potion", "pickup", "heal", "collect", "item");
            if (blockCheckpoint)
                AddSoundSlot(slots, "sfx_checkpoint", "체크포인트", "checkpoint", 1f, true, "save", "respawn");
            if (blockGoal)
                AddSoundSlot(slots, "sfx_clear", "클리어", "clear", 1f, true, "success", "goal", "win");
            if (blockPuzzleDoor)
                AddSoundSlot(slots, "sfx_door_open", "문 열림", "door", 1f, true, "open", "gate", "pressureplate");

            return slots.Values.ToList();
        }

        void AddSoundSlot(Dictionary<string, SoundSlotDefinition> slots, string id, string label, string primary, float volume, bool important, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            slots[id] = new SoundSlotDefinition
            {
                id = id,
                label = label,
                primary = primary,
                keywords = keywords ?? Array.Empty<string>(),
                volume = volume,
                important = important,
                genre = genre
            };
        }

        IEnumerable<SoundSlotStatus> BuildSoundSlotStatuses(VWS.SoundEventRegistry registry)
        {
            foreach (var slot in CanonicalSoundSlots())
                yield return BuildSoundSlotStatus(slot, registry);
        }

        SoundSlotStatus BuildSoundSlotStatus(SoundSlotDefinition definition, VWS.SoundEventRegistry registry)
        {
            AudioClip registryClip = null;
            var fromRegistry = registry && registry.TryGet(definition.id, out registryClip, out _) && registryClip;
            if (fromRegistry)
            {
                return new SoundSlotStatus
                {
                    definition = definition,
                    clip = registryClip,
                    clipPath = AssetDatabase.GetAssetPath(registryClip),
                    state = "PASS",
                    reason = "사운드 이벤트 레지스트리에 연결됨",
                    fromRegistry = true
                };
            }

            var match = FindAudioMatch(definition.primary, definition.genre, definition.keywords);
            if (match != null && match.clip)
            {
                return new SoundSlotStatus
                {
                    definition = definition,
                    clip = match.clip,
                    clipPath = match.path,
                    state = "PASS",
                    reason = "자동 후보: " + match.reason,
                    score = match.score,
                    fromRegistry = false
                };
            }

            return new SoundSlotStatus
            {
                definition = definition,
                state = definition.important ? "WARN" : "WARN",
                reason = "스캔 범위에서 후보를 찾지 못했습니다. 권장 파일명: " + definition.id + ".wav"
            };
        }

        string SoundSlotMessage(SoundSlotStatus status)
        {
            if (status.clip)
            {
                var prefix = status.fromRegistry ? "레지스트리 연결" : "자동 후보";
                var path = string.IsNullOrWhiteSpace(status.clipPath) ? "" : " / " + status.clipPath;
                return prefix + ": " + status.clip.name + path + " / " + status.reason;
            }

            return "누락: " + status.reason;
        }

        AudioCandidateMatch FindAudioMatch(string primary, VWS.GenreType? targetGenre, params string[] keywords)
        {
            var allKeywords = new List<string>();
            if (!string.IsNullOrWhiteSpace(primary))
                allKeywords.Add(primary);
            if (keywords != null)
                allKeywords.AddRange(keywords.Where(k => !string.IsNullOrWhiteSpace(k)));

            var bestScore = int.MinValue;
            AudioCandidateMatch best = null;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", AudioScanRoots()))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (!clip)
                    continue;

                var text = Normalize(path + " " + clip.name);
                var score = 0;
                var reasons = new List<string>();
                if (targetGenre.HasValue && GuessGenreFromText(text) == targetGenre.Value)
                {
                    score += 30;
                    reasons.Add("장르 일치");
                }
                if (text.Contains("/bgm/") || text.Contains("bgm"))
                {
                    if (string.Equals(primary, "bgm", StringComparison.OrdinalIgnoreCase))
                    {
                        score += 25;
                        reasons.Add("BGM 이름/폴더");
                    }
                    else
                    {
                        score -= 10;
                    }
                }
                foreach (var keyword in allKeywords)
                {
                    var normalized = Normalize(keyword);
                    if (!string.IsNullOrEmpty(normalized) && text.Contains(normalized))
                    {
                        score += 12;
                        reasons.Add("키워드 " + keyword);
                    }
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = new AudioCandidateMatch
                    {
                        clip = clip,
                        path = path,
                        score = score,
                        reason = (reasons.Count > 0 ? string.Join(", ", reasons) : "키워드 약함") + " / 점수 " + score
                    };
                }
            }

            return bestScore > 0 ? best : null;
        }

        void AppendValidationReport(VWS.SoundEventRegistry registry)
        {
            log.Add("검증 리포트:");
            log.Add("정보: 프리셋 " + PresetLabel());
            log.Add("정보: 설계 기준 " + DesignSourceLabel());
            AddValidationLine("게임 매니저", FindFirstObjectByType<VWS.GameManager>());
            AddValidationLine("플레이어", GameObject.FindGameObjectWithTag("Player"));
            if (blockPlayer)
                AddPlayerCharacterValidation();
            if (blockWeapon)
                AddWeaponValidation();
            AddValidationLine("메인 카메라", Camera.main);
            AddValidationLine("VARCO 게임 HUD", FindFirstObjectByType<VWS.VARCOGameHUD>());
            log.Add(StateLabel("PASS") + ": 난이도 " + DifficultyLabel()
                + " HP=" + PlayerMaxHpForDifficulty()
                + " 회복=" + HealAmountForDifficulty()
                + " 위험DPS=" + HazardDpsForDifficulty());
            log.Add(StateLabel("PASS") + ": 카메라 " + CameraPresetLabel());
            log.Add(StateLabel("PASS") + ": 플레이어 이동 " + PlayerMovementLabel());
            if (blockPlayer && autoAnimations)
                AppendAnimationValidationLog();

            if (blockEnemyWave)
            {
                AddValidationLine("적 AI", FindFirstObjectByType<VWS.EnemyAI_NavMesh>());
                AddWaveValidation();
            }

            if (blockItems)
                AddValidationCount("수집 아이템", FindObjectsByType<VWS.ItemPickup>(FindObjectsSortMode.None).Length, Mathf.Max(1, itemGoal));
            if (blockGoal)
                AddValidationLine("목표 트리거", FindFirstObjectByType<VWS.GoalTrigger>());
            if (blockHealthPickup)
                AddValidationLine("회복 아이템", FindFirstObjectByType<VWS.HealthPickup>());
            if (blockHazard)
                AddValidationLine("위험 구역", FindFirstObjectByType<VWS.HazardZone>());
            if (blockCheckpoint)
                AddValidationLine("체크포인트", FindFirstObjectByType<VWS.Checkpoint>());
            if (blockFallRespawn)
                AddValidationLine("낙사 리스폰 안전망", FindFirstObjectByType<VWS.DeathZone>());
            if (blockMovingPlatform)
                AddValidationLine("이동 발판", FindFirstObjectByType<VWS.MovingPlatform>());
            if (blockPuzzleDoor)
            {
                AddValidationLine("문 컨트롤러", FindFirstObjectByType<VWS.DoorController>());
                AddValidationLine("압력판", FindFirstObjectByType<VWS.PressurePlate>());
            }
            if (blockMovableBox)
                AddValidationLine("밀 수 있는 상자", FindFirstObjectByType<VWS.MovableBox>());
            if (blockCountdown)
                AddValidationLine("제한시간 타이머", FindFirstObjectByType<VWS.CountdownTimer>());
            if (blockVisuals)
                AddValidationLine("글로벌 볼륨", FindFirstObjectByType<Volume>());
            if (blockSound)
            {
                AddValidationLine("사운드 이벤트 목록", registry);
                AddValidationLine("BGM 오디오 소스", GameObject.Find("VW_Audio_BGM"));
                AppendSoundValidationLog(registry);
            }
            AppendPlayReadyValidationLog();

            var activePath = SceneManager.GetActiveScene().path;
            var inBuild = !string.IsNullOrWhiteSpace(activePath) && EditorBuildSettings.scenes.Any(s => s.enabled && s.path == activePath);
            log.Add(CheckLabel(inBuild) + ": 현재 씬 빌드 설정 포함");

            foreach (var role in ActiveRolesForCurrentBlocks())
            {
                var slot = BuildAssetSlotStatus(role);
                log.Add(StateLabel(slot.state) + ": " + AssetRoleLabel(role) + " -> " + slot.message);
            }
        }

        void AppendAnimationValidationLog()
        {
            var statuses = BuildAnimationSlotStatuses().ToList();
            if (statuses.Count == 0)
                return;

            var readyCount = statuses.Count(status => status.state == "PASS");
            var requiredCount = statuses.Count(status => status.definition.important);
            var readyRequiredCount = statuses.Count(status => status.definition.important && status.state == "PASS");
            log.Add(StateLabel(readyRequiredCount >= requiredCount ? "PASS" : "WARN")
                + ": 애니메이션 슬롯 " + readyCount + "/" + statuses.Count
                + " 준비 / 필수 " + readyRequiredCount + "/" + requiredCount);

            foreach (var status in statuses.Where(item => item.state != "PASS").Take(5))
                log.Add(StateLabel(status.state) + ": " + AnimationSlotLabel(status.definition) + " - " + status.reason);
        }

        void AppendSoundValidationLog(VWS.SoundEventRegistry registry)
        {
            var statuses = BuildSoundSlotStatuses(registry).ToList();
            if (statuses.Count == 0)
                return;

            var connectedCount = statuses.Count(status => status.state == "PASS");
            log.Add(StateLabel(connectedCount == statuses.Count ? "PASS" : "WARN")
                + ": 사운드/BGM 슬롯 " + connectedCount + "/" + statuses.Count + " 연결");

            foreach (var status in statuses.Where(item => item.state != "PASS").Take(5))
                log.Add(StateLabel(status.state) + ": " + status.definition.label + " - " + status.reason);
        }

        void AppendPlayReadyValidationLog()
        {
            foreach (var finding in BuildPlayReadyChecklist())
                log.Add(StateLabel(finding.state) + ": " + finding.area + " - " + finding.message);
        }

        void AddValidationLine(string label, Object obj)
        {
            log.Add(CheckLabel(obj) + ": " + label);
        }

        void AddValidationLine(string label, GameObject obj)
        {
            log.Add(CheckLabel(obj) + ": " + label);
        }

        void AddValidationCount(string label, int count, int expected)
        {
            log.Add(CheckLabel(count >= expected) + ": " + label + " " + count + "/" + expected);
        }

        void AddPlayerCharacterValidation()
        {
            var best = FindBest(AssetRole.Player, genre);
            if (best == null)
            {
                log.Add(StateLabel("WARN") + ": 플레이어 캐릭터 " + PlayerCharacterLabel() + " -> 기본 오브젝트 생성");
                return;
            }

            var matches = PlayerCandidateMatchesSelection(best);
            log.Add(CheckLabel(matches) + ": 플레이어 캐릭터 " + PlayerCharacterLabel() + " -> " + BuildAssetLabel(best));
        }

        static Transform FindEquippedWeapon(GameObject player)
        {
            if (!player)
                return null;

            return player.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t && t.name == "VARCO_EquippedWeapon");
        }

        void AddWeaponValidation()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var equipped = FindEquippedWeapon(player);
            log.Add(CheckLabel(equipped) + ": 무기 장착");
        }

        void AddWaveValidation()
        {
            var wave = FindFirstObjectByType<VWS.WaveManager>();
            AddValidationLine("웨이브 매니저", wave);
            if (!wave)
                return;

            var waveCount = wave.waves != null ? wave.waves.Length : 0;
            AddValidationCount("웨이브 항목", waveCount, 1);
            AddValidationLine("웨이브 스폰 영역", wave.randomSpawnArea);

            var totalEnemies = 0;
            var missingPrefab = false;
            var mismatchedPrefab = false;
            if (wave.waves != null)
            {
                foreach (var data in wave.waves)
                {
                    if (data == null)
                    {
                        missingPrefab = true;
                        continue;
                    }

                    totalEnemies += Mathf.Max(0, data.enemyCount);
                    if (!data.enemyPrefab || !data.enemyPrefab.GetComponentInChildren<VWS.EnemyHealth>(true))
                        missingPrefab = true;
                    else if (!EnemyPrefabMatchesSelection(data.enemyPrefab))
                        mismatchedPrefab = true;
                }
            }

            log.Add(CheckLabel(!missingPrefab) + ": 웨이브 적 프리팹");
            log.Add(CheckLabel(!mismatchedPrefab) + ": 웨이브 적 캐릭터 " + EnemyCharacterLabel());
            AddValidationCount("웨이브 적 총합", totalEnemies, EffectiveWaveEnemyCount());

            var shouldClear = PrimaryClearCondition() == VWS.CompletionCondition.DefeatWaves;
            log.Add(CheckLabel(wave.clearWhenAllWavesCleared == shouldClear)
                + ": 웨이브 클리어 규칙 " + BoolLabel(wave.clearWhenAllWavesCleared) + " / 기대값 " + BoolLabel(shouldClear));
        }

        bool AddActiveSceneToBuildSettings()
        {
            var path = SceneManager.GetActiveScene().path;
            if (string.IsNullOrWhiteSpace(path))
            {
                log.Add("현재 씬을 빌드 설정에 넣으려면 먼저 씬 저장이 필요합니다.");
                return false;
            }

            if (useOnlyActiveSceneForWindowsBuild)
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
                log.Add("Windows 빌드 설정을 현재 씬 1개로 정리했습니다: " + path);
                return true;
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            var index = scenes.FindIndex(s => s.path == path);
            if (index >= 0)
            {
                scenes[index] = new EditorBuildSettingsScene(path, true);
                log.Add("현재 씬은 이미 빌드 설정에 포함되어 있습니다.");
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
                log.Add("현재 씬을 빌드 설정에 포함했습니다.");
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            return true;
        }

        bool PrepareActiveSceneForBuild()
        {
            EnsureSceneCanBeSaved();

            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrWhiteSpace(scene.path))
            {
                log.Add("빌드 준비 실패: 현재 씬을 자동 저장하지 못했습니다.");
                return false;
            }

            if (!AddActiveSceneToBuildSettings())
                return false;

            if (scene.isDirty)
                return SaveActiveScene();

            log.Add("빌드용 씬 저장 상태를 확인했습니다.");
            AssetDatabase.SaveAssets();
            return true;
        }

        void BuildWindowsExe()
        {
            FixAllCurrentScene();
            if (!PrepareActiveSceneForBuild())
            {
                SaveOneClickReport("BuildPreflight");
                EditorUtility.DisplayDialog("VARCO 게임 메이커", "Windows 빌드를 위한 씬 저장 또는 빌드 설정 자동 준비에 실패했습니다. 생성된 리포트를 확인하세요.", "확인");
                return;
            }

            var scenePath = SceneManager.GetActiveScene().path;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                EditorUtility.DisplayDialog("VARCO 게임 메이커", "빌드하기 전에 씬을 저장하세요.", "확인");
                return;
            }

            var preflight = BuildWindowsPreflightFindings(scenePath);
            AppendBuildPreflightLog(preflight);
            if (preflight.Any(finding => finding.state == "FAIL"))
            {
                log.Add("Windows 빌드 전 점검 실패. 실패 항목을 먼저 해결해야 합니다.");
                SaveOneClickReport("BuildPreflight");
                EditorUtility.DisplayDialog("VARCO 게임 메이커", "Windows 빌드 전 점검에서 실패 항목이 있습니다. 생성된 리포트를 확인하세요.", "확인");
                return;
            }

            Directory.CreateDirectory(BuildRoot);
            var outputDir = Path.Combine(BuildRoot, "VARCO_" + genre);
            Directory.CreateDirectory(outputDir);
            var outputPath = WindowsBuildOutputPath();

            var buildScenes = WindowsBuildScenePaths(scenePath);
            log.Add("실제 Windows 빌드 대상 씬: " + string.Join(", ", buildScenes));
            var report = BuildPipeline.BuildPlayer(buildScenes, outputPath, BuildTarget.StandaloneWindows64, BuildOptions.None);
            log.Add("빌드 결과: " + report.summary.result + " -> " + outputPath.Replace("\\", "/"));
            log.Add("빌드 요약: 에러 " + report.summary.totalErrors
                + " / 경고 " + report.summary.totalWarnings
                + " / 크기 " + FormatBytes(report.summary.totalSize));
            if (report.summary.result == BuildResult.Succeeded)
                log.Add("Windows EXE 빌드 완료. 폴더: " + outputDir.Replace("\\", "/"));
            else
                log.Add("Windows EXE 빌드가 완료되지 않았습니다. Unity Console과 리포트를 확인하세요.");
            SaveOneClickReport("BuildWindows");
        }

        void GenerateBuildPreflightReport()
        {
            log.Clear();
            log.Add("Windows 빌드 전 점검 리포트를 생성했습니다. 실제 빌드는 실행하지 않았습니다.");
            AppendBuildPreflightLog(BuildWindowsReadinessFindings());
            SaveOneClickReport("BuildPreflight");
        }

        List<AcceptanceFinding> BuildWindowsReadinessFindings()
        {
            var scene = SceneManager.GetActiveScene();
            var scenePath = scene.path;
            var findings = new List<AcceptanceFinding>();
            var acceptance = BuildAcceptanceChecklist();
            var acceptanceFailCount = acceptance.Count(finding => finding.state == "FAIL");
            var acceptanceWarnCount = acceptance.Count(finding => finding.state == "WARN");
            var currentSceneInBuild = !string.IsNullOrWhiteSpace(scenePath)
                && EditorBuildSettings.scenes.Any(s => s.enabled && s.path == scenePath);
            var outputPath = WindowsBuildOutputPath();
            var extraEnabledScenes = EnabledBuildScenePaths()
                .Where(path => !string.Equals(path, scenePath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            AddAcceptanceFinding(findings, EditorApplication.isPlayingOrWillChangePlaymode ? "FAIL" : "PASS", "Unity 상태",
                EditorApplication.isPlayingOrWillChangePlaymode ? "Play 모드가 켜져 있거나 전환 중입니다. 빌드 전에 Play 모드를 끄세요." : "Play 모드가 꺼져 있습니다.");
            AddAcceptanceFinding(findings, EditorApplication.isCompiling ? "FAIL" : "PASS", "Unity 상태",
                EditorApplication.isCompiling ? "스크립트 컴파일 중입니다. 컴파일이 끝난 뒤 빌드하세요." : "스크립트 컴파일이 끝난 상태입니다.");
            AddAcceptanceFinding(findings, !string.IsNullOrWhiteSpace(scenePath) ? "PASS" : saveScene ? "WARN" : "FAIL", "씬",
                !string.IsNullOrWhiteSpace(scenePath) ? "현재 씬이 저장되어 있습니다: " + scenePath + "." : saveScene ? "현재 씬은 아직 저장되지 않았지만 빌드 전에 자동 저장을 시도합니다." : "현재 씬이 저장되지 않았고 자동 저장도 꺼져 있습니다.");
            AddAcceptanceFinding(findings, !scene.isDirty ? "PASS" : saveScene ? "WARN" : "FAIL", "씬",
                !scene.isDirty ? "저장되지 않은 씬 변경 사항이 없습니다." : saveScene ? "저장되지 않은 변경 사항은 빌드 전에 자동 저장됩니다." : "저장되지 않은 변경 사항이 있고 자동 저장이 꺼져 있습니다.");
            AddAcceptanceFinding(findings, currentSceneInBuild ? "PASS" : addSceneToBuild ? "WARN" : "FAIL", "빌드 설정",
                currentSceneInBuild ? "현재 씬이 빌드 설정에 포함되어 있습니다." : addSceneToBuild ? "현재 씬은 빌드 전에 빌드 설정에 자동 추가됩니다." : "현재 씬이 빌드 설정에 없고 자동 추가도 꺼져 있습니다.");
            AddAcceptanceFinding(findings, useOnlyActiveSceneForWindowsBuild || extraEnabledScenes.Count == 0 ? "PASS" : "WARN", "빌드 설정",
                useOnlyActiveSceneForWindowsBuild
                    ? "Windows 자동 빌드는 빌드 설정을 현재 씬 1개로 정리합니다."
                    : extraEnabledScenes.Count == 0
                        ? "활성 빌드 씬이 현재 씬뿐입니다."
                        : "현재 씬 외 활성 빌드 씬 " + extraEnabledScenes.Count + "개가 있습니다. Windows EXE는 현재 씬만 직접 사용하지만, 혼동을 줄이려면 '현재 씬만 사용'을 켜세요.");
            AddAcceptanceFinding(findings, BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64) ? "PASS" : "FAIL", "빌드 타겟",
                BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64) ? "Windows 64비트 빌드 대상이 지원됩니다." : "현재 Unity 설치에서 Windows 64비트 빌드 대상이 지원되지 않습니다.");
            AddAcceptanceFinding(findings, acceptanceFailCount == 0 ? "PASS" : "FAIL", "완성 점검",
                acceptanceFailCount == 0 ? "완성 체크리스트에 실패 항목이 없습니다." : "완성 체크리스트 실패 항목 " + acceptanceFailCount + "개가 남아 있습니다.");
            AddAcceptanceFinding(findings, acceptanceWarnCount == 0 ? "PASS" : "WARN", "완성 점검",
                acceptanceWarnCount == 0 ? "확인 필요 항목 없이 빌드할 수 있습니다." : "확인 필요 항목 " + acceptanceWarnCount + "개가 남아 있습니다. 자동 보정 후 빌드를 권장합니다.");
            AddAcceptanceFinding(findings, Directory.Exists(BuildRoot) ? "PASS" : "WARN", "출력",
                Directory.Exists(BuildRoot) ? "빌드 출력 폴더가 준비되어 있습니다: " + BuildRoot + "." : "빌드 출력 폴더는 빌드 시 자동 생성됩니다: " + BuildRoot + ".");
            AddAcceptanceFinding(findings, File.Exists(outputPath) ? "WARN" : "PASS", "출력",
                File.Exists(outputPath) ? "기존 실행 파일을 덮어쓸 수 있습니다: " + outputPath.Replace("\\", "/") + "." : "새 실행 파일 경로가 준비되어 있습니다: " + outputPath.Replace("\\", "/") + ".");

            return findings;
        }

        string WindowsBuildOutputPath()
        {
            return Path.Combine(BuildRoot, "VARCO_" + genre, "VARCO_" + genre + ".exe");
        }

        static string[] WindowsBuildScenePaths(string scenePath)
        {
            return string.IsNullOrWhiteSpace(scenePath)
                ? Array.Empty<string>()
                : new[] { scenePath };
        }

        static List<string> EnabledBuildScenePaths()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene != null && scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToList();
        }

        List<AcceptanceFinding> BuildWindowsPreflightFindings(string scenePath)
        {
            var findings = new List<AcceptanceFinding>();
            var scene = SceneManager.GetActiveScene();
            var acceptance = BuildAcceptanceChecklist();
            var acceptanceFailCount = acceptance.Count(finding => finding.state == "FAIL");
            var acceptanceWarnCount = acceptance.Count(finding => finding.state == "WARN");
            var currentSceneInBuild = !string.IsNullOrWhiteSpace(scenePath)
                && EditorBuildSettings.scenes.Any(s => s.enabled && s.path == scenePath);
            var buildScenes = WindowsBuildScenePaths(scenePath);

            AddAcceptanceFinding(findings, EditorApplication.isPlayingOrWillChangePlaymode ? "FAIL" : "PASS", "빌드 전 점검",
                EditorApplication.isPlayingOrWillChangePlaymode ? "Play 모드가 켜져 있거나 전환 중입니다. Play 모드를 끄고 다시 빌드하세요." : "Unity Play 모드가 꺼져 있습니다.");
            AddAcceptanceFinding(findings, EditorApplication.isCompiling ? "FAIL" : "PASS", "빌드 전 점검",
                EditorApplication.isCompiling ? "스크립트 컴파일 중입니다. 컴파일이 끝난 뒤 다시 빌드하세요." : "스크립트 컴파일이 끝난 상태입니다.");
            AddAcceptanceFinding(findings, !string.IsNullOrWhiteSpace(scenePath) ? "PASS" : "FAIL", "빌드 전 점검",
                !string.IsNullOrWhiteSpace(scenePath) ? "빌드할 씬이 저장되어 있습니다: " + scenePath + "." : "현재 씬이 저장되어 있지 않습니다.");
            AddAcceptanceFinding(findings, currentSceneInBuild ? "PASS" : "FAIL", "빌드 전 점검",
                currentSceneInBuild ? "현재 씬이 빌드 설정에 포함되어 있습니다." : "현재 씬이 빌드 설정에 없습니다.");
            AddAcceptanceFinding(findings, buildScenes.Length == 1 && buildScenes[0] == scenePath ? "PASS" : "WARN", "빌드 대상 씬",
                buildScenes.Length == 1 && buildScenes[0] == scenePath
                    ? "실제 Windows EXE 빌드 대상은 현재 씬 1개입니다: " + scenePath + "."
                    : "실제 Windows EXE 빌드 대상 씬을 다시 확인하세요: " + string.Join(", ", buildScenes) + ".");
            AddAcceptanceFinding(findings, BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64) ? "PASS" : "FAIL", "빌드 전 점검",
                BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64) ? "Windows 64비트 빌드 대상이 지원됩니다." : "현재 Unity 설치에서 Windows 64비트 빌드 대상이 지원되지 않습니다.");
            AddAcceptanceFinding(findings, acceptanceFailCount == 0 ? "PASS" : "FAIL", "완성 점검",
                acceptanceFailCount == 0 ? "완성 체크리스트에 실패 항목이 없습니다." : "완성 체크리스트 실패 항목 " + acceptanceFailCount + "개가 남아 있습니다.");
            AddAcceptanceFinding(findings, acceptanceWarnCount == 0 ? "PASS" : "WARN", "완성 점검",
                acceptanceWarnCount == 0 ? "확인 필요 항목 없이 빌드할 수 있습니다." : "확인 필요 항목 " + acceptanceWarnCount + "개가 남아 있지만 빌드는 시도할 수 있습니다.");
            AddAcceptanceFinding(findings, scene.isDirty ? "WARN" : "PASS", "빌드 전 점검",
                scene.isDirty ? "현재 씬에 저장되지 않은 변경이 있습니다. 빌드 전에 자동 저장을 확인하세요." : "현재 씬 변경 사항이 저장되어 있습니다.");
            AddAcceptanceFinding(findings, Directory.Exists(BuildRoot) ? "PASS" : "WARN", "빌드 전 점검",
                Directory.Exists(BuildRoot) ? "빌드 출력 폴더가 준비되어 있습니다: " + BuildRoot + "." : "빌드 출력 폴더는 빌드 시 자동 생성됩니다: " + BuildRoot + ".");

            return findings;
        }

        void AppendBuildPreflightLog(List<AcceptanceFinding> findings)
        {
            log.Add("Windows 빌드 전 점검:");
            foreach (var finding in findings)
                log.Add(StateLabel(finding.state) + ": " + finding.area + " - " + finding.message);
        }

        static string FormatBytes(ulong bytes)
        {
            const double kb = 1024d;
            const double mb = kb * 1024d;
            const double gb = mb * 1024d;
            if (bytes >= (ulong)gb)
                return (bytes / gb).ToString("0.00") + " GB";
            if (bytes >= (ulong)mb)
                return (bytes / mb).ToString("0.00") + " MB";
            if (bytes >= (ulong)kb)
                return (bytes / kb).ToString("0.00") + " KB";
            return bytes + " B";
        }

        void OpenGenreScene()
        {
            var path = GenreScenePath(genre);
            if (File.Exists(path))
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                log.Add("씬 열기 완료: " + path);
            }
        }

        static string GenreScenePath(VWS.GenreType targetGenre)
        {
            switch (targetGenre)
            {
                case VWS.GenreType.Arena:
                    return "Assets/Scenes/VARCO_Arena/VARCO_Arena_Example.unity";
                case VWS.GenreType.Exploration:
                    return "Assets/Scenes/VARCO_Exploration/VARCO_Exploration_Example.unity";
                case VWS.GenreType.Puzzle:
                    return "Assets/Scenes/VARCO_Puzzle/VARCO_Puzzle_Example.unity";
                default:
                    return "Assets/Scenes/VARCO_Platform/VARCO_Platform_Space3D.unity";
            }
        }

        void EnsureSceneCanBeSaved()
        {
            var scene = SceneManager.GetActiveScene();
            if (!string.IsNullOrWhiteSpace(scene.path))
                return;

            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Scenes/VARCO_AutoBuild");
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/VARCO_AutoBuild/VARCO_" + genre + "_AutoBuild.unity");
            if (EditorSceneManager.SaveScene(scene, path))
                log.Add("새 씬 저장됨: " + path);
            else
                log.Add("새 씬 저장 실패: " + path);
        }

        bool SaveActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            var saved = EditorSceneManager.SaveScene(scene);
            log.Add(saved ? "현재 씬을 저장했습니다." : "현재 씬 저장에 실패했습니다.");
            return saved;
        }

        void EnsureFolders()
        {
            EnsureFolder("Assets/Audio");
            EnsureFolder("Assets/Audio/BGM");
            EnsureFolder("Assets/Audio/SFX");
            EnsureFolder(TtsAudioFolder);
            EnsureFolder("Assets/Animations");
            EnsureFolder(GeneratedAnimationFolder);
            EnsureFolder("Assets/ScriptableObjects");
            EnsureFolder("Assets/ScriptableObjects/GameProfiles");
            EnsureFolder("Assets/ScriptableObjects/SoundEvents");
            EnsureFolder(PresetFolder);
            EnsureFolder(VisualProfileFolder);
        }

        static void EnsureFolder(string path)
        {
            path = path.Replace("\\", "/").TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent))
                return;
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        static void EnsureTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || IsBuiltInUnityTag(tag))
                return;

            try
            {
                GameObject.FindGameObjectsWithTag(tag);
            }
            catch
            {
                var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                var prop = tags.FindProperty("tags");
                for (int i = 0; i < prop.arraySize; i++)
                {
                    if (prop.GetArrayElementAtIndex(i).stringValue == tag)
                        return;
                }

                prop.InsertArrayElementAtIndex(prop.arraySize);
                prop.GetArrayElementAtIndex(prop.arraySize - 1).stringValue = tag;
                tags.ApplyModifiedProperties();
            }
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

        AssetCandidate FindBest(AssetRole role, VWS.GenreType targetGenre)
        {
            var presetKitCandidate = FindBestPresetKitCandidate(role, targetGenre);
            if (presetKitCandidate != null)
                return presetKitCandidate;

            var preferredKind = PreferredCharacterKind(role, targetGenre);
            var explicitEnemyKind = HasExplicitEnemyCharacterChoice(role);
            var explicitPlayerGenre = ExplicitPlayerCharacterGenre(role);
            var ignorePlayerGenre = role == AssetRole.Player && playerCharacter == PlayerCharacterChoice.Any;
            return candidates
                .Where(c => IsCandidateAllowedForCurrentPresetKit(c, targetGenre) && IsCandidateUsableForRole(c, role, targetGenre))
                .OrderByDescending(c => AssetMatchesCurrentPreference(role, c) ? 1 : 0)
                .ThenByDescending(c => explicitPlayerGenre.HasValue && c.genre == explicitPlayerGenre.Value ? 1 : 0)
                .ThenByDescending(c => explicitEnemyKind && c.characterKind == preferredKind ? 1 : 0)
                .ThenByDescending(c => !explicitPlayerGenre.HasValue && !ignorePlayerGenre && c.genre == targetGenre ? 1 : 0)
                .ThenByDescending(c => !explicitEnemyKind && preferredKind != CharacterKind.None && c.characterKind == preferredKind ? 1 : 0)
                .ThenByDescending(c => RoleFitScore(c, role, targetGenre))
                .ThenByDescending(c => c.hasVisuals ? 1 : 0)
                .ThenByDescending(c => IsCharacterRole(role) && c.hasSkinnedMesh ? 1 : 0)
                .ThenByDescending(c => c.isPrefab ? 1 : 0)
                .ThenByDescending(c => c.score)
                .FirstOrDefault();
        }

        AssetCandidate FindBestPresetKitCandidate(AssetRole role, VWS.GenreType targetGenre)
        {
            var preferredKind = PreferredCharacterKind(role, targetGenre);
            return candidates
                .Where(c => IsCurrentPresetKitCandidate(c, targetGenre)
                    && !IsPresetKitPlaceholderPath(c.path)
                    && IsCandidateUsableForRole(c, role, targetGenre))
                .OrderByDescending(c => c.role == role ? 1 : 0)
                .ThenByDescending(c => preferredKind != CharacterKind.None && c.characterKind == preferredKind ? 1 : 0)
                .ThenByDescending(c => PresetKitSlotLooksLikeRole(c.path, role) ? 1 : 0)
                .ThenByDescending(c => RoleFitScore(c, role, targetGenre))
                .ThenByDescending(c => c.isPrefab ? 1 : 0)
                .ThenByDescending(c => c.score)
                .FirstOrDefault();
        }

        AssetCandidate FindBestExternalCandidate(AssetRole role, VWS.GenreType targetGenre)
        {
            var preferredKind = PreferredCharacterKind(role, targetGenre);
            var explicitEnemyKind = HasExplicitEnemyCharacterChoice(role);
            var explicitPlayerGenre = ExplicitPlayerCharacterGenre(role);
            var ignorePlayerGenre = role == AssetRole.Player && playerCharacter == PlayerCharacterChoice.Any;
            return candidates
                .Where(c => !c.fromPresetKit && IsCandidateUsableForRole(c, role, targetGenre))
                .OrderByDescending(c => AssetMatchesCurrentPreference(role, c) ? 1 : 0)
                .ThenByDescending(c => explicitPlayerGenre.HasValue && c.genre == explicitPlayerGenre.Value ? 1 : 0)
                .ThenByDescending(c => explicitEnemyKind && c.characterKind == preferredKind ? 1 : 0)
                .ThenByDescending(c => !explicitPlayerGenre.HasValue && !ignorePlayerGenre && c.genre == targetGenre ? 1 : 0)
                .ThenByDescending(c => !explicitEnemyKind && preferredKind != CharacterKind.None && c.characterKind == preferredKind ? 1 : 0)
                .ThenByDescending(c => RoleFitScore(c, role, targetGenre))
                .ThenByDescending(c => c.hasVisuals ? 1 : 0)
                .ThenByDescending(c => IsCharacterRole(role) && c.hasSkinnedMesh ? 1 : 0)
                .ThenByDescending(c => c.isPrefab ? 1 : 0)
                .ThenByDescending(c => c.score)
                .FirstOrDefault();
        }

        bool IsCandidateAllowedForCurrentPresetKit(AssetCandidate candidate, VWS.GenreType targetGenre)
        {
            if (candidate == null || !candidate.fromPresetKit)
                return true;

            if (IsPresetKitPlaceholderPath(candidate.path))
                return false;

            return IsCurrentPresetKitCandidate(candidate, targetGenre);
        }

        bool IsCurrentPresetKitCandidate(AssetCandidate candidate, VWS.GenreType targetGenre)
        {
            if (candidate == null || !candidate.fromPresetKit)
                return false;

            return string.Equals(candidate.presetKitKey, PresetKitKey(targetGenre, blockTemplate), StringComparison.OrdinalIgnoreCase);
        }

        static bool PresetKitSlotLooksLikeRole(string path, AssetRole role)
        {
            var name = Normalize(Path.GetFileNameWithoutExtension(path));
            return ContainsAny(name, role.ToString(), PresetKitSlotFileName(role));
        }

        IEnumerable<AssetCandidate> RankedCandidatesForRole(AssetRole role, int maxCount)
        {
            var preferredKind = PreferredCharacterKind(role, genre);
            var explicitEnemyKind = HasExplicitEnemyCharacterChoice(role);
            var explicitPlayerGenre = ExplicitPlayerCharacterGenre(role);
            var ignorePlayerGenre = role == AssetRole.Player && playerCharacter == PlayerCharacterChoice.Any;
            return candidates
                .Where(c => IsCandidateAllowedForCurrentPresetKit(c, genre) && IsCandidateUsableForRole(c, role, genre))
                .OrderByDescending(c => IsCurrentPresetKitCandidate(c, genre) ? 1 : 0)
                .ThenByDescending(c => AssetMatchesCurrentPreference(role, c) ? 1 : 0)
                .ThenByDescending(c => explicitPlayerGenre.HasValue && c.genre == explicitPlayerGenre.Value ? 1 : 0)
                .ThenByDescending(c => explicitEnemyKind && c.characterKind == preferredKind ? 1 : 0)
                .ThenByDescending(c => !explicitPlayerGenre.HasValue && !ignorePlayerGenre && c.genre == genre ? 1 : 0)
                .ThenByDescending(c => !explicitEnemyKind && preferredKind != CharacterKind.None && c.characterKind == preferredKind ? 1 : 0)
                .ThenByDescending(c => RoleFitScore(c, role, genre))
                .ThenByDescending(c => c.hasVisuals ? 1 : 0)
                .ThenByDescending(c => IsCharacterRole(role) && c.hasSkinnedMesh ? 1 : 0)
                .ThenByDescending(c => c.isPrefab ? 1 : 0)
                .ThenByDescending(c => c.score)
                .Take(Mathf.Max(1, maxCount));
        }

        bool HasExplicitEnemyCharacterChoice(AssetRole role)
        {
            return role == AssetRole.Enemy
                && enemyCharacter != EnemyCharacterChoice.Auto
                && enemyCharacter != EnemyCharacterChoice.Any
                && PreferredCharacterKind(role, genre) != CharacterKind.None;
        }

        VWS.GenreType? ExplicitPlayerCharacterGenre(AssetRole role)
        {
            if (role != AssetRole.Player)
                return null;

            switch (playerCharacter)
            {
                case PlayerCharacterChoice.Arena:
                    return VWS.GenreType.Arena;
                case PlayerCharacterChoice.Exploration:
                    return VWS.GenreType.Exploration;
                case PlayerCharacterChoice.Puzzle:
                    return VWS.GenreType.Puzzle;
                case PlayerCharacterChoice.Platform:
                    return VWS.GenreType.Platform;
                default:
                    return null;
            }
        }

        bool PlayerCandidateMatchesSelection(AssetCandidate candidate)
        {
            if (candidate == null)
                return false;

            var explicitGenre = ExplicitPlayerCharacterGenre(AssetRole.Player);
            if (explicitGenre.HasValue)
                return candidate.genre == explicitGenre.Value;
            if (playerCharacter == PlayerCharacterChoice.Any)
                return candidate.role != AssetRole.Enemy && !LooksLikeEnemyText(candidate.normalizedText ?? string.Empty);
            return candidate.characterKind == CharacterKind.Player
                || (candidate.role == AssetRole.Player && !LooksLikeEnemyText(candidate.normalizedText ?? string.Empty));
        }

        static bool IsCharacterRole(AssetRole role)
        {
            return role == AssetRole.Player || role == AssetRole.Enemy;
        }

        static bool IsSimpleFunctionalRole(AssetRole role)
        {
            switch (role)
            {
                case AssetRole.Player:
                case AssetRole.Enemy:
                case AssetRole.ArenaCover:
                case AssetRole.Unknown:
                    return false;
                default:
                    return true;
            }
        }

        static bool IsComplexForSimpleFunction(AssetRole role, AssetCandidate candidate)
        {
            if (candidate == null || !IsSimpleFunctionalRole(role))
                return false;

            return candidate.transformCount > 500
                || candidate.rendererCount > 120
                || candidate.lightCount > 24;
        }

        string BuildAssetShortReason(AssetCandidate candidate)
        {
            if (candidate == null)
                return "후보 없음";

            var reasons = new List<string>();
            if (candidate.genre == genre)
                reasons.Add("현재 장르 일치");
            else if (candidate.genre.HasValue)
                reasons.Add("다른 장르 후보: " + GenreLabel(candidate.genre.Value));
            else
                reasons.Add("장르 제한 없음");

            reasons.Add(AssetMatchesCurrentPreference(candidate.role, candidate) ? "선택 조건 일치" : "선택 조건 확인 필요");
            if (!string.IsNullOrWhiteSpace(candidate.matchReason))
                reasons.Add(candidate.matchReason);

            return string.Join(" / ", reasons);
        }

        static string BuildBaseAssetReason(AssetCandidate candidate, string normalizedText)
        {
            if (candidate == null)
                return string.Empty;

            var reasons = new List<string>
            {
                candidate.isPrefab ? "프리팹 우선" : "모델 후보"
            };

            if (candidate.hasVisuals)
                reasons.Add("보이는 Mesh/Renderer " + candidate.rendererCount + "개");
            else
                reasons.Add("비주얼 없음");

            if (candidate.animatorCount > 0)
                reasons.Add("Animator " + candidate.animatorCount + "개");
            if (candidate.hasSkinnedMesh)
                reasons.Add("리깅 캐릭터 가능");
            if (candidate.usedInternalEvidence)
                reasons.Add("프리팹 내부 단서 " + candidate.internalEvidenceCount + "개 사용");
            if (candidate.fromPresetKit)
                reasons.Add("프리셋 키트 우선");
            if (candidate.genre.HasValue)
                reasons.Add(GenreLabel(candidate.genre.Value) + " 키워드");
            if (candidate.characterKind != CharacterKind.None)
                reasons.Add(CharacterKindLabel(candidate.characterKind) + " 키워드");
            if (!string.IsNullOrWhiteSpace(normalizedText) && normalizedText.Contains("idle"))
                reasons.Add("대표 Idle 모델 가능");
            if (!string.IsNullOrWhiteSpace(normalizedText) && ContainsAny(normalizedText, "walk", "attack", "death", "jump", "push"))
                reasons.Add("애니메이션 클립명 가능성");

            return string.Join(", ", reasons);
        }

        static string BuildAssetLabel(AssetCandidate candidate)
        {
            if (candidate == null)
                return "없음";

            var source = candidate.isPrefab ? "프리팹" : "모델";
            var genreText = candidate.genre.HasValue ? GenreLabel(candidate.genre.Value) : "전체";
            var visualText = candidate.hasVisuals
                ? candidate.rendererCount + "R/" + candidate.animatorCount + "A" + (candidate.hasSkinnedMesh ? "/Skin" : string.Empty)
                : "비주얼 없음";
            var reasonText = string.IsNullOrWhiteSpace(candidate.matchReason) ? string.Empty : ", " + candidate.matchReason;
            return candidate.DisplayName
                + " [" + source
                + ", " + genreText
                + ", " + CharacterKindLabel(candidate.characterKind)
                + (candidate.fromPresetKit ? ", 프리셋 키트" : string.Empty)
                + ", 점수 " + candidate.score
                + ", " + visualText
                + reasonText
                + "] (" + candidate.path + ")";
        }

        CharacterKind PreferredCharacterKind(AssetRole role, VWS.GenreType targetGenre)
        {
            if (role == AssetRole.Player)
                return CharacterKind.Player;
            if (role != AssetRole.Enemy)
                return CharacterKind.None;

            var selected = EnemyChoiceToCharacterKind();
            if (selected.HasValue)
                return selected.Value;

            switch (targetGenre)
            {
                case VWS.GenreType.Exploration:
                    return CharacterKind.Zombie;
                case VWS.GenreType.Arena:
                    return CharacterKind.Boss;
                default:
                    return CharacterKind.None;
            }
        }

        CharacterKind? EnemyChoiceToCharacterKind()
        {
            switch (enemyCharacter)
            {
                case EnemyCharacterChoice.Boss:
                    return CharacterKind.Boss;
                case EnemyCharacterChoice.Zombie:
                    return CharacterKind.Zombie;
                case EnemyCharacterChoice.Orc:
                    return CharacterKind.Orc;
                case EnemyCharacterChoice.Drone:
                    return CharacterKind.Drone;
                case EnemyCharacterChoice.Any:
                    return CharacterKind.None;
                default:
                    return null;
            }
        }

        string EnemyCharacterLabel()
        {
            var selected = EnemyChoiceToCharacterKind();
            if (selected.HasValue && selected.Value != CharacterKind.None)
                return CharacterKindLabel(selected.Value);
            if (enemyCharacter == EnemyCharacterChoice.Any)
                return "아무 적";
            return "자동/" + CharacterKindLabel(PreferredCharacterKind(AssetRole.Enemy, genre));
        }

        string PlayerCharacterLabel()
        {
            var explicitGenre = ExplicitPlayerCharacterGenre(AssetRole.Player);
            if (explicitGenre.HasValue)
                return GenreLabel(explicitGenre.Value);
            if (playerCharacter == PlayerCharacterChoice.Any)
                return "아무 플레이어";
            return "자동/" + GenreLabel(genre);
        }

        static VWS.GenreType? GuessGenreFromText(string text)
        {
            if (ContainsAny(text, "arena", "combat", "battle", "boss", "duel", "colosseum",
                    "아레나", "전투", "배틀", "보스", "격투", "싸움", "결투", "투기장")) return VWS.GenreType.Arena;
            if (ContainsAny(text, "exploration", "explorer", "forest", "nature", "town", "field", "outdoor", "adventure", "tree", "plant", "bush", "grass", "village", "woodland",
                    "탐험", "탐색", "숲", "자연", "마을", "필드", "야외", "모험", "나무", "식물", "수풀", "풀", "초원", "야생")) return VWS.GenreType.Exploration;
            if (ContainsAny(text, "puzzle", "dungeon", "door_room", "escape", "pressure_plate", "switch", "room", "temple", "castle", "ruin",
                    "퍼즐", "던전", "탈출", "압력판", "스위치", "문", "방", "사원", "성", "폐허")) return VWS.GenreType.Puzzle;
            if (ContainsAny(text, "platform", "space", "sci_fi", "scifi", "science_fiction", "lift", "moving_platform", "spaceship", "asteroid", "alien_planet",
                    "플랫폼", "우주", "스페이스", "sf", "과학", "공상과학", "리프트", "이동발판", "이동_발판", "발판", "우주선", "소행성", "외계행성")) return VWS.GenreType.Platform;
            return null;
        }

        static CharacterKind GuessCharacterKind(string text)
        {
            if (ContainsAny(text, "player", "hero", "explorer", "adventurer", "astronaut", "avatar",
                    "플레이어", "주인공", "영웅", "탐험가", "우주인", "아바타", "기사", "전사")) return CharacterKind.Player;
            if (ContainsAny(text, "boss",
                    "보스", "대장")) return CharacterKind.Boss;
            if (ContainsAny(text, "zombie", "undead",
                    "좀비", "언데드")) return CharacterKind.Zombie;
            if (ContainsAny(text, "orc",
                    "오크")) return CharacterKind.Orc;
            if (ContainsAny(text, "drone", "robot", "turret",
                    "드론", "로봇", "터렛", "포탑")) return CharacterKind.Drone;
            if (ContainsAny(text, "door", "gate", "item", "goal", "checkpoint", "switch", "crate", "box", "wall", "rock", "tree", "plant", "prop", "scenery", "environment",
                    "문", "게이트", "아이템", "목표", "체크포인트", "스위치", "상자", "박스", "벽", "바위", "나무", "식물", "소품", "배경", "환경")) return CharacterKind.Object;
            return CharacterKind.None;
        }

        IEnumerable<AssetRole> ActiveRolesForCurrentBlocks()
        {
            if (blockPlayer) yield return AssetRole.Player;
            if (blockWeapon) yield return AssetRole.Weapon;
            if (blockEnemyWave) yield return AssetRole.Enemy;
            if (blockItems) yield return AssetRole.ItemPickup;
            if (blockHealthPickup) yield return AssetRole.HealthPickup;
            if (blockGoal) yield return AssetRole.Goal;
            if (blockPuzzleDoor)
            {
                yield return AssetRole.Door;
                yield return AssetRole.PressurePlate;
            }
            if (blockHazard) yield return AssetRole.HazardZone;
            if (blockMovingPlatform) yield return AssetRole.MovingPlatform;
            if (blockMovableBox) yield return AssetRole.MovableBox;
            if (blockCheckpoint) yield return AssetRole.Checkpoint;
            if (blockCover) yield return AssetRole.ArenaCover;
        }

        static IEnumerable<AssetRole> RequiredRolesForGenre(VWS.GenreType targetGenre)
        {
            yield return AssetRole.Player;
            if (targetGenre == VWS.GenreType.Arena || targetGenre == VWS.GenreType.Exploration)
            {
                yield return AssetRole.Enemy;
                yield return AssetRole.Weapon;
                yield return AssetRole.HealthPickup;
            }

            if (targetGenre == VWS.GenreType.Exploration || targetGenre == VWS.GenreType.Platform)
            {
                yield return AssetRole.ItemPickup;
                yield return AssetRole.Checkpoint;
                yield return AssetRole.HazardZone;
            }

            if (targetGenre == VWS.GenreType.Puzzle)
            {
                yield return AssetRole.Door;
                yield return AssetRole.PressurePlate;
                yield return AssetRole.MovableBox;
            }

            if (targetGenre == VWS.GenreType.Platform)
                yield return AssetRole.MovingPlatform;

            yield return AssetRole.ArenaCover;
            yield return AssetRole.Goal;
        }

        VARCOAutoConnectorWindow.CameraViewPreset CameraForGenre()
        {
            if (genre == VWS.GenreType.Arena ||
                genre == VWS.GenreType.Exploration ||
                genre == VWS.GenreType.Puzzle)
                return VARCOAutoConnectorWindow.CameraViewPreset.QuarterView;
            return VARCOAutoConnectorWindow.CameraViewPreset.ThirdPerson;
        }

        VARCOAutoConnectorWindow.CameraViewPreset EffectiveCameraPreset()
        {
            switch (cameraPreset)
            {
                case CameraPresetChoice.QuarterView:
                    return VARCOAutoConnectorWindow.CameraViewPreset.QuarterView;
                case CameraPresetChoice.TopDown:
                    return VARCOAutoConnectorWindow.CameraViewPreset.TopDown;
                case CameraPresetChoice.SideView:
                    return VARCOAutoConnectorWindow.CameraViewPreset.SideView;
                case CameraPresetChoice.ThirdPerson:
                    return VARCOAutoConnectorWindow.CameraViewPreset.ThirdPerson;
                default:
                    return CameraForGenre();
            }
        }

        Vector3 PlayerPosition()
        {
            if (blockTemplate == BlockTemplate.FullFeatureSandbox)
                return new Vector3(-12f, 0.1f, -6f);

            switch (recipe)
            {
                case GameRecipe.SurvivalTimer:
                    return new Vector3(0f, 0.1f, 0f);
                case GameRecipe.BossBattle:
                    return new Vector3(0f, 0.1f, -7.2f);
                case GameRecipe.CombatWave:
                    return new Vector3(0f, 0.1f, -7.2f);
                case GameRecipe.CollectAndEscape:
                    return new Vector3(0f, 0.1f, -5.2f);
                case GameRecipe.ZombieSurvival:
                    return new Vector3(-8f, 0.1f, -8f);
                case GameRecipe.TreasureHunt:
                    return new Vector3(-8f, 0.1f, -6.2f);
                case GameRecipe.ExplorationQuest:
                    return new Vector3(0f, 0.1f, -11.1f);
                case GameRecipe.EscapeRoom:
                    return new Vector3(0f, 0.1f, -5.8f);
                case GameRecipe.DoorPuzzle:
                    return new Vector3(-3.2f, 0.1f, -5.2f);
                case GameRecipe.PlatformCourse:
                    return new Vector3(-6.2f, 1.15f, 0f);
                case GameRecipe.ObstacleRun:
                    return new Vector3(-11.2f, 1.15f, 0f);
            }

            switch (genre)
            {
                case VWS.GenreType.Platform:
                    return new Vector3(-11.2f, 1.15f, 0f);
                case VWS.GenreType.Puzzle:
                    return new Vector3(0f, 0.1f, -5.5f);
                case VWS.GenreType.Exploration:
                    return new Vector3(0f, 0.1f, -7f);
                default:
                    return new Vector3(0f, 0.1f, -6f);
            }
        }

        Quaternion PlayerRotation()
        {
            if (genre == VWS.GenreType.Platform)
                return Quaternion.Euler(0f, 90f, 0f);
            if (recipe == GameRecipe.ZombieSurvival || recipe == GameRecipe.TreasureHunt)
                return Quaternion.Euler(0f, 35f, 0f);
            return Quaternion.identity;
        }

        float GenreSunAngle()
        {
            switch (genre)
            {
                case VWS.GenreType.Puzzle: return 38f;
                case VWS.GenreType.Platform: return 48f;
                default: return 55f;
            }
        }

        Color GenreFogColor()
        {
            switch (genre)
            {
                case VWS.GenreType.Arena: return new Color(0.08f, 0.08f, 0.09f);
                case VWS.GenreType.Exploration: return new Color(0.08f, 0.105f, 0.095f);
                case VWS.GenreType.Puzzle: return new Color(0.12f, 0.09f, 0.055f);
                default: return new Color(0.015f, 0.02f, 0.04f);
            }
        }

        Color GenreAmbientSky()
        {
            switch (genre)
            {
                case VWS.GenreType.Puzzle: return new Color(0.38f, 0.28f, 0.18f);
                case VWS.GenreType.Platform: return new Color(0.06f, 0.1f, 0.18f);
                default: return new Color(0.22f, 0.26f, 0.3f);
            }
        }

        Color GenreAmbientEquator()
        {
            switch (genre)
            {
                case VWS.GenreType.Exploration: return new Color(0.18f, 0.23f, 0.17f);
                case VWS.GenreType.Puzzle: return new Color(0.35f, 0.22f, 0.12f);
                default: return new Color(0.17f, 0.2f, 0.24f);
            }
        }

        static string BgmIdForGenre(VWS.GenreType targetGenre)
        {
            switch (targetGenre)
            {
                case VWS.GenreType.Arena:
                    return "bgm_arena_battle_loop";
                case VWS.GenreType.Exploration:
                    return "bgm_exploration_loop";
                case VWS.GenreType.Puzzle:
                    return "bgm_puzzle_loop";
                default:
                    return "bgm_platform_space_loop";
            }
        }

        static string BuildSoundId(string assetPath)
        {
            var file = Path.GetFileNameWithoutExtension(assetPath);
            var id = Regex.Replace(file.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
            id = Regex.Replace(id, @"_+", "_");
            if (id.StartsWith("sfx_", StringComparison.Ordinal) || id.StartsWith("bgm_", StringComparison.Ordinal) || id.StartsWith("amb_", StringComparison.Ordinal))
                return id;

            var normalized = assetPath.Replace('\\', '/').ToLowerInvariant();
            if (normalized.Contains("/bgm/")) return "bgm_" + id;
            if (normalized.Contains("/ambient/")) return "amb_" + id;
            return "sfx_" + id;
        }

        static float DefaultVolumeForPath(string path)
        {
            var normalized = path.Replace('\\', '/').ToLowerInvariant();
            if (normalized.Contains("/bgm/")) return 0.45f;
            if (normalized.Contains("/ambient/")) return 0.65f;
            return 1f;
        }

        static string Normalize(string value)
        {
            var normalized = Regex.Replace((value ?? string.Empty).Replace('\\', '/').ToLowerInvariant(), @"[^\p{L}\p{N}_/]+", "_");
            return Regex.Replace(normalized, @"_+", "_").Trim('_');
        }

        static bool ContainsAny(string text, params string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                var normalized = Normalize(keyword);
                if (!string.IsNullOrEmpty(normalized) && text.Contains(normalized))
                    return true;
            }

            return false;
        }

        static bool ContainsSegment(string text, params string[] keywords)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (var keyword in keywords)
            {
                var normalized = Normalize(keyword);
                if (string.IsNullOrEmpty(normalized))
                    continue;

                if (Regex.IsMatch(text, @"(^|[_/])" + Regex.Escape(normalized) + @"([_/]|$)"))
                    return true;
            }

            return false;
        }

        static string SafeFileName(string value)
        {
            return Regex.Replace(value ?? "VARCO", @"[^A-Za-z0-9_]+", "_").Trim('_');
        }

        static void SetColor(GameObject go, Color color)
        {
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var material = new Material(shader) { color = color };
                renderer.sharedMaterial = material;
            }
        }

        static void DrawHeader(string text)
        {
            GUILayout.Space(10f);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }
    }

    [CreateAssetMenu(fileName = "VARCO_GameMakerPreset", menuName = "VARCO/데이터/게임 메이커 프리셋", order = 2)]
    public class VARCOGameMakerPreset : ScriptableObject
    {
        [Header("게임")]
        public VWS.GenreType genre = VWS.GenreType.Arena;
        public VARCOGameMakerWindow.SceneMode sceneMode = VARCOGameMakerWindow.SceneMode.CurrentScene;
        public VARCOGameMakerWindow.GameRecipe recipe = VARCOGameMakerWindow.GameRecipe.GenreDefault;
        public VARCOGameMakerWindow.BlockTemplate blockTemplate = VARCOGameMakerWindow.BlockTemplate.Custom;
        public VARCOGameMakerWindow.PlayerCharacterChoice playerCharacter = VARCOGameMakerWindow.PlayerCharacterChoice.Auto;
        public VARCOGameMakerWindow.EnemyCharacterChoice enemyCharacter = VARCOGameMakerWindow.EnemyCharacterChoice.Auto;
        public VARCOGameMakerWindow.DifficultyPreset difficulty = VARCOGameMakerWindow.DifficultyPreset.Normal;
        public VARCOGameMakerWindow.CameraPresetChoice cameraPreset = VARCOGameMakerWindow.CameraPresetChoice.Auto;
        public VARCOGameMakerWindow.PlayerMovementChoice playerMovement = VARCOGameMakerWindow.PlayerMovementChoice.Auto;

        [Header("개수")]
        [Min(0)] public int itemGoal = 3;
        [Min(1)] public int waveEnemyCount = 3;
        [Min(10f)] public float countdownSeconds = 90f;

        [Header("블록")]
        public bool blockPlayer = true;
        public bool blockWeapon = true;
        public bool blockEnemyWave;
        public bool blockItems;
        public bool blockGoal = true;
        public bool blockHealthPickup;
        public bool blockHazard;
        public bool blockCheckpoint;
        public bool blockFallRespawn;
        public bool blockMovingPlatform;
        public bool blockPuzzleDoor;
        public bool blockMovableBox;
        public bool blockCover;
        public bool blockCountdown;
        public bool blockHud = true;
        public bool blockVisuals = true;
        public bool blockSound = true;

        [Header("자동화")]
        public bool createMissingObjects = true;
        public bool autoConnectPrefabs = true;
        public bool autoAnimations = true;
        public bool autoSounds = true;
        public bool addModernHud = true;
        public bool applyVisualPreset = true;
        public bool runSafetyPass = true;
        public bool addSceneToBuild = true;
        public bool saveScene = true;
    }
}
#endif
