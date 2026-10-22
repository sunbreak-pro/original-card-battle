// A vertex-coloured triangle list in stage px (y down), plus the cutting operations the kit needs.
// Colours are straight alpha (what uGUI's default UI shader interpolates); a vertex made on an edge gets the
// straight lerp of the edge's ends, so cutting never changes what the GPU would have drawn.
using System;
using System.Collections.Generic;

namespace Trial.Geometry
{
    public sealed class MeshBuilder
    {
        /// <summary>Edge length a radial paint is refined to before its colours are sampled (kit.md §4.6).</summary>
        public const float DefaultRefinePx = 6f;
        private const float SideEps = 1e-6f;

        public List<Vec2> Pos = new List<Vec2>();
        public List<Rgba> Col = new List<Rgba>();
        public List<int> Idx = new List<int>();

        public int VertexCount => Pos.Count;
        public int TriangleCount => Idx.Count / 3;

        public int AddVertex(Vec2 p, Rgba c)
        {
            Pos.Add(p);
            Col.Add(c);
            return Pos.Count - 1;
        }

        public void AddTri(int a, int b, int c)
        {
            Idx.Add(a);
            Idx.Add(b);
            Idx.Add(c);
        }

        public void Clear()
        {
            Pos.Clear();
            Col.Clear();
            Idx.Clear();
        }

        /// <summary>Appends another mesh (its indices are offset).</summary>
        public void Append(MeshBuilder other)
        {
            if (other == null) return;
            int off = Pos.Count;
            Pos.AddRange(other.Pos);
            Col.AddRange(other.Col);
            foreach (int i in other.Idx) Idx.Add(i + off);
        }

        public RectF Bounds() => RectF.Bounds(Pos);

        /// <summary>Total triangle area (px²), for tests.</summary>
        public float Area()
        {
            double a = 0;
            for (int t = 0; t + 2 < Idx.Count; t += 3) a += Math.Abs(TriArea2(Pos[Idx[t]], Pos[Idx[t + 1]], Pos[Idx[t + 2]]));
            return (float)(a * 0.5);
        }

        /// <summary>
        /// ∫ alpha dA over the mesh (px²): the coverage the mesh paints (vertex alphas interpolate linearly, so a
        /// triangle contributes area × mean alpha). Overlaps count twice.
        /// </summary>
        public float AlphaArea()
        {
            double a = 0;
            for (int t = 0; t + 2 < Idx.Count; t += 3)
            {
                int i = Idx[t], j = Idx[t + 1], k = Idx[t + 2];
                a += Math.Abs(TriArea2(Pos[i], Pos[j], Pos[k])) * 0.5 * (Col[i].A + Col[j].A + Col[k].A) / 3.0;
            }
            return (float)a;
        }

        private static double TriArea2(Vec2 a, Vec2 b, Vec2 c) =>
            ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);

        /// <summary>
        /// Cuts every triangle along each line (exact linear gradients: between two stop isolines the colour is
        /// linear, so vertex colours interpolate it exactly once the mesh has a vertex row on every isoline).
        /// </summary>
        public void SplitByLines(IEnumerable<IsoLine> lines)
        {
            if (lines == null) return;
            foreach (IsoLine l in lines) SplitByLine(l);
        }

