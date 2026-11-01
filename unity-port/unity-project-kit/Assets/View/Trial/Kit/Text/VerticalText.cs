// writing-mode: vertical-rl, one TMP label per character (the title's tagline animates every character anyway;
// meta.css:47-50, title.js:33-43).
//
// - A column is as wide as the line height and its characters stack downwards at pitch = size + letter-spacing
//   (letter-spacing follows every character, the last included). Columns run right to left (flex row-reverse,
//   justify-content: flex-start), `gap` apart; a column may start lower (the tagline's last column, margin-top 96).
// - Upright CJK glyphs fill their em box in the cell: the box top sits at the cell top (the face's ideographic top,
//   OS/2 typo ascender 0.88 em for Shippori and Zen Kaku), horizontally centred in the column.
// - Punctuation and long marks take TextMath.Vertical's placement: 、。 to the upper right, ー and brackets turned 90°.
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    public sealed class VerticalText
    {
        /// <summary>The ideographic em-box top above the baseline (OS/2 sTypoAscender / unitsPerEm of the CJK faces).</summary>
        public const float IdeographicTopEm = 0.88f;

        /// <summary>The container (placed by the caller); its box is width × the tallest column.</summary>
        public RectTransform Root { get; private set; }

        /// <summary>One RectTransform per column, in the order given (the first is the rightmost).</summary>
        public List<RectTransform> Columns { get; } = new List<RectTransform>();

        /// <summary>Every character's label, in reading order (the JS charEls).</summary>
        public List<TextMeshProUGUI> Chars { get; } = new List<TextMeshProUGUI>();

        /// <summary>Each character's cell (its parent RectTransform), in reading order: animate these.</summary>
        public List<RectTransform> Cells { get; } = new List<RectTransform>();

        /// <summary>Characters advance by this many px.</summary>
        public float Pitch { get; private set; }

        public float ColumnWidth { get; private set; }

        /// <summary>
        /// Vertical-rl columns inside a box `width` px wide: columns[0] at the right edge, the next `gap` px to its left.
        /// columnMarginTop[i] (optional) moves column i down.
        /// </summary>
        public static VerticalText Make(RectTransform parent, IReadOnlyList<string> columns, TextStyle style, float width, float gap,
                                        IReadOnlyList<float> columnMarginTop = null, string name = null)
        {
            if (style == null) throw new ArgumentNullException(nameof(style));
            var v = new VerticalText();
            v.Root = Ui.Div(parent, string.IsNullOrEmpty(name) ? "vertical" : name);
            float size = style.Size;
            v.Pitch = size + style.LetterSpacingPx;
            v.ColumnWidth = style.LineHeightPx;
            float right = width;
            float tallest = 0f;
            // Each character's label is a single unspaced line; the cell carries the pitch.
            TextStyle charStyle = style.Clone();
            charStyle.LetterSpacingEm = 0f;
            charStyle.WhiteSpace = WhiteSpace.Pre;
            charStyle.Align = TextAlign.Center;
            FontMetrics m = TrialFonts.MetricsOf(style.Family, style.Weight);
            for (int c = 0; c < (columns?.Count ?? 0); c++)
            {
                string line = columns[c] ?? "";
                float top = columnMarginTop != null && c < columnMarginTop.Count ? columnMarginTop[c] : 0f;
                float x = right - v.ColumnWidth;
                RectTransform col = Ui.Div(v.Root, "col" + c, x, top, v.ColumnWidth, 0f);
                v.Columns.Add(col);
                int n = 0;
                for (int i = 0; i < line.Length; i++)
                {
                    string ch = line.Substring(i, 1);
                    if (char.IsHighSurrogate(line[i]) && i + 1 < line.Length) { ch = line.Substring(i, 2); i++; }
                    RectTransform cell = Ui.Div(col, "cell", 0f, n * v.Pitch, v.ColumnWidth, size);
                    TextMeshProUGUI t = Label.Make(cell, ch, charStyle, 0f, 0f, v.ColumnWidth, size, "ch");
                    // The em box top at the cell top: TMP puts the baseline ascent below the label top, CSS the ideographic top.
                    LabelState st = Label.StateOf(t);
                    st.HalfLeading = (IdeographicTopEm - m.AscentEm) * size;
                    st.AscentPx = m.AscentEm * size;
                    Label.FixFirstBaseline(t);
                    VerticalGlyph g = TextMath.Vertical(ch[0]);
                    if (g.OffsetEm.X != 0f || g.OffsetEm.Y != 0f || g.RotateDeg != 0f)
                    {
                        UiNode node = UiNode.Of(t.rectTransform);
                        node.TranslateX = g.OffsetEm.X * size;
                        node.TranslateY = g.OffsetEm.Y * size;
                        node.Rotate = g.RotateDeg;
                    }
                    v.Chars.Add(t);
                    v.Cells.Add(cell);
                    n++;
                }
                float h = n * v.Pitch;
                Ui.Size(col, v.ColumnWidth, h);
                tallest = Math.Max(tallest, top + h);
                right = x - gap;
            }
            Ui.Size(v.Root, width, tallest);
            return v;
        }
    }
}
