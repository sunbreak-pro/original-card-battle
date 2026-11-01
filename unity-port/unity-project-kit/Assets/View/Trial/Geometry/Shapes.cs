// Rounded rectangles, ellipses and anti-aliased convex fills and rings.
//
// Radii holds a CSS border-radius before the box is known (px or %); Resolve() applies the CSS clamping (all radii
// scaled by one factor when two of them overflow a side, CSS Backgrounds 3 §5.5) and gives a CornerSet in px.
// RoundRectPoints() samples the outline clockwise on screen (TL arc → TR → BR → BL) with the same number of points
// per corner for any radii, so two outlines of one box (outer and inner edge of a border) pair point by point.
//
// Anti-aliasing (kit.md §4.3): a convex fill is a solid core inset by feather/2 plus a rim to +feather/2 whose
// outer vertices have alpha 0; a ring has the same fringe on both edges, and a ring thinner than the feather keeps
// the feather's width with its alpha scaled by width / feather (coverage).
//
// Painting: every shape is cut into convex pieces carrying a coverage alpha, then painted: a solid colour as is; a
// linear gradient after splitting the pieces along its IsoLines (exact); a radial gradient after splitting along
// sector rays and, inside each sector, the chords of its stop ellipses (the chord sag stays under 0.5 px); any other
// non-linear paint along a grid. Pieces go into the MeshBuilder as fans (AddVertex / AddTri only).
using System;
using System.Collections.Generic;

namespace Trial.Geometry
{
    /// <summary>A CSS border-radius before its box is known: per corner (rx, ry), each px or % (of the box width / height).</summary>
    public readonly struct Radii : IEquatable<Radii>
    {
        public readonly CssLength TLx, TLy, TRx, TRy, BRx, BRy, BLx, BLy;

        public Radii(CssLength tlx, CssLength tly, CssLength trx, CssLength try_, CssLength brx, CssLength bry, CssLength blx, CssLength bly)
        {
            TLx = tlx; TLy = tly; TRx = trx; TRy = try_; BRx = brx; BRy = bry; BLx = blx; BLy = bly;
        }

        public static readonly Radii Zero = new Radii(CssLength.Zero, CssLength.Zero, CssLength.Zero, CssLength.Zero,
            CssLength.Zero, CssLength.Zero, CssLength.Zero, CssLength.Zero);

        /// <summary>The same circular radius on every corner (px).</summary>
        public static Radii All(float px) => All(CssLength.FromPx(px));

        public static Radii All(CssLength r) => new Radii(r, r, r, r, r, r, r, r);

        /// <summary>border-radius: tl tr br bl (px, circular).</summary>
        public static Radii Px(float tl, float tr, float br, float bl) => new Radii(
            CssLength.FromPx(tl), CssLength.FromPx(tl), CssLength.FromPx(tr), CssLength.FromPx(tr),
            CssLength.FromPx(br), CssLength.FromPx(br), CssLength.FromPx(bl), CssLength.FromPx(bl));

        /// <summary>border-radius: 50% (an ellipse inscribed in the box; a circle on a square).</summary>
        public static readonly Radii Pill = All(CssLength.FromPercent(50f));

        public bool IsZero => TLx.IsZero && TLy.IsZero && TRx.IsZero && TRy.IsZero && BRx.IsZero && BRy.IsZero && BLx.IsZero && BLy.IsZero;

        /// <summary>The corners in px with CSS clamping (§5.5: f = min(Li / Si); when f &lt; 1 every radius is multiplied by f).</summary>
        public CornerSet Resolve(float w, float h)
        {
            float tlx = Math.Max(0f, TLx.Resolve(w)), tly = Math.Max(0f, TLy.Resolve(h));
            float trx = Math.Max(0f, TRx.Resolve(w)), trY = Math.Max(0f, TRy.Resolve(h));
            float brx = Math.Max(0f, BRx.Resolve(w)), bry = Math.Max(0f, BRy.Resolve(h));
            float blx = Math.Max(0f, BLx.Resolve(w)), bly = Math.Max(0f, BLy.Resolve(h));
            if (tlx == 0f || tly == 0f) tlx = tly = 0f;
            if (trx == 0f || trY == 0f) trx = trY = 0f;
            if (brx == 0f || bry == 0f) brx = bry = 0f;
            if (blx == 0f || bly == 0f) blx = bly = 0f;
            float f = 1f;
            f = Fit(f, w, tlx + trx);
            f = Fit(f, w, blx + brx);
            f = Fit(f, h, tly + bly);
            f = Fit(f, h, trY + bry);
            return new CornerSet(new Vec2(tlx * f, tly * f), new Vec2(trx * f, trY * f), new Vec2(brx * f, bry * f), new Vec2(blx * f, bly * f));
        }

        public CornerSet Resolve(RectF box) => Resolve(box.W, box.H);

        private static float Fit(float f, float side, float sum) => sum > side && sum > 0f ? Math.Min(f, Math.Max(0f, side) / sum) : f;

        public bool Equals(Radii o) => TLx.Equals(o.TLx) && TLy.Equals(o.TLy) && TRx.Equals(o.TRx) && TRy.Equals(o.TRy) &&
                                       BRx.Equals(o.BRx) && BRy.Equals(o.BRy) && BLx.Equals(o.BLx) && BLy.Equals(o.BLy);
        public override bool Equals(object obj) => obj is Radii r && Equals(r);
        public override int GetHashCode() => TLx.GetHashCode() ^ TRx.GetHashCode() * 3 ^ BRx.GetHashCode() * 7 ^ BLx.GetHashCode() * 11;

        public override string ToString() =>
            TLx + " " + TRx + " " + BRx + " " + BLx + " / " + TLy + " " + TRy + " " + BRy + " " + BLy;
    }

