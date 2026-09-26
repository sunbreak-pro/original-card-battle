using System;
using System.Collections.Generic;
using System.Linq;
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
        public void SlowTakesOneCellOffEveryMove_ForAsLongAsItIsHeld()
        {
            var slowed = StatusSet.Empty.Add(StatusKind.Slow, 2);
            Assert.That(Statuses.MoveReduction(slowed), Is.EqualTo(1));
            Assert.That(Combat.CellsAfterSlow(1, slowed), Is.EqualTo(0), "a one-cell move still stops dead");
            Assert.That(Combat.CellsAfterSlow(2, slowed), Is.EqualTo(1), "a two-cell move becomes one");

            var afterOneTurn = slowed.TickTurnStart();
            Assert.That(Statuses.MoveReduction(afterOneTurn), Is.EqualTo(1), "one stack is still one stack");

            var afterTwoTurns = afterOneTurn.TickTurnStart();
            Assert.That(Statuses.MoveReduction(afterTwoTurns), Is.EqualTo(0));
            Assert.That(Combat.CellsAfterSlow(1, afterTwoTurns), Is.EqualTo(1));
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
            var player = Fixtures.Combatant(cell: 2, statuses: StatusSet.Empty.Add(StatusKind.Slow));

            Assert.Multiple(() =>
            {
                Assert.That(player.Statuses.Has(StatusKind.Slow), Is.True);
                Assert.That(Statuses.MoveReduction(player.Statuses), Is.EqualTo(1));
                Assert.That(player.Cell, Is.EqualTo(2), "still where it stood");
            });
        }

        [Test]
        public void SlowOnTheEnemy_ShortensItsPushAndItsStep()
        {
            // v4.3 (§5 / §21.3): 鈍足 reduces every cell change the holder causes — its own move and
            // the push / pull it lands. The old #116 reading (nothing to pin on the polearm) is gone.
            var polearm = Fixtures.Combatant(hp: 60, cell: 5, statuses: StatusSet.Empty.Add(StatusKind.Slow));

            Assert.Multiple(() =>
            {
                Assert.That(polearm.Statuses.Has(StatusKind.Slow), Is.True, "the chip still shows");
                Assert.That(Combat.CellsAfterSlow(2, polearm.Statuses), Is.EqualTo(1), "the two-cell shove pushes one");
                Assert.That(Combat.CellsAfterSlow(1, polearm.Statuses), Is.EqualTo(0), "the one-cell step is stopped");
            });
        }

        // ---- §5 上限: the ターンで減る型 stops at four (#205) ----

        private static IEnumerable<StatusKind> TurnDecayKinds() =>
            Enum.GetValues(typeof(StatusKind)).Cast<StatusKind>().Where(k => Statuses.DecayOf(k) == StatusDecay.OnTurn);

        private static IEnumerable<StatusKind> UseDecayKinds() =>
            Enum.GetValues(typeof(StatusKind)).Cast<StatusKind>().Where(k => Statuses.DecayOf(k) == StatusDecay.OnUse);

        [TestCaseSource(nameof(TurnDecayKinds))]
        public void ATurnDecayWord_StopsAtTheCap_AndTheRestIsDropped(StatusKind kind)
        {
            var set = StatusSet.Empty.Add(kind, 3).Add(kind, 3);
            Assert.That(set.Stacks(kind), Is.EqualTo(Constants.TurnDecayStackMax));
        }

        [TestCaseSource(nameof(UseDecayKinds))]
        public void AUseDecayWord_IsNotCapped(StatusKind kind)
        {
            // §19.5 S14: 溜め放ち keeps its uncounted stacks.
            var set = StatusSet.Empty.Add(kind, 3).Add(kind, 3);
            Assert.That(set.Stacks(kind), Is.EqualTo(6));
        }

        [Test]
        public void AFreshHolder_TakesColumnFoursBleedWhole()
        {
            // Column 4 of the card scale gives 出血 / 再生 4: the cap never cuts one card on a clean holder.
            Assert.That(StatusSet.Empty.Add(StatusKind.Bleed, 4).Stacks(StatusKind.Bleed), Is.EqualTo(4));
        }

        [Test]
        public void AWordAtTheCap_ComesBackUnchanged()
        {
            var full = StatusSet.Of((StatusKind.Regen, Constants.TurnDecayStackMax));
            Assert.That(full.Add(StatusKind.Regen, 2), Is.SameAs(full));
        }

        [Test]
        public void Of_HoldsATurnDecayWordToTheCap_ButNotAUseDecayWord()
        {
            Assert.Multiple(() =>
            {
                Assert.That(StatusSet.Of((StatusKind.Regen, 9)).Stacks(StatusKind.Regen), Is.EqualTo(Constants.TurnDecayStackMax));
                Assert.That(StatusSet.Of((StatusKind.Empower, 9)).Stacks(StatusKind.Empower), Is.EqualTo(9));
            });
        }

        [TestCaseSource(nameof(TurnDecayKinds))]
        public void ToppingUpTwiceEveryTurn_NeverClimbsPastTheCap(StatusKind kind)
        {
            // An elite or a boss can put the same word on twice a phase (2 + 2), and the holder loses
            // one a turn. Uncapped, that climbs by three a turn; capped, it settles at 4 on, 3 after the tick.
            var set = StatusSet.Empty;
            var afterTopUp = new List<int>();
            var afterTick = new List<int>();
            for (int turn = 0; turn < 100; turn++)
            {
                set = set.Add(kind, 2).Add(kind, 2);
                afterTopUp.Add(set.Stacks(kind));
                set = set.TickTurnStart();
                afterTick.Add(set.Stacks(kind));
            }
            Assert.Multiple(() =>
            {
                Assert.That(afterTopUp, Has.All.EqualTo(Constants.TurnDecayStackMax));
                Assert.That(afterTick, Has.All.EqualTo(Constants.TurnDecayStackMax - 1));
            });
        }
    }
}
