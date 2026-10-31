// The supplies step of 出立 (出立の支度, scenes/meta/departure.js:509-658): the tool and consumable slots and their
// supply cards, the talent grid (3 × 6) with its page turns, the start-position diagram (DS FloorTile / DistanceChip),
// the five info lines, the status line and the two buttons. The screen, the deck step, the step switch and the keys are
// DepartureScene.cs; what the supply items are and what a built Departure hands on is Trial.Bridge.DepartureCarry (the
// port of DepartureSupplies.js).
//
// Every word, count, verdict and the start cell come from DepartureBuilder (the life's limits); nothing here decides.
// The look is meta.css:237-265 and :421-422, copied verbatim below.
using System;
using System.Collections.Generic;
using System.Globalization;
using Depiction.Bridge;
using DungeonContent;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using Trial.Script;
using UnityEngine;
using DepButton = Trial.Meta.DepartureScene.DepButton;
using StatusLine = Trial.Meta.DepartureScene.StatusLine;

namespace Trial.Meta
{
    internal sealed class DepartureSuppliesView
    {
        // ---- meta.css: 出立の支度, verbatim (meta.css:237-265, :421-422) -------------------------------------------------

        /// <summary>meta.css:237 .ocbg-meta-slots.</summary>
        internal const string SlotsCss = "position: absolute;";

        /// <summary>meta.css:238 .ocbg-meta-slot.</summary>
        internal const string SlotCss = "position: absolute; display: flex; align-items: center; gap: 12px; padding: 0 14px; background: color-mix(in srgb, var(--ground) 55%, transparent); border: var(--stroke-frame) dashed var(--steel); border-radius: var(--radius-panel); text-align: left; cursor: default; transition: border-color .14s, background .14s;";

        /// <summary>meta.css:239 .ocbg-meta-slot b.</summary>
        internal const string SlotBCss = "font: 700 20px/1.2 var(--font-display); letter-spacing: .04em; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;";

        /// <summary>meta.css:240 .ocbg-meta-slot-n.</summary>
        internal const string SlotNCss = "width: 26px; height: 26px; flex: none; display: inline-flex; align-items: center; justify-content: center; border: var(--stroke-rule) solid var(--steel); border-radius: 50%; font: 600 16px/1 var(--font-mono);";

        /// <summary>meta.css:241 .ocbg-meta-slot.is-full.</summary>
        internal const string SlotFullCss = "border-style: solid; border-color: var(--amber); background: color-mix(in srgb, var(--amber) 12%, var(--panel)); cursor: pointer;";

        /// <summary>meta.css:242 .ocbg-meta-slot.is-full:hover.</summary>
        internal const string SlotFullHoverCss = "background: color-mix(in srgb, var(--amber) 20%, var(--panel));";

        /// <summary>meta.css:243 .ocbg-meta-slot.is-bonus:not(.is-full).</summary>
        internal const string SlotBonusCss = "border-color: var(--wick);";

        /// <summary>meta.css:421 .ocbg-meta-slot-x.</summary>
        internal const string SlotXCss = "margin-left: auto; display: inline-flex; opacity: .55; transition: opacity .14s;";

        /// <summary>meta.css:422 .ocbg-meta-slot.is-full:hover .ocbg-meta-slot-x.</summary>
        internal const string SlotXHoverCss = "opacity: 1;";

        /// <summary>meta.css:244 .ocbg-meta-supgrid.</summary>
        internal const string SupGridCss = "position: absolute;";

        /// <summary>meta.css:245 .ocbg-meta-supply.</summary>
        internal const string SupplyCss = "display: flex; align-items: center; gap: var(--space-12); padding: 8px 14px 8px 16px; background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); transition: border-color .14s;";

        /// <summary>meta.css:246 .ocbg-meta-supply.is-in.</summary>
        internal const string SupplyInCss = "border-color: var(--amber);";

