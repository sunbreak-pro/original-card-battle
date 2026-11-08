// #89: the seven layers' backdrops (battle-visual-v1 §2.4). The values §2.4 writes stay, every layer keeps
// the reach red readable on its floor tile (§2.3), and the brightness follows the layer order. #609 made layer 1
// bright; 2026-11-03 (こうだいさん's request) gave every layer its own colour, layer 1 brighter still, layer 3 a
// bright layer too (one more bright layer in the upper part) and layer 7 a starry sky. 2026-11-04 (こうだいさん, world-v1
// v5.2 §3 rule 3, #916): only layer 7 is dark; layer 4 is as bright as layer 1, and 5 and 6 sit a little below layer 3.
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
            [3] = new[] { ("#bafcf3", 0f), ("#86f2e4", 0.25f), ("#58d8c6", 0.5f), ("#32b09e", 0.75f), ("#1a8270", 1f) },
            [4] = new[] { ("#ffffff", 0f), ("#ece6fc", 0.25f), ("#d4cbf0", 0.5f), ("#b9d8e4", 0.7f), ("#8090c2", 1f) },
            [5] = new[] { ("#ba505f", 0f), ("#e9939d", 0.25f), ("#fde9e7", 0.5f), ("#f4bec3", 0.75f), ("#ca6b78", 1f) },
            [6] = new[] { ("#5470da", 0f), ("#94aef6", 0.25f), ("#ccd8ff", 0.5f), ("#acc2f8", 0.75f), ("#728cde", 1f) },
            [7] = new[] { ("#05061a", 0f), ("#0a0b26", 0.5f), ("#04040f", 1f) },
        };

        private static readonly Dictionary<int, (string Hex, float A)> SectionTile = new Dictionary<int, (string, float)>
        {
            [1] = ("#0a140c", 0.88f),
            [2] = ("#14120c", 0.88f),
            [3] = ("#081416", 0.88f),
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
        public void TheBrightness_FollowsTheLayerOrder()
        {
            double[] l = new double[BackdropSpec.Layers + 1];
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++) l[layer] = MeanLuminance(BackdropSpec.For(layer));

            Assert.That(l[4], Is.EqualTo(l[1]).Within(0.03), "4 is as bright as 1 (#916)");
            for (int layer = 1; layer < BackdropSpec.Layers; layer++)
            {
                Assert.That(l[layer], Is.GreaterThanOrEqualTo(0.40), layer + " is not a dark layer");
            }
            foreach (int layer in new[] { 5, 6 })
            {
                Assert.That(l[layer], Is.LessThan(l[3]), layer + " sits a little below layer 3 (#916)");
                Assert.That(l[layer], Is.InRange(0.45, 0.50), layer + " stays near layer 3, not dark (#916)");
            }
            Assert.That(l[6], Is.LessThan(l[5]), "6 a little below 5");
            for (int layer = 1; layer < BackdropSpec.Layers; layer++)
            {
                Assert.That(l[7], Is.LessThan(0.05), "7, the starry sky, is the only dark layer");
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
