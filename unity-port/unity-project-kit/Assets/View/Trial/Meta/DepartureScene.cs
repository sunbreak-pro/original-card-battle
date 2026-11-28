// 出立 (CAMP_FACILITIES_DESIGN.md §3.2) as one screen, 「支度の卓」 (#1051, the design canvas's 出立：支度の卓): the JS's two
// steps (デッキを組む, departure.js:200-507; 出立の支度, departure.js:509-658) laid on one table. The owned cards are on
// the left as small cards, the shelf of tools and consumables under them, and the pack on the right: the deck as a pile
// with its count and gauge, the tool and consumable slots, one reading panel for the card or thing under the pointer,
// the preset decks and 出立する. The shared header band stands on top (#1074): 「拠点」 with 「支度の卓」 and the chip back,
// the collapse calendar, HP and stamina at full, and the deck's count, kept as the deck changes. The JS's talent is
// gone since #1045 and the attribute breakdown of the canvas's pack is left out (2026-11-25 Koudai-san; the detail
// view is #1054).
// This file holds the screen, its band, the cards, the pack, the reading panel, the leave, the keys and the draft;
// the shelf and the slots are DepartureSuppliesView.cs. DepartureSupplies.js is Trial.Bridge.DepartureCarry, cardFace.js
// is MetaCardFace, and every box's place and size is Trial.Bridge.DepartTable (held under dotnet test).
//
// It decides nothing: what a filter keeps, what a page holds, whether a card may go in, every word about a card and
// whether the life may set out come from Depiction.Bridge.DeckBuilder / DepartureBuilder (whose rules are the core's),
// built with the life's DeckLimits (ctx.LifeStart's DeckMin / DeckMax / ToolSlots / ConsumableSlots, the survivor's
// bonus included).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Depiction;
using Depiction.Bridge;
using Exploration.Bridge;
using InheritanceCore;
using TMPro;
using Trial.Bridge;
using Trial.Geometry;
using Trial.Header;
using Trial.Kit;
using Trial.Script;
using UnityEngine;
using UnityEngine.EventSystems;
using TableCss = Trial.Bridge.DepartTable.Css;
using TextMeasure = Trial.Kit.TextMeasure;

namespace Trial.Meta
{
    public sealed class DepartureScene : IScene
    {
        // ---- departure.js: constants ------------------------------------------------------------------------------

        // js: scenes/meta/departure.js:40 — the attribute's icon and its role colour (battle-visual-v1 §2.2), keyed by BattleAttribute.
        internal static readonly IReadOnlyDictionary<BattleCore.BattleAttribute, string> AttrIcon = new Dictionary<BattleCore.BattleAttribute, string>
        {
            [BattleCore.BattleAttribute.Attack] = "sword",
            [BattleCore.BattleAttribute.Guard] = "shield",
            [BattleCore.BattleAttribute.Skill] = "star",
            [BattleCore.BattleAttribute.Stance] = "flag",
        };

        // js: scenes/meta/departure.js:41
        internal static readonly IReadOnlyDictionary<BattleCore.BattleAttribute, Rgba> AttrInk = new Dictionary<BattleCore.BattleAttribute, Rgba>
        {
            [BattleCore.BattleAttribute.Attack] = DsTokens.Phosphor,
            [BattleCore.BattleAttribute.Guard] = DsTokens.Guard,
            [BattleCore.BattleAttribute.Skill] = DsTokens.Amber,
            [BattleCore.BattleAttribute.Stance] = DsTokens.HpText,
        };

        // js: scenes/meta/departure.js:44 — a design-document citation inside an item's note (「（seven_layers_v4.md §3.2）」): not for the player.
        private static readonly Regex DocCitation = new Regex("（[^（）]*\\.md[^（）]*）", RegexOptions.CultureInvariant);

        // js: scenes/meta/departure.js:49 — a sentence carrying a design-status remark (「…（アーマーは凍結）」): it goes with its remark.
        private static readonly Regex DesignStatus = new Regex("。?[^。（）]*（[^（）]*凍結[^（）]*）", RegexOptions.CultureInvariant);

        // js: scenes/meta/departure.js:50
        /// <summary>playerNote(note): an item's note without the design citations and the design-status sentences.</summary>
        internal static string PlayerNote(string note) => DesignStatus.Replace(DocCitation.Replace(note ?? "", ""), "").Trim();

        // ---- the draft (departure.js:52-90) --------------------------------------------------------------------------

        /// <summary>
        /// 出立's draft: the deck being built and the supplies picked, not yet set out. It is kept while the explorer steps
        /// back to the base (継承の間 in the generations) and comes again, over closing the game and opening it again, so ← or a closed window
        /// throws nothing away. Only the life it was made for reads it back (Trial.Bridge.DepartureDraft, which the base's
        /// way down reads too, #1052); 出立する clears it, since the router then keeps the deck itself (ctx.SavedDeck). A
        /// per-device convenience: memory first, the store behind it, every access guarded.
        /// </summary>
        // js: scenes/meta/departure.js:62
        public const string DraftKey = DepartureDraft.Key;

        /// <summary>
        /// Where the draft is kept (the browser's localStorage): the app's store when the router sets one (app.md §2.1:
        /// departure-draft.v1.json under the save folder), else PlayerPrefs under <see cref="DraftKey"/>.
        /// </summary>
        public static IKeyValueStore DraftStore { get; set; }

        private static DepartureDraft _draftInMemory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _draftInMemory = null;
            DraftStore = null;
        }

        private static IKeyValueStore Store => DraftStore ?? PrefsStore.Instance;

        // js: scenes/meta/departure.js:77
        /// <summary>readDraft(id): memory first, then the store (the browser's older sessionStorage copy has no counterpart here).</summary>
        internal static DepartureDraft ReadDraft(string id)
        {
            if (id == null) return null;
            if (_draftInMemory != null && _draftInMemory.Id == id) return _draftInMemory;
            return DepartureDraft.Read(Store, id);
        }

        // js: scenes/meta/departure.js:81
        private static void WriteDraft(DepartureDraft d)
        {
            if (d == null) return;
            _draftInMemory = d;
            d.Write(Store);
        }

        // js: scenes/meta/departure.js:85
        /// <summary>The draft goes (the life sets out with its deck: the router keeps it from here).</summary>
        internal static void ClearDraft()
        {
            _draftInMemory = null;
            DepartureDraft.Clear(Store);
        }

        /// <summary>
        /// The pack the table opens with for ctx, as the base's way down shows it and sets out with it (#1052): this life's
        /// draft, else ctx.SavedDeck, else the starter deck (Trial.Bridge.DeparturePack.Open).
        /// </summary>
        internal static DeparturePack PackOf(DepartureCtx ctx) =>
            DeparturePack.Open(ctx.LifeStart, ctx.Owned, ctx.OwnsCatalogue, ctx.SavedDeck, ReadDraft(DepartureDraft.IdOf(ctx.LifeStart)));

        /// <summary>The draft kept in PlayerPrefs (the default store): one string per key, every access guarded.</summary>
        private sealed class PrefsStore : IKeyValueStore
        {
            public static readonly PrefsStore Instance = new PrefsStore();

            public string Read(string key)
            {
                try { return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null; }
                catch (Exception) { return null; }
            }

            public string ReadHead(string key, int maxChars)
            {
                string s = Read(key);
                return s == null ? null : s.Length <= maxChars ? s : s.Substring(0, Math.Max(0, maxChars));
            }

            public bool Write(string key, string text)
            {
                try
                {
                    PlayerPrefs.SetString(key, text ?? "");
                    PlayerPrefs.Save();
                    return true;
                }
                catch (Exception) { return false; }
            }

            public void Remove(string key)
            {
                try
                {
                    PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save();
                }
                catch (Exception) { /* storage unavailable */ }
            }
        }

        // ---- the screen's state ----------------------------------------------------------------------------------

        private readonly DepartureCtx _ctx;
        private readonly MetaSound _sound;
        private readonly LifeStart _lifeStart;
        private readonly List<string> _owned;   // null: the whole catalogue
        private readonly BattleCore.DeckLimits _limits;
        private readonly int _deckMin, _deckCap;
        private readonly RectTransform _root;
        private readonly MetaBackdrop _backdrop;
        private readonly SceneClock _clock;
        private bool _alive = true;
        private bool _busy;