        /// <summary>meta.css:247 .ocbg-meta-supply.is-pulse (the animation; its @keyframes ocbg-meta-pulse, meta.css:248, is CssKeyframes').</summary>
        internal const string SupplyPulseAnimation = "ocbg-meta-pulse .5s ease-out";

        /// <summary>meta.css:249 .ocbg-meta-supply-text.</summary>
        internal const string SupplyTextCss = "flex: 1; min-width: 0;";

        /// <summary>meta.css:250 .ocbg-meta-supply-head.</summary>
        internal const string SupplyHeadCss = "display: flex; align-items: baseline; gap: 10px;";

        /// <summary>meta.css:251 .ocbg-meta-supply-head b.</summary>
        internal const string SupplyHeadBCss = "font: 700 22px/1.2 var(--font-display); letter-spacing: .04em; white-space: nowrap;";

        /// <summary>meta.css:252 .ocbg-meta-supply-head small.</summary>
        internal const string SupplyHeadSmallCss = "font: 400 16px/1 var(--font-body); white-space: nowrap;";

        /// <summary>meta.css:253 .ocbg-meta-side.</summary>
        internal const string SideCss = "margin-left: auto; display: inline-flex; align-items: center; gap: 4px; padding: 2px 8px; border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-tag); font: 400 16px/1.2 var(--font-body); white-space: nowrap; align-self: center;";

        /// <summary>meta.css:254 .ocbg-meta-supply p.</summary>
        internal const string SupplyPCss = "font: 400 16px/1.35 var(--font-body); margin-top: 4px; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;";

        /// <summary>meta.css:255 .ocbg-meta-supply .ocbg-meta-cell-row.</summary>
        internal const string SupplyRowCss = "position: static; width: 152px; flex: none; gap: 6px;";

        /// <summary>meta.css:256 .ocbg-meta-talents.</summary>
        internal const string TalentsCss = "position: absolute;";

        /// <summary>meta.css:257 .ocbg-meta-talent.</summary>
        internal const string TalentCss = "position: absolute; display: flex; align-items: center; gap: 8px; padding: 0 12px; background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); font: 700 18px/1 var(--font-body); white-space: nowrap; overflow: hidden; transition: border-color .14s, background .14s;";

        /// <summary>meta.css:258 .ocbg-meta-talent span.</summary>
        internal const string TalentSpanCss = "overflow: hidden; text-overflow: ellipsis;";

        /// <summary>meta.css:259 .ocbg-meta-talent:hover.</summary>
        internal const string TalentHoverCss = "border-color: var(--ink);";

        /// <summary>meta.css:260 .ocbg-meta-talent.is-on.</summary>
        internal const string TalentOnCss = "border: var(--stroke-frame) solid var(--amber); background: color-mix(in srgb, var(--amber) 22%, var(--panel)); box-shadow: 0 0 14px color-mix(in srgb, var(--amber) 35%, transparent);";

        /// <summary>meta.css:261 .ocbg-meta-info.</summary>
        internal const string InfoCss = "background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); padding: 14px 20px;";

        /// <summary>meta.css:262 .ocbg-meta-info h4.</summary>
        internal const string InfoH4Css = "font: 700 16px/1 var(--font-body); letter-spacing: .1em;";

        /// <summary>meta.css:263 .ocbg-meta-lane.</summary>
        internal const string LaneCss = "position: relative; height: 112px;";

        /// <summary>meta.css:264 .ocbg-meta-lane-unit.</summary>
        internal const string LaneUnitCss = "position: absolute; display: flex; align-items: flex-end; justify-content: center;";

        /// <summary>meta.css:265 .ocbg-meta-info-lines li.</summary>
        internal const string InfoLineCss = "font: 400 18px/1.6 var(--font-body);";

        // ---- state ------------------------------------------------------------------------------------------------

