using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>§5.1 のダメージ式 and the Guard rules around it.</summary>
    public class CombatTests
    {
        [Test]
        public void RawPower_IsTheFacePlusTheTrait()
        {
            // 薙ぎ払い against a player standing 遠間: 8 + 3.
            Assert.That(Combat.ComputeRawPower(face: 8, traitBonus: 3), Is.EqualTo(11));
        }

        [Test]
        public void RawPower_IsJustTheFace_WhenNoTraitFired()
        {
            Assert.That(Combat.ComputeRawPower(face: 8), Is.EqualTo(8));
        }

        [Test]
        public void RawPower_AddsBeforeItMultiplies()
        {
            // (6 + 4) × 1.5 = 15, not 6 + (4 × 1.5).
            Assert.That(Combat.ComputeRawPower(face: 6, traitBonus: 4, empowerMult: 1.5), Is.EqualTo(15));
        }

        [Test]
        public void RawPower_RoundsHalfAwayFromZero()
        {
            // 5 × 1.5 = 7.5 → 8, not the banker's 7 that the design tables would not read as.
            Assert.That(Combat.ComputeRawPower(face: 5, empowerMult: 1.5), Is.EqualTo(8));
            // 3 × 1.5 = 4.5 → 5.
            Assert.That(Combat.ComputeRawPower(face: 3, empowerMult: 1.5), Is.EqualTo(5));
        }

        [Test]
        public void RawPower_NeverGoesBelowZero()
        {
            Assert.That(Combat.ComputeRawPower(face: 2, traitBonus: -5), Is.EqualTo(0));
        }

        [Test]
        public void ApplyGuard_SubtractsGuardAndSpendsIt()
        {
            var (damage, guardAfter, absorbed) = Combat.ApplyGuard(raw: 11, guard: 4);
            Assert.Multiple(() =>
            {
                Assert.That(damage, Is.EqualTo(7));
                Assert.That(guardAfter, Is.EqualTo(0));
                Assert.That(absorbed, Is.EqualTo(4));
            });
        }

        [Test]
        public void ApplyGuard_StopsAtZeroDamageWhenGuardCovers()
        {
            var (damage, guardAfter, absorbed) = Combat.ApplyGuard(raw: 5, guard: 9);
            Assert.Multiple(() =>
            {
                Assert.That(damage, Is.EqualTo(0));
                Assert.That(guardAfter, Is.EqualTo(4));
                Assert.That(absorbed, Is.EqualTo(5));
            });
        }

        [TestCase(0, 0)]
        [TestCase(2, 0)]
        [TestCase(3, 3)]
        [TestCase(9, 3)]
        public void ReserveGuard_PaysThreeOnlyAtThreeStaminaOrMore(int staminaLeft, int expected)
        {
            Assert.That(Combat.ReserveGuard(staminaLeft), Is.EqualTo(expected));
        }

        [Test]
        public void RecoverStamina_AddsThreeAndCapsAtTheMaximum()
        {
            Assert.That(Combat.RecoverStamina(current: 4, max: 10, recovery: Constants.StaminaRecovery), Is.EqualTo(7));
            Assert.That(Combat.RecoverStamina(current: 9, max: 10, recovery: Constants.StaminaRecovery), Is.EqualTo(10));
        }

        [Test]
        public void RecoverStamina_TakesTheBonusTheReserveTraitLeftBehind()
        {
            Assert.That(Combat.RecoverStamina(current: 2, max: 10, recovery: 2, bonus: 1), Is.EqualTo(5));
        }

        [TestCase(0, 5)]
        [TestCase(2, 7)]
        [TestCase(-3, 3)]
        [TestCase(-9, 3)]
        [TestCase(9, 8)]
        public void DrawCount_IsFivePlusTheModifier_ClampedToThreeAndEight(int modifier, int expected)
        {
            Assert.That(Combat.DrawCount(modifier), Is.EqualTo(expected));
        }

        [Test]
        public void ClampMaxStamina_StaysWithinThreeAndFourteen()
        {
            Assert.That(Combat.ClampMaxStamina(1), Is.EqualTo(3));
            Assert.That(Combat.ClampMaxStamina(10), Is.EqualTo(10));
            Assert.That(Combat.ClampMaxStamina(20), Is.EqualTo(14));
        }

        [Test]
        public void IsDefeated_TripsTheMomentHpReachesZero()
        {
            Assert.That(Combat.IsDefeated(1), Is.False);
            Assert.That(Combat.IsDefeated(0), Is.True);
            Assert.That(Combat.IsDefeated(-4), Is.True);
        }

        [Test]
        public void Constants_MatchTheCanonNumbers()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Constants.HandDraw, Is.EqualTo(5));
                Assert.That(Constants.HandLimit, Is.EqualTo(8));
                Assert.That(Constants.BaseMaxStamina, Is.EqualTo(10));
                Assert.That(Constants.StaminaRecovery, Is.EqualTo(3));
                Assert.That(Constants.PlayerMaxHp, Is.EqualTo(50));
                Assert.That(Constants.ReserveThreshold, Is.EqualTo(3));
                Assert.That(Constants.ReserveGuard, Is.EqualTo(3));
                Assert.That(Constants.DeckMin, Is.EqualTo(20));
                Assert.That(Constants.DeckMax, Is.EqualTo(40));
                Assert.That(Constants.CopiesMax, Is.EqualTo(3));
                Assert.That(Constants.StatusApplyDefault, Is.EqualTo(2));
            });
        }

        [Test]
        public void FaceOrder_PutsAttackBeforeMoveBeforeGuard()
        {
            Assert.That(Constants.FaceOrder, Is.EqualTo(new[]
            {
                BattleAttribute.Attack,
                BattleAttribute.Move,
                BattleAttribute.Guard,
                BattleAttribute.Skill,
                BattleAttribute.Stance,
            }));
        }
    }
}