        private string _notice = "";
        private bool _noticeOverCap;
        private DeckBuilder _deck;
        private readonly string _draftId;
        private readonly DepartureBuilder _supplies;
        private bool _settingOut;
        private readonly DeckFilter _filter = new DeckFilter();
        private int _page;
        private BattleCore.CardDef _selected;
        private int _randomSeed;
        private readonly Dictionary<string, CardFace> _faces = new Dictionary<string, CardFace>(StringComparer.Ordinal);

        // what the reading panel reads: the thing or the card under the pointer, else the selected card
        private SupplyOption _hoverItem;
        private string _hoverItemKind;
        private BattleCore.CardDef _hoverCard;

        // the header band
        private HeaderBar _header;

        // the cards
        private RectTransform _filters;
        private readonly List<FilterButton> _attrBtns = new List<FilterButton>();
        private readonly List<FilterButton> _costBtns = new List<FilterButton>();
        private RectTransform _grid;
        private List<Cell> _cells = new List<Cell>();
        private RectTransform _pageNav;
        private MetaButton _prevBtn, _nextBtn;
        private TextMeshProUGUI _pageText;

        // the pack
        private RectTransform _pile;
        private TextMeshProUGUI _totalEl;
        private MetaRollNumber _total;
        private RectTransform _gaugeFill;
        private CssStates _gaugeFillStates;
        private TextMeshProUGUI _deckLine;
        private RectTransform _readerBody;
        private StatusLine _statusEl;
        private readonly List<MetaButton> _presets = new List<MetaButton>();
        private MetaButton _setOutBtn;

        // the shelf and the slots
        private DepartureSuppliesView _supView;

        // the keys and the focus
        private readonly Action _popKeys;
        private readonly FocusScope _scope;
        private readonly Dictionary<MetaButton, Focusable> _metaFocus = new Dictionary<MetaButton, Focusable>();
        private bool _tabbed;

        private DepartureScene(RectTransform host, DepartureCtx ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _sound = MetaUtil.SoundOf(ctx);
            _lifeStart = ctx.LifeStart;
            // js: scenes/meta/departure.js:109 — the kinds the heir may build from: the router's (or the starter's eleven), plus
            // the preset deck's (Expedition.BeginLife owns its start deck's kinds anyway), so the preset opens whole. The preset
            // is the starter deck since #1038, where the JS's is the prototype deck. The life's limits, the deck and the
            // supplies are the pack the base's way down shows (DeparturePack.Open, #1052).
            DeparturePack pack = PackOf(ctx);
            _owned = pack.Owned;
            _limits = pack.Limits;
            _deckMin = _limits.DeckMin;
            _deckCap = _limits.DeckMax;
            _root = MetaUtil.SceneRoot(host, "departure");
            _backdrop = MetaBackdrop.Create(_root, HallBackdropSpec.For(0.5), new MetaBackdrop.Options { Vignette = 0.6f });
            _clock = MetaUtil.SceneClock((dt, now) => { if (_backdrop != null) _backdrop.Paint(now); }, ctx.Stage);
            _scope = Focus.PushScope("departure");

            // ---- state (the bridge's builders) ----
            // A saved deck (or a draft) past the life's cap — a survivor's 45 after the bonus ended — opens with every card
            // and the reason (DeckBuilder.ReadOverCap); any other broken string, as the C# reads it.
            _draftId = pack.DraftId;
            _deck = pack.Deck;
            _notice = pack.Notice;
            _noticeOverCap = pack.OverCap;
            _supplies = pack.Supplies;
            // js: scenes/meta/departure.js:157 — (Seed & 0x3fffffff) || 1
            ulong seed = _lifeStart != null ? _lifeStart.Seed : (ctx.Seed ?? 1UL);
            _randomSeed = (int)(seed & 0x3fffffffUL);
            if (_randomSeed == 0) _randomSeed = 1;

            BuildCards();
            BuildPack();
            _supView = new DepartureSuppliesView(this, _root);
            BuildHeader();

            // ---- first frame (departure.js:737-754) ----
            DrawFilters();
            DrawGrid(0);
            _selected = _cells.Count > 0 ? _cells[0].Def : null;
            RefreshDeck();
            _supView.RefreshSupplies();
            DrawReader();
            RectTransform enterEl = MetaStyle.Fade(_root);
            global::Trial.Kit.Styler enter = global::Trial.Kit.Styler.Of(enterEl, o: 1f);
            _clock.Tween(enter, "o", 0f, 500f, Ease.Out).Then(() => { if (enterEl) Ui.Remove(enterEl); });
            for (int i = 0; i < _cells.Count; i++)
            {
                Cell c = _cells[i];
                global::Trial.Kit.Styler s = global::Trial.Kit.Styler.Of(c.Node, o: 0f, y: 18f);
                _clock.Tween(s, new[] { ("o", 1f), ("y", 0f) }, 380f, Ease.Out, 150f + i * 28f).Then(() => { if (c.Node) c.Node.ResetTransform(); });
            }
            SyncFocus();

            // ---- keyboard: a cursor over the page's cards (the selected card), + / − for its count ----
            KeyStack.PointerDownCapture += OnPointerDownCapture;
            _popKeys = KeyStack.Push(OnKey);
        }

        // js: scenes/meta/departure.js:103
        /// <summary>
        /// mountDeparture(host, ctx) → { destroy() }. With ctx.OnSettings, Esc opens the settings (CAMP_FACILITIES_DESIGN.md
        /// §3.5); without it Esc goes back. A draft of the same life wins over ctx.SavedDeck: the screen reopens as it was left.
        /// </summary>
        public static IScene Mount(RectTransform host, DepartureCtx ctx) => new DepartureScene(host, ctx);

        // ---- what the supplies view reads ------------------------------------------------------------------------------

        internal DepartureCtx Ctx => _ctx;

        internal MetaSound Sound => _sound;
        internal SceneClock Clock => _clock;
        internal DeckBuilder Deck => _deck;
        internal DepartureBuilder Supplies => _supplies;
        internal BattleCore.DeckLimits Limits => _limits;
        internal bool Busy => _busy;

        // js: scenes/meta/departure.js:148
        /// <summary>keepDraft(): the draft written (not once the life sets out, nor without a life to key it).</summary>
        internal void KeepDraft()
        {
            if (_draftId == null || _settingOut) return;
            WriteDraft(new DepartureDraft
            {
                Id = _draftId,
                Deck = _deck.Save(),
                Tools = _supplies.Tools.ToList(),
                Consumables = _supplies.Consumables.ToList(),
            });
        }

        // js: scenes/meta/departure.js:123
        /// <summary>adopt(src): a deck on the owned kinds with the life's limits (WithOwned keeps a deck over the cap).</summary>
        private DeckBuilder Adopt(DeckBuilder src) => src.WithOwned(_owned);

        // js: scenes/meta/departure.js:159
        private CardFace FaceFor(BattleCore.CardDef def)
        {
            if (!_faces.TryGetValue(def.Id, out CardFace face))
            {
                face = MetaCardFace.FaceOf(def);
                _faces[def.Id] = face;
            }
            return face;
        }

        // js: scenes/meta/departure.js:160
        private bool IsOwned(string id) => _owned == null || _owned.Contains(id);

        // ================================================================================ the header band

        /// <summary>
        /// The shared band (#1074): 「拠点へ戻る」 goes back as Esc does without the settings; the owned cards stay lit (this is
        /// their screen); the journal and the settings open as J and Esc do, and stand dimmed when the router gives no way
        /// to them. The survivor's bonus, which stood beside the old title, moves to the tool heading's row (DrawBonus).
        /// </summary>
        private void BuildHeader()
        {
            _header = HeaderBar.Create(_root, DepartTable.BandOf(_lifeStart?.Lineage?.Calendar, _deck.Total), new HeaderActions
            {
                OnBack = Back,
                OnJournal = OpenJournal,
                OnSettings = OpenSettings,
            });
            if (_ctx.OnJournal == null) _header.SetButton(HeaderButtonKind.Journal, HeaderButtonState.Off);
            if (_ctx.OnSettings == null) _header.SetButton(HeaderButtonKind.Settings, HeaderButtonState.Off);
            DrawBonus();
        }

