using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Trial.Geometry.Tests
{
    public class ShapesTests
    {
        /// <summary>∫ alpha over the mesh (alpha is linear in each triangle: area × mean vertex alpha).</summary>
        internal static double Coverage(MeshBuilder mb, Func<Rgba, bool> filter = null)
        {
            double sum = 0;
            for (int i = 0; i < mb.Idx.Count; i += 3)
            {
                int a = mb.Idx[i], b = mb.Idx[i + 1], c = mb.Idx[i + 2];
                if (filter != null && !(filter(mb.Col[a]) && filter(mb.Col[b]) && filter(mb.Col[c]))) continue;
                Vec2 pa = mb.Pos[a], pb = mb.Pos[b], pc = mb.Pos[c];
                double area = Math.Abs((pb.X - pa.X) * (double)(pc.Y - pa.Y) - (pc.X - pa.X) * (double)(pb.Y - pa.Y)) * 0.5;
                sum += area * (mb.Col[a].A + mb.Col[b].A + mb.Col[c].A) / 3.0;
            }
            return sum;
        }

        [Test]
        public void Radii_CssClamping()
        {
            CornerSet pill = Radii.Pill.Resolve(200f, 100f);
            Assert.That(pill.TL, Is.EqualTo(new Vec2(100f, 50f)));
            CornerSet big = Radii.All(30f).Resolve(40f, 40f);
            Assert.That(big.TL.X, Is.EqualTo(20f).Within(1e-5f));
            Assert.That(big.BR.Y, Is.EqualTo(20f).Within(1e-5f));
            CornerSet some = Css.Box("border-radius: 0 var(--radius-tag) var(--radius-tag) 0").Radius.Resolve(100f, 40f);
            Assert.That((some.TL.X, some.TR.X, some.BR.X, some.BL.X), Is.EqualTo((0f, 4f, 4f, 0f)));
            CornerSet tb = Css.Box("border-radius: 3px 3px 0 0").Radius.Resolve(10f, 3f);
            // 3 + 0 ≤ 3 on the left side: no clamping
            Assert.That(tb.TL, Is.EqualTo(new Vec2(3f, 3f)));
            CornerSet elliptic = Css.Box("border-radius: 10px / 5px").Radius.Resolve(100f, 100f);
            Assert.That(elliptic.TR, Is.EqualTo(new Vec2(10f, 5f)));
        }

        [Test]
        public void RoundRectPoints_HaveTheSameCountForAnyRadii()
        {
            int segs = 5;
            Assert.That(Shapes.RoundRectPoints(new RectF(0f, 0f, 100f, 40f), CornerSet.All(6f), segs).Count, Is.EqualTo(24));
            Assert.That(Shapes.RoundRectPoints(new RectF(1f, 1f, 98f, 38f), CornerSet.All(0f), segs).Count, Is.EqualTo(24));
            Polyline p = Shapes.RoundRect(new RectF(0f, 0f, 100f, 40f), Radii.Zero);
            Assert.That(p.Points.Count, Is.EqualTo(4));
            Assert.That(p.Closed, Is.True);
            Polyline e = Shapes.Ellipse(new RectF(0f, 0f, 16f, 16f));
            foreach (Vec2 v in e.Points) Assert.That(Vec2.Dist(v, new Vec2(8f, 8f)), Is.EqualTo(8f).Within(1e-3f));
        }

        [Test]
        public void FillConvexAA_CoversTheShapeArea()
        {
            var mb = new MeshBuilder();
            Shapes.FillConvexAA(Shapes.RoundRect(new RectF(10f, 10f, 100f, 40f), Radii.Zero), Paint.Solid(DsTokens.Ink), 1f, mb);
            Assert.That(Coverage(mb), Is.EqualTo(4000.0).Within(4000.0 * 0.002));
            var mbr = new MeshBuilder();
            Shapes.FillConvexAA(Shapes.RoundRect(new RectF(0f, 0f, 100f, 40f), Radii.All(6f)), Paint.Solid(DsTokens.Ink), 1.5f, mbr);
            double rounded = 4000.0 - 4 * (36.0 - Math.PI * 9.0);
            Assert.That(Coverage(mbr), Is.EqualTo(rounded).Within(rounded * 0.003));
            // the fringe is alpha 0 outside, 1 inside
            Assert.That(mbr.Col.Exists(c => c.A == 0f), Is.True);
            Assert.That(mbr.Col.Exists(c => c.A == 1f), Is.True);
        }

        [Test]
        public void Ring_IsTheBandBetweenTheOutlines()
        {
            var outer = new Polyline(Shapes.RoundRectPoints(new RectF(0f, 0f, 100f, 40f), CornerSet.Zero, 2), true);
            var inner = new Polyline(Shapes.RoundRectPoints(new RectF(2f, 2f, 96f, 36f), CornerSet.Zero, 2), true);
            var mb = new MeshBuilder();
            Shapes.Ring(outer, inner, Paint.Solid(DsTokens.Steel), 1f, mb);
            Assert.That(Coverage(mb), Is.EqualTo(4000.0 - 96 * 36).Within(3.0));
            // thinner than the feather: coverage keeps the area
            var thinInner = new Polyline(Shapes.RoundRectPoints(new RectF(0.5f, 0.5f, 99f, 39f), CornerSet.Zero, 2), true);
            var thin = new MeshBuilder();
            Shapes.Ring(outer, thinInner, Paint.Solid(DsTokens.Steel), 1.5f, thin);
            Assert.That(Coverage(thin), Is.EqualTo(4000.0 - 99 * 39).Within(3.0));
            Assert.Throws<ArgumentException>(() => Shapes.Ring(outer, new Polyline(new List<Vec2> { Vec2.Zero }, true), Paint.Solid(Rgba.White), 1f, new MeshBuilder()));
        }

        [Test]
        public void LinearGradientFill_IsSplitAtTheStops_AndExactAtTheVertices()
        {
            var box = new RectF(0f, 0f, 200f, 132f);
            // bundle.css:155 the banner
            Paint p = Css.Background("linear-gradient(90deg, rgba(10,12,18,0), rgba(10,12,18,.8) 28%, rgba(10,12,18,.8) 72%, rgba(10,12,18,0))", box);
            var mb = new MeshBuilder();
            Shapes.FillConvex(Shapes.RoundRect(box, Radii.Zero), p, mb);
            Assert.That(mb.Pos.Exists(v => Math.Abs(v.X - 56f) < 1e-3f), Is.True);
            Assert.That(mb.Pos.Exists(v => Math.Abs(v.X - 144f) < 1e-3f), Is.True);
            for (int i = 0; i < mb.Pos.Count; i++)
                Assert.That(mb.Col[i].Approximately(p.At(mb.Pos[i]), 1e-4f), Is.True);
            // ∫ alpha: the two ramps average .4, the middle .8
            Assert.That(Coverage(mb), Is.EqualTo(132.0 * (56 * 0.4 + 88 * 0.8 + 56 * 0.4)).Within(1.0));
        }

        [Test]
        public void RadialFill_FollowsTheEllipse()
        {
            var box = new RectF(0f, 0f, 200f, 100f);
            Paint p = Css.Background("radial-gradient(closest-side, rgba(255,216,154,.55), rgba(0,0,0,0))", box);
            var mb = new MeshBuilder();
            Shapes.FillConvexAA(Shapes.Ellipse(box), p, 1f, mb);
            Assert.That(mb.TriangleCount, Is.GreaterThan(64));
            // ∫ α over an ellipse with α = .55 (1 − ρ): .55 · π·a·b / 3
            double expect = 0.55 * Math.PI * 100 * 50 / 3.0;
            Assert.That(Coverage(mb), Is.EqualTo(expect).Within(expect * 0.03));
        }
    }
}
