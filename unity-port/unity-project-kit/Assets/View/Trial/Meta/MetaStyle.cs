// The shared rules of scenes/meta/meta.css (the "shared" and "buttons" blocks, the washi, the washi button, the keys
// line, the hall's h2 and the text-wrap rules) copied verbatim as named declaration blocks, so a reviewer can diff them
// against the stylesheet, and the builders the meta screens make them with:
//   MetaStyle.Fade / Shade / Panel / Keys / Dot / H2 / Washi / Scroll / Text / P,   MetaButton (primary, btn, chip-btn,
//   washi-btn, x, step, nav),
//   TitleTip (the HTML title attribute: a delayed tip while the pointer rests on an element).
// Rules for one element are composed in cascade order (specificity, then stylesheet order) and parsed by Trial.Geometry's
// Css (var(), color-mix(), shorthands). Transitions run through the kit's CssStates on the document clock (CssTime).
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Trial.Meta
{
    public static class MetaStyle
    {
        // ---- meta.css: shared, verbatim -----------------------------------------------------------------------------

        /// <summary>meta.css:6 .ocbg-meta (the scene root).</summary>
        public const string MetaCss = "position: absolute; inset: 0; overflow: hidden; font-family: var(--font-body); color: var(--ink);";

        /// <summary>meta.css:10 .ocbg-meta button (the reset every meta button starts from).</summary>
        public const string ButtonCss = "font-family: inherit; color: inherit; background: none; border: 0; padding: 0; cursor: pointer; -webkit-tap-highlight-color: transparent;";

        /// <summary>meta.css:11 .ocbg-meta button:focus-visible.</summary>
        public const string ButtonFocusVisibleCss = "outline: 2px solid var(--wick); outline-offset: 3px;";

        /// <summary>meta.css:12 .ocbg-meta button[disabled].</summary>
        public const string ButtonDisabledCss = "cursor: default;";

        /// <summary>meta.css:13 .ocbg-meta-backdrop.</summary>
        public const string BackdropCss = "position: absolute; left: 0; top: 0; width: 1920px; height: 1080px; display: block; pointer-events: none;";

        /// <summary>meta.css:14 .ocbg-meta-fade.</summary>
        public const string FadeCss = "position: absolute; background: var(--ground); pointer-events: none; z-index: 50;";

        /// <summary>meta.css:15 .ocbg-meta-keys.</summary>
        public const string KeysCss = "position: absolute; font: 400 16px/22px var(--font-body); color: var(--ink); letter-spacing: .06em; white-space: nowrap;";

        /// <summary>meta.css:16 .ocbg-meta-keys.is-in-panel.</summary>
        public const string KeysInPanelCss = "position: static;";

        /// <summary>meta.css:420 .ocbg-meta-keys.is-right.</summary>
        public const string KeysRightCss = "text-align: right;";

        /// <summary>meta.css:17 .ocbg-meta-shade.</summary>
        public const string ShadeCss = "position: absolute; background: color-mix(in srgb, var(--ground) 72%, transparent);";

        /// <summary>meta.css:18 .ocbg-meta-shade.is-light.</summary>
        public const string ShadeLightCss = "background: color-mix(in srgb, var(--ground) 30%, transparent);";

        /// <summary>meta.css:19 .ocbg-meta-shade.is-deep.</summary>
        public const string ShadeDeepCss = "background: color-mix(in srgb, var(--ground) 86%, transparent);";

        /// <summary>meta.css:20 .ocbg-meta-panel.</summary>
        public const string PanelCss = "background: var(--panel-solid); border: var(--stroke-rule) solid var(--steel); border-radius: var(--radius-panel); box-shadow: var(--shadow-tip);";

        /// <summary>meta.css:21 .ocbg-meta-x.</summary>
        public const string XCss = "width: 44px; height: 44px; display: inline-flex; align-items: center; justify-content: center; border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel);";

        /// <summary>meta.css:22 .ocbg-meta-x:hover.</summary>
        public const string XHoverCss = "border-color: var(--ink);";

        /// <summary>meta.css:23 .ocbg-meta-dot.</summary>
        public const string DotCss = "display: inline-block; width: 10px; height: 10px; border-radius: 50%; background: var(--steel); flex: none;";

        /// <summary>meta.css:24 ::-webkit-scrollbar.</summary>
        public const string ScrollbarCss = "width: 8px; height: 8px;";

        /// <summary>meta.css:25 ::-webkit-scrollbar-thumb.</summary>
        public const string ScrollbarThumbCss = "background: var(--steel); border-radius: 4px;";

        /// <summary>meta.css:26 ::-webkit-scrollbar-track.</summary>
        public const string ScrollbarTrackCss = "background: transparent;";

        // ---- meta.css: buttons, verbatim ----------------------------------------------------------------------------

        /// <summary>meta.css:29 .ocbg-meta-primary.</summary>
        public const string PrimaryCss = "display: flex; align-items: center; justify-content: center; gap: var(--space-16); border: var(--stroke-frame) solid var(--amber); border-radius: var(--radius-panel); background: color-mix(in srgb, var(--amber) 16%, transparent); font: 700 28px/1 var(--font-display); letter-spacing: .2em; color: var(--ink); transition: background .14s, box-shadow .14s, transform .14s, opacity .2s;";

        /// <summary>meta.css:30 .ocbg-meta-primary small.</summary>
        public const string PrimarySmallCss = "font: 400 16px/1 var(--font-mono); letter-spacing: .04em; padding: 4px 8px; border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-tag);";

        /// <summary>meta.css:31 .ocbg-meta-primary:hover:not([disabled]).</summary>
        public const string PrimaryHoverCss = "background: color-mix(in srgb, var(--amber) 28%, transparent); box-shadow: var(--shadow-reach); transform: translateY(-2px);";

        /// <summary>meta.css:32 .ocbg-meta-primary[disabled].</summary>
        public const string PrimaryDisabledCss = "opacity: .32;";

        /// <summary>meta.css:33 .ocbg-meta-primary.is-compact.</summary>
        public const string PrimaryCompactCss = "height: 56px; padding: 0 28px; font-size: 22px;";

        /// <summary>meta.css:34 .ocbg-meta-btn.</summary>
        public const string BtnCss = "display: inline-flex; align-items: center; justify-content: center; gap: var(--space-8); height: 100%; padding: 0 var(--space-24); background: var(--panel); border: var(--stroke-rule) solid var(--steel); border-radius: var(--radius-panel); font: 700 20px/1 var(--font-body); letter-spacing: .06em; transition: border-color .14s, background .14s, transform .14s;";

        /// <summary>meta.css:35 .ocbg-meta-btn:hover:not([disabled]).</summary>
        public const string BtnHoverCss = "border-color: var(--ink); background: color-mix(in srgb, var(--ink) 8%, var(--panel-solid)); transform: translateY(-1px);";

        /// <summary>meta.css:36 .ocbg-meta-chip-btn.</summary>
        public const string ChipBtnCss = "display: flex; align-items: center; justify-content: center; gap: var(--space-8); background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); font: 700 20px/1 var(--font-body);";

        /// <summary>meta.css:37 .ocbg-meta-chip-btn small.</summary>
        public const string ChipBtnSmallCss = "font: 400 16px/1 var(--font-mono);";

        /// <summary>meta.css:38 .ocbg-meta-chip-btn:hover.</summary>
        public const string ChipBtnHoverCss = "border-color: var(--ink);";

        /// <summary>meta.css:417 .ocbg-meta-chip-btn.is-square.</summary>
        public const string ChipBtnSquareCss = "padding: 0;";

        /// <summary>meta.css:418 .ocbg-meta-chip-btn (the review pass's transition).</summary>
        public const string ChipBtnTransitionCss = "transition: border-color .14s, transform .14s;";

        /// <summary>meta.css:419 .ocbg-meta-chip-btn:hover (the review pass's lift).</summary>
        public const string ChipBtnHoverLiftCss = "transform: translateY(-1px);";

        // ---- meta.css: the .ocbg-meta-scoped rules 出立 and 所持カード share, verbatim ------------------------------------

        /// <summary>meta.css:172 .ocbg-meta .ocbg-meta-grid.</summary>
        public const string GridCss = "position: absolute;";

        /// <summary>meta.css:180 .ocbg-meta .ocbg-meta-cell-row (− ×n ＋ under a card).</summary>
        public const string CellRowCss = "position: absolute; left: 13px; right: 13px; bottom: 0; height: 36px; display: flex; align-items: center; justify-content: space-between;";

        /// <summary>meta.css:181 .ocbg-meta .ocbg-meta-step (the − and ＋ buttons).</summary>
        public const string StepCss = "width: 44px; height: 36px; display: inline-flex; align-items: center; justify-content: center; background: var(--panel-solid); border: var(--stroke-rule) solid var(--steel); border-radius: var(--radius-tag); font: 700 22px/1 var(--font-body); transition: border-color .12s, background .12s;";

        /// <summary>meta.css:182 .ocbg-meta .ocbg-meta-step:hover:not([disabled]).</summary>
        public const string StepHoverCss = "border-color: var(--ink); background: color-mix(in srgb, var(--ink) 8%, var(--panel-solid));";

        /// <summary>meta.css:183 .ocbg-meta .ocbg-meta-step[disabled].</summary>
        public const string StepDisabledCss = "opacity: .28;";

        /// <summary>meta.css:184 .ocbg-meta .ocbg-meta-count.</summary>
        public const string CountCss = "min-width: 44px; text-align: center; font: 700 26px/1 var(--font-mono); color: var(--amber); display: inline-block;";

        /// <summary>meta.css:186 .ocbg-meta .ocbg-meta-pagenav.</summary>
        public const string PageNavCss = "display: flex; align-items: center; justify-content: space-between;";

        /// <summary>meta.css:187 .ocbg-meta .ocbg-meta-nav (前のページ / 次のページ).</summary>
        public const string NavCss = "height: 48px; padding: 0 20px; display: inline-flex; align-items: center; gap: 8px; background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); font: 700 18px/1 var(--font-body); transition: border-color .14s;";

        /// <summary>meta.css:188 .ocbg-meta .ocbg-meta-nav.is-small.</summary>
        public const string NavSmallCss = "height: 40px; padding: 0 14px; font-size: 16px;";

        /// <summary>meta.css:189 .ocbg-meta .ocbg-meta-nav:hover:not([disabled]).</summary>
        public const string NavHoverCss = "border-color: var(--ink);";

        /// <summary>meta.css:190 .ocbg-meta .ocbg-meta-nav[disabled].</summary>
        public const string NavDisabledCss = "opacity: .3;";

        /// <summary>meta.css:191 .ocbg-meta .ocbg-meta-pagetext.</summary>
        public const string PageTextCss = "font: 400 20px/1 var(--font-body);";

        /// <summary>meta.css:193 .ocbg-meta .ocbg-meta-detail.</summary>
        public const string DetailCss = "background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel);";

        /// <summary>meta.css:194 .ocbg-meta .ocbg-meta-detail-hint.</summary>
        public const string DetailHintCss = "position: absolute; left: 0; right: 0; top: 50%; transform: translateY(-50%); text-align: center; font: 400 20px/1.5 var(--font-body);";

        /// <summary>meta.css:195 .ocbg-meta .ocbg-meta-detail-text h3.</summary>
        public const string DetailH3Css = "font: 700 30px/1.2 var(--font-display); letter-spacing: .06em; margin-bottom: var(--space-12);";

        /// <summary>meta.css:196 .ocbg-meta .ocbg-meta-detail-text p.</summary>
        public const string DetailPCss = "font: 400 18px/1.5 var(--font-body); margin-bottom: var(--space-8);";

        /// <summary>meta.css:197 p.is-flavour (the detail's and the owned lines' flavour line).</summary>
        public const string FlavourCss = "font-family: var(--font-display);";

        /// <summary>meta.css:198 .ocbg-meta .ocbg-meta-terms.</summary>
        public const string TermsCss = "display: flex; flex-direction: column; gap: 4px; padding-top: var(--space-12); border-top: var(--stroke-rule) solid var(--rule); max-height: 104px; overflow-y: auto;";

        /// <summary>meta.css:214 .ocbg-meta .ocbg-meta-status.</summary>
        public const string StatusCss = "display: flex; align-items: flex-start; gap: 10px; font: 400 20px/1.45 var(--font-body);";

        /// <summary>meta.css:215 .ocbg-meta .ocbg-meta-status::before.</summary>
        public const string StatusBeforeCss = "content: \"\"; width: 12px; height: 12px; margin-top: 8px; flex: none; border-radius: 50%; border: var(--stroke-frame) solid var(--steel);";

        /// <summary>meta.css:216 .ocbg-meta .ocbg-meta-status.is-ok::before.</summary>
        public const string StatusOkBeforeCss = "background: var(--amber); border-color: var(--amber); box-shadow: 0 0 10px color-mix(in srgb, var(--amber) 60%, transparent);";

        /// <summary>meta.css:334 .ocbg-meta .ocbg-meta-pips.</summary>
        public const string PipsCss = "display: inline-flex; gap: 4px;";

        /// <summary>meta.css:335 .ocbg-meta .ocbg-meta-pips i.</summary>
        public const string PipCss = "width: 12px; height: 12px; border-radius: 50%; border: var(--stroke-frame) solid var(--amber);";

        /// <summary>meta.css:336 .ocbg-meta .ocbg-meta-pips i.is-on.</summary>
        public const string PipOnCss = "background: var(--amber);";

        /// <summary>meta.css:337 .ocbg-meta .ocbg-meta-pips.is-big.</summary>
        public const string PipsBigCss = "gap: 8px;";

        /// <summary>meta.css:338 .ocbg-meta .ocbg-meta-pips.is-big i.</summary>
        public const string PipBigCss = "width: 20px; height: 20px;";

        /// <summary>meta.css:395 .ocbg-meta .ocbg-meta-cell, .ocbg-meta-cell-face.</summary>
        public const string CellCss = "text-align: left;";

        // ---- meta.css: washi, verbatim ------------------------------------------------------------------------------

        /// <summary>
        /// meta.css:268-273 .ocbg-meta-washi. The first image layer, var(--ocbg-washi-tex, none), is the fibre tile
        /// (WashiTile); <see cref="WashiPaintCss"/> is the same rule without it, for the parser.
        /// </summary>
        public const string WashiCss = "background-color: var(--washi); color: var(--washi-ink); background-image: var(--ocbg-washi-tex, none), radial-gradient(ellipse at 30% 18%, color-mix(in srgb, var(--washi) 65%, var(--ink) 35%) 0%, color-mix(in srgb, var(--washi) 0%, transparent) 60%), radial-gradient(ellipse at 78% 92%, color-mix(in srgb, var(--washi-ink) 7%, transparent) 0%, color-mix(in srgb, var(--washi-ink) 0%, transparent) 55%); background-size: 360px 360px, 100% 100%, 100% 100%;";

        /// <summary><see cref="WashiCss"/> without the tile layer (the tile is a RawImage, WashiTile.ApplyWashi).</summary>
        public const string WashiPaintCss = "background-color: var(--washi); color: var(--washi-ink); background-image: radial-gradient(ellipse at 30% 18%, color-mix(in srgb, var(--washi) 65%, var(--ink) 35%) 0%, color-mix(in srgb, var(--washi) 0%, transparent) 60%), radial-gradient(ellipse at 78% 92%, color-mix(in srgb, var(--washi-ink) 7%, transparent) 0%, color-mix(in srgb, var(--washi-ink) 0%, transparent) 55%); background-size: 100% 100%, 100% 100%;";

        /// <summary>meta.css:291 .ocbg-meta-washi-btn.</summary>
        public const string WashiBtnCss = "display: inline-flex; align-items: center; gap: var(--space-16); padding: 16px 30px; border: var(--stroke-frame) solid var(--washi-ink); border-radius: var(--radius-panel); color: var(--washi-ink); font: 700 26px/1 var(--font-display); letter-spacing: .16em; transition: background .14s, transform .14s;";

        /// <summary>meta.css:292 .ocbg-meta-washi-btn small.</summary>
        public const string WashiBtnSmallCss = "font: 400 16px/1 var(--font-mono); letter-spacing: .04em; padding: 4px 8px; border: var(--stroke-rule) solid color-mix(in srgb, var(--washi-ink) 40%, transparent); border-radius: var(--radius-tag);";

        /// <summary>meta.css:293 .ocbg-meta-washi-btn:hover.</summary>
        public const string WashiBtnHoverCss = "background: color-mix(in srgb, var(--washi-ink) 8%, transparent); transform: translateY(-2px);";

        // ---- meta.css: the hall's h2, the text rules ----------------------------------------------------------------

        /// <summary>meta.css:67 .ocbg-meta-hall-h2.</summary>
        public const string H2Css = "font: 700 24px/1 var(--font-display); letter-spacing: .14em; white-space: nowrap; padding-bottom: 12px;";

        /// <summary>meta.css:68 .ocbg-meta-hall-h2::after.</summary>
        public const string H2AfterCss = "content: \"\"; position: absolute; left: 0; bottom: 0; width: 56px; height: 2px; background: var(--amber);";

        /// <summary>meta.css:435 .ocbg-meta p, .ocbg-meta li.</summary>
        public const string PCss = "text-wrap: pretty;";

        /// <summary>meta.css:438 .ocbg-meta p, li, h3.</summary>
        public const string PhraseCss = "word-break: auto-phrase;";

        /// <summary>meta.css:439 .ocbg-meta .ocb-card p, li (the card keeps the DS's wrapping).</summary>
        public const string CardPCss = "word-break: normal; text-wrap: wrap;";

        /// <summary>meta.css:440 .ocbg-meta-hall-line, .ocbg-meta-heir-reason.</summary>
        public const string BalanceCss = "text-wrap: balance;";

        // ---- text ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// A text style inside a meta scene: the stage's .ocb-root (line-height 1.375), .ocbg-meta (body font, ink), then
        /// `css` (the element's rules in cascade order; later declarations win, as the nearest inherited value does).
        /// </summary>
        public static TextStyle Text(string css) => TextStyle.FromCss(CommonStyles.OcbRootCss + MetaCss + (css ?? ""));

        /// <summary>
        /// A p / li / h3 of a meta scene (meta.css:435, :438): Japanese wraps by phrase (word-break: auto-phrase);
        /// balance = text-wrap: balance (meta.css:440).
        /// </summary>
        public static TextStyle P(string css, bool balance = false)
        {
            TextStyle s = Text(css);
            s.AutoPhrase = true;
            s.Balance = balance;
            return s;
        }

        /// <summary>The declarations of a block without the given properties (lower case; "transform" also drops translate / scale / rotate).</summary>
        public static string Without(string css, params string[] props)
        {
            if (string.IsNullOrEmpty(css) || props == null || props.Length == 0) return css ?? "";
            var drop = new HashSet<string>(props, StringComparer.Ordinal);
            if (drop.Contains("transform")) { drop.Add("translate"); drop.Add("scale"); drop.Add("rotate"); }
            var sb = new StringBuilder();
            int depth = 0, start = 0;
            for (int i = 0; i <= css.Length; i++)
            {
                char c = i < css.Length ? css[i] : ';';
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ';' && depth <= 0)
                {
                    string d = css.Substring(start, i - start);
                    int colon = d.IndexOf(':');
                    string name = colon > 0 ? d.Substring(0, colon).Trim().ToLowerInvariant() : null;
                    if (name != null && !drop.Contains(name)) sb.Append(d.Trim()).Append("; ");
                    start = i + 1;
                }
            }
            return sb.ToString();
        }

        // ---- builders -----------------------------------------------------------------------------------------------

        /// <summary>
        /// .ocbg-meta-fade over the whole stage (place(h('div', { class: 'ocbg-meta-fade' }), 0, 0, 1920, 1080)): --ground,
        /// z-index 50, no pointer events. Tween it with Styler.Of(fade, o: 1).
        /// </summary>
        public static RectTransform Fade(RectTransform parent, float x = 0f, float y = 0f, float w = MetaUtil.W, float h = MetaUtil.H)
        {
            BoxGraphic g = BoxGraphic.Make(parent, FadeCss, x, y, w, h, "ocbg-meta-fade");
            UiNode n = UiNode.Of(g.rectTransform);
            n.Z = 50;
            n.PointerEvents = PointerEvents.None;
            return g.rectTransform;
        }

        /// <summary>.ocbg-meta-shade (light: .is-light, deep: .is-deep) over the stage; it takes the pointer (the overlays close on its click).</summary>
        public static BoxGraphic Shade(RectTransform parent, bool light = false, bool deep = false, Action onClick = null)
        {
            BoxGraphic g = BoxGraphic.Make(parent, ShadeCss + (light ? ShadeLightCss : "") + (deep ? ShadeDeepCss : ""), 0f, 0f, MetaUtil.W, MetaUtil.H, "ocbg-meta-shade");
            ClickArea area = ClickArea.On(g);
            if (onClick != null) area.Click += _ => onClick();
            return g;
        }

        /// <summary>.ocbg-meta-panel (panel-solid, 1px steel, radius 6, shadow-tip) at (x, y), w × h.</summary>
        public static BoxGraphic Panel(RectTransform parent, float x, float y, float w, float h, string extraCss = null) =>
            BoxGraphic.Make(parent, PanelCss + (extraCss ?? ""), x, y, w, h, "ocbg-meta-panel");

        /// <summary>
        /// .ocbg-meta-keys at (x, y): 400 16px/22px body, .06em, ink, nowrap. w null = as wide as the text; right =
        /// .is-right (text-align: right, in the w given).
        /// </summary>
        public static TextMeshProUGUI Keys(RectTransform parent, string text, float x, float y, float? w = null, bool right = false)
        {
            TextStyle s = Text(KeysCss + (right ? KeysRightCss : ""));
            if (right) s.Align = TextAlign.Right;
            return Label.Make(parent, text ?? "", s, x, y, w, null, "ocbg-meta-keys");
        }

        /// <summary>.ocbg-meta-dot: a 10×10 steel circle at (x, y); extraCss recolours it (the hall's lit dot: background wick + glow).</summary>
        public static BoxGraphic Dot(RectTransform parent, float x, float y, string extraCss = null)
        {
            BoxGraphic g = BoxGraphic.Make(parent, DotCss + (extraCss ?? ""), x, y, 10f, 10f, "ocbg-meta-dot");
            Ocb.NoShrink(g);
            return g;
        }

        /// <summary>
        /// The hall's h2 (.ocbg-meta-hall-h2) at (x, y): 700 24px/1 display, .14em, nowrap, padding-bottom 12 (36 tall),
        /// with its ::after, a 56×2 amber bar at the bottom left. Returns the h2 element (riseIn moves it); its label is
        /// the first child.
        /// </summary>
        public static RectTransform H2(RectTransform parent, string text, float x, float y)
        {
            TextStyle s = Text(H2Css);
            float lh = s.LineHeightPx;
            float pad = 12f;
            float w = TextMeasure.Width(text ?? "", s);
            RectTransform h2 = Ui.Div(parent, "ocbg-meta-hall-h2", x, y, w, lh + pad);
            UiNode.Of(h2);
            Label.Make(h2, text ?? "", s, 0f, 0f, null, null, "text");
            BoxGraphic.Make(h2, "background: var(--amber)", 0f, lh + pad - 2f, 56f, 2f, "after");
            return h2;
        }

        /// <summary>
        /// An overflow-y: auto box of a meta scene (the kit's ScrollArea) at (x, y), w × h, with the meta scrollbar of
        /// meta.css:24-26: 8 px wide, no track, the thumb steel with radius 4 (thumbCss is appended to the thumb's rule:
        /// the journal's `background: color-mix(in srgb, var(--washi-ink) 30%, transparent)`, meta.css:378). thumb false
        /// hides it: the headless reference shots draw no scrollbars (headless Chrome hides them), a desktop Chrome does.
        /// Put the rows in its Content.
        /// </summary>
        public static ScrollArea Scroll(RectTransform parent, float x, float y, float w, float h, string name = null, bool thumb = true, string thumbCss = null)
        {
            ScrollArea a = ScrollArea.Make(parent, name ?? "scroll", x, y, w, h);
            a.ShowThumb = thumb;
            a.ThumbWidth = 8f;
            BoxGraphic.On(a.Thumb, ScrollbarThumbCss + (thumbCss ?? ""));
            return a;
        }

        /// <summary>
        /// .ocbg-meta-washi on a new element at (x, y), w × h: the --washi fill, its two radial gradients and the fibre tile
        /// (WashiTile), with the element's own box declarations (extraCss: radius, borders, shadows) painted in CSS order —
        /// outer shadows and the background under the tile, inset shadows and borders over it. Add the content after.
        /// </summary>
        public static WashiBox Washi(RectTransform parent, float x, float y, float w, float h, string extraCss = null, string name = null)
        {
            RectTransform root = Ui.Div(parent, name ?? "ocbg-meta-washi", x, y, w, h);
            var box = new WashiBox { Root = root, Node = UiNode.Of(root), Css = WashiPaintCss + (extraCss ?? "") };
            BoxStyle full = global::Trial.Geometry.Css.Box(box.Css);
            BoxStyle under = full.Clone();
            under.Shadows = full.Shadows.FindAll(s => !s.Inset);
            under.Top = under.Top.WithColor(Rgba.Transparent);
            under.Right = under.Right.WithColor(Rgba.Transparent);
            under.Bottom = under.Bottom.WithColor(Rgba.Transparent);
            under.Left = under.Left.WithColor(Rgba.Transparent);
            under.Outline = global::Trial.Geometry.Outline.None;
            box.Under = BoxGraphic.Make(root, under, 0f, 0f, w, h, "washi");
            box.Tex = WashiTile.ApplyWashi(root, w, h, full.BorderWidths);
            BoxStyle over = full.Clone();
            over.BackgroundColor = Rgba.Transparent;
            over.Backgrounds.Clear();
            over.Shadows = full.Shadows.FindAll(s => s.Inset);
            if (!over.IsEmpty) box.Over = BoxGraphic.Make(root, over, 0f, 0f, w, h, "washi-edge");
            box.Border = full.BorderWidths;
            return box;
        }
    }

    /// <summary>A washi element (MetaStyle.Washi): its root and the three paint layers under the content.</summary>
    public sealed class WashiBox
    {
        public RectTransform Root;
        public UiNode Node;

        /// <summary>The composed declarations (WashiPaintCss + the element's own).</summary>
        public string Css;

        /// <summary>Outer shadows, the --washi fill and its two gradients.</summary>
        public BoxGraphic Under;

        /// <summary>The fibre tile.</summary>
        public RawImage Tex;

        /// <summary>Inset shadows and borders (null when the element has neither).</summary>
        public BoxGraphic Over;

        public Insets Border;

        /// <summary>The element's new size (the ending card grows with its lines): every layer follows.</summary>
        public void Resize(float w, float h)
        {
            if (!Root) return;
            Node.W = w;
            Node.H = h;
            if (Under) UiNode.Of(Under.rectTransform).Place(0f, 0f, w, h);
            if (Tex)
            {
                Ui.Place(Tex.rectTransform, 0f, 0f, w, h);
                WashiTile.SetTile(Tex, w, h, Border);
            }
            if (Over) UiNode.Of(Over.rectTransform).Place(0f, 0f, w, h);
        }
    }

    /// <summary>The kinds of meta button (meta.css): .ocbg-meta-primary, -btn, -chip-btn, -washi-btn, -x, -step (− ＋), -nav (page turns).</summary>
    public enum MetaButtonKind { Primary, Btn, Chip, Washi, X, Step, Nav }

    /// <summary>
    /// A meta button (`button.ocbg-meta-primary` etc.): its box, an optional icon, the label (span) and the small tag,
    /// laid out by the rule's flex (centred, gap); the states :hover (pointer), [disabled], :focus-visible (Tab) and any
    /// extra class, with the rule's transitions (CssStates on the document clock). Click is onclick (nothing while
    /// disabled). A Styler on the button is an inline style: it wins over the rules' opacity and transform, as in CSS —
    /// make it with <see cref="Styler"/> (or set InlineOpacity / InlineTransform), and clear the transform with
    /// <see cref="ClearTransform"/> (el.style.transform = '').
    /// </summary>
    public sealed class MetaButton
    {
        public MetaButtonKind Kind { get; private set; }
        public RectTransform Root { get; private set; }
        public UiNode Node { get; private set; }
        public BoxGraphic Box { get; private set; }
        public CssStates States { get; private set; }
        public ClickArea Area { get; private set; }

        /// <summary>The icon before the label (chip-btn, x), or null.</summary>
        public PathGraphic IconGraphic { get; private set; }

        /// <summary>The label (the span), or null for an icon-only button.</summary>
        public TextMeshProUGUI Label { get; private set; }

        /// <summary>The small tag (「Esc」「Enter」), or null.</summary>
        public RectTransform Small { get; private set; }
        public TextMeshProUGUI SmallLabel { get; private set; }

        /// <summary>onclick.</summary>
        public event Action Click;

        private string _base, _hover, _disabledCss, _textCss, _smallCss;
        private bool _hoverWhenDisabled;
        private readonly List<(string Name, string Css)> _extra = new List<(string, string)>();
        private readonly HashSet<string> _extraOn = new HashSet<string>(StringComparer.Ordinal);
        private bool _hovered, _disabled, _focus, _inlineO, _inlineT;
        private float? _w, _h;

        // ---- makers -----------------------------------------------------------------------------------------------

        /// <summary>
        /// A meta button under parent at (0, 0): text (null for an icon-only one), small (the tag, or null), icon (a DS
        /// icon before the text — after it with iconAfter, as 「次のページ →」 — or null) at iconSize in iconColor (null =
        /// ink). compact = the kind's smaller class (.ocbg-meta-primary.is-compact, .ocbg-meta-nav.is-small), square =
        /// .ocbg-meta-chip-btn.is-square. Sized to its content until SetBox gives it a box. The rules' width / height are
        /// layout (SetBox, Layout), never transitioned: .ocbg-meta-btn's `height: 100%` is the h its row gives it.
        /// </summary>
        public static MetaButton Make(RectTransform parent, MetaButtonKind kind, string text, string small = null, string icon = null,
            float iconSize = 24f, Rgba? iconColor = null, bool compact = false, bool square = false, string name = null, bool iconAfter = false)
        {
            var b = new MetaButton { Kind = kind };
            switch (kind)
            {
                case MetaButtonKind.Primary:
                    b._base = MetaStyle.ButtonCss + MetaStyle.PrimaryCss + (compact ? MetaStyle.PrimaryCompactCss : "");
                    b._hover = MetaStyle.PrimaryHoverCss;
                    b._disabledCss = MetaStyle.ButtonDisabledCss + MetaStyle.PrimaryDisabledCss;
                    b._textCss = MetaStyle.PrimaryCss + (compact ? MetaStyle.PrimaryCompactCss : "");
                    b._smallCss = MetaStyle.PrimarySmallCss;
                    break;
                case MetaButtonKind.Btn:
                    b._base = MetaStyle.ButtonCss + MetaStyle.BtnCss;
                    b._hover = MetaStyle.BtnHoverCss;
                    b._disabledCss = MetaStyle.ButtonDisabledCss;
                    b._textCss = MetaStyle.BtnCss;
                    b._smallCss = "";
                    break;
                case MetaButtonKind.Chip:
                    b._base = MetaStyle.ButtonCss + MetaStyle.ChipBtnCss + (square ? MetaStyle.ChipBtnSquareCss : "") + MetaStyle.ChipBtnTransitionCss;
                    b._hover = MetaStyle.ChipBtnHoverCss + MetaStyle.ChipBtnHoverLiftCss;
                    b._hoverWhenDisabled = true;   // .ocbg-meta-chip-btn:hover has no :not([disabled])
                    b._disabledCss = MetaStyle.ButtonDisabledCss;
                    b._textCss = MetaStyle.ChipBtnCss;
                    b._smallCss = MetaStyle.ChipBtnSmallCss;
                    break;
                case MetaButtonKind.Step:
                    b._base = MetaStyle.ButtonCss + MetaStyle.StepCss;
                    b._hover = MetaStyle.StepHoverCss;
                    b._disabledCss = MetaStyle.ButtonDisabledCss + MetaStyle.StepDisabledCss;
                    b._textCss = MetaStyle.StepCss;
                    b._smallCss = "";
                    break;
                case MetaButtonKind.Nav:
                    b._base = MetaStyle.ButtonCss + MetaStyle.NavCss + (compact ? MetaStyle.NavSmallCss : "");
                    b._hover = MetaStyle.NavHoverCss;
                    b._disabledCss = MetaStyle.ButtonDisabledCss + MetaStyle.NavDisabledCss;
                    b._textCss = MetaStyle.NavCss + (compact ? MetaStyle.NavSmallCss : "");
                    b._smallCss = "";
                    break;
                case MetaButtonKind.Washi:
                    b._base = MetaStyle.ButtonCss + MetaStyle.WashiBtnCss;
                    b._hover = MetaStyle.WashiBtnHoverCss;
                    b._hoverWhenDisabled = true;   // .ocbg-meta-washi-btn:hover has no :not([disabled])
                    b._disabledCss = MetaStyle.ButtonDisabledCss;
                    b._textCss = MetaStyle.WashiBtnCss;
                    b._smallCss = MetaStyle.WashiBtnSmallCss;
                    break;
                default:
                    b._base = MetaStyle.ButtonCss + MetaStyle.XCss;
                    b._hover = MetaStyle.XHoverCss;
                    b._hoverWhenDisabled = true;   // .ocbg-meta-x:hover
                    b._disabledCss = MetaStyle.ButtonDisabledCss;
                    b._textCss = MetaStyle.XCss;
                    b._smallCss = "";
                    if (icon == null) { icon = "exile"; iconSize = 22f; }
                    break;
            }
            b.Root = Ui.Div(parent, name ?? "ocbg-meta-" + KindName(kind));
            b.Node = UiNode.Of(b.Root);
            b.Box = BoxGraphic.On(b.Root);
            if (icon != null && !iconAfter) b.IconGraphic = Ocb.Icon(b.Root, icon, iconSize, iconColor ?? DsTokens.Ink);
            if (text != null)
            {
                TextStyle ts = MetaStyle.Text(b._textCss);
                ts.WhiteSpace = WhiteSpace.NoWrap;
                b.Label = global::Trial.Kit.Label.Make(b.Root, text, ts, "span");
            }
            if (icon != null && iconAfter) b.IconGraphic = Ocb.Icon(b.Root, icon, iconSize, iconColor ?? DsTokens.Ink);
            if (small != null) b.MakeSmall(small);
            b.Area = ClickArea.On(b.Box);
            b.Area.Click += _ => b.Press();
            b.Area.HoverChanged += on => b.SetHover(on);
            b.States = CssStates.Of(b.Root);
            b.Restyle(instant: true);
            b.Layout();
            return b;
        }

        private static string KindName(MetaButtonKind k) =>
            k == MetaButtonKind.Primary ? "primary" : k == MetaButtonKind.Btn ? "btn" : k == MetaButtonKind.Chip ? "chip-btn" : k == MetaButtonKind.Washi ? "washi-btn"
            : k == MetaButtonKind.Step ? "step" : k == MetaButtonKind.Nav ? "nav" : "x";

        private void MakeSmall(string text)
        {
            BoxStyle sb = global::Trial.Geometry.Css.Box(_smallCss);
            Insets bw = sb.BorderWidths, pad = sb.Padding;
            TextStyle ss = MetaStyle.Text(_textCss + _smallCss);
            ss.WhiteSpace = WhiteSpace.NoWrap;
            float tw = TextMeasure.Width(text, ss);
            Small = Ui.Div(Root, "small", 0f, 0f, tw + pad.Horizontal + bw.Horizontal, ss.LineHeightPx + pad.Vertical + bw.Vertical);
            UiNode.Of(Small);
            if (!sb.IsEmpty) BoxGraphic.On(Small).Style = sb;
            SmallLabel = global::Trial.Kit.Label.Make(Small, text, ss, bw.Left + pad.Left, bw.Top + pad.Top, null, null, "text");
            Ocb.NoShrink(Small);
        }

        /// <summary>.ocbg-meta-primary at (x, y), w × h (w null: as wide as its content): the text and the small tag.</summary>
        public static MetaButton Primary(RectTransform parent, string text, string small, float x, float y, float? w, float? h, bool compact = false)
        {
            MetaButton b = Make(parent, MetaButtonKind.Primary, text, small, compact: compact);
            b.SetBox(x, y, w, h);
            return b;
        }

        /// <summary>.ocbg-meta-btn at (x, y), w × h.</summary>
        public static MetaButton Btn(RectTransform parent, string text, float x, float y, float? w, float? h)
        {
            MetaButton b = Make(parent, MetaButtonKind.Btn, text);
            b.SetBox(x, y, w, h);
            return b;
        }

        /// <summary>.ocbg-meta-chip-btn at (x, y), w × h: the text and the small tag (「設定」 + 「Esc」).</summary>
        public static MetaButton Chip(RectTransform parent, string text, string small, float x, float y, float? w, float? h)
        {
            MetaButton b = Make(parent, MetaButtonKind.Chip, text, small);
            b.SetBox(x, y, w, h);
            return b;
        }

        /// <summary>.ocbg-meta-chip-btn with an icon and the text (「← タイトルへ」: back 20 ink).</summary>
        public static MetaButton ChipIconText(RectTransform parent, string icon, float iconSize, string text, float x, float y, float? w, float? h, Rgba? iconColor = null)
        {
            MetaButton b = Make(parent, MetaButtonKind.Chip, text, null, icon, iconSize, iconColor);
            b.SetBox(x, y, w, h);
            return b;
        }

        /// <summary>.ocbg-meta-chip-btn.is-square with an icon alone (the journal button: journal 30 ink).</summary>
        public static MetaButton ChipIcon(RectTransform parent, string icon, float iconSize, float x, float y, float? w, float? h, Rgba? iconColor = null)
        {
            MetaButton b = Make(parent, MetaButtonKind.Chip, null, null, icon, iconSize, iconColor, square: true);
            b.SetBox(x, y, w, h);
            return b;
        }

        /// <summary>.ocbg-meta-washi-btn (inline-flex, sized to its content): the text and the small tag.</summary>
        public static MetaButton Washi(RectTransform parent, string text, string small) => Make(parent, MetaButtonKind.Washi, text, small);

        /// <summary>.ocbg-meta-step (「−」 or 「＋」, 44×36) at (x, y).</summary>
        public static MetaButton Step(RectTransform parent, string text, float x, float y)
        {
            MetaButton b = Make(parent, MetaButtonKind.Step, text);
            b.Place(x, y);
            return b;
        }

        /// <summary>
        /// .ocbg-meta-nav (small: .is-small) at (x, y): 「← 前のページ」 (back before the text) or 「次のページ →」 (next: forward
        /// after it); the icon 20 (18 when small) ink.
        /// </summary>
        public static MetaButton Nav(RectTransform parent, string text, bool next, float x, float y, bool small = false)
        {
            MetaButton b = Make(parent, MetaButtonKind.Nav, text, null, next ? "forward" : "back", small ? 18f : 20f, null, compact: small, iconAfter: next);
            b.Place(x, y);
            return b;
        }

        /// <summary>.ocbg-meta-x at (x, y): 44×44 with exile 22 ink.</summary>
        public static MetaButton X(RectTransform parent, float x, float y)
        {
            MetaButton b = Make(parent, MetaButtonKind.X, null);
            b.Place(x, y);
            return b;
        }

        // ---- states -----------------------------------------------------------------------------------------------

        /// <summary>[disabled]: no clicks, no :hover (except chip / washi / x, whose :hover has no :not([disabled])).</summary>
        public bool Disabled
        {
            get => _disabled;
            set
            {
                if (_disabled == value) return;
                _disabled = value;
                if (Focusable != null) Focusable.Disabled = value;
                Restyle();
            }
        }

        public bool Hovered => _hovered;

        private void SetHover(bool on)
        {
            _hovered = on;
            Restyle();
        }

        /// <summary>:focus-visible (the Tab focus ring: outline 2px wick, offset 3px).</summary>
        public void SetFocusRing(bool on)
        {
            _focus = on;
            Restyle();
        }

        /// <summary>
        /// An extra class of the button (the settings footer's .is-focus, …): its declarations, registered after the
        /// button's own rules (later wins), on or off.
        /// </summary>
        public void SetClass(string name, string css, bool on)
        {
            int i = _extra.FindIndex(e => e.Name == name);
            if (i < 0) _extra.Add((name, css ?? ""));
            else _extra[i] = (name, css ?? _extra[i].Css);
            if (on) _extraOn.Add(name); else _extraOn.Remove(name);
            Restyle();
        }

        /// <summary>The scene holds an inline opacity on the button (a Styler): the rules' opacity no longer applies.</summary>
        public bool InlineOpacity
        {
            get => _inlineO;
            set { if (_inlineO == value) return; _inlineO = value; Restyle(instant: true); }
        }

        /// <summary>The scene holds an inline transform on the button (a Styler): the rules' transform (the :hover lift) no longer applies.</summary>
        public bool InlineTransform
        {
            get => _inlineT;
            set { if (_inlineT == value) return; _inlineT = value; Restyle(instant: true); }
        }

        /// <summary>styler(button, init): a Styler on the button's node; its opacity and transform are inline from now on.</summary>
        public global::Trial.Kit.Styler Styler(float o = 1f, float x = 0f, float y = 0f, float s = 1f, float r = 0f)
        {
            _inlineO = true;
            _inlineT = true;
            Restyle(instant: true);
            return global::Trial.Kit.Styler.Of(Node, o, x, y, s, r);
        }

        /// <summary>el.style.transform = '': the inline transform goes and the rules' transform (the :hover lift) applies again.</summary>
        public void ClearTransform()
        {
            if (!Node) return;
            Node.ResetTransform();
            _inlineT = false;
            Restyle(instant: true);
        }

        // What the state machine manages: the rules without their width / height (layout's) and without what an inline
        // style holds (a Styler's opacity / transform win over the rules, as in CSS).
        private string F(string css)
        {
            if (_inlineO && _inlineT) return MetaStyle.Without(css, "width", "height", "opacity", "transform");
            if (_inlineO) return MetaStyle.Without(css, "width", "height", "opacity");
            if (_inlineT) return MetaStyle.Without(css, "width", "height", "transform");
            return MetaStyle.Without(css, "width", "height");
        }

        private void Restyle(bool instant = false)
        {
            if (!Root || States == null) return;
            States.Base(F(_base));
            States.State(":focus-visible", F(MetaStyle.ButtonFocusVisibleCss), _focus);
            States.State(":hover", F(_hover), _hovered && (_hoverWhenDisabled || !_disabled));
            States.State("[disabled]", F(_disabledCss), _disabled);
            foreach ((string name, string css) in _extra) States.State(name, F(css), _extraOn.Contains(name));
            States.Apply(instant);
        }

        // ---- press, focus, layout ---------------------------------------------------------------------------------

        /// <summary>b.click(): onclick runs unless the button is disabled.</summary>
        public void Press()
        {
            if (_disabled || !Root) return;
            try { Click?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            Microtasks.Drain();
        }

        /// <summary>Its entry in a Tab scope (null until FocusIn).</summary>
        public Focusable Focusable { get; private set; }

        /// <summary>Adds the button to a Tab scope (DOM order: call in the order the buttons stand).</summary>
        public Focusable FocusIn(FocusScope scope)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            Focusable = scope.Add(Root, Press, SetFocusRing);
            Focusable.Disabled = _disabled;
            return Focusable;
        }

        /// <summary>place(button, x, y, w, h): w / h null keep the content's size on that axis.</summary>
        public void SetBox(float x, float y, float? w, float? h)
        {
            _w = w;
            _h = h;
            Node.Place(x, y);
            Layout();
        }

        /// <summary>place(button, x, y): the size stays.</summary>
        public void Place(float x, float y)
        {
            Node.X = x;
            Node.Y = y;
        }

        /// <summary>The content laid out by the rule's flex (padding, border, gap, centring); the box sized to the content where no size was given.</summary>
        public void Layout()
        {
            if (!Root) return;
            // the box given, else the rule's px size (x 44×44, step 44×36, compact height 56), else the content's
            CssBlock rule = Ocb.Rule(_base);
            float? w = _w ?? Ocb.Px(rule.Layout.Width), h = _h ?? Ocb.Px(rule.Layout.Height);
            if (w.HasValue) Node.W = w.Value;
            if (h.HasValue) Node.H = h.Value;
            Ocb.Flow(Root, _base, null, w, h);
        }

        /// <summary>The button's text (relaid out).</summary>
        public string Text
        {
            get => Label ? Label.text : null;
            set
            {
                if (!Label) return;
                global::Trial.Kit.Label.SetText(Label, value ?? "");
                Layout();
            }
        }

        public void Remove()
        {
            if (Root) Ui.Remove(Root);
        }
    }

    /// <summary>
    /// The HTML title attribute (a tooltip while the pointer rests on an element): after 500 ms of real time a small tip
    /// (the DS tooltip's look: panel-solid, 1px steel, radius 4, shadow-tip, 400 16px/1.4 body, pre-line) shows near the
    /// pointer on a layer of its own above every scene; it goes when the pointer leaves or the element goes. The text
    /// may depend on where the pointer is (a term inside a label): each stretch with its own title acts as the browser's
    /// own element — moving onto it starts the delay, moving within it keeps a shown tip where it is, moving onto
    /// another title (or none) hides the tip and starts again.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class TitleTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        /// <summary>How long the pointer rests before the tip shows (ms, real time).</summary>
        public const float DelayMs = 500f;

        public const string TipCss = "background: var(--panel-solid); border: 1px solid var(--steel); border-radius: 4px; box-shadow: var(--shadow-tip); padding: 6px 10px; font: 400 16px/1.4 var(--font-body); color: var(--ink); white-space: pre-line;";

        /// <summary>The widest a tip grows before it wraps (px).</summary>
        public const float MaxW = 480f;

        private static RectTransform _layer, _tip;
        private static BoxGraphic _tipBox;
        private static TextMeshProUGUI _tipText;
        private static TitleTip _owner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _layer = null;
            _tip = null;
            _tipBox = null;
            _tipText = null;
            _owner = null;
        }

        /// <summary>The text at the pointer (null or empty: none).</summary>
        public Func<PointerEventData, string> TextAt;

        private bool _over;
        private int _token;
        private string _target;   // the title under the pointer now (null: none)
        private PointerEventData _last;

        /// <summary>A fixed title on the element (it becomes a pointer target).</summary>
        public static TitleTip On(Component element, string title) => On(element, _ => title);

        /// <summary>A title that depends on where the pointer is (it becomes a pointer target).</summary>
        public static TitleTip On(Component element, Func<PointerEventData, string> textAt)
        {
            if (!element) throw new ArgumentNullException(nameof(element));
            GameObject go = element.gameObject;
            TitleTip t = go.GetComponent<TitleTip>();
            if (!t) t = go.AddComponent<TitleTip>();
            t.TextAt = textAt;
            Graphic g = go.GetComponent<Graphic>();
            if (g) g.raycastTarget = true;
            else Hit.Area((RectTransform)go.transform);
            return t;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _over = true;
            Retarget(e);
        }

        public void OnPointerMove(PointerEventData e)
        {
            if (_over) Retarget(e);
        }

        public void OnPointerExit(PointerEventData e)
        {
            _over = false;
            _target = null;
            _token++;
            Hide();
        }

        private void OnDisable()
        {
            _over = false;
            _target = null;
            _token++;
            Hide();
        }

        // The title under the pointer changed (onto a term, off it, onto another): the shown tip goes and the delay
        // starts again for the new one. The same title: nothing changes (a shown tip does not follow the pointer).
        private void Retarget(PointerEventData e)
        {
            _last = e;
            string t = null;
            try { t = TextAt?.Invoke(e); }
            catch (Exception ex) { Debug.LogException(ex); }
            if (string.IsNullOrEmpty(t)) t = null;
            if (t == _target) return;
            _target = t;
            int token = ++_token;
            Hide();
            if (t == null) return;
            CssTime.Clock.WaitReal(DelayMs).Then(() => { if (this && _over && token == _token) Show(); });
        }

        private void Show()
        {
            string text = _target;
            if (string.IsNullOrEmpty(text)) { Hide(); return; }
            TrialStage st = TrialStage.Current;
            if (!st) return;
            if (!_layer)
            {
                _layer = st.Layer("ocbg-title-tips", 950, raycasts: false);
                _tip = Ui.Div(_layer, "title-tip");
                UiNode.Of(_tip);
                _tipBox = BoxGraphic.On(_tip, TipCss);
                _tipText = Label.Make(_tip, "", MetaStyle.Text(TipCss), 0f, 0f, null, null, "text");
            }
            _owner = this;
            _tip.gameObject.SetActive(true);
            TextStyle s = Label.StyleOf(_tipText) ?? MetaStyle.Text(TipCss);
            BoxStyle b = _tipBox.Style;
            Insets bw = b.BorderWidths, pad = b.Padding;
            float tw = Math.Min(MaxW, TextMeasure.WidestLine(text, s, MaxW));
            float th = TextMeasure.Height(text, s, tw + 0.01f);
            float w = tw + pad.Horizontal + bw.Horizontal, h = th + pad.Vertical + bw.Vertical;
            Label.SetText(_tipText, text);
            Ui.Place(_tipText.rectTransform, bw.Left + pad.Left, bw.Top + pad.Top, tw + 0.01f, th);
            Vector2 at = _last != null ? st.ToStage(_last.position) : new Vector2(MetaUtil.W / 2f, MetaUtil.H / 2f);
            float x = Mathf.Clamp(at.x + 12f, 4f, MetaUtil.W - w - 4f);
            float y = at.y + 22f;
            if (y + h > MetaUtil.H - 4f) y = at.y - h - 8f;
            UiNode.Of(_tip).Place(x, y, w, h);
        }

        private void Hide()
        {
            if (_owner != this) return;
            _owner = null;
            if (_tip) _tip.gameObject.SetActive(false);
        }
    }
}
