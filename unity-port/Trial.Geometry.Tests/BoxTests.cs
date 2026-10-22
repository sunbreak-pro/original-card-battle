using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Trial.Geometry.Tests
{
    public class BoxTests
    {
        private static MeshBuilder Build(RectF box, string css, float feather = 0f, CssVars vars = null)
        {
            var mb = new MeshBuilder();
            BoxMesh.Build(box, Css.Box(css, DsTheme.Dusk, vars), feather, mb);
            return mb;
        }

        [Test]
        public void OuterShadow_AlphaOnTheEdge_IsHalfTheColourAlpha()
        {
            var box = new RectF(0f, 0f, 200f, 200f);
            MeshBuilder mb = Build(box, "box-shadow: 0 0 20px rgba(0,0,0,.6)");
            // vertices on the border-box edge away from the corners (σ = 10: more than 3σ in)
            var onEdge = Enumerable.Range(0, mb.VertexCount)
                .Where(i => Math.Abs(mb.Pos[i].X - 200f) < 1e-3f && mb.Pos[i].Y > 29.9f && mb.Pos[i].Y < 170.1f).ToList();
            Assert.That(onEdge.Count, Is.GreaterThan(0));
            foreach (int i in onEdge) Assert.That(mb.Col[i].A, Is.EqualTo(0.3f).Within(0.003f));
            // σ = blur / 2: one σ out the alpha is a · ½·erfc(1/√2) = a · 0.1587
            var oneSigma = Enumerable.Range(0, mb.VertexCount)
                .Where(i => Math.Abs(mb.Pos[i].X - 210f) < 1e-3f && mb.Pos[i].Y > 29.9f && mb.Pos[i].Y < 170.1f).ToList();
            Assert.That(oneSigma.Count, Is.GreaterThan(0));
            foreach (int i in oneSigma) Assert.That(mb.Col[i].A, Is.EqualTo(0.6f * 0.158655f).Within(0.002f));
            // along the edge the alpha holds the plateau (no vertex sits mid-edge; the two 3σ points carry it)
            Assert.That(onEdge.Count(i => mb.Pos[i].Y > 100f) > 0 && onEdge.Count(i => mb.Pos[i].Y < 100f) > 0, Is.True);
        }

        [Test]
        public void OuterShadow_IsNotPaintedInsideTheBorderBox()
        {
            var box = new RectF(0f, 0f, 120f, 80f);
            MeshBuilder mb = Build(box, "border-radius: 6px; box-shadow: var(--shadow-card), var(--shadow-lamp)");
            for (int i = 0; i < mb.Idx.Count; i += 3)
            {
                Vec2 c = (mb.Pos[mb.Idx[i]] + mb.Pos[mb.Idx[i + 1]] + mb.Pos[mb.Idx[i + 2]]) / 3f;
                bool inside = c.X > 6f && c.X < 114f && c.Y > 6f && c.Y < 74f;
                Assert.That(inside, Is.False, "a shadow triangle inside the box at " + c);
            }
            // the lamp's spread ring: a solid 2 px band outside the box (0 0 0 2px #ffd89a)
            Assert.That(mb.Col.Any(col => col.Approximately(DsTokens.Wick, 1e-3f)), Is.True);
        }

        [Test]
        public void InsetBar_IsAStripOnTheLeftInnerEdge()
        {
            // explore.css:309 the trace row's cursor: box-shadow: inset 3px 0 0 var(--amber)
            var box = new RectF(0f, 0f, 400f, 52f);
            MeshBuilder mb = Build(box, "box-shadow: inset 3px 0 0 var(--amber)");
            Assert.That(ShapesTests.Coverage(mb), Is.EqualTo(3.0 * 52.0).Within(0.5));
            Assert.That(mb.Pos.Max(p => p.X), Is.LessThanOrEqualTo(3.01f));
            Assert.That(mb.Col.Where(c => c.A > 0f).All(c => c.WithAlpha(1f).Approximately(DsTokens.Amber, 1e-3f)), Is.True);
        }

        [Test]
        public void InsetRing_FromSpread()
        {
            // field.css:24 inset 0 0 0 2px panel-solid: a 2 px ring inside the padding box
            var box = new RectF(0f, 0f, 100f, 40f);
            MeshBuilder mb = Build(box, "box-shadow: inset 0 0 0 2px var(--panel-solid)");
            Assert.That(ShapesTests.Coverage(mb), Is.EqualTo(4000.0 - 96 * 36).Within(1.0));
        }

        [Test]
        public void BlurredInset_FadesInward()
        {
            var box = new RectF(0f, 0f, 600f, 400f);
            MeshBuilder mb = Build(box, "box-shadow: inset 0 0 90px rgba(29,26,22,.14)");
            var nearEdge = Enumerable.Range(0, mb.VertexCount).Where(i => Math.Abs(mb.Pos[i].X - 0f) < 1e-3f && mb.Pos[i].Y > 150f && mb.Pos[i].Y < 250f);
            foreach (int i in nearEdge) Assert.That(mb.Col[i].A, Is.EqualTo(0.07f).Within(0.002f)); // Φ(0) = ½ at the edge
            Assert.That(mb.Col.Max(c => c.A), Is.LessThanOrEqualTo(0.14f + 1e-4f));
        }

        [Test]
        public void DashGap_BlinkSelectBestDashGap()
        {
            BoxMesh.DashPattern(BorderStyle.Dashed, 1f, out float d1, out float g1);
            Assert.That((d1, g1), Is.EqualTo((3f, 2f)));
            BoxMesh.DashPattern(BorderStyle.Dashed, 2f, out float d2, out float g2);
            Assert.That((d2, g2), Is.EqualTo((6f, 4f)));
            BoxMesh.DashPattern(BorderStyle.Dashed, 3f, out float d3, out float g3);
            Assert.That((d3, g3), Is.EqualTo((6f, 3f)));
            BoxMesh.DashPattern(BorderStyle.Dotted, 1f, out float d4, out float g4);
            Assert.That((d4, g4), Is.EqualTo((1f, 1f)));
            // an open 100 px side, 1 px dashed: 20 dashes, gaps stretched to 40/19, a dash at both ends
            List<(float from, float to)> dashes = BoxMesh.DashIntervals(100f, 3f, 2f, false);
            Assert.That(dashes.Count, Is.EqualTo(20));
            Assert.That(dashes[0].from, Is.EqualTo(0f));
            Assert.That(dashes[19].to, Is.EqualTo(100f).Within(1e-3f));
            Assert.That(dashes[1].from - dashes[0].to, Is.EqualTo(40f / 19f).Within(1e-4f));
            // a 16 px circle with a 2 px dashed border (centre radius 7) shows 4 dashes (kit.md §3.2)
            Assert.That(BoxMesh.DashIntervals((float)(2 * Math.PI * 7), 6f, 4f, true).Count, Is.EqualTo(4));
            // too short for two dashes: solid
            Assert.That(BoxMesh.DashIntervals(10f, 6f, 4f, false), Is.EqualTo(new List<(float, float)> { (0f, 10f) }));
        }

        [Test]
        public void DashedBorder_StartsAndEndsOnADashAtTheCorners()
        {
            var box = new RectF(0f, 0f, 100f, 40f);
            MeshBuilder mb = Build(box, "border: 1px dashed var(--steel)");
            var top = mb.Pos.Where(p => p.Y < 1.0001f && p.Y > -0.0001f).ToList();
            Assert.That(top.Min(p => p.X), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(top.Max(p => p.X), Is.EqualTo(100f).Within(1e-3f));
            // the top side's dashes cover 3/5 of it, give or take the stretched gaps
            double cover = ShapesTests.Coverage(mb);
            double perimeter = 2 * (100 + 40) - 4;
            Assert.That(cover / perimeter, Is.InRange(0.55, 0.66));
            Assert.That(mb.Pos.All(p => p.X >= -1e-3f && p.X <= 100.001f && p.Y >= -1e-3f && p.Y <= 40.001f), Is.True);
        }

        [Test]
        public void DashedRoundedBorder_FollowsTheArc()
        {
            var box = new RectF(0f, 0f, 16f, 16f);
            MeshBuilder mb = Build(box, "border: 2px dashed var(--amber); border-radius: 50%");
            foreach (Vec2 p in mb.Pos)
            {
                float r = Vec2.Dist(p, new Vec2(8f, 8f));
                Assert.That(r, Is.InRange(5.9f, 8.05f)); // chords and miters sit a hair off the arcs (sag < 0.1 px)
            }
            double cover = ShapesTests.Coverage(mb);
            // 4 dashes of 6 px, 2 px wide
            Assert.That(cover, Is.EqualTo(4 * 6 * 2).Within(3.0));
        }

        [Test]
        public void DashedRoundedRect_SpreadsWholeDashesAroundTheLoop()
        {
            // explore.css:138 a far node's token: 72×72, 2 px dashed, radius 6 → one closed path along the band's middle
            var box = new RectF(0f, 0f, 72f, 72f);
            MeshBuilder mb = Build(box, "border: 2px dashed var(--steel); border-radius: var(--radius-panel)");
            double loop = 4 * (70 - 2 * 5) + 2 * Math.PI * 5; // middle line: 70×70 with radius 5
            List<(float from, float to)> dashes = BoxMesh.DashIntervals((float)loop, 6f, 4f, true);
            double gap = dashes[1].from - dashes[0].to;
            Assert.That(gap, Is.InRange(3.0, 5.0)); // the nominal 2w gap, stretched or squeezed to fit whole dashes
            Assert.That(dashes.Count * 6 + dashes.Count * gap, Is.EqualTo(loop).Within(1e-2));
            // the mesh carries those dashes: 2 px wide, 6 px long each
            Assert.That(ShapesTests.Coverage(mb), Is.EqualTo(dashes.Count * 6 * 2).Within(dashes.Count * 0.3));
            // the first dash starts where the top edge leaves the top-left arc
            // (the band turns there by half a polygon step: its corners sit within 0.1 px of x = 6)
            Assert.That(mb.Pos.Any(p => Math.Abs(p.X - 6f) < 0.15f && p.Y < 2.1f && p.Y > -0.1f), Is.True);
        }

        [Test]
        public void ZeroShadow_IsHiddenBehindTheBox()
        {
            // ocb-pip-lit starts at box-shadow: 0 0 0 0 rgba(255,216,154,.9): nothing shows
            MeshBuilder mb = Build(new RectF(0f, 0f, 16f, 16f), "box-shadow: 0 0 0 0 rgba(255,216,154,.9)", 1f);
            Assert.That(mb.VertexCount, Is.EqualTo(0));
            MeshBuilder ring = Build(new RectF(0f, 0f, 16f, 16f), "border-radius: 50%; box-shadow: 0 0 0 4px rgba(255,216,154,.5)");
            double area = Math.PI * (12 * 12 - 8 * 8);
            Assert.That(ShapesTests.Coverage(ring), Is.EqualTo(0.5 * area).Within(0.5 * area * 0.02));
        }

        [Test]
        public void DoubleBorder_IsTwoThirds()
        {
            var box = new RectF(0f, 0f, 40f, 30f);
            MeshBuilder mb = Build(box, "border: 3px double var(--boss)");
            Assert.That(ShapesTests.Coverage(mb), Is.EqualTo((40 * 30 - 38 * 28) + (36 * 26 - 34 * 24)).Within(0.5));
        }

        [Test]
        public void PerSideBorders_SplitAlongTheCornerLines()
        {
            // common.css:18 .ocbg-notice: 1px rule, border-left 3px caution
            var box = new RectF(0f, 0f, 200f, 60f);
            MeshBuilder mb = Build(box, "border: 1px solid #00ff00; border-left: 3px solid #ff0000");
            for (int i = 0; i < mb.VertexCount; i++)
            {
                Rgba c = mb.Col[i];
                if (c.R > 0.5f) Assert.That(mb.Pos[i].X, Is.LessThanOrEqualTo(3.001f));
            }
            double red = ShapesTests.Coverage(mb, c => c.R > 0.5f);
            double green = ShapesTests.Coverage(mb, c => c.G > 0.5f);
            // left: 3 × 60 minus the two corner triangles given away (½·3·1 each)
            Assert.That(red, Is.EqualTo(180 - 3.0).Within(0.6));
            Assert.That(red + green, Is.EqualTo(200 * 60 - 196 * 58).Within(0.6));
        }

        [Test]
        public void Outline_GrowsTheRadiusByOffsetAndWidth()
        {
            // explore.css:97 the boss node: outline 2px solid var(--boss), offset 4px, radius 6
            var box = new RectF(0f, 0f, 100f, 40f);
            MeshBuilder mb = Build(box, "border-radius: var(--radius-panel); outline: 2px solid var(--boss); outline-offset: 4px");
            Assert.That(mb.Pos.Min(p => p.X), Is.EqualTo(-6f).Within(1e-3f));
            Assert.That(mb.Pos.Max(p => p.Y), Is.EqualTo(46f).Within(1e-3f));
            // rounded with radius 12 (= 6 + 4 + 2): the corner's outer points lie on a circle of 12 about (6, 6)
            var corner = mb.Pos.Where(p => p.X < 6f - 1e-3f && p.Y < 6f - 1e-3f).ToList();
            Assert.That(corner.Max(p => Vec2.Dist(p, new Vec2(6f, 6f))), Is.EqualTo(12f).Within(1e-3f));
            Assert.That(mb.Pos.Min(p => Vec2.Dist(p, new Vec2(-6f, -6f))), Is.GreaterThan(4.9f));
            // and nothing inside the offset gap
            Assert.That(mb.Pos.Any(p => p.X > -3.99f && p.X < 0f && p.Y > 10f && p.Y < 30f), Is.False);
        }

        [Test]
        public void Background_UnderTheBorder_ClippedToTheRadius()
        {
            var box = new RectF(0f, 0f, 216f, 304f);
            var vars = new CssVars().Set("--attr", "var(--violet)");
            MeshBuilder mb = Build(box, "border-radius: var(--radius-panel); background: var(--card); border: 2px solid var(--attr)", 1f, vars);
            Assert.That(mb.Col.Any(c => c.Approximately(DsTokens.Violet, 1e-3f)), Is.True);
            Assert.That(mb.Col.Any(c => c.Approximately(DsTokens.Card, 1e-3f)), Is.True);
            double area = 216.0 * 304.0 - 4 * (36.0 - Math.PI * 9.0);
            double card = ShapesTests.Coverage(mb, c => c.Approximately(DsTokens.Card.WithAlpha(c.A), 1e-3f));
            Assert.That(card, Is.EqualTo(area).Within(area * 0.003));
        }

        [Test]
        public void ContentBox_SubtractsBorderAndPadding()
        {
            BoxStyle s = Css.Box("border: 2px solid var(--steel); padding: 16px 28px");
            RectF c = s.ContentBox(new RectF(0f, 0f, 200f, 100f));
            Assert.That(c, Is.EqualTo(new RectF(30f, 18f, 140f, 64f)));
            Assert.That(s.PaddingBox(new RectF(0f, 0f, 200f, 100f)), Is.EqualTo(new RectF(2f, 2f, 196f, 96f)));
        }

        [Test]
        public void SizedBackgroundLayer_PaintsOnlyItsTile()
        {
            // fx.js:158 thrust streak: a band 100% × 6px centred, no-repeat
            var box = new RectF(0f, 0f, 120f, 18f);
            MeshBuilder mb = Build(box, "background: linear-gradient(270deg, #fff 0%, #fff 70%, transparent 100%) center / 100% 6px no-repeat");
            Assert.That(mb.Pos.Min(p => p.Y), Is.EqualTo(6f).Within(1e-3f));
            Assert.That(mb.Pos.Max(p => p.Y), Is.EqualTo(12f).Within(1e-3f));
        }
    }
}
