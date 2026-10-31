// Stroking polylines: caps, joins, dashes, the anti-aliasing fringe of kit.md §4.3 and soft (blurred) bands (§4.4).
//
// Coordinates: the polyline is already in output coordinates (Flatten.Run with the same transform). `m` is the
// transform the line went through: lengths given in user units (StrokeStyle.Width, Dash, DashOffset, Soft's
// halfExtent and profile distance) are scaled by m.MeanScale, except a NonScaling style whose lengths are output px.
// Pass Affine.Identity when the lengths are already in output px. Feather is always in output px.
//
// The band of a stroke of width w with feather f is w + f wide: a solid core |d| ≤ (w − f)/2 and a ramp to alpha 0
// at |d| = (w + f)/2, centred on the true edge, so the coverage integral stays w. A stroke thinner than f keeps
// width f and multiplies its alpha by w / f. Butt and square ends get the same ramp along the line.
//
// The mesh does not double-paint: neighbouring segments meet on the bisector of their joint (miter points on the
// outer side when the join is a miter within the limit, or the turn is under 20°; the inner side always, unless
// the segments are too short for it), so a translucent stroke stays even. Round joins and caps are fans whose rings
// are the band's levels.
using System;
using System.Collections.Generic;

namespace Trial.Geometry
{
    public enum Cap { Butt, Round, Square }

    public enum Join { Miter, Round, Bevel }

    public sealed class StrokeStyle
    {
        public float Width = 1f;
        public Cap Cap = Cap.Butt;
        public Join Join = Join.Miter;
        public float MiterLimit = 4f;
        /// <summary>stroke-dasharray in user units (null or empty = solid). An odd count repeats, as in SVG.</summary>
        public float[] Dash;
        public float DashOffset;
        /// <summary>vector-effect: non-scaling-stroke: Width, Dash and DashOffset are in output px.</summary>
        public bool NonScaling;

        public StrokeStyle() { }

        public StrokeStyle(float width, Cap cap = Cap.Butt, Join join = Join.Miter, float[] dash = null, float dashOffset = 0f)
        {
            Width = width; Cap = cap; Join = join; Dash = dash; DashOffset = dashOffset;
        }

        public StrokeStyle Clone() => new StrokeStyle
        {
            Width = Width, Cap = Cap, Join = Join, MiterLimit = MiterLimit,
            Dash = Dash == null ? null : (float[])Dash.Clone(), DashOffset = DashOffset, NonScaling = NonScaling,
        };
    }

    public static class Stroker
    {
        /// <summary>Chord tolerance (output px) of round joins and caps.</summary>
        public const float FanTolerancePx = 0.1f;
        /// <summary>tan(10°): joins that turn less than 20° meet on the bisector on both sides, whatever the join.</summary>
        private const float SmallTurnTan = 0.17632698f;
        private const float DupEps = 1e-6f;

        /// <summary>Strokes the polyline (dashed when the style says so) into <paramref name="mb"/> with the paint.</summary>
        public static void Stroke(Polyline line, StrokeStyle s, Affine m, float feather, MeshBuilder mb, Paint paint)
        {
            if (line == null || s == null || mb == null || line.Points.Count == 0) return;
            float k = s.NonScaling ? 1f : m.MeanScale;
            float w = s.Width * k;
            if (!(w > 0f)) return;
            float f = feather > 0f ? feather : 0f;

            float coverage = 1f, wEff = w;
            if (f > 0f && w < f) { coverage = w / f; wEff = f; }
            float hc = f > 0f ? (wEff - f) * 0.5f : wEff * 0.5f;
            float ho = f > 0f ? (wEff + f) * 0.5f : wEff * 0.5f;
            float[] offs, alphas;
            if (f <= 0f) { offs = new[] { -ho, 0f, ho }; alphas = new[] { 1f, 1f, 1f }; }
            else if (hc <= 1e-4f) { offs = new[] { -ho, 0f, ho }; alphas = new[] { 0f, coverage, 0f }; }
            else { offs = new[] { -ho, -hc, 0f, hc, ho }; alphas = new[] { 0f, coverage, coverage, coverage, 0f }; }

            var cov = new MeshBuilder();
            var ribbon = new Ribbon(offs, alphas, s.Join, s.MiterLimit, s.Cap, w * 0.5f, f * 0.5f, cov);

            float[] pattern = ScaledPattern(s.Dash, k);
            if (pattern != null)
            {
                foreach (Polyline piece in Dash(line, pattern, s.DashOffset * k)) ribbon.Build(piece.Points, false);
            }
            else
            {
                ribbon.Build(line.Points, line.Closed);
            }
            MeshBuilder.PaintInto(cov, paint, mb);
        }

