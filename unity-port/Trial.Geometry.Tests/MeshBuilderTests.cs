using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Trial.Geometry;

namespace Trial.Geometry.Tests
{
    public class MeshBuilderTests
    {
        /// <summary>A w×h rectangle at (x, y) as two triangles, colours per corner (TL, TR, BR, BL).</summary>
        private static MeshBuilder Rect(float x, float y, float w, float h, Rgba? tl = null, Rgba? tr = null, Rgba? br = null, Rgba? bl = null)
        {
            var mb = new MeshBuilder();
            int a = mb.AddVertex(new Vec2(x, y), tl ?? Rgba.White);
            int b = mb.AddVertex(new Vec2(x + w, y), tr ?? Rgba.White);
            int c = mb.AddVertex(new Vec2(x + w, y + h), br ?? Rgba.White);
            int d = mb.AddVertex(new Vec2(x, y + h), bl ?? Rgba.White);
            mb.AddTri(a, b, c);
            mb.AddTri(a, c, d);
            return mb;
        }

        private static List<Vec2> Poly(params float[] xy)
        {
            var o = new List<Vec2>();
            for (int i = 0; i + 1 < xy.Length; i += 2) o.Add(new Vec2(xy[i], xy[i + 1]));
            return o;
        }

        /// <summary>The colour the mesh shows at p (barycentric over the triangle containing it), or null.</summary>
        private static Rgba? Sample(MeshBuilder mb, Vec2 p)
        {
            for (int t = 0; t < mb.Idx.Count; t += 3)
            {
                Vec2 a = mb.Pos[mb.Idx[t]], b = mb.Pos[mb.Idx[t + 1]], c = mb.Pos[mb.Idx[t + 2]];
                float d = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
                if (Math.Abs(d) < 1e-9f) continue;
                float l1 = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / d;
                float l2 = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / d;
                float l3 = 1 - l1 - l2;
                if (l1 < -1e-5f || l2 < -1e-5f || l3 < -1e-5f) continue;
                Rgba ca = mb.Col[mb.Idx[t]], cb = mb.Col[mb.Idx[t + 1]], cc = mb.Col[mb.Idx[t + 2]];
                return new Rgba(ca.R * l1 + cb.R * l2 + cc.R * l3, ca.G * l1 + cb.G * l2 + cc.G * l3,
                    ca.B * l1 + cb.B * l2 + cc.B * l3, ca.A * l1 + cb.A * l2 + cc.A * l3);
            }
            return null;
        }

        [Test]
        public void ClipConvexKeepsTheIntersection()
        {
            MeshBuilder mb = Rect(0, 0, 10, 10);
            mb.ClipConvex(Poly(5, 5, 15, 5, 15, 15, 5, 15));
            Assert.That(mb.Area(), Is.EqualTo(25f).Within(1e-4f));
            Assert.That(mb.Bounds(), Is.EqualTo(new RectF(5, 5, 5, 5)));

            // either winding, a diamond
            foreach (bool reverse in new[] { false, true })
            {
                MeshBuilder m2 = Rect(0, 0, 10, 10);
                List<Vec2> diamond = Poly(5, 0, 10, 5, 5, 10, 0, 5);
                if (reverse) diamond.Reverse();
                m2.ClipConvex(diamond);
                Assert.That(m2.Area(), Is.EqualTo(50f).Within(1e-4f));
            }

            // fully inside is kept as it is, fully outside goes
            MeshBuilder m3 = Rect(2, 2, 2, 2);
            m3.ClipConvex(Poly(0, 0, 10, 0, 10, 10, 0, 10));
            Assert.That(m3.TriangleCount, Is.EqualTo(2));
            MeshBuilder m4 = Rect(20, 20, 2, 2);
            m4.ClipConvex(Poly(0, 0, 10, 0, 10, 10, 0, 10));
            Assert.That(m4.TriangleCount, Is.EqualTo(0));
        }

        [Test]
        public void ClippingKeepsTheColourField()
        {
            var red = new Rgba(1, 0, 0, 1);
            var blue = new Rgba(0, 0, 1, 0.5f);
            MeshBuilder mb = Rect(0, 0, 10, 10, red, blue, blue, red);
            Rgba before = Sample(mb, new Vec2(7, 3)).Value;
            mb.ClipConvex(Poly(5, 0, 10, 5, 5, 10, 0, 5));
            Rgba after = Sample(mb, new Vec2(7, 3)).Value;
            Assert.That(after.Approximately(before, 1e-4f), Is.True, before + " vs " + after);
        }

