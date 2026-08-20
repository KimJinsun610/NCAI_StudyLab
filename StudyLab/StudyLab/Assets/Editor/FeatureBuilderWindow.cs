#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VWS = VARCO_Workshop;

namespace VARCO_Workshop.Editor
{
    /// <summary>
    /// VARCO / 기능 블록 빌더 전용 창.
    /// 기능 블록을 체크박스로 선택해 씬에 즉시 배치·자동 연결합니다.
    /// </summary>
    public class FeatureBuilderWindow : EditorWindow
    {
        // ── 기존 기능 5종 ──────────────────────────────────────────
        bool _door       = false;
        bool _wave       = false;
        bool _item       = false;
        bool _platform   = false;
        bool _checkpoint = false;

        // ── 신규 기능 4종 ──────────────────────────────────────────
        bool _healthPickup = false;
        bool _hazardZone   = false;
        bool _countdown    = false;
        bool _lockedDoor   = false;   // MultiKey Door: ItemPickup N개 모아야 열리는 문
        bool _movableBox   = false;
        bool _arenaCover   = false;

        // ── Options ────────────────────────────────────────────────
        VWS.CompletionCondition _winCondition = VWS.CompletionCondition.ReachGoal;
        int   _waveEnemyCount  = 3;
        int   _itemCount       = 3;
        int   _healAmount      = 25;
        int   _hazardDPS       = 15;
        float _countdownSec    = 60f;
        int   _lockedDoorKeys  = 2;   // 잠긴 문이 열리기 위한 아이템 수

        // ── UI ─────────────────────────────────────────────────────
        Vector2 _scroll;

        GameObject _connectSelected;
        RuntimeAnimatorController _connectAnimator;
        ConnectGenre _connectGenre = ConnectGenre.Exploration;
        ConnectFeatureBlock _connectBlock = ConnectFeatureBlock.Player;
        DoorConnectPart _connectDoorPart = DoorConnectPart.Door;
        bool _connectEnemyToWave = true;
        bool _saveEnemyAsPrefab = true;
        int _connectWaveIndex = 0;
        int _connectRequiredItems = 0;
        int _connectHealAmount = 25;
        int _connectHazardDps = 15;
        float _connectPlatformDistance = 4f;
        float _connectPlatformSpeed = 1.2f;
        VARCOAutoConnectorWindow.CameraViewPreset _connectCameraView = VARCOAutoConnectorWindow.CameraViewPreset.SideView;

        enum ConnectGenre
        {
            CombatArena,
            Exploration,
            Puzzle,
            Platformer,
            Common
        }

        enum ConnectFeatureBlock
        {
            Player,
            Door,
            EnemyWave,
            ItemPickup,
            MovingPlatform,
            Checkpoint,
            HealthPickup,
            HazardZone,
            Countdown,
            LockedDoor,
            MovableBox,
            ArenaCover
        }

        enum DoorConnectPart
        {
            Door,
            PressurePlate
        }

        static readonly ConnectFeatureBlock[] CombatArenaBlocks =
        {
            ConnectFeatureBlock.Player,
            ConnectFeatureBlock.EnemyWave,
            ConnectFeatureBlock.HealthPickup,
            ConnectFeatureBlock.ArenaCover,
            ConnectFeatureBlock.Countdown
        };

        static readonly ConnectFeatureBlock[] ExplorationBlocks =
        {
            ConnectFeatureBlock.Player,
            ConnectFeatureBlock.EnemyWave,
            ConnectFeatureBlock.ItemPickup,
            ConnectFeatureBlock.Checkpoint,
            ConnectFeatureBlock.HazardZone,
            ConnectFeatureBlock.ArenaCover,
            ConnectFeatureBlock.LockedDoor
        };

        static readonly ConnectFeatureBlock[] PuzzleBlocks =
        {
            ConnectFeatureBlock.Player,
            ConnectFeatureBlock.Door,
            ConnectFeatureBlock.LockedDoor,
            ConnectFeatureBlock.MovableBox,
            ConnectFeatureBlock.ItemPickup,
            ConnectFeatureBlock.HazardZone,
            ConnectFeatureBlock.Checkpoint,
            ConnectFeatureBlock.ArenaCover
        };

        static readonly ConnectFeatureBlock[] PlatformerBlocks =
        {
            ConnectFeatureBlock.Player,
            ConnectFeatureBlock.MovingPlatform,
            ConnectFeatureBlock.Checkpoint,
            ConnectFeatureBlock.HazardZone,
            ConnectFeatureBlock.ItemPickup,
            ConnectFeatureBlock.ArenaCover
        };

        static readonly ConnectFeatureBlock[] CommonBlocks =
        {
            ConnectFeatureBlock.Player,
            ConnectFeatureBlock.HealthPickup,
            ConnectFeatureBlock.Countdown,
            ConnectFeatureBlock.ItemPickup,
            ConnectFeatureBlock.Checkpoint,
            ConnectFeatureBlock.ArenaCover
        };

        static readonly GUIContent[] GenreLabels =
        {
            new GUIContent("전투 아레나"),
            new GUIContent("탐험"),
            new GUIContent("퍼즐"),
            new GUIContent("플랫폼 (초보 추천)"),
            new GUIContent("공통")
        };

        static readonly GUIContent[] DoorPartLabels =
        {
            new GUIContent("문 모델"),
            new GUIContent("발판 모델")
        };

        static readonly VWS.CompletionCondition[] CompletionConditionOptions =
        {
            VWS.CompletionCondition.ReachGoal,
            VWS.CompletionCondition.DefeatWaves,
            VWS.CompletionCondition.CollectItems
        };

        static readonly GUIContent[] CompletionConditionLabels =
        {
            new GUIContent("목표 지점 도달"),
            new GUIContent("적 웨이브 모두 처치"),
            new GUIContent("아이템 수집 후 목표 도달")
        };

        // ── 기존 데모 Enemy 경로 ───────────────────────────────────
        const string EnemyPrefabPath = "Assets/Prefabs/Characters/Demo_VW_Enemy.prefab";
        const string ExplorationZombieControllerPath = "Assets/Animations/Generated/EXPLORATION_Zombie_Controller.controller";

