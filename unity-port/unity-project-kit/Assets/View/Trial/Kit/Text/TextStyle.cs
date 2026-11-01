// A text style as the CSS declares it (kit.md §4.9): family, weight, size, line height in px, letter-spacing in em,
// colour, alignment, white-space, overflow, line clamp, text-shadows, plus the wrapping hints. Label.Apply maps it onto
// a TextMeshPro label; TextMeasure measures with it.
//
//   TextStyle.FromCss("font: 700 22px/28px var(--font-display); letter-spacing: .02em; color: var(--ink)")
//   TextStyle.Font("400 20px/32px var(--font-body)").WithColor(DsTokens.InkMuted)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Trial.Geometry;

namespace Trial.Kit
{
    /// <summary>CSS overflow + text-overflow for a label.</summary>
    public enum TextOverflow { Visible, Clip, Ellipsis }

    public sealed class TextStyle
    {
        public Family Family = Family.Body;
        public int Weight = 400;
        /// <summary>font-size px.</summary>
        public float Size = 16f;
        /// <summary>line-height px; NaN = normal (the face's content area).</summary>
        public float LineHeight = float.NaN;
        /// <summary>letter-spacing in em.</summary>
        public float LetterSpacingEm;
        /// <summary>color (the stage's default is --ink, .ocbg-stage).</summary>
        public Rgba Color = DsTokens.Ink;
        public TextAlign Align = TextAlign.Start;
        public WhiteSpace WhiteSpace = WhiteSpace.Normal;
        public TextOverflow Overflow = TextOverflow.Visible;
        /// <summary>-webkit-line-clamp; 0 = none.</summary>
        public int LineClamp;
        /// <summary>text-shadow layers in CSS order; null or empty = none.</summary>
        public TextShadow[] Shadows;
        public bool Italic;
        public bool TabularNums;
        /// <summary>text-wrap: balance.</summary>
        public bool Balance;
        /// <summary>word-break: auto-phrase (the Wrap.JaPhrases heuristic).</summary>
        public bool AutoPhrase;
        /// <summary>TMP rich text (tags) on; Label turns it on when AutoPhrase needs it.</summary>
        public bool RichText;
        public DecorationLine Decoration = DecorationLine.None;
        public BorderStyle DecorationStyle = BorderStyle.Solid;
        public Rgba? DecorationColor;
        public float? DecorationThickness;
        public float? UnderlineOffset;

        /// <summary>The resolved font (line-height normal resolved).</summary>
        public FontSpec Spec => new FontSpec(Family, Weight, Size, LineHeightPx);

        /// <summary>line-height in px (normal resolved with TextMath's metrics of the face).</summary>
        public float LineHeightPx => float.IsNaN(LineHeight) ? TextMath.NormalLineHeight(Family, Weight, Size) : LineHeight;

        /// <summary>letter-spacing in px.</summary>
        public float LetterSpacingPx => LetterSpacingEm * Size;

        public TextStyle Clone()
        {
            var c = (TextStyle)MemberwiseClone();
            if (Shadows != null) c.Shadows = (TextShadow[])Shadows.Clone();
            return c;
        }

        // ---- building ------------------------------------------------------------------------------------

        /// <summary>A CSS font shorthand ("700 22px/28px var(--font-display)") with the defaults for the rest.</summary>
        public static TextStyle Font(string fontShorthand)
        {
            FontSpec f = TextMath.ParseFont(fontShorthand);
            return new TextStyle { Family = f.Family, Weight = f.Weight, Size = f.Size, LineHeight = f.LineHeightPx };
        }

        /// <summary>The text properties of a CSS declaration block (parsed by Trial.Geometry's Css.Text).</summary>
        public static TextStyle FromCss(string declarations, DsTheme theme = DsTheme.Dusk) => From(global::Trial.Geometry.Css.Text(declarations, theme));

        /// <summary>The same as FromCss (kit.md §4.9's name).</summary>
        public static TextStyle Css(string declarations) => FromCss(declarations);

