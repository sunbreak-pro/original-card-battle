// Port of scenes/meta/owned.js (所持カード, CAMP_FACILITIES_DESIGN.md §3.4): the cards this character owns as this life
// has grown them — the current name (MasteryBook.Mastered), the 習熟 step and the ticks toward the next one (the book's
// record and its profile's thresholds), the 永続能力 a stance card carries (CardFaceText's lasting rows) — with a mark on
// the cards in the battle deck. Read only. Built from the deck screen's parts; C or Esc closes it.
//
// The look is meta.css's .ocbg-meta-owned rules (meta.css:322-350, :407, :447-449) copied verbatim below, laid out in
// stage px as the browser lays them out (the panel's and the detail's children sit in their padding box, inside the
// 1 px border). Every word comes from MetaText (Trial.Script), every number from the MasteryBook and its profiles, the
// 習熟 verdicts (the top step, the step held back by the life's one 3 段) from Exploration.Bridge.MasteryView.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Depiction;
using Depiction.Bridge;
using Exploration.Bridge;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using Trial.Script;
using UnityEngine;
using TextMeasure = Trial.Kit.TextMeasure;

namespace Trial.Meta
{
    public sealed class OwnedCards : IOverlay
    {
        // ---- meta.css: owned cards, verbatim --------------------------------------------------------------------------

        /// <summary>meta.css:323 .ocbg-meta-owned .ocbg-meta-owned-panel > header.</summary>
        public const string HeaderCss = "position: absolute; left: 28px; right: 24px; top: 20px; display: flex; align-items: center; gap: var(--space-16);";

        /// <summary>meta.css:324 .ocbg-meta-owned .ocbg-meta-owned-panel > header h2.</summary>
        public const string H2Css = "font: 700 34px/1.2 var(--font-display); letter-spacing: .16em;";

        /// <summary>meta.css:325 .ocbg-meta-owned .ocbg-meta-owned-count.</summary>
        public const string CountCss = "font: 600 22px/1 var(--font-mono);";

        /// <summary>meta.css:326 .ocbg-meta-owned .ocbg-meta-owned-panel > header .ocbg-meta-keys.</summary>
        public const string HeaderKeysCss = "margin-left: auto;";

        /// <summary>meta.css:327 .ocbg-meta-owned .ocbg-meta-cell.</summary>
        public const string CellCss = "position: absolute; display: flex; flex-direction: column; align-items: center; padding-top: 4px; border-radius: var(--radius-panel); transition: background .14s;";

        /// <summary>meta.css:328 .ocbg-meta-owned .ocbg-meta-cell:hover.</summary>
        public const string CellHoverCss = "background: color-mix(in srgb, var(--ink) 5%, transparent);";

        /// <summary>meta.css:329 .ocbg-meta-owned .ocbg-meta-cell .ocbg-meta-cardslot.</summary>
        public const string CardSlotCss = "transition: transform .14s;";

        /// <summary>meta.css:330 .ocbg-meta-owned .ocbg-meta-cell:hover .ocbg-meta-cardslot.</summary>
        public const string CardSlotHoverCss = "transform: translateY(-5px);";

        /// <summary>meta.css:331 .ocbg-meta-owned .ocbg-meta-cell.is-sel.</summary>
        public const string CellSelCss = "background: color-mix(in srgb, var(--amber) 10%, transparent); box-shadow: inset 0 0 0 2px var(--amber);";

        /// <summary>meta.css:332 .ocbg-meta-owned .ocbg-meta-owned-row.</summary>
        public const string RowCss = "position: absolute; left: 10px; right: 10px; bottom: 10px; height: 34px; display: flex; align-items: center; justify-content: center; gap: var(--space-8); font: 400 16px/1 var(--font-body);";

        /// <summary>meta.css:333 .ocbg-meta-owned .ocbg-meta-indeck.</summary>
        public const string InDeckCss = "display: inline-flex; margin-left: 4px;";

        /// <summary>meta.css:339 .ocbg-meta-owned .ocbg-meta-detail.is-owned.</summary>
        public const string DetailOwnedCss = "background: var(--panel);";

        /// <summary>meta.css:340 .ocbg-meta-owned .ocbg-meta-indeck-line.</summary>
        public const string InDeckLineCss = "display: flex; align-items: center; gap: 8px;";

        /// <summary>meta.css:341 .ocbg-meta-owned .ocbg-meta-mastery, .ocbg-meta-owned .ocbg-meta-lasting.</summary>
        public const string MasteryCss = "display: flex; flex-direction: column; gap: 10px; padding-top: var(--space-12); border-top: var(--stroke-rule) solid var(--rule);";

        /// <summary>meta.css:342 .ocbg-meta-owned .ocbg-meta-mastery h4, .ocbg-meta-owned .ocbg-meta-lasting h4.</summary>
        public const string H4Css = "font: 700 18px/1 var(--font-body); letter-spacing: .14em;";