        private readonly DepartureScene _s;
        private readonly RectTransform _page;
        private readonly RectTransform _toolSlotRow, _consSlotRow, _toolGrid, _consGrid;
        private readonly List<SupplyCard> _toolCards = new List<SupplyCard>();
        private readonly List<SupplyCard> _consCards = new List<SupplyCard>();
        private readonly List<DepButton> _toolSlots = new List<DepButton>();
        private readonly List<DepButton> _consSlots = new List<DepButton>();
        private readonly RectTransform _talentGrid, _talentNav;
        private readonly List<DepButton> _talents = new List<DepButton>();
        private readonly MetaButton _tPrev, _tNext;
        private readonly TextMeshProUGUI _tText;
        private readonly RectTransform _lane, _infoLines;
        private readonly StatusLine _supStatus;
        private readonly MetaButton _backToDeck, _setOutBtn;

        /// <summary>talentPage: the talent grid's page (clamped by DepartureBuilder.TalentPage).</summary>
        internal int TalentPage;

        /// <summary>One supply card (card.option, card.count, card.plus, card.minus, card.kind).</summary>
        private sealed class SupplyCard
        {
            public SupplyOption Option;
            public string Kind;
            public RectTransform Root;
            public BoxGraphic Box;
            public CssStates States;
            public TextMeshProUGUI Count;
            public MetaButton Plus, Minus;
        }

        // js: scenes/meta/departure.js:510
        internal DepartureSuppliesView(DepartureScene scene, RectTransform page)
        {
            _s = scene ?? throw new ArgumentNullException(nameof(scene));
            _page = page ?? throw new ArgumentNullException(nameof(page));
            DepartureBuilder supplies = _s.Supplies;
            DepartureCtx ctx = _s.Ctx;

            DepartureScene.StepHead(_page, supplies.LifeTitle, supplies.RaisedLine);
            MetaStyle.Keys(_page, MetaText.SupplyKeys + "　" + (ctx.OnSettings != null ? MetaText.EscSettings : MetaText.EscBack), 1236f, 100f, 636f, right: true);

            DepartureScene.Text(_page, supplies.LifeToolHeading, DepartureScene.DepH2Css, 64f, 146f, 1140f, null, "ocbg-meta-dep-h2");
            _toolSlotRow = Ui.Div(_page, "ocbg-meta-slots", 64f, 184f, 1140f, 64f);
            UiNode.Of(_toolSlotRow);
            _toolGrid = Ui.Div(_page, "ocbg-meta-supgrid", 64f, 262f, 1140f, 360f);
            UiNode.Of(_toolGrid);
            DepartureScene.Text(_page, supplies.LifeConsumableHeading, DepartureScene.DepH2Css, 64f, 636f, 1140f, null, "ocbg-meta-dep-h2");
            _consSlotRow = Ui.Div(_page, "ocbg-meta-slots", 64f, 674f, 1140f, 64f);
            UiNode.Of(_consSlotRow);
            _consGrid = Ui.Div(_page, "ocbg-meta-supgrid", 64f, 752f, 1140f, 270f);
            UiNode.Of(_consGrid);

            // js: scenes/meta/departure.js:554 — the cards, two columns, 580 × 90 apart
            for (int i = 0; i < supplies.ToolOptions.Count; i++)
                _toolCards.Add(MakeSupplyCard(_toolGrid, supplies.ToolOptions[i], "tool", (i % 2) * 580f, (i / 2) * 90f));
            for (int i = 0; i < supplies.ConsumableOptions.Count; i++)
                _consCards.Add(MakeSupplyCard(_consGrid, supplies.ConsumableOptions[i], "consumable", (i % 2) * 580f, (i / 2) * 90f));

            // talent (DepartureBuilder.TalentPage) and the info panel
            DepartureScene.Text(_page, DepartureBuilder.TalentHeading, DepartureScene.DepH2Css + DepartureScene.DepH2WrapCss, 1236f, 146f, 636f, null, "ocbg-meta-dep-h2 is-wrap");
            _talentGrid = Ui.Div(_page, "ocbg-meta-talents", 1236f, 212f, 636f, 300f);
            UiNode.Of(_talentGrid);
            _talentNav = Ui.Div(_page, "ocbg-meta-pagenav", 1236f, 520f, 636f, 40f);
            UiNode.Of(_talentNav);
            _tPrev = MetaButton.Nav(_talentNav, MetaText.PrevPage, false, 0f, 0f, small: true);
            _tPrev.Click += () => { TalentPage--; _s.Sound.Play("nodeHover"); DrawTalent(); };
            _tText = Label.Make(_talentNav, "", MetaStyle.Text(MetaStyle.PageTextCss), "ocbg-meta-pagetext");
            _tNext = MetaButton.Nav(_talentNav, MetaText.NextPage, true, 0f, 0f, small: true);
            _tNext.Click += () => { TalentPage++; _s.Sound.Play("nodeHover"); DrawTalent(); };

            // .ocbg-meta-info (1236, 576, 636, 324): border 1 + padding 14 / 20 → the content at (21, 15), 594 wide
            RectTransform info = Ui.Div(_page, "ocbg-meta-info", 1236f, 576f, 636f, 324f);
            UiNode.Of(info);
            BoxGraphic.On(info, InfoCss);
            TextStyle h4 = MetaStyle.Text(InfoH4Css);
            Label.Make(info, MetaText.StartHeading, h4, 21f, 15f, null, null, "h4");
            _lane = Ui.Div(info, "ocbg-meta-lane", 21f, 15f + h4.LineHeightPx, 594f, 112f);
            UiNode.Of(_lane);
            _infoLines = Ui.Div(info, "ocbg-meta-info-lines", 21f, 15f + h4.LineHeightPx + 112f, 594f, 0f);
            UiNode.Of(_infoLines);

            _supStatus = new StatusLine(_page, 1236f, 912f, 636f);
            _backToDeck = MetaButton.Btn(_page, DepartureBuilder.BackLabel, 1236f, 972f, 236f, 72f);
            _backToDeck.Click += () => _s.ShowDeck().Forget();
            _setOutBtn = MetaButton.Primary(_page, DepartureBuilder.SetOutLabel, "Enter", 1488f, 972f, 384f, 72f);
            _setOutBtn.Click += () => _s.SetOut();
        }

