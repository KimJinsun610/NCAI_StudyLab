#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace VARCO_Workshop.Editor
{
    /// <summary>Assets/Study_Lab/UI_prefeb/gameover.prefab, gameclear.prefab에 각각 GameOverPanel/GameClearPanel
    /// 컴포넌트를 붙이고 Button의 OnClick을 RestartScene()에 연결합니다. 씬은 전혀 건드리지 않고 프리팹만 수정합니다.</summary>
    public static class VARCOGameOverPanelWiring
    {
        const string GameOverPrefabPath = "Assets/Study_Lab/UI_prefeb/gameover.prefab";
        const string GameClearPrefabPath = "Assets/Study_Lab/UI_prefeb/gameclear.prefab";

        [MenuItem("VARCO/테스트 씬/게임오버 패널 연결")]
        public static void WireGameOverPanel()
        {
            var root = LoadAndFixRoot(GameOverPrefabPath);
            if (!root) return;

            // gameclear를 복제해서 만든 경우 등, 엉뚱한 패널 컴포넌트가 붙어있을 수 있어 정리합니다.
            var stray = root.GetComponent<GameClearPanel>();
            if (stray) Object.DestroyImmediate(stray, true);

            var panel = root.GetComponent<GameOverPanel>();
            if (!panel) panel = root.AddComponent<GameOverPanel>();

            WireButtonClick(root, panel.RestartScene);
            SaveAndUnload(root, GameOverPrefabPath);
            Debug.Log("[VARCO] 게임오버 패널을 연결했습니다: " + GameOverPrefabPath);
        }

        [MenuItem("VARCO/테스트 씬/게임클리어 패널 연결")]
        public static void WireGameClearPanel()
        {
            var root = LoadAndFixRoot(GameClearPrefabPath);
            if (!root) return;

            // gameover를 복제해서 만든 경우 등, 엉뚱한 패널 컴포넌트가 붙어있을 수 있어 정리합니다.
            var stray = root.GetComponent<GameOverPanel>();
            if (stray) Object.DestroyImmediate(stray, true);

            var panel = root.GetComponent<GameClearPanel>();
            if (!panel) panel = root.AddComponent<GameClearPanel>();

            WireButtonClick(root, panel.RestartScene);
            SaveAndUnload(root, GameClearPrefabPath);
            Debug.Log("[VARCO] 게임클리어 패널을 연결했습니다: " + GameClearPrefabPath);
        }

        static GameObject LoadAndFixRoot(string prefabPath)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
            {
                Debug.LogError("[VARCO] 프리팹을 찾지 못했습니다: " + prefabPath);
                return null;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);

            // 프리팹에 루트 스케일이 0으로 저장돼 있어서 그대로 두면 활성화해도 아무것도 안 보입니다.
            var rootRect = root.GetComponent<RectTransform>();
            if (rootRect && rootRect.localScale == Vector3.zero)
                rootRect.localScale = Vector3.one;

            return root;
        }

        static void WireButtonClick(GameObject root, UnityEngine.Events.UnityAction call)
        {
            var buttonTransform = root.transform.Find("Button");
            if (!buttonTransform)
            {
                Debug.LogWarning($"[VARCO] '{root.name}' 프리팹에서 'Button' 오브젝트를 찾지 못해 클릭 연결을 건너뜁니다.");
                return;
            }

            var button = buttonTransform.GetComponent<Button>();
            if (!button)
            {
                Debug.LogWarning("[VARCO] 'Button' 오브젝트에 Button 컴포넌트가 없어 클릭 연결을 건너뜁니다.");
                return;
            }

            // 중복/엉뚱한 연결 방지 — 기존에 걸린 리스너를 지우고 하나만 새로 겁니다.
            for (var i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            UnityEventTools.AddPersistentListener(button.onClick, call);
        }

        static void SaveAndUnload(GameObject root, string prefabPath)
        {
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