        /// <summary>meta.css:343 .ocbg-meta-owned .ocbg-meta-mastery-rank.</summary>
        public const string MasteryRankCss = "display: flex; align-items: center; gap: var(--space-16);";

        /// <summary>meta.css:344 .ocbg-meta-owned .ocbg-meta-mastery-rank b.</summary>
        public const string MasteryRankBCss = "font: 700 30px/1 var(--font-display); letter-spacing: .06em;";

        /// <summary>meta.css:345 .ocbg-meta-owned .ocbg-meta-mastery-next.</summary>
        public const string MasteryNextCss = "display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: var(--space-12); font: 400 18px/1.3 var(--font-body);";

        /// <summary>meta.css:346 .ocbg-meta-owned .ocbg-meta-mastery-next b.</summary>
        public const string MasteryNextBCss = "font: 600 20px/1 var(--font-mono);";

        /// <summary>meta.css:347 .ocbg-meta-owned .ocbg-meta-ticks.</summary>
        public const string TicksCss = "position: relative; height: 10px; border-radius: 5px; background: color-mix(in srgb, var(--ground) 70%, transparent); border: var(--stroke-rule) solid var(--rule); overflow: hidden;";

        /// <summary>meta.css:348 .ocbg-meta-owned .ocbg-meta-ticks i.</summary>
        public const string TicksFillCss = "position: absolute; left: 0; top: 0; bottom: 0; background: var(--amber);";

        /// <summary>meta.css:349 .ocbg-meta-owned .ocbg-meta-mastery p, .ocbg-meta-owned .ocbg-meta-lasting p.</summary>
        public const string MasteryPCss = "display: flex; align-items: center; gap: 8px; font: 400 18px/1.45 var(--font-body);";

        /// <summary>meta.css:350 .ocbg-meta-owned .ocbg-meta-owned-lines p.</summary>
        public const string LinesPCss = "font: 400 18px/1.5 var(--font-body); margin-bottom: 6px;";

        /// <summary>meta.css:407 .ocbg-meta-owned .ocbg-meta-owned-below.</summary>
        public const string BelowCss = "display: flex; flex-direction: column; gap: var(--space-24); overflow-y: auto;";

        /// <summary>meta.css:448 .ocbg-meta-owned .ocbg-meta-mastery p.ocbg-meta-mastery-capped.</summary>
        public const string CappedCss = "display: block; padding: 8px 12px; border-left: 2px solid var(--steel); background: color-mix(in srgb, var(--steel) 10%, transparent);";

        /// <summary>meta.css:449 .ocbg-meta-owned .ocbg-meta-mastery p.ocbg-meta-mastery-ticks.</summary>
        public const string TicksPCss = "font: 600 18px/1.3 var(--font-mono);";

        /// <summary>meta.css:25 ::-webkit-scrollbar-thumb (the below box's thumb).</summary>
        private const string ThumbCss = "background: var(--steel); border-radius: 4px;";

        // ---- owned.js constants -------------------------------------------------------------------------------------

        // js: scenes/meta/owned.js:14 (never written before a member access: ViewRulesBoundaryTests)
        private const int Columns = 6;
        private const int Rows = 3;
        private const int PerPage = Columns * Rows;
        private const float FaceScale = 0.68f;

        /// <summary>
        /// Mastery.TopRank (BattleCore/Mastery.cs:240): how many pips a row draws. The View may not name the rule class
        /// (ViewRulesBoundaryTests) and no bridge exposes the number, so it is mirrored here; the verdicts that compare a
        /// step with it come from Exploration.Bridge.MasteryView.
        /// </summary>
        private const int PipCount = 3;

        // The panel and its parts in stage px (owned.js:59-75).
        private const float PanelX = 40f, PanelY = 36f, PanelW = 1840f, PanelH = 1008f;
        private const float CellW = 176f, CellH = 262f, CellStepX = 184f, CellStepY = 274f;
        private const float DetailW = 652f, DetailH = 896f;
        private const float BelowW = 612f, BelowH = 532f;

        // ---- state ----------------------------------------------------------------------------------------------------

        private readonly OwnedCtx _ctx;
        private readonly MetaSound _sound;
        private readonly RectTransform _root;
        private readonly SceneClock _clock;
        private readonly BattleCore.MasteryBook _book;
        private readonly Func<string, bool> _inDeck;
        private readonly List<BattleCore.CardDef> _cards;
        private readonly Dictionary<string, CardFace> _faces = new Dictionary<string, CardFace>(StringComparer.Ordinal);
        private bool _alive = true;
        private bool _closing;
        private int _page;
        private BattleCore.CardDef _selected;

        private readonly UiNode _shade, _panel;
        private readonly RectTransform _grid, _pageNav, _detail;
        private readonly MetaButton _prevBtn, _nextBtn;
        private readonly TextMeshProUGUI _pageText;
        private readonly List<Cell> _cells = new List<Cell>();
        private global::Trial.Kit.Styler _ps, _pd;
        private Action _popKeys;

