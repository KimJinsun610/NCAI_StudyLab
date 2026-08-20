#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VARCO_Workshop.Editor
{
    public class VARCOPresetMakerWindow : EditorWindow
    {
        const string PresetKitRoot = "Assets/VARCOPresetKits";
        const string AudioRoot = "Assets/Audio";
        const string BgmFolder = "Assets/Audio/BGM";
        const string SfxFolder = "Assets/Audio/SFX";
        const string TtsFolder = "Assets/Audio/TTS";
        const string SoundRegistryPath = "Assets/ScriptableObjects/SoundEvents/WorkshopSoundEventRegistry.asset";

        static readonly string[] AudioExtensions = { ".wav", ".mp3", ".ogg", ".aif", ".aiff" };

        static readonly PresetOption[] Presets =
        {
            new PresetOption("전투 아레나", VARCOGameMakerWindow.BlockTemplate.ArenaCombatWave, "웨이브 전투, 무기, 체력 회복, 엄폐물을 배치합니다."),
            new PresetOption("아레나 보스전", VARCOGameMakerWindow.BlockTemplate.ArenaBossBattle, "보스형 적과 전투 구성을 배치합니다."),
            new PresetOption("제한시간 생존", VARCOGameMakerWindow.BlockTemplate.SurvivalTimer, "제한시간, 적, 회복 아이템을 중심으로 구성합니다."),
            new PresetOption("탐험 좀비 게임", VARCOGameMakerWindow.BlockTemplate.ExplorationZombieQuest, "탐험형 플레이어, 좀비 적, 수집과 목표 지점을 구성합니다."),
            new PresetOption("탐험 좀비 생존", VARCOGameMakerWindow.BlockTemplate.ExplorationZombieSurvival, "좀비 생존 전투와 회복, 위험 구역을 구성합니다."),
            new PresetOption("탐험 보물찾기", VARCOGameMakerWindow.BlockTemplate.ExplorationTreasureHunt, "수집 아이템과 목표 지점을 중심으로 탐험 게임을 구성합니다."),
            new PresetOption("수집 후 탈출", VARCOGameMakerWindow.BlockTemplate.CollectAndEscape, "아이템 수집 뒤 탈출 지점으로 이동하는 구성을 만듭니다."),
            new PresetOption("퍼즐 방", VARCOGameMakerWindow.BlockTemplate.PuzzleDoorRoom, "문, 발판, 이동 상자, 목표 지점을 구성합니다."),
            new PresetOption("퍼즐 탈출방", VARCOGameMakerWindow.BlockTemplate.PuzzleEscapeRoom, "수집, 문, 발판, 탈출 목표를 포함합니다."),
            new PresetOption("플랫폼 코스", VARCOGameMakerWindow.BlockTemplate.PlatformSpaceCourse, "점프 코스, 낙하 리스폰, 체크포인트를 구성합니다."),
            new PresetOption("플랫폼 장애물 코스", VARCOGameMakerWindow.BlockTemplate.PlatformObstacleRun, "플랫폼, 위험 구역, 목표 지점을 구성합니다."),
            new PresetOption("전체 기능 샌드박스", VARCOGameMakerWindow.BlockTemplate.FullFeatureSandbox, "주요 기능을 한 씬에서 확인할 수 있게 구성합니다.")
        };

        Vector2 scroll;
        int selectedPreset;
        List<VARCOGameMakerWindow.OneClickCardReadinessLine> lastReadiness;

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/프리셋 만들기", priority = -99)]
        public static void Open()
        {
            var window = GetWindow<VARCOPresetMakerWindow>("VARCO 프리셋 만들기");
            window.minSize = new Vector2(500f, 520f);
            window.Focus();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(10f);

            EditorGUILayout.LabelField("VARCO 프리셋 만들기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "프리셋 키트의 역할 프리팹을 기준으로 게임 씬을 생성합니다. 캐릭터는 캐릭터 프리팹 생성기에서 역할별 애니메이션 파일을 직접 지정하는 것을 권장합니다.",
                MessageType.Info);

            DrawPresetSelector();
            DrawSelectedKitStatus();
            DrawAudioAndTtsStatus();

            GUILayout.Space(8f);
            if (GUILayout.Button("선택 프리셋 씬 생성", GUILayout.Height(38f)))
                VARCOGameMakerWindow.BuildTemplateGame(Presets[selectedPreset].template);

            if (GUILayout.Button("선택 프리셋 Windows 빌드 생성", GUILayout.Height(32f)))
            {
                if (EditorUtility.DisplayDialog("Windows 빌드 생성", "선택한 프리셋 씬을 만든 뒤 Windows 빌드를 생성합니다. 시간이 걸릴 수 있습니다.", "진행", "취소"))
                    VARCOGameMakerWindow.BuildTemplateGame(Presets[selectedPreset].template, buildWindows: true);
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("키트 프리팹 최신화", GUILayout.Height(30f)))
                VARCOGameMakerWindow.FillAllPresetKitPrefabsFromMenu();

            if (GUILayout.Button("현재 씬 자동 보정", GUILayout.Height(28f)))
                VARCOGameMakerWindow.FixCurrentSceneFromMenu();

            if (GUILayout.Button("현재 씬 건강 검사", GUILayout.Height(28f)))
                VARCOSceneHealthCheckWindow.Open();

            GUILayout.Space(8f);
            if (GUILayout.Button("프리셋 준비 상태 새로 고침", GUILayout.Height(28f)))
                RefreshReadiness();

            DrawReadiness();

            GUILayout.Space(10f);
            if (GUILayout.Button("레거시 세부 자동 제작 열기", GUILayout.Height(24f)))
                VARCOGameMakerWindow.Open();

            EditorGUILayout.EndScrollView();
        }

        void DrawPresetSelector()
        {
            var labels = Presets.Select(p => p.name).ToArray();
            selectedPreset = EditorGUILayout.Popup("프리셋", selectedPreset, labels);
            EditorGUILayout.HelpBox(Presets[selectedPreset].description, MessageType.None);
        }

        void DrawSelectedKitStatus()
        {
            var folder = Path.Combine(PresetKitRoot, KitFolderName(Presets[selectedPreset].template)).Replace('\\', '/');
            var fullFolder = Path.GetFullPath(folder);
            if (!Directory.Exists(fullFolder))
            {
                EditorGUILayout.HelpBox("선택한 프리셋 키트 폴더가 아직 없습니다.", MessageType.Warning);
                return;
            }

            var prefabs = Directory.GetFiles(fullFolder, "*.prefab", SearchOption.AllDirectories);
            var realCount = prefabs.Count(path => !Path.GetFileNameWithoutExtension(path).Contains("_SLOT_PLACEHOLDER"));
            EditorGUILayout.HelpBox("선택 키트: " + folder + "\n역할 프리팹: " + realCount + "개", MessageType.None);

            if (GUILayout.Button("선택 키트 폴더 열기", GUILayout.Height(24f)))
                EditorUtility.RevealInFinder(fullFolder);
        }

        void DrawAudioAndTtsStatus()
        {
            var bgmCount = CountAudioFiles(BgmFolder);
            var sfxCount = CountAudioFiles(SfxFolder);
            var ttsCount = CountAudioFiles(TtsFolder);
            var registryReady = File.Exists(Path.GetFullPath(SoundRegistryPath));

            GUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "사운드 준비 상태\n"
                + "- BGM: " + bgmCount + "개 / SFX: " + sfxCount + "개 / TTS: " + ttsCount + "개\n"
                + "- 사운드 레지스트리: " + (registryReady ? "생성됨" : "프리셋 생성 시 자동 생성")
                + "\n- TTS는 API 수업 전 음성 파일을 넣어두는 선택 폴더입니다.",
                ttsCount > 0 ? MessageType.None : MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("오디오/TTS 폴더 준비", GUILayout.Height(24f)))
                {
                    EnsureAudioFolders();
                    AssetDatabase.Refresh();
                }

                if (GUILayout.Button("TTS 폴더 열기", GUILayout.Height(24f)))
                {
                    EnsureAudioFolders();
                    EditorUtility.RevealInFinder(Path.GetFullPath(TtsFolder));
                }
            }
        }

        void RefreshReadiness()
        {
            lastReadiness = VARCOGameMakerWindow.BuildOneClickCardReadinessSummaries(Presets.Select(p => p.template));
        }

        void DrawReadiness()
        {
            if (lastReadiness == null || lastReadiness.Count == 0)
                return;

            GUILayout.Space(8f);
            EditorGUILayout.LabelField("프리셋 준비 상태", EditorStyles.boldLabel);
            foreach (var line in lastReadiness)
            {
                var marker = line.recommended ? "추천: " : string.Empty;
                EditorGUILayout.LabelField(marker + line.title + " - " + line.stateLabel, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(line.summary, EditorStyles.wordWrappedMiniLabel);
            }
        }

        static string KitFolderName(VARCOGameMakerWindow.BlockTemplate template)
        {
            return GenrePrefix(template) + "_" + template;
        }

        static void EnsureAudioFolders()
        {
            EnsureFolder(AudioRoot);
            EnsureFolder(BgmFolder);
            EnsureFolder(SfxFolder);
            EnsureFolder(TtsFolder);
        }

        static void EnsureFolder(string path)
        {
            path = path.Replace("\\", "/").TrimEnd('/');
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

        static int CountAudioFiles(string folder)
        {
            var fullFolder = Path.GetFullPath(folder);
            if (!Directory.Exists(fullFolder))
                return 0;

            return Directory.GetFiles(fullFolder, "*.*", SearchOption.AllDirectories)
                .Count(IsAudioFile);
        }

        static bool IsAudioFile(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return AudioExtensions.Contains(extension);
        }

        static string GenrePrefix(VARCOGameMakerWindow.BlockTemplate template)
        {
            switch (template)
            {
                case VARCOGameMakerWindow.BlockTemplate.ArenaCombatWave:
                case VARCOGameMakerWindow.BlockTemplate.SurvivalTimer:
                case VARCOGameMakerWindow.BlockTemplate.ArenaBossBattle:
                    return "Arena";
                case VARCOGameMakerWindow.BlockTemplate.PuzzleDoorRoom:
                case VARCOGameMakerWindow.BlockTemplate.PuzzleEscapeRoom:
                    return "Puzzle";
                case VARCOGameMakerWindow.BlockTemplate.PlatformSpaceCourse:
                case VARCOGameMakerWindow.BlockTemplate.PlatformObstacleRun:
                    return "Platform";
                default:
                    return "Exploration";
            }
        }

        struct PresetOption
        {
            public readonly string name;
            public readonly VARCOGameMakerWindow.BlockTemplate template;
            public readonly string description;

            public PresetOption(string name, VARCOGameMakerWindow.BlockTemplate template, string description)
            {
                this.name = name;
                this.template = template;
                this.description = description;
            }
        }
    }
}
#endif
