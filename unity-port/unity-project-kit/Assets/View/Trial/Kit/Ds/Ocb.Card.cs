// The DS card (vendor/ds/bundle.js:192-298, bundle.css:14-52, 182-190): Card(def, st) 216×304, its long-description
// fit (fitCardText, measureBox) and the right-click CardDetail (560×280).
//
// The card is built part by part in its paint order (clip bars → cost → attribute icon → reach tag → name → values →
// description → trait box → dark sheet → edge), each part a builder on the OcbCard handle, so the battle hand
// (hand-card.js) composes the same parts with its own fits and the exploration / meta screens call Card(def, st) as the
// JS does. The card declares no CSS transition or animation; the scenes tween the dark sheet and the lamp themselves.
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>One value column of a card: { icon, n, word, ink? }.</summary>
    public sealed class OcbCardValue
    {
        public string Icon;
        /// <summary>String(v.n).</summary>
        public string N;
        public string Word;
        /// <summary>v.ink; null = by icon (sword phosphor, shield guard, heal hp-text, else ink).</summary>
        public Rgba? Ink;

        public OcbCardValue() { }

        public OcbCardValue(string icon, string n, string word, Rgba? ink = null)
        {
            Icon = icon;
            N = n;
            Word = word;
            Ink = ink;
        }

        public OcbCardValue(string icon, int n, string word, Rgba? ink = null)
            : this(icon, n.ToString(CultureInfo.InvariantCulture), word, ink) { }
    }

    /// <summary>A card's trait: { cond, eff } (the box's two rows).</summary>
    public sealed class OcbCardTrait
    {
        public string Cond;
        public string Eff;

        public OcbCardTrait() { }
        public OcbCardTrait(string cond, string eff) { Cond = cond; Eff = eff; }
    }

    /// <summary>
    /// The def Card reads (bundle.js:194): { name, attr, cost, reach: [min, max] | 'self', values, desc, lines (stance),
    /// trait: { cond, eff } | null, terms, detail }.
    /// </summary>
    public sealed class OcbCardDef
    {
        public string Name = "";
        /// <summary>attack / defense / skill / stance; anything else keeps the card's steel frame (and ATTR.attack's icon).</summary>
        public string Attr = "attack";
        public int Cost;
        /// <summary>reach === 'self'.</summary>
        public bool ReachSelf;
        /// <summary>reach[0], reach[1] as the card prints them.</summary>
        public string ReachMin = "", ReachMax = "";
        public List<OcbCardValue> Values = new List<OcbCardValue>();
        public string Desc = "";
        /// <summary>A stance card's rows (each with the ∞ mark).</summary>
        public List<string> Lines = new List<string>();
        /// <summary>null = a plain card (素直な札).</summary>
        public OcbCardTrait Trait;
        /// <summary>CardDetail's terms: [word, meaning] rows.</summary>
        public List<KeyValuePair<string, string>> Terms = new List<KeyValuePair<string, string>>();
        /// <summary>CardDetail's full text.</summary>
        public string Detail = "";

        /// <summary>reach: [min, max].</summary>
        public OcbCardDef WithReach(string min, string max)
        {
            ReachSelf = false;
            ReachMin = min ?? "";
            ReachMax = max ?? "";
            return this;
        }

        public OcbCardDef WithReach(int min, int max) =>
            WithReach(min.ToString(CultureInfo.InvariantCulture), max.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The state Card reads (bundle.js:195): { cost, costDown, lamp, unpayable, desc, fit }.</summary>
    public sealed class OcbCardState
    {
        /// <summary>The cost shown (null = def.cost).</summary>
        public int? Cost;
        public bool CostDown;
        /// <summary>lamp: the card is lit (.is-lit) and its trait lamp is on.</summary>
        public bool Lamp;
        public bool Unpayable;
        /// <summary>A description in place of def.desc (and of a stance card's rows, unless it is empty: `!st.desc`).</summary>
        public string Desc;
        /// <summary>'unity' | 'a' | 'b' | 'c' (fitCardText), or null.</summary>
        public string Fit;
        /// <summary>
        /// The card sits in the game's .ocbg-stage, so common.css:23 prints the reach tag's 「自分」 at 17 px (the DS's own
        /// 14 px only outside the game). True by default: every card the game shows is on its stage.
        /// </summary>
        public bool InStage = true;
    }

    /// <summary>A built card: its parts in paint order and its state classes.</summary>
    public sealed class OcbCard : OcbElement
    {
        /// <summary>The card's attribute (the is-… class).</summary>
        public string Attr { get; internal set; }

        /// <summary>The scope holding --attr and --attr-ink (the parts' var() resolve against it).</summary>
        public CssVars Vars { get; internal set; }

        public bool IsStance => Attr == "stance";

        public RectTransform Clip { get; internal set; }
        public BoxGraphic ClipBefore { get; internal set; }
        public BoxGraphic ClipAfter { get; internal set; }
        public OcbElement CostEl { get; internal set; }
        public TextMeshProUGUI CostLabel { get; internal set; }
        /// <summary>The 「▼n」 tag (null when the cost is not down).</summary>
        public OcbElement DownTag { get; internal set; }
        public PathGraphic AttrIcon { get; internal set; }
        public OcbElement ReachEl { get; internal set; }
        public TextMeshProUGUI NameLabel { get; internal set; }
        public OcbElement ValuesEl { get; internal set; }
        public OcbElement DescEl { get; internal set; }
        /// <summary>The description's labels (one, or one per fit row / stance row).</summary>
        public List<TextMeshProUGUI> DescLabels { get; } = new List<TextMeshProUGUI>();
        public OcbElement TraitEl { get; internal set; }
        /// <summary>The trait box's two rows (cond, eff), or the plain card's two.</summary>
        public List<TextMeshProUGUI> TraitRows { get; } = new List<TextMeshProUGUI>();
        public RectTransform LampSlot { get; internal set; }
        public OcbElement Lamp { get; internal set; }
        public OcbElement Dim { get; internal set; }
        public OcbElement Edge { get; internal set; }

        /// <summary>The fit mode and the text fitCardText works on (data-fit, data-text).</summary>
        public string FitMode { get; internal set; }
        public string FitText { get; internal set; }

        private bool _lit, _lampOn, _unpayable;

        // ---- states ----------------------------------------------------------------------------------------

        /// <summary>.is-lit: the edge in --wick and the card's shadow with the lamp ring and glow (shadow-card, shadow-lamp).</summary>
        public bool Lit
        {
            get => _lit;
            set
            {
                _lit = value;
                if (Root) Ocb.Restyle(Root, Ocb.CardRootCss(Attr, _lit), Vars);
                if (Edge != null && Edge.Root) Ocb.Restyle(Edge.Root, Ocb.CardEdgeCss + (_lit ? Ocb.CardLitEdgeCss : ""), Vars);
            }
        }

        /// <summary>.ocb-lamp.is-on: the trait lamp 16×16 --wick with its glow (a plain card has no lamp).</summary>
        public bool LampOn
        {
            get => _lampOn;
            set
            {
                _lampOn = value;
                if (Lamp == null || !Lamp.Root) return;
                Ocb.Restyle(Lamp.Root, Ocb.LampCss + (_lampOn ? Ocb.LampOnCss : ""), Vars);
                Ocb.PlaceLamp(this);
            }
        }

        /// <summary>st.lamp: both the card's .is-lit and the lamp's .is-on (Card sets them together).</summary>
        public void SetLamp(bool on)
        {
            Lit = on;
            LampOn = on;
        }

        /// <summary>.is-unpayable: the dark sheet at --opacity-unpayable (else 0).</summary>
        public bool Unpayable
        {
            get => _unpayable;
            set
            {
                _unpayable = value;
                if (Dim != null && Dim.Root) Ocb.Restyle(Dim.Root, Ocb.CardDimCss + (_unpayable ? Ocb.CardUnpayableDimCss : ""), Vars, opacity: true);
            }
        }

        /// <summary>The dark sheet's opacity set directly (the battle hand tweens it on its clock).</summary>
        public float DimOpacity
        {
            get => Dim != null && Dim.Node ? Dim.Node.Opacity : 0f;
            set { if (Dim != null && Dim.Node) Dim.Node.Opacity = value; }
        }

        internal void SetFlags(bool lit, bool lampOn, bool unpayable)
        {
            _lit = lit;
            _lampOn = lampOn;
            _unpayable = unpayable;
        }

        // ---- the parts, in paint order -----------------------------------------------------------------------

        // js: vendor/ds/bundle.js:204
        /// <summary>
        /// .ocb-cost at (16, 11), 44×44: the number centred (700 28/1 mono amber); .is-down = amber ground, --ground
        /// text, and the 「▼n」 tag (downText, null = none) under the circle: centred, 9 px below its padding box.
        /// </summary>
        public OcbElement CostPart(string text, bool down, string downText = null)
        {
            string css = Ocb.CostCss + (down ? Ocb.CostDownCss : "");
            OcbElement cost = Ocb.Make<OcbElement>(Root, "ocb-cost", css, Vars);
            cost.Place(16f, 11f);   // .ocb-cost left 16, top 11
            Insets bw = cost.Box.Style.BorderWidths;
            float cw = cost.W - bw.Horizontal, ch = cost.H - bw.Vertical;
            TextStyle ts = Ocb.TextOf(Ocb.CardCss + css, Vars);
            float lh = ts.LineHeightPx;
            // display: flex, centred: the text's line box in the middle of the content box
            CostLabel = Ocb.Text(cost.Root, text, ts, bw.Left, bw.Top + (ch - lh) * 0.5f, cw, lh, TextAlign.Center, "text");
            cost.SetPart("text", CostLabel);
            if (downText != null)
            {
                OcbElement tag = Ocb.Make<OcbElement>(cost.Root, "ocb-down", Ocb.CostDownTagCss, Vars);
                BoxStyle ds = tag.Box.Style;
                Insets tb = ds.BorderWidths, tp = ds.Padding;
                TextStyle dts = Ocb.TextOf(Ocb.CardCss + css + Ocb.CostDownTagCss, Vars);
                TextMeshProUGUI dl = Ocb.Text(tag.Root, downText, dts);
                float tw = Ocb.TextWidth(downText, dts);
                float th = tag.H;   // height: 18px (border-box)
                float w = tw + tp.Horizontal + tb.Horizontal;
                Ui.Place(dl.rectTransform, tb.Left + tp.Left, tb.Top + tp.Top);   // the line box at the top of the content box
                // position: absolute in the cost's padding box: left 50% + translateX(-50%), bottom -9px
                float padW = cost.W - bw.Horizontal, padH = cost.H - bw.Vertical;
                tag.Node.Place(bw.Left + padW * 0.5f - w * 0.5f, bw.Top + padH + 9f - th, w, th);
                tag.SetPart("text", dl);
                DownTag = tag;
                cost.SetPart("down", tag.Root);
            }
            CostEl = cost;
            SetPart("cost", cost.Root);
            return cost;
        }

        // js: vendor/ds/bundle.js:207
        /// <summary>.ocb-attr-icon: the attribute's icon, 24 px, at (68, 21).</summary>
        public PathGraphic AttrIconPart(string icon, Rgba color)
        {
            AttrIcon = Ocb.Icon(Root, icon, 24f, color);
            Ui.Place(AttrIcon.rectTransform, 68f, 21f);   // .ocb-attr-icon left 68, top 21
            SetPart("attr-icon", AttrIcon);
            return AttrIcon;
        }

        // js: vendor/ds/bundle.js:208
        /// <summary>
        /// .ocb-reach, right 12 / top 11, 32 tall: `range` 18 + the reach text, or (self) `person` 22 + 「自分」 (small: 14 px,
        /// or 17 px on the game's stage, common.css:23).
        /// </summary>
        public OcbElement ReachPart(bool self, string text, bool inStage = true)
        {
            OcbElement reach = Ocb.Make<OcbElement>(Root, "ocb-reach", Ocb.ReachCss, Vars);
            if (self)
            {
                Ocb.Icon(reach.Root, "person", 22f, DsTokens.Ink);
                string small = Ocb.ReachCss + Ocb.ReachSmallCss + (inStage ? CommonStyles.CardReachSmallCss : "");
                reach.SetPart("small", Ocb.Text(reach.Root, "自分", Ocb.TextOf(small, Vars), "small"));
            }
            else
            {
                Ocb.Icon(reach.Root, "range", 18f, DsTokens.Ink);
                reach.SetPart("text", Ocb.Text(reach.Root, text, Ocb.TextOf(Ocb.ReachCss, Vars)));
            }
            Ocb.Flow(reach.Root, Ocb.ReachCss, Vars);
            reach.Place(DsTokens.CardW - 12f - reach.W, 11f);   // right 12, top 11
            ReachEl = reach;
            SetPart("reach", reach.Root);
            return reach;
        }

        // js: vendor/ds/bundle.js:212
        /// <summary>.ocb-card-name: left 16, top 60, right 12; 700 22/28 display, .02em, nowrap (overflow visible). fontSize overrides the size.</summary>
        public TextMeshProUGUI NamePart(string name, float? fontSize = null)
        {
            TextStyle ts = Ocb.TextOf(Ocb.CardNameCss, Vars);
            if (fontSize.HasValue) ts.Size = fontSize.Value;
            NameLabel = Ocb.Text(Root, name, ts, 16f, 60f, DsTokens.CardW - 16f - 12f, ts.LineHeightPx, TextAlign.Start, "ocb-card-name");
            SetPart("name", NameLabel);
            return NameLabel;
        }

        // js: vendor/ds/bundle.js:213
        /// <summary>
        /// .ocb-values at (16, 94): one .ocb-val per value, 14 apart. A column is the grid `24px auto` (gap 4, centred):
        /// the icon 24 and the number (700 36/40 mono in the value's ink) on the first row, the word (400 16/18 ink-muted,
        /// margin-top −2) under the number.
        /// </summary>
        public OcbElement ValuesPart(IReadOnlyList<OcbCardValue> values)
        {
            OcbElement row = Ocb.Make<OcbElement>(Root, "ocb-values", Ocb.ValuesCss, Vars);
            row.Place(16f, 94f);   // .ocb-values left 16, top 94
            float x = 0f, rowH = 0f;
            float gap = Ocb.Rule(Ocb.ValuesCss).Layout.ColumnGap ?? 0f;
            float colGap = Ocb.Rule(Ocb.ValCss).Layout.ColumnGap ?? 4f;
            var vals = new List<(RectTransform val, PathGraphic ic, TextMeshProUGUI b, TextMeshProUGUI s, float bLh, float smallH, float row1, float row2, float col2)>();
            foreach (OcbCardValue v in values ?? Array.Empty<OcbCardValue>())
            {
                Rgba ink = v.Ink ?? Ocb.ValueInk(v.Icon);
                RectTransform val = Ocb.El(row.Root, "ocb-val", Ocb.ValCss, Vars);
                PathGraphic ic = Ocb.Icon(val, v.Icon, 24f, ink);
                TextStyle bs = Ocb.TextOf(Ocb.ValBCss, Vars).WithColor(ink);   // color: var(--val-ink) = the value's ink
                TextStyle ss = Ocb.TextOf(Ocb.ValSmallCss, Vars);
                string n = v.N ?? "", word = v.Word ?? "";
                TextMeshProUGUI b = Ocb.Text(val, n, bs, "b");
                TextMeshProUGUI s = Ocb.Text(val, word, ss, "small");
                // grid-template-columns: 24px auto; column-gap 4; align-items: center
                float bw = Ocb.TextWidth(n, bs), sw = Ocb.TextWidth(word, ss);
                float col2 = Math.Max(bw, sw);
                float row1 = Math.Max(24f, bs.LineHeightPx);
                // an empty <small> has no line box: 0 tall, and its −2 margin cannot make the track negative
                float smallH = word.Length > 0 ? ss.LineHeightPx : 0f;
                float row2 = Math.Max(0f, smallH - 2f);
                float w = 24f + colGap + col2;
                UiNode.Of(val).Place(x, 0f, w, row1 + row2);
                x += w + gap;
                rowH = Math.Max(rowH, row1 + row2);
                vals.Add((val, ic, b, s, bs.LineHeightPx, smallH, row1, row2, col2));
            }
            // display: flex (align-items: stretch): every column as tall as the row. A shorter grid (a value without a
            // word) is stretched, and align-content: normal shares the extra height equally between its two auto rows,
            // so its icon and number move down (measured in Chrome: 40 + 16 → rows 48 / 8, the number at 4, the icon at 12).
            foreach (var c in vals)
            {
                float extra = Math.Max(0f, rowH - (c.row1 + c.row2)) * 0.5f;
                float r1 = c.row1 + extra, r2 = c.row2 + extra;
                Ui.Place(c.ic.rectTransform, 0f, (r1 - 24f) * 0.5f);
                Ui.Place(c.b.rectTransform, 24f + colGap, (r1 - c.bLh) * 0.5f, c.col2, c.bLh);
                Ui.Place(c.s.rectTransform, 24f + colGap, r1 + (r2 - (c.smallH - 2f)) * 0.5f - 2f, c.col2, c.smallH);
                UiNode.Of(c.val).H = rowH;
            }
            row.Node.W = Math.Max(0f, x - (vals.Count > 0 ? gap : 0f));
            row.Node.H = rowH;
            ValuesEl = row;
            SetPart("values", row.Root);
            return row;
        }

        // js: vendor/ds/bundle.js:221
        /// <summary>.ocb-desc: left 16, top 156, 188×66 (a stance card: top 94, 88 tall); 400 16/22 body, wrapping, overflow visible.</summary>
        public OcbElement DescPart(string text)
        {
            OcbElement desc = NewDesc();
            AddDescText(text ?? "", Ocb.TextOf(Ocb.DescCss, Vars), 0f, desc.H, TextAlign.Start);
            return desc;
        }

        // js: vendor/ds/bundle.js:223
        /// <summary>A stance card's rows: .ocb-inf (flex, gap 6, centred, 22 tall, nowrap) each with `infinity` 18 in --hp-text.</summary>
        public OcbElement DescLinesPart(IReadOnlyList<string> lines)
        {
            OcbElement desc = NewDesc();
            TextStyle ts = Ocb.TextOf(Ocb.DescCss + Ocb.DescInfCss, Vars);
            float y = 0f;
            foreach (string line in lines ?? Array.Empty<string>())
            {
                RectTransform inf = Ocb.El(desc.Root, "ocb-inf", Ocb.DescInfCss, Vars);
                Ocb.Icon(inf, "infinity", 18f, DsTokens.HpText);
                DescLabels.Add(Ocb.Text(inf, line, ts));
                Ocb.Flow(inf, Ocb.DescInfCss, Vars);
                UiNode.Of(inf).Place(0f, y);
                y += UiNode.Of(inf).H;
            }
            return desc;
        }

        private OcbElement NewDesc()
        {
            string css = Ocb.DescCss + (IsStance ? Ocb.StanceDescCss : "");
            OcbElement desc = Ocb.Make<OcbElement>(Root, "ocb-desc", css, Vars);
            CssBlock b = Ocb.Rule(css, Vars);
            desc.Node.Place(Ocb.Px(b.Layout.Left) ?? 16f, Ocb.Px(b.Layout.Top) ?? 156f, Ocb.Px(b.Layout.Width) ?? 188f, Ocb.Px(b.Layout.Height) ?? 66f);
            DescEl = desc;
            DescLabels.Clear();
            SetPart("desc", desc.Root);
            return desc;
        }

        internal TextMeshProUGUI AddDescText(string text, TextStyle style, float y, float h, TextAlign align)
        {
            TextMeshProUGUI t = Ocb.Text(DescEl.Root, text, style, 0f, y, DescEl.W, h, align, "text");
            DescLabels.Add(t);
            return t;
        }

        /// <summary>Replaces the description's text (and its style: meta's pre-line and .is-small go through here).</summary>
        public void SetDesc(string text, TextStyle style = null)
        {
            if (DescEl == null || !DescEl.Root) return;
            Ui.Clear(DescEl.Root);
            DescLabels.Clear();
            AddDescText(text ?? "", style ?? Ocb.TextOf(Ocb.DescCss, Vars), 0f, DescEl.H, TextAlign.Start);
        }

        // js: vendor/ds/bundle.js:229
        /// <summary>
        /// .ocb-trait: left 12, right 7, bottom 7, 62 tall, flex centred: the lamp slot (26 wide, the lamp centred) and
        /// the two rows (400 16/22, the second 700). trait null = .is-plain: dashed steel, ink-muted, padding-left 12, no
        /// lamp, 「素直な札」 and 「特性なし（+2 込み）」 (both 400).
        /// </summary>
        public OcbElement TraitPart(OcbCardTrait trait, bool lampOn)
        {
            bool plain = trait == null;
            string css = Ocb.TraitCss + (plain ? Ocb.TraitPlainCss : "");
            OcbElement box = Ocb.Make<OcbElement>(Root, "ocb-trait", css, Vars);
            // left 12, right 7, bottom 7, height 62 of the 216×304 card
            float h = Ocb.Px(Ocb.Rule(css).Layout.Height) ?? 62f;
            box.Node.Place(12f, DsTokens.CardH - 7f - h, DsTokens.CardW - 12f - 7f, h);
            TraitEl = box;
            TraitRows.Clear();
            Lamp = null;
            LampSlot = null;
            if (!plain)
            {
                LampSlot = Ocb.El(box.Root, "ocb-lamp-slot", Ocb.TraitLampSlotCss, Vars);
                Lamp = Ocb.Make<OcbElement>(LampSlot, "ocb-lamp", Ocb.LampCss + (lampOn ? Ocb.LampOnCss : ""), Vars);
                _lampOn = lampOn;
            }
            RectTransform rows = Ocb.El(box.Root, "rows");
            string p = css + Ocb.TraitPCss;
            TextStyle s1 = Ocb.TextOf(p, Vars);
            // the plain card's second row carries style="font-weight:400" over `.ocb-trait p + p`
            TextStyle s2 = Ocb.TextOf(p + Ocb.TraitPPCss + (plain ? "font-weight:400" : ""), Vars);
            string top = plain ? "素直な札" : trait.Cond ?? "";
            string bottom = plain ? "特性なし（+2 込み）" : trait.Eff ?? "";
            TextMeshProUGUI r1 = Ocb.Text(rows, top, s1, "p");
            TextMeshProUGUI r2 = Ocb.Text(rows, bottom, s2, "p");
            float w = Math.Max(Ocb.TextWidth(top, s1), Ocb.TextWidth(bottom, s2));
            Ui.Place(r1.rectTransform, 0f, 0f);
            Ui.Place(r2.rectTransform, 0f, s1.LineHeightPx);
            UiNode.Of(rows).Place(0f, 0f, w, s1.LineHeightPx + s2.LineHeightPx);
            TraitRows.Add(r1);
            TraitRows.Add(r2);
            box.SetPart("rows", rows);
            Ocb.LayoutTrait(this);
            SetPart("trait", box.Root);
            if (Lamp != null) SetPart("lamp", Lamp.Root);
            return box;
        }

        // js: vendor/ds/bundle.js:237
        /// <summary>.ocb-card-dim: inset 0, radius 6, black at opacity 0 (.is-unpayable: --opacity-unpayable).</summary>
        public OcbElement DimPart(bool unpayable)
        {
            Dim = Ocb.Make<OcbElement>(Root, "ocb-card-dim", Ocb.CardDimCss + (unpayable ? Ocb.CardUnpayableDimCss : ""), Vars);
            Dim.Node.Place(0f, 0f, DsTokens.CardW, DsTokens.CardH);
            _unpayable = unpayable;
            SetPart("dim", Dim.Root);
            return Dim;
        }

        // js: vendor/ds/bundle.js:238
        /// <summary>.ocb-card-edge: inset 0, 2px solid --attr, radius 6 (.is-lit: --wick).</summary>
        public OcbElement EdgePart()
        {
            Edge = Ocb.Make<OcbElement>(Root, "ocb-card-edge", Ocb.CardEdgeCss + (_lit ? Ocb.CardLitEdgeCss : ""), Vars);
            Edge.Node.Place(0f, 0f, DsTokens.CardW, DsTokens.CardH);
            SetPart("edge", Edge.Root);
            return Edge;
        }
    }

    /// <summary>A built CardDetail: the card at 80 % and the text column.</summary>
    public sealed class OcbCardDetail : OcbElement
    {
        public OcbCard Card { get; internal set; }
        public RectTransform CardHolder { get; internal set; }
        public RectTransform Body { get; internal set; }
        public TextMeshProUGUI Title { get; internal set; }
        public TextMeshProUGUI Meta { get; internal set; }
        public TextMeshProUGUI Full { get; internal set; }
        /// <summary>The terms box (null without terms).</summary>
        public OcbElement Terms { get; internal set; }
    }

    public static partial class Ocb
    {
        /// <summary>The card element's declarations: .ocb-card, its attribute class, .is-lit.</summary>
        public static string CardRootCss(string attr, bool lit)
        {
            string a;
            switch (attr)
            {
                case "attack": a = CardAttackCss; break;
                case "defense": a = CardDefenseCss; break;
                case "skill": a = CardSkillCss; break;
                case "stance": a = CardStanceCss; break;
                default: a = ""; break;   // an unknown is-… class sets nothing: the card's steel and ink stay
            }
            return CardCss + a + (lit ? CardLitCss : "");
        }

        // js: vendor/ds/bundle.js:216
        /// <summary>A value's ink by its icon: sword phosphor, shield guard, heal hp-text, else ink.</summary>
        public static Rgba ValueInk(string icon) =>
            icon == "sword" ? DsTokens.Phosphor : icon == "shield" ? DsTokens.Guard : icon == "heal" ? DsTokens.HpText : DsTokens.Ink;

        /// <summary>
        /// The card's frame: the 216×304 element with its box (--card, radius 6, --shadow-card; lit: + --shadow-lamp)
        /// and the .ocb-card-clip with the attribute's two bars (::before 8 wide on the left, ::after 5 tall on top),
        /// clipped to the card's rounded corners. Add the parts with the handle's *Part builders, in paint order.
        /// </summary>
        public static OcbCard CardShell(RectTransform parent, string attr, bool lit = false)
        {
            string css = CardRootCss(attr, lit);
            CssVars vars = VarsOf(css);
            var card = new OcbCard { Attr = attr ?? "", Vars = vars };
            card.Root = El(parent, "ocb-card", css, vars);
            card.Node = UiNode.Of(card.Root);
            card.Box = card.Root.GetComponent<BoxGraphic>();
            card.Node.Place(0f, 0f, DsTokens.CardW, DsTokens.CardH);
            card.SetFlags(lit, false, false);

            // .ocb-card-clip: inset 0, radius 6, overflow hidden (the bars are cut at the card's corners)
            card.Clip = El(card.Root, "ocb-card-clip", CardClipCss, vars);
            UiNode.Of(card.Clip).Place(0f, 0f, DsTokens.CardW, DsTokens.CardH);
            List<Vec2> corners = BoxMesh.ClipPolygon(new RectF(0f, 0f, DsTokens.CardW, DsTokens.CardH), BoxOf(CardClipCss));
            RectTransform before = El(card.Clip, "before", CardClipBeforeCss, vars);
            UiNode.Of(before).Place(0f, 0f, 8f, DsTokens.CardH);   // left 0, top 0, bottom 0, width 8
            card.ClipBefore = before.GetComponent<BoxGraphic>();
            if (card.ClipBefore) card.ClipBefore.ClipConvex = corners;
            RectTransform after = El(card.Clip, "after", CardClipAfterCss, vars);
            UiNode.Of(after).Place(0f, 0f, DsTokens.CardW, 5f);   // left 0, top 0, right 0, height 5
            card.ClipAfter = after.GetComponent<BoxGraphic>();
            if (card.ClipAfter) card.ClipAfter.ClipConvex = corners;
            card.SetPart("clip", card.Clip);
            return card;
        }

        // js: vendor/ds/bundle.js:197
        /// <summary>
        /// Card(def, st): a 216×304 card at (0, 0) of parent. st.fit set = fitCardText runs on it (the description fitted
        /// with TMP's own measurement).
        /// </summary>
        public static OcbCard Card(RectTransform parent, OcbCardDef def, OcbCardState st = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            st = st ?? new OcbCardState();
            OcbAttrInfo a = AttrOf(def.Attr);
            OcbCard card = CardShell(parent, def.Attr, st.Lamp);
            card.Root.name = "ocb-card " + def.Name;
            int cost = st.Cost ?? def.Cost;
            bool down = st.CostDown || cost < def.Cost;
            card.CostPart(cost.ToString(CultureInfo.InvariantCulture), down,
                down ? "▼" + (def.Cost - cost).ToString(CultureInfo.InvariantCulture) : null);
            card.AttrIconPart(a.Icon, a.Ink);
            card.ReachPart(def.ReachSelf, def.ReachSelf ? null : ReachText(def.ReachMin, def.ReachMax), st.InStage);
            card.NamePart(def.Name);
            if (def.Attr != "stance" && def.Values != null && def.Values.Count > 0) card.ValuesPart(def.Values);
            string fitText;
            if (def.Attr == "stance" && string.IsNullOrEmpty(st.Desc))   // `!st.desc`: an empty desc keeps the rows
            {
                card.DescLinesPart(def.Lines ?? new List<string>());
                fitText = string.Concat(def.Lines ?? new List<string>());   // desc.textContent: the rows' text, without the icons
            }
            else
            {
                fitText = st.Desc ?? (string.IsNullOrEmpty(def.Desc) ? "" : def.Desc);   // st.desc != null ? st.desc : (def.desc || '')
                card.DescPart(fitText);
            }
            card.TraitPart(def.Trait, st.Lamp);
            card.DimPart(st.Unpayable);
            card.EdgePart();
            if (!string.IsNullOrEmpty(st.Fit))
            {
                card.FitMode = st.Fit;
                card.FitText = fitText;
                FitCardText(card);
            }
            return card;
        }

        /// <summary>The lamp slot centred in the trait box after the lamp changed size (10 ↔ 16).</summary>
        internal static void PlaceLamp(OcbCard card) => LayoutTrait(card);

        /// <summary>The trait box's flex row (align-items: center): the lamp slot, then the rows, after the padding.</summary>
        internal static void LayoutTrait(OcbCard card)
        {
            OcbElement box = card.TraitEl;
            if (box == null || !box.Root) return;
            BoxStyle s = box.Box.Style;
            Insets bw = s.BorderWidths, pad = s.Padding;
            float x = bw.Left + pad.Left;
            float innerH = box.H - bw.Vertical - pad.Vertical;
            float y0 = bw.Top + pad.Top;
            if (card.LampSlot)
            {
                // .ocb-lamp-slot: width 26, display flex, justify-content center; its height is the lamp's
                float lamp = card.Lamp.H;
                float slotW = Px(Rule(TraitLampSlotCss).Layout.Width) ?? 26f;
                UiNode.Of(card.LampSlot).Place(x, y0 + (innerH - lamp) * 0.5f, slotW, lamp);
                card.Lamp.Node.Place((slotW - card.Lamp.W) * 0.5f, 0f);
                x += slotW;
            }
            RectTransform rows = box.PartRect("rows");
            if (rows)
            {
                UiNode n = UiNode.Of(rows);
                n.Place(x, y0 + (innerH - n.H) * 0.5f);
            }
        }

        // ---- long descriptions (undecided look 5) -------------------------------------------------------------

        /// <summary>measureBox's inline style (bundle.js:251), for a size and a width (wrap) or nowrap.</summary>
        public static string MeasureCss(float size, float width, bool wrap) =>
            "font-family:var(--font-body);font-size:" + Num(size) + "px;line-height:" + Num(JsRound(size * 1.375f)) + "px;line-break:strict;overflow-wrap:anywhere;"
            + (wrap ? "width:" + Num(width) + "px;" : "white-space:nowrap;display:inline-block;");

        // js: vendor/ds/bundle.js:246
        /// <summary>
        /// measureBox(text, size, width, wrap) → { w: scrollWidth, h: scrollHeight } of the text in the body font at
        /// `size` with line-height round(size × 1.375): wrapped at `width`, or on one line. Measured with TMP (what is
        /// measured is what is drawn); scroll sizes are whole px.
        /// </summary>
        public static Vector2 MeasureBox(string text, float size, float width, bool wrap)
        {
            TextStyle ts = TextOf(MeasureCss(size, width, wrap));
            float lh = ts.LineHeightPx;
            if (wrap)
            {
                int lines = string.IsNullOrEmpty(text) ? 0 : TextMeasure.Lines(text, ts, width);
                float widest = string.IsNullOrEmpty(text) ? 0f : TextMeasure.WidestLine(text, ts, width);
                return new Vector2(JsRound(Math.Max(width, widest)), lines * lh);
            }
            return new Vector2(JsRound(TextMeasure.Width(text ?? "", ts)), string.IsNullOrEmpty(text) ? 0f : lh);
        }

        // js: vendor/ds/bundle.js:257
        /// <summary>
        /// fitCardText(card): the description refitted by the card's fit mode, in the 188 px box (66 tall, 88 on a stance
        /// card), as the DS does it:
        /// 'unity' one line, nowrap, overflow visible;
        /// 'a' one line, shrunk 16 → 12 px until it fits 188 (the loop stops at 12 even when it still does not), then an ellipsis;
        /// 'b' wrapped, shrunk 16 → 12 px until its height fits, line height round(size × 1.375), clamped to floor(H / lh) lines;
        /// 'c' cut after the first 「、」, each part shrunk on its own, one line each with an ellipsis.
        /// A stance card's rows come back as their plain text (the JS's textContent drops the ∞ icons).
        /// </summary>
        public static void FitCardText(OcbCard card)
        {
            if (card == null || card.DescEl == null || !card.DescEl.Root || string.IsNullOrEmpty(card.FitMode)) return;
            string mode = card.FitMode, text = card.FitText ?? "";
            const float W = 188f;
            float H = card.IsStance ? 88f : 66f;
            RectTransform d = card.DescEl.Root;
            Ui.Clear(d);
            card.DescLabels.Clear();
            string baseCss = DescCss + (card.IsStance ? StanceDescCss : "");
            if (mode == "unity")
            {
                card.AddDescText(text, TextOf(baseCss + DescFitUnityCss, card.Vars), 0f, H, TextAlign.Start);
                return;
            }
            float size;
            if (mode == "a")
            {
                for (size = 16f; size > 12f && MeasureBox(text, size, W, false).x > W; size--) { /* shrink */ }
                TextStyle ts = TextOf(baseCss + "white-space:nowrap;overflow:hidden;text-overflow:ellipsis;font-size:" + Num(size) + "px", card.Vars);
                card.AddDescText(text, ts, 0f, H, TextAlign.Start);
                return;
            }
            if (mode == "b")
            {
                for (size = 16f; size > 12f && MeasureBox(text, size, W, true).y > H; size--) { /* shrink */ }
                float lh = JsRound(size * 1.375f);
                int lines = (int)Math.Floor(H / lh);
                TextStyle ts = TextOf(baseCss + "font-size:" + Num(size) + "px;line-height:" + Num(lh) + "px;display:-webkit-box;-webkit-box-orient:vertical;-webkit-line-clamp:" + lines.ToString(CultureInfo.InvariantCulture) + ";overflow:hidden", card.Vars);
                ts.LineClamp = lines;
                ts.Overflow = TextOverflow.Ellipsis;
                card.AddDescText(text, ts, 0f, H, TextAlign.Start);
                return;
            }
            if (mode == "c")
            {
                int cut = text.IndexOf('、');
                string[] parts = cut > 0 ? new[] { text.Substring(0, cut + 1), text.Substring(cut + 1) } : new[] { text };
                float y = 0f;
                foreach (string p in parts)
                {
                    float s;
                    for (s = 16f; s > 12f && MeasureBox(p, s, W, false).x > W; s--) { /* shrink */ }
                    TextStyle ts = TextOf(baseCss + "white-space:nowrap;overflow:hidden;text-overflow:ellipsis;font-size:" + Num(s) + "px;line-height:22px", card.Vars);
                    card.AddDescText(p, ts, y, ts.LineHeightPx, TextAlign.Start);
                    y += ts.LineHeightPx;
                }
                return;
            }
            // an unknown mode: the class reset and the text back, as the JS leaves it
            card.AddDescText(text, TextOf(baseCss, card.Vars), 0f, H, TextAlign.Start);
        }

        /// <summary>Math.round for a non-negative value (half up).</summary>
        internal static float JsRound(float x) => (float)Math.Floor(x + 0.5f);

        /// <summary>
        /// .ocb-term on a range of a label (the card's glossary words): text-decoration underline dotted --ink-muted 2px,
        /// text-underline-offset 4px.
        /// </summary>
        public static TextDecor TermUnderline(TMP_Text label, int start, int length) =>
            TextDecor.Underline(label, start, length, 2f, 4f, dotted: true, color: DsTokens.InkMuted);

        // ---- the right-click detail ---------------------------------------------------------------------------

        // js: vendor/ds/bundle.js:288
        /// <summary>
        /// CardDetail(def, st): .ocb-detail 560×280 (padding 16, gap 16, panel-solid, 2px steel, radius 6, shadow-tip):
        /// the card at scale .8 in a 173×243 slot, then the column (gap 6): h3 the name (700 24/32 display), the meta line
        /// (「攻撃 ・ 敵 1 体 ・ 間合い 1〜2」 / 「… ・ 自分」, ink-muted), the full text, and the terms box at the bottom
        /// (margin-top: auto; padding 8 10, black .35, radius 4; each row 「<b>word</b> meaning」, the word 6 px apart).
        /// </summary>
        public static OcbCardDetail CardDetail(RectTransform parent, OcbCardDef def, OcbCardState st = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            OcbCardDetail det = Make<OcbCardDetail>(parent, "ocb-detail", DetailCss);
            BoxStyle bs = det.Box.Style;
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            float gap = Rule(DetailCss).Layout.ColumnGap ?? 16f;
            float cx = bw.Left + pad.Left, cy = bw.Top + pad.Top;
            float innerW = det.W - bw.Horizontal - pad.Horizontal, innerH = det.H - bw.Vertical - pad.Vertical;

            // .ocb-detail-card: 173×243, flex: none; the card at scale(.8), transform-origin 0 0
            CssBlock holderRule = Rule(DetailCardCss);
            float hw = Px(holderRule.Layout.Width) ?? 173f, hh = Px(holderRule.Layout.Height) ?? 243f;
            det.CardHolder = El(det.Root, "ocb-detail-card", DetailCardCss);
            UiNode.Of(det.CardHolder).Place(cx, cy, hw, hh);
            det.Card = Card(det.CardHolder, def, st ?? new OcbCardState());
            det.Card.Node.SetOrigin(0f, 0f);
            det.Card.Node.SetStyleTransform(sx: 0.8f, sy: 0.8f);

            // .ocb-detail-body: column, gap 6, min-width 0 (its max-content width, shrunk to what is left)
            float colGap = Rule(DetailBodyCss).Layout.RowGap ?? 6f;
            TextStyle h3 = TextOf(DetailCss + DetailH3Css);
            TextStyle meta = TextOf(DetailCss + DetailMetaCss);
            TextStyle full = TextOf(DetailCss + DetailFullCss);
            TextStyle terms = TextOf(DetailCss + DetailTermsCss);
            terms.RichText = true;
            string metaText = AttrOf(def.Attr).Name + " ・ " + (def.ReachSelf ? "自分" : "敵 1 体 ・ 間合い " + ReachText(def.ReachMin, def.ReachMax));
            string fullText = def.Detail ?? "";
            var termRows = new List<string>();
            string bold = "<font=\"" + TrialFonts.Tag(Family.Body, 700) + "\">";
            float marginB = Px(Rule(DetailTermsBCss).Layout.MarginRight) ?? 6f;
            foreach (KeyValuePair<string, string> t in def.Terms ?? new List<KeyValuePair<string, string>>())
                termRows.Add(bold + Escape(t.Key) + "</font><space=" + Num(marginB) + ">" + Escape(t.Value));
            BoxStyle termsBox = BoxOf(DetailTermsCss);
            Insets tp = termsBox.Padding;

            float avail = Math.Max(0f, innerW - hw - gap);
            float maxContent = Math.Max(Math.Max(TextWidth(def.Name ?? "", h3), TextWidth(metaText, meta)), TextWidth(fullText, full));
            foreach (string r in termRows) maxContent = Math.Max(maxContent, TextWidth(r, terms) + tp.Horizontal);
            float bodyW = Math.Min(maxContent, avail);
            det.Body = El(det.Root, "ocb-detail-body", DetailBodyCss);
            UiNode.Of(det.Body).Place(cx + hw + gap, cy, bodyW, innerH);   // align-items: stretch: as tall as the row

            float y = 0f;
            float th = TextHeight(def.Name ?? "", h3, bodyW);
            det.Title = Label.Make(det.Body, def.Name ?? "", h3, 0f, y, bodyW, th, "h3");
            y += th + colGap;
            float mh = TextHeight(metaText, meta, bodyW);
            det.Meta = Label.Make(det.Body, metaText, meta, 0f, y, bodyW, mh, "ocb-meta");
            y += mh + colGap;
            float fh = TextHeight(fullText, full, bodyW);
            det.Full = Label.Make(det.Body, fullText, full, 0f, y, bodyW, fh, "ocb-full");
            y += fh + colGap;
            if (termRows.Count > 0)
            {
                det.Terms = Make<OcbElement>(det.Body, "ocb-detail-terms", DetailTermsCss);
                float rw = Math.Max(0f, bodyW - tp.Horizontal);
                float ry = tp.Top;
                foreach (string r in termRows)
                {
                    float rh = TextHeight(r, terms, rw);
                    Label.Make(det.Terms.Root, r, terms, tp.Left, ry, rw, rh, "term");
                    ry += rh;
                }
                float termsH = ry + tp.Bottom;
                // margin-top: auto takes the free space of the column; with none left (the text overflows the fixed
                // 280) it resolves to 0 and the box follows the full text, overflowing the bottom as the browser does
                det.Terms.Node.Place(0f, Math.Max(y, innerH - termsH), bodyW, termsH);
            }
            det.SetPart("card", det.Card.Root);
            det.SetPart("body", det.Body);
            return det;
        }
    }
}