        /// <summary>One grid cell: its card, its box and the state machines of its :hover / .is-sel rules.</summary>
        private sealed class Cell
        {
            public BattleCore.CardDef Def;
            public RectTransform Root;
            public UiNode Node;
            public CssStates States;
        }

        // js: scenes/meta/owned.js:33
        private OwnedCards(RectTransform host, OwnedCtx ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _sound = MetaUtil.SoundOf(ctx);
            _root = MetaUtil.SceneRoot(host, "owned");
            _clock = MetaUtil.SceneClock(null, ctx.Stage);
            Expedition ex = ctx.Expedition;
            _book = ctx.Book ?? ex?.Mastery;
            _inDeck = DeckIds(ctx.Deck ?? (object)ex?.Deck);
            var ownedSet = new HashSet<string>(ctx.Owned ?? (ex != null ? (IEnumerable<string>)ex.Owned : Array.Empty<string>()), StringComparer.Ordinal);
            _cards = BattleCore.CardCatalog.All.Where(def => ownedSet.Contains(def.Id)).ToList();
            _selected = _cards.Count > 0 ? _cards[0] : null;

            // the shade (.is-deep) closes on a click; the panel takes the pointer so a click on it stays there
            BoxGraphic shade = MetaStyle.Shade(_root, deep: true, onClick: () => Close());
            _shade = UiNode.Of(shade.rectTransform);
            BoxGraphic panel = MetaStyle.Panel(_root, PanelX, PanelY, PanelW, PanelH);
            panel.name = "ocbg-meta-panel ocbg-meta-owned-panel";
            panel.raycastTarget = true;
            _panel = UiNode.Of(panel.rectTransform);
            RectTransform inner = PaddingBox(panel.rectTransform, PanelW, PanelH);

            // header: 所持カード, N 種, the keys (margin-left: auto), ×
            RectTransform header = Ui.Div(inner, "header", 28f, 20f, PanelW - 2f - 28f - 24f, 0f);
            UiNode.Of(header);
            Label.Make(header, MetaText.OwnedTitle, MetaStyle.Text(H2Css), "h2");
            Label.Make(header, MetaText.OwnedCount(_cards.Count), MetaStyle.Text(CountCss), "ocbg-meta-owned-count");
            TextMeshProUGUI keys = MetaStyle.Keys(header, MetaText.OwnedKeys, 0f, 0f);
            Ocb.AutoMarginLeft(keys);
            MetaButton x = MetaButton.X(header, 0f, 0f);
            x.Click += () => Close();
            Ocb.Flow(header, MetaStyle.Without(HeaderCss, "position", "left", "right", "top"), null, PanelW - 2f - 28f - 24f, null);

            _grid = Ui.Div(inner, "ocbg-meta-grid", 28f, 92f, 1100f, 830f);
            UiNode.Of(_grid);
            _pageNav = Ui.Div(inner, "ocbg-meta-pagenav", 28f, 940f, 1100f, 48f);
            UiNode.Of(_pageNav);
            _prevBtn = MetaButton.Nav(_pageNav, MetaText.PrevPage, false, 0f, 0f);
            _prevBtn.Click += () => Turn(-1);
            _pageText = Label.Make(_pageNav, "", MetaStyle.Text(MetaStyle.PageTextCss), "ocbg-meta-pagetext");
            _nextBtn = MetaButton.Nav(_pageNav, MetaText.NextPage, true, 0f, 0f);
            _nextBtn.Click += () => Turn(1);
            BoxGraphic detail = BoxGraphic.Make(inner, MetaStyle.DetailCss + DetailOwnedCss, 1160f, 92f, DetailW, DetailH, "ocbg-meta-detail is-owned");
            UiNode.Of(detail.rectTransform);
            _detail = PaddingBox(detail.rectTransform, DetailW, DetailH);

            DrawGrid(0);
            DrawDetail();
            _ps = global::Trial.Kit.Styler.Of(_panel, o: 0f, y: 14f);
            _pd = global::Trial.Kit.Styler.Of(_shade, o: 0f);
            _clock.Tween(_pd, new[] { ("o", 1f) }, 200f, Ease.Out);
            _clock.Tween(_ps, new[] { ("o", 1f), ("y", 0f) }, 260f, Ease.Out);
            _sound.Play("journalOpen");

            _popKeys = KeyStack.Push(OnKey);
        }

        /// <summary>mountOwnedCards(host, ctx) → { destroy(), close() }.</summary>
        public static IOverlay Mount(RectTransform host, OwnedCtx ctx) => new OwnedCards(host, ctx);

