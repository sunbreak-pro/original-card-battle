using System;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>§3: the column a card sits in is its cost, and the invest choice is gone.</summary>
    public class ColumnTests
    {
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 3)]
        public void CostOf_IsTheColumnNumber_ExceptColumnFourWhichStaysAtThree(int column, int expected)
        {
            Assert.That(Columns.CostOf(column), Is.EqualTo(expected));
        }

        [TestCase(0)]
        [TestCase(5)]
        [TestCase(-1)]
        public void CostOf_RejectsColumnsOutsideTheTable(int column)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Columns.CostOf(column));
        }

        [Test]
        public void Scales_MatchTheCanonTable()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Columns.SingleAttackPower, Is.EqualTo(new[] { 6, 13, 21, 30 }));
                Assert.That(Columns.SingleGuard, Is.EqualTo(new[] { 4, 9, 15, 22 }));
                Assert.That(Columns.DualAttackPower, Is.EqualTo(new[] { 4, 8, 14, 20 }));
                Assert.That(Columns.DualGuard, Is.EqualTo(new[] { 3, 6, 10, 14 }));
                Assert.That(Columns.SingleSkillKinds, Is.EqualTo(new[] { 1, 2, 3, 3 }));
            });
        }

        [Test]
        public void Value_ReadsColumnsOneBased()
        {
            Assert.That(Columns.Value(Columns.SingleAttackPower, 1), Is.EqualTo(6));
            Assert.That(Columns.Value(Columns.SingleAttackPower, 4), Is.EqualTo(30));
        }

        [Test]
        public void CardDef_TakesItsCostFromItsColumn()
        {
            var card = Fixtures.Card("sweep_test", column: 2, face: new Face(Power: 13));
            Assert.That(card.Cost, Is.EqualTo(2));
        }

        [Test]
        public void EnemyActionDef_TakesItsCostFromTheSameTable()
        {
            var action = Fixtures.EnemyAction("shove_test", column: 2, face: new Face(Power: 5, Push: 2));
            Assert.That(action.Cost, Is.EqualTo(2));
        }

        [Test]
        public void CanPay_RefusesACardTheStaminaDoesNotCover()
        {
            var card = Fixtures.Card("heavy", column: 3, face: new Face(Power: 21));
            Assert.That(Combat.CanPay(card.Cost, stamina: 2), Is.False);
            Assert.That(Combat.CanPay(card.Cost, stamina: 3), Is.True);
        }

        [Test]
        public void MoveCards_SitInColumnOne()
        {
            // §2.1: a single-attribute move card is cost 1 whatever it carries.
            Assert.That(Columns.CostOf(Columns.MoveCardColumn), Is.EqualTo(1));
        }
    }
}
