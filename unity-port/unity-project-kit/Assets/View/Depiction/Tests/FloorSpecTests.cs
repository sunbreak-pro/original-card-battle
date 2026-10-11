// #242: the floor of battle-visual-v1 §4.2 on the 1920×1080 screen — the band x 300〜1620 split by the
// line's cells (220 px for 6, 165 for 8, 264 for 5) with 3 px free on each side, the figures in the
// middle of their cells (§4.3), and the empty cells the gap number spans.
using NUnit.Framework;

namespace Depiction.Tests
{
    public class FloorSpecTests
    {
        [TestCase(6, 220f)]
        [TestCase(8, 165f)]
        [TestCase(5, 264f)]
        public void TheBand_IsSplitEvenlyByTheCells(int cells, float width)
        {
            Assert.That(FloorSpec.CellWidth(cells), Is.EqualTo(width).Within(1e-3f));
            Assert.That(FloorSpec.TileWidth(cells), Is.EqualTo(width - 6f).Within(1e-3f));
        }

        [Test]
        public void TheCells_RunFromX300ToX1620()
        {
            Assert.That(FloorSpec.TileLeft(6, 1), Is.EqualTo(303f).Within(1e-3f));
            Assert.That(FloorSpec.TileLeft(6, 6) + FloorSpec.TileWidth(6), Is.EqualTo(1617f).Within(1e-3f));
        }

        [Test]
        public void AFigure_StandsInTheMiddleOfItsCells()
        {
            Assert.That(FloorSpec.FeetX(6, 2, 1), Is.EqualTo(630f).Within(1e-3f));
            Assert.That(FloorSpec.FeetX(6, 6, 1), Is.EqualTo(1510f).Within(1e-3f));
            Assert.That(FloorSpec.FeetX(6, 5, 2), Is.EqualTo(1400f).Within(1e-3f), "a large elite on cells 5 and 6");
        }

        [Test]
        public void TheGapNumber_SpansTheEmptyCells()
        {
            // Player on 2, enemy on 6: cells 3〜5 are empty, gap 3.
            FloorSpec.GapSpan(6, 2, 1, 6, out float from, out float to);
            Assert.That(from, Is.EqualTo(740f).Within(1e-3f));
            Assert.That(to, Is.EqualTo(1400f).Within(1e-3f));

            // Gap 0: both ends are the border the two cells share.
            FloorSpec.GapSpan(6, 2, 1, 3, out from, out to);
            Assert.That(from, Is.EqualTo(to).Within(1e-3f));
        }
    }
}