        // js: scenes/meta/owned.js:20
        /// <summary>Card ids in the battle deck, from a DeckBuilder, a list of CardInstances (or CardDefs) or a list of ids.</summary>
        private static Func<string, bool> DeckIds(object deck)
        {
            if (deck == null) return _ => false;
            if (deck is DeckBuilder builder) return id => builder.CountOf(id) > 0;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (deck is IEnumerable list)
            {
                foreach (object c in list)
                {
                    if (c is string s) ids.Add(s);
                    else if (c is BattleCore.CardInstance ci && ci.Def != null) ids.Add(ci.Def.Id);
                    else if (c is BattleCore.CardDef d) ids.Add(d.Id);
                }
            }
            return id => ids.Contains(id);
        }

        // js: scenes/meta/owned.js:47
        private int RankOf(string id) => _book != null ? _book.RankOf(id) : 0;

        // js: scenes/meta/owned.js:48
        private int TicksOf(string id) => _book != null ? _book.TicksOf(id) : 0;

        // js: scenes/meta/owned.js:49
        private BattleCore.MasteryProfile ProfileOf(string id) => _book != null ? _book.ProfileOf(id) : BattleCore.MasteryProfiles.Find(id);

        // js: scenes/meta/owned.js:50
        private BattleCore.CardDef Grown(BattleCore.CardDef def) => _book != null ? _book.Mastered(def) : def;

        // js: scenes/meta/owned.js:57
        private CardFace FaceFor(BattleCore.CardDef def)
        {
            if (!_faces.TryGetValue(def.Id, out CardFace face))
            {
                face = MetaCardFace.FaceOf(Grown(def));
                _faces[def.Id] = face;
            }
            return face;
        }

        // js: scenes/meta/owned.js:77
        /// <summary>
        /// pips(rank, size): .ocbg-meta-pips (.is-big): one ring per step (12 px, 20 when big; 2 px amber), filled up to
        /// the rank, gap 4 (8 when big). At (0, 0) of parent.
        /// </summary>
        private static RectTransform Pips(RectTransform parent, int rank, bool big)
        {
            float size = big ? 20f : 12f, gap = big ? 8f : 4f;
            RectTransform el = Ui.Div(parent, "ocbg-meta-pips" + (big ? " is-big" : ""), 0f, 0f, PipCount * size + (PipCount - 1) * gap, size);
            UiNode.Of(el);
            Ocb.NoShrink(el);
            for (int i = 1; i <= PipCount; i++)
            {
                string css = MetaStyle.PipCss + (big ? MetaStyle.PipBigCss : "") + (i <= rank ? MetaStyle.PipOnCss : "");
                BoxGraphic.Make(el, css, (i - 1) * (size + gap), 0f, size, size, i <= rank ? "i is-on" : "i");
            }
            return el;
        }

        // js: scenes/meta/owned.js:84
        private void DrawGrid(int dir)
        {
            int pages = Math.Max(1, CeilDiv(_cards.Count, PerPage));
            _page = Math.Max(0, Math.Min(pages - 1, _page));
            Label.SetText(_pageText, (_page + 1) + " / " + pages);
            _prevBtn.Disabled = _page == 0;
            _nextBtn.Disabled = _page >= pages - 1;
            Ocb.Flow(_pageNav, MetaStyle.PageNavCss, null, 1100f, 48f);
            Ui.Clear(_grid);
            _cells.Clear();
            List<BattleCore.CardDef> shown = _cards.Skip(_page * PerPage).Take(PerPage).ToList();
            for (int i = 0; i < shown.Count; i++) _cells.Add(MakeCell(shown[i], i));
            // the new buttons get .is-sel before the browser first styles them: no background transition
            PaintSel(instant: true);
            if (dir != 0)
            {
                for (int i = 0; i < _cells.Count; i++)
                {
                    UiNode node = _cells[i].Node;
                    global::Trial.Kit.Styler s = global::Trial.Kit.Styler.Of(node, o: 0f, x: 30f * dir);
                    _clock.Tween(s, new[] { ("o", 1f), ("x", 0f) }, 220f, Ease.Out, (i % Columns) * 16f)
                        .Then(() => { if (node) node.ResetTransform(); });
                }
            }
        }

