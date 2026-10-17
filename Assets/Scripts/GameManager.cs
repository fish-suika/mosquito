using UnityEngine;
using UnityEngine.SceneManagement;

namespace MosquitoGame
{
    /// <summary>スコア・ゲームオーバー・リトライ・ポーズと、最小限の画面表示。</summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState { Playing, GameOver }

        public MosquitoController mosquito;
        public Human human;
        [Tooltip("true: クリックでマウスを固定し、Esc で解除してポーズする")]
        public bool manageCursor = true;

        public GameState State { get; private set; }
        public bool Paused { get; private set; }
        public float Score { get; private set; }
        public string EndReason { get; private set; }
        public int EscapeCount;
        public bool IsPlaying => State == GameState.Playing && !Paused;

        static float best;
        float overTime, startTime;
        GUIStyle label, big, small;
        Texture2D white;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
        }

        void Start()
        {
            startTime = Time.unscaledTime;
            if (manageCursor) LockCursor(true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void AddScore(float v)
        {
            if (State == GameState.Playing) Score += v;
        }

        public void EndGame(string reason)
        {
            if (State != GameState.Playing) return;
            State = GameState.GameOver;
            EndReason = reason;
            overTime = Time.unscaledTime;
            if (Score > best) best = Score;
            if (manageCursor) LockCursor(false);
        }

        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void Update()
        {
            if (State == GameState.GameOver)
            {
                if (Time.unscaledTime - overTime > 0.6f && (Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(0)))
                    Restart();
                return;
            }
            if (!manageCursor) return;
            if (!Paused && Input.GetKeyDown(KeyCode.Escape)) SetPaused(true);
            else if (Paused && Input.GetMouseButtonDown(0)) SetPaused(false);
        }

        void SetPaused(bool p)
        {
            Paused = p;
            Time.timeScale = p ? 0f : 1f;
            LockCursor(!p);
        }

        static void LockCursor(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        // ---- 画面表示（OnGUI）----

        void Styles()
        {
            if (label != null) return;
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo UI", "Meiryo", "Arial" }, 28);
            label = new GUIStyle { font = font, fontSize = 22, normal = { textColor = Color.white }, wordWrap = false };
            big = new GUIStyle(label) { fontSize = 54, alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
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
            float w = Screen.width, h = Screen.height;
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

            if (Paused)
            {
                Rect(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.55f));
                GUI.Label(new Rect(0, h * 0.38f, w, 80), "ポーズ中", big);
                GUI.Label(new Rect(0, h * 0.38f + 80, w, 30), "クリックで再開", small);
            }

            if (State == GameState.GameOver)
            {
                Rect(new Rect(0, 0, w, h), new Color(0.15f, 0f, 0f, 0.6f));
                GUI.Label(new Rect(0, h * 0.28f, w, 80), "GAME OVER（" + EndReason + "）", big);
                GUI.Label(new Rect(0, h * 0.28f + 90, w, 60), "スコア  " + Mathf.FloorToInt(Score) + "　ベスト  " + Mathf.FloorToInt(best), Center(label));
                if (EscapeCount > 0)
                    GUI.Label(new Rect(0, h * 0.28f + 130, w, 30), "逃げ切り " + EscapeCount + " 回", Center(small));
                GUI.Label(new Rect(0, h * 0.28f + 180, w, 30), "R キーかクリックでリトライ", Center(small));
            }

            float flashAge = Time.unscaledTime - flashStart;
            if (flashAge < flashDuration)
            {
                var c = flashColor;
                c.a *= 1f - flashAge / flashDuration;
                Rect(new Rect(0, 0, w, h), c);
            }
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
