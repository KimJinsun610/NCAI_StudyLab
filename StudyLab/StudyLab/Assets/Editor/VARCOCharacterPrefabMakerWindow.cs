#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VARCO_Workshop.Editor
{
    public class VARCOCharacterPrefabMakerWindow : EditorWindow
    {
        readonly VARCOCharacterPrefabBuildSettings settings = new VARCOCharacterPrefabBuildSettings();
        readonly List<string> logs = new List<string>();
        Vector2 scroll;
        Vector2 sourceScroll;
        Vector2 logScroll;
        bool advancedSourcesOpen;

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/캐릭터 프리팹 생성기", priority = -99)]
        public static void Open()
        {
            var window = GetWindow<VARCOCharacterPrefabMakerWindow>("VARCO 캐릭터 프리팹 생성기");
            window.minSize = new Vector2(620f, 720f);
            window.Focus();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(10f);

            EditorGUILayout.LabelField("VARCO 캐릭터 프리팹 생성기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "캐릭터 모델과 애니메이션이 들어 있는 USDZ/FBX/GLB 파일들을 모아 하나의 게임용 캐릭터 프리팹과 AnimatorController를 생성합니다. Avatar가 없는 클립은 Generic 리그로 보고 방향 정규화를 적용합니다.",
                MessageType.Info);

            DrawCharacterSettings();
            GUILayout.Space(8f);
            DrawSourceTools();
            GUILayout.Space(8f);
            DrawOutputSettings();
            GUILayout.Space(8f);
            DrawActions();
            GUILayout.Space(8f);
            DrawLogs();

            EditorGUILayout.EndScrollView();
        }

        void DrawCharacterSettings()
        {
            EditorGUILayout.LabelField("1. 캐릭터 선택", EditorStyles.boldLabel);
            settings.characterKind = (VARCOCharacterPrefabKind)EditorGUILayout.EnumPopup("캐릭터 프리셋", settings.characterKind);
            settings.modelAsset = (GameObject)EditorGUILayout.ObjectField("대표 모델", settings.modelAsset, typeof(GameObject), false);
            settings.characterName = EditorGUILayout.TextField("생성 이름", settings.characterName);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("선택 모델 사용", GUILayout.Height(24f)))
                {
                    var selectedModel = Selection.objects
                        .Select(AssetDatabase.GetAssetPath)
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                        .FirstOrDefault(go => go);
                    if (selectedModel)
                    {
                        settings.modelAsset = selectedModel;
                        if (string.IsNullOrWhiteSpace(settings.characterName) || settings.characterName == "VARCO_Character")
                            settings.characterName = selectedModel.name;
                    }
                }

                if (GUILayout.Button("VARCO3DImports에서 첫 모델 찾기", GUILayout.Height(24f)))
                    PickFirstModelFromImports();
            }
        }

        void DrawSourceTools()
        {
            EditorGUILayout.LabelField("2. 역할별 애니메이션 파일", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("각 슬롯에 해당 동작이 들어있는 USDZ/FBX/GLB/GLTF/AnimationClip 파일을 직접 넣으세요. 수동 슬롯이 항상 자동 추론보다 우선합니다.", MessageType.None);

            DrawRoleSourceField("Idle", ref settings.idleAnimationSource);
            DrawRoleSourceField("Walk", ref settings.walkAnimationSource);
            DrawRoleSourceField("Run", ref settings.runAnimationSource);
            DrawRoleSourceField("Jump", ref settings.jumpAnimationSource);
            DrawRoleSourceField("Attack", ref settings.attackAnimationSource);
            DrawRoleSourceField("Dead", ref settings.deadAnimationSource);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("선택 파일을 빈 슬롯에 자동 배치", GUILayout.Height(26f)))
                    FillRoleSlotsFromSelection();
                if (GUILayout.Button("역할 슬롯 비우기", GUILayout.Height(26f)))
                    ClearRoleSlots();
            }

            advancedSourcesOpen = EditorGUILayout.Foldout(advancedSourcesOpen, "빈 슬롯 자동 보조 소스", true);
            if (!advancedSourcesOpen)
                return;

            EditorGUILayout.HelpBox("비어 있는 역할만 자동으로 채우고 싶을 때 추가 소스 목록을 사용하세요. 직접 지정한 슬롯은 덮어쓰지 않습니다.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("선택 에셋 추가", GUILayout.Height(26f)))
                    AddSelectionToSources();
                if (GUILayout.Button("목록 비우기", GUILayout.Height(26f)))
                    settings.animationSources.Clear();
            }

            settings.useVarcoImportsAsFallback = EditorGUILayout.ToggleLeft("빈 슬롯이 있으면 Assets/VARCO3DImports도 보조 스캔", settings.useVarcoImportsAsFallback);

            sourceScroll = EditorGUILayout.BeginScrollView(sourceScroll, GUILayout.MinHeight(90f), GUILayout.MaxHeight(170f));
            for (int i = 0; i < settings.animationSources.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    settings.animationSources[i] = EditorGUILayout.ObjectField(settings.animationSources[i], typeof(Object), false);
                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                    {
                        settings.animationSources.RemoveAt(i);
                        i--;
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawOutputSettings()
        {
            EditorGUILayout.LabelField("3. 출력 / Generic 보정", EditorStyles.boldLabel);
            settings.outputRoot = EditorGUILayout.TextField("캐릭터 프리팹 출력", settings.outputRoot);
            settings.animationOutputRoot = EditorGUILayout.TextField("애니메이션 출력", settings.animationOutputRoot);
            settings.overwriteExisting = EditorGUILayout.ToggleLeft("같은 이름의 생성물 덮어쓰기", settings.overwriteExisting);
            settings.normalizeGenericDirection = EditorGUILayout.ToggleLeft("Avatar 없는 Generic 클립 방향 정규화", settings.normalizeGenericDirection);
            using (new EditorGUI.DisabledScope(!settings.normalizeGenericDirection))
                settings.removeGenericRootXZMotion = EditorGUILayout.ToggleLeft("Generic 루트 XZ 이동 제거", settings.removeGenericRootXZMotion);
        }

        void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("스캔 리포트", GUILayout.Height(32f)))
                    Scan();
                if (GUILayout.Button("캐릭터 프리팹 생성", GUILayout.Height(36f)))
                    Build();
            }
        }

        void DrawLogs()
        {
            EditorGUILayout.LabelField("작업 로그", EditorStyles.boldLabel);
            if (logs.Count == 0)
            {
                EditorGUILayout.HelpBox("아직 실행 내역이 없습니다.", MessageType.None);
                return;
            }

            logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.MinHeight(140f), GUILayout.MaxHeight(260f));
            foreach (var line in logs)
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("로그 지우기", GUILayout.Height(22f)))
                logs.Clear();
        }

        void DrawRoleSourceField(string label, ref Object source)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                source = EditorGUILayout.ObjectField(label, source, typeof(Object), false);
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                    source = null;
            }
        }

        void FillRoleSlotsFromSelection()
        {
            foreach (var obj in Selection.objects)
            {
                if (!obj)
                    continue;

                var role = GuessRoleFromObject(obj);
                if (string.IsNullOrEmpty(role))
                {
                    AddUniqueSource(obj);
                    continue;
                }

                AssignRoleSlotIfEmpty(role, obj);
            }
        }

        void ClearRoleSlots()
        {
            settings.idleAnimationSource = null;
            settings.walkAnimationSource = null;
            settings.runAnimationSource = null;
            settings.jumpAnimationSource = null;
            settings.attackAnimationSource = null;
            settings.deadAnimationSource = null;
        }

        static string GuessRoleFromObject(Object obj)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            var text = (obj.name + " " + path).ToLowerInvariant();

            if (ContainsAny(text, "idle", "wait", "stand"))
                return "Idle";
            if (ContainsAny(text, "walk", "walking"))
                return "Walk";
            if (ContainsAny(text, "run", "running", "sprint", "dash"))
                return "Run";
            if (ContainsAny(text, "jump", "leap", "fall"))
                return "Jump";
            if (ContainsAny(text, "attack", "atk", "slash", "punch", "kick", "shoot", "hit"))
                return "Attack";
            if (ContainsAny(text, "dead", "death", "die", "dying"))
                return "Dead";

            return null;
        }

        void AssignRoleSlotIfEmpty(string role, Object obj)
        {
            switch (role)
            {
                case "Idle":
                    if (!settings.idleAnimationSource)
                        settings.idleAnimationSource = obj;
                    break;
                case "Walk":
                    if (!settings.walkAnimationSource)
                        settings.walkAnimationSource = obj;
                    break;
                case "Run":
                    if (!settings.runAnimationSource)
                        settings.runAnimationSource = obj;
                    break;
                case "Jump":
                    if (!settings.jumpAnimationSource)
                        settings.jumpAnimationSource = obj;
                    break;
                case "Attack":
                    if (!settings.attackAnimationSource)
                        settings.attackAnimationSource = obj;
                    break;
                case "Dead":
                    if (!settings.deadAnimationSource)
                        settings.deadAnimationSource = obj;
                    break;
            }
        }

        static bool ContainsAny(string text, params string[] needles)
        {
            foreach (var needle in needles)
                if (text.Contains(needle))
                    return true;
            return false;
        }

        void AddSelectionToSources()
        {
            foreach (var obj in Selection.objects)
            {
                AddUniqueSource(obj);
            }
        }

        void AddUniqueSource(Object obj)
        {
            if (!obj || settings.animationSources.Contains(obj))
                return;

            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrWhiteSpace(path))
                return;

            settings.animationSources.Add(obj);
        }

        void AddFolder(string folder)
        {
            var folderAsset = AssetDatabase.LoadAssetAtPath<Object>(folder);
            if (folderAsset && !settings.animationSources.Contains(folderAsset))
                settings.animationSources.Add(folderAsset);
        }

        void PickFirstModelFromImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/VARCO3DImports" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".usdz" && ext != ".fbx" && ext != ".glb" && ext != ".gltf" && ext != ".prefab")
                    continue;

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!model || model.GetComponentsInChildren<Renderer>(true).Length == 0)
                    continue;

                settings.modelAsset = model;
                settings.characterName = model.name;
                return;
            }
        }

        void Scan()
        {
            logs.Clear();
            var scanSources = GatherScanSources();
            var report = VARCOCharacterPrefabBuildUtility.BuildScanReport(scanSources, settings.useVarcoImportsAsFallback);
            logs.AddRange(report.Split('\n'));
            Debug.Log(report);
        }

        List<Object> GatherScanSources()
        {
            var scanSources = new List<Object>();
            AddScanSource(scanSources, settings.idleAnimationSource);
            AddScanSource(scanSources, settings.walkAnimationSource);
            AddScanSource(scanSources, settings.runAnimationSource);
            AddScanSource(scanSources, settings.jumpAnimationSource);
            AddScanSource(scanSources, settings.attackAnimationSource);
            AddScanSource(scanSources, settings.deadAnimationSource);
            foreach (var source in settings.animationSources)
                AddScanSource(scanSources, source);
            return scanSources;
        }

        static void AddScanSource(List<Object> sources, Object source)
        {
            if (!source || sources.Contains(source))
                return;
            sources.Add(source);
        }

        void Build()
        {
            logs.Clear();
            var result = VARCOCharacterPrefabBuildUtility.Build(settings);
            logs.AddRange(result.logs);
            Debug.Log(result.Report);

            if (result.prefab)
            {
                Selection.activeObject = result.prefab;
                EditorGUIUtility.PingObject(result.prefab);
            }
        }
    }
}
#endif
