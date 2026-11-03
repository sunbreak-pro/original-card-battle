// The pips of the stamina tag (Script/StaminaPips.cs, #822): battle-visual-v1 §4.7. Ten to a row, the
// 11th and after on a second row with the tag 20 px taller, the 構え divider on the first row only, and
// stamina above the maximum (息継ぎの笛, #508) as pips of their own after the maximum's.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class StaminaPipsTests
    {
        private static List<PipShape> Shapes(int stamina, int max, int cost = 0, int next = 0) =>
            Enumerable.Range(0, StaminaPips.Count(stamina, max)).Select(i => StaminaPips.ShapeAt(i, stamina, max, cost, next)).ToList();

        [TestCase(10, 10, 10, false, 84f)]
        [TestCase(6, 10, 10, false, 84f)]
        [TestCase(11, 11, 11, true, 104f)]
        [TestCase(12, 14, 14, true, 104f)]
        [TestCase(20, 20, 20, true, 104f)]
        [TestCase(13, 10, 13, true, 104f)]
        public void APipPerPoint_TenToARow_AndATallerTagForASecondRow(int stamina, int max, int count, bool twoRows, float height)
        {
            Assert.That(StaminaPips.Count(stamina, max), Is.EqualTo(count));
            Assert.That(StaminaPips.TwoRows(stamina, max), Is.EqualTo(twoRows));
            Assert.That(StaminaPips.TagHeightFor(stamina, max), Is.EqualTo(height));
            Assert.That(StaminaPips.TagRaise(stamina, max), Is.EqualTo(height - StaminaPips.TagHeight), "the tag grows upward");
        }

        [Test]
        public void TheRows_AreTenAndTheRest()
        {
            Assert.That(Enumerable.Range(0, 20).Select(StaminaPips.RowOf),
                Is.EqualTo(Enumerable.Repeat(0, 10).Concat(Enumerable.Repeat(1, 10))));
            Assert.That(StaminaPips.Count(25, 25), Is.EqualTo(20), "the core stops the maximum at 20, two rows at most");
            Assert.That(StaminaPips.RowPitch, Is.EqualTo(20f), "16 px pips, 4 px between the rows");
        }

        [Test]
        public void TheShapes_RunOnFromTheFirstRowIntoTheSecond()
        {
            // §4.7: 最大 14 で今 12 なら、1 行目の 10 個と 2 行目の 2 個が塗りで、2 行目の残り 2 個が空きの輪.
            List<PipShape> shapes = Shapes(12, 14);
            Assert.That(shapes.Take(12), Is.All.EqualTo(PipShape.Have));
            Assert.That(shapes.Skip(12), Is.EqualTo(new[] { PipShape.Empty, PipShape.Empty }));
        }

        [Test]
        public void APaidCardAndTheNextRecovery_ReadAcrossTheRows()
        {
            // 最大 14, 今 11, a cost-3 card held: 8 filled, 3 rings to pay; with 3 back next turn after them.
            Assert.That(Shapes(11, 14, cost: 3), Is.EqualTo(
                Enumerable.Repeat(PipShape.Have, 8).Concat(Enumerable.Repeat(PipShape.Pay, 3)).Concat(Enumerable.Repeat(PipShape.Empty, 3))));
            Assert.That(Shapes(11, 14, next: 3), Is.EqualTo(
                Enumerable.Repeat(PipShape.Have, 11).Concat(Enumerable.Repeat(PipShape.Next, 3))));
        }

        [Test]
        public void StaminaAboveTheMaximum_TakesPipsOfItsOwn_PaidFirst()
        {
            // #508 (2026-11-03): 13 / 最大 10 shows the ten and three more in another colour, on the second row.
            Assert.That(Shapes(13, 10), Is.EqualTo(Enumerable.Repeat(PipShape.Have, 10).Concat(Enumerable.Repeat(PipShape.Over, 3))));
            Assert.That(Shapes(13, 10, cost: 2), Is.EqualTo(
                Enumerable.Repeat(PipShape.Have, 10).Concat(new[] { PipShape.Over, PipShape.Pay, PipShape.Pay })));
            Assert.That(Shapes(13, 10, cost: 5), Is.EqualTo(
                Enumerable.Repeat(PipShape.Have, 8).Concat(Enumerable.Repeat(PipShape.Pay, 5))));
        }

        [TestCase(3, 10, 10, true)]
        [TestCase(3, 14, 14, true)]
        [TestCase(3, 3, 3, false)]
        [TestCase(0, 10, 10, false)]
        [TestCase(10, 14, 14, false)]
        public void TheDivider_StandsOnTheFirstRowOnly(int after, int stamina, int max, bool shown)
        {
            Assert.That(StaminaPips.DividerShown(after, stamina, max), Is.EqualTo(shown));
        }
    }
}
