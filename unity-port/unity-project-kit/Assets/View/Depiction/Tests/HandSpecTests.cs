// #242: the hand of battle-visual-v1 §4.8 on the 1920×1080 screen — the middle card's top at y 896,
// the k-th from the middle k² × 3.2 px lower, the tilt about the middle of the bottom edge, and the
// hover that lifts a card upright until its top is at y 760. Six cards a turn (#351) is the case to hold.
using System;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class HandSpecTests
    {
        private const float Spacing = 160f;
        private const float Degrees = 2.5f;

        private static float Top(FanPlace place)
        {
            // Back from the centre to the top edge's middle, undoing the tilt (screen y grows downward).
            double radians = place.Degrees * Math.PI / 180.0;
            return place.Y - HandSpec.CardHeight * 0.5f * (float)Math.Cos(radians);
        }

        [Test]
        public void TheMiddleCard_ShowsItsUpper184Px()
        {
            FanPlace middle = HandSpec.Rest(5, 2, Spacing, Degrees);

            Assert.That(middle.X, Is.EqualTo(960f).Within(1e-3f));
            Assert.That(middle.Degrees, Is.EqualTo(0f));
            Assert.That(Top(middle), Is.EqualTo(896f).Within(1e-3f));
            Assert.That(1080f - Top(middle), Is.EqualTo(184f).Within(1e-3f));
        }

        [Test]
        public void SixCards_SitAt150Apart_AroundTheCentre_AndFitTheHandBand()
        {
            float spacing = HandFan.Spacing(6, Spacing);
            FanPlace first = HandSpec.Rest(6, 0, spacing, Degrees);
            FanPlace last = HandSpec.Rest(6, 5, spacing, Degrees);

            Assert.That(spacing, Is.EqualTo(150f).Within(1e-3f));
            Assert.That((first.X + last.X) * 0.5f, Is.EqualTo(960f).Within(1e-3f), "centred on x 960");
            Assert.That(first.Degrees, Is.EqualTo(6.25f).Within(1e-3f), "2.5 steps out, leaning left");
            Assert.That(last.Degrees, Is.EqualTo(-6.25f).Within(1e-3f));
            Assert.That(HandSpec.Width(6, spacing), Is.EqualTo(966f).Within(1e-3f));
            Assert.That(HandSpec.Width(8, HandFan.Spacing(8, Spacing)), Is.LessThanOrEqualTo(1100f), "§4.8: eight still fit the 1100 px band");
        }

        [Test]
        public void TheKthCard_SitsKSquaredTimes3Point2Lower_TiltedAboutItsBottomEdge()
        {
            float spacing = HandFan.Spacing(6, Spacing);
            for (int i = 0; i < 6; i++)
            {
                FanPlace place = HandSpec.Rest(6, i, spacing, Degrees);
                float step = i - 2.5f;
                double radians = place.Degrees * Math.PI / 180.0;
                // The middle of the bottom edge stays where the untilted card would put it.
                float bottomX = place.X + HandSpec.CardHeight * 0.5f * (float)Math.Sin(radians);
                float bottomY = place.Y + HandSpec.CardHeight * 0.5f * (float)Math.Cos(radians);
                Assert.That(bottomX, Is.EqualTo(960f + step * spacing).Within(1e-3f), "card " + i);
                Assert.That(bottomY - HandSpec.CardHeight, Is.EqualTo(896f + step * step * 3.2f).Within(1e-3f), "card " + i);
            }
        }

        [Test]
        public void AHoveredCard_StandsUpright_WithItsTopAtY760_AndItsBottom16PxAboveTheEdge()
        {
            float spacing = HandFan.Spacing(6, Spacing);
            FanPlace rest = HandSpec.Rest(6, 1, spacing, Degrees);
            FanPlace hover = HandSpec.Hover(6, 1, spacing);

            Assert.That(hover.Degrees, Is.EqualTo(0f));
            Assert.That(hover.Y - HandSpec.CardHeight * 0.5f, Is.EqualTo(760f).Within(1e-3f));
            Assert.That(1080f - (hover.Y + HandSpec.CardHeight * 0.5f), Is.EqualTo(16f).Within(1e-3f));
            Assert.That(hover.X, Is.EqualTo(960f - 1.5f * spacing).Within(1e-3f), "it rises above its own place");
            Assert.That(rest.Degrees, Is.Not.EqualTo(0f));
        }
    }
}
