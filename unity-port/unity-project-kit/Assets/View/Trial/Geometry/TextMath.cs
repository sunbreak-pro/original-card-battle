// Text maths between CSS and TextMeshPro (kit.md §3.6, §4.9).
//
// Fonts: Display = Shippori Mincho (400/700/800), Body = Zen Kaku Gothic New (400/700), Mono = IBM Plex Mono
// (400/600/700). Their vertical metrics (hhea = OS/2 win, read from the TTFs): Shippori and Zen Kaku ascent 1.160 em,
// descent 0.288 em (content area 1.448 em); Plex Mono 1.025 / 0.275 (1.300 em); line gap 0.
//
// - letter-spacing: TMP characterSpacing is in 1/100 em → em × 100 (added after every glyph, like Chrome).
// - line-height: TMP pitch = (ascent + descent) × size + lineSpacing × size / 100 → lineSpacing = (L/size − content) × 100.
// - baseline: CSS puts the first baseline at top + (L − content·size)/2 + ascent·size (half-leading); TMP at
//   top + ascent·size: shift the label by the half-leading (negative for tight line heights).
// - vertical-rl punctuation: 、。 sit in the upper right of their cell; ー and the brackets turn 90° clockwise.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Trial.Geometry
{
    public enum Family { Display, Body, Mono }

    /// <summary>A resolved font: family, weight, size and line height in px.</summary>
    public readonly struct FontSpec : IEquatable<FontSpec>
    {
        public readonly Family Family;
        public readonly int Weight;
        public readonly float Size, LineHeightPx;

        public FontSpec(Family family, int weight, float size, float lineHeightPx)
        {
            Family = family; Weight = weight; Size = size; LineHeightPx = lineHeightPx;
        }

        public FontMetrics Metrics => TextMath.Metrics(Family, Weight);

        public bool Equals(FontSpec o) => Family == o.Family && Weight == o.Weight && Math.Abs(Size - o.Size) < 1e-4f && Math.Abs(LineHeightPx - o.LineHeightPx) < 1e-4f;
        public override bool Equals(object obj) => obj is FontSpec f && Equals(f);
        public override int GetHashCode() => (int)Family * 31 ^ Weight * 7 ^ Size.GetHashCode() ^ LineHeightPx.GetHashCode() * 3;

        public string ToCss() => string.Format(CultureInfo.InvariantCulture, "{0} {1}px/{2}px var(--font-{3})", Weight,
            Size.ToString("0.###", CultureInfo.InvariantCulture), LineHeightPx.ToString("0.###", CultureInfo.InvariantCulture),
            Family.ToString().ToLowerInvariant());

        public override string ToString() => ToCss();
    }

    /// <summary>Vertical metrics in em (descent positive).</summary>
    public readonly struct FontMetrics
    {
        public readonly float AscentEm, DescentEm, LineGapEm, UnderlinePosEm, UnderlineThicknessEm;

        public FontMetrics(float ascent, float descent, float lineGap, float underlinePos, float underlineThickness)
        {
            AscentEm = ascent; DescentEm = descent; LineGapEm = lineGap; UnderlinePosEm = underlinePos; UnderlineThicknessEm = underlineThickness;
        }

        public float ContentEm => AscentEm + DescentEm;
    }

    /// <summary>How a character of vertical-rl text is placed: offset (em, y down) and a clockwise rotation.</summary>
    public readonly struct VerticalGlyph
    {
        public readonly Vec2 OffsetEm;
        public readonly float RotateDeg;

        public VerticalGlyph(Vec2 offsetEm, float rotateDeg) { OffsetEm = offsetEm; RotateDeg = rotateDeg; }

        public static readonly VerticalGlyph Upright = new VerticalGlyph(Vec2.Zero, 0f);
    }

    public static class TextMath
    {
        // §3.6 (unitsPerEm 1000), checked against the TTFs in RPG-by-card/Assets/View/Resources/Fonts: hhea = OS/2 win
        // for every face (Plex Mono sets USE_TYPO_METRICS, and its typo metrics equal hhea too). Every Shippori weight
        // and both Zen Kaku weights share their family's values; Plex Mono's underline differs per weight
        // (Regular −140/60, SemiBold −112/88, Bold −100/100).
        private static readonly FontMetrics Shippori = new FontMetrics(1.160f, 0.288f, 0f, -0.075f, 0.050f);
        private static readonly FontMetrics ZenKaku = new FontMetrics(1.160f, 0.288f, 0f, -0.133f, 0.020f);
        private static readonly FontMetrics PlexRegular = new FontMetrics(1.025f, 0.275f, 0f, -0.140f, 0.060f);
        private static readonly FontMetrics PlexSemiBold = new FontMetrics(1.025f, 0.275f, 0f, -0.112f, 0.088f);
        private static readonly FontMetrics PlexBold = new FontMetrics(1.025f, 0.275f, 0f, -0.100f, 0.100f);

        /// <summary>The metrics of the face CSS font matching picks for the weight (Plex Mono has 400 / 600 / 700).</summary>
        public static FontMetrics Metrics(Family family, int weight)
        {
            switch (family)
            {
                case Family.Display: return Shippori;
                case Family.Mono: return weight <= 500 ? PlexRegular : weight <= 600 ? PlexSemiBold : PlexBold;
                default: return ZenKaku;
            }
        }

        /// <summary>
        /// A CSS font shorthand: "[italic] [weight] size[/line-height] family", e.g. "700 22px/28px var(--font-display)",
        /// "400 18px/1.4 var(--font-body)", "600 20px/1 var(--font-mono)". A unitless line height multiplies the size;
        /// "normal" is the font's content area (+ line gap). Throws FormatException when it is not a font shorthand.
        /// </summary>
        public static FontSpec ParseFont(string css, DsTheme theme = DsTheme.Dusk, CssVars vars = null)
        {
            if (TryParseFont(css, out FontSpec spec, out _, theme, vars)) return spec;
            throw new FormatException("Not a CSS font shorthand: '" + css + "'");
        }

        /// <summary>As ParseFont; italic reports font-style italic / oblique.</summary>
        public static bool TryParseFont(string css, out FontSpec spec, out bool italic, DsTheme theme = DsTheme.Dusk, CssVars vars = null)
        {
            spec = default;
            italic = false;
            if (string.IsNullOrWhiteSpace(css)) return false;
            string s = css.Trim();
            if (s.IndexOf("var(", StringComparison.Ordinal) >= 0 && !CssLex.TrySubstitute(s, vars, theme, null, out s)) return false;
            List<string> tokens = CssLex.SplitSpaces(s);
            int weight = 400;
            int i = 0;
            for (; i < tokens.Count; i++)
            {
                string t = tokens[i].ToLowerInvariant();
                if (t == "italic" || t == "oblique") { italic = true; continue; }
                if (t == "normal" || t == "small-caps") continue;
                if (TryWeight(t, out int w) && !t.EndsWith("px", StringComparison.Ordinal)) { weight = w; continue; }
                break;
            }
            if (i >= tokens.Count) return false;
            string sizePart = tokens[i];
            string lhPart = null;
            int slash = sizePart.IndexOf('/');
            if (slash >= 0)
            {
                lhPart = sizePart.Substring(slash + 1);
                sizePart = sizePart.Substring(0, slash);
                if (lhPart.Length == 0 && i + 1 < tokens.Count) lhPart = tokens[++i];
            }
            else if (i + 1 < tokens.Count && tokens[i + 1].StartsWith("/", StringComparison.Ordinal))
            {
                lhPart = tokens[++i].Substring(1);
                if (lhPart.Length == 0 && i + 1 < tokens.Count) lhPart = tokens[++i];
            }
            if (!CssLex.TryNumber(sizePart, out float size, out string unit) || (unit != "px" && !(unit.Length == 0 && size == 0f))) return false;
            var familyText = new StringBuilder();
            for (int k = i + 1; k < tokens.Count; k++)
            {
                if (familyText.Length > 0) familyText.Append(' ');
                familyText.Append(tokens[k]);
            }
            if (familyText.Length == 0) return false;
            Family family = FamilyOf(familyText.ToString());
            float lh = NormalLineHeight(family, weight, size);
            if (lhPart != null && !TryLineHeight(lhPart, size, family, weight, out lh)) return false;
            spec = new FontSpec(family, weight, size, lh);
            return true;
        }

        /// <summary>A line-height value against a font size: px, a ratio, a percentage or "normal".</summary>
        public static bool TryLineHeight(string text, float size, Family family, int weight, out float px)
        {
            px = 0f;
            string t = text.Trim().ToLowerInvariant();
            if (t == "normal") { px = NormalLineHeight(family, weight, size); return true; }
            if (!CssLex.TryNumber(t, out float v, out string unit)) return false;
            switch (unit)
            {
                case "": px = v * size; return true;
                case "px": px = v; return true;
                case "%": px = v * size / 100f; return true;
                case "em": px = v * size; return true;
                default: return false;
            }
        }

        /// <summary>The family of a font-family list: its first name (Shippori / serif → Display, Plex / monospace → Mono, else Body).</summary>
        public static Family FamilyOf(string fontFamily)
        {
            string first = CssLex.SplitTopLevel(fontFamily ?? "", ',')[0].Trim().Trim('"', '\'').ToLowerInvariant();
            if (first.Contains("shippori") || first.Contains("mincho") || first == "serif") return Family.Display;
            if (first.Contains("plex mono") || first.Contains("monospace") || first == "ui-monospace" || first == "menlo") return Family.Mono;
            return Family.Body;
        }

        /// <summary>font-weight: normal 400, bold 700, or a number 1..1000.</summary>
        public static bool TryWeight(string text, out int weight)
        {
            weight = 400;
            string t = text.Trim().ToLowerInvariant();
            if (t == "normal") return true;
            if (t == "bold") { weight = 700; return true; }
            if (t == "bolder") { weight = 700; return true; }
            if (t == "lighter") { weight = 300; return true; }
            if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) && w >= 1 && w <= 1000)
            {
                weight = w;
                return true;
            }
            return false;
        }

        /// <summary>CSS line-height: normal = (ascent + descent + line gap) × size.</summary>
        public static float NormalLineHeight(Family family, int weight, float size)
        {
            FontMetrics m = Metrics(family, weight);
            return (m.AscentEm + m.DescentEm + m.LineGapEm) * size;
        }

        /// <summary>TMP characterSpacing for a CSS letter-spacing in em (1/100 em units).</summary>
        public static float TmpCharacterSpacing(float em) => em * 100f;

        /// <summary>TMP characterSpacing for a CSS letter-spacing in px at a font size.</summary>
        public static float TmpCharacterSpacingPx(float px, float sizePx) => sizePx > 0f ? px / sizePx * 100f : 0f;

        /// <summary>TMP lineSpacing for a CSS line pitch: (pitch / size − (ascent + descent + gap)) × 100. Descent positive or negative.</summary>
        public static float TmpLineSpacing(float pitchPx, float sizePx, float ascentEm, float descentEm, float lineGapEm = 0f)
        {
            if (sizePx <= 0f) return 0f;
            return (pitchPx / sizePx - (ascentEm + Math.Abs(descentEm) + lineGapEm)) * 100f;
        }

        public static float TmpLineSpacing(FontSpec f)
        {
            FontMetrics m = f.Metrics;
            return TmpLineSpacing(f.LineHeightPx, f.Size, m.AscentEm, m.DescentEm, m.LineGapEm);
        }

        /// <summary>CSS first baseline below the line box top: (L − content)/2 + ascent (half-leading model).</summary>
        public static float CssBaselineFromTop(float lineHeightPx, float sizePx, float ascentEm, float descentEm)
        {
            float content = (ascentEm + Math.Abs(descentEm)) * sizePx;
            return (lineHeightPx - content) * 0.5f + ascentEm * sizePx;
        }

        public static float CssBaselineFromTop(FontSpec f)
        {
            FontMetrics m = f.Metrics;
            return CssBaselineFromTop(f.LineHeightPx, f.Size, m.AscentEm, m.DescentEm);
        }

        /// <summary>The half-leading (L − content)/2: how far a TMP label (baseline at top + ascent) moves down to sit as CSS does.</summary>
        public static float HalfLeading(float lineHeightPx, float sizePx, float ascentEm, float descentEm) =>
            (lineHeightPx - (ascentEm + Math.Abs(descentEm)) * sizePx) * 0.5f;

        public static float HalfLeading(FontSpec f)
        {
            FontMetrics m = f.Metrics;
            return HalfLeading(f.LineHeightPx, f.Size, m.AscentEm, m.DescentEm);
        }

        /// <summary>The underline's top below the baseline: |underline position| × size + text-underline-offset (kit.md §4.9).</summary>
        public static float UnderlineBelowBaseline(FontSpec f, float offsetPx) => Math.Abs(f.Metrics.UnderlinePosEm) * f.Size + offsetPx;

        // The fonts' `vert` substitutes, measured in the TTFs (glyph bounding boxes, unitsPerEm 1000): a vertical form
        // of 、。，． and the small kana is the horizontal glyph moved right and up (same outline); the long marks and
        // brackets are the horizontal glyph turned 90° clockwise about the centre of the ideographic em box (within
        // 0.01 em; Zen Kaku's … also moves right, its horizontal dots sit on the baseline). Offsets in em, y down.
        private static readonly Dictionary<char, Vec2> PunctShipporiRegular = new Dictionary<char, Vec2>
        {
            ['、'] = new Vec2(0.648f, -0.661f), ['。'] = new Vec2(0.615f, -0.620f), ['，'] = new Vec2(0.750f, -0.718f), ['．'] = new Vec2(0.751f, -0.718f),
        };
        private static readonly Dictionary<char, Vec2> PunctShipporiBold = new Dictionary<char, Vec2>
        {
            ['、'] = new Vec2(0.655f, -0.690f), ['。'] = new Vec2(0.586f, -0.606f), ['，'] = new Vec2(0.746f, -0.688f), ['．'] = new Vec2(0.723f, -0.687f),
        };
        private static readonly Dictionary<char, Vec2> PunctShipporiExtraBold = new Dictionary<char, Vec2>
        {
            ['、'] = new Vec2(0.657f, -0.699f), ['。'] = new Vec2(0.576f, -0.601f), ['，'] = new Vec2(0.745f, -0.678f), ['．'] = new Vec2(0.713f, -0.677f),
        };
        private static readonly Dictionary<char, Vec2> PunctZenKakuRegular = new Dictionary<char, Vec2>
        {
            ['、'] = new Vec2(0.615f, -0.622f), ['。'] = new Vec2(0.593f, -0.600f), ['，'] = new Vec2(0.691f, -0.647f), ['．'] = new Vec2(0.690f, -0.694f),
        };
        private static readonly Dictionary<char, Vec2> PunctZenKakuBold = new Dictionary<char, Vec2>
        {
            ['、'] = new Vec2(0.603f, -0.610f), ['。'] = new Vec2(0.588f, -0.589f), ['，'] = new Vec2(0.646f, -0.581f), ['．'] = new Vec2(0.646f, -0.647f),
        };
        // Shippori Mincho Regular (its Bold / ExtraBold differ by 0.03 em at most); Zen Kaku moves every small kana by (0.11, −0.11).
        private static readonly Dictionary<char, Vec2> SmallKanaShippori = new Dictionary<char, Vec2>
        {
            ['ぁ'] = new Vec2(0.105f, -0.101f), ['ぃ'] = new Vec2(0.125f, -0.148f), ['ぅ'] = new Vec2(0.149f, -0.103f), ['ぇ'] = new Vec2(0.118f, -0.108f),
            ['ぉ'] = new Vec2(0.086f, -0.127f), ['っ'] = new Vec2(0.119f, -0.138f), ['ゃ'] = new Vec2(0.111f, -0.094f), ['ゅ'] = new Vec2(0.106f, -0.080f),
            ['ょ'] = new Vec2(0.155f, -0.097f), ['ゎ'] = new Vec2(0.121f, -0.109f), ['ァ'] = new Vec2(0.119f, -0.123f), ['ィ'] = new Vec2(0.135f, -0.134f),
            ['ゥ'] = new Vec2(0.123f, -0.120f), ['ェ'] = new Vec2(0.107f, -0.156f), ['ォ'] = new Vec2(0.142f, -0.136f), ['ッ'] = new Vec2(0.160f, -0.126f),
            ['ャ'] = new Vec2(0.107f, -0.112f), ['ュ'] = new Vec2(0.098f, -0.156f), ['ョ'] = new Vec2(0.156f, -0.154f), ['ヮ'] = new Vec2(0.148f, -0.112f),
            ['ヵ'] = new Vec2(0.125f, -0.084f), ['ヶ'] = new Vec2(0.141f, -0.116f),
        };

        /// <summary>
        /// vertical-rl placement of a character in the Display face at 400, the face of the trial's only vertical
        /// text (the title tagline, meta.css:48).
        /// </summary>
        public static VerticalGlyph Vertical(char c) => Vertical(c, Family.Display, 400);

        /// <summary>
        /// vertical-rl placement of a character as the face's `vert` forms draw it: OffsetEm moves the upright
        /// horizontal glyph (、。 to the upper right); RotateDeg turns it clockwise about the centre of the
        /// ideographic em box (0.5 em right of the pen, 0.38 em above the baseline: OS/2 typo box +0.88 / −0.12 em),
        /// the offset applying after the turn. Mono text falls back to the Body face for Japanese.
        /// </summary>
        public static VerticalGlyph Vertical(char c, Family family, int weight = 400)
        {
            bool display = family == Family.Display;
            Dictionary<char, Vec2> punct = display
                ? (weight <= 500 ? PunctShipporiRegular : weight <= 700 ? PunctShipporiBold : PunctShipporiExtraBold)
                : (weight <= 500 ? PunctZenKakuRegular : PunctZenKakuBold);
            if (punct.TryGetValue(c, out Vec2 off)) return new VerticalGlyph(off, 0f);
            if (display ? SmallKanaShippori.TryGetValue(c, out off) : SmallKanaShippori.ContainsKey(c))
                return new VerticalGlyph(display ? off : new Vec2(0.11f, -0.11f), 0f);
            if (c == '…' && !display) return new VerticalGlyph(weight <= 500 ? new Vec2(0.335f, 0.039f) : new Vec2(0.315f, 0.074f), 90f);
            switch (c)
            {
                case 'ー':
                case '－':
                case '〜':
                case '～':
                case '…':
                case '‥':
                case '―':
                case '—':
                case '「':
                case '」':
                case '『':
                case '』':
                case '（':
                case '）':
                case '(':
                case ')':
                case '【':
                case '】':
                case '〈':
                case '〉':
                case '《':
                case '》':
                case '［':
                case '］':
                case '｛':
                case '｝':
                    return new VerticalGlyph(Vec2.Zero, 90f);
                default:
                    return VerticalGlyph.Upright;
            }
        }

        /// <summary>The px offset (y down) of a vertical-rl character in the Display face: 、。 to the upper right; 0 for upright ones.</summary>
        public static Vec2 VerticalPunctuationOffset(char c, float sizePx) => Vertical(c).OffsetEm * sizePx;

        public static Vec2 VerticalPunctuationOffset(char c, float sizePx, Family family, int weight = 400) =>
            Vertical(c, family, weight).OffsetEm * sizePx;
    }

    // ---- text style data (what Css.Text returns) ---------------------------------------------------------------

    public enum WhiteSpace { Normal, NoWrap, Pre, PreLine, PreWrap }

    public enum TextAlign { Start, Left, Center, Right, End, Justify }

    public enum DecorationLine { None, Underline, LineThrough, Overline }

    /// <summary>One text-shadow layer (σ = blur / 2, measured).</summary>
    public readonly struct TextShadow : IEquatable<TextShadow>
    {
        public readonly float X, Y, Blur;
        public readonly Rgba Color;

        public TextShadow(float x, float y, float blur, Rgba color) { X = x; Y = y; Blur = Math.Max(0f, blur); Color = color; }

        public float Sigma => Blur * 0.5f;

        public static TextShadow Lerp(TextShadow a, TextShadow b, float t) => new TextShadow(
            a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Blur + (b.Blur - a.Blur) * t, GradientPaint.LerpPremulKeep(a.Color, b.Color, t));

        public static readonly TextShadow Zero = new TextShadow(0f, 0f, 0f, Rgba.Transparent);

        public string ToCss() => string.Format(CultureInfo.InvariantCulture, "{0}px {1}px {2}px {3}",
            Math.Round(X, 3), Math.Round(Y, 3), Math.Round(Blur, 3), CssColor.ToCss(Color));

        public bool Equals(TextShadow o) => Math.Abs(X - o.X) < 1e-4f && Math.Abs(Y - o.Y) < 1e-4f && Math.Abs(Blur - o.Blur) < 1e-4f && Color.Approximately(o.Color, 1f / 1024f);
        public override bool Equals(object obj) => obj is TextShadow s && Equals(s);
        public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode() * 3 ^ Blur.GetHashCode() * 7;
        public override string ToString() => ToCss();
    }

    /// <summary>
    /// The text properties of a declaration block. Null = not declared here (Inherit fills the inherited ones from
    /// the parent). Line height is either px or a ratio of the size; letter spacing is em or px.
    /// </summary>
    public sealed class TextStyleData
    {
        public Family? Family;
        public int? Weight;
        public float? Size;
        public float? LineHeightPx;
        public float? LineHeightRatio;
        public bool? LineHeightNormal;
        public float? LetterSpacingEm;
        public float? LetterSpacingPx;
        public Rgba? Color;
        public TextAlign? Align;
        public WhiteSpace? WhiteSpace;
        public bool? Italic;
        public bool? TabularNums;
        /// <summary>text-overflow: ellipsis (not inherited).</summary>
        public bool? Ellipsis;
        /// <summary>-webkit-line-clamp (not inherited); 0 = none.</summary>
        public int? LineClamp;
        /// <summary>text-shadow layers in CSS order; empty = none; null = not declared.</summary>
        public List<TextShadow> Shadows;
        public DecorationLine? Decoration;
        public BorderStyle? DecorationStyle;
        public Rgba? DecorationColor;
        public float? DecorationThickness;
        public float? UnderlineOffset;
        public string WordBreak, LineBreak, OverflowWrap, TextWrap, WritingMode;

        /// <summary>The resolved font (defaults: body 400 16px, line-height normal).</summary>
        public FontSpec Font
        {
            get
            {
                Family fam = Family ?? Trial.Geometry.Family.Body;
                int w = Weight ?? 400;
                float size = Size ?? 16f;
                return new FontSpec(fam, w, size, LineHeightFor(size, fam, w));
            }
        }

        public float LineHeightFor(float size, Family family, int weight)
        {
            if (LineHeightPx.HasValue) return LineHeightPx.Value;
            if (LineHeightRatio.HasValue) return LineHeightRatio.Value * size;
            return TextMath.NormalLineHeight(family, weight, size);
        }

        /// <summary>letter-spacing in em at this style's size (0 when not set).</summary>
        public float LetterSpacingEmResolved
        {
            get
            {
                if (LetterSpacingEm.HasValue) return LetterSpacingEm.Value;
                float size = Size ?? 16f;
                return LetterSpacingPx.HasValue && size > 0f ? LetterSpacingPx.Value / size : 0f;
            }
        }

        /// <summary>This style over its parent: inherited properties not declared here come from the parent.</summary>
        public TextStyleData Inherit(TextStyleData parent)
        {
            var c = (TextStyleData)MemberwiseClone();
            if (parent == null) return c;
            c.Family = Family ?? parent.Family;
            c.Weight = Weight ?? parent.Weight;
            c.Size = Size ?? parent.Size;
            if (!LineHeightPx.HasValue && !LineHeightRatio.HasValue && !LineHeightNormal.HasValue)
            {
                c.LineHeightPx = parent.LineHeightPx;
                c.LineHeightRatio = parent.LineHeightRatio;
                c.LineHeightNormal = parent.LineHeightNormal;
            }
            if (!LetterSpacingEm.HasValue && !LetterSpacingPx.HasValue)
            {
                // the computed value of letter-spacing is a length: an em value inherits as px of the parent's size
                if (parent.LetterSpacingEm.HasValue) c.LetterSpacingPx = parent.LetterSpacingEm.Value * (parent.Size ?? 16f);
                else c.LetterSpacingPx = parent.LetterSpacingPx;
            }
            c.Color = Color ?? parent.Color;
            c.Align = Align ?? parent.Align;
            c.WhiteSpace = WhiteSpace ?? parent.WhiteSpace;
            c.Italic = Italic ?? parent.Italic;
            c.TabularNums = TabularNums ?? parent.TabularNums;
            c.Shadows = Shadows ?? parent.Shadows;
            c.WordBreak = WordBreak ?? parent.WordBreak;
            c.LineBreak = LineBreak ?? parent.LineBreak;
            c.OverflowWrap = OverflowWrap ?? parent.OverflowWrap;
            c.TextWrap = TextWrap ?? parent.TextWrap;
            c.WritingMode = WritingMode ?? parent.WritingMode;
            return c;
        }

        /// <summary>Later declarations win: every property `over` declares replaces this one's.</summary>
        public TextStyleData Merge(TextStyleData over)
        {
            var c = (TextStyleData)MemberwiseClone();
            if (over == null) return c;
            if (over.Family.HasValue) c.Family = over.Family;
            if (over.Weight.HasValue) c.Weight = over.Weight;
            if (over.Size.HasValue) c.Size = over.Size;
            if (over.LineHeightPx.HasValue || over.LineHeightRatio.HasValue || over.LineHeightNormal.HasValue)
            {
                c.LineHeightPx = over.LineHeightPx;
                c.LineHeightRatio = over.LineHeightRatio;
                c.LineHeightNormal = over.LineHeightNormal;
            }
            if (over.LetterSpacingEm.HasValue || over.LetterSpacingPx.HasValue)
            {
                c.LetterSpacingEm = over.LetterSpacingEm;
                c.LetterSpacingPx = over.LetterSpacingPx;
            }
            if (over.Color.HasValue) c.Color = over.Color;
            if (over.Align.HasValue) c.Align = over.Align;
            if (over.WhiteSpace.HasValue) c.WhiteSpace = over.WhiteSpace;
            if (over.Italic.HasValue) c.Italic = over.Italic;
            if (over.TabularNums.HasValue) c.TabularNums = over.TabularNums;
            if (over.Ellipsis.HasValue) c.Ellipsis = over.Ellipsis;
            if (over.LineClamp.HasValue) c.LineClamp = over.LineClamp;
            if (over.Shadows != null) c.Shadows = over.Shadows;
            if (over.Decoration.HasValue) c.Decoration = over.Decoration;
            if (over.DecorationStyle.HasValue) c.DecorationStyle = over.DecorationStyle;
            if (over.DecorationColor.HasValue) c.DecorationColor = over.DecorationColor;
            if (over.DecorationThickness.HasValue) c.DecorationThickness = over.DecorationThickness;
            if (over.UnderlineOffset.HasValue) c.UnderlineOffset = over.UnderlineOffset;
            c.WordBreak = over.WordBreak ?? c.WordBreak;
            c.LineBreak = over.LineBreak ?? c.LineBreak;
            c.OverflowWrap = over.OverflowWrap ?? c.OverflowWrap;
            c.TextWrap = over.TextWrap ?? c.TextWrap;
            c.WritingMode = over.WritingMode ?? c.WritingMode;
            return c;
        }

        public TextStyleData Clone()
        {
            var c = (TextStyleData)MemberwiseClone();
            if (Shadows != null) c.Shadows = new List<TextShadow>(Shadows);
            return c;
        }
    }
}
