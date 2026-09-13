using BattleCore;
using NUnit.Framework;

namespace BattleCore.Tests
{
    public class CombatTests
    {
        // rangeToIndex / indexToRange

        [Test]
        public void RangeToIndex_MapsBandsToOrderedIndices()
        {
            Assert.That(Combat.RangeToIndex(RangeBand.Close), Is.EqualTo(0));
            Assert.That(Combat.RangeToIndex(RangeBand.Mid), Is.EqualTo(1));
            Assert.That(Combat.RangeToIndex(RangeBand.Far), Is.EqualTo(2));
        }

        [Test]
        public void IndexToRange_MapsIndicesBackAndClampsOutOfRange()
        {
            Assert.That(Combat.IndexToRange(0), Is.EqualTo(RangeBand.Close));
            Assert.That(Combat.IndexToRange(2), Is.EqualTo(RangeBand.Far));
            Assert.That(Combat.IndexToRange(-5), Is.EqualTo(RangeBand.Close));
            Assert.That(Combat.IndexToRange(99), Is.EqualTo(RangeBand.Far));
        }

        // clampDistance / shiftDistance

        [Test]
        public void ClampDistance_ClampsToZeroTwo()
        {
            Assert.That(Combat.ClampDistance(-1), Is.EqualTo(0));
            Assert.That(Combat.ClampDistance(0), Is.EqualTo(0));
            Assert.That(Combat.ClampDistance(2), Is.EqualTo(2));
            Assert.That(Combat.ClampDistance(3), Is.EqualTo(2));
        }

        [Test]
        public void ShiftDistance_ShiftsThenClamps()
        {
            Assert.That(Combat.ShiftDistance(1, -1), Is.EqualTo(0));
            Assert.That(Combat.ShiftDistance(1, 1), Is.EqualTo(2));
            Assert.That(Combat.ShiftDistance(0, -1), Is.EqualTo(0));
            Assert.That(Combat.ShiftDistance(2, 1), Is.EqualTo(2));
        }

        // staminaRecovery

        [Test]
        public void StaminaRecovery_RecoversByBand()
        {
            Assert.That(Combat.StaminaRecovery(RangeBand.Close), Is.EqualTo(1));
            Assert.That(Combat.StaminaRecovery(RangeBand.Mid), Is.EqualTo(2));
            Assert.That(Combat.StaminaRecovery(RangeBand.Far), Is.EqualTo(3));
        }

        // staminaDamageMultiplier

        [Test]
        public void StaminaDamageMultiplier_IsFullAtOrAboveThreshold()
        {
            Assert.That(Combat.StaminaDamageMultiplier(20), Is.EqualTo(1));
            Assert.That(Combat.StaminaDamageMultiplier(8), Is.EqualTo(1));
        }

        [Test]
        public void StaminaDamageMultiplier_DecaysLinearlyBelowThresholdDownToFloor()
        {
            Assert.That(Combat.StaminaDamageMultiplier(4), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(Combat.StaminaDamageMultiplier(1), Is.EqualTo(0.4));
            Assert.That(Combat.StaminaDamageMultiplier(0), Is.EqualTo(0.4));
        }

        // rangeMultiplier

        [Test]
        public void RangeMultiplier_PeaksAtEffectiveRangeAndFallsOff()
        {
            Assert.That(Combat.RangeMultiplier(0, RangeBand.Close), Is.EqualTo(1));
            Assert.That(Combat.RangeMultiplier(1, RangeBand.Close), Is.EqualTo(0.5));
            Assert.That(Combat.RangeMultiplier(2, RangeBand.Close), Is.EqualTo(0.15));
            Assert.That(Combat.RangeMultiplier(1, RangeBand.Mid), Is.EqualTo(1));
            Assert.That(Combat.RangeMultiplier(2, RangeBand.Far), Is.EqualTo(1));
        }

        // computeAttackDamage

        [Test]
        public void ComputeAttackDamage_FullBasePowerAtOptimalRangeAndFullStamina()
        {
            Assert.That(Combat.ComputeAttackDamage(9, RangeBand.Close, 20, 0), Is.EqualTo(9));
        }

        [Test]
        public void ComputeAttackDamage_HalvesRoundedAtOneBandOff()
        {
            // 9*0.5=4.5 -> 5
            Assert.That(Combat.ComputeAttackDamage(9, RangeBand.Close, 20, 1), Is.EqualTo(5));
        }

        [Test]
        public void ComputeAttackDamage_NearlyWhiffsAtTwoBandsOff()
        {
            // 9*0.15=1.35 -> 1
            Assert.That(Combat.ComputeAttackDamage(9, RangeBand.Close, 20, 2), Is.EqualTo(1));
        }

        [Test]
        public void ComputeAttackDamage_AppliesFatigueDecayOnTopOfRange()
        {
            Assert.That(Combat.ComputeAttackDamage(8, RangeBand.Mid, 4, 1), Is.EqualTo(4));
        }

        [Test]
        public void ComputeAttackDamage_NeverReturnsNegative()
        {
            Assert.That(Combat.ComputeAttackDamage(0, RangeBand.Mid, 4, 1), Is.EqualTo(0));
        }
    }
}
