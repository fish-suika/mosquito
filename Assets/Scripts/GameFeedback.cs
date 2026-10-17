using UnityEngine;

namespace MosquitoGame
{
    /// <summary>音と演出（羽音・吸血音・寝息・叩く音・画面の揺れ・フラッシュ）をまとめて受け持つ。</summary>
    [DefaultExecutionOrder(50)]
    public class GameFeedback : MonoBehaviour
    {
        public MosquitoController mosquito;
        public Human human;
        public ThirdPersonCamera cam;

        [Header("音量")]
        public float masterVolume = 1f;
        public float buzzFar = 0.12f;        // 人間から遠いときの羽音
        public float buzzNear = 0.8f;        // 耳の近くの羽音
        public float buzzNearDistance = 20f; // この距離より近づくほど大きくなる

        AudioSource buzz, gulp, breath, crickets, sfx;
        AudioClip whoosh, slap, alert;

        void Start()
        {
            buzz = Loop("Buzz", ProceduralAudio.Buzz(), 0f, false);
            gulp = Loop("Gulp", ProceduralAudio.Gulp(), 0f, false);
            breath = Loop("Breath", ProceduralAudio.Breath(), 0f, true);
            crickets = Loop("Crickets", ProceduralAudio.Crickets(), 0.05f, false);
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            whoosh = ProceduralAudio.Whoosh();
            slap = ProceduralAudio.Slap();
            alert = ProceduralAudio.Alert();
            clear = ProceduralAudio.Clear();

            if (human != null)
            {
                human.OnAlert += HandleAlert;
                human.OnWindup += HandleWindup;
                human.OnStrike += HandleStrike;
                breath.transform.SetParent(human.head != null ? human.head : human.transform, false);
            }
        }

        void OnDestroy()
        {
            if (human == null) return;
            human.OnAlert -= HandleAlert;
            human.OnWindup -= HandleWindup;
            human.OnStrike -= HandleStrike;
        }

        AudioSource Loop(string name, AudioClip clip, float volume, bool spatial)
        {
            var go = new GameObject("Audio_" + name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = volume;
            s.playOnAwake = false;
            if (spatial)
            {
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 4f;
                s.maxDistance = 45f;
            }
            else s.spatialBlend = 0f;
            s.Play();
            return s;
        }

        void Update()
        {
            if (mosquito == null || human == null) return;
            var gm = GameManager.Instance;
            bool playing = gm == null || gm.IsPlaying;
            float dt = Time.unscaledDeltaTime;
            float master = masterVolume * AudioVolume.Master * (playing ? 1f : 0f);
            float vBuzz = AudioVolume.Buzz, vSfx = AudioVolume.Sfx, vAmb = AudioVolume.Ambient;

            bool sucking = mosquito.CurrentMode == MosquitoController.Mode.Sucking;
            float dist = Vector3.Distance(mosquito.Position, human.head != null ? human.head.position : human.transform.position);
            float near = Mathf.Clamp01(1f - dist / buzzNearDistance);
            float buzzTarget = sucking ? 0.04f : Mathf.Lerp(buzzFar, buzzNear, near * near);
            if (mosquito.IsDashing) buzzTarget *= 1.25f;
            buzz.volume = Mathf.MoveTowards(buzz.volume, buzzTarget * vBuzz * master, 3f * dt);
            float speed = mosquito.Velocity.magnitude / Mathf.Max(0.1f, mosquito.flySpeed);
            buzz.pitch = 1f + 0.15f * Mathf.Clamp01(speed) + (mosquito.IsDashing ? 0.3f : 0f);

            gulp.volume = Mathf.MoveTowards(gulp.volume, (sucking ? 0.45f : 0f) * vSfx * master, 4f * dt);
            if (sucking && mosquito.CurrentPart.HasValue)
                gulp.pitch = 0.9f + StealthRules.SuckPoints(mosquito.CurrentPart.Value) / 200f;

            // 寝息は風のように聞こえてうるさかったので、かなり小さくした（Phase 3 の最終チェックでの指摘）
            float breathTarget = human.State == HumanState.Sleeping ? 0.12f : human.State == HumanState.Drowsy ? 0.07f : 0f;
            breath.volume = Mathf.MoveTowards(breath.volume, breathTarget * vAmb * master, 1f * dt);
            crickets.volume = Mathf.MoveTowards(crickets.volume, 0.05f * vAmb * master, 1f * dt);

            if (gm != null && gm.State == GameManager.GameState.Cleared && !clearPlayed)
            {
                clearPlayed = true;
                sfx.PlayOneShot(clear, 0.8f * AudioVolume.Master * vSfx);
            }
        }

        bool clearPlayed;
        AudioClip clear;

        float Vol() => masterVolume * AudioVolume.Master * AudioVolume.Sfx;

        void HandleAlert()
        {
            sfx.PlayOneShot(alert, 0.9f * Vol());
            if (cam != null) cam.Shake(0.15f, 0.25f);
            if (GameManager.Instance != null) GameManager.Instance.Flash(new Color(1f, 0.15f, 0.1f, 0.35f), 0.5f);
        }

        void HandleWindup()
        {
            sfx.PlayOneShot(whoosh, 0.3f * Vol());   // 風を切る音。うるさいという指摘で 0.7 → 0.3
        }

        void HandleStrike(bool hit)
        {
            sfx.PlayOneShot(slap, (hit ? 1f : 0.7f) * Vol());
            if (cam != null) cam.Shake(hit ? 0.6f : 0.25f, hit ? 0.5f : 0.25f);
            if (hit && GameManager.Instance != null) GameManager.Instance.Flash(new Color(1f, 1f, 1f, 0.85f), 0.45f);
        }
    }
}
