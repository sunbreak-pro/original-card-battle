// The HUD pieces (vendor/ds/bundle.js:545-573, bundle.css:168-181) and the classes the battle builds its list
// drawer and its result card from (bundle.css:192-206: .ocb-drawer*, .ocb-end, .ocb-end-paper; the DS has no builder
// for them in the game's set, so these follow the DS's own markup: PileDrawer's rows, mountBattle's end paper).
// The buttons (speed, journal, end turn) take the pointer: :hover from a ClickArea, Click for onclick.
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>A built DS button (role="button"): its :hover and its clicks.</summary>
    public sealed class OcbButton : OcbElement
    {
        /// <summary>The pointer listener over the button's box.</summary>
        public ClickArea Area { get; internal set; }
        public TextMeshProUGUI Label { get; internal set; }
        /// <summary>The element's title (the browser's tooltip); not drawn.</summary>
        public string Title { get; internal set; }
        /// <summary>onclick.</summary>
        public event Action Click;

        internal Func<bool, string> CssOf;
        internal bool Opacity;
        private bool _hover;

        /// <summary>:hover.</summary>
        public bool Hover
        {
            get => _hover;
            set
            {
                _hover = value;
                Restate();
            }
        }

        internal void Restate()
        {
            if (Root && CssOf != null) Ocb.Restyle(Root, CssOf(_hover), null, Opacity);
        }

        // Each listener on its own, the microtasks drained after each (the browser runs them after every event listener).
        internal void Press()
        {
            Action handlers = Click;
            if (handlers == null) return;
            foreach (Delegate d in handlers.GetInvocationList())
            {
                try { ((Action)d)(); }
                catch (Exception e) { Debug.LogException(e); }
                Microtasks.Drain();
            }
        }
    }

    /// <summary>A built end turn button: .is-off on top of the button.</summary>
    public sealed class OcbEndTurn
    {
        public OcbButton Button { get; internal set; }
        private bool _off;

        /// <summary>.is-off: opacity --opacity-enemy-turn (the enemy's turn).</summary>
        public bool Off
        {
            get => _off;
            set
            {
                _off = value;
                Button.Restate();
            }
        }

        internal bool OffState => _off;
    }

    /// <summary>The DS's end screen: the shade and the washi paper.</summary>
    public sealed class OcbEnd : OcbElement
    {
        public OcbElement Paper { get; internal set; }
        public TextMeshProUGUI Title { get; internal set; }
        public List<TextMeshProUGUI> Paragraphs { get; } = new List<TextMeshProUGUI>();
        /// <summary>The paper's button (null without one).</summary>
        public OcbButton Button { get; internal set; }
    }

    public static partial class Ocb
    {
        private static OcbButton Button(RectTransform parent, string name, Func<bool, string> cssOf, bool opacity = false)
        {
            var b = Make<OcbButton>(parent, name, cssOf(false));
            b.CssOf = cssOf;
            b.Opacity = opacity;
            b.Area = ClickArea.On(b.Box ? (Component)b.Box : b.Root);
            b.Area.HoverChanged += on => b.Hover = on;
            b.Area.Click += _ => b.Press();
            return b;
        }

        // js: vendor/ds/bundle.js:548
        /// <summary>
        /// CornerInfo(d): .ocb-panel.ocb-corner, 44 tall, padding 0 16, inline-flex centred, gap 18, 600 20/1 mono, nowrap:
        /// hourglass 22 + the turn, stairs 22 + the floor, miasma 22 (phosphor) + 「n%」 (each a span, gap 6). At (0, 0).
        /// </summary>
        public static OcbElement CornerInfo(RectTransform parent, string turn, string floor, string miasma)
        {
            string css = PanelCss + CornerCss;
            OcbElement el = Make<OcbElement>(parent, "ocb-corner", css);
            TextStyle ts = TextOf(css + CornerSpanCss);
            void Item(string key, string icon, Rgba color, string text)
            {
                RectTransform span = El(el.Root, key, CornerSpanCss);
                Icon(span, icon, 22f, color);
                el.SetPart(key, Text(span, text, ts, "text"));
                Flow(span, CornerSpanCss);
            }
            Item("turn", "hourglass", DsTokens.Ink, turn);
            Item("floor", "stairs", DsTokens.Ink, floor);
            Item("miasma", "miasma", DsTokens.Phosphor, miasma + "%");
            Flow(el.Root, css);
            return el;
        }

        /// <summary>The speed toggle's label (bundle.js:557): 「×1.0」 for 1, else 「×」 + String(speed).</summary>
        public static string SpeedText(float speed) => "×" + (speed == 1f ? "1.0" : Num(speed));

        // js: vendor/ds/bundle.js:554
        /// <summary>
        /// SpeedToggle(speed): .ocb-panel.ocb-speed 120×56 (padding 0 12, flex centred, gap 8, 600 20/1 mono, pointer):
        /// speed1/2/3 24 (×1.5 and up: 3, ×1.25 and up: 2) and 「×1.0 / ×1.25 / ×1.5」. :hover: ink border, rgba(40,44,58,.92).
        /// </summary>
        public static OcbButton SpeedToggle(RectTransform parent, float speed)
        {
            int n = speed >= 1.5f ? 3 : speed >= 1.25f ? 2 : 1;
            OcbButton b = Button(parent, "ocb-speed", hover => PanelCss + SpeedCss + (hover ? SpeedHoverCss : ""));
            b.Title = "戦闘の速さ。押すたびに ×1.0 → ×1.25 → ×1.5 と巡ります";
            b.SetPart("icon", Icon(b.Root, "speed" + n.ToString(System.Globalization.CultureInfo.InvariantCulture), 24f, DsTokens.Ink));
            b.Label = Text(b.Root, SpeedText(speed), TextOf(PanelCss + SpeedCss), "text");
            Flow(b.Root, PanelCss + SpeedCss);
            return b;
        }

        // js: vendor/ds/bundle.js:559
        /// <summary>JournalButton(): .ocb-panel.ocb-btn-sq 56×56 with `journal` 30 (ink) centred; :hover as the speed toggle.</summary>
        public static OcbButton JournalButton(RectTransform parent)
        {
            OcbButton b = Button(parent, "ocb-btn-sq", hover => PanelCss + BtnSqCss + (hover ? SpeedHoverCss : ""));
            b.Title = "手記";
            b.SetPart("icon", Icon(b.Root, "journal", 30f, DsTokens.Ink));
            Flow(b.Root, PanelCss + BtnSqCss);
            return b;
        }

        // js: vendor/ds/bundle.js:560
        /// <summary>
        /// PlayedAttrs(counts): .ocb-attrs (flex, gap 6) of the four attributes: each .ocb-panel.ocb-attr 40×40 at
        /// opacity .55 (played: 1) with its icon 24 (its ink when played, else steel) and, when played, the count badge
        /// (20×20 circle at right −8, top −8; panel-solid, 1px ink-muted, 400 14/18 mono, centred).
        /// </summary>
        public static OcbElement PlayedAttrs(RectTransform parent, IReadOnlyDictionary<string, int> counts)
        {
            OcbElement el = Make<OcbElement>(parent, "ocb-attrs", AttrsCss);
            foreach (string k in AttrKeys)
            {
                int n = counts != null && counts.TryGetValue(k, out int c) ? c : 0;
                OcbAttrInfo a = Attr[k];
                string css = PanelCss + AttrCss + (n != 0 ? AttrOnCss : "");
                OcbElement box = Make<OcbElement>(el.Root, "ocb-attr " + k, css);
                Fixed(box.Root);
                Icon(box.Root, a.Icon, 24f, n != 0 ? a.Ink : DsTokens.Steel);
                if (n != 0)
                {
                    OcbElement count = Make<OcbElement>(box.Root, "ocb-count", AttrCountCss);
                    Absolute(count.Root);
                    Insets cb = count.Box.Style.BorderWidths;
                    TextStyle ts = TextOf(css + AttrCountCss);
                    count.SetPart("text", Text(count.Root, n.ToString(System.Globalization.CultureInfo.InvariantCulture), ts,
                        cb.Left, cb.Top, count.W - cb.Horizontal, ts.LineHeightPx, TextAlign.Center, "text"));
                    Insets bb = box.Box.Style.BorderWidths;
                    // position: absolute in the box's padding box: right -8px, top -8px
                    count.Node.Place(box.W - bb.Right + 8f - count.W, bb.Top - 8f);
                    box.SetPart("count", count.Root);
                }
                Flow(box.Root, css);
                el.SetPart(k, box.Root);
            }
            Flow(el.Root, AttrsCss);
            return el;
        }

        // js: vendor/ds/bundle.js:571
        /// <summary>
        /// GuardPreview(n): .ocb-guardpre, 56 tall, padding 0 14, inline-flex centred, gap 8, 1px dashed guard, radius 6,
        /// rgba(16,20,30,.6), 700 24/1 mono guard: shield 30 (guard, fill .2) and 「+n」.
        /// </summary>
        public static OcbElement GuardPreview(RectTransform parent, string n)
        {
            OcbElement el = Make<OcbElement>(parent, "ocb-guardpre", GuardPreCss);
            Icon(el.Root, "shield", 30f, DsTokens.Guard, DsTokens.Guard, 0.2f);
            el.SetPart("text", Text(el.Root, "+" + n, TextOf(GuardPreCss), "text"));
            Flow(el.Root, GuardPreCss);
            return el;
        }

        // js: vendor/ds/bundle.js:572
        /// <summary>
        /// EndTurnButton(off): .ocb-endturn 224×72 (2px amber, radius 6, amber .16, 700 24/28 body, .08em, ink, pointer):
        /// 「ターン終了」 centred. :hover amber .28; .is-off: opacity --opacity-enemy-turn (the hover still lights it, as the
        /// stylesheet says).
        /// </summary>
        public static OcbEndTurn EndTurnButton(RectTransform parent, bool off)
        {
            var et = new OcbEndTurn();
            OcbButton b = Button(parent, "ocb-endturn", hover => EndTurnCss + (hover ? EndTurnHoverCss : "") + (et.OffState ? EndTurnOffCss : ""), opacity: true);
            et.Button = b;
            BoxStyle s = b.Box.Style;
            Insets bw = s.BorderWidths;
            TextStyle ts = TextOf(EndTurnCss);
            const string text = "ターン終了";
            float tw = TextWidth(text, ts);
            // display: flex, centred: the text item (its trailing letter-spacing included) in the middle of the content box
            b.Label = Label.Make(b.Root, text, ts, (b.W - tw) * 0.5f, bw.Top + (b.H - bw.Vertical - ts.LineHeightPx) * 0.5f, null, null, "text");
            et.Off = off;
            return et;
        }

        // ---- the list drawer's classes (bundle.css:193-199) ------------------------------------------------------

        // js: vendor/ds/bundle.js:2600 (PileDrawer's tab: the class as hud/piles.js uses it)
        /// <summary>
        /// .ocb-drawer-tab: 700 18/1 body ink-muted, padding 10 14, a 2px transparent bottom border (.is-on: ink, amber
        /// underline), sized to its text. At (0, 0) of parent.
        /// </summary>
        public static OcbButton DrawerTab(RectTransform parent, string text, bool on)
        {
            string css = DrawerTabCss + (on ? DrawerTabOnCss : "");
            OcbButton b = Button(parent, "ocb-drawer-tab", _ => css);
            BoxStyle s = b.Box ? b.Box.Style : BoxOf(css);
            Insets bw = s.BorderWidths, pad = s.Padding;
            TextStyle ts = TextOf(css);
            b.Label = Text(b.Root, text ?? "", ts);
            Ui.Place(b.Label.rectTransform, bw.Left + pad.Left, bw.Top + pad.Top);
            b.Node.Place(0f, 0f, TextWidth(text ?? "", ts) + pad.Horizontal + bw.Horizontal, ts.LineHeightPx + pad.Vertical + bw.Vertical);
            return b;
        }

        /// <summary>.ocb-drawer-tabs: the tabs in a row, 4 apart (`on` = the index of the tab that is on). At (0, 0).</summary>
        public static OcbElement DrawerTabs(RectTransform parent, IReadOnlyList<string> tabs, int on)
        {
            OcbElement el = Make<OcbElement>(parent, "ocb-drawer-tabs", DrawerTabsCss);
            for (int i = 0; i < (tabs?.Count ?? 0); i++)
            {
                OcbButton t = DrawerTab(el.Root, tabs[i], i == on);
                Fixed(t.Root);
                el.SetPart("tab" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), t.Root);
            }
            Flow(el.Root, DrawerTabsCss);
            return el;
        }

        // js: vendor/ds/bundle.js:2621 (PileDrawer's row: the class as hud/piles.js uses it)
        /// <summary>
        /// .ocb-drawer-row, w wide: 40 tall, padding 0 12, a 1px --rule bottom border, flex centred, gap 10, 400 18/1 body:
        /// the 6×24 band in `band` (--attr; radius 2), the name, and the count at margin-left auto (600 mono).
        /// </summary>
        public static OcbElement DrawerRow(RectTransform parent, float w, Rgba band, string name, string count)
        {
            OcbElement el = Make<OcbElement>(parent, "ocb-drawer-row", DrawerRowCss);
            el.Node.W = w;
            var vars = new CssVars().Set("--attr", CssColor.ToCss(band));
            RectTransform i = El(el.Root, "i", DrawerRowICss, vars);
            NoShrink(i);
            el.SetPart("band", i);
            el.SetPart("name", Text(el.Root, name ?? "", TextOf(DrawerRowCss), "name"));
            TextMeshProUGUI c = Text(el.Root, count ?? "", TextOf(DrawerRowCss + MonoCss + DrawerRowMonoCss), "ocb-mono");
            AutoMarginLeft(c);
            el.SetPart("count", c);
            Flow(el.Root, DrawerRowCss, null, w);
            return el;
        }

        // ---- the end screen (bundle.css:202-206) ----------------------------------------------------------------

        // js: vendor/ds/bundle.js:2253 (mountBattle's end paper)
        /// <summary>
        /// .ocb-end over the whole parent (rgba(8,10,14,.62), the paper centred) with .ocb-end-paper: 720 wide, padding
        /// 48 56 40, washi, washi-ink, radius 4, 0 18px 40px rgba(0,0,0,.5), centred text: the h2 (800 64/72 display,
        /// .12em, 8 below), each p (400 20/32 body), the button (margin-top 28, 700 22/1 body, .08em, 2px washi-ink, radius
        /// 6, padding 16 36; null = none).
        /// </summary>
        public static OcbEnd End(RectTransform parent, string title, IReadOnlyList<string> paragraphs, string button)
        {
            float pw = parent && parent.rect.width > 0f ? parent.rect.width : TrialStage.W;
            float ph = parent && parent.rect.height > 0f ? parent.rect.height : TrialStage.H;
            var end = Make<OcbEnd>(parent, "ocb-end", EndCss);
            end.Node.Place(0f, 0f, pw, ph);
            if (end.Box) end.Box.raycastTarget = true;   // the shade is a div: it takes the pointer, nothing under it is clicked
            end.Paper = Make<OcbElement>(end.Root, "ocb-end-paper", EndPaperCss);
            BoxStyle s = end.Paper.Box.Style;
            Insets bw = s.BorderWidths, pad = s.Padding;
            float innerW = end.Paper.W - bw.Horizontal - pad.Horizontal;
            float x = bw.Left + pad.Left, y = bw.Top + pad.Top;

            TextStyle h2 = TextOf(EndPaperCss + EndPaperH2Css);
            float th = TextHeight(title ?? "", h2, innerW);
            end.Title = Label.Make(end.Paper.Root, title ?? "", h2, x, y, innerW, th, "h2");
            y += th + (Px(Rule(EndPaperH2Css).Layout.MarginBottom) ?? 8f);
            TextStyle p = TextOf(EndPaperCss + EndPaperPCss);
            foreach (string para in paragraphs ?? Array.Empty<string>())
            {
                float h = TextHeight(para ?? "", p, innerW);
                end.Paragraphs.Add(Label.Make(end.Paper.Root, para ?? "", p, x, y, innerW, h, "p"));
                y += h;
            }
            if (button != null)
            {
                OcbButton b = Button(end.Paper.Root, "button", hover => EndPaperButtonCss);
                BoxStyle bs = b.Box.Style;
                Insets bb = bs.BorderWidths, bp = bs.Padding;
                TextStyle bt = TextOf(EndPaperCss + EndPaperButtonCss);
                float tw = TextWidth(button, bt);
                float w = tw + bp.Horizontal + bb.Horizontal, h = bt.LineHeightPx + bp.Vertical + bb.Vertical;
                b.Label = Label.Make(b.Root, button, bt, bb.Left + bp.Left, bb.Top + bp.Top, null, null, "text");
                float mt = Px(Rule(EndPaperButtonCss).Layout.MarginTop) ?? 28f;
                // an inline-block centred on its own line (text-align: center); the line box is its margin box
                b.Node.Place(x + (innerW - w) * 0.5f, y + mt, w, h);
                y += mt + h;
                end.Button = b;
            }
            float paperH = y + pad.Bottom + bw.Bottom;
            end.Paper.Node.Place((pw - end.Paper.W) * 0.5f, (ph - paperH) * 0.5f, null, paperH);
            return end;
        }
    }
}
