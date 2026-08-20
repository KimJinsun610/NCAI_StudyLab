#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VARCO_Workshop.Editor
{
    public class VARCOGameMakerSimpleWindow : EditorWindow
    {
        Vector2 scroll;

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/레거시/간략 자동 제작/간략 버전 열기", priority = -100)]
        public static void Open()
        {
            var window = GetWindow<VARCOGameMakerSimpleWindow>("VARCO 간략 자동 제작");
            window.minSize = new Vector2(440f, 420f);
            window.Focus();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(10f);

            EditorGUILayout.LabelField("VARCO 간략 자동 제작", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "현재 프로젝트에 있는 VARCO 에셋을 기준으로 게임을 만들고, 필요한 보정과 검증만 빠르게 실행합니다. 세부 장르와 기능 조정은 세부 버전에서 진행합니다.",
                MessageType.Info);

            DrawMainButton("현재 VARCO 에셋으로 게임 만들기", VARCOGameMakerWindow.BuildBeginnerGameFromMenu);
            DrawMainButton("현재 씬 자동 보정", VARCOGameMakerWindow.FixCurrentSceneFromMenu);
            DrawMainButton("선택 프리팹에 기능 추가", VARCOSelectedFeatureApplicatorWindow.Open);
            DrawMainButton("현재 씬 건강 검사", VARCOSceneHealthCheckWindow.Open);
            DrawMainButton("검증 리포트 만들기", VARCOMenuSmokeTestWindow.RunQuickSmokeFromMenu);

            GUILayout.Space(10f);
            if (GUILayout.Button("세부 버전 열기", GUILayout.Height(34f)))
                VARCOGameMakerWindow.Open();

            if (GUILayout.Button("프로젝트 사용 매뉴얼 만들기", GUILayout.Height(28f)))
                VARCOGameMakerWindow.GenerateProjectManualFromMenu();

            EditorGUILayout.EndScrollView();
        }

        static void DrawMainButton(string label, System.Action action)
        {
            if (GUILayout.Button(label, GUILayout.Height(38f)))
                action?.Invoke();
        }
    }
}
#endif
