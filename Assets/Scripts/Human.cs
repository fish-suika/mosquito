using UnityEngine;

namespace MosquitoGame
{
    /// <summary>人間：寝ている/うとうと/起きている/攻撃中、気づきゲージ、叩く動作。</summary>
    [DefaultExecutionOrder(-20)]
    public class Human : MonoBehaviour
    {
        enum Phase { Idle, Windup, Strike, Recover }

        [Header("参照")]
        public Transform yawPivot;   // 腰。ここを中心に上体が起き上がる/左右を向く
        public Transform upper;      // 上体（起き上がりの回転）
        public Transform head;       // 頭。head.up が顔の向き（=視線）
        public Transform hand;       // 叩く手
        public Transform marker;     // 叩く場所の予兆

        public MosquitoController mosquito;

        [Header("姿勢")]
        public float sitAngle = 70f;

        [Header("攻撃")]
        public float firstAttackDelay = 1.0f;
        public float windupTime = 0.55f;
        public float lockAfter = 0.35f;       // 構えてからこの秒数までは蚊を追いかけ、以降は場所を固定する
        public float strikeTime = 0.12f;
        public float recoverTime = 0.8f;
        public float hitRadius = 2.8f;
        public float handHeight = 9f;

        [Header("テスト用")]
        public bool freezeStateChanges;       // true なら寝る/起きるの自動切り替えをしない

        public HumanState State { get; private set; } = HumanState.Sleeping;
        public float Awareness { get; private set; }
        public float LostTimer { get; private set; }
        public bool CanSeeMosquito { get; private set; }
        public float ItchRate;

        float stateTimer = 15f, sit, yaw, atkTimer, phaseElapsed;
        Phase phase;
        Vector3 target;
        static readonly RaycastHit[] hits = new RaycastHit[16];

        void Start()
        {
            SetAttackVisible(false);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return;
            float dt = Time.deltaTime;

            if (State == HumanState.Alert) UpdateAlert(dt);
            else UpdateCalm(dt);

            AnimatePose(dt);
        }

        // ---- 気づきゲージと状態 ----

        void UpdateCalm(float dt)
        {
            ComputeVision(State, out bool visible, out float visionGain, out float dist);
            CanSeeMosquito = visible;
            float noise = StealthRules.NoiseGain(State, dist);
            Awareness = StealthRules.StepAwareness(Awareness, visionGain + noise + ItchRate, dt);
            if (Awareness >= StealthRules.MaxAwareness)
            {
                EnterAlert();
                return;
            }

            if (freezeStateChanges) return;
            stateTimer -= dt;
            if (stateTimer > 0f) return;
            switch (State)
            {
                case HumanState.Sleeping: State = HumanState.Drowsy; stateTimer = Random.Range(5f, 8f); break;
                case HumanState.Drowsy:
                    if (Random.value < 0.6f) { State = HumanState.Awake; stateTimer = Random.Range(5f, 8f); }
                    else { State = HumanState.Sleeping; stateTimer = Random.Range(14f, 22f); }
                    break;
                default: State = HumanState.Drowsy; stateTimer = Random.Range(4f, 6f); break;
            }
        }

        void EnterAlert()
        {
            State = HumanState.Alert;
            Awareness = StealthRules.MaxAwareness;
            LostTimer = 0f;
            phase = Phase.Idle;
            atkTimer = firstAttackDelay;
        }

        void UpdateAlert(float dt)
        {
            ComputeVision(HumanState.Alert, out bool visible, out _, out _);
            CanSeeMosquito = visible;
            LostTimer = visible ? 0f : LostTimer + dt;
            if (LostTimer >= StealthRules.EscapeSeconds)
            {
                Escape();
                return;
            }
            UpdateAttack(dt);
        }

        void Escape()
        {
            State = HumanState.Drowsy;
            Awareness = StealthRules.AfterEscapeAwareness;
            stateTimer = Random.Range(5f, 8f);
            LostTimer = 0f;
            phase = Phase.Idle;
            SetAttackVisible(false);
            var gm = GameManager.Instance;
            if (gm != null) gm.EscapeCount++;
        }

        void ComputeVision(HumanState s, out bool visible, out float visionGain, out float dist)
        {
            visible = false; visionGain = 0f; dist = float.MaxValue;
            if (mosquito == null || head == null) return;
            Vector3 eye = head.position + head.up * 1.2f;
            Vector3 to = mosquito.Position - eye;
            dist = to.magnitude;
            float angle = Vector3.Angle(head.up, to);
            if (!StealthRules.InVision(s, dist, angle)) return;
            if (Blocked(eye, to, dist)) return;
            visible = true;
            visionGain = StealthRules.VisionGain(s, dist, angle);
        }

