// Status chips, the tooltip and the status panel (vendor/ds/bundle.js:340-406, bundle.css:85-114). The chip's
// :hover is the pointer's (a ClickArea over its box) or the .is-hover class; the panel's HP bar fill keeps its
// `transition: width .3s ease-out` (CssStates on the document clock). The game's HUD draws its own panel
// (.ocbg-battle-stat) from these parts; StatusPanel is the DS's, ported for completeness (kit.md §2.0).
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>StatusChip's c: { id, n, boss, hover }.</summary>
    public sealed class OcbChipData
    {
        public string Id;
        /// <summary>String(c.n).</summary>
        public string N;
        public bool Boss;
        public bool Hover;

        public OcbChipData() { }
        public OcbChipData(string id, int n, bool boss = false) { Id = id; N = n.ToString(System.Globalization.CultureInfo.InvariantCulture); Boss = boss; }
    }

    /// <summary>A built status chip: its classes and its :hover.</summary>
    public sealed class OcbChip : OcbElement
    {
        public bool Self { get; internal set; }
        public bool Boss { get; internal set; }
        /// <summary>The pointer listener (enter / leave / click) over the chip's box.</summary>
        public ClickArea Area { get; internal set; }
        /// <summary>data-status, data-n.</summary>
        public string StatusId { get; internal set; }
        public string N { get; internal set; }

        private bool _hoverClass, _pointer;

        /// <summary>.is-hover (the scenes light a chip whose tip is open).</summary>
        public bool Hover
        {
            get => _hoverClass;
            set { _hoverClass = value; Ocb.RestyleChip(this, _hoverClass || _pointer); }
        }

        internal void SetPointer(bool on)
        {
            _pointer = on;
            Ocb.RestyleChip(this, _hoverClass || _pointer);
        }

        internal void Init(bool hoverClass) => _hoverClass = hoverClass;
    }

    /// <summary>StatusPanel's d (bundle.js:359).</summary>
    public sealed class OcbStatusPanelData
    {
        /// <summary>'foe' or 'self'.</summary>
        public string Side = "self";
        /// <summary>d.width (0 = 210 for a foe, 300 for self).</summary>
        public float Width;
        public int Hp, MaxHp = 1, Loss, Guard, St;
        public bool Reserve;
        public List<OcbChipData> Chips = new List<OcbChipData>();
        public bool Elite;
        public float? SwatchAngle;
        /// <summary>d.badges(): extra chips built first in the chip row (the DS's FoeBadges), or null.</summary>
        public Action<RectTransform> Badges;
    }

    /// <summary>A built status panel with el.update.</summary>
    public sealed class OcbStatusPanel : OcbElement
    {
        public string Side { get; internal set; }
        /// <summary>.is-elite (set by the builder; el.update keeps the classes).</summary>
        public bool Elite { get; internal set; }
        public OcbElement HpRow { get; internal set; }
        public TextMeshProUGUI HpNumber { get; internal set; }
        public OcbElement Bar { get; internal set; }
        public RectTransform BarInner { get; internal set; }
        public OcbElement Fill { get; internal set; }
        public CssStates FillStates { get; internal set; }
        public HatchGraphic LossStripes { get; internal set; }
        public RectTransform Ticks { get; internal set; }
        public OcbElement GuardRow { get; internal set; }
        public OcbElement ChipRow { get; internal set; }
        public List<OcbChip> ChipEls { get; } = new List<OcbChip>();
        internal int LastMax = -1;

        // js: vendor/ds/bundle.js:374
        /// <summary>el.update(d): the numbers, the bar (its fill transitions .3s), the guard row and the chips again.</summary>
        public void Update(OcbStatusPanelData d) => Ocb.UpdateStatusPanel(this, d);
    }

    public static partial class Ocb
    {
        /// <summary>STATUS[id] || { name: id, self: false } (bundle.js:344).</summary>
        public static OcbStatusInfo StatusOf(string id) =>
            id != null && Status.TryGetValue(id, out OcbStatusInfo s) ? s : new OcbStatusInfo(id ?? "", false, null, null);

        // js: vendor/ds/bundle.js:343
        /// <summary>
        /// StatusChip(c): .ocb-chip, 30 tall, padding 0 8 0 6, gap 4, black .35, 1px ink-muted, radius 3 (.is-self: steel,
        /// radius 15; .is-boss: 3px double --boss; :hover / .is-hover: ink border, white .1), 400 16/1 body, nowrap: the
        /// status icon 18 (ink), its word, the count in mono 600. At (0, 0) of parent.
        /// </summary>
        public static OcbChip StatusChip(RectTransform parent, OcbChipData c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            OcbStatusInfo s = StatusOf(c.Id);
            var el = Make<OcbChip>(parent, "ocb-chip", ChipCss + (s.Self ? ChipSelfCss : "") + (c.Boss ? ChipBossCss : "") + (c.Hover ? ChipHoverCss : ""));
            el.Self = s.Self;
            el.Boss = c.Boss;
            el.StatusId = c.Id;
            el.N = c.N ?? "";
            el.Init(c.Hover);
            Icon(el.Root, c.Id, 18f, DsTokens.Ink);
            el.SetPart("name", Text(el.Root, s.Name, TextOf(ChipCss), "name"));
            el.SetPart("n", Text(el.Root, el.N, TextOf(ChipCss + MonoCss + ChipMonoCss), "ocb-mono"));
            Flow(el.Root, ChipCss);
            el.Area = ClickArea.On(el.Box);
            el.Area.HoverChanged += on => el.SetPointer(on);
            return el;
        }

        /// <summary>The 「+n」 chip a full chip row ends with (bundle.js:399): plus 18 and the count, the plain chip look.</summary>
        public static OcbChip MoreChip(RectTransform parent, int n)
        {
            var el = Make<OcbChip>(parent, "ocb-chip", ChipCss);
            el.StatusId = "more";   // data-more="1"
            el.N = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Icon(el.Root, "plus", 18f, DsTokens.Ink);
            el.SetPart("n", Text(el.Root, el.N, TextOf(ChipCss + MonoCss + ChipMonoCss), "ocb-mono"));
            Flow(el.Root, ChipCss);
            el.Area = ClickArea.On(el.Box);
            el.Area.HoverChanged += on => el.SetPointer(on);
            return el;
        }

        internal static void RestyleChip(OcbChip chip, bool hover)
        {
            if (!chip.Root) return;
            string css = ChipCss + (chip.Self ? ChipSelfCss : "") + (chip.Boss ? ChipBossCss : "") + (hover ? ChipHoverCss : "");
            if (chip.StatusId == "more") css = ChipCss + (hover ? ChipHoverCss : "");
            Restyle(chip.Root, css);
        }

        // js: vendor/ds/bundle.js:350
        /// <summary>
        /// StatusTip(c): .ocb-tip 340 wide: the head (icon 20, 「name n」, and at margin-left auto 「自分に付ける系」 /
        /// 「相手に付ける系」) and the rows 効き方 / 減り方. An id STATUS lacks fails, as the JS does.
        /// </summary>
        public static OcbElement StatusTip(RectTransform parent, string id, string n)
        {
            if (id == null || !Status.TryGetValue(id, out OcbStatusInfo s))
                throw new ArgumentException("StatusTip: no STATUS entry for '" + id + "' (the JS throws on s.name)", nameof(id));
            return Tip(parent, id, DsTokens.Ink, s.Name + " " + n, s.Self ? "自分に付ける系" : "相手に付ける系",
                new KeyValuePair<string, string>("効き方", s.How), new KeyValuePair<string, string>("減り方", s.Decay));
        }

        /// <summary>
        /// .ocb-tip (bundle.css:110-114): 340 wide, padding 10 12 12, panel-solid, 1px steel, radius 6, shadow-tip. The
        /// head (flex, centred, gap 6, 700 18/24 body, margin-bottom 6): the icon 20 (none when null), the title, the
        /// small note (margin-left auto, 400 14/18 ink-muted; none when null). Each row a p (400 16/22, wrapping): its
        /// label span (ink-muted, margin-right 8) then the text. At (0, 0) of parent; its height follows the text.
        /// </summary>
        public static OcbElement Tip(RectTransform parent, string icon, Rgba iconColor, string title, string small,
            params KeyValuePair<string, string>[] rows)
        {
            OcbElement tip = Make<OcbElement>(parent, "ocb-tip", TipCss);
            BoxStyle bs = tip.Box.Style;
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            float innerW = tip.W - bw.Horizontal - pad.Horizontal;
            float x = bw.Left + pad.Left, y = bw.Top + pad.Top;

            OcbElement head = Make<OcbElement>(tip.Root, "ocb-tip-head", TipHeadCss);
            if (icon != null) head.SetPart("icon", Icon(head.Root, icon, 20f, iconColor));
            head.SetPart("title", Text(head.Root, title ?? "", TextOf(TipCss + TipHeadCss), "title"));
            if (small != null)
            {
                TextMeshProUGUI sm = Text(head.Root, small, TextOf(TipCss + TipHeadCss + TipHeadSmallCss), "small");
                AutoMarginLeft(sm);
                head.SetPart("small", sm);
            }
            Flow(head.Root, TipHeadCss, null, innerW);
            head.Place(x, y);
            y += head.H + (Px(Rule(TipHeadCss).Layout.MarginBottom) ?? 6f);
            tip.SetPart("head", head.Root);

            TextStyle ps = TextOf(TipCss + TipPCss);
            ps.RichText = true;
            float spanGap = Px(Rule(TipPSpanCss).Layout.MarginRight) ?? 8f;
            Rgba spanColor = TextOf(TipCss + TipPCss + TipPSpanCss).Color;
            int i = 0;
            foreach (KeyValuePair<string, string> r in rows ?? Array.Empty<KeyValuePair<string, string>>())
            {
                string rich = (r.Key != null ? Colored(r.Key, spanColor) + "<space=" + Num(spanGap) + ">" : "") + Escape(r.Value ?? "");
                float h = TextHeight(rich, ps, innerW);
                tip.SetPart("p" + i, Label.Make(tip.Root, rich, ps, x, y, innerW, h, "p"));
                y += h;
                i++;
            }
            tip.Node.H = y + pad.Bottom + bw.Bottom;
            return tip;
        }

        // js: vendor/ds/bundle.js:361
        /// <summary>
        /// StatusPanel(d): .ocb-panel.ocb-stat (width d.width, else 210 foe / 300 self; padding 8 10 10; column, gap 8;
        /// .is-elite: phosphor border): the HP row (swatch 16, heart 20 crimson, the number 700 24/28 mono at least 40
        /// wide and right-aligned, the bar: 14 tall, black .5, 1px rule, radius 3, the crimson fill, the wick loss
        /// stripes, a 2px tick every 10 HP), the guard row (shield 28, the guard 700 24/28 mono; a foe adds pip 16, its
        /// stamina, a reserve shield 24) and the chips (wrapping, gap 5; at most 4 for a foe, 6 for self, the last a 「+n」).
        /// </summary>
        public static OcbStatusPanel StatusPanel(RectTransform parent, OcbStatusPanelData d)
        {
            d = d ?? new OcbStatusPanelData();
            OcbStatusPanel el = Make<OcbStatusPanel>(parent, "ocb-stat", StatPanelCss(d.Elite));
            el.Side = d.Side;
            el.Elite = d.Elite;
            el.Node.W = d.Width > 0f || d.Width < 0f ? d.Width : (d.Side == "foe" ? 210f : 300f);
            BoxStyle bs = el.Box.Style;
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            float innerW = el.W - bw.Horizontal - pad.Horizontal;

            // the HP row: [swatch] heart, number, bar
            el.HpRow = Make<OcbElement>(el.Root, "ocb-hp", HpCss);
            if (d.SwatchAngle.HasValue) Swatch(el.HpRow.Root, d.SwatchAngle.Value, 16f);
            Icon(el.HpRow.Root, "heart", 20f, DsTokens.Crimson);
            TextStyle hpStyle = TextOf(HpCss + MonoCss + HpBCss);
            el.HpNumber = Text(el.HpRow.Root, "", hpStyle, "b");
            FlexBox.Attrs(el.HpNumber.rectTransform).MinMain = Px(Rule(HpBCss).Layout.MinWidth) ?? 40f;   // min-width: 40px
            el.Bar = Make<OcbElement>(el.HpRow.Root, "ocb-bar", BarCss);
            FlexBox.Attrs(el.Bar.Root).Fill();   // flex: 1
            Fixed(el.Bar.Root);
            el.BarInner = El(el.Bar.Root, "padding-box");
            el.Fill = Make<OcbElement>(el.BarInner, "ocb-bar-fill", BarFillCss);
            el.FillStates = CssStates.Of(el.Fill.Root);
            el.LossStripes = HatchGraphic.Stripes(el.BarInner, 0f, 0f, 0f, 0f, 135f, 0f, 2f, 5f, DsTokens.Wick, 0f, "ocb-bar-loss");
            el.LossStripes.TrySetStripesCss("repeating-linear-gradient(135deg, var(--wick) 0 2px, transparent 2px 5px)");
            el.Ticks = El(el.BarInner, "ocb-bar-ticks", BarTicksCss);

            el.GuardRow = Make<OcbElement>(el.Root, "ocb-guardrow", GuardRowCss);
            el.ChipRow = Make<OcbElement>(el.Root, "ocb-chips", ChipsCss);
            el.SetPart("hp", el.HpRow.Root);
            el.SetPart("guard", el.GuardRow.Root);
            el.SetPart("chips", el.ChipRow.Root);
            el.Update(d);
            return el;
        }

        private static string StatPanelCss(bool elite) => PanelCss + StatCss + (elite ? StatEliteCss : "");

        internal static void UpdateStatusPanel(OcbStatusPanel el, OcbStatusPanelData d)
        {
            if (!el.Root || d == null) return;
            string panelCss = StatPanelCss(el.Elite);   // the classes are the builder's: update does not change them
            BoxStyle bs = el.Box.Style;
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            float innerW = el.W - bw.Horizontal - pad.Horizontal;
            int maxHp = d.MaxHp == 0 ? 1 : d.MaxHp;

            // hpNum.textContent = d.hp; the row laid out again (the number's width may change)
            LabelState hpState = Label.StateOf(el.HpNumber);
            hpState.AutoW = true;   // measured again: min-width 40 or its text, whichever is wider (text-align: right)
            hpState.AutoH = true;
            Label.SetText(el.HpNumber, d.Hp.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Flow(el.HpRow.Root, HpCss, null, innerW);
            Insets barB = el.Bar.Box.Style.BorderWidths;
            float pw = Math.Max(0f, el.Bar.W - barB.Horizontal), ph = Math.Max(0f, el.Bar.H - barB.Vertical);
            UiNode.Of(el.BarInner).Place(barB.Left, barB.Top, pw, ph);
            // overflow: hidden with radius 3: the padding box's rounded rect (radius 2) clips the fill, loss and ticks
            List<Vec2> clip = Shapes.RoundRect(new RectF(0f, 0f, pw, ph), CornerSet.All(Math.Max(0f, 3f - barB.Top))).Points;

            // fill: width hp / max (transition: width .3s ease-out)
            float fillPct = 100f * Math.Max(0, d.Hp) / maxHp;
            el.Fill.Node.Place(0f, 0f, null, ph);
            el.FillStates.Base(BarFillCss + "width: " + Num(fillPct) + "%;");
            el.FillStates.Apply();
            if (el.Fill.Box) el.Fill.Box.ClipConvex = clip;

            // loss: [hp − l, hp], striped
            int l = Math.Min(d.Loss, d.Hp);
            float lossLeft = pw * (d.Hp - l) / maxHp, lossW = pw * l / maxHp;
            el.LossStripes.gameObject.SetActive(l > 0);
            Ui.Place(el.LossStripes.rectTransform, lossLeft, 0f, lossW, ph);
            el.LossStripes.ClipConvex = Translate(clip, -lossLeft, 0f);

            // ticks every 10 HP (rebuilt when the max changes). Their left is a percentage of the bar, so they follow it
            // when the bar's width changes (the HP number is wider than its 40 px minimum at 100 and up): placed every update.
            UiNode.Of(el.Ticks).Place(0f, 0f, pw, ph);
            if (el.LastMax != d.MaxHp)
            {
                Ui.Clear(el.Ticks);
                for (int t = 10; t < d.MaxHp; t += 10) El(el.Ticks, "i", BarTickCss);
                el.LastMax = d.MaxHp;
            }
            for (int i = 0, t = 10; i < el.Ticks.childCount; i++, t += 10)
            {
                var tick = (RectTransform)el.Ticks.GetChild(i);
                float left = pw * t / maxHp - 1f;   // calc(t% − 1px)
                UiNode.Of(tick).Place(left, 0f, 2f, ph);
                BoxGraphic tg = tick.GetComponent<BoxGraphic>();
                if (tg) tg.ClipConvex = Translate(clip, -left, 0f);
            }

            // the guard row
            Ui.Clear(el.GuardRow.Root);
            bool zero = !(d.Guard > 0);
            string rowCss = GuardRowCss + (zero ? GuardRowZeroCss : "");
            Restyle(el.GuardRow.Root, rowCss, null, opacity: true);
            if (!zero) Icon(el.GuardRow.Root, "shield", 28f, DsTokens.Guard, DsTokens.Guard, 0.2f);
            else Icon(el.GuardRow.Root, "shield", 28f, DsTokens.Steel, dashed: true);
            string bCss = rowCss + MonoCss + GuardRowBCss + (zero ? GuardRowZeroBCss : "");
            Text(el.GuardRow.Root, d.Guard.ToString(System.Globalization.CultureInfo.InvariantCulture), TextOf(bCss), "b");
            if (d.Side == "foe")
            {
                RectTransform foe = El(el.GuardRow.Root, "ocb-foe-st", FoeStCss);
                AutoMarginLeft(foe);
                Icon(foe, "pip", 16f, DsTokens.InkMuted);
                // `.ocb-guardrow b` < `.ocb-foe-st b` (later) < `.ocb-guardrow.is-zero b` (more specific); then the inline font
                string fb = rowCss + FoeStCss + MonoCss + GuardRowBCss + FoeStBCss + (zero ? GuardRowZeroBCss : "") + "font:700 24px/28px var(--font-mono)";
                Text(foe, d.St.ToString(System.Globalization.CultureInfo.InvariantCulture), TextOf(fb), "b");
                if (d.Reserve) Icon(foe, "shield", 24f, DsTokens.Guard, DsTokens.Guard, 0.2f);
                else Icon(foe, "shield", 24f, DsTokens.Guard, dashed: true);
                Flow(foe, FoeStCss);
            }
            Flow(el.GuardRow.Root, rowCss, null, innerW);

            // the chips
            Ui.Clear(el.ChipRow.Root);
            el.ChipEls.Clear();
            List<OcbChipData> list = d.Chips ?? new List<OcbChipData>();
            int max = d.Side == "foe" ? 4 : 6;
            List<OcbChipData> shown = list.Count > max ? list.GetRange(0, max - 1) : list;
            d.Badges?.Invoke(el.ChipRow.Root);
            foreach (OcbChipData c in shown) el.ChipEls.Add(StatusChip(el.ChipRow.Root, c));
            if (list.Count > max) el.ChipEls.Add(MoreChip(el.ChipRow.Root, list.Count - shown.Count));
            bool any = el.ChipRow.Root.childCount > 0;
            el.ChipRow.Node.Visible = any;   // .ocb-chips:empty { display: none }
            if (any) Flow(el.ChipRow.Root, ChipsCss, null, innerW);

            Flow(el.Root, panelCss, null, el.W);
        }

        private static List<Vec2> Translate(List<Vec2> pts, float dx, float dy)
        {
            var o = new List<Vec2>(pts.Count);
            foreach (Vec2 p in pts) o.Add(new Vec2(p.X + dx, p.Y + dy));
            return o;
        }
    }
}