        // js: scenes/meta/owned.js:91 (the map's body: one button.ocbg-meta-cell)
        private Cell MakeCell(BattleCore.CardDef def, int i)
        {
            int col = i % Columns, row = i / Columns;
            CardFace face = FaceFor(def);
            int rank = RankOf(def.Id);
            bool inDeck = _inDeck(def.Id);
            RectTransform cell = Ui.Div(_grid, "ocbg-meta-cell is-owned" + (inDeck ? " is-in" : ""), col * CellStepX, row * CellStepY, CellW, CellH);
            UiNode node = UiNode.Of(cell);
            BoxGraphic box = BoxGraphic.On(cell);
            CssStates states = CssStates.Of(cell);
            states.Base(MetaStyle.Without(MetaStyle.ButtonCss + MetaStyle.CellCss + CellCss, "display", "position"))
                .State(":hover", CellHoverCss)
                .State(".is-sel", CellSelCss);
            states.Apply(instant: true);

            // the card at .68, centred (align-items: center) under the 4 px padding-top; it rises 5 px on :hover
            RectTransform slot = MetaCardFace.ScaledCard(cell, face, FaceScale);
            UiNode slotNode = UiNode.Of(slot);
            slotNode.Place((CellW - slotNode.W) / 2f, 4f);
            CssStates slotStates = CssStates.Of(slot);
            slotStates.Base(CardSlotCss).State(":hover", CardSlotHoverCss);
            slotStates.Apply(instant: true);

            // the row: pips, N 段, the deck mark
            RectTransform rowEl = Ui.Div(cell, "ocbg-meta-owned-row", 10f, CellH - 10f - 34f, CellW - 20f, 34f);
            UiNode.Of(rowEl);
            Pips(rowEl, rank, false);
            Label.Make(rowEl, MetaText.RankLine(rank), MetaStyle.Text(RowCss), "span");
            if (inDeck)
            {
                RectTransform mark = Ui.Div(rowEl, "ocbg-meta-indeck", 0f, 0f, 18f, 18f);
                UiNode.Of(mark);
                Ocb.NoShrink(mark);
                FlexBox.Attrs(mark).MarginL = 4f;   // InDeckCss margin-left
                MetaUtil.Icon(mark, "draw", 18f, DsTokens.Amber);
                TitleTip.On(mark, MetaText.InDeck);
            }
            Ocb.Flow(rowEl, MetaStyle.Without(RowCss, "position", "left", "right", "bottom", "font"), null, CellW - 20f, 34f);

            ClickArea area = ClickArea.On(box);
            area.Click += _ => Select(def);
            area.Enter += _ => _sound.Play("nodeHover");
            area.HoverChanged += on =>
            {
                if (states) states.SetState(":hover", on);
                if (slotStates) slotStates.SetState(":hover", on);
            };
            return new Cell { Def = def, Root = cell, Node = node, States = states };
        }

        // js: scenes/meta/owned.js:108
        /// <summary>
        /// .is-sel on the selected card's cell. instant: the cells were made in this same call (drawGrid, or moveSel's
        /// page turn right before select): the browser styles them for the first time with the class already on, so
        /// the 140 ms background transition does not run.
        /// </summary>
        private void PaintSel(bool instant = false)
        {
            foreach (Cell c in _cells)
            {
                if (!c.States) continue;
                if (c.States.SetState(".is-sel", _selected != null && c.Def.Id == _selected.Id, false)) c.States.Apply(instant);
            }
        }

        // js: scenes/meta/owned.js:110
        private void DrawDetail()
        {
            Ui.Clear(_detail);
            if (_selected == null)
            {
                // .ocbg-meta-detail-hint: across the box, centred on its middle (top 50%, translateY(-50%))
                TextStyle hs = MetaStyle.Text(MetaStyle.DetailHintCss);
                hs.Align = TextAlign.Center;
                float hh = hs.LineHeightPx;
                Label.Make(_detail, MetaText.OwnedNone, hs, 0f, (DetailH - 2f) / 2f - hh / 2f, DetailW - 2f, hh, "ocbg-meta-detail-hint");
                return;
            }
            BattleCore.CardDef def = _selected;
            BattleCore.CardDef g = Grown(def);
            CardFace face = FaceFor(def);
            int rank = RankOf(def.Id);
            int ticks = TicksOf(def.Id);
            BattleCore.MasteryProfile profile = ProfileOf(def.Id);
            OcbCard big = MetaCardFace.CardEl(_detail, face);
            big.Node.Place(20f, 20f);

            // the text column: the grown name, 元の名, デッキの札
            const float headW = 372f;
            RectTransform head = Ui.Div(_detail, "ocbg-meta-detail-text", 260f, 20f, headW, 0f);
            UiNode.Of(head);
            float y = 0f;
            y += Block(head, g.Name, MetaStyle.Text(MetaStyle.DetailH3Css), 0f, y, headW, "h3") + 12f;
            if (g.Name != def.Name) y += Block(head, MetaText.BaseName(def.Name), MetaStyle.Text(MetaStyle.DetailPCss), 0f, y, headW, "p") + 8f;
            if (_inDeck(def.Id))
            {
                TextStyle ls = MetaStyle.Text(MetaStyle.DetailPCss + InDeckLineCss);
                float lh = ls.LineHeightPx;
                RectTransform line = Ui.Div(head, "ocbg-meta-indeck-line", 0f, y, headW, lh);
                UiNode.Of(line);
                Ui.Place(MetaUtil.Icon(line, "draw", 18f, DsTokens.Amber).rectTransform, 0f, (lh - 18f) / 2f);
                Label.Make(line, MetaText.InDeck, ls, 18f + 8f, 0f, null, null, "text");
                y += lh + 8f;
            }
            UiNode.Of(head).H = y;

            // 習熟 / 永続能力 / the rest of the detail, in a scrolling column
            ScrollArea below = ScrollArea.Make(_detail, "ocbg-meta-owned-below", 20f, 344f, BelowW, BelowH);
            below.ShowThumb = true;
            BoxGraphic.On(below.Thumb, ThumbCss);
            float total = DrawBelow(below.Content, BelowW, def, g, face, rank, ticks, profile);
            if (total > BelowH + 0.5f)
            {
                // a custom ::-webkit-scrollbar takes its 8 px from the content
                Ui.Clear(below.Content);
                total = DrawBelow(below.Content, BelowW - below.ThumbWidth, def, g, face, rank, ticks, profile);
            }
            below.Content.sizeDelta = new Vector2(BelowW, total);
            below.ScrollHeight = total;
            below.Refresh();

            global::Trial.Kit.Styler s = global::Trial.Kit.Styler.Of(big.Node, o: 0f, y: 8f);
            _clock.Tween(s, new[] { ("o", 1f), ("y", 0f) }, 200f, Ease.Out);
        }

