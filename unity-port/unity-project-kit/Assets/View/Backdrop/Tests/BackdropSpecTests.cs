// #89: the seven layers' backdrops (battle-visual-v1 §2.4). The values §2.4 writes stay, every layer keeps
// the reach red readable on its floor tile (§2.3), and the brightness follows world-v1 §3's rule 3
// (1 and 2 bright, 3 dark, 4 the brightest, then darker down to 7). #609 made layer 1 bright; 2026-11-03
// (こうだいさん's request) gave every layer its own colour, layer 1 brighter still and layer 7 a starry sky.
using System.Collections.Generic;
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

        // battle-visual-v1 §2.4: each layer's sky, top (0) to bottom (1), and its floor tile.
        private static readonly Dictionary<int, (string Hex, float At)[]> SectionSky = new Dictionary<int, (string, float)[]>
        {
            [1] = new[] { ("#fafde0", 0f), ("#ebf8ac", 0.22f), ("#c4e878", 0.48f), ("#96ce56", 0.75f), ("#62a242", 1f) },
            [2] = new[] { ("#fbf8e2", 0f), ("#ebe5bb", 0.35f), ("#cfc999", 0.7f), ("#9a9268", 1f) },
            [3] = new[] { ("#0e0c28", 0f), ("#141e48", 0.3f), ("#18465e", 0.5f), ("#0f2a3e", 0.7f), ("#060c18", 1f) },
            [4] = new[] { ("#ffffff", 0f), ("#f1ecff", 0.25f), ("#dcd4f6", 0.5f), ("#c4e4ee", 0.7f), ("#8d9ccc", 1f) },
            [5] = new[] { ("#2e0d18", 0f), ("#6a1f2e", 0.4f), ("#8a3040", 0.62f), ("#2a0a12", 1f) },
            [6] = new[] { ("#04061c", 0f), ("#0c1450", 0.35f), ("#181a70", 0.5f), ("#0a0e3a", 0.7f), ("#020310", 1f) },
            [7] = new[] { ("#05061a", 0f), ("#0a0b26", 0.5f), ("#04040f", 1f) },
        };

        private static readonly Dictionary<int, (string Hex, float A)> SectionTile = new Dictionary<int, (string, float)>
        {
            [1] = ("#0a140c", 0.88f),
            [2] = ("#14120c", 0.88f),
            [3] = ("#0a101c", 0.8f),
            [4] = ("#0e101c", 0.84f),
            [5] = ("#14080c", 0.82f),
            [6] = ("#080814", 0.84f),
            [7] = ("#060610", 0.84f),
        };

        [Test]
        public void EveryLayer_KeepsTheValuesSection24Writes()
        {
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++)
            {
                LayerBackdrop backdrop = BackdropSpec.For(layer);
                (string Hex, float At)[] want = SectionSky[layer];
                Assert.That(backdrop.Sky, Has.Length.EqualTo(want.Length), "layer " + layer);
                for (int i = 0; i < want.Length; i++)
                {
                    AssertHex(backdrop.Sky[i].Color, want[i].Hex);
                    Assert.That(backdrop.Sky[i].At, Is.EqualTo(want[i].At).Within(1e-4f), "layer " + layer + " stop " + i);
                }
                AssertHex(backdrop.Tile, SectionTile[layer].Hex);
                Assert.That(backdrop.Tile.A, Is.EqualTo(SectionTile[layer].A).Within(1e-4f), "layer " + layer + " tile");
            }
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
            // §2.3 measured 3.0〜4.0 on layer 4 and 3.7 or more on the rest; layer 4 stays the tightest.
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
            Assert.That(l[1], Is.GreaterThan(l[3]), "1 is bright, above 3");
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
            foreach (SkyStop stop in four.Sky) AssertHex(BackdropSpec.SkyAt(four, stop.At), ToHex(stop.Color));
            SkyStop a = four.Sky[0], b = four.Sky[1];
            Rgba middle = BackdropSpec.SkyAt(four, (a.At + b.At) / 2f);
            Assert.That(middle.R, Is.EqualTo((a.Color.R + b.Color.R) / 2f).Within(1e-4f));
            Assert.That(middle.B, Is.EqualTo((a.Color.B + b.Color.B) / 2f).Within(1e-4f));
        }

        private static string ToHex(Rgba c)
        {
            static int Byte(float v) => (int)System.Math.Floor(v * 255.0 + 0.5);
            return "#" + Byte(c.R).ToString("x2") + Byte(c.G).ToString("x2") + Byte(c.B).ToString("x2");
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