        // js: scenes/meta/departure.js:165
        /// <summary>
        /// bonusChip(line): what the life's limits raise (the bridges' RaisedLine under DepartureBuilder.BonusLabel), as a tag
        /// at the right end of the tool heading's row (DepartTable.BonusTag); nothing on a life without the survivor's
        /// bonus. A line cut by the tag's width reads whole on hover.
        /// </summary>
        private void DrawBonus()
        {
            string line = string.Join("、", new[] { _deck.RaisedLine, _supplies.RaisedLine }.Where(l => !string.IsNullOrEmpty(l)));
            if (line.Length == 0) return;
            TextStyle labelS = MetaStyle.Text(TableCss.BonusLabel), lineS = MetaStyle.Text(TableCss.BonusLine);
            string label = DepartureBuilder.BonusLabel;
            float labelW = TextMeasure.Width(label, labelS), lineW = TextMeasure.Width(line, lineS);
            float lead = DepartTable.BonusDot + DepartTable.BonusDotGap + labelW + DepartTable.BonusWordsGap;
            TableBox tag = DepartTable.BonusTag(lead + lineW);
            BoxGraphic box = BoxGraphic.Make(_root, TableCss.Bonus, tag.X, tag.Y, tag.W, tag.H, "ocbg-meta-bonus");
            float x = 1f + DepartTable.BonusPadX;
            BoxGraphic.Make(box.rectTransform, TableCss.BonusDot, x, (tag.H - DepartTable.BonusDot) / 2f, DepartTable.BonusDot, DepartTable.BonusDot, "dot");
            x += DepartTable.BonusDot + DepartTable.BonusDotGap;
            Label.Make(box.rectTransform, label, labelS, x, (tag.H - labelS.LineHeightPx) / 2f, null, null, "b");
            x += labelW + DepartTable.BonusWordsGap;
            float shownW = Math.Max(0f, tag.W - 1f - DepartTable.BonusPadX - x);
            Label.Make(box.rectTransform, line, lineS, x, (tag.H - lineS.LineHeightPx) / 2f, shownW, null, "span");
            if (shownW < lineW) TitleTip.On(box, label + "　" + line);
        }

        // ================================================================================ the cards (departure.js:200-507)

        private void BuildCards()
        {
            BuildFilters();

            // the card grid
            _grid = Ui.Div(_root, "ocbg-meta-grid", 0f, 0f, MetaUtil.W, MetaUtil.H);
            UiNode.Of(_grid);

            // page nav (DeckSelectScreen: 前のページ / n / m ページ（k 種）/ 次のページ), and the keys at its right end
            TableBox nav = DepartTable.PageNav;
            _pageNav = Ui.Div(_root, "ocbg-meta-pagenav", nav.X, nav.Y, nav.W, nav.H);
            UiNode.Of(_pageNav);
            _prevBtn = MetaButton.Nav(_pageNav, MetaText.PrevPage, false, 0f, 0f, small: true);
            _prevBtn.Click += () => Turn(-1);
            _pageText = Label.Make(_pageNav, "", MetaStyle.Text(TableCss.PageText), "ocbg-meta-pagetext");
            _nextBtn = MetaButton.Nav(_pageNav, MetaText.NextPage, true, 0f, 0f, small: true);
            _nextBtn.Click += () => Turn(1);
            TextStyle keys = MetaStyle.Text(TableCss.Keys);
            keys.Align = TextAlign.Right;
            Label.Make(_pageNav, MetaText.DeckKeys, keys, 0f, (nav.H - keys.LineHeightPx) / 2f, nav.W, null, "ocbg-meta-keys");
        }

        // ---- filters (DeckBuilder.ShownAttributeOptions — the battle's words, X-17 — / CostOptions) ----

        private sealed class FilterButton
        {
            public DepButton Button;
            public BattleCore.BattleAttribute Attribute;
            public int Cost;
            public float W;
        }

        private void BuildFilters()
        {
            TableBox row = DepartTable.FilterRow;
            _filters = Ui.Div(_root, "ocbg-meta-filters", row.X, row.Y, row.W, row.H);
            UiNode.Of(_filters);
            TextStyle headS = MetaStyle.Text(TableCss.Heading);
            Label.Make(_filters, DepartTable.CardsHeading, headS, 0f, (row.H - headS.LineHeightPx) / 2f, null, null, "h2");
            float x = TextMeasure.Width(DepartTable.CardsHeading, headS) + 20f;
            foreach (KeyValuePair<string, BattleCore.BattleAttribute> opt in DeckBuilder.ShownAttributeOptions)
            {
                FilterButton f = MakeFilter(opt.Key, false, opt.Value, 0, x);
                f.Button.OnClick = () =>
                {
                    _filter.Attribute = f.Attribute;
                    _page = 0;
                    _sound.Play("uiClick");
                    DrawFilters();
                    DrawGrid(0);
                };
                _attrBtns.Add(f);
                x += f.W + DepartTable.FilterGap;
            }
            // the rule between the two groups
            BoxGraphic.Make(_filters, "background: var(--rule);", x + 2f, (row.H - 32f) / 2f, 1f, 32f, "gap");
            x += 6f + DepartTable.FilterGap;
            foreach (KeyValuePair<string, int> opt in DeckBuilder.CostOptions)
            {
                FilterButton f = MakeFilter(opt.Key, true, BattleCore.BattleAttribute.None, opt.Value, x);
                f.Button.OnClick = () =>
                {
                    _filter.Cost = f.Cost;
                    _page = 0;
                    _sound.Play("uiClick");
                    DrawFilters();
                    DrawGrid(0);
                };
                _costBtns.Add(f);
                x += f.W + DepartTable.FilterGap;
            }
        }

        /// <summary>
        /// One filter at x of the row, 44 tall: the attribute's icon and word, 「全て」 / 「全コスト」, or a cost as its amber
        /// circle alone (the word 「コスト n」 is its title on hover). Its width holds its content with the .is-on border.
        /// </summary>
        private FilterButton MakeFilter(string word, bool cost, BattleCore.BattleAttribute attribute, int value, float x)
        {
            var f = new FilterButton { Attribute = attribute, Cost = value };
            f.Button = new DepButton(_filters, cost ? "ocbg-meta-filter is-cost" : "ocbg-meta-filter", TableCss.Filter)
                .OnHover(TableCss.FilterHover)
                .OnClass("is-on", TableCss.FilterOn)
                .Done();
            const float pad = 16f, gap = 8f, h = 44f;
            var parts = new List<(RectTransform El, float W, float H)>();
            if (!cost && AttrIcon.TryGetValue(attribute, out string icon))
            {
                PathGraphic g = MetaUtil.Icon(f.Button.Root, icon, 20f, AttrInk[attribute]);
                parts.Add((g.rectTransform, 20f, 20f));
            }
            if (cost && value > 0)
            {
                RectTransform dot = Ocb.El(f.Button.Root, "ocbg-meta-costdot", TableCss.CostDot);
                Label.Make(dot, value.ToString(CultureInfo.InvariantCulture), MetaStyle.Text(TableCss.Filter + TableCss.CostDot), "n");
                Ocb.Flow(dot, TableCss.CostDot);
                parts.Add((dot, 26f, 26f));
                TitleTip.On(f.Button.Box, word);
            }
            else
            {
                TextStyle ts = MetaStyle.Text(TableCss.Filter);
                TextMeshProUGUI t = Label.Make(f.Button.Root, word ?? "", ts, "span");
                parts.Add((t.rectTransform, TextMeasure.Width(word ?? "", ts), ts.LineHeightPx));
            }
            float content = parts.Sum(p => p.W) + gap * (parts.Count - 1);
            f.W = content + 2f * pad + 4f;
            float cx = (f.W - content) / 2f;
            foreach ((RectTransform el, float w, float ph) in parts)
            {
                Ui.Place(el, cx, (h - ph) / 2f);
                cx += w + gap;
            }
            f.Button.Node.Place(x, 0f, f.W, h);
            return f;
        }

        // js: scenes/meta/departure.js:230
        private void DrawFilters()
        {
            foreach (FilterButton b in _attrBtns) b.Button.Toggle("is-on", _filter.Attribute == b.Attribute);
            foreach (FilterButton b in _costBtns) b.Button.Toggle("is-on", _filter.Cost == b.Cost);
        }

        // ---- the card grid ----

        /// <summary>One grid cell (cell.def, cell.count, cell.plus, cell.minus).</summary>
        internal sealed class Cell
        {
            public BattleCore.CardDef Def;
            public RectTransform Root;
            public UiNode Node;
            public DepButton Face;
            public MetaButton Minus, Plus;
            public TextMeshProUGUI Count;
            public UiNode CountNode;
        }