        private void SplitByLine(IsoLine line)
        {
            int triCount = Idx.Count / 3;
            if (triCount == 0) return;
            var side = new float[Pos.Count];
            bool any = false, pos = false, neg = false;
            for (int i = 0; i < Pos.Count; i++)
            {
                float s = line.Side(Pos[i]);
                if (Math.Abs(s) < SideEps) s = 0f;
                side[i] = s;
                if (s > 0f) pos = true; else if (s < 0f) neg = true;
            }
            any = pos && neg;
            if (!any) return;

            var cache = new Dictionary<long, int>();
            var idx = new List<int>(Idx.Count + 16);
            for (int t = 0; t < triCount; t++)
            {
                int a = Idx[3 * t], b = Idx[3 * t + 1], c = Idx[3 * t + 2];
                float sa = side[a], sb = side[b], sc = side[c];
                bool hasPos = sa > 0f || sb > 0f || sc > 0f;
                bool hasNeg = sa < 0f || sb < 0f || sc < 0f;
                if (!(hasPos && hasNeg))
                {
                    idx.Add(a); idx.Add(b); idx.Add(c);
                    continue;
                }
                // rotate so that `a` is the vertex alone on its side (or the one on the line)
                int[] v = { a, b, c };
                float[] s = { sa, sb, sc };
                int lone = -1;
                for (int k = 0; k < 3; k++)
                {
                    if (s[k] == 0f) { lone = k; break; }
                }
                if (lone >= 0)
                {
                    // one vertex on the line, the other two on opposite sides: two triangles
                    int v0 = v[lone], v1 = v[(lone + 1) % 3], v2 = v[(lone + 2) % 3];
                    int m = EdgePoint(v1, v2, side, cache);
                    idx.Add(v0); idx.Add(v1); idx.Add(m);
                    idx.Add(v0); idx.Add(m); idx.Add(v2);
                    continue;
                }
                for (int k = 0; k < 3; k++)
                {
                    float sk = s[k], s1 = s[(k + 1) % 3], s2 = s[(k + 2) % 3];
                    if ((sk > 0f) != (s1 > 0f) && (sk > 0f) != (s2 > 0f)) { lone = k; break; }
                }
                {
                    int v0 = v[lone], v1 = v[(lone + 1) % 3], v2 = v[(lone + 2) % 3];
                    int m1 = EdgePoint(v0, v1, side, cache);
                    int m2 = EdgePoint(v0, v2, side, cache);
                    idx.Add(v0); idx.Add(m1); idx.Add(m2);
                    idx.Add(m1); idx.Add(v1); idx.Add(v2);
                    idx.Add(m1); idx.Add(v2); idx.Add(m2);
                }
            }
            Idx = idx;
        }

        private int EdgePoint(int i, int j, float[] side, Dictionary<long, int> cache)
        {
            long key = i < j ? ((long)i << 32) | (uint)j : ((long)j << 32) | (uint)i;
            if (cache.TryGetValue(key, out int m)) return m;
            int lo = i < j ? i : j, hi = i < j ? j : i;
            float t = side[lo] / (side[lo] - side[hi]);
            m = AddVertex(Vec2.Lerp(Pos[lo], Pos[hi], t), Rgba.LerpStraight(Col[lo], Col[hi], t));
            cache[key] = m;
            return m;
        }

        /// <summary>
        /// Splits every edge longer than <paramref name="maxEdgePx"/> at its midpoint until none is (radial paints,
        /// gradient strokes). Whether an edge splits depends on the edge alone, so neighbours stay crack-free.
        /// </summary>
        public void Refine(float maxEdgePx)
        {
            if (!(maxEdgePx > 0f)) return;
            float max2 = maxEdgePx * maxEdgePx;
            for (int pass = 0; pass < 24; pass++)
            {
                int triCount = Idx.Count / 3;
                var cache = new Dictionary<long, int>();
                var idx = new List<int>(Idx.Count * 2);
                bool split = false;
                for (int t = 0; t < triCount; t++)
                {
                    int a = Idx[3 * t], b = Idx[3 * t + 1], c = Idx[3 * t + 2];
                    int mab = Long(a, b, max2) ? Mid(a, b, cache) : -1;
                    int mbc = Long(b, c, max2) ? Mid(b, c, cache) : -1;
                    int mca = Long(c, a, max2) ? Mid(c, a, cache) : -1;
                    int n = (mab >= 0 ? 1 : 0) + (mbc >= 0 ? 1 : 0) + (mca >= 0 ? 1 : 0);
                    if (n == 0) { idx.Add(a); idx.Add(b); idx.Add(c); continue; }
                    split = true;
                    if (n == 3)
                    {
                        Tri(idx, a, mab, mca); Tri(idx, mab, b, mbc); Tri(idx, mca, mbc, c); Tri(idx, mab, mbc, mca);
                    }
                    else if (n == 1)
                    {
                        if (mab >= 0) { Tri(idx, a, mab, c); Tri(idx, mab, b, c); }
                        else if (mbc >= 0) { Tri(idx, a, b, mbc); Tri(idx, a, mbc, c); }
                        else { Tri(idx, a, b, mca); Tri(idx, mca, b, c); }
                    }
                    else
                    {
                        // two split edges: rotate so the unsplit edge is (c, a)… then cut the quad
                        if (mca < 0) { Tri(idx, mab, b, mbc); Tri(idx, a, mab, mbc); Tri(idx, a, mbc, c); }
                        else if (mab < 0) { Tri(idx, mbc, c, mca); Tri(idx, b, mbc, mca); Tri(idx, b, mca, a); }
                        else { Tri(idx, mca, a, mab); Tri(idx, c, mca, mab); Tri(idx, c, mab, b); }
                    }
                }
                Idx = idx;
                if (!split) break;
            }
        }