        private static float[] ScaledPattern(float[] dash, float k)
        {
            if (dash == null || dash.Length == 0) return null;
            double sum = 0;
            foreach (float v in dash)
            {
                if (v < 0f || float.IsNaN(v) || float.IsInfinity(v)) return null;
                sum += v;
            }
            if (sum <= 0) return null;
            var o = new float[dash.Length];
            for (int i = 0; i < dash.Length; i++) o[i] = dash[i] * k;
            return o;
        }

        /// <summary>
        /// Splits the polyline into its dashes (SVG stroke-dasharray / stroke-dashoffset): the pattern starts at the
        /// polyline's first point shifted by <paramref name="offset"/> (may be negative), runs over the closing
        /// segment of a closed line, and each dash is an open polyline (capped by the stroker). A zero on-length
        /// gives a zero-length dash (two equal points: a dot under round caps). An invalid pattern (negative, all
        /// zero) returns the line itself.
        /// </summary>
        public static List<Polyline> Dash(Polyline line, float[] pattern, float offset)
        {
            var result = new List<Polyline>();
            if (line == null || line.Points.Count == 0) return result;
            if (pattern == null || pattern.Length == 0) { result.Add(line); return result; }
            double sum = 0;
            foreach (float v in pattern)
            {
                if (v < 0f || float.IsNaN(v) || float.IsInfinity(v)) { result.Add(line); return result; }
                sum += v;
            }
            if (sum <= 0) { result.Add(line); return result; }
            int n = pattern.Length % 2 == 1 ? pattern.Length * 2 : pattern.Length;
            var pat = new double[n];
            for (int i = 0; i < n; i++) pat[i] = pattern[i % pattern.Length];
            double period = pattern.Length % 2 == 1 ? sum * 2 : sum;

            double phase = offset % period;
            if (phase < 0) phase += period;
            int idx = 0;
            while (phase > 0 && phase >= pat[idx])
            {
                phase -= pat[idx];
                idx = (idx + 1) % n;
            }
            double remaining = pat[idx] - phase;
            bool on = idx % 2 == 0;

            List<Vec2> pts = line.Points;
            int count = pts.Count;
            int segs = line.Closed ? count : count - 1;
            List<Vec2> piece = on ? new List<Vec2> { pts[0] } : null;
            for (int si = 0; si < segs; si++)
            {
                Vec2 a = pts[si], b = pts[(si + 1) % count];
                double len = Vec2.Dist(a, b);
                if (len <= 0) continue;
                double t = 0;
                while (len - t >= remaining)
                {
                    t += remaining;
                    Vec2 q = t >= len ? b : Vec2.Lerp(a, b, (float)(t / len));
                    if (on)
                    {
                        piece.Add(q);
                        result.Add(new Polyline(piece, false));
                        piece = null;
                    }
                    else
                    {
                        piece = new List<Vec2> { q };
                    }
                    idx = (idx + 1) % n;
                    remaining = pat[idx];
                    on = !on;
                }
                remaining -= len - t;
                if (on && piece != null && !(piece[piece.Count - 1] == b)) piece.Add(b);
            }
            if (on && piece != null && piece.Count >= 2) result.Add(new Polyline(piece, false));
            return result;
        }

        /// <summary>
        /// A soft band along the line: alpha(d) = color.A × profile(d) across it (d = signed distance from the centre
        /// line, in user units: scaled by m.MeanScale), out to ±<paramref name="halfExtent"/>; round joins and ends.
        /// Drop-shadows of strokes use Blur.StrokeShadowProfile; glows any falloff.
        /// </summary>
        public static void Soft(Polyline line, Func<float, float> profile, float halfExtent, Affine m, MeshBuilder mb, Rgba color)
        {
            if (line == null || profile == null || mb == null || line.Points.Count == 0) return;
            float k = m.MeanScale;
            if (!(k > 0f)) k = 1f;
            float h = halfExtent * k;
            if (!(h > 0f)) return;
            int perSide = Math.Max(4, Math.Min(32, (int)Math.Ceiling(h / 1.5f)));
            int L = perSide * 2 + 1;
            var offs = new float[L];
            var alphas = new float[L];
            for (int i = 0; i < L; i++)
            {
                float o = -h + 2f * h * i / (L - 1);
                if (i == perSide) o = 0f;
                offs[i] = o;
                float a = profile(o / k);
                alphas[i] = a < 0f ? 0f : a > 1f ? 1f : a;
            }
            var cov = new MeshBuilder();
            new Ribbon(offs, alphas, Join.Round, 4f, Cap.Round, h, 0f, cov).Build(line.Points, line.Closed);
            MeshBuilder.PaintInto(cov, new SolidPaint(color), mb);
        }

