using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Trial.Geometry;

namespace Trial.Geometry.Tests
{
    /// <summary>Every shipped path: parses, flattens, (fills) is simple and triangulates to its own area, (strokes) strokes.</summary>
    public class DataPathsTests
    {
        public sealed class Shipped
        {
            public string Name, D;
            public RectF ViewBox;
            public bool Filled;
            public override string ToString() => Name;
        }

        public static IEnumerable<Shipped> All()
        {
            foreach (string n in DsIcons.Names)
                yield return new Shipped { Name = "ds:" + n, D = DsIcons.Paths[n], ViewBox = IconDef.Box24, Filled = DsIcons.IsFilled(n) };
            // the DS shield is also drawn filled (opts.fill guard at .2: bundle.js:387, hud/util.js:103)
            yield return new Shipped { Name = "ds:shield(fill)", D = DsIcons.Paths["shield"], ViewBox = IconDef.Box24, Filled = true };
            foreach (string n in ExploreIcons.Names)
                yield return new Shipped { Name = "explore:" + n, D = ExploreIcons.Paths[n], ViewBox = IconDef.Box24, Filled = ExploreIcons.IsFilled(n) };
            foreach (string n in ExtraIcons.Names)
            {
                IconDef d = ExtraIcons.Get(n);
                yield return new Shipped { Name = "extra:" + n, D = d.D, ViewBox = d.ViewBox, Filled = d.Filled };
            }
            foreach (FigureDef f in DsFigures.Figures)
                for (int i = 0; i < f.Paths.Count; i++)
                    yield return new Shipped { Name = "figure:" + f.Name + "#" + i, D = f.Paths[i], ViewBox = f.ViewBox, Filled = true };
        }

        public static IEnumerable<Shipped> Fills() => All().Where(s => s.Filled);

        [TestCaseSource(nameof(All))]
        public void Parses(Shipped s)
        {
            PathData p = PathData.Parse(s.D);
            Assert.That(p.Error, Is.Null, s.Name);
            Assert.That(p.SubPaths.Count, Is.GreaterThan(0));
            Assert.That(p.SubPaths.All(sp => sp.Segments.Count > 0), Is.True, "no lone movetos");
            RectF b = p.ControlBounds();
            Assert.That(b.Left, Is.GreaterThanOrEqualTo(s.ViewBox.Left - 1f), "inside its viewBox");
            Assert.That(b.Right, Is.LessThanOrEqualTo(s.ViewBox.Right + 1f), "inside its viewBox");
            Assert.That(b.Top, Is.GreaterThanOrEqualTo(s.ViewBox.Top - 1f), "inside its viewBox");
            Assert.That(b.Bottom, Is.LessThanOrEqualTo(s.ViewBox.Bottom + 1f), "inside its viewBox");
        }

        [TestCaseSource(nameof(All))]
        public void Flattens(Shipped s)
        {
            foreach (float size in new[] { 16f, 24f, 160f })
            {
                Affine m = Affine.ViewBox(s.ViewBox, size, size);
                List<Polyline> lines = Flatten.Run(PathData.Parse(s.D), m, 0.2f);
                Assert.That(lines.Count, Is.EqualTo(PathData.Parse(s.D).SubPaths.Count), s.Name);
                foreach (Polyline l in lines)
                {
                    Assert.That(l.Points.Count, Is.GreaterThanOrEqualTo(2));
                    foreach (Vec2 v in l.Points) Assert.That(float.IsFinite(v.X) && float.IsFinite(v.Y), Is.True);
                    for (int i = 1; i < l.Points.Count; i++)
                        Assert.That(Vec2.Dist(l.Points[i - 1], l.Points[i]), Is.GreaterThan(0f).Or.EqualTo(0f));
                }
            }
        }

        [TestCaseSource(nameof(Fills))]
        public void FilledSubpathsAreSimpleAndFillTheirArea(Shipped s)
        {
            foreach (float scale in new[] { 0.5f, 1f, 3f })
            {
                foreach (Polyline ring in Flatten.Run(PathData.Parse(s.D), Affine.Scale(scale), 0.2f))
                {
                    Assert.That(Triangulate.IsSimple(ring), Is.True, s.Name + " at " + scale);
                    var mb = new MeshBuilder();
                    Triangulate.Fill(ring, mb, new SolidPaint(Rgba.White));
                    Assert.That(mb.Area(), Is.EqualTo(Math.Abs(ring.SignedArea)).Within(Math.Abs(ring.SignedArea) * 1e-4 + 1e-3), s.Name + " area");
                }
            }
        }

