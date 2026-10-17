using TMPro;
using UnityEngine;

namespace MosquitoGame
{
    /// <summary>
    /// ポーズ画面（UI Canvas + TextMeshPro）。タイトルと同じ見た目で「再開」「遊び方」「設定」「やり直し」のボタンを出す。
    /// 遊び方・設定のパネルはタイトルと同じ MenuPanels。時間が止まっていても動くよう、アニメーションは unscaledTime で行う。
    /// </summary>
    public class PauseScreen : MonoBehaviour
    {
        public TMP_FontAsset font;
        public Material logoMat, redMat, softMat;
        public GameManager gm;

        Canvas canvas;
        CanvasGroup group;
        MenuPanels panels;
        bool built, visible;
        float shownAt;

        public bool Active => visible;
        public bool PanelOpen => panels != null && panels.IsOpen;

        public void SetVisible(bool v)
        {
            if (v && !built) Build();
            if (!built) return;
            visible = v;
            canvas.gameObject.SetActive(v);
            if (v) { shownAt = Time.unscaledTime; group.alpha = 0f; }
            else panels.Close();
        }

        void Update()
        {
            if (!visible) return;
            group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.25f));
        }

        void Build()
        {
            if (font == null) return;
            UiKit.Setup(font, logoMat, redMat, softMat);
            UiKit.EnsureEventSystem(transform);
            canvas = UiKit.MakeCanvas(transform, "PauseCanvas", 60, out group);
            var root = (RectTransform)canvas.transform;

            UiKit.Stretch(UiKit.Img("Dim", root, null, new Color(0.01f, 0.02f, 0.07f, 0.82f)).rectTransform);
            UiKit.Stretch(UiKit.Img("Vignette", root, UiKit.Vignette, new Color(0f, 0.01f, 0.04f, 0.9f)).rectTransform);

            var title = UiKit.Txt("Title", root, "ポーズ中", 72f, Color.white, logoMat, 18f);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 110f));
            var bar = UiKit.Img("Bar", root, null, new Color(UiKit.Blood.r, UiKit.Blood.g, UiKit.Blood.b, 0.85f));
            UiKit.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -222f), new Vector2(60f, 3f));

            panels = MenuPanels.Build(root);

            // パネルは最後に作って最前面に置くので、開いている間は後ろのボタンを押せない
            string[] labels = { "再開", "遊び方", "設定", "やり直し" };
            UnityEngine.Events.UnityAction[] actions = { () => gm.ResumeFromUI(), () => panels.ShowHowTo(), () => panels.ShowSettings(), () => gm.Restart() };
            for (int i = 0; i < 4; i++)
            {
                var b = UiKit.MakeButton(root, labels[i], 320f, 62f, 28f, 14f, i == 0, actions[i]);
                UiKit.Place(b.root, new Vector2(0.5f, 1f), new Vector2(0f, -300f - i * 84f), new Vector2(320f, 62f));
            }
            panels.transform.SetAsLastSibling();

            built = true;
        }
    }
}
