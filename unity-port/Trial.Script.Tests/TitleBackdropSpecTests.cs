using Backdrop;
using NUnit.Framework;

namespace Trial.Script.Tests
{
    /// <summary>
    /// Which layer's backdrop and art the meta screens show (#858): the title the deepest layer, the hall its own room,
    /// which is not a layer and so takes no layer's art.
    /// </summary>
    public class TitleBackdropSpecTests
    {
        [Test]
        public void TheTitle_ShowsTheDeepestLayer_AsTheJsDoes()
        {
            Assert.That(TitleBackdropSpec.Layer, Is.EqualTo(7));
            Assert.That(TitleBackdropSpec.For().Layer, Is.EqualTo(7));
        }

        [Test]
        public void TheTitle_TakesTheArtOfLayer7()
        {
            Assert.That(BackdropArt.ResourcePath(TitleBackdropSpec.For().Layer), Is.EqualTo("Backdrops/layer-7"));
        }

        [Test]
        public void TheHallsRoom_TakesNoLayersArt()
        {
            Assert.That(BackdropArt.HasSlot(HallBackdropSpec.For().Layer), Is.False);
            Assert.That(BackdropArt.ResourcePath(HallBackdropSpec.For(0.5).Layer), Is.Null);
        }
    }
}