        // js: scenes/meta/departure.js:528
        /// <summary>
        /// supplyCard(option, kind): .ocbg-meta-supply (560 × 82) at (x, y) of grid — the name, its reading and its side
        /// (戦闘 with `range`, 探索 with `stairs`), the player's note in two lines, and the − ×n ＋ row (152 wide, gap 6).
        /// </summary>
        private SupplyCard MakeSupplyCard(RectTransform grid, SupplyOption option, string kind, float x, float y)
        {
            ItemDef item = kind == "tool" ? ItemCatalogue.Tool(option.Id) : ItemCatalogue.Consumable(option.Id);
            bool battle = item.Side == EffectSide.Battle;
            var c = new SupplyCard { Option = option, Kind = kind };
            c.Root = Ui.Div(grid, battle ? "ocbg-meta-supply is-battle" : "ocbg-meta-supply", x, y, 560f, 82f);
            UiNode.Of(c.Root);
            c.Box = BoxGraphic.On(c.Root);
            c.States = CssStates.Of(c.Root).Base(SupplyCss).State(".is-in", SupplyInCss);
            c.States.Apply(true);

            // the content box: border 1 + padding 8 14 8 16 → x 17, 64 tall; the text (flex: 1) 364 wide, gap 12, the row 152
            const float contentX = 17f, contentY = 9f, contentH = 64f, rowW = 152f, gap = 12f;
            float textW = 560f - 2f - 16f - 14f - gap - rowW;

            // .ocbg-meta-supply-head (baseline, gap 10): b, small, the side tag (margin-left: auto, align-self: center)
            TextStyle nameS = MetaStyle.Text(SupplyHeadBCss), readS = MetaStyle.Text(SupplyHeadSmallCss);
            float bN = TextMath.CssBaselineFromTop(nameS.Spec), bR = TextMath.CssBaselineFromTop(readS.Spec);
            float above = Math.Max(bN, bR), below = Math.Max(nameS.LineHeightPx - bN, readS.LineHeightPx - bR);
            RectTransform side = Ui.Div(c.Root, "ocbg-meta-side");
            UiNode sideNode = UiNode.Of(side);
            BoxGraphic.On(side, SideCss);
            MetaUtil.Icon(side, battle ? "range" : "stairs", 16f, DsTokens.Ink);
            Label.Make(side, battle ? MetaText.SideBattle : MetaText.SideExplore, MetaStyle.Text(SideCss), "text");
            Ocb.Flow(side, SideCss);
            float headH = Math.Max(above + below, sideNode.H);
            TextStyle noteS = MetaStyle.P(SupplyPCss);
            noteS.LineClamp = 2;
            string note = DepartureScene.PlayerNote(option.Note);
            float noteH = TextMeasure.Height(note, noteS, textW);
            float textH = headH + 4f + noteH;
            float ty = contentY + (contentH - textH) / 2f;   // align-items: center

            Label.Make(c.Root, option.Name ?? "", nameS, contentX, ty + above - bN, null, null, "b");
            float nameW = TextMeasure.Width(option.Name ?? "", nameS);
            Label.Make(c.Root, item.Reading ?? "", readS, contentX + nameW + 10f, ty + above - bR, null, null, "small");
            sideNode.Place(contentX + textW - sideNode.W, ty + (headH - sideNode.H) / 2f);
            Label.Make(c.Root, note, noteS, contentX, ty + headH + 4f, textW, noteH, "p");

            // − ×n ＋ (space-between over 152 with gap 6: 0, 54, 108), centred in the content box
            RectTransform rowEl = Ui.Div(c.Root, "ocbg-meta-cell-row", contentX + textW + gap, contentY + (contentH - 36f) / 2f, rowW, 36f);
            UiNode.Of(rowEl);
            c.Minus = MetaButton.Step(rowEl, "−", 0f, 0f);
            c.Count = Label.Make(rowEl, "", MetaStyle.Text(MetaStyle.CountCss), (rowW - 44f) / 2f, (36f - 26f) / 2f, 44f, 26f, "ocbg-meta-count");
            c.Plus = MetaButton.Step(rowEl, "＋", rowW - 44f, 0f);
            c.Minus.Click += () =>
            {
                DepartureBuilder s = _s.Supplies;
                if (kind == "tool" ? s.RemoveTool(option.Id) : s.RemoveConsumable(option.Id)) { _s.Sound.Play("uiCancel"); RefreshSupplies(); }
            };
            c.Plus.Click += () =>
            {
                DepartureBuilder s = _s.Supplies;
                if (kind == "tool" ? s.AddTool(option.Id) : s.AddConsumable(option.Id)) { _s.Sound.Play("consumableUse"); RefreshSupplies(); Pulse(c); }
                else { _s.Sound.Play("refusal"); _s.Shake(c.Plus); }
            };
            return c;
        }

