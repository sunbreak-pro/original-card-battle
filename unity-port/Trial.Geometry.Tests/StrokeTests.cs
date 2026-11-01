using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Trial.Geometry;

namespace Trial.Geometry.Tests
{
    public class StrokeTests
    {
        private static Polyline Line(bool closed, params float[] xy)
        {
            var pts = new List<Vec2>();
            for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Vec2(xy[i], xy[i + 1]));
            return new Polyline(pts, closed);
        }

        private static MeshBuilder Stroke(Polyline l, StrokeStyle s, float feather, Affine? m = null, Rgba? colour = null)
        {
            var mb = new MeshBuilder();
            Stroker.Stroke(l, s, m ?? Affine.Identity, feather, mb, new SolidPaint(colour ?? Rgba.White));
            return mb;
        }

        // ---- dashes ---------------------------------------------------------------------------------

        [Test]
        public void DashLengthsAlongAStraightLine()
        {
            List<Polyline> d = Stroker.Dash(Line(false, 0, 0, 100, 0), new[] { 10f, 5f }, 0f);
            Assert.That(d.Count, Is.EqualTo(7));
            Assert.That(d.Select(p => p.Points[0].X), Is.EqualTo(new[] { 0f, 15f, 30f, 45f, 60f, 75f, 90f }).Within(1e-4f));
            Assert.That(d.All(p => Math.Abs(p.Length - 10f) < 1e-4f), Is.True);
            Assert.That(d.All(p => !p.Closed), Is.True);
        }

