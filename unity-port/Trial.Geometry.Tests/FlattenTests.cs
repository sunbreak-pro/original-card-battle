using System;
using System.Collections.Generic;
using NUnit.Framework;
using Trial.Geometry;

namespace Trial.Geometry.Tests
{
    public class FlattenTests
    {
        [Test]
        public void ArcToCenterSpecExample()
        {
            // SVG 1.1 §8.3.8 figure: M125,75 a100,50 0 0,1 100,50 → centre (125, 125), θ1 = −90°, Δθ = +90°
            ArcCenter a = Flatten.ArcToCenter(new Vec2(125, 75), new Vec2(225, 125), 100, 50, 0, false, true);
            Assert.That(a.IsLine || a.IsEmpty, Is.False);
            Assert.That(a.Center.X, Is.EqualTo(125f).Within(1e-3f));
            Assert.That(a.Center.Y, Is.EqualTo(125f).Within(1e-3f));
            Assert.That(a.Theta1, Is.EqualTo(-Math.PI / 2).Within(1e-5));
            Assert.That(a.DTheta, Is.EqualTo(Math.PI / 2).Within(1e-5));

            // the other three arcs of the same figure
            ArcCenter b = Flatten.ArcToCenter(new Vec2(125, 75), new Vec2(225, 125), 100, 50, 0, true, true);
            Assert.That(b.Center.X, Is.EqualTo(225f).Within(1e-3f));
            Assert.That(b.Center.Y, Is.EqualTo(75f).Within(1e-3f));
            Assert.That(b.DTheta, Is.EqualTo(1.5 * Math.PI).Within(1e-5));
            ArcCenter c = Flatten.ArcToCenter(new Vec2(125, 75), new Vec2(225, 125), 100, 50, 0, false, false);
            Assert.That(c.Center.X, Is.EqualTo(225f).Within(1e-3f));
            Assert.That(c.DTheta, Is.EqualTo(-Math.PI / 2).Within(1e-5));
            ArcCenter d = Flatten.ArcToCenter(new Vec2(125, 75), new Vec2(225, 125), 100, 50, 0, true, false);
            Assert.That(d.Center.X, Is.EqualTo(125f).Within(1e-3f));
            Assert.That(d.DTheta, Is.EqualTo(-1.5 * Math.PI).Within(1e-5));
        }

