// CSS and SVG gradients as Paints.
//
// LinearGradient.Css: CSS angle semantics (0deg = to top, clockwise; the gradient line passes through the box centre
// and is |W sinθ| + |H cosθ| long, so the corners sit on the 0 % / 100 % lines). Colour stops are resolved with the
// CSS Images "colour stop fix-up" (first 0 %, last 100 %, positions never go back, missing ones spread evenly) and
// interpolate in premultiplied sRGB, as CSS does. LinearGradient.Points: SVG userSpaceOnUse (x1 y1 x2 y2), stops as
// fractions, unpremultiplied as Chrome paints SVG gradients. Bands() reports one IsoLine per stop (and per repeat)
// so a mesh split along them interpolates the gradient exactly; a segment whose premultiplied interpolation is not
// linear in straight colours (both colours and alphas differ) gets seven extra lines.
//
// RadialGradient: ellipse/circle with CSS size keywords (closest-side, farthest-side, closest-corner,
// farthest-corner), explicit radii (px or % of the box) and `at` positions; px stops are measured along the
// horizontal radius (the CSS gradient ray). Points(): canvas / SVG circles with an optional inner radius. Radial
// paints are not piecewise linear: NeedsRefine is true.
//
// The background specs (LinearGradientSpec, RadialGradientSpec, UrlImage) are the parsed, box-free form a BoxStyle
// keeps; Resolve(box) turns them into Paints once the box is known.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Trial.Geometry
{
    /// <summary>A colour stop. Pos is a fraction of the gradient line (0.55 = 55 %) or px when PosIsPx; NaN = no position.</summary>
    public readonly struct GradientStop : IEquatable<GradientStop>
    {
        public readonly Rgba Color;
        public readonly float Pos;
        public readonly bool PosIsPx;

        public GradientStop(Rgba color, float pos, bool posIsPx = false) { Color = color; Pos = pos; PosIsPx = posIsPx; }

        public bool HasPos => !float.IsNaN(Pos);

        public static GradientStop At(Rgba color, float fraction) => new GradientStop(color, fraction, false);
        public static GradientStop Px(Rgba color, float px) => new GradientStop(color, px, true);
        public static GradientStop Auto(Rgba color) => new GradientStop(color, float.NaN, false);

        public bool Equals(GradientStop o) =>
            Color.Approximately(o.Color, 1f / 1024f) && (float.IsNaN(Pos) ? float.IsNaN(o.Pos) : Math.Abs(Pos - o.Pos) < 1e-4f) && PosIsPx == o.PosIsPx;
        public override bool Equals(object obj) => obj is GradientStop s && Equals(s);
        public override int GetHashCode() => Color.GetHashCode() ^ PosIsPx.GetHashCode();

        public string ToCss()
        {
            string c = CssColor.ToCss(Color);
            if (!HasPos) return c;
            return c + " " + (PosIsPx ? Fmt(Pos) + "px" : Fmt(Pos * 100f) + "%");
        }

        internal static string Fmt(float v) => Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);
    }

    /// <summary>The shared stop machinery of the gradient paints.</summary>
    public abstract class GradientPaint : Paint
    {
        /// <summary>Resolved stop positions along the gradient (0..1 of the line, may lie outside) and their colours.</summary>
        protected float[] T;
        protected Rgba[] C;

        /// <summary>repeating-*-gradient: the stops repeat every (last − first).</summary>
        public bool Repeating;

        /// <summary>CSS interpolates premultiplied (true); SVG gradients interpolate straight colours (false).</summary>
        public bool Premultiplied = true;

        public IReadOnlyList<float> StopPositions => T;
        public IReadOnlyList<Rgba> StopColors => C;

        public override bool IsSolid
        {
            get
            {
                for (int i = 1; i < C.Length; i++) if (!C[i].Approximately(C[0], 0f)) return false;
                return true;
            }
        }

        /// <summary>
        /// CSS Images 3 §3.4.3 colour stop fix-up, px converted with lineLength. Transparent stops borrow the RGB of a
        /// neighbouring visible stop, so straight vertex interpolation next to them matches premultiplied interpolation.
        /// </summary>
        protected void ResolveStops(IReadOnlyList<GradientStop> stops, float lineLength)
        {
            int n = stops?.Count ?? 0;
            if (n == 0)
            {
                T = new[] { 0f };
                C = new[] { Rgba.Transparent };
                return;
            }
            var t = new float[n];
            var c = new Rgba[n];
            for (int i = 0; i < n; i++)
            {
                GradientStop s = stops[i];
                c[i] = s.Color;
                t[i] = !s.HasPos ? float.NaN : s.PosIsPx ? (lineLength > 0f ? s.Pos / lineLength : 0f) : s.Pos;
            }
            if (float.IsNaN(t[0])) t[0] = 0f;
            if (n > 1 && float.IsNaN(t[n - 1])) t[n - 1] = 1f;
            float max = t[0];
            for (int i = 1; i < n; i++)
            {
                if (float.IsNaN(t[i])) continue;
                if (t[i] < max) t[i] = max;
                else max = t[i];
            }
            for (int i = 1; i < n; i++)
            {
                if (!float.IsNaN(t[i])) continue;
                int j = i;
                while (float.IsNaN(t[j])) j++;
                float a = t[i - 1], b = t[j];
                int gaps = j - (i - 1);
                for (int k = i; k < j; k++) t[k] = a + (b - a) * (k - (i - 1)) / gaps;
                i = j;
            }
            if (Premultiplied)
            {
                for (int i = 0; i < n; i++)
                {
                    if (c[i].A > 0f) continue;
                    int src = -1;
                    if (i + 1 < n && c[i + 1].A > 0f) src = i + 1;
                    else if (i > 0 && c[i - 1].A > 0f) src = i - 1;
                    if (src >= 0) c[i] = new Rgba(c[src].R, c[src].G, c[src].B, 0f);
                }
            }
            T = t;
            C = c;
        }

        /// <summary>The colour at gradient parameter t (0 = first stop line, 1 = 100 %).</summary>
        public Rgba ColorAtT(float t)
        {
            int n = T.Length;
            if (n == 1) return C[0];
            if (Repeating)
            {
                float period = T[n - 1] - T[0];
                if (period <= 1e-6f) return C[n - 1];
                float k = (float)Math.Floor((t - T[0]) / period);
                t -= k * period;
            }
            if (t <= T[0]) return C[0];
            if (t >= T[n - 1]) return C[n - 1];
            int i = 0;
            while (i + 1 < n && T[i + 1] <= t) i++;
            if (i + 1 >= n) return C[n - 1];
            float span = T[i + 1] - T[i];
            if (span <= 0f) return C[i + 1];
            float u = (t - T[i]) / span;
            return Premultiplied ? LerpPremulKeep(C[i], C[i + 1], u) : Rgba.LerpStraight(C[i], C[i + 1], u);
        }

        /// <summary>Premultiplied lerp that keeps the stops' (borrowed) RGB where the alpha is 0.</summary>
        internal static Rgba LerpPremulKeep(Rgba a, Rgba b, float u)
        {
            float alpha = a.A + (b.A - a.A) * u;
            if (alpha <= 0f) return Rgba.LerpStraight(a, b, u).WithAlpha(0f);
            float r = (a.R * a.A + (b.R * b.A - a.R * a.A) * u) / alpha;
            float g = (a.G * a.A + (b.G * b.A - a.G * a.A) * u) / alpha;
            float bl = (a.B * a.A + (b.B * b.A - a.B * a.A) * u) / alpha;
            return new Rgba(r, g, bl, alpha);
        }

        /// <summary>The t values (one period, unrepeated) where a mesh has to be cut: every stop, plus sub-lines in non-linear segments.</summary>
        internal List<float> KinkTs()
        {
            var list = new List<float>();
            int n = T.Length;
            for (int i = 0; i < n; i++)
            {
                if (list.Count == 0 || Math.Abs(list[list.Count - 1] - T[i]) > 1e-6f) list.Add(T[i]);
                if (i + 1 < n && Premultiplied && NonLinearSegment(C[i], C[i + 1]) && T[i + 1] - T[i] > 1e-6f)
                    for (int k = 1; k < 8; k++) list.Add(T[i] + (T[i + 1] - T[i]) * k / 8f);
            }
            return list;
        }

        private static bool NonLinearSegment(Rgba a, Rgba b)
        {
            if (Math.Abs(a.A - b.A) <= 1f / 255f) return false;
            return Math.Abs(a.R - b.R) > 1f / 255f || Math.Abs(a.G - b.G) > 1f / 255f || Math.Abs(a.B - b.B) > 1f / 255f;
        }

        protected string StopsCss()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < T.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(CssColor.ToCss(C[i])).Append(' ').Append(GradientStop.Fmt(T[i] * 100f)).Append('%');
            }
            return sb.ToString();
        }
    }

    public sealed class LinearGradient : GradientPaint
    {
        /// <summary>The gradient line: t = 0 at Start, 1 at End.</summary>
        public Vec2 Start { get; private set; }
        public Vec2 End { get; private set; }

        private Vec2 _dir;   // (End − Start) / |End − Start|²
        private float _dot0; // Dot(_dir, Start)

        private LinearGradient() { }

        /// <summary>The CSS gradient line length for an angle on a W×H box: |W sinθ| + |H cosθ|.</summary>
        public static float LineLength(float angleDeg, float w, float h)
        {
            double a = angleDeg * Math.PI / 180.0;
            return (float)(Math.Abs(w * Math.Sin(a)) + Math.Abs(h * Math.Cos(a)));
        }

        /// <summary>CSS linear-gradient(angle, stops) on a box. 180 = top to bottom (the CSS default).</summary>
        public static LinearGradient Css(float angleDeg, RectF box, params GradientStop[] stops) => Css(angleDeg, box, false, stops);

        public static LinearGradient Css(float angleDeg, RectF box, bool repeating, IReadOnlyList<GradientStop> stops)
        {
            double a = angleDeg * Math.PI / 180.0;
            var d = new Vec2((float)Math.Sin(a), (float)-Math.Cos(a));
            float len = LineLength(angleDeg, box.W, box.H);
            Vec2 c = box.Center;
            var g = new LinearGradient { Repeating = repeating, Premultiplied = true };
            g.SetLine(c - d * (len * 0.5f), c + d * (len * 0.5f));
            g.ResolveStops(stops, len);
            return g;
        }

        /// <summary>The CSS angle of "to top", "to bottom right", ... on a W×H box (the corner forms put the 50 % line through the other two corners).</summary>
        public static float SideAngle(string toWhat, float w, float h)
        {
            string s = toWhat.Trim().ToLowerInvariant();
            if (s.StartsWith("to ", StringComparison.Ordinal)) s = s.Substring(3).Trim();
            bool top = s.Contains("top"), bottom = s.Contains("bottom"), left = s.Contains("left"), right = s.Contains("right");
            if (!(top || bottom) && !(left || right)) return 180f;
            if (!(left || right)) return top ? 0f : 180f;
            if (!(top || bottom)) return right ? 90f : 270f;
            // corner: the direction is perpendicular to the diagonal through the other two corners
            float sx = right ? 1f : -1f, sy = bottom ? 1f : -1f;
            double deg = Math.Atan2(sx * h, -sy * w) * 180.0 / Math.PI; // CSS angle: d = (sinθ, −cosθ)
            return (float)((deg + 360.0) % 360.0);
        }

        /// <summary>SVG linearGradient in user space (x1 y1 → x2 y2); stops as fractions; spread pad.</summary>
        public static LinearGradient Points(Vec2 p0, Vec2 p1, params GradientStop[] stops)
        {
            var g = new LinearGradient { Premultiplied = false };
            g.SetLine(p0, p1);
            g.ResolveStops(stops, Vec2.Dist(p0, p1));
            return g;
        }

        private void SetLine(Vec2 s, Vec2 e)
        {
            Start = s;
            End = e;
            Vec2 v = e - s;
            float l2 = v.LenSq;
            _dir = l2 > 0f ? v / l2 : Vec2.Zero;
            _dot0 = Vec2.Dot(_dir, s);
        }

        /// <summary>The gradient parameter at p (0 at Start, 1 at End).</summary>
        public float TAt(Vec2 p) => Vec2.Dot(_dir, p) - _dot0;

        public override Rgba At(Vec2 p) => ColorAtT(TAt(p));

        public override IEnumerable<IsoLine> Bands(RectF bounds)
        {
            var result = new List<IsoLine>();
            if (_dir == Vec2.Zero) return result;
            float t0 = float.MaxValue, t1 = float.MinValue;
            foreach (Vec2 q in new[] { bounds.TopLeft, bounds.TopRight, bounds.BottomRight, bounds.BottomLeft })
            {
                float t = TAt(q);
                t0 = Math.Min(t0, t);
                t1 = Math.Max(t1, t);
            }
            List<float> kinks = KinkTs();
            if (!Repeating)
            {
                foreach (float t in kinks) if (t > t0 && t < t1) result.Add(new IsoLine(_dir, t + _dot0));
                return result;
            }
            float period = T[T.Length - 1] - T[0];
            if (period <= 1e-6f) return result;
            int kFrom = (int)Math.Floor((t0 - T[T.Length - 1]) / period);
            int kTo = (int)Math.Ceiling((t1 - T[0]) / period);
            const int MaxLines = 8192;
            var ts = new List<float>();
            for (int k = kFrom; k <= kTo && ts.Count < MaxLines; k++)
                foreach (float t in kinks)
                {
                    float tt = t + k * period;
                    if (tt > t0 && tt < t1) ts.Add(tt);
                }
            ts.Sort();
            float eps = 1e-6f * Math.Max(1f, Math.Abs(period));
            for (int i = 0; i < ts.Count; i++)
                if (i == 0 || ts[i] - ts[i - 1] > eps) result.Add(new IsoLine(_dir, ts[i] + _dot0));
            return result;
        }

        /// <summary>
        /// True when this is a repeating gradient of one colour band over transparency (hard stops, e.g.
        /// `amber 0 9px, transparent 9px 15px`): from/to/period in px along the gradient line, for Hatch.Stripes.
        /// </summary>
        public bool TryAsStripes(out float from, out float to, out float period, out Rgba color)
        {
            from = to = period = 0f;
            color = Rgba.Transparent;
            if (!Repeating || T.Length != 4) return false;
            float len = Vec2.Dist(Start, End);
            if (Math.Abs(T[1] - T[2]) > 1e-6f || Math.Abs(T[0] - T[1]) < 1e-6f) return false;
            if (!C[0].Approximately(C[1], 0f) || C[2].A > 0f || C[3].A > 0f || C[0].A <= 0f) return false;
            from = T[0] * len;
            to = T[1] * len;
            period = (T[3] - T[0]) * len;
            color = C[0];
            return period > 0f;
        }

        public override string ToString() =>
            (Repeating ? "repeating-" : "") + "linear-gradient(" + Start + "→" + End + ", " + StopsCss() + ")";
    }

    public sealed class RadialGradient : GradientPaint
    {
        public Vec2 Center { get; private set; }
        /// <summary>The ending shape's radii (the 100 % ellipse).</summary>
        public float Rx { get; private set; }
        public float Ry { get; private set; }
        /// <summary>Canvas inner radius (createRadialGradient r0): t = 0 at R0, 1 at Rx.</summary>
        public float R0 { get; private set; }

        private RadialGradient() { }

        public override bool NeedsRefine => true;

        /// <summary>
        /// CSS radial-gradient prelude ("closest-side", "ellipse 72% 66% at 50% 50%", "80% 70% at 50% 0%",
        /// "ellipse at 30% 18%", "1100px 760px at 300px -80px", "circle at 50% 50%", "" = ellipse farthest-corner at center).
        /// </summary>
        public static RadialGradient Css(string sizeAndPosition, RectF box, params GradientStop[] stops) =>
            Css(sizeAndPosition, box, false, stops);

        public static RadialGradient Css(string sizeAndPosition, RectF box, bool repeating, IReadOnlyList<GradientStop> stops)
        {
            if (!TryShape(sizeAndPosition ?? "", box, out Vec2 c, out float rx, out float ry))
                throw new FormatException("Not a radial-gradient shape: '" + sizeAndPosition + "'");
            var g = new RadialGradient { Center = c, Rx = rx, Ry = ry, R0 = 0f, Repeating = repeating, Premultiplied = true };
            g.ResolveStops(stops, rx);
            return g;
        }

        /// <summary>An ellipse centred at c with radii rx, ry (stops in px are along rx).</summary>
        public static RadialGradient Ellipse(Vec2 c, float rx, float ry, params GradientStop[] stops)
        {
            var g = new RadialGradient { Center = c, Rx = rx, Ry = ry, Premultiplied = true };
            g.ResolveStops(stops, rx);
            return g;
        }

        /// <summary>Canvas createRadialGradient(c, r0, c, r1) / SVG radialGradient (r0 = 0): concentric circles, stops as fractions.</summary>
        public static RadialGradient Points(Vec2 c, float r0, float r1, params GradientStop[] stops)
        {
            var g = new RadialGradient { Center = c, Rx = r1, Ry = r1, R0 = r0, Premultiplied = false };
            g.ResolveStops(stops, r1 - r0);
            return g;
        }

        public float TAt(Vec2 p)
        {
            if (Rx <= 0f || Ry <= 0f) return 1f;
            float dx = (p.X - Center.X) / Rx, dy = (p.Y - Center.Y) / Ry;
            float d = (float)Math.Sqrt(dx * dx + dy * dy) * Rx;
            float span = Rx - R0;
            return span > 0f ? (d - R0) / span : 1f;
        }

        public override Rgba At(Vec2 p) => ColorAtT(TAt(p));

        /// <summary>Parses the prelude against a box into centre and radii (CSS Images 3 §3.2).</summary>
        public static bool TryShape(string prelude, RectF box, out Vec2 centre, out float rx, out float ry)
        {
            centre = box.Center;
            rx = ry = 0f;
            List<string> tokens = CssLex.SplitSpaces(prelude.Trim().ToLowerInvariant());
            int at = tokens.IndexOf("at");
            List<string> shapePart = at >= 0 ? tokens.GetRange(0, at) : tokens;
            if (at >= 0)
            {
                List<string> pos = tokens.GetRange(at + 1, tokens.Count - at - 1);
                if (!CssPosition.TryParse(pos, out CssLength px, out CssLength py)) return false;
                centre = new Vec2(box.X + px.Resolve(box.W), box.Y + py.Resolve(box.H));
            }
            bool circle = false;
            string sizeKeyword = null;
            var lengths = new List<CssLength>();
            foreach (string t in shapePart)
            {
                if (t == "circle") circle = true;
                else if (t == "ellipse") circle = false;
                else if (t == "closest-side" || t == "farthest-side" || t == "closest-corner" || t == "farthest-corner") sizeKeyword = t;
                else if (CssLength.TryParse(t, out CssLength len)) lengths.Add(len);
                else return false;
            }
            if (lengths.Count == 1 && !shapePart.Contains("ellipse")) circle = true;
            float l = centre.X - box.X, r = box.Right - centre.X, tp = centre.Y - box.Y, b = box.Bottom - centre.Y;
            l = Math.Abs(l); r = Math.Abs(r); tp = Math.Abs(tp); b = Math.Abs(b);
            if (lengths.Count == 2)
            {
                rx = lengths[0].Resolve(box.W);
                ry = lengths[1].Resolve(box.H);
                return true;
            }
            if (lengths.Count == 1)
            {
                rx = ry = lengths[0].Resolve(box.W);
                return true;
            }
            string kw = sizeKeyword ?? "farthest-corner";
            if (circle)
            {
                switch (kw)
                {
                    case "closest-side": rx = Math.Min(Math.Min(l, r), Math.Min(tp, b)); break;
                    case "farthest-side": rx = Math.Max(Math.Max(l, r), Math.Max(tp, b)); break;
                    case "closest-corner": rx = Hyp(Math.Min(l, r), Math.Min(tp, b)); break;
                    default: rx = Hyp(Math.Max(l, r), Math.Max(tp, b)); break;
                }
                ry = rx;
                return true;
            }
            float sx, sy;
            switch (kw)
            {
                case "closest-side": rx = Math.Min(l, r); ry = Math.Min(tp, b); return true;
                case "farthest-side": rx = Math.Max(l, r); ry = Math.Max(tp, b); return true;
                case "closest-corner": sx = Math.Min(l, r); sy = Math.Min(tp, b); break;
                default: sx = Math.Max(l, r); sy = Math.Max(tp, b); break;
            }
            // the ellipse through the corner keeps the aspect ratio of the matching -side ellipse: × √2
            rx = sx * 1.41421356f;
            ry = sy * 1.41421356f;
            return true;
        }

        private static float Hyp(float a, float b) => (float)Math.Sqrt(a * a + b * b);

        public override string ToString() =>
            (Repeating ? "repeating-" : "") + "radial-gradient(" + Center + " " + Rx + "×" + Ry + ", " + StopsCss() + ")";
    }

    /// <summary>Several paints stacked (CSS background layers: the first is on top), composited source-over.</summary>
    public sealed class LayeredPaint : Paint
    {
        public readonly IReadOnlyList<Paint> Layers;

        public LayeredPaint(IReadOnlyList<Paint> topFirst) { Layers = topFirst; }

        public override Rgba At(Vec2 p)
        {
            Rgba acc = Rgba.Transparent;
            for (int i = Layers.Count - 1; i >= 0; i--) acc = Over(Layers[i].At(p), acc);
            return acc;
        }

        public override IEnumerable<IsoLine> Bands(RectF bounds)
        {
            List<IsoLine> all = null;
            foreach (Paint l in Layers)
            {
                IEnumerable<IsoLine> b = l.Bands(bounds);
                if (b == null) continue;
                all = all ?? new List<IsoLine>();
                all.AddRange(b);
            }
            return all;
        }

        public override bool IsSolid
        {
            get
            {
                foreach (Paint l in Layers) if (!l.IsSolid) return false;
                return true;
            }
        }

        public override bool NeedsRefine
        {
            get
            {
                foreach (Paint l in Layers) if (l.NeedsRefine) return true;
                return false;
            }
        }

        /// <summary>Straight-alpha source-over.</summary>
        public static Rgba Over(Rgba src, Rgba dst)
        {
            float a = src.A + dst.A * (1f - src.A);
            if (a <= 0f) return Rgba.Transparent;
            float k = dst.A * (1f - src.A);
            return new Rgba((src.R * src.A + dst.R * k) / a, (src.G * src.A + dst.G * k) / a, (src.B * src.A + dst.B * k) / a, a);
        }
    }

    // ---- the parsed, box-free background images -------------------------------------------------------------

    /// <summary>A background-image value before its box is known.</summary>
    public abstract class BackgroundImage
    {
        /// <summary>The paint over the positioning box; null for an image the kit cannot paint (url, none).</summary>
        public abstract Paint Resolve(RectF box);
        public abstract string ToCss();

        /// <summary>A copy whose stops a caller may change (the parse cache keeps the original).</summary>
        public virtual BackgroundImage Clone() => (BackgroundImage)MemberwiseClone();
    }

    public sealed class LinearGradientSpec : BackgroundImage
    {
        /// <summary>CSS angle in degrees (180 = to bottom); ignored when ToSide is set.</summary>
        public float AngleDeg = 180f;
        /// <summary>"to bottom right" etc. (the angle then depends on the box), or null.</summary>
        public string ToSide;
        public bool Repeating;
        public List<GradientStop> Stops = new List<GradientStop>();

        public float AngleFor(RectF box) => ToSide != null ? LinearGradient.SideAngle(ToSide, box.W, box.H) : AngleDeg;

        public override Paint Resolve(RectF box) => LinearGradient.Css(AngleFor(box), box, Repeating, Stops);

        public override BackgroundImage Clone()
        {
            var c = (LinearGradientSpec)MemberwiseClone();
            c.Stops = new List<GradientStop>(Stops);
            return c;
        }

        public override string ToCss()
        {
            var sb = new StringBuilder(Repeating ? "repeating-linear-gradient(" : "linear-gradient(");
            sb.Append(ToSide ?? GradientStop.Fmt(AngleDeg) + "deg");
            foreach (GradientStop s in Stops) sb.Append(", ").Append(s.ToCss());
            return sb.Append(')').ToString();
        }
    }

    public sealed class RadialGradientSpec : BackgroundImage
    {
        /// <summary>The shape/size/position prelude as written ("closest-side", "ellipse 72% 66% at 50% 50%"), "" for none.</summary>
        public string Prelude = "";
        public bool Repeating;
        public List<GradientStop> Stops = new List<GradientStop>();

        public override Paint Resolve(RectF box) => RadialGradient.Css(Prelude, box, Repeating, Stops);

        public override BackgroundImage Clone()
        {
            var c = (RadialGradientSpec)MemberwiseClone();
            c.Stops = new List<GradientStop>(Stops);
            return c;
        }

        public override string ToCss()
        {
            var sb = new StringBuilder(Repeating ? "repeating-radial-gradient(" : "radial-gradient(");
            bool first = true;
            if (!string.IsNullOrEmpty(Prelude)) { sb.Append(Prelude); first = false; }
            foreach (GradientStop s in Stops)
            {
                if (!first) sb.Append(", ");
                sb.Append(s.ToCss());
                first = false;
            }
            return sb.Append(')').ToString();
        }
    }

    /// <summary>url(...) or an unresolvable image (the washi texture var): kept for the record, never painted by the kit.</summary>
    public sealed class UrlImage : BackgroundImage
    {
        public string Text;
        public override Paint Resolve(RectF box) => null;
        public override string ToCss() => Text;
    }

    public enum BackgroundRepeat { Repeat, NoRepeat, RepeatX, RepeatY }

    /// <summary>One layer of a background: image (or the colour layer), size, position and repeat.</summary>
    public sealed class BackgroundLayer
    {
        public BackgroundImage Image;
        /// <summary>background-size: Auto lengths = the positioning area (a gradient's intrinsic size); "cover"/"contain" in SizeKeyword.</summary>
        public CssLength SizeW = CssLength.Auto, SizeH = CssLength.Auto;
        public string SizeKeyword;
        /// <summary>background-position (default 0 % 0 %).</summary>
        public CssLength PosX = CssLength.Zero, PosY = CssLength.Zero;
        public BackgroundRepeat Repeat = BackgroundRepeat.Repeat;

        /// <summary>The tile's rect inside the positioning area (CSS Backgrounds 3 §3.6 / §3.9).</summary>
        public RectF Tile(RectF area)
        {
            float w = SizeW.IsAuto ? area.W : SizeW.Resolve(area.W);
            float h = SizeH.IsAuto ? area.H : SizeH.Resolve(area.H);
            if (SizeKeyword == "cover" || SizeKeyword == "contain") { w = area.W; h = area.H; }
            float x = area.X + PosX.Resolve(area.W - w);
            float y = area.Y + PosY.Resolve(area.H - h);
            return new RectF(x, y, w, h);
        }

        /// <summary>True when one tile covers the whole positioning area (the common gradient case).</summary>
        public bool CoversArea(RectF area)
        {
            RectF t = Tile(area);
            return t.X <= area.X + 1e-3f && t.Y <= area.Y + 1e-3f && t.Right >= area.Right - 1e-3f && t.Bottom >= area.Bottom - 1e-3f;
        }

        public BackgroundLayer Clone()
        {
            var c = (BackgroundLayer)MemberwiseClone();
            c.Image = Image?.Clone();
            return c;
        }
    }
}
