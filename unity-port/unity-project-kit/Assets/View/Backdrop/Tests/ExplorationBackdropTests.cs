// #90: the exploration screen's backdrop (battle-visual-v1 §2.5). It is the battle's place of the same
// layer under a veil, the map's text and the amber accent read on every layer, and the veil keeps
// world-v1 §3's rule 3 (4 the brightest, 3 darker than 1, 2 and 4). Since 2026-11-03 the veil is a dark shade of
// each layer's own hue, so the place keeps its colour under the map.
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
        public void TheDeepLayers_DarkenInTurn_UnderTheVeil()
        {
            // world-v1 §3 rule 3: from 5 down the light drains away, in exploration as in battle.
            double five = ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(5));
            double six = ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(6));
            double seven = ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(7));
            Assert.That(six, Is.LessThan(five));
            Assert.That(seven, Is.LessThan(six));
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
            // Without a veil, the text would not read on 1, 2 and 4: that is what the veil is for.
            foreach (int layer in new[] { 1, 2, 4 })
            {
                var bare = new ExplorationBackdrop { Layer = layer, Place = BackdropSpec.For(layer), Veil = ExplorationBackdropSpec.Ground.WithAlpha(0f) };
                Assert.That(ExplorationBackdropSpec.LowestContrast(bare, ExplorationBackdropSpec.Ink),
                    Is.LessThan(ExplorationBackdropSpec.InkMinContrast), "layer " + layer);
                Assert.That(ExplorationBackdropSpec.For(layer).Veil.A,
                    Is.GreaterThan(ExplorationBackdropSpec.For(3).Veil.A), "layer " + layer);
            }
        }

        [Test]
        public void Layer4_StaysTheBrightest_UnderTheVeil()
        {
            double four = ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(4));
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++)
            {
                if (layer == 4) continue;
                Assert.That(ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(layer)), Is.LessThan(four), "layer " + layer);
            }
        }

        [Test]
        public void Layer3_StaysDarkerThan1_2And4_UnderTheVeil()
        {
            double three = ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(3));
            Assert.That(three, Is.LessThan(ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(1))));
            Assert.That(three, Is.LessThan(ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(2))));
            Assert.That(three, Is.LessThan(ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(4))));
        }
    }
}
