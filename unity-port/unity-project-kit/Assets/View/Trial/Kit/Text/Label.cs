// TextMeshPro labels laid out as the browser lays out the same CSS text (kit.md §4.9):
// - fontSize = px; no auto sizing; raycastTarget off; one of TrialFonts' faces.
// - letter-spacing: characterSpacing = em × 100 (TMP adds it after every glyph, as Chrome does).
// - line-height: lineSpacing = (L / size − (ascent + descent + gap)) × 100 with the face's own metrics, so the line
//   pitch is L.
// - baseline: CSS puts the first baseline at top + (L − content)/2 + ascent, TMP at top + ascent: the label is top
//   aligned with a top margin of (L − content)/2 (negative for tight line heights).
// - white-space: normal / nowrap collapse spaces and newlines, pre-line keeps newlines, pre / pre-wrap keep all;
//   nowrap / pre do not wrap.
// - overflow: ellipsis → TMP Ellipsis; clip → Truncate; -webkit-line-clamp n → the box n lines tall with Ellipsis.
// - a box without a width is as wide as its text (max-content); without a height it is lines × L tall.
// - text-shadow: blur ≤ 3 px as the face's underlay (TextShadows); larger ones are left for TextGlow (GlowShadows).
// - colour goes through Materials.Vertex (the gamma target) and follows the CSS filter of the ancestors (LabelState).
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    public static class Label
    {
        /// <summary>A label at (0, 0) of parent, sized to its text.</summary>
        public static TextMeshProUGUI Make(RectTransform parent, string text, TextStyle style, string name = null)
            => Make(parent, text, style, 0f, 0f, null, null, name);

        /// <summary>A label placed at (x, y) in parent's stage px; w / h null = sized to the text (max-content width, lines × L height).</summary>
        public static TextMeshProUGUI Make(RectTransform parent, string text, TextStyle style, float x, float y, float? w = null, float? h = null, string name = null)
        {
            if (style == null) throw new ArgumentNullException(nameof(style));
            var go = new GameObject(string.IsNullOrEmpty(name) ? "label" : name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            if (parent != null)
            {
                go.layer = parent.gameObject.layer;
                rt.SetParent(parent, false);
            }
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w ?? 0f, h ?? 0f);
            var t = go.AddComponent<TextMeshProUGUI>();
            LabelState st = go.AddComponent<LabelState>();
            st.AutoW = !w.HasValue;
            st.AutoH = !h.HasValue;
            Apply(t, style);
            SetText(t, text);
            return t;
        }

        /// <summary>The state component of a label (added when missing).</summary>
        public static LabelState StateOf(TMP_Text t)
        {
            LabelState st = t.GetComponent<LabelState>();
            if (!st) st = t.gameObject.AddComponent<LabelState>();
            return st;
        }

        /// <summary>The style last applied to the label (null when it never got one).</summary>
        public static TextStyle StyleOf(TMP_Text t)
        {
            LabelState st = t ? t.GetComponent<LabelState>() : null;
            return st ? st.Style : null;
        }

        /// <summary>Maps a TextStyle onto a TMP label (font, size, spacing, line pitch, baseline, wrapping, overflow, colour, shadows).</summary>
        public static void Apply(TMP_Text t, TextStyle style)
        {
            if (!t) throw new ArgumentNullException(nameof(t));
            if (style == null) throw new ArgumentNullException(nameof(style));
            LabelState st = StateOf(t);
            st.Style = style.Clone();

            TMP_FontAsset font = TrialFonts.Get(style.Family, style.Weight);
            if (font && t.font != font) t.font = font;
            t.fontSize = style.Size;
            t.enableAutoSizing = false;
            t.fontStyle = style.Italic ? FontStyles.Italic : FontStyles.Normal;
            t.characterSpacing = TextMath.TmpCharacterSpacing(style.LetterSpacingEm);
            t.wordSpacing = 0f;
            t.paragraphSpacing = 0f;
            t.richText = style.RichText || style.AutoPhrase;
            t.parseCtrlCharacters = false;
            t.raycastTarget = false;
            t.isOrthographic = true;

            FontMetrics m = TrialFonts.MetricsOf(style.Family, style.Weight);
            float lh = style.LineHeightPx;
            t.lineSpacing = TextMath.TmpLineSpacing(lh, style.Size, m.AscentEm, m.DescentEm, m.LineGapEm);
            float halfLeading = TextMath.HalfLeading(lh, style.Size, m.AscentEm, m.DescentEm);
            // Chrome adds letter-spacing after the last character too, TMP only between characters (measured: 3 × .1em
            // at 20 px is 4 px wider in TMP, 6 in Chrome): the trailing one becomes a right margin, so widths, wrapping and
            // centring match.
            st.MarginRight = style.LetterSpacingPx;
            t.margin = new Vector4(0f, halfLeading, st.MarginRight, 0f);
            st.HalfLeading = halfLeading;
            st.AscentPx = m.AscentEm * style.Size;

            t.horizontalAlignment = HorizontalOf(style.Align);
            t.verticalAlignment = VerticalAlignmentOptions.Top;

            switch (style.WhiteSpace)
            {
                case WhiteSpace.NoWrap: t.textWrappingMode = TextWrappingModes.NoWrap; break;
                case WhiteSpace.Pre: t.textWrappingMode = TextWrappingModes.PreserveWhitespaceNoWrap; break;
                case WhiteSpace.PreWrap: t.textWrappingMode = TextWrappingModes.PreserveWhitespace; break;
                default: t.textWrappingMode = TextWrappingModes.Normal; break;
            }

            if (style.LineClamp > 0)
            {
                t.overflowMode = TextOverflowModes.Ellipsis;
                t.maxVisibleLines = style.LineClamp;
            }
            else
            {
                t.maxVisibleLines = 99999;
                t.overflowMode = style.Overflow == TextOverflow.Ellipsis ? TextOverflowModes.Ellipsis
                    : style.Overflow == TextOverflow.Clip ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;
            }

            st.Glow = TextShadows.Apply(t, style.Shadows);
            st.Filter = UiNode.FilterAt(t.transform);
            st.ApplyColor();
            if (st.Text != null) SetText(t, st.Text);
        }

        /// <summary>CSS text-align → TMP.</summary>
        public static HorizontalAlignmentOptions HorizontalOf(TextAlign a)
        {
            switch (a)
            {
                case TextAlign.Center: return HorizontalAlignmentOptions.Center;
                case TextAlign.Right:
                case TextAlign.End: return HorizontalAlignmentOptions.Right;
                case TextAlign.Justify: return HorizontalAlignmentOptions.Justified;
                default: return HorizontalAlignmentOptions.Left;
            }
        }

        /// <summary>el.textContent = text: white-space applied, the wrap helpers run, an auto box re-sized.</summary>
        public static void SetText(TMP_Text t, string text)
        {
            if (!t) return;
            LabelState st = StateOf(t);
            TextStyle style = st.Style ?? new TextStyle();
            st.Text = text ?? "";
            string shown = Prepare(st.Text, style.WhiteSpace);
            if (style.AutoPhrase) shown = Wrap.JaPhrases(shown);
            t.text = shown;
            Relayout(t);
        }

        /// <summary>
        /// Sizes an auto box, balances, and fixes the first baseline again. TMP lays text out only inside an active,
        /// enabled canvas: a label built under an inactive parent is laid out again when it is enabled (LabelState).
        /// </summary>
        public static void Relayout(TMP_Text t)
        {
            if (!t) return;
            LabelState st = StateOf(t);
            TextStyle style = st.Style ?? new TextStyle();
            Resize(t);
            if (style.Balance) Wrap.Balance(t);
            FixFirstBaseline(t);
            st.LaidOut = string.IsNullOrEmpty(t.text) || t.textInfo.lineCount > 0;
        }

        /// <summary>
        /// Puts the first baseline where CSS puts it. TMP raises a line to the tallest face in it, so a fallback glyph
        /// (Japanese in a Mono label: Zen Kaku's 1.160 em ascent over Plex's 1.025) would push the line down
        /// (measured 2.7 px at 20 px); CSS lays the line out with the element's own face. The top margin absorbs it.
        /// </summary>
        public static void FixFirstBaseline(TMP_Text t)
        {
            if (!t || string.IsNullOrEmpty(t.text)) return;
            LabelState st = StateOf(t);
            Vector4 m = t.margin;
            if (Math.Abs(m.y - st.HalfLeading) > 0.001f)
            {
                m.y = st.HalfLeading;
                t.margin = m;
            }
            t.ForceMeshUpdate(true, true);
            if (t.textInfo.lineCount == 0) return;
            float expected = t.rectTransform.rect.yMax - (st.HalfLeading + st.AscentPx);
            float actual = t.textInfo.lineInfo[0].baseline;
            float delta = actual - expected;
            if (Math.Abs(delta) < 0.01f) return;
            m.y = st.HalfLeading + delta;
            t.margin = m;
        }

        /// <summary>Sets the colour (CSS color), through the filter in force and the gamma encoding.</summary>
        public static void SetColor(TMP_Text t, Rgba color)
        {
            if (!t) return;
            LabelState st = StateOf(t);
            if (st.Style == null) st.Style = new TextStyle();
            st.Style.Color = color;
            st.ApplyColor();
        }

        /// <summary>The text-shadows the underlay could not draw (for TextGlow).</summary>
        public static IReadOnlyList<TextShadow> GlowShadows(TMP_Text t)
        {
            LabelState st = t ? t.GetComponent<LabelState>() : null;
            return st && st.Glow != null ? st.Glow : (IReadOnlyList<TextShadow>)Array.Empty<TextShadow>();
        }

        /// <summary>Re-sizes an auto-sized box to its text: max-content width, lines × line height.</summary>
        public static void Resize(TMP_Text t)
        {
            if (!t) return;
            LabelState st = StateOf(t);
            if (!st.AutoW && !st.AutoH) return;
            TextStyle style = st.Style ?? new TextStyle();
            RectTransform rt = t.rectTransform;
            UiNode node = rt.GetComponent<UiNode>();
            float w = node ? node.W : rt.sizeDelta.x;
            float h = node ? node.H : rt.sizeDelta.y;
            if (st.AutoW)
            {
                // The preferred width counts a positive right margin (the trailing letter-spacing) but not a negative one.
                float pref = string.IsNullOrEmpty(t.text) ? 0f : t.GetPreferredValues(t.text, 100000f, 100000f).x + Math.Min(0f, st.MarginRight);
                w = Mathf.Ceil(Math.Max(0f, pref) * 100f) / 100f;
                Ui.Size(rt, w, h);
            }
            if (st.AutoH)
            {
                t.ForceMeshUpdate(true, true);
                int lines = string.IsNullOrEmpty(t.text) ? 0 : Math.Max(1, t.textInfo.lineCount);
                if (style.LineClamp > 0) lines = Math.Min(lines, style.LineClamp);
                h = lines * style.LineHeightPx;
                Ui.Size(rt, w, h);
            }
        }

        /// <summary>CSS white-space processing of a text node: collapsing spaces and newlines as the browser does.</summary>
        public static string Prepare(string text, WhiteSpace ws)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            if (ws == WhiteSpace.Pre || ws == WhiteSpace.PreWrap) return text.Replace("\r\n", "\n");
            bool keepNewlines = ws == WhiteSpace.PreLine;
            var sb = new StringBuilder(text.Length);
            bool pendingSpace = false;
            bool lineStart = true;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r') continue;
                if (c == '\n' && keepNewlines)
                {
                    // spaces before a kept newline vanish; so do those after it
                    pendingSpace = false;
                    sb.Append('\n');
                    lineStart = true;
                    continue;
                }
                if (c == ' ' || c == '\t' || c == '\n' || c == '\f')
                {
                    if (!lineStart) pendingSpace = true;
                    continue;
                }
                if (pendingSpace) sb.Append(' ');
                pendingSpace = false;
                lineStart = false;
                sb.Append(c);
            }
            return sb.ToString();
        }
    }

    /// <summary>What a label was given (style, text, auto sizing) and its colour pipeline: the CSS filter in force and the gamma encoding.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class LabelState : MonoBehaviour, IFilterTarget
    {
        public TextStyle Style;
        public string Text;
        public bool AutoW;
        public bool AutoH;
        /// <summary>The top margin that puts the first baseline where CSS does (the half-leading).</summary>
        public float HalfLeading;
        /// <summary>The face's ascent in px (TMP's baseline sits this far below the margin).</summary>
        public float AscentPx;
        /// <summary>The base right margin: the trailing letter-spacing.</summary>
        public float MarginRight;
        public ColorFilter Filter = ColorFilter.None;
        public List<TextShadow> Glow;

        private TMP_Text _t;

        private TMP_Text T
        {
            get
            {
                if (!_t) _t = GetComponent<TMP_Text>();
                return _t;
            }
        }

        public void SetInheritedFilter(ColorFilter filter)
        {
            Filter = filter;
            ApplyColor();
        }

        /// <summary>Writes the style's colour (filtered, encoded) into the label.</summary>
        public void ApplyColor()
        {
            TMP_Text t = T;
            if (!t || Style == null) return;
            t.color = Materials.VertexColor(Filter.IsNone ? Style.Color : Filter.Apply(Style.Color));
        }

        /// <summary>The last Relayout found a canvas (TMP produced lines); false = lay out again on enable.</summary>
        public bool LaidOut;

        private void OnEnable()
        {
            Materials.GammaEncodingChanged += OnEncodingChanged;
            if (!LaidOut && Style != null && Text != null)
            {
                TMP_Text t = T;
                if (t) Label.Relayout(t);
            }
        }

        private void OnDisable() => Materials.GammaEncodingChanged -= OnEncodingChanged;

        private void OnEncodingChanged()
        {
            TMP_Text t = T;
            if (!t || Style == null) return;
            Glow = TextShadows.Apply(t, Style.Shadows);
            ApplyColor();
        }

        private void OnTransformParentChanged()
        {
            Filter = UiNode.FilterAt(transform);
            ApplyColor();
        }
    }
}