        // js: scenes/meta/departure.js:239
        private void DrawGrid(int dir)
        {
            DeckPage p = _deck.Page(_filter, _page, DepartTable.PerPage);
            _page = p.Index;
            Label.SetText(_pageText, p.Text);
            _prevBtn.Disabled = p.Index == 0;
            _nextBtn.Disabled = p.Index >= p.Count - 1;
            LayoutPageNav();
            Ui.Clear(_grid);
            if (_hoverCard != null && !p.Cards.Any(d => d.Id == _hoverCard.Id)) { _hoverCard = null; DrawReader(); }
            var cells = new List<Cell>();
            for (int i = 0; i < p.Cards.Count; i++) cells.Add(MakeCell(p.Cards[i], DepartTable.Cell(i)));
            _cells = cells;
            RefreshCells();
            if (dir != 0)
            {
                for (int i = 0; i < _cells.Count; i++)
                {
                    Cell c = _cells[i];
                    global::Trial.Kit.Styler s = global::Trial.Kit.Styler.Of(c.Node, o: 0f, x: 36f * dir);
                    _clock.Tween(s, new[] { ("o", 1f), ("x", 0f) }, 240f, Ease.Out, (i % DepartTable.GridColumns) * 18f)
                        .Then(() => { if (c.Node) c.Node.ResetTransform(); });
                }
            }
            SyncFocus();
        }

        /// <summary>
        /// One small card in its cell (#1051): the card's frame in its attribute's colour (the left edge 6 px), the face's
        /// few words (MetaCardFace.SmallFace) and the − ×n ＋ row at its foot; an unowned card is dimmed and says 未所持
        /// instead of the row. Pressing the card selects it (the reading panel and the cursor keys follow it).
        /// </summary>
        private Cell MakeCell(BattleCore.CardDef def, TableBox box)
        {
            var cell = new Cell { Def = def };
            bool owned = IsOwned(def.Id);
            CardFace face = FaceFor(def);
            SmallCard small = DepartTable.SmallCardOf(face);
            cell.Root = Ui.Div(_grid, owned ? "ocbg-meta-cell" : "ocbg-meta-cell is-unowned", box.X, box.Y, box.W, box.H);
            cell.Node = UiNode.Of(cell.Root);

            string frame = MetaCardFace.FrameCss(small.Kind);
            cell.Face = new DepButton(cell.Root, "ocbg-meta-small-card", TableCss.Card + "border-color: " + frame + ";")
                .OnHover(TableCss.CardHover)
                .OnClass("is-in", TableCss.CardIn)
                .OnClass("is-sel", TableCss.CardSel)
                .OnClass("is-unowned", TableCss.CardUnowned);
            if (!owned) cell.Face.Toggle("is-unowned", true, apply: false);
            cell.Face.Done();
            cell.Face.Node.Place(0f, 0f, box.W, box.H);
            MetaCardFace.SmallFace(cell.Face.Root, small);
            cell.Face.OnClick = () => Choose(def);
            cell.Face.Area.Up += e => { if (e != null && e.button == PointerEventData.InputButton.Right) Choose(def); };   // oncontextmenu
            cell.Face.Area.HoverChanged += on => HoverCard(def, on);

            TableBox foot = DepartTable.CardFoot;
            if (!owned)
            {
                TextStyle ts = MetaStyle.Text(TableCss.Unowned);
                float w = TextMeasure.Width(MetaText.NotOwned, ts) + 28f + 2f, h = ts.LineHeightPx + 8f + 2f;
                BoxGraphic tag = BoxGraphic.Make(cell.Face.Root, TableCss.Unowned, foot.X + (foot.W - w) / 2f, foot.Y + (foot.H - h) / 2f, w, h, "ocbg-meta-unowned");
                Label.Make(tag.rectTransform, MetaText.NotOwned, ts, 15f, 5f, null, null, "text");
                UiNode.Of(tag.rectTransform).PointerEvents = PointerEvents.None;
            }
            // − ×n ＋, on every card the life owns
            RectTransform rowEl = Ui.Div(cell.Face.Root, "ocbg-meta-cell-row", foot.X, foot.Y, foot.W, foot.H);
            UiNode.Of(rowEl);
            cell.Minus = MetaButton.Step(rowEl, "−", 0f, 0f);
            TextStyle cs = MetaStyle.Text(TableCss.CardCount);
            cs.Align = TextAlign.Center;
            cell.Count = Label.Make(rowEl, "", cs, 44f, (foot.H - cs.LineHeightPx) / 2f, foot.W - 88f, cs.LineHeightPx, "ocbg-meta-count");
            cell.CountNode = UiNode.Of(cell.Count.rectTransform);
            cell.Plus = MetaButton.Step(rowEl, "＋", foot.W - 44f, 0f);
            cell.Minus.Click += () => Remove(def, cell);
            cell.Plus.Click += () => Add(def, cell);
            if (!owned) UiNode.Of(rowEl).Visible = false;
            return cell;
        }

        // js: scenes/meta/departure.js:272
        private void RefreshCells()
        {
            foreach (Cell c in _cells)
            {
                int n = _deck.CountOf(c.Def.Id);
                Label.SetText(c.Count, "×" + n.ToString(CultureInfo.InvariantCulture));
                Label.SetColor(c.Count, n > 0 ? DsTokens.Amber : DsTokens.InkMuted);
                // #1038: the ＋ of a refusal with a reason stays pressable, so pressing it says why (Add).
                c.Plus.Disabled = !_deck.CanAdd(c.Def.Id) && _deck.RefusalOf(c.Def.Id).Length == 0;
                c.Minus.Disabled = n == 0;
                c.Face.Toggle("is-in", n > 0);
                c.Face.Toggle("is-sel", _selected != null && _selected.Id == c.Def.Id);
            }
        }

        // ---- page nav ----

        /// <summary>The page nav: ← and → at the left with the page words between them; the keys keep the right end.</summary>
        private void LayoutPageNav()
        {
            TableBox nav = DepartTable.PageNav;
            TextStyle ts = Label.StyleOf(_pageText);
            float tw = TextMeasure.Width(Label.StateOf(_pageText).Text ?? "", ts);
            float[] xs = DepartTable.RowFrom(0f, 16f, _prevBtn.Node.W, tw, _nextBtn.Node.W);
            _prevBtn.Place(xs[0], (nav.H - _prevBtn.Node.H) / 2f);
            Ui.Place(_pageText.rectTransform, xs[1], (nav.H - ts.LineHeightPx) / 2f);
            _nextBtn.Place(xs[2], (nav.H - _nextBtn.Node.H) / 2f);
        }

        // js: scenes/meta/departure.js:288
        private void Turn(int by)
        {
            int before = _page;
            _page += by;
            DeckPage p = _deck.Page(_filter, _page, DepartTable.PerPage);
            if (p.Index == before) { _page = before; return; }
            _sound.Play("nodeHover");
            DrawGrid(by);
        }

        // js: scenes/meta/departure.js:317
        private void Choose(BattleCore.CardDef def)
        {
            if (_selected != null && _selected.Id == def.Id) return;
            _selected = def;
            _sound.Play("uiClick");
            DrawReader();
            RefreshCells();
        }

        private void HoverCard(BattleCore.CardDef def, bool on)
        {
            if (on)
            {
                _sound.Play("nodeHover");
                _hoverCard = def;
            }
            else if (_hoverCard != null && _hoverCard.Id == def.Id)
            {
                _hoverCard = null;
            }
            DrawReader();
        }

        /// <summary>A thing on the shelf or in a slot under the pointer (null: none); the reading panel reads it.</summary>
        internal void HoverItem(SupplyOption option, string kind)
        {
            _hoverItem = option;
            _hoverItemKind = kind;
            DrawReader();
        }

        // ================================================================================ the pack

