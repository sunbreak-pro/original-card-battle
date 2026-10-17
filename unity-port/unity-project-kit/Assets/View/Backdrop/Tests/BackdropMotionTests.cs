// #89: the backdrop's motion — a light's breath, a flash, the drifting specks and the 400 ms crossfade
// between layers (battle_ui_ux_v2 §4.3). The same moment always gives the same picture.
using NUnit.Framework;

namespace Backdrop.Tests
{
    public class BackdropMotionTests
    {
        [Test]
        public void AStillLight_StaysFull()
        {
            Assert.That(BackdropMotion.FlickerAlpha(Flicker.Still, 1234), Is.EqualTo(1f));
        }

        [Test]
        public void ABreath_DipsByItsDepthHalfwayAndComesBack()
        {
            var breath = new Flicker(4000, 0.4f);
            Assert.That(BackdropMotion.FlickerAlpha(breath, 0), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(BackdropMotion.FlickerAlpha(breath, 2000), Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(BackdropMotion.FlickerAlpha(breath, 4000), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(BackdropMotion.FlickerAlpha(breath, 1000), Is.InRange(0.6f, 1f));
        }

        [Test]
        public void AFlash_IsLitOnlyAtTheStartOfItsPeriod()
        {
            var lightning = new Flicker(5200, 1f, flash: true);
            Assert.That(BackdropMotion.FlickerAlpha(lightning, 0), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(BackdropMotion.FlickerAlpha(lightning, BackdropMotion.FlashMs / 2.0), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(BackdropMotion.FlickerAlpha(lightning, 1000), Is.EqualTo(0f));
            Assert.That(BackdropMotion.FlickerAlpha(lightning, 5200), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void TheCrossfade_Takes400msAndLandsSoftly()
        {
            Assert.That(BackdropMotion.Crossfade(-10), Is.EqualTo(0f));
            Assert.That(BackdropMotion.Crossfade(0), Is.EqualTo(0f));
            Assert.That(BackdropMotion.Crossfade(200), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(BackdropMotion.Crossfade(BackdropSpec.CrossfadeMs), Is.EqualTo(1f));
            Assert.That(BackdropMotion.Crossfade(390), Is.LessThan(1f));
            Assert.That(BackdropMotion.Crossfade(390) - BackdropMotion.Crossfade(380), Is.LessThan(BackdropMotion.Crossfade(210) - BackdropMotion.Crossfade(200)));
        }

        [Test]
        public void ASpeck_StaysOnTheScreenAndMovesTheSameWayEveryRun()
        {
            Motes motes = BackdropSpec.For(7).Motes;
            for (int i = 0; i < motes.Count; i++)
            {
                for (int s = 0; s < 120; s += 7)
                {
                    BackdropMotion.MoteAt(motes, i, s * 1000.0, 1920f, 1080f, out float x, out float y);
                    Assert.That(x, Is.InRange(0f, 1920f));
                    Assert.That(y, Is.InRange(0f, 1080f));
                    BackdropMotion.MoteAt(motes, i, s * 1000.0, 1920f, 1080f, out float x2, out float y2);
                    Assert.That(x2, Is.EqualTo(x));
                    Assert.That(y2, Is.EqualTo(y));
                }
            }
        }

        [Test]
        public void TheMiasmaOfLayer7_Rises()
        {
            Motes motes = BackdropSpec.For(7).Motes;
            BackdropMotion.MoteAt(motes, 0, 0, 1920f, 100000f, out _, out float before);
            BackdropMotion.MoteAt(motes, 0, 2000, 1920f, 100000f, out _, out float after);
            Assert.That(after, Is.LessThan(before));
        }

        [Test]
        public void TheSpecks_StartSpreadAcrossTheScreen()
        {
            Motes motes = BackdropSpec.For(1).Motes;
            int left = 0;
            for (int i = 0; i < motes.Count; i++)
            {
                BackdropMotion.MoteAt(motes, i, 0, 1920f, 1080f, out float x, out _);
                if (x < 960f) left++;
            }
            Assert.That(left, Is.InRange(motes.Count / 4, motes.Count * 3 / 4));
        }

        [Test]
        public void ATwinkle_DipsNoFurtherThanItsDepth()
        {
            Motes motes = BackdropSpec.For(4).Motes;
            for (int ms = 0; ms < 10000; ms += 137)
            {
                Assert.That(BackdropMotion.MoteAlpha(motes, 3, ms), Is.InRange(1f - motes.Twinkle - 1e-5f, 1f + 1e-5f));
            }
        }
    }
}