        // ── 색상 ──────────────────────────────────────────────────
        static readonly Color ColDoor       = new Color(0.60f, 0.40f, 0.20f);
        static readonly Color ColPlate      = new Color(0.90f, 0.85f, 0.20f);
        static readonly Color ColItem       = new Color(0.20f, 0.80f, 0.30f);
        static readonly Color ColPlatform   = new Color(0.30f, 0.55f, 0.90f);
        static readonly Color ColCheckpoint = new Color(0.85f, 0.45f, 0.90f);
        static readonly Color ColHealth     = new Color(0.90f, 0.25f, 0.30f);
        static readonly Color ColHazard     = new Color(1.00f, 0.45f, 0.10f);
        static readonly Color ColGoal       = new Color(1.00f, 0.85f, 0.10f);
        static readonly Color ColLocked     = new Color(0.50f, 0.30f, 0.70f);

        // 구버전 창은 VARCO 메뉴에 직접 노출하지 않습니다. 신규 사용자는 게임 메이커와 한글 블록 조립기를 사용합니다.
        public static void Open()
        {
            var win = GetWindow<FeatureBuilderWindow>("기능 블록 빌더");
            win.minSize = new Vector2(370, 600);
        }

        // ─────────────────────────────────────────────────────────
        // GUI
        // ─────────────────────────────────────────────────────────
        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(10);

            DrawUnifiedToolShortcuts();
            GUILayout.Space(14);

            DrawConnectorSection();
            GUILayout.Space(16);

            // ── 장르 빠른 설정 ────────────────────────────────────
            DrawSectionHeader("⚡ 장르 빠른 설정");
            EditorGUILayout.HelpBox("장르를 선택하면 해당 장르에 어울리는 기능 블록이 자동으로 체크됩니다.", MessageType.None);
            GUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🗡️ 전투 아레나", GUILayout.Height(30)))
                { ClearAll(); _wave = true; _healthPickup = true; _arenaCover = true; _countdown = true; }
                if (GUILayout.Button("🗺️ 탐험", GUILayout.Height(30)))
                { ClearAll(); _wave = true; _item = true; _checkpoint = true; _hazardZone = true; _lockedDoor = true; _arenaCover = true; }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🧩 퍼즐", GUILayout.Height(30)))
                { ClearAll(); _door = true; _lockedDoor = true; _movableBox = true; _hazardZone = true; _checkpoint = true; _arenaCover = true; }
                if (GUILayout.Button("🟦 플랫폼", GUILayout.Height(30)))
                { ClearAll(); _platform = true; _checkpoint = true; _hazardZone = true; _item = true; _arenaCover = true; }
            }
            GUILayout.Space(12);

            // ── 기본 기능 블록 ─────────────────────────────────────
            DrawSectionHeader("■ 기본 기능 블록");
            EditorGUILayout.HelpBox(
                "모델 없이 빠르게 테스트할 때 쓰는 예제 오브젝트 생성기입니다. 직접 배치한 모델에 기능을 붙일 때는 위의 '선택 모델 기능 연결'을 사용하세요.",
                MessageType.None);
            GUILayout.Space(2);

            _door       = DrawToggleRow(_door,       "🚪 문 열기",       "압력판이 문을 열도록 연결");
            _wave       = DrawToggleRow(_wave,       "👾 적 웨이브",      "적을 자동 생성하는 웨이브 매니저");
            _item       = DrawToggleRow(_item,       "⭐ 아이템 줍기",    "아이템 수집과 카운터 연결");
            _platform   = DrawToggleRow(_platform,   "🟦 움직이는 발판",  "두 지점 사이를 왕복하는 발판");
            _checkpoint = DrawToggleRow(_checkpoint, "🏁 체크포인트",     "체크포인트와 낙사 복귀 지점");

            if (_item)
            {
                EditorGUI.indentLevel++;
                _itemCount = EditorGUILayout.IntSlider("  아이템 수", _itemCount, 1, 8);
                EditorGUI.indentLevel--;
            }
            if (_wave)
            {
                EditorGUI.indentLevel++;
                _waveEnemyCount = EditorGUILayout.IntSlider("  적 수 (웨이브1)", _waveEnemyCount, 1, 10);
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(12);

            // ── 추가 기능 블록 ─────────────────────────────────────
            DrawSectionHeader("■ 추가 기능 블록");
            GUILayout.Space(2);

            _healthPickup = DrawToggleRow(_healthPickup, "💊 회복 아이템",   "플레이어 체력을 회복");
            _hazardZone   = DrawToggleRow(_hazardZone,   "☠️ 위험 구역",     "닿으면 초당 데미지를 주는 구역");
            _countdown    = DrawToggleRow(_countdown,    "⏱️ 제한 시간",     "시간이 끝나면 게임 오버");
            _lockedDoor   = DrawToggleRow(_lockedDoor,   "🔐 잠긴 문",       "아이템 N개 모아야 열리는 목표");
            _movableBox   = DrawToggleRow(_movableBox,   "📦 밀 수 있는 상자", "물리로 밀 수 있는 퍼즐 상자");
            _arenaCover   = DrawToggleRow(_arenaCover,   "🧱 환경 소품/엄폐물", "장르 분위기에 맞는 소품, 장애물, 엄폐물");

            if (_healthPickup)
            {
                EditorGUI.indentLevel++;
                _healAmount = EditorGUILayout.IntSlider("  회복량 (HP)", _healAmount, 5, 100);
                EditorGUI.indentLevel--;
            }
            if (_hazardZone)
            {
                EditorGUI.indentLevel++;
                _hazardDPS = EditorGUILayout.IntSlider("  초당 데미지", _hazardDPS, 1, 50);
                EditorGUI.indentLevel--;
            }
            if (_countdown)
            {
                EditorGUI.indentLevel++;
                _countdownSec = EditorGUILayout.Slider("  제한 시간 (초)", _countdownSec, 10f, 300f);
                EditorGUI.indentLevel--;
            }
            if (_lockedDoor)
            {
                EditorGUI.indentLevel++;
                _lockedDoorKeys = EditorGUILayout.IntSlider("  필요 열쇠 수", _lockedDoorKeys, 1, 8);
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(14);

            // ── 클리어 조건 ────────────────────────────────────────
            DrawSectionHeader("■ 클리어 조건");
            EditorGUILayout.HelpBox(
                "게임이 언제 끝나는지 정하는 전역 규칙입니다. 모델 하나에 붙는 기능이 아니라 게임 매니저, 웨이브 매니저, 목표 지점 설정을 함께 바꿉니다.",
                MessageType.None);
            GUILayout.Space(2);
            _winCondition = DrawCompletionConditionPopup("클리어 조건", _winCondition);
            EditorGUILayout.HelpBox(WinConditionHint(), MessageType.Info);

            GUILayout.Space(18);

            // ── 실행 버튼 ──────────────────────────────────────────
            bool anySelected = _door || _wave || _item || _platform || _checkpoint
                            || _healthPickup || _hazardZone || _countdown || _lockedDoor
                            || _movableBox || _arenaCover;

            using (new EditorGUI.DisabledScope(!anySelected))
            {
                GUI.backgroundColor = anySelected ? new Color(0.35f, 0.85f, 0.55f) : Color.gray;
                if (GUILayout.Button("▶  씬에 추가하기", GUILayout.Height(46)))
                {
                    EditorUtility.DisplayProgressBar("VARCO 기능 블록 빌더", "기능 블록 생성 중...", 0.2f);
                    try { BuildFeatures(); }
                    finally { EditorUtility.ClearProgressBar(); }
                }
                GUI.backgroundColor = Color.white;
            }

            GUILayout.Space(6);

            if (GUILayout.Button("게임 매니저 / 시작 설정 확인·생성", GUILayout.Height(30)))
            {
                EnsureSystemObjects();
                EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }

            GUILayout.Space(4);
            if (GUILayout.Button("선택 전체 초기화"))
                ClearAll();

            GUILayout.Space(10);
            EditorGUILayout.EndScrollView();
        }

        void ClearAll() => _door = _wave = _item = _platform = _checkpoint
                        = _healthPickup = _hazardZone = _countdown = _lockedDoor
                        = _movableBox = _arenaCover = false;

        static void DrawUnifiedToolShortcuts()
        {
            DrawSectionHeader("통합 툴 바로가기");
            EditorGUILayout.HelpBox(
                "이 창은 기존 기능 블록 빌더입니다. 처음 사용하는 사람은 아래 통합 툴에서 게임 제작, 에셋 자동 인식, HUD, 사운드, 빌드까지 한 번에 진행하는 흐름을 권장합니다.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("VARCO 통합 스튜디오 열기", GUILayout.Height(32)))
                    VARCOUnifiedStudioWindow.Open();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("게임 메이커 열기", GUILayout.Height(28)))
                    VARCOGameMakerWindow.Open();

                if (GUILayout.Button("한글 블록 조립기 열기", GUILayout.Height(28)))
                    VARCOBlockCodingBuilderWindow.Open();
            }
        }

