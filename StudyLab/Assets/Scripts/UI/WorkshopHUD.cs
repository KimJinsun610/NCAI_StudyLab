using UnityEngine;

namespace VARCO_Workshop
{
    [DefaultExecutionOrder(240)]
    public class WorkshopHUD : MonoBehaviour
    {
        public bool showDuringPlay = true;
        public KeyCode toggleKey = KeyCode.F9;
        public string title = "VARCO 워크숍 HUD";

        PlayerHealth playerHealth;
        CollectibleCounter collectibleCounter;
        CountdownTimer countdownTimer;
        GameManager gameManager;

        GUIStyle panelStyle;
        GUIStyle labelStyle;
        GUIStyle smallStyle;
        GUIStyle resultStyle;
        GUIStyle resultSubStyle;

        public static WorkshopHUD EnsureExists()
        {
            var existing = FindFirstObjectByType<WorkshopHUD>();
            if (existing)
                return existing;

            var go = new GameObject("VW_WorkshopHUD");
            return go.AddComponent<WorkshopHUD>();
        }

        void OnEnable()
        {
            Rebind();
        }

        void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
                showDuringPlay = !showDuringPlay;

            if (!playerHealth || !gameManager)
                Rebind();
        }

        void OnGUI()
        {
            if (!Application.isPlaying || !showDuringPlay)
                return;

            BuildStyles();
            DrawStatusPanel();

            var state = gameManager ? gameManager.State : GameState.Playing;
            if (state == GameState.Clear || state == GameState.GameOver)
                DrawResultOverlay(state);
        }

        void Rebind()
        {
            gameManager = GameManager.Instance ? GameManager.Instance : FindFirstObjectByType<GameManager>();
            countdownTimer = FindFirstObjectByType<CountdownTimer>();

            var player = GameObject.FindGameObjectWithTag("Player");
            if (!player)
            {
                playerHealth = null;
                collectibleCounter = null;
                return;
            }

            playerHealth = player.GetComponent<PlayerHealth>();
            collectibleCounter = player.GetComponent<CollectibleCounter>();
        }

        void DrawStatusPanel()
        {
            var width = Mathf.Min(320f, Screen.width - 24f);
            GUILayout.BeginArea(new Rect(12f, 12f, width, 146f), GUIContent.none, panelStyle);
            GUILayout.Label(title, labelStyle);

            if (gameManager)
            {
                GUILayout.Label($"상태: {StateLabel(gameManager.State)}", smallStyle);
                if (gameManager.profile)
                    GUILayout.Label($"장르: {GenreLabel(gameManager.profile.genre)}", smallStyle);
                if (gameManager.KillCount > 0)
                    GUILayout.Label($"처치: {gameManager.KillCount}", smallStyle);
            }

            if (playerHealth)
                GUILayout.Label($"체력: {playerHealth.CurrentHP}/{playerHealth.maxHP}", smallStyle);

            if (collectibleCounter)
                GUILayout.Label($"수집: {collectibleCounter.Count}", smallStyle);

            if (countdownTimer)
                GUILayout.Label($"시간: {Mathf.CeilToInt(countdownTimer.Remaining)}초", smallStyle);

            GUILayout.EndArea();
        }

        void DrawResultOverlay(GameState state)
        {
            var width = Mathf.Min(520f, Screen.width - 32f);
            var height = 168f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(rect, GUIContent.none, panelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(state == GameState.Clear ? "클리어" : "게임 오버", resultStyle);
            GUILayout.Space(8f);
            GUILayout.Label("이동, 사운드, 크기, 목표 흐름을 확인한 뒤 Play 모드를 종료하세요.", resultSubStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        static string StateLabel(GameState state)
        {
            switch (state)
            {
                case GameState.Ready:
                    return "준비";
                case GameState.Playing:
                    return "진행 중";
                case GameState.Clear:
                    return "클리어";
                case GameState.GameOver:
                    return "게임 오버";
                default:
                    return state.ToString();
            }
        }

        static string GenreLabel(GenreType genre)
        {
            switch (genre)
            {
                case GenreType.Arena:
                    return "아레나";
                case GenreType.Exploration:
                    return "탐험";
                case GenreType.Puzzle:
                    return "퍼즐";
                case GenreType.Platform:
                    return "플랫폼";
                default:
                    return genre.ToString();
            }
        }

        void BuildStyles()
        {
            if (panelStyle != null)
                return;

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(16, 16, 12, 12),
                alignment = TextAnchor.UpperLeft
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.92f, 0.96f, 1f, 1f) }
            };

            resultStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            resultSubStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.9f, 0.95f, 1f, 1f) }
            };
        }
    }
}
