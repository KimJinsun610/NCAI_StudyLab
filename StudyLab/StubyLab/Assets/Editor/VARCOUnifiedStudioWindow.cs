#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VWS = VARCO_Workshop;

namespace VARCO_Workshop.Editor
{
    public class VARCOUnifiedStudioWindow : EditorWindow
    {
        const string RegistryPath = "Assets/ScriptableObjects/SoundEvents/WorkshopSoundEventRegistry.asset";
        static readonly string[] GameObjectAssetRoots =
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
            "Assets/Resources"
        };

        static readonly string[] AnimationAssetRoots =
        {
            "Assets/Animations",
            "Assets/VARCO3DImports",
            "Assets/Importing Assets",
            "Assets/Models",
            "Assets/Characters",
            "Assets/Generated",
            "Assets/Resources"
        };

        static readonly OneClickGameCard[] OneClickGameCards =
        {
            new OneClickGameCard(VARCOGameMakerWindow.BlockTemplate.ArenaCombatWave, "전투 아레나", "보스/적 웨이브, 공격, 체력, 전투 HUD를 묶습니다.", VARCOGameMakerWindow.BuildArenaGameFromMenu),
            new OneClickGameCard(VARCOGameMakerWindow.BlockTemplate.ExplorationZombieQuest, "탐험 좀비 퀘스트", "탐험 플레이어, 좀비, 수집, 목표, 체크포인트를 묶습니다.", VARCOGameMakerWindow.BuildExplorationGameFromMenu),
            new OneClickGameCard(VARCOGameMakerWindow.BlockTemplate.PuzzleDoorRoom, "문 열기 퍼즐방", "퍼즐 플레이어, 문, 압력판, 이동 상자, 목표를 묶습니다.", VARCOGameMakerWindow.BuildPuzzleGameFromMenu),
            new OneClickGameCard(VARCOGameMakerWindow.BlockTemplate.PlatformSpaceCourse, "스페이스 플랫폼", "플랫폼 플레이어, 낙사 리스폰, 발판, 체크포인트를 묶습니다.", VARCOGameMakerWindow.BuildPlatformGameFromMenu),
            new OneClickGameCard(VARCOGameMakerWindow.BlockTemplate.CollectAndEscape, "수집 후 탈출", "아이템을 모은 뒤 목표 지점으로 탈출하는 흐름을 만듭니다.", VARCOGameMakerWindow.BuildCollectAndEscapeGameFromMenu),
            new OneClickGameCard(VARCOGameMakerWindow.BlockTemplate.SurvivalTimer, "제한시간 생존", "타이머, 적 웨이브, 체력, 회복 아이템을 생존 규칙으로 묶습니다.", VARCOGameMakerWindow.BuildSurvivalTimerGameFromMenu),
            new OneClickGameCard(VARCOGameMakerWindow.BlockTemplate.ArenaBossBattle, "아레나 보스전", "강한 보스 1명, 전투 플레이어, 공격 피드백을 우선 연결합니다.", VARCOGameMakerWindow.BuildArenaBossBattleGameFromMenu),
            new OneClickGameCard(VARCOGameMakerWindow.BlockTemplate.ExplorationZombieSurvival, "탐험 좀비 생존", "탐험 이동, 좀비 다수, 위험 구역, 회복 아이템을 묶습니다.", VARCOGameMakerWindow.BuildExplorationZombieSurvivalGameFromMenu),
            new OneClickGameCard(VARCOGameMakerWindow.BlockTemplate.ExplorationTreasureHunt, "탐험 보물찾기", "수집 아이템, 길 안내, 목표 지점, HUD 진행도를 묶습니다.", VARCOGameMakerWindow.BuildExplorationTreasureHuntGameFromMenu),
            new OneClickGameCard(VARCOGameMakerWindow.BlockTemplate.PuzzleEscapeRoom, "퍼즐 탈출방", "문, 발판, 상자, 제한시간, 탈출 목표를 묶습니다.", VARCOGameMakerWindow.BuildPuzzleEscapeRoomGameFromMenu),
            new OneClickGameCard(VARCOGameMakerWindow.BlockTemplate.PlatformObstacleRun, "플랫폼 장애물 코스", "점프 코스, 움직이는 발판, 위험 구역, 체크포인트를 묶습니다.", VARCOGameMakerWindow.BuildPlatformObstacleRunGameFromMenu),
            new OneClickGameCard(VARCOGameMakerWindow.BlockTemplate.FullFeatureSandbox, "전체 기능 샌드박스", "플레이어, 적, 아이템, 문, 발판, HUD, 사운드를 한 번에 확인합니다.", VARCOGameMakerWindow.BuildFullFeatureSandboxFromMenu)
        };

        Vector2 scroll;
        string lastMessage = "준비됨: 위에서부터 필요한 버튼만 누르면 됩니다.";
        readonly AssetReadinessItem[] assetReadinessItems = CreateAssetReadinessItems();
        int modelCount;
        int audioCount;
        int animationCount;
        int selectedSceneObjects;
        int selectedProjectAssets;
        string activeSceneName = "";
        int internalEvidenceAssetCount;
        int visualModelAssetCount;
        int animatorAssetCount;
        int prefabAudioSourceCount;
        bool hasGameManager;
        bool hasPlayer;
        bool hasHud;
        bool hasGoal;
        bool hasCamera;
        bool hasLight;
        bool hasBgm;
        bool hasSoundRegistry;
        int readinessReady;
        const int ReadinessTotal = 8;
        int scenePlayerCount;
        int sceneEnemyCount;
        int sceneItemCount;
        int sceneHealthPickupCount;
        int sceneGoalCount;
        int sceneWaveManagerCount;
        int sceneCheckpointCount;
        int sceneDeathZoneCount;
        int sceneHazardCount;
        int sceneMovingPlatformCount;
        int sceneDoorCount;
        int scenePressurePlateCount;
        int sceneMovableBoxCount;
        int sceneCountdownCount;
        int sceneThirdPersonCameraCount;
        int sceneSoundHookCount;
        int sceneCompositionReady;
        const int SceneCompositionTotal = 10;
        int assetRoleReady;
        int assetRoleTotal;
        List<VARCOGameMakerWindow.UnifiedAssetMatchLine> gameMakerAssetMatches = new List<VARCOGameMakerWindow.UnifiedAssetMatchLine>();
        int gameMakerAssetMatchReady;
        int gameMakerAssetMatchTotal;
        bool gameMakerAssetMatchesCalculated;
        List<VARCOGameMakerWindow.OneClickCardReadinessLine> gameCardReadiness = new List<VARCOGameMakerWindow.OneClickCardReadinessLine>();
        bool gameCardReadinessCalculated;
        bool assetAnalysisCalculated;
        bool showPresetLibrary;
        bool showAssetTools;
        bool showPolishTools;
        bool showBuildTools = true;
        GenreRecommendation recommendedGame;
        bool buildEditorReady;
        bool buildSceneSaved;
        bool buildSceneChangesSaved;
        bool buildSceneIncluded;
        bool buildTargetSupported;
        int buildReady;
        const int BuildReadinessTotal = 5;
        VARCOEditorRepaintGate repaintGate;

        sealed class OneClickGameCard
        {
            public readonly VARCOGameMakerWindow.BlockTemplate Template;
            public readonly string Title;
            public readonly string Detail;
            public readonly Action BuildAction;

            public OneClickGameCard(VARCOGameMakerWindow.BlockTemplate template, string title, string detail, Action buildAction)
            {
                Template = template;
                Title = title;
                Detail = detail;
                BuildAction = buildAction;
            }
        }

        public static void Open()
        {
            var window = GetWindow<VARCOUnifiedStudioWindow>("VARCO 통합 스튜디오");
            window.minSize = new Vector2(560f, 680f);
            window.RefreshSummary();
        }

        void OnEnable()
        {
            repaintGate = new VARCOEditorRepaintGate(this);
            RefreshSummary();
        }

        void OnDisable()
        {
            if (repaintGate != null)
                repaintGate.Dispose();
            repaintGate = null;
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(8f);

            DrawTitle();
            DrawSummary();
            DrawOneClickSection();
            DrawBlockSection();
            DrawAssetSection();
            DrawPolishSection();
            DrawVerifySection();

            GUILayout.Space(8f);
            EditorGUILayout.EndScrollView();
        }

