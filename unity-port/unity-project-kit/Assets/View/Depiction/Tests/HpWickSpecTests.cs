// The wick stripe on the HP bar (Script/HpWickSpec.cs, battle-visual-v1 §4.5 HP の減る分, #455): the
// tile is 135° stripes 3 px wide and 3 px apart in the wick colour, the stripe covers the HP the
// script says the held card takes off, and a loss of 0 draws no stripe.
using NUnit.Framework;

namespace Depiction.Tests
{
    public class HpWickSpecTests
    {
        [Test]
        public void TheStripe_IsSection4_5s()
        {
            Assert.That(HpWickSpec.AngleDegrees, Is.EqualTo(135f));
            Assert.That(HpWickSpec.StripeWidth, Is.EqualTo(3f));
            Assert.That(HpWickSpec.Gap, Is.EqualTo(3f));
            Assert.That(HpWickSpec.ColorHex, Is.EqualTo("#ffd89a"));
        }

        [Test]
        public void TheTile_DrawsTheSpacingWithinTwoHundredthsOfAPixel()
        {
            Assert.That(HpWickSpec.TileSize, Is.EqualTo(17));
            Assert.That(HpWickSpec.TilePeriod, Is.EqualTo(HpWickSpec.Period).Within(0.02f));
        }

        [Test]
        public void TheStripes_RunUpToTheRight()
        {
            // 135° in CSS: constant along x − y with y upwards, so a step right and up stays on a stripe.
            int tile = HpWickSpec.TileSize;
            for (int x = 0; x < tile; x++)
            {
                for (int y = 0; y < tile; y++)
                {
                    Assert.That(HpWickSpec.Alpha(x + 1, y + 1), Is.EqualTo(HpWickSpec.Alpha(x, y)).Within(1e-5f), $"({x}, {y})");
                }
            }
        }

        [Test]
        public void ARowOfTheTile_CrossesTwoStripesAndIsHalfWick()
        {
            int tile = HpWickSpec.TileSize;
            int rises = 0;
            float sum = 0f;
            for (int x = 0; x < tile; x++)
            {
                float here = HpWickSpec.Alpha(x, 0);
                float before = HpWickSpec.Alpha(x - 1, 0);
                if (before < 0.5f && here >= 0.5f) rises++;
                sum += here;
            }
            Assert.That(rises, Is.EqualTo(HpWickSpec.PeriodsPerTile), "one stripe for each 6 px period");
            Assert.That(sum / tile, Is.EqualTo(0.5f).Within(0.06f), "3 px of wick for every 3 px of gap");
        }

        [Test]
        public void TheTile_RepeatsWithoutASeam()
        {
            int tile = HpWickSpec.TileSize;
            for (int y = 0; y < tile; y++)
            {
                Assert.That(HpWickSpec.Alpha(tile, y), Is.EqualTo(HpWickSpec.Alpha(0, y)).Within(1e-5f));
                Assert.That(HpWickSpec.Alpha(y, tile), Is.EqualTo(HpWickSpec.Alpha(y, 0)).Within(1e-5f));
            }
        }

        [Test]
        public void TheStripe_CoversTheHpTheCardTakesOff()
        {
            // 30 of 40 HP, 10 taken off: the bar is filled to 0.75 and the stripe runs back to 0.5.
            Assert.That(HpWickSpec.Span(30, 40, 10, out float from, out float to), Is.True);
            Assert.That(from, Is.EqualTo(0.5f));
            Assert.That(to, Is.EqualTo(0.75f));
        }

        [Test]
        public void ALossOfAllTheHp_RunsTheStripeToTheBarsLeftEnd()
        {
            Assert.That(HpWickSpec.Span(12, 40, 12, out float from, out float to), Is.True);
            Assert.That(from, Is.EqualTo(0f));
            Assert.That(to, Is.EqualTo(0.3f).Within(1e-6f));
        }

        [Test]
        public void ALossOfZero_DrawsNoStripe()
        {
            Assert.That(HpWickSpec.Span(30, 40, 0, out float from, out float to), Is.False);
            Assert.That(to - from, Is.EqualTo(0f));
            Assert.That(HpWickSpec.Span(30, 40, -2, out _, out _), Is.False, "nothing the script could write draws a stripe below 0");
            Assert.That(HpWickSpec.Span(0, 40, 5, out _, out _), Is.False, "an empty bar has nothing to stripe");
        }
    }
}
