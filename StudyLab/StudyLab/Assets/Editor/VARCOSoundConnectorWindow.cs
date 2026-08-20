#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VWS = VARCO_Workshop;

namespace VARCO_Workshop.Editor
{
    public class VARCOSoundConnectorWindow : EditorWindow
    {
        enum SoundKind { BGM, SFX, Ambient }
        enum ConnectionMode
        {
            SceneStartBgm,
            SceneStartAmbient,
            SelectedObjectTrigger,
            SelectedObjectCollision,
            SelectedObjectStart,
            SelectedObjectKeyDown,
            SelectedObjectMouseDown
        }

        struct SoundPreset
        {
            public string label;
            public string id;
            public SoundKind kind;
            public string prompt;

            public SoundPreset(string label, string id, SoundKind kind, string prompt)
            {
                this.label = label;
                this.id = id;
                this.kind = kind;
                this.prompt = prompt;
            }
        }

        static readonly SoundPreset[] Presets =
        {
            new SoundPreset("전투 BGM", "bgm_battle_loop", SoundKind.BGM, "어두운 판타지 전투 음악, 긴장감 있는 타악기, 낮은 현악기, 반복 재생 가능한 90초 루프"),
            new SoundPreset("탐험 BGM", "bgm_exploration_loop", SoundKind.BGM, "신비로운 탐험 배경 음악, 은은한 패드와 낮은 드론, 반복 재생 가능한 90초 루프"),
            new SoundPreset("공간 환경음", "amb_scene_space", SoundKind.Ambient, "어두운 던전의 바람과 낮은 울림, 멀리서 들리는 금속 잔향, 자연스럽게 반복되는 환경음"),
            new SoundPreset("공격음", "sfx_player_attack", SoundKind.SFX, "짧은 금속 검 휘두르는 소리, 어두운 판타지 아레나, 묵직한 타격감, 선명한 시작점, 음악 없음, 1초"),
            new SoundPreset("플레이어 발자국", "sfx_player_footstep", SoundKind.SFX, "짧고 가벼운 캐릭터 발자국 효과음, 돌 또는 금속 바닥을 밟는 명확한 접촉음, 반복 재생해도 거슬리지 않음, 음악 없음, 0.3초"),
            new SoundPreset("피격음", "sfx_hit_damage", SoundKind.SFX, "짧고 둔탁한 피격 효과음, 갑옷 충격과 낮은 타격감, 음악 없음, 1초"),
            new SoundPreset("아이템 획득음", "sfx_collect_item", SoundKind.SFX, "밝고 짧은 아이템 획득 효과음, 작은 마법 입자 느낌, 선명한 상승음, 음악 없음, 1초"),
            new SoundPreset("문 열림음", "sfx_door_open", SoundKind.SFX, "무거운 나무 문이 열리는 소리, 돌 구조물의 낮은 울림, 짧은 기계 장치음, 음악 없음, 2초"),
            new SoundPreset("체크포인트음", "sfx_checkpoint", SoundKind.SFX, "따뜻하고 짧은 체크포인트 활성화 효과음, 부드러운 마법 빛 느낌, 음악 없음, 2초"),
            new SoundPreset("클리어음", "sfx_clear", SoundKind.SFX, "짧은 목표 달성 효과음, 밝은 종소리와 상승하는 마법음, 긍정적인 느낌, 2초"),
            new SoundPreset("게임 오버음", "sfx_game_over", SoundKind.SFX, "짧고 낮은 실패 효과음, 어두운 타격감과 내려앉는 드론, 음악 없음, 2초")
        };

        static readonly GUIContent[] ConnectionLabels =
        {
            new GUIContent("씬 시작 - 배경음 재생"),
            new GUIContent("씬 시작 - 환경음 재생"),
            new GUIContent("선택 오브젝트 - 트리거 접촉"),
            new GUIContent("선택 오브젝트 - 충돌"),
            new GUIContent("선택 오브젝트 - 시작 시점"),
            new GUIContent("선택 오브젝트 - 키 입력"),
            new GUIContent("선택 오브젝트 - 마우스 클릭")
        };

        static readonly KeyCode[] TestKeyOptions =
        {
            KeyCode.None,
            KeyCode.Space,
            KeyCode.E,
            KeyCode.F,
            KeyCode.Q,
            KeyCode.R,
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3
        };