        private void BuildPack()
        {
            TableBox pack = DepartTable.Pack;
            BoxGraphic.Make(_root, TableCss.Pack, pack.X, pack.Y, pack.W, pack.H, "ocbg-meta-pack");
            BoxGraphic.Make(_root, TableCss.Shelf, DepartTable.Shelf.X, DepartTable.Shelf.Y, DepartTable.Shelf.W, DepartTable.Shelf.H, "ocbg-meta-shelf");

            // the pile, and beside it: 束 n 枚 and the rule, the gauge, the deck's line
            TableBox pile = DepartTable.Pile;
            _pile = Ui.Div(_root, "ocbg-meta-pile", pile.X, pile.Y, pile.W, pile.H);
            UiNode.Of(_pile);
            TableBox text = DepartTable.PileText;
            TextStyle head = MetaStyle.Text(TableCss.Heading), total = MetaStyle.Text(TableCss.PileTotal), unit = MetaStyle.Text(TableCss.PileUnit), rule = MetaStyle.Text(TableCss.PileRule);
            float baseline = text.Y + TextMath.CssBaselineFromTop(total.Spec);
            float headW = TextMeasure.Width(DepartTable.PileHeading, head);
            const float totalW = 80f;
            float[] xs = DepartTable.RowFrom(text.X, 10f, headW, totalW);
            Label.Make(_root, DepartTable.PileHeading, head, xs[0], baseline - TextMath.CssBaselineFromTop(head.Spec), null, null, "h3");
            total.Align = TextAlign.Right;
            _totalEl = Label.Make(_root, "", total, xs[1], text.Y, totalW, total.LineHeightPx, "ocbg-meta-total");
            Label.Make(_root, MetaText.CardsUnit, unit, xs[1] + totalW + 8f, baseline - TextMath.CssBaselineFromTop(unit.Spec), null, null, "unit");
            string ruleLine = DepartTable.RuleLine(_limits);
            Label.Make(_root, ruleLine, rule, text.Right - TextMeasure.Width(ruleLine, rule), baseline - TextMath.CssBaselineFromTop(rule.Spec), null, null, "rule");
            // js: departure.js:342 — rollNumber(clock, totalEl, deck.Total) on the scene clock, shown with Math.round.
            _total = MetaUtil.RollNumber(_clock, _totalEl, _deck.Total);

            TableBox gauge = DepartTable.Gauge;
            BoxGraphic.Make(_root, TableCss.Gauge, gauge.X, gauge.Y, gauge.W, gauge.H, "ocbg-meta-gauge");
            _gaugeFill = Ui.Div(_root, "ocbg-meta-gauge-fill", gauge.X + 1f, gauge.Y + 1f, 0f, gauge.H - 2f);
            UiNode.Of(_gaugeFill);
            BoxGraphic.On(_gaugeFill);
            _gaugeFillStates = CssStates.Of(_gaugeFill).Base(TableCss.GaugeFill).State(".is-ok", TableCss.GaugeFillOk);
            _gaugeFillStates.Apply(true);
            // the minimum's mark, over the gauge
            float mx = gauge.X + 1f + (gauge.W - 2f) * _deckMin / _deckCap - 1f;
            BoxGraphic.Make(_root, TableCss.GaugeMin, mx, gauge.Y - 5f, 2f, gauge.H + 10f, "ocbg-meta-gauge-min");
            TableBox line = DepartTable.DeckLine;
            _deckLine = Label.Make(_root, "", MetaStyle.P(TableCss.DeckLine), line.X, line.Y, line.W, line.H, "ocbg-meta-deck-line");

            // the reading panel
            TableBox reader = DepartTable.Reader;
            BoxGraphic.Make(_root, TableCss.Reader, reader.X, reader.Y, reader.W, reader.H, "ocbg-meta-reader");
            _readerBody = Ui.Div(_root, "ocbg-meta-reader-body", 0f, 0f, MetaUtil.W, MetaUtil.H);
            UiNode.Of(_readerBody);

            // the preset decks: the starter's (#1038), a random one, none
            string[] names = { DepartTable.StarterPreset, MetaText.Random, MetaText.Clear };
            Action[] acts =
            {
                () => Replace(Adopt(DeckBuilder.Starter(_limits))),
                () => Replace(RandomDeck()),
                () => Replace(new DeckBuilder(_owned, _limits)),
            };
            for (int i = 0; i < names.Length; i++)
            {
                TableBox b = DepartTable.Preset(i);
                MetaButton btn = MetaButton.Btn(_root, names[i], b.X, b.Y, b.W, b.H);
                btn.Click += acts[i];
                _presets.Add(btn);
            }

            TableBox status = DepartTable.Status;
            _statusEl = new StatusLine(_root, status.X, status.Y, status.W);
            TableBox setOut = DepartTable.SetOut;
            _setOutBtn = MetaButton.Primary(_root, DepartureBuilder.SetOutLabel, "Enter", setOut.X, setOut.Y, setOut.W, setOut.H);
            _setOutBtn.Click += SetOut;
        }

        /// <summary>The pile: one sheet per ninth of the cap (DepartTable.PileSheets), the front one amber once the deck may set out.</summary>
        private void DrawPile(bool ok)
        {
            Ui.Clear(_pile);
            int sheets = DepartTable.PileSheets(_deck.Total, _deckCap);
            TableBox pile = DepartTable.Pile;
            for (int k = sheets - 1; k >= 0; k--)
            {
                TableBox s = DepartTable.Sheet(k, sheets);
                string css = k == 0 ? TableCss.SheetFront + (ok ? TableCss.SheetFrontOk : "") : TableCss.Sheet;
                BoxGraphic.Make(_pile, css, s.X - pile.X, s.Y - pile.Y, s.W, s.H, k == 0 ? "sheet front" : "sheet");
            }
        }

        // ================================================================================ the reading panel

        /// <summary>
        /// The one reading panel (#1051): the thing under the pointer, else the card under it, else the selected card.
        /// A card reads DeckBuilder.ShownDetailOf (its name, cost, attributes and reach, what it does, its trait, its
        /// flavour); a thing reads its name, reading, side and note. A tool that moves the start (間合いの履) also draws the
        /// start-position diagram under its note, with the pack as it stands (the JS's 開始の位置, departure.js:616).
        /// Lines that run past the panel are left out from the end.
        /// </summary>
        private void DrawReader()
        {
            if (_readerBody == null || !_readerBody) return;
            Ui.Clear(_readerBody);
            TableBox c = DepartTable.ReaderContent;
            if (_hoverItem != null) { ReadItem(c, _hoverItem, _hoverItemKind); return; }
            BattleCore.CardDef def = _hoverCard ?? _selected;
            if (def == null)
            {
                TextStyle hs = MetaStyle.P(MetaStyle.DetailHintCss);
                float hh = TextMeasure.Height(MetaText.PickCardHint, hs, c.W);
                Label.Make(_readerBody, MetaText.PickCardHint, hs, c.X, c.Y + (c.H - hh) / 2f, c.W, hh, "hint");
                return;
            }
            List<string> lines = DeckBuilder.ShownDetailOf(def);
            TextStyle name = MetaStyle.Text(TableCss.ReaderName);
            Label.Make(_readerBody, lines[0], name, c.X, c.Y, null, null, "h3");
            float y = c.Y + name.LineHeightPx + 8f;
            for (int i = 1; i < lines.Count; i++)
            {
                bool flavour = i == lines.Count - 1 && lines[i].StartsWith("「", StringComparison.Ordinal);
                TextStyle ps = MetaStyle.P(flavour ? TableCss.ReaderFlavour : TableCss.ReaderLine);
                float ph = TextMeasure.Height(lines[i], ps, c.W);
                if (y + ph > c.Bottom) break;
                Label.Make(_readerBody, lines[i], ps, c.X, y, c.W, ph, flavour ? "p is-flavour" : "p");
                y += ph + 4f;
            }
        }