        static bool Blocked(Vector3 eye, Vector3 to, float dist)
        {
            float len = dist - 0.35f;
            if (len <= 0f) return false;
            int n = Physics.RaycastNonAlloc(eye, to / dist, hits, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (hits[i].collider.GetComponentInParent<Human>() == null) return true;   // 人間自身は視界を遮らない
            return false;
        }

        // ---- 叩く ----

        void UpdateAttack(float dt)
        {
            if (mosquito == null) return;
            phaseElapsed += dt;
            switch (phase)
            {
                case Phase.Idle:
                    atkTimer -= dt;
                    if (atkTimer <= 0f)
                    {
                        phase = Phase.Windup; phaseElapsed = 0f;
                        target = mosquito.Position;
                        SetAttackVisible(true);
                        PlaceAttack(1f);
                    }
                    break;

                case Phase.Windup:
                    if (phaseElapsed < lockAfter) target = mosquito.Position;
                    PlaceAttack(1f - 0.35f * Mathf.Clamp01(phaseElapsed / windupTime));
                    if (phaseElapsed >= windupTime) { phase = Phase.Strike; phaseElapsed = 0f; }
                    break;

                case Phase.Strike:
                    PlaceAttack(Mathf.Lerp(0.65f, 0f, Mathf.Clamp01(phaseElapsed / strikeTime)));
                    if (phaseElapsed >= strikeTime)
                    {
                        phase = Phase.Recover; phaseElapsed = 0f;
                        if (Vector3.Distance(mosquito.Position, target) < hitRadius)
                        {
                            var gm = GameManager.Instance;
                            if (gm != null) gm.EndGame("叩かれた");
                        }
                    }
                    break;

                case Phase.Recover:
                    if (phaseElapsed >= recoverTime)
                    {
                        phase = Phase.Idle; atkTimer = 0.3f;
                        SetAttackVisible(false);
                    }
                    break;
            }
        }

        /// <summary>height01: 1=高く構えた位置、0=叩いた位置</summary>
        void PlaceAttack(float height01)
        {
            if (hand != null) hand.position = target + Vector3.up * (handHeight * height01);
            if (marker != null)
            {
                float groundY = 0.05f;
                if (Physics.Raycast(target + Vector3.up * 0.2f, Vector3.down, out RaycastHit h, 40f, ~0, QueryTriggerInteraction.Ignore)
                    && h.collider.GetComponentInParent<Human>() == null)
                    groundY = h.point.y + 0.05f;
                marker.position = new Vector3(target.x, groundY, target.z);
            }
        }

        void SetAttackVisible(bool v)
        {
            if (hand != null) hand.gameObject.SetActive(v);
            if (marker != null) marker.gameObject.SetActive(v);
        }

        // ---- 姿勢 ----

        void AnimatePose(float dt)
        {
            float targetSit, speed = 0.8f;
            switch (State)
            {
                case HumanState.Sleeping: targetSit = 0f; break;
                case HumanState.Drowsy: targetSit = 0.25f; break;
                case HumanState.Awake: targetSit = 1f; break;
                default: targetSit = 1f; speed = 2.5f; break;
            }
            sit = Mathf.MoveTowards(sit, targetSit, speed * dt);
            if (upper != null) upper.localRotation = Quaternion.Euler(0f, 0f, -sitAngle * sit);

            float targetYaw = 0f, yawSpeed = 40f;
            if (State == HumanState.Awake) targetYaw = 45f * Mathf.Sin(Time.time * 0.5f);
            else if (State == HumanState.Alert)
            {
                yawSpeed = 120f;
                if (CanSeeMosquito && mosquito != null && yawPivot != null)
                {
                    Vector3 d = mosquito.Position - yawPivot.position;
                    targetYaw = Mathf.Clamp(Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg, -80f, 80f);
                }
                else targetYaw = 60f * Mathf.Sin(Time.time * 0.9f);   // 見失ったら左右を探す
            }
            yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, yawSpeed * dt);
            if (yawPivot != null) yawPivot.localRotation = Quaternion.Euler(0f, yaw * Mathf.Clamp01(sit * 2f), 0f);
        }
    }
}
