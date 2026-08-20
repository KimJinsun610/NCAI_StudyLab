#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VWS = VARCO_Workshop;
using Object = UnityEngine.Object;

namespace VARCO_Workshop.Editor
{
    public class VARCOClassPipelineWindow : EditorWindow
    {
        const string AudioRoot = "Assets/Audio";
        const string BgmFolder = "Assets/Audio/BGM";
        const string SfxFolder = "Assets/Audio/SFX";
        const string AmbientFolder = "Assets/Audio/Ambient";
        const string RegistryPath = "Assets/ScriptableObjects/SoundEvents/WorkshopSoundEventRegistry.asset";
        const string ProfileFolder = "Assets/ScriptableObjects/GameProfiles";

        static readonly Dictionary<VWS.GenreType, string> SceneByGenre = new Dictionary<VWS.GenreType, string>
        {
            { VWS.GenreType.Platform, "Assets/Scenes/VARCO_Platform/VARCO_Platform_Space3D.unity" },
            { VWS.GenreType.Arena, "Assets/Scenes/VARCO_Arena/VARCO_Arena_Example.unity" },
            { VWS.GenreType.Exploration, "Assets/Scenes/VARCO_Exploration/VARCO_Exploration_Example.unity" },
            { VWS.GenreType.Puzzle, "Assets/Scenes/VARCO_Puzzle/VARCO_Puzzle_Example.unity" }
        };

        static readonly Dictionary<VWS.GenreType, string> ProfileByGenre = new Dictionary<VWS.GenreType, string>
        {
            { VWS.GenreType.Platform, "Assets/ScriptableObjects/GameProfiles/VARCO_Platform_Profile.asset" },
            { VWS.GenreType.Arena, "Assets/ScriptableObjects/GameProfiles/VARCO_Arena_Profile.asset" },
            { VWS.GenreType.Exploration, "Assets/ScriptableObjects/GameProfiles/VARCO_Exploration_Profile.asset" },
            { VWS.GenreType.Puzzle, "Assets/ScriptableObjects/GameProfiles/VARCO_Puzzle_Profile.asset" }
        };

        static readonly VWS.GenreType[] GenreOptions =
        {
            VWS.GenreType.Platform,
            VWS.GenreType.Arena,
            VWS.GenreType.Exploration,
            VWS.GenreType.Puzzle
        };

        static readonly GUIContent[] GenreLabels =
        {
            new GUIContent("플랫폼"),
            new GUIContent("전투 아레나"),
            new GUIContent("탐험"),
            new GUIContent("퍼즐")
        };

        readonly List<string> logLines = new List<string>();
        Vector2 scroll;
        VWS.GenreType selectedGenre = VWS.GenreType.Platform;

        public static void Open()
        {
            var window = GetWindow<VARCOClassPipelineWindow>("수업용 자동 준비");
            window.minSize = new Vector2(520, 640);
        }

        public static void RunClassPipelineSetup()
        {
            var openedScenePath = SceneManager.GetActiveScene().path;
            var log = new List<string>();

            EnsureClassFolders(log);
            var registry = SyncAudioRegistry(log);
            EnsureGenreProfiles(log);
            ApplyDefaultsToBuildScenes(registry, log);
            RunBlockCodingSafetyPassForBuildScenes(log);
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrWhiteSpace(openedScenePath) && File.Exists(openedScenePath))
                EditorSceneManager.OpenScene(openedScenePath, OpenSceneMode.Single);

            Debug.Log("[VARCO 수업용 자동 준비]\n" + string.Join("\n", log));
        }

        void OnEnable()
        {
            logLines.Clear();
            logLines.Add("준비됨: 처음 사용자는 위에서부터 순서대로 누르면 됩니다.");
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(8);

            EditorGUILayout.LabelField("VARCO 수업용 자동 준비", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "씬 열기, 에셋 폴더 준비, 사운드 동기화, HUD, 안전 보정까지 한 곳에서 처리합니다. 코딩을 모르는 사용자는 이 창과 게임 메이커만 쓰면 됩니다.",
                MessageType.Info);

            DrawStepOpenScene();
            DrawStepPrepareProject();
            DrawStepCurrentScene();
            DrawStepVerification();
            DrawLog();

            EditorGUILayout.EndScrollView();
        }