        // js: scenes/meta/departure.js:549
        /// <summary>pulse(el): the card's ring (ocbg-meta-pulse .5s ease-out) from the start again on every press.</summary>
        private static void Pulse(SupplyCard c)
        {
            if (c?.Box == null || !c.Box) return;
            CssAnimator.Set(c.Box, SupplyPulseAnimation);
        }

        // js: scenes/meta/departure.js:559
        /// <summary>
        /// drawSlots(row, ids, count, options, removeFn, isBonus): `count` plates at (i × 224, 0, 212, 64) — a full one names
        /// its item and takes it out on a press (uiCancel), an empty one is a disabled dashed 「空き」; a bonus slot (the
        /// survivor's 4th tool) keeps the wick dashes while empty and says why on hover.
        /// </summary>
        private void DrawSlots(RectTransform row, List<DepButton> plates, IReadOnlyList<string> ids, int count, IReadOnlyList<SupplyOption> options,
            Func<string, bool> removeFn, Func<int, bool> isBonus)
        {
            Ui.Clear(row);
            plates.Clear();
            for (int i = 0; i < count; i++)
            {
                string id = i < ids.Count ? ids[i] : null;
                SupplyOption opt = null;
                if (id != null) foreach (SupplyOption o in options) if (o.Id == id) { opt = o; break; }
                bool bonus = isBonus != null && isBonus(i);
                bool full = opt != null;
                DepButton plate = null;
                plate = new DepButton(row, "ocbg-meta-slot" + (full ? " is-full" : "") + (bonus ? " is-bonus" : ""), SlotCss)
                    .OnClass("is-full", SlotFullCss)
                    .Rule(".is-full:hover", SlotFullHoverCss, () => plate.Has("is-full") && plate.Hovered)
                    .Rule(".is-bonus:not(.is-full)", SlotBonusCss, () => plate.Has("is-bonus") && !plate.Has("is-full"))
                    .OnDisabled(MetaStyle.ButtonDisabledCss);
                if (full) plate.Toggle("is-full", true, apply: false);
                if (bonus) plate.Toggle("is-bonus", true, apply: false);
                plate.Disabled = !full;
                plate.Done();
                plate.Node.Place(i * 224f, 0f, 212f, 64f);
                if (bonus) TitleTip.On(plate.Box, DepartureBuilder.BonusLabel);
                string removeId = id;
                plate.OnClick = () => { if (full && removeFn(removeId)) { _s.Sound.Play("uiCancel"); RefreshSupplies(); } };

                // the content: border 2 + padding 14 → x 16; the number (26 × 26), gap 12, the name, the ✕ at the right end
                const float cx = 16f;
                RectTransform n = Ocb.El(plate.Root, "ocbg-meta-slot-n", SlotNCss);
                Label.Make(n, (i + 1).ToString(CultureInfo.InvariantCulture), MetaStyle.Text(SlotCss + SlotNCss), "text");
                Ocb.Flow(n, SlotNCss);
                UiNode.Of(n).Place(cx, (64f - 26f) / 2f);
                TextStyle bs = MetaStyle.Text(SlotCss + SlotBCss);
                string word = full ? opt.Name : MetaText.EmptySlot;
                float avail = 212f - 2f * 2f - 14f * 2f - 26f - 12f - (full ? 12f + 16f : 0f);
                float bw = Math.Min(TextMeasure.Width(word, bs), avail);
                Label.Make(plate.Root, word, bs, cx + 26f + 12f, (64f - bs.LineHeightPx) / 2f, bw + 0.01f, bs.LineHeightPx, "b");
                if (full)
                {
                    RectTransform x = Ui.Div(plate.Root, "ocbg-meta-slot-x", 212f - 2f - 14f - 16f, (64f - 16f) / 2f, 16f, 16f);
                    UiNode.Of(x);
                    MetaUtil.Icon(x, "exile", 16f, DsTokens.Ink);
                    CssStates xs = CssStates.Of(x).Base(MetaStyle.Without(SlotXCss, "display")).State(".is-full:hover", SlotXHoverCss);
                    xs.Apply(true);
                    plate.Area.HoverChanged += on => { if (xs) xs.SetState(".is-full:hover", on && plate.Has("is-full")); };
                }
                plates.Add(plate);
            }
        }

