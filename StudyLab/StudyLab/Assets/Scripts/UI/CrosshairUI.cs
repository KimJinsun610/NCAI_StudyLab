using UnityEngine;
using UnityEngine.UI;

namespace VARCO_Workshop
{
    /// <summary>화면 정중앙에 표시되는 작은 흰 점 조준점. 별도 스프라이트 에셋 없이
    /// 코드로 원형 텍스처를 만들어 사용합니다(CombatHealthUI와 같은 자가 설치 방식).</summary>
    [DefaultExecutionOrder(200)]
    public class CrosshairUI : MonoBehaviour
    {
        public float dotSize = 6f;
        public Color dotColor = Color.white;

        Canvas canvas;
        static Sprite circleSpriteCache;

        public static CrosshairUI EnsureExists()
        {
            var existing = FindFirstObjectByType<CrosshairUI>(FindObjectsInactive.Include);
            if (existing) return existing;

            var go = new GameObject("VW_Crosshair");
            return go.AddComponent<CrosshairUI>();
        }

        void Awake()
        {
            BuildCanvas();
            BuildDot();
        }

        void BuildCanvas()
        {
            canvas = GetComponent<Canvas>();
            if (!canvas) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // HP 패널(50)보다 위에 그려지도록
            if (!GetComponent<CanvasScaler>())
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        void BuildDot()
        {
            var go = new GameObject("Dot", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(dotSize, dotSize);

            var image = go.AddComponent<Image>();
            image.sprite = GetCircleSprite();
            image.color = dotColor;
            image.raycastTarget = false;
        }

        static Sprite GetCircleSprite()
        {
            if (circleSpriteCache) return circleSpriteCache;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var center = new Vector2(size * 0.5f, size * 0.5f);
            var radius = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    var alpha = Mathf.Clamp01(radius - dist); // 가장자리 1px 정도 부드럽게
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            circleSpriteCache = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return circleSpriteCache;
        }
    }
}