    /// <summary>Resolved corner radii in px: (rx, ry) per corner.</summary>
    public readonly struct CornerSet : IEquatable<CornerSet>
    {
        public readonly Vec2 TL, TR, BR, BL;

        public CornerSet(Vec2 tl, Vec2 tr, Vec2 br, Vec2 bl) { TL = tl; TR = tr; BR = br; BL = bl; }

        public static readonly CornerSet Zero = new CornerSet(Vec2.Zero, Vec2.Zero, Vec2.Zero, Vec2.Zero);

        public static CornerSet All(float r) => new CornerSet(new Vec2(r, r), new Vec2(r, r), new Vec2(r, r), new Vec2(r, r));

        public bool IsZero => TL == Vec2.Zero && TR == Vec2.Zero && BR == Vec2.Zero && BL == Vec2.Zero;

        public float Max => Math.Max(Math.Max(Math.Max(TL.X, TL.Y), Math.Max(TR.X, TR.Y)), Math.Max(Math.Max(BR.X, BR.Y), Math.Max(BL.X, BL.Y)));

        /// <summary>The inner edge of a border (CSS: r − border width per axis, never below 0).</summary>
        public CornerSet Inset(float top, float right, float bottom, float left) => new CornerSet(
            new Vec2(Math.Max(0f, TL.X - left), Math.Max(0f, TL.Y - top)),
            new Vec2(Math.Max(0f, TR.X - right), Math.Max(0f, TR.Y - top)),
            new Vec2(Math.Max(0f, BR.X - right), Math.Max(0f, BR.Y - bottom)),
            new Vec2(Math.Max(0f, BL.X - left), Math.Max(0f, BL.Y - bottom)));

        /// <summary>
        /// The corners of a shape grown by d (negative shrinks): r + d, and a sharp corner stays sharp when growing only
        /// if keepSharp (an outline); otherwise a sharp corner grown by d is rounded by d (the distance contour).
        /// </summary>
        public CornerSet Grow(float d, bool keepSharp = false)
        {
            Vec2 G(Vec2 r)
            {
                if (keepSharp && r == Vec2.Zero) return Vec2.Zero;
                return new Vec2(Math.Max(0f, r.X + d), Math.Max(0f, r.Y + d));
            }
            return new CornerSet(G(TL), G(TR), G(BR), G(BL));
        }

        /// <summary>The box-shadow spread rule (CSS Backgrounds 3 §7.1.1): r + s·(1 + (r/s − 1)³) when r &lt; s, r + s otherwise.</summary>
        public CornerSet Spread(float s)
        {
            Vec2 G(Vec2 r) => new Vec2(SpreadOne(r.X, s), SpreadOne(r.Y, s));
            return new CornerSet(G(TL), G(TR), G(BR), G(BL));
        }

        internal static float SpreadOne(float r, float s)
        {
            if (s <= 0f) return Math.Max(0f, r + s);
            if (r >= s) return r + s;
            float q = r / s - 1f;
            return Math.Max(0f, r + s * (1f + q * q * q));
        }

        public bool Equals(CornerSet o) => TL == o.TL && TR == o.TR && BR == o.BR && BL == o.BL;
        public override bool Equals(object obj) => obj is CornerSet c && Equals(c);
        public override int GetHashCode() => TL.GetHashCode() ^ TR.GetHashCode() * 3 ^ BR.GetHashCode() * 7 ^ BL.GetHashCode() * 11;
    }