        private void ReadItem(TableBox c, SupplyOption option, string kind)
        {
            DungeonContent.ItemDef item = kind == "tool" ? DungeonContent.ItemCatalogue.Tool(option.Id) : DungeonContent.ItemCatalogue.Consumable(option.Id);
            bool battle = item.Side == DungeonContent.EffectSide.Battle;
            TextStyle nameS = MetaStyle.Text(TableCss.ReaderName), readS = MetaStyle.Text(TableCss.ReaderReading), sideS = MetaStyle.Text(TableCss.ReaderSide);
            float baseline = c.Y + TextMath.CssBaselineFromTop(nameS.Spec);
            float nameW = TextMeasure.Width(option.Name ?? "", nameS);
            Label.Make(_readerBody, option.Name ?? "", nameS, c.X, c.Y, null, null, "h3");
            Label.Make(_readerBody, item.Reading ?? "", readS, c.X + nameW + 12f, baseline - TextMath.CssBaselineFromTop(readS.Spec), null, null, "small");
            // the side: 戦闘 with `range`, 探索 with `stairs`, at the right end
            string sideWord = battle ? MetaText.SideBattle : MetaText.SideExplore;
            float sideW = 10f + 16f + 4f + TextMeasure.Width(sideWord, sideS) + 10f + 2f, sideH = sideS.LineHeightPx + 6f + 2f;
            BoxGraphic side = BoxGraphic.Make(_readerBody, TableCss.ReaderSide, c.Right - sideW, c.Y + (nameS.LineHeightPx - sideH) / 2f, sideW, sideH, "ocbg-meta-side");
            PathGraphic icon = MetaUtil.Icon(side.rectTransform, battle ? "range" : "stairs", 16f, DsTokens.Ink);
            Ui.Place(icon.rectTransform, 11f, (sideH - 16f) / 2f);
            Label.Make(side.rectTransform, sideWord, sideS, 11f + 16f + 4f, 4f, null, null, "text");

            float y = c.Y + nameS.LineHeightPx + 8f;
            string note = PlayerNote(option.Note);
            TextStyle noteS = MetaStyle.P(TableCss.ReaderText);
            float noteH = TextMeasure.Height(note, noteS, c.W);
            Label.Make(_readerBody, note, noteS, c.X, y, c.W, Math.Min(noteH, Math.Max(0f, c.Bottom - y)), "p");
            y += noteH + 12f;
            if (option.StartCellsBack <= 0) return;

            // the start-position diagram (#1051: its place is here, under the note of the tool that moves the start)
            TextStyle startS = MetaStyle.Text(TableCss.ReaderStart);
            string startLine = MetaText.StartHeading + "　" + _supplies.StartLine;
            if (y + startS.LineHeightPx + DepartTable.LaneH > c.Bottom) return;
            Label.Make(_readerBody, startLine, startS, c.X, y, null, null, "h4");
            y += startS.LineHeightPx;
            RectTransform lane = Ui.Div(_readerBody, "ocbg-meta-lane", c.X, y, c.W, DepartTable.LaneH);
            UiNode.Of(lane);
            DrawLane(lane);
        }

        /// <summary>
        /// drawLane(): the start a tool moves back (DepartureBuilder.PlayerStartCell / StartGap) on max(6, the enemy's cell)
        /// floor tiles 88 apart: the player's tile lit, the two figures over their tiles, the distance chip between them.
        /// </summary>
        // js: scenes/meta/departure.js:616
        private void DrawLane(RectTransform lane)
        {
            const float cellW = DepartTable.LaneCellW;
            int pc = _supplies.PlayerStartCell;
            int gap = _supplies.StartGap;
            int foe = pc + gap + 1;
            int cellsN = DepartTable.LaneCells(pc, gap);
            for (int i = 1; i <= cellsN; i++)
            {
                OcbFloorTile t = Ocb.FloorTile(lane, cellW - 6f);
                t.Place((i - 1) * cellW + 3f, 58f);
                if (i == pc)
                {
                    t.Reach.Node.Opacity = 0.65f;
                    t.ReachLine.Node.Opacity = 1f;
                }
            }
            LaneUnit(lane, (pc - 1) * cellW, DsTokens.Amber);
            LaneUnit(lane, (foe - 1) * cellW, DsTokens.Violet);
            OcbElement chip = Ocb.DistanceChip(lane, gap.ToString(CultureInfo.InvariantCulture), gap * cellW);
            chip.Place(pc * cellW, 61f);
        }

        /// <summary>.ocbg-meta-lane-unit at (x, 12, 88, 44): `person` 34 at the bottom centre (align-items: flex-end).</summary>
        private static void LaneUnit(RectTransform lane, float x, Rgba color)
        {
            RectTransform unit = Ui.Div(lane, "ocbg-meta-lane-unit", x, 12f, 88f, 44f);
            UiNode.Of(unit);
            PathGraphic g = MetaUtil.Icon(unit, "person", 34f, color);
            Ui.Place(g.rectTransform, (88f - 34f) / 2f, 44f - 34f);
        }

        // ================================================================================ changing the deck

        // js: scenes/meta/departure.js:446
        /// <summary>
        /// 「ランダム」: DeckBuilder.Random holds §8 and its closers, but it draws from the whole catalogue. For a character that
        /// owns fewer kinds, its picks go through Add on a builder of the owned kinds (which refuses the rest) and further
        /// seeded decks top it up to the same size.
        /// </summary>
        private DeckBuilder RandomDeck()
        {
            var b = new DeckBuilder(_owned, _limits);
            for (int tries = 0; tries < 16 && b.Total < DeckBuilder.RandomDefaultSize; tries++)
            {
                DeckBuilder r = DeckBuilder.Random(_randomSeed++, DeckBuilder.RandomDefaultSize, _limits);
                foreach (BattleCore.CardInstance card in r.Build())
                {
                    if (b.Total >= DeckBuilder.RandomDefaultSize) break;
                    b.Add(card.Def.Id);
                }
            }
            return b;
        }

        // js: scenes/meta/departure.js:457
        private void Replace(DeckBuilder next)
        {
            _deck = next;
            SetNotice("");
            _sound.Play("rewardReveal");
            RefreshDeck();
            for (int i = 0; i < _cells.Count; i++)
            {
                global::Trial.Kit.Styler s = global::Trial.Kit.Styler.Of(_cells[i].CountNode, o: 0f, s: 1.4f);
                _clock.Tween(s, new[] { ("o", 1f), ("s", 1f) }, 260f, Ease.Out, i * 12f);
            }
        }

        // js: scenes/meta/departure.js:464
        private void Add(BattleCore.CardDef def, Cell cell)
        {
            if (_deck.Add(def.Id))
            {
                _sound.Play("cardAdded");
                if (!_noticeOverCap) SetNotice("");
                if (_selected == null || _selected.Id != def.Id) { _selected = def; DrawReader(); }
                RefreshDeck();
                global::Trial.Kit.Styler s = global::Trial.Kit.Styler.Of(cell.CountNode, s: 1.5f);
                _clock.Tween(s, "s", 1f, 240f, Ease.Out);
            }
            else
            {
                _sound.Play("refusal");
                Shake(cell.Plus);
                // #1038: the second starter stance is refused with the reason; the JS has no such rule.
                string why = _deck.RefusalOf(def.Id);
                if (why.Length > 0) SetNotice(why);
            }
        }

        // js: scenes/meta/departure.js:476
        private void Remove(BattleCore.CardDef def, Cell cell)
        {
            if (_deck.Remove(def.Id))
            {
                _sound.Play("uiCancel");
                if (!_noticeOverCap) SetNotice("");
                RefreshDeck();
                global::Trial.Kit.Styler s = global::Trial.Kit.Styler.Of(cell.CountNode, s: 0.7f);
                _clock.Tween(s, "s", 1f, 200f, Ease.Out);
            }
        }

        // js: scenes/meta/departure.js:484
        /// <summary>
        /// shake(el): the button to x 6, −6, 4, −4, 0, 32 ms each (linear), then its transform cleared. styler(el) sets the
        /// opacity inline too (1), so a disabled ＋ shaken by the key stays fully opaque until it is drawn again, as in the JS.
        /// </summary>
        internal void Shake(MetaButton el)
        {
            if (el == null || !el.Root) return;
            Shake(el.Styler(), () => { if (el.Root) el.ClearTransform(); });
        }

        /// <summary>shake(el) for a button of the departure's own classes (a thing on the shelf).</summary>
        internal void Shake(DepButton el)
        {
            if (el == null || !el.Root) return;
            Shake(el.InlineStyler(), () => { if (el.Root) el.ClearTransform(); });
        }

        private void Shake(global::Trial.Kit.Styler s, Action clear)
        {
            float[] seq = { 6f, -6f, 4f, -4f, 0f };
            Promise p = Promise.Resolved();
            foreach (float x in seq)
            {
                float to = x;
                p = p.Then(() => _clock.Tween(s, "x", to, 32f, Ease.Linear));
            }
            p.Then(clear);
        }

        // js: scenes/meta/departure.js:489
        private void RefreshDeck()
        {
            RefreshCells();
            _total.To(_deck.Total).Forget();
            _header?.SetDeckCount(_deck.Total);
            TableBox gauge = DepartTable.Gauge;
            float pct = Math.Min(100f, 100f * _deck.Total / _deckCap);
            bool ok = _deck.IsValid;
            _gaugeFillStates.SetState(".is-ok", ok, false);
            UiNode.Of(_gaugeFill).W = (gauge.W - 2f) * pct / 100f;
            _gaugeFillStates.Apply();
            DrawPile(ok);
            if (_noticeOverCap && _deck.Total <= _deckCap) SetNotice("");
            DrawDeckLine();
            RefreshStatus();
        }

