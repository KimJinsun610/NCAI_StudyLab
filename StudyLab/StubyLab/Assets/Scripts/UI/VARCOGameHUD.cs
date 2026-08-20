using UnityEngine;
using UnityEngine.UI;

namespace VARCO_Workshop
{
    [DefaultExecutionOrder(260)]
    public class VARCOGameHUD : MonoBehaviour
    {
        public GenreType fallbackGenre = GenreType.Arena;
        public string objectiveOverride = "";
        public string modeLabelOverride = "";
        public bool showHud = true;
        public bool hideWorkshopHud = true;
        public KeyCode toggleKey = KeyCode.F10;

        Canvas canvas;
        Text titleText;
        Text objectiveText;
        Text controlText;
        Text nextActionText;
        Text hpText;
        Text itemsText;
        Text timeText;
        Text stateText;
        Text noticeText;
        GameObject noticeRoot;
        Image hpFill;
        Image timeFill;
        GameObject overlayRoot;
        Text overlayTitle;
        Text overlaySubtitle;

        GameManager gameManager;
        PlayerHealth playerHealth;
        CollectibleCounter collectibleCounter;
        CountdownTimer countdownTimer;
        WaveManager waveManager;
        float nextRebindTime;
        int cachedItemTarget;
        float noticeUntil;
        float nextLegacyHudHideTime;
        string noticeMessage = "";
        static VARCOGameHUD activeHud;

        public static VARCOGameHUD EnsureExists(GenreType genre = GenreType.Arena)
        {
            var existing = FindFirstObjectByType<VARCOGameHUD>();
            if (existing)
            {
                existing.fallbackGenre = genre;
                return existing;
            }

            var go = new GameObject("VARCO_GameHUD");
            var hud = go.AddComponent<VARCOGameHUD>();
            hud.fallbackGenre = genre;
            return hud;
        }

        void Awake()
        {
            BuildCanvas();
            if (hideWorkshopHud)
                HideLegacyWorkshopHud();
        }

        void OnEnable()
        {
            activeHud = this;
            if (hideWorkshopHud)
                HideLegacyWorkshopHud();
            Rebind();
        }

        void OnDisable()
        {
            if (activeHud == this)
                activeHud = null;
        }

        public static void ShowNotice(string message, float seconds = 2.2f)
        {
            if (!activeHud || string.IsNullOrWhiteSpace(message))
                return;

            activeHud.noticeMessage = message;
            activeHud.noticeUntil = Time.unscaledTime + Mathf.Max(0.5f, seconds);
            if (activeHud.noticeRoot)
                activeHud.noticeRoot.SetActive(true);
        }

        void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
                showHud = !showHud;

            if (hideWorkshopHud && Time.unscaledTime >= nextLegacyHudHideTime)
            {
                nextLegacyHudHideTime = Time.unscaledTime + 1f;
                HideLegacyWorkshopHud();
            }

            if (Time.unscaledTime >= nextRebindTime && (!gameManager || !playerHealth || !countdownTimer || !waveManager))
                Rebind();
        }

        void LateUpdate()
        {
            if (canvas)
                canvas.enabled = showHud;

            if (!showHud)
                return;

            RefreshTexts();
        }

        void Rebind()
        {
            nextRebindTime = Time.unscaledTime + 0.5f;
            gameManager = GameManager.Instance ? GameManager.Instance : FindFirstObjectByType<GameManager>();
            countdownTimer = FindFirstObjectByType<CountdownTimer>();
            waveManager = WaveManager.Instance ? WaveManager.Instance : FindFirstObjectByType<WaveManager>();

            var player = GameObject.FindGameObjectWithTag("Player");
            if (!player)
            {
                playerHealth = null;
                collectibleCounter = null;
                return;
            }

            playerHealth = player.GetComponent<PlayerHealth>();
            collectibleCounter = player.GetComponent<CollectibleCounter>();
            cachedItemTarget = FindSceneItemTarget();
        }

        void RefreshTexts()
        {
            var genre = GetGenre();
            var state = gameManager ? gameManager.State : GameState.Playing;

            if (titleText)
                titleText.text = BuildTitle(genre);
            if (objectiveText)
                objectiveText.text = BuildObjective(genre);
            if (controlText)
                controlText.text = BuildControlGuide(genre);
            if (nextActionText)
                nextActionText.text = BuildNextActionLine(genre, state);
            if (stateText)
                stateText.text = StateLabel(state);

            RefreshHealth();
            RefreshItems();
            RefreshTimer();
            RefreshNotice();
            RefreshOverlay(state, genre);
        }