        static readonly GUIContent[] TestKeyLabels =
        {
            new GUIContent("사용 안 함"),
            new GUIContent("스페이스"),
            new GUIContent("E 키"),
            new GUIContent("F 키"),
            new GUIContent("Q 키"),
            new GUIContent("R 키"),
            new GUIContent("숫자 1"),
            new GUIContent("숫자 2"),
            new GUIContent("숫자 3")
        };

        const string RegistryPath = "Assets/ScriptableObjects/SoundEvents/WorkshopSoundEventRegistry.asset";
        const string AudioRoot = "Assets/Audio";

        Vector2 scroll;
        int presetIndex = 3;
        VWS.SoundEventRegistry registry;
        AudioClip selectedClip;
        string eventId = "sfx_player_attack";
        string customPrompt = "";
        float volume = 1f;
        ConnectionMode connectionMode = ConnectionMode.SelectedObjectTrigger;
        GameObject targetObject;
        bool onlyPlayer = true;
        bool playOnce;
        KeyCode key = KeyCode.Space;
        bool spatial3D = true;
        string log = "";

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/BGM-SFX 추가", priority = -98)]
        public static void OpenFromMenu()
        {
            Open();
        }

        public static void Open()
        {
            var window = GetWindow<VARCOSoundConnectorWindow>("BGM-SFX 추가");
            window.minSize = new Vector2(420, 640);
            window.TryUseSelectedAudioClip();
        }

        void OnEnable()
        {
            registry = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(RegistryPath);
            ApplyPreset(Presets[presetIndex], overwritePrompt: true);
            TryUseSelectedAudioClip();
        }

        void OnSelectionChange()
        {
            if (Selection.activeGameObject != null && targetObject == null)
                targetObject = Selection.activeGameObject;

            if (TryUseSelectedAudioClip())
                Repaint();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(8);

            EditorGUILayout.LabelField("BGM-SFX 추가", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "BGM은 씬 시작 오디오로 연결하고, SFX는 선택 오브젝트의 접촉/충돌/키 입력 사운드로 연결합니다. Project 창에서 오디오 클립을 선택한 뒤 이 메뉴를 열면 기본값을 자동으로 채웁니다.",
                MessageType.Info);

            DrawSetup();
            GUILayout.Space(12);
            DrawPromptAndRegistry();
            GUILayout.Space(12);
            DrawConnection();
            GUILayout.Space(12);
            DrawLog();

            EditorGUILayout.EndScrollView();
        }

        void DrawSetup()
        {
            DrawHeader("1. 폴더와 레지스트리");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Audio 폴더 만들기", GUILayout.Height(30)))
                    EnsureAudioFolders();
                if (GUILayout.Button("레지스트리 생성/열기", GUILayout.Height(30)))
                    registry = EnsureRegistry();
            }

            registry = (VWS.SoundEventRegistry)EditorGUILayout.ObjectField("사운드 이벤트 목록", registry, typeof(VWS.SoundEventRegistry), false);
            if (registry == null)
                EditorGUILayout.HelpBox("사운드 이벤트 목록이 없으면 오디오 클립을 이벤트 ID로 관리할 수 없습니다. 먼저 생성하세요.", MessageType.Warning);
        }