        [Test]
        public void ArcSweepOneRunsClockwiseOnScreen()
        {
            // a half circle from (0,0) to (10,0), sweep 1: through the top (y < 0) on a y-down screen
            ArcCenter a = Flatten.ArcToCenter(new Vec2(0, 0), new Vec2(10, 0), 5, 5, 0, false, true);
            Assert.That(a.Center.X, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(a.Center.Y, Is.EqualTo(0f).Within(1e-4f));
            Vec2 mid = a.PointAt(a.Theta1 + a.DTheta / 2);
            Assert.That(mid.Y, Is.EqualTo(-5f).Within(1e-4f));
        }

        [Test]
        public void ArcRadiusCorrectionScalesTooSmallRadii()
        {
            // F.6.6: rx = ry = 1 cannot span 10 units: the radii grow to 5 and the centre is the midpoint
            ArcCenter a = Flatten.ArcToCenter(new Vec2(0, 0), new Vec2(10, 0), 1, 1, 0, false, true);
            Assert.That(a.Rx, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(a.Ry, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(a.Center.X, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(Math.Abs(a.DTheta), Is.EqualTo(Math.PI).Within(1e-4));
            // negative radii are taken as absolute
            ArcCenter n = Flatten.ArcToCenter(new Vec2(0, 0), new Vec2(10, 0), -5, -5, 0, false, true);
            Assert.That(n.Rx, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void ArcZeroRadiusIsALineAndEqualEndpointsAreOmitted()
        {
            Assert.That(Flatten.ArcToCenter(new Vec2(0, 0), new Vec2(10, 0), 0, 5, 0, false, true).IsLine, Is.True);
            Assert.That(Flatten.ArcToCenter(new Vec2(3, 3), new Vec2(3, 3), 5, 5, 0, false, true).IsEmpty, Is.True);
            List<Polyline> l = Flatten.Run(PathData.Parse("M0 0A0 5 0 0 1 10 0"), Affine.Identity);
            Assert.That(l[0].Points.Count, Is.EqualTo(2));
        }

        [Test]
        public void RotatedArcsHitTheirEndpoints()
        {
            var rnd = new Random(7);
            for (int i = 0; i < 200; i++)
            {
                var p1 = new Vec2((float)(rnd.NextDouble() * 40 - 20), (float)(rnd.NextDouble() * 40 - 20));
                var p2 = new Vec2((float)(rnd.NextDouble() * 40 - 20), (float)(rnd.NextDouble() * 40 - 20));
                float rx = (float)(rnd.NextDouble() * 20 + 0.5), ry = (float)(rnd.NextDouble() * 20 + 0.5);
                float rot = (float)(rnd.NextDouble() * 360 - 180);
                bool large = rnd.Next(2) == 1, sweep = rnd.Next(2) == 1;
                ArcCenter a = Flatten.ArcToCenter(p1, p2, rx, ry, rot, large, sweep);
                Vec2 s = a.PointAt(a.Theta1), e = a.PointAt(a.Theta1 + a.DTheta);
                Assert.That(Vec2.Dist(s, p1), Is.LessThan(2e-3f), "start " + i);
                Assert.That(Vec2.Dist(e, p2), Is.LessThan(2e-3f), "end " + i);
                Assert.That(sweep ? a.DTheta >= 0 : a.DTheta <= 0, Is.True, "sweep direction " + i);
                if (a.Rx <= rx * 1.0001f) Assert.That(Math.Abs(a.DTheta) > Math.PI, Is.EqualTo(large), "large-arc flag " + i);
                else Assert.That(Math.Abs(a.DTheta), Is.EqualTo(Math.PI).Within(1e-3), "scaled-up radii give a half ellipse " + i);
            }
        }

        [Test]
        public void CircleFlattensWithinTolerance()
        {
            // pip: a circle of r 6 made of two arcs, at 10× (r = 60 px) with tolerance .2 px
            Affine m = Affine.Scale(10f);
            List<Polyline> l = Flatten.Run(PathData.Parse("M12 6a6 6 0 110 12a6 6 0 010-12z"), m, 0.2f);
            Assert.That(l.Count, Is.EqualTo(1));
            Polyline c = l[0];
            Assert.That(c.Closed, Is.True);
            var centre = new Vec2(120, 120);
            for (int i = 0; i < c.Points.Count; i++)
            {
                Vec2 a = c.Points[i], b = c.Points[(i + 1) % c.Points.Count];
                Assert.That(Vec2.Dist(a, centre), Is.EqualTo(60f).Within(1e-3f));
                float chordMid = Vec2.Dist((a + b) * 0.5f, centre);
                Assert.That(60f - chordMid, Is.LessThanOrEqualTo(0.2001f), "sagitta");
            }
            Assert.That(Vec2.Dist(c.Points[0], c.Points[c.Points.Count - 1]), Is.GreaterThan(1e-3f), "closed rings do not repeat their start");
        }

        [Test]
        public void CubicFlattensWithinToleranceAfterTheTransform()
        {
            var p0 = new Vec2(0, 0); var p1 = new Vec2(0, 100); var p2 = new Vec2(100, 100); var p3 = new Vec2(100, 0);
            foreach (float scale in new[] { 0.5f, 1f, 4f })
            {
                Affine m = Affine.Scale(scale);
                List<Polyline> l = Flatten.Run(PathData.Parse("M0 0C0 100 100 100 100 0"), m, 0.2f);
                List<Vec2> pts = l[0].Points;
                // every true curve point is within tolerance of the polyline
                for (int k = 0; k <= 400; k++)
                {
                    float t = k / 400f, u = 1 - t;
                    Vec2 q = (p0 * (u * u * u) + p1 * (3 * u * u * t) + p2 * (3 * u * t * t) + p3 * (t * t * t)) * scale;
                    float best = float.MaxValue;
                    for (int i = 0; i + 1 < pts.Count; i++) best = Math.Min(best, Flatten.DistToSegment(q, pts[i], pts[i + 1]));
                    Assert.That(best, Is.LessThanOrEqualTo(0.2f + 1e-3f), "scale " + scale + " t " + t);
                }
                Assert.That(pts[pts.Count - 1].X, Is.EqualTo(100f * scale).Within(1e-4f));
            }
            int coarse = Flatten.Run(PathData.Parse("M0 0C0 100 100 100 100 0"), Affine.Scale(0.5f))[0].Points.Count;
            int fine = Flatten.Run(PathData.Parse("M0 0C0 100 100 100 100 0"), Affine.Scale(4f))[0].Points.Count;
            Assert.That(fine, Is.GreaterThan(coarse), "the tolerance is in output px");
        }

        [Test]
        public void ArcStepFollowsTheOutputRadius()
        {
            int small = Flatten.Run(PathData.Parse("M12 6a6 6 0 110 12a6 6 0 010-12z"), Affine.Scale(1f))[0].Points.Count;
            int big = Flatten.Run(PathData.Parse("M12 6a6 6 0 110 12a6 6 0 010-12z"), Affine.Scale(20f))[0].Points.Count;
            Assert.That(big, Is.GreaterThan(small * 3));
            // the documented step: 2·acos(1 − tol / r)
            Assert.That(Flatten.StepAngle(100, 0.2), Is.EqualTo(2 * Math.Acos(1 - 0.002)).Within(1e-12));
        }

        [Test]
        public void TinySegmentsSurviveAndZeroLengthBecomesTwoEqualPoints()
        {
            List<Polyline> l = Flatten.Run(PathData.Parse("M9 12.5h.01"), Affine.Scale(14f / 24f));
            Assert.That(l[0].Points.Count, Is.EqualTo(2));
            Assert.That(l[0].Length, Is.EqualTo(0.01f * 14f / 24f).Within(1e-6f));

            List<Polyline> z = Flatten.Run(PathData.Parse("M5 5L5 5"), Affine.Identity);
            Assert.That(z[0].Points.Count, Is.EqualTo(2));
            Assert.That(z[0].Length, Is.EqualTo(0f));

            Assert.That(Flatten.Run(PathData.Parse("M5 5"), Affine.Identity).Count, Is.EqualTo(0), "a lone moveto draws nothing");

            // "M x y Z" is a zero-length subpath (SVG 2): round caps draw it as a dot, a fill draws nothing
            List<Polyline> mz = Flatten.Run(PathData.Parse("M5 5ZM20 20"), Affine.Translate(1, 0));
            Assert.That(mz.Count, Is.EqualTo(1));
            Assert.That(mz[0].Points, Is.EqualTo(new[] { new Vec2(6, 5), new Vec2(6, 5) }));
            var dot = new MeshBuilder();
            Stroker.Stroke(mz[0], new StrokeStyle(4f, Cap.Round), Affine.Identity, 0f, dot, new SolidPaint(Rgba.White));
            Assert.That(dot.AlphaArea(), Is.EqualTo(Math.PI * 4).Within(0.01));
            var none = new MeshBuilder();
            Triangulate.Fill(mz[0], none, new SolidPaint(Rgba.White));
            Assert.That(none.TriangleCount, Is.EqualTo(0));
        }

        [Test]
        public void FlattenAppliesTheTransform()
        {
            Affine m = Affine.ViewBox(new RectF(0, 0, 24, 24), 48, 48).Then(Affine.Translate(100, 0));
            List<Polyline> l = Flatten.Run("M5 12h14", m);
            Assert.That(l[0].Points[0].X, Is.EqualTo(110f).Within(1e-4f));
            Assert.That(l[0].Points[1].X, Is.EqualTo(138f).Within(1e-4f));
            Assert.That(l[0].Points[1].Y, Is.EqualTo(24f).Within(1e-4f));
            Assert.That(Flatten.MaxScale(Affine.Scale(2, 5)), Is.EqualTo(5f).Within(1e-5f));
            Assert.That(Flatten.MaxScale(Affine.RotateCss(30).Then(Affine.Scale(3))), Is.EqualTo(3f).Within(1e-5f));
        }
    }
}