        // js: scenes/meta/owned.js:128-164 (the below box's three blocks, a column with gap 24); returns its height
        private float DrawBelow(RectTransform below, float w, BattleCore.CardDef def, BattleCore.CardDef g, CardFace face, int rank, int ticks,
            BattleCore.MasteryProfile profile)
        {
            const float gap = 24f;

            // 習熟: the step, and the ticks toward the next one against the profile's threshold.
            RectTransform m = Section(below, "ocbg-meta-mastery", 0f, w);
            float y = SectionTop(m, MetaText.Mastery, w);
            RectTransform rankRow = Ui.Div(m, "ocbg-meta-mastery-rank", 0f, y, w, 0f);
            UiNode.Of(rankRow);
            Pips(rankRow, rank, true);
            Label.Make(rankRow, MetaText.RankLine(rank), MetaStyle.Text(MasteryRankBCss), "b");
            y += Ocb.Flow(rankRow, MetaStyle.Without(MasteryRankCss, "display"), null, w, null).Height + 10f;
            TextStyle ps = MetaStyle.Text(MasteryPCss);
            // The bridge's verdicts (js: rank >= TopRank; rank === TopRank - 1 && topHolderOther(id)). Without a book every
            // card is at step 0: neither holds.
            MasteryProgressInfo info = _book != null ? MasteryView.MasteryProgress(_book, def.Id) : null;
            bool top = info != null && info.Top;
            bool capped = info != null && info.Capped;
            if (profile == null)
            {
                y += Block(m, MetaText.NoGrowth, ps, 0f, y, w, "p") + 10f;
            }
            else if (top)
            {
                y += Block(m, MetaText.TopRank, ps, 0f, y, w, "p") + 10f;
            }
            else if (capped)
            {
                // One card of a life holds 3 段 (concept-v3 §14 1 つの生の段数; MasteryBook.Rerank stops the rest at 2).
                // The ticks keep counting but buy no step, so the bar and 次の名 would read as stuck.
                string holderId = _book.TopRankHolder;
                BattleCore.CardDef holder = BattleCore.CardCatalog.All.FirstOrDefault(d => d.Id == holderId);
                string holderName = holder != null ? Grown(holder).Name : holderId;
                y += CappedLine(m, MetaText.TopTaken(holderName), y, w) + 10f;
                y += Block(m, MetaText.TicksSoFar(ticks), MetaStyle.Text(MasteryPCss + TicksPCss), 0f, y, w, "p ocbg-meta-mastery-ticks") + 10f;
            }
            else
            {
                int need = info != null && info.Need.HasValue ? info.Need.Value : profile.Thresholds[rank];
                y += NextRow(m, ticks, need, y, w) + 10f;
                string nextName = profile.NameAt(rank + 1);
                if (!string.IsNullOrEmpty(nextName)) y += Block(m, MetaText.NextName(nextName), ps, 0f, y, w, "p") + 10f;
            }
            float mh = y - 10f;
            SizeSection(m, w, mh);
            float at = mh + gap;

            // 永続能力: a stance card's lasting rows (CardFaceText.LastingLines), or none.
            RectTransform lasting = Section(below, "ocbg-meta-lasting", at, w);
            y = SectionTop(lasting, MetaText.Lasting, w);
            if (face.Lasting && face.TextLines != null && face.TextLines.Count > 0)
            {
                foreach (string l in face.TextLines) y += IconLine(lasting, l, ps, y, w) + 10f;
            }
            else
            {
                y += Block(lasting, MetaText.NoLasting, ps, 0f, y, w, "p") + 10f;
            }
            float lhgt = y - 10f;
            SizeSection(lasting, w, lhgt);
            at += lhgt + gap;

            // the rest of the detail lines (the name is the h3 already); the flavour line in the display face
            List<string> lines = DeckBuilder.ShownDetailOf(g);
            RectTransform more = Ui.Div(below, "ocbg-meta-owned-lines", 0f, at, w, 0f);
            UiNode.Of(more);
            y = 0f;
            foreach (string l in lines.Skip(1))
            {
                bool flavour = l.StartsWith("「", StringComparison.Ordinal);
                TextStyle s = MetaStyle.Text(LinesPCss + (flavour ? MetaStyle.FlavourCss : ""));
                y += Block(more, l, s, 0f, y, w, flavour ? "p is-flavour" : "p") + 6f;
            }
            UiNode.Of(more).H = y;
            return at + y;
        }

