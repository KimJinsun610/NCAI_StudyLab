#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VARCO_Workshop.Editor
{
    /// <summary>
    /// VARCO 게임 만들기 — 이 프로젝트의 유일한 작업 창입니다.
    /// 에셋 준비 → 씬 만들기 → 규칙 조립(블록코딩) → 사운드 → 점검까지
    /// 수업에 필요한 전체 파이프라인을 이 창 하나에서 순서대로 진행합니다.
    /// </summary>
    public class VARCOBlockCodingProWindow : EditorWindow
    {
        BlockGenre genre = BlockGenre.공통;
        int templateIndex;
        Vector2 scroll;
        bool showStep1 = true, showStep2 = true, showStep3 = true, showStep4 = true, showStep5 = true;

        [MenuItem("VARCO/게임 만들기", priority = -100)]
        public static void Open()
        {
            var window = GetWindow<VARCOBlockCodingProWindow>("VARCO 게임 만들기");
            window.minSize = new Vector2(500, 600);
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            showStep1 = Section("1단계 · 에셋 준비", showStep1, DrawStep1);
            showStep2 = Section("2단계 · 맵 만들기", showStep2, DrawStep2);
            showStep3 = Section("3단계 · 규칙 조립 (블록코딩)", showStep3, DrawStep3);
            showStep4 = Section("4단계 · 사운드 연결", showStep4, DrawStep4);
            showStep5 = Section("5단계 · 점검 & 마감", showStep5, DrawStep5);

            EditorGUILayout.EndScrollView();
        }

        bool Section(string title, bool open, System.Action body)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            var next = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
            if (next) body();
            EditorGUILayout.EndVertical();
            return next;
        }

        // ------------------------------------------------------------------
        void DrawStep1()
        {
            EditorGUILayout.HelpBox(
                "VARCO AI로 만든 3D 에셋은 Unity 상단의 별도 메뉴\n" +
                "[VARCO3D > Connect VARCO3D] 를 켜서 가져옵니다.\n" +
                "가져온 모델을 아래 버튼으로 게임에 바로 쓸 수 있는 형태로 만듭니다.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("캐릭터 프리팹 생성기", GUILayout.Height(26))) VARCOCharacterPrefabMakerWindow.Open();
            if (GUILayout.Button("오브젝트 프리팹 생성기", GUILayout.Height(26))) VARCOPrefabMakerWindow.OpenObjectGenerator();
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("선택 캐릭터 애니메이션 방향 통일", GUILayout.Height(22)))
                VARCOCharacterPrefabBuildUtility.NormalizeSelectedCharacterAnimationsMenu();
        }

        void DrawStep2()
        {
            EditorGUILayout.HelpBox("장르만 고르면 조작 가능한 플레이어, 카메라, 화면 표시, 목표 지점이 갖춰진 기본 맵이 한 번에 만들어집니다.", MessageType.Info);
            if (GUILayout.Button("장르 고르고 기본 맵 만들기", GUILayout.Height(30))) VARCOPresetMakerWindow.Open();
        }

        // ------------------------------------------------------------------
        void DrawStep3()
        {
            EditorGUILayout.HelpBox(
                "① 장르를 고르고 ② 만들고 싶은 규칙을 고른 뒤 ③ 화면 왼쪽 목록에서 오브젝트를 클릭하고 추가하세요.\n" +
                "추가한 뒤에는 오른쪽 인스펙터 창에서 색깔 카드를 채워 넣으면 됩니다.",
                MessageType.Info);

            var newGenre = (BlockGenre)EditorGUILayout.EnumPopup("장르", genre);
            if (newGenre != genre) { genre = newGenre; templateIndex = 0; }

            var list = VARCOBlockTemplates.ByGenre(genre);
            if (list.Count == 0)
            {
                EditorGUILayout.HelpBox("이 장르에 등록된 템플릿이 없습니다.", MessageType.Warning);
                return;
            }

            templateIndex = Mathf.Clamp(templateIndex, 0, list.Count - 1);
            var names = new string[list.Count];
            for (int i = 0; i < list.Count; i++) names[i] = list[i].Name;
            templateIndex = EditorGUILayout.Popup("만들 규칙 (" + list.Count + "개)", templateIndex, names);

            var def = list[templateIndex];
            EditorGUILayout.LabelField(def.Desc, EditorStyles.wordWrappedMiniLabel);

            // 미리보기
            var preview = new BlockRule();
            def.Build(preview);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("미리보기", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("언제: " + BlockLabels.Of(preview.trigger), EditorStyles.miniLabel);
            if (preview.conditions.Count > 0)
                EditorGUILayout.LabelField("이럴 때만: " + preview.conditions.Count + "개 조건", EditorStyles.miniLabel);
            for (int i = 0; i < preview.actions.Count; i++)
                EditorGUILayout.LabelField("  " + (i + 1) + ". " + BlockActionCatalog.Get(preview.actions[i].type).Label, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
            {
                if (GUILayout.Button("선택한 오브젝트에 규칙 추가", GUILayout.Height(32)))
                    foreach (var go in Selection.gameObjects) AddTemplate(go, def);
            }

            if (Selection.gameObjects.Length == 0)
                EditorGUILayout.HelpBox("화면 왼쪽 목록(Hierarchy)에서 규칙을 붙일 오브젝트를 먼저 클릭하세요.", MessageType.Warning);

            EditorGUILayout.LabelField(
                "전체 규칙 " + VARCOBlockTemplates.All.Count + "개 · 할 수 있는 동작 " + BlockActionCatalog.Map.Count + "종",
                EditorStyles.centeredGreyMiniLabel);
        }

        void DrawStep4()
        {
            EditorGUILayout.HelpBox("VARCO Sound로 만든 배경음과 효과음을 게임 시작, 부딪힘, 아이템 획득 같은 순간에 연결합니다.", MessageType.Info);
            if (GUILayout.Button("BGM · SFX 연결 열기", GUILayout.Height(30))) VARCOSoundConnectorWindow.OpenFromMenu();
        }

        void DrawStep5()
        {
            EditorGUILayout.HelpBox("발표 전에 꼭 실행하세요. 맵을 고친 뒤에도 다시 한 번 눌러주는 게 안전합니다.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("자동 고치기", GUILayout.Height(28))) VARCOBlockCodingBuilderWindow.RunSafetyPassMenu();
            if (GUILayout.Button("문제 없는지 검사", GUILayout.Height(28))) VARCOSceneHealthCheckWindow.Open();
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("적 이동 경로 다시 계산 (배치를 바꿨다면)", GUILayout.Height(24)))
                VARCOGameMakerWindow.RevalidateChangedPresetSceneFromMenu();
        }

        // ------------------------------------------------------------------
        void AddTemplate(GameObject go, VARCOBlockTemplates.Def def)
        {
            var comp = go.GetComponent<VARCOBlockRule>();
            if (!comp) comp = Undo.AddComponent<VARCOBlockRule>(go);

            Undo.RecordObject(comp, "블록 규칙 추가");

            var rule = new BlockRule();
            def.Build(rule);
            comp.rules.Add(rule);

            EditorUtility.SetDirty(comp);
            Debug.Log("[VARCO 게임 만들기] '" + go.name + "'에 [" + def.Genre + "] " + def.Name + " 규칙 추가됨.");
        }
    }
}
#endif
