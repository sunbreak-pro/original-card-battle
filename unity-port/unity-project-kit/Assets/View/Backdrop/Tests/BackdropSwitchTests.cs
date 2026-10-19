// #607: the switching BackdropLayers draws. The first layer shows at once, the same layer again does
// nothing, a new layer fades in over 400 ms (battle_ui_ux_v2 §4.3), and a third layer mid-fade drops
// the one fading out.
using NUnit.Framework;

namespace Backdrop.Tests
{
    public class BackdropSwitchTests
    {
        [Test]
        public void TheFirstLayer_ShowsAtOnce()
        {
            var sw = new BackdropSwitch();
            Assert.That(sw.Shown, Is.EqualTo(0));
            Assert.That(sw.Show(2, 1000), Is.EqualTo(BackdropSwitch.Change.First));
            Assert.That(sw.Shown, Is.EqualTo(2));
            Assert.That(sw.Leaving, Is.EqualTo(0));
            Assert.That(sw.Tick(1000), Is.EqualTo(1f));
        }

        [Test]
        public void TheSameLayerAgain_ChangesNothing()
        {
            var sw = new BackdropSwitch();
            sw.Show(4, 0);
            Assert.That(sw.Show(4, 50), Is.EqualTo(BackdropSwitch.Change.None));
            Assert.That(sw.Leaving, Is.EqualTo(0));
        }

        [Test]
        public void ANewLayer_FadesInOver400Ms_ThenTheOldOneGoes()
        {
            var sw = new BackdropSwitch();
            sw.Show(1, 0);
            Assert.That(sw.Show(2, 1000), Is.EqualTo(BackdropSwitch.Change.Fade));
            Assert.That(sw.Shown, Is.EqualTo(2));
            Assert.That(sw.Leaving, Is.EqualTo(1));

            Assert.That(sw.Tick(1000), Is.EqualTo(0f));
            float half = sw.Tick(1000 + BackdropSpec.CrossfadeMs / 2.0);
            Assert.That(half, Is.InRange(0.01f, 0.99f));
            Assert.That(sw.Leaving, Is.EqualTo(1), "still fading");

            Assert.That(sw.Tick(1000 + BackdropSpec.CrossfadeMs), Is.EqualTo(1f));
            Assert.That(sw.Leaving, Is.EqualTo(0), "the old layer is done");
        }

        [Test]
        public void AThirdLayerMidFade_DropsTheOneFadingOut()
        {
            var sw = new BackdropSwitch();
            sw.Show(1, 0);
            sw.Show(2, 1000);
            sw.Tick(1100);
            Assert.That(sw.Show(3, 1200), Is.EqualTo(BackdropSwitch.Change.FadeDroppingOldest));
            Assert.That(sw.Shown, Is.EqualTo(3));
            Assert.That(sw.Leaving, Is.EqualTo(2), "the half-shown layer now fades out");
            Assert.That(sw.Tick(1200), Is.EqualTo(0f), "the fade starts over");
        }

        [TestCase(0, 1)]
        [TestCase(9, 7)]
        public void ALayerOutsideTheLair_IsHeldToTheNearestEnd(int asked, int shown)
        {
            var sw = new BackdropSwitch();
            sw.Show(asked, 0);
            Assert.That(sw.Shown, Is.EqualTo(shown));
            Assert.That(sw.Show(shown, 10), Is.EqualTo(BackdropSwitch.Change.None));
        }
    }
}
