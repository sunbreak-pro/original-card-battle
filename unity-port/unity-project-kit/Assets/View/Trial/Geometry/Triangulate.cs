// Filling a simple polygon: ear clipping (either winding; duplicate and collinear points are dropped first).
// Every shipped fill is one simple ring per subpath (kit.md §4.6); IsSimple is what the tests assert over them.
using System;
using System.Collections.Generic;

namespace Trial.Geometry
{
    public static class Triangulate
    {
        private const double AreaEps = 1e-9;

        /// <summary>Fills the ring (closed implicitly) with the paint: ear clipping, CW/CCW agnostic, dedupes points.</summary>
        public static void Fill(Polyline ring, MeshBuilder mb, Paint paint)
        {
            if (ring == null || mb == null) return;
            var cov = new MeshBuilder();
            FillCoverage(Clean(ring.Points), cov, 1f);
            MeshBuilder.PaintInto(cov, paint, mb);
        }

        /// <summary>
        /// A concave fill with the anti-aliasing of kit.md §4.3: the triangulated core plus an outer fringe along the
        /// outline. The fringe is the outer half of a ramp centred on the outline (the browser's coverage: ½ on the
        /// edge, 0 at f/2 outside), so it starts at alpha ½ on the outline and ends at 0 half a
        /// <paramref name="feather"/> (output px) out; its inner half would lie under the core, where a translucent
        /// paint must not be painted twice. A full 1 → 0 ramp outside the outline would grow every filled icon and
        /// figure by f/2 on each side (a 16 px pip by about a quarter of its area).
        /// </summary>
        public static void FillAA(Polyline ring, MeshBuilder mb, Paint paint, float feather)
        {
            if (ring == null || mb == null) return;
            List<Vec2> pts = Clean(ring.Points);
            var cov = new MeshBuilder();
            FillCoverage(pts, cov, 1f);
            if (feather > 0f && pts.Count >= 3) Fringe(pts, feather, cov);
            MeshBuilder.PaintInto(cov, paint, mb);
        }

        /// <summary>
        /// True when the ring (closed implicitly) has at least three distinct points, a non-zero area and no two
        /// edges that cross or touch other than neighbours at their shared point.
        /// </summary>
        public static bool IsSimple(Polyline ring)
        {
            if (ring == null) return false;
            List<Vec2> p = DedupeOnly(ring.Points);
            int n = p.Count;
            if (n < 3) return false;
            if (Math.Abs(SignedArea2(p)) <= AreaEps) return false;
            for (int i = 0; i < n; i++)
            {
                Vec2 a = p[i], b = p[(i + 1) % n];
                for (int j = i + 1; j < n; j++)
                {
                    Vec2 c = p[j], d = p[(j + 1) % n];
                    bool adjacentNext = j == i + 1;
                    bool adjacentWrap = i == 0 && j == n - 1;
                    if (adjacentNext)
                    {
                        // b == c: they may only meet there; a reversal along the same line overlaps
                        if (Backtracks(a, b, d)) return false;
                        continue;
                    }
                    if (adjacentWrap)
                    {
                        // d == a
                        if (Backtracks(c, a, b)) return false;
                        continue;
                    }
                    if (SegmentsTouch(a, b, c, d)) return false;
                }
            }
            return true;
        }

        // ---- internals --------------------------------------------------------------------------------

        private static List<Vec2> DedupeOnly(List<Vec2> src)
        {
            var o = new List<Vec2>(src.Count);
            foreach (Vec2 v in src)
                if (o.Count == 0 || !Same(o[o.Count - 1], v)) o.Add(v);
            while (o.Count > 1 && Same(o[o.Count - 1], o[0])) o.RemoveAt(o.Count - 1);
            return o;
        }

