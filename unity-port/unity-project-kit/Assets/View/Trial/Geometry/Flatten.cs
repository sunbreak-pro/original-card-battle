// Path → polylines in output coordinates.
//  - Lines as they are; cubics and quads by recursive subdivision until every control point lies within the
//    tolerance of the chord (in output px, after the transform: an affine map keeps a Bézier a Bézier).
//  - Arcs by SVG 1.1 implementation notes F.6.5 (endpoint → centre) with the F.6.6 corrections (zero radius → a
//    line, equal endpoints → omitted, negative radii → absolute, radii scaled up when the endpoints do not fit),
//    stepped at 2·acos(1 − tol / r) with r the largest radius in output px.
using System;
using System.Collections.Generic;

namespace Trial.Geometry
{
    public sealed class Polyline
    {
        public List<Vec2> Points;
        public bool Closed;

        public Polyline() { Points = new List<Vec2>(); }
        public Polyline(List<Vec2> points, bool closed) { Points = points ?? new List<Vec2>(); Closed = closed; }

        public int Count => Points.Count;

        /// <summary>Sum of the segment lengths, plus the closing segment when Closed.</summary>
        public float Length
        {
            get
            {
                double len = 0;
                for (int i = 1; i < Points.Count; i++) len += Vec2.Dist(Points[i - 1], Points[i]);
                if (Closed && Points.Count > 1) len += Vec2.Dist(Points[Points.Count - 1], Points[0]);
                return (float)len;
            }
        }

        /// <summary>Shoelace area; positive = clockwise on a y-down screen.</summary>
        public float SignedArea
        {
            get
            {
                double a = 0;
                int n = Points.Count;
                for (int i = 0; i < n; i++)
                {
                    Vec2 p = Points[i], q = Points[(i + 1) % n];
                    a += (double)p.X * q.Y - (double)q.X * p.Y;
                }
                return (float)(a * 0.5);
            }
        }

        public RectF Bounds => RectF.Bounds(Points);

        public Polyline Transformed(Affine m)
        {
            var pts = new List<Vec2>(Points.Count);
            foreach (Vec2 p in Points) pts.Add(m.Apply(p));
            return new Polyline(pts, Closed);
        }
    }

    /// <summary>An SVG arc in centre parameterisation (F.6.5). Angles in radians, in the ellipse's own frame.</summary>
    public readonly struct ArcCenter
    {
        public readonly Vec2 Center;
        public readonly float Rx, Ry;
        /// <summary>x-axis rotation in radians.</summary>
        public readonly float Phi;
        public readonly float Theta1, DTheta;
        /// <summary>True when the arc degenerates to a straight line (a zero radius).</summary>
        public readonly bool IsLine;
        /// <summary>True when the endpoints coincide (the arc is omitted).</summary>
        public readonly bool IsEmpty;

        public ArcCenter(Vec2 center, float rx, float ry, float phi, float theta1, float dTheta, bool isLine, bool isEmpty)
        {
            Center = center; Rx = rx; Ry = ry; Phi = phi; Theta1 = theta1; DTheta = dTheta; IsLine = isLine; IsEmpty = isEmpty;
        }

        public Vec2 PointAt(double theta)
        {
            double cp = Math.Cos(Phi), sp = Math.Sin(Phi), ct = Math.Cos(theta), st = Math.Sin(theta);
            return new Vec2((float)(Center.X + Rx * cp * ct - Ry * sp * st), (float)(Center.Y + Rx * sp * ct + Ry * cp * st));
        }
    }

    public static class Flatten
    {
        public const float DefaultTolerancePx = 0.2f;
        private const int MaxDepth = 18;
        private const float DupEps = 1e-5f;

