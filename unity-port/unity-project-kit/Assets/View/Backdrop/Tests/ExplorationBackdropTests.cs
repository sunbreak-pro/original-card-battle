// #90: the exploration screen's backdrop (battle-visual-v1 §2.5). It is the battle's place of the same
// layer under a veil, the map's text and the amber accent read on every layer, and the veil keeps
// world-v1 §3's rule 3 (4 the brightest, 3 darker than 2 and 4).
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
        public void TheVeil_IsTheGroundColour()
        {
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++)
            {
                Rgba veil = ExplorationBackdropSpec.For(layer).Veil;
                Rgba ground = ExplorationBackdropSpec.Ground;
                Assert.That(new[] { veil.R, veil.G, veil.B }, Is.EqualTo(new[] { ground.R, ground.G, ground.B }), "layer " + layer);
                Assert.That(veil.A, Is.InRange(0.3f, 0.75f), "layer " + layer);
            }
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
            // Without a veil, the text would not read on 2 and 4: that is what the veil is for.
            foreach (int layer in new[] { 2, 4 })
            {
                var bare = new ExplorationBackdrop { Layer = layer, Place = BackdropSpec.For(layer), Veil = ExplorationBackdropSpec.Ground.WithAlpha(0f) };
                Assert.That(ExplorationBackdropSpec.LowestContrast(bare, ExplorationBackdropSpec.Ink),
                    Is.LessThan(ExplorationBackdropSpec.InkMinContrast), "layer " + layer);
                Assert.That(ExplorationBackdropSpec.For(layer).Veil.A,
                    Is.GreaterThan(ExplorationBackdropSpec.For(1).Veil.A), "layer " + layer);
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
        public void Layer3_StaysDarkerThan2And4_UnderTheVeil()
        {
            double three = ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(3));
            Assert.That(three, Is.LessThan(ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(2))));
            Assert.That(three, Is.LessThan(ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(4))));
        }
    }
}
