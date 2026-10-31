// #575: the numbers in the status panel's 効き方 come from the BattleCore constants the core's own
// arithmetic reads, so the two cannot drift. Runs under `dotnet test` (Depiction.Bridge.Tests.csproj)
// and in Unity's EditMode runner from the same file.
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class StatusChipNumbersTests
    {
        [Test]
        public void Slow_SaysTheCellsTheCoreTakesOff()
        {
            int taken = Statuses.MoveReduction(StatusSet.Of((StatusKind.Slow, 1)));
            Assert.That(taken, Is.EqualTo(Statuses.SlowCellsPerMove));
            Assert.That(StatusChipText.Effect(StatusKind.Slow, 1), Does.EndWith(Statuses.SlowCellsPerMove + " 減る"));
        }

        [Test]
        public void Swift_SaysTheCellsOfTheFreeStep()
        {
            Assert.That(StatusChipText.Effect(StatusKind.Swift, 1), Does.Contain(Statuses.SwiftFreeCells + " マスだけ"));
        }

        [Test]
        public void MiasmaShroud_SaysTheMaximumStaminaEachStackTakes()
        {
            Assert.That(StatusChipText.Effect(StatusKind.MiasmaShroud, 2), Does.Contain("スタック 1 につき " + Statuses.MiasmaShroudMaxStaminaPerStack + " 下がる"));
        }

        [Test]
        public void Depths_SaysWhatEachStackTakesAndTheCoreTakesTheSame()
        {
            var depths = StatusSet.Of((StatusKind.Depths, 2));
            Assert.That(Statuses.RecoveryLoss(depths, adjacent: true, recoveryBefore: 6), Is.EqualTo(2 * Statuses.DepthsRecoveryPerStack));
            Assert.That(Statuses.RecoveryLoss(depths, adjacent: false, recoveryBefore: 6), Is.EqualTo(0));
            Assert.That(StatusChipText.Effect(StatusKind.Depths, 2), Does.Contain("スタック 1 につき " + Statuses.DepthsRecoveryPerStack + " 減る"));
        }

        [Test]
        public void Withering_SaysWhatEachStackTakesAndTheFloorItStopsAt()
        {
            var withering = StatusSet.Of((StatusKind.Withering, 2));
            Assert.That(Statuses.RecoveryLoss(withering, adjacent: false, recoveryBefore: 6), Is.EqualTo(2 * Statuses.WitheringRecoveryPerStack));
            Assert.That(Statuses.RecoveryLoss(withering, adjacent: false, recoveryBefore: Statuses.WitheringRecoveryFloor), Is.EqualTo(0));
            string text = StatusChipText.Effect(StatusKind.Withering, 2);
            Assert.That(text, Does.Contain("スタック 1 につき " + Statuses.WitheringRecoveryPerStack + " 減る"));
            Assert.That(text, Does.Contain(Statuses.WitheringRecoveryFloor + " より下がらない"));
        }
    }
}