        [Test]
        public void SubtractConvexKeepsTheOutside()
        {
            MeshBuilder a = Rect(0, 0, 10, 10);
            a.SubtractConvex(Poly(5, 5, 15, 5, 15, 15, 5, 15));
            Assert.That(a.Area(), Is.EqualTo(75f).Within(1e-4f));

            MeshBuilder b = Rect(0, 0, 10, 10);
            b.SubtractConvex(Poly(2, 2, 8, 2, 8, 8, 2, 8));
            Assert.That(b.Area(), Is.EqualTo(64f).Within(1e-4f));
            Assert.That(Sample(b, new Vec2(5, 5)), Is.Null, "the hole is empty");
            Assert.That(Sample(b, new Vec2(1, 5)), Is.Not.Null);

            MeshBuilder c = Rect(0, 0, 10, 10);
            c.SubtractConvex(Poly(-1, -1, 11, -1, 11, 11, -1, 11));
            Assert.That(c.TriangleCount, Is.EqualTo(0));

            MeshBuilder d = Rect(0, 0, 10, 10);
            d.SubtractConvex(Poly(20, 20, 30, 20, 30, 30));
            Assert.That(d.TriangleCount, Is.EqualTo(2), "untouched triangles stay whole");
        }

        [Test]
        public void SplitByLinesCutsWithoutChangingTheColours()
        {
            var c0 = new Rgba(1, 0, 0, 1);
            var c1 = new Rgba(0, 1, 0, 0.2f);
            MeshBuilder mb = Rect(0, 0, 10, 10, c0, c1, c1, c0);
            var probes = new[] { new Vec2(1, 1), new Vec2(3.3f, 7), new Vec2(6, 2), new Vec2(9, 9) };
            Rgba[] before = probes.Select(p => Sample(mb, p).Value).ToArray();
            mb.SplitByLines(new[] { new IsoLine(new Vec2(1, 0), 3f), new IsoLine(new Vec2(1, 0), 7f), IsoLine.Through(new Vec2(5, 5), new Vec2(1, 1)) });
            Assert.That(mb.Area(), Is.EqualTo(100f).Within(1e-3f));
            Assert.That(mb.TriangleCount, Is.GreaterThan(6));
            for (int i = 0; i < probes.Length; i++)
                Assert.That(Sample(mb, probes[i]).Value.Approximately(before[i], 1e-4f), Is.True, probes[i].ToString());
            // no triangle straddles x = 3 or x = 7
            for (int t = 0; t < mb.Idx.Count; t += 3)
            {
                float[] xs = { mb.Pos[mb.Idx[t]].X, mb.Pos[mb.Idx[t + 1]].X, mb.Pos[mb.Idx[t + 2]].X };
                foreach (float cut in new[] { 3f, 7f })
                    Assert.That(xs.Min() >= cut - 1e-4f || xs.Max() <= cut + 1e-4f, Is.True);
            }
        }

        [Test]
        public void RefineBoundsEveryEdge()
        {
            MeshBuilder mb = Rect(0, 0, 100, 3);
            mb.Refine(6f);
            Assert.That(mb.Area(), Is.EqualTo(300f).Within(1e-2f));
            for (int t = 0; t < mb.Idx.Count; t += 3)
                for (int e = 0; e < 3; e++)
                    Assert.That(Vec2.Dist(mb.Pos[mb.Idx[t + e]], mb.Pos[mb.Idx[t + (e + 1) % 3]]), Is.LessThanOrEqualTo(6f + 1e-4f));
        }

