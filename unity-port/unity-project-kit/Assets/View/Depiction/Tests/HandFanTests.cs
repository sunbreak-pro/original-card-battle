using System;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class HandFanTests
    {
        // The values the BattleDepiction scene ships with (DepictionPlayer and the Card prefab).
        private const float Spacing = 204f;
        private const float DegreesPerCard = 2.5f;
        private const float DropPixels = 4f;
        private const float CardWidth = 190f;
        private const float CardHeight = 260f;
        private const float Tolerance = 1e-4f;

        /// <summary>Compares card centres (pivots), not their top edges: a steep fan's outer top corner may stand higher.</summary>
        [Test]
        public void TheMiddleCardRestsHighest([Values(3, 5, 7)] int count, [Values(2.5f, 8f, 20f)] float degreesPerCard)
        {
            // Each step away from the middle rests lower than the one before it, however steep the tilt.
            int middle = count / 2;
            for (int i = middle; i < count - 1; i++)
            {
                FanPlace inner = HandFan.Place(count, i, Spacing, degreesPerCard, DropPixels, CardWidth);
                FanPlace outer = HandFan.Place(count, i + 1, Spacing, degreesPerCard, DropPixels, CardWidth);
                Assert.That(outer.Y, Is.LessThan(inner.Y), "card " + (i + 1) + " of " + count);
            }
        }

        [Test]
        public void TheFanIsMirrorSymmetric([Values(2, 3, 4, 5, 6)] int count)
        {
            for (int i = 0; i < count; i++)
            {
                FanPlace left = Place(count, i);
                FanPlace right = Place(count, count - 1 - i);
                Assert.That(left.X, Is.EqualTo(-right.X).Within(Tolerance), "x of card " + i + " of " + count);
                Assert.That(left.Y, Is.EqualTo(right.Y).Within(Tolerance), "y of card " + i + " of " + count);
                Assert.That(left.Degrees, Is.EqualTo(-right.Degrees).Within(Tolerance), "tilt of card " + i + " of " + count);
            }
        }

        [Test]
        public void NoLowerCornerDipsBelowTheBaseLine([Values(1, 2, 3, 5, 7)] int count, [Values(2.5f, 8f, 20f)] float degreesPerCard)
        {
            // The base line is where an upright card resting at y = 0 has its bottom edge.
            float baseLine = -CardHeight * 0.5f;
            for (int i = 0; i < count; i++)
            {
                FanPlace place = HandFan.Place(count, i, Spacing, degreesPerCard, DropPixels, CardWidth);
                Assert.That(LowestCornerY(place), Is.GreaterThanOrEqualTo(baseLine - Tolerance), "card " + i + " of " + count);
            }

            // ...and the outermost card sits on it, not somewhere above: only its own cos(tilt) lifts it.
            float edgeTilt = (count - 1) * 0.5f * degreesPerCard;
            FanPlace outermost = HandFan.Place(count, 0, Spacing, degreesPerCard, DropPixels, CardWidth);
            float expected = (float)(-CardHeight * 0.5 * Math.Cos(edgeTilt * Math.PI / 180.0));
            Assert.That(LowestCornerY(outermost), Is.EqualTo(expected).Within(1e-3f), "outermost of " + count);
        }

        [Test]
        public void OneCardRestsUprightInTheMiddle()
        {
            FanPlace only = Place(1, 0);

            Assert.That(only.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(only.Y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(only.Degrees, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void TwoCardsLeanAwayFromEachOtherAtTheSameHeight()
        {
            FanPlace left = Place(2, 0);
            FanPlace right = Place(2, 1);

            Assert.That(left.X, Is.EqualTo(-Spacing * 0.5f).Within(Tolerance));
            Assert.That(right.X, Is.EqualTo(Spacing * 0.5f).Within(Tolerance));
            Assert.That(left.Degrees, Is.EqualTo(DegreesPerCard * 0.5f).Within(Tolerance));
            Assert.That(right.Degrees, Is.EqualTo(-DegreesPerCard * 0.5f).Within(Tolerance));
            Assert.That(left.Y, Is.EqualTo(right.Y).Within(Tolerance));
            Assert.That(left.Y, Is.GreaterThan(0f), "a tilted pair is raised by its corner dip");
        }

        [TestCase(1, 160f)]
        [TestCase(5, 160f)]
        [TestCase(6, 150f)]
        [TestCase(7, 140f)]
        [TestCase(8, 126f)]
        public void TheSpacing_FollowsTheVisualTable(int count, float expected)
        {
            // battle-visual-v1.md §4.8, with the 160 px the inspector ships for five cards or fewer.
            Assert.That(HandFan.Spacing(count, 160f), Is.EqualTo(expected).Within(1e-3f));
        }

        [Test]
        public void TheSpacing_NeverWidens_AsTheHandGrows()
        {
            for (int count = 1; count < HandFan.MostCards; count++)
            {
                Assert.That(HandFan.Spacing(count + 1, Spacing), Is.LessThanOrEqualTo(HandFan.Spacing(count, Spacing)), count + " to " + (count + 1));
            }
        }

        [Test]
        public void EightCards_FitTheHandBand()
        {
            // §4.8: fan width = (count - 1) x spacing + the 216 px card; 1098 px inside the 1100 px band.
            float width = (HandFan.MostCards - 1) * HandFan.Spacing(HandFan.MostCards, 160f) + 216f;
            Assert.That(width, Is.EqualTo(1098f).Within(1e-2f));
            Assert.That(width, Is.LessThanOrEqualTo(1100f));
        }

        [Test]
        public void TheSpacing_ScalesWithTheInspectorValue()
        {
            Assert.That(HandFan.Spacing(6, 204f), Is.EqualTo(204f * 150f / 160f).Within(1e-3f));
            Assert.That(HandFan.Spacing(3, 204f), Is.EqualTo(204f));
        }

        private static FanPlace Place(int count, int index)
        {
            return HandFan.Place(count, index, Spacing, DegreesPerCard, DropPixels, CardWidth);
        }

        /// <summary>Lowest of the four corners of a centre-pivoted card at <paramref name="place"/>.</summary>
        private static float LowestCornerY(FanPlace place)
        {
            double radians = place.Degrees * Math.PI / 180.0;
            double lowest = double.MaxValue;
            foreach (float cx in new[] { -CardWidth * 0.5f, CardWidth * 0.5f })
            {
                foreach (float cy in new[] { -CardHeight * 0.5f, CardHeight * 0.5f })
                {
                    lowest = Math.Min(lowest, place.Y + cx * Math.Sin(radians) + cy * Math.Cos(radians));
                }
            }
            return (float)lowest;
        }
    }
}