        /// <summary>
        /// Lines, cubics, quads, arcs → one polyline per subpath that has segments, in output coordinates (after
        /// <paramref name="m"/>); tolerance in output px. Closed subpaths do not repeat their first point.
        /// A zero-length subpath comes out as two equal points (round caps draw it as a dot). A lone moveto draws
        /// nothing, but a moveto closed at once ("M x y Z") is a zero-length subpath (SVG 2; Skia strokes it as
        /// a zero-length line).
        /// </summary>
        public static List<Polyline> Run(PathData path, Affine m, float tolerancePx = DefaultTolerancePx)
        {
            var result = new List<Polyline>();
            if (path == null) return result;
            float tol = tolerancePx > 0f ? tolerancePx : DefaultTolerancePx;
            foreach (SubPath sp in path.SubPaths)
            {
                if (sp.Segments.Count == 0)
                {
                    if (sp.Closed)
                    {
                        Vec2 p = m.Apply(sp.Start);
                        result.Add(new Polyline(new List<Vec2> { p, p }, true));
                    }
                    continue;
                }
                var pts = new List<Vec2> { m.Apply(sp.Start) };
                foreach (PathSegment seg in sp.Segments)
                {
                    switch (seg.Kind)
                    {
                        case SegmentKind.Line:
                            pts.Add(m.Apply(seg.To));
                            break;
                        case SegmentKind.Cubic:
                            Cubic(m.Apply(seg.From), m.Apply(seg.C1), m.Apply(seg.C2), m.Apply(seg.To), tol, pts);
                            break;
                        case SegmentKind.Quad:
                            Quad(m.Apply(seg.From), m.Apply(seg.C1), m.Apply(seg.To), tol, pts);
                            break;
                        case SegmentKind.Arc:
                            Arc(seg, m, tol, pts);
                            break;
                    }
                }
                result.Add(new Polyline(Clean(pts, sp.Closed), sp.Closed));
            }
            return result;
        }

        /// <summary>Parse (cached) and flatten.</summary>
        public static List<Polyline> Run(string d, Affine m, float tolerancePx = DefaultTolerancePx) =>
            Run(PathData.Cached(d), m, tolerancePx);

        /// <summary>Removes consecutive duplicates and, when closed, a last point equal to the first; keeps ≥ 2 points.</summary>
        internal static List<Vec2> Clean(List<Vec2> pts, bool closed)
        {
            var o = new List<Vec2>(pts.Count);
            foreach (Vec2 p in pts)
                if (o.Count == 0 || !Near(o[o.Count - 1], p)) o.Add(p);
            if (closed && o.Count > 2 && Near(o[o.Count - 1], o[0])) o.RemoveAt(o.Count - 1);
            if (o.Count == 1) o.Add(o[0]);
            return o;
        }

        private static bool Near(Vec2 a, Vec2 b) => Math.Abs(a.X - b.X) <= DupEps && Math.Abs(a.Y - b.Y) <= DupEps;

        /// <summary>Appends the cubic's points after p0 (p0 itself is not added).</summary>
        public static void Cubic(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, float tol, List<Vec2> outPts) =>
            CubicRec(p0, p1, p2, p3, tol, outPts, 0);

        /// <summary>Appends the quad's points after p0 (degree-elevated to a cubic).</summary>
        public static void Quad(Vec2 p0, Vec2 c, Vec2 p2, float tol, List<Vec2> outPts)
        {
            Vec2 c1 = p0 + (c - p0) * (2f / 3f);
            Vec2 c2 = p2 + (c - p2) * (2f / 3f);
            CubicRec(p0, c1, c2, p2, tol, outPts, 0);
        }

        private static void CubicRec(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, float tol, List<Vec2> outPts, int depth)
        {
            if (depth >= MaxDepth || (DistToSegment(p1, p0, p3) <= tol && DistToSegment(p2, p0, p3) <= tol))
            {
                outPts.Add(p3);
                return;
            }
            Vec2 p01 = (p0 + p1) * 0.5f, p12 = (p1 + p2) * 0.5f, p23 = (p2 + p3) * 0.5f;
            Vec2 p012 = (p01 + p12) * 0.5f, p123 = (p12 + p23) * 0.5f;
            Vec2 mid = (p012 + p123) * 0.5f;
            CubicRec(p0, p01, p012, mid, tol, outPts, depth + 1);
            CubicRec(mid, p123, p23, p3, tol, outPts, depth + 1);
        }

        public static float DistToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            float l2 = ab.LenSq;
            if (l2 <= 1e-12f) return (p - a).Len;
            float t = Vec2.Dot(p - a, ab) / l2;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return (p - (a + ab * t)).Len;
        }