        // ---- the band builder ------------------------------------------------------------------------------

        private sealed class Ribbon
        {
            private readonly float[] _offs, _alphas;   // ascending offsets, symmetric, with 0 in the middle
            private readonly Join _join;
            private readonly float _miterLimit;
            private readonly Cap _cap;
            private readonly float _halfWidth;         // true half width (square caps, zero-length squares)
            private readonly float _capFringe;          // half the feather: the ramp at butt / square ends
            private readonly MeshBuilder _mb;
            private readonly int _mid;

            public Ribbon(float[] offs, float[] alphas, Join join, float miterLimit, Cap cap, float halfWidth, float capFringe, MeshBuilder mb)
            {
                _offs = offs; _alphas = alphas; _join = join; _miterLimit = miterLimit; _cap = cap;
                _halfWidth = halfWidth; _capFringe = capFringe; _mb = mb; _mid = offs.Length / 2;
            }

            public void Build(List<Vec2> raw, bool closed)
            {
                if (raw == null || raw.Count == 0) return;
                var pts = new List<Vec2>(raw.Count);
                foreach (Vec2 p in raw)
                    if (pts.Count == 0 || !Near(pts[pts.Count - 1], p)) pts.Add(p);
                if (closed) while (pts.Count > 1 && Near(pts[pts.Count - 1], pts[0])) pts.RemoveAt(pts.Count - 1);

                if (pts.Count < 2)
                {
                    ZeroLength(pts[0]);
                    return;
                }

                int count = pts.Count;
                int nSeg = closed ? count : count - 1;
                int L = _offs.Length;
                var dir = new Vec2[nSeg];
                var nor = new Vec2[nSeg];
                var len = new float[nSeg];
                for (int i = 0; i < nSeg; i++)
                {
                    Vec2 d = pts[(i + 1) % count] - pts[i];
                    len[i] = d.Len;
                    dir[i] = d / len[i];
                    nor[i] = dir[i].Perp;
                }
                var st = new Vec2[nSeg, L];
                var en = new Vec2[nSeg, L];
                for (int i = 0; i < nSeg; i++)
                {
                    Vec2 a = pts[i], b = pts[(i + 1) % count];
                    for (int k = 0; k < L; k++)
                    {
                        st[i, k] = a + nor[i] * _offs[k];
                        en[i, k] = b + nor[i] * _offs[k];
                    }
                }

                int firstJoin = closed ? 0 : 1;
                int lastJoin = closed ? count - 1 : count - 2;
                for (int j = firstJoin; j <= lastJoin; j++)
                {
                    int A = (j - 1 + nSeg) % nSeg, B = j % nSeg;
                    JoinAt(pts[j], dir[A], nor[A], len[A], dir[B], nor[B], len[B], en, A, st, B);
                }

                if (!closed)
                {
                    // A butt end pulls the band back by f/2 (its ramp is centred on the true end); never by more than
                    // half the end segment, whose other half may already be taken by the inner miter of the join
                    // (or the other end's pull-back when it is the only segment), or the segment's band turns over.
                    CapEnd(pts[count - 1], dir[nSeg - 1], nor[nSeg - 1], en, nSeg - 1, len[nSeg - 1]);
                    CapEnd(pts[0], -dir[0], -nor[0], st, 0, len[0], mirrored: true);
                }

                for (int i = 0; i < nSeg; i++)
                {
                    for (int k = 0; k + 1 < L; k++)
                        Quad(st[i, k], _alphas[k], st[i, k + 1], _alphas[k + 1], en[i, k + 1], _alphas[k + 1], en[i, k], _alphas[k]);
                }
            }

            private static bool Near(Vec2 a, Vec2 b) => Math.Abs(a.X - b.X) <= DupEps && Math.Abs(a.Y - b.Y) <= DupEps;