        // js: scenes/meta/departure.js:583
        /// <summary>drawTalent(): the page of the deck's kinds (3 × 6, 214 × 50 apart, 204 × 42), the chosen one lit.</summary>
        internal void DrawTalent()
        {
            Ui.Clear(_talentGrid);
            _talents.Clear();
            DeckBuilder deck = _s.Deck;
            DeckPage p = DepartureBuilder.TalentPage(deck, TalentPage, DepartureScene.TalentPerPage);
            TalentPage = p.Index;
            Label.SetText(_tText, p.Text);
            _tPrev.Disabled = p.Index == 0;
            _tNext.Disabled = p.Index >= p.Count - 1;
            DepartureScene.LayoutPageNav(_talentNav, _tPrev, _tText, _tNext, 636f, 40f);
            string chosen = _s.Supplies.TalentIn(deck);
            for (int i = 0; i < p.Cards.Count; i++)
            {
                BattleCore.CardDef def = p.Cards[i];
                bool on = def.Id == chosen;
                DepButton b = new DepButton(_talentGrid, on ? "ocbg-meta-talent is-on" : "ocbg-meta-talent", TalentCss)
                    .OnHover(TalentHoverCss)
                    .OnClass("is-on", TalentOnCss);
                if (on) b.Toggle("is-on", true, apply: false);
                b.Done();
                b.Node.Place((i % DepartureScene.TalentColumns) * 214f, (i / DepartureScene.TalentColumns) * 50f, 204f, 42f);
                // the content: border (1, 2 while .is-on) + padding 12; the icon 18, gap 8, the name cut with 「…」
                float border = on ? 2f : 1f;
                string icon = DepartureScene.AttrIcon.TryGetValue(def.Attribute, out string ic) ? ic : "star";
                Rgba ink = on ? DsTokens.Ground : DepartureScene.AttrInk.TryGetValue(def.Attribute, out Rgba c) ? c : DsTokens.Ink;
                PathGraphic g = MetaUtil.Icon(b.Root, icon, 18f, ink);
                Ui.Place(g.rectTransform, border + 12f, (42f - 18f) / 2f);
                TextStyle ts = MetaStyle.Text(TalentCss + TalentSpanCss);
                ts.Overflow = TextOverflow.Ellipsis;
                float avail = 204f - 2f * border - 24f - 18f - 8f;
                float tw = Math.Min(TextMeasure.Width(def.Name ?? "", ts), avail);
                Label.Make(b.Root, def.Name ?? "", ts, border + 12f + 18f + 8f, (42f - ts.LineHeightPx) / 2f, tw + 0.01f, ts.LineHeightPx, "span");
                string id = def.Id;
                b.OnClick = () => { if (_s.Supplies.ChooseTalent(_s.Deck, id)) { _s.Sound.Play("uiConfirm"); RefreshSupplies(); } };
                _talents.Add(b);
            }
            _s.SyncFocus();
        }