        /// <summary>A .ocbg-meta-mastery / .ocbg-meta-lasting block at (0, y): its border-top rule (sized later).</summary>
        private static RectTransform Section(RectTransform parent, string name, float y, float w)
        {
            RectTransform el = Ui.Div(parent, name, 0f, y, w, 0f);
            UiNode.Of(el);
            BoxGraphic.On(el, MetaStyle.Without(MasteryCss, "display", "flex-direction", "gap"));
            return el;
        }

        /// <summary>The block's h4 under its rule and 12 px padding-top; returns where the next row starts (gap 10).</summary>
        private static float SectionTop(RectTransform section, string title, float w)
        {
            float y = 1f + 12f;
            TextStyle hs = MetaStyle.Text(H4Css);
            Label.Make(section, title, hs, 0f, y, null, null, "h4");
            return y + hs.LineHeightPx + 10f;
        }

        private static void SizeSection(RectTransform section, float w, float h)
        {
            UiNode n = UiNode.Of(section);
            n.W = w;
            n.H = h;
        }

        /// <summary>A block of text (h3 / h4 / p) at (x, y), w wide, as tall as its lines; returns its height.</summary>
        private static float Block(RectTransform parent, string text, TextStyle style, float x, float y, float w, string name)
        {
            float h = Math.Max(style.LineHeightPx, TextMeasure.Height(text ?? "", style, w));
            Label.Make(parent, text ?? "", style, x, y, w, h, name);
            return h;
        }

        /// <summary>
        /// p.ocbg-meta-mastery-capped (display: block; padding 8 12; border-left 2px steel; steel 10 %) at (0, y);
        /// returns its height.
        /// </summary>
        private static float CappedLine(RectTransform parent, string text, float y, float w)
        {
            TextStyle s = MetaStyle.Text(MasteryPCss + CappedCss);
            const float padX = 12f, padY = 8f, border = 2f;
            float tw = w - border - padX * 2f;
            float th = Math.Max(s.LineHeightPx, TextMeasure.Height(text, s, tw));
            float h = th + padY * 2f;
            BoxGraphic box = BoxGraphic.Make(parent, CappedCss, 0f, y, w, h, "p ocbg-meta-mastery-capped");
            Label.Make(box.rectTransform, text, s, border + padX, padY, tw, th, "text");
            return h;
        }

        /// <summary>
        /// .ocbg-meta-mastery-next at (0, y): 「次の段まで」, the ticks bar (1fr) and 「刻み t / need」, a grid of auto / 1fr /
        /// auto with gap 12, centred on the row; returns the row's height.
        /// </summary>
        private static float NextRow(RectTransform parent, int ticks, int need, float y, float w)
        {
            TextStyle spanStyle = MetaStyle.Text(MasteryNextCss);
            TextStyle bStyle = MetaStyle.Text(MasteryNextCss + MasteryNextBCss);
            string label = MetaText.NextRank, count = MetaText.TicksLine(ticks, need);
            float spanW = TextMeasure.Width(label, spanStyle), bW = TextMeasure.Width(count, bStyle);
            float rowH = Math.Max(spanStyle.LineHeightPx, Math.Max(bStyle.LineHeightPx, 10f));
            RectTransform row = Ui.Div(parent, "ocbg-meta-mastery-next", 0f, y, w, rowH);
            UiNode.Of(row);
            Label.Make(row, label, spanStyle, 0f, (rowH - spanStyle.LineHeightPx) / 2f, null, null, "span");
            float barX = spanW + 12f, barW = Math.Max(0f, w - spanW - bW - 24f);
            BoxGraphic bar = BoxGraphic.Make(row, TicksCss, barX, (rowH - 10f) / 2f, barW, 10f, "ocbg-meta-ticks");
            // i: the padding box's height, min(100, ticks / need) of its width, clipped by the rounded overflow: hidden
            float pct = Math.Min(100f, 100f * ticks / need);
            BoxGraphic fill = BoxGraphic.Make(bar.rectTransform, TicksFillCss, 1f, 1f, Math.Max(0f, barW - 2f) * pct / 100f, 8f, "i");
            List<Vec2> clip = bar.ClipPolygon(paddingBox: true);
            fill.ClipConvex = clip.Select(p => new Vec2(p.X - 1f, p.Y - 1f)).ToList();
            Label.Make(row, count, bStyle, w - bW, (rowH - bStyle.LineHeightPx) / 2f, null, null, "b");
            return rowH;
        }

