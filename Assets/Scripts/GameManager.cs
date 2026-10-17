using UnityEngine;
using UnityEngine.SceneManagement;

namespace MosquitoGame
{
    /// <summary>スコア・タイトル・ゲームオーバー・リトライ・ポーズ・ベストスコア保存と、画面表示。</summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState { Title, Playing, GameOver }

        public MosquitoController mosquito;
        public Human human;
        [Tooltip("true: クリックでマウスを固定し、Esc（またはマウス固定が外れたとき）でポーズする")]
        public bool manageCursor = true;
        [Tooltip("true: 最初にタイトル画面を出す（リトライのときは出さない）")]
        public bool startAtTitle = true;
        [Tooltip("画面表示の日本語フォント。空なら Editor では OS のフォントを使う（WebGL では必ず設定する）")]
        public Font uiFont;

        public GameState State { get; private set; }
        public bool Paused { get; private set; }
        public float Score { get; private set; }
        public float Best => best;
        public bool NewRecord { get; private set; }
        public string EndReason { get; private set; }
        public int EscapeCount;
        public bool IsPlaying => State == GameState.Playing && !Paused;

        const string BestKey = "mosquito.best";
        static bool skipTitle;      // リトライ（シーン再読み込み）のときはタイトルを飛ばす
        static float best;
        static bool bestLoaded;

