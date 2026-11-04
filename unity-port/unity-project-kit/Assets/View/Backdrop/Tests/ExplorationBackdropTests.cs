// #90: the exploration screen's backdrop (battle-visual-v1 §2.5). It is the battle's place of the same
// layer under a veil, the map's text and the amber accent read on every layer, and the veil keeps the layer order
// (since 2026-11-04, #916: layer 7 the only dark layer). Since 2026-11-03 the veil is a dark shade of each layer's own
// hue, so the place keeps its colour under the map; layers 1 to 6 are bright and take the thick veil.
using NUnit.Framework;

namespace Backdrop.Tests
{
    public class ExplorationBackdropTests
    {
        [Test]
        public void EveryLayer_StandsInTheBattlesPlaceOfTheSameLayer()
        {
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++)
            {
                ExplorationBackdrop backdrop = ExplorationBackdropSpec.For(layer);
                Assert.That(backdrop.Layer, Is.EqualTo(layer));
                Assert.That(backdrop.Place, Is.SameAs(BackdropSpec.For(layer)), "layer " + layer);
            }
        }

        [TestCase(0, 1)]
        [TestCase(8, 7)]
        public void ALayerOutsideTheLair_IsHeldToTheNearestEnd(int asked, int drawn)
        {
            Assert.That(ExplorationBackdropSpec.For(asked).Layer, Is.EqualTo(drawn));
            Assert.That(ExplorationBackdropSpec.For(asked).Place.Layer, Is.EqualTo(drawn));
        }

        [Test]
        public void TheVeil_IsADarkShadeOfItsLayer()
        {
            double ground = BackdropSpec.Luminance(ExplorationBackdropSpec.Ground);
            var seen = new System.Collections.Generic.HashSet<(float, float, float)>();
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++)
            {
                Rgba veil = ExplorationBackdropSpec.For(layer).Veil;
                Assert.That(BackdropSpec.Luminance(veil.WithAlpha(1f)), Is.LessThanOrEqualTo(ground * 1.6), "layer " + layer + " is a dark shade");
                Assert.That(veil.A, Is.InRange(0.3f, 0.75f), "layer " + layer);
                Assert.That(seen.Add((veil.R, veil.G, veil.B)), Is.True, "layer " + layer + " has a hue of its own");
            }
        }

        [Test]
        public void TheStarrySky_StaysTheDarkest_UnderTheVeil()
        {
            // world-v1 §3 rule 3 (v5.2, #916): layer 7 is the only dark layer, in exploration as in battle. Measured on the
            // sky's mean, as §2.4 measures the battle: the peak of layer 7 is a lone star, not the place.
            double seven = VeiledSkyMean(ExplorationBackdropSpec.For(7));
            for (int layer = 1; layer < BackdropSpec.Layers; layer++)
            {
                Assert.That(VeiledSkyMean(ExplorationBackdropSpec.For(layer)), Is.GreaterThan(seven), "layer " + layer);
            }
        }

        private static double VeiledSkyMean(ExplorationBackdrop backdrop)
        {
            double sum = 0;
            for (int step = 0; step <= 20; step++)
            {
                sum += BackdropSpec.Luminance(BackdropSpec.Over(backdrop.Veil, BackdropSpec.SkyAt(backdrop.Place, step / 20f)));
            }
            return sum / 21;
        }

        [Test]
        public void TheBodyText_ReadsOnEveryLayer()
        {
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++)
            {
                double contrast = ExplorationBackdropSpec.LowestContrast(ExplorationBackdropSpec.For(layer), ExplorationBackdropSpec.Ink);
                Assert.That(contrast, Is.GreaterThanOrEqualTo(ExplorationBackdropSpec.InkMinContrast), "layer " + layer);
            }
        }

        [Test]
        public void TheAmberAccent_ReadsOnEveryLayer()
        {
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++)
            {
                double contrast = ExplorationBackdropSpec.LowestContrast(ExplorationBackdropSpec.For(layer), ExplorationBackdropSpec.Amber);
                Assert.That(contrast, Is.GreaterThanOrEqualTo(ExplorationBackdropSpec.AmberMinContrast), "layer " + layer);
            }
        }

        [Test]
        public void TheBrightLayers_NeedTheThickVeil()
        {
            // Without a veil, the text would not read on the bright layers 1 to 6: that is what the veil is for.
            foreach (int layer in new[] { 1, 2, 3, 4, 5, 6 })
            {
                var bare = new ExplorationBackdrop { Layer = layer, Place = BackdropSpec.For(layer), Veil = ExplorationBackdropSpec.Ground.WithAlpha(0f) };
                Assert.That(ExplorationBackdropSpec.LowestContrast(bare, ExplorationBackdropSpec.Ink),
                    Is.LessThan(ExplorationBackdropSpec.InkMinContrast), "layer " + layer);
                Assert.That(ExplorationBackdropSpec.For(layer).Veil.A,
                    Is.GreaterThan(ExplorationBackdropSpec.For(7).Veil.A), "layer " + layer);
            }
        }

    }
}