        private static void Tri(List<int> idx, int a, int b, int c) { idx.Add(a); idx.Add(b); idx.Add(c); }

        private bool Long(int i, int j, float max2) => (Pos[i] - Pos[j]).LenSq > max2;

        private int Mid(int i, int j, Dictionary<long, int> cache)
        {
            long key = i < j ? ((long)i << 32) | (uint)j : ((long)j << 32) | (uint)i;
            if (cache.TryGetValue(key, out int m)) return m;
            int lo = i < j ? i : j, hi = i < j ? j : i;
            m = AddVertex((Pos[lo] + Pos[hi]) * 0.5f, Rgba.LerpStraight(Col[lo], Col[hi], 0.5f));
            cache[key] = m;
            return m;
        }

        // ---- polygon clipping ------------------------------------------------------------------------

        private struct PV
        {
            public Vec2 P;
            public Rgba C;
            public PV(Vec2 p, Rgba c) { P = p; C = c; }
        }

        /// <summary>Keeps only what lies inside the convex polygon (any winding). Sutherland–Hodgman per triangle.</summary>
        public void ClipConvex(IReadOnlyList<Vec2> convexPolygon)
        {
            if (convexPolygon == null || convexPolygon.Count < 3) { Clear(); return; }
            float orient = PolyOrientation(convexPolygon);
            if (orient == 0f) { Clear(); return; }
            Rebuild((tri, emit) =>
            {
                List<PV> poly = tri;
                int n = convexPolygon.Count;
                for (int e = 0; e < n && poly.Count > 0; e++)
                    poly = ClipHalf(poly, convexPolygon[e], convexPolygon[(e + 1) % n], orient, true);
                if (poly.Count >= 3) emit(poly);
            }, convexPolygon, orient, inside: true);
        }

        /// <summary>
        /// Keeps only what lies outside the convex polygon (an outer box-shadow is painted only outside the border
        /// box). The outside of a triangle is cut into convex pieces: piece i = outside edge i ∩ inside edges 0..i−1.
        /// </summary>
        public void SubtractConvex(IReadOnlyList<Vec2> convexPolygon)
        {
            if (convexPolygon == null || convexPolygon.Count < 3) return;
            float orient = PolyOrientation(convexPolygon);
            if (orient == 0f) return;
            Rebuild((tri, emit) =>
            {
                int n = convexPolygon.Count;
                List<PV> rest = tri;
                for (int e = 0; e < n && rest.Count >= 3; e++)
                {
                    Vec2 a = convexPolygon[e], b = convexPolygon[(e + 1) % n];
                    List<PV> outside = ClipHalf(rest, a, b, orient, false);
                    if (outside.Count >= 3) emit(outside);
                    rest = ClipHalf(rest, a, b, orient, true);
                }
            }, convexPolygon, orient, inside: false);
        }