            private void JoinAt(Vec2 p, Vec2 dA, Vec2 nA, float lenA, Vec2 dB, Vec2 nB, float lenB, Vec2[,] en, int a, Vec2[,] st, int b)
            {
                int L = _offs.Length;
                float dot = Vec2.Dot(dA, dB);
                float cross = Vec2.Cross(dA, dB);
                if (1f + dot < 1e-6f)
                {
                    // a reversal: both ends stay square; a round join is a half disc ahead of segment A
                    if (_join == Join.Round)
                        Fan(p, Atan2(nA), -(float)Math.PI, RingsFrom(+1), RingsFrom(-1));
                    return;
                }
                Vec2 mv = (nA + nB) / (1f + dot);                    // |mv| = 1 / cos(half turn)
                float tanHalf = Math.Abs(cross) / (1f + dot);
                float outerSign = cross > 0f ? -1f : 1f;             // turning clockwise on screen: the outer side is −n
                float miterRatio = mv.Len;
                bool straight = Math.Abs(cross) < 1e-7f && dot > 0f;
                bool outerShared = straight || tanHalf <= SmallTurnTan || (_join == Join.Miter && miterRatio <= _miterLimit);
                float maxTrim = 0.5f * Math.Min(lenA, lenB);
                for (int k = 0; k < L; k++)
                {
                    float o = _offs[k];
                    bool inner = o * outerSign < 0f;
                    bool shared = inner ? Math.Abs(o) * tanHalf <= maxTrim : outerShared;
                    if (shared)
                    {
                        Vec2 q = p + mv * o;
                        en[a, k] = q;
                        st[b, k] = q;
                    }
                }
                if (outerShared) return;
                Vec2 uA = nA * outerSign, uB = nB * outerSign;
                float sweep = (float)Math.Atan2(Vec2.Cross(uA, uB), Vec2.Dot(uA, uB));
                (float r, float a)[] rings = RingsFrom(outerSign);
                if (_join == Join.Round) Fan(p, Atan2(uA), sweep, rings, rings);
                else Bevel(p, uA, uB, rings);
            }

            /// <summary>The band's levels on one side as rings (radius, alpha), from the centre out.</summary>
            private (float r, float a)[] RingsFrom(float side)
            {
                var rings = new (float r, float a)[_mid + 1];
                for (int i = 0; i <= _mid; i++)
                {
                    int k = side > 0f ? _mid + i : _mid - i;
                    rings[i] = (Math.Abs(_offs[k]), _alphas[k]);
                }
                return rings;
            }

            private void CapEnd(Vec2 p, Vec2 d, Vec2 n, Vec2[,] edge, int seg, float segLen, bool mirrored = false)
            {
                int L = _offs.Length;
                if (_cap == Cap.Round)
                {
                    // spokes from +n (the level +r of this end, which is −r of the segment when mirrored) round through d
                    (float r, float a)[] r0 = RingsFrom(mirrored ? -1f : 1f);
                    (float r, float a)[] r1 = RingsFrom(mirrored ? 1f : -1f);
                    Fan(p, Atan2(n), -(float)Math.PI, r0, r1);
                    return;
                }
                float ext = _cap == Cap.Square ? _halfWidth : 0f;
                float shift = ext - _capFringe;
                if (shift < 0f) shift = Math.Max(shift, -0.5f * segLen);
                for (int k = 0; k < L; k++) edge[seg, k] = edge[seg, k] + d * shift;
                if (_capFringe <= 0f) return;
                float grow = ext + _capFringe - shift;
                // where the pull-back was cut short, the ramp starts at the value the centred ramp has there
                float start = Math.Min(1f, grow / (2f * _capFringe));
                for (int k = 0; k + 1 < L; k++)
                {
                    Vec2 a0 = edge[seg, k], a1 = edge[seg, k + 1];
                    Quad(a0, _alphas[k] * start, a1, _alphas[k + 1] * start, a1 + d * grow, 0f, a0 + d * grow, 0f);
                }
            }

            private void ZeroLength(Vec2 p)
            {
                if (_cap == Cap.Round)
                {
                    (float r, float a)[] rings = RingsFrom(1f);
                    Fan(p, 0f, 2f * (float)Math.PI, rings, rings, closedFan: true);
                }
                else if (_cap == Cap.Square)
                {
                    // SVG 2: a zero-length subpath with square caps is a square aligned with the x axis
                    var pts = new List<Vec2> { new Vec2(p.X - _halfWidth, p.Y), new Vec2(p.X + _halfWidth, p.Y) };
                    new Ribbon(_offs, _alphas, _join, _miterLimit, Cap.Butt, _halfWidth, _capFringe, _mb).Build(pts, false);
                }
            }