        /// <summary>The talent page turned by the keys (departure.js:798-799): only when that way is open, with nodeHover.</summary>
        internal void TurnTalent(int by)
        {
            MetaButton b = by < 0 ? _tPrev : _tNext;
            if (b.Disabled) return;
            TalentPage += by;
            _s.Sound.Play("nodeHover");
            DrawTalent();
        }

        // js: scenes/meta/departure.js:616
        /// <summary>
        /// drawLane(): the start a tool moves back (DepartureBuilder.PlayerStartCell / StartGap) on max(6, the enemy's cell)
        /// floor tiles 88 apart: the player's tile lit, the two figures over their tiles, the distance chip between them.
        /// </summary>
        private void DrawLane()
        {
            Ui.Clear(_lane);
            const float cellW = 88f;
            DepartureBuilder supplies = _s.Supplies;
            int pc = supplies.PlayerStartCell;
            int gap = supplies.StartGap;
            int foe = pc + gap + 1;
            int cellsN = Math.Max(6, foe);
            for (int i = 1; i <= cellsN; i++)
            {
                OcbFloorTile t = Ocb.FloorTile(_lane, cellW - 6f);
                t.Place((i - 1) * cellW + 3f, 58f);
                if (i == pc)
                {
                    t.Reach.Node.Opacity = 0.65f;
                    t.ReachLine.Node.Opacity = 1f;
                }
            }
            LaneUnit((pc - 1) * cellW, DsTokens.Amber);
            LaneUnit((foe - 1) * cellW, DsTokens.Violet);
            OcbElement chip = Ocb.DistanceChip(_lane, gap.ToString(CultureInfo.InvariantCulture), gap * cellW);
            chip.Place(pc * cellW, 61f);
        }