    public static class Shapes
    {
        /// <summary>Radial gradients are split so the chord sag stays under this (px).</summary>
        public const float RadialSagPx = 0.5f;
        /// <summary>Grid step for other non-linear paints (px).</summary>
        public static float RefinePx = 6f;

        /// <summary>Points per quarter arc of radius r for a chord error ≤ tol (≥ 1, ≤ 64).</summary>
        public static int CornerSegments(float r, float tolPx = 0.2f)
        {
            if (r <= tolPx || tolPx <= 0f) return 1;
            double step = 2.0 * Math.Acos(1.0 - tolPx / r);
            int n = (int)Math.Ceiling(Math.PI / 2.0 / step);
            return Math.Max(1, Math.Min(64, n));
        }

        /// <summary>
        /// The outline of a rounded rect, clockwise on screen, segs + 1 points per corner starting at the TL arc's
        /// left end. Zero-radius corners repeat their point, so every outline with the same segs has 4·(segs+1) points.
        /// </summary>
        public static List<Vec2> RoundRectPoints(RectF r, CornerSet c, int segs)
        {
            var pts = new List<Vec2>(4 * (segs + 1));
            Corner(pts, new Vec2(r.X + c.TL.X, r.Y + c.TL.Y), c.TL, 180.0, segs);
            Corner(pts, new Vec2(r.Right - c.TR.X, r.Y + c.TR.Y), c.TR, 270.0, segs);
            Corner(pts, new Vec2(r.Right - c.BR.X, r.Bottom - c.BR.Y), c.BR, 0.0, segs);
            Corner(pts, new Vec2(r.X + c.BL.X, r.Bottom - c.BL.Y), c.BL, 90.0, segs);
            return pts;
        }

        private static void Corner(List<Vec2> pts, Vec2 centre, Vec2 rad, double fromDeg, int segs)
        {
            for (int i = 0; i <= segs; i++)
            {
                double a = (fromDeg + 90.0 * i / segs) * Math.PI / 180.0;
                pts.Add(new Vec2(centre.X + rad.X * (float)Math.Cos(a), centre.Y + rad.Y * (float)Math.Sin(a)));
            }
        }

        public static Polyline RoundRect(RectF r, Radii radii, float tolPx = 0.2f) => RoundRect(r, radii.Resolve(r), tolPx);

        /// <summary>The rounded rect as a closed polyline (clockwise on screen, no repeated points).</summary>
        public static Polyline RoundRect(RectF r, CornerSet c, float tolPx = 0.2f) =>
            new Polyline(Dedupe(RoundRectPoints(r, c, CornerSegments(c.Max, tolPx))), true);

        /// <summary>The ellipse inscribed in r, clockwise on screen.</summary>
        public static Polyline Ellipse(RectF r, float tolPx = 0.2f)
        {
            var half = new Vec2(r.W / 2f, r.H / 2f);
            return RoundRect(r, new CornerSet(half, half, half, half), tolPx);
        }

