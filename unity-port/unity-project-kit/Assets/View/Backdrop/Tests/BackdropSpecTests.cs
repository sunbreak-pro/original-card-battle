// #89: the seven layers' backdrops (battle-visual-v1 §2.4). The values §2.4 fixed before #89 stay, every
// layer keeps the reach red readable on its floor tile (§2.3), and the brightness follows world-v1 §3's
// rule 3 (1 and 2 bright, 3 dark, 4 the brightest, then darker down to 7).
using NUnit.Framework;

namespace Backdrop.Tests
{
    public class BackdropSpecTests
    {
        [Test]
        public void EveryLayer_HasItsOwnBackdrop()
        {
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++)
            {
                LayerBackdrop backdrop = BackdropSpec.For(layer);
                Assert.That(backdrop.Layer, Is.EqualTo(layer));
                Assert.That(backdrop.Sky, Has.Length.GreaterThanOrEqualTo(2), "layer " + layer);
                Assert.That(backdrop.Sky[0].At, Is.EqualTo(0f));
                Assert.That(backdrop.Sky[backdrop.Sky.Length - 1].At, Is.EqualTo(1f));
                Assert.That(backdrop.Hazes, Is.Not.Empty, "layer " + layer);
            }
        }

        [TestCase(0, 1)]
        [TestCase(-3, 1)]
        [TestCase(8, 7)]
        [TestCase(99, 7)]
        public void ALayerOutsideTheLair_IsHeldToTheNearestEnd(int asked, int drawn)
        {
            Assert.That(BackdropSpec.For(asked).Layer, Is.EqualTo(drawn));
        }

        [Test]
        public void Layers1_4_5_KeepTheValuesFixedBefore89()
        {
            AssertHex(BackdropSpec.For(1).Sky[0].Color, "#1d3340");
            AssertHex(BackdropSpec.For(1).Sky[1].Color, "#0f1a22");
            AssertHex(BackdropSpec.For(4).Sky[0].Color, "#e4e6f2");
            AssertHex(BackdropSpec.For(4).Sky[1].Color, "#b7bbd6");
            AssertHex(BackdropSpec.For(4).Sky[2].Color, "#7d82a8");
            AssertHex(BackdropSpec.For(5).Sky[0].Color, "#1a0e12");
            AssertHex(BackdropSpec.For(5).Sky[1].Color, "#070507");
            Assert.That(BackdropSpec.For(1).Tile.A, Is.EqualTo(0.72f));
            Assert.That(BackdropSpec.For(4).Tile.A, Is.EqualTo(0.82f));
            Assert.That(BackdropSpec.For(5).Tile.A, Is.EqualTo(0.80f));
        }

        [Test]
        public void TheReachRed_KeepsAContrastOf3OnEveryLayersFloor()
        {
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++)
            {
                double contrast = BackdropSpec.ReachRedContrast(BackdropSpec.For(layer));
                Assert.That(contrast, Is.GreaterThanOrEqualTo(BackdropSpec.ReachRedMinContrast), "layer " + layer);
            }
        }

        [Test]
        public void Layer4_IsTheLowestForTheRed_AsSection23Measured()
        {
            // §2.3 measured 3.0〜4.0 on layer 4 and 4.4 or more on 1 and 5; layer 4 stays the tightest.
            double four = BackdropSpec.ReachRedContrast(BackdropSpec.For(4));
            Assert.That(four, Is.LessThan(4.0));
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++)
            {
                if (layer == 4) continue;
                Assert.That(BackdropSpec.ReachRedContrast(BackdropSpec.For(layer)), Is.GreaterThan(four), "layer " + layer);
            }
        }

        [Test]
        public void TheBrightness_FollowsWorldRule3()
        {
            double[] l = new double[BackdropSpec.Layers + 1];
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++) l[layer] = MeanLuminance(BackdropSpec.For(layer));

            for (int layer = 1; layer <= BackdropSpec.Layers; layer++)
            {
                if (layer != 4) Assert.That(l[4], Is.GreaterThan(l[layer]), "4 is the brightest, above " + layer);
            }
            Assert.That(l[2], Is.GreaterThan(l[3]), "3 drops after 2");
            Assert.That(l[5], Is.LessThan(l[4]));
            Assert.That(l[6], Is.LessThan(l[5]), "5 onward goes darker");
            Assert.That(l[7], Is.LessThan(l[6]), "7 is the darkest");
            for (int layer = 1; layer < BackdropSpec.Layers; layer++)
            {
                Assert.That(l[7], Is.LessThan(l[layer]), "7 is darker than " + layer);
            }
        }

        [Test]
        public void TheSky_RunsStraightBetweenItsStops()
        {
            LayerBackdrop four = BackdropSpec.For(4);
            AssertHex(BackdropSpec.SkyAt(four, 0f), "#e4e6f2");
            AssertHex(BackdropSpec.SkyAt(four, 0.5f), "#b7bbd6");
            AssertHex(BackdropSpec.SkyAt(four, 1f), "#7d82a8");
            Rgba quarter = BackdropSpec.SkyAt(four, 0.25f);
            Assert.That(quarter.R, Is.EqualTo((0xe4 + 0xb7) / 2f / 255f).Within(1e-4f));
        }

        [Test]
        public void AHaze_LaysItsAlphaOnlyWhereItReaches()
        {
            var top = new Haze { Shape = HazeShape.TopGlow, Color = new Rgba(1f, 1f, 1f, 0.6f), Size = 0.5f };
            Assert.That(BackdropSpec.HazeAlphaAt(top, 0f), Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(BackdropSpec.HazeAlphaAt(top, 0.25f), Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(BackdropSpec.HazeAlphaAt(top, 0.6f), Is.EqualTo(0f));

            var band = new Haze { Shape = HazeShape.Band, Color = new Rgba(1f, 1f, 1f, 0.1f), Y = 0.5f, Size = 0.2f };
            Assert.That(BackdropSpec.HazeAlphaAt(band, 0.5f), Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(BackdropSpec.HazeAlphaAt(band, 0.65f), Is.EqualTo(0f));
        }

        [Test]
        public void TheContrast_IsWcag2s()
        {
            Assert.That(BackdropSpec.Contrast(new Rgba(1f, 1f, 1f), new Rgba(0f, 0f, 0f)), Is.EqualTo(21.0).Within(1e-9));
            Assert.That(BackdropSpec.Contrast(new Rgba(0.5f, 0.5f, 0.5f), new Rgba(0.5f, 0.5f, 0.5f)), Is.EqualTo(1.0).Within(1e-9));
        }

        private static double MeanLuminance(LayerBackdrop backdrop)
        {
            double sum = 0;
            for (int step = 0; step <= 20; step++) sum += BackdropSpec.Luminance(BackdropSpec.SkyAt(backdrop, step / 20f));
            return sum / 21;
        }

        private static void AssertHex(Rgba color, string hex)
        {
            Rgba want = Rgba.Hex(hex);
            Assert.That(color.R, Is.EqualTo(want.R).Within(1e-4f), hex);
            Assert.That(color.G, Is.EqualTo(want.G).Within(1e-4f), hex);
            Assert.That(color.B, Is.EqualTo(want.B).Within(1e-4f), hex);
        }
    }
}
