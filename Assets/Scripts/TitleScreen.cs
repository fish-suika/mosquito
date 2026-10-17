using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MosquitoGame
{
    /// <summary>
    /// タイトル画面（UI Canvas + TextMeshPro）。ロゴ・月明かり・ほたる・横切る蚊と、「はじめる」「遊び方」「設定」のボタン。
    /// 遊び方・設定はボタンを押すとパネルが開く（MenuPanels。ポーズ画面と同じもの）。
    /// 画面は実行時にコードで組み立てる（画像ファイルなし）。リトライのとき（GameManager が最初から Playing）は何も出さない。
    /// 開始ボタン・Enter・Space は GameManager.RequestStart() 経由で、ゲーム側の挙動は変えない。
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class TitleScreen : MonoBehaviour
    {
        public TMP_FontAsset font;
        public Material logoMat, redMat, softMat;
        public GameManager gm;

        /// <summary>タイトル画面を出している</summary>
        public bool Active { get; private set; }
        public bool PanelOpen => panels != null && panels.IsOpen;

        enum Phase { Idle, FadeToBlack, FadeFromBlack, Done }

        class Fly { public RectTransform rt; public Image img; public float speed, phase, baseX, amp, freq; }
        class Mosq { public RectTransform root, wingL, wingR; }
        class Intro { public CanvasGroup group; public RectTransform rt; public float delay, rise; public Vector2 home; }

        Canvas canvas;
        CanvasGroup rootGroup;
        Image blackOverlay, moonGlow, buttonGlow;
        RectTransform logoRow, ornament;
        TextMeshProUGUI kaText, restText;
        MenuPanels panels;
        Mosq emblem, crosser;
        readonly List<Fly> flies = new List<Fly>();
        readonly List<Intro> intros = new List<Intro>();
        Phase phase = Phase.Idle;
        float t0, phaseT, nextCross, crossT0 = -100f, crossY, crossDir = 1f;
        bool startRequested;

        // ---------------------------------------------------------------- 起動

        void Start()
        {
            if (gm == null) gm = GameManager.Instance;
            if (gm == null || gm.State != GameManager.GameState.Title) { enabled = false; return; }
            if (font == null)
            {
                // フォントがなくてタイトルを作れないときは、止まらないようにそのままゲームを始める
                enabled = false;
                gm.StartGame();
                return;
            }
            UiKit.Setup(font, logoMat, redMat, softMat);
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
            panels.Close();
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
            moonGlow.color = new Color(UiKit.Moon.r, UiKit.Moon.g, UiKit.Moon.b, 0.30f + 0.06f * Mathf.Sin(t * 0.6f));
            buttonGlow.color = new Color(0.45f, 0.62f, 1f, 0.35f + 0.2f * Mathf.Sin(t * 2.2f));

            // ロゴは呼吸するように明るさが変わり、ふわっと浮く
            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 1.4f);
            kaText.color = Color.Lerp(new Color(1f, 0.28f, 0.32f), new Color(1f, 0.45f, 0.45f), breathe);
            restText.color = Color.Lerp(new Color(0.88f, 0.93f, 1f), Color.white, breathe);
            if (t > 1.4f) logoRow.anchoredPosition = new Vector2(0f, LogoY + 3f * Mathf.Sin(t * 0.9f));   // 登場の動きが終わってから浮かせる
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

        const float LogoY = -230f;

        void Build()
        {
            UiKit.EnsureEventSystem(transform);
            canvas = UiKit.MakeCanvas(transform, "TitleCanvas", 50, out rootGroup);
            rootGroup.alpha = 0f;
            var root = (RectTransform)canvas.transform;

            // --- 背景 ---
            UiKit.Stretch(UiKit.Img("Dim", root, null, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.66f)).rectTransform);

            moonGlow = UiKit.Img("MoonGlow", root, UiKit.Glow, UiKit.Moon);
            UiKit.Place(moonGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(420f, 250f), new Vector2(1000f, 1000f));

            var flyRoot = UiKit.Rt("Fireflies", root); UiKit.Stretch(flyRoot);
            for (int i = 0; i < 22; i++)
            {
                float size = Random.Range(8f, 22f);
                var im = UiKit.Img("Fly", flyRoot, UiKit.Glow, Random.value < 0.5f ? new Color(0.85f, 1f, 0.7f, 0.4f) : new Color(0.7f, 0.85f, 1f, 0.4f));
                UiKit.Place(im.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(Random.Range(-640f, 640f), Random.Range(-360f, 360f)), new Vector2(size, size));
                flies.Add(new Fly { rt = im.rectTransform, img = im, speed = Random.Range(6f, 22f), phase = Random.Range(0f, 6.28f), baseX = im.rectTransform.anchoredPosition.x, amp = Random.Range(12f, 40f), freq = Random.Range(0.3f, 0.9f) });
            }

            crosser = MakeMosquito(root, new Color(0.75f, 0.85f, 1f, 0.55f), 1f);
            crosser.root.gameObject.SetActive(false);

            UiKit.Stretch(UiKit.Img("Vignette", root, UiKit.Vignette, new Color(0f, 0.01f, 0.04f, 0.9f)).rectTransform);

            // --- ロゴ ---
            var tag = UiKit.Txt("Tagline", root, "S T E A L T H    M O S Q U I T O", 21f, new Color(0.6f, 0.72f, 1f, 0.85f), softMat, 14f);
            UiKit.Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(900f, 40f));
            AddIntro(tag.rectTransform, 0.2f, 14f);

            logoRow = UiKit.Rt("LogoRow", root);
            UiKit.Place(logoRow, new Vector2(0.5f, 1f), new Vector2(0f, LogoY), new Vector2(1000f, 170f));
            kaText = UiKit.Txt("Ka", logoRow, "蚊", 150f, UiKit.Blood, redMat, 0f, TextAlignmentOptions.MidlineLeft);
            restText = UiKit.Txt("Rest", logoRow, "のステルスゲーム", 62f, Color.white, logoMat, 12f, TextAlignmentOptions.MidlineLeft);
            float kaW = kaText.GetPreferredValues().x, restW = restText.GetPreferredValues().x, gap = 10f;
            float total = kaW + gap + restW;
            SetLeftPivot(kaText.rectTransform, -total * 0.5f, 0f, new Vector2(kaW + 4f, 170f));
            SetLeftPivot(restText.rectTransform, -total * 0.5f + kaW + gap, -22f, new Vector2(restW + 4f, 100f));
            emblem = MakeMosquito(logoRow, new Color(0.88f, 0.93f, 1f, 0.95f), 1.5f);
            emblem.root.anchoredPosition = new Vector2(total * 0.5f + 78f, 34f);
            emblem.root.localRotation = Quaternion.Euler(0f, 0f, 12f);
            AddIntro(logoRow, 0.45f, 26f);

            // 飾り線（血のしずく型のひし形）
            var ornGroup = UiKit.Rt("OrnamentGroup", root);
            UiKit.Place(ornGroup, new Vector2(0.5f, 1f), new Vector2(0f, -338f), new Vector2(600f, 20f));
            ornament = UiKit.Rt("Diamond", ornGroup);
            UiKit.Place(ornament, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(11f, 11f));
            var ornImg = ornament.gameObject.AddComponent<Image>(); ornImg.color = UiKit.Blood; ornImg.raycastTarget = false;
            foreach (float s in new[] { -1f, 1f })
            {
                var line = UiKit.Img("Line", ornGroup, null, new Color(0.55f, 0.68f, 1f, 0.55f));
                UiKit.Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(s * 165f, 0f), new Vector2(290f, 1.5f));
            }
            AddIntro(ornGroup, 0.9f, 0f);

            var catchText = UiKit.Txt("Catch", root, "夜の寝室で、人間に見つからないように血を吸え", 25f, new Color(0.82f, 0.88f, 1f, 0.95f), softMat, 7f);
            UiKit.Place(catchText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -380f), new Vector2(1000f, 40f));
            AddIntro(catchText.rectTransform, 1.05f, 10f);

            // --- ボタン（はじめる／遊び方／設定）---
            var start = UiKit.MakeButton(root, "はじめる", 320f, 66f, 32f, 18f, true, () => gm.RequestStart());
            UiKit.Place(start.root, new Vector2(0.5f, 1f), new Vector2(0f, -474f), new Vector2(320f, 66f));
            buttonGlow = start.glow;
            AddIntro(start.root, 1.3f, 12f);

            var how = UiKit.MakeButton(root, "遊び方", 190f, 50f, 22f, 10f, false, () => panels.ShowHowTo());
            UiKit.Place(how.root, new Vector2(0.5f, 1f), new Vector2(-105f, -556f), new Vector2(190f, 50f));
            AddIntro(how.root, 1.5f, 12f);
            var set = UiKit.MakeButton(root, "設定", 190f, 50f, 22f, 10f, false, () => panels.ShowSettings());
            UiKit.Place(set.root, new Vector2(0.5f, 1f), new Vector2(105f, -556f), new Vector2(190f, 50f));
            AddIntro(set.root, 1.6f, 12f);

            var hint = UiKit.Txt("Hint", root, "Enter キーでも始められます", 16f, new Color(0.7f, 0.8f, 1f, 0.6f), softMat, 3f);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -606f), new Vector2(600f, 26f));
            AddIntro(hint.rectTransform, 1.7f, 0f);

            if (gm.BestTime > 0f)
            {
                var best = UiKit.Txt("BestTime", root, "ベストタイム  " + FormatTime(gm.BestTime), 17f, new Color(1f, 0.82f, 0.55f, 0.95f), softMat, 2f);
                UiKit.Place(best.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -648f), new Vector2(600f, 28f));
                AddIntro(best.rectTransform, 1.8f, 0f);
            }

            panels = MenuPanels.Build(root);

            // --- 暗転用の黒（いちばん前）---
            blackOverlay = UiKit.Img("Black", root, null, new Color(0f, 0f, 0f, 0f));
            UiKit.Stretch(blackOverlay.rectTransform);
        }

        static string FormatTime(float t)
        {
            int s = Mathf.FloorToInt(t);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        void AddIntro(RectTransform rt, float delay, float rise)
        {
            var g = rt.gameObject.GetComponent<CanvasGroup>();
            if (g == null) g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            intros.Add(new Intro { group = g, rt = rt, delay = delay, rise = rise, home = rt.anchoredPosition });
        }

        static void SetLeftPivot(RectTransform rt, float x, float y, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = size;
        }

        // ---------------------------------------------------------------- 蚊の図形

        Mosq MakeMosquito(RectTransform parent, Color color, float scale)
        {
            var m = new Mosq();
            m.root = UiKit.Rt("Mosquito", parent);
            UiKit.Place(m.root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            m.root.localScale = Vector3.one * scale;

            // 向きは右。腹・胸・頭・口針・脚・触角・羽
            Part(m.root, UiKit.Circle, color, new Vector2(-22f, -1f), new Vector2(42f, 15f), 0f);
            Part(m.root, UiKit.Circle, color, new Vector2(3f, 0f), new Vector2(19f, 18f), 0f);
            Part(m.root, UiKit.Circle, color, new Vector2(17f, 1f), new Vector2(11f, 11f), 0f);
            Part(m.root, null, color, new Vector2(30f, -2f), new Vector2(20f, 2.2f), -8f);
            for (int i = 0; i < 3; i++)
                Part(m.root, null, color, new Vector2(-4f + i * 8f, -12f), new Vector2(2f, 22f), (i - 1) * 22f);
            Part(m.root, null, color, new Vector2(22f, 9f), new Vector2(14f, 1.5f), 35f);

            var wc = new Color(color.r, color.g, color.b, color.a * 0.55f);
            m.wingL = WingOf(m.root, wc);
            m.wingR = WingOf(m.root, wc);
            return m;
        }

        RectTransform WingOf(RectTransform parent, Color c)
        {
            var pivot = UiKit.Rt("Wing", parent);
            UiKit.Place(pivot, new Vector2(0.5f, 0.5f), new Vector2(3f, 8f), new Vector2(2f, 2f));
            var blade = UiKit.Img("Blade", pivot, UiKit.Circle, c);
            blade.rectTransform.pivot = new Vector2(0f, 0.5f);
            blade.rectTransform.anchorMin = blade.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            blade.rectTransform.anchoredPosition = Vector2.zero;
            blade.rectTransform.sizeDelta = new Vector2(46f, 11f);
            return pivot;
        }

        void Part(RectTransform parent, Sprite s, Color c, Vector2 pos, Vector2 size, float rotZ)
        {
            var im = UiKit.Img("Part", parent, s, c);
            UiKit.Place(im.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
            im.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        }
    }
}