        void DrawTitle()
        {
            EditorGUILayout.LabelField("VARCO 통합 스튜디오", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "게임 제작, 에셋 적용, 블록 조립, HUD/사운드/애니메이션 연결, 검사와 Windows 빌드를 한 창에서 시작합니다.",
                MessageType.Info);
        }

        void DrawSummary()
        {
            DrawHeader("현재 상태");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("열린 씬", string.IsNullOrWhiteSpace(activeSceneName) ? "저장되지 않은 씬" : activeSceneName);
                EditorGUILayout.LabelField("에셋 분석", assetAnalysisCalculated ? "완료" : "대기");
                EditorGUILayout.LabelField("인식 가능한 3D 에셋", AssetCountLabel(modelCount));
                EditorGUILayout.LabelField("오디오 클립", AssetCountLabel(audioCount));
                EditorGUILayout.LabelField("애니메이션 클립", AssetCountLabel(animationCount));
                EditorGUILayout.LabelField("선택한 씬 오브젝트", selectedSceneObjects.ToString());
                EditorGUILayout.LabelField("선택한 프로젝트 에셋", selectedProjectAssets.ToString());

                GUILayout.Space(4f);
                EditorGUILayout.LabelField("플레이 준비도", readinessReady + " / " + ReadinessTotal);
                DrawStatusRow("게임 매니저", hasGameManager, "클리어/게임오버 상태 관리");
                DrawStatusRow("플레이어", hasPlayer, "조작, 체력, 카메라 대상");
                DrawStatusRow("VARCO HUD", hasHud, "체력, 목표, 수집, 타이머 표시");
                DrawStatusRow("목표/클리어 조건", hasGoal, "목표 지점, 퍼즐 목표, 웨이브 조건");
                DrawStatusRow("카메라", hasCamera, "게임 화면");
                DrawStatusRow("조명", hasLight, "기본 가시성");
                DrawStatusRow("BGM 오디오", hasBgm, "배경음 재생");
                DrawStatusRow("사운드 목록", hasSoundRegistry, "효과음/BGM 자동 연결");

                DrawSceneCompositionBox();

                GUILayout.Space(4f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("상태 새로고침", GUILayout.Height(26)))
                        RefreshSummary();
                    if (GUILayout.Button("에셋 분석 실행", GUILayout.Height(26)))
                        RunAndRefresh(RefreshAssetAnalysis, "VARCO 에셋 분석을 완료했습니다.");
                    if (GUILayout.Button("씬 건강 검사 열기", GUILayout.Height(26)))
                        RunAndRefresh(VARCOSceneHealthCheckWindow.Open, "씬 건강 검사를 열었습니다.");
                }
            }

            if (!string.IsNullOrWhiteSpace(lastMessage))
                EditorGUILayout.HelpBox(lastMessage, MessageType.None);
        }

        void DrawOneClickSection()
        {
            DrawHeader("1. 자동 제작");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "코딩이나 컴포넌트 설정 없이 현재 VARCO 에셋에 가장 어울리는 장르, 플레이어, 적, 아이템, HUD, 사운드, 비주얼을 자동으로 묶습니다.",
                    MessageType.None);

                DrawSmartActionBox();
                DrawRecommendedGameBox();

                if (DrawPrimaryButton("자동 전체 구성"))
                    RunOneClickFinishPass();

                if (GUILayout.Button("처음 사용자용 게임 만들기", GUILayout.Height(30)))
                    RunAndRefresh(VARCOGameMakerWindow.BuildBeginnerGameFromMenu, "처음 사용자용 게임 생성을 실행했습니다.");

                DrawGenreQuickStartGrid();

                showPresetLibrary = EditorGUILayout.Foldout(showPresetLibrary, "상세 게임 프리셋", true);
                if (showPresetLibrary)
                    DrawOneClickGameCards();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("가장 맞는 게임 만들기", GUILayout.Height(30)))
                        RunAndRefresh(VARCOGameMakerWindow.RunBestGameFromMenu, "가장 맞는 게임 만들기를 실행했습니다.");
                    if (GUILayout.Button("추천 기준 에셋 요청서", GUILayout.Height(30)))
                        RunAndRefresh(VARCOGameMakerWindow.GenerateBestAssetRequestFromMenu, "추천 게임 기준으로 부족한 에셋 요청서를 생성했습니다.");
                }

                if (GUILayout.Button("게임 메이커 열기", GUILayout.Height(30)))
                    RunAndRefresh(VARCOGameMakerWindow.Open, "게임 메이커를 열었습니다.");
            }
        }

        void DrawSmartActionBox()
        {
            var action = BuildSmartAction();
            GUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("지금 누를 버튼", EditorStyles.miniBoldLabel);
                EditorGUILayout.HelpBox(action.Detail, action.MessageType);
                using (new EditorGUI.DisabledScope(action.Kind == SmartActionKind.None))
                {
                    if (DrawPrimaryButton(action.ButtonLabel))
                        RunAndRefresh(() => ExecuteSmartAction(action.Kind), action.SuccessMessage);
                }
            }
        }

        void DrawRecommendedGameBox()
        {
            if (recommendedGame == null)
            {
                if (!assetAnalysisCalculated)
                    EditorGUILayout.HelpBox("에셋 추천은 `에셋 분석 실행` 후 표시됩니다. 빠른 제작은 아래 장르 버튼을 사용하세요.", MessageType.None);
                return;
            }

            GUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("현재 에셋 추천", EditorStyles.miniBoldLabel);
                var missingText = recommendedGame.Missing.Count == 0
                    ? "부족한 핵심 역할 없음"
                    : "부족: " + string.Join(", ", recommendedGame.Missing.Take(4));
                EditorGUILayout.HelpBox(
                    recommendedGame.Title + "  |  준비도 " + recommendedGame.ReadyGroups + " / " + recommendedGame.TotalGroups
                    + "\n" + recommendedGame.Detail
                    + "\n" + missingText,
                    recommendedGame.Missing.Count >= 3 ? MessageType.Warning : MessageType.Info);

                if (recommendedGame.Matched.Count > 0)
                    EditorGUILayout.LabelField("감지됨: " + string.Join(", ", recommendedGame.Matched.Take(5)), EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("추천대로 게임 만들기", GUILayout.Height(30)))
                        RunAndRefresh(RunRecommendedGenreOneClick, recommendedGame.Title + " 자동 제작을 실행했습니다.");
                    if (GUILayout.Button("게임 메이커 열기", GUILayout.Height(30)))
                        RunAndRefresh(VARCOGameMakerWindow.Open, "게임 메이커를 열었습니다.");
                }
            }
        }

        void DrawGenreQuickStartGrid()
        {
            GUILayout.Space(4f);
            EditorGUILayout.LabelField("장르 바로 시작", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("전투 아레나", GUILayout.Height(30)))
                    RunAndRefresh(VARCOGameMakerWindow.BuildArenaGameFromMenu, "전투 아레나 자동 제작을 실행했습니다.");
                if (GUILayout.Button("탐험 좀비", GUILayout.Height(30)))
                    RunAndRefresh(VARCOGameMakerWindow.BuildExplorationGameFromMenu, "탐험 좀비 게임 자동 제작을 실행했습니다.");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("퍼즐 방", GUILayout.Height(30)))
                    RunAndRefresh(VARCOGameMakerWindow.BuildPuzzleGameFromMenu, "퍼즐 방 자동 제작을 실행했습니다.");
                if (GUILayout.Button("플랫폼 코스", GUILayout.Height(30)))
                    RunAndRefresh(VARCOGameMakerWindow.BuildPlatformGameFromMenu, "플랫폼 코스 자동 제작을 실행했습니다.");
            }

            if (GUILayout.Button("전체 기능 샌드박스", GUILayout.Height(30)))
                RunAndRefresh(VARCOGameMakerWindow.BuildFullFeatureSandboxFromMenu, "전체 기능 샌드박스 자동 제작을 실행했습니다.");
        }

        void DrawOneClickGameCards()
        {
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("게임 프리셋", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "원하는 게임 느낌을 고르면 장르, 캐릭터 종류, 목표, HUD, 사운드, 애니메이션, 안전 보정을 함께 적용합니다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("카드별 VARCO 에셋 준비도 계산", GUILayout.Height(28f)))
                    CalculateGameCardReadiness();
                if (gameCardReadinessCalculated && GUILayout.Button("준비도 새로고침", GUILayout.Height(28f)))
                    CalculateGameCardReadiness();
            }

            if (DrawPrimaryButton("현재 에셋 추천 프리셋으로 제작"))
                RunAndRefresh(RunRecommendedCardOneClick, "현재 VARCO 에셋에 가장 잘 맞는 프리셋으로 자동 제작을 실행했습니다.");

            DrawRecommendedCardReadiness();

            for (var i = 0; i < OneClickGameCards.Length; i += 2)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawOneClickGameCard(OneClickGameCards[i]);
                    if (i + 1 < OneClickGameCards.Length)
                        DrawOneClickGameCard(OneClickGameCards[i + 1]);
                    else
                        GUILayout.FlexibleSpace();
                }
            }
        }

        void DrawOneClickGameCard(OneClickGameCard card)
        {
            var readiness = FindGameCardReadiness(card.Template);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                var title = readiness != null && readiness.recommended ? "추천: " + card.Title : card.Title;
                EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(card.Detail, EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(34f));
                DrawGameCardReadiness(readiness);
                if (GUILayout.Button("이 프리셋으로 제작", GUILayout.Height(26f)))
                    RunAndRefresh(card.BuildAction, card.Title + " 게임 프리셋 자동 제작을 실행했습니다.");
            }
        }

        void DrawGameCardReadiness(VARCOGameMakerWindow.OneClickCardReadinessLine readiness)
        {
            if (readiness == null)
            {
                EditorGUILayout.LabelField("준비도: 계산 전", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            EditorGUILayout.LabelField(
                readiness.stateLabel + " | " + readiness.readyRoles + " / " + readiness.totalRoles + " 직접 매칭",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(readiness.missingText, EditorStyles.wordWrappedMiniLabel);
        }

        void DrawRecommendedCardReadiness()
        {
            if (!gameCardReadinessCalculated)
            {
                EditorGUILayout.LabelField("준비도를 계산하면 현재 에셋에 가장 잘 맞는 카드를 표시합니다.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            var recommended = gameCardReadiness.FirstOrDefault(line => line != null && line.recommended);
            if (recommended == null)
                return;

            EditorGUILayout.HelpBox(
                "현재 에셋 추천 카드: " + recommended.title
                + "\n" + recommended.summary
                + "\n" + recommended.missingText,
                recommended.fallbackRoles == 0 ? MessageType.Info : MessageType.Warning);
        }

        void CalculateGameCardReadiness()
        {
            try
            {
                gameCardReadiness = VARCOGameMakerWindow.BuildOneClickCardReadinessSummaries(
                    OneClickGameCards.Select(card => card.Template));
                gameCardReadinessCalculated = true;
                lastMessage = "카드별 VARCO 에셋 준비도를 계산했습니다.";
            }
            catch (Exception ex)
            {
                gameCardReadiness = new List<VARCOGameMakerWindow.OneClickCardReadinessLine>();
                gameCardReadinessCalculated = false;
                lastMessage = "카드별 준비도 계산 실패: " + ex.Message;
            }
        }

        void RunRecommendedCardOneClick()
        {
            CalculateGameCardReadiness();
            var recommended = gameCardReadiness.FirstOrDefault(line => line != null && line.recommended);
            if (recommended == null)
            {
                VARCOGameMakerWindow.BuildBeginnerGameFromMenu();
                return;
            }

            var card = OneClickGameCards.FirstOrDefault(item => item.Template == recommended.template);
            if (card == null)
            {
                VARCOGameMakerWindow.BuildTemplateGame(recommended.template);
                return;
            }

            card.BuildAction();
        }

        VARCOGameMakerWindow.OneClickCardReadinessLine FindGameCardReadiness(VARCOGameMakerWindow.BlockTemplate template)
        {
            if (!gameCardReadinessCalculated)
                return null;

            return gameCardReadiness.FirstOrDefault(line => line != null && line.template == template);
        }

        void DrawBlockSection()
        {
            DrawHeader("2. 블록 코딩");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "선택 모델을 플레이어, 좀비, 문, 발판, 체크포인트, 환경 소품/엄폐물 같은 한글 블록으로 연결합니다.",
                    MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("한글 블록 조립기", GUILayout.Height(30)))
                        RunAndRefresh(VARCOBlockCodingBuilderWindow.Open, "한글 블록 조립기를 열었습니다.");
                    if (GUILayout.Button("선택 모델 기능 연결", GUILayout.Height(30)))
                        RunAndRefresh(VARCOAutoConnectorWindow.Open, "선택 모델 기능 연결 창을 열었습니다.");
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(selectedSceneObjects == 0))
                    {
                        if (GUILayout.Button("선택 모델 자동 판단", GUILayout.Height(30)))
                            RunAndRefresh(VARCOBlockCodingBuilderWindow.AutoConnectSelectionMenu, "선택 모델 자동 판단 연결을 실행했습니다.");
                    }

                    using (new EditorGUI.DisabledScope(selectedProjectAssets == 0))
                    {
                        if (GUILayout.Button("선택 에셋 배치 후 연결", GUILayout.Height(30)))
                            RunAndRefresh(VARCOBlockCodingBuilderWindow.PlaceAndAutoConnectSelectedAssetsMenu, "선택 에셋 배치 후 자동 연결을 실행했습니다.");
                    }
                }
            }
        }

        void DrawAssetSection()
        {
            showAssetTools = EditorGUILayout.Foldout(showAssetTools, "3. 에셋 분석과 요청", true);
            if (!showAssetTools)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "부족한 캐릭터, 좀비, 환경 소품, 사운드, BGM을 VarcoAI에서 바로 만들 수 있도록 요청서와 매칭 진단서를 생성합니다.",
                    MessageType.None);

                if (GUILayout.Button("에셋 분석 실행", GUILayout.Height(30)))
                    RunAndRefresh(RefreshAssetAnalysis, "VARCO 에셋 분석을 완료했습니다.");

                if (assetAnalysisCalculated)
                    DrawAssetReadinessBox();
                else
                    EditorGUILayout.HelpBox("창을 가볍게 열기 위해 에셋 전체 분석은 자동 실행하지 않습니다.", MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("부족한 에셋 요청서", GUILayout.Height(30)))
                        RunAndRefresh(VARCOGameMakerWindow.GenerateAssetRequestFromMenu, "부족한 에셋 요청서를 생성했습니다.");
                    if (GUILayout.Button("에셋 자동 매칭 진단서", GUILayout.Height(30)))
                        RunAndRefresh(VARCOGameMakerWindow.GenerateAssetMatchingReportFromMenu, "에셋 자동 매칭 진단서를 생성했습니다.");
                }
            }
        }

        void DrawPolishSection()
        {
            showPolishTools = EditorGUILayout.Foldout(showPolishTools, "4. 사운드와 애니메이션", true);
            if (!showPolishTools)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "선택한 모델에 애니메이션 컨트롤러를 연결하고, BGM/효과음/환경음을 씬과 오브젝트에 붙입니다.",
                    MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("애니메이션 자동 설정", GUILayout.Height(30)))
                        RunAndRefresh(VARCOAnimationSetupWindow.Open, "애니메이션 자동 설정 창을 열었습니다.");
                    if (GUILayout.Button("사운드 자동 연결", GUILayout.Height(30)))
                        RunAndRefresh(VARCOSoundConnectorWindow.Open, "사운드 자동 연결 창을 열었습니다.");
                }

                if (GUILayout.Button("수업용 전체 준비 실행", GUILayout.Height(28)))
                    RunAndRefresh(VARCOClassPipelineWindow.RunClassPipelineSetup, "수업용 전체 준비를 실행했습니다.");
            }
        }

        void DrawVerifySection()
        {
            showBuildTools = EditorGUILayout.Foldout(showBuildTools, "5. 보정, 검사, 빌드", true);
            if (!showBuildTools)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "플레이 전에 빠진 GameManager, HUD, BGM, 체크포인트 낙사 구역, 목표 조건을 점검하고 Windows 실행 파일까지 만듭니다.",
                    MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("현재 씬 자동 보정", GUILayout.Height(30)))
                        RunAndRefresh(VARCOGameMakerWindow.FixCurrentSceneFromMenu, "현재 씬 자동 보정을 실행했습니다.");
                    if (GUILayout.Button("플레이 준비 검사 리포트", GUILayout.Height(30)))
                        RunAndRefresh(VARCOGameMakerWindow.GeneratePlayReadyReportFromMenu, "플레이 준비 검사 리포트를 생성했습니다.");
                }

                if (GUILayout.Button("초보자 플레이 설명서", GUILayout.Height(30)))
                    RunAndRefresh(VARCOGameMakerWindow.GenerateBeginnerPlayGuideFromMenu, "초보자 플레이 설명서를 생성했습니다.");

                if (GUILayout.Button("프로젝트 사용 매뉴얼", GUILayout.Height(30)))
                    RunAndRefresh(VARCOGameMakerWindow.GenerateProjectManualFromMenu, "프로젝트 사용 매뉴얼을 생성했습니다.");

                if (GUILayout.Button("한글 UX 점검 리포트", GUILayout.Height(30)))
                    RunAndRefresh(VARCOGameMakerWindow.GenerateKoreanUxReportFromMenu, "한글 UX 점검 리포트를 생성했습니다.");

                if (GUILayout.Button("자동 보정 후 검사 리포트", GUILayout.Height(30)))
                    RunAndRefresh(RunFixAndReport, "자동 보정 후 플레이 준비 검사 리포트를 생성했습니다.");

                DrawBuildReadinessBox();

                if (GUILayout.Button("Windows 빌드 전 자동 준비", GUILayout.Height(30)))
                    RunAndRefresh(PrepareCurrentSceneForWindowsBuild, "현재 씬 저장과 Windows 빌드 설정 준비를 완료했습니다.");

                if (DrawPrimaryButton("Windows EXE 빌드"))
                    RunAndRefresh(VARCOGameMakerWindow.BuildWindowsExeFromMenu, "Windows EXE 빌드를 시작했습니다.");
            }
        }

        static bool DrawPrimaryButton(string label)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.35f, 0.78f, 0.55f);
            var clicked = GUILayout.Button(label, GUILayout.Height(38));
            GUI.backgroundColor = oldColor;
            return clicked;
        }

        static void DrawHeader(string title)
        {
            GUILayout.Space(8f);
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.72f, 0.88f, 1f) }
            };
            EditorGUILayout.LabelField(title, style);
        }

        void DrawBuildReadinessBox()
        {
            GUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Windows 빌드 준비도", buildReady + " / " + BuildReadinessTotal, EditorStyles.miniBoldLabel);
                DrawStatusRow("Unity 상태", buildEditorReady, "Play 모드 꺼짐, 컴파일 대기 없음");
                DrawStatusRow("씬 저장 위치", buildSceneSaved, "빌드할 씬 파일 경로");
                DrawStatusRow("씬 변경 저장", buildSceneChangesSaved, "저장되지 않은 변경 없음");
                DrawStatusRow("빌드 설정", buildSceneIncluded, "현재 씬이 Build Settings에 포함");
                DrawStatusRow("Windows 타겟", buildTargetSupported, "Windows 64비트 빌드 지원");
            }
        }

        void DrawSceneCompositionBox()
        {
            GUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("실제 씬 구성: " + sceneCompositionReady + " / " + SceneCompositionTotal, EditorStyles.miniBoldLabel);
                DrawCountStatusRow("플레이어 조작", scenePlayerCount, "PlayerController 연결");
                DrawCountStatusRow("적/전투", sceneEnemyCount + sceneWaveManagerCount, "적 AI/체력 또는 웨이브 매니저");
                DrawCountStatusRow("수집 아이템", sceneItemCount, "ItemPickup");
                DrawCountStatusRow("목표/클리어", sceneGoalCount, "목표, 플랫폼 목표, 퍼즐 목표");
                DrawCountStatusRow("체크포인트", sceneCheckpointCount, "리스폰 저장 지점");
                DrawCountStatusRow("낙사 안전망", sceneDeathZoneCount, "DeathZone");
                DrawCountStatusRow("위험 구역", sceneHazardCount, "피해/함정");
                DrawCountStatusRow("퍼즐 장치", sceneDoorCount + scenePressurePlateCount + sceneMovableBoxCount, "문, 발판, 상자");
                DrawCountStatusRow("카메라 추적", sceneThirdPersonCameraCount, "ThirdPersonCamera");
                DrawCountStatusRow("사운드 반응", sceneSoundHookCount, "SoundEvent 연결");

                if (sceneCompositionReady < SceneCompositionTotal)
                    EditorGUILayout.LabelField("부족한 구성은 자동 제작이나 현재 씬 자동 보정으로 채울 수 있습니다.", EditorStyles.wordWrappedMiniLabel);
            }
        }

        void DrawAssetReadinessBox()
        {
            GUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("역할별 에셋 준비도: " + assetRoleReady + " / " + assetRoleTotal, EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(
                    "검사 기준: 파일명, 폴더, 자식 이름, 메시, 재질, Animator, 애니메이션 클립",
                    EditorStyles.wordWrappedMiniLabel);
                foreach (var item in assetReadinessItems)
                    DrawAssetStatusRow(item);

                if (assetRoleReady < assetRoleTotal)
                    EditorGUILayout.HelpBox("부족한 역할은 아래 요청서 버튼으로 VarcoAI 제작 목록을 바로 뽑을 수 있습니다.", MessageType.None);

                DrawGameMakerAssetMatchBox();
            }
        }

        void DrawGameMakerAssetMatchBox()
        {
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("실제 자동 제작 에셋 매칭: " + gameMakerAssetMatchReady + " / " + gameMakerAssetMatchTotal, EditorStyles.miniBoldLabel);

            if (gameMakerAssetMatches == null || gameMakerAssetMatches.Count == 0)
            {
                EditorGUILayout.HelpBox("자동 제작 매칭 요약을 아직 계산하지 못했습니다. 버튼을 누르면 검사합니다.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField(
                gameMakerAssetMatchesCalculated
                    ? "게임 메이커가 실제로 붙일 후보와 선택 이유입니다."
                    : "창을 빠르게 열기 위해 깊은 매칭 계산은 버튼을 눌렀을 때 실행합니다.",
                EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("실제 매칭 계산", GUILayout.Height(24f)))
                {
                    RefreshGameMakerAssetMatches(force: true);
                    lastMessage = "실제 자동 제작 에셋 매칭 요약을 계산했습니다.";
                    RequestRepaint();
                }

                if (GUILayout.Button("매칭 진단서", GUILayout.Height(24f)))
                    RunAndRefresh(VARCOGameMakerWindow.GenerateAssetMatchingReportFromMenu, "에셋 자동 매칭 진단서를 생성했습니다.");
            }

            foreach (var match in gameMakerAssetMatches)
                DrawAssetMatchSummaryRow(match);
        }

        static void DrawAssetMatchSummaryRow(VARCOGameMakerWindow.UnifiedAssetMatchLine match)
        {
            if (match == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var oldColor = GUI.color;
                    GUI.color = MatchStateColor(match.stateCode);
                    GUILayout.Label(match.stateLabel, GUILayout.Width(42f));
                    GUI.color = oldColor;
                    EditorGUILayout.LabelField(match.roleLabel, GUILayout.Width(118f));
                    EditorGUILayout.LabelField(match.assetName, EditorStyles.miniBoldLabel);
                }

                EditorGUILayout.LabelField(match.detail, EditorStyles.wordWrappedMiniLabel);
                if (!string.IsNullOrWhiteSpace(match.assetPath))
                    EditorGUILayout.LabelField(match.assetPath, EditorStyles.miniLabel);
            }
        }

        static Color MatchStateColor(string stateCode)
        {
            switch (stateCode)
            {
                case "PASS":
                    return new Color(0.45f, 0.85f, 0.55f);
                case "WARN":
                    return new Color(1f, 0.78f, 0.25f);
                case "FALLBACK":
                    return new Color(1f, 0.55f, 0.35f);
                default:
                    return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        static void DrawCountStatusRow(string label, int count, string hint)
        {
            var ok = count > 0;
            using (new EditorGUILayout.HorizontalScope())
            {
                var oldColor = GUI.color;
                GUI.color = ok ? new Color(0.45f, 0.85f, 0.55f) : new Color(1f, 0.55f, 0.35f);
                GUILayout.Label(ok ? "있음" : "부족", GUILayout.Width(42f));
                GUI.color = oldColor;
                EditorGUILayout.LabelField(label, GUILayout.Width(118f));
                EditorGUILayout.LabelField(ok ? count + "개" : hint, EditorStyles.miniLabel);
            }
        }

        static void DrawStatusRow(string label, bool ok, string hint)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var oldColor = GUI.color;
                GUI.color = ok ? new Color(0.45f, 0.85f, 0.55f) : new Color(1f, 0.55f, 0.35f);
                GUILayout.Label(ok ? "완료" : "필요", GUILayout.Width(42f));
                GUI.color = oldColor;
                EditorGUILayout.LabelField(label, GUILayout.Width(118f));
                EditorGUILayout.LabelField(hint, EditorStyles.miniLabel);
            }
        }

        static void DrawAssetStatusRow(AssetReadinessItem item)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var oldColor = GUI.color;
                GUI.color = item.IsReady ? new Color(0.45f, 0.85f, 0.55f) : new Color(1f, 0.55f, 0.35f);
                GUILayout.Label(item.IsReady ? "있음" : "부족", GUILayout.Width(42f));
                GUI.color = oldColor;

                EditorGUILayout.LabelField(item.Label, GUILayout.Width(118f));
                var detail = item.IsReady
                    ? item.Count + "개 발견" + (item.InternalCount > 0 ? " / 내부 단서 " + item.InternalCount + "개" : "")
                    : item.Hint;
                EditorGUILayout.LabelField(detail, EditorStyles.miniLabel);
            }
        }

        void RunOneClickFinishPass()
        {
            RunAndRefresh(() =>
            {
                VARCOClassPipelineWindow.RunClassPipelineSetup();
                VARCOGameMakerWindow.BuildBeginnerGameFromMenu();
                VARCOGameMakerWindow.FixCurrentSceneFromMenu();
                VARCOGameMakerWindow.GeneratePlayReadyReportFromMenu();
            }, "자동 전체 구성을 실행했습니다. 게임 생성, 보정, 리포트까지 이어서 처리했습니다.");
        }

        SmartActionPlan BuildSmartAction()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return new SmartActionPlan(
                    SmartActionKind.None,
                    "Play 모드 종료 필요",
                    "Play 모드가 켜져 있습니다. 씬을 안전하게 바꾸려면 먼저 Unity Play 모드를 끄세요.",
                    "대기 중입니다.",
                    MessageType.Warning);
            }

            if (modelCount == 0 || assetRoleReady <= 2)
            {
                return new SmartActionPlan(
                    SmartActionKind.AssetRequest,
                    "부족한 에셋 요청서 만들기",
                    "현재 감지된 VARCO 모델/역할 에셋이 부족합니다. 먼저 VarcoAI에서 만들 에셋 목록을 한글 요청서로 뽑는 것이 좋습니다.",
                    "부족한 에셋 요청서를 생성했습니다.",
                    MessageType.Warning);
            }

            if (readinessReady < ReadinessTotal || sceneCompositionReady < SceneCompositionTotal)
            {
                return new SmartActionPlan(
                    SmartActionKind.FinishGame,
                    "지금 게임 완성하기",
                    "현재 씬에 빠진 플레이어, 목표, HUD, 사운드, 안전망이 남아 있습니다. 게임 생성, 자동 보정, 검사 리포트까지 이어서 실행합니다.",
                    "게임 생성, 자동 보정, 검사 리포트까지 실행했습니다.",
                    MessageType.Info);
            }

            if (buildReady < BuildReadinessTotal)
            {
                return new SmartActionPlan(
                    SmartActionKind.PrepareBuild,
                    "Windows 빌드 준비하기",
                    "게임 구성은 준비되어 보입니다. 현재 씬 저장과 Build Settings 포함을 자동으로 맞춰 Windows 빌드 전 상태로 만듭니다.",
                    "Windows 빌드 전 준비를 완료했습니다.",
                    MessageType.Info);
            }

            return new SmartActionPlan(
                SmartActionKind.Report,
                "완성 검사 리포트 만들기",
                "씬 구성과 빌드 준비가 모두 좋아 보입니다. 최종 점검 리포트를 만들어 수업/공유용 확인 자료로 남깁니다.",
                "완성 검사 리포트를 생성했습니다.",
                MessageType.None);
        }

        void ExecuteSmartAction(SmartActionKind kind)
        {
            switch (kind)
            {
                case SmartActionKind.AssetRequest:
                    VARCOGameMakerWindow.GenerateBestAssetRequestFromMenu();
                    break;
                case SmartActionKind.FinishGame:
                    VARCOClassPipelineWindow.RunClassPipelineSetup();
                    VARCOGameMakerWindow.BuildBeginnerGameFromMenu();
                    VARCOGameMakerWindow.FixCurrentSceneFromMenu();
                    VARCOGameMakerWindow.GeneratePlayReadyReportFromMenu();
                    break;
                case SmartActionKind.PrepareBuild:
                    PrepareCurrentSceneForWindowsBuild();
                    break;
                case SmartActionKind.Report:
                    VARCOGameMakerWindow.GeneratePlayReadyReportFromMenu();
                    break;
            }
        }

        static void RunFixAndReport()
        {
            VARCOGameMakerWindow.FixCurrentSceneFromMenu();
            VARCOGameMakerWindow.GeneratePlayReadyReportFromMenu();
        }

        static void PrepareCurrentSceneForWindowsBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("VARCO 통합 스튜디오", "빌드 준비 전에 Play 모드를 먼저 꺼주세요.", "확인");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrWhiteSpace(scene.path))
            {
                EnsureFolder("Assets/Scenes");
                EnsureFolder("Assets/Scenes/VARCO_AutoBuild");
                var path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/VARCO_AutoBuild/VARCO_Unified_AutoBuild.unity");
                if (!EditorSceneManager.SaveScene(scene, path))
                {
                    EditorUtility.DisplayDialog("VARCO 통합 스튜디오", "현재 씬을 자동 저장하지 못했습니다.", "확인");
                    return;
                }
            }
            else if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
            {
                EditorUtility.DisplayDialog("VARCO 통합 스튜디오", "현재 씬 변경 사항을 저장하지 못했습니다.", "확인");
                return;
            }

            AddActiveSceneToBuildSettings();
            AssetDatabase.SaveAssets();
        }

        static void AddActiveSceneToBuildSettings()
        {
            var path = SceneManager.GetActiveScene().path;
            if (string.IsNullOrWhiteSpace(path))
                return;

            var scenes = EditorBuildSettings.scenes.ToList();
            var index = scenes.FindIndex(scene => string.Equals(scene.path, path, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                scenes[index] = new EditorBuildSettingsScene(path, true);
            else
                scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                return;

            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        void RunAndRefresh(Action action, string successMessage)
        {
            try
            {
                action();
                lastMessage = successMessage;
            }
            catch (Exception ex)
            {
                lastMessage = "실행 중 문제가 생겼습니다: " + ex.Message;
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("VARCO 통합 스튜디오", lastMessage, "확인");
            }

            RefreshSummary();
        }

        void RefreshSummary()
        {
            var scene = SceneManager.GetActiveScene();
            activeSceneName = string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path;

            selectedSceneObjects = Selection.gameObjects.Count(go => go && !EditorUtility.IsPersistent(go));
            selectedProjectAssets = Selection.objects.Count(obj => obj && EditorUtility.IsPersistent(obj));
            RefreshReadiness();
            RefreshSceneComposition();
            RefreshBuildReadiness();

            RequestRepaint();
        }

        void RequestRepaint(bool immediate = false)
        {
            if (repaintGate == null)
                repaintGate = new VARCOEditorRepaintGate(this);
            repaintGate.Request(immediate);
        }

        void RefreshAssetAnalysis()
        {
            modelCount = CountAssetsInRoots("t:GameObject", GameObjectAssetRoots);
            audioCount = CountAssets("t:AudioClip", "Assets");
            animationCount = CountAssetsInRoots("t:AnimationClip", AnimationAssetRoots);
            RefreshAssetReadiness();
            assetAnalysisCalculated = true;
        }

        void RefreshReadiness()
        {
            hasGameManager = UnityEngine.Object.FindFirstObjectByType<VWS.GameManager>() != null;
            hasHud = UnityEngine.Object.FindFirstObjectByType<VWS.VARCOGameHUD>() != null;
            hasPlayer = GameObject.FindGameObjectWithTag("Player") != null
                || UnityEngine.Object.FindFirstObjectByType<VWS.PlayerController_ThirdPerson>() != null
                || UnityEngine.Object.FindFirstObjectByType<VWS.PlayerController_Platform>() != null;
            hasGoal = UnityEngine.Object.FindFirstObjectByType<VWS.GoalTrigger>() != null
                || UnityEngine.Object.FindFirstObjectByType<VWS.PlatformGoal>() != null
                || UnityEngine.Object.FindFirstObjectByType<VWS.PuzzleGoal>() != null
                || UnityEngine.Object.FindFirstObjectByType<VWS.WaveManager>() != null;
            hasCamera = Camera.main != null || UnityEngine.Object.FindFirstObjectByType<Camera>() != null;
            hasLight = UnityEngine.Object.FindFirstObjectByType<Light>() != null;
            hasBgm = HasBgmAudioSource();
            hasSoundRegistry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(RegistryPath) != null;

            readinessReady = new[]
            {
                hasGameManager,
                hasPlayer,
                hasHud,
                hasGoal,
                hasCamera,
                hasLight,
                hasBgm,
                hasSoundRegistry
            }.Count(v => v);
        }

        void RefreshSceneComposition()
        {
            var thirdPersonPlayers = CountSceneComponents<VWS.PlayerController_ThirdPerson>();
            var platformPlayers = CountSceneComponents<VWS.PlayerController_Platform>();
            scenePlayerCount = thirdPersonPlayers + platformPlayers;
            sceneEnemyCount = CountSceneComponents<VWS.EnemyAI_NavMesh>() + CountSceneComponents<VWS.EnemyHealth>();
            sceneItemCount = CountSceneComponents<VWS.ItemPickup>();
            sceneHealthPickupCount = CountSceneComponents<VWS.HealthPickup>();
            sceneGoalCount = CountSceneComponents<VWS.GoalTrigger>()
                + CountSceneComponents<VWS.PlatformGoal>()
                + CountSceneComponents<VWS.PuzzleGoal>();
            sceneWaveManagerCount = CountSceneComponents<VWS.WaveManager>();
            sceneCheckpointCount = CountSceneComponents<VWS.Checkpoint>();
            sceneDeathZoneCount = CountSceneComponents<VWS.DeathZone>();
            sceneHazardCount = CountSceneComponents<VWS.HazardZone>();
            sceneMovingPlatformCount = CountSceneComponents<VWS.MovingPlatform>();
            sceneDoorCount = CountSceneComponents<VWS.DoorController>();
            scenePressurePlateCount = CountSceneComponents<VWS.PressurePlate>();
            sceneMovableBoxCount = CountSceneComponents<VWS.MovableBox>();
            sceneCountdownCount = CountSceneComponents<VWS.CountdownTimer>();
            sceneThirdPersonCameraCount = CountSceneComponents<VWS.ThirdPersonCamera>();
            sceneSoundHookCount = CountSceneComponents<VWS.SoundEventEmitter>()
                + CountSceneComponents<VWS.SoundEventTrigger>();

            sceneCompositionReady = new[]
            {
                scenePlayerCount > 0,
                sceneEnemyCount + sceneWaveManagerCount > 0,
                sceneItemCount > 0 || sceneHealthPickupCount > 0,
                sceneGoalCount > 0,
                sceneCheckpointCount > 0,
                sceneDeathZoneCount > 0,
                sceneHazardCount > 0,
                sceneMovingPlatformCount + sceneDoorCount + scenePressurePlateCount + sceneMovableBoxCount > 0,
                sceneThirdPersonCameraCount > 0,
                sceneSoundHookCount > 0
            }.Count(v => v);
        }

        void RefreshAssetReadiness()
        {
            var roots = ExistingFolders(GameObjectAssetRoots);
            var evidence = roots.Length == 0
                ? Array.Empty<AssetEvidence>()
                : AssetDatabase.FindAssets("t:GameObject", roots)
                    .Distinct()
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(BuildAssetEvidence)
                    .ToArray();

            foreach (var item in assetReadinessItems)
            {
                item.Count = evidence.Count(asset => ContainsAny(asset.FullText, item.Keywords));
                item.InternalCount = evidence.Count(asset => asset.HasInternalEvidence && ContainsAny(asset.InternalText, item.Keywords));
            }

            assetRoleTotal = assetReadinessItems.Length;
            assetRoleReady = assetReadinessItems.Count(item => item.IsReady);
            internalEvidenceAssetCount = evidence.Count(asset => asset.HasInternalEvidence);
            visualModelAssetCount = evidence.Count(asset => asset.HasVisuals);
            animatorAssetCount = evidence.Count(asset => asset.HasAnimator);
            prefabAudioSourceCount = evidence.Count(asset => asset.HasAudioSource);
            RefreshGameMakerAssetMatches();
            recommendedGame = BuildGenreRecommendation();
        }

        void RefreshGameMakerAssetMatches(bool force = false)
        {
            if (!force)
            {
                if (gameMakerAssetMatchesCalculated && gameMakerAssetMatches != null && gameMakerAssetMatches.Count > 0)
                {
                    gameMakerAssetMatchTotal = gameMakerAssetMatches.Count;
                    gameMakerAssetMatchReady = gameMakerAssetMatches.Count(match => match != null && match.stateCode == "PASS");
                    return;
                }

                gameMakerAssetMatchesCalculated = false;
                gameMakerAssetMatches = new List<VARCOGameMakerWindow.UnifiedAssetMatchLine>
                {
                    new VARCOGameMakerWindow.UnifiedAssetMatchLine
                    {
                        stateCode = "WARN",
                        stateLabel = "대기",
                        roleLabel = "실제 매칭",
                        assetName = "버튼으로 계산",
                        detail = "`실제 매칭 계산`을 누르면 현재 VARCO 에셋 기준으로 플레이어, 적, 아이템, 목표 등에 어떤 프리팹이 붙는지 보여줍니다.",
                        candidateCount = 0,
                        hasAsset = false
                    }
                };
                gameMakerAssetMatchTotal = 0;
                gameMakerAssetMatchReady = 0;
                return;
            }

            try
            {
                gameMakerAssetMatches = VARCOGameMakerWindow.BuildUnifiedAssetMatchSummary(8)
                    ?? new List<VARCOGameMakerWindow.UnifiedAssetMatchLine>();
                gameMakerAssetMatchesCalculated = true;
            }
            catch (Exception ex)
            {
                gameMakerAssetMatchesCalculated = false;
                gameMakerAssetMatches = new List<VARCOGameMakerWindow.UnifiedAssetMatchLine>
                {
                    new VARCOGameMakerWindow.UnifiedAssetMatchLine
                    {
                        stateCode = "WARN",
                        stateLabel = "확인",
                        roleLabel = "매칭 요약",
                        assetName = "계산 실패",
                        detail = "GameMaker 매칭 요약을 계산하지 못했습니다: " + ex.Message,
                        candidateCount = 0,
                        hasAsset = false
                    }
                };
            }

            gameMakerAssetMatchTotal = gameMakerAssetMatches.Count;
            gameMakerAssetMatchReady = gameMakerAssetMatches.Count(match => match != null && match.stateCode == "PASS");
        }

        void RunRecommendedGenreOneClick()
        {
            if (recommendedGame == null)
            {
                VARCOGameMakerWindow.RunBestGameFromMenu();
                return;
            }

            switch (recommendedGame.Genre)
            {
                case VWS.GenreType.Arena:
                    VARCOGameMakerWindow.BuildArenaGameFromMenu();
                    break;
                case VWS.GenreType.Puzzle:
                    VARCOGameMakerWindow.BuildPuzzleGameFromMenu();
                    break;
                case VWS.GenreType.Platform:
                    VARCOGameMakerWindow.BuildPlatformGameFromMenu();
                    break;
                default:
                    VARCOGameMakerWindow.BuildExplorationGameFromMenu();
                    break;
            }
        }

        static bool HasBgmAudioSource()
        {
            var bgm = GameObject.Find("VW_Audio_BGM") ?? GameObject.Find("VARCO_Audio_BGM");
            if (bgm && bgm.GetComponent<AudioSource>())
                return true;

            return UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                .Any(source => source && source.loop && source.playOnAwake && source.clip);
        }

        void RefreshBuildReadiness()
        {
            var scene = SceneManager.GetActiveScene();
            var scenePath = scene.path;
            buildEditorReady = !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
            buildSceneSaved = !string.IsNullOrWhiteSpace(scenePath);
            buildSceneChangesSaved = !scene.isDirty;
            buildSceneIncluded = buildSceneSaved && EditorBuildSettings.scenes.Any(s => s.enabled && s.path == scenePath);
            buildTargetSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

            buildReady = new[]
            {
                buildEditorReady,
                buildSceneSaved,
                buildSceneChangesSaved,
                buildSceneIncluded,
                buildTargetSupported
            }.Count(v => v);
        }

        static int CountAssets(string filter, string root)
        {
            return AssetDatabase.IsValidFolder(root)
                ? AssetDatabase.FindAssets(filter, new[] { root }).Length
                : 0;
        }

        static int CountAssetsInRoots(string filter, string[] roots)
        {
            var validRoots = ExistingFolders(roots);
            return validRoots.Length == 0
                ? 0
                : AssetDatabase.FindAssets(filter, validRoots).Distinct().Count();
        }

        string AssetCountLabel(int count)
        {
            return assetAnalysisCalculated ? count.ToString() : "분석 전";
        }

        static int CountSceneComponents<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;
        }

        static string[] ExistingFolders(string[] roots)
        {
            return roots
                .Where(root => !string.IsNullOrWhiteSpace(root) && AssetDatabase.IsValidFolder(root))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
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

        static AssetEvidence BuildAssetEvidence(string path)
        {
            var pathText = NormalizeEvidence(path + " " + Path.GetFileNameWithoutExtension(path));
            var internalParts = new List<string>();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var evidence = new AssetEvidence
            {
                Path = path,
                PathText = pathText,
                InternalText = string.Empty,
                FullText = pathText
            };

            if (!asset)
                return evidence;

            foreach (var transform in asset.GetComponentsInChildren<Transform>(true).Take(80))
            {
                if (transform && transform != asset.transform)
                    AddInternalEvidence(internalParts, transform.name, 180);
            }

            var renderers = asset.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer && !(renderer is ParticleSystemRenderer))
                .Take(36)
                .ToArray();
            evidence.HasVisuals = renderers.Length > 0;
            foreach (var renderer in renderers)
            {
                AddInternalEvidence(internalParts, renderer.name, 180);
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material)
                        AddInternalEvidence(internalParts, material.name, 180);
                }
            }

            foreach (var meshFilter in asset.GetComponentsInChildren<MeshFilter>(true).Take(24))
            {
                if (meshFilter && meshFilter.sharedMesh)
                    AddInternalEvidence(internalParts, meshFilter.sharedMesh.name, 180);
            }

            foreach (var skinned in asset.GetComponentsInChildren<SkinnedMeshRenderer>(true).Take(24))
            {
                if (!skinned)
                    continue;

                AddInternalEvidence(internalParts, skinned.name, 180);
                if (skinned.sharedMesh)
                    AddInternalEvidence(internalParts, skinned.sharedMesh.name, 180);
            }

            var animators = asset.GetComponentsInChildren<Animator>(true).Take(12).ToArray();
            evidence.HasAnimator = animators.Length > 0;
            foreach (var animator in animators)
            {
                AddInternalEvidence(internalParts, animator.name, 180);
                if (!animator.runtimeAnimatorController)
                    continue;

                AddInternalEvidence(internalParts, animator.runtimeAnimatorController.name, 180);
                foreach (var clip in animator.runtimeAnimatorController.animationClips.Take(16))
                {
                    if (clip)
                        AddInternalEvidence(internalParts, clip.name, 180);
                }
            }

            evidence.HasAudioSource = asset.GetComponentsInChildren<AudioSource>(true).Any(source => source);
            foreach (var source in asset.GetComponentsInChildren<AudioSource>(true).Take(12))
            {
                AddInternalEvidence(internalParts, source.name, 180);
                if (source.clip)
                    AddInternalEvidence(internalParts, source.clip.name, 180);
            }

            evidence.InternalText = NormalizeEvidence(string.Join(" ", internalParts));
            evidence.FullText = pathText + " " + evidence.InternalText;
            evidence.HasInternalEvidence = !string.IsNullOrWhiteSpace(evidence.InternalText);
            return evidence;
        }

        static void AddInternalEvidence(List<string> parts, string value, int maxCount)
        {
            if (parts == null || parts.Count >= maxCount || string.IsNullOrWhiteSpace(value))
                return;

            var normalized = NormalizeEvidence(value);
            if (string.IsNullOrEmpty(normalized) || IsGenericInternalEvidenceName(normalized))
                return;

            if (parts.Any(existing => string.Equals(NormalizeEvidence(existing), normalized, StringComparison.Ordinal)))
                return;

            parts.Add(value);
        }

        static string NormalizeEvidence(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace("\\", "/").Replace(" ", "_").ToLowerInvariant();
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

        GenreRecommendation BuildGenreRecommendation()
        {
            return new[]
            {
                ScoreArenaRecommendation(),
                ScoreExplorationRecommendation(),
                ScorePuzzleRecommendation(),
                ScorePlatformRecommendation()
            }
                .OrderByDescending(score => score.Score)
                .ThenByDescending(score => score.ReadyGroups)
                .First();
        }

        GenreRecommendation ScoreArenaRecommendation()
        {
            var score = new GenreRecommendation(VWS.GenreType.Arena, "전투 아레나", "플레이어와 적/보스가 준비되어 있을 때 가장 빠르게 완성되는 전투 게임입니다.");
            AddRoleGroup(score, "플레이어", 120, AssetReadinessRole.CommonPlayer);
            AddRoleGroup(score, "적/보스", 130, AssetReadinessRole.EnemyBoss, AssetReadinessRole.DroneEnemy);
            AddRoleGroup(score, "회복 아이템", 55, AssetReadinessRole.HealthPickup);
            AddRoleGroup(score, "환경 소품/엄폐물", 70, AssetReadinessRole.EnvironmentProp);
            AddRoleGroup(score, "목표/포탈", 45, AssetReadinessRole.DoorGoal);
            return score;
        }

        GenreRecommendation ScoreExplorationRecommendation()
        {
            var score = new GenreRecommendation(VWS.GenreType.Exploration, "탐험 좀비", "탐험 캐릭터, 좀비, 아이템, 환경 소품이 있으면 퀘스트/생존 게임으로 묶기 좋습니다.");
            AddRoleGroup(score, "탐험 플레이어", 130, AssetReadinessRole.ExplorationPlayer, AssetReadinessRole.CommonPlayer);
            AddRoleGroup(score, "좀비", 140, AssetReadinessRole.Zombie);
            AddRoleGroup(score, "수집 아이템", 85, AssetReadinessRole.Collectible);
            AddRoleGroup(score, "문/목표/포탈", 65, AssetReadinessRole.DoorGoal);
            AddRoleGroup(score, "환경 소품/엄폐물", 90, AssetReadinessRole.EnvironmentProp);
            AddRoleGroup(score, "체크포인트", 45, AssetReadinessRole.Checkpoint);
            return score;
        }

        GenreRecommendation ScorePuzzleRecommendation()
        {
            var score = new GenreRecommendation(VWS.GenreType.Puzzle, "퍼즐 방", "문, 발판, 스위치, 상자 같은 장치가 보이면 방탈출/문 열기 퍼즐에 잘 맞습니다.");
            AddRoleGroup(score, "퍼즐 플레이어", 120, AssetReadinessRole.PuzzlePlayer, AssetReadinessRole.CommonPlayer);
            AddRoleGroup(score, "퍼즐 장치", 140, AssetReadinessRole.PuzzleDevice);
            AddRoleGroup(score, "문/목표/포탈", 110, AssetReadinessRole.DoorGoal);
            AddRoleGroup(score, "수집 아이템", 60, AssetReadinessRole.Collectible);
            AddRoleGroup(score, "체크포인트", 40, AssetReadinessRole.Checkpoint);
            AddRoleGroup(score, "환경 소품/엄폐물", 55, AssetReadinessRole.EnvironmentProp);
            return score;
        }

        GenreRecommendation ScorePlatformRecommendation()
        {
            var score = new GenreRecommendation(VWS.GenreType.Platform, "플랫폼 코스", "플랫폼/우주 캐릭터, 드론, 아이템, 체크포인트가 있으면 점프 코스 게임에 잘 맞습니다.");
            AddRoleGroup(score, "플랫폼 플레이어", 130, AssetReadinessRole.PlatformPlayer, AssetReadinessRole.CommonPlayer);
            AddRoleGroup(score, "드론/기계 적", 95, AssetReadinessRole.DroneEnemy, AssetReadinessRole.EnemyBoss);
            AddRoleGroup(score, "체크포인트", 100, AssetReadinessRole.Checkpoint);
            AddRoleGroup(score, "수집 아이템", 80, AssetReadinessRole.Collectible);
            AddRoleGroup(score, "문/목표/포탈", 65, AssetReadinessRole.DoorGoal);
            AddRoleGroup(score, "환경 소품/엄폐물", 55, AssetReadinessRole.EnvironmentProp);
            return score;
        }

        void AddRoleGroup(GenreRecommendation score, string label, int weight, params AssetReadinessRole[] roles)
        {
            var count = roles.Sum(CountReadyRole);
            score.TotalGroups++;
            if (count > 0)
            {
                score.Score += weight + Mathf.Min(count, 8);
                score.ReadyGroups++;
                score.Matched.Add(label + " " + count + "개");
                return;
            }

            score.Score -= Mathf.Max(20, weight / 2);
            score.Missing.Add(label);
        }

        int CountReadyRole(AssetReadinessRole role)
        {
            var item = assetReadinessItems.FirstOrDefault(candidate => candidate.Role == role);
            return item != null ? item.Count : 0;
        }

        static AssetReadinessItem[] CreateAssetReadinessItems()
        {
            return new[]
            {
                new AssetReadinessItem(AssetReadinessRole.CommonPlayer, "공통 플레이어", "플레이어/캐릭터 에셋 필요", "player", "hero", "character", "avatar", "플레이어", "캐릭터", "주인공", "영웅"),
                new AssetReadinessItem(AssetReadinessRole.ExplorationPlayer, "탐험 플레이어", "explorer/탐험가 에셋 권장", "explorer", "adventurer", "exploration", "탐험", "탐험가", "모험가"),
                new AssetReadinessItem(AssetReadinessRole.PuzzlePlayer, "퍼즐 플레이어", "puzzle/room용 캐릭터 권장", "puzzle_player", "puzzleplayer", "puzzle", "room_player", "퍼즐", "방탈출"),
                new AssetReadinessItem(AssetReadinessRole.PlatformPlayer, "플랫폼 플레이어", "platform/space 캐릭터 권장", "platform_player", "platformplayer", "platform", "astronaut", "space", "플랫폼", "우주"),
                new AssetReadinessItem(AssetReadinessRole.EnemyBoss, "적/보스", "전투용 적 에셋 필요", "enemy", "boss", "orc", "monster", "mob", "적", "보스", "몬스터"),
                new AssetReadinessItem(AssetReadinessRole.Zombie, "좀비", "탐험 좀비 장르용 에셋", "zombie", "undead", "좀비", "언데드"),
                new AssetReadinessItem(AssetReadinessRole.DroneEnemy, "드론/기계 적", "SF/플랫폼용 적 에셋", "drone", "robot", "turret", "mech", "드론", "로봇", "포탑"),
                new AssetReadinessItem(AssetReadinessRole.Collectible, "수집 아이템", "열쇠/보물/코인 에셋 필요", "item", "pickup", "collectible", "coin", "gem", "key", "treasure", "crystal", "아이템", "수집", "열쇠", "보물", "코인", "보석"),
                new AssetReadinessItem(AssetReadinessRole.HealthPickup, "회복 아이템", "체력 회복 에셋 권장", "health", "heal", "potion", "hp", "medkit", "회복", "포션", "체력"),
                new AssetReadinessItem(AssetReadinessRole.DoorGoal, "문/목표/포탈", "클리어 지점 에셋 필요", "door", "gate", "goal", "finish", "exit", "portal", "flag", "문", "게이트", "목표", "출구", "포탈"),
                new AssetReadinessItem(AssetReadinessRole.PuzzleDevice, "퍼즐 장치", "발판/스위치/상자 에셋 권장", "pressure", "plate", "switch", "button", "lever", "crate", "box", "push", "발판", "스위치", "상자"),
                new AssetReadinessItem(AssetReadinessRole.Checkpoint, "체크포인트", "리스폰 지점 에셋 권장", "checkpoint", "savepoint", "respawn", "beacon", "체크포인트", "리스폰", "세이브"),
                new AssetReadinessItem(AssetReadinessRole.EnvironmentProp, "환경 소품/엄폐물", "나무/바위/장애물 에셋 권장", "obstacle", "cover", "prop", "tree", "plant", "rock", "wall", "environment", "scenery", "소품", "엄폐", "장애물", "나무", "바위", "환경")
            };
        }

        enum AssetReadinessRole
        {
            CommonPlayer,
            ExplorationPlayer,
            PuzzlePlayer,
            PlatformPlayer,
            EnemyBoss,
            Zombie,
            DroneEnemy,
            Collectible,
            HealthPickup,
            DoorGoal,
            PuzzleDevice,
            Checkpoint,
            EnvironmentProp
        }

        class GenreRecommendation
        {
            public GenreRecommendation(VWS.GenreType genre, string title, string detail)
            {
                Genre = genre;
                Title = title;
                Detail = detail;
            }

            public VWS.GenreType Genre { get; }
            public string Title { get; }
            public string Detail { get; }
            public int Score { get; set; }
            public int ReadyGroups { get; set; }
            public int TotalGroups { get; set; }
            public List<string> Matched { get; } = new List<string>();
            public List<string> Missing { get; } = new List<string>();
        }

        enum SmartActionKind
        {
            None,
            AssetRequest,
            FinishGame,
            PrepareBuild,
            Report
        }

        class SmartActionPlan
        {
            public SmartActionPlan(SmartActionKind kind, string buttonLabel, string detail, string successMessage, MessageType messageType)
            {
                Kind = kind;
                ButtonLabel = buttonLabel;
                Detail = detail;
                SuccessMessage = successMessage;
                MessageType = messageType;
            }

            public SmartActionKind Kind { get; }
            public string ButtonLabel { get; }
            public string Detail { get; }
            public string SuccessMessage { get; }
            public MessageType MessageType { get; }
        }

        class AssetReadinessItem
        {
            public AssetReadinessItem(AssetReadinessRole role, string label, string hint, params string[] keywords)
            {
                Role = role;
                Label = label;
                Hint = hint;
                Keywords = keywords;
            }

            public AssetReadinessRole Role { get; }
            public string Label { get; }
            public string Hint { get; }
            public string[] Keywords { get; }
            public int Count { get; set; }
            public int InternalCount { get; set; }
            public bool IsReady => Count > 0;
        }

        class AssetEvidence
        {
            public string Path { get; set; }
            public string PathText { get; set; }
            public string InternalText { get; set; }
            public string FullText { get; set; }
            public bool HasInternalEvidence { get; set; }
            public bool HasVisuals { get; set; }
            public bool HasAnimator { get; set; }
            public bool HasAudioSource { get; set; }
        }
    }

    sealed class VARCOEditorRepaintGate
    {
        readonly EditorWindow owner;
        readonly double intervalSeconds;
        double nextAllowedRepaintTime;
        bool repaintQueued;

        public VARCOEditorRepaintGate(EditorWindow owner, double intervalSeconds = 0.25)
        {
            this.owner = owner;
            this.intervalSeconds = intervalSeconds;
        }

        public void Request(bool immediate = false)
        {
            if (!owner)
                return;

            var now = EditorApplication.timeSinceStartup;
            if (immediate || now >= nextAllowedRepaintTime)
            {
                CancelQueuedTick();
                nextAllowedRepaintTime = now + intervalSeconds;
                owner.Repaint();
                return;
            }

            if (repaintQueued)
                return;

            repaintQueued = true;
            EditorApplication.update += Tick;
        }

        public void Dispose()
        {
            CancelQueuedTick();
        }

        void Tick()
        {
            if (!owner)
            {
                CancelQueuedTick();
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (now < nextAllowedRepaintTime)
                return;

            CancelQueuedTick();
            nextAllowedRepaintTime = now + intervalSeconds;
            owner.Repaint();
        }

        void CancelQueuedTick()
        {
            if (!repaintQueued)
                return;

            repaintQueued = false;
            EditorApplication.update -= Tick;
        }
    }
}
#endif
