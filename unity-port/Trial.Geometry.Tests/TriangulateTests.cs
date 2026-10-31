using System;
using System.Collections.Generic;
using NUnit.Framework;
using Trial.Geometry;

namespace Trial.Geometry.Tests
{
    public class TriangulateTests
    {
        private static Polyline Ring(params float[] xy)
        {
            var pts = new List<Vec2>();
            for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Vec2(xy[i], xy[i + 1]));
            return new Polyline(pts, true);
        }

        [Test]
        public void ConvexAndConcaveRingsFillTheirAreaInEitherWinding()
        {
            Polyline square = Ring(0, 0, 10, 0, 10, 10, 0, 10);
            Polyline l = Ring(0, 0, 10, 0, 10, 4, 4, 4, 4, 10, 0, 10);       // an L, area 64
            Polyline star = Ring(12, 3, 14, 10, 21, 12, 14, 14, 12, 21, 10, 14, 3, 12, 10, 10); // the star icon
            foreach (Polyline r in new[] { square, l, star })
            {
                foreach (bool reverse in new[] { false, true })
                {
                    var pts = new List<Vec2>(r.Points);
                    if (reverse) pts.Reverse();
                    var ring = new Polyline(pts, true);
                    var mb = new MeshBuilder();
                    Triangulate.Fill(ring, mb, new SolidPaint(Rgba.White));
                    Assert.That(mb.Area(), Is.EqualTo(Math.Abs(ring.SignedArea)).Within(1e-3));
                    Assert.That(mb.TriangleCount, Is.EqualTo(ring.Points.Count - 2));
                    Assert.That(Triangulate.IsSimple(ring), Is.True);
                }
            }
        }

        [Test]
        public void DuplicateAndCollinearPointsAreDropped()
        {
            Polyline r = Ring(0, 0, 5, 0, 5, 0, 10, 0, 10, 10, 0, 10, 0, 0);
            var mb = new MeshBuilder();
            Triangulate.Fill(r, mb, new SolidPaint(Rgba.White));
            Assert.That(mb.Area(), Is.EqualTo(100f).Within(1e-3f));
            Assert.That(mb.TriangleCount, Is.EqualTo(2));
        }

        [Test]
        public void IsSimpleRejectsCrossingsTouchesAndSpikes()
        {
            Assert.That(Triangulate.IsSimple(Ring(0, 0, 10, 10, 10, 0, 0, 10)), Is.False, "bow tie");
            Assert.That(Triangulate.IsSimple(Ring(0, 0, 10, 0, 10, 10, 5, 0, 0, 10)), Is.False, "a vertex touching an edge");
            Assert.That(Triangulate.IsSimple(Ring(0, 0, 10, 0, 5, 0, 5, 10)), Is.False, "a spike back along an edge");
            Assert.That(Triangulate.IsSimple(Ring(0, 0, 10, 0)), Is.False, "two points");
            Assert.That(Triangulate.IsSimple(Ring(0, 0, 10, 0, 20, 0)), Is.False, "no area");
            Assert.That(Triangulate.IsSimple(Ring(0, 0, 10, 0, 10, 10)), Is.True);
        }

        [Test]
        public void ColoursComeFromThePaint()
        {
            var mb = new MeshBuilder();
            Triangulate.Fill(Ring(0, 0, 10, 0, 10, 10), mb, new SolidPaint(Rgba.Hex(0x8b6fd0, 0.35f)));
            foreach (Rgba c in mb.Col) Assert.That(c, Is.EqualTo(Rgba.Hex(0x8b6fd0, 0.35f)));
        }

        [Test]
        public void FillAAAddsAnOuterFringeFadingToZero()
        {
            var mb = new MeshBuilder();
            Polyline sq = Ring(0, 0, 10, 0, 10, 10, 0, 10);
            Triangulate.FillAA(sq, mb, new SolidPaint(Rgba.White), 1f);
            RectF b = mb.Bounds();
            // the outer half of a ramp centred on the outline: ½ on it, 0 at f/2 outside (the shape does not grow)
            Assert.That(b.Left, Is.EqualTo(-0.5f).Within(1e-4f));
            Assert.That(b.Right, Is.EqualTo(10.5f).Within(1e-4f));
            for (int i = 0; i < mb.Pos.Count; i++)
            {
                Vec2 p = mb.Pos[i];
                bool outside = p.X < -1e-4f || p.X > 10.0001f || p.Y < -1e-4f || p.Y > 10.0001f;
                if (outside) Assert.That(mb.Col[i].A, Is.EqualTo(0f), p.ToString());
                else Assert.That(mb.Col[i].A, Is.EqualTo(1f).Or.EqualTo(0.5f), p.ToString());
            }
            // core 100 + a fringe of mean alpha ¼ and width ½ around the 40 px perimeter (+ the corner wedges)
            Assert.That(mb.AlphaArea(), Is.EqualTo(100f + 5f).Within(0.3f));

            // a 16 px pip (r 4): the painted coverage stays the disc's area (the browser's), not a disc ½ px larger
            var pip = new MeshBuilder();
            foreach (Polyline ring in Flatten.Run(DsIcons.Paths["pip"], DsIcons.Get("pip").ToBox(16f)))
                Triangulate.FillAA(ring, pip, new SolidPaint(Rgba.White), 1f);
            Assert.That(pip.AlphaArea(), Is.EqualTo(Math.PI * 16).Within(Math.PI * 16 * 0.05));

            // concave: the fringe goes outward on every edge (an L in both windings)
            foreach (bool reverse in new[] { false, true })
            {
                var pts = new List<Vec2> { new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 4), new Vec2(4, 4), new Vec2(4, 10), new Vec2(0, 10) };
                if (reverse) pts.Reverse();
                var m2 = new MeshBuilder();
                Triangulate.FillAA(new Polyline(pts, true), m2, new SolidPaint(Rgba.White), 1f);
                for (int i = 0; i < m2.Pos.Count; i++)
                {
                    if (m2.Col[i].A > 0f) continue;
                    Vec2 p = m2.Pos[i];
                    bool insideL = (p.X > 0.001f && p.X < 9.999f && p.Y > 0.001f && p.Y < 3.999f) || (p.X > 0.001f && p.X < 3.999f && p.Y > 0.001f && p.Y < 9.999f);
                    Assert.That(insideL, Is.False, "fringe vertex inside the shape: " + p);
                }
            }
        }
    }
}