        /// <summary>A convex fill with an AA fringe: solid core shrunk by feather/2, alpha-0 rim grown by feather/2.</summary>
        public static void FillConvexAA(Polyline convex, Paint paint, float feather, MeshBuilder mb)
        {
            List<Vec2> pts = Pieces.CleanConvex(convex.Points);
            if (pts.Count < 3) return;
            RectF bounds = RectF.Bounds(pts).Inflate(feather);
            foreach (List<CV> piece in Pieces.ConvexAA(pts, feather)) Pieces.EmitPainted(piece, paint, bounds, mb);
        }

        /// <summary>A convex fill without a fringe (used where the edge is covered, or for clipped tiles).</summary>
        public static void FillConvex(Polyline convex, Paint paint, MeshBuilder mb)
        {
            List<Vec2> pts = Pieces.CleanConvex(convex.Points);
            if (pts.Count < 3) return;
            var piece = new List<CV>(pts.Count);
            foreach (Vec2 p in pts) piece.Add(new CV(p, Rgba.White));
            Pieces.EmitPainted(piece, paint, RectF.Bounds(pts), mb);
        }

        /// <summary>
        /// The band between two closed outlines with the same point count (point i of each pairs up, e.g. two
        /// RoundRectPoints of one box): a solid border. AA on both edges; thinner than the feather → coverage alpha.
        /// </summary>
        public static void Ring(Polyline outer, Polyline inner, Paint paint, float feather, MeshBuilder mb)
        {
            if (outer.Points.Count != inner.Points.Count)
                throw new ArgumentException("Ring: outer and inner need the same number of points (use RoundRectPoints with one segs).");
            RectF bounds = RectF.Bounds(outer.Points).Inflate(feather);
            foreach (List<CV> piece in Pieces.RingTriangles(outer.Points, inner.Points, feather))
                Pieces.EmitPainted(piece, paint, bounds, mb);
        }

        internal static List<Vec2> Dedupe(List<Vec2> pts)
        {
            var o = new List<Vec2>(pts.Count);
            foreach (Vec2 p in pts)
                if (o.Count == 0 || (p - o[o.Count - 1]).LenSq > 1e-10f) o.Add(p);
            while (o.Count > 1 && (o[0] - o[o.Count - 1]).LenSq <= 1e-10f) o.RemoveAt(o.Count - 1);
            return o;
        }
    }

    /// <summary>A mesh vertex while it is being cut: position and colour (or coverage in alpha).</summary>
    internal readonly struct CV
    {
        public readonly Vec2 P;
        public readonly Rgba C;

        public CV(Vec2 p, Rgba c) { P = p; C = c; }
        public CV(Vec2 p, float coverage) { P = p; C = new Rgba(1f, 1f, 1f, coverage); }

        public static CV Lerp(CV a, CV b, float t) => new CV(Vec2.Lerp(a.P, b.P, t), Rgba.LerpStraight(a.C, b.C, t));
    }

    /// <summary>A convex polygon prepared for many clip / subtract tests: its inside half-planes and bounds.</summary>
    internal sealed class ConvexRegion
    {
        public enum Where { Outside, Inside, Crossing }

        public readonly List<(Vec2 n, float c)> Planes;
        public readonly RectF Bounds;

        public ConvexRegion(IReadOnlyList<Vec2> convex)
        {
            Planes = Pieces.HalfPlanes(convex);
            Bounds = RectF.Bounds(convex);
        }

        /// <summary>Quick classification of a polygon: wholly outside (bounds apart, or beyond one edge), wholly inside, or crossing.</summary>
        public Where Classify(List<CV> poly)
        {
            const float Eps = 1e-5f;
            float l = float.MaxValue, t = float.MaxValue, r = float.MinValue, b = float.MinValue;
            foreach (CV v in poly)
            {
                if (v.P.X < l) l = v.P.X;
                if (v.P.X > r) r = v.P.X;
                if (v.P.Y < t) t = v.P.Y;
                if (v.P.Y > b) b = v.P.Y;
            }
            if (r <= Bounds.X + Eps || l >= Bounds.Right - Eps || b <= Bounds.Y + Eps || t >= Bounds.Bottom - Eps) return Where.Outside;
            bool allInside = true;
            foreach ((Vec2 n, float c) in Planes)
            {
                bool anyIn = false, anyOut = false;
                foreach (CV v in poly)
                {
                    float s = n.X * v.P.X + n.Y * v.P.Y - c;
                    if (s > Eps) anyIn = true;
                    else if (s < -Eps) anyOut = true;
                }
                if (!anyIn) return Where.Outside;
                if (anyOut) allInside = false;
            }
            return allInside ? Where.Inside : Where.Crossing;
        }
    }