        /// <summary>A TextStyle from parsed data (undeclared properties keep the defaults).</summary>
        public static TextStyle From(TextStyleData d)
        {
            var s = new TextStyle();
            if (d == null) return s;
            if (d.Family.HasValue) s.Family = d.Family.Value;
            if (d.Weight.HasValue) s.Weight = d.Weight.Value;
            if (d.Size.HasValue) s.Size = d.Size.Value;
            if (d.LineHeightPx.HasValue) s.LineHeight = d.LineHeightPx.Value;
            else if (d.LineHeightRatio.HasValue) s.LineHeight = d.LineHeightRatio.Value * s.Size;
            s.LetterSpacingEm = d.LetterSpacingEm ?? (d.LetterSpacingPx.HasValue && s.Size > 0f ? d.LetterSpacingPx.Value / s.Size : 0f);
            if (d.Color.HasValue) s.Color = d.Color.Value;
            if (d.Align.HasValue) s.Align = d.Align.Value;
            if (d.WhiteSpace.HasValue) s.WhiteSpace = d.WhiteSpace.Value;
            if (d.Italic.HasValue) s.Italic = d.Italic.Value;
            if (d.TabularNums.HasValue) s.TabularNums = d.TabularNums.Value;
            if (d.LineClamp.HasValue) s.LineClamp = d.LineClamp.Value;
            if (d.Ellipsis == true) s.Overflow = TextOverflow.Ellipsis;
            else if (s.LineClamp > 0) s.Overflow = TextOverflow.Ellipsis;
            if (d.Shadows != null) s.Shadows = d.Shadows.ToArray();
            if (d.Decoration.HasValue) s.Decoration = d.Decoration.Value;
            if (d.DecorationStyle.HasValue) s.DecorationStyle = d.DecorationStyle.Value;
            s.DecorationColor = d.DecorationColor;
            s.DecorationThickness = d.DecorationThickness;
            s.UnderlineOffset = d.UnderlineOffset;
            if (d.TextWrap != null && d.TextWrap.IndexOf("balance", StringComparison.Ordinal) >= 0) s.Balance = true;
            if (d.WordBreak != null && d.WordBreak.IndexOf("auto-phrase", StringComparison.Ordinal) >= 0) s.AutoPhrase = true;
            return s;
        }

        // ---- fluent changes (each returns a copy) --------------------------------------------------------

        public TextStyle WithColor(Rgba c) { TextStyle s = Clone(); s.Color = c; return s; }
        public TextStyle WithAlign(TextAlign a) { TextStyle s = Clone(); s.Align = a; return s; }
        public TextStyle WithWhiteSpace(WhiteSpace w) { TextStyle s = Clone(); s.WhiteSpace = w; return s; }
        public TextStyle WithSpacing(float em) { TextStyle s = Clone(); s.LetterSpacingEm = em; return s; }
        public TextStyle WithSize(float px, float? lineHeightPx = null) { TextStyle s = Clone(); s.Size = px; if (lineHeightPx.HasValue) s.LineHeight = lineHeightPx.Value; return s; }
        public TextStyle WithLineHeight(float px) { TextStyle s = Clone(); s.LineHeight = px; return s; }
        public TextStyle WithWeight(int w) { TextStyle s = Clone(); s.Weight = w; return s; }
        public TextStyle WithFamily(Family f) { TextStyle s = Clone(); s.Family = f; return s; }
        public TextStyle WithOverflow(TextOverflow o, int lineClamp = 0) { TextStyle s = Clone(); s.Overflow = o; s.LineClamp = lineClamp; return s; }
        public TextStyle WithShadows(params TextShadow[] shadows) { TextStyle s = Clone(); s.Shadows = shadows; return s; }

        /// <summary>Everything that changes how the text lays out (not its colour or shadows): the measuring cache key.</summary>
        public string LayoutKey()
        {
            var sb = new StringBuilder(64);
            sb.Append((int)Family).Append('|').Append(TrialFonts.ResolveWeight(Family, Weight)).Append('|')
              .Append(Size.ToString("0.###", CultureInfo.InvariantCulture)).Append('|')
              .Append(LineHeightPx.ToString("0.###", CultureInfo.InvariantCulture)).Append('|')
              .Append(LetterSpacingEm.ToString("0.####", CultureInfo.InvariantCulture)).Append('|')
              .Append((int)WhiteSpace).Append('|').Append(Italic ? 1 : 0).Append('|').Append(RichText || AutoPhrase ? 1 : 0);
            return sb.ToString();
        }

        public override string ToString() =>
            Spec.ToCss() + "; letter-spacing: " + LetterSpacingEm.ToString("0.###", CultureInfo.InvariantCulture) + "em; color: " + CssColor.ToCss(Color);
    }
}
