using System.Collections.Generic;
using NUnit.Framework;
using Trial.Geometry;

namespace Trial.Geometry.Tests
{
    public class PathDataTests
    {
        private static void AssertV(Vec2 v, float x, float y, float eps = 1e-4f)
        {
            Assert.That(v.X, Is.EqualTo(x).Within(eps), "x of " + v);
            Assert.That(v.Y, Is.EqualTo(y).Within(eps), "y of " + v);
        }

        [Test]
        public void MovetoLinetoAndClose()
        {
            PathData p = PathData.Parse("M10 20L30 40Z");
            Assert.That(p.Error, Is.Null);
            Assert.That(p.SubPaths.Count, Is.EqualTo(1));
            SubPath sp = p.SubPaths[0];
            AssertV(sp.Start, 10, 20);
            Assert.That(sp.Closed, Is.True);
            Assert.That(sp.Segments.Count, Is.EqualTo(1));
            AssertV(sp.Segments[0].To, 30, 40);
        }

        [Test]
        public void ImplicitRepeatsAfterMovetoAreLinetos()
        {
            // "L90 80 86 112 96 150" style and "M a b c d" = M then L
            PathData p = PathData.Parse("M0 0 10 0 10 10");
            Assert.That(p.Error, Is.Null);
            Assert.That(p.SubPaths[0].Segments.Count, Is.EqualTo(2));
            AssertV(p.SubPaths[0].Segments[1].To, 10, 10);

            PathData r = PathData.Parse("m5 5 10 0 0 10");
            Assert.That(r.SubPaths[0].Segments.Count, Is.EqualTo(2));
            AssertV(r.SubPaths[0].Start, 5, 5);
            AssertV(r.SubPaths[0].Segments[0].To, 15, 5);
            AssertV(r.SubPaths[0].Segments[1].To, 15, 15);

            PathData l = PathData.Parse("M40 46L90 80 86 112 96 150H28");
            Assert.That(l.SubPaths[0].Segments.Count, Is.EqualTo(4));
            AssertV(l.SubPaths[0].Segments[2].To, 96, 150);
            AssertV(l.SubPaths[0].Segments[3].To, 28, 150);
        }

        [Test]
        public void NumbersWithoutSeparators()
        {
            // "-1.5 5.5L9", ".7.5" = .7 then .5, "1.4-1.1"
            PathData p = PathData.Parse("M20 4l-1.5 5.5L9 19");
            Assert.That(p.Error, Is.Null);
            AssertV(p.SubPaths[0].Segments[0].To, 18.5f, 9.5f);
            AssertV(p.SubPaths[0].Segments[1].To, 9, 19);

            PathData q = PathData.Parse("M0 0l.7.5");
            Assert.That(q.Error, Is.Null);
            AssertV(q.SubPaths[0].Segments[0].To, 0.7f, 0.5f);

            PathData r = PathData.Parse("M10 10l1.4-1.1");
            AssertV(r.SubPaths[0].Segments[0].To, 11.4f, 8.9f);

            PathData e = PathData.Parse("M1e1 2E-1L-.5e+1,3");
            Assert.That(e.Error, Is.Null);
            AssertV(e.SubPaths[0].Start, 10f, 0.2f);
            AssertV(e.SubPaths[0].Segments[0].To, -5f, 3f);
        }

        [Test]
        public void PackedArcFlags()
        {
            // "a3.5 3.5 0 110 7" = rx 3.5, ry 3.5, rot 0, large 1, sweep 1, x 0, y 7
            PathData p = PathData.Parse("M12 4a3.5 3.5 0 110 7");
            Assert.That(p.Error, Is.Null);
            PathSegment s = p.SubPaths[0].Segments[0];
            Assert.That(s.Kind, Is.EqualTo(SegmentKind.Arc));
            Assert.That(s.Rx, Is.EqualTo(3.5f));
            Assert.That(s.Ry, Is.EqualTo(3.5f));
            Assert.That(s.XAxisRotation, Is.EqualTo(0f));
            Assert.That(s.LargeArc, Is.True);
            Assert.That(s.Sweep, Is.True);
            AssertV(s.To, 12, 11);

            // "a4.5 4.5 0 018-2.8" = large 0, sweep 1, x 8, y −2.8
            PathData h = PathData.Parse("M4 10a4.5 4.5 0 018-2.8");
            PathSegment a = h.SubPaths[0].Segments[0];
            Assert.That(a.LargeArc, Is.False);
            Assert.That(a.Sweep, Is.True);
            AssertV(a.To, 12, 7.2f);

            // absolute A with packed flags: "A4.5 4.5 0 0120 10" = large 0, sweep 1, x 20, y 10
            PathData b = PathData.Parse("M12 7.2A4.5 4.5 0 0120 10");
            PathSegment ab = b.SubPaths[0].Segments[0];
            Assert.That(ab.LargeArc, Is.False);
            Assert.That(ab.Sweep, Is.True);
            AssertV(ab.To, 20, 10);

            // "a1.6 1.6 0 11-2 2": large 1, sweep 1, x −2, y 2
            PathData k = PathData.Parse("M5.6 17.2a1.6 1.6 0 11-2 2");
            PathSegment ak = k.SubPaths[0].Segments[0];
            Assert.That(ak.LargeArc && ak.Sweep, Is.True);
            AssertV(ak.To, 3.6f, 19.2f);
        }

