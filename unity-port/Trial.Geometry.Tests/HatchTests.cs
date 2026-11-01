using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Trial.Geometry.Tests
{
    public class HatchTests
    {
        private static List<Vec2> Rect(RectF r) => new List<Vec2> { r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft };

        [Test]
        public void Rule_PhaseZeroAtTheOrigin()
        {
            var o = new Vec2(10f, 20f);
            var u = new Vec2((float)Math.Cos(Math.PI / 4), (float)Math.Sin(Math.PI / 4));
            Assert.That(Hatch.Painted(o + u * 0.01f, o, 45f, 11f, 3f), Is.True);
            Assert.That(Hatch.Painted(o + u * 2.9f, o, 45f, 11f, 3f), Is.True);
            Assert.That(Hatch.Painted(o + u * 4f, o, 45f, 11f, 3f), Is.False);
            Assert.That(Hatch.Painted(o + u * 12f, o, 45f, 11f, 3f), Is.True);
            Assert.That(Hatch.Painted(o - u * 9f, o, 45f, 11f, 3f), Is.True); // frac(−9/11)·11 = 2
            Assert.That(Hatch.Painted(o - u * 7f, o, 45f, 11f, 3f), Is.False); // = 4
            // moving along a bar keeps the phase: 45° bars run ／
            var along = new Vec2(-u.Y, u.X);
            Assert.That(Hatch.Painted(o + u * 1f + along * 37f, o, 45f, 11f, 3f), Is.True);
        }

        [Test]
        public void Bars_MeshMatchesTheRule()
        {
            // the floor threat window: tile + (1,1), (w − 2) × 38
            var clip = Rect(new RectF(301f, 561f, 118f, 38f));
            var origin = new Vec2(301f, 561f);
            var mb = new MeshBuilder();
            Hatch.Bars(clip, origin, 45f, 11f, 3f, DsTokens.ReachRed, 0f, mb);
            Assert.That(mb.TriangleCount, Is.GreaterThan(0));
            double u = Math.Cos(Math.PI / 4);
            foreach (Vec2 p in mb.Pos)
            {
                double x = (p.X - origin.X) * u + (p.Y - origin.Y) * u;
                double f = x / 11.0 - Math.Floor(x / 11.0 + 1e-6);
                Assert.That(f * 11.0, Is.LessThanOrEqualTo(3.0 + 1e-3).Or.GreaterThanOrEqualTo(11.0 - 1e-3), "vertex outside a bar: " + p);
                Assert.That(p.X, Is.InRange(300.999f, 419.001f));
                Assert.That(p.Y, Is.InRange(560.999f, 599.001f));
            }
            // a bar boundary passes through the origin
            Assert.That(mb.Pos.Any(p => Vec2.Dist(p, origin) < 1e-3f), Is.True);
            Assert.That(ShapesTests.Coverage(mb), Is.EqualTo(118.0 * 38.0 * 3.0 / 11.0).Within(118.0 * 38.0 * 3.0 / 11.0 * 0.04));
        }

        [Test]
        public void Bars_WithFeather_KeepTheCoverage()
        {
            var clip = Rect(new RectF(0f, 0f, 200f, 100f));
            var mb = new MeshBuilder();
            Hatch.Bars(clip, Vec2.Zero, -45f, 11f, 3f, DsTokens.ReachRed, 1.5f, mb);
            Assert.That(ShapesTests.Coverage(mb), Is.EqualTo(200.0 * 100.0 * 3.0 / 11.0).Within(200.0 * 100.0 * 3.0 / 11.0 * 0.02));
            // the swatch: 2 px bars every 6 px in a (22 − 4)² window
            var sw = new MeshBuilder();
            Hatch.Bars(Rect(new RectF(2f, 2f, 18f, 18f)), new Vec2(2f, 2f), 45f, 6f, 2f, DsTokens.ReachRed, 1f, sw);
            Assert.That(ShapesTests.Coverage(sw), Is.EqualTo(18.0 * 18.0 / 3.0).Within(18.0 * 18.0 / 3.0 * 0.05));
        }

        [Test]
        public void Stripes_135_PhaseAtTheTopLeft()
        {
            // bundle.css:92 HP loss: repeating-linear-gradient(135deg, wick 0 2px, transparent 2px 5px)
            var box = new RectF(50f, 10f, 100f, 14f);
            var mb = new MeshBuilder();
            Hatch.Stripes(box, Rect(box), 135f, 0f, 2f, 5f, DsTokens.Wick, 0f, mb);
            double u = Math.Sqrt(0.5);
            foreach (Vec2 p in mb.Pos)
            {
                double s = (p.X - 50f) * u + (p.Y - 10f) * u;
                double f = s / 5.0 - Math.Floor(s / 5.0 + 1e-6);
                Assert.That(f * 5.0, Is.LessThanOrEqualTo(2.0 + 1e-3).Or.GreaterThanOrEqualTo(5.0 - 1e-3));
            }
            Assert.That(mb.Pos.Any(p => Vec2.Dist(p, box.TopLeft) < 1e-3f), Is.True);
            // and it agrees with the gradient paint of the same CSS
            Paint g = Css.Background("repeating-linear-gradient(135deg, var(--wick) 0 2px, transparent 2px 5px)", box);
            Assert.That(g.At(new Vec2(50.5f, 10.5f)).A, Is.EqualTo(1f));
            Assert.That(g.At(new Vec2(52f, 12f)).A, Is.EqualTo(0f));
        }

        [Test]
        public void Stripes_90_ThrowLine()
        {
            var box = new RectF(400f, 563f, 1120f, 3f);
            var mb = new MeshBuilder();
            Hatch.Stripes(box, Rect(box), 90f, 0f, 9f, 15f, DsTokens.Amber, 0f, mb);
            foreach (Vec2 p in mb.Pos)
            {
                double x = (p.X - 400f) % 15.0;
                Assert.That(x, Is.LessThanOrEqualTo(9.0 + 1e-3).Or.GreaterThanOrEqualTo(15.0 - 1e-3));
            }
            Assert.That(ShapesTests.Coverage(mb), Is.EqualTo(1120.0 * 3 * 9 / 15).Within(10.0));
        }

        [Test]
        public void RevealTo_CutsTheClipFromTheRight()
        {
            List<Vec2> cut = Hatch.RevealTo(Rect(new RectF(0f, 0f, 100f, 40f)), 30f);
            Assert.That(cut.Max(p => p.X), Is.EqualTo(30f).Within(1e-4f));
            Assert.That(cut.Count, Is.EqualTo(4));
        }
    }
}
