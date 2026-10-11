// The three piles at the bottom left (Script/PileLayout.cs, battle-visual-v1 §4.7, #455): where they
// stand, and how many sheets of thickness a count draws.
using NUnit.Framework;

namespace Depiction.Tests
{
    public class PileLayoutTests
    {
        [Test]
        public void ThePiles_StandFromX32_64By84_22Apart()
        {
            Assert.That(PileLayout.LeftOf(PileKind.Draw), Is.EqualTo(32f));
            Assert.That(PileLayout.LeftOf(PileKind.Discard), Is.EqualTo(118f));
            Assert.That(PileLayout.LeftOf(PileKind.Exile), Is.EqualTo(204f));
            Assert.That(PileLayout.Top, Is.EqualTo(948f));
            Assert.That(PileLayout.Width, Is.EqualTo(64f));
            Assert.That(PileLayout.Height, Is.EqualTo(84f));
        }

        [Test]
        public void TheThree_SpanTheStaminaTagsWidth()
        {
            Assert.That(PileLayout.SpanWidth, Is.EqualTo(236f), "§4.7: the stamina tag is 236 wide, the same as the three piles");
        }

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(6, 1)]
        [TestCase(7, 2)]
        [TestCase(11, 2)]
        [TestCase(12, 3)]
        [TestCase(40, 3)]
        public void APile_ThickensBySheets_AsItsCountGrows(int count, int behind)
        {
            Assert.That(PileLayout.SheetsBehind(count), Is.EqualTo(behind));
        }

        [Test]
        public void TheThickestPile_StaysClearOfTheStaminaTag()
        {
            // The tag: y 846, 84 tall, so it ends at 930.
            float top = PileLayout.Top - PileLayout.MaxSheetsBehind * PileLayout.SheetStep;
            Assert.That(top, Is.GreaterThan(846f + 84f));
        }

        [Test]
        public void AnEmptyPile_IsAnEmptySlot()
        {
            Assert.That(PileLayout.IsEmpty(0), Is.True);
            Assert.That(PileLayout.IsEmpty(1), Is.False);
        }

        [Test]
        public void TheCount_IsReadOffTheFrame()
        {
            var piles = new PileFrame { Shown = true, Draw = 14, Discard = 5, Exile = 1 };
            Assert.That(PileLayout.CountOf(piles, PileKind.Draw), Is.EqualTo(14));
            Assert.That(PileLayout.CountOf(piles, PileKind.Discard), Is.EqualTo(5));
            Assert.That(PileLayout.CountOf(piles, PileKind.Exile), Is.EqualTo(1));
            Assert.That(PileLayout.CountOf(null, PileKind.Draw), Is.EqualTo(0));
        }
    }
}
