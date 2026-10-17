using NUnit.Framework;

namespace MosquitoGame.Tests
{
    public class StealthRulesTests
    {
        [Test]
        public void SuckPoints_AreHigherForRiskierParts()
        {
            Assert.Less(StealthRules.SuckPoints(PartType.Ankle), StealthRules.SuckPoints(PartType.Arm));
            Assert.Less(StealthRules.SuckPoints(PartType.Arm), StealthRules.SuckPoints(PartType.Neck));
            Assert.Less(StealthRules.SuckPoints(PartType.Neck), StealthRules.SuckPoints(PartType.Face));
        }

        [Test]
        public void RiskierParts_EarnMoreBeforeBeingFound()
        {
            // 見つかるまでの時間 ≒ 100 ÷ （かゆみ ＋ 羽音）。危ない部位ほど「見つかるまでに稼げる点」が大きいこと
            float Earn(PartType p, float noise) => 100f / (StealthRules.Itch(p) + noise) * StealthRules.SuckPoints(p);
            float ankle = Earn(PartType.Ankle, 0f), arm = Earn(PartType.Arm, 1f), neck = Earn(PartType.Neck, 2.5f), face = Earn(PartType.Face, 3f);
            Assert.Less(ankle, arm);
            Assert.Less(arm, neck);
            Assert.Less(neck, face);
        }

        [Test]
        public void BloodGoal_NeedsSeveralVisits_ButIsReachable()
        {
            // 顔を1回吸い切ってもクリアできず（1回で稼げる最大より大きい）、数回で届く（4回以内）こと
            float faceVisit = 100f / (StealthRules.Itch(PartType.Face) + 3f) * StealthRules.SuckPoints(PartType.Face);
            Assert.Greater(StealthRules.BloodGoal, faceVisit);
            Assert.Less(StealthRules.BloodGoal, faceVisit * 4f);
        }

        [Test]
        public void Itch_IsFasterForRiskierParts()
        {
            Assert.Less(StealthRules.Itch(PartType.Ankle), StealthRules.Itch(PartType.Arm));
            Assert.Less(StealthRules.Itch(PartType.Arm), StealthRules.Itch(PartType.Neck));
            Assert.Less(StealthRules.Itch(PartType.Neck), StealthRules.Itch(PartType.Face));
        }

        [Test]
        public void RepeatMultiplier_GrowsWithStreak()
        {
            Assert.AreEqual(1f, StealthRules.RepeatMultiplier(0));
            Assert.AreEqual(2f, StealthRules.RepeatMultiplier(2));
            Assert.AreEqual(1f, StealthRules.RepeatMultiplier(-3));
        }

        [Test]
        public void Vision_SleepingSeesNothing()
        {
            Assert.AreEqual(0f, StealthRules.VisionGain(HumanState.Sleeping, 1f, 0f));
        }

        [Test]
        public void Vision_OutsideConeOrRangeGivesNothing()
        {
            Assert.AreEqual(0f, StealthRules.VisionGain(HumanState.Awake, 10f, 80f));
            Assert.AreEqual(0f, StealthRules.VisionGain(HumanState.Awake, 60f, 0f));
            Assert.Greater(StealthRules.VisionGain(HumanState.Awake, 10f, 10f), 0f);
        }

        [Test]
        public void Vision_ClosersIsStronger()
        {
            Assert.Greater(StealthRules.VisionGain(HumanState.Awake, 5f, 0f), StealthRules.VisionGain(HumanState.Awake, 30f, 0f));
        }

        [Test]
        public void Noise_OnlyWithinRange_AndStrongerWhenClose()
        {
            Assert.AreEqual(0f, StealthRules.NoiseGain(HumanState.Awake, StealthRules.NoiseRange));
            Assert.Greater(StealthRules.NoiseGain(HumanState.Awake, 1f), StealthRules.NoiseGain(HumanState.Awake, 5f));
            Assert.Less(StealthRules.NoiseGain(HumanState.Sleeping, 1f), StealthRules.NoiseGain(HumanState.Awake, 1f));
        }

        [Test]
        public void StepAwareness_RisesWithGain_DecaysWithout_AndClamps()
        {
            Assert.AreEqual(20f, StealthRules.StepAwareness(10f, 10f, 1f), 1e-4f);
            Assert.AreEqual(10f - StealthRules.AwarenessDecay, StealthRules.StepAwareness(10f, 0f, 1f), 1e-4f);
            Assert.AreEqual(0f, StealthRules.StepAwareness(1f, 0f, 10f));
            Assert.AreEqual(StealthRules.MaxAwareness, StealthRules.StepAwareness(99f, 50f, 10f));
        }

        [Test]
        public void Face_FillsGaugeMuchFasterThanAnkle()
        {
            float ankleSeconds = StealthRules.MaxAwareness / StealthRules.Itch(PartType.Ankle);
            float faceSeconds = StealthRules.MaxAwareness / StealthRules.Itch(PartType.Face);
            Assert.Greater(ankleSeconds, faceSeconds * 4f);
        }
    }
}
