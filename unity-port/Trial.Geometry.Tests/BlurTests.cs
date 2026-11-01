using System;
using NUnit.Framework;
using Trial.Geometry;

namespace Trial.Geometry.Tests
{
    public class BlurTests
    {
        // Reference values (Abramowitz & Stegun table 7.1 / scipy.special.erf)
        [TestCase(0.0, 0.0)]
        [TestCase(0.1, 0.1124629160182849)]
        [TestCase(0.25, 0.2763263901682369)]
        [TestCase(0.46875, 0.4926134732179379)]
        [TestCase(0.5, 0.5204998778130465)]
        [TestCase(1.0, 0.8427007929497149)]
        [TestCase(1.5, 0.9661051464753108)]
        [TestCase(2.0, 0.9953222650189527)]
        [TestCase(3.0, 0.9999779095030014)]
        [TestCase(-1.0, -0.8427007929497149)]
        [TestCase(-0.3, -0.3286267594591274)]
        public void ErfMatchesTheTable(double x, double expected)
        {
            Assert.That(Blur.ErfD(x), Is.EqualTo(expected).Within(1e-13));
            Assert.That(Blur.Erf((float)x), Is.EqualTo((float)expected).Within(1e-6f));
        }

        [TestCase(0.5, 0.4795001221869535)]
        [TestCase(2.0, 0.004677734981047266)]
        [TestCase(3.0, 2.209049699858544e-05)]
        [TestCase(5.0, 1.5374597944280349e-12)]
        [TestCase(-2.0, 1.9953222650189528)]
        public void ErfcKeepsItsTail(double x, double expected)
        {
            Assert.That(Blur.ErfcD(x), Is.EqualTo(expected).Within(Math.Abs(expected) * 1e-12));
        }

        [Test]
        public void EdgeAlphaIsHalfErfc()
        {
            Assert.That(Blur.EdgeAlpha(0f, 3f), Is.EqualTo(0.5f).Within(1e-7f), "on the edge: half the colour's alpha");
            Assert.That(Blur.EdgeAlpha(3f, 3f), Is.EqualTo(0.15865525f).Within(1e-6f));
            Assert.That(Blur.EdgeAlpha(-3f, 3f), Is.EqualTo(0.84134475f).Within(1e-6f));
            Assert.That(Blur.EdgeAlpha(1f, 0f), Is.EqualTo(0f));
            Assert.That(Blur.EdgeAlpha(-1f, 0f), Is.EqualTo(1f));
            Assert.That(Blur.EdgeAlpha(0f, 0f), Is.EqualTo(0.5f));
        }

        [Test]
        public void BoxShadowAlphaOnTheEdgeIsHalfTheColourAlpha()
        {
            // box-shadow: 0 0 18px 2px amber 38% → σ = 9; at the shadow shape's edge: .5 × .38
            float sigma = Blur.SigmaOfShadowBlur(18f);
            Assert.That(sigma, Is.EqualTo(9f));
            Assert.That(0.38f * Blur.EdgeAlpha(0f, sigma), Is.EqualTo(0.19f).Within(1e-6f));
            Assert.That(Blur.SigmaOfDropShadow(20f), Is.EqualTo(20f));
        }

        [Test]
        public void BandAlphaIsABlurredStroke()
        {
            Assert.That(Blur.BandAlpha(0f, 4f, 2f), Is.EqualTo(0.68268949f).Within(1e-6f));
            Assert.That(Blur.BandAlpha(2f, 4f, 2f), Is.EqualTo(0.47724987f).Within(1e-6f));
            Assert.That(Blur.BandAlpha(1f, 4f, 0f), Is.EqualTo(1f));
            Assert.That(Blur.BandAlpha(3f, 4f, 0f), Is.EqualTo(0f));
            // the integral across stays the width
            double sum = 0;
            for (double d = -20; d <= 20; d += 0.01) sum += Blur.BandAlpha((float)d, 4f, 2f) * 0.01;
            Assert.That(sum, Is.EqualTo(4.0).Within(1e-3));
            Assert.That(Blur.StrokeShadowProfile(4f, 2f, 0.5f)(0f), Is.EqualTo(0.5f * 0.68268949f).Within(1e-6f));
            Assert.That(Blur.StrokeShadowReach(4f, 2f), Is.EqualTo(8f));
            Assert.That(Blur.Phi(0f), Is.EqualTo(0.5f));
            Assert.That(Blur.Phi(1.959964f), Is.EqualTo(0.975f).Within(1e-6f));
        }
    }
}