        private static float PolyOrientation(IReadOnlyList<Vec2> poly)
        {
            double a = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                Vec2 p = poly[i], q = poly[(i + 1) % poly.Count];
                a += (double)p.X * q.Y - (double)q.X * p.Y;
            }
            return a > 0 ? 1f : a < 0 ? -1f : 0f;
        }

        private static float EdgeSide(Vec2 a, Vec2 b, Vec2 p, float orient) =>
            orient * ((b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X));

        private static List<PV> ClipHalf(List<PV> poly, Vec2 a, Vec2 b, float orient, bool keepInside)
        {
            var o = new List<PV>(poly.Count + 2);
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                PV cur = poly[i], nxt = poly[(i + 1) % n];
                float sc = EdgeSide(a, b, cur.P, orient), sn = EdgeSide(a, b, nxt.P, orient);
                if (!keepInside) { sc = -sc; sn = -sn; }
                bool cin = sc >= 0f, nin = sn >= 0f;
                if (cin) o.Add(cur);
                if (cin != nin)
                {
                    float t = sc / (sc - sn);
                    o.Add(new PV(Vec2.Lerp(cur.P, nxt.P, t), Rgba.LerpStraight(cur.C, nxt.C, t)));
                }
            }
            return o;
        }

        private void Rebuild(Action<List<PV>, Action<List<PV>>> clip, IReadOnlyList<Vec2> poly, float orient, bool inside)
        {
            var pos = new List<Vec2>(Pos.Count);
            var col = new List<Rgba>(Col.Count);
            var idx = new List<int>(Idx.Count);
            var remap = new Dictionary<int, int>();
            int n = poly.Count;

            int Keep(int v)
            {
                if (remap.TryGetValue(v, out int r)) return r;
                pos.Add(Pos[v]);
                col.Add(Col[v]);
                r = pos.Count - 1;
                remap[v] = r;
                return r;
            }

            void Emit(List<PV> p)
            {
                int b0 = pos.Count;
                foreach (PV q in p) { pos.Add(q.P); col.Add(q.C); }
                for (int k = 1; k + 1 < p.Count; k++) { idx.Add(b0); idx.Add(b0 + k); idx.Add(b0 + k + 1); }
            }

            for (int t = 0; t + 2 < Idx.Count; t += 3)
            {
                int a = Idx[t], b = Idx[t + 1], c = Idx[t + 2];
                // fast paths: fully inside every edge, or fully outside one edge
                bool allIn = true, oneOut = false;
                for (int e = 0; e < n; e++)
                {
                    Vec2 ea = poly[e], eb = poly[(e + 1) % n];
                    float s1 = EdgeSide(ea, eb, Pos[a], orient), s2 = EdgeSide(ea, eb, Pos[b], orient), s3 = EdgeSide(ea, eb, Pos[c], orient);
                    if (s1 < 0f || s2 < 0f || s3 < 0f) allIn = false;
                    if (s1 <= 0f && s2 <= 0f && s3 <= 0f) { oneOut = true; break; }
                }
                if ((inside && allIn) || (!inside && oneOut))
                {
                    idx.Add(Keep(a)); idx.Add(Keep(b)); idx.Add(Keep(c));
                    continue;
                }
                if ((inside && oneOut) || (!inside && allIn)) continue;
                var tri = new List<PV>(3) { new PV(Pos[a], Col[a]), new PV(Pos[b], Col[b]), new PV(Pos[c], Col[c]) };
                clip(tri, Emit);
            }
            Pos = pos;
            Col = col;
            Idx = idx;
        }

        /// <summary>
        /// mask-image fades: multiplies every vertex alpha by a piecewise-linear ramp along x (or y when
        /// <paramref name="vertical"/>), in mesh px. The mesh is split at every stop first, so the product stays
        /// exact; outside the ramp the end values hold.
        /// </summary>
        public void ApplyAlphaRamp(bool vertical, IReadOnlyList<(float at, float alpha)> ramp)
        {
            if (ramp == null || ramp.Count == 0) return;
            // a stable sort: two stops at one position are a hard step, in the order given
            var stops = new List<(float at, float alpha)>(ramp.Count);
            foreach (var s in ramp) stops.Add(s);
            for (int i = 1; i < stops.Count; i++)
            {
                var cur = stops[i];
                int j = i - 1;
                while (j >= 0 && stops[j].at > cur.at) { stops[j + 1] = stops[j]; j--; }
                stops[j + 1] = cur;
            }
            var n = vertical ? new Vec2(0f, 1f) : new Vec2(1f, 0f);
            var lines = new List<IsoLine>(stops.Count);
            foreach (var s in stops) lines.Add(new IsoLine(n, s.at));
            SplitByLines(lines);

            // per corner, from the side its triangle lies on (a vertex on a hard step takes both values)
            var pos = new List<Vec2>(Pos.Count);
            var col = new List<Rgba>(Col.Count);
            var idx = new List<int>(Idx.Count);
            var made = new Dictionary<(int, float), int>();
            for (int t = 0; t + 2 < Idx.Count; t += 3)
            {
                float c = 0f;
                for (int k = 0; k < 3; k++) c += vertical ? Pos[Idx[t + k]].Y : Pos[Idx[t + k]].X;
                c /= 3f;
                for (int k = 0; k < 3; k++)
                {
                    int v = Idx[t + k];
                    float x = vertical ? Pos[v].Y : Pos[v].X;
                    float a = RampAt(stops, x, c > x);
                    if (!made.TryGetValue((v, a), out int nv))
                    {
                        pos.Add(Pos[v]);
                        col.Add(Col[v].MulAlpha(a));
                        nv = pos.Count - 1;
                        made[(v, a)] = nv;
                    }
                    idx.Add(nv);
                }
            }
            Pos = pos;
            Col = col;
            Idx = idx;
        }

        /// <summary>The ramp at x (sorted stops; the end values hold outside). On a hard step, the value after it.</summary>
        public static float RampAt(IReadOnlyList<(float at, float alpha)> sorted, float x) => RampAt(sorted, x, true);

        /// <summary>The ramp at x; on a hard step (two stops at x) the value after it when `fromAbove`, else before it.</summary>
        public static float RampAt(IReadOnlyList<(float at, float alpha)> sorted, float x, bool fromAbove)
        {
            const float eps = 1e-4f;
            if (x < sorted[0].at - eps) return sorted[0].alpha;
            int first = -1, last = -1;
            for (int k = 0; k < sorted.Count; k++)
            {
                if (Math.Abs(sorted[k].at - x) <= eps)
                {
                    if (first < 0) first = k;
                    last = k;
                }
            }
            if (first >= 0) return fromAbove ? sorted[last].alpha : sorted[first].alpha;
            for (int k = 1; k < sorted.Count; k++)
            {
                if (x <= sorted[k].at)
                {
                    float span = sorted[k].at - sorted[k - 1].at;
                    if (span <= 0f) return sorted[k].alpha;
                    float t = (x - sorted[k - 1].at) / span;
                    return sorted[k - 1].alpha + (sorted[k].alpha - sorted[k - 1].alpha) * t;
                }
            }
            return sorted[sorted.Count - 1].alpha;
        }

        public void ApplyFilter(ColorFilter f)
        {
            if (f.IsNone) return;
            for (int i = 0; i < Col.Count; i++) Col[i] = f.Apply(Col[i]);
        }

        public void Transform(Affine m)
        {
            for (int i = 0; i < Pos.Count; i++) Pos[i] = m.Apply(Pos[i]);
        }

        /// <summary>
        /// Colours a coverage mesh (vertex alpha = coverage, RGB ignored) with a paint and appends it to
        /// <paramref name="target"/>: linear paints are split along their isolines, radial ones refined, then every
        /// vertex takes paint.At(p) with its alpha multiplied by the coverage.
        /// </summary>
        public static void PaintInto(MeshBuilder coverage, Paint paint, MeshBuilder target)
        {
            if (coverage == null || target == null) return;
            if (paint == null) paint = new SolidPaint(Rgba.White);
            if (paint.IsSolid || coverage.Pos.Count == 0)
            {
                for (int i = 0; i < coverage.Pos.Count; i++)
                    coverage.Col[i] = paint.At(coverage.Pos[i]).MulAlpha(coverage.Col[i].A);
                target.Append(coverage);
                return;
            }
            IEnumerable<IsoLine> bands = paint.Bands(coverage.Bounds());
            if (bands != null) coverage.SplitByLines(bands);
            if (paint.NeedsRefine) coverage.Refine(DefaultRefinePx);

            // A vertex on a hard stop has two colours: each corner is sampled a hair inside its own triangle, and
            // where that differs from the shared sample the corner gets its own vertex.
            int n = coverage.Pos.Count;
            var shared = new Rgba[n];
            for (int i = 0; i < n; i++) shared[i] = paint.At(coverage.Pos[i]);
            var pos = new List<Vec2>(n);
            var col = new List<Rgba>(n);
            var idx = new List<int>(coverage.Idx.Count);
            var plain = new int[n];
            for (int i = 0; i < n; i++) plain[i] = -1;
            for (int t = 0; t + 2 < coverage.Idx.Count; t += 3)
            {
                Vec2 c = (coverage.Pos[coverage.Idx[t]] + coverage.Pos[coverage.Idx[t + 1]] + coverage.Pos[coverage.Idx[t + 2]]) / 3f;
                for (int k = 0; k < 3; k++)
                {
                    int v = coverage.Idx[t + k];
                    Vec2 p = coverage.Pos[v];
                    Rgba inside = paint.At(p + (c - p) * 1e-3f);
                    if (inside.Approximately(shared[v], 1.5f / 255f))
                    {
                        if (plain[v] < 0)
                        {
                            pos.Add(p);
                            col.Add(shared[v].MulAlpha(coverage.Col[v].A));
                            plain[v] = pos.Count - 1;
                        }
                        idx.Add(plain[v]);
                    }
                    else
                    {
                        pos.Add(p);
                        col.Add(inside.MulAlpha(coverage.Col[v].A));
                        idx.Add(pos.Count - 1);
                    }
                }
            }
            int off = target.Pos.Count;
            target.Pos.AddRange(pos);
            target.Col.AddRange(col);
            foreach (int i in idx) target.Idx.Add(i + off);
        }
    }
}
