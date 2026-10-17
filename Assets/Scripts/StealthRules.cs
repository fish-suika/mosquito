using UnityEngine;

namespace MosquitoGame
{
    public enum PartType { Ankle, Arm, Neck, Face }

    public enum HumanState { Sleeping, Drowsy, Awake, Alert }

    /// <summary>ゲームの決まりごと（点数・気づきゲージ・視界）。数値の調整はここだけで行う。</summary>
    public static class StealthRules
    {
        public const float MaxAwareness = 100f;
        public const float AwarenessDecay = 6f;       // 何にも気づかれていないとき、毎秒これだけ減る
        public const float NoiseRange = 7f;           // 羽音が届く距離
        public const float EscapeSeconds = 5f;        // 攻撃中に視界の外にいればこの秒数で逃げ切り
        public const float AfterEscapeAwareness = 40f;

        public static float SuckPoints(PartType t)
        {
            switch (t)
            {
                case PartType.Ankle: return 10f;
                case PartType.Arm: return 20f;
                case PartType.Neck: return 35f;
                default: return 60f;
            }
        }

        public static float Itch(PartType t)
        {
            switch (t)
            {
                case PartType.Ankle: return 3f;
                case PartType.Arm: return 7f;
                case PartType.Neck: return 14f;
                default: return 26f;
            }
        }

        /// <summary>同じ部位を続けて吸うほどかゆみが速く溜まる（streak = 直前までの連続回数）。</summary>
        public static float RepeatMultiplier(int streak) => 1f + 0.5f * Mathf.Max(0, streak);

        public static float VisionHalfAngle(HumanState s)
        {
            switch (s)
            {
                case HumanState.Drowsy: return 30f;
                case HumanState.Awake: return 55f;
                case HumanState.Alert: return 80f;
                default: return 0f;
            }
        }

        public static float VisionRange(HumanState s)
        {
            switch (s)
            {
                case HumanState.Drowsy: return 25f;
                case HumanState.Awake: return 45f;
                case HumanState.Alert: return 70f;
                default: return 0f;
            }
        }

        public static float VisionRate(HumanState s)
        {
            switch (s)
            {
                case HumanState.Drowsy: return 10f;
                case HumanState.Awake: return 25f;
                default: return 0f;
            }
        }

        public static float NoiseRate(HumanState s)
        {
            switch (s)
            {
                case HumanState.Sleeping: return 4f;
                case HumanState.Drowsy: return 10f;
                case HumanState.Awake: return 14f;
                default: return 0f;
            }
        }

        public static bool InVision(HumanState s, float dist, float angleDeg)
        {
            float range = VisionRange(s);
            return range > 0f && dist < range && angleDeg < VisionHalfAngle(s);
        }

        /// <summary>視界に入っているときに気づきゲージへ入る毎秒の量。</summary>
        public static float VisionGain(HumanState s, float dist, float angleDeg)
        {
            if (!InVision(s, dist, angleDeg)) return 0f;
            return VisionRate(s) * (1f - 0.5f * dist / VisionRange(s));
        }

        /// <summary>羽音で気づきゲージへ入る毎秒の量（近いほど大きい）。</summary>
        public static float NoiseGain(HumanState s, float dist)
        {
            if (dist >= NoiseRange) return 0f;
            return NoiseRate(s) * (1f - dist / NoiseRange);
        }

        /// <summary>gain が 0 より大きければ溜まり、0 なら減る。0〜100 に収める。</summary>
        public static float StepAwareness(float awareness, float gainPerSec, float dt)
        {
            float next = gainPerSec > 0f ? awareness + gainPerSec * dt : awareness - AwarenessDecay * dt;
            return Mathf.Clamp(next, 0f, MaxAwareness);
        }
    }
}
