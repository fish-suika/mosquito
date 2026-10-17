using UnityEngine;
using UnityEngine.SceneManagement;

namespace MosquitoGame
{
    /// <summary>タイトル・ゲームオーバー・クリア・リトライ・ポーズの流れと、プレイ中の画面表示（OnGUI）。スコアはない（血のメーターが満タンでクリア）。</summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState { Title, Playing, GameOver, Cleared }

        public MosquitoController mosquito;
        public Human human;
        [Tooltip("true: クリックでマウスを固定し、Esc（またはマウス固定が外れたとき）でポーズする")]
        public bool manageCursor = true;
        [Tooltip("true: 最初にタイトル画面を出す（リトライのときは出さない）")]
        public bool startAtTitle = true;
        [Tooltip("プレイ中の表示（OnGUI）の日本語フォント。空なら Editor では OS のフォントを使う（WebGL では必ず設定する）")]
        public Font uiFont;
        [Tooltip("UI Canvas + TextMeshPro のタイトル画面")]
        public TitleScreen titleScreen;
        [Tooltip("UI Canvas + TextMeshPro のポーズ画面")]
        public PauseScreen pauseScreen;

        public GameState State { get; private set; }
        public bool Paused { get; private set; }
        public float PlayTime { get; private set; }       // 遊んだ秒数（ポーズ中は進まない）
        public float BestTime => bestTime;                // クリアの最短タイム（0 = まだクリアしていない）
        public bool NewBestTime { get; private set; }
        public string EndReason { get; private set; }
        public int EscapeCount;
        public bool IsPlaying => State == GameState.Playing && !Paused;

        const string BestTimeKey = "mosquito.bestTime";
        static bool skipTitle;      // リトライ（シーン再読み込み）のときはタイトルを飛ばす
        static float bestTime;
        static bool bestLoaded;

