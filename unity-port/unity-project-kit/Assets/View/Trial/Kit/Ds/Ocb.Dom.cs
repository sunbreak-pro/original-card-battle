// The DOM side of the design system's bundle.js (vendor/ds/bundle.js:14-61: h, add, sv, px, clamp, lerp, place,
// EASE) for uGUI, and the handle every Ocb builder returns in place of a DOM node (kit.md §4.14).
//
// An Ocb element is a RectTransform with a UiNode (its box in stage px, its CSS transform and opacity), a BoxGraphic
// when its rule paints anything (background, border, shadow), and its children: TMP labels for text nodes, PathGraphic
// icons, more elements. Styles come from Ocb.Styles.cs (bundle.css verbatim) composed in cascade order and parsed by
// Trial.Geometry's Css; `var(--attr)` and the other custom properties an element declares reach its children through
// a CssVars scope (VarsOf). Text styles inherit by concatenation: .ocb-root, then each ancestor's rule, then the
// element's own (later declarations win, as the cascade gives an inherited property its nearest value).
//
// Layout: the flex rows and columns of the DS run through Trial.Geometry's Flex solver (Flow), with the rule's own
// padding, border, gap, alignment and size; text that wraps is measured with TMP (TextMeasure) first, so what is
// measured is what is drawn.
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>
    /// A built DS element (the JS returns a DOM node): its root, its node, its own box, and named parts (the children
    /// the scenes reach for: a label, an icon, a lamp). The component handles add their state bools and Update(d).
    /// </summary>
    public class OcbElement
    {
        private readonly Dictionary<string, Component> _parts = new Dictionary<string, Component>(StringComparer.Ordinal);

        /// <summary>The element (anchor and pivot follow the UiNode; place it with Place / Node).</summary>
        public RectTransform Root { get; internal set; }

        /// <summary>The element's node: its box in the parent's stage px, its CSS transform, opacity, filter.</summary>
        public UiNode Node { get; internal set; }

        /// <summary>The element's own box (background, border, radius, shadow); null when its rule paints nothing.</summary>
        public BoxGraphic Box { get; internal set; }

        /// <summary>The named parts (CSS class names without "ocb-": "cost", "name", "lamp", ...).</summary>
        public IReadOnlyDictionary<string, Component> Parts => _parts;

        /// <summary>A named part as T (null when missing or of another type).</summary>
        public T Part<T>(string name) where T : Component =>
            name != null && _parts.TryGetValue(name, out Component c) && c ? c as T : null;

        /// <summary>A named part's RectTransform (null when missing).</summary>
        public RectTransform PartRect(string name) =>
            name != null && _parts.TryGetValue(name, out Component c) && c ? c.transform as RectTransform : null;

        internal void SetPart(string name, Component c)
        {
            if (c) _parts[name] = c;
            else _parts.Remove(name);
        }

        /// <summary>The border-box width in stage px.</summary>
        public float W => Node ? Node.W : 0f;

        /// <summary>The border-box height in stage px.</summary>
        public float H => Node ? Node.H : 0f;

        /// <summary>place(el, x, y): left / top in the parent's stage px (the size stays).</summary>
        public OcbElement Place(float x, float y)
        {
            if (Node) Node.Place(x, y);
            return this;
        }

        /// <summary>The element is still on screen (not destroyed).</summary>
        public bool Alive => Root;

        /// <summary>el.remove().</summary>
        public void Remove()
        {
            if (Root) Ui.Remove(Root);
        }
    }

    public static partial class Ocb
    {
        /// <summary>The theme the DS port resolves var() with: the game always runs dusk (kit.md §1).</summary>
        public const DsTheme Theme = DsTheme.Dusk;

        // js: vendor/ds/bundle.js:54 EASE (the cubic set of the brief) — the kit's Trial.Ease has the same five.
        /// <summary>OCB.EASE.out (1 − (1 − t)³).</summary>
        public static float EaseOut(float t) => Ease.Out(t);
        /// <summary>OCB.EASE.in (t³).</summary>
        public static float EaseIn(float t) => Ease.In(t);
        /// <summary>OCB.EASE.inout.</summary>
        public static float EaseInOut(float t) => Ease.InOut(t);
        /// <summary>OCB.EASE.linear.</summary>
        public static float EaseLinear(float t) => Ease.Linear(t);
        /// <summary>OCB.EASE.smooth (t²(3 − 2t)).</summary>
        public static float EaseSmooth(float t) => Ease.Smooth(t);

        // js: vendor/ds/bundle.js:44
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;

        // js: vendor/ds/bundle.js:45
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // js: vendor/ds/bundle.js:46
        /// <summary>place(el, x, y, w, h): left / top in stage px, and the size when given.</summary>
        public static RectTransform Place(RectTransform el, float x, float y, float? w = null, float? h = null) => Ui.Place(el, x, y, w, h);

        /// <summary>place(el, x, y) for a built element (returns it, typed).</summary>
        public static T Place<T>(T el, float x, float y) where T : OcbElement
        {
            el?.Place(x, y);
            return el;
        }

        // ---- styles -------------------------------------------------------------------------------------------

        /// <summary>A declaration block parsed in the DS theme (cached by Css.Parse; the result is the caller's copy).</summary>
        public static CssBlock Rule(string css, CssVars vars = null) => Css.Parse(css ?? "", Theme, vars);

        /// <summary>The box (background, border, radius, shadows, padding) of a declaration block.</summary>
        public static BoxStyle BoxOf(string css, CssVars vars = null) => Css.Box(css ?? "", Theme, vars);

        /// <summary>
        /// The text style of an element inside .ocb-root: RootCss, then `css` (its ancestors' rules and its own, in that
        /// order). The DS's flex items and absolutely placed text are laid out by the builders, so text-align is left as
        /// the rule says.
        /// </summary>
        public static TextStyle TextOf(string css, CssVars vars = null) => TextStyle.From(Css.Text(RootCss + (css ?? ""), Theme, vars));

        /// <summary>
        /// A scope holding the custom properties a rule declares (--attr, --attr-ink, --val-ink, --banner-line), over a
        /// parent scope: var() in the element's children resolves against it.
        /// </summary>
        public static CssVars VarsOf(string css, CssVars parent = null)
        {
            CssBlock b = Rule(css, parent);
            if (b.CustomProperties.Count == 0) return parent;
            var v = new CssVars(parent);
            foreach (KeyValuePair<string, string> kv in b.CustomProperties) v.Set(kv.Key, kv.Value);
            return v;
        }

        /// <summary>The px of a declared length (null when not declared, auto, or relative).</summary>
        public static float? Px(CssLength? l) => l.HasValue && l.Value.IsPx ? l.Value.Px : (float?)null;

        // ---- elements -----------------------------------------------------------------------------------------

        /// <summary>
        /// h('div', { class }): an element under parent at (0, 0) with a UiNode, sized by the rule's width / height when
        /// it declares px, with a BoxGraphic when the rule paints anything.
        /// </summary>
        public static RectTransform El(RectTransform parent, string name, string css = null, CssVars vars = null)
        {
            RectTransform rt = Ui.Div(parent, name);
            UiNode node = UiNode.Of(rt);
            if (!string.IsNullOrEmpty(css))
            {
                CssBlock b = Rule(css, vars);
                float? w = Px(b.Layout.Width), h = Px(b.Layout.Height);
                if (w.HasValue || h.HasValue) node.Place(0f, 0f, w ?? 0f, h ?? 0f);
                if (!b.Box.IsEmpty)
                {
                    BoxGraphic g = rt.gameObject.AddComponent<BoxGraphic>();
                    g.Style = b.Box;
                }
                if (b.Opacity.HasValue && b.Opacity.Value < 1f) node.Opacity = b.Opacity.Value;
            }
            return rt;
        }

        /// <summary>An element as a handle (El + its node and box).</summary>
        internal static T Make<T>(RectTransform parent, string name, string css, CssVars vars = null) where T : OcbElement, new()
        {
            RectTransform rt = El(parent, name, css, vars);
            return new T { Root = rt, Node = UiNode.Of(rt), Box = rt.GetComponent<BoxGraphic>() };
        }

        /// <summary>
        /// Re-styles an element (a state class turned on or off): its box made or replaced, its declared px size set, and
        /// (opacity: true, for an element whose rules declare one) its opacity, 1 when no rule in force declares it.
        /// </summary>
        public static BoxGraphic Restyle(RectTransform el, string css, CssVars vars = null, bool opacity = false)
        {
            if (!el) return null;
            CssBlock b = Rule(css, vars);
            BoxGraphic g = el.GetComponent<BoxGraphic>();
            if (!g && !b.Box.IsEmpty) g = el.gameObject.AddComponent<BoxGraphic>();
            if (g) g.Style = b.Box;
            UiNode n = UiNode.Of(el);
            float? w = Px(b.Layout.Width), h = Px(b.Layout.Height);
            if (w.HasValue) n.W = w.Value;
            if (h.HasValue) n.H = h.Value;
            if (opacity) n.Opacity = b.Opacity ?? 1f;
            return g;
        }

        /// <summary>A text node (a TMP label sized to its text) under parent at (0, 0).</summary>
        public static TextMeshProUGUI Text(RectTransform parent, string text, TextStyle style, string name = "text") =>
            Label.Make(parent, text ?? "", style, name);

        /// <summary>A text node in a fixed box (x, y, w, h), aligned as given.</summary>
        public static TextMeshProUGUI Text(RectTransform parent, string text, TextStyle style, float x, float y, float w, float h, TextAlign align, string name = "text")
        {
            TextStyle s = style.Clone();
            s.Align = align;
            return Label.Make(parent, text ?? "", s, x, y, w, h, name);
        }

        /// <summary>`flex: none` on a flex item (icons, swatches, lamps, dots): it neither shrinks nor stretches.</summary>
        public static void NoShrink(Component c)
        {
            if (!c) return;
            FlexAttrs a = FlexBox.Attrs((RectTransform)c.transform);
            a.Shrink = 0f;
            a.FixedCross = true;
        }

        /// <summary>A flex item with a fixed size (width and height declared): not stretched on the cross axis.</summary>
        public static void Fixed(Component c)
        {
            if (!c) return;
            FlexBox.Attrs((RectTransform)c.transform).FixedCross = true;
        }

        /// <summary>margin-left: auto on a flex item.</summary>
        public static void AutoMarginLeft(Component c)
        {
            if (!c) return;
            FlexBox.Attrs((RectTransform)c.transform).AutoMarginL = true;
        }

        /// <summary>A child that is not in the flex flow (position: absolute).</summary>
        public static void Absolute(Component c)
        {
            if (!c) return;
            FlexBox.Attrs((RectTransform)c.transform).Absolute = true;
        }

        /// <summary>
        /// Lays out a flex container's children with the rule's display, direction, wrap, gap, alignment, padding and
        /// border; w / h = its border-box size (null = the rule's px size, else its content's). The container takes the
        /// content size where neither is given.
        /// </summary>
        public static FlexResult Flow(RectTransform el, string css, CssVars vars = null, float? w = null, float? h = null)
        {
            CssBlock b = Rule(css, vars);
            FlexSpec spec = b.Layout.ToFlexSpec(b.Box.Padding + b.Box.BorderWidths, w ?? Px(b.Layout.Width), h ?? Px(b.Layout.Height));
            return FlexBox.Layout(el, spec);
        }

        /// <summary>The left / top of rt in an ancestor's stage px (the sum of the placed offsets on the way up; transforms ignored).</summary>
        public static Vec2 OffsetIn(Transform ancestor, RectTransform rt)
        {
            float x = 0f, y = 0f;
            for (Transform t = rt; t != null && t != ancestor; t = t.parent)
            {
                if (!(t is RectTransform r)) break;
                Vector2 p = Ui.PosOf(r);
                x += p.x;
                y += p.y;
            }
            return new Vec2(x, y);
        }

        /// <summary>The height of a block of text at a width (lines × line height), as a wrapped DOM block measures.</summary>
        public static float TextHeight(string text, TextStyle style, float width) =>
            string.IsNullOrEmpty(text) ? 0f : TextMeasure.Lines(text, style, width) * style.LineHeightPx;

        /// <summary>The max-content width of a text (the trailing letter-spacing included, as Chrome lays it out).</summary>
        public static float TextWidth(string text, TextStyle style) => TextMeasure.Width(text, style);

        /// <summary>A label of the kit's colour pipeline: TMP's rich-text colour tag of a colour (gamma-encoded like the vertices).</summary>
        internal static string Colored(string text, Rgba c) => Materials.ColorTag(c) + Escape(text) + "</color>";

        /// <summary>Text made safe for TMP rich text (a literal '&lt;' cannot open a tag).</summary>
        internal static string Escape(string text) => string.IsNullOrEmpty(text) ? "" : text.Replace("<", "<noparse><</noparse>");

        /// <summary>String(n) of a JS number given as a float (integers without a decimal point, as the DS prints them).</summary>
        public static string Num(float n) => n.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }
}
