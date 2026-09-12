using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    public class CombatTests
    {
        [TestCase(0, RangeBand.Close, 1.0)]
        [TestCase(1, RangeBand.Close, 0.5)]
        [TestCase(2, RangeBand.Close, 0.15)]
        [TestCase(1, RangeBand.Mid, 1.0)]
        [TestCase(0, RangeBand.Far, 0.15)]
        public void RangeMultiplier_FollowsDiffTable(int distance, RangeBand eff, double expected)
        {
            Assert.That(Combat.RangeMultiplier(distance, eff), Is.EqualTo(expected));
        }

        [Test]
        public void ComputeAttackDamage_RoundsHalfAwayFromZero()
        {
            // 5 × 0.5 = 2.5 → 3 (not banker's 2)
            Assert.That(Combat.ComputeAttackDamage(5, RangeBand.Close, 1), Is.EqualTo(3));
            // 11 × 0.15 = 1.65 → 2
            Assert.That(Combat.ComputeAttackDamage(11, RangeBand.Close, 2), Is.EqualTo(2));
            // 1 × 0.15 = 0.15 → 0
            Assert.That(Combat.ComputeAttackDamage(1, RangeBand.Close, 2), Is.EqualTo(0));
        }

        [Test]
        public void ComputeAttackDamage_DesperateMultiplies()
        {
            Assert.That(Combat.ComputeAttackDamage(8, RangeBand.Close, 0, desperate: true), Is.EqualTo(12));
        }

        [Test]
        public void ApplyGuard_SplitsDamageAndGuard()
        {
            var (damage, guardAfter, absorbed) = Combat.ApplyGuard(6, 3);
            Assert.That(damage, Is.EqualTo(3));
            Assert.That(guardAfter, Is.EqualTo(0));
            Assert.That(absorbed, Is.EqualTo(3));

            var full = Combat.ApplyGuard(2, 5);
            Assert.That(full.Damage, Is.EqualTo(0));
            Assert.That(full.GuardAfter, Is.EqualTo(3));
        }

        [TestCase(2, 0)]
        [TestCase(3, 2)]
        [TestCase(10, 2)]
        public void ReserveGuard_ThresholdThree(int left, int expected)
        {
            Assert.That(Combat.ReserveGuard(left), Is.EqualTo(expected));
        }

        [TestCase(0, 0)]
        [TestCase(19, 0)]
        [TestCase(20, 1)]
        [TestCase(59, 2)]
        [TestCase(80, 4)]
        [TestCase(99, 4)]
        public void MiasmaStaminaPenalty_StepsEveryTwentyPercent(int percent, int expected)
        {
            Assert.That(Combat.MiasmaStaminaPenalty(percent), Is.EqualTo(expected));
        }

        [Test]
        public void ComputeMaxStamina_ClampsTempAndRange()
        {
            Assert.That(Combat.ComputeMaxStamina(0, 0), Is.EqualTo(10));
            Assert.That(Combat.ComputeMaxStamina(+7, 0), Is.EqualTo(14));   // temp clamped to +4
            Assert.That(Combat.ComputeMaxStamina(-9, 90), Is.EqualTo(3));   // floor 3
            Assert.That(Combat.ComputeMaxStamina(0, 32), Is.EqualTo(9));
        }

        [Test]
        public void ChooseInvest_KeepsReserveThenFallsBackToMinimum()
        {
            Assert.That(Combat.ChooseInvest(1, 10), Is.EqualTo(3));   // 10 − 3 = 7 ≥ 3
            Assert.That(Combat.ChooseInvest(1, 5), Is.EqualTo(2));    // 5 − 2 = 3
            Assert.That(Combat.ChooseInvest(1, 3), Is.EqualTo(1));    // nothing keeps 3 → minimum
            Assert.That(Combat.ChooseInvest(0, 2), Is.EqualTo(0));
            Assert.That(Combat.ChooseInvest(1, 0), Is.Null);
        }

        [Test]
        public void ShiftDistance_ClampsAtEnds()
        {
            Assert.That(Combat.ShiftDistance(0, -1), Is.EqualTo(0));
            Assert.That(Combat.ShiftDistance(2, 1), Is.EqualTo(2));
            Assert.That(Combat.ShiftDistance(1, 1), Is.EqualTo(2));
        }
    }
}