        float overTime, lockedAt;
        GUIStyle label, big, small, side;
        Texture2D white;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            if (!bestLoaded)
            {
                bestTime = PlayerPrefs.GetFloat(BestTimeKey, 0f);
                bestLoaded = true;
            }
            State = startAtTitle && !skipTitle ? GameState.Title : GameState.Playing;
            skipTitle = false;
        }

        void Start()
        {
            if (State == GameState.Playing && manageCursor) LockCursor(true);
            else if (State == GameState.Title && manageCursor) LockCursor(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>開始の入口（ボタン・Enter・Space）。タイトルがあれば暗転してから StartGame を呼ぶ。</summary>
        public void RequestStart()
        {
            if (State != GameState.Title) return;
            if (titleScreen != null && titleScreen.Active) titleScreen.BeginStart();
            else StartGame();
        }

        public void StartGame()
        {
            if (State != GameState.Title) return;
            State = GameState.Playing;
            if (manageCursor) LockCursor(true);
        }

        public void EndGame(string reason)
        {
            if (State != GameState.Playing) return;
            State = GameState.GameOver;
            EndReason = reason;
            overTime = Time.unscaledTime;
            if (manageCursor) LockCursor(false);
        }

        /// <summary>血のメーターが満タン＝お腹いっぱいでクリア。</summary>
        public void Clear()
        {
            if (State != GameState.Playing) return;
            State = GameState.Cleared;
            overTime = Time.unscaledTime;
            NewBestTime = bestTime <= 0f || PlayTime < bestTime;
            if (NewBestTime) { bestTime = PlayTime; PlayerPrefs.SetFloat(BestTimeKey, bestTime); }
            PlayerPrefs.Save();
            if (manageCursor) LockCursor(false);
        }

        static string FormatTime(float t)
        {
            int s = Mathf.FloorToInt(t);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        public void Restart()
        {
            skipTitle = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>ポーズ画面の「再開」ボタンから</summary>
        public void ResumeFromUI()
        {
            if (Paused) SetPaused(false);
        }

        void Update()
        {
            if (State == GameState.Title)
            {
                // マウスでは「はじめる」ボタン。キーは Enter / Space（遊び方・設定のパネルが開いている間は無効）
                bool panel = titleScreen != null && titleScreen.PanelOpen;
                if (!panel && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))) RequestStart();
                return;
            }
            if (State == GameState.GameOver || State == GameState.Cleared)
            {
                if (Time.unscaledTime - overTime > 0.6f && (Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(0)))
                    Restart();
                return;
            }
            if (IsPlaying) PlayTime += Time.deltaTime;
            if (!manageCursor) return;
            if (!Paused)
            {
                // Esc、またはブラウザ・OS 側でマウス固定が外れたらポーズ（固定の要求直後は少し待つ）
                bool lost = Time.unscaledTime - lockedAt > 0.6f && Cursor.lockState != CursorLockMode.Locked;
                if (Input.GetKeyDown(KeyCode.Escape) || lost) SetPaused(true);
            }
            else
            {
                // マウスでは「再開」「やり直し」ボタン。キーは Enter で再開、R でやり直し（パネルが開いている間は Enter 無効）
                bool panel = pauseScreen != null && pauseScreen.PanelOpen;
                if (Input.GetKeyDown(KeyCode.R)) Restart();
                else if (!panel && Input.GetKeyDown(KeyCode.Return)) SetPaused(false);
            }
        }

        void SetPaused(bool p)
        {
            Paused = p;
            Time.timeScale = p ? 0f : 1f;
            LockCursor(!p);
            if (pauseScreen != null) pauseScreen.SetVisible(p);
        }

        void LockCursor(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
            if (on) lockedAt = Time.unscaledTime;
        }

        // ---- プレイ中の画面表示（OnGUI）----

        void Styles()
        {
            if (label != null) return;
            var font = uiFont != null ? uiFont : Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo UI", "Meiryo", "Arial" }, 28);
            label = new GUIStyle { font = font, fontSize = 22, normal = { textColor = Color.white }, wordWrap = false };
            big = new GUIStyle(label) { fontSize = 54, alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            side = new GUIStyle(small) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            white = Texture2D.whiteTexture;
        }

        void Rect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        void Bar(Rect r, float v, Color fill)
        {
            Rect(r, new Color(0f, 0f, 0f, 0.55f));
            Rect(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(v), r.height - 4), fill);
        }

        static string StateName(HumanState s)
        {
            switch (s)
            {
                case HumanState.Sleeping: return "寝ている";
                case HumanState.Drowsy: return "うとうと";
                case HumanState.Awake: return "起きている";
                default: return "見つかった！";
            }
        }

        static string PartName(PartType t)
        {
            switch (t)
            {
                case PartType.Ankle: return "足首";
                case PartType.Arm: return "腕";
                case PartType.Neck: return "首";
                default: return "顔";
            }
        }

        void OnGUI()
        {
            if (State == GameState.Title) return;   // タイトルは TitleScreen（UI Canvas）
            if (Paused) return;                      // ポーズ中は PauseScreen（UI Canvas）だけを出す
            Styles();
            // 画面の大きさに合わせて表示を拡大縮小する（高さ 720 を基準）
            float s = Mathf.Clamp(Screen.height / 720f, 0.7f, 3f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * s);
            float w = Screen.width / s, h = Screen.height / s;

            DrawVignette(w, h);
            DrawControlPanel(w, h);

            if (human != null)
            {
                float a = human.Awareness / StealthRules.MaxAwareness;
                var barRect = new Rect(w * 0.5f - 220, h - 70, 440, 26);
                Bar(barRect, a, Color.Lerp(new Color(0.3f, 0.9f, 0.4f), new Color(1f, 0.25f, 0.2f), a));
                small.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(barRect.x, barRect.y - 30, barRect.width, 26), "気づき　" + StateName(human.State), small);

                if (human.State == HumanState.Alert && State == GameState.Playing)
                {
                    float left = Mathf.Max(0f, StealthRules.EscapeSeconds - human.LostTimer);
                    string msg = human.CanSeeMosquito ? "視界から逃げて隠れろ！" : "逃げ切りまで " + left.ToString("0.0") + " 秒";
                    GUI.Label(new Rect(0, h * 0.14f, w, 40), msg, Center(label));
                }
            }

            if (mosquito != null)
            {
                var dashRect = new Rect(w - 244, h - 60, 220, 16);
                Bar(dashRect, mosquito.DashReady01, mosquito.DashReady01 >= 1f ? new Color(0.4f, 0.8f, 1f) : new Color(0.5f, 0.5f, 0.55f));
                GUI.Label(new Rect(dashRect.x, dashRect.y - 26, dashRect.width, 24), "ダッシュ（Shift／右クリック）", Center(small));

                bool sucking = mosquito.CurrentMode == MosquitoController.Mode.Sucking && mosquito.CurrentPart.HasValue && State == GameState.Playing && !Paused;
                if (sucking)
                {
                    var part = mosquito.CurrentPart.Value;
                    GUI.Label(new Rect(0, h * 0.60f, w, 36), "吸血中：" + PartName(part), Center(label));

                    // かゆみの速さ（バレやすさ）メーター。連続で同じ部位を吸うと倍率が上がって伸びる
                    float risk = Mathf.Clamp01(StealthRules.Itch(part) * StealthRules.RepeatMultiplier(mosquito.Streak) / 40f);
                    var riskRect = new Rect(w * 0.5f - 130, h * 0.60f + 44, 260, 16);
                    Bar(riskRect, risk, Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.2f, 0.15f), risk));
                    string r = "バレやすさ" + (mosquito.Streak > 0 ? "（連続 ×" + StealthRules.RepeatMultiplier(mosquito.Streak).ToString("0.0") + "）" : "");
                    GUI.Label(new Rect(riskRect.x - 40, riskRect.y + 18, riskRect.width + 80, 24), r, Center(small));

                    DrawBloodNearMosquito(s, w, h);
                }
            }

            if (State == GameState.GameOver)
            {
                Rect(new Rect(0, 0, w, h), new Color(0.15f, 0f, 0f, 0.6f));
                GUI.Label(new Rect(0, h * 0.28f, w, 80), "GAME OVER（" + EndReason + "）", big);
                GUI.Label(new Rect(0, h * 0.28f + 96, w, 40),
                    "血のメーター " + Mathf.FloorToInt((mosquito != null ? mosquito.Blood01 : 0f) * 100f) + "％"
                    + (EscapeCount > 0 ? "　逃げ切り " + EscapeCount + " 回" : ""), Center(label));
                GUI.Label(new Rect(0, h * 0.28f + 160, w, 30), "R キーかクリックでリトライ", Center(small));
            }

            if (State == GameState.Cleared)
            {
                Rect(new Rect(0, 0, w, h), new Color(0.05f, 0.18f, 0.08f, 0.62f));
                GUI.Label(new Rect(0, h * 0.26f, w, 80), "満腹！ クリア", big);
                GUI.Label(new Rect(0, h * 0.26f + 80, w, 36), "おなかいっぱい。夜の窓から帰っていった", Center(small));
                GUI.Label(new Rect(0, h * 0.26f + 130, w, 40), "クリアタイム  " + FormatTime(PlayTime) + "　ベスト  " + FormatTime(bestTime), Center(label));
                if (NewBestTime) GUI.Label(new Rect(0, h * 0.26f + 172, w, 36), "ベストタイム更新！", Center(label));
                if (EscapeCount > 0) GUI.Label(new Rect(0, h * 0.26f + 212, w, 30), "逃げ切り " + EscapeCount + " 回", Center(small));
                GUI.Label(new Rect(0, h * 0.26f + 262, w, 30), "R キーかクリックでもう一度", Center(small));
            }

            float flashAge = Time.unscaledTime - flashStart;
            if (flashAge < flashDuration)
            {
                var c = flashColor;
                c.a *= 1f - flashAge / flashDuration;
                Rect(new Rect(0, 0, w, h), c);
            }
        }

        /// <summary>血のメーターは、血を吸っている間だけ蚊の横に出す</summary>
        void DrawBloodNearMosquito(float s, float w, float h)
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(mosquito.Position);
            if (sp.z <= 0f) return;
            float v = mosquito.Blood01;
            float x = Mathf.Clamp(sp.x / s + 52f, 8f, w - 218f);
            float y = Mathf.Clamp((Screen.height - sp.y) / s - 32f, 8f, h - 70f);
            Rect(new Rect(x, y, 210, 58), new Color(0f, 0.01f, 0.05f, 0.62f));
            small.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(x + 10, y + 3, 200, 24), "血のメーター　" + Mathf.FloorToInt(v * 100f) + "％", small);
            Color c = Color.Lerp(new Color(0.75f, 0.1f, 0.12f), new Color(1f, 0.25f, 0.25f), v);
            if (v > 0.85f) c = Color.Lerp(c, Color.white, 0.25f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f));   // もうすぐ満腹
            Bar(new Rect(x + 10, y + 32, 190, 16), v, c);
            small.alignment = TextAnchor.MiddleCenter;
        }

        // 画面の右側に、プレイ中ずっと出しておく操作説明
        static readonly string[] SideLines =
        {
            "移動　　　WASD",
            "視点　　　マウス",
            "上昇　　　Space",
            "下降　　　C",
            "吸血　　　左クリック長押し",
            "ダッシュ　Shift／右クリック",
            "ポーズ　　Esc",
        };

        void DrawControlPanel(float w, float h)
        {
            const float pw = 236f, lh = 24f;
            float ph = 34f + SideLines.Length * lh;
            var r = new Rect(w - pw - 14f, h * 0.20f, pw, ph);
            Rect(r, new Color(0f, 0f, 0f, 0.42f));
            GUI.Label(new Rect(r.x + 12, r.y + 4, pw, 26), "操作", Left(small));
            for (int i = 0; i < SideLines.Length; i++)
                GUI.Label(new Rect(r.x + 12, r.y + 32 + i * lh, pw - 12, lh), SideLines[i], side);
        }

        // ---- 演出 ----

        Color flashColor;
        float flashStart = -10f, flashDuration = 0.01f;
        Texture2D vignette;

        /// <summary>画面全体を一瞬染める（見つかった・叩かれた）。</summary>
        public void Flash(Color c, float duration)
        {
            flashColor = c; flashStart = Time.unscaledTime; flashDuration = Mathf.Max(0.01f, duration);
        }

        void DrawVignette(float w, float h)
        {
            if (human == null) return;
            float a = human.Awareness / StealthRules.MaxAwareness;
            float alpha = Mathf.Clamp01((a - 0.35f) / 0.65f) * 0.55f;
            if (human.State == HumanState.Alert) alpha = 0.45f + 0.15f * Mathf.Sin(Time.unscaledTime * 8f);
            if (alpha <= 0.01f) return;
            if (vignette == null)
            {
                const int n = 64;
                vignette = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.55f) / 0.9f);
                        vignette.SetPixel(x, y, new Color(1f, 0.05f, 0.05f, d * d));
                    }
                vignette.Apply();
            }
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(0, 0, w, h), vignette);
            GUI.color = old;
        }

        GUIStyle Left(GUIStyle s) { s.alignment = TextAnchor.UpperLeft; return s; }
        GUIStyle Center(GUIStyle s) { s.alignment = TextAnchor.MiddleCenter; return s; }
    }
}
