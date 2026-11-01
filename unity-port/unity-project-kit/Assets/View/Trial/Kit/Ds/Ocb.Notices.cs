// The notices (vendor/ds/bundle.js:525-543, bundle.css:150-163): the hover line, the reach panel, the refusal line,
// the banner and the floating number.
using System;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    public static partial class Ocb
    {
        // js: vendor/ds/bundle.js:528
        /// <summary>
        /// HoverLine(text, lit): .ocb-hoverline 420 wide, at least 48 tall (padding 10 14, flex centred, gap 10, panel-solid,
        /// 1px steel, radius 6, 400 18/26 body): the lamp (10 steel; lit: 16 wick with its glow) and the text, wrapping in
        /// what is left. The two flex items shrink as Chrome shrinks them (by their widths) when the text's one line is wider
        /// than the row: the lamp narrows a little then, as it does in the browser. At (0, 0) of parent.
        /// </summary>
        public static OcbElement HoverLine(RectTransform parent, string text, bool lit)
        {
            OcbElement el = Make<OcbElement>(parent, "ocb-hoverline", HoverLineCss);
            BoxStyle bs = el.Box.Style;
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            CssBlock rule = Rule(HoverLineCss);
            float gap = rule.Layout.ColumnGap ?? 10f;
            float innerW = el.W - bw.Horizontal - pad.Horizontal;
            string lampCss = LampCss + (lit ? LampOnCss : "");
            OcbElement lamp = Make<OcbElement>(el.Root, "ocb-lamp", lampCss);
            TextStyle ts = TextOf(HoverLineCss);
            text = text ?? "";
            float lampW = lamp.W, tw = TextWidth(text, ts);
            float over = lampW + gap + tw - innerW;
            if (over > 0f && lampW + tw > 0f)
            {
                // flex-shrink 1 on both, weighted by their bases; the lamp's automatic minimum is its empty content (0)
                float sum = lampW + tw;
                lampW = Math.Max(0f, lampW - over * lampW / sum);
                tw = Math.Max(0f, innerW - gap - lampW);   // the text takes the rest of the row
            }
            float th = TextHeight(text, ts, tw + 0.01f);
            float contentH = Math.Max(lamp.H, th);
            float minH = Px(rule.Layout.MinHeight) ?? 48f;
            float h = Math.Max(minH, contentH + pad.Vertical + bw.Vertical);
            el.Node.H = h;
            float cy = bw.Top + pad.Top, ch = h - bw.Vertical - pad.Vertical;
            lamp.Node.Place(bw.Left + pad.Left, cy + (ch - lamp.H) * 0.5f, lampW, lamp.H);
            TextMeshProUGUI t = Label.Make(el.Root, text, ts, bw.Left + pad.Left + lampW + gap, cy + (ch - th) * 0.5f, tw + 0.01f, th, "span");
            el.SetPart("lamp", lamp.Root);
            el.SetPart("text", t);
            return el;
        }

        // js: vendor/ds/bundle.js:531
        /// <summary>
        /// ReachPanel(text): .ocb-reachpanel 280×76 (padding 12 16, flex centred, gap 12, panel-solid, 1px dashed --miss,
        /// radius 6, 400 18/26 body): `range` 22 ink and the text in two lines, cut after 「間合いが」. At (0, 0) of parent.
        /// </summary>
        public static OcbElement ReachPanel(RectTransform parent, string text)
        {
            text = text ?? "";
            string t1 = text, t2 = "";
            int cut = text.IndexOf("間合いが", StringComparison.Ordinal);
            if (cut >= 0) { t1 = text.Substring(0, cut + 4); t2 = text.Substring(cut + 4); }
            OcbElement el = Make<OcbElement>(parent, "ocb-reachpanel", ReachPanelCss);
            BoxStyle bs = el.Box.Style;
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            float gap = Rule(ReachPanelCss).Layout.ColumnGap ?? 12f;
            float innerW = el.W - bw.Horizontal - pad.Horizontal, innerH = el.H - bw.Vertical - pad.Vertical;
            PathGraphic icon = Icon(el.Root, "range", 22f, DsTokens.Ink);
            TextStyle ts = TextOf(ReachPanelCss);
            // the text block: its max-content width, shrunk to what is left after the icon and the gap
            float avail = Math.Max(0f, innerW - 22f - gap);
            float bw1 = Math.Min(Math.Max(TextWidth(t1, ts), t2.Length > 0 ? TextWidth(t2, ts) : 0f), avail);
            float h1 = TextHeight(t1, ts, bw1 + 0.01f), h2 = t2.Length > 0 ? TextHeight(t2, ts, bw1 + 0.01f) : 0f;
            float bh = h1 + h2;
            float x0 = bw.Left + pad.Left, y0 = bw.Top + pad.Top;
            Ui.Place(icon.rectTransform, x0, y0 + (innerH - 22f) * 0.5f);
            RectTransform block = El(el.Root, "lines");
            UiNode.Of(block).Place(x0 + 22f + gap, y0 + (innerH - bh) * 0.5f, bw1, bh);
            el.SetPart("line1", Label.Make(block, t1, ts, 0f, 0f, bw1 + 0.01f, h1, "line1"));
            if (t2.Length > 0) el.SetPart("line2", Label.Make(block, t2, ts, 0f, h1, bw1 + 0.01f, h2, "line2"));
            return el;
        }

        // js: vendor/ds/bundle.js:537
        /// <summary>
        /// RefusalLine(text): .ocb-refusal, 48 tall, padding 0 20 0 16, inline-flex centred, gap 12, panel-solid, 1px steel,
        /// radius 6, 400 20/28 body, nowrap: the amber dot 16 and the text. At (0, 0) of parent.
        /// </summary>
        public static OcbElement RefusalLine(RectTransform parent, string text)
        {
            OcbElement el = Make<OcbElement>(parent, "ocb-refusal", RefusalCss);
            RectTransform dot = El(el.Root, "ocb-dot", DotCss);
            NoShrink(dot);
            el.SetPart("dot", dot);
            el.SetPart("text", Text(el.Root, text ?? "", TextOf(RefusalCss), "text"));
            Flow(el.Root, RefusalCss);
            return el;
        }

        // js: vendor/ds/bundle.js:538
        /// <summary>
        /// Banner(text, foe): .ocb-banner, as wide as its parent (left 0, right 0; 1920 when the parent has no width), 132
        /// tall, the 90° band (rgba(10,12,18,0) → .8 at 28 % … 72 % → 0), 800 64/72 display, .12em, centred; ::before /
        /// ::after: 2px lines from 22 % to 78 %, 10 px in from the top and the bottom, --banner-line (amber; .is-foe violet)
        /// at opacity .7. pointer-events: none. At (0, 0) of parent: place its top.
        /// </summary>
        public static OcbElement Banner(RectTransform parent, string text, bool foe)
        {
            string css = BannerCss + (foe ? BannerFoeCss : "");
            CssVars vars = VarsOf(css);
            OcbElement el = Make<OcbElement>(parent, "ocb-banner", css, vars);
            float w = parent && parent.rect.width > 0f ? parent.rect.width : TrialStage.W;
            el.Node.W = w;
            el.Node.PointerEvents = PointerEvents.None;
            float h = el.H;
            RectTransform before = El(el.Root, "before", BannerLinesCss + BannerBeforeCss, vars);
            UiNode.Of(before).Place(w * 0.22f, 10f, w * 0.56f, 2f);   // left 22%, right 22%, top 10, height 2
            TextStyle ts = TextOf(css, vars);
            float tw = TextWidth(text ?? "", ts);
            TextMeshProUGUI t = Label.Make(el.Root, text ?? "", ts, (w - tw) * 0.5f, (h - ts.LineHeightPx) * 0.5f, null, null, "text");
            RectTransform after = El(el.Root, "after", BannerLinesCss + BannerAfterCss, vars);
            UiNode.Of(after).Place(w * 0.22f, h - 10f - 2f, w * 0.56f, 2f);   // bottom 10
            el.SetPart("before", before);
            el.SetPart("text", t);
            el.SetPart("after", after);
            return el;
        }

        // js: vendor/ds/bundle.js:539
        /// <summary>
        /// FloatNum(kind, n, label): .ocb-float (flex centred, gap 6, padding 2 10, radius 6, black .8, nowrap,
        /// translate(−50%, −50%): place it at its centre; pointer-events none). damage: heart 36 hp-text + n (700 48/52 mono
        /// hp-text); 'guard': shield 30 (guard, fill .2) + n (34/40 guard); 'heal': heal 34 hp-text + n; 'miss': n alone
        /// (400 24/32 body ink, line-through 2px --miss). label: a small (400 16/20 body ink) after n.
        /// </summary>
        public static OcbElement FloatNum(RectTransform parent, string kind, string n, string label = null)
        {
            string css = FloatCss;   // .is-guard / .is-miss restyle the number only
            OcbElement el = Make<OcbElement>(parent, "ocb-float", css);
            el.Node.PointerEvents = PointerEvents.None;
            if (kind == "guard") Icon(el.Root, "shield", 30f, DsTokens.Guard, DsTokens.Guard, 0.2f);
            else if (kind == "heal") Icon(el.Root, "heal", 34f, DsTokens.HpText);
            else if (kind != "miss") Icon(el.Root, "heart", 36f, DsTokens.HpText);
            string bCss = FloatCss + FloatBCss + (kind == "guard" ? FloatGuardBCss : kind == "miss" ? FloatMissBCss : "");
            TextMeshProUGUI b = Text(el.Root, n ?? "", TextOf(bCss), "b");
            el.SetPart("b", b);
            if (!string.IsNullOrEmpty(label)) el.SetPart("small", Text(el.Root, label, TextOf(FloatCss + FloatSmallCss), "small"));
            Flow(el.Root, css);
            if (kind == "miss") el.SetPart("line-through", TextDecor.FromStyle(b));   // text-decoration: line-through 2px var(--miss)
            el.Node.SetStyleTransform(pctX: -0.5f, pctY: -0.5f);   // transform: translate(-50%, -50%)
            return el;
        }
    }
}
