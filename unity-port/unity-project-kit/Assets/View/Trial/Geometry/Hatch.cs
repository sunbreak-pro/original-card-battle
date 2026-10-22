// Parallel bars clipped to a convex outline (kit.md §2.3, §4.5).
//
// Bars: the DS `hatch()` SVG pattern (bundle.js:132): a <pattern width=gap height=gap patternUnits=userSpaceOnUse
// patternTransform="rotate(angle)"> holding a rect `width` wide. A point q is painted when
// frac(((q − origin)·(cosθ, sinθ)) / gap)·gap < width, θ clockwise on screen, phase 0 at the svg's top-left.
// 45° bars run ／, −45° ＼, 90° —.
//
// Stripes: a CSS repeating-linear-gradient with hard stops (`var(--wick) 0 2px, transparent 2px 5px`): bands
// [from, to) every `period` along the CSS gradient direction (sinθ, −cosθ), phase 0 at the gradient line's start
// (for 135° that is the box's top-left corner, measured in Chrome).
//
// Each band is three strips across (AA ramp, solid, AA ramp) whose coverage is linear in the band coordinate, so
// clipping to the outline keeps it exact. The clipped ends are hard (they sit under the 2 px frame).
using System;
using System.Collections.Generic;

namespace Trial.Geometry
{
    public static class Hatch
    {
        /// <summary>SVG pattern semantics: bars `width` every `gap` at angleDeg, phase at origin, clipped to clip (convex).</summary>
        // js: vendor/ds/bundle.js:132 (hatch)
        public static void Bars(IReadOnlyList<Vec2> clip, Vec2 origin, float angleDeg, float gap, float width, Rgba color, float feather, MeshBuilder mb)
        {
            double a = angleDeg * Math.PI / 180.0;
            var u = new Vec2((float)Math.Cos(a), (float)Math.Sin(a));
            Bands(clip, origin, u, gap, 0f, width, color, feather, mb);
        }

        /// <summary>
        /// CSS repeating-linear-gradient with hard stops: bands [from, to) every `period` px along the CSS angle,
        /// phase at the box's gradient-line start, clipped to clip (convex).
        /// </summary>
        public static void Stripes(RectF box, IReadOnlyList<Vec2> clip, float cssAngleDeg, float from, float to, float period, Rgba color,
            float feather, MeshBuilder mb)
        {
            double a = cssAngleDeg * Math.PI / 180.0;
            var d = new Vec2((float)Math.Sin(a), (float)-Math.Cos(a));
            float len = LinearGradient.LineLength(cssAngleDeg, box.W, box.H);
            Vec2 start = box.Center - d * (len * 0.5f);
            Bands(clip, start, d, period, from, to, color, feather, mb);
        }

        /// <summary>The pattern rule itself (for tests and hit checks): is q inside a bar?</summary>
        public static bool Painted(Vec2 q, Vec2 origin, float angleDeg, float gap, float width)
        {
            double a = angleDeg * Math.PI / 180.0;
            double x = (q.X - origin.X) * Math.Cos(a) + (q.Y - origin.Y) * Math.Sin(a);
            double f = x / gap - Math.Floor(x / gap);
            return f * gap < width;
        }

        /// <summary>A convex outline cut to x ≤ maxX: the floor hatch's left-to-right reveal (clip-path: inset(0 x% 0 0)).</summary>
        public static List<Vec2> RevealTo(IReadOnlyList<Vec2> convex, float maxX)
        {
            var poly = new List<CV>(convex.Count);
            foreach (Vec2 p in convex) poly.Add(new CV(p, 1f));
            List<CV> cut = Pieces.ClipHalf(poly, new Vec2(-1f, 0f), -maxX);
            var o = new List<Vec2>(cut.Count);
            foreach (CV v in cut) o.Add(v.P);
            return o;
        }

        /// <summary>Bands [from, to) + k·period along the unit vector u from origin, clipped to clip.</summary>
        internal static void Bands(IReadOnlyList<Vec2> clip, Vec2 origin, Vec2 u, float period, float from, float to, Rgba color,
            float feather, MeshBuilder mb)
        {
            if (clip == null || clip.Count < 3 || period <= 0f || to <= from || color.A <= 0f) return;
            List<Vec2> outline = Pieces.CleanConvex(clip);
            if (outline.Count < 3) return;
            var region = new ConvexRegion(outline);
            Vec2 v = u.Perp;
            float xmin = float.MaxValue, xmax = float.MinValue, ymin = float.MaxValue, ymax = float.MinValue;
            foreach (Vec2 p in outline)
            {
                Vec2 r = p - origin;
                float x = Vec2.Dot(r, u), y = Vec2.Dot(r, v);
                xmin = Math.Min(xmin, x); xmax = Math.Max(xmax, x);
                ymin = Math.Min(ymin, y); ymax = Math.Max(ymax, y);
            }
            ymin -= 1f;
            ymax += 1f;
            float h = feather * 0.5f;
            int k0 = (int)Math.Floor((xmin - to - feather) / period);
            int k1 = (int)Math.Ceiling((xmax - from + feather) / period);
            if (k1 - k0 > 20000) return;
            float w = to - from;
            for (int k = k0; k <= k1; k++)
            {
                float a = k * period + from, b = k * period + to;
                if (feather <= 0f) Strip(a, b, 1f, 1f);
                else if (w >= feather)
                {
                    Strip(a - h, a + h, 0f, 1f);
                    Strip(a + h, b - h, 1f, 1f);
                    Strip(b - h, b + h, 1f, 0f);
                }
                else
                {
                    float m = (a + b) * 0.5f, peak = w / feather;
                    Strip(m - feather, m, 0f, peak);
                    Strip(m, m + feather, peak, 0f);
                }
            }

            void Strip(float x0, float x1, float a0, float a1)
            {
                if (x1 - x0 <= 1e-6f || x1 < xmin || x0 > xmax) return;
                var quad = new List<CV>
                {
                    new CV(origin + u * x0 + v * ymin, a0), new CV(origin + u * x1 + v * ymin, a1),
                    new CV(origin + u * x1 + v * ymax, a1), new CV(origin + u * x0 + v * ymax, a0),
                };
                List<CV> cut = Pieces.ClipConvex(quad, region);
                if (cut.Count < 3) return;
                var col = new List<CV>(cut.Count);
                foreach (CV c in cut)
                {
                    float x = Vec2.Dot(c.P - origin, u);
                    float t = (x - x0) / (x1 - x0);
                    float cov = a0 + (a1 - a0) * Math.Max(0f, Math.Min(1f, t));
                    col.Add(new CV(c.P, color.MulAlpha(cov)));
                }
                Pieces.EmitColored(col, mb);
            }
        }
    }
}