        void DrawConnectorSection()
        {
            DrawSectionHeader("선택 모델 기능 연결");
            GUILayout.Space(2);
            EditorGUILayout.HelpBox(
                "이미 하이어라키에 배치한 모델을 선택하고 기능을 붙이는 곳입니다. 모델 위치/회전/스케일은 그대로 두고 필요한 충돌 영역, 물리 몸체, 태그, 게임 기능만 연결합니다.",
                MessageType.Info);

            DrawConnectorGenrePicker();

            bool requiresModel = _connectBlock != ConnectFeatureBlock.Countdown;

            using (new EditorGUI.DisabledScope(!requiresModel))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _connectSelected = (GameObject)EditorGUILayout.ObjectField(
                        "선택 모델",
                        _connectSelected ? _connectSelected : Selection.activeGameObject,
                        typeof(GameObject),
                        true);

                    if (GUILayout.Button("현재 선택", GUILayout.Width(78)))
                        _connectSelected = Selection.activeGameObject;
                }
            }

            if (_connectBlock == ConnectFeatureBlock.Door)
                _connectDoorPart = (DoorConnectPart)EditorGUILayout.Popup(new GUIContent("문 열기 역할"), (int)_connectDoorPart, DoorPartLabels);

            if (_connectBlock == ConnectFeatureBlock.Player || _connectBlock == ConnectFeatureBlock.EnemyWave)
            {
                if (_connectBlock == ConnectFeatureBlock.EnemyWave &&
                    _connectGenre == ConnectGenre.Exploration &&
                    !_connectAnimator)
                {
                    _connectAnimator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ExplorationZombieControllerPath);
                }

                _connectAnimator = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
                    "애니메이션 컨트롤러",
                    _connectAnimator,
                    typeof(RuntimeAnimatorController),
                    false);
            }

            DrawConnectorOptions();

            GUILayout.Space(6);
            using (new EditorGUI.DisabledScope(requiresModel && _connectSelected == null))
            {
                GUI.backgroundColor = (!requiresModel || _connectSelected) ? new Color(0.45f, 0.85f, 0.55f) : Color.gray;
                if (GUILayout.Button(requiresModel ? "선택 모델에 기능 연결" : "씬에 기능 연결", GUILayout.Height(38)))
                    ConnectSelectedModel();
                GUI.backgroundColor = Color.white;
            }

            if (GUILayout.Button("테스트 튜닝 오버레이 추가", GUILayout.Height(26)))
                AddPlaytestTuningOverlay();
        }

        void DrawConnectorGenrePicker()
        {
            EditorGUI.BeginChangeCheck();
            _connectGenre = (ConnectGenre)EditorGUILayout.Popup(new GUIContent("장르"), (int)_connectGenre, GenreLabels);
            if (EditorGUI.EndChangeCheck())
            {
                _connectBlock = GetGenreBlocks(_connectGenre)[0];
                _connectCameraView = GetDefaultCameraView(_connectGenre);
            }

            var blocks = GetGenreBlocks(_connectGenre);
            int selectedIndex = System.Array.IndexOf(blocks, _connectBlock);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
                _connectBlock = blocks[0];
            }

            var labels = new GUIContent[blocks.Length];
            for (int i = 0; i < blocks.Length; i++)
            {
                labels[i] = new GUIContent(
                    $"{GetGenreName(_connectGenre)} - {GetBlockName(blocks[i], _connectGenre)}",
                    GetBlockHint(blocks[i], _connectGenre));
            }

            selectedIndex = EditorGUILayout.Popup(new GUIContent("기능"), selectedIndex, labels);
            _connectBlock = blocks[selectedIndex];

            EditorGUILayout.HelpBox(GetGenreHint(_connectGenre), MessageType.None);
        }

        static ConnectFeatureBlock[] GetGenreBlocks(ConnectGenre genre)
        {
            switch (genre)
            {
                case ConnectGenre.CombatArena: return CombatArenaBlocks;
                case ConnectGenre.Exploration: return ExplorationBlocks;
                case ConnectGenre.Puzzle: return PuzzleBlocks;
                case ConnectGenre.Platformer: return PlatformerBlocks;
                default: return CommonBlocks;
            }
        }

        static string GetGenreName(ConnectGenre genre)
        {
            switch (genre)
            {
                case ConnectGenre.CombatArena: return "전투 아레나";
                case ConnectGenre.Exploration: return "탐험";
                case ConnectGenre.Puzzle: return "퍼즐";
                case ConnectGenre.Platformer: return "플랫폼";
                default: return "공통";
            }
        }

        static VARCOAutoConnectorWindow.CameraViewPreset GetDefaultCameraView(ConnectGenre genre)
        {
            switch (genre)
            {
                case ConnectGenre.CombatArena:
                case ConnectGenre.Exploration:
                case ConnectGenre.Puzzle:
                    return VARCOAutoConnectorWindow.CameraViewPreset.QuarterView;
                case ConnectGenre.Platformer:
                    return VARCOAutoConnectorWindow.CameraViewPreset.SideView;
                default:
                    return VARCOAutoConnectorWindow.CameraViewPreset.ThirdPerson;
            }
        }

        static string GetGenreHint(ConnectGenre genre)
        {
            switch (genre)
            {
                case ConnectGenre.CombatArena:
                    return "Hades식 전투 아레나: 전투 플레이어, 적 웨이브, 회복 아이템, 환경 소품/엄폐물을 빠르게 연결합니다.";
                case ConnectGenre.Exploration:
                    return "The Ascent식 탐험: 탐험 플레이어, 좀비, 수집, 체크포인트, 위험 구역, 환경 소품으로 경로를 구성합니다.";
                case ConnectGenre.Puzzle:
                    return "Death's Door식 퍼즐: 퍼즐 플레이어, 문, 발판, 열쇠 목표, 밀 수 있는 상자, 환경 소품을 연결합니다.";
                case ConnectGenre.Platformer:
                    return "Trine식 플랫폼: 플랫폼 플레이어, 움직이는 발판, 체크포인트, 낙하/위험 구역, 발판 주변 소품을 연결합니다.";
                default:
                    return "장르와 무관하게 자주 쓰는 기본 연결입니다.";
            }
        }

        static string GetBlockName(ConnectFeatureBlock block)
        {
            switch (block)
            {
                case ConnectFeatureBlock.Player: return "플레이어";
                case ConnectFeatureBlock.Door: return "문/발판";
                case ConnectFeatureBlock.EnemyWave: return "적 웨이브";
                case ConnectFeatureBlock.ItemPickup: return "아이템";
                case ConnectFeatureBlock.MovingPlatform: return "움직이는 발판";
                case ConnectFeatureBlock.Checkpoint: return "체크포인트";
                case ConnectFeatureBlock.HealthPickup: return "회복 아이템";
                case ConnectFeatureBlock.HazardZone: return "위험 구역";
                case ConnectFeatureBlock.Countdown: return "제한 시간";
                case ConnectFeatureBlock.LockedDoor: return "잠긴 문/목표";
                case ConnectFeatureBlock.MovableBox: return "밀 수 있는 상자";
                case ConnectFeatureBlock.ArenaCover: return "환경 소품/엄폐물";
                default: return block.ToString();
            }
        }

        static string GetBlockName(ConnectFeatureBlock block, ConnectGenre genre)
        {
            if (block == ConnectFeatureBlock.Player)
            {
                switch (genre)
                {
                    case ConnectGenre.CombatArena: return "전투 플레이어";
                    case ConnectGenre.Exploration: return "탐험 플레이어";
                    case ConnectGenre.Puzzle: return "퍼즐 플레이어";
                    case ConnectGenre.Platformer: return "플랫폼 플레이어";
                }
            }

            if (genre == ConnectGenre.Exploration && block == ConnectFeatureBlock.EnemyWave)
                return "좀비";

            return GetBlockName(block);
        }

        static string GetBlockHint(ConnectFeatureBlock block)
        {
            switch (block)
            {
                case ConnectFeatureBlock.Player:
                    return "선택 모델에 플레이어 조작, 카메라 대상, HP/수집 카운터를 연결합니다.";
                case ConnectFeatureBlock.Door:
                    return "선택 모델을 문 또는 발판으로 만들고 가까운 상대 오브젝트와 연결합니다.";
                case ConnectFeatureBlock.EnemyWave:
                    return "선택 모델을 적으로 만들고 웨이브 매니저에서 생성되도록 연결합니다.";
                case ConnectFeatureBlock.ItemPickup:
                    return "선택 모델을 수집 아이템으로 만듭니다.";
                case ConnectFeatureBlock.MovingPlatform:
                    return "선택 모델을 왕복 이동 발판으로 만들고 시작/끝 지점을 생성합니다.";
                case ConnectFeatureBlock.Checkpoint:
                    return "선택 모델을 체크포인트 트리거로 만듭니다.";
                case ConnectFeatureBlock.HealthPickup:
                    return "선택 모델을 HP 회복 아이템으로 만듭니다.";
                case ConnectFeatureBlock.HazardZone:
                    return "선택 모델을 초당 데미지를 주는 위험 트리거로 만듭니다.";
                case ConnectFeatureBlock.Countdown:
                    return "선택 모델 없이 씬에 제한 시간 시스템을 연결합니다.";
                case ConnectFeatureBlock.LockedDoor:
                    return "선택 모델을 아이템 N개가 필요한 목표 트리거로 만듭니다.";
                case ConnectFeatureBlock.MovableBox:
                    return "선택 모델을 밀 수 있는 물리 상자로 만듭니다.";
                case ConnectFeatureBlock.ArenaCover:
                    return "선택 모델을 충돌 영역이 있는 정적 환경 소품/엄폐물로 만듭니다.";
                default:
                    return "";
            }
        }

        static string GetBlockHint(ConnectFeatureBlock block, ConnectGenre genre)
        {
            if (block == ConnectFeatureBlock.Player)
                return GetBlockName(block, genre) + " 조작, 카메라 대상, HP/수집 카운터를 연결합니다.";

            if (genre == ConnectGenre.Exploration && block == ConnectFeatureBlock.EnemyWave)
                return "선택 모델을 탐험용 좀비로 만들고 웨이브 매니저에서 생성되도록 연결합니다.";

            return GetBlockHint(block);
        }

        void DrawConnectorOptions()
        {
            switch (_connectBlock)
            {
                case ConnectFeatureBlock.Player:
                    _connectCameraView = VARCOAutoConnectorWindow.DrawCameraViewPopup("카메라 시점", _connectCameraView);
                    break;

                case ConnectFeatureBlock.EnemyWave:
                    _saveEnemyAsPrefab = true;
                    _connectEnemyToWave = true;
                    _connectWaveIndex = EditorGUILayout.IntField("웨이브 번호", _connectWaveIndex);
                    string enemyHelp = _connectGenre == ConnectGenre.Exploration
                        ? "선택 모델을 탐험용 좀비 프리팹으로 저장하고 웨이브 매니저에 바로 연결합니다. '적 웨이브' 기능 블록을 만든 뒤 실행하면 이 좀비가 생성됩니다."
                        : "선택 모델을 적 프리팹으로 저장하고 웨이브 매니저에 바로 연결합니다. 그래서 '적 웨이브' 기능 블록을 만든 뒤 실행하면 이 모델이 생성됩니다.";
                    EditorGUILayout.HelpBox(
                        enemyHelp,
                        MessageType.None);
                    break;

                case ConnectFeatureBlock.LockedDoor:
                    _connectRequiredItems = EditorGUILayout.IntSlider("필요 아이템 수", _connectRequiredItems, 0, 12);
                    break;

                case ConnectFeatureBlock.HealthPickup:
                    _connectHealAmount = EditorGUILayout.IntSlider("회복량", _connectHealAmount, 5, 100);
                    break;

                case ConnectFeatureBlock.HazardZone:
                    _connectHazardDps = EditorGUILayout.IntSlider("초당 데미지", _connectHazardDps, 1, 50);
                    break;

                case ConnectFeatureBlock.MovingPlatform:
                    _connectPlatformDistance = EditorGUILayout.Slider("이동 거리", _connectPlatformDistance, 1f, 12f);
                    _connectPlatformSpeed = EditorGUILayout.Slider("속도", _connectPlatformSpeed, 0.2f, 6f);
                    break;

                case ConnectFeatureBlock.Countdown:
                    _countdownSec = EditorGUILayout.Slider("제한 시간 (초)", _countdownSec, 10f, 300f);
                    EditorGUILayout.HelpBox("제한 시간은 선택 모델에 붙는 기능이 아니라 씬 시스템 기능입니다.", MessageType.None);
                    break;
            }
        }

        void ConnectSelectedModel()
        {
            if (_connectBlock == ConnectFeatureBlock.Countdown)
            {
                EnsureSystemObjects();
                BuildCountdown();
                EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                return;
            }

            _connectSelected = _connectSelected ? _connectSelected : Selection.activeGameObject;
            var role = ResolveConnectorRole();
            VARCOAutoConnectorWindow.ConnectFromFeatureBuilder(
                _connectSelected,
                role,
                _connectAnimator,
                _connectEnemyToWave,
                _saveEnemyAsPrefab,
                _connectWaveIndex,
                _connectRequiredItems,
                _connectHealAmount,
                _connectHazardDps,
                _connectPlatformDistance,
                _connectPlatformSpeed,
                false,
                _connectCameraView);
        }

        void AddPlaytestTuningOverlay()
        {
            var overlayType = System.Type.GetType("VARCO_Workshop.PlaytestTuningOverlay, Assembly-CSharp");
            if (overlayType == null || !typeof(Component).IsAssignableFrom(overlayType))
            {
                Debug.LogWarning("[VARCO 기능 블록 빌더] 플레이 테스트 튜닝 오버레이 스크립트가 아직 컴파일되지 않았습니다. Unity 컴파일이 끝난 뒤 버튼을 다시 눌러주세요.");
                return;
            }

            var overlay = FindFirstObjectByType(overlayType);
            if (overlay)
            {
                Selection.activeObject = ((Component)overlay).gameObject;
                return;
            }

            var go = new GameObject("VW_PlaytestTuningOverlay");
            Undo.RegisterCreatedObjectUndo(go, "플레이 테스트 튜닝 오버레이 추가");
            go.AddComponent(overlayType);
            Selection.activeObject = go;
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        VARCOAutoConnectorWindow.Role ResolveConnectorRole()
        {
            switch (_connectBlock)
            {
                case ConnectFeatureBlock.Player:
                    return _connectGenre == ConnectGenre.Platformer
                        ? VARCOAutoConnectorWindow.Role.PlatformPlayer
                        : VARCOAutoConnectorWindow.Role.Player;
                case ConnectFeatureBlock.Door:
                    return _connectDoorPart == DoorConnectPart.Door
                        ? VARCOAutoConnectorWindow.Role.Door
                        : VARCOAutoConnectorWindow.Role.PressurePlate;
                case ConnectFeatureBlock.EnemyWave:
                    return VARCOAutoConnectorWindow.Role.Enemy;
                case ConnectFeatureBlock.ItemPickup:
                    return VARCOAutoConnectorWindow.Role.ItemPickup;
                case ConnectFeatureBlock.MovingPlatform:
                    return VARCOAutoConnectorWindow.Role.MovingPlatform;
                case ConnectFeatureBlock.Checkpoint:
                    return VARCOAutoConnectorWindow.Role.Checkpoint;
                case ConnectFeatureBlock.HealthPickup:
                    return VARCOAutoConnectorWindow.Role.HealthPickup;
                case ConnectFeatureBlock.HazardZone:
                    return VARCOAutoConnectorWindow.Role.HazardZone;
                case ConnectFeatureBlock.LockedDoor:
                    return VARCOAutoConnectorWindow.Role.Goal;
                case ConnectFeatureBlock.MovableBox:
                    return VARCOAutoConnectorWindow.Role.MovableBox;
                case ConnectFeatureBlock.ArenaCover:
                    return VARCOAutoConnectorWindow.Role.ArenaCover;
                default:
                    return VARCOAutoConnectorWindow.Role.ItemPickup;
            }
        }

        // ─────────────────────────────────────────────────────────
        // Build Entry
        // ─────────────────────────────────────────────────────────
        void BuildFeatures()
        {
            Undo.SetCurrentGroupName("기능 블록 빌더: 블록 추가");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureSystemObjects();

            if (_door)         BuildDoorSystem();
            if (_wave)         BuildEnemyWave();
            if (_item)         BuildItemPickup();
            if (_platform)     BuildMovingPlatform();
            if (_checkpoint)   BuildCheckpoint();
            if (_healthPickup) BuildHealthPickup();
            if (_hazardZone)   BuildHazardZone();
            if (_countdown)    BuildCountdown();
            if (_lockedDoor)   BuildLockedDoor();
            if (_movableBox)   BuildMovableBox();
            if (_arenaCover)   BuildArenaCover();

            ApplyWinCondition();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "기능 블록 빌더",
                "씬에 기능 블록을 추가했습니다.\n\n" +
                "· Ctrl+Z 로 전체 되돌리기 가능\n" +
                "· Ctrl+S 로 씬 저장\n" +
                "· 직접 배치한 모델은 위의 '선택 모델 기능 연결' 섹션 사용",
                "확인");
        }

        // ─────────────────────────────────────────────────────────
        // System Objects
        // ─────────────────────────────────────────────────────────
        void EnsureSystemObjects()
        {
            if (FindFirstObjectByType<VWS.GameManager>() != null) return;
            var go = new GameObject("VW_Bootstrap");
            Undo.RegisterCreatedObjectUndo(go, "VW_Bootstrap 생성");
            go.AddComponent<VWS.GameManager>();
            go.AddComponent<VWS.SceneBootstrap>();
            Debug.Log("[기능 블록 빌더] 게임 매니저와 시작 설정을 추가했습니다.");
        }

        // ─────────────────────────────────────────────────────────
        // 🚪 DoorSystem
        // ─────────────────────────────────────────────────────────
        void BuildDoorSystem()
        {
            var root = new GameObject("FB_DoorSystem");
            Undo.RegisterCreatedObjectUndo(root, "문/발판 블록 생성");
            root.transform.position = new Vector3(5, 0, 0);

            var doorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorGo.name = "Door";
            doorGo.transform.SetParent(root.transform, false);
            doorGo.transform.localPosition = new Vector3(0, 1.5f, 0);
            doorGo.transform.localScale    = new Vector3(2f, 3f, 0.2f);
            SetColor(doorGo, ColDoor);
            var door = doorGo.AddComponent<VWS.DoorController>();

            var plateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plateGo.name = "PressurePlate";
            plateGo.transform.SetParent(root.transform, false);
            plateGo.transform.localPosition = new Vector3(0, 0.05f, 3.5f);
            plateGo.transform.localScale    = new Vector3(2f, 0.1f, 2f);
            SetColor(plateGo, ColPlate);
            plateGo.GetComponent<Collider>().isTrigger = true;
            var plate = plateGo.AddComponent<VWS.PressurePlate>();

            var so = new SerializedObject(plate);
            so.FindProperty("targets").arraySize = 1;
            so.FindProperty("targets").GetArrayElementAtIndex(0).objectReferenceValue = door;
            so.ApplyModifiedProperties();
        }

        // ─────────────────────────────────────────────────────────
        // 👾 EnemyWave
        // ─────────────────────────────────────────────────────────
        void BuildEnemyWave()
        {
            var root = new GameObject("FB_EnemyWave");
            Undo.RegisterCreatedObjectUndo(root, "적 웨이브 블록 생성");
            root.transform.position = new Vector3(-6, 0, 0);

            var areaGo = new GameObject("SpawnArea");
            areaGo.transform.SetParent(root.transform, false);
            var areaBox = areaGo.AddComponent<BoxCollider>();
            areaBox.size = new Vector3(8f, 1f, 8f); areaBox.isTrigger = true;

            var wm   = root.AddComponent<VWS.WaveManager>();
            var wmso = new SerializedObject(wm);
            wmso.FindProperty("delayBetweenWaves").floatValue       = 1.5f;
            wmso.FindProperty("clearWhenAllWavesCleared").boolValue = true;
            wmso.FindProperty("randomSpawnArea").objectReferenceValue = areaBox;
            wmso.FindProperty("waves").arraySize = 1;
            var w0 = wmso.FindProperty("waves").GetArrayElementAtIndex(0);
            w0.FindPropertyRelative("enemyCount").intValue      = _waveEnemyCount;
            w0.FindPropertyRelative("spawnInterval").floatValue = 0.8f;

            var enemyPf = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (enemyPf)
                w0.FindPropertyRelative("enemyPrefab").objectReferenceValue = enemyPf;
            else
                Debug.LogWarning("[기능 블록 빌더] 적 프리팹 없음 → 아래 '선택 모델 기능 연결 > 적 웨이브'로 직접 연결하세요.");

            wmso.ApplyModifiedProperties();
        }

        // ─────────────────────────────────────────────────────────
        // ⭐ ItemPickup
        // ─────────────────────────────────────────────────────────
        void BuildItemPickup()
        {
            var root = new GameObject("FB_ItemPickup");
            Undo.RegisterCreatedObjectUndo(root, "아이템 수집 블록 생성");
            root.transform.position = new Vector3(0, 0, 7);

            float r = 2.2f;
            for (int i = 0; i < _itemCount; i++)
            {
                float angle = i * Mathf.PI * 2f / _itemCount;
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Item_{i + 1:00}";
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, 0.5f, Mathf.Sin(angle) * r);
                go.transform.localScale    = Vector3.one * 0.45f;
                SetColor(go, ColItem);
                go.GetComponent<Collider>().isTrigger = true;
                go.AddComponent<VWS.ItemPickup>();
            }
        }

        // ─────────────────────────────────────────────────────────
        // 🟦 MovingPlatform
        // ─────────────────────────────────────────────────────────
        void BuildMovingPlatform()
        {
            var root = new GameObject("FB_MovingPlatform");
            Undo.RegisterCreatedObjectUndo(root, "움직이는 발판 블록 생성");
            root.transform.position = new Vector3(0, 0, -7);

            var wA = new GameObject("WaypointA");
            wA.transform.SetParent(root.transform, false);
            wA.transform.localPosition = new Vector3(-4f, 1f, 0);

            var wB = new GameObject("WaypointB");
            wB.transform.SetParent(root.transform, false);
            wB.transform.localPosition = new Vector3(4f, 1f, 0);

            var platGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platGo.name = "Platform";
            platGo.transform.SetParent(root.transform, false);
            platGo.transform.localPosition = wA.transform.localPosition;
            platGo.transform.localScale    = new Vector3(3f, 0.3f, 3f);
            SetColor(platGo, ColPlatform);

            var mp   = platGo.AddComponent<VWS.MovingPlatform>();
            var mpso = new SerializedObject(mp);
            mpso.FindProperty("a").objectReferenceValue = wA.transform;
            mpso.FindProperty("b").objectReferenceValue = wB.transform;
            mpso.FindProperty("speed").floatValue = 1.2f;
            mpso.ApplyModifiedProperties();
        }

        // ─────────────────────────────────────────────────────────
        // 🏁 Checkpoint
        // ─────────────────────────────────────────────────────────
        void BuildCheckpoint()
        {
            var root = new GameObject("FB_Checkpoint");
            Undo.RegisterCreatedObjectUndo(root, "체크포인트 블록 생성");
            root.transform.position = new Vector3(-9, 0, 5);

            var markerGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            markerGo.name = "CheckpointMarker";
            markerGo.transform.SetParent(root.transform, false);
            markerGo.transform.localPosition = new Vector3(0, 1f, 0);
            markerGo.transform.localScale    = new Vector3(1.5f, 1f, 1.5f);
            SetColor(markerGo, ColCheckpoint);
            markerGo.GetComponent<Collider>().isTrigger = true;
            var cp   = markerGo.AddComponent<VWS.Checkpoint>();
            var cpso = new SerializedObject(cp);
            cpso.FindProperty("setOnStart").boolValue = false;
            cpso.ApplyModifiedProperties();

            var dzGo  = new GameObject("DeathZone");
            dzGo.transform.SetParent(root.transform, false);
            dzGo.transform.localPosition = new Vector3(0, -12f, 0);
            var dzBox = dzGo.AddComponent<BoxCollider>();
            dzBox.size = new Vector3(200f, 1f, 200f); dzBox.isTrigger = true;
            dzGo.AddComponent<VWS.DeathZone>();
        }

        // ─────────────────────────────────────────────────────────
        // 💊 HealthPickup
        // ─────────────────────────────────────────────────────────
        void BuildHealthPickup()
        {
            var root = new GameObject("FB_HealthPickup");
            Undo.RegisterCreatedObjectUndo(root, "회복 아이템 블록 생성");
            root.transform.position = new Vector3(9, 0, 0);

            // 십자가 모양: 세로 + 가로 큐브
            void CrossBar(string name, Vector3 lp, Vector3 ls)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = lp;
                go.transform.localScale    = ls;
                SetColor(go, ColHealth);
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            CrossBar("Cross_V", new Vector3(0, 0.6f, 0), new Vector3(0.3f, 1.0f, 0.3f));
            CrossBar("Cross_H", new Vector3(0, 0.6f, 0), new Vector3(0.9f, 0.3f, 0.3f));

            // 픽업 트리거
            var trigger = new GameObject("Trigger");
            trigger.transform.SetParent(root.transform, false);
            trigger.transform.localPosition = new Vector3(0, 0.6f, 0);
            var box = trigger.AddComponent<BoxCollider>();
            box.size = new Vector3(1.2f, 1.2f, 1.2f); box.isTrigger = true;
            var hp = trigger.AddComponent<VWS.HealthPickup>();
            var hpso = new SerializedObject(hp);
            hpso.FindProperty("healAmount").intValue = _healAmount;
            hpso.ApplyModifiedProperties();

            Debug.Log($"[기능 블록 빌더] 회복 아이템 배치 완료. 회복량: {_healAmount}. 빨간 십자 모양입니다.");
        }

        // ─────────────────────────────────────────────────────────
        // ☠️ HazardZone
        // ─────────────────────────────────────────────────────────
        void BuildHazardZone()
        {
            var root = new GameObject("FB_HazardZone");
            Undo.RegisterCreatedObjectUndo(root, "위험 구역 블록 생성");
            root.transform.position = new Vector3(0, 0, 9);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "HazardFloor";
            go.transform.SetParent(root.transform, false);
            go.transform.localScale = new Vector3(5f, 0.1f, 5f);
            SetColor(go, ColHazard);
            go.GetComponent<Collider>().isTrigger = true;

            var hz   = go.AddComponent<VWS.HazardZone>();
            var hzso = new SerializedObject(hz);
            hzso.FindProperty("damagePerSecond").intValue = _hazardDPS;
            hzso.ApplyModifiedProperties();

            Debug.Log($"[기능 블록 빌더] 위험 구역 배치 완료. 초당 피해: {_hazardDPS}. 주황 바닥입니다.");
        }

        // ─────────────────────────────────────────────────────────
        // ⏱️ CountdownTimer
        // ─────────────────────────────────────────────────────────
        void BuildCountdown()
        {
            // 씬에 이미 있으면 추가 금지
            if (FindFirstObjectByType<VWS.CountdownTimer>() != null)
            {
                Debug.LogWarning("[기능 블록 빌더] 제한 시간 타이머가 이미 씬에 있습니다.");
                return;
            }

            // GameManager 루트에 붙이거나 독립 오브젝트로
            var gm = FindFirstObjectByType<VWS.GameManager>();
            var target = gm != null ? gm.gameObject : new GameObject("FB_CountdownTimer");
            if (gm == null) Undo.RegisterCreatedObjectUndo(target, "제한 시간 타이머 생성");

            var ct   = target.AddComponent<VWS.CountdownTimer>();
            var ctso = new SerializedObject(ct);
            ctso.FindProperty("totalSeconds").floatValue = _countdownSec;
            ctso.ApplyModifiedProperties();

            Debug.Log($"[기능 블록 빌더] 제한 시간 타이머 추가 완료. 제한 시간: {_countdownSec}초. 시간이 0이 되면 게임 오버.");
        }

        // ─────────────────────────────────────────────────────────
        // 🔐 LockedDoor (아이템 N개 필요한 GoalTrigger + Door)
        // ─────────────────────────────────────────────────────────
        void BuildLockedDoor()
        {
            var root = new GameObject("FB_LockedDoor");
            Undo.RegisterCreatedObjectUndo(root, "잠긴 문 블록 생성");
            root.transform.position = new Vector3(-5, 0, 9);

            // 문 비주얼
            var doorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorGo.name = "LockedDoor";
            doorGo.transform.SetParent(root.transform, false);
            doorGo.transform.localPosition = new Vector3(0, 1.5f, 0);
            doorGo.transform.localScale    = new Vector3(2f, 3f, 0.3f);
            SetColor(doorGo, ColLocked);

            // 통과 트리거 (GoalTrigger = 문 열림 + 클리어)
            var trigGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trigGo.name = "UnlockTrigger";
            trigGo.transform.SetParent(root.transform, false);
            trigGo.transform.localPosition = new Vector3(0, 1.5f, 0.4f);
            trigGo.transform.localScale    = new Vector3(2.5f, 3.5f, 0.5f);
            trigGo.GetComponent<MeshRenderer>().enabled = false;   // 투명
            trigGo.GetComponent<Collider>().isTrigger   = true;

            var gt   = trigGo.AddComponent<VWS.GoalTrigger>();
            var gtso = new SerializedObject(gt);
            gtso.FindProperty("requiredItems").intValue = _lockedDoorKeys;
            gtso.ApplyModifiedProperties();

            // 키 아이템들
            float r = 1.5f;
            for (int i = 0; i < _lockedDoorKeys; i++)
            {
                float angle = i * Mathf.PI * 2f / _lockedDoorKeys;
                var key = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                key.name = $"Key_{i + 1:00}";
                key.transform.SetParent(root.transform, false);
                key.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, 0.5f, 5f + Mathf.Sin(angle) * r);
                key.transform.localScale    = Vector3.one * 0.4f;
                SetColor(key, ColLocked);
                key.GetComponent<Collider>().isTrigger = true;
                key.AddComponent<VWS.ItemPickup>();
            }

            Debug.Log($"[기능 블록 빌더] 잠긴 문 배치 완료. 열쇠 {_lockedDoorKeys}개가 필요합니다. 보라색 문과 열쇠 구체가 만들어졌습니다.");
        }

        // ─────────────────────────────────────────────────────────
        // 클리어 조건
        // ─────────────────────────────────────────────────────────
        void BuildMovableBox()
        {
            var root = new GameObject("FB_MovableBoxPuzzle");
            Undo.RegisterCreatedObjectUndo(root, "상자 퍼즐 블록 생성");
            root.transform.position = new Vector3(4, 0, 8);

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "MovableBox";
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = new Vector3(0, 0.65f, 0);
            box.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
            SetColor(box, new Color(0.55f, 0.42f, 0.24f));
            var rb = box.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            box.AddComponent<VWS.MovableBox>();

            var plateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plateGo.name = "BoxTargetPlate";
            plateGo.transform.SetParent(root.transform, false);
            plateGo.transform.localPosition = new Vector3(2.8f, 0.05f, 0);
            plateGo.transform.localScale = new Vector3(1.5f, 0.1f, 1.5f);
            SetColor(plateGo, ColPlate);
            plateGo.GetComponent<Collider>().isTrigger = true;

            Debug.Log("[기능 블록 빌더] 움직이는 상자 퍼즐 블록을 추가했습니다. 상자를 목표 발판 쪽으로 밀어 넣으면 됩니다.");
        }

        void BuildArenaCover()
        {
            var root = new GameObject("FB_ArenaCover");
            Undo.RegisterCreatedObjectUndo(root, "전투 엄폐물 블록 생성");
            root.transform.position = Vector3.zero;

            Vector3[] positions =
            {
                new Vector3(-3.5f, 0.75f, 2.5f),
                new Vector3(3.5f, 0.75f, 2.0f),
                new Vector3(0f, 0.75f, -2.8f)
            };

            Vector3[] scales =
            {
                new Vector3(1.2f, 1.5f, 2.8f),
                new Vector3(2.5f, 1.5f, 1.1f),
                new Vector3(3.2f, 1.5f, 1.0f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cover.name = $"Cover_{i + 1:00}";
                cover.transform.SetParent(root.transform, false);
                cover.transform.localPosition = positions[i];
                cover.transform.localScale = scales[i];
                SetColor(cover, new Color(0.22f, 0.25f, 0.30f));
                cover.isStatic = true;
            }

            Debug.Log("[기능 블록 빌더] 전투/탐험 엄폐물을 추가했습니다. 엄폐물 위치를 바꾼 뒤에는 내비게이션을 다시 구우세요.");
        }

        void ApplyWinCondition()
        {
            switch (_winCondition)
            {
                case VWS.CompletionCondition.ReachGoal:
                    EnsureGoalTrigger(0);
                    SetWaveManagerClear(false);
                    break;

                case VWS.CompletionCondition.DefeatWaves:
                    SetWaveManagerClear(true);
                    foreach (var gt in FindObjectsByType<VWS.GoalTrigger>(FindObjectsSortMode.None))
                        gt.gameObject.SetActive(false);
                    break;

                case VWS.CompletionCondition.CollectItems:
                    EnsureGoalTrigger(_itemCount);
                    SetWaveManagerClear(false);
                    break;
            }
        }

        void EnsureGoalTrigger(int requiredItems)
        {
            if (FindFirstObjectByType<VWS.GoalTrigger>() != null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Goal";
            Undo.RegisterCreatedObjectUndo(go, "목표 지점 생성");
            go.transform.position   = new Vector3(0, 0.75f, -12);
            go.transform.localScale = new Vector3(2.5f, 1.5f, 1f);
            SetColor(go, ColGoal);
            go.GetComponent<Collider>().isTrigger = true;
            var gt   = go.AddComponent<VWS.GoalTrigger>();
            var gtso = new SerializedObject(gt);
            gtso.FindProperty("requiredItems").intValue = requiredItems;
            gtso.ApplyModifiedProperties();
        }

        void SetWaveManagerClear(bool value)
        {
            foreach (var wm in FindObjectsByType<VWS.WaveManager>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(wm);
                so.FindProperty("clearWhenAllWavesCleared").boolValue = value;
                so.ApplyModifiedProperties();
            }
        }

        // ─────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────
        static void SetColor(GameObject go, Color color)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (!mr) return;
            var sh = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                  ?? Shader.Find("Standard");
            if (!sh) return;
            var mat = new Material(sh) { name = go.name + "_mat" };
            if (mat.HasColor("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasColor("_Color")) mat.SetColor("_Color", color);
            mr.sharedMaterial = mat;
        }

        string WinConditionHint() => _winCondition switch
        {
            VWS.CompletionCondition.ReachGoal    => "금색 목표 큐브에 닿으면 클리어됩니다.",
            VWS.CompletionCondition.DefeatWaves  => "웨이브 매니저의 모든 웨이브를 처치하면 자동으로 클리어됩니다.",
            VWS.CompletionCondition.CollectItems => $"초록 아이템 {_itemCount}개를 수집한 뒤 목표 큐브에 닿으면 클리어됩니다.",
            _                                    => "직접 클리어 처리가 필요한 조건입니다."
        };

        static VWS.CompletionCondition DrawCompletionConditionPopup(string label, VWS.CompletionCondition current)
        {
            var index = System.Array.IndexOf(CompletionConditionOptions, current);
            if (index < 0)
                index = 0;

            index = EditorGUILayout.Popup(new GUIContent(label), index, CompletionConditionLabels);
            return CompletionConditionOptions[Mathf.Clamp(index, 0, CompletionConditionOptions.Length - 1)];
        }

        static bool DrawToggleRow(bool val, string label, string desc)
        {
            EditorGUILayout.BeginHorizontal();
            val = EditorGUILayout.ToggleLeft(label, val, GUILayout.Width(190));
            var gray = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
            EditorGUILayout.LabelField(desc, gray);
            EditorGUILayout.EndHorizontal();
            return val;
        }

        static void DrawSectionHeader(string title)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal   = { textColor = new Color(0.75f, 0.90f, 1.00f) }
            };
            EditorGUILayout.LabelField(title, style);
            var rect = GUILayoutUtility.GetLastRect();
            rect.y += EditorGUIUtility.singleLineHeight + 1;
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.35f, 0.60f, 0.85f, 0.45f));
            GUILayout.Space(3);
        }
    }
}
#endif
