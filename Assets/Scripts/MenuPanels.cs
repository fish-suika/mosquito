using TMPro;
using UnityEngine;

namespace MosquitoGame
{
    /// <summary>
    /// 「遊び方」「設定（音量）」のパネル。タイトルとポーズ画面で同じものを使う。
    /// 画面いっぱいの暗い幕の上に、閉じるボタンつきのカードを1枚出す。
    /// </summary>
    public class MenuPanels : MonoBehaviour
    {
        RectTransform overlay, howTo, settings;
        readonly TextMeshProUGUI[] pcts = new TextMeshProUGUI[4];

        public bool IsOpen => overlay != null && overlay.gameObject.activeSelf;

        static readonly string[] Ops =
        {
            "視点　　　　マウス",
            "移動　　　　W A S D",
            "上昇 / 下降　Space / C",
            "吸血　　　　左クリック長押し",
            "ダッシュ　　Shift / 右クリック",
            "ポーズ　　　Esc",
        };

        static readonly string[] Rules =
        {
            "血のメーターを満タンにしたらクリア",
            "　（吸っている間、蚊の横に出ます）",
            "危ない場所ほど、たくさん吸える",
            "かゆみ・視界・羽音で「気づき」が溜まる",
            "満タンで見つかり、手で叩かれる",
            "ダッシュでよけて、物陰に隠れれば逃げ切り",
            "叩かれたらゲームオーバー",
        };