        /// <summary>Dedupe, then drop collinear points (straight-through and spikes) until none is left.</summary>
        private static List<Vec2> Clean(List<Vec2> src)
        {
            List<Vec2> o = DedupeOnly(src);
            bool changed = true;
            while (changed && o.Count >= 3)
            {
                changed = false;
                for (int i = 0; i < o.Count && o.Count >= 3; i++)
                {
                    Vec2 a = o[(i - 1 + o.Count) % o.Count], b = o[i], c = o[(i + 1) % o.Count];
                    double cr = Cross(a, b, c);
                    double scale = Math.Max((b - a).LenSq, (c - b).LenSq);
                    if (Math.Abs(cr) <= 1e-10 * Math.Max(scale, 1e-6) || Same(a, c))
                    {
                        o.RemoveAt(i);
                        i--;
                        changed = true;
                    }
                }
            }
            return o;
        }

        private static bool Same(Vec2 a, Vec2 b) => Math.Abs(a.X - b.X) <= 1e-6f && Math.Abs(a.Y - b.Y) <= 1e-6f;

        private static double Cross(Vec2 a, Vec2 b, Vec2 c) =>
            ((double)b.X - a.X) * ((double)c.Y - b.Y) - ((double)b.Y - a.Y) * ((double)c.X - b.X);

        private static double SignedArea2(List<Vec2> p)
        {
            double a = 0;
            for (int i = 0; i < p.Count; i++)
            {
                Vec2 u = p[i], v = p[(i + 1) % p.Count];
                a += (double)u.X * v.Y - (double)v.X * u.Y;
            }
            return a;
        }

        private static double Orient(Vec2 a, Vec2 b, Vec2 c) =>
            ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);

        private static bool OnSegment(Vec2 a, Vec2 b, Vec2 p) =>
            Math.Min(a.X, b.X) - 1e-9 <= p.X && p.X <= Math.Max(a.X, b.X) + 1e-9 &&
            Math.Min(a.Y, b.Y) - 1e-9 <= p.Y && p.Y <= Math.Max(a.Y, b.Y) + 1e-9;