        void DrawPromptAndRegistry()
        {
            DrawHeader("2. 사운드 목록과 프롬프트");
            EditorGUI.BeginChangeCheck();
            presetIndex = EditorGUILayout.Popup("사운드 예시", presetIndex, BuildPresetLabels());
            if (EditorGUI.EndChangeCheck())
                ApplyPreset(Presets[presetIndex], overwritePrompt: true);

            eventId = EditorGUILayout.TextField("이벤트 ID", eventId);
            selectedClip = (AudioClip)EditorGUILayout.ObjectField("오디오 클립", selectedClip, typeof(AudioClip), false);
            volume = EditorGUILayout.Slider("볼륨", volume, 0f, 1f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selectedClip == null))
                {
                    if (GUILayout.Button("BGM으로 설정", GUILayout.Height(26)))
                        ConfigureSelectedClip(SoundKind.BGM);
                    if (GUILayout.Button("SFX로 설정", GUILayout.Height(26)))
                        ConfigureSelectedClip(SoundKind.SFX);
                }
            }

            EditorGUILayout.LabelField("한글 프롬프트 예시");
            customPrompt = EditorGUILayout.TextArea(customPrompt, GUILayout.MinHeight(64));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("프롬프트 복사", GUILayout.Height(28)))
                {
                    EditorGUIUtility.systemCopyBuffer = customPrompt;
                    Log("프롬프트를 클립보드에 복사했습니다.");
                }
                using (new EditorGUI.DisabledScope(selectedClip == null || string.IsNullOrWhiteSpace(eventId)))
                {
                    if (GUILayout.Button("이벤트 등록/갱신", GUILayout.Height(28)))
                        AddOrUpdateEvent();
                }
            }
        }

        void DrawConnection()
        {
            DrawHeader("3. Unity 이벤트에 연결");
            connectionMode = (ConnectionMode)EditorGUILayout.Popup(new GUIContent("연결 방식"), (int)connectionMode, ConnectionLabels);
            targetObject = (GameObject)EditorGUILayout.ObjectField("선택 오브젝트", targetObject ? targetObject : Selection.activeGameObject, typeof(GameObject), true);

            if (!targetObject && Selection.activeGameObject)
                targetObject = Selection.activeGameObject;

            if (connectionMode == ConnectionMode.SelectedObjectTrigger || connectionMode == ConnectionMode.SelectedObjectCollision)
            {
                onlyPlayer = EditorGUILayout.ToggleLeft("플레이어와 닿을 때만 재생", onlyPlayer);
                playOnce = EditorGUILayout.ToggleLeft("한 번만 재생", playOnce);
            }

            if (connectionMode == ConnectionMode.SelectedObjectKeyDown)
                key = DrawTestKeyPopup("테스트 키", key);

            spatial3D = EditorGUILayout.ToggleLeft("3D 공간 사운드 사용", spatial3D);

            using (new EditorGUILayout.HorizontalScope())
            {
                bool sceneMode = connectionMode == ConnectionMode.SceneStartBgm || connectionMode == ConnectionMode.SceneStartAmbient;
                using (new EditorGUI.DisabledScope(!sceneMode || (registry == null && selectedClip == null)))
                {
                    if (GUILayout.Button("씬/BGM 연결", GUILayout.Height(34)))
                        ConnectSceneSound();
                }
                using (new EditorGUI.DisabledScope(sceneMode || targetObject == null || (registry == null && selectedClip == null)))
                {
                    if (GUILayout.Button("선택 오브젝트에 연결", GUILayout.Height(34)))
                        ConnectSelectedObject();
                }
            }

            EditorGUILayout.HelpBox(
                "트리거 접촉 방식은 대상 오브젝트에 충돌 영역이 필요합니다. 트리거 설정이 꺼져 있으면 자동으로 켜줍니다. 충돌 방식은 트리거가 아닌 충돌 영역과 물리 몸체 조합에서 사용하세요.",
                MessageType.None);
        }

        void DrawLog()
        {
            if (string.IsNullOrEmpty(log))
                return;

            DrawHeader("작업 기록");
            EditorGUILayout.TextArea(log, GUILayout.MinHeight(100));
        }

        static void DrawHeader(string text)
        {
            GUILayout.Space(4);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        static string[] BuildPresetLabels()
        {
            var labels = new string[Presets.Length];
            for (int i = 0; i < Presets.Length; i++)
                labels[i] = $"{Presets[i].label} ({Presets[i].id})";
            return labels;
        }

        static KeyCode DrawTestKeyPopup(string label, KeyCode current)
        {
            var index = System.Array.IndexOf(TestKeyOptions, current);
            if (index < 0)
                index = 1;

            index = EditorGUILayout.Popup(new GUIContent(label), index, TestKeyLabels);
            return TestKeyOptions[Mathf.Clamp(index, 0, TestKeyOptions.Length - 1)];
        }

        void ApplyPreset(SoundPreset preset, bool overwritePrompt)
        {
            eventId = preset.id;
            if (overwritePrompt)
                customPrompt = preset.prompt;
        }

        bool TryUseSelectedAudioClip()
        {
            var clip = Selection.activeObject as AudioClip;
            if (clip == null || clip == selectedClip)
                return false;

            selectedClip = clip;
            ConfigureSelectedClip(GuessSoundKind(clip), overwriteClip: false);
            return true;
        }

        void ConfigureSelectedClip(SoundKind kind, bool overwriteClip = true)
        {
            if (overwriteClip)
            {
                var clip = Selection.activeObject as AudioClip;
                if (clip != null)
                    selectedClip = clip;
            }

            if (selectedClip == null)
                return;

            eventId = BuildEventId(kind, selectedClip.name);

            switch (kind)
            {
                case SoundKind.BGM:
                    connectionMode = ConnectionMode.SceneStartBgm;
                    spatial3D = false;
                    volume = Mathf.Clamp01(volume <= 0f || Mathf.Approximately(volume, 1f) ? 0.45f : volume);
                    break;
                case SoundKind.Ambient:
                    connectionMode = ConnectionMode.SceneStartAmbient;
                    spatial3D = true;
                    volume = Mathf.Clamp01(volume <= 0f || Mathf.Approximately(volume, 1f) ? 0.55f : volume);
                    break;
                default:
                    if (connectionMode == ConnectionMode.SceneStartBgm || connectionMode == ConnectionMode.SceneStartAmbient)
                        connectionMode = ConnectionMode.SelectedObjectTrigger;
                    spatial3D = true;
                    if (volume <= 0f)
                        volume = 1f;
                    break;
            }
        }

        static SoundKind GuessSoundKind(AudioClip clip)
        {
            var path = AssetDatabase.GetAssetPath(clip).Replace('\\', '/').ToLowerInvariant();
            if (path.Contains("/bgm/") || clip.name.ToLowerInvariant().Contains("bgm"))
                return SoundKind.BGM;
            if (path.Contains("/ambient/") || clip.name.ToLowerInvariant().Contains("ambient") || clip.name.ToLowerInvariant().Contains("amb_"))
                return SoundKind.Ambient;
            return SoundKind.SFX;
        }

        static string BuildEventId(SoundKind kind, string clipName)
        {
            var prefix = kind == SoundKind.BGM ? "bgm_" : kind == SoundKind.Ambient ? "amb_" : "sfx_";
            var lower = clipName.ToLowerInvariant();
            var builder = new System.Text.StringBuilder();
            var previousUnderscore = false;

            for (int i = 0; i < lower.Length; i++)
            {
                var c = lower[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    builder.Append(c);
                    previousUnderscore = false;
                }
                else if (!previousUnderscore)
                {
                    builder.Append('_');
                    previousUnderscore = true;
                }
            }

            var id = builder.ToString().TrimEnd('_');
            if (id.StartsWith(prefix))
                return id;

            return string.IsNullOrEmpty(id) ? prefix + "event" : prefix + id;
        }

        void EnsureAudioFolders()
        {
            EnsureFolder("Assets", "Audio");
            EnsureFolder(AudioRoot, "BGM");
            EnsureFolder(AudioRoot, "SFX");
            EnsureFolder(AudioRoot, "Ambient");
            AssetDatabase.Refresh();
            Log("Assets/Audio/BGM, SFX, Ambient 폴더를 확인했습니다.");
        }

        VWS.SoundEventRegistry EnsureRegistry()
        {
            EnsureFolder("Assets", "ScriptableObjects");
            EnsureFolder("Assets/ScriptableObjects", "SoundEvents");

            var asset = AssetDatabase.LoadAssetAtPath<VWS.SoundEventRegistry>(RegistryPath);
            if (asset == null)
            {
                asset = CreateInstance<VWS.SoundEventRegistry>();
                AssetDatabase.CreateAsset(asset, RegistryPath);
                AssetDatabase.SaveAssets();
                Log("WorkshopSoundEventRegistry를 생성했습니다.");
            }
            else
            {
                Log("기존 WorkshopSoundEventRegistry를 사용합니다.");
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return asset;
        }

        static void EnsureFolder(string parent, string folder)
        {
            var path = $"{parent}/{folder}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folder);
        }

        void AddOrUpdateEvent()
        {
            registry = registry != null ? registry : EnsureRegistry();
            Undo.RecordObject(registry, "VARCO 사운드 이벤트 목록 갱신");

            var entry = registry.events.Find(e => e != null && e.id == eventId);
            if (entry == null)
            {
                entry = new VWS.SoundEventRegistry.Entry();
                registry.events.Add(entry);
            }

            entry.id = eventId;
            entry.clip = selectedClip;
            entry.volume = volume;

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            Log($"이벤트 등록: {eventId} -> {selectedClip.name}");
        }

        void ConnectSceneSound()
        {
            registry = registry != null ? registry : EnsureRegistry();

            var modeName = connectionMode == ConnectionMode.SceneStartAmbient ? "Ambient" : "BGM";
            var modeLabel = connectionMode == ConnectionMode.SceneStartAmbient ? "환경음" : "배경 음악";
            var go = GameObject.Find($"VW_Audio_{modeName}");
            if (!go)
            {
                go = new GameObject($"VW_Audio_{modeName}");
                Undo.RegisterCreatedObjectUndo(go, modeLabel + " 오디오 생성");
            }
            else
            {
                Undo.RecordObject(go, modeLabel + " 오디오 설정");
            }

            var src = EnsureComponent<AudioSource>(go);
            src.playOnAwake = true;
            src.loop = true;
            src.spatialBlend = connectionMode == ConnectionMode.SceneStartAmbient && spatial3D ? 1f : 0f;
            src.volume = volume;

            if (selectedClip != null)
                src.clip = selectedClip;
            else if (registry != null && registry.TryGet(eventId, out var clip, out var registryVolume))
            {
                src.clip = clip;
                src.volume = registryVolume * volume;
            }

            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Log($"{go.name}에 씬 시작 사운드를 연결했습니다.");
        }

        void ConnectSelectedObject()
        {
            targetObject = targetObject ? targetObject : Selection.activeGameObject;
            if (targetObject == null)
                return;

            registry = registry != null ? registry : EnsureRegistry();
            Undo.RegisterFullObjectHierarchyUndo(targetObject, "Connect Sound Event");

            var trigger = EnsureComponent<VWS.SoundEventTrigger>(targetObject);
            trigger.registry = registry;
            trigger.eventId = eventId;
            trigger.fallbackClip = selectedClip;
            trigger.volumeMultiplier = volume;
            trigger.onlyPlayer = onlyPlayer;
            trigger.playOnce = playOnce;
            trigger.key = key;
            trigger.triggerMode = ToTriggerMode(connectionMode);

            var src = EnsureComponent<AudioSource>(targetObject);
            src.playOnAwake = false;
            src.spatialBlend = spatial3D ? 1f : 0f;

            if (connectionMode == ConnectionMode.SelectedObjectTrigger)
            {
                var collider = EnsureCollider(targetObject);
                collider.isTrigger = true;
            }
            else if (connectionMode == ConnectionMode.SelectedObjectCollision)
            {
                var collider = EnsureCollider(targetObject);
                collider.isTrigger = false;
            }

            EditorUtility.SetDirty(targetObject);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Log($"{targetObject.name}에 {eventId} 사운드 트리거를 연결했습니다.");
        }

        static VWS.SoundTriggerMode ToTriggerMode(ConnectionMode mode)
        {
            switch (mode)
            {
                case ConnectionMode.SelectedObjectTrigger: return VWS.SoundTriggerMode.OnTriggerEnter;
                case ConnectionMode.SelectedObjectCollision: return VWS.SoundTriggerMode.OnCollisionEnter;
                case ConnectionMode.SelectedObjectKeyDown: return VWS.SoundTriggerMode.OnKeyDown;
                case ConnectionMode.SelectedObjectMouseDown: return VWS.SoundTriggerMode.OnMouseDown;
                default: return VWS.SoundTriggerMode.OnStart;
            }
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component != null)
                return component;

            return Undo.AddComponent<T>(go);
        }

        static Collider EnsureCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                return collider;

            var box = Undo.AddComponent<BoxCollider>(go);
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return box;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            box.center = go.transform.InverseTransformPoint(bounds.center);
            box.size = new Vector3(
                Mathf.Max(0.1f, bounds.size.x / Mathf.Max(0.0001f, go.transform.lossyScale.x)),
                Mathf.Max(0.1f, bounds.size.y / Mathf.Max(0.0001f, go.transform.lossyScale.y)),
                Mathf.Max(0.1f, bounds.size.z / Mathf.Max(0.0001f, go.transform.lossyScale.z)));
            return box;
        }

        void Log(string message)
        {
            log += message + "\n";
            Debug.Log("[VARCO 사운드 자동 연결] " + message);
        }
    }
}
#endif