        [Test]
        public void RelativeHorizontalVerticalAndTinyDots()
        {
            PathData p = PathData.Parse("M9 12.5h.01M15 12.5h.01M10 17h4");
            Assert.That(p.Error, Is.Null);
            Assert.That(p.SubPaths.Count, Is.EqualTo(3));
            AssertV(p.SubPaths[0].Segments[0].To, 9.01f, 12.5f);
            AssertV(p.SubPaths[2].Segments[0].To, 14, 17);

            PathData v = PathData.Parse("M6 21V11v-3H2h1");
            Assert.That(v.Error, Is.Null);
            AssertV(v.SubPaths[0].Segments[0].To, 6, 11);
            AssertV(v.SubPaths[0].Segments[1].To, 6, 8);
            AssertV(v.SubPaths[0].Segments[2].To, 2, 8);
            AssertV(v.SubPaths[0].Segments[3].To, 3, 8);
        }

        [Test]
        public void SmoothCubicReflectsThePreviousControlPoint()
        {
            // "M62 6c8 0 14 6 14 14s-6 14-14 14": s reflects (76−14+…): c2 of the c is (76, 12), reflected about (76, 20) → (76, 28)
            PathData p = PathData.Parse("M62 6c8 0 14 6 14 14s-6 14-14 14");
            Assert.That(p.Error, Is.Null);
            PathSegment c = p.SubPaths[0].Segments[0];
            AssertV(c.C1, 70, 6);
            AssertV(c.C2, 76, 12);
            AssertV(c.To, 76, 20);
            PathSegment s = p.SubPaths[0].Segments[1];
            Assert.That(s.Kind, Is.EqualTo(SegmentKind.Cubic));
            AssertV(s.C1, 76, 28);
            AssertV(s.C2, 70, 34);
            AssertV(s.To, 62, 34);

            // absolute S after a C ("C46 40 56 38 62 38S80 40 86 46")
            PathData a = PathData.Parse("M40 46C46 40 56 38 62 38S80 40 86 46");
            PathSegment sa = a.SubPaths[0].Segments[1];
            AssertV(sa.C1, 68, 38);
            AssertV(sa.C2, 80, 40);
            AssertV(sa.To, 86, 46);

            // S without a preceding cubic uses the current point
            PathData n = PathData.Parse("M0 0L10 0S20 10 30 0");
            PathSegment sn = n.SubPaths[0].Segments[1];
            AssertV(sn.C1, 10, 0);
        }

        [Test]
        public void SmoothQuadReflectsThePreviousQuadControlPoint()
        {
            PathData p = PathData.Parse("M0 0Q10 10 20 0T40 0t20 0");
            Assert.That(p.Error, Is.Null);
            PathSegment t1 = p.SubPaths[0].Segments[1];
            Assert.That(t1.Kind, Is.EqualTo(SegmentKind.Quad));
            AssertV(t1.C1, 30, -10);
            PathSegment t2 = p.SubPaths[0].Segments[2];
            AssertV(t2.C1, 50, 10);
            AssertV(t2.To, 60, 0);

            // relative q (spearman's head: "L70 16Q63 21 68 27")
            PathData q = PathData.Parse("M90 10L70 16Q63 21 68 27");
            PathSegment sq = q.SubPaths[0].Segments[1];
            AssertV(sq.C1, 63, 21);
            AssertV(sq.To, 68, 27);

            PathData rq = PathData.Parse("M10 10q5 5 10 0");
            AssertV(rq.SubPaths[0].Segments[0].C1, 15, 15);
            AssertV(rq.SubPaths[0].Segments[0].To, 20, 10);
        }

        [Test]
        public void ZThenDrawingStartsANewSubpathAtTheStart()
        {
            PathData p = PathData.Parse("M10 10h10v10zl5 5");
            Assert.That(p.Error, Is.Null);
            Assert.That(p.SubPaths.Count, Is.EqualTo(2));
            Assert.That(p.SubPaths[0].Closed, Is.True);
            AssertV(p.SubPaths[1].Start, 10, 10);
            AssertV(p.SubPaths[1].Segments[0].To, 15, 15);

            // m after z is relative to the subpath start
            PathData m = PathData.Parse("M8 3h9v14H8zm-3 4v14h9");
            AssertV(m.SubPaths[1].Start, 5, 7);
            AssertV(m.SubPaths[1].Segments[1].To, 14, 21);
        }

        [Test]
        public void WhitespaceAndCommas()
        {
            PathData p = PathData.Parse("  M 3 2 H61 V31 C61 50 46 63 32 70 C18 63 3 50 3 31 Z ");
            Assert.That(p.Error, Is.Null);
            Assert.That(p.SubPaths[0].Segments.Count, Is.EqualTo(4), "H V C C; Z closes without a segment");
            Assert.That(p.SubPaths[0].Closed, Is.True);

            PathData c = PathData.Parse("M1,2 L3,4,5,6");
            Assert.That(c.Error, Is.Null);
            AssertV(c.SubPaths[0].Segments[1].To, 5, 6);
        }