    /// <summary>Convex-polygon cutting and painting shared by the shape, box, hatch and gradient code.</summary>
    internal static class Pieces
    {
        public static float SignedArea(IReadOnlyList<Vec2> pts)
        {
            double a = 0;
            for (int i = 0, n = pts.Count; i < n; i++)
            {
                Vec2 p = pts[i], q = pts[(i + 1) % n];
                a += (double)p.X * q.Y - (double)q.X * p.Y;
            }
            return (float)(a * 0.5);
        }

        public static float SignedArea(List<CV> pts)
        {
            double a = 0;
            for (int i = 0, n = pts.Count; i < n; i++)
            {
                Vec2 p = pts[i].P, q = pts[(i + 1) % n].P;
                a += (double)p.X * q.Y - (double)q.X * p.Y;
            }
            return (float)(a * 0.5);
        }

        /// <summary>No repeated points, clockwise on screen (positive area).</summary>
        public static List<Vec2> CleanConvex(IReadOnlyList<Vec2> pts)
        {
            var o = Shapes.Dedupe(new List<Vec2>(pts));
            if (o.Count >= 3 && SignedArea(o) < 0f) o.Reverse();
            return o;
        }

        /// <summary>Splits a convex polygon by the line Dot(n, p) = c into the parts with Side ≥ 0 and ≤ 0.</summary>
        public static void Split(List<CV> poly, Vec2 n, float c, out List<CV> pos, out List<CV> neg)
        {
            pos = new List<CV>(poly.Count + 2);
            neg = new List<CV>(poly.Count + 2);
            int count = poly.Count;
            for (int i = 0; i < count; i++)
            {
                CV a = poly[i], b = poly[(i + 1) % count];
                float sa = Vec2.Dot(n, a.P) - c, sb = Vec2.Dot(n, b.P) - c;
                if (sa >= 0f) pos.Add(a);
                if (sa <= 0f) neg.Add(a);
                if ((sa > 0f && sb < 0f) || (sa < 0f && sb > 0f))
                {
                    CV m = CV.Lerp(a, b, sa / (sa - sb));
                    pos.Add(m);
                    neg.Add(m);
                }
            }
            if (pos.Count < 3) pos.Clear();
            if (neg.Count < 3) neg.Clear();
        }

        /// <summary>Keeps the part with Dot(n, p) − c ≥ 0.</summary>
        public static List<CV> ClipHalf(List<CV> poly, Vec2 n, float c)
        {
            Split(poly, n, c, out List<CV> pos, out _);
            return pos;
        }

        /// <summary>The inside half-planes of a convex polygon's edges (any orientation) as (n, c) with Dot(n, p) − c ≥ 0 inside.</summary>
        public static List<(Vec2 n, float c)> HalfPlanes(IReadOnlyList<Vec2> convex)
        {
            var list = new List<(Vec2, float)>(convex.Count);
            float sign = SignedArea(convex) >= 0f ? 1f : -1f;
            for (int i = 0, k = convex.Count; i < k; i++)
            {
                Vec2 a = convex[i], b = convex[(i + 1) % k];
                Vec2 e = b - a;
                if (e.LenSq <= 1e-12f) continue;
                Vec2 n = e.Perp * sign;
                list.Add((n, Vec2.Dot(n, a)));
            }
            return list;
        }

        public static List<CV> ClipConvex(List<CV> poly, IReadOnlyList<Vec2> clip) => ClipConvex(poly, new ConvexRegion(clip));