        private static bool SegmentsTouch(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
        {
            double eps = 1e-12;
            double o1 = Orient(a, b, c), o2 = Orient(a, b, d), o3 = Orient(c, d, a), o4 = Orient(c, d, b);
            if (((o1 > eps && o2 < -eps) || (o1 < -eps && o2 > eps)) && ((o3 > eps && o4 < -eps) || (o3 < -eps && o4 > eps))) return true;
            if (Math.Abs(o1) <= eps && OnSegment(a, b, c)) return true;
            if (Math.Abs(o2) <= eps && OnSegment(a, b, d)) return true;
            if (Math.Abs(o3) <= eps && OnSegment(c, d, a)) return true;
            if (Math.Abs(o4) <= eps && OnSegment(c, d, b)) return true;
            return false;
        }

        /// <summary>Edges a→b and b→c overlap (c turns straight back along a→b).</summary>
        private static bool Backtracks(Vec2 a, Vec2 b, Vec2 c)
        {
            double cr = Orient(a, b, c);
            if (Math.Abs(cr) > 1e-12) return false;
            double dot = ((double)b.X - a.X) * ((double)c.X - b.X) + ((double)b.Y - a.Y) * ((double)c.Y - b.Y);
            return dot < 0;
        }

        private static bool PointInTri(Vec2 p, Vec2 a, Vec2 b, Vec2 c, double sign)
        {
            // inclusive of the edges (a reflex vertex on the diagonal blocks the ear)
            double d1 = Orient(a, b, p) * sign, d2 = Orient(b, c, p) * sign, d3 = Orient(c, a, p) * sign;
            return d1 >= -1e-12 && d2 >= -1e-12 && d3 >= -1e-12;
        }

        private static void FillCoverage(List<Vec2> pts, MeshBuilder cov, float alpha)
        {
            int n = pts.Count;
            if (n < 3) return;
            double area = SignedArea2(pts);
            if (Math.Abs(area) <= AreaEps) return;
            double sign = area > 0 ? 1 : -1;
            var white = new Rgba(1f, 1f, 1f, alpha);
            int b0 = cov.Pos.Count;
            for (int i = 0; i < n; i++) cov.AddVertex(pts[i], white);

            var prev = new int[n];
            var next = new int[n];
            for (int i = 0; i < n; i++) { prev[i] = (i - 1 + n) % n; next[i] = (i + 1) % n; }
            int remaining = n;
            int v = 0;
            int stall = 0;
            while (remaining > 3)
            {
                int a = prev[v], c = next[v];
                if (IsEar(pts, prev, next, a, v, c, sign))
                {
                    cov.AddTri(b0 + a, b0 + v, b0 + c);
                    next[a] = c;
                    prev[c] = a;
                    remaining--;
                    v = c;
                    stall = 0;
                    continue;
                }
                v = next[v];
                if (++stall > remaining)
                {
                    // no ear (numerical trouble or a non-simple ring): cut the most convex corner and carry on
                    int best = v;
                    double bestCross = double.NegativeInfinity;
                    int w = v;
                    for (int k = 0; k < remaining; k++)
                    {
                        double cr = Cross(pts[prev[w]], pts[w], pts[next[w]]) * sign;
                        if (cr > bestCross) { bestCross = cr; best = w; }
                        w = next[w];
                    }
                    int pa = prev[best], pc = next[best];
                    if (bestCross > 0) cov.AddTri(b0 + pa, b0 + best, b0 + pc);
                    next[pa] = pc;
                    prev[pc] = pa;
                    remaining--;
                    v = pc;
                    stall = 0;
                }
            }
            {
                int a = prev[v], c = next[v];
                if (Math.Abs(Cross(pts[a], pts[v], pts[c])) > 0) cov.AddTri(b0 + a, b0 + v, b0 + c);
            }
        }

        private static bool IsEar(List<Vec2> pts, int[] prev, int[] next, int a, int b, int c, double sign)
        {
            Vec2 pa = pts[a], pb = pts[b], pc = pts[c];
            if (Cross(pa, pb, pc) * sign <= 0) return false;
            for (int w = next[c]; w != a; w = next[w])
            {
                Vec2 p = pts[w];
                // only reflex (or flat) vertices can sit inside an ear of a simple polygon
                if (Cross(pts[prev[w]], p, pts[next[w]]) * sign > 0) continue;
                if (Same(p, pa) || Same(p, pb) || Same(p, pc)) continue;
                if (PointInTri(p, pa, pb, pc, sign)) return false;
            }
            return true;
        }

        /// <summary>Alpha of the fringe on the outline: the centred coverage ramp is ½ on the edge.</summary>
        internal const float FringeEdgeAlpha = 0.5f;

        /// <summary>The outer fringe: a strip from the outline (alpha ½) to the outline grown by f/2 (alpha 0).</summary>
        private static void Fringe(List<Vec2> pts, float feather, MeshBuilder cov)
        {
            float f = feather * 0.5f;
            int n = pts.Count;
            double area = SignedArea2(pts);
            // positive area = clockwise on a y-down screen: the outward normal of edge a→b is −Perp(b − a)
            float outward = area > 0 ? -1f : 1f;
            var off = new Vec2[n];
            for (int i = 0; i < n; i++)
            {
                Vec2 p0 = pts[(i - 1 + n) % n], p1 = pts[i], p2 = pts[(i + 1) % n];
                Vec2 n0 = (p1 - p0).Norm.Perp * outward, n1 = (p2 - p1).Norm.Perp * outward;
                Vec2 sum = n0 + n1;
                float dot = Vec2.Dot(n0, n1);
                Vec2 m;
                if (1f + dot < 1e-4f) m = n0;
                else
                {
                    m = sum / (1f + dot);                  // the miter vector, |m| = 1 / cos(half turn)
                    float len = m.Len;
                    if (len > 4f) m = m * (4f / len);      // keep sharp spikes from shooting the fringe far out
                }
                off[i] = p1 + m * f;
            }
            var solid = new Rgba(1f, 1f, 1f, FringeEdgeAlpha);
            var clear = new Rgba(1f, 1f, 1f, 0f);
            int b0 = cov.Pos.Count;
            for (int i = 0; i < n; i++)
            {
                cov.AddVertex(pts[i], solid);
                cov.AddVertex(off[i], clear);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int a = b0 + 2 * i, ao = a + 1, b = b0 + 2 * j, bo = b + 1;
                cov.AddTri(a, b, bo);
                cov.AddTri(a, bo, ao);
            }
        }
    }
}