        /// <summary>A lasting row (p: flex, centred, gap 8): ∞ 18 px hp-text and the text; returns its height.</summary>
        private static float IconLine(RectTransform parent, string text, TextStyle style, float y, float w)
        {
            float tw = Math.Max(1f, w - 18f - 8f);
            float th = Math.Max(style.LineHeightPx, TextMeasure.Height(text, style, tw));
            float h = Math.Max(th, 18f);
            RectTransform p = Ui.Div(parent, "p", 0f, y, w, h);
            UiNode.Of(p);
            Ui.Place(MetaUtil.Icon(p, "infinity", 18f, DsTokens.HpText).rectTransform, 0f, (h - 18f) / 2f);
            Label.Make(p, text, style, 18f + 8f, (h - th) / 2f, tw, th, "text");
            return h;
        }

        // js: scenes/meta/owned.js:169
        /// <summary>select(def). freshCells: the grid was redrawn in this same call (moveSel's page turn).</summary>
        private void Select(BattleCore.CardDef def, bool freshCells = false)
        {
            if (_selected != null && _selected.Id == def.Id) return;
            _selected = def;
            _sound.Play("uiClick");
            PaintSel(freshCells);
            DrawDetail();
        }

        // js: scenes/meta/owned.js:176
        private void Turn(int by)
        {
            int pages = Math.Max(1, CeilDiv(_cards.Count, PerPage));
            int next = Math.Max(0, Math.Min(pages - 1, _page + by));
            if (next == _page) return;
            _page = next;
            _sound.Play("nodeHover");
            DrawGrid(by);
        }

        // js: scenes/meta/owned.js:185
        /// <summary>The keyboard walks the whole list (a page turns under the cursor); ↑↓ move a row within the page.</summary>
        private void MoveSel(int by, bool rowStep = false)
        {
            if (_cards.Count == 0) return;
            int i = _selected != null ? _cards.FindIndex(d => d.Id == _selected.Id) : -1;
            int j = i < 0 ? 0 : i + by;
            if (rowStep && (FloorDiv(j, PerPage) != FloorDiv(i, PerPage) || j < 0 || j >= _cards.Count)) return;
            j = Math.Max(0, Math.Min(_cards.Count - 1, j));
            if (j == i) return;
            int p = FloorDiv(j, PerPage);
            bool turned = false;
            if (p != _page)
            {
                int by2 = p > _page ? 1 : -1;
                _page = p;
                _sound.Play("nodeHover");
                DrawGrid(by2);
                turned = true;
            }
            Select(_cards[j], turned);
        }

        // js: scenes/meta/owned.js:205
        public void Close() => CloseAsync().Forget();

        private async Promise CloseAsync()
        {
            if (_closing) return;
            _closing = true;
            _sound.Play("journalClose");
            _clock.Tween(_pd, new[] { ("o", 0f) }, 160f, Ease.In).Forget();
            await _clock.Tween(_ps, new[] { ("o", 0f), ("y", 10f) }, 170f, Ease.In);
            if (_alive) _ctx.OnClose?.Invoke();
        }

        // js: scenes/meta/owned.js:214
        private bool OnKey(KeyEvent e)
        {
            if (_closing) return true;
            // only a fresh press closes (a held C or Esc repeats; the screen under it would reopen it)
            if (e.Key == "Escape" || e.Key == "c" || e.Key == "C") { if (!e.Repeat) Close(); return true; }
            if (e.Key == "PageUp") { Turn(-1); return true; }
            if (e.Key == "PageDown") { Turn(1); return true; }
            if (e.Key == "ArrowLeft") { MoveSel(-1); return true; }
            if (e.Key == "ArrowRight") { MoveSel(1); return true; }
            if (e.Key == "ArrowUp") { MoveSel(-Columns, true); return true; }
            if (e.Key == "ArrowDown") { MoveSel(Columns, true); return true; }
            return MetaUtil.ModalSwallow(e);
        }

        // js: scenes/meta/owned.js:229
        public void Destroy()
        {
            _alive = false;
            _popKeys?.Invoke();
            _popKeys = null;
            _clock.Stop();
            Ui.Remove(_root);
        }

        /// <summary>owned.js has no refresh.</summary>
        public void Refresh() { }

        // ---- helpers ------------------------------------------------------------------------------------------------

        /// <summary>An element's padding box (inside its 1 px border): where its absolutely placed children are measured from.</summary>
        private static RectTransform PaddingBox(RectTransform el, float w, float h)
        {
            RectTransform inner = Ui.Div(el, "padding-box", 1f, 1f, w - 2f, h - 2f);
            UiNode.Of(inner);
            return inner;
        }

        /// <summary>Math.ceil(a / b) for a ≥ 0, b > 0.</summary>
        private static int CeilDiv(int a, int b) => (a + b - 1) / b;

        /// <summary>Math.floor(a / b) for b > 0 (−1 / 18 is −1, as in the JS).</summary>
        private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    }
}
