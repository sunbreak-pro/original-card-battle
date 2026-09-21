using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>§5: statuses are stacks now, and 鈍足 is the one word the slice carries.</summary>
    public class StatusTests
    {
        [Test]
        public void AnEmptySet_HoldsNothing()
        {
            Assert.Multiple(() =>
            {
                Assert.That(StatusSet.Empty.KindCount, Is.EqualTo(0));
                Assert.That(StatusSet.Empty.Has(StatusKind.Slow), Is.False);
                Assert.That(StatusSet.Empty.Stacks(StatusKind.Slow), Is.EqualTo(0));
            });
        }

        [Test]
        public void ApplyingAStatus_GivesTwoStacksByDefault()
        {
            var set = StatusSet.Empty.Add(StatusKind.Slow);
            Assert.That(set.Stacks(StatusKind.Slow), Is.EqualTo(Constants.StatusApplyDefault));
        }

        [Test]
        public void ApplyingTwice_Stacks()
        {
            var set = StatusSet.Empty.Add(StatusKind.Slow, 2).Add(StatusKind.Slow, 1);
            Assert.That(set.Stacks(StatusKind.Slow), Is.EqualTo(3));
        }

        [Test]
        public void SlowIsTheTurnDecayType()
        {
            Assert.That(Statuses.DecayOf(StatusKind.Slow), Is.EqualTo(StatusDecay.OnTurn));
        }

        [Test]
        public void TurnStart_TakesOneStackOffTheTurnDecayType()
        {
            var set = StatusSet.Empty.Add(StatusKind.Slow, 2);
            var after = set.TickTurnStart();
            Assert.That(after.Stacks(StatusKind.Slow), Is.EqualTo(1));
        }

        [Test]
        public void TheLastStackDropsTheWordEntirely()
        {
            var set = StatusSet.Empty.Add(StatusKind.Slow, 1).TickTurnStart();
            Assert.Multiple(() =>
            {
                Assert.That(set.Has(StatusKind.Slow), Is.False);
                Assert.That(set.KindCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void SlowStopsThePositionSwitch_ForAsLongAsItIsHeld()
        {
            var slowed = StatusSet.Empty.Add(StatusKind.Slow, 2);
            Assert.That(Statuses.CanSwitchPosition(slowed), Is.False);

            var afterOneTurn = slowed.TickTurnStart();
            Assert.That(Statuses.CanSwitchPosition(afterOneTurn), Is.False, "one stack is still one stack");

            var afterTwoTurns = afterOneTurn.TickTurnStart();
            Assert.That(Statuses.CanSwitchPosition(afterTwoTurns), Is.True);
        }

        [Test]
        public void APlayerHoldsSixKindsAtMost()
        {
            // One word exists today, so this cannot fire yet; the rule is in place for #48.
            Assert.That(Constants.StatusKindsPlayer, Is.EqualTo(6));

            var atLimit = StatusSet.Empty.Add(StatusKind.Slow, 2, kindLimit: 1);
            var stillAtLimit = atLimit.Add(StatusKind.Slow, 2, kindLimit: 1);

            Assert.Multiple(() =>
            {
                Assert.That(atLimit.KindCount, Is.EqualTo(1));
                Assert.That(stillAtLimit.Stacks(StatusKind.Slow), Is.EqualTo(4), "an existing word keeps stacking");
            });
        }

        [Test]
        public void AddingZeroOrLess_ChangesNothing()
        {
            var set = StatusSet.Empty.Add(StatusKind.Slow, 0);
            Assert.That(set.KindCount, Is.EqualTo(0));
        }

        [Test]
        public void SetsWithTheSameStacks_AreEqual()
        {
            var a = StatusSet.Empty.Add(StatusKind.Slow, 2);
            var b = StatusSet.Of((StatusKind.Slow, 2));
            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
                Assert.That(a, Is.Not.EqualTo(StatusSet.Empty));
            });
        }

        [Test]
        public void AddingDoesNotDisturbTheSetItCameFrom()
        {
            var before = StatusSet.Empty.Add(StatusKind.Slow, 2);
            var after = before.Add(StatusKind.Slow, 2);
            Assert.Multiple(() =>
            {
                Assert.That(before.Stacks(StatusKind.Slow), Is.EqualTo(2));
                Assert.That(after.Stacks(StatusKind.Slow), Is.EqualTo(4));
            });
        }

        [Test]
        public void SlowedCombatantsStateCarriesTheWord()
        {
            var player = Fixtures.Combatant(
                position: Position.Near, statuses: StatusSet.Empty.Add(StatusKind.Slow));

            Assert.Multiple(() =>
            {
                Assert.That(player.Statuses.Has(StatusKind.Slow), Is.True);
                Assert.That(Statuses.CanSwitchPosition(player.Statuses), Is.False);
                Assert.That(player.Position, Is.EqualTo(Position.Near), "pinned where it stood");
            });
        }

        [Test]
        public void SlowOnAPositionlessEnemy_HasNoMechanicalEffect()
        {
            // The canon says 鈍足 blocks the position switch, and the polearm has no position to
            // switch. Implemented literally; the contradiction with the roster note is #116.
            var polearm = Fixtures.Combatant(
                hp: 60, position: null, statuses: StatusSet.Empty.Add(StatusKind.Slow));

            Assert.Multiple(() =>
            {
                Assert.That(polearm.Statuses.Has(StatusKind.Slow), Is.True, "the chip still shows");
                Assert.That(polearm.Position, Is.Null, "and there is nothing for it to pin");
            });
        }
    }
}
