using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MosquitoGame
{
    /// <summary>
    /// タイトル画面（UI Canvas + TextMeshPro）。ロゴ・月明かり・ほたる・横切る蚊・ボタン・操作／ルール／音量のカード。
    /// 画面は実行時にコードで組み立てる（画像ファイルなし）。リトライのとき（GameManager が最初から Playing）は何も出さない。
    /// 開始ボタン・Enter・Space の動作は GameManager.RequestStart() 経由で、ゲーム側の挙動は変えない。
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class TitleScreen : MonoBehaviour
    {
        public TMP_FontAsset font;
        public Material logoMat, redMat, softMat;
        public GameManager gm;

        /// <summary>タイトル画面を出している（出せないときは GameManager が従来の OnGUI のタイトルを使う）</summary>
        public bool Active { get; private set; }

        enum Phase { Idle, FadeToBlack, FadeFromBlack, Done }

        class Fly { public RectTransform rt; public Image img; public float speed, phase, baseX, amp, freq; }
        class Mosq { public RectTransform root, wingL, wingR; }
        class Intro { public CanvasGroup group; public RectTransform rt; public float delay, rise; public Vector2 home; }

        static readonly Color Ink = new Color(0.02f, 0.03f, 0.09f);
        static readonly Color Moon = new Color(0.62f, 0.75f, 1f);
        static readonly Color Blood = new Color(1f, 0.22f, 0.28f);

        Canvas canvas;
        CanvasGroup rootGroup;
        Image blackOverlay, moonGlow, buttonGlow;
        RectTransform logoRow, ornament;
        TextMeshProUGUI kaText, restText;
        Mosq emblem, crosser;
        readonly List<Fly> flies = new List<Fly>();
        readonly List<Intro> intros = new List<Intro>();
        Phase phase = Phase.Idle;
        float t0, phaseT, nextCross, crossT0 = -100f, crossY, crossDir = 1f;
        bool startRequested;

        static Sprite sGlow, sCircle, sRound, sVignette;

        // ---------------------------------------------------------------- 起動

        void Start()
        {
            if (gm == null) gm = GameManager.Instance;
            if (gm == null || gm.State != GameManager.GameState.Title || font == null) { enabled = false; return; }
            Build();
            Active = true;
            t0 = Time.unscaledTime;
            nextCross = 4f;
        }

        public void BeginStart()
        {
            if (startRequested) return;
            startRequested = true;
            phase = Phase.FadeToBlack;
            phaseT = 0f;
        }

        void Update()
        {
            if (!Active) return;
            float t = Time.unscaledTime - t0;
            float dt = Time.unscaledDeltaTime;

            AnimateIntro(t);
            AnimateAmbient(t, dt);

            switch (phase)
            {
                case Phase.FadeToBlack:
                    phaseT += dt;
                    blackOverlay.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, phaseT / 0.6f));
                    if (phaseT >= 0.6f)
                    {
                        gm.StartGame();                    // 暗転しきったらゲームを始める
                        rootGroup.alpha = 0f;              // タイトルの中身は消し、黒だけ残して明るくしていく
                        phase = Phase.FadeFromBlack; phaseT = 0f;
                    }
                    break;
                case Phase.FadeFromBlack:
                    phaseT += dt;
                    blackOverlay.color = new Color(0f, 0f, 0f, 1f - Mathf.SmoothStep(0f, 1f, phaseT / 0.9f));
                    if (phaseT >= 0.9f) { phase = Phase.Done; Active = false; canvas.gameObject.SetActive(false); }
                    break;
            }
        }

        void LateUpdate()
        {
            if (!Active || phase == Phase.FadeFromBlack) return;
            // 背景（3D の寝室）をゆっくり揺らす。カメラは毎フレーム ThirdPersonCamera が計算し直すので、ここでの揺れは残らない
            var cam = Camera.main;
            if (cam == null) return;
            float t = Time.unscaledTime;
            cam.transform.position += cam.transform.right * (Mathf.Sin(t * 0.21f) * 0.35f) + cam.transform.up * (Mathf.Sin(t * 0.15f) * 0.15f);
            cam.transform.rotation *= Quaternion.Euler(Mathf.Sin(t * 0.17f) * 0.8f, Mathf.Sin(t * 0.13f) * 1.2f, 0f);
        }

        // ---------------------------------------------------------------- 動き

        void AnimateIntro(float t)
        {
            foreach (var it in intros)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - it.delay) / 0.7f));
                it.group.alpha = k;
                it.rt.anchoredPosition = it.home + new Vector2(0f, -it.rise * (1f - k));
            }
            if (phase == Phase.Idle) rootGroup.alpha = Mathf.Clamp01(t / 0.5f);
        }

        void AnimateAmbient(float t, float dt)
        {
            // 月明かり・ボタンの光はゆっくり脈打つ
            moonGlow.color = new Color(Moon.r, Moon.g, Moon.b, 0.30f + 0.06f * Mathf.Sin(t * 0.6f));
            buttonGlow.color = new Color(0.45f, 0.62f, 1f, 0.35f + 0.2f * Mathf.Sin(t * 2.2f));

            // ロゴは呼吸するように明るさが変わり、ふわっと浮く
            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 1.4f);
            kaText.color = Color.Lerp(new Color(1f, 0.28f, 0.32f), new Color(1f, 0.45f, 0.45f), breathe);
            restText.color = Color.Lerp(new Color(0.88f, 0.93f, 1f), Color.white, breathe);
            if (t > 1.4f) logoRow.anchoredPosition = new Vector2(0f, -150f + 3f * Mathf.Sin(t * 0.9f));   // 登場の動きが終わってから浮かせる
            ornament.localRotation = Quaternion.Euler(0f, 0f, 45f);

            foreach (var f in flies)
            {
                var p = f.rt.anchoredPosition;
                p.y += f.speed * dt;
                if (p.y > 400f) { p.y = -400f; f.baseX = Random.Range(-640f, 640f); }
                p.x = f.baseX + Mathf.Sin(t * f.freq + f.phase) * f.amp;
                f.rt.anchoredPosition = p;
                var c = f.img.color;
                c.a = 0.12f + 0.38f * (0.5f + 0.5f * Mathf.Sin(t * 1.3f + f.phase * 3f));
                f.img.color = c;
            }

            FlapMosquito(emblem, t, 14f, 28f);

            // ときどき小さな蚊が画面を横切る
            if (t > nextCross) { crossT0 = t; crossDir = Random.value < 0.5f ? 1f : -1f; crossY = Random.Range(60f, 250f); nextCross = t + Random.Range(7f, 12f); }
            float u = (t - crossT0) / 6f;
            crosser.root.gameObject.SetActive(u >= 0f && u <= 1f);
            if (u >= 0f && u <= 1f)
            {
                float x = Mathf.Lerp(-720f, 720f, u) * crossDir;
                float y = crossY + 40f * Mathf.Sin(u * 11f) + 20f * Mathf.Sin(u * 23f);
                crosser.root.anchoredPosition = new Vector2(x, y);
                crosser.root.localScale = new Vector3(crossDir, 1f, 1f) * 0.8f;
                FlapMosquito(crosser, t, 55f, 38f);
            }
        }

        static void FlapMosquito(Mosq m, float t, float speed, float amp)
        {
            float a = Mathf.Sin(t * speed) * amp;
            m.wingL.localRotation = Quaternion.Euler(0f, 0f, 30f + a);
            m.wingR.localRotation = Quaternion.Euler(0f, 0f, 150f - a);
        }

        // ---------------------------------------------------------------- 組み立て

        void Build()
        {
            EnsureSprites();

            // EventSystem（ボタン・スライダーに必要）
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }

            var cgo = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // どんな縦横比でも 1280×720 の内容が欠けずに収まる
            rootGroup = cgo.GetComponent<CanvasGroup>();
            rootGroup.alpha = 0f;
            var root = (RectTransform)cgo.transform;

            // --- 背景 ---
            Stretch(Img("Dim", root, null, new Color(Ink.r, Ink.g, Ink.b, 0.66f)).rectTransform);

            moonGlow = Img("MoonGlow", root, sGlow, Moon);
            Place(moonGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(420f, 250f), new Vector2(1000f, 1000f));

            var flyRoot = Rt("Fireflies", root); Stretch(flyRoot);
            for (int i = 0; i < 22; i++)
            {
                float size = Random.Range(8f, 22f);
                var im = Img("Fly", flyRoot, sGlow, Random.value < 0.5f ? new Color(0.85f, 1f, 0.7f, 0.4f) : new Color(0.7f, 0.85f, 1f, 0.4f));
                Place(im.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(Random.Range(-640f, 640f), Random.Range(-360f, 360f)), new Vector2(size, size));
                flies.Add(new Fly { rt = im.rectTransform, img = im, speed = Random.Range(6f, 22f), phase = Random.Range(0f, 6.28f), baseX = im.rectTransform.anchoredPosition.x, amp = Random.Range(12f, 40f), freq = Random.Range(0.3f, 0.9f) });
            }

            crosser = MakeMosquito(root, new Color(0.75f, 0.85f, 1f, 0.55f), 1f);
            crosser.root.gameObject.SetActive(false);

            Stretch(Img("Vignette", root, sVignette, new Color(0f, 0.01f, 0.04f, 0.9f)).rectTransform);

            // --- ロゴ ---
            var tag = Txt("Tagline", root, "S T E A L T H    M O S Q U I T O", 21f, new Color(0.6f, 0.72f, 1f, 0.85f), softMat, 14f);
            Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(900f, 40f));
            AddIntro(tag.rectTransform, 0.2f, 14f);

            logoRow = Rt("LogoRow", root);
            Place(logoRow, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1000f, 170f));
            kaText = Txt("Ka", logoRow, "蚊", 150f, Blood, redMat, 0f, TextAlignmentOptions.MidlineLeft);
            restText = Txt("Rest", logoRow, "のステルスゲーム", 62f, Color.white, logoMat, 12f, TextAlignmentOptions.MidlineLeft);
            float kaW = kaText.GetPreferredValues().x, restW = restText.GetPreferredValues().x, gap = 10f;
            float total = kaW + gap + restW;
            SetLeftPivot(kaText.rectTransform, -total * 0.5f, 0f, new Vector2(kaW + 4f, 170f));
            SetLeftPivot(restText.rectTransform, -total * 0.5f + kaW + gap, -22f, new Vector2(restW + 4f, 100f));
            emblem = MakeMosquito(logoRow, new Color(0.88f, 0.93f, 1f, 0.95f), 1.5f);
            emblem.root.anchoredPosition = new Vector2(total * 0.5f + 78f, 34f);
            emblem.root.localRotation = Quaternion.Euler(0f, 0f, 12f);
            AddIntro(logoRow, 0.45f, 26f);

            // 飾り線（血のしずく型のひし形）
            var ornGroup = Rt("OrnamentGroup", root);
            Place(ornGroup, new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(600f, 20f));
            ornament = Rt("Diamond", ornGroup);
            Place(ornament, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(11f, 11f));
            var ornImg = ornament.gameObject.AddComponent<Image>(); ornImg.color = Blood; ornImg.raycastTarget = false;
            foreach (float s in new[] { -1f, 1f })
            {
                var line = Img("Line", ornGroup, null, new Color(0.55f, 0.68f, 1f, 0.55f));
                Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(s * 165f, 0f), new Vector2(290f, 1.5f));
            }
            AddIntro(ornGroup, 0.9f, 0f);

            var catchText = Txt("Catch", root, "夜の寝室で、人間に見つからないように血を吸え", 25f, new Color(0.82f, 0.88f, 1f, 0.95f), softMat, 7f);
            Place(catchText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -296f), new Vector2(1000f, 40f));
            AddIntro(catchText.rectTransform, 1.05f, 10f);

            // --- はじめるボタン ---
            var btnRoot = Rt("StartButton", root);
            Place(btnRoot, new Vector2(0.5f, 1f), new Vector2(0f, -372f), new Vector2(300f, 62f));
            buttonGlow = Img("Glow", btnRoot, sRound, new Color(0.45f, 0.62f, 1f, 0.4f), true);
            Place(buttonGlow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(314f, 76f));
            var btnBg = Img("Bg", btnRoot, sRound, new Color(0.07f, 0.11f, 0.26f, 0.96f), true);
            Stretch(btnBg.rectTransform);
            btnBg.raycastTarget = true;
            var btnLabel = Txt("Label", btnRoot, "はじめる", 30f, Color.white, softMat, 16f);
            Stretch(btnLabel.rectTransform);
            var button = btnRoot.gameObject.AddComponent<Button>();
            button.targetGraphic = btnBg;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => gm.RequestStart());
            var hover = btnRoot.gameObject.AddComponent<HoverScale>();
            hover.bg = btnBg;
            AddIntro(btnRoot, 1.3f, 12f);

            var hint = Txt("Hint", root, "Enter キーでも始められます", 16f, new Color(0.7f, 0.8f, 1f, 0.6f), softMat, 3f);
            Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -424f), new Vector2(600f, 26f));
            AddIntro(hint.rectTransform, 1.5f, 0f);

            // --- カード（操作／ルール／音量とベスト）---
            BuildHowToCards(root);

            // --- 暗転用の黒 ---
            blackOverlay = Img("Black", root, null, new Color(0f, 0f, 0f, 0f));
            Stretch(blackOverlay.rectTransform);
        }

        void BuildHowToCards(RectTransform root)
        {
            const float top = -462f, h = 232f, w = 380f;

            var c1 = Card(root, -400f, top, w, h, "操作", 1.5f);
            string[] ops = { "視点　　　　マウス", "移動　　　　W A S D", "上昇 / 下降　Space / C", "吸血　　　　左クリック長押し", "ダッシュ　　Shift / 右クリック", "ポーズ　　　Esc" };
            Lines(c1, ops, 17f);

            var c2 = Card(root, 0f, top, w, h, "ルール", 1.65f);
            string[] rules =
            {
                "血のメーターを満タンにしたらクリア",
                "危ない場所ほど多く吸える",
                "　顔 120・首 55・腕 20・足首 6（点／秒）",
                "かゆみ・視界・羽音で「気づき」が溜まる",
                "満タンで見つかり、手で叩かれる",
                "ダッシュでよけて、物陰に隠れれば逃げ切り",
            };
            Lines(c2, rules, 16f);

            var c3 = Card(root, 400f, top, w, h, "音量", 1.8f);
            string[] names = { "全体", "羽音", "効果音", "環境音" };
            System.Func<float>[] get = { () => AudioVolume.Master, () => AudioVolume.Buzz, () => AudioVolume.Sfx, () => AudioVolume.Ambient };
            System.Action<float>[] set = { v => AudioVolume.Master = v, v => AudioVolume.Buzz = v, v => AudioVolume.Sfx = v, v => AudioVolume.Ambient = v };
            for (int i = 0; i < 4; i++)
            {
                float y = -58f - i * 34f;
                var label = Txt("Name", c3, names[i], 17f, new Color(0.85f, 0.9f, 1f), softMat, 2f, TextAlignmentOptions.MidlineLeft);
                PlaceTopLeft(label.rectTransform, new Vector2(26f, y), new Vector2(80f, 28f));
                var pct = Txt("Pct", c3, Mathf.RoundToInt(get[i]() * 100f) + "%", 16f, new Color(0.85f, 0.9f, 1f, 0.9f), softMat, 0f, TextAlignmentOptions.MidlineRight);
                PlaceTopLeft(pct.rectTransform, new Vector2(w - 26f - 52f, y), new Vector2(52f, 28f));
                MakeSlider(c3, new Vector2(112f, y - 4f), w - 112f - 26f - 62f, get[i](), set[i], pct);
            }
            string rec = (gm.Best > 0f ? "ベストスコア  " + Mathf.FloorToInt(gm.Best) : "") + (gm.BestTime > 0f ? "　ベストタイム  " + FormatTime(gm.BestTime) : "");
            if (rec.Length > 0)
            {
                var best = Txt("Best", c3, rec, 16f, new Color(1f, 0.82f, 0.55f, 0.95f), softMat, 1f);
                PlaceTopLeft(best.rectTransform, new Vector2(10f, -h + 38f), new Vector2(w - 20f, 28f));
            }
        }

        static string FormatTime(float t)
        {
            int s = Mathf.FloorToInt(t);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        RectTransform Card(RectTransform root, float x, float top, float w, float h, string title, float delay)
        {
            var card = Rt("Card_" + title, root);
            Place(card, new Vector2(0.5f, 1f), new Vector2(x, top - h * 0.5f), new Vector2(w, h));
            var edge = Img("Edge", card, sRound, new Color(0.45f, 0.6f, 1f, 0.32f), true);
            Stretch(edge.rectTransform);
            var fill = Img("Fill", card, sRound, new Color(0.04f, 0.07f, 0.17f, 0.82f), true);
            Stretch(fill.rectTransform); fill.rectTransform.offsetMin = new Vector2(1.5f, 1.5f); fill.rectTransform.offsetMax = new Vector2(-1.5f, -1.5f);
            var head = Txt("Title", card, title, 20f, new Color(0.7f, 0.82f, 1f), softMat, 10f);
            PlaceTopLeft(head.rectTransform, new Vector2(0f, -10f), new Vector2(w, 32f));
            var bar = Img("Bar", card, null, new Color(Blood.r, Blood.g, Blood.b, 0.8f));
            PlaceTopLeft(bar.rectTransform, new Vector2(w * 0.5f - 18f, -44f), new Vector2(36f, 2f));
            AddIntro(card, delay, 16f);
            return card;
        }

        void Lines(RectTransform card, string[] lines, float size)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var t = Txt("Line", card, lines[i], size, new Color(0.88f, 0.92f, 1f, 0.95f), softMat, 1f, TextAlignmentOptions.MidlineLeft);
                PlaceTopLeft(t.rectTransform, new Vector2(26f, -58f - i * 28f), new Vector2(card.sizeDelta.x - 40f, 28f));
            }
        }

        void MakeSlider(RectTransform parent, Vector2 topLeft, float width, float value, System.Action<float> onChange, TextMeshProUGUI pct)
        {
            var root = Rt("Slider", parent);
            PlaceTopLeft(root, topLeft, new Vector2(width, 24f));
            var bg = Img("Track", root, sRound, new Color(0.1f, 0.14f, 0.3f, 1f), true);
            bg.raycastTarget = true;
            bg.rectTransform.anchorMin = new Vector2(0f, 0.38f); bg.rectTransform.anchorMax = new Vector2(1f, 0.62f);
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;

            var fillArea = Rt("FillArea", root);
            fillArea.anchorMin = new Vector2(0f, 0.38f); fillArea.anchorMax = new Vector2(1f, 0.62f);
            fillArea.offsetMin = new Vector2(2f, 0f); fillArea.offsetMax = new Vector2(-2f, 0f);
            var fill = Img("Fill", fillArea, sRound, new Color(0.45f, 0.62f, 1f, 1f), true);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(value, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            var handleArea = Rt("HandleArea", root);
            Stretch(handleArea); handleArea.offsetMin = new Vector2(10f, 0f); handleArea.offsetMax = new Vector2(-10f, 0f);
            var handle = Img("Handle", handleArea, sCircle, new Color(0.92f, 0.96f, 1f, 1f));
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

        void AddIntro(RectTransform rt, float delay, float rise)
        {
            var g = rt.gameObject.GetComponent<CanvasGroup>();
            if (g == null) g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            intros.Add(new Intro { group = g, rt = rt, delay = delay, rise = rise, home = rt.anchoredPosition });
        }

        // ---------------------------------------------------------------- 蚊の図形

        Mosq MakeMosquito(RectTransform parent, Color color, float scale)
        {
            var m = new Mosq();
            m.root = Rt("Mosquito", parent);
            Place(m.root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            m.root.localScale = Vector3.one * scale;

            // 向きは右。腹・胸・頭・口針・脚・触角・羽
            Part(m.root, sCircle, color, new Vector2(-22f, -1f), new Vector2(42f, 15f), 0f);
            Part(m.root, sCircle, color, new Vector2(3f, 0f), new Vector2(19f, 18f), 0f);
            Part(m.root, sCircle, color, new Vector2(17f, 1f), new Vector2(11f, 11f), 0f);
            Part(m.root, null, color, new Vector2(30f, -2f), new Vector2(20f, 2.2f), -8f);
            for (int i = 0; i < 3; i++)
            {
                Part(m.root, null, color, new Vector2(-4f + i * 8f, -12f), new Vector2(2f, 22f), (i - 1) * 22f);
            }
            Part(m.root, null, color, new Vector2(22f, 9f), new Vector2(14f, 1.5f), 35f);

            var wc = new Color(color.r, color.g, color.b, color.a * 0.55f);
            m.wingL = WingOf(m.root, sCircle, wc);
            m.wingR = WingOf(m.root, sCircle, wc);
            return m;
        }

        RectTransform WingOf(RectTransform parent, Sprite s, Color c)
        {
            var pivot = Rt("Wing", parent);
            Place(pivot, new Vector2(0.5f, 0.5f), new Vector2(3f, 8f), new Vector2(2f, 2f));
            var blade = Img("Blade", pivot, s, c);
            blade.rectTransform.pivot = new Vector2(0f, 0.5f);
            blade.rectTransform.anchorMin = blade.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            blade.rectTransform.anchoredPosition = Vector2.zero;
            blade.rectTransform.sizeDelta = new Vector2(46f, 11f);
            return pivot;
        }

        void Part(RectTransform parent, Sprite s, Color c, Vector2 pos, Vector2 size, float rotZ)
        {
            var im = Img("Part", parent, s, c);
            Place(im.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
            im.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        }

        // ---------------------------------------------------------------- UI の小道具

        RectTransform Rt(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        Image Img(string name, Transform parent, Sprite sprite, Color c, bool sliced = false)
        {
            var rt = Rt(name, parent);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite;
            im.color = c;
            im.raycastTarget = false;
            if (sliced) im.type = Image.Type.Sliced;
            return im;
        }

        TextMeshProUGUI Txt(string name, Transform parent, string text, float size, Color c, Material mat, float spacing, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rt(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
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

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>親（カード）の左上を原点に、左上の位置と大きさで置く（y は下向きが負）</summary>
        static void PlaceTopLeft(RectTransform rt, Vector2 topLeft, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = size;
        }

        static void SetLeftPivot(RectTransform rt, float x, float y, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = size;
        }

        // ---------------------------------------------------------------- 画像（実行時に生成）

        static void EnsureSprites()
        {
            if (sGlow != null) return;
            sGlow = Make(128, (x, y, n) => { float d = Dist(x, y, n); return Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f); }, false);
            sCircle = Make(64, (x, y, n) => Mathf.Clamp01((1f - Dist(x, y, n)) * 32f), false);
            sRound = Make(64, (x, y, n) =>
            {
                // 角丸四角（半径 22px）。端から 22px を 9 分割の境界にする
                float px = x + 0.5f, py = y + 0.5f, r = 22f, half = n * 0.5f;
                float qx = Mathf.Abs(px - half) - (half - r), qy = Mathf.Abs(py - half) - (half - r);
                float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                return Mathf.Clamp01(0.5f - d);
            }, true);
            sVignette = Make(128, (x, y, n) => { float d = Dist(x, y, n); return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.5f) / 0.65f)); }, false);
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
