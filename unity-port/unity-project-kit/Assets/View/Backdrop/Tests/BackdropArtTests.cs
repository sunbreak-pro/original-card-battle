// #858: where a layer's painted backdrop is looked for, when the code-drawn far view gives way to it, and how the
// picture is cropped to fill a screen without stretching.
using NUnit.Framework;

namespace Backdrop.Tests
{
    public class BackdropArtTests
    {
        [Test]
        public void EveryLayer_LooksForItsArt_UnderBackdrops()
        {
            for (int layer = 1; layer <= BackdropSpec.Layers; layer++)
            {
                Assert.That(BackdropArt.HasSlot(layer), Is.True, "layer " + layer);
                Assert.That(BackdropArt.ResourcePath(layer), Is.EqualTo("Backdrops/layer-" + layer), "layer " + layer);
            }
        }

        [Test]
        public void ARoomOrAWrongLayer_HasNoArt_RatherThanAnotherLayers()
        {
            foreach (int layer in new[] { 0, -1, BackdropSpec.Layers + 1, 99 })
            {
                Assert.That(BackdropArt.HasSlot(layer), Is.False, "layer " + layer);
                Assert.That(BackdropArt.ResourcePath(layer), Is.Null, "layer " + layer);
            }
        }

        [Test]
        public void TheFarView_IsDrawn_OnlyWithoutArt()
        {
            Assert.That(BackdropArt.DrawsFarView(false), Is.True);
            Assert.That(BackdropArt.DrawsFarView(true), Is.False);
        }

        [Test]
        public void AStagePicture_FillsTheStageWhole()
        {
            AssertCrop(BackdropArt.Cover(1920f, 1080f, 1920f, 1080f), 0f, 0f, 1f, 1f);
            AssertCrop(BackdropArt.Cover(3840f, 2160f, 1920f, 1080f), 0f, 0f, 1f, 1f);
        }

        [Test]
        public void AWiderPicture_LosesItsSides_Evenly()
        {
            // 4:1 into 16:9: 16/9 ÷ 4 of the width shows, centred
            float w = (1920f / 1080f) / 4f;
            AssertCrop(BackdropArt.Cover(4000f, 1000f, 1920f, 1080f), (1f - w) / 2f, 0f, w, 1f);
        }

        [Test]
        public void ATallerPicture_LosesItsTopAndBottom_Evenly()
        {
            // a square into 16:9: 9/16 of the height shows, centred
            AssertCrop(BackdropArt.Cover(1000f, 1000f, 1920f, 1080f), 0f, (1f - 0.5625f) / 2f, 1f, 0.5625f);
        }

        [Test]
        public void TheSkyStrip_ShowsTheMiddleBandOfAStagePicture()
        {
            // a 600 × 120 strip (5:1) shows the full width and 16:9 ÷ 5 of the height
            float h = (1920f / 1080f) / 5f;
            AssertCrop(BackdropArt.Cover(1920f, 1080f, 600f, 120f), 0f, (1f - h) / 2f, 1f, h);
        }

        [Test]
        public void ASizeThatIsNotPositive_ShowsTheWholePicture()
        {
            AssertCrop(BackdropArt.Cover(0f, 1080f, 1920f, 1080f), 0f, 0f, 1f, 1f);
            AssertCrop(BackdropArt.Cover(1920f, 1080f, 0f, 0f), 0f, 0f, 1f, 1f);
            AssertCrop(BackdropArt.Cover(float.NaN, 1080f, 1920f, 1080f), 0f, 0f, 1f, 1f);
        }

        private static void AssertCrop(ArtCrop c, float x, float y, float w, float h)
        {
            Assert.That(new[] { c.X, c.Y, c.W, c.H }, Is.EqualTo(new[] { x, y, w, h }).Within(1e-6f));
        }
    }
}
