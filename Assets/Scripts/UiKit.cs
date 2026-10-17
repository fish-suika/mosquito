using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MosquitoGame
{
    /// <summary>タイトル・ポーズ画面（UI Canvas + TextMeshPro）で共有する部品づくり。画像はすべて実行時に生成する。</summary>
    public static class UiKit
    {
        public static TMP_FontAsset Font;
        public static Material LogoMat, RedMat, SoftMat;

        public static readonly Color Ink = new Color(0.02f, 0.03f, 0.09f);
        public static readonly Color Moon = new Color(0.62f, 0.75f, 1f);
        public static readonly Color Blood = new Color(1f, 0.22f, 0.28f);
        public static readonly Color Pale = new Color(0.88f, 0.92f, 1f, 0.95f);

        public static Sprite Glow, Circle, Round, Vignette;

        public static bool Ready => Font != null;

        public static void Setup(TMP_FontAsset font, Material logo, Material red, Material soft)
        {
            Font = font; LogoMat = logo; RedMat = red; SoftMat = soft;
            EnsureSprites();
        }

        // ---------------------------------------------------------------- 基本部品

        public static RectTransform Rt(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Img(string name, Transform parent, Sprite sprite, Color c, bool sliced = false)
        {
            var rt = Rt(name, parent);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite;
            im.color = c;
            im.raycastTarget = false;
            if (sliced) im.type = Image.Type.Sliced;
            return im;
        }

        public static TextMeshProUGUI Txt(string name, Transform parent, string text, float size, Color c, Material mat, float spacing, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rt(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font;
            if (mat != null) t.fontSharedMaterial = mat;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.characterSpacing = spacing;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;   // 文節の途中で勝手に改行しない
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>親の左上を原点に、左上の位置と大きさで置く（y は下向きが負）</summary>
        public static void PlaceTopLeft(RectTransform rt, Vector2 topLeft, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = size;
        }

        public static Canvas MakeCanvas(Transform parent, string name, int sortingOrder, out CanvasGroup group)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // どんな縦横比でも 1280×720 の内容が欠けずに収まる
            group = go.GetComponent<CanvasGroup>();
            return canvas;
        }

        /// <summary>ボタンやスライダーに必要な EventSystem（なければ作る）</summary>
        public static void EnsureEventSystem(Transform parent)
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(parent, false);
        }

        // ---------------------------------------------------------------- ボタン・スライダー

        public class ButtonParts { public RectTransform root; public Image bg, glow; public TextMeshProUGUI label; }

        /// <summary>角丸のボタン。触ると光って少し大きくなり、押すと沈む。</summary>
        public static ButtonParts MakeButton(Transform parent, string text, float w, float h, float fontSize, float spacing, bool withGlow, UnityEngine.Events.UnityAction onClick)
        {
            var p = new ButtonParts();
            p.root = Rt("Button_" + text, parent);
            p.root.sizeDelta = new Vector2(w, h);
            if (withGlow)
            {
                p.glow = Img("Glow", p.root, Round, new Color(0.45f, 0.62f, 1f, 0.4f), true);
                Place(p.glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w + 14f, h + 14f));
            }
            p.bg = Img("Bg", p.root, Round, new Color(0.07f, 0.11f, 0.26f, 0.96f), true);
            Stretch(p.bg.rectTransform);
            p.bg.raycastTarget = true;
            p.label = Txt("Label", p.root, text, fontSize, Color.white, SoftMat, spacing);
            Stretch(p.label.rectTransform);
            var b = p.root.gameObject.AddComponent<Button>();
            b.targetGraphic = p.bg;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(onClick);
            p.root.gameObject.AddComponent<HoverScale>().bg = p.bg;
            return p;
        }

        public static void MakeSlider(RectTransform parent, Vector2 topLeft, float width, float value, System.Action<float> onChange, TextMeshProUGUI pct)
        {
            var root = Rt("Slider", parent);
            PlaceTopLeft(root, topLeft, new Vector2(width, 24f));
            var bg = Img("Track", root, Round, new Color(0.1f, 0.14f, 0.3f, 1f), true);
            bg.raycastTarget = true;
            bg.rectTransform.anchorMin = new Vector2(0f, 0.38f); bg.rectTransform.anchorMax = new Vector2(1f, 0.62f);
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;

            var fillArea = Rt("FillArea", root);
            fillArea.anchorMin = new Vector2(0f, 0.38f); fillArea.anchorMax = new Vector2(1f, 0.62f);
            fillArea.offsetMin = new Vector2(2f, 0f); fillArea.offsetMax = new Vector2(-2f, 0f);
            var fill = Img("Fill", fillArea, Round, new Color(0.45f, 0.62f, 1f, 1f), true);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(value, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            var handleArea = Rt("HandleArea", root);
            Stretch(handleArea); handleArea.offsetMin = new Vector2(10f, 0f); handleArea.offsetMax = new Vector2(-10f, 0f);
            var handle = Img("Handle", handleArea, Circle, new Color(0.92f, 0.96f, 1f, 1f));
            handle.raycastTarget = true;
            handle.rectTransform.anchorMin = new Vector2(0f, 0f); handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.sizeDelta = new Vector2(20f, 0f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v => { onChange(v); pct.text = Mathf.RoundToInt(v * 100f) + "%"; });
            root.gameObject.AddComponent<SaveVolumeOnRelease>();
        }

        // ---------------------------------------------------------------- 画像（実行時に生成）

        static void EnsureSprites()
        {
            if (Glow != null) return;
            Glow = Make(128, (x, y, n) => { float d = Dist(x, y, n); return Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f); }, false);
            Circle = Make(64, (x, y, n) => Mathf.Clamp01((1f - Dist(x, y, n)) * 32f), false);
            Round = Make(64, (x, y, n) =>
            {
                // 角丸四角（半径 22px）。端から 22px を 9 分割の境界にする
                float px = x + 0.5f, py = y + 0.5f, r = 22f, half = n * 0.5f;
                float qx = Mathf.Abs(px - half) - (half - r), qy = Mathf.Abs(py - half) - (half - r);
                float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                return Mathf.Clamp01(0.5f - d);
            }, true);
            Vignette = Make(128, (x, y, n) => { float d = Dist(x, y, n); return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.5f) / 0.65f)); }, false);
        }

        static float Dist(int x, int y, int n)
        {
            float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static Sprite Make(int n, System.Func<int, int, int, float> alpha, bool sliced)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = new Color(1f, 1f, 1f, alpha(x, y, n));
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, sliced ? new Vector4(22, 22, 22, 22) : Vector4.zero);
        }
    }

    /// <summary>ボタンの上に乗ると少し大きく光り、押すと沈む。</summary>
    public class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Image bg;
        bool over, down;
        float k;

        public void OnPointerEnter(PointerEventData e) { over = true; }
        public void OnPointerExit(PointerEventData e) { over = false; down = false; }
        public void OnPointerDown(PointerEventData e) { down = true; }
        public void OnPointerUp(PointerEventData e) { down = false; }

        void OnDisable() { over = down = false; k = 1f; transform.localScale = Vector3.one; }

        void Update()
        {
            float target = down ? 0.96f : over ? 1.06f : 1f;
            k = Mathf.Lerp(k == 0f ? 1f : k, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            transform.localScale = Vector3.one * k;
            if (bg != null) bg.color = Color.Lerp(new Color(0.07f, 0.11f, 0.26f, 0.96f), new Color(0.16f, 0.24f, 0.52f, 1f), over ? 1f : 0f);
        }
    }

    /// <summary>音量スライダーから手を離したときに設定を保存する。</summary>
    public class SaveVolumeOnRelease : MonoBehaviour, IPointerUpHandler
    {
        public void OnPointerUp(PointerEventData e) { AudioVolume.Save(); }
    }
}