        [Test]
        public void AlphaRampsMultiplyExactly()
        {
            MeshBuilder mb = Rect(0, 0, 100, 10);
            mb.ApplyAlphaRamp(false, new List<(float at, float alpha)> { (0f, 0f), (20f, 1f), (80f, 1f), (100f, 0f) });
            Assert.That(mb.AlphaArea(), Is.EqualTo(10f * (10f + 60f + 10f)).Within(1e-2f));
            Assert.That(Sample(mb, new Vec2(10, 5)).Value.A, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(Sample(mb, new Vec2(50, 5)).Value.A, Is.EqualTo(1f).Within(1e-4f));

            MeshBuilder v = Rect(0, 0, 10, 100, Rgba.White.WithAlpha(0.5f), Rgba.White.WithAlpha(0.5f), Rgba.White.WithAlpha(0.5f), Rgba.White.WithAlpha(0.5f));
            // mask-image: linear-gradient(to bottom, #000 70%, transparent) → ramp in px, unsorted input is fine
            v.ApplyAlphaRamp(true, new List<(float at, float alpha)> { (100f, 0f), (70f, 1f) });
            Assert.That(v.AlphaArea(), Is.EqualTo(0.5f * 10f * (70f + 15f)).Within(1e-2f));
            Assert.That(Sample(v, new Vec2(5, 10)).Value.A, Is.EqualTo(0.5f).Within(1e-4f), "before the first stop the first alpha holds");
        }

        [Test]
        public void AHardStepInARampHasBothValuesOnItsLine()
        {
            MeshBuilder mb = Rect(0, 0, 100, 10);
            mb.ApplyAlphaRamp(false, new List<(float at, float alpha)> { (50f, 0f), (50f, 1f) });
            Assert.That(mb.AlphaArea(), Is.EqualTo(500f).Within(1e-2f));
            Assert.That(Sample(mb, new Vec2(49.9f, 5)).Value.A, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Sample(mb, new Vec2(50.1f, 5)).Value.A, Is.EqualTo(1f).Within(1e-4f));
        }

        private sealed class HardStop : Paint
        {
            public override Rgba At(Vec2 p) => p.X < 5f ? new Rgba(1, 0, 0, 1) : new Rgba(0, 0, 1, 1);
            public override IEnumerable<IsoLine> Bands(RectF bounds) => new[] { new IsoLine(new Vec2(1, 0), 5f) };
        }

        [Test]
        public void PaintsWithAHardStopGiveEachSideItsColour()
        {
            var mb = new MeshBuilder();
            Triangulate.Fill(new Polyline(Poly(0, 0, 10, 0, 10, 10, 0, 10), true), mb, new HardStop());
            Rgba left = Sample(mb, new Vec2(4.99f, 5)).Value, right = Sample(mb, new Vec2(5.01f, 5)).Value;
            Assert.That(left.R, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(right.B, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void FilterAndTransform()
        {
            MeshBuilder mb = Rect(0, 0, 10, 10, new Rgba(0, 1, 0, 0.5f));
            mb.ApplyFilter(new ColorFilter(1f, 1f));
            Assert.That(mb.Col[0].R, Is.EqualTo(0.7152f).Within(1e-5f));
            Assert.That(mb.Col[0].A, Is.EqualTo(0.5f));
            mb.Transform(Affine.Translate(5, 0).Then(Affine.Scale(2)));
            Assert.That(mb.Bounds(), Is.EqualTo(new RectF(10, 0, 20, 20)));
        }

        [Test]
        public void AppendOffsetsIndices()
        {
            MeshBuilder a = Rect(0, 0, 1, 1);
            MeshBuilder b = Rect(5, 5, 1, 1);
            a.Append(b);
            Assert.That(a.VertexCount, Is.EqualTo(8));
            Assert.That(a.Idx.Skip(6).Min(), Is.EqualTo(4));
            Assert.That(a.Area(), Is.EqualTo(2f).Within(1e-5f));
        }

        private sealed class XRamp : Paint
        {
            // a linear "gradient" along x with a kink at x = 5: alpha 0 → 1 → 1
            public override Rgba At(Vec2 p) => Rgba.White.WithAlpha(Math.Min(1f, Math.Max(0f, p.X / 5f)));
            public override IEnumerable<IsoLine> Bands(RectF bounds) => new[] { new IsoLine(new Vec2(1, 0), 5f) };
        }

        private sealed class Radial : Paint
        {
            public override Rgba At(Vec2 p) => Rgba.White.WithAlpha(Math.Max(0f, 1f - Vec2.Dist(p, new Vec2(50, 50)) / 50f));
            public override bool NeedsRefine => true;
        }

        [Test]
        public void PaintsAreSplitAlongTheirBandsAndRefinedWhenRadial()
        {
            var mb = new MeshBuilder();
            Triangulate.Fill(new Polyline(Poly(0, 0, 10, 0, 10, 10, 0, 10), true), mb, new XRamp());
            Assert.That(Sample(mb, new Vec2(2.5f, 5)).Value.A, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(Sample(mb, new Vec2(7.5f, 5)).Value.A, Is.EqualTo(1f).Within(1e-4f));

            var r = new MeshBuilder();
            Triangulate.Fill(new Polyline(Poly(0, 0, 100, 0, 100, 100, 0, 100), true), r, new Radial());
            Assert.That(r.TriangleCount, Is.GreaterThan(200));
            Assert.That(Sample(r, new Vec2(50, 50)).Value.A, Is.EqualTo(1f).Within(0.08f));
        }
    }
}