        /// <summary>poly ∩ region (poly convex).</summary>
        public static List<CV> ClipConvex(List<CV> poly, ConvexRegion region)
        {
            switch (region.Classify(poly))
            {
                case ConvexRegion.Where.Outside: return new List<CV>();
                case ConvexRegion.Where.Inside: return poly;
            }
            List<CV> cur = poly;
            foreach ((Vec2 n, float c) in region.Planes)
            {
                cur = ClipHalf(cur, n, c);
                if (cur.Count == 0) break;
            }
            return cur;
        }

        /// <summary>poly minus a convex hole, as convex pieces.</summary>
        public static List<List<CV>> SubtractConvex(List<CV> poly, IReadOnlyList<Vec2> hole) => SubtractConvex(poly, new ConvexRegion(hole));

        public static List<List<CV>> SubtractConvex(List<CV> poly, ConvexRegion hole)
        {
            var result = new List<List<CV>>();
            switch (hole.Classify(poly))
            {
                case ConvexRegion.Where.Outside: result.Add(poly); return result;
                case ConvexRegion.Where.Inside: return result;
            }
            List<CV> rest = poly;
            foreach ((Vec2 n, float c) in hole.Planes)
            {
                Split(rest, n, c, out List<CV> inside, out List<CV> outside);
                if (outside.Count >= 3) result.Add(outside);
                rest = inside;
                if (rest.Count < 3) return result;
            }
            return result; // whatever is left lies inside the hole
        }

        /// <summary>Emits a convex polygon whose vertex colours are final.</summary>
        public static void EmitColored(List<CV> poly, MeshBuilder mb)
        {
            if (poly.Count < 3 || Math.Abs(SignedArea(poly)) < 1e-6f) return;
            int baseIdx = mb.AddVertex(poly[0].P, poly[0].C);
            for (int i = 1; i < poly.Count; i++) mb.AddVertex(poly[i].P, poly[i].C);
            for (int i = 1; i + 1 < poly.Count; i++) mb.AddTri(baseIdx, baseIdx + i, baseIdx + i + 1);
        }

        /// <summary>Emits a convex polygon whose vertex alphas are coverage, painted with paint (split as the paint needs).</summary>
        public static void EmitPainted(List<CV> poly, Paint paint, RectF bounds, MeshBuilder mb)
        {
            if (poly.Count < 3) return;
            if (paint == null || paint.IsSolid)
            {
                Rgba col = paint == null ? Rgba.White : paint.At(poly[0].P);
                EmitColored(Recolour(poly, _ => col), mb);
                return;
            }
            var pieces = new List<List<CV>> { poly };
            if (paint is LayeredPaint layered)
                foreach (Paint layer in layered.Layers) pieces = SplitFor(pieces, layer, bounds);
            else pieces = SplitFor(pieces, paint, bounds);
            foreach (List<CV> piece in pieces) EmitColored(Recolour(piece, paint.At), mb);
        }

        /// <summary>Cuts pieces where one paint's colour is not linear: radial sectors and chords, linear isolines, else a grid.</summary>
        private static List<List<CV>> SplitFor(List<List<CV>> pieces, Paint paint, RectF bounds)
        {
            if (paint.IsSolid) return pieces;
            if (paint is RadialGradient rg) return SplitRadial(pieces, rg);
            IEnumerable<IsoLine> bands = paint.Bands(bounds);
            if (bands != null)
                foreach (IsoLine l in bands) pieces = SplitAll(pieces, l.N, l.C);
            if (paint.NeedsRefine) pieces = SplitGrid(pieces, Shapes.RefinePx);
            return pieces;
        }

        private static List<CV> Recolour(List<CV> poly, Func<Vec2, Rgba> at)
        {
            var o = new List<CV>(poly.Count);
            foreach (CV v in poly) o.Add(new CV(v.P, at(v.P).MulAlpha(v.C.A)));
            return o;
        }