        /// <summary>The deck's line under the gauge: the notice (a refusal's reason, an over-cap deck's) or DeckBuilder.StatusText.</summary>
        private void DrawDeckLine()
        {
            Label.SetText(_deckLine, _notice.Length > 0 ? _notice : _deck.StatusText);
        }

        // js: scenes/meta/departure.js:502
        private void SetNotice(string text)
        {
            _notice = text ?? "";
            _noticeOverCap = false;
            DrawDeckLine();
        }

        /// <summary>
        /// The status line by 出立する and the button: DepartureBuilder.StatusText while the deck and the pack may set out,
        /// else the deck's own line (what it still lacks), since the deck is on this same screen now.
        /// </summary>
        internal void RefreshStatus()
        {
            bool ok = _supplies.CanSetOut(_deck);
            _statusEl.Set(ok ? _supplies.StatusText(_deck) : _deck.StatusText, ok);
            _setOutBtn.Disabled = !ok;
            KeepDraft();
        }

        // ================================================================================ leaving

        // js: scenes/meta/departure.js:696
        private void Back()
        {
            if (_busy) return;
            _sound.Play("uiCancel");
            Leave(() => _ctx.OnBack?.Invoke()).Forget();
        }

        // js: scenes/meta/departure.js:702
        internal void SetOut()
        {
            if (_busy || !_supplies.CanSetOut(_deck)) return;
            BattleCore.Departure departure = _supplies.Build(_deck);
            // The tools that act inside a battle go to the battles (Expedition's battleToolIds); the whole loadout, with
            // the life's slot count, goes to the exploration (#104) — DepartureCarry.Carry.
            CarriedSupplies carry = DepartureCarry.Carry(departure, _limits);
            string deckSave = _deck.Save();
            _settingOut = true;
            ClearDraft();   // the life sets out with this deck; the router keeps it from here (ctx.SavedDeck)
            _sound.Play("uiConfirm");
            Leave(() => _ctx.OnSetOut?.Invoke(DepartureChoice.Of(departure, carry, deckSave))).Forget();
        }

        // js: scenes/meta/departure.js:724
        private void OpenSettings()
        {
            if (_busy || _ctx.OnSettings == null) return;
            _sound.Play("uiClick");
            _ctx.OnSettings();
        }

        private void OpenJournal()
        {
            if (_busy || _ctx.OnJournal == null) return;
            _ctx.OnJournal();
        }

        // js: scenes/meta/departure.js:729
        private async Promise Leave(Action cb)
        {
            _busy = true;
            RectTransform fadeEl = MetaStyle.Fade(_root);
            global::Trial.Kit.Styler fade = global::Trial.Kit.Styler.Of(fadeEl, o: 0f);
            await _clock.Tween(fade, "o", 1f, 520f, Ease.In);
            if (_alive) cb();
        }

        // ================================================================================ keys (departure.js:756-803)

        // js: scenes/meta/departure.js:757
        private int CursorIndex() => _selected != null ? _cells.FindIndex(c => c.Def.Id == _selected.Id) : -1;

        // js: scenes/meta/departure.js:758
        private void MoveCursor(int by, bool rowStep = false)
        {
            int i = CursorIndex();
            if (i < 0) { if (_cells.Count > 0) Choose(_cells[0].Def); return; }
            int j = i + by;
            if (rowStep) { if (j >= 0 && j < _cells.Count) Choose(_cells[j].Def); return; }
            if (j >= 0 && j < _cells.Count) { Choose(_cells[j].Def); return; }
            int before = _page;
            Turn(by < 0 ? -1 : 1);
            if (_page != before && _cells.Count > 0) Choose(by < 0 ? _cells[_cells.Count - 1].Def : _cells[0].Def);
        }

        // js: scenes/meta/departure.js:768
        private Cell CursorCell()
        {
            int i = CursorIndex();
            return i >= 0 ? _cells[i] : null;
        }

        // A button reached with Tab takes Enter itself (the default action presses it); a pointer press takes the focus
        // away (Focus), so Enter then means the screen's own Enter.
        private void OnPointerDownCapture() => _tabbed = false;

        // js: scenes/meta/departure.js:774
        private bool KeyboardFocus() => _tabbed && Focus.InScope(_scope) != null;

        // js: scenes/meta/departure.js:779 — the deck step's keys and the supplies step's Enter, on the one table
        private bool OnKey(KeyEvent e)
        {
            if (e.Key == "Tab") { _tabbed = true; return false; }
            if (_busy) return false;
            // a held key repeats: only a fresh press opens an overlay or goes back (the overlays close on a fresh press only)
            if (e.Key == "Escape")
            {
                if (!e.Repeat) { if (_ctx.OnSettings != null) OpenSettings(); else Back(); }
                return true;
            }
            if ((e.Key == "j" || e.Key == "J") && _ctx.OnJournal != null) { if (!e.Repeat) OpenJournal(); return true; }
            if (e.Key == "Enter" && KeyboardFocus()) return false;
            if (e.Key == "PageUp") { Turn(-1); return true; }
            if (e.Key == "PageDown") { Turn(1); return true; }
            if (e.Key == "ArrowLeft") { MoveCursor(-1); return true; }
            if (e.Key == "ArrowRight") { MoveCursor(1); return true; }
            if (e.Key == "ArrowUp") { MoveCursor(-DepartTable.GridColumns, true); return true; }
            if (e.Key == "ArrowDown") { MoveCursor(DepartTable.GridColumns, true); return true; }
            if (e.Key == "+" || e.Key == "=" || e.Key == ";") { Cell c = CursorCell(); if (c != null) Add(c.Def, c); return true; }
            if (e.Key == "-" || e.Key == "_" || e.Key == "Backspace" || e.Key == "Delete") { Cell c = CursorCell(); if (c != null) Remove(c.Def, c); return true; }
            if (e.Key == "Enter") { if (_supplies.CanSetOut(_deck)) SetOut(); else _sound.Play("refusal"); return true; }
            return false;
        }

        // ================================================================================ focus (the Tab order)

        private Focusable FocusOf(MetaButton b)
        {
            if (b == null) return null;
            if (!_metaFocus.TryGetValue(b, out Focusable f))
            {
                f = new Focusable { Target = b.Root, Press = b.Press, Ring = b.SetFocusRing, CanFocus = () => !b.Disabled };
                _metaFocus[b] = f;
            }
            return f;
        }

        /// <summary>
        /// The Tab order, the table read left to right: the filters, each card's face − ＋, the page turns, the shelf, the
        /// slots, the presets, 出立する, then the band's 「拠点へ戻る」, 手記 and 設定. A disabled button and a removed one are skipped
        /// (Focusable.IsFocusable); buttons drawn again replace the old.
        /// </summary>
        internal void SyncFocus()
        {
            if (_scope == null) return;
            var want = new List<Focusable>();
            void Push(Focusable f) { if (f != null) want.Add(f); }
            foreach (FilterButton b in _attrBtns) Push(b.Button.Focusable);
            foreach (FilterButton b in _costBtns) Push(b.Button.Focusable);
            foreach (Cell c in _cells)
            {
                Push(c.Face.Focusable);
                if (IsOwned(c.Def.Id))
                {
                    Push(FocusOf(c.Minus));
                    Push(FocusOf(c.Plus));
                }
            }
            Push(FocusOf(_prevBtn));
            Push(FocusOf(_nextBtn));
            if (_supView != null)
            {
                foreach (object o in _supView.FocusOrder())
                {
                    if (o is MetaButton mb) Push(FocusOf(mb));
                    else if (o is DepButton db) Push(db.Focusable);
                }
            }
            foreach (MetaButton b in _presets) Push(FocusOf(b));
            Push(FocusOf(_setOutBtn));
            if (_header != null)
                foreach (Focusable f in _header.Focusables) Push(f);

            var keep = new HashSet<Focusable>(want);
            foreach (Focusable f in _scope.Items.ToArray()) if (!keep.Contains(f)) _scope.Remove(f);
            // the persisting buttons keep their relative order; the new ones go in at their DOM place
            for (int i = 0; i < want.Count; i++) if (!_scope.Contains(want[i])) _scope.Insert(i, want[i]);
            foreach (MetaButton b in _metaFocus.Keys.Where(k => !k.Root).ToList()) _metaFocus.Remove(b);
        }

        // ================================================================================ destroy

