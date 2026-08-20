#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VARCO_Workshop.Editor
{
    public class VARCOAnimationSetupWindow : EditorWindow
    {
        enum AnimationPreset
        {
            ArenaPlayer,
            ArenaBoss,
            ArenaOrc,
            ExplorationPlayer,
            ExplorationZombie,
            PlatformPlayer,
            PuzzlePlayer,
            Custom
        }

        static readonly AnimationPreset[] PresetOptions =
        {
            AnimationPreset.ArenaPlayer,
            AnimationPreset.ArenaBoss,
            AnimationPreset.ArenaOrc,
            AnimationPreset.ExplorationPlayer,
            AnimationPreset.ExplorationZombie,
            AnimationPreset.PlatformPlayer,
            AnimationPreset.PuzzlePlayer,
            AnimationPreset.Custom
        };

        static readonly GUIContent[] PresetLabels =
        {
            new GUIContent("전투 플레이어"),
            new GUIContent("전투 보스"),
            new GUIContent("전투 오크"),
            new GUIContent("탐험 플레이어"),
            new GUIContent("탐험 좀비"),
            new GUIContent("플랫폼 플레이어"),
            new GUIContent("퍼즐 플레이어"),
            new GUIContent("직접 입력")
        };

        GameObject _selected;
        AnimationPreset _preset = AnimationPreset.ArenaPlayer;
        string _customPrefix = "";
        string _outputFolder = "Assets/Animations/Generated";
        bool _assignToSelected = true;
        bool _overwriteExisting = true;

        AnimationClip _idle;
        AnimationClip _walk;
        AnimationClip _attack;
        AnimationClip _attack2;
        AnimationClip _death;
        AnimationClip _jump;
        AnimationClip _push;

        Vector2 _scroll;
        string _log = "";

        public static void Open()
        {
            var window = GetWindow<VARCOAnimationSetupWindow>("애니메이션 자동 설정");
            window.minSize = new Vector2(430f, 560f);
            window._selected = Selection.activeGameObject;
            window.AutoDetectClips();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(8);
            EditorGUILayout.LabelField("선택 모델 애니메이션 자동 설정", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "VARCO3DImports의 폴더/클립 이름을 기준으로 대기, 걷기, 공격, 사망 등의 클립을 찾아 애니메이션 컨트롤러를 만들고 선택 모델에 연결합니다.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _selected = (GameObject)EditorGUILayout.ObjectField("선택 모델", _selected ? _selected : Selection.activeGameObject, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck() && _selected)
                Selection.activeGameObject = _selected;

            EditorGUI.BeginChangeCheck();
            _preset = DrawPresetPopup("애니메이션 종류", _preset);
            if (_preset == AnimationPreset.Custom)
                _customPrefix = EditorGUILayout.TextField("검색 접두어", _customPrefix);
            if (EditorGUI.EndChangeCheck())
                AutoDetectClips();

            _outputFolder = EditorGUILayout.TextField("저장 폴더", _outputFolder);
            _assignToSelected = EditorGUILayout.ToggleLeft("생성 후 선택 모델에 바로 연결", _assignToSelected);
            _overwriteExisting = EditorGUILayout.ToggleLeft("같은 이름의 컨트롤러가 있으면 덮어쓰기", _overwriteExisting);

            GUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("현재 선택 사용"))
                    _selected = Selection.activeGameObject;
                if (GUILayout.Button("클립 자동 찾기"))
                    AutoDetectClips();
            }

            GUILayout.Space(8);
            DrawClipField("대기", ref _idle, true);
            DrawClipField("걷기/달리기", ref _walk, true);
            DrawClipField("공격", ref _attack, false);
            DrawClipField("공격 2", ref _attack2, false);
            DrawClipField("사망", ref _death, false);
            DrawClipField("점프", ref _jump, false);
            DrawClipField("밀기", ref _push, false);

            GUILayout.Space(10);
            using (new EditorGUI.DisabledScope(_idle == null))
            {
                GUI.backgroundColor = _idle ? new Color(0.45f, 0.85f, 0.55f) : Color.gray;
                if (GUILayout.Button("애니메이션 컨트롤러 생성/연결", GUILayout.Height(42)))
                    CreateAndAssignController();
                GUI.backgroundColor = Color.white;
            }

            if (!string.IsNullOrEmpty(_log))
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("작업 기록", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_log, GUILayout.MinHeight(120));
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawClipField(string label, ref AnimationClip clip, bool required)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                clip = (AnimationClip)EditorGUILayout.ObjectField(label, clip, typeof(AnimationClip), false);
                GUILayout.Label(required ? "필수" : "선택", GUILayout.Width(36));
            }
        }

        static AnimationPreset DrawPresetPopup(string label, AnimationPreset current)
        {
            var index = System.Array.IndexOf(PresetOptions, current);
            if (index < 0)
                index = 0;

            index = EditorGUILayout.Popup(new GUIContent(label), index, PresetLabels);
            return PresetOptions[Mathf.Clamp(index, 0, PresetOptions.Length - 1)];
        }

        void AutoDetectClips()
        {
            var prefix = GetSearchPrefix();
            if (string.IsNullOrEmpty(prefix))
            {
                Log("오류: 검색 접두어가 비어 있습니다.");
                return;
            }

            var clips = LoadImportClips();
            _idle = FindBestClip(clips, prefix, "idle");
            _walk = FindBestClip(clips, prefix, "walk", "run");
            _attack = FindBestClip(clips, prefix, "attack");
            _attack2 = FindBestClip(clips, prefix, "attack2", "attack_2", "attack 2");
            _death = FindBestClip(clips, prefix, "death", "die");
            _jump = FindBestClip(clips, prefix, "jump");
            _push = FindBestClip(clips, prefix, "push");

            Log($"완료: '{prefix}' 기준으로 클립 검색 완료");
        }

        void CreateAndAssignController()
        {
            if (!_idle)
            {
                Log("오류: 대기 클립은 반드시 필요합니다.");
                return;
            }

            EnsureFolder(_outputFolder);
            var controllerName = $"{GetSearchPrefix()}_Controller.controller".Replace(" ", "_");
            var controllerPath = Path.Combine(_outputFolder, controllerName).Replace("\\", "/");
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) && _overwriteExisting)
                AssetDatabase.DeleteAsset(controllerPath);

            controllerPath = _overwriteExisting ? controllerPath : AssetDatabase.GenerateUniqueAssetPath(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            ConfigureController(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (_assignToSelected)
                AssignController(controller);

            Log($"완료: 애니메이션 컨트롤러 생성 -> {controllerPath}");
        }

        void ConfigureController(AnimatorController controller)
        {
            controller.AddParameter("IsWalk", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsAttack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsDead", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsJump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsPush", AnimatorControllerParameterType.Bool);

            var sm = controller.layers[0].stateMachine;
            ClearStateMachine(sm);

            var idle = AddState(sm, "Idle", _idle, new Vector3(260f, 80f, 0f));
            sm.defaultState = idle;

            AnimatorState walk = null;
            if (_walk)
            {
                walk = AddState(sm, "Walk", _walk, new Vector3(520f, 80f, 0f));
                AddBoolTransition(idle, walk, "IsWalk", true);
                AddBoolTransition(walk, idle, "IsWalk", false);
            }

            if (_attack)
                AddTriggeredAction(sm, idle, "Attack", _attack, "IsAttack", new Vector3(390f, 250f, 0f));
            if (_attack2)
                AddTriggeredAction(sm, idle, "Attack2", _attack2, "IsAttack", new Vector3(620f, 250f, 0f));
            if (_jump)
                AddTriggeredAction(sm, idle, "Jump", _jump, "IsJump", new Vector3(150f, 250f, 0f));
            if (_push)
            {
                var push = AddState(sm, "Push", _push, new Vector3(760f, 80f, 0f));
                AddBoolTransition(idle, push, "IsPush", true);
                AddBoolTransition(push, idle, "IsPush", false);
            }
            if (_death)
            {
                var death = AddState(sm, "Death", _death, new Vector3(390f, 420f, 0f));
                var t = sm.AddAnyStateTransition(death);
                t.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
                t.hasExitTime = false;
                t.duration = 0.05f;
                t.canTransitionToSelf = false;
            }
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
            var t = from.AddTransition(to);
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
            t.hasExitTime = false;
            t.duration = 0.08f;
        }

        static void AddTriggeredAction(AnimatorStateMachine sm, AnimatorState idle, string stateName, Motion motion, string trigger, Vector3 position)
        {
            var state = AddState(sm, stateName, motion, position);
            var enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.duration = 0.04f;
            enter.canTransitionToSelf = false;

            var exit = state.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = 0.92f;
            exit.duration = 0.08f;
        }

        static void ClearStateMachine(AnimatorStateMachine sm)
        {
            foreach (var state in sm.states.ToArray())
                sm.RemoveState(state.state);
            foreach (var transition in sm.anyStateTransitions.ToArray())
                sm.RemoveAnyStateTransition(transition);
        }

        void AssignController(RuntimeAnimatorController controller)
        {
            if (!_selected)
            {
                Log("확인 필요: 선택 모델이 없어 컨트롤러 생성만 완료했습니다.");
                return;
            }

            var animator = _selected.GetComponentInChildren<Animator>(true);
            if (!animator)
                animator = Undo.AddComponent<Animator>(_selected);

            Undo.RecordObject(animator, "VARCO 애니메이션 컨트롤러 연결");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);
            Log($"완료: {_selected.name} Animator에 컨트롤러 연결");
        }

        string GetSearchPrefix()
        {
            if (_preset == AnimationPreset.Custom)
                return _customPrefix.Trim();

            switch (_preset)
            {
                case AnimationPreset.ArenaPlayer: return "ARENA_Player";
                case AnimationPreset.ArenaBoss: return "ARENA_Boss";
                case AnimationPreset.ArenaOrc: return "ARENA_Orc";
                case AnimationPreset.ExplorationPlayer: return "EXPLORATION_Player";
                case AnimationPreset.ExplorationZombie: return "EXPLORATION_Zombie";
                case AnimationPreset.PlatformPlayer: return "PLATFORM_Player";
                case AnimationPreset.PuzzlePlayer: return "PUZZLE_Player";
                default: return "";
            }
        }

        static List<ClipCandidate> LoadImportClips()
        {
            var result = new List<ClipCandidate>();
            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/VARCO3DImports" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                        result.Add(new ClipCandidate(path, clip));
                }
            }
            return result;
        }

        static AnimationClip FindBestClip(List<ClipCandidate> clips, string prefix, params string[] keywords)
        {
            prefix = Normalize(prefix);
            ClipCandidate best = null;
            var bestScore = int.MinValue;

            foreach (var candidate in clips)
            {
                var haystack = Normalize(candidate.Path + "/" + candidate.Clip.name);
                if (!haystack.Contains(prefix))
                    continue;

                var score = 0;
                foreach (var keyword in keywords)
                {
                    var key = Normalize(keyword);
                    if (haystack.Contains(key))
                        score += 100;
                }

                var folderName = Normalize(Path.GetFileName(Path.GetDirectoryName(candidate.Path) ?? ""));
                if (folderName.Contains(prefix))
                    score += 20;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return bestScore > 0 ? best?.Clip : null;
        }

        static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value)
                ? ""
                : value.ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
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

        void Log(string message)
        {
            _log = message + "\n" + _log;
            Debug.Log("[VARCO 애니메이션 자동 설정] " + message);
        }

        class ClipCandidate
        {
            public readonly string Path;
            public readonly AnimationClip Clip;

            public ClipCandidate(string path, AnimationClip clip)
            {
                Path = path;
                Clip = clip;
            }
        }
    }
}
#endif