            private static float Atan2(Vec2 v) => (float)Math.Atan2(v.Y, v.X);

            /// <summary>
            /// A fan of rings around c from angle a0 over `sweep` (radians, y-down screen angles). Ring alphas go from
            /// r0 at the first spoke to r1 at the last (equal for joins; the two sides of a cap otherwise).
            /// The inner spokes sit slightly outside the circle so the polygon keeps the sector's area (a 4 px dot
            /// stays as heavy as the browser's); the end spokes stay on it to meet the band (unless `closedFan`).
            /// </summary>
            private void Fan(Vec2 c, float a0, float sweep, (float r, float a)[] r0, (float r, float a)[] r1, bool closedFan = false)
            {
                float rmax = r0[r0.Length - 1].r;
                int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / Flatten.StepAngle(rmax, FanTolerancePx)));
                double step = Math.Abs(sweep) / steps;
                double sRatio = Math.Abs(sweep) / Math.Sin(step);   // Σ sector / triangle area ratio
                double k = 1;
                if (closedFan) k = Math.Sqrt(step / Math.Sin(step));
                else if (steps == 2) k = sRatio / 2;
                else if (steps > 2) k = (-1 + Math.Sqrt(1 + (steps - 2) * sRatio)) / (steps - 2);
                int R = r0.Length;
                var white = new Rgba(1f, 1f, 1f, 1f);
                int centre = _mb.AddVertex(c, white.WithAlpha(r0[0].a));
                int prevBase = -1;
                for (int s = 0; s <= steps; s++)
                {
                    float t = (float)s / steps;
                    double ang = a0 + sweep * t;
                    float scale = closedFan || (s > 0 && s < steps) ? (float)k : 1f;
                    var u = new Vec2((float)Math.Cos(ang) * scale, (float)Math.Sin(ang) * scale);
                    int b0 = _mb.Pos.Count;
                    for (int i = 1; i < R; i++)
                    {
                        float a = r0[i].a + (r1[i].a - r0[i].a) * t;
                        _mb.AddVertex(c + u * r0[i].r, white.WithAlpha(a));
                    }
                    if (prevBase >= 0)
                    {
                        _mb.AddTri(centre, prevBase, b0);
                        for (int i = 1; i + 1 < R; i++)
                        {
                            int p0 = prevBase + i - 1, p1 = prevBase + i, q0 = b0 + i - 1, q1 = b0 + i;
                            _mb.AddTri(p0, p1, q1);
                            _mb.AddTri(p0, q1, q0);
                        }
                    }
                    prevBase = b0;
                }
            }

            private void Bevel(Vec2 c, Vec2 uA, Vec2 uB, (float r, float a)[] rings)
            {
                var white = new Rgba(1f, 1f, 1f, 1f);
                int centre = _mb.AddVertex(c, white.WithAlpha(rings[0].a));
                int R = rings.Length;
                int pa = -1, pb = -1;
                for (int i = 1; i < R; i++)
                {
                    int qa = _mb.AddVertex(c + uA * rings[i].r, white.WithAlpha(rings[i].a));
                    int qb = _mb.AddVertex(c + uB * rings[i].r, white.WithAlpha(rings[i].a));
                    if (i == 1) _mb.AddTri(centre, qa, qb);
                    else { _mb.AddTri(pa, qa, qb); _mb.AddTri(pa, qb, pb); }
                    pa = qa; pb = qb;
                }
            }

            private void Quad(Vec2 p0, float a0, Vec2 p1, float a1, Vec2 p2, float a2, Vec2 p3, float a3)
            {
                int i0 = V(p0, a0), i1 = V(p1, a1), i2 = V(p2, a2), i3 = V(p3, a3);
                _mb.AddTri(i0, i1, i2);
                _mb.AddTri(i0, i2, i3);
            }

            // band vertices with the same position and alpha are shared (neighbouring strips and segments)
            private readonly Dictionary<(float, float, float), int> _shared = new Dictionary<(float, float, float), int>();

            private int V(Vec2 p, float a)
            {
                var key = (p.X, p.Y, a);
                if (_shared.TryGetValue(key, out int i)) return i;
                i = _mb.AddVertex(p, new Rgba(1f, 1f, 1f, a));
                _shared[key] = i;
                return i;
            }
        }
    }
}