        void RefreshHealth()
        {
            var current = playerHealth ? playerHealth.CurrentHP : 0;
            var max = playerHealth ? Mathf.Max(1, playerHealth.maxHP) : 1;
            var t = Mathf.Clamp01((float)current / max);

            if (hpFill)
                hpFill.rectTransform.anchorMax = new Vector2(t, 1f);
            if (hpText)
                hpText.text = "체력 " + current + " / " + max;
        }

        void RefreshItems()
        {
            var current = collectibleCounter ? collectibleCounter.Count : 0;
            var target = GetItemTarget();
            if (itemsText)
            {
                if (GetClearCondition() == CompletionCondition.DefeatWaves)
                {
                    var enemyTarget = GetEnemyTarget();
                    itemsText.text = enemyTarget > 0
                        ? "처치 " + KillCount() + " / " + enemyTarget
                        : "처치 " + KillCount();
                }
                else if (GetClearCondition() == CompletionCondition.ReachGoal && target <= 0)
                {
                    itemsText.text = "목표 지점";
                }
                else
                {
                    itemsText.text = target > 0 ? "수집 " + current + " / " + target : "수집 " + current;
                }
            }
        }

        void RefreshTimer()
        {
            if (!countdownTimer)
            {
                if (timeText)
                    timeText.text = "시간 --";
                if (timeFill)
                    timeFill.rectTransform.anchorMax = Vector2.one;
                return;
            }

            var remaining = Mathf.CeilToInt(countdownTimer.Remaining);
            if (timeText)
                timeText.text = "시간 " + remaining + "초";
            if (timeFill)
                timeFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(countdownTimer.NormalizedRemaining), 1f);
        }

        void RefreshOverlay(GameState state, GenreType genre)
        {
            var visible = state == GameState.Clear || state == GameState.GameOver;
            if (overlayRoot)
                overlayRoot.SetActive(visible);
            if (!visible)
                return;

            if (overlayTitle)
                overlayTitle.text = state == GameState.Clear ? "클리어" : "게임 오버";
            if (overlaySubtitle)
                overlaySubtitle.text = state == GameState.Clear ? WinMessage(genre) : GameOverMessage(genre);
        }

        void RefreshNotice()
        {
            var visible = !string.IsNullOrWhiteSpace(noticeMessage) && Time.unscaledTime <= noticeUntil;
            if (noticeRoot)
                noticeRoot.SetActive(visible);
            if (visible && noticeText)
                noticeText.text = noticeMessage;
        }

        GenreType GetGenre()
        {
            if (gameManager && gameManager.profile)
                return gameManager.profile.genre;
            return fallbackGenre;
        }

        int GetItemTarget()
        {
            if (gameManager && gameManager.profile && gameManager.profile.itemGoal > 0)
                return gameManager.profile.itemGoal;

            return cachedItemTarget;
        }

        static int FindSceneItemTarget()
        {
            var goals = FindObjectsByType<GoalTrigger>(FindObjectsSortMode.None);
            var target = 0;
            foreach (var goal in goals)
                if (goal && goal.requiredItems > target)
                    target = goal.requiredItems;
            return target;
        }

        int GetEnemyTarget()
        {
            if (waveManager && waveManager.TotalEnemyCount > 0)
                return waveManager.TotalEnemyCount;
            return gameManager && gameManager.profile ? Mathf.Max(0, gameManager.profile.waveCount) : 0;
        }

        CompletionCondition GetClearCondition()
        {
            if (gameManager && gameManager.profile)
                return gameManager.profile.clearCondition;
            return fallbackGenre == GenreType.Arena ? CompletionCondition.DefeatWaves : CompletionCondition.ReachGoal;
        }

        int KillCount()
        {
            return gameManager ? gameManager.KillCount : 0;
        }

        string BuildTitle(GenreType genre)
        {
            var title = "VARCO " + GenreLabel(genre);
            if (!string.IsNullOrWhiteSpace(modeLabelOverride))
                title += " | " + modeLabelOverride;
            return title;
        }

        string BuildObjective(GenreType genre)
        {
            string objective;
            if (!string.IsNullOrWhiteSpace(objectiveOverride))
                objective = objectiveOverride;
            else if (gameManager && gameManager.profile && !string.IsNullOrWhiteSpace(gameManager.profile.objectiveText))
                objective = gameManager.profile.objectiveText;
            else
            {
                switch (genre)
                {
                    case GenreType.Arena:
                        objective = "모든 적 웨이브를 처치하세요.";
                        break;
                    case GenreType.Exploration:
                        objective = "아이템을 모으고 살아남아 목표 지점에 도달하세요.";
                        break;
                    case GenreType.Puzzle:
                        objective = "문 퍼즐을 풀고 목표 지점에 도달하세요.";
                        break;
                    case GenreType.Platform:
                        objective = "발판을 건너 목표 지점에 도달하세요.";
                        break;
                    default:
                        objective = "목표 지점에 도달하세요.";
                        break;
                }
            }

            var progress = BuildProgressLine();
            return string.IsNullOrWhiteSpace(progress) ? objective : objective + "\n" + progress;
        }

        string BuildProgressLine()
        {
            switch (GetClearCondition())
            {
                case CompletionCondition.DefeatWaves:
                    var enemyTarget = GetEnemyTarget();
                    var killCount = KillCount();
                    if (enemyTarget > 0)
                    {
                        var alive = waveManager ? waveManager.AliveCount : 0;
                        return "진행: 적 처치 " + killCount + " / " + enemyTarget + (alive > 0 ? " | 남은 적 " + alive : "");
                    }
                    return killCount > 0 ? "진행: 적 처치 " + killCount : "진행: 적 웨이브 대기";
                case CompletionCondition.CollectItems:
                    var current = collectibleCounter ? collectibleCounter.Count : 0;
                    var target = GetItemTarget();
                    return target > 0 ? "진행: 아이템 " + current + " / " + target : "진행: 아이템 " + current;
                case CompletionCondition.ReachGoal:
                    var itemTarget = GetItemTarget();
                    if (itemTarget > 0)
                    {
                        var collected = collectibleCounter ? collectibleCounter.Count : 0;
                        return "진행: 아이템 " + collected + " / " + itemTarget + " 수집 후 목표 지점";
                    }
                    return "진행: 목표 지점으로 이동";
                default:
                    return string.Empty;
            }
        }

        string BuildNextActionLine(GenreType genre, GameState state)
        {
            if (state == GameState.Clear)
                return "다음 행동: Play를 끄고 통합 스튜디오에서 다른 장르나 에셋으로 다시 만들어보세요.";
            if (state == GameState.GameOver)
                return Checkpoint.HasLastPos
                    ? "다음 행동: 체크포인트 위치에서 다시 도전하세요."
                    : "다음 행동: 시작 위치에서 다시 도전하세요. 회복 아이템과 위험 구역을 확인하세요.";

            var urgent = BuildUrgentHint();
            if (!string.IsNullOrWhiteSpace(urgent))
                return "주의: " + urgent;

            switch (GetClearCondition())
            {
                case CompletionCondition.DefeatWaves:
                    var enemyTarget = GetEnemyTarget();
                    var alive = waveManager ? waveManager.AliveCount : 0;
                    if (enemyTarget > 0 && KillCount() >= enemyTarget)
                        return "다음 행동: 모든 적을 처치했습니다. 클리어 판정을 기다리세요.";
                    return alive > 0
                        ? "다음 행동: 남은 적 " + alive + "명을 찾아 공격하세요."
                        : "다음 행동: 적 웨이브가 시작될 위치를 확인하세요.";

                case CompletionCondition.CollectItems:
                    var current = collectibleCounter ? collectibleCounter.Count : 0;
                    var target = GetItemTarget();
                    if (target > 0 && current < target)
                        return "다음 행동: 아이템 " + (target - current) + "개를 더 모으세요.";
                    return "다음 행동: 수집이 끝났습니다. 목표 지점이나 포탈로 이동하세요.";

                case CompletionCondition.ReachGoal:
                    var itemTarget = GetItemTarget();
                    var collected = collectibleCounter ? collectibleCounter.Count : 0;
                    if (itemTarget > 0 && collected < itemTarget)
                        return "다음 행동: 목표 지점 전에 아이템 " + (itemTarget - collected) + "개를 더 모으세요.";
                    if (genre == GenreType.Puzzle)
                        return "다음 행동: 발판, 스위치, 상자로 문을 열고 목표 지점으로 이동하세요.";
                    if (genre == GenreType.Platform)
                        return Checkpoint.HasLastPos
                            ? "다음 행동: 체크포인트가 저장됐습니다. 다음 발판으로 이동하세요."
                            : "다음 행동: 발판을 건너 체크포인트와 목표 지점을 찾으세요.";
                    return "다음 행동: 목표 지점, 출구, 포탈로 이동하세요.";

                default:
                    return "다음 행동: 화면의 목표와 표시된 진행도를 따라 이동하세요.";
            }
        }

        string BuildUrgentHint()
        {
            if (playerHealth)
            {
                var max = Mathf.Max(1, playerHealth.maxHP);
                if ((float)playerHealth.CurrentHP / max <= 0.3f)
                    return "체력이 낮습니다. 회복 아이템을 찾거나 위험 구역에서 벗어나세요.";
            }

            if (countdownTimer && countdownTimer.Remaining <= 10f)
                return "시간이 얼마 남지 않았습니다. 목표 행동을 바로 진행하세요.";

            return string.Empty;
        }

        string BuildControlGuide(GenreType genre)
        {
            if (gameManager && gameManager.profile && !string.IsNullOrWhiteSpace(gameManager.profile.controlGuideText))
                return gameManager.profile.controlGuideText;

            switch (genre)
            {
                case GenreType.Platform:
                    return "이동: A/D 또는 방향키 | 점프: Space";
                case GenreType.Puzzle:
                    return "이동: WASD | 시점: 마우스 | 상자 밀기: 몸으로 밀기";
                case GenreType.Exploration:
                    return "이동: WASD | 시점: 마우스 | 공격: 마우스 왼쪽";
                case GenreType.Arena:
                    return "이동: WASD | 시점: 마우스 | 공격: 마우스 왼쪽";
                default:
                    return "이동: WASD | 시점: 마우스";
            }
        }

        string WinMessage(GenreType genre)
        {
            if (gameManager && gameManager.profile && !string.IsNullOrWhiteSpace(gameManager.profile.clearMessage))
                return gameManager.profile.clearMessage;

            switch (genre)
            {
                case GenreType.Arena:
                    return "아레나를 클리어했습니다.";
                case GenreType.Exploration:
                    return "탐험을 완료했습니다.";
                case GenreType.Puzzle:
                    return "퍼즐을 해결했습니다.";
                case GenreType.Platform:
                    return "플랫폼 코스를 완료했습니다.";
                default:
                    return "게임을 클리어했습니다.";
            }
        }

        string GameOverMessage(GenreType genre)
        {
            if (gameManager && gameManager.profile && !string.IsNullOrWhiteSpace(gameManager.profile.gameOverMessage))
                return gameManager.profile.gameOverMessage;
            return "다시 도전하세요. 마지막 체크포인트와 체력 상태를 확인하세요.";
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

        void BuildCanvas()
        {
            canvas = GetComponent<Canvas>();
            if (!canvas)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;

            var scaler = GetComponent<CanvasScaler>();
            if (!scaler)
                scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            if (!GetComponent<GraphicRaycaster>())
                gameObject.AddComponent<GraphicRaycaster>();

            var root = CreateRect("HUD Root", transform);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            BuildTopPanel(root);
            BuildNotice(root);
            BuildOverlay(root);
        }

        void BuildTopPanel(RectTransform parent)
        {
            var panel = CreatePanel("Status Panel", parent, new Color(0.03f, 0.04f, 0.055f, 0.78f));
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(16f, -16f);
            panel.sizeDelta = new Vector2(390f, 150f);

            titleText = CreateText("Title", panel, 14, FontStyle.Bold, TextAnchor.UpperLeft);
            SetTopLeft(titleText.rectTransform, new Vector2(14f, -10f), new Vector2(362f, 22f));

            stateText = CreateText("State", panel, 11, FontStyle.Bold, TextAnchor.UpperLeft);
            stateText.color = new Color(0.68f, 0.91f, 1f, 1f);
            SetTopLeft(stateText.rectTransform, new Vector2(14f, -35f), new Vector2(118f, 17f));

            objectiveText = CreateText("Objective", panel, 11, FontStyle.Normal, TextAnchor.UpperLeft);
            objectiveText.color = new Color(0.9f, 0.94f, 1f, 1f);
            SetTopLeft(objectiveText.rectTransform, new Vector2(14f, -56f), new Vector2(362f, 32f));

            controlText = CreateText("Controls", panel, 9, FontStyle.Normal, TextAnchor.UpperLeft);
            controlText.color = new Color(0.7f, 0.82f, 0.92f, 1f);
            SetTopLeft(controlText.rectTransform, new Vector2(14f, -92f), new Vector2(362f, 16f));

            nextActionText = CreateText("Next Action", panel, 9, FontStyle.Bold, TextAnchor.UpperLeft);
            nextActionText.color = new Color(1f, 0.91f, 0.42f, 1f);
            SetTopLeft(nextActionText.rectTransform, new Vector2(14f, -111f), new Vector2(362f, 16f));

            var hpBar = CreateBar("HP Bar", panel, new Vector2(14f, -132f), new Vector2(138f, 12f), new Color(0.12f, 0.7f, 0.35f, 0.95f), out hpFill);
            hpText = CreateText("HP Text", hpBar, 9, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(hpText.rectTransform, Vector2.zero, Vector2.zero);

            var timeBar = CreateBar("Time Bar", panel, new Vector2(160f, -132f), new Vector2(92f, 12f), new Color(0.19f, 0.58f, 0.98f, 0.95f), out timeFill);
            timeText = CreateText("Time Text", timeBar, 9, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(timeText.rectTransform, Vector2.zero, Vector2.zero);

            itemsText = CreateText("Items", panel, 11, FontStyle.Bold, TextAnchor.MiddleRight);
            itemsText.color = new Color(1f, 0.88f, 0.34f, 1f);
            SetTopRight(itemsText.rectTransform, new Vector2(-14f, -130f), new Vector2(112f, 16f));
        }

        void BuildNotice(RectTransform parent)
        {
            noticeRoot = new GameObject("Korean Notice", typeof(RectTransform), typeof(Image));
            noticeRoot.transform.SetParent(parent, false);
            var rect = (RectTransform)noticeRoot.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-14f, -14f);
            rect.sizeDelta = new Vector2(420f, 38f);

            var bg = noticeRoot.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.065f, 0.84f);
            bg.raycastTarget = false;

            noticeText = CreateText("Notice Text", rect, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
            noticeText.color = new Color(0.92f, 0.98f, 1f, 1f);
            Stretch(noticeText.rectTransform, new Vector2(14f, 4f), new Vector2(-14f, -4f));
            noticeRoot.SetActive(false);
        }

        static void SetTopLeft(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        static void SetTopRight(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        static void SetCenter(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        void BuildOverlay(RectTransform parent)
        {
            overlayRoot = new GameObject("Result Overlay", typeof(RectTransform), typeof(Image));
            overlayRoot.transform.SetParent(parent, false);
            var rect = (RectTransform)overlayRoot.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(460f, 158f);
            var bg = overlayRoot.GetComponent<Image>();
            bg.color = new Color(0.025f, 0.03f, 0.04f, 0.86f);
            bg.raycastTarget = false;

            overlayTitle = CreateText("Result Title", rect, 36, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetCenter(overlayTitle.rectTransform, new Vector2(0f, 28f), new Vector2(420f, 52f));

            overlaySubtitle = CreateText("Result Subtitle", rect, 15, FontStyle.Normal, TextAnchor.MiddleCenter);
            overlaySubtitle.color = new Color(0.9f, 0.94f, 1f, 1f);
            SetCenter(overlaySubtitle.rectTransform, new Vector2(0f, -30f), new Vector2(410f, 40f));

            overlayRoot.SetActive(false);
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static RectTransform CreateBar(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Color fillColor, out Image fill)
        {
            var bar = CreatePanel(name, parent, new Color(0.1f, 0.11f, 0.13f, 0.92f));
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 1f);
            bar.anchoredPosition = anchoredPosition;
            bar.sizeDelta = size;

            var fillRect = CreateRect("Fill", bar);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            fill = fillRect.gameObject.AddComponent<Image>();
            fill.color = fillColor;
            fill.raycastTarget = false;
            return bar;
        }

        static Text CreateText(string name, Transform parent, int fontSize, FontStyle style, TextAnchor alignment)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = GetBuiltinFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(8, fontSize - 4);
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            return text;
        }

        static Font GetBuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        static void HideLegacyWorkshopHud()
        {
            foreach (var hud in FindObjectsByType<WorkshopHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hud)
                {
                    hud.showDuringPlay = false;
                    hud.gameObject.SetActive(false);
                }
            }
        }
    }
}