        public static List<List<CV>> SplitAll(List<List<CV>> pieces, Vec2 n, float c)
        {
            var o = new List<List<CV>>(pieces.Count + 4);
            foreach (List<CV> p in pieces)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (CV v in p)
                {
                    float s = Vec2.Dot(n, v.P) - c;
                    if (s < lo) lo = s;
                    if (s > hi) hi = s;
                }
                if (lo >= 0f || hi <= 0f) { o.Add(p); continue; }
                Split(p, n, c, out List<CV> a, out List<CV> b);
                if (a.Count >= 3) o.Add(a);
                if (b.Count >= 3) o.Add(b);
            }
            return o;
        }

        private static List<List<CV>> SplitGrid(List<List<CV>> pieces, float step)
        {
            var all = new List<Vec2>();
            foreach (List<CV> p in pieces) foreach (CV v in p) all.Add(v.P);
            RectF b = RectF.Bounds(all);
            if (step <= 0f) return pieces;
            int nx = (int)Math.Min(512, Math.Ceiling(b.W / step)), ny = (int)Math.Min(512, Math.Ceiling(b.H / step));
            for (int i = 1; i < nx; i++) pieces = SplitAll(pieces, new Vec2(1f, 0f), b.X + b.W * i / nx);
            for (int j = 1; j < ny; j++) pieces = SplitAll(pieces, new Vec2(0f, 1f), b.Y + b.H * j / ny);
            return pieces;
        }

        /// <summary>Sector rays through the centre, then each sector along the chords of the stop ellipses.</summary>
        private static List<List<CV>> SplitRadial(List<List<CV>> pieces, RadialGradient g)
        {
            float R = Math.Max(g.Rx, g.Ry);
            if (R <= 0f) return pieces;
            double step = 2.0 * Math.Acos(Math.Max(-1.0, 1.0 - Shapes.RadialSagPx / R));
            int half = (int)Math.Max(8, Math.Min(128, Math.Ceiling(Math.PI / Math.Max(1e-3, step))));
            Vec2 c = g.Center;
            for (int k = 0; k < half; k++)
            {
                double th = Math.PI * k / half;
                var dir = new Vec2(g.Rx * (float)Math.Cos(th), g.Ry * (float)Math.Sin(th));
                Vec2 n = dir.Perp;
                pieces = SplitAll(pieces, n, Vec2.Dot(n, c));
            }
            List<float> ts = g.KinkTs();
            var scales = new List<float>(ts.Count);
            foreach (float t in ts)
            {
                float s = g.Rx > 0f ? (g.R0 + t * (g.Rx - g.R0)) / g.Rx : 0f;
                if (s > 1e-5f) scales.Add(s);
            }
            var o = new List<List<CV>>();
            double dth = Math.PI / half;
            foreach (List<CV> p in pieces)
            {
                Vec2 m = Vec2.Zero;
                foreach (CV v in p) m += v.P;
                m /= p.Count;
                Vec2 rel = m - c;
                double ang = Math.Atan2(rel.Y / Math.Max(1e-6f, g.Ry), rel.X / Math.Max(1e-6f, g.Rx));
                if (ang < 0) ang += 2 * Math.PI;
                int sector = (int)Math.Floor(ang / dth);
                double a0 = sector * dth, a1 = a0 + dth;
                var e0 = new Vec2(g.Rx * (float)Math.Cos(a0), g.Ry * (float)Math.Sin(a0));
                var e1 = new Vec2(g.Rx * (float)Math.Cos(a1), g.Ry * (float)Math.Sin(a1));
                Vec2 n = (e1 - e0).Perp;
                var list = new List<List<CV>> { p };
                foreach (float s in scales) list = SplitAll(list, n, Vec2.Dot(n, c + e0 * s));
                o.AddRange(list);
            }
            return o;
        }

        // ---- AA building blocks ------------------------------------------------------------------------------

        /// <summary>Outward miter offset of a clean clockwise convex polygon by d (negative = inward).</summary>
        public static List<Vec2> Offset(List<Vec2> pts, float d)
        {
            int n = pts.Count;
            var o = new List<Vec2>(n);
            for (int i = 0; i < n; i++)
            {
                Vec2 a = pts[(i + n - 1) % n], p = pts[i], b = pts[(i + 1) % n];
                Vec2 n1 = Outward(p - a), n2 = Outward(b - p);
                Vec2 m = (n1 + n2).Norm;
                if (m == Vec2.Zero) m = n1;
                float cos = Math.Max(0.25f, Vec2.Dot(m, n1));
                o.Add(p + m * (d / cos));
            }
            return o;
        }

