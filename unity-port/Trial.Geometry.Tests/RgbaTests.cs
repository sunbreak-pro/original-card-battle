using NUnit.Framework;
using Trial.Geometry;

namespace Trial.Geometry.Tests
{
    public class RgbaTests
    {
        [Test]
        public void HexAndBytes()
        {
            Rgba a = Rgba.Hex(0xE8A84C);
            Assert.That((a.R8, a.G8, a.B8, a.A), Is.EqualTo((232, 168, 76, 1f)));
            Rgba b = Rgba.Bytes(139, 111, 208, 0.35f);
            Assert.That(b.R, Is.EqualTo(139f / 255f));
            Assert.That(b.A, Is.EqualTo(0.35f));
            Assert.That(Rgba.Hex(0x06090c, 0.5f), Is.EqualTo(Rgba.Bytes(6, 9, 12, 0.5f)));
            Assert.That(b.ToString(), Is.EqualTo("rgba(139, 111, 208, 0.35)"));
        }

        [Test]
        public void WithAlphaAndMulAlpha()
        {
            Rgba c = Rgba.Hex(0x102030, 0.8f);
            Assert.That(c.WithAlpha(0.25f).A, Is.EqualTo(0.25f));
            Assert.That(c.WithAlpha(0.25f).R, Is.EqualTo(c.R));
            Assert.That(c.MulAlpha(0.5f).A, Is.EqualTo(0.4f).Within(1e-6f));
        }

        [Test]
        public void LerpPremulKeepsTheHueOfAFadeToTransparent()
        {
            var red = new Rgba(1, 0, 0, 1);
            Rgba m = Rgba.LerpPremul(red, Rgba.Transparent, 0.5f);
            Assert.That(m.R, Is.EqualTo(1f).Within(1e-6f), "no darkening towards transparent black");
            Assert.That(m.A, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(Rgba.LerpPremul(red, Rgba.Transparent, 1f), Is.EqualTo(Rgba.Transparent));
            // the straight lerp would darken it
            Assert.That(Rgba.LerpStraight(red, Rgba.Transparent, 0.5f).R, Is.EqualTo(0.5f));
        }

        [Test]
        public void LerpPremulWeighsByAlpha()
        {
            var red = new Rgba(1, 0, 0, 1);
            var blue = new Rgba(0, 0, 1, 0.5f);
            Rgba m = Rgba.LerpPremul(red, blue, 0.5f);
            // premultiplied: (1,0,0,1) and (0,0,.5,.5) → (.5, 0, .25, .75) → straight (2/3, 0, 1/3, .75)
            Assert.That(m.R, Is.EqualTo(2f / 3f).Within(1e-6f));
            Assert.That(m.G, Is.EqualTo(0f));
            Assert.That(m.B, Is.EqualTo(1f / 3f).Within(1e-6f));
            Assert.That(m.A, Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(Rgba.LerpPremul(red, blue, 0f), Is.EqualTo(red));
            Assert.That(Rgba.LerpPremul(red, blue, 1f).Approximately(blue, 1e-6f), Is.True);
        }
    }
}
