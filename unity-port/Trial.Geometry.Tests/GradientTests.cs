using System;
using System.Linq;
using NUnit.Framework;

namespace Trial.Geometry.Tests
{
    public class GradientTests
    {
        private static void Near(Rgba a, Rgba e, float eps = 1f / 512f) =>
            Assert.That(a.Approximately(e, eps), Is.True, "got " + a + ", expected " + e);

        [Test]
        public void LineLength_At135_IsWSinPlusHCos()
        {
            Assert.That(LinearGradient.LineLength(135f, 200f, 100f), Is.EqualTo(200f * Math.Sin(Math.PI * 0.75) + 100f * Math.Abs(Math.Cos(Math.PI * 0.75))).Within(1e-3));
            Assert.That(LinearGradient.LineLength(135f, 200f, 100f), Is.EqualTo(212.132f).Within(1e-3f));
            Assert.That(LinearGradient.LineLength(180f, 200f, 100f), Is.EqualTo(100f).Within(1e-4f));
            Assert.That(LinearGradient.LineLength(90f, 200f, 100f), Is.EqualTo(200f).Within(1e-4f));
        }

        [Test]
        public void Css135_PutsTheCornersOnZeroAndOne()
        {
            var box = new RectF(10f, 20f, 200f, 100f);
            LinearGradient g = LinearGradient.Css(135f, box, GradientStop.At(Rgba.Black, 0f), GradientStop.At(Rgba.White, 1f));
            Assert.That(g.TAt(box.TopLeft), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(g.TAt(box.BottomRight), Is.EqualTo(1f).Within(1e-5f));
            // a fixed angle (unlike "to bottom right") does not put the other corners on the 50 % line on a
            // non-square box; they sit symmetrically about it
            Assert.That(g.TAt(box.TopRight) + g.TAt(box.BottomLeft), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(g.TAt(box.Center), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(g.Start.X, Is.EqualTo(10f + 100f - 0.70710678f * 106.066f).Within(1e-2f));
        }

        [Test]
        public void DefaultIsTopToBottom_WithPremultipliedInterpolation()
        {
            var box = new RectF(0f, 0f, 100f, 40f);
            Paint p = Css.Background("linear-gradient(rgba(232,168,76,.55), rgba(232,168,76,.18))", box);
            Assert.That(p.At(new Vec2(50f, 0f)).A, Is.EqualTo(0.55f).Within(1e-4f));
            Assert.That(p.At(new Vec2(50f, 40f)).A, Is.EqualTo(0.18f).Within(1e-4f));
            Assert.That(p.At(new Vec2(50f, 20f)).A, Is.EqualTo(0.365f).Within(1e-4f));
            // to transparent: the hue stays (premultiplied), only the alpha falls
            Paint fade = Css.Background("linear-gradient(90deg, var(--amber), transparent)", box);
            Near(fade.At(new Vec2(50f, 10f)), DsTokens.Amber.WithAlpha(0.5f), 1e-4f);
        }

        [Test]
        public void StopFixup_FollowsCssImages()
        {
            var box = new RectF(0f, 0f, 100f, 100f);
            var g = (LinearGradient)Css.Background("linear-gradient(#f00, #0f0 30%, #00f 20%, #fff)", box);
            Assert.That(g.StopPositions, Is.EqualTo(new[] { 0f, 0.3f, 0.3f, 1f }).Within(1e-5f));
            var even = (LinearGradient)Css.Background("linear-gradient(#000 0%, #111, #222, #333 90%)", box);
            Assert.That(even.StopPositions, Is.EqualTo(new[] { 0f, 0.3f, 0.6f, 0.9f }).Within(1e-5f));
            var auto = (LinearGradient)Css.Background("linear-gradient(#000, #888, #fff)", box);
            Assert.That(auto.StopPositions, Is.EqualTo(new[] { 0f, 0.5f, 1f }).Within(1e-5f));
        }

        [Test]
        public void PxStops_AndRepeating_ThrowLine()
        {
            // bundle.css:148 .ocb-throwline: 9 on, 6 off from the left edge
            var box = new RectF(400f, 563f, 300f, 3f);
            var g = (LinearGradient)Css.Background("repeating-linear-gradient(90deg, var(--amber) 0 9px, transparent 9px 15px)", box);
            Assert.That(g.At(new Vec2(404f, 564f)).A, Is.EqualTo(1f));
            Assert.That(g.At(new Vec2(410f, 564f)).A, Is.EqualTo(0f));
            Assert.That(g.At(new Vec2(419f, 564f)).A, Is.EqualTo(1f));
            Assert.That(g.At(new Vec2(425f, 564f)).A, Is.EqualTo(0f));
            Assert.That(g.TryAsStripes(out float from, out float to, out float period, out Rgba c), Is.True);
            Assert.That((from, to, period), Is.EqualTo((0f, 9f, 15f)));
            Assert.That(c, Is.EqualTo(DsTokens.Amber));
        }

        [Test]
        public void Bands_OnePerInteriorStop_AndPerRepeat()
        {
            var box = new RectF(0f, 0f, 100f, 40f);
            var g = (LinearGradient)Css.Background("linear-gradient(#000, #fff 50%, #000)", box);
            var lines = g.Bands(box).ToList();
            Assert.That(lines.Count, Is.EqualTo(1));
            Assert.That(lines[0].Side(new Vec2(0f, 20f)), Is.EqualTo(0f).Within(1e-3f));
            var rep = (LinearGradient)Css.Background("repeating-linear-gradient(90deg, #fff 0 9px, transparent 9px 15px)", new RectF(0f, 0f, 150f, 3f));
            // stops at 0, 9 (twice) and 15 each period: lines at 9, 15, 24, 30, ... inside (0, 150)
            var xs = rep.Bands(new RectF(0f, 0f, 150f, 3f)).Select(l => l.C / l.N.X).Distinct().OrderBy(x => x).ToList();
            Assert.That(xs.First(), Is.EqualTo(9f).Within(1e-3f));
            Assert.That(xs.Count, Is.EqualTo(19));
        }

        [Test]
        public void Bands_AddSubLines_WhereStraightInterpolationWouldDiffer()
        {
            var box = new RectF(0f, 0f, 100f, 100f);
            // the sigil (fx.js:433): ink .95 → phosphor .98 → violet .95: colours and alphas differ
            var g = (LinearGradient)Css.Background("linear-gradient(180deg, rgba(236,235,242,.5) 0%, rgba(185,163,255,.98) 100%)", box);
            Assert.That(g.Bands(box).Count(), Is.EqualTo(7));
            // same colour, different alpha: linear as is
            var same = (LinearGradient)Css.Background("linear-gradient(rgba(10,12,18,0), rgba(10,12,18,.8))", box);
            Assert.That(same.Bands(box).Count(), Is.EqualTo(0));
        }

        [Test]
        public void SideKeywords()
        {
            Assert.That(LinearGradient.SideAngle("to bottom right", 100f, 100f), Is.EqualTo(135f).Within(1e-4f));
            Assert.That(LinearGradient.SideAngle("to top", 100f, 50f), Is.EqualTo(0f));
            Assert.That(LinearGradient.SideAngle("to right", 100f, 50f), Is.EqualTo(90f));
            Assert.That(LinearGradient.SideAngle("to left", 100f, 50f), Is.EqualTo(270f));
            Assert.That(LinearGradient.SideAngle("to top right", 100f, 100f), Is.EqualTo(45f).Within(1e-4f));
            var g = (LinearGradient)Css.Background("linear-gradient(to bottom right, #000, #fff)", new RectF(0f, 0f, 200f, 100f));
            // the corner form: the 50 % line passes through the other two corners
            Assert.That(g.TAt(new Vec2(200f, 0f)), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(g.TAt(new Vec2(0f, 100f)), Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void Radial_SizeKeywordsAndPositions()
        {
            Assert.That(RadialGradient.TryShape("closest-side", new RectF(0f, 0f, 520f, 400f), out Vec2 c1, out float rx1, out float ry1), Is.True);
            Assert.That((c1.X, c1.Y, rx1, ry1), Is.EqualTo((260f, 200f, 260f, 200f)));
            RadialGradient.TryShape("", new RectF(0f, 0f, 100f, 100f), out _, out float rx2, out float ry2);
            Assert.That(rx2, Is.EqualTo(70.7107f).Within(1e-3f));
            Assert.That(ry2, Is.EqualTo(70.7107f).Within(1e-3f));
            RadialGradient.TryShape("ellipse 72% 66% at 50% 50%", new RectF(0f, 0f, 1920f, 1080f), out Vec2 c3, out float rx3, out float ry3);
            Assert.That((c3.X, c3.Y), Is.EqualTo((960f, 540f)));
            Assert.That(rx3, Is.EqualTo(1382.4f).Within(1e-2f));
            Assert.That(ry3, Is.EqualTo(712.8f).Within(1e-2f));
            RadialGradient.TryShape("60% 55% at 34% 88%", new RectF(0f, 0f, 1920f, 1080f), out Vec2 c4, out float rx4, out float ry4);
            Assert.That(c4.X, Is.EqualTo(652.8f).Within(1e-2f));
            Assert.That(c4.Y, Is.EqualTo(950.4f).Within(1e-2f));
            Assert.That((rx4, ry4), Is.EqualTo((1152f, 594f)).Within(1e-2f));
            RadialGradient.TryShape("ellipse at 30% 18%", new RectF(0f, 0f, 1920f, 1080f), out Vec2 c5, out float rx5, out float ry5);
            Assert.That(c5.X, Is.EqualTo(576f).Within(1e-2f));
            Assert.That(rx5, Is.EqualTo(1344f * 1.41421356f).Within(1e-1f));
            Assert.That(ry5, Is.EqualTo(885.6f * 1.41421356f).Within(1e-1f));
            RadialGradient.TryShape("1100px 760px at 300px -80px", new RectF(0f, 0f, 1920f, 1080f), out Vec2 c6, out float rx6, out float ry6);
            Assert.That((c6.X, c6.Y, rx6, ry6), Is.EqualTo((300f, -80f, 1100f, 760f)));
            RadialGradient.TryShape("circle closest-side", new RectF(0f, 0f, 100f, 60f), out _, out float rx7, out float ry7);
            Assert.That((rx7, ry7), Is.EqualTo((30f, 30f)));
        }

        [Test]
        public void Radial_ColoursAlongTheRay()
        {
            var box = new RectF(0f, 0f, 520f, 400f);
            // explore.css:72 the lamp's pool
            Paint p = Css.Background("radial-gradient(closest-side, color-mix(in srgb, var(--amber) 16%, transparent), color-mix(in srgb, var(--amber) 5%, transparent) 55%, transparent)", box);
            Assert.That(p.NeedsRefine, Is.True);
            Assert.That(p.At(box.Center).A, Is.EqualTo(0.16f).Within(1e-4f));
            Assert.That(p.At(new Vec2(260f + 260f * 0.55f, 200f)).A, Is.EqualTo(0.05f).Within(1e-4f));
            Assert.That(p.At(new Vec2(260f, 200f + 200f * 0.55f)).A, Is.EqualTo(0.05f).Within(1e-4f));
            Assert.That(p.At(new Vec2(519f, 200f)).A, Is.LessThan(0.01f));
            Near(p.At(new Vec2(260f + 260f * 0.3f, 200f)).WithAlpha(1f), DsTokens.Amber, 1e-4f);
        }

        [Test]
        public void PointsGradient_IsSvgUnpremultiplied()
        {
            var g = LinearGradient.Points(new Vec2(0f, 0f), new Vec2(100f, 0f), GradientStop.At(Rgba.Hex(0xff0000, 1f), 0f), GradientStop.At(Rgba.Hex(0x0000ff, 0f), 1f));
            Rgba mid = g.At(new Vec2(50f, 0f));
            Near(mid, new Rgba(0.5f, 0f, 0.5f, 0.5f));
            Assert.That(g.At(new Vec2(-10f, 0f)), Is.EqualTo(Rgba.Hex(0xff0000)));
            var canvas = RadialGradient.Points(new Vec2(0f, 0f), 10f, 20f, GradientStop.At(Rgba.White, 0f), GradientStop.At(Rgba.Black, 1f));
            Assert.That(canvas.At(new Vec2(5f, 0f)), Is.EqualTo(Rgba.White));
            Near(canvas.At(new Vec2(15f, 0f)), new Rgba(0.5f, 0.5f, 0.5f));
        }

        [Test]
        public void LayeredPaint_ComposesSourceOver()
        {
            var box = new RectF(0f, 0f, 100f, 100f);
            Paint p = Css.Background("linear-gradient(rgba(255,255,255,.5), rgba(255,255,255,.5)), #000", box);
            Near(p.At(new Vec2(10f, 10f)), new Rgba(0.5f, 0.5f, 0.5f, 1f));
        }
    }
}