        [Test]
        public void ErrorsStopTheParseAndKeepWhatCameBefore()
        {
            PathData p = PathData.Parse("M0 0L10 0L20");
            Assert.That(p.Error, Is.Not.Null);
            Assert.That(p.SubPaths[0].Segments.Count, Is.EqualTo(1));

            Assert.That(PathData.Parse("L0 0").Error, Is.Not.Null);
            Assert.That(PathData.Parse("M0 0 z 5").Error, Is.Not.Null);
            Assert.That(PathData.Parse("M0 0a1 1 0 2 0 5 5").Error, Is.Not.Null, "flags must be 0 or 1");
            Assert.That(PathData.Parse("").Error, Is.Null);
            Assert.That(PathData.Parse("").SubPaths.Count, Is.EqualTo(0));
        }

        [Test]
        public void CachedReturnsTheSameInstance()
        {
            PathData a = PathData.Cached("M0 0L1 1");
            Assert.That(PathData.Cached("M0 0L1 1"), Is.SameAs(a));
        }

        // ---- transforms (Vec2, RectF, Affine) -----------------------------------------------------------

        [Test]
        public void AffineRotateCssIsClockwiseOnScreen()
        {
            Vec2 p = Affine.RotateCss(90f).Apply(new Vec2(1, 0));
            AssertV(p, 0, 1); // right → down
            Vec2 q = Affine.RotateCss(-4f).Apply(new Vec2(0, 1));
            Assert.That(q.X, Is.GreaterThan(0f), "rotate(−4°) turns the down vector toward +x (counter-clockwise on screen)");
        }

        [Test]
        public void AffineThenComposesInOrder()
        {
            Affine m = Affine.Translate(10, 0).Then(Affine.Scale(2, 3));
            AssertV(m.Apply(new Vec2(1, 1)), 22, 3);
            Affine n = Affine.Scale(2, 3).Then(Affine.Translate(10, 0));
            AssertV(n.Apply(new Vec2(1, 1)), 12, 3);
            Affine about = Affine.ScaleAbout(0.84f, new Vec2(32, 36));
            AssertV(about.Apply(new Vec2(32, 36)), 32, 36);
            AssertV(about.Apply(new Vec2(42, 36)), 32 + 8.4f, 36);
            Affine inv = n.Inverse();
            AssertV(inv.Apply(n.Apply(new Vec2(5, 7))), 5, 7);
            Assert.That(Affine.Scale(2, 8).MeanScale, Is.EqualTo(4f).Within(1e-6f));
        }

        [Test]
        public void AffineViewBoxMeetCentres()
        {
            Affine m = Affine.ViewBox(new RectF(0, 0, 24, 24), 48, 48);
            AssertV(m.Apply(new Vec2(24, 24)), 48, 48);
            // a 64×72 shield in a 100×114 box: scale min(100/64, 114/72) = 1.5625, centred vertically
            Affine s = Affine.ViewBox(new RectF(0, 0, 64, 72), 100, 114);
            Assert.That(s.A, Is.EqualTo(1.5625f).Within(1e-6f));
            AssertV(s.Apply(new Vec2(0, 0)), 0, (114 - 72 * 1.5625f) / 2);
            // a non-zero origin
            Affine o = Affine.ViewBox(new RectF(10, 10, 10, 20), 20, 20);
            AssertV(o.Apply(new Vec2(10, 10)), 5, 0);
            AssertV(o.Apply(new Vec2(20, 30)), 15, 20);
        }

        [Test]
        public void VecAndRectBasics()
        {
            var a = new Vec2(3, 4);
            Assert.That(a.Len, Is.EqualTo(5f));
            AssertV(a.Norm, 0.6f, 0.8f);
            AssertV(new Vec2(1, 0).Perp, 0, 1);
            Assert.That(Vec2.Cross(new Vec2(1, 0), new Vec2(0, 1)), Is.EqualTo(1f));
            AssertV(Vec2.Lerp(new Vec2(0, 0), new Vec2(10, 20), 0.25f), 2.5f, 5f);
            var r = new RectF(10, 20, 30, 40);
            Assert.That(r.Right, Is.EqualTo(40f));
            Assert.That(r.Bottom, Is.EqualTo(60f));
            Assert.That(r.Contains(new Vec2(10, 20)), Is.True);
            Assert.That(r.Inflate(2).W, Is.EqualTo(34f));
            Assert.That(r.Inset(1, 2, 3, 4), Is.EqualTo(new RectF(14, 21, 24, 36)));
            Assert.That(RectF.Bounds(new List<Vec2> { new Vec2(5, 1), new Vec2(-1, 9) }), Is.EqualTo(RectF.FromLtrb(-1, 1, 5, 9)));
        }
    }
}