        public static MenuPanels Build(RectTransform canvasRoot)
        {
            var go = new GameObject("MenuPanels", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            UiKit.Stretch((RectTransform)go.transform);
            var m = go.AddComponent<MenuPanels>();
            m.Create((RectTransform)go.transform);
            return m;
        }

        void Create(RectTransform root)
        {
            overlay = UiKit.Rt("Overlay", root);
            UiKit.Stretch(overlay);
            var dim = UiKit.Img("Dim", overlay, null, new Color(0f, 0.01f, 0.04f, 0.8f));
            UiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;   // 後ろのボタンを押せなくする

            howTo = Card(overlay, "遊び方", 860f, 470f);
            var opsHead = UiKit.Txt("OpsHead", howTo, "操作", 20f, new Color(1f, 0.62f, 0.66f), UiKit.SoftMat, 8f, TextAlignmentOptions.MidlineLeft);
            UiKit.PlaceTopLeft(opsHead.rectTransform, new Vector2(48f, -96f), new Vector2(300f, 30f));
            for (int i = 0; i < Ops.Length; i++)
            {
                var t = UiKit.Txt("Op", howTo, Ops[i], 19f, UiKit.Pale, UiKit.SoftMat, 1f, TextAlignmentOptions.MidlineLeft);
                UiKit.PlaceTopLeft(t.rectTransform, new Vector2(48f, -134f - i * 40f), new Vector2(380f, 34f));
            }
            var rulesHead = UiKit.Txt("RulesHead", howTo, "ルール", 20f, new Color(1f, 0.62f, 0.66f), UiKit.SoftMat, 8f, TextAlignmentOptions.MidlineLeft);
            UiKit.PlaceTopLeft(rulesHead.rectTransform, new Vector2(448f, -96f), new Vector2(300f, 30f));
            for (int i = 0; i < Rules.Length; i++)
            {
                var t = UiKit.Txt("Rule", howTo, Rules[i], 18f, UiKit.Pale, UiKit.SoftMat, 1f, TextAlignmentOptions.MidlineLeft);
                UiKit.PlaceTopLeft(t.rectTransform, new Vector2(448f, -134f - i * 34f), new Vector2(390f, 30f));
            }

            settings = Card(overlay, "設定", 560f, 400f);
            var sub = UiKit.Txt("Sub", settings, "音量", 20f, new Color(1f, 0.62f, 0.66f), UiKit.SoftMat, 8f, TextAlignmentOptions.MidlineLeft);
            UiKit.PlaceTopLeft(sub.rectTransform, new Vector2(48f, -96f), new Vector2(300f, 30f));
            string[] names = { "全体", "羽音", "効果音", "環境音" };
            System.Func<float>[] get = { () => AudioVolume.Master, () => AudioVolume.Buzz, () => AudioVolume.Sfx, () => AudioVolume.Ambient };
            System.Action<float>[] set = { v => AudioVolume.Master = v, v => AudioVolume.Buzz = v, v => AudioVolume.Sfx = v, v => AudioVolume.Ambient = v };
            for (int i = 0; i < 4; i++)
            {
                float y = -138f - i * 46f;
                var label = UiKit.Txt("Name", settings, names[i], 19f, UiKit.Pale, UiKit.SoftMat, 2f, TextAlignmentOptions.MidlineLeft);
                UiKit.PlaceTopLeft(label.rectTransform, new Vector2(48f, y), new Vector2(100f, 32f));
                pcts[i] = UiKit.Txt("Pct", settings, Mathf.RoundToInt(get[i]() * 100f) + "%", 18f, UiKit.Pale, UiKit.SoftMat, 0f, TextAlignmentOptions.MidlineRight);
                UiKit.PlaceTopLeft(pcts[i].rectTransform, new Vector2(560f - 48f - 60f, y), new Vector2(60f, 32f));
                UiKit.MakeSlider(settings, new Vector2(160f, y - 4f), 560f - 160f - 48f - 74f, get[i](), set[i], pcts[i]);
            }

            overlay.gameObject.SetActive(false);
        }

        RectTransform Card(RectTransform parent, string title, float w, float h)
        {
            var card = UiKit.Rt("Card_" + title, parent);
            UiKit.Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            var edge = UiKit.Img("Edge", card, UiKit.Round, new Color(0.45f, 0.6f, 1f, 0.4f), true);
            UiKit.Stretch(edge.rectTransform);
            var fill = UiKit.Img("Fill", card, UiKit.Round, new Color(0.04f, 0.07f, 0.17f, 0.97f), true);
            UiKit.Stretch(fill.rectTransform);
            fill.rectTransform.offsetMin = new Vector2(1.5f, 1.5f); fill.rectTransform.offsetMax = new Vector2(-1.5f, -1.5f);
            fill.raycastTarget = true;
            var head = UiKit.Txt("Title", card, title, 30f, Color.white, UiKit.LogoMat, 14f);
            UiKit.PlaceTopLeft(head.rectTransform, new Vector2(0f, -22f), new Vector2(w, 50f));
            var bar = UiKit.Img("Bar", card, null, new Color(UiKit.Blood.r, UiKit.Blood.g, UiKit.Blood.b, 0.85f));
            UiKit.PlaceTopLeft(bar.rectTransform, new Vector2(w * 0.5f - 24f, -74f), new Vector2(48f, 2.5f));

            var close = UiKit.MakeButton(card, "閉じる", 200f, 48f, 22f, 8f, false, Close);
            UiKit.Place(close.root, new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(200f, 48f));
            card.gameObject.SetActive(false);
            return card;
        }

        public void ShowHowTo() { Open(howTo); }
        public void ShowSettings()
        {
            Open(settings);
            float[] v = { AudioVolume.Master, AudioVolume.Buzz, AudioVolume.Sfx, AudioVolume.Ambient };
            for (int i = 0; i < 4; i++) if (pcts[i] != null) pcts[i].text = Mathf.RoundToInt(v[i] * 100f) + "%";
        }

        void Open(RectTransform card)
        {
            overlay.gameObject.SetActive(true);
            howTo.gameObject.SetActive(card == howTo);
            settings.gameObject.SetActive(card == settings);
        }

        public void Close()
        {
            AudioVolume.Save();
            if (overlay != null) overlay.gameObject.SetActive(false);
        }

        void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }
    }
}