        /// <summary>SVG F.6.5 endpoint → centre conversion with the F.6.6 out-of-range corrections.</summary>
        public static ArcCenter ArcToCenter(Vec2 p1, Vec2 p2, float rx, float ry, float xAxisRotationDeg, bool largeArc, bool sweep)
        {
            if (p1.X == p2.X && p1.Y == p2.Y) return new ArcCenter(p1, 0f, 0f, 0f, 0f, 0f, false, true);
            double arx = Math.Abs((double)rx), ary = Math.Abs((double)ry);
            if (arx == 0 || ary == 0) return new ArcCenter(p1, 0f, 0f, 0f, 0f, 0f, true, false);

            double phi = (xAxisRotationDeg % 360.0) * Math.PI / 180.0;
            double cphi = Math.Cos(phi), sphi = Math.Sin(phi);
            double dx2 = (p1.X - (double)p2.X) / 2.0, dy2 = (p1.Y - (double)p2.Y) / 2.0;
            double x1p = cphi * dx2 + sphi * dy2;
            double y1p = -sphi * dx2 + cphi * dy2;

            // F.6.6 step 3: scale the radii up when the endpoints do not fit
            double lambda = x1p * x1p / (arx * arx) + y1p * y1p / (ary * ary);
            if (lambda > 1)
            {
                double s = Math.Sqrt(lambda);
                arx *= s;
                ary *= s;
            }

            double rx2 = arx * arx, ry2 = ary * ary;
            double num = rx2 * ry2 - rx2 * y1p * y1p - ry2 * x1p * x1p;
            double den = rx2 * y1p * y1p + ry2 * x1p * x1p;
            double coef = den == 0 ? 0 : Math.Sqrt(Math.Max(0, num / den));
            if (largeArc == sweep) coef = -coef;
            double cxp = coef * arx * y1p / ary;
            double cyp = -coef * ary * x1p / arx;

            double cx = cphi * cxp - sphi * cyp + (p1.X + (double)p2.X) / 2.0;
            double cy = sphi * cxp + cphi * cyp + (p1.Y + (double)p2.Y) / 2.0;

            double ux = (x1p - cxp) / arx, uy = (y1p - cyp) / ary;
            double vx = (-x1p - cxp) / arx, vy = (-y1p - cyp) / ary;
            double theta1 = Math.Atan2(uy, ux);
            double dtheta = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
            if (!sweep && dtheta > 0) dtheta -= 2 * Math.PI;
            else if (sweep && dtheta < 0) dtheta += 2 * Math.PI;

            return new ArcCenter(new Vec2((float)cx, (float)cy), (float)arx, (float)ary, (float)phi, (float)theta1, (float)dtheta, false, false);
        }

        /// <summary>The angle step whose chord stays within tol of a circle of radius r (both in output px).</summary>
        public static double StepAngle(double rOut, double tol)
        {
            if (rOut <= tol) return Math.PI / 2;
            double step = 2 * Math.Acos(1 - tol / rOut);
            return Math.Min(step, Math.PI / 2);
        }

        /// <summary>The largest stretch of the transform's linear part (its largest singular value).</summary>
        public static float MaxScale(Affine m)
        {
            double a = m.A, b = m.B, c = m.C, d = m.D;
            double t = a * a + b * b + c * c + d * d;
            double det = a * d - b * c;
            double disc = Math.Sqrt(Math.Max(0, t * t - 4 * det * det));
            return (float)Math.Sqrt((t + disc) / 2);
        }

        private static void Arc(PathSegment seg, Affine m, float tol, List<Vec2> outPts)
        {
            ArcCenter a = ArcToCenter(seg.From, seg.To, seg.Rx, seg.Ry, seg.XAxisRotation, seg.LargeArc, seg.Sweep);
            if (a.IsEmpty) return;
            if (a.IsLine)
            {
                outPts.Add(m.Apply(seg.To));
                return;
            }
            AppendArc(a, m, tol, outPts);
            outPts[outPts.Count - 1] = m.Apply(seg.To); // land exactly on the endpoint
        }

        /// <summary>Appends the arc's points after its start (the start itself is not added), transformed by m.</summary>
        public static void AppendArc(ArcCenter a, Affine m, float tol, List<Vec2> outPts)
        {
            double rOut = Math.Max(a.Rx, a.Ry) * MaxScale(m);
            double step = StepAngle(rOut, tol);
            int n = Math.Max(1, (int)Math.Ceiling(Math.Abs(a.DTheta) / step));
            for (int i = 1; i <= n; i++)
            {
                double th = a.Theta1 + a.DTheta * i / n;
                outPts.Add(m.Apply(a.PointAt(th)));
            }
        }
    }
}