        /// <summary>Outward normal of an edge of a clockwise-on-screen polygon.</summary>
        public static Vec2 Outward(Vec2 edge) => new Vec2(edge.Y, -edge.X).Norm;

        /// <summary>Core + rim pieces (coverage in alpha) of a clean clockwise convex polygon.</summary>
        public static List<List<CV>> ConvexAA(List<Vec2> pts, float feather)
        {
            var result = new List<List<CV>>();
            if (feather <= 0f)
            {
                var flat = new List<CV>(pts.Count);
                foreach (Vec2 p in pts) flat.Add(new CV(p, 1f));
                result.Add(flat);
                return result;
            }
            float h = feather * 0.5f;
            RectF b = RectF.Bounds(pts);
            float hIn = Math.Min(h, 0.45f * Math.Min(b.W, b.H));
            float coreA = 1f;
            if (Math.Min(b.W, b.H) < feather) coreA = Math.Max(0f, Math.Min(1f, Math.Min(b.W, b.H) / feather));
            List<Vec2> inner = Offset(pts, -hIn), outer = Offset(pts, h);
            var core = new List<CV>(pts.Count);
            foreach (Vec2 p in inner) core.Add(new CV(p, coreA));
            result.Add(core);
            for (int i = 0, n = pts.Count; i < n; i++)
            {
                int j = (i + 1) % n;
                result.Add(new List<CV> { new CV(inner[i], coreA), new CV(inner[j], coreA), new CV(outer[j], 0f), new CV(outer[i], 0f) });
            }
            return result;
        }

        /// <summary>
        /// The triangles of a ring between paired outlines (outer[i] ↔ inner[i]), coverage in alpha: four contours
        /// (outer + h α0, outer − h α1, inner + h α1, inner − h α0) where the band is at least the feather wide, and
        /// (mid + f α0, mid α w/f, mid − f α0) where it is thinner.
        /// </summary>
        public static List<List<CV>> RingTriangles(IReadOnlyList<Vec2> outer, IReadOnlyList<Vec2> inner, float feather)
        {
            int n = outer.Count;
            var sections = new CV[n][];
            float h = feather * 0.5f;
            for (int i = 0; i < n; i++)
            {
                Vec2 o = outer[i], q = inner[i];
                Vec2 d = o - q;
                float w = d.Len;
                Vec2 dir = w > 1e-5f ? d / w : FallbackNormal(outer, i);
                if (feather <= 0f)
                    sections[i] = new[] { new CV(o, 1f), new CV(o, 1f), new CV(q, 1f), new CV(q, 1f) };
                else if (w >= feather)
                    sections[i] = new[] { new CV(o + dir * h, 0f), new CV(o - dir * h, 1f), new CV(q + dir * h, 1f), new CV(q - dir * h, 0f) };
                else
                {
                    Vec2 m = (o + q) * 0.5f;
                    float a = w / feather;
                    sections[i] = new[] { new CV(m + dir * feather, 0f), new CV(m, a), new CV(m, a), new CV(m - dir * feather, 0f) };
                }
            }
            var tris = new List<List<CV>>(n * 6);
            for (int i = 0; i < n; i++)
            {
                CV[] s0 = sections[i], s1 = sections[(i + 1) % n];
                for (int k = 0; k < 3; k++)
                {
                    tris.Add(new List<CV> { s0[k], s1[k], s1[k + 1] });
                    tris.Add(new List<CV> { s0[k], s1[k + 1], s0[k + 1] });
                }
            }
            return tris;
        }

        private static Vec2 FallbackNormal(IReadOnlyList<Vec2> pts, int i)
        {
            int n = pts.Count;
            for (int k = 1; k < n; k++)
            {
                Vec2 e = pts[(i + k) % n] - pts[(i + n - k) % n];
                if (e.LenSq > 1e-10f) return Outward(e);
            }
            return new Vec2(0f, -1f);
        }
    }
}