        [Test]
        public void DashOffsetShiftsThePattern()
        {
            List<Polyline> a = Stroker.Dash(Line(false, 0, 0, 100, 0), new[] { 10f, 5f }, 5f);
            Assert.That(a[0].Points[0].X, Is.EqualTo(0f));
            Assert.That(a[0].Length, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(a[1].Points[0].X, Is.EqualTo(10f).Within(1e-4f));

            // a negative offset moves the pattern forward: −5 → phase 10 (the start of the gap)
            List<Polyline> b = Stroker.Dash(Line(false, 0, 0, 100, 0), new[] { 10f, 5f }, -5f);
            Assert.That(b[0].Points[0].X, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(b[0].Length, Is.EqualTo(10f).Within(1e-4f));

            // a whole period of offset changes nothing
            List<Polyline> c = Stroker.Dash(Line(false, 0, 0, 100, 0), new[] { 10f, 5f }, 30f);
            Assert.That(c.Select(p => p.Points[0].X), Is.EqualTo(new[] { 0f, 15f, 30f, 45f, 60f, 75f, 90f }).Within(1e-4f));
        }

        [Test]
        public void OddPatternsRepeatAndInvalidOnesDrawSolid()
        {
            List<Polyline> d = Stroker.Dash(Line(false, 0, 0, 30, 0), new[] { 5f }, 0f);
            Assert.That(d.Count, Is.EqualTo(3));
            Assert.That(d.Select(p => p.Points[0].X), Is.EqualTo(new[] { 0f, 10f, 20f }).Within(1e-4f));

            Polyline l = Line(false, 0, 0, 30, 0);
            Assert.That(Stroker.Dash(l, new[] { 0f, 0f }, 0f).Single(), Is.SameAs(l));
            Assert.That(Stroker.Dash(l, new[] { 3f, -1f }, 0f).Single(), Is.SameAs(l));
            Assert.That(Stroker.Dash(l, null, 0f).Single(), Is.SameAs(l));
        }

        [Test]
        public void DashesFollowCornersAndTheClosingSegment()
        {
            // 10 + 10 polyline, pattern 4 2: [0,4] [6,10] [12,16] [18,20]
            List<Polyline> d = Stroker.Dash(Line(false, 0, 0, 10, 0, 10, 10), new[] { 4f, 2f }, 0f);
            Assert.That(d.Select(p => p.Length), Is.EqualTo(new[] { 4f, 4f, 4f, 2f }).Within(1e-4f));

            // offset 1: [0,3] [5,9] [11,15] [17,20]: the dash [9,11] … check one dash bends round the corner
            List<Polyline> e = Stroker.Dash(Line(false, 0, 0, 10, 0, 10, 10), new[] { 4f, 2f }, 3f);
            Polyline corner = e.First(p => p.Points.Count == 3);
            Assert.That(corner.Points[1], Is.EqualTo(new Vec2(10, 0)));
            Assert.That(corner.Length, Is.EqualTo(4f).Within(1e-4f));

            // a closed 10×10 square, 10 on 10 off: the top and the bottom edges
            List<Polyline> sq = Stroker.Dash(Line(true, 0, 0, 10, 0, 10, 10, 0, 10), new[] { 10f, 10f }, 0f);
            Assert.That(sq.Count, Is.EqualTo(2));
            Assert.That(sq[1].Points[0], Is.EqualTo(new Vec2(10, 10)));
            Assert.That(sq[1].Points[sq[1].Points.Count - 1], Is.EqualTo(new Vec2(0, 10)));

            // the total dash length is the pattern's duty times the length
            Polyline circle = Flatten.Run(ExtraIcons.CutMarkCirclePath, Affine.Identity)[0];
            List<Polyline> dots = Stroker.Dash(circle, new[] { 3f, 3f }, 0f);
            float on = dots.Sum(p => p.Length);
            // the flattened circle (chords within .2 px) is a little shorter than 2π·8
            Assert.That(circle.Length, Is.EqualTo(2 * Math.PI * 8).Within(0.5));
            Assert.That(on, Is.EqualTo(24f + Math.Min(3f, circle.Length - 48f)).Within(0.01f));
            Assert.That(dots.Count, Is.EqualTo(9));
            Assert.That(dots[0].Points[0].X, Is.EqualTo(20f).Within(1e-4f), "a circle's dashes start at 3 o'clock");
        }

        [Test]
        public void ZeroOnLengthsMakeDots()
        {
            List<Polyline> d = Stroker.Dash(Line(false, 0, 0, 10, 0), new[] { 0f, 5f }, 0f);
            Assert.That(d.Count, Is.EqualTo(3));
            Assert.That(d.All(p => p.Length == 0f && p.Points.Count == 2), Is.True);
        }

        [Test]
        public void DashedStrokeScalesThePatternWithTheTransform()
        {
            var s = new StrokeStyle(2.2f, Cap.Butt, Join.Round, new[] { 2.4f, 2.4f });
            MeshBuilder one = Stroke(Line(false, 0, 0, 48, 0), s, 0f);
            Affine m = Affine.Scale(2f);
            MeshBuilder two = Stroke(Line(false, 0, 0, 96, 0), s, 0f, m);
            Assert.That(two.AlphaArea(), Is.EqualTo(4f * one.AlphaArea()).Within(1e-2f));
            // NonScaling: the pattern stays in output px
            var ns = s.Clone();
            ns.NonScaling = true;
            MeshBuilder three = Stroke(Line(false, 0, 0, 96, 0), ns, 0f, m);
            Assert.That(three.AlphaArea(), Is.EqualTo(2f * one.AlphaArea()).Within(1e-2f));
        }

        // ---- caps, dots, anti-aliasing ----------------------------------------------------------------

        [Test]
        public void ZeroLengthRoundCapIsADot()
        {
            var s = new StrokeStyle(4f, Cap.Round);
            MeshBuilder hard = Stroke(Line(false, 5, 5, 5, 5), s, 0f);
            Assert.That(hard.AlphaArea(), Is.EqualTo(Math.PI * 4).Within(0.01), "the dot keeps the disc's area");
            RectF b = hard.Bounds();
            Assert.That(b.W, Is.EqualTo(4f).Within(0.2f));
            Assert.That(b.Center.X, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(b.Center.Y, Is.EqualTo(5f).Within(1e-4f));

            // with a 1 px feather: a ramp from r − ½ to r + ½ adds π f²/12
            MeshBuilder soft = Stroke(Line(false, 5, 5, 5, 5), s, 1f);
            Assert.That(soft.AlphaArea(), Is.EqualTo(Math.PI * 4 + Math.PI / 12).Within(0.08));

            // butt caps draw nothing for a zero-length piece; square caps a w×w square
            Assert.That(Stroke(Line(false, 5, 5, 5, 5), new StrokeStyle(4f, Cap.Butt), 0f).TriangleCount, Is.EqualTo(0));
            Assert.That(Stroke(Line(false, 5, 5, 5, 5), new StrokeStyle(4f, Cap.Square), 0f).AlphaArea(), Is.EqualTo(16f).Within(1e-3f));
        }

        [Test]
        public void TheHDotOfTheBossIconIsAFullDot()
        {
            // "M9 12.5h.01" at 24 px: a 0.01 px segment with round caps = a dot of diameter 2.2
            Affine m = Affine.ViewBox(IconDef.Box24, 24, 24);
            Polyline l = Flatten.Run("M9 12.5h.01", m)[0];
            MeshBuilder mb = Stroke(l, new StrokeStyle(2.2f, Cap.Round, Join.Round), 0f, m);
            Assert.That(mb.AlphaArea(), Is.EqualTo(Math.PI * 1.1 * 1.1 + 0.01 * 2.2).Within(0.03));
        }

        [Test]
        public void StraightStrokeHasTheAAFringeOfSection43()
        {
            // w 4, f 1: core |d| ≤ 1.5 at alpha 1, ramp to 0 at |d| = 2.5
            MeshBuilder mb = Stroke(Line(false, 0, 0, 100, 0), new StrokeStyle(4f, Cap.Butt), 1f);
            for (int i = 0; i < mb.Pos.Count; i++)
            {
                float d = Math.Abs(mb.Pos[i].Y), a = mb.Col[i].A;
                if (d > 2.4f) Assert.That(a, Is.EqualTo(0f), "outer edge");
                else if (d < 1.6f && mb.Pos[i].X > 0.4999f && mb.Pos[i].X < 99.5001f) Assert.That(a, Is.EqualTo(1f), "core");
            }
            Assert.That(mb.Pos.Max(p => Math.Abs(p.Y)), Is.EqualTo(2.5f).Within(1e-4f));
            Assert.That(mb.Pos.Max(p => p.X), Is.EqualTo(100.5f).Within(1e-4f), "the butt end fades over ±f/2");
            // coverage is preserved: ∫α = w·L
            Assert.That(mb.AlphaArea(), Is.EqualTo(400f).Within(0.6f));
        }

        [Test]
        public void ThinStrokesKeepWidthFAndScaleTheirAlpha()
        {
            MeshBuilder mb = Stroke(Line(false, 0, 0, 100, 0), new StrokeStyle(0.5f, Cap.Butt), 1f);
            Assert.That(mb.Col.Max(c => c.A), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(mb.Pos.Max(p => Math.Abs(p.Y)), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(mb.AlphaArea(), Is.EqualTo(50f).Within(0.5f));
        }

        [Test]
        public void AButtEndOnAShortLastSegmentDoesNotTurnItsBandOver()
        {
            // the last segment (0.3 px) is shorter than the f/2 pull-back of a butt end: the band must not fold back
            // over the previous segment (a translucent stroke would show a darker spot); coverage stays about w·L
            // (the end ramp, pulled back only 0.15, starts at the centred ramp's 0.65: 0.06 px² per px of width over)
            Polyline l = Line(false, 0, 0, 10, 0, 10.3f, 0);
            MeshBuilder mb = Stroke(l, new StrokeStyle(2f, Cap.Butt, Join.Miter), 1f);
            Assert.That(mb.AlphaArea(), Is.EqualTo(2f * 10.3f + 2f * 0.06125f).Within(0.02f));
            Assert.That(mb.Pos.Max(p => p.X), Is.EqualTo(10.8f).Within(1e-4f), "the ramp still ends f/2 past the end");
        }

        [Test]
        public void SquareCapsExtendByHalfTheWidth()
        {
            MeshBuilder mb = Stroke(Line(false, 0, 0, 10, 0), new StrokeStyle(4f, Cap.Square), 0f);
            RectF b = mb.Bounds();
            Assert.That(b.Left, Is.EqualTo(-2f).Within(1e-4f));
            Assert.That(b.Right, Is.EqualTo(12f).Within(1e-4f));
            Assert.That(mb.AlphaArea(), Is.EqualTo(14f * 4f).Within(1e-3f));
        }

        [Test]
        public void RoundCapsAddHalfDiscs()
        {
            MeshBuilder mb = Stroke(Line(false, 0, 0, 10, 0), new StrokeStyle(4f, Cap.Round), 0f);
            Assert.That(mb.AlphaArea(), Is.EqualTo(40f + Math.PI * 4).Within(0.02));
            Assert.That(mb.Bounds().Left, Is.EqualTo(-2f).Within(0.1f));
        }

        // ---- joins ------------------------------------------------------------------------------------

        [Test]
        public void JoinsDoNotDoublePaint()
        {
            // a right angle, w 4, hard edges: miter = exact union 400; round = 400 − (4 − π); bevel = 400 − 2
            Polyline l = Line(false, 0, 0, 50, 0, 50, 50);
            Assert.That(Stroke(l, new StrokeStyle(4f, Cap.Butt, Join.Miter), 0f).AlphaArea(), Is.EqualTo(400f).Within(1e-2f));
            Assert.That(Stroke(l, new StrokeStyle(4f, Cap.Butt, Join.Round), 0f).AlphaArea(), Is.EqualTo(400f - (4f - (float)Math.PI)).Within(0.02f));
            Assert.That(Stroke(l, new StrokeStyle(4f, Cap.Butt, Join.Bevel), 0f).AlphaArea(), Is.EqualTo(398f).Within(1e-2f));
            // the same turning the other way
            Polyline r = Line(false, 0, 0, 50, 0, 50, -50);
            Assert.That(Stroke(r, new StrokeStyle(4f, Cap.Butt, Join.Miter), 0f).AlphaArea(), Is.EqualTo(400f).Within(1e-2f));
            Assert.That(Stroke(r, new StrokeStyle(4f, Cap.Butt, Join.Round), 0f).AlphaArea(), Is.EqualTo(400f - (4f - (float)Math.PI)).Within(0.02f));
        }

        [Test]
        public void AFlattenedCircleStrokeIsAnEvenRing()
        {
            // a ring r 8, w 2 (the cut mark without its dashes): area 2π·8·2, whatever the vertex count
            foreach (float scale in new[] { 1f, 4f })
            {
                Affine m = Affine.Scale(scale);
                Polyline c = Flatten.Run(ExtraIcons.CutMarkCirclePath, m)[0];
                MeshBuilder mb = Stroke(c, new StrokeStyle(2f, Cap.Butt, Join.Miter), 0f, m);
                double expected = 2 * Math.PI * 8 * 2 * scale * scale;
                Assert.That(mb.AlphaArea(), Is.EqualTo(expected).Within(expected * 0.01), "scale " + scale);
            }
        }

        [Test]
        public void MiterLimitFallsBackToBevel()
        {
            // a hairpin: the miter would reach far past x = 100
            Polyline l = Line(false, 0, 0, 100, 0, 0, 10);
            MeshBuilder miter = Stroke(l, new StrokeStyle(4f, Cap.Butt, Join.Miter), 0f);
            Assert.That(miter.Bounds().Right, Is.LessThan(102.5f));
            var unlimited = new StrokeStyle(4f, Cap.Butt, Join.Miter) { MiterLimit = 1000f };
            Assert.That(Stroke(l, unlimited, 0f).Bounds().Right, Is.GreaterThan(110f));
        }

        [Test]
        public void NearReversalsDoNotSpike()
        {
            // a hairpin that turns back by all but a hair: no miter spike, whatever the join
            Polyline l = Line(false, 0, 0, 100, 0, 0, 0.0005f);
            foreach (Join j in new[] { Join.Miter, Join.Round, Join.Bevel })
                Assert.That(Stroke(l, new StrokeStyle(4f, Cap.Butt, j), 1f).Bounds().Right, Is.LessThan(103f), j.ToString());
        }

        [Test]
        public void StrokeWidthScalesWithTheTransformUnlessNonScaling()
        {
            Affine m = Affine.Scale(3f);
            var l = Line(false, 0, 0, 30, 0);
            Assert.That(Stroke(l, new StrokeStyle(2f), 0f, m).Bounds().H, Is.EqualTo(6f).Within(1e-4f));
            Assert.That(Stroke(l, new StrokeStyle(2f) { NonScaling = true }, 0f, m).Bounds().H, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void ClosedPolylinesHaveNoCaps()
        {
            MeshBuilder mb = Stroke(Line(true, 0, 0, 10, 0, 10, 10, 0, 10), new StrokeStyle(2f, Cap.Round, Join.Miter), 0f);
            Assert.That(mb.AlphaArea(), Is.EqualTo(12f * 12f - 8f * 8f).Within(1e-3f));
        }

        // ---- soft bands -------------------------------------------------------------------------------

        [Test]
        public void SoftBandFollowsItsProfile()
        {
            var mb = new MeshBuilder();
            Func<float, float> profile = Blur.StrokeShadowProfile(4f, 2f);
            Stroker.Soft(Line(false, 0, 0, 100, 0), profile, Blur.StrokeShadowReach(4f, 2f), Affine.Identity, mb, Rgba.Hex(0x6fd0b0, 0.5f));
            int checkedCount = 0;
            for (int i = 0; i < mb.Pos.Count; i++)
            {
                Vec2 p = mb.Pos[i];
                if (p.X < -1e-4f || p.X > 100.0001f) continue;
                Assert.That(mb.Col[i].A, Is.EqualTo(0.5f * profile(p.Y)).Within(1e-5f), p.ToString());
                checkedCount++;
            }
            Assert.That(checkedCount, Is.GreaterThan(20));
            float centre = mb.Col.Where((c, i) => Math.Abs(mb.Pos[i].Y) < 1e-4f && mb.Pos[i].X > -1e-4f && mb.Pos[i].X < 100.0001f).Max(c => c.A);
            Assert.That(centre, Is.EqualTo(0.5f * 0.6826895f).Within(1e-4f));
            // blurring keeps the stroke's coverage: ∫ profile dd ≈ w along the straight part
            Assert.That(mb.AlphaArea(), Is.GreaterThan(0.5f * 4f * 100f * 0.97f));
        }

        [Test]
        public void SoftDistancesScaleWithTheTransform()
        {
            var mb = new MeshBuilder();
            Stroker.Soft(Line(false, 0, 0, 100, 0), d => 1f - Math.Abs(d) / 5f, 5f, Affine.Scale(2f), mb, Rgba.White);
            Assert.That(mb.Pos.Where(p => p.X >= 0f && p.X <= 100f).Max(p => Math.Abs(p.Y)), Is.EqualTo(10f).Within(1e-4f));
        }
    }
}
