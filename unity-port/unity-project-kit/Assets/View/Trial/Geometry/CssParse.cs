// A CSS declaration parser for the subset the design system (vendor/ds/bundle.css) and the scenes' stylesheets use,
// so the C# can quote the CSS text verbatim: Css.Box("background: var(--panel); border: 1px solid var(--rule);
// border-radius: 6px"). Results are data (no Unity types), cached by text + theme + variables, and returned as
// copies so a caller may change them.
//
// var(--x[, fallback]) resolves against the block's own custom properties, the caller's CssVars, then DsTokens
// (the theme's tokens.css text), recursively. currentColor resolves against the block's `color`, the vars' "color",
// then --ink (the colour of .ocb-root, which every scene sits in). Within a block later declarations win and
// shorthands reset their longhands, as in CSS. Declarations the kit has no use for (cursor, user-select, ...) land
// in Ignored; anything not understood lands in Unknown, a value that does not parse in Invalid.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Trial.Geometry
{
    // ---- variables ---------------------------------------------------------------------------------------------

    /// <summary>Custom properties a scene sets on an element (--attr, --val-ink, --plan-alpha, ...), with an optional parent scope.</summary>
    public sealed class CssVars
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly CssVars _parent;
        private string _signature;

        public CssVars(CssVars parent = null) { _parent = parent; }

        /// <summary>Sets --name (the leading "--" is optional; "color" is the element's colour for currentColor).</summary>
        public CssVars Set(string name, string value)
        {
            _map[Key(name)] = value;
            _signature = null;
            return this;
        }

        public bool TryGet(string name, out string value)
        {
            string key = Key(name);
            for (CssVars v = this; v != null; v = v._parent)
                if (v._map.TryGetValue(key, out value)) return true;
            value = null;
            return false;
        }

        private static string Key(string name) => name == "color" || name.StartsWith("--", StringComparison.Ordinal) ? name : "--" + name;

        /// <summary>
        /// A text that identifies the scope's content, parents included (for caches). Only this scope's own part is
        /// memoised: a parent changed after the child was used must change the child's signature too.
        /// </summary>
        public string Signature
        {
            get
            {
                if (_signature == null)
                {
                    var keys = new List<string>(_map.Keys);
                    keys.Sort(StringComparer.Ordinal);
                    var sb = new StringBuilder();
                    foreach (string k in keys) sb.Append(k).Append('=').Append(_map[k]).Append(';');
                    _signature = sb.ToString();
                }
                return _parent == null ? _signature : _signature + "{" + _parent.Signature + "}";
            }
        }
    }

    // ---- lengths -----------------------------------------------------------------------------------------------

    /// <summary>A CSS length: px + % of a basis + em of the font size (calc sums), or auto.</summary>
    public readonly struct CssLength : IEquatable<CssLength>
    {
        public readonly float Px, Percent, Em;
        public readonly bool IsAuto;

        public CssLength(float px, float percent = 0f, float em = 0f, bool isAuto = false) { Px = px; Percent = percent; Em = em; IsAuto = isAuto; }

        public static readonly CssLength Zero = new CssLength(0f);
        public static readonly CssLength Auto = new CssLength(0f, 0f, 0f, true);

        public static CssLength FromPx(float px) => new CssLength(px);
        public static CssLength FromPercent(float pct) => new CssLength(0f, pct);
        public static CssLength FromEm(float em) => new CssLength(0f, 0f, em);

        public bool IsZero => !IsAuto && Px == 0f && Percent == 0f && Em == 0f;
        public bool IsPx => !IsAuto && Percent == 0f && Em == 0f;

        /// <summary>The length in px against a basis (for %) and a font size (for em); auto resolves to 0.</summary>
        public float Resolve(float basis, float fontSize = 16f) => IsAuto ? 0f : Px + Percent * basis / 100f + Em * fontSize;

        public static CssLength Lerp(CssLength a, CssLength b, float t) =>
            new CssLength(a.Px + (b.Px - a.Px) * t, a.Percent + (b.Percent - a.Percent) * t, a.Em + (b.Em - a.Em) * t, a.IsAuto && b.IsAuto);

        public static CssLength operator +(CssLength a, CssLength b) => new CssLength(a.Px + b.Px, a.Percent + b.Percent, a.Em + b.Em);
        public static CssLength operator -(CssLength a) => new CssLength(-a.Px, -a.Percent, -a.Em, a.IsAuto);

        /// <summary>"12px", "-1px", "0", "50%", ".5em", "1rem", "auto", "calc(100% + 3px)".</summary>
        public static bool TryParse(string text, out CssLength length)
        {
            length = Zero;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim().ToLowerInvariant();
            if (t == "auto") { length = Auto; return true; }
            if (t.StartsWith("calc(", StringComparison.Ordinal) && t.EndsWith(")", StringComparison.Ordinal)) return TryCalc(t.Substring(5, t.Length - 6), out length);
            if (!CssLex.TryNumber(t, out float v, out string unit)) return false;
            switch (unit)
            {
                case "px": length = new CssLength(v); return true;
                case "%": length = new CssLength(0f, v); return true;
                case "em": length = new CssLength(0f, 0f, v); return true;
                case "rem": length = new CssLength(v * 16f); return true;
                case "": if (v == 0f) { length = Zero; return true; } return false;
                default: return false;
            }
        }

        private static bool TryCalc(string body, out CssLength length)
        {
            length = Zero;
            var acc = Zero;
            int sign = 1;
            foreach (string tok in CssLex.SplitSpaces(body))
            {
                if (tok == "+") { sign = 1; continue; }
                if (tok == "-") { sign = -1; continue; }
                string t = tok.StartsWith("(", StringComparison.Ordinal) && tok.EndsWith(")", StringComparison.Ordinal) ? "calc" + tok : tok;
                if (!TryParse(t, out CssLength part) || part.IsAuto) return false;
                acc = sign > 0 ? acc + part : acc + (-part);
                sign = 1;
            }
            length = acc;
            return true;
        }

        public bool Equals(CssLength o) => Px == o.Px && Percent == o.Percent && Em == o.Em && IsAuto == o.IsAuto;
        public override bool Equals(object obj) => obj is CssLength l && Equals(l);
        public override int GetHashCode() => Px.GetHashCode() ^ Percent.GetHashCode() * 3 ^ Em.GetHashCode() * 7 ^ IsAuto.GetHashCode();

        public string ToCss()
        {
            if (IsAuto) return "auto";
            var parts = new List<string>();
            if (Px != 0f) parts.Add(F(Px) + "px");
            if (Percent != 0f) parts.Add(F(Percent) + "%");
            if (Em != 0f) parts.Add(F(Em) + "em");
            if (parts.Count == 0) return "0";
            if (parts.Count == 1) return parts[0];
            return "calc(" + string.Join(" + ", parts).Replace("+ -", "- ") + ")";
        }

        private static string F(float v) => Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);
        public override string ToString() => ToCss();
    }

    internal static class CssPosition
    {
        /// <summary>background-position / gradient `at` values: 1 or 2 values (keywords, lengths), or 4 (keyword offset pairs).</summary>
        public static bool TryParse(List<string> tokens, out CssLength x, out CssLength y)
        {
            x = CssLength.FromPercent(50f);
            y = CssLength.FromPercent(50f);
            if (tokens.Count == 0) return true;
            if (tokens.Count == 1)
            {
                string t = tokens[0];
                if (t == "top" || t == "bottom") return TryAxis(t, false, out y);
                return TryAxis(t, true, out x);
            }
            if (tokens.Count == 2)
            {
                string a = tokens[0], b = tokens[1];
                if (a == "top" || a == "bottom" || b == "left" || b == "right") { string s = a; a = b; b = s; }
                return TryAxis(a, true, out x) & TryAxis(b, false, out y);
            }
            if (tokens.Count == 4)
            {
                bool okA = Edge(tokens[0], tokens[1], out CssLength a, out bool aHorizontal);
                bool okB = Edge(tokens[2], tokens[3], out CssLength b, out bool bHorizontal);
                if (!okA || !okB || aHorizontal == bHorizontal) return false;
                x = aHorizontal ? a : b;
                y = aHorizontal ? b : a;
                return true;
            }
            return false;
        }

        private static bool Edge(string keyword, string offset, out CssLength v, out bool horizontal)
        {
            v = CssLength.Zero;
            horizontal = keyword == "left" || keyword == "right";
            if (!CssLength.TryParse(offset, out CssLength off)) return false;
            switch (keyword)
            {
                case "left":
                case "top": v = off; return true;
                case "right":
                case "bottom": v = CssLength.FromPercent(100f) + (-off); return true;
                default: return false;
            }
        }

        private static bool TryAxis(string t, bool horizontal, out CssLength v)
        {
            switch (t)
            {
                case "center": v = CssLength.FromPercent(50f); return true;
                case "left": v = CssLength.FromPercent(0f); return horizontal;
                case "right": v = CssLength.FromPercent(100f); return horizontal;
                case "top": v = CssLength.FromPercent(0f); return !horizontal;
                case "bottom": v = CssLength.FromPercent(100f); return !horizontal;
            }
            return CssLength.TryParse(t, out v) && !v.IsAuto;
        }
    }

    // ---- transforms --------------------------------------------------------------------------------------------

    public enum TransformKind { Translate, Scale, Rotate }

    public readonly struct TransformFn : IEquatable<TransformFn>
    {
        public readonly TransformKind Kind;
        public readonly CssLength X, Y;
        public readonly float Sx, Sy, Deg;

        private TransformFn(TransformKind kind, CssLength x, CssLength y, float sx, float sy, float deg)
        {
            Kind = kind; X = x; Y = y; Sx = sx; Sy = sy; Deg = deg;
        }

        public static TransformFn Translate(CssLength x, CssLength y) => new TransformFn(TransformKind.Translate, x, y, 1f, 1f, 0f);
        public static TransformFn Scale(float sx, float sy) => new TransformFn(TransformKind.Scale, CssLength.Zero, CssLength.Zero, sx, sy, 0f);
        public static TransformFn Rotate(float deg) => new TransformFn(TransformKind.Rotate, CssLength.Zero, CssLength.Zero, 1f, 1f, deg);

        public static TransformFn Identity(TransformKind k) =>
            k == TransformKind.Translate ? Translate(CssLength.Zero, CssLength.Zero) : k == TransformKind.Scale ? Scale(1f, 1f) : Rotate(0f);

        public static TransformFn Lerp(TransformFn a, TransformFn b, float t)
        {
            switch (a.Kind)
            {
                case TransformKind.Translate: return Translate(CssLength.Lerp(a.X, b.X, t), CssLength.Lerp(a.Y, b.Y, t));
                case TransformKind.Scale: return Scale(a.Sx + (b.Sx - a.Sx) * t, a.Sy + (b.Sy - a.Sy) * t);
                default: return Rotate(a.Deg + (b.Deg - a.Deg) * t);
            }
        }

        public string ToCss()
        {
            switch (Kind)
            {
                case TransformKind.Translate: return "translate(" + X.ToCss() + ", " + Y.ToCss() + ")";
                case TransformKind.Scale: return string.Format(CultureInfo.InvariantCulture, "scale({0}, {1})", Math.Round(Sx, 5), Math.Round(Sy, 5));
                default: return string.Format(CultureInfo.InvariantCulture, "rotate({0}deg)", Math.Round(Deg, 4));
            }
        }

        public bool Equals(TransformFn o) => Kind == o.Kind && X.Equals(o.X) && Y.Equals(o.Y) && Math.Abs(Sx - o.Sx) < 1e-5f && Math.Abs(Sy - o.Sy) < 1e-5f && Math.Abs(Deg - o.Deg) < 1e-4f;
        public override bool Equals(object obj) => obj is TransformFn f && Equals(f);
        public override int GetHashCode() => (int)Kind ^ X.GetHashCode() ^ Sx.GetHashCode() * 3 ^ Deg.GetHashCode() * 7;
    }

    /// <summary>A transform list decomposed as translate · rotate · scale. TxPercent / TyPercent are % of the element's own width / height.</summary>
    public readonly struct TransformParts
    {
        public readonly float Tx, Ty, TxPercent, TyPercent, Rot, Sx, Sy;

        public TransformParts(float tx, float ty, float txPct, float tyPct, float rot, float sx, float sy)
        {
            Tx = tx; Ty = ty; TxPercent = txPct; TyPercent = tyPct; Rot = rot; Sx = sx; Sy = sy;
        }

        public static readonly TransformParts Identity = new TransformParts(0f, 0f, 0f, 0f, 0f, 1f, 1f);
    }

    /// <summary>A CSS transform list (translate / translateX / translateY / scale / scaleX / scaleY / rotate); empty = none.</summary>
    public sealed class TransformSpec
    {
        public readonly IReadOnlyList<TransformFn> Functions;

        public TransformSpec(IReadOnlyList<TransformFn> functions) { Functions = functions ?? Array.Empty<TransformFn>(); }

        public static readonly TransformSpec None = new TransformSpec(Array.Empty<TransformFn>());

        public bool IsNone => Functions.Count == 0;

        public static TransformSpec Parse(string css)
        {
            if (TryParse(css, out TransformSpec t)) return t;
            throw new FormatException("Not a CSS transform: '" + css + "'");
        }

        public static bool TryParse(string css, out TransformSpec spec)
        {
            spec = None;
            if (string.IsNullOrWhiteSpace(css)) return false;
            string s = css.Trim().ToLowerInvariant();
            if (s == "none") return true;
            var list = new List<TransformFn>();
            foreach (string tok in CssLex.SplitSpaces(s))
            {
                if (!CssLex.TryFunction(tok, out string name, out string args)) return false;
                List<string> a = CssLex.SplitTopLevel(args, ',');
                if (a.Count == 1 && a[0].IndexOf(' ') >= 0) a = CssLex.SplitSpaces(a[0]);
                switch (name)
                {
                    case "translate":
                    {
                        if (!CssLength.TryParse(a[0], out CssLength x)) return false;
                        CssLength y = CssLength.Zero;
                        if (a.Count > 1 && !CssLength.TryParse(a[1], out y)) return false;
                        list.Add(TransformFn.Translate(x, y));
                        break;
                    }
                    case "translatex":
                        if (!CssLength.TryParse(a[0], out CssLength tx)) return false;
                        list.Add(TransformFn.Translate(tx, CssLength.Zero));
                        break;
                    case "translatey":
                        if (!CssLength.TryParse(a[0], out CssLength ty)) return false;
                        list.Add(TransformFn.Translate(CssLength.Zero, ty));
                        break;
                    case "scale":
                    {
                        if (!TryScale(a[0], out float sx)) return false;
                        float sy = sx;
                        if (a.Count > 1 && !TryScale(a[1], out sy)) return false;
                        list.Add(TransformFn.Scale(sx, sy));
                        break;
                    }
                    case "scalex":
                        if (!TryScale(a[0], out float scx)) return false;
                        list.Add(TransformFn.Scale(scx, 1f));
                        break;
                    case "scaley":
                        if (!TryScale(a[0], out float scy)) return false;
                        list.Add(TransformFn.Scale(1f, scy));
                        break;
                    case "rotate":
                    case "rotatez":
                        if (!CssLex.TryAngleDeg(a[0], out float deg)) return false;
                        list.Add(TransformFn.Rotate(deg));
                        break;
                    default:
                        return false;
                }
            }
            spec = new TransformSpec(list);
            return true;
        }

        private static bool TryScale(string t, out float v)
        {
            if (!CssLex.TryNumber(t.Trim(), out v, out string unit)) return false;
            if (unit == "%") v /= 100f;
            else if (unit.Length != 0) return false;
            return true;
        }

        /// <summary>The individual `translate: x [y]` property.</summary>
        public static bool TryParseTranslateProperty(string css, out TransformSpec spec)
        {
            spec = None;
            string s = css.Trim().ToLowerInvariant();
            if (s == "none") return true;
            List<string> a = CssLex.SplitSpaces(s);
            if (a.Count < 1 || !CssLength.TryParse(a[0], out CssLength x)) return false;
            CssLength y = CssLength.Zero;
            if (a.Count > 1 && !CssLength.TryParse(a[1], out y)) return false;
            spec = new TransformSpec(new[] { TransformFn.Translate(x, y) });
            return true;
        }

        /// <summary>The individual `scale: s [sy]` property.</summary>
        public static bool TryParseScaleProperty(string css, out TransformSpec spec)
        {
            spec = None;
            string s = css.Trim().ToLowerInvariant();
            if (s == "none") return true;
            List<string> a = CssLex.SplitSpaces(s);
            if (a.Count < 1 || !TryScale(a[0], out float sx)) return false;
            float sy = sx;
            if (a.Count > 1 && !TryScale(a[1], out sy)) return false;
            spec = new TransformSpec(new[] { TransformFn.Scale(sx, sy) });
            return true;
        }

        /// <summary>The individual `rotate: θ` property.</summary>
        public static bool TryParseRotateProperty(string css, out TransformSpec spec)
        {
            spec = None;
            string s = css.Trim().ToLowerInvariant();
            if (s == "none") return true;
            if (!CssLex.TryAngleDeg(s, out float deg)) return false;
            spec = new TransformSpec(new[] { TransformFn.Rotate(deg) });
            return true;
        }

        /// <summary>
        /// CSS Transforms interpolation: none pairs with identity functions; lists of the same function kinds
        /// interpolate function by function; anything else interpolates the decomposed parts.
        /// </summary>
        public static TransformSpec Interpolate(TransformSpec a, TransformSpec b, float t)
        {
            if (a.IsNone && b.IsNone) return None;
            IReadOnlyList<TransformFn> fa = a.Functions, fb = b.Functions;
            if (a.IsNone) fa = IdentityOf(fb);
            if (b.IsNone) fb = IdentityOf(fa);
            if (fa.Count == fb.Count)
            {
                bool same = true;
                for (int i = 0; i < fa.Count && same; i++) same = fa[i].Kind == fb[i].Kind;
                if (same)
                {
                    var o = new TransformFn[fa.Count];
                    for (int i = 0; i < fa.Count; i++) o[i] = TransformFn.Lerp(fa[i], fb[i], t);
                    return new TransformSpec(o);
                }
            }
            TransformParts pa = a.Compose(), pb = b.Compose();
            float L(float x, float y) => x + (y - x) * t;
            return new TransformSpec(new[]
            {
                TransformFn.Translate(new CssLength(L(pa.Tx, pb.Tx), L(pa.TxPercent, pb.TxPercent)), new CssLength(L(pa.Ty, pb.Ty), L(pa.TyPercent, pb.TyPercent))),
                TransformFn.Rotate(L(pa.Rot, pb.Rot)),
                TransformFn.Scale(L(pa.Sx, pb.Sx), L(pa.Sy, pb.Sy)),
            });
        }

        private static TransformFn[] IdentityOf(IReadOnlyList<TransformFn> fns)
        {
            var o = new TransformFn[fns.Count];
            for (int i = 0; i < fns.Count; i++) o[i] = TransformFn.Identity(fns[i].Kind);
            return o;
        }

        /// <summary>The 2×2 linear part (a b c d: x' = a·x + c·y, y' = b·x + d·y) and the translations (px, per-width, per-height).</summary>
        private void Accumulate(out float a, out float b, out float c, out float d, out Vec2 tPx, out Vec2 perW, out Vec2 perH)
        {
            a = 1f; b = 0f; c = 0f; d = 1f;
            tPx = Vec2.Zero; perW = Vec2.Zero; perH = Vec2.Zero;
            foreach (TransformFn f in Functions)
            {
                switch (f.Kind)
                {
                    case TransformKind.Translate:
                    {
                        var vPx = new Vec2(f.X.Px + f.X.Em * 16f, f.Y.Px + f.Y.Em * 16f);
                        tPx += new Vec2(a * vPx.X + c * vPx.Y, b * vPx.X + d * vPx.Y);
                        float px = f.X.Percent / 100f, py = f.Y.Percent / 100f;
                        perW += new Vec2(a * px, b * px);
                        perH += new Vec2(c * py, d * py);
                        break;
                    }
                    case TransformKind.Scale:
                        a *= f.Sx; b *= f.Sx; c *= f.Sy; d *= f.Sy;
                        break;
                    case TransformKind.Rotate:
                    {
                        double r = f.Deg * Math.PI / 180.0;
                        float cs = (float)Math.Cos(r), sn = (float)Math.Sin(r);
                        float na = a * cs + c * sn, nb = b * cs + d * sn, nc = -a * sn + c * cs, nd = -b * sn + d * cs;
                        a = na; b = nb; c = nc; d = nd;
                        break;
                    }
                }
            }
        }

        /// <summary>The list as translate (px and % of the box) · rotate (CSS degrees, clockwise) · scale.</summary>
        public TransformParts Compose()
        {
            Accumulate(out float a, out float b, out float c, out float d, out Vec2 tPx, out Vec2 perW, out Vec2 perH);
            float sx = (float)Math.Sqrt(a * a + b * b);
            float rot = (float)(Math.Atan2(b, a) * 180.0 / Math.PI);
            float sy = sx > 0f ? (a * d - b * c) / sx : (float)Math.Sqrt(c * c + d * d);
            return new TransformParts(tPx.X, tPx.Y, perW.X * 100f + perH.X * 100f, perW.Y * 100f + perH.Y * 100f, rot, sx, sy);
        }

        /// <summary>The full matrix for an element of size w×h whose transform-origin is at `origin` (px in the element's coordinates).</summary>
        public Affine ToAffine(float w, float h, Vec2 origin)
        {
            Accumulate(out float a, out float b, out float c, out float d, out Vec2 tPx, out Vec2 perW, out Vec2 perH);
            Vec2 t = tPx + perW * w + perH * h;
            float e = origin.X - (a * origin.X + c * origin.Y) + t.X;
            float f = origin.Y - (b * origin.X + d * origin.Y) + t.Y;
            return new Affine(a, b, c, d, e, f);
        }

        public string ToCss()
        {
            if (IsNone) return "none";
            var parts = new List<string>();
            foreach (TransformFn f in Functions) parts.Add(f.ToCss());
            return string.Join(" ", parts);
        }

        public override string ToString() => ToCss();
    }

    // ---- filters, svg paint, layout ----------------------------------------------------------------------------

    /// <summary>filter: drop-shadow(x y b c). The blur is the Gaussian σ itself (measured: σ = b).</summary>
    public readonly struct DropShadow : IEquatable<DropShadow>
    {
        public readonly float X, Y, Blur;
        public readonly Rgba Color;

        public DropShadow(float x, float y, float blur, Rgba color) { X = x; Y = y; Blur = blur; Color = color; }

        public float Sigma => Blur;

        public bool Equals(DropShadow o) => X == o.X && Y == o.Y && Blur == o.Blur && Color.Approximately(o.Color, 1f / 1024f);
        public override bool Equals(object obj) => obj is DropShadow s && Equals(s);
        public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode() * 3 ^ Blur.GetHashCode() * 7;
    }

    public sealed class FilterSpec
    {
        public ColorFilter Color = ColorFilter.None;
        public List<DropShadow> DropShadows = new List<DropShadow>();

        public bool IsNone => Color.IsNone && DropShadows.Count == 0;

        public FilterSpec Clone() => new FilterSpec { Color = Color, DropShadows = new List<DropShadow>(DropShadows) };
    }

    /// <summary>The SVG presentation properties a stylesheet sets (explore's map edges).</summary>
    public sealed class SvgStyle
    {
        public Rgba? Stroke, Fill;
        public bool StrokeNone, FillNone;
        public float? StrokeWidth;
        public Cap? LineCap;
        public Join? LineJoin;
        public float[] DashArray;
        public float? DashOffset;
        public float? FillOpacity, StrokeOpacity;

        public SvgStyle Clone()
        {
            var c = (SvgStyle)MemberwiseClone();
            if (DashArray != null) c.DashArray = (float[])DashArray.Clone();
            return c;
        }
    }

    public enum Position { Static, Relative, Absolute, Fixed }

    /// <summary>The box-model and flex / grid declarations of a block (null = not declared).</summary>
    public sealed class LayoutStyle
    {
        public string Display;
        public Position? Position;
        public CssLength? Left, Top, Right, Bottom;
        public CssLength? Width, Height, MinWidth, MinHeight, MaxWidth, MaxHeight;
        public CssLength? MarginTop, MarginRight, MarginBottom, MarginLeft;
        public bool? BorderBox;
        public bool? Column, Reverse, Wrap;
        public float? RowGap, ColumnGap;
        public Align? AlignItems, AlignSelf, JustifySelf, JustifyItems;
        public Justify? JustifyContent;
        public AlignContent? AlignContent;
        public float? Grow, Shrink;
        public CssLength? Basis;
        public int? Order, ZIndex;
        public string OverflowX, OverflowY;
        public List<Track> GridColumns, GridRows;
        public int? GridColumnStart, GridColumnEnd, GridColumnSpan, GridRowStart, GridRowEnd, GridRowSpan;
        public string VerticalAlign;

        public bool IsFlex => Display == "flex" || Display == "inline-flex";
        public bool IsGrid => Display == "grid" || Display == "inline-grid";

        /// <summary>A FlexSpec for this container. padding = padding + border widths; w / h = its border-box size (null = fit content).</summary>
        public FlexSpec ToFlexSpec(Insets padding, float? w = null, float? h = null) => new FlexSpec
        {
            Column = Column ?? false,
            Reverse = Reverse ?? false,
            Wrap = Wrap ?? false,
            RowGap = RowGap ?? 0f,
            ColumnGap = ColumnGap ?? 0f,
            Padding = padding,
            AlignItems = AlignItems ?? Align.Stretch,
            Justify = JustifyContent ?? Justify.Start,
            AlignContent = AlignContent ?? Geometry.AlignContent.Stretch,
            Width = w,
            Height = h,
        };

        /// <summary>A FlexItem for this element in a container whose direction is `column`; w / h = its natural border-box size.</summary>
        public FlexItem ToFlexItem(float w, float h, bool column, float fontSize = 16f)
        {
            var it = new FlexItem
            {
                W = w, H = h,
                Grow = Grow ?? 0f,
                Shrink = Shrink ?? 1f,
                Self = AlignSelf,
                Order = Order ?? 0,
                FixedCross = column ? Width.HasValue && !Width.Value.IsAuto : Height.HasValue && !Height.Value.IsAuto,
            };
            if (Basis.HasValue && !Basis.Value.IsAuto)
            {
                // a zero basis is taken as 0 % (what `flex: 1` computes to in Chrome): 0 in a sized container, the
                // content size in one that sizes to its content
                CssLength b = Basis.Value;
                if (b.Percent != 0f || (b.Px == 0f && b.Em == 0f)) it.BasisPercent = b.Percent;
                else it.Basis = b.Px + b.Em * fontSize;
            }
            Margin(MarginLeft, out it.MarginL, out it.AutoMarginL, fontSize);
            Margin(MarginRight, out it.MarginR, out it.AutoMarginR, fontSize);
            Margin(MarginTop, out it.MarginT, out it.AutoMarginT, fontSize);
            Margin(MarginBottom, out it.MarginB, out it.AutoMarginB, fontSize);
            CssLength? min = column ? MinHeight : MinWidth;
            if (min.HasValue && !min.Value.IsAuto) it.MinMain = min.Value.Px;
            // Flexbox §4.5: min-width / min-height auto is 0 for a scroll container (overflow other than visible / clip)
            else if (IsScrollContainer(column ? OverflowY : OverflowX)) it.MinMain = 0f;
            CssLength? max = column ? MaxHeight : MaxWidth;
            if (max.HasValue && !max.Value.IsAuto) it.MaxMain = max.Value.Px;
            return it;
        }

        private static bool IsScrollContainer(string overflow)
        {
            string o = overflow?.Trim().ToLowerInvariant();
            return o == "hidden" || o == "auto" || o == "scroll" || o == "overlay";
        }

        private static void Margin(CssLength? m, out float px, out bool auto, float fontSize)
        {
            px = 0f;
            auto = false;
            if (!m.HasValue) return;
            if (m.Value.IsAuto) { auto = true; return; }
            px = m.Value.Px + m.Value.Em * fontSize;
        }

        public LayoutStyle Clone()
        {
            var c = (LayoutStyle)MemberwiseClone();
            if (GridColumns != null) c.GridColumns = new List<Track>(GridColumns);
            if (GridRows != null) c.GridRows = new List<Track>(GridRows);
            return c;
        }
    }

    /// <summary>Everything one declaration block says.</summary>
    public sealed class CssBlock
    {
        public BoxStyle Box = new BoxStyle();
        public TextStyleData Text = new TextStyleData();
        public LayoutStyle Layout = new LayoutStyle();
        public SvgStyle Svg = new SvgStyle();
        /// <summary>transform (null = not declared).</summary>
        public TransformSpec Transform;
        /// <summary>The individual translate / scale / rotate properties (null = not declared).</summary>
        public TransformSpec Translate, Scale, Rotate;
        public CssLength? OriginX, OriginY;
        public List<TransitionSpec> Transitions;
        public List<CssAnimation> Animations;
        public FilterSpec Filter;
        public float? Opacity;
        public BackgroundImage MaskImage;
        public string MixBlendMode, Visibility, PointerEvents, Content;
        public Dictionary<string, string> CustomProperties = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>Declarations with no effect in the kit (cursor, user-select, will-change, ...).</summary>
        public List<(string Property, string Value)> Ignored = new List<(string, string)>();
        /// <summary>Properties the parser does not know.</summary>
        public List<(string Property, string Value)> Unknown = new List<(string, string)>();
        /// <summary>Known properties whose value did not parse (or used an undefined var() without a fallback).</summary>
        public List<(string Property, string Value)> Invalid = new List<(string, string)>();

        /// <summary>The element's transform with the individual properties, as CSS applies them: translate, rotate, scale, then transform.</summary>
        public TransformSpec CombinedTransform()
        {
            var list = new List<TransformFn>();
            if (Translate != null) list.AddRange(Translate.Functions);
            if (Rotate != null) list.AddRange(Rotate.Functions);
            if (Scale != null) list.AddRange(Scale.Functions);
            if (Transform != null) list.AddRange(Transform.Functions);
            return list.Count == 0 ? TransformSpec.None : new TransformSpec(list);
        }

        /// <summary>A deep copy: the parse cache keeps the original, so nothing a caller changes (an animation's delay,
        /// a gradient's stops) may be shared with it.</summary>
        public CssBlock Clone()
        {
            var c = (CssBlock)MemberwiseClone();
            c.Box = Box.Clone();
            c.Text = Text.Clone();
            c.Layout = Layout.Clone();
            c.Svg = Svg.Clone();
            if (Transitions != null) c.Transitions = Transitions.ConvertAll(t => t.Clone());
            if (Animations != null) c.Animations = Animations.ConvertAll(a => a.Clone());
            if (MaskImage != null) c.MaskImage = MaskImage.Clone();
            if (Filter != null) c.Filter = Filter.Clone();
            c.CustomProperties = new Dictionary<string, string>(CustomProperties, StringComparer.Ordinal);
            c.Ignored = new List<(string, string)>(Ignored);
            c.Unknown = new List<(string, string)>(Unknown);
            c.Invalid = new List<(string, string)>(Invalid);
            return c;
        }
    }

    // ---- the parser --------------------------------------------------------------------------------------------

    public static class Css
    {
        private static readonly Dictionary<string, CssBlock> Cache = new Dictionary<string, CssBlock>(StringComparer.Ordinal);
        private const int CacheLimit = 4096;

        /// <summary>Parses a declaration block ("a: b; c: d"). Cached; the result is a copy the caller owns.</summary>
        public static CssBlock Parse(string declarations, DsTheme theme = DsTheme.Dusk, CssVars vars = null)
        {
            string key = (int)theme + "|" + (vars?.Signature ?? "") + "|" + declarations;
            lock (Cache)
            {
                if (Cache.TryGetValue(key, out CssBlock hit)) return hit.Clone();
            }
            CssBlock block = ParseUncached(declarations ?? "", theme, vars);
            lock (Cache)
            {
                if (Cache.Count >= CacheLimit) Cache.Clear();
                Cache[key] = block;
            }
            return block.Clone();
        }

        /// <summary>Drops every cached parse (tests; the cache is otherwise bounded).</summary>
        public static void ClearCache()
        {
            lock (Cache) Cache.Clear();
        }

        public static BoxStyle Box(string declarations, DsTheme theme = DsTheme.Dusk, CssVars vars = null) => Parse(declarations, theme, vars).Box;
        public static TextStyleData Text(string declarations, DsTheme theme = DsTheme.Dusk, CssVars vars = null) => Parse(declarations, theme, vars).Text;
        public static LayoutStyle Layout(string declarations, DsTheme theme = DsTheme.Dusk, CssVars vars = null) => Parse(declarations, theme, vars).Layout;

        /// <summary>A box-shadow value ("0 6px 14px rgba(0,0,0,.45), inset 3px 0 0 var(--amber)"); "none" → empty.</summary>
        public static IReadOnlyList<BoxShadow> Shadows(string css, DsTheme theme = DsTheme.Dusk, CssVars vars = null)
        {
            if (TryShadows(css, theme, vars, out List<BoxShadow> list)) return list;
            throw new FormatException("Not a box-shadow: '" + css + "'");
        }

        /// <summary>A text-shadow value; "none" → empty.</summary>
        public static IReadOnlyList<TextShadow> TextShadows(string css, DsTheme theme = DsTheme.Dusk, CssVars vars = null)
        {
            if (TryTextShadows(css, theme, vars, out List<TextShadow> list)) return list;
            throw new FormatException("Not a text-shadow: '" + css + "'");
        }

        /// <summary>A background value painted over a box: its layers composited into one paint (null for none / transparent).</summary>
        public static Paint Background(string css, RectF box, DsTheme theme = DsTheme.Dusk, CssVars vars = null)
        {
            var style = new BoxStyle();
            if (!TryBackground(Resolve(css, theme, vars), style, theme, vars, null)) throw new FormatException("Not a background: '" + css + "'");
            return style.BackgroundPaint(box);
        }

        /// <summary>A background value as layers (top first) and the colour under them.</summary>
        public static List<BackgroundLayer> Backgrounds(string css, out Rgba color, DsTheme theme = DsTheme.Dusk, CssVars vars = null)
        {
            var style = new BoxStyle();
            if (!TryBackground(Resolve(css, theme, vars), style, theme, vars, null)) throw new FormatException("Not a background: '" + css + "'");
            color = style.BackgroundColor;
            return style.Backgrounds;
        }

        /// <summary>One image value: linear-gradient(…), repeating-linear-gradient(…), radial-gradient(…), url(…), none (→ null).</summary>
        public static BackgroundImage Image(string css, DsTheme theme = DsTheme.Dusk, CssVars vars = null)
        {
            if (TryImage(Resolve(css, theme, vars).Trim(), theme, vars, out BackgroundImage img)) return img;
            throw new FormatException("Not an image: '" + css + "'");
        }

        public static IReadOnlyList<TransitionSpec> Transitions(string css) => TransitionSpec.ParseList(css);
        public static TransformSpec Transform(string css) => TransformSpec.Parse(css);
        public static Rgba Color(string css, DsTheme theme = DsTheme.Dusk, CssVars vars = null) => CssColor.Parse(css, theme, vars);

        public static FilterSpec Filter(string css, DsTheme theme = DsTheme.Dusk, CssVars vars = null)
        {
            if (TryFilter(Resolve(css, theme, vars), theme, vars, null, out FilterSpec f)) return f;
            throw new FormatException("Not a filter: '" + css + "'");
        }

        public static CssLength Length(string css, DsTheme theme = DsTheme.Dusk, CssVars vars = null)
        {
            if (CssLength.TryParse(Resolve(css, theme, vars), out CssLength l)) return l;
            throw new FormatException("Not a length: '" + css + "'");
        }

        private static string Resolve(string css, DsTheme theme, CssVars vars)
        {
            if (css == null) return "";
            if (css.IndexOf("var(", StringComparison.Ordinal) < 0 && css.IndexOf("env(", StringComparison.Ordinal) < 0) return css;
            if (!CssLex.TrySubstitute(css, vars, theme, null, out string s)) throw new FormatException("Undefined var() in '" + css + "'");
            return s;
        }

        // ---- the block ----------------------------------------------------------------------------------------

        private sealed class Ctx
        {
            public DsTheme Theme;
            public CssVars Vars;
            public Dictionary<string, string> Local;
            public CssBlock Block;
            public Rgba? CurrentColor;
        }

        private static CssBlock ParseUncached(string declarations, DsTheme theme, CssVars vars)
        {
            var block = new CssBlock();
            List<(string prop, string value)> decls = CssLex.Declarations(declarations);
            var local = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string p, string v) in decls)
                if (p.StartsWith("--", StringComparison.Ordinal)) { local[p] = v; block.CustomProperties[p] = v; }
            var ctx = new Ctx { Theme = theme, Vars = vars, Local = local, Block = block };
            // the block's own colour is currentColor for the others, wherever it is declared
            foreach ((string p, string v) in decls)
                if (p == "color" && CssLex.TrySubstitute(v, vars, theme, local, out string cs) && CssColor.TryParse(cs, out Rgba cc, theme, vars))
                    ctx.CurrentColor = cc;
            foreach ((string prop, string raw) in decls)
            {
                if (prop.StartsWith("--", StringComparison.Ordinal)) continue;
                string value = raw;
                if (value.IndexOf("var(", StringComparison.Ordinal) >= 0 || value.IndexOf("env(", StringComparison.Ordinal) >= 0)
                {
                    if (!CssLex.TrySubstitute(value, vars, theme, local, out value))
                    {
                        block.Invalid.Add((prop, raw));
                        continue;
                    }
                }
                value = value.Trim();
                Apply(ctx, prop, value, raw);
            }
            return block;
        }

        private static readonly HashSet<string> IgnoredProps = new HashSet<string>(StringComparer.Ordinal)
        {
            "cursor", "user-select", "-webkit-user-select", "touch-action", "will-change", "-webkit-tap-highlight-color",
            "scroll-snap-type", "scroll-snap-align", "scrollbar-width", "resize", "list-style", "accent-color", "caret-color",
            "-webkit-font-smoothing", "-webkit-box-orient", "background-attachment", "background-clip", "background-origin",
            "grid-template-areas", "grid-area", "tab-size", "outline-color-legacy",
        };

        private static void Apply(Ctx ctx, string prop, string value, string raw)
        {
            CssBlock b = ctx.Block;
            bool ok;
            switch (prop)
            {
                // -------- paint
                case "background": ok = TryBackground(value, b.Box, ctx.Theme, ctx.Vars, ctx); break;
                case "background-color": ok = TryColor(ctx, value, out Rgba bg); if (ok) b.Box.BackgroundColor = bg; break;
                case "background-image": ok = TryBackgroundImages(value, b.Box, ctx); break;
                case "background-size": ok = TryLayerList(value, b.Box, ApplySize); break;
                case "background-position": ok = TryLayerList(value, b.Box, ApplyPosition); break;
                case "background-repeat": ok = TryLayerList(value, b.Box, ApplyRepeat); break;
                case "border": ok = TryBorderAll(ctx, value); break;
                case "border-top": ok = TryBorderSide(ctx, value, out BorderSide st); if (ok) b.Box.Top = st; break;
                case "border-right": ok = TryBorderSide(ctx, value, out BorderSide sr); if (ok) b.Box.Right = sr; break;
                case "border-bottom": ok = TryBorderSide(ctx, value, out BorderSide sb); if (ok) b.Box.Bottom = sb; break;
                case "border-left": ok = TryBorderSide(ctx, value, out BorderSide sl); if (ok) b.Box.Left = sl; break;
                case "border-width": ok = TryFour(value, CssLex.TryWidthPx, out float[] bw); if (ok) SetSides(b.Box, bw, (s, v) => s.WithWidth(v)); break;
                case "border-style": ok = TryFour(value, TryBorderStyleText, out BorderStyle[] bs); if (ok) SetSides(b.Box, bs, (s, v) => s.WithStyle(v)); break;
                case "border-color": ok = TryFourColors(ctx, value, out Rgba[] bc); if (ok) SetSides(b.Box, bc, (s, v) => s.WithColor(v)); break;
                case "border-top-width": ok = CssLex.TryWidthPx(value, out float w0); if (ok) b.Box.Top = b.Box.Top.WithWidth(w0); break;
                case "border-right-width": ok = CssLex.TryWidthPx(value, out float w1); if (ok) b.Box.Right = b.Box.Right.WithWidth(w1); break;
                case "border-bottom-width": ok = CssLex.TryWidthPx(value, out float w2); if (ok) b.Box.Bottom = b.Box.Bottom.WithWidth(w2); break;
                case "border-left-width": ok = CssLex.TryWidthPx(value, out float w3); if (ok) b.Box.Left = b.Box.Left.WithWidth(w3); break;
                case "border-top-style": ok = TryBorderStyleText(value, out BorderStyle y0); if (ok) b.Box.Top = b.Box.Top.WithStyle(y0); break;
                case "border-right-style": ok = TryBorderStyleText(value, out BorderStyle y1); if (ok) b.Box.Right = b.Box.Right.WithStyle(y1); break;
                case "border-bottom-style": ok = TryBorderStyleText(value, out BorderStyle y2); if (ok) b.Box.Bottom = b.Box.Bottom.WithStyle(y2); break;
                case "border-left-style": ok = TryBorderStyleText(value, out BorderStyle y3); if (ok) b.Box.Left = b.Box.Left.WithStyle(y3); break;
                case "border-top-color": ok = TryColor(ctx, value, out Rgba c0); if (ok) b.Box.Top = b.Box.Top.WithColor(c0); break;
                case "border-right-color": ok = TryColor(ctx, value, out Rgba c1); if (ok) b.Box.Right = b.Box.Right.WithColor(c1); break;
                case "border-bottom-color": ok = TryColor(ctx, value, out Rgba c2); if (ok) b.Box.Bottom = b.Box.Bottom.WithColor(c2); break;
                case "border-left-color": ok = TryColor(ctx, value, out Rgba c3); if (ok) b.Box.Left = b.Box.Left.WithColor(c3); break;
                case "border-radius": ok = TryRadius(value, out Radii rad); if (ok) b.Box.Radius = rad; break;
                case "border-top-left-radius": ok = TryCorner(value, out CssLength ax, out CssLength ay); if (ok) b.Box.Radius = WithCorner(b.Box.Radius, 0, ax, ay); break;
                case "border-top-right-radius": ok = TryCorner(value, out CssLength bx, out CssLength by); if (ok) b.Box.Radius = WithCorner(b.Box.Radius, 1, bx, by); break;
                case "border-bottom-right-radius": ok = TryCorner(value, out CssLength cx, out CssLength cy); if (ok) b.Box.Radius = WithCorner(b.Box.Radius, 2, cx, cy); break;
                case "border-bottom-left-radius": ok = TryCorner(value, out CssLength dx, out CssLength dy); if (ok) b.Box.Radius = WithCorner(b.Box.Radius, 3, dx, dy); break;
                case "box-shadow": ok = TryShadowsCtx(value, ctx, out List<BoxShadow> shadows); if (ok) b.Box.Shadows = shadows; break;
                case "outline": ok = TryOutline(ctx, value, out Outline ol); if (ok) b.Box.Outline = ol; break;
                case "outline-width": ok = CssLex.TryWidthPx(value, out float ow); if (ok) b.Box.Outline = b.Box.Outline.WithWidth(ow); break;
                case "outline-style": ok = TryBorderStyleText(value, out BorderStyle os); if (ok) b.Box.Outline = b.Box.Outline.WithStyle(os); break;
                case "outline-color": ok = TryColor(ctx, value, out Rgba oc); if (ok) b.Box.Outline = b.Box.Outline.WithColor(oc); break;
                case "outline-offset": ok = CssLex.TryPx(value, out float oo); if (ok) b.Box.Outline = b.Box.Outline.WithOffset(oo); break;
                case "padding": ok = TryFour(value, CssLex.TryPx, out float[] pad); if (ok) b.Box.Padding = new Insets(pad[0], pad[1], pad[2], pad[3]); break;
                case "padding-top": ok = CssLex.TryPx(value, out float p0); if (ok) b.Box.Padding = new Insets(p0, b.Box.Padding.Right, b.Box.Padding.Bottom, b.Box.Padding.Left); break;
                case "padding-right": ok = CssLex.TryPx(value, out float p1); if (ok) b.Box.Padding = new Insets(b.Box.Padding.Top, p1, b.Box.Padding.Bottom, b.Box.Padding.Left); break;
                case "padding-bottom": ok = CssLex.TryPx(value, out float p2); if (ok) b.Box.Padding = new Insets(b.Box.Padding.Top, b.Box.Padding.Right, p2, b.Box.Padding.Left); break;
                case "padding-left": ok = CssLex.TryPx(value, out float p3); if (ok) b.Box.Padding = new Insets(b.Box.Padding.Top, b.Box.Padding.Right, b.Box.Padding.Bottom, p3); break;
                case "opacity": ok = TryOpacity(value, out float op); if (ok) b.Opacity = op; break;
                case "filter": ok = TryFilter(value, ctx.Theme, ctx.Vars, ctx, out FilterSpec fs); if (ok) b.Filter = fs; break;
                case "mix-blend-mode": b.MixBlendMode = value.ToLowerInvariant(); ok = true; break;
                case "mask-image":
                case "-webkit-mask-image":
                    if (value == "none") { b.MaskImage = null; ok = true; }
                    else
                    {
                        ok = TryImage(value, ctx.Theme, ctx.Vars, out BackgroundImage mask);
                        if (ok) b.MaskImage = mask;
                    }
                    break;
                // -------- svg
                case "stroke": ok = TrySvgPaint(ctx, value, out Rgba? strokeC, out bool strokeNone); if (ok) { b.Svg.Stroke = strokeC; b.Svg.StrokeNone = strokeNone; } break;
                case "fill": ok = TrySvgPaint(ctx, value, out Rgba? fillC, out bool fillNone); if (ok) { b.Svg.Fill = fillC; b.Svg.FillNone = fillNone; } break;
                case "stroke-width": ok = CssLex.TryPxOrNumber(value, out float sw); if (ok) b.Svg.StrokeWidth = sw; break;
                case "stroke-linecap": ok = TryCap(value, out Cap cap); if (ok) b.Svg.LineCap = cap; break;
                case "stroke-linejoin": ok = TryJoin(value, out Join join); if (ok) b.Svg.LineJoin = join; break;
                case "stroke-dasharray": ok = TryDashArray(value, out float[] da); if (ok) b.Svg.DashArray = da; break;
                case "stroke-dashoffset": ok = CssLex.TryPxOrNumber(value, out float dof); if (ok) b.Svg.DashOffset = dof; break;
                case "fill-opacity": ok = TryOpacity(value, out float fo); if (ok) b.Svg.FillOpacity = fo; break;
                case "stroke-opacity": ok = TryOpacity(value, out float so); if (ok) b.Svg.StrokeOpacity = so; break;
                // -------- text
                case "color":
                    ok = true;
                    if (value != "inherit")
                    {
                        ok = TryColor(ctx, value, out Rgba col);
                        if (ok) b.Text.Color = col;
                    }
                    break;
                case "font": ok = TryFontShorthand(ctx, value); break;
                case "font-family": ok = true; if (value.Trim() != "inherit") b.Text.Family = TextMath.FamilyOf(value); break;
                case "font-size": ok = CssLex.TryPx(value, out float fz); if (ok) b.Text.Size = fz; break;
                case "font-weight": ok = TextMath.TryWeight(value, out int fw); if (ok) b.Text.Weight = fw; break;
                case "font-style": ok = true; b.Text.Italic = value.Trim() == "italic" || value.Trim() == "oblique"; break;
                case "line-height": ok = TryLineHeight(value, b.Text); break;
                case "letter-spacing": ok = TryLetterSpacing(value, b.Text); break;
                case "text-align": ok = TryTextAlign(value, out TextAlign ta); if (ok) b.Text.Align = ta; break;
                case "white-space": ok = TryWhiteSpace(value, out WhiteSpace ws); if (ok) b.Text.WhiteSpace = ws; break;
                case "text-overflow": ok = true; b.Text.Ellipsis = value.Trim() == "ellipsis"; break;
                case "text-shadow": ok = TryTextShadowsCtx(value, ctx, out List<TextShadow> tsh); if (ok) b.Text.Shadows = tsh; break;
                case "text-decoration": ok = TryTextDecoration(ctx, value, b.Text); break;
                case "text-decoration-line": ok = TryDecorationLine(value, out DecorationLine dl); if (ok) b.Text.Decoration = dl; break;
                case "text-decoration-style": ok = TryBorderStyleText(value, out BorderStyle dst); if (ok) b.Text.DecorationStyle = dst; break;
                case "text-decoration-color": ok = TryColor(ctx, value, out Rgba dcl); if (ok) b.Text.DecorationColor = dcl; break;
                case "text-decoration-thickness": ok = CssLex.TryPx(value, out float dth); if (ok) b.Text.DecorationThickness = dth; break;
                case "text-underline-offset": ok = CssLex.TryPx(value, out float uo); if (ok) b.Text.UnderlineOffset = uo; break;
                case "font-variant-numeric": ok = true; b.Text.TabularNums = value.Contains("tabular-nums"); break;
                case "word-break": ok = true; b.Text.WordBreak = value; break;
                case "line-break": ok = true; b.Text.LineBreak = value; break;
                case "overflow-wrap": ok = true; b.Text.OverflowWrap = value; break;
                case "text-wrap": ok = true; b.Text.TextWrap = value; break;
                case "writing-mode": ok = true; b.Text.WritingMode = value; break;
                case "-webkit-line-clamp": ok = int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int clamp); if (ok) b.Text.LineClamp = clamp; break;
                // -------- layout
                case "display": ok = true; b.Layout.Display = value.ToLowerInvariant(); break;
                case "position": ok = TryPosition(value, out Position pos); if (ok) b.Layout.Position = pos; break;
                case "inset": ok = TryFourLengths(value, out CssLength[] ins); if (ok) { b.Layout.Top = ins[0]; b.Layout.Right = ins[1]; b.Layout.Bottom = ins[2]; b.Layout.Left = ins[3]; } break;
                case "left": ok = CssLength.TryParse(value, out CssLength ll); if (ok) b.Layout.Left = ll; break;
                case "top": ok = CssLength.TryParse(value, out CssLength lt); if (ok) b.Layout.Top = lt; break;
                case "right": ok = CssLength.TryParse(value, out CssLength lr); if (ok) b.Layout.Right = lr; break;
                case "bottom": ok = CssLength.TryParse(value, out CssLength lb); if (ok) b.Layout.Bottom = lb; break;
                case "width": ok = TrySize(value, out CssLength lw); if (ok) b.Layout.Width = lw; break;
                case "height": ok = TrySize(value, out CssLength lh); if (ok) b.Layout.Height = lh; break;
                case "min-width": ok = TrySize(value, out CssLength mnw); if (ok) b.Layout.MinWidth = mnw; break;
                case "min-height": ok = TrySize(value, out CssLength mnh); if (ok) b.Layout.MinHeight = mnh; break;
                case "max-width": ok = TrySize(value, out CssLength mxw); if (ok) b.Layout.MaxWidth = mxw; break;
                case "max-height": ok = TrySize(value, out CssLength mxh); if (ok) b.Layout.MaxHeight = mxh; break;
                case "margin": ok = TryFourLengths(value, out CssLength[] mg); if (ok) { b.Layout.MarginTop = mg[0]; b.Layout.MarginRight = mg[1]; b.Layout.MarginBottom = mg[2]; b.Layout.MarginLeft = mg[3]; } break;
                case "margin-top": ok = CssLength.TryParse(value, out CssLength m0); if (ok) b.Layout.MarginTop = m0; break;
                case "margin-right": ok = CssLength.TryParse(value, out CssLength m1); if (ok) b.Layout.MarginRight = m1; break;
                case "margin-bottom": ok = CssLength.TryParse(value, out CssLength m2); if (ok) b.Layout.MarginBottom = m2; break;
                case "margin-left": ok = CssLength.TryParse(value, out CssLength m3); if (ok) b.Layout.MarginLeft = m3; break;
                case "box-sizing": ok = true; b.Layout.BorderBox = value.Trim() == "border-box"; break;
                case "flex": ok = TryFlex(value, b.Layout); break;
                case "flex-direction": ok = TryDirection(value, b.Layout); break;
                case "flex-wrap": ok = true; b.Layout.Wrap = value.Trim() == "wrap" || value.Trim() == "wrap-reverse"; break;
                case "flex-grow": ok = CssLex.TryNumber(value.Trim(), out float fg, out string fgu) && fgu.Length == 0; if (ok) b.Layout.Grow = fg; break;
                case "flex-shrink": ok = CssLex.TryNumber(value.Trim(), out float fsk, out string fsu) && fsu.Length == 0; if (ok) b.Layout.Shrink = fsk; break;
                case "flex-basis": ok = TrySize(value, out CssLength fb); if (ok) b.Layout.Basis = fb; break;
                case "gap": ok = TryGap(value, b.Layout); break;
                case "row-gap": ok = CssLex.TryPx(value, out float rg); if (ok) b.Layout.RowGap = rg; break;
                case "column-gap": ok = CssLex.TryPx(value, out float cg); if (ok) b.Layout.ColumnGap = cg; break;
                case "align-items": ok = TryAlign(value, out Align ai); if (ok) b.Layout.AlignItems = ai; break;
                // auto = the container's align-items / justify-items (null)
                case "align-self": ok = value.Trim() == "auto" || TryAlign(value, out _); if (ok) b.Layout.AlignSelf = value.Trim() == "auto" ? (Align?)null : AlignOf(value); break;
                case "justify-self": ok = value.Trim() == "auto" || TryAlign(value, out _); if (ok) b.Layout.JustifySelf = value.Trim() == "auto" ? (Align?)null : AlignOf(value); break;
                case "justify-items": ok = TryAlign(value, out Align jit); if (ok) b.Layout.JustifyItems = jit; break;
                case "justify-content": ok = TryJustify(value, out Justify jc); if (ok) b.Layout.JustifyContent = jc; break;
                case "align-content": ok = TryAlignContent(value, out AlignContent ac); if (ok) b.Layout.AlignContent = ac; break;
                case "order": ok = int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ord); if (ok) b.Layout.Order = ord; break;
                case "z-index":
                    ok = value == "auto";
                    if (!ok && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int zi))
                    {
                        b.Layout.ZIndex = zi;
                        ok = true;
                    }
                    break;
                case "overflow": ok = true; { List<string> ov = CssLex.SplitSpaces(value); b.Layout.OverflowX = ov[0]; b.Layout.OverflowY = ov.Count > 1 ? ov[1] : ov[0]; } break;
                case "overflow-x": ok = true; b.Layout.OverflowX = value.Trim(); break;
                case "overflow-y": ok = true; b.Layout.OverflowY = value.Trim(); break;
                case "grid-template-columns": ok = TryTracks(value, out List<Track> gcols); if (ok) b.Layout.GridColumns = gcols; break;
                case "grid-template-rows": ok = TryTracks(value, out List<Track> grows); if (ok) b.Layout.GridRows = grows; break;
                case "grid-column": ok = TryGridLine(value, out int gcs, out int gce, out int gsp); if (ok) { b.Layout.GridColumnStart = gcs; b.Layout.GridColumnEnd = gce; b.Layout.GridColumnSpan = gsp; } break;
                case "grid-row": ok = TryGridLine(value, out int grs, out int gre, out int grp); if (ok) { b.Layout.GridRowStart = grs; b.Layout.GridRowEnd = gre; b.Layout.GridRowSpan = grp; } break;
                case "vertical-align": ok = true; b.Layout.VerticalAlign = value.Trim(); break;
                case "visibility": ok = true; b.Visibility = value.Trim(); break;
                case "pointer-events": ok = true; b.PointerEvents = value.Trim(); break;
                case "content": ok = true; b.Content = value; break;
                // -------- transform and motion
                case "transform": ok = TransformSpec.TryParse(value, out TransformSpec tf); if (ok) b.Transform = tf; break;
                case "translate": ok = TransformSpec.TryParseTranslateProperty(value, out TransformSpec tr); if (ok) b.Translate = tr; break;
                case "scale": ok = TransformSpec.TryParseScaleProperty(value, out TransformSpec sc); if (ok) b.Scale = sc; break;
                case "rotate": ok = TransformSpec.TryParseRotateProperty(value, out TransformSpec ro); if (ok) b.Rotate = ro; break;
                case "transform-origin": ok = TryOrigin(value, out CssLength ox, out CssLength oy); if (ok) { b.OriginX = ox; b.OriginY = oy; } break;
                case "transition": ok = true; b.Transitions = TransitionSpec.ParseList(value); break;
                case "transition-property":
                case "transition-duration":
                case "transition-delay":
                case "transition-timing-function":
                    ok = TryTransitionLonghand(b, prop, value);
                    break;
                case "animation": ok = true; b.Animations = CssAnimation.ParseList(value); break;
                case "animation-duration":
                case "animation-delay":
                case "animation-name":
                case "animation-timing-function":
                case "animation-iteration-count":
                case "animation-direction":
                case "animation-fill-mode":
                case "animation-play-state":
                    ok = TryAnimationLonghand(b, prop, value);
                    break;
                default:
                    if (IgnoredProps.Contains(prop)) { b.Ignored.Add((prop, raw)); return; }
                    b.Unknown.Add((prop, raw));
                    return;
            }
            if (!ok) b.Invalid.Add((prop, raw));
        }

        /// <summary>transition-property / -duration / -delay / -timing-function: comma lists matched to the transitions by index (cycled).</summary>
        private static bool TryTransitionLonghand(CssBlock b, string prop, string value)
        {
            List<string> parts = CssLex.SplitTopLevel(value, ',');
            if (b.Transitions == null || b.Transitions.Count == 0)
            {
                b.Transitions = new List<TransitionSpec>();
                int n = prop == "transition-property" ? parts.Count : 1;
                for (int i = 0; i < n; i++) b.Transitions.Add(new TransitionSpec());
            }
            for (int i = 0; i < b.Transitions.Count; i++)
            {
                string p = parts[i % parts.Count].Trim().ToLowerInvariant();
                TransitionSpec t = b.Transitions[i];
                switch (prop)
                {
                    case "transition-property": t.Property = p; break;
                    case "transition-duration": if (!CssLex.TryTimeMs(p, out t.DurationMs)) return false; break;
                    case "transition-delay": if (!CssLex.TryTimeMs(p, out t.DelayMs)) return false; break;
                    default:
                        if (!CssAnimation.IsTiming(p)) return false;
                        t.EasingText = p;
                        t.Easing = CssAnimation.EaseOfCss(p);
                        break;
                }
            }
            return true;
        }

        /// <summary>
        /// animation-* longhands: comma lists matched to the animations by index and cycled, as CSS does (a single
        /// `animation-duration: 1ms` applies to every animation of the element).
        /// </summary>
        private static bool TryAnimationLonghand(CssBlock b, string prop, string value)
        {
            List<string> parts = CssLex.SplitTopLevel(value, ',');
            if (b.Animations == null || b.Animations.Count == 0)
            {
                b.Animations = new List<CssAnimation>();
                int n = prop == "animation-name" ? parts.Count : 1;
                for (int i = 0; i < n; i++) b.Animations.Add(new CssAnimation());
            }
            for (int i = 0; i < b.Animations.Count; i++)
            {
                string raw = parts[i % parts.Count].Trim();
                string p = raw.ToLowerInvariant();
                CssAnimation a = b.Animations[i];
                switch (prop)
                {
                    case "animation-duration": if (!CssLex.TryTimeMs(p, out float dur) || dur < 0f) return false; a.DurationMs = dur; break;
                    case "animation-delay": if (!CssLex.TryTimeMs(p, out float del)) return false; a.DelayMs = del; break;
                    case "animation-name": a.Name = raw; a.Keyframes = CssKeyframes.Get(raw); break;
                    case "animation-timing-function":
                        if (!CssAnimation.IsTiming(p)) return false;
                        a.EasingText = p;
                        a.Easing = CssAnimation.EaseOfCss(p);
                        break;
                    case "animation-iteration-count":
                        if (p == "infinite") a.Iterations = float.PositiveInfinity;
                        else if (CssLex.TryNumber(p, out float count, out string unit) && unit.Length == 0 && count >= 0f) a.Iterations = count;
                        else return false;
                        break;
                    case "animation-direction":
                        switch (p)
                        {
                            case "normal": a.Direction = AnimDirection.Normal; break;
                            case "reverse": a.Direction = AnimDirection.Reverse; break;
                            case "alternate": a.Direction = AnimDirection.Alternate; break;
                            case "alternate-reverse": a.Direction = AnimDirection.AlternateReverse; break;
                            default: return false;
                        }
                        break;
                    case "animation-fill-mode":
                        switch (p)
                        {
                            case "none": a.Fill = AnimFill.None; break;
                            case "forwards": a.Fill = AnimFill.Forwards; break;
                            case "backwards": a.Fill = AnimFill.Backwards; break;
                            case "both": a.Fill = AnimFill.Both; break;
                            default: return false;
                        }
                        break;
                    default: // animation-play-state
                        if (p != "running" && p != "paused") return false;
                        a.Paused = p == "paused";
                        break;
                }
            }
            return true;
        }

        // ---- colours ------------------------------------------------------------------------------------------

        private static bool TryColor(Ctx ctx, string value, out Rgba c)
        {
            string v = value.Trim();
            if (v.Equals("currentcolor", StringComparison.OrdinalIgnoreCase)) { c = CurrentColor(ctx); return true; }
            return CssColor.TryParse(v, out c, ctx?.Theme ?? DsTheme.Dusk, ctx?.Vars);
        }

        private static Rgba CurrentColor(Ctx ctx)
        {
            if (ctx?.CurrentColor != null) return ctx.CurrentColor.Value;
            if (ctx?.Vars != null && ctx.Vars.TryGet("color", out string cs) && CssColor.TryParse(cs, out Rgba c, ctx.Theme, ctx.Vars)) return c;
            return DsTokens.Ink;
        }

        private static bool IsColor(Ctx ctx, string token, out Rgba c)
        {
            c = Rgba.Transparent;
            string t = token.Trim();
            if (t.Length == 0) return false;
            if (t.Equals("currentcolor", StringComparison.OrdinalIgnoreCase)) { c = CurrentColor(ctx); return true; }
            char first = t[0];
            if (char.IsDigit(first) || first == '.' || first == '-' || first == '+') return false;
            return CssColor.TryParse(t, out c, ctx?.Theme ?? DsTheme.Dusk, ctx?.Vars);
        }

        // ---- backgrounds --------------------------------------------------------------------------------------

        private static bool TryBackground(string value, BoxStyle box, DsTheme theme, CssVars vars, Ctx ctx)
        {
            ctx = ctx ?? new Ctx { Theme = theme, Vars = vars };
            box.BackgroundColor = Rgba.Transparent;
            box.Backgrounds = new List<BackgroundLayer>();
            string v = value.Trim();
            if (v == "none" || v == "transparent") return true;
            List<string> layers = CssLex.SplitTopLevel(v, ',');
            for (int li = 0; li < layers.Count; li++)
            {
                bool last = li == layers.Count - 1;
                var layer = new BackgroundLayer();
                bool any = false;
                List<string> tokens = SplitSlash(CssLex.SplitSpaces(layers[li]));
                var posTokens = new List<string>();
                var sizeTokens = new List<string>();
                bool afterSlash = false;
                foreach (string tok in tokens)
                {
                    string t = tok.ToLowerInvariant();
                    if (t == "/") { afterSlash = true; continue; }
                    if (TryImage(tok, theme, vars, out BackgroundImage img)) { layer.Image = img; any = true; continue; }
                    if (t == "none") { any = true; continue; }
                    if (t == "repeat" || t == "no-repeat" || t == "repeat-x" || t == "repeat-y" || t == "space" || t == "round")
                    {
                        layer.Repeat = t == "no-repeat" ? BackgroundRepeat.NoRepeat : t == "repeat-x" ? BackgroundRepeat.RepeatX : t == "repeat-y" ? BackgroundRepeat.RepeatY : BackgroundRepeat.Repeat;
                        continue;
                    }
                    if (t == "padding-box" || t == "border-box" || t == "content-box" || t == "scroll" || t == "fixed" || t == "local") continue;
                    if (last && IsColor(ctx, tok, out Rgba col)) { box.BackgroundColor = col; any = true; continue; }
                    if (afterSlash) sizeTokens.Add(t);
                    else posTokens.Add(t);
                }
                if (posTokens.Count > 0)
                {
                    if (!CssPosition.TryParse(posTokens, out CssLength px, out CssLength py)) return false;
                    layer.PosX = px;
                    layer.PosY = py;
                }
                if (sizeTokens.Count > 0 && !ApplySize(layer, string.Join(" ", sizeTokens))) return false;
                if (!any && posTokens.Count == 0 && sizeTokens.Count == 0) return false;
                if (layer.Image != null || !last) box.Backgrounds.Add(layer);
            }
            return true;
        }

        private static List<string> SplitSlash(List<string> tokens)
        {
            var o = new List<string>();
            foreach (string t in tokens)
            {
                int i = CssLex.IndexOfTopLevel(t, '/');
                if (i < 0 || t == "/") { o.Add(t); continue; }
                if (i > 0) o.Add(t.Substring(0, i));
                o.Add("/");
                if (i + 1 < t.Length) o.Add(t.Substring(i + 1));
            }
            return o;
        }

        private static bool TryBackgroundImages(string value, BoxStyle box, Ctx ctx)
        {
            var list = new List<BackgroundLayer>();
            foreach (string part in CssLex.SplitTopLevel(value, ','))
            {
                string p = part.Trim();
                var layer = new BackgroundLayer();
                if (p != "none")
                {
                    if (!TryImage(p, ctx.Theme, ctx.Vars, out BackgroundImage img)) return false;
                    layer.Image = img;
                }
                list.Add(layer);
            }
            box.Backgrounds = list;
            return true;
        }

        private static bool TryLayerList(string value, BoxStyle box, Func<BackgroundLayer, string, bool> apply)
        {
            List<string> parts = CssLex.SplitTopLevel(value, ',');
            if (box.Backgrounds.Count == 0) box.Backgrounds.Add(new BackgroundLayer());
            for (int i = 0; i < box.Backgrounds.Count; i++)
                if (!apply(box.Backgrounds[i], parts[i % parts.Count].Trim().ToLowerInvariant())) return false;
            return true;
        }

        private static bool ApplySize(BackgroundLayer layer, string text)
        {
            List<string> t = CssLex.SplitSpaces(text);
            if (t.Count == 1 && (t[0] == "cover" || t[0] == "contain")) { layer.SizeKeyword = t[0]; return true; }
            if (t.Count < 1 || t.Count > 2) return false;
            if (!CssLength.TryParse(t[0], out CssLength w)) return false;
            CssLength h = CssLength.Auto;
            if (t.Count == 2 && !CssLength.TryParse(t[1], out h)) return false;
            layer.SizeW = w;
            layer.SizeH = h;
            return true;
        }

        private static bool ApplyPosition(BackgroundLayer layer, string text)
        {
            if (!CssPosition.TryParse(CssLex.SplitSpaces(text), out CssLength x, out CssLength y)) return false;
            layer.PosX = x;
            layer.PosY = y;
            return true;
        }

        private static bool ApplyRepeat(BackgroundLayer layer, string text)
        {
            switch (text.Trim())
            {
                case "repeat": layer.Repeat = BackgroundRepeat.Repeat; return true;
                case "no-repeat": layer.Repeat = BackgroundRepeat.NoRepeat; return true;
                case "repeat-x": layer.Repeat = BackgroundRepeat.RepeatX; return true;
                case "repeat-y": layer.Repeat = BackgroundRepeat.RepeatY; return true;
                default: return false;
            }
        }

        /// <summary>One image token: a gradient function or url(); false for anything else (none included).</summary>
        internal static bool TryImage(string token, DsTheme theme, CssVars vars, out BackgroundImage image)
        {
            image = null;
            string t = token.Trim();
            if (!CssLex.TryFunction(t, out string name, out string args)) return false;
            name = name.ToLowerInvariant();
            if (name == "url") { image = new UrlImage { Text = t }; return true; }
            bool repeating = name.StartsWith("repeating-", StringComparison.Ordinal);
            string kind = repeating ? name.Substring(10) : name;
            List<string> parts = CssLex.SplitTopLevel(args, ',');
            if (parts.Count == 0) return false;
            if (kind == "linear-gradient")
            {
                var spec = new LinearGradientSpec { Repeating = repeating };
                int first = 0;
                string p0 = parts[0].Trim().ToLowerInvariant();
                if (p0.StartsWith("to ", StringComparison.Ordinal)) { spec.ToSide = p0; first = 1; }
                else if (CssLex.TryAngleDeg(p0, out float deg)) { spec.AngleDeg = deg; first = 1; }
                if (!TryStops(parts, first, theme, vars, spec.Stops)) return false;
                image = spec;
                return true;
            }
            if (kind == "radial-gradient")
            {
                var spec = new RadialGradientSpec { Repeating = repeating };
                int first = 0;
                string p0 = parts[0].Trim().ToLowerInvariant();
                if (IsRadialPrelude(p0)) { spec.Prelude = p0; first = 1; }
                if (!TryStops(parts, first, theme, vars, spec.Stops)) return false;
                image = spec;
                return true;
            }
            return false;
        }

        private static bool IsRadialPrelude(string p)
        {
            if (p.StartsWith("circle", StringComparison.Ordinal) || p.StartsWith("ellipse", StringComparison.Ordinal) ||
                p.StartsWith("closest-", StringComparison.Ordinal) || p.StartsWith("farthest-", StringComparison.Ordinal) ||
                p.StartsWith("at ", StringComparison.Ordinal)) return true;
            List<string> toks = CssLex.SplitSpaces(p);
            return toks.Count > 0 && CssLength.TryParse(toks[0], out _) && !CssColor.TryParse(p, out _);
        }

        private static bool TryStops(List<string> parts, int first, DsTheme theme, CssVars vars, List<GradientStop> stops)
        {
            for (int i = first; i < parts.Count; i++)
            {
                List<string> toks = CssLex.SplitSpaces(parts[i].Trim());
                if (toks.Count == 0) return false;
                if (toks.Count == 1 && CssLength.TryParse(toks[0], out _)) continue; // a transition hint: ignored (the trial writes none)
                if (!CssColor.TryParse(toks[0], out Rgba c, theme, vars)) return false;
                if (toks.Count == 1) { stops.Add(GradientStop.Auto(c)); continue; }
                for (int k = 1; k < toks.Count && k <= 2; k++)
                {
                    if (!CssLength.TryParse(toks[k], out CssLength pos) || pos.IsAuto) return false;
                    if (pos.Percent != 0f && pos.Px != 0f) return false;
                    stops.Add(pos.Percent != 0f || pos.IsZero && toks[k].EndsWith("%", StringComparison.Ordinal)
                        ? GradientStop.At(c, pos.Percent / 100f)
                        : pos.IsZero ? GradientStop.At(c, 0f) : GradientStop.Px(c, pos.Px));
                }
            }
            return stops.Count > 0;
        }

        // ---- borders ------------------------------------------------------------------------------------------

        private static bool TryBorderStyleText(string text, out BorderStyle style)
        {
            switch (text.Trim().ToLowerInvariant())
            {
                case "none":
                case "hidden": style = BorderStyle.None; return true;
                case "solid":
                case "groove":
                case "ridge":
                case "inset":
                case "outset": style = BorderStyle.Solid; return true;
                case "dashed": style = BorderStyle.Dashed; return true;
                case "dotted": style = BorderStyle.Dotted; return true;
                case "double": style = BorderStyle.Double; return true;
                default: style = BorderStyle.None; return false;
            }
        }

        /// <summary>"1px solid var(--rule)" in any order; omitted parts take the initial values (medium = 3px, none, currentColor).</summary>
        private static bool TryBorderSide(Ctx ctx, string value, out BorderSide side)
        {
            side = BorderSide.None;
            float width = 3f;
            BorderStyle style = BorderStyle.None;
            Rgba color = CurrentColor(ctx);
            string v = value.Trim();
            if (v == "none" || v == "0") { side = new BorderSide(0f, BorderStyle.None, color); return true; }
            foreach (string tok in CssLex.SplitSpaces(v))
            {
                if (TryBorderStyleText(tok, out BorderStyle s)) { style = s; continue; }
                if (CssLex.TryWidthPx(tok, out float w)) { width = w; continue; }
                if (IsColor(ctx, tok, out Rgba c)) { color = c; continue; }
                return false;
            }
            side = new BorderSide(width, style, color);
            return true;
        }

        private static bool TryBorderAll(Ctx ctx, string value)
        {
            if (!TryBorderSide(ctx, value, out BorderSide s)) return false;
            ctx.Block.Box.SetBorder(s);
            return true;
        }

        private static void SetSides<T>(BoxStyle box, T[] v, Func<BorderSide, T, BorderSide> with)
        {
            box.Top = with(box.Top, v[0]);
            box.Right = with(box.Right, v[1]);
            box.Bottom = with(box.Bottom, v[2]);
            box.Left = with(box.Left, v[3]);
        }

        private delegate bool TryOne<T>(string text, out T value);

        /// <summary>1–4 values in CSS box order (top, right, bottom, left).</summary>
        private static bool TryFour<T>(string value, TryOne<T> one, out T[] four)
        {
            four = null;
            List<string> toks = CssLex.SplitSpaces(value);
            if (toks.Count < 1 || toks.Count > 4) return false;
            var v = new T[toks.Count];
            for (int i = 0; i < toks.Count; i++) if (!one(toks[i], out v[i])) return false;
            four = Expand(v);
            return true;
        }

        private static T[] Expand<T>(T[] v)
        {
            switch (v.Length)
            {
                case 1: return new[] { v[0], v[0], v[0], v[0] };
                case 2: return new[] { v[0], v[1], v[0], v[1] };
                case 3: return new[] { v[0], v[1], v[2], v[1] };
                default: return v;
            }
        }

        private static bool TryFourColors(Ctx ctx, string value, out Rgba[] four)
        {
            four = null;
            List<string> toks = CssLex.SplitSpaces(value);
            if (toks.Count < 1 || toks.Count > 4) return false;
            var v = new Rgba[toks.Count];
            for (int i = 0; i < toks.Count; i++) if (!TryColor(ctx, toks[i], out v[i])) return false;
            four = Expand(v);
            return true;
        }

        private static bool TryFourLengths(string value, out CssLength[] four)
        {
            four = null;
            List<string> toks = CssLex.SplitSpaces(value);
            if (toks.Count < 1 || toks.Count > 4) return false;
            var v = new CssLength[toks.Count];
            for (int i = 0; i < toks.Count; i++) if (!CssLength.TryParse(toks[i], out v[i])) return false;
            four = Expand(v);
            return true;
        }

        /// <summary>border-radius: 1–4 values, optionally "/ 1–4 vertical values".</summary>
        private static bool TryRadius(string value, out Radii radii)
        {
            radii = Radii.Zero;
            string v = value.Trim();
            int slash = CssLex.IndexOfTopLevel(v, '/');
            string hText = slash >= 0 ? v.Substring(0, slash) : v;
            string vText = slash >= 0 ? v.Substring(slash + 1) : v;
            if (!TryFourLengths(hText, out CssLength[] h) || !TryFourLengths(vText, out CssLength[] vv)) return false;
            foreach (CssLength l in h) if (l.IsAuto) return false;
            foreach (CssLength l in vv) if (l.IsAuto) return false;
            radii = new Radii(h[0], vv[0], h[1], vv[1], h[2], vv[2], h[3], vv[3]);
            return true;
        }

        private static bool TryCorner(string value, out CssLength x, out CssLength y)
        {
            y = CssLength.Zero;
            List<string> t = CssLex.SplitSpaces(value);
            if (t.Count < 1 || !CssLength.TryParse(t[0], out x)) { x = CssLength.Zero; return false; }
            y = x;
            return t.Count < 2 || CssLength.TryParse(t[1], out y);
        }

        private static Radii WithCorner(Radii r, int corner, CssLength x, CssLength y) => corner switch
        {
            0 => new Radii(x, y, r.TRx, r.TRy, r.BRx, r.BRy, r.BLx, r.BLy),
            1 => new Radii(r.TLx, r.TLy, x, y, r.BRx, r.BRy, r.BLx, r.BLy),
            2 => new Radii(r.TLx, r.TLy, r.TRx, r.TRy, x, y, r.BLx, r.BLy),
            _ => new Radii(r.TLx, r.TLy, r.TRx, r.TRy, r.BRx, r.BRy, x, y),
        };

        private static bool TryOutline(Ctx ctx, string value, out Outline outline)
        {
            outline = Outline.None;
            if (!TryBorderSide(ctx, value, out BorderSide s)) return false;
            float offset = ctx.Block.Box.Outline.Offset;
            outline = new Outline(s.Width, s.Style, s.Color, offset);
            return true;
        }

        // ---- shadows ------------------------------------------------------------------------------------------

        public static bool TryShadows(string value, DsTheme theme, CssVars vars, out List<BoxShadow> list)
        {
            string v = value ?? "";
            if (v.IndexOf("var(", StringComparison.Ordinal) >= 0 && !CssLex.TrySubstitute(v, vars, theme, null, out v)) { list = null; return false; }
            return TryShadowsCtx(v, new Ctx { Theme = theme, Vars = vars }, out list);
        }

        private static bool TryShadowsCtx(string value, Ctx ctx, out List<BoxShadow> list)
        {
            list = new List<BoxShadow>();
            string v = value.Trim();
            if (v == "none") return true;
            foreach (string layer in CssLex.SplitTopLevel(v, ','))
            {
                bool inset = false;
                var lens = new List<float>();
                Rgba color = CurrentColor(ctx);
                foreach (string tok in CssLex.SplitSpaces(layer))
                {
                    if (tok.Equals("inset", StringComparison.OrdinalIgnoreCase)) { inset = true; continue; }
                    if (CssLex.TryPx(tok, out float px)) { lens.Add(px); continue; }
                    if (IsColor(ctx, tok, out Rgba c)) { color = c; continue; }
                    list = null;
                    return false;
                }
                if (lens.Count < 2 || lens.Count > 4) { list = null; return false; }
                list.Add(new BoxShadow(lens[0], lens[1], lens.Count > 2 ? lens[2] : 0f, lens.Count > 3 ? lens[3] : 0f, color, inset));
            }
            return true;
        }

        public static bool TryTextShadows(string value, DsTheme theme, CssVars vars, out List<TextShadow> list)
        {
            string v = value ?? "";
            if (v.IndexOf("var(", StringComparison.Ordinal) >= 0 && !CssLex.TrySubstitute(v, vars, theme, null, out v)) { list = null; return false; }
            return TryTextShadowsCtx(v, new Ctx { Theme = theme, Vars = vars }, out list);
        }

        private static bool TryTextShadowsCtx(string value, Ctx ctx, out List<TextShadow> list)
        {
            list = new List<TextShadow>();
            string v = value.Trim();
            if (v == "none") return true;
            foreach (string layer in CssLex.SplitTopLevel(v, ','))
            {
                var lens = new List<float>();
                Rgba color = CurrentColor(ctx);
                foreach (string tok in CssLex.SplitSpaces(layer))
                {
                    if (CssLex.TryPx(tok, out float px)) { lens.Add(px); continue; }
                    if (IsColor(ctx, tok, out Rgba c)) { color = c; continue; }
                    list = null;
                    return false;
                }
                if (lens.Count < 2 || lens.Count > 3) { list = null; return false; }
                list.Add(new TextShadow(lens[0], lens[1], lens.Count > 2 ? lens[2] : 0f, color));
            }
            return true;
        }

        // ---- filters, svg -------------------------------------------------------------------------------------

        private static bool TryFilter(string value, DsTheme theme, CssVars vars, Ctx ctx, out FilterSpec spec)
        {
            ctx = ctx ?? new Ctx { Theme = theme, Vars = vars };
            spec = new FilterSpec();
            string v = value.Trim();
            if (v == "none") return true;
            foreach (string tok in CssLex.SplitSpaces(v))
            {
                if (!CssLex.TryFunction(tok, out string name, out string args)) return false;
                name = name.ToLowerInvariant();
                switch (name)
                {
                    case "brightness":
                    {
                        if (!TryAmount(args, out float k)) return false;
                        spec.Color = new ColorFilter(spec.Color.Brightness * k, spec.Color.Grayscale);
                        break;
                    }
                    case "grayscale":
                    {
                        if (!TryAmount(args, out float g)) return false;
                        spec.Color = spec.Color.Then(new ColorFilter(1f, Math.Min(1f, g)));
                        break;
                    }
                    case "drop-shadow":
                    {
                        var lens = new List<float>();
                        Rgba color = CurrentColor(ctx);
                        foreach (string t in CssLex.SplitSpaces(args))
                        {
                            if (CssLex.TryPx(t, out float px)) { lens.Add(px); continue; }
                            if (IsColor(ctx, t, out Rgba c)) { color = c; continue; }
                            return false;
                        }
                        if (lens.Count < 2 || lens.Count > 3) return false;
                        spec.DropShadows.Add(new DropShadow(lens[0], lens[1], lens.Count > 2 ? lens[2] : 0f, color));
                        break;
                    }
                    default:
                        return false;
                }
            }
            return true;
        }

        private static bool TryAmount(string text, out float v)
        {
            string t = text.Trim();
            if (t.Length == 0) { v = 1f; return true; }
            if (!CssLex.TryNumber(t, out v, out string unit)) return false;
            if (unit == "%") v /= 100f;
            else if (unit.Length != 0) return false;
            return true;
        }

        private static bool TrySvgPaint(Ctx ctx, string value, out Rgba? color, out bool none)
        {
            color = null;
            none = value.Trim() == "none";
            if (none) return true;
            if (!TryColor(ctx, value, out Rgba c)) return false;
            color = c;
            return true;
        }

        private static bool TryCap(string v, out Cap cap)
        {
            switch (v.Trim())
            {
                case "butt": cap = Cap.Butt; return true;
                case "round": cap = Cap.Round; return true;
                case "square": cap = Cap.Square; return true;
                default: cap = Cap.Butt; return false;
            }
        }

        private static bool TryJoin(string v, out Join join)
        {
            switch (v.Trim())
            {
                case "miter": join = Join.Miter; return true;
                case "round": join = Join.Round; return true;
                case "bevel": join = Join.Bevel; return true;
                default: join = Join.Miter; return false;
            }
        }

        private static bool TryDashArray(string v, out float[] dash)
        {
            dash = null;
            string t = v.Trim();
            if (t == "none") return true;
            var list = new List<float>();
            foreach (string tok in CssLex.SplitSpaces(t.Replace(',', ' ')))
            {
                if (!CssLex.TryPxOrNumber(tok, out float d)) return false;
                list.Add(d);
            }
            dash = list.ToArray();
            return true;
        }

        // ---- text ---------------------------------------------------------------------------------------------

        private static bool TryFontShorthand(Ctx ctx, string value)
        {
            string v = value.Trim();
            if (v == "inherit") return true;
            if (!TextMath.TryParseFont(v, out FontSpec f, out bool italic, ctx.Theme, ctx.Vars)) return false;
            TextStyleData t = ctx.Block.Text;
            t.Family = f.Family;
            t.Weight = f.Weight;
            t.Size = f.Size;
            t.Italic = italic;
            // the shorthand resets font-variant (font-variant-numeric included) to normal; letter-spacing it leaves alone
            t.TabularNums = false;
            // keep the line height as written: px, a ratio, or normal
            List<string> toks = CssLex.SplitSpaces(v);
            string lh = null;
            for (int i = 0; i < toks.Count; i++)
            {
                int s = toks[i].IndexOf('/');
                if (s >= 0) { lh = toks[i].Substring(s + 1); if (lh.Length == 0 && i + 1 < toks.Count) lh = toks[i + 1]; break; }
                if (toks[i] == "/" && i + 1 < toks.Count) { lh = toks[i + 1]; break; }
            }
            t.LineHeightPx = null;
            t.LineHeightRatio = null;
            t.LineHeightNormal = null;
            if (lh == null) t.LineHeightNormal = true;
            else if (!TryLineHeight(lh, t)) return false;
            return true;
        }

        private static bool TryLineHeight(string value, TextStyleData t)
        {
            string v = value.Trim().ToLowerInvariant();
            if (v == "inherit") return true;
            t.LineHeightPx = null;
            t.LineHeightRatio = null;
            t.LineHeightNormal = null;
            if (v == "normal") { t.LineHeightNormal = true; return true; }
            if (!CssLex.TryNumber(v, out float n, out string unit)) return false;
            switch (unit)
            {
                case "": t.LineHeightRatio = n; return true;
                case "px": t.LineHeightPx = n; return true;
                case "%": t.LineHeightRatio = n / 100f; return true;
                case "em": t.LineHeightRatio = n; return true;
                default: return false;
            }
        }

        private static bool TryLetterSpacing(string value, TextStyleData t)
        {
            string v = value.Trim().ToLowerInvariant();
            t.LetterSpacingEm = null;
            t.LetterSpacingPx = null;
            if (v == "normal") { t.LetterSpacingPx = 0f; return true; }
            if (!CssLex.TryNumber(v, out float n, out string unit)) return false;
            switch (unit)
            {
                case "em": t.LetterSpacingEm = n; return true;
                case "px": t.LetterSpacingPx = n; return true;
                case "": if (n == 0f) { t.LetterSpacingPx = 0f; return true; } return false;
                default: return false;
            }
        }

        private static bool TryTextAlign(string v, out TextAlign a)
        {
            switch (v.Trim())
            {
                case "left": a = TextAlign.Left; return true;
                case "right": a = TextAlign.Right; return true;
                case "center": a = TextAlign.Center; return true;
                case "start": a = TextAlign.Start; return true;
                case "end": a = TextAlign.End; return true;
                case "justify": a = TextAlign.Justify; return true;
                default: a = TextAlign.Start; return false;
            }
        }

        private static bool TryWhiteSpace(string v, out WhiteSpace w)
        {
            switch (v.Trim())
            {
                case "normal": w = WhiteSpace.Normal; return true;
                case "nowrap": w = WhiteSpace.NoWrap; return true;
                case "pre": w = WhiteSpace.Pre; return true;
                case "pre-line": w = WhiteSpace.PreLine; return true;
                case "pre-wrap": w = WhiteSpace.PreWrap; return true;
                default: w = WhiteSpace.Normal; return false;
            }
        }

        private static bool TryDecorationLine(string v, out DecorationLine line)
        {
            switch (v.Trim())
            {
                case "none": line = DecorationLine.None; return true;
                case "underline": line = DecorationLine.Underline; return true;
                case "line-through": line = DecorationLine.LineThrough; return true;
                case "overline": line = DecorationLine.Overline; return true;
                default: line = DecorationLine.None; return false;
            }
        }

        /// <summary>"underline dotted var(--ink-muted) 2px" / "line-through 3px var(--miss)" / "none".</summary>
        private static bool TryTextDecoration(Ctx ctx, string value, TextStyleData t)
        {
            t.Decoration = DecorationLine.None;
            t.DecorationStyle = BorderStyle.Solid;
            t.DecorationColor = null;
            t.DecorationThickness = null;
            foreach (string tok in CssLex.SplitSpaces(value))
            {
                string k = tok.ToLowerInvariant();
                if (TryDecorationLine(k, out DecorationLine l)) { t.Decoration = l; continue; }
                if (k == "wavy") { t.DecorationStyle = BorderStyle.Solid; continue; }
                if (k != "none" && TryBorderStyleText(k, out BorderStyle s)) { t.DecorationStyle = s; continue; }
                if (k == "auto" || k == "from-font") continue;
                if (CssLex.TryPx(k, out float th)) { t.DecorationThickness = th; continue; }
                if (IsColor(ctx, tok, out Rgba c)) { t.DecorationColor = c; continue; }
                return false;
            }
            return true;
        }

        // ---- layout -------------------------------------------------------------------------------------------

        private static bool TryPosition(string v, out Position p)
        {
            switch (v.Trim())
            {
                case "static": p = Position.Static; return true;
                case "relative": p = Position.Relative; return true;
                case "absolute": p = Position.Absolute; return true;
                case "fixed": p = Position.Fixed; return true;
                case "sticky": p = Position.Relative; return true;
                default: p = Position.Static; return false;
            }
        }

        private static bool TrySize(string v, out CssLength l)
        {
            string t = v.Trim().ToLowerInvariant();
            if (t == "none" || t == "max-content" || t == "min-content" || t == "fit-content") { l = CssLength.Auto; return true; }
            return CssLength.TryParse(t, out l);
        }

        /// <summary>flex: none | auto | g | g s | g s basis | basis.</summary>
        private static bool TryFlex(string value, LayoutStyle l)
        {
            List<string> t = CssLex.SplitSpaces(value.Trim().ToLowerInvariant());
            if (t.Count == 1 && t[0] == "none") { l.Grow = 0f; l.Shrink = 0f; l.Basis = CssLength.Auto; return true; }
            if (t.Count == 1 && t[0] == "auto") { l.Grow = 1f; l.Shrink = 1f; l.Basis = CssLength.Auto; return true; }
            float? grow = null, shrink = null;
            CssLength? basis = null;
            foreach (string tok in t)
            {
                if (CssLex.TryNumber(tok, out float n, out string unit) && unit.Length == 0 && (grow == null || shrink == null) && basis == null)
                {
                    if (grow == null) grow = n;
                    else shrink = n;
                    continue;
                }
                if (TrySize(tok, out CssLength b)) { basis = b; continue; }
                return false;
            }
            l.Grow = grow ?? 1f;
            l.Shrink = shrink ?? 1f;
            l.Basis = basis ?? (grow.HasValue ? CssLength.FromPercent(0f) : CssLength.Auto);
            if (!grow.HasValue && basis.HasValue) l.Grow = 1f;
            return true;
        }

        private static bool TryDirection(string v, LayoutStyle l)
        {
            switch (v.Trim())
            {
                case "row": l.Column = false; l.Reverse = false; return true;
                case "row-reverse": l.Column = false; l.Reverse = true; return true;
                case "column": l.Column = true; l.Reverse = false; return true;
                case "column-reverse": l.Column = true; l.Reverse = true; return true;
                default: return false;
            }
        }

        private static bool TryGap(string v, LayoutStyle l)
        {
            List<string> t = CssLex.SplitSpaces(v);
            if (t.Count < 1 || t.Count > 2) return false;
            if (!CssLex.TryPx(t[0], out float row)) return false;
            float column = row;
            if (t.Count == 2 && !CssLex.TryPx(t[1], out column)) return false;
            l.RowGap = row;
            l.ColumnGap = column;
            return true;
        }

        private static Align AlignOf(string v) => TryAlign(v, out Align a) ? a : Align.Stretch;

        /// <summary>align-items / align-self / justify-items / justify-self. `normal` behaves as stretch for flex and grid items.</summary>
        private static bool TryAlign(string v, out Align a)
        {
            switch (v.Trim())
            {
                case "normal": a = Align.Stretch; return true;
                case "flex-start":
                case "start":
                case "self-start":
                case "left": a = Align.Start; return true;
                case "center": a = Align.Center; return true;
                case "flex-end":
                case "end":
                case "self-end":
                case "right": a = Align.End; return true;
                case "stretch": a = Align.Stretch; return true;
                case "baseline":
                case "first baseline": a = Align.Baseline; return true;
                default: a = Align.Start; return false;
            }
        }

        private static bool TryJustify(string v, out Justify j)
        {
            switch (v.Trim())
            {
                case "flex-start":
                case "start":
                case "normal":
                case "left": j = Justify.Start; return true;
                case "center": j = Justify.Center; return true;
                case "flex-end":
                case "end":
                case "right": j = Justify.End; return true;
                case "space-between": j = Justify.SpaceBetween; return true;
                case "space-around": j = Justify.SpaceAround; return true;
                case "space-evenly": j = Justify.SpaceEvenly; return true;
                default: j = Justify.Start; return false;
            }
        }

        private static bool TryAlignContent(string v, out AlignContent a)
        {
            switch (v.Trim())
            {
                case "flex-start":
                case "start": a = AlignContent.Start; return true;
                case "center": a = AlignContent.Center; return true;
                case "flex-end":
                case "end": a = AlignContent.End; return true;
                case "space-between": a = AlignContent.SpaceBetween; return true;
                case "space-around": a = AlignContent.SpaceAround; return true;
                case "stretch":
                case "normal": a = AlignContent.Stretch; return true;
                default: a = AlignContent.Stretch; return false;
            }
        }

        private static bool TryTracks(string v, out List<Track> tracks)
        {
            try
            {
                tracks = Grid.ParseTracks(v);
                return true;
            }
            catch (FormatException)
            {
                tracks = null;
                return false;
            }
        }

        /// <summary>grid-column / grid-row: "2", "1 / -1", "2 / 3", "span 2", "3 / span 2".</summary>
        private static bool TryGridLine(string v, out int start, out int end, out int span)
        {
            start = 0;
            end = 0;
            span = 1;
            string t = v.Trim();
            int slash = t.IndexOf('/');
            string a = slash >= 0 ? t.Substring(0, slash).Trim() : t;
            string b = slash >= 0 ? t.Substring(slash + 1).Trim() : null;
            if (!Line(a, out start, out int spanA)) return false;
            if (spanA > 0) span = spanA;
            if (b != null)
            {
                if (!Line(b, out end, out int spanB)) return false;
                if (spanB > 0) span = spanB;
            }
            return true;

            bool Line(string s, out int line, out int sp)
            {
                line = 0;
                sp = 0;
                if (s == "auto") return true;
                if (s.StartsWith("span", StringComparison.Ordinal))
                    return int.TryParse(s.Substring(4).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out sp);
                return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out line);
            }
        }

        private static bool TryOrigin(string v, out CssLength x, out CssLength y)
        {
            List<string> t = CssLex.SplitSpaces(v.Trim().ToLowerInvariant());
            if (t.Count == 3) t.RemoveAt(2); // a z offset
            return CssPosition.TryParse(t, out x, out y);
        }

        private static bool TryOpacity(string v, out float o)
        {
            o = 1f;
            if (!CssLex.TryNumber(v.Trim(), out float n, out string unit)) return false;
            if (unit == "%") n /= 100f;
            else if (unit.Length != 0) return false;
            o = Math.Max(0f, Math.Min(1f, n));
            return true;
        }

        // ---- serialising (round trips, logs) -----------------------------------------------------------------

        /// <summary>The paint of a BoxStyle as CSS declarations (colours as rgba(), lengths in px).</summary>
        public static string ToCss(BoxStyle s)
        {
            var sb = new StringBuilder();
            var layers = new List<string>();
            foreach (BackgroundLayer l in s.Backgrounds) layers.Add(l.Image?.ToCss() ?? "none");
            if (layers.Count > 0) sb.Append("background-image: ").Append(string.Join(", ", layers)).Append("; ");
            if (s.BackgroundColor.A > 0f) sb.Append("background-color: ").Append(CssColor.ToCss(s.BackgroundColor)).Append("; ");
            AppendSide(sb, "border-top", s.Top);
            AppendSide(sb, "border-right", s.Right);
            AppendSide(sb, "border-bottom", s.Bottom);
            AppendSide(sb, "border-left", s.Left);
            if (!s.Radius.IsZero)
                sb.Append("border-radius: ").Append(s.Radius.TLx.ToCss()).Append(' ').Append(s.Radius.TRx.ToCss()).Append(' ')
                  .Append(s.Radius.BRx.ToCss()).Append(' ').Append(s.Radius.BLx.ToCss()).Append(" / ").Append(s.Radius.TLy.ToCss()).Append(' ')
                  .Append(s.Radius.TRy.ToCss()).Append(' ').Append(s.Radius.BRy.ToCss()).Append(' ').Append(s.Radius.BLy.ToCss()).Append("; ");
            if (s.Shadows.Count > 0) sb.Append("box-shadow: ").Append(BoxShadow.ToCss(s.Shadows)).Append("; ");
            if (s.Outline.Style != BorderStyle.None)
                sb.Append("outline: ").Append(new BorderSide(s.Outline.Width, s.Outline.Style, s.Outline.Color)).Append("; outline-offset: ")
                  .Append(s.Outline.Offset.ToString("0.###", CultureInfo.InvariantCulture)).Append("px; ");
            if (!s.Padding.Equals(Insets.Zero))
                sb.Append(string.Format(CultureInfo.InvariantCulture, "padding: {0}px {1}px {2}px {3}px; ", s.Padding.Top, s.Padding.Right, s.Padding.Bottom, s.Padding.Left));
            return sb.ToString().TrimEnd();
        }

        private static void AppendSide(StringBuilder sb, string name, BorderSide side)
        {
            if (side.Style == BorderStyle.None && side.Width == 0f) return;
            sb.Append(name).Append(": ").Append(side).Append("; ");
        }
    }

    // ---- lexing ------------------------------------------------------------------------------------------------

    internal static class CssLex
    {
        /// <summary>"a: b; c: d" → (property lower-cased, value) pairs; comments and !important dropped.</summary>
        public static List<(string prop, string value)> Declarations(string block)
        {
            var list = new List<(string, string)>();
            string s = StripComments(block ?? "");
            foreach (string decl in SplitTopLevel(s, ';'))
            {
                string d = decl.Trim();
                if (d.Length == 0) continue;
                int colon = d.IndexOf(':');
                if (colon <= 0) continue;
                string prop = d.Substring(0, colon).Trim();
                if (!prop.StartsWith("--", StringComparison.Ordinal)) prop = prop.ToLowerInvariant();
                string value = d.Substring(colon + 1).Trim();
                int imp = value.LastIndexOf("!important", StringComparison.OrdinalIgnoreCase);
                if (imp >= 0) value = value.Substring(0, imp).Trim();
                list.Add((prop, value));
            }
            return list;
        }

        public static string StripComments(string s)
        {
            int start = s.IndexOf("/*", StringComparison.Ordinal);
            if (start < 0) return s;
            var sb = new StringBuilder();
            int i = 0;
            while (start >= 0)
            {
                sb.Append(s, i, start - i);
                int end = s.IndexOf("*/", start + 2, StringComparison.Ordinal);
                if (end < 0) return sb.ToString();
                i = end + 2;
                start = s.IndexOf("/*", i, StringComparison.Ordinal);
            }
            sb.Append(s, i, s.Length - i);
            return sb.ToString();
        }

        /// <summary>Splits at sep outside parentheses and quotes; the parts are trimmed (empty parts kept only between separators).</summary>
        public static List<string> SplitTopLevel(string s, char sep)
        {
            var list = new List<string>();
            int depth = 0, start = 0;
            char quote = '\0';
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
                if (c == '"' || c == '\'') { quote = c; continue; }
                if (c == '(') depth++;
                else if (c == ')') depth = Math.Max(0, depth - 1);
                else if (c == sep && depth == 0)
                {
                    list.Add(s.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }
            list.Add(s.Substring(start).Trim());
            return list;
        }

        public static int IndexOfTopLevel(string s, char c)
        {
            int depth = 0;
            char quote = '\0';
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (quote != '\0') { if (ch == quote) quote = '\0'; continue; }
                if (ch == '"' || ch == '\'') { quote = ch; continue; }
                if (ch == '(') depth++;
                else if (ch == ')') depth = Math.Max(0, depth - 1);
                else if (ch == c && depth == 0) return i;
            }
            return -1;
        }

        /// <summary>Splits at whitespace outside parentheses and quotes; no empty tokens.</summary>
        public static List<string> SplitSpaces(string s)
        {
            var list = new List<string>();
            int depth = 0, start = -1;
            char quote = '\0';
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (quote != '\0')
                {
                    if (c == quote) quote = '\0';
                    continue;
                }
                if (c == '"' || c == '\'') { quote = c; if (start < 0) start = i; continue; }
                if (c == '(') { depth++; if (start < 0) start = i; continue; }
                if (c == ')') { depth = Math.Max(0, depth - 1); continue; }
                if (char.IsWhiteSpace(c) && depth == 0)
                {
                    if (start >= 0) { list.Add(s.Substring(start, i - start)); start = -1; }
                    continue;
                }
                if (start < 0) start = i;
            }
            if (start >= 0) list.Add(s.Substring(start));
            return list;
        }

        /// <summary>A whole token "number[unit]": sign, digits, a dot, an exponent; the unit lower-cased ("" / "%" / "px" / "deg" ...).</summary>
        public static bool TryNumber(string s, out float value, out string unit)
        {
            value = 0f;
            unit = "";
            if (string.IsNullOrEmpty(s)) return false;
            int i = 0, n = s.Length;
            if (s[i] == '+' || s[i] == '-') i++;
            bool digits = false;
            while (i < n && char.IsDigit(s[i])) { i++; digits = true; }
            if (i < n && s[i] == '.')
            {
                i++;
                while (i < n && char.IsDigit(s[i])) { i++; digits = true; }
            }
            if (!digits) return false;
            if (i < n && (s[i] == 'e' || s[i] == 'E'))
            {
                int j = i + 1;
                if (j < n && (s[j] == '+' || s[j] == '-')) j++;
                if (j < n && char.IsDigit(s[j]))
                {
                    i = j;
                    while (i < n && char.IsDigit(s[i])) i++;
                }
            }
            if (!float.TryParse(s.Substring(0, i), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
            unit = s.Substring(i).ToLowerInvariant();
            for (int k = 0; k < unit.Length; k++)
                if (!(char.IsLetter(unit[k]) || unit[k] == '%')) return false;
            return true;
        }

        /// <summary>A px length or 0.</summary>
        public static bool TryPx(string s, out float px)
        {
            px = 0f;
            if (!TryNumber(s.Trim(), out float v, out string unit)) return false;
            if (unit == "px" || (unit.Length == 0 && v == 0f)) { px = v; return true; }
            if (unit == "rem") { px = v * 16f; return true; }
            return false;
        }

        /// <summary>A border / outline width: px, 0, thin (1), medium (3), thick (5).</summary>
        public static bool TryWidthPx(string s, out float px)
        {
            switch (s.Trim().ToLowerInvariant())
            {
                case "thin": px = 1f; return true;
                case "medium": px = 3f; return true;
                case "thick": px = 5f; return true;
            }
            return TryPx(s, out px);
        }

        /// <summary>SVG lengths: a number or px.</summary>
        public static bool TryPxOrNumber(string s, out float v)
        {
            if (!TryNumber(s.Trim(), out v, out string unit)) return false;
            return unit.Length == 0 || unit == "px";
        }

        public static bool TryTimeMs(string s, out float ms)
        {
            ms = 0f;
            if (!TryNumber(s.Trim(), out float v, out string unit)) return false;
            if (unit == "ms") { ms = v; return true; }
            if (unit == "s") { ms = v * 1000f; return true; }
            return false;
        }

        public static bool TryAngleDeg(string s, out float deg)
        {
            deg = 0f;
            if (!TryNumber(s.Trim(), out float v, out string unit)) return false;
            switch (unit)
            {
                case "deg": deg = v; return true;
                case "rad": deg = (float)(v * 180.0 / Math.PI); return true;
                case "turn": deg = v * 360f; return true;
                case "grad": deg = v * 0.9f; return true;
                case "": if (v == 0f) { deg = 0f; return true; } return false;
                default: return false;
            }
        }

        /// <summary>"name(args)" as one whole token.</summary>
        public static bool TryFunction(string token, out string name, out string args)
        {
            name = args = null;
            string t = token.Trim();
            int open = t.IndexOf('(');
            if (open <= 0 || !t.EndsWith(")", StringComparison.Ordinal)) return false;
            // the first '(' must close at the very end
            int depth = 0;
            for (int i = open; i < t.Length; i++)
            {
                if (t[i] == '(') depth++;
                else if (t[i] == ')')
                {
                    depth--;
                    if (depth == 0 && i != t.Length - 1) return false;
                }
            }
            name = t.Substring(0, open).Trim();
            args = t.Substring(open + 1, t.Length - open - 2);
            for (int i = 0; i < name.Length; i++)
                if (!(char.IsLetterOrDigit(name[i]) || name[i] == '-' || name[i] == '_')) return false;
            return true;
        }

        /// <summary>
        /// Replaces every var(--x[, fallback]) (and env(name, fallback)) with its value: the block's own custom
        /// properties, then vars, then DsTokens for the theme, then the fallback. False when one has no value.
        /// </summary>
        public static bool TrySubstitute(string s, CssVars vars, DsTheme theme, IReadOnlyDictionary<string, string> local, out string result)
            => TrySubstitute(s, vars, theme, local, 0, out result);

        private static bool TrySubstitute(string s, CssVars vars, DsTheme theme, IReadOnlyDictionary<string, string> local, int depth, out string result)
        {
            result = s;
            if (depth > 24) return false;
            var sb = new StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                int v = IndexOfFunction(s, "var(", i);
                int e = IndexOfFunction(s, "env(", i);
                int at = v < 0 ? e : e < 0 ? v : Math.Min(v, e);
                if (at < 0) { sb.Append(s, i, s.Length - i); break; }
                bool isEnv = at == e;
                sb.Append(s, i, at - i);
                int open = at + 3;
                int close = MatchParen(s, open);
                if (close < 0) return false;
                string inner = s.Substring(open + 1, close - open - 1);
                int comma = IndexOfTopLevel(inner, ',');
                string name = (comma >= 0 ? inner.Substring(0, comma) : inner).Trim();
                string fallback = comma >= 0 ? inner.Substring(comma + 1).Trim() : null;
                string value = null;
                if (!isEnv)
                {
                    if (local != null && local.TryGetValue(name, out string lv)) value = lv;
                    else if (vars != null && vars.TryGet(name, out string vv)) value = vv;
                    else if (DsTokens.TryResolve(name, theme, out string tv)) value = tv;
                }
                if (value == null) value = fallback ?? (isEnv ? "0px" : null);
                if (value == null) return false;
                if (!TrySubstitute(value, vars, theme, local, depth + 1, out string resolved)) return false;
                sb.Append(resolved);
                i = close + 1;
            }
            result = sb.ToString();
            return true;
        }

        private static int IndexOfFunction(string s, string fn, int from)
        {
            int i = s.IndexOf(fn, from, StringComparison.Ordinal);
            while (i > 0 && (char.IsLetterOrDigit(s[i - 1]) || s[i - 1] == '-'))
                i = s.IndexOf(fn, i + 1, StringComparison.Ordinal);
            return i;
        }

        private static int MatchParen(string s, int open)
        {
            int depth = 0;
            for (int i = open; i < s.Length; i++)
            {
                if (s[i] == '(') depth++;
                else if (s[i] == ')' && --depth == 0) return i;
            }
            return -1;
        }
    }
}