        void DrawStepOpenScene()
        {
            DrawHeader("1. 장르 씬 열기");
            selectedGenre = DrawGenrePopup("목표 장르", selectedGenre);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("선택한 씬 열기", GUILayout.Height(30)))
                    OpenGenreScene(selectedGenre);
                if (GUILayout.Button("씬 파일 찾기", GUILayout.Height(30)))
                    PingSceneAsset(SceneByGenre[selectedGenre]);
            }

            EditorGUILayout.HelpBox(
                "처음 시연은 플랫폼 씬을 추천합니다. 이동, 아이템, 위험 구역, 체크포인트, 움직이는 발판, BGM, 목표 흐름이 한 번에 확인됩니다.",
                MessageType.None);
        }

        void DrawStepPrepareProject()
        {
            DrawHeader("2. 에셋 폴더/사운드 준비");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("필수 폴더 만들기", GUILayout.Height(30)))
                {
                    logLines.Clear();
                    EnsureClassFolders(logLines);
                    AssetDatabase.SaveAssets();
                }

                if (GUILayout.Button("사운드 자동 등록", GUILayout.Height(30)))
                {
                    logLines.Clear();
                    SyncAudioRegistry(logLines);
                    AssetDatabase.SaveAssets();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("장르 프로필 만들기", GUILayout.Height(30)))
                {
                    logLines.Clear();
                    EnsureGenreProfiles(logLines);
                    AssetDatabase.SaveAssets();
                }

                if (GUILayout.Button("전체 준비 실행", GUILayout.Height(30)))
                {
                    logLines.Clear();
                    EnsureClassFolders(logLines);
                    var registry = SyncAudioRegistry(logLines);
                    EnsureGenreProfiles(logLines);
                    ApplyDefaultsToBuildScenes(registry, logLines);
                    RunBlockCodingSafetyPassForBuildScenes(logLines);
                    AssetDatabase.SaveAssets();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("3D 에셋 폴더 보기", GUILayout.Height(28)))
                    PingFolder("Assets/VARCO3DImports");
                if (GUILayout.Button("사운드 폴더 보기", GUILayout.Height(28)))
                    PingFolder(AudioRoot);
            }
        }

        void DrawStepCurrentScene()
        {
            DrawHeader("3. 현재 씬 자동 연결");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("게임 메이커 열기", GUILayout.Height(30)))
                    VARCOGameMakerWindow.Open();
                if (GUILayout.Button("한글 블록 조립기", GUILayout.Height(30)))
                    VARCOBlockCodingBuilderWindow.Open();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("선택 모델 자동 판단 연결", GUILayout.Height(30)))
                    VARCOBlockCodingBuilderWindow.AutoConnectSelectionMenu();
                if (GUILayout.Button("선택 에셋/폴더 배치+연결", GUILayout.Height(30)))
                    VARCOBlockCodingBuilderWindow.PlaceAndAutoConnectSelectedAssetsMenu();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("사운드 연결", GUILayout.Height(30)))
                    VARCOSoundConnectorWindow.Open();
                if (GUILayout.Button("애니메이션 연결", GUILayout.Height(30)))
                    VARCOAnimationSetupWindow.Open();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("HUD 추가/정리", GUILayout.Height(30)))
                {
                    logLines.Clear();
                    EnsureWorkshopHudInCurrentScene(logLines);
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }
                if (GUILayout.Button("현재 씬 안전 보정", GUILayout.Height(30)))
                {
                    logLines.Clear();
                    RunBlockCodingSafetyPassForCurrentScene(logLines, saveScene: false);
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }
            }
        }

        void DrawStepVerification()
        {
            DrawHeader("4. 플레이 전 검증");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("씬 건강 검사", GUILayout.Height(30)))
                    VARCOSceneHealthCheckWindow.Open();
                if (GUILayout.Button("학생 체크리스트 복사", GUILayout.Height(30)))
                {
                    EditorGUIUtility.systemCopyBuffer =
                        "1. VARCO > 간략 자동 제작 또는 VARCO > 세부 자동 제작에서 원하는 제작 메뉴를 실행합니다.\n" +
                        "2. 생성된 씬을 확인합니다.\n" +
                        "3. GLB/FBX 모델은 Assets/VARCO3DImports에 넣습니다.\n" +
                        "4. WAV/MP3 사운드는 Assets/Audio/BGM 또는 Assets/Audio/SFX에 넣습니다.\n" +
                        "5. VARCO > 세부 자동 제작 > 블록코딩 > 선택 프리팹에 기능 추가로 필요한 기능을 보강합니다.\n" +
                        "6. VARCO > 세부 자동 제작 > 검증 > 현재 씬 건강 검사에서 오류가 없는지 확인합니다.\n" +
                        "7. Play를 눌러 테스트합니다.";
                    Log("학생 체크리스트를 클립보드에 복사했습니다.");
                }
            }
        }

        void DrawLog()
        {
            if (logLines.Count == 0)
                return;

            DrawHeader("작업 기록");
            EditorGUILayout.TextArea(string.Join("\n", logLines), GUILayout.MinHeight(130));
        }

        static void DrawHeader(string text)
        {
            GUILayout.Space(12);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        static VWS.GenreType DrawGenrePopup(string label, VWS.GenreType current)
        {
            var index = Array.IndexOf(GenreOptions, current);
            if (index < 0)
                index = 0;

            index = EditorGUILayout.Popup(new GUIContent(label), index, GenreLabels);
            return GenreOptions[Mathf.Clamp(index, 0, GenreOptions.Length - 1)];
        }

        static string GenreLabel(VWS.GenreType genre)
        {
            switch (genre)
            {
                case VWS.GenreType.Arena:
                    return "전투 아레나";
                case VWS.GenreType.Exploration:
                    return "탐험";
                case VWS.GenreType.Puzzle:
                    return "퍼즐";
                default:
                    return "플랫폼";
            }
        }

        void OpenGenreScene(VWS.GenreType genre)
        {
            var scenePath = SceneByGenre[genre];
            if (!File.Exists(scenePath))
            {
                Log($"씬 파일을 찾을 수 없습니다: {scenePath}");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Log($"{GenreLabel(genre)} 씬을 열었습니다: {scenePath}");
        }

        static void PingSceneAsset(string scenePath)
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (!scene)
                return;

            Selection.activeObject = scene;
            EditorGUIUtility.PingObject(scene);
        }

        static void PingFolder(string path)
        {
            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (!obj)
                return;

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        static void EnsureClassFolders(List<string> log)
        {
            EnsureFolder("Assets", "VARCO3DImports", log);
            EnsureFolder("Assets", "Audio", log);
            EnsureFolder(AudioRoot, "BGM", log);
            EnsureFolder(AudioRoot, "SFX", log);
            EnsureFolder(AudioRoot, "Ambient", log);
            EnsureFolder("Assets", "ScriptableObjects", log);
            EnsureFolder("Assets/ScriptableObjects", "SoundEvents", log);
            EnsureFolder("Assets/ScriptableObjects", "GameProfiles", log);
            EnsureFolder("Assets", "Scenes", log);
            EnsureFolder("Assets", "Documentation", log);
            AssetDatabase.Refresh();
        }

        static VWS.SoundEventRegistry SyncAudioRegistry(List<string> log)
        {
            EnsureFolder("Assets", "ScriptableObjects", log);
            EnsureFolder("Assets/ScriptableObjects", "SoundEvents", log);

            var registry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(RegistryPath);
            if (!registry)
            {
                registry = CreateInstance<VWS.SoundEventRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
                log.Add($"사운드 레지스트리를 만들었습니다: {RegistryPath}");
            }

            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(path => path)
                .Select(path => new { path, clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path) })
                .Where(item => item.clip != null)
                .ToList();

            Undo.RecordObject(registry, "VARCO 사운드 목록 동기화");

            var added = 0;
            var updated = 0;
            foreach (var item in clips)
            {
                var id = BuildSoundId(item.path);
                var entry = registry.events.FirstOrDefault(e => e != null && e.id == id);
                if (entry == null)
                {
                    entry = new VWS.SoundEventRegistry.Entry
                    {
                        id = id,
                        volume = DefaultVolumeFor(id)
                    };
                    registry.events.Add(entry);
                    added++;
                }
                else if (entry.clip != item.clip)
                {
                    updated++;
                }

                entry.clip = item.clip;
                if (entry.volume <= 0f || ShouldUseClassDefaultVolume(entry, id))
                    entry.volume = DefaultVolumeFor(id);
            }

            EditorUtility.SetDirty(registry);
            log.Add($"사운드 자동 등록 완료: 클립 {clips.Count}개, 추가 {added}개, 갱신 {updated}개.");
            return registry;
        }

        static void EnsureGenreProfiles(List<string> log)
        {
            EnsureFolder("Assets", "ScriptableObjects", log);
            EnsureFolder("Assets/ScriptableObjects", "GameProfiles", log);

            foreach (var pair in ProfileByGenre)
            {
                var profile = AssetDatabase.LoadAssetAtPath<VWS.GameProfile>(pair.Value);
                if (!profile)
                {
                    profile = CreateInstance<VWS.GameProfile>();
                    AssetDatabase.CreateAsset(profile, pair.Value);
                    log.Add($"{GenreLabel(pair.Key)} 장르 프로필을 만들었습니다: {pair.Value}");
                }

                ConfigureProfile(profile, pair.Key);
                EditorUtility.SetDirty(profile);
            }
        }

        static void ApplyDefaultsToBuildScenes(VWS.SoundEventRegistry registry, List<string> log)
        {
            var originalScene = SceneManager.GetActiveScene().path;
            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path) && File.Exists(scene.path))
                .Select(scene => scene.path)
                .Distinct()
                .ToList();

            foreach (var scenePath in enabledScenes)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var genre = GuessGenre(scenePath);
                var changed = false;

                changed |= ConfigureGameManager(genre, log);
                changed |= EnsureWorkshopHudInCurrentScene(log);
                changed |= FixSoundTriggers(registry, log);
                changed |= EnsureSceneBgm(registry, genre, log);

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    log.Add($"씬을 저장했습니다: {scenePath}");
                }
                else
                {
                    log.Add($"수정할 항목이 없는 씬입니다: {scenePath}");
                }
            }

            if (!string.IsNullOrWhiteSpace(originalScene) && File.Exists(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        }

        static bool ConfigureGameManager(VWS.GenreType genre, List<string> log)
        {
            var gm = Object.FindFirstObjectByType<VWS.GameManager>();
            if (!gm)
            {
                var root = new GameObject("VW_Bootstrap");
                gm = root.AddComponent<VWS.GameManager>();
                root.AddComponent<VWS.SceneBootstrap>();
                log.Add("VW_Bootstrap과 GameManager를 만들었습니다.");
            }

            var changed = false;
            var profile = AssetDatabase.LoadAssetAtPath<VWS.GameProfile>(ProfileByGenre[genre]);
            if (gm.profile != profile)
            {
                gm.profile = profile;
                changed = true;
            }

            if (gm.loadResultScenes)
            {
                gm.loadResultScenes = false;
                changed = true;
            }

            if (gm.clearSceneName != "Clear")
            {
                gm.clearSceneName = "Clear";
                changed = true;
            }

            if (gm.gameOverSceneName != "GameOver")
            {
                gm.gameOverSceneName = "GameOver";
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(gm);

            return changed;
        }

        static bool EnsureWorkshopHudInCurrentScene(List<string> log)
        {
            var hudType = Type.GetType("VARCO_Workshop.VARCOGameHUD, Assembly-CSharp");
            if (hudType == null)
            {
                log.Add("VARCOGameHUD가 아직 컴파일되지 않았습니다. Unity 스크립트 갱신 뒤 다시 실행하세요.");
                return false;
            }

            var changed = false;
            var existing = Object.FindFirstObjectByType(hudType);
            if (!existing)
            {
                var go = new GameObject("VARCO_GameHUD");
                var hud = go.AddComponent(hudType);
                var fallbackGenre = hudType.GetField("fallbackGenre");
                fallbackGenre?.SetValue(hud, GuessGenre(SceneManager.GetActiveScene().path));
                log.Add("VARCO_GameHUD를 추가했습니다.");
                changed = true;
            }

            foreach (var legacy in Object.FindObjectsByType<VWS.WorkshopHUD>(FindObjectsSortMode.None))
            {
                if (!legacy.showDuringPlay)
                    continue;

                legacy.showDuringPlay = false;
                EditorUtility.SetDirty(legacy);
                changed = true;
            }

            return changed;
        }

        static bool FixSoundTriggers(VWS.SoundEventRegistry registry, List<string> log)
        {
            if (!registry)
                return false;

            var changed = false;
            foreach (var trigger in Object.FindObjectsByType<VWS.SoundEventTrigger>(FindObjectsSortMode.None))
            {
                if (trigger.registry != registry)
                {
                    trigger.registry = registry;
                    changed = true;
                }

                if (!trigger.fallbackClip)
                    continue;

                var fallbackPath = AssetDatabase.GetAssetPath(trigger.fallbackClip);
                var fallbackId = BuildSoundId(fallbackPath);
                var idMissing = string.IsNullOrWhiteSpace(trigger.eventId) || !RegistryHasClip(registry, trigger.eventId);
                var genericCheckpointId = trigger.eventId == "sfx_checkpoint" && fallbackId != "sfx_checkpoint";

                if (idMissing || genericCheckpointId)
                {
                    trigger.eventId = fallbackId;
                    changed = true;
                }

                EditorUtility.SetDirty(trigger);
            }

            if (changed)
                log.Add("현재 씬의 SoundEventTrigger 레지스트리/이벤트 ID를 정리했습니다.");

            return changed;
        }

        static bool EnsureSceneBgm(VWS.SoundEventRegistry registry, VWS.GenreType genre, List<string> log)
        {
            var existing = GameObject.Find("VW_Audio_BGM");
            var existingSource = existing != null ? existing.GetComponent<AudioSource>() : null;
            if (existingSource != null && existingSource.clip != null)
                return false;

            var bgmClip = FindBgmClip(registry, genre);
            if (!bgmClip)
                return false;

            var go = existing ? existing : new GameObject("VW_Audio_BGM");
            var source = existingSource != null ? existingSource : go.AddComponent<AudioSource>();
            source.clip = bgmClip;
            source.playOnAwake = true;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0.45f;
            EditorUtility.SetDirty(go);
            log.Add($"{GenreLabel(genre)} BGM을 연결했습니다: {bgmClip.name}");
            return true;
        }

        static AudioClip FindBgmClip(VWS.SoundEventRegistry registry, VWS.GenreType genre)
        {
            var preferred = PreferredBgmIds(genre);
            if (registry)
            {
                foreach (var id in preferred)
                    if (registry.TryGet(id, out var clip, out _) && clip)
                        return clip;
            }

            foreach (var id in preferred)
            {
                var guid = AssetDatabase.FindAssets($"{id} t:AudioClip", new[] { BgmFolder }).FirstOrDefault();
                if (string.IsNullOrEmpty(guid))
                    continue;

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip)
                    return clip;
            }

            return null;
        }

        static IEnumerable<string> PreferredBgmIds(VWS.GenreType genre)
        {
            switch (genre)
            {
                case VWS.GenreType.Arena:
                    yield return "bgm_arena_battle_loop";
                    yield return "bgm_battle_loop";
                    yield return "bgm_arena_bgm1";
                    break;
                case VWS.GenreType.Exploration:
                    yield return "bgm_exploration_loop";
                    break;
                case VWS.GenreType.Puzzle:
                    yield return "bgm_puzzle_loop";
                    break;
                default:
                    yield return "bgm_platform_space_loop";
                    break;
            }
        }

        static VWS.GenreType GuessGenre(string scenePath)
        {
            var lower = scenePath.ToLowerInvariant();
            if (lower.Contains("arena")) return VWS.GenreType.Arena;
            if (lower.Contains("exploration")) return VWS.GenreType.Exploration;
            if (lower.Contains("puzzle")) return VWS.GenreType.Puzzle;
            return VWS.GenreType.Platform;
        }

        static void ConfigureProfile(VWS.GameProfile profile, VWS.GenreType genre)
        {
            profile.genre = genre;

            switch (genre)
            {
                case VWS.GenreType.Arena:
                    profile.playerMaxHP = 100;
                    profile.waveCount = 3;
                    profile.itemGoal = 0;
                    profile.clearCondition = VWS.CompletionCondition.DefeatWaves;
                    profile.designNotes = "수업용 데모: 플레이어/적 모델을 바꾼 뒤 공격, HP, 웨이브, BGM을 확인하세요.";
                    break;
                case VWS.GenreType.Exploration:
                    profile.playerMaxHP = 100;
                    profile.waveCount = 0;
                    profile.itemGoal = 3;
                    profile.clearCondition = VWS.CompletionCondition.CollectItems;
                    profile.designNotes = "수업용 데모: 수집 아이템을 경로에 배치하고 목표 지점까지 이동하세요.";
                    break;
                case VWS.GenreType.Puzzle:
                    profile.playerMaxHP = 100;
                    profile.waveCount = 0;
                    profile.itemGoal = 0;
                    profile.clearCondition = VWS.CompletionCondition.ReachGoal;
                    profile.designNotes = "수업용 데모: Play 전에 상자, 발판, 문, 목표 오브젝트를 연결하세요.";
                    break;
                default:
                    profile.playerMaxHP = 100;
                    profile.waveCount = 0;
                    profile.itemGoal = 4;
                    profile.clearCondition = VWS.CompletionCondition.ReachGoal;
                    profile.designNotes = "수업용 데모: 이동, 점프, 위험 구역, 체크포인트, 아이템, 움직이는 발판, 목표를 확인하세요.";
                    break;
            }
        }

        static string BuildSoundId(string assetPath)
        {
            var file = Path.GetFileNameWithoutExtension(assetPath);
            var id = Regex.Replace(file.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
            id = Regex.Replace(id, @"_+", "_");

            if (id.StartsWith("sfx_") || id.StartsWith("bgm_") || id.StartsWith("amb_"))
                return id;

            var normalizedPath = assetPath.Replace('\\', '/').ToLowerInvariant();
            if (normalizedPath.Contains("/bgm/"))
                return "bgm_" + id;
            if (normalizedPath.Contains("/ambient/"))
                return "amb_" + id;
            return "sfx_" + id;
        }

        static float DefaultVolumeFor(string id)
        {
            if (id.StartsWith("bgm_", StringComparison.Ordinal)) return 0.45f;
            if (id.StartsWith("amb_", StringComparison.Ordinal)) return 0.65f;
            return 1f;
        }

        static bool ShouldUseClassDefaultVolume(VWS.SoundEventRegistry.Entry entry, string id)
        {
            if (entry == null)
                return false;

            if (id.StartsWith("bgm_", StringComparison.Ordinal))
                return entry.volume > 0.65f;
            if (id.StartsWith("amb_", StringComparison.Ordinal))
                return entry.volume > 0.8f;
            return false;
        }

        static bool RegistryHasClip(VWS.SoundEventRegistry registry, string id)
        {
            return registry && registry.events.Any(e => e != null && e.id == id && e.clip != null);
        }

        static void RunBlockCodingSafetyPassForBuildScenes(List<string> log)
        {
            var type = Type.GetType("VARCO_Workshop.Editor.VARCOBlockCodingBuilderWindow, Assembly-CSharp-Editor");
            var method = type?.GetMethod("RunSafetyPassForBuildScenes", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                log?.Add("Unity 스크립트 갱신 후 블록코딩 안전 보정을 사용할 수 있습니다.");
                return;
            }

            method.Invoke(null, new object[] { log });
        }

        static void RunBlockCodingSafetyPassForCurrentScene(List<string> log, bool saveScene)
        {
            var type = Type.GetType("VARCO_Workshop.Editor.VARCOBlockCodingBuilderWindow, Assembly-CSharp-Editor");
            var method = type?.GetMethod("RunSafetyPassForCurrentScene", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                log?.Add("Unity 스크립트 갱신 후 블록코딩 안전 보정을 사용할 수 있습니다.");
                return;
            }

            method.Invoke(null, new object[] { log, saveScene });
        }

        static void EnsureFolder(string parent, string folder, List<string> log)
        {
            var path = $"{parent}/{folder}";
            if (AssetDatabase.IsValidFolder(path))
                return;

            AssetDatabase.CreateFolder(parent, folder);
            log.Add($"폴더를 만들었습니다: {path}");
        }

        void Log(string message)
        {
            logLines.Add(message);
            Debug.Log("[VARCO 수업용 자동 준비] " + message);
        }
    }
}
#endif