        // js: scenes/meta/departure.js:806
        public void Destroy()
        {
            if (!_alive) return;
            _alive = false;
            _popKeys?.Invoke();
            KeyStack.PointerDownCapture -= OnPointerDownCapture;
            _scope?.Dispose();
            _clock.Stop();
            _backdrop?.Destroy();
            Ui.Remove(_root);
        }

        /// <summary>departure.js has no refresh(): an overlay closing over the screen changes nothing on it.</summary>
        public void Refresh() { }

        // ================================================================================ helpers

        /// <summary>
        /// p.ocbg-meta-status at (x, y), w wide: the ::before ring (12 × 12, 8 px down, 2 px steel; .is-ok: amber, filled, a
        /// glow) and, 10 px after it, the words (400 20px/1.45, wrapping by phrase).
        /// </summary>
        internal sealed class StatusLine
        {
            private readonly RectTransform _root;
            private readonly CssStates _dot;
            private readonly TextMeshProUGUI _text;
            private readonly float _w;

            public StatusLine(RectTransform parent, float x, float y, float w)
            {
                _w = w;
                _root = Ui.Div(parent, "ocbg-meta-status", x, y, w, 0f);
                UiNode.Of(_root);
                RectTransform dot = Ui.Div(_root, "before", 0f, 8f, 12f, 12f);
                UiNode.Of(dot);
                BoxGraphic.On(dot);
                _dot = CssStates.Of(dot).Base(MetaStyle.Without(MetaStyle.StatusBeforeCss, "width", "height")).State(".is-ok", MetaStyle.StatusOkBeforeCss);
                _dot.Apply(true);
                _text = Label.Make(_root, "", MetaStyle.P(MetaStyle.StatusCss), 12f + 10f, 0f, w - 22f, null, "text");
            }

            public RectTransform Root => _root;

            public void Set(string text, bool ok)
            {
                Label.SetText(_text, text ?? "");
                _dot.SetState(".is-ok", ok);
                TextStyle s = Label.StyleOf(_text);
                UiNode.Of(_root).H = Math.Max(20f, TextMeasure.Height(text ?? "", s, _w - 22f));
            }
        }

        /// <summary>
        /// A button of the departure's own classes (.ocbg-meta-back, -filter, -small-card, -shelf-item, -slot): its box,
        /// its rules in cascade order (the base, :focus-visible, then the rules added with Rule / OnHover / OnClass /
        /// OnDisabled), their transitions (CssStates on the document clock), the pointer (onclick, hover) and its Tab entry.
        /// A Styler made with InlineStyler is an inline style: it wins over the rules' opacity and transform, as in CSS.
        /// The content (icons, labels) is the caller's.
        /// </summary>
        internal sealed class DepButton
        {
            private readonly string _base;
            private readonly List<(string Name, string Css, Func<bool> When)> _rules = new List<(string, string, Func<bool>)>();
            private readonly HashSet<string> _classes = new HashSet<string>(StringComparer.Ordinal);
            private bool _hovered, _disabled, _focus, _inlineO, _inlineT, _registered, _done;

            // The frame it was made in. The browser computes a new element's style once, after the script that made it
            // has run, so a class the script toggles right after making the element (drawGrid then refreshCells'
            // is-sel, moveCursor's choose after a page turn) shows at once: no transition. Every restyle in that frame
            // is instant here for the same reason, unless StyledAtOnce is off (see there).
            private readonly int _bornFrame = Time.frameCount;

            /// <summary>
            /// True (the default): the button's style is first computed after the frame that made it, so its first-frame
            /// changes do not transition. False where the JS forces a style pass between making the button and changing
            /// it, so the change then transitions in.
            /// </summary>
            public bool StyledAtOnce = true;

            public DepButton(RectTransform parent, string name, string css, bool focusable = true)
            {
                _base = MetaStyle.ButtonCss + (css ?? "");
                Root = Ui.Div(parent, name);
                Node = UiNode.Of(Root);
                Box = BoxGraphic.On(Root);
                Area = ClickArea.On(Box);
                Area.Click += _ => Press();
                Area.HoverChanged += on => { _hovered = on; Restyle(); };
                States = CssStates.Of(Root);
                if (focusable) Focusable = new Focusable { Target = Root, Press = Press, Ring = SetFocusRing, CanFocus = () => !_disabled };
            }

            public RectTransform Root { get; }
            public UiNode Node { get; }
            public BoxGraphic Box { get; }
            public ClickArea Area { get; }
            public CssStates States { get; }

            /// <summary>Its Tab entry (null for tabindex -1).</summary>
            public Focusable Focusable { get; }

            /// <summary>onclick.</summary>
            public Action OnClick;

            public bool Hovered => _hovered;

            /// <summary>A rule of the button, after the ones added before it (cascade order), in force while `when` holds.</summary>
            public DepButton Rule(string name, string css, Func<bool> when)
            {
                _rules.Add((name, css ?? "", when));
                _registered = false;
                return this;
            }

            /// <summary>:hover (whenDisabled false: :hover:not([disabled])).</summary>
            public DepButton OnHover(string css, bool whenDisabled = true) => Rule(":hover", css, () => _hovered && (whenDisabled || !_disabled));

            /// <summary>.cls on the button (Toggle turns it on and off).</summary>
            public DepButton OnClass(string cls, string css) => Rule("." + cls, css, () => _classes.Contains(cls));

            /// <summary>[disabled].</summary>
            public DepButton OnDisabled(string css) => Rule("[disabled]", css, () => _disabled);

            /// <summary>The rules are all given: the first look, at once.</summary>
            public DepButton Done()
            {
                _done = true;
                Restyle(true);
                return this;
            }

            public bool Has(string cls) => _classes.Contains(cls);

            /// <summary>classList.toggle(cls, on).</summary>
            public void Toggle(string cls, bool on, bool apply = true)
            {
                bool changed = on ? _classes.Add(cls) : _classes.Remove(cls);
                if (changed && apply) Restyle();
            }

            /// <summary>[disabled]: no clicks, no Tab.</summary>
            public bool Disabled
            {
                get => _disabled;
                set
                {
                    if (_disabled == value) return;
                    _disabled = value;
                    Restyle();
                }
            }

            /// <summary>b.click(): onclick unless disabled; the microtasks run after it.</summary>
            public void Press()
            {
                if (_disabled || !Root) return;
                try { OnClick?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
                Microtasks.Drain();
            }

            /// <summary>:focus-visible (outline 2px --wick, offset 3).</summary>
            public void SetFocusRing(bool on)
            {
                _focus = on;
                Restyle();
            }

            /// <summary>styler(el, init): opacity and transform inline from now on.</summary>
            public global::Trial.Kit.Styler InlineStyler(float o = 1f, float x = 0f, float y = 0f, float s = 1f, float r = 0f)
            {
                _inlineO = true;
                _inlineT = true;
                _registered = false;
                Restyle(true);
                return global::Trial.Kit.Styler.Of(Node, o, x, y, s, r);
            }

            /// <summary>el.style.transform = ''.</summary>
            public void ClearTransform()
            {
                if (!Node) return;
                Node.ResetTransform();
                _inlineT = false;
                _registered = false;
                Restyle(true);
            }

            private string F(string css)
            {
                if (_inlineO && _inlineT) return MetaStyle.Without(css, "width", "height", "opacity", "transform");
                if (_inlineO) return MetaStyle.Without(css, "width", "height", "opacity");
                if (_inlineT) return MetaStyle.Without(css, "width", "height", "transform");
                return MetaStyle.Without(css, "width", "height");
            }

            private static bool Eval(Func<bool> when)
            {
                try { return when != null && when(); }
                catch (Exception e) { Debug.LogException(e); return false; }
            }

            /// <summary>The rules in force applied (instant: no transitions).</summary>
            public void Restyle(bool instant = false)
            {
                if (!_done || !Root || States == null) return;
                if (StyledAtOnce && Time.frameCount == _bornFrame) instant = true;
                if (!_registered)
                {
                    States.Base(F(_base));
                    States.State(":focus-visible", F(MetaStyle.ButtonFocusVisibleCss), _focus);
                    foreach (var r in _rules) States.State(r.Name, F(r.Css), Eval(r.When));
                    _registered = true;
                    States.Apply(instant);
                    return;
                }
                States.SetState(":focus-visible", _focus, false);
                foreach (var r in _rules) States.SetState(r.Name, Eval(r.When), false);
                States.Apply(instant);
            }
        }
    }
}
