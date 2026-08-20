using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VARCO_Workshop
{
    [DefaultExecutionOrder(200)]
    public class CombatHealthUI : MonoBehaviour
    {
        public Vector3 enemyBarOffset = new Vector3(0f, 2.2f, 0f);
        public Vector2 enemyBarSize = new Vector2(72f, 8f);
        public float enemyScanInterval = 1.0f;
        [Range(0, 12)] public int maxVisibleEnemyBars = 6;

        Canvas canvas;
        Camera mainCamera;
        PlayerHealth player;
        VARCOGameHUD modernHud;
        RectTransform playerFillRect;
        RectTransform playerPanelRoot;
        Image playerFill;
        Text playerText;
        float nextScan;
        float nextHudScan;

        readonly Dictionary<EnemyHealth, EnemyBar> enemyBars = new Dictionary<EnemyHealth, EnemyBar>();
        readonly List<EnemyHealth> removeBuffer = new List<EnemyHealth>();

        public static CombatHealthUI EnsureExists()
        {
            var existing = FindFirstObjectByType<CombatHealthUI>(FindObjectsInactive.Include);
            if (existing) return existing;

            var go = new GameObject("VW_CombatHUD");
            return go.AddComponent<CombatHealthUI>();
        }

        class EnemyBar
        {
            public RectTransform root;
            public RectTransform fillRect;
            public Image fill;
            public Text text;
        }

        void Awake()
        {
            BuildCanvas();
            BuildPlayerPanel();
        }

        void OnEnable()
        {
            BindPlayer();
        }

        void OnDisable()
        {
            if (player) player.OnHPChanged -= UpdatePlayerHP;
            foreach (var pair in enemyBars)
                if (pair.Key) pair.Key.OnHPChanged -= UpdateEnemyHP;
        }

        void LateUpdate()
        {
            if (!mainCamera) mainCamera = Camera.main;
            if (!player) BindPlayer();
            UpdatePlayerPanelVisibility();

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + enemyScanInterval;
                ScanEnemies();
            }

            UpdateEnemyBarPositions();
        }

        void BindPlayer()
        {
            if (player) return;
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (!playerGo) return;
            player = playerGo.GetComponent<PlayerHealth>();
            if (!player) return;
            player.OnHPChanged += UpdatePlayerHP;
            UpdatePlayerHP(player.CurrentHP, player.maxHP);
        }

        void ScanEnemies()
        {
            var enemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (!enemy || enemyBars.ContainsKey(enemy)) continue;
                AddEnemyBar(enemy);
            }
        }

        void AddEnemyBar(EnemyHealth enemy)
        {
            var bar = CreateBar("Enemy HP", enemyBarSize, new Color(0.2f, 0.78f, 0.32f, 0.92f), showText: false);
            enemyBars.Add(enemy, bar);
            enemy.OnHPChanged += UpdateEnemyHP;
            UpdateEnemyHP(enemy.CurrentHP, enemy.MaxHP);
        }

        void UpdatePlayerHP(int current, int max)
        {
            if (!playerFill || !playerText) return;
            float t = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            if (playerFillRect)
                playerFillRect.anchorMax = new Vector2(t, 1f);
            playerText.text = $"HP {current}/{max}";
        }

        void UpdateEnemyHP(int current, int max)
        {
            foreach (var pair in enemyBars)
            {
                if (!pair.Key) continue;
                if (pair.Key.CurrentHP != current || pair.Key.MaxHP != max) continue;
                float t = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
                SetEnemyBarFill(pair.Value, t);
                if (pair.Value.text) pair.Value.text.text = $"{current}/{max}";
            }
        }

        void UpdateEnemyBarPositions()
        {
            removeBuffer.Clear();
            var visibleCount = 0;
            foreach (var pair in enemyBars)
            {
                var enemy = pair.Key;
                var bar = pair.Value;
                if (!enemy || enemy.CurrentHP <= 0)
                {
                    if (bar.root) Destroy(bar.root.gameObject);
                    removeBuffer.Add(enemy);
                    continue;
                }

                float t = enemy.MaxHP > 0 ? Mathf.Clamp01((float)enemy.CurrentHP / enemy.MaxHP) : 0f;
                SetEnemyBarFill(bar, t);
                if (bar.text) bar.text.text = $"{enemy.CurrentHP}/{enemy.MaxHP}";

                if (!mainCamera)
                {
                    bar.root.gameObject.SetActive(false);
                    continue;
                }

                var world = enemy.transform.position + enemyBarOffset;
                var screen = mainCamera.WorldToScreenPoint(world);
                bool visible = screen.z > 0f;
                if (visible && maxVisibleEnemyBars <= 0)
                    visible = false;
                if (visible && visibleCount >= maxVisibleEnemyBars)
                    visible = false;

                bar.root.gameObject.SetActive(visible);
                if (visible)
                {
                    visibleCount++;
                    bar.root.position = screen;
                }
            }

            foreach (var enemy in removeBuffer)
            {
                if (enemy) enemy.OnHPChanged -= UpdateEnemyHP;
                enemyBars.Remove(enemy);
            }
        }

        void BuildCanvas()
        {
            canvas = GetComponent<Canvas>();
            if (!canvas) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            if (!GetComponent<CanvasScaler>())
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            if (!GetComponent<GraphicRaycaster>())
                gameObject.AddComponent<GraphicRaycaster>();
        }

        void BuildPlayerPanel()
        {
            var root = new GameObject("Player HP", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rect = (RectTransform)root.transform;
            playerPanelRoot = rect;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(240f, 34f);

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.72f, 0.08f, 0.06f, 0.88f);

            var fillRoot = new GameObject("Fill", typeof(RectTransform));
            fillRoot.transform.SetParent(root.transform, false);
            var fillRect = (RectTransform)fillRoot.transform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            playerFillRect = fillRect;
            playerFill = fillRoot.AddComponent<Image>();
            playerFill.type = Image.Type.Simple;
            playerFill.color = new Color(0.2f, 0.78f, 0.32f, 0.92f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(root.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            playerText = textGo.AddComponent<Text>();
            playerText.alignment = TextAnchor.MiddleCenter;
            playerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            playerText.fontSize = 16;
            playerText.color = Color.white;
            playerText.raycastTarget = false;
        }

        void UpdatePlayerPanelVisibility()
        {
            if (!playerPanelRoot)
                return;

            if (Time.unscaledTime >= nextHudScan)
            {
                nextHudScan = Time.unscaledTime + 1.0f;
                modernHud = FindFirstObjectByType<VARCOGameHUD>();
            }

            playerPanelRoot.gameObject.SetActive(!modernHud);
        }

        EnemyBar CreateBar(string name, Vector2 size, Color fillColor, bool showText)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = size;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.16f, 0.035f, 0.035f, 0.82f);
            bg.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(root.transform, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            var fill = fillGo.AddComponent<Image>();
            fill.type = Image.Type.Simple;
            fill.color = fillColor;
            fill.raycastTarget = false;

            Text text = null;
            if (showText)
            {
                var textGo = new GameObject("Text", typeof(RectTransform));
                textGo.transform.SetParent(root.transform, false);
                text = textGo.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.raycastTarget = false;
            }

            return new EnemyBar { root = rect, fillRect = fillRect, fill = fill, text = text };
        }

        static void SetEnemyBarFill(EnemyBar bar, float value)
        {
            if (bar == null || !bar.fillRect) return;
            bar.fillRect.anchorMin = new Vector2(0f, 0f);
            bar.fillRect.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            bar.fillRect.offsetMin = new Vector2(2f, 2f);
            bar.fillRect.offsetMax = new Vector2(-2f, -2f);
        }
    }
}