        /// <summary>.ocbg-meta-lane-unit at (x, 12, 88, 44): `person` 34 at the bottom centre (align-items: flex-end).</summary>
        private void LaneUnit(float x, Rgba color)
        {
            RectTransform unit = Ui.Div(_lane, "ocbg-meta-lane-unit", x, 12f, 88f, 44f);
            UiNode.Of(unit);
            PathGraphic g = MetaUtil.Icon(unit, "person", 34f, color);
            Ui.Place(g.rectTransform, (88f - 34f) / 2f, 44f - 34f);
        }

        // js: scenes/meta/departure.js:638
        /// <summary>
        /// refreshSupplies(): every card's count and ＋ / −, both slot rows, the talents, the start diagram, the info lines,
        /// the status line, 出立する, and the draft.
        /// </summary>
        internal void RefreshSupplies()
        {
            DepartureBuilder supplies = _s.Supplies;
            DeckBuilder deck = _s.Deck;
            var all = new List<SupplyCard>(_toolCards);
            all.AddRange(_consCards);
            foreach (SupplyCard c in all)
            {
                int n = c.Kind == "tool" ? supplies.ToolCount(c.Option.Id) : supplies.ConsumableCount(c.Option.Id);
                Label.SetText(c.Count, n == 0 ? "" : "×" + n.ToString(CultureInfo.InvariantCulture));
                c.States.SetState(".is-in", n > 0);
                c.Plus.Disabled = !(c.Kind == "tool" ? supplies.CanAddTool(c.Option.Id) : supplies.CanAddConsumable(c.Option.Id));
                c.Minus.Disabled = n == 0;
            }
            DrawSlots(_toolSlotRow, _toolSlots, supplies.Tools, supplies.Limits.ToolSlots, supplies.ToolOptions, id => supplies.RemoveTool(id), i => supplies.IsBonusToolSlot(i));
            DrawSlots(_consSlotRow, _consSlots, supplies.Consumables, supplies.Limits.ConsumableSlots, supplies.ConsumableOptions, id => supplies.RemoveConsumable(id), null);
            DrawTalent();
            DrawLane();
            Ui.Clear(_infoLines);
            TextStyle ls = MetaStyle.P(InfoLineCss);
            float y = 0f;
            foreach (string l in new[] { supplies.TalentLine(deck), supplies.StartLine, supplies.ToolsLine, supplies.ConsumablesLine, MetaText.DeckCount(deck.Total) })
            {
                float h = TextMeasure.Height(l ?? "", ls, 594f);
                Label.Make(_infoLines, l ?? "", ls, 0f, y, 594f, h, "li");
                y += h;
            }
            bool ok = supplies.CanSetOut(deck);
            _supStatus.Set(supplies.StatusText(deck), ok);
            _setOutBtn.Disabled = !ok;
            _s.KeepDraft();
            _s.SyncFocus();
        }

        /// <summary>
        /// The supplies step's buttons in DOM order (the Tab order): the tool slots, each tool card's − ＋, the consumable
        /// slots, each consumable card's − ＋, the talents, the talent page turns, デッキに戻る, 出立する.
        /// </summary>
        internal IEnumerable<object> FocusOrder()
        {
            foreach (DepButton b in _toolSlots) yield return b;
            foreach (SupplyCard c in _toolCards) { yield return c.Minus; yield return c.Plus; }
            foreach (DepButton b in _consSlots) yield return b;
            foreach (SupplyCard c in _consCards) { yield return c.Minus; yield return c.Plus; }
            foreach (DepButton b in _talents) yield return b;
            yield return _tPrev;
            yield return _tNext;
            yield return _backToDeck;
            yield return _setOutBtn;
        }
    }
}