        float overTime, startTime, lockedAt;
        GUIStyle label, big, small, title;
        Texture2D white;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            if (!bestLoaded)
            {
                best = PlayerPrefs.GetFloat(BestKey, 0f);
                bestLoaded = true;
            }
            State = startAtTitle && !skipTitle ? GameState.Title : GameState.Playing;
            skipTitle = false;
        }

        void Start()
        {
            startTime = Time.unscaledTime;
            if (State == GameState.Playing && manageCursor) LockCursor(true);
            else if (State == GameState.Title && manageCursor) LockCursor(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void AddScore(float v)
        {
            if (State == GameState.Playing) Score += v;
        }

        public void StartGame()
        {
            if (State != GameState.Title) return;
            State = GameState.Playing;
            startTime = Time.unscaledTime;
            if (manageCursor) LockCursor(true);
        }

        public void EndGame(string reason)
        {
            if (State != GameState.Playing) return;
            State = GameState.GameOver;
            EndReason = reason;
            overTime = Time.unscaledTime;
            NewRecord = Score > best && Score >= 1f;
            if (NewRecord)
            {
                best = Score;
                PlayerPrefs.SetFloat(BestKey, best);
                PlayerPrefs.Save();
            }
            if (manageCursor) LockCursor(false);
        }

        public void Restart()
        {
            skipTitle = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void Update()
        {
            if (State == GameState.Title)
            {
                if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) StartGame();
                return;
            }
            if (State == GameState.GameOver)
            {
                if (Time.unscaledTime - overTime > 0.6f && (Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(0)))
                    Restart();
                return;
            }
            if (!manageCursor) return;
            if (!Paused)
            {
                // Esc、またはブラウザ・OS 側でマウス固定が外れたらポーズ（固定の要求直後は少し待つ）
                bool lost = Time.unscaledTime - lockedAt > 0.6f && Cursor.lockState != CursorLockMode.Locked;
                if (Input.GetKeyDown(KeyCode.Escape) || lost) SetPaused(true);
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.R)) Restart();
                else if (Input.GetMouseButtonDown(0)) SetPaused(false);
            }
        }

        void SetPaused(bool p)
        {
            Paused = p;
            Time.timeScale = p ? 0f : 1f;
            LockCursor(!p);
        }

        void LockCursor(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
            if (on) lockedAt = Time.unscaledTime;
        }

        // ---- 画面表示（OnGUI）----

        void Styles()
        {
            if (label != null) return;
            var font = uiFont != null ? uiFont : Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo UI", "Meiryo", "Arial" }, 28);
            label = new GUIStyle { font = font, fontSize = 22, normal = { textColor = Color.white }, wordWrap = false };
            big = new GUIStyle(label) { fontSize = 54, alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            title = new GUIStyle(label) { fontSize = 72, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
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
            Styles();
            // 画面の大きさに合わせて表示を拡大縮小する（高さ 720 を基準）
            float s = Mathf.Clamp(Screen.height / 720f, 0.7f, 3f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * s);
            float w = Screen.width / s, h = Screen.height / s;

            if (State == GameState.Title)
            {
                DrawTitle(w, h);
                return;
            }

            DrawVignette(w, h);

            label.alignment = TextAnchor.UpperLeft;
            GUI.Label(new Rect(24, 16, 500, 36), "スコア  " + Mathf.FloorToInt(Score), label);
            if (best > 0f) GUI.Label(new Rect(24, 48, 500, 30), "ベスト  " + Mathf.FloorToInt(best), Left(small));

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
                GUI.Label(new Rect(dashRect.x, dashRect.y - 26, dashRect.width, 24), "ダッシュ（右クリック）", Center(small));

                if (mosquito.CurrentMode == MosquitoController.Mode.Sucking && mosquito.CurrentPart.HasValue)
                {
                    var part = mosquito.CurrentPart.Value;
                    string t = "吸血中：" + PartName(part) + "  +" + Mathf.RoundToInt(mosquito.CurrentSuckRate) + "/秒　この吸血 +" + Mathf.FloorToInt(mosquito.SessionPoints);
                    GUI.Label(new Rect(0, h * 0.60f, w, 36), t, Center(label));

                    // かゆみの速さ（バレやすさ）メーター。連続で同じ部位を吸うと倍率が上がって伸びる
                    float risk = Mathf.Clamp01(StealthRules.Itch(part) * StealthRules.RepeatMultiplier(mosquito.Streak) / 40f);
                    var riskRect = new Rect(w * 0.5f - 130, h * 0.60f + 44, 260, 16);
                    Bar(riskRect, risk, Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.2f, 0.15f), risk));
                    string r = "バレやすさ" + (mosquito.Streak > 0 ? "（連続 ×" + StealthRules.RepeatMultiplier(mosquito.Streak).ToString("0.0") + "）" : "");
                    GUI.Label(new Rect(riskRect.x - 40, riskRect.y + 18, riskRect.width + 80, 24), r, Center(small));
                }
            }

            if (Time.unscaledTime - startTime < 10f && State == GameState.Playing)
                GUI.Label(new Rect(0, h - 120, w, 28), "WASD 移動　Space 上昇　Shift 下降　左クリック長押し 吸血　右クリック ダッシュ　Esc ポーズ", Center(small));

            if (Paused) DrawPause(w, h);

            if (State == GameState.GameOver)
            {
                Rect(new Rect(0, 0, w, h), new Color(0.15f, 0f, 0f, 0.6f));
                GUI.Label(new Rect(0, h * 0.26f, w, 80), "GAME OVER（" + EndReason + "）", big);
                GUI.Label(new Rect(0, h * 0.26f + 90, w, 40), "スコア  " + Mathf.FloorToInt(Score) + "　ベスト  " + Mathf.FloorToInt(best), Center(label));
                if (NewRecord) GUI.Label(new Rect(0, h * 0.26f + 130, w, 36), "ベスト更新！", Center(label));
                if (EscapeCount > 0)
                    GUI.Label(new Rect(0, h * 0.26f + 170, w, 30), "逃げ切り " + EscapeCount + " 回", Center(small));
                GUI.Label(new Rect(0, h * 0.26f + 220, w, 30), "R キーかクリックでリトライ", Center(small));
            }

            float flashAge = Time.unscaledTime - flashStart;
            if (flashAge < flashDuration)
            {
                var c = flashColor;
                c.a *= 1f - flashAge / flashDuration;
                Rect(new Rect(0, 0, w, h), c);
            }
        }

        // ---- タイトル・あそびかた・ポーズ ----

        static readonly string[] HowToLines =
        {
            "【操作】",
            "マウス：視点　　WASD：移動　　Space：上昇　　Shift：下降",
            "左クリック長押し：肌に着地して吸血　　右クリック：回避ダッシュ",
            "【ルール】",
            "吸う場所が危ないほど高得点（顔 " + (int)StealthRules.SuckPoints(PartType.Face) + "・首 " + (int)StealthRules.SuckPoints(PartType.Neck)
                + "・腕 " + (int)StealthRules.SuckPoints(PartType.Arm) + "・足首 " + (int)StealthRules.SuckPoints(PartType.Ankle) + " 点／秒）",
            "かゆみ・視界・羽音で「気づき」が溜まり、満タンで見つかる",
            "見つかったら手で叩いてくる。ダッシュでよけて、物陰に隠れれば逃げ切り",
            "叩かれたらゲームオーバー。スコアを稼ごう",
        };

        void DrawHowTo(float w, float top)
        {
            for (int i = 0; i < HowToLines.Length; i++)
            {
                bool head = HowToLines[i].StartsWith("【");
                var st = head ? Center(label) : Center(small);
                GUI.Label(new Rect(0, top + i * 30 + (head ? 6 : 0), w, 30), HowToLines[i], st);
            }
        }

        void DrawTitle(float w, float h)
        {
            Rect(new Rect(0, 0, w, h), new Color(0.02f, 0.03f, 0.08f, 0.82f));
            GUI.Label(new Rect(0, h * 0.10f, w, 90), "蚊のステルスゲーム", title);
            GUI.Label(new Rect(0, h * 0.10f + 90, w, 36), "夜の寝室で、人間に見つからないように血を吸え", Center(label));
            DrawHowTo(w, h * 0.38f);
            if (best > 0f) GUI.Label(new Rect(0, h * 0.38f + HowToLines.Length * 30 + 20, w, 30), "ベスト  " + Mathf.FloorToInt(best), Center(label));
            float blink = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3f);
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, blink);
            GUI.Label(new Rect(0, h * 0.88f, w, 40), "クリックではじめる", Center(label));
            GUI.color = old;
        }

        void DrawPause(float w, float h)
        {
            Rect(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.7f));
            GUI.Label(new Rect(0, h * 0.08f, w, 80), "ポーズ中", big);
            DrawHowTo(w, h * 0.30f);
            GUI.Label(new Rect(0, h * 0.82f, w, 36), "クリックで再開　　R でやり直し", Center(label));
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
