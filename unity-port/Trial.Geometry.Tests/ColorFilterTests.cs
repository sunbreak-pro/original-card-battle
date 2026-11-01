using NUnit.Framework;
using Trial.Geometry;

namespace Trial.Geometry.Tests
{
    public class ColorFilterTests
    {
        [Test]
        public void BrightnessScalesAndClamps()
        {
            var c = new Rgba(1f, 0.5f, 0.2f, 0.7f);
            Rgba d = new ColorFilter(0.5f).Apply(c);
            Assert.That((d.R, d.G, d.B, d.A), Is.EqualTo((0.5f, 0.25f, 0.1f, 0.7f)));
            Rgba b = new ColorFilter(2f).Apply(c);
            Assert.That(b.R, Is.EqualTo(1f));
            Assert.That(b.G, Is.EqualTo(1f));
            Assert.That(b.B, Is.EqualTo(0.4f).Within(1e-6f));
        }

        [Test]
        public void GrayscaleUsesRec709Weights()
        {
            Rgba g = new ColorFilter(1f, 1f).Apply(new Rgba(0f, 1f, 0f));
            Assert.That(g.R, Is.EqualTo(0.7152f).Within(1e-6f));
            Assert.That(g.G, Is.EqualTo(0.7152f).Within(1e-6f));
            Assert.That(g.B, Is.EqualTo(0.7152f).Within(1e-6f));
            // half: the spec's matrix row for G is .7152 + .2848·(1 − g)
            Rgba h = new ColorFilter(1f, 0.5f).Apply(new Rgba(0f, 1f, 0f));
            Assert.That(h.G, Is.EqualTo(0.7152f + 0.2848f * 0.5f).Within(1e-6f));
            Assert.That(h.R, Is.EqualTo(0.7152f - 0.7152f * 0.5f).Within(1e-6f));
        }

        [Test]
        public void BrightnessClampsBeforeGrayscale()
        {
            // a brightness flash (2) inside a greyed ancestor (grayscale(1)): the browser clamps the flash first
            Rgba g = new ColorFilter(1f, 1f).Then(new ColorFilter(2f)).Apply(new Rgba(1f, 0.5f, 0f));
            Assert.That(g.R, Is.EqualTo(0.2126f + 0.7152f).Within(1e-6f));
            Assert.That(g.B, Is.EqualTo(0.2126f + 0.7152f).Within(1e-6f));
            // hand.css:31 "grayscale(0.85) brightness(0.62)": linear, so the order does not matter
            var c = new Rgba(0.9f, 0.4f, 0.1f, 0.6f);
            ColorFilter list = ColorFilter.Parse("grayscale(0.85) brightness(0.62)");
            Rgba seq = new ColorFilter(0.62f).Apply(new ColorFilter(1f, 0.85f).Apply(c));
            Assert.That(list.Apply(c).Approximately(seq, 1e-6f), Is.True);
        }

        [Test]
        public void ThenComposesBothFilters()
        {
            var c = new Rgba(0.3f, 0.6f, 0.9f);
            var f1 = new ColorFilter(0.8f, 0.3f);
            var f2 = new ColorFilter(0.9f, 0.5f);
            Rgba seq = f1.Apply(f2.Apply(c));
            Rgba once = f1.Then(f2).Apply(c);
            Assert.That(once.Approximately(seq, 1e-5f), Is.True, seq + " vs " + once);
            Assert.That(ColorFilter.None.IsNone, Is.True);
        }

        [Test]
        public void ParseReadsTheCssFunctions()
        {
            ColorFilter f = ColorFilter.Parse("brightness(.6) grayscale(50%)");
            Assert.That(f.Brightness, Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(f.Grayscale, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(ColorFilter.Parse("none").IsNone, Is.True);
            Assert.That(ColorFilter.Parse(null).IsNone, Is.True);
            Assert.That(ColorFilter.Parse("grayscale(1)").Grayscale, Is.EqualTo(1f));
            // another function with a nested colour does not swallow what follows it
            ColorFilter d = ColorFilter.Parse("drop-shadow(0 0 8px rgba(111, 208, 176, .7)) brightness(.5)");
            Assert.That(d.Brightness, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(d.Grayscale, Is.EqualTo(0f));
        }
    }
}