        [TestCaseSource(nameof(All))]
        public void Strokes(Shipped s)
        {
            Affine m = Affine.ViewBox(s.ViewBox, 24, 24);
            var style = new StrokeStyle(IconDef.DefaultStrokeWidth, Cap.Round, Join.Round);
            var mb = new MeshBuilder();
            foreach (Polyline l in Flatten.Run(PathData.Parse(s.D), m, 0.2f))
                Stroker.Stroke(l, style, m, 1f, mb, new SolidPaint(Rgba.White));
            Assert.That(mb.TriangleCount, Is.GreaterThan(0), s.Name);
            foreach (Vec2 v in mb.Pos) Assert.That(float.IsFinite(v.X) && float.IsFinite(v.Y), Is.True, s.Name);
            foreach (Rgba c in mb.Col) Assert.That(c.A >= 0f && c.A <= 1f, Is.True);
            RectF b = mb.Bounds();
            Assert.That(b.Left, Is.GreaterThan(-4f));
            Assert.That(b.Right, Is.LessThan(28f));
        }

        [Test]
        public void IconMeshFollowsIconsRules()
        {
            Rgba crimson = Rgba.Hex(0xc0392b);
            var heart = new MeshBuilder();
            IconMesh.Build(DsIcons.Get("heart"), 24, crimson, 1f, heart, fill: Rgba.White, width: 5f, dashed: true);
            Assert.That(heart.Col.All(c => c.R == crimson.R && c.G == crimson.G && c.B == crimson.B), Is.True, "filled icons ignore the options");
            RectF hb = heart.Bounds();
            Assert.That(hb.Left, Is.EqualTo(4f - 0.5f).Within(0.05f), "the fill's fringe (f/2 out), no stroke");

            var plus = new MeshBuilder();
            IconMesh.Build(DsIcons.Get("plus"), 48, Rgba.White, 1f, plus);
            RectF pb = plus.Bounds();
            // M5 12h14 at 2× with a 4.4 px round-capped stroke: x from 10 − 2.2 − ½ to 38 + 2.2 + ½
            Assert.That(pb.Left, Is.EqualTo(10f - 2.2f - 0.5f).Within(0.15f));
            Assert.That(pb.Right, Is.EqualTo(38f + 2.2f + 0.5f).Within(0.15f));

            var guard = Rgba.Hex(0x4f8fbf);
            var shield = new MeshBuilder();
            IconMesh.Build(DsIcons.Get("shield"), 30, guard, 1f, shield, fill: guard.WithAlpha(0.2f));
            Assert.That(shield.Col.Any(c => System.Math.Abs(c.A - 0.2f) < 1e-6f), Is.True, "fill-opacity .2 under the stroke");
            Assert.That(shield.Col.Any(c => c.A == 1f), Is.True);
        }

        [Test]
        public void FigureMeshPaintsRimThenFill()
        {
            FigureDef f = DsFigures.Get("hound");
            var mb = new MeshBuilder();
            IconMesh.Figure(f, 109f, Rgba.Hex(0x1a1528), f.DefaultRim, 1f, mb);
            RectF b = mb.Bounds();
            float scale = 109f / f.H;
            Assert.That(b.Bottom, Is.LessThanOrEqualTo(109f + 2f * scale + 1.5f));
            Assert.That(b.Right, Is.LessThanOrEqualTo(f.W * scale + 2f * scale + 1.5f));
            // the last vertices are the fill group's (drawn over the rim)
            Assert.That(mb.Col[mb.Col.Count - 1].R, Is.EqualTo(Rgba.Hex(0x1a1528).R));
            Assert.That(mb.Col[0].A, Is.LessThanOrEqualTo(0.35f + 1e-6f));
        }

        [Test]
        public void TheShieldIconFillsAndStrokesDashed()
        {
            IconDef d = DsIcons.Get("shield");
            Affine m = d.ToBox(28);
            var mb = new MeshBuilder();
            foreach (Polyline l in Flatten.Run(d.Path, m))
            {
                Triangulate.FillAA(l, mb, new SolidPaint(Rgba.Bytes(70, 130, 180, 0.2f)), 1f);
                Stroker.Stroke(l, d.Style(dashed: true), m, 1f, mb, new SolidPaint(Rgba.Bytes(70, 130, 180)));
            }
            Assert.That(mb.TriangleCount, Is.GreaterThan(50));
        }
    }
}
