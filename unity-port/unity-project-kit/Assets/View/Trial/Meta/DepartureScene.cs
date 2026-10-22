// Port of scenes/meta/departure.js (出立, CAMP_FACILITIES_DESIGN.md §3.2): the battle deck first, then the tools, the
// consumables and the talent — Unity's DeckSelectScreen (#190) and DepartureScreen (#58) as two steps of one screen.
// This file holds the screen, its header band, the deck step (デッキを組む, departure.js:200-507), the step switch, the
// leave, the keys and the draft; the supplies step (出立の支度, departure.js:509-658) is DepartureSuppliesView.cs.
// DepartureSupplies.js is Trial.Bridge.DepartureCarry; cardFace.js is MetaCardFace.
//
// It decides nothing: what a filter keeps, what a page holds, whether a card may go in, every word about a card and
// whether the life may set out come from Depiction.Bridge.DeckBuilder / DepartureBuilder (whose rules are the core's),
// built with the life's DeckLimits (ctx.LifeStart's DeckMin / DeckMax / ToolSlots / ConsumableSlots, the survivor's
// bonus included). The look is meta.css's 出立 block, copied verbatim below so it diffs against the stylesheet.
//
// Layout: every element at the JS's stage px. A child the JS places absolutely inside a bordered box sits in that box's
// padding box (one border width in), as CSS absolute positioning does. The flex rows the JS leaves to the browser are
// laid out by the same rules (Ocb.Flow, and by hand where the rule is simple: the filter row's `flex: 1 1 0` with
// min-content minimums, the − ×n ＋ rows' space-between).
//
// DOM order: the JS appends the header first and re-appends it after both steps (departure.js:740-742); here it is
// built after both steps, which is the same final order (z order, and the Tab order: the steps' buttons, ←, 設定, 手記).
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
using Trial.Kit;
using Trial.Script;
using UnityEngine;
using UnityEngine.EventSystems;
using TextMeasure = Trial.Kit.TextMeasure;

namespace Trial.Meta
{
    public sealed class DepartureScene : IScene
    {
        // ---- meta.css: 出立, verbatim (meta.css:150-235, :421-446) ---------------------------------------------------

        /// <summary>meta.css:151 .ocbg-meta-dep-page.</summary>
        internal const string DepPageCss = "position: absolute;";

        /// <summary>meta.css:152 .ocbg-meta-back.</summary>
        internal const string BackCss = "display: inline-flex; align-items: center; gap: var(--space-8); padding: 0 18px; background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); font: 700 18px/1 var(--font-body); letter-spacing: .06em; transition: border-color .14s;";

        /// <summary>meta.css:153 .ocbg-meta-back:hover.</summary>
        internal const string BackHoverCss = "border-color: var(--ink);";

        /// <summary>meta.css:423 .ocbg-meta-back.</summary>
        internal const string BackNoWrapCss = "white-space: nowrap;";

        /// <summary>meta.css:424 .ocbg-meta-back:hover.</summary>
        internal const string BackHoverLiftCss = "transform: translateX(-2px);";

        /// <summary>meta.css:425 .ocbg-meta-back.</summary>
        internal const string BackTransitionCss = "transition: border-color .14s, transform .14s;";

        /// <summary>meta.css:154 .ocbg-meta-stepper.</summary>
        internal const string StepperCss = "display: flex; align-items: center; justify-content: flex-end; gap: var(--space-12);";

        /// <summary>meta.css:155 .ocbg-meta-stepper span.</summary>
        internal const string StepperSpanCss = "display: inline-flex; align-items: center; gap: 10px; font: 700 20px/1 var(--font-display); letter-spacing: .1em; opacity: .5; transition: opacity .2s;";

        /// <summary>meta.css:156 .ocbg-meta-stepper b.</summary>
        internal const string StepperBCss = "width: 32px; height: 32px; display: inline-flex; align-items: center; justify-content: center; border: var(--stroke-frame) solid var(--steel); border-radius: 50%; font: 600 16px/1 var(--font-mono);";

        /// <summary>meta.css:157 .ocbg-meta-stepper span.is-on.</summary>
        internal const string StepperSpanOnCss = "opacity: 1;";

        /// <summary>meta.css:158 .ocbg-meta-stepper span.is-on b.</summary>
        internal const string StepperBOnCss = "border-color: var(--amber); background: color-mix(in srgb, var(--amber) 18%, transparent);";

        /// <summary>meta.css:159 .ocbg-meta-stepper i.</summary>
        internal const string StepperICss = "width: 56px; height: 1px; background: var(--rule);";

        /// <summary>meta.css:160 .ocbg-meta-dep-title.</summary>
        internal const string DepTitleCss = "font: 700 32px/1.2 var(--font-display); letter-spacing: .06em; white-space: nowrap;";

        /// <summary>meta.css:161 .ocbg-meta-dep-notice.</summary>
        internal const string DepNoticeCss = "font: 400 18px/1.4 var(--font-body);";

        /// <summary>meta.css:162 .ocbg-meta-dep-h2.</summary>
        internal const string DepH2Css = "font: 700 20px/1.4 var(--font-body); letter-spacing: .04em; white-space: nowrap;";

        /// <summary>meta.css:163 .ocbg-meta-dep-h2.is-wrap.</summary>
        internal const string DepH2WrapCss = "white-space: normal;";

        /// <summary>meta.css:165 .ocbg-meta-filters.</summary>
        internal const string FiltersCss = "display: flex; gap: var(--space-8);";

        /// <summary>meta.css:166 .ocbg-meta-filter.</summary>
        internal const string FilterCss = "height: 48px; padding: 0 14px; flex: 1 1 0; display: inline-flex; align-items: center; justify-content: center; gap: 8px; background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); font: 700 18px/1 var(--font-body); white-space: nowrap; transition: border-color .14s, background .14s;";

        /// <summary>meta.css:167 .ocbg-meta-filter:hover.</summary>
        internal const string FilterHoverCss = "border-color: var(--ink);";

        /// <summary>meta.css:168 .ocbg-meta-filter.is-on.</summary>
        internal const string FilterOnCss = "border: var(--stroke-frame) solid var(--amber); background: color-mix(in srgb, var(--amber) 16%, var(--panel));";

        /// <summary>meta.css:169 .ocbg-meta-filter-gap.</summary>
        internal const string FilterGapCss = "flex: 0 0 12px;";

        /// <summary>meta.css:170 .ocbg-meta-costdot.</summary>
        internal const string CostDotCss = "width: 26px; height: 26px; display: inline-flex; align-items: center; justify-content: center; border: var(--stroke-frame) solid var(--amber); border-radius: 50%; font: 700 16px/1 var(--font-mono); font-style: normal;";

        /// <summary>meta.css:174 .ocbg-meta-departure .ocbg-meta-cell.</summary>
        internal const string CellCss = "position: absolute;";

        /// <summary>meta.css:175 .ocbg-meta-cell-face.</summary>
        internal const string CellFaceCss = "position: relative; display: block; width: 156px; height: 219px; margin: 0 auto; border-radius: var(--radius-panel); transition: transform .14s ease-out, box-shadow .14s;";

        /// <summary>meta.css:176 .ocbg-meta-cell-face:hover.</summary>
        internal const string CellFaceHoverCss = "transform: translateY(-6px); box-shadow: 0 10px 22px color-mix(in srgb, var(--ground) 80%, transparent);";

        /// <summary>meta.css:177 .ocbg-meta-cell.is-sel .ocbg-meta-cell-face.</summary>
        internal const string CellSelFaceCss = "box-shadow: 0 0 0 3px var(--amber), 0 0 20px color-mix(in srgb, var(--amber) 40%, transparent);";

        /// <summary>meta.css:178 .ocbg-meta-cell.is-unowned .ocbg-meta-cell-face.</summary>
        internal const string CellUnownedFaceCss = "opacity: .4;";

        /// <summary>meta.css:179 .ocbg-meta-unowned.</summary>
        internal const string UnownedCss = "position: absolute; left: 50%; top: 92px; transform: translateX(-50%); padding: 4px 10px; background: var(--panel-solid); border: var(--stroke-rule) solid var(--steel); border-radius: var(--radius-tag); font: 700 16px/1 var(--font-body); pointer-events: none;";

        /// <summary>meta.css:202 .ocbg-meta-countbox.</summary>
        internal const string CountBoxCss = "background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); padding: 16px 22px; display: flex; flex-direction: column; gap: var(--space-12);";

        /// <summary>meta.css:203 .ocbg-meta-count-row.</summary>
        internal const string CountRowCss = "display: flex; align-items: center; gap: var(--space-24);";

        /// <summary>meta.css:204 .ocbg-meta-total-wrap.</summary>
        internal const string TotalWrapCss = "display: flex; align-items: baseline; gap: 8px; min-width: 128px;";

        /// <summary>meta.css:205 .ocbg-meta-total.</summary>
        internal const string TotalCss = "font: 700 64px/1 var(--font-mono); color: var(--amber); display: inline-block; min-width: 80px; text-align: right;";

        /// <summary>meta.css:206 .ocbg-meta-total-wrap span.</summary>
        internal const string TotalUnitCss = "font: 700 24px/1 var(--font-body);";

        /// <summary>meta.css:207 .ocbg-meta-gauge-wrap.</summary>
        internal const string GaugeWrapCss = "position: relative; flex: 1; padding-bottom: 24px;";

        /// <summary>meta.css:208 .ocbg-meta-gauge.</summary>
        internal const string GaugeCss = "position: relative; height: 14px; border-radius: 7px; background: color-mix(in srgb, var(--ground) 70%, transparent); border: var(--stroke-rule) solid var(--rule); overflow: hidden;";

        /// <summary>meta.css:209 .ocbg-meta-gauge-fill.</summary>
        internal const string GaugeFillCss = "position: absolute; left: 0; top: 0; bottom: 0; background: var(--steel); transition: width .26s ease-out, background .2s;";

        /// <summary>meta.css:210 .ocbg-meta-gauge.is-ok .ocbg-meta-gauge-fill.</summary>
        internal const string GaugeFillOkCss = "background: var(--amber);";

        /// <summary>meta.css:211 .ocbg-meta-gauge-min.</summary>
        internal const string GaugeMinCss = "position: absolute; top: -2px; bottom: -2px; width: 2px; margin-left: -1px; background: var(--ink);";

        /// <summary>meta.css:212 .ocbg-meta-gauge-labels span.</summary>
        internal const string GaugeLabelCss = "position: absolute; top: 22px; transform: translateX(-50%); font: 600 16px/1 var(--font-mono);";

        /// <summary>meta.css:213 .ocbg-meta-gauge-labels span:last-child.</summary>
        internal const string GaugeLabelLastCss = "transform: translateX(-100%);";

        /// <summary>meta.css:219 .ocbg-meta-decklist.</summary>
        internal const string DeckListCss = "background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); padding: 10px 16px; display: flex; flex-direction: column; gap: 6px;";

        /// <summary>meta.css:220 .ocbg-meta-decklist header.</summary>
        internal const string DeckListHeaderCss = "display: flex; align-items: baseline; gap: 12px; height: 16px;";

        /// <summary>meta.css:221 .ocbg-meta-decklist h4.</summary>
        internal const string DeckListH4Css = "font: 700 16px/1 var(--font-body); letter-spacing: .1em;";

        /// <summary>meta.css:222 .ocbg-meta-decklist-kinds.</summary>
        internal const string DeckListKindsCss = "font: 600 16px/1 var(--font-mono);";

        /// <summary>meta.css:223 .ocbg-meta-decklist-more.</summary>
        internal const string DeckListMoreCss = "margin-left: auto; font: 400 16px/1 var(--font-body); letter-spacing: .06em; white-space: nowrap;";

        /// <summary>meta.css:224 .ocbg-meta-decklist-more:not(:empty)::before.</summary>
        internal const string DeckListMoreBeforeCss = "content: \"▼\"; display: inline-block; margin-right: 6px; font-size: 12px; color: var(--amber); transform: translateY(-1px);";

        /// <summary>meta.css:225 .ocbg-meta-decklist-more.is-up:not(:empty)::before.</summary>
        internal const string DeckListMoreUpCss = "content: \"▲\";";

        /// <summary>meta.css:226 .ocbg-meta-decklist-body.</summary>
        internal const string DeckListBodyCss = "position: relative; display: flex; flex-wrap: wrap; gap: 6px 8px; overflow-y: auto; height: 84px; flex: none; align-content: flex-start; scroll-snap-type: y mandatory; scrollbar-width: none;";

        /// <summary>meta.css:228 .ocbg-meta-deckchip.</summary>
        internal const string DeckChipCss = "height: 24px; padding: 2px 8px; border-radius: var(--radius-tag); background: color-mix(in srgb, var(--ink) 7%, transparent); font: 400 16px/20px var(--font-body); white-space: nowrap; scroll-snap-align: start; transition: box-shadow .14s, background .14s;";

        /// <summary>meta.css:229 .ocbg-meta-deckchip:hover.</summary>
        internal const string DeckChipHoverCss = "background: color-mix(in srgb, var(--ink) 13%, transparent);";

        /// <summary>meta.css:230 .ocbg-meta-deckchip.is-sel.</summary>
        internal const string DeckChipSelCss = "box-shadow: inset 0 0 0 var(--stroke-rule) var(--amber); background: color-mix(in srgb, var(--amber) 16%, transparent);";

        /// <summary>meta.css:231 .ocbg-meta-decklist-body.is-more-below (mask-image).</summary>
        internal const string DeckListMoreBelowMask = "linear-gradient(180deg, var(--ground) 86%, transparent)";

        /// <summary>meta.css:232 .ocbg-meta-decklist-body.is-more-above (mask-image).</summary>
        internal const string DeckListMoreAboveMask = "linear-gradient(0deg, var(--ground) 86%, transparent)";

        /// <summary>meta.css:233 .ocbg-meta-decklist-body.is-more-above.is-more-below (mask-image).</summary>
        internal const string DeckListMoreBothMask = "linear-gradient(180deg, transparent, var(--ground) 24%, var(--ground) 86%, transparent)";

        /// <summary>meta.css:234 .ocbg-meta-presets.</summary>
        internal const string PresetsCss = "display: flex; gap: var(--space-12);";

        /// <summary>meta.css:443 .ocbg-meta-dep-head.</summary>
        internal const string DepHeadCss = "display: flex; align-items: center; gap: var(--space-24);";

        /// <summary>meta.css:444 .ocbg-meta-bonus.</summary>
        internal const string BonusCss = "display: inline-flex; align-items: center; gap: 10px; padding: 7px 16px; border: var(--stroke-rule) solid var(--wick); border-radius: var(--radius-panel); background: color-mix(in srgb, var(--wick) 10%, var(--panel)); color: var(--ink); font: 400 18px/1.2 var(--font-body); white-space: nowrap;";

        /// <summary>meta.css:445 .ocbg-meta-bonus b.</summary>
        internal const string BonusBCss = "font: 700 18px/1.2 var(--font-body); letter-spacing: .04em;";

        /// <summary>meta.css:446 .ocbg-meta-bonus-dot.</summary>
        internal const string BonusDotCss = "width: 10px; height: 10px; flex: none; border-radius: 50%; background: var(--wick); box-shadow: 0 0 8px var(--wick);";

        // ---- departure.js: constants ------------------------------------------------------------------------------

        // js: scenes/meta/departure.js:30 — DeckSelectScreen: 6 columns × 3 rows, the card at 72% above its − ×n ＋ row.
        // (Columns is only ever read bare: a `Columns.` member access is what the View rules boundary forbids.)
        internal const int Columns = 6;
        internal const int Rows = 3;
        internal const int PerPage = Columns * Rows;
        internal const float FaceScale = 0.72f;

        // js: scenes/meta/departure.js:35 — DepartureScreen: the talent grid, 3 × 6.
        internal const int TalentColumns = 3;
        internal const int TalentRows = 6;
        internal const int TalentPerPage = TalentColumns * TalentRows;

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
        /// 出立's draft: the deck being built and the supplies picked, not yet set out. It is kept while the heir steps back
        /// into 継承の間 and comes again, over closing the game and opening it again, so 「← 継承の間へ」 or a closed window
        /// throws nothing away. Only the life it was made for reads it back (the lineage seed, the generation and the
        /// life's seed); 出立する clears it, since the router then keeps the deck itself (ctx.SavedDeck). A per-device
        /// convenience: memory first, the store behind it, every access guarded.
        /// </summary>
        // js: scenes/meta/departure.js:62
        public const string DraftKey = "ocb.departure.draft.v1";

        /// <summary>
        /// Where the draft is kept (the browser's localStorage): the app's store when the router sets one (app.md §2.1:
        /// departure-draft.v1.json under the save folder), else PlayerPrefs under <see cref="DraftKey"/>.
        /// </summary>
        public static IKeyValueStore DraftStore { get; set; }

        private static Draft _draftInMemory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _draftInMemory = null;
            DraftStore = null;
        }

        private static IKeyValueStore Store => DraftStore ?? PrefsStore.Instance;

        /// <summary>The draft as departure.js keeps it: { id, deck: DeckBuilder.Save, tools, consumables, talent }.</summary>
        private sealed class Draft
        {
            public string Id;
            public string Deck;
            public List<string> Tools = new List<string>();
            public List<string> Consumables = new List<string>();
            public string Talent;
        }

        // js: scenes/meta/departure.js:64
        /// <summary>draftIdOf(lifeStart): `lineageSeed|generation|lifeSeed`, or null without a life.</summary>
        private static string DraftIdOf(LifeStart lifeStart)
        {
            if (lifeStart == null || lifeStart.Life == null) return null;
            string lineageSeed = lifeStart.Lineage != null ? lifeStart.Lineage.Seed : "";
            return string.Join("|", lineageSeed ?? "", lifeStart.Life.Generation.ToString(CultureInfo.InvariantCulture),
                lifeStart.Seed.ToString(CultureInfo.InvariantCulture));
        }

        // js: scenes/meta/departure.js:69
        /// <summary>storedDraft(storage, id): the stored draft of this life, or null (storage missing, broken, another life).</summary>
        private static Draft StoredDraft(IKeyValueStore storage, string id)
        {
            try
            {
                string text = storage.Read(DraftKey);
                object d = string.IsNullOrEmpty(text) ? null : global::InheritanceCore.Json.Parse(text);
                if (d is JsonObject o && o["id"] is string did && did == id && o["deck"] is string deck)
                {
                    var draft = new Draft { Id = did, Deck = deck, Talent = o["talent"] as string };
                    if (o["tools"] is List<object> tools) foreach (object t in tools) if (t is string s) draft.Tools.Add(s);
                    if (o["consumables"] is List<object> cons) foreach (object c in cons) if (c is string s) draft.Consumables.Add(s);
                    return draft;
                }
            }
            catch (Exception)
            {
                // storage unavailable
            }
            return null;
        }

        // js: scenes/meta/departure.js:77
        /// <summary>readDraft(id): memory first, then the store (the browser's older sessionStorage copy has no counterpart here).</summary>
        private static Draft ReadDraft(string id)
        {
            if (_draftInMemory != null && _draftInMemory.Id == id) return _draftInMemory;
            return StoredDraft(Store, id);
        }

        // js: scenes/meta/departure.js:81
        private static void WriteDraft(Draft d)
        {
            _draftInMemory = d;
            try
            {
                var o = new JsonObject();
                o.Set("id", d.Id);
                o.Set("deck", d.Deck);
                o.Set("tools", d.Tools.Cast<object>().ToList());
                o.Set("consumables", d.Consumables.Cast<object>().ToList());
                o.Set("talent", d.Talent);
                Store.Write(DraftKey, global::InheritanceCore.Json.Stringify(o));
            }
            catch (Exception)
            {
                // storage unavailable
            }
        }

        // js: scenes/meta/departure.js:85
        private static void ClearDraft()
        {
            _draftInMemory = null;
            try { Store.Remove(DraftKey); }
            catch (Exception) { /* storage unavailable */ }
        }

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
        private string _step = "deck";
        private int _randomSeed;
        private readonly Dictionary<string, CardFace> _faces = new Dictionary<string, CardFace>(StringComparer.Ordinal);

        // the header
        private DepButton _backBtn;
        private TextMeshProUGUI _backText;
        private MetaButton _settingsBtn, _journalBtn;
        private RectTransform _stepper;
        private StepSpan _stepDeck, _stepSup;

        // the deck step
        private RectTransform _deckPage;
        private TextMeshProUGUI _noticeEl, _deckKeys;
        private RectTransform _filters;
        private readonly List<FilterButton> _attrBtns = new List<FilterButton>();
        private readonly List<FilterButton> _costBtns = new List<FilterButton>();
        private RectTransform _grid;
        private List<Cell> _cells = new List<Cell>();
        private RectTransform _pageNav;
        private MetaButton _prevBtn, _nextBtn;
        private TextMeshProUGUI _pageText;
        private RectTransform _detail, _detailBody;
        private TextMeshProUGUI _totalEl;
        private MetaRollNumber _total;
        private RectTransform _gauge, _gaugeFill;
        private CssStates _gaugeFillStates;
        private float _gaugePadW;
        private StatusLine _statusEl;
        private RectTransform _listBox;
        private TextMeshProUGUI _listKinds, _listMore, _listMoreMark;
        private ScrollArea _listBody;
        private readonly List<Chip> _chips = new List<Chip>();
        private readonly List<MetaButton> _presets = new List<MetaButton>();
        private MetaButton _toSupplies;

        // the supplies step
        private RectTransform _supPage;
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
            // js: scenes/meta/departure.js:109 — the kinds the heir may build from: the router's (or the initial forty), plus
            // the prototype deck's (Expedition.BeginLife owns its start deck's kinds anyway), so 「試作デッキ」 opens at its full 20.
            _owned = ctx.OwnsCatalogue ? null
                : DeckBuilder.WithPrototypeKinds(ctx.Owned != null && ctx.Owned.Count > 0 ? ctx.Owned : Expedition.InitialKinds);
            // The life's limits (DeckLimits.From reads the four numbers; a missing one is the C#'s).
            _limits = LimitsOf(_lifeStart);
            _deckMin = _limits.DeckMin;
            _deckCap = _limits.DeckMax;
            _root = MetaUtil.SceneRoot(host, "departure");
            _backdrop = MetaBackdrop.Create(_root, HallBackdropSpec.For(0.5), new MetaBackdrop.Options { Vignette = 0.6f });
            _clock = MetaUtil.SceneClock((dt, now) => { if (_backdrop != null) _backdrop.Paint(now); }, ctx.Stage);
            _scope = Focus.PushScope("departure");

            // ---- state (the bridge's builders) ----
            _draftId = DraftIdOf(_lifeStart);
            Draft draft = _draftId != null ? ReadDraft(_draftId) : null;
            // A saved deck (or a draft) past the life's cap — a survivor's 45 after the bonus ended — opens with every card
            // and the reason (DeckBuilder.ReadOverCap); any other broken string, as the C# reads it.
            string saved = draft != null ? draft.Deck : ctx.SavedDeck;
            if (draft != null || ctx.SavedDeck != null)
            {
                DeckBuilder read = DeckBuilder.ReadOverCap(saved, out string value, _limits);
                _deck = Adopt(read);
                _notice = value ?? "";
                _noticeOverCap = _deck.Total > _limits.DeckMax;
            }
            else
            {
                _deck = Adopt(DeckBuilder.Prototype(_limits));
            }
            _supplies = DepartureCarry.NewBuilder(_limits);
            if (draft != null)
            {
                foreach (string id in draft.Tools) _supplies.AddTool(id);
                foreach (string id in draft.Consumables) _supplies.AddConsumable(id);
                if (!string.IsNullOrEmpty(draft.Talent)) _supplies.ChooseTalent(_deck, draft.Talent);
            }
            // js: scenes/meta/departure.js:157 — (Seed & 0x3fffffff) || 1
            ulong seed = _lifeStart != null ? _lifeStart.Seed : (ctx.Seed ?? 1UL);
            _randomSeed = (int)(seed & 0x3fffffffUL);
            if (_randomSeed == 0) _randomSeed = 1;

            BuildDeckStep();
            _supPage = Ui.Div(_root, "ocbg-meta-dep-page", 0f, 0f, MetaUtil.W, MetaUtil.H);
            UiNode.Of(_supPage);
            _supView = new DepartureSuppliesView(this, _supPage);
            UiNode.Of(_supPage).Visible = false;   // supPage.style.display = 'none'
            BuildHeader();

            // ---- first frame (departure.js:737-754) ----
            DrawFilters();
            DrawGrid(0);
            _selected = _cells.Count > 0 ? _cells[0].Def : null;
            RefreshDeck();
            DrawDetail();
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

        /// <summary>DeckLimits.From(lifeStart): the life's four numbers (a missing one, or no life, is the C#'s).</summary>
        private static BattleCore.DeckLimits LimitsOf(LifeStart lifeStart)
        {
            if (lifeStart == null) return BattleCore.DeckLimits.Default;
            int? min = lifeStart.DeckMin > 0 ? lifeStart.DeckMin : (int?)null;
            int? max = lifeStart.DeckMax > 0 && lifeStart.DeckMax >= (min ?? 1) ? lifeStart.DeckMax : (int?)null;
            int? tools = lifeStart.ToolSlots >= 0 ? lifeStart.ToolSlots : (int?)null;
            int? cons = lifeStart.ConsumableSlots >= 0 ? lifeStart.ConsumableSlots : (int?)null;
            try { return new BattleCore.DeckLimits(min, max, tools, cons); }
            catch (ArgumentOutOfRangeException) { return BattleCore.DeckLimits.Default; }
        }

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
            WriteDraft(new Draft
            {
                Id = _draftId,
                Deck = _deck.Save(),
                Tools = _supplies.Tools.ToList(),
                Consumables = _supplies.Consumables.ToList(),
                Talent = _supplies.Talent,
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

        // js: scenes/meta/departure.js:165
        /// <summary>
        /// bonusChip(line): what the life's limits raise (the bridge's RaisedLine under DepartureBuilder.BonusLabel) beside
        /// a step's title, as a flex item of `head`; nothing on a life without the survivor's bonus.
        /// </summary>
        internal static RectTransform BonusChip(RectTransform head, string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            RectTransform chip = Ui.Div(head, "ocbg-meta-bonus");
            UiNode.Of(chip);
            BoxGraphic.On(chip, BonusCss);
            BoxGraphic dot = BoxGraphic.Make(chip, BonusDotCss, 0f, 0f, 10f, 10f, "ocbg-meta-bonus-dot");
            Ocb.NoShrink(dot);
            Label.Make(chip, DepartureBuilder.BonusLabel, MetaStyle.Text(BonusCss + BonusBCss), "b");
            Label.Make(chip, line, MetaStyle.Text(BonusCss), "span");
            Ocb.Flow(chip, BonusCss);
            return chip;
        }

        /// <summary>The title row of a step (.ocbg-meta-dep-head at (64, 88), 1160 wide): the h1 and the bonus chip.</summary>
        internal static RectTransform StepHead(RectTransform page, string title, string raised)
        {
            RectTransform head = Ui.Div(page, "ocbg-meta-dep-head", 64f, 88f, 1160f, 0f);
            UiNode.Of(head);
            Label.Make(head, title ?? "", MetaStyle.Text(DepTitleCss), "ocbg-meta-dep-title");
            BonusChip(head, raised);
            Ocb.Flow(head, DepHeadCss, null, 1160f, null);
            return head;
        }

        // ================================================================================ the header (departure.js:171-198)

        private void BuildHeader()
        {
            // js: scenes/meta/departure.js:172 — ← 継承の間へ (デッキに戻る on the supplies step)
            _backBtn = new DepButton(_root, "ocbg-meta-back", BackCss + BackNoWrapCss + BackTransitionCss)
                .OnHover(BackHoverCss + BackHoverLiftCss)
                .Done();
            _backBtn.OnClick = Back;
            MetaUtil.Icon(_backBtn.Root, "back", 22f, DsTokens.Ink);
            _backText = Label.Make(_backBtn.Root, InheritanceText.ToHallButton, MetaStyle.Text(BackCss + BackNoWrapCss), "span");
            _backBtn.Node.Place(64f, 24f);
            LayoutBack();

            // Top right: 設定 (Esc) and the journal (J) when the router gives them (CAMP_FACILITIES_DESIGN.md §3.3 / §3.5).
            // Same order as 継承の間: the journal, then 設定 at the right edge.
            float cornerX = 1872f;
            if (_ctx.OnSettings != null)
            {
                cornerX -= 120f;
                _settingsBtn = MetaButton.Chip(_root, MetaText.Settings, "Esc", cornerX, 24f, 120f, 52f);
                _settingsBtn.Click += OpenSettings;
                cornerX -= 16f;
            }
            if (_ctx.OnJournal != null)
            {
                cornerX -= 56f;
                _journalBtn = MetaButton.ChipIcon(_root, "journal", 30f, cornerX, 24f, 56f, 52f, DsTokens.Ink);
                TitleTip.On(_journalBtn.Box, global::Journal.JournalBook.Heading);
                _journalBtn.Click += () => { if (!_busy) _ctx.OnJournal?.Invoke(); };
            }
            if (_settingsBtn != null || _journalBtn != null) cornerX -= 24f;

            // the stepper (〔1〕デッキを組む ── 〔2〕出立の支度), right-aligned up to the corner
            _stepper = Ui.Div(_root, "ocbg-meta-stepper", 1236f, 24f, cornerX - 1236f, 52f);
            UiNode.Of(_stepper);
            _stepDeck = new StepSpan(_stepper, "1", MetaText.StepDeck, true);
            BoxGraphic.Make(_stepper, StepperICss, 0f, 0f, 56f, 1f, "i");
            _stepSup = new StepSpan(_stepper, "2", MetaText.StepSupplies, false);
            Ocb.Flow(_stepper, StepperCss, null, cornerX - 1236f, 52f);

            // DOM order: ←, the stepper, then the corner (設定, 手記), over both steps
            MoveLast(_backBtn.Root);
            MoveLast(_stepper);
            if (_settingsBtn != null) MoveLast(_settingsBtn.Root);
            if (_journalBtn != null) MoveLast(_journalBtn.Root);
        }

        /// <summary>The back button sized to its content (inline-flex: padding 18, the icon, gap 8, the words), 52 tall.</summary>
        private void LayoutBack()
        {
            if (_backBtn == null || !_backBtn.Root) return;
            Ocb.Flow(_backBtn.Root, BackCss, null, null, 52f);
        }

        /// <summary>root.appendChild(el) for an element already in the root: it moves to the end of the DOM order.</summary>
        private static void MoveLast(RectTransform el)
        {
            if (!el || !el.parent) return;
            double max = 0;
            foreach (Transform t in el.parent)
            {
                UiNode n = t.GetComponent<UiNode>();
                if (n && n.DomOrder > max) max = n.DomOrder;
            }
            UiNode.Of(el).DomOrder = max + 0.5;
            el.SetAsLastSibling();
        }

        // ================================================================================ deck step (departure.js:200-507)

        private void BuildDeckStep()
        {
            _deckPage = Ui.Div(_root, "ocbg-meta-dep-page", 0f, 0f, MetaUtil.W, MetaUtil.H);
            UiNode.Of(_deckPage);
            StepHead(_deckPage, _deck.LifeTitle, _deck.RaisedLine);
            _noticeEl = Text(_deckPage, _notice, DepNoticeCss, 1236f, 92f, 636f, null, "ocbg-meta-dep-notice", p: true);
            // the deck step's keys end on Esc like the supplies step's (Esc opens the settings here, else goes back)
            _deckKeys = MetaStyle.Keys(_deckPage, MetaText.DeckKeys + "　" + (_ctx.OnSettings != null ? MetaText.EscSettings : MetaText.EscBack),
                1236f, 100f, 636f, right: true);
            _deckKeys.gameObject.SetActive(string.IsNullOrEmpty(_notice));

            BuildFilters();

            // the card grid
            _grid = Ui.Div(_deckPage, "ocbg-meta-grid", 64f, 214f, 1140f, 800f);
            UiNode.Of(_grid);

            // page nav (DeckSelectScreen: 前のページ / n / m ページ（k 種）/ 次のページ)
            _pageNav = Ui.Div(_deckPage, "ocbg-meta-pagenav", 64f, 1016f, 1140f, 48f);
            UiNode.Of(_pageNav);
            _prevBtn = MetaButton.Nav(_pageNav, MetaText.PrevPage, false, 0f, 0f);
            _prevBtn.Click += () => Turn(-1);
            _pageText = Label.Make(_pageNav, "", MetaStyle.Text(MetaStyle.PageTextCss), "ocbg-meta-pagetext");
            _nextBtn = MetaButton.Nav(_pageNav, MetaText.NextPage, true, 0f, 0f);
            _nextBtn.Click += () => Turn(1);

            // detail (DeckBuilder.DetailOf + the face's terms)
            _detail = Ui.Div(_deckPage, "ocbg-meta-detail", 1236f, 146f, 636f, 452f);
            UiNode.Of(_detail);
            BoxGraphic.On(_detail, MetaStyle.DetailCss);
            _detailBody = Ui.Div(_detail, "body", 0f, 0f, 636f, 452f);
            UiNode.Of(_detailBody);

            BuildCountBox();
            BuildDeckList();

            // presets
            string[] names = { MetaText.Prototype, MetaText.Random, MetaText.Clear };
            Action[] acts =
            {
                () => Replace(Adopt(DeckBuilder.Prototype(_limits))),
                () => Replace(RandomDeck()),
                () => Replace(new DeckBuilder(_owned, _limits)),
            };
            RectTransform presetRow = Ui.Div(_deckPage, "ocbg-meta-presets", 1236f, 916f, 636f, 50f);
            UiNode.Of(presetRow);
            float presetW = (636f - 12f * 2f) / 3f;   // flex: 1, gap 12
            for (int i = 0; i < names.Length; i++)
            {
                MetaButton b = MetaButton.Btn(presetRow, names[i], i * (presetW + 12f), 0f, presetW, 50f);
                Action act = acts[i];
                b.Click += act;
                _presets.Add(b);
            }
            _toSupplies = MetaButton.Primary(_deckPage, MetaText.ToSupplies, "Enter", 1236f, 984f, 636f, 72f);
            _toSupplies.Click += () => ShowSupplies().Forget();
        }

        // ---- filters (DeckBuilder.ShownAttributeOptions — the battle's words, X-17 — / CostOptions) ----

        private sealed class FilterButton
        {
            public DepButton Button;
            public bool IsCost;
            public BattleCore.BattleAttribute Attribute;
            public int Cost;
            public float ContentW;
        }

        private void BuildFilters()
        {
            _filters = Ui.Div(_deckPage, "ocbg-meta-filters", 64f, 146f, 1140f, 50f);
            UiNode.Of(_filters);
            foreach (KeyValuePair<string, BattleCore.BattleAttribute> opt in DeckBuilder.ShownAttributeOptions)
            {
                FilterButton f = MakeFilter(opt.Key, false, opt.Value, 0);
                f.Button.OnClick = () =>
                {
                    _filter.Attribute = f.Attribute;
                    _page = 0;
                    _sound.Play("uiClick");
                    DrawFilters();
                    DrawGrid(0);
                };
                _attrBtns.Add(f);
            }
            foreach (KeyValuePair<string, int> opt in DeckBuilder.CostOptions)
            {
                FilterButton f = MakeFilter(opt.Key, true, BattleCore.BattleAttribute.None, opt.Value);
                f.Button.OnClick = () =>
                {
                    _filter.Cost = f.Cost;
                    _page = 0;
                    _sound.Play("uiClick");
                    DrawFilters();
                    DrawGrid(0);
                };
                _costBtns.Add(f);
            }
        }

        private FilterButton MakeFilter(string word, bool cost, BattleCore.BattleAttribute attribute, int value)
        {
            var f = new FilterButton { IsCost = cost, Attribute = attribute, Cost = value };
            f.Button = new DepButton(_filters, cost ? "ocbg-meta-filter is-cost" : "ocbg-meta-filter", FilterCss)
                .OnHover(FilterHoverCss)
                .OnClass("is-on", FilterOnCss)
                .Done();
            float content = 0f;
            if (!cost && AttrIcon.TryGetValue(attribute, out string icon))
            {
                PathGraphic g = MetaUtil.Icon(f.Button.Root, icon, 20f, AttrInk[attribute]);
                Ocb.NoShrink(g);
                content += 20f + 8f;
            }
            if (cost && value > 0)
            {
                RectTransform dot = Ocb.El(f.Button.Root, "ocbg-meta-costdot", CostDotCss);
                Label.Make(dot, value.ToString(CultureInfo.InvariantCulture), MetaStyle.Text(FilterCss + CostDotCss), "n");
                Ocb.Flow(dot, CostDotCss);
                Ocb.NoShrink(dot);
                content += 26f + 8f;
            }
            TextStyle ts = MetaStyle.Text(FilterCss);
            Label.Make(f.Button.Root, word ?? "", ts, "span");
            f.ContentW = content + TextMeasure.Width(word ?? "", ts);
            return f;
        }

        // js: scenes/meta/departure.js:230
        private void DrawFilters()
        {
            foreach (FilterButton b in _attrBtns) b.Button.Toggle("is-on", _filter.Attribute == b.Attribute);
            foreach (FilterButton b in _costBtns) b.Button.Toggle("is-on", _filter.Cost == b.Cost);
            LayoutFilters();
        }

        /// <summary>
        /// .ocbg-meta-filters (display: flex, gap 8) over 1140: every .ocbg-meta-filter is flex: 1 1 0 with its min-content
        /// width as the minimum (nowrap content + padding 14 + its border, 2 px while .is-on), the .ocbg-meta-filter-gap
        /// 0 0 12px. CSS Flexbox §9.7 as Chrome runs it: with box-sizing: border-box a basis of 0 floors the content box at
        /// 0, so each button's outer base size is its padding and border (30, 32 while .is-on) and the free space beyond
        /// them is shared evenly; a button whose share leaves it under its minimum is frozen there and the rest shared
        /// again. (So an .is-on button stands 2 px wider than its unfrozen neighbours: 106 against 104 in shot 302.)
        /// </summary>
        private void LayoutFilters()
        {
            var all = new List<FilterButton>(_attrBtns);
            all.AddRange(_costBtns);
            int n = all.Count;
            if (n == 0) return;
            float gaps = 8f * n;   // n buttons + the gap element: n gaps between n + 1 items
            float room = 1140f - gaps - 12f;
            var min = new float[n];
            var basis = new float[n];
            var w = new float[n];
            var frozen = new bool[n];
            for (int i = 0; i < n; i++)
            {
                float border = all[i].Button.Has("is-on") ? 2f : 1f;
                basis[i] = 28f + 2f * border;
                min[i] = all[i].ContentW + basis[i];
            }
            int open = n;
            while (open > 0)
            {
                // the free space left over the frozen buttons' sizes and the open buttons' bases, shared evenly
                float free = room;
                for (int i = 0; i < n; i++) free -= frozen[i] ? w[i] : basis[i];
                float share = free / open;
                bool froze = false;
                for (int i = 0; i < n; i++)
                {
                    if (frozen[i] || min[i] <= basis[i] + share) continue;
                    frozen[i] = true;
                    w[i] = min[i];
                    open--;
                    froze = true;
                }
                if (froze) continue;
                for (int i = 0; i < n; i++) if (!frozen[i]) w[i] = basis[i] + share;
                break;
            }
            float x = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i == _attrBtns.Count) x += 12f + 8f;   // the .ocbg-meta-filter-gap and its gap
                DepButton b = all[i].Button;
                b.Node.Place(x, 0f, w[i], 48f);
                Ocb.Flow(b.Root, FilterCss + (b.Has("is-on") ? FilterOnCss : ""), null, w[i], 48f);
                x += w[i] + 8f;
            }
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
            public bool IsIn;
        }

        // js: scenes/meta/departure.js:239
        private void DrawGrid(int dir)
        {
            DeckPage p = _deck.Page(_filter, _page, PerPage);
            _page = p.Index;
            Label.SetText(_pageText, p.Text);
            _prevBtn.Disabled = p.Index == 0;
            _nextBtn.Disabled = p.Index >= p.Count - 1;
            LayoutPageNav(_pageNav, _prevBtn, _pageText, _nextBtn, 1140f, 48f);
            Ui.Clear(_grid);
            var cells = new List<Cell>();
            for (int i = 0; i < p.Cards.Count; i++)
            {
                BattleCore.CardDef def = p.Cards[i];
                int col = i % Columns, row = i / Columns;
                cells.Add(MakeCell(def, col * 190f, row * 266f));
            }
            _cells = cells;
            RefreshCells();
            if (dir != 0)
            {
                for (int i = 0; i < _cells.Count; i++)
                {
                    Cell c = _cells[i];
                    global::Trial.Kit.Styler s = global::Trial.Kit.Styler.Of(c.Node, o: 0f, x: 36f * dir);
                    _clock.Tween(s, new[] { ("o", 1f), ("x", 0f) }, 240f, Ease.Out, (i % Columns) * 18f)
                        .Then(() => { if (c.Node) c.Node.ResetTransform(); });
                }
            }
            SyncFocus();
        }

        private Cell MakeCell(BattleCore.CardDef def, float x, float y)
        {
            var cell = new Cell { Def = def };
            bool owned = IsOwned(def.Id);
            cell.Root = Ui.Div(_grid, owned ? "ocbg-meta-cell" : "ocbg-meta-cell is-unowned", x, y, 182f, 262f);
            cell.Node = UiNode.Of(cell.Root);

            // the face (.ocbg-meta-cell-face, 156 × 219, centred: margin 0 auto) holding the card at 72 %
            cell.Face = new DepButton(cell.Root, "ocbg-meta-cell-face", CellFaceCss + MetaStyle.CellCss)
                .OnHover(CellFaceHoverCss)
                .OnClass("is-sel", CellSelFaceCss)
                .OnClass("is-unowned", CellUnownedFaceCss);
            if (!owned) cell.Face.Toggle("is-unowned", true, apply: false);
            cell.Face.Done();
            cell.Face.Node.Place((182f - 156f) / 2f, 0f, 156f, 219f);
            MetaCardFace.ScaledCard(cell.Face.Root, FaceFor(def), FaceScale);
            cell.Face.OnClick = () => Choose(def);
            cell.Face.Area.Up += e => { if (e != null && e.button == PointerEventData.InputButton.Right) Choose(def); };   // oncontextmenu
            cell.Face.Area.Enter += _ => _sound.Play("nodeHover");

            if (!owned)
            {
                // .ocbg-meta-unowned: left 50%, top 92, translateX(-50%); it lets the pointer through
                TextStyle ts = MetaStyle.Text(UnownedCss);
                float w = TextMeasure.Width(MetaText.NotOwned, ts) + 20f + 2f, h = ts.LineHeightPx + 8f + 2f;
                BoxGraphic tag = BoxGraphic.Make(cell.Root, UnownedCss, (182f - w) / 2f, 92f, w, h, "ocbg-meta-unowned");
                Label.Make(tag.rectTransform, MetaText.NotOwned, ts, 11f, 5f, null, null, "text");
                UiNode.Of(tag.rectTransform).PointerEvents = PointerEvents.None;
            }

            // .ocbg-meta-cell-row: left 13, right 13, bottom 0, 36 tall; − ×n ＋ spread (space-between)
            RectTransform rowEl = Ui.Div(cell.Root, "ocbg-meta-cell-row", 13f, 262f - 36f, 156f, 36f);
            UiNode.Of(rowEl);
            cell.Minus = MetaButton.Step(rowEl, "−", 0f, 0f);
            cell.Count = Label.Make(rowEl, "", MetaStyle.Text(MetaStyle.CountCss), (156f - 44f) / 2f, (36f - 26f) / 2f, 44f, 26f, "ocbg-meta-count");
            cell.CountNode = UiNode.Of(cell.Count.rectTransform);
            cell.Plus = MetaButton.Step(rowEl, "＋", 156f - 44f, 0f);
            cell.Minus.Click += () => Remove(def, cell);
            cell.Plus.Click += () => Add(def, cell);
            return cell;
        }

        // js: scenes/meta/departure.js:272
        private void RefreshCells()
        {
            foreach (Cell c in _cells)
            {
                string text = _deck.CountText(c.Def.Id);
                Label.SetText(c.Count, text);
                c.IsIn = text != "";
                c.Plus.Disabled = !_deck.CanAdd(c.Def.Id);
                c.Minus.Disabled = _deck.CountOf(c.Def.Id) == 0;
                c.Face.Toggle("is-sel", _selected != null && _selected.Id == c.Def.Id);
            }
        }

        // ---- page nav ----

        /// <summary>
        /// .ocbg-meta-pagenav (flex, centred, space-between) w × h: ← at the left, the page words between, → at the right.
        /// </summary>
        internal static void LayoutPageNav(RectTransform nav, MetaButton prev, TextMeshProUGUI text, MetaButton next, float w, float h)
        {
            float pw = prev.Node.W, nw = next.Node.W;
            TextStyle ts = Label.StyleOf(text);
            float tw = TextMeasure.Width(Label.StateOf(text).Text ?? "", ts);
            float free = Math.Max(0f, w - pw - tw - nw);
            prev.Place(0f, (h - prev.Node.H) / 2f);
            Ui.Place(text.rectTransform, pw + free / 2f, (h - ts.LineHeightPx) / 2f);
            next.Place(w - nw, (h - next.Node.H) / 2f);
        }

        // js: scenes/meta/departure.js:288
        private void Turn(int by)
        {
            int before = _page;
            _page += by;
            DeckPage p = _deck.Page(_filter, _page, PerPage);
            if (p.Index == before) { _page = before; return; }
            _sound.Play("nodeHover");
            DrawGrid(by);
        }

        // ---- detail ----

        // js: scenes/meta/departure.js:300
        private void DrawDetail()
        {
            Ui.Clear(_detailBody);
            if (_selected == null)
            {
                // .ocbg-meta-detail-hint: left 0, right 0, top 50%, translateY(-50%), centred (in the padding box)
                TextStyle hs = MetaStyle.P(MetaStyle.DetailHintCss);
                float hh = TextMeasure.Height(MetaText.PickCardHint, hs, 634f);
                Label.Make(_detailBody, MetaText.PickCardHint, hs, 1f, 1f + (450f - hh) / 2f, 634f, hh, "ocbg-meta-detail-hint");
                return;
            }
            CardFace face = FaceFor(_selected);
            List<string> lines = DeckBuilder.ShownDetailOf(_selected);
            OcbCard big = MetaCardFace.CardEl(_detailBody, face);
            big.Node.Place(1f + 16f, 1f + 16f);
            // the text column (252, 16, w 368): the name as h3, then one p per line, the last a flavour line when it is 「…」
            RectTransform textCol = Ui.Div(_detailBody, "ocbg-meta-detail-text", 1f + 252f, 1f + 16f, 368f, 0f);
            UiNode.Of(textCol);
            float y = 0f;
            TextStyle h3 = MetaStyle.P(MetaStyle.DetailH3Css);
            float h3H = TextMeasure.Height(lines[0], h3, 368f);
            Label.Make(textCol, lines[0], h3, 0f, y, 368f, h3H, "h3");
            y += h3H + 12f;
            for (int i = 0; i < lines.Count - 1; i++)
            {
                string l = lines[i + 1];
                bool flavour = i == lines.Count - 2 && l.StartsWith("「", StringComparison.Ordinal);
                TextStyle ps = MetaStyle.P(MetaStyle.DetailPCss + (flavour ? MetaStyle.FlavourCss : ""));
                float ph = TextMeasure.Height(l, ps, 368f);
                Label.Make(textCol, l, ps, 0f, y, 368f, ph, flavour ? "p is-flavour" : "p");
                y += ph + 8f;
            }
            TermsBox(_detailBody, face, 1f + 16f, 1f + 334f, 604f);
            global::Trial.Kit.Styler s = global::Trial.Kit.Styler.Of(big.Node, o: 0f, y: 10f);
            _clock.Tween(s, new[] { ("o", 1f), ("y", 0f) }, 200f, Ease.Out);
        }

        /// <summary>
        /// .ocbg-meta-terms at (x, y), w wide: termRows(face) stacked (gap 4) under a 1 px rule and a 12 px padding, at most
        /// 104 tall; past that it scrolls (overflow-y: auto, the 8 px steel thumb taking its width from the rows).
        /// </summary>
        private static void TermsBox(RectTransform parent, CardFace face, float x, float y, float w)
        {
            List<TermDefinition> terms = face?.Detail?.Terms;
            if (terms == null || terms.Count == 0) return;
            RectTransform box = Ui.Div(parent, "ocbg-meta-terms", x, y, w, 0f);
            UiNode node = UiNode.Of(box);
            BoxGraphic.Make(box, "background: var(--rule);", 0f, 0f, w, 1f, "border-top");
            ScrollArea area = ScrollArea.Make(box, "scroll", 0f, 1f, w, 103f);
            area.ShowThumb = true;
            BoxGraphic.On(area.Thumb, MetaStyle.ScrollbarThumbCss);
            float content = StackTerms(area.Content, face, w);
            if (12f + content > 103f)
            {
                Ui.Clear(area.Content);
                content = StackTerms(area.Content, face, w - area.ThumbWidth);
            }
            float scrollH = 12f + content;
            float boxH = Math.Min(104f, 1f + scrollH);
            node.H = boxH;
            Ui.Place(area.Viewport, 0f, 1f, w, boxH - 1f);
            area.Content.sizeDelta = new Vector2(w, scrollH);
            area.ScrollHeight = scrollH;
            area.Refresh();
        }

        private static float StackTerms(RectTransform content, CardFace face, float w)
        {
            List<TextMeshProUGUI> rows = MetaCardFace.TermRows(content, face, w);
            float y = 12f;
            for (int i = 0; i < rows.Count; i++)
            {
                float h = rows[i].rectTransform.sizeDelta.y;
                Ui.Place(rows[i].rectTransform, 0f, y);
                y += h + (i < rows.Count - 1 ? 4f : 0f);
            }
            return y - 12f;
        }

        // js: scenes/meta/departure.js:317
        private void Choose(BattleCore.CardDef def)
        {
            if (_selected != null && _selected.Id == def.Id) return;
            _selected = def;
            _sound.Play("uiClick");
            DrawDetail();
            RefreshCells();
            MarkChip();
        }

        // ---- the count: how many, the life's 20〜40 (45 with the survivor's bonus), DeckBuilder.StatusText ----

        private void BuildCountBox()
        {
            RectTransform box = Ui.Div(_deckPage, "ocbg-meta-countbox", 1236f, 614f, 636f, 150f);
            UiNode.Of(box);
            BoxGraphic.On(box, CountBoxCss);
            // the content box: border 1 + padding 16 / 22 → (23, 17), 590 wide; a column with gap 12
            const float cx = 23f, cy = 17f, cw = 636f - 2f - 44f;

            // .ocbg-meta-count-row (align-items: center, gap 24): the total (min-width 128) and the gauge (flex: 1)
            TextStyle totalStyle = MetaStyle.Text(TotalCss);
            TextStyle unitStyle = MetaStyle.Text(TotalUnitCss);
            float rowH = totalStyle.LineHeightPx;   // 64: the tallest item
            RectTransform totalWrap = Ui.Div(box, "ocbg-meta-total-wrap", cx, cy, 128f, rowH);
            UiNode.Of(totalWrap);
            // the number (min-width 80, right-aligned) and 「枚」 on its baseline, 8 apart
            _totalEl = Label.Make(totalWrap, "", totalStyle, 0f, 0f, 80f, rowH, "ocbg-meta-total");
            float baseTotal = TextMath.CssBaselineFromTop(totalStyle.Spec);
            float baseUnit = TextMath.CssBaselineFromTop(unitStyle.Spec);
            Label.Make(totalWrap, MetaText.CardsUnit, unitStyle, 80f + 8f, baseTotal - baseUnit, null, null, "span");

            float gx = cx + 128f + 24f, gw = cw - 128f - 24f;
            float wrapH = 14f + 24f;
            RectTransform gaugeWrap = Ui.Div(box, "ocbg-meta-gauge-wrap", gx, cy + (rowH - wrapH) / 2f, gw, wrapH);
            UiNode.Of(gaugeWrap);
            _gauge = Ui.Div(gaugeWrap, "ocbg-meta-gauge", 0f, 0f, gw, 14f);
            UiNode.Of(_gauge);
            BoxGraphic.On(_gauge, GaugeCss);
            // overflow: hidden: what is inside is cut to the padding box (1 px in), its corners rounded 7 − 1
            _gaugePadW = gw - 2f;
            _gaugeFill = Ui.Div(_gauge, "ocbg-meta-gauge-fill", 1f, 1f, 0f, 12f);
            UiNode.Of(_gaugeFill);
            BoxGraphic fill = BoxGraphic.On(_gaugeFill);
            fill.ClipConvex = Stadium(0f, 0f, _gaugePadW, 12f, 6f);
            _gaugeFillStates = CssStates.Of(_gaugeFill).Base(GaugeFillCss + "width: 0px;").State(".is-ok", GaugeFillOkCss);
            float minPct = 100f * _deckMin / _deckCap;
            // .ocbg-meta-gauge-min: left minPct %, top / bottom −2, 2 wide, margin-left −1; clipped like the fill
            float mx = 1f + _gaugePadW * minPct / 100f - 1f;
            BoxGraphic mark = BoxGraphic.Make(_gauge, GaugeMinCss, mx, 1f - 2f, 2f, 12f + 4f, "ocbg-meta-gauge-min");
            mark.ClipConvex = Stadium(1f - mx, 2f, _gaugePadW, 12f, 6f);
            // the scale's words: the minimum under its mark (centred), the cap at the right end (right-aligned), top 22
            TextStyle ls = MetaStyle.Text(GaugeLabelCss);
            string minText = _deckMin.ToString(CultureInfo.InvariantCulture), capText = _deckCap.ToString(CultureInfo.InvariantCulture);
            Label.Make(gaugeWrap, minText, ls, gw * minPct / 100f - TextMeasure.Width(minText, ls) / 2f, 22f, null, null, "min");
            Label.Make(gaugeWrap, capText, ls, gw - TextMeasure.Width(capText, ls), 22f, null, null, "max");

            _statusEl = new StatusLine(box, cx, cy + rowH + 12f, cw);
            // js: departure.js:342 — rollNumber(clock, totalEl, deck.Total) on the scene clock (util.js: reduced motion
            // cuts its tween to 1 ms like every other tween of the scene), shown with Math.round.
            _total = MetaUtil.RollNumber(_clock, _totalEl, _deck.Total);
        }

        /// <summary>A convex rounded rectangle (x, y, w, h, radius r) as a polygon in box px (y down), clockwise from the top-left.</summary>
        internal static List<Vec2> Stadium(float x, float y, float w, float h, float r)
        {
            r = Math.Max(0f, Math.Min(r, Math.Min(w, h) / 2f));
            var pts = new List<Vec2>();
            const int Seg = 8;
            void Arc(float cx, float cy, float from, float to)
            {
                for (int i = 0; i <= Seg; i++)
                {
                    double a = (from + (to - from) * i / Seg) * Math.PI / 180.0;
                    pts.Add(new Vec2(cx + r * (float)Math.Cos(a), cy + r * (float)Math.Sin(a)));
                }
            }
            Arc(x + w - r, y + r, -90f, 0f);
            Arc(x + w - r, y + h - r, 0f, 90f);
            Arc(x + r, y + h - r, 90f, 180f);
            Arc(x + r, y + r, 180f, 270f);
            return pts;
        }

        // ---- the deck as listed (DeckBuilder.DeckLines), one chip a kind ----
        // Three whole rows show; a longer deck scrolls by whole rows (wheel, or Shift+↑↓), fades at the edge it continues
        // past and says so in its heading, like 継承の間's lists. A chip picks its card and turns the grid to it (so its −
        // is at hand); the picked card's chip is marked and kept in view, so the cursor keys reach the list too.

        private sealed class Chip
        {
            public DepButton Button;
            public BattleCore.CardDef Def;
            public float Top;
        }

        private void BuildDeckList()
        {
            _listBox = Ui.Div(_deckPage, "ocbg-meta-decklist", 1236f, 776f, 636f, 128f);
            UiNode.Of(_listBox);
            BoxGraphic.On(_listBox, DeckListCss);
            // the content box: border 1 + padding 10 / 16 → (17, 11), 602 wide; header 16, gap 6, body 84
            TextStyle h4 = MetaStyle.Text(DeckListH4Css);
            TextStyle kinds = MetaStyle.Text(DeckListKindsCss);
            TextStyle more = MetaStyle.Text(DeckListMoreCss);
            float b = Math.Max(TextMath.CssBaselineFromTop(h4.Spec), Math.Max(TextMath.CssBaselineFromTop(kinds.Spec), TextMath.CssBaselineFromTop(more.Spec)));
            float h4W = TextMeasure.Width(MetaText.DeckListHeading, h4);
            Label.Make(_listBox, MetaText.DeckListHeading, h4, 17f, 11f + b - TextMath.CssBaselineFromTop(h4.Spec), null, null, "h4");
            _listKinds = Label.Make(_listBox, "", kinds, 17f + h4W + 12f, 11f + b - TextMath.CssBaselineFromTop(kinds.Spec), null, null, "ocbg-meta-decklist-kinds");
            _listMore = Label.Make(_listBox, "", more, 17f, 11f + b - TextMath.CssBaselineFromTop(more.Spec), null, null, "ocbg-meta-decklist-more");
            TextStyle mark = MetaStyle.Text(DeckListMoreCss + DeckListMoreBeforeCss);
            _listMoreMark = Label.Make(_listBox, "▼", mark, 17f, 11f + b - TextMath.CssBaselineFromTop(mark.Spec) - 1f, null, null, "before");
            _listMoreMark.gameObject.SetActive(false);

            _listBody = ScrollArea.Make(_listBox, "ocbg-meta-decklist-body", 17f, 33f, 602f, 84f);
            _listBody.ShowThumb = false;   // scrollbar-width: none
            _listBody.Content.sizeDelta = new Vector2(602f, 0f);
            _listBody.SetFades(DeckListMoreBelowMask, DeckListMoreAboveMask, DeckListMoreBothMask);
            // scroll-snap-type: y mandatory, the chips snapping at their row's top
            _listBody.SnapPoints = () =>
            {
                var snaps = new List<float>();
                foreach (Chip c in _chips) if (snaps.Count == 0 || snaps[snaps.Count - 1] != c.Top) snaps.Add(c.Top);
                snaps.Add(_listBody.MaxScroll);
                return snaps;
            };
            _listBody.Scrolled += ListEdge;
        }

        // js: scenes/meta/departure.js:356
        private void ListEdge()
        {
            if (_listBody == null || !_listBody) return;
            float room = _listBody.ScrollHeight - _listBody.ClientHeight;
            bool more = _listBody.ClientHeight > 0f && room > 2f;
            bool below = more && _listBody.ScrollTop < room - 2f;
            // is-more-below / is-more-above: the ScrollArea's fades follow the same tests
            _listBody.Refresh();
            bool up = more && !below;
            string text = more ? MetaText.DeckListMore : "";
            Label.SetText(_listMore, text);
            bool shown = text.Length > 0;
            _listMoreMark.gameObject.SetActive(shown);
            if (shown)
            {
                Label.SetText(_listMoreMark, up ? "▲" : "▼");
                // margin-left: auto: the cue at the header's right end, the mark 6 px before its words
                float tw = TextMeasure.Width(text, Label.StyleOf(_listMore));
                float mw = TextMeasure.Width(up ? "▲" : "▼", Label.StyleOf(_listMoreMark));
                float right = 17f + 602f;
                Ui.Place(_listMore.rectTransform, right - tw, Ui.PosOf(_listMore.rectTransform).y);
                Ui.Place(_listMoreMark.rectTransform, right - tw - 6f - mw, Ui.PosOf(_listMoreMark.rectTransform).y);
            }
        }

        // js: scenes/meta/departure.js:366
        /// <summary>listRow(): one row of chips (a chip's height and the row gap, as meta.css lays them out).</summary>
        private float ListRow() => _chips.Count > 0 ? _chips[0].Button.Node.H + 6f : 30f;

        // js: scenes/meta/departure.js:370
        private void ScrollList(int rows)
        {
            float before = _listBody.ScrollTop;
            _listBody.ScrollTop = Math.Max(0f, Math.Min(_listBody.ScrollHeight - _listBody.ClientHeight, before + rows * ListRow()));
            if (_listBody.ScrollTop != before) _sound.Play("nodeHover");
            ListEdge();
        }

        // js: scenes/meta/departure.js:376
        private void DrawList()
        {
            List<string> lines = _deck.DeckLines();
            // DeckLines is the deck's kinds in canon order, as Build lays them out (copies side by side).
            var kinds = new List<BattleCore.CardDef>();
            foreach (BattleCore.CardInstance card in _deck.Build())
                if (kinds.Count == 0 || !ReferenceEquals(kinds[kinds.Count - 1], card.Def)) kinds.Add(card.Def);
            float top = _listBody.ScrollTop;
            Ui.Clear(_listBody.Content);
            _chips.Clear();
            // flex-wrap: wrap, gap 6 (rows) 8 (chips), 602 wide
            TextStyle ts = MetaStyle.Text(DeckChipCss);
            float x = 0f, y = 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                BattleCore.CardDef def = i < kinds.Count ? kinds[i] : null;
                float w = TextMeasure.Width(lines[i], ts) + 16f;
                if (x > 0f && x + w > 602f) { x = 0f; y += 24f + 6f; }
                var chip = new Chip { Def = def, Top = y };
                chip.Button = new DepButton(_listBody.Content, "ocbg-meta-deckchip", DeckChipCss, focusable: false)   // tabindex -1
                    .OnHover(DeckChipHoverCss)
                    .OnClass("is-sel", DeckChipSelCss);
                chip.Button.StyledAtOnce = false;   // listBody.scrollTop = top below lays the new chips out before markChip
                chip.Button.Done();
                chip.Button.Node.Place(x, y, w, 24f);
                Label.Make(chip.Button.Root, lines[i], ts, 8f, 2f, null, null, "text");
                chip.Button.OnClick = () => { if (def != null && !_busy) Reveal(def); };
                _chips.Add(chip);
                x += w + 8f;
            }
            float contentH = _chips.Count > 0 ? y + 24f : 0f;
            _listBody.Content.sizeDelta = new Vector2(602f, contentH);
            _listBody.ScrollHeight = contentH;
            Label.SetText(_listKinds, lines.Count > 0 ? MetaText.OwnedCount(lines.Count) : "");
            _listBody.ScrollTop = top;
            _listBody.Refresh();
        }

        // js: scenes/meta/departure.js:393
        /// <summary>markChip(): marks the picked card's chip and scrolls it into view; then the edges and the cue follow the list.</summary>
        private void MarkChip()
        {
            Chip on = null;
            foreach (Chip chip in _chips)
            {
                bool isSel = _selected != null && chip.Def != null && chip.Def.Id == _selected.Id;
                chip.Button.Toggle("is-sel", isSel);
                if (isSel) on = chip;
            }
            if (on != null && _listBody.ClientHeight > 0f)
            {
                float top = on.Top;
                float bottom = top + on.Button.Node.H;
                if (top < _listBody.ScrollTop) _listBody.ScrollTop = top;
                else if (bottom > _listBody.ScrollTop + _listBody.ClientHeight) _listBody.ScrollTop = bottom - _listBody.ClientHeight;
            }
            ListEdge();
        }

        // js: scenes/meta/departure.js:409
        /// <summary>reveal(def): a chip pressed — its card is picked and the grid turns to the page that holds it.</summary>
        private void Reveal(BattleCore.CardDef def)
        {
            List<BattleCore.CardDef> kept = _deck.Filter(_filter);
            int at = kept.FindIndex(d => d.Id == def.Id);
            if (at < 0)
            {
                // The filter hides it: back to 全て / 全コスト, as the deck list shows every kind.
                _filter.Attribute = BattleCore.BattleAttribute.None;
                _filter.Cost = 0;
                DrawFilters();
                kept = _deck.Filter(_filter);
                at = kept.FindIndex(d => d.Id == def.Id);
                _page = -1;
            }
            int target = Math.Max(0, (int)Math.Floor(at / (double)PerPage));
            if (target != _page)
            {
                int dir = _page < 0 ? 0 : target > _page ? 1 : -1;
                _page = target;
                DrawGrid(dir);
            }
            if (_selected != null && _selected.Id == def.Id)
            {
                _sound.Play("uiClick");
                RefreshCells();
                MarkChip();
                return;
            }
            Choose(def);
        }

        // ---- presets ----

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
                if (_selected == null || _selected.Id != def.Id) { _selected = def; DrawDetail(); }
                RefreshDeck();
                global::Trial.Kit.Styler s = global::Trial.Kit.Styler.Of(cell.CountNode, s: 1.5f);
                _clock.Tween(s, "s", 1f, 240f, Ease.Out);
            }
            else
            {
                _sound.Play("refusal");
                Shake(cell.Plus);
            }
        }

        // js: scenes/meta/departure.js:476
        private void Remove(BattleCore.CardDef def, Cell cell)
        {
            if (_deck.Remove(def.Id))
            {
                _sound.Play("uiCancel");
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
            global::Trial.Kit.Styler s = el.Styler();
            float[] seq = { 6f, -6f, 4f, -4f, 0f };
            Promise p = Promise.Resolved();
            foreach (float x in seq)
            {
                float to = x;
                p = p.Then(() => _clock.Tween(s, "x", to, 32f, Ease.Linear));
            }
            p.Then(() => { if (el.Root) el.ClearTransform(); });
        }

        // js: scenes/meta/departure.js:489
        private void RefreshDeck()
        {
            RefreshCells();
            _total.To(_deck.Total).Forget();
            float pct = Math.Min(100f, 100f * _deck.Total / _deckCap);
            bool ok = _deck.IsValid;
            _gaugeFillStates.SetState(".is-ok", ok, false);
            _gaugeFillStates.Base(GaugeFillCss + "width: " + (_gaugePadW * pct / 100f).ToString("0.###", CultureInfo.InvariantCulture) + "px;");
            _gaugeFillStates.Apply();
            _statusEl.Set(_deck.StatusText, ok);
            DrawList();
            MarkChip();
            if (_noticeOverCap && _deck.Total <= _deckCap) SetNotice("");
            _toSupplies.Disabled = !ok;
            KeepDraft();
        }

        // js: scenes/meta/departure.js:502
        private void SetNotice(string text)
        {
            _notice = text ?? "";
            _noticeOverCap = false;
            Label.SetText(_noticeEl, _notice);
            _deckKeys.gameObject.SetActive(_notice.Length == 0);
        }

        // ================================================================================ steps (departure.js:661-735)

        // js: scenes/meta/departure.js:661
        internal async Promise ShowSupplies()
        {
            if (_busy || !_deck.IsValid || _step != "deck") return;
            _busy = true;
            _sound.Play("uiConfirm");
            _step = "supplies";
            _supView.TalentPage = 0;
            _supView.RefreshSupplies();
            _stepDeck.SetOn(false);
            _stepSup.SetOn(true);
            Label.SetText(_backText, DepartureBuilder.BackLabel);
            LayoutBack();
            global::Trial.Kit.Styler a = global::Trial.Kit.Styler.Of(_deckPage), b = global::Trial.Kit.Styler.Of(_supPage, o: 0f, x: 80f);
            UiNode.Of(_supPage).Visible = true;
            SyncFocus();
            _clock.Tween(a, new[] { ("o", 0f), ("x", -80f) }, 260f, Ease.In).Forget();
            await _clock.Tween(b, new[] { ("o", 1f), ("x", 0f) }, 340f, Ease.Out, 140f);
            if (!_alive) return;
            UiNode.Of(_deckPage).Visible = false;
            UiNode.Of(_supPage).ResetTransform();
            _busy = false;
        }

        // js: scenes/meta/departure.js:678
        internal async Promise ShowDeck()
        {
            if (_busy || _step != "supplies") return;
            _busy = true;
            _sound.Play("uiCancel");
            _step = "deck";
            RefreshDeck();
            DrawGrid(0);
            _stepSup.SetOn(false);
            _stepDeck.SetOn(true);
            Label.SetText(_backText, InheritanceText.ToHallButton);
            LayoutBack();
            global::Trial.Kit.Styler a = global::Trial.Kit.Styler.Of(_supPage), b = global::Trial.Kit.Styler.Of(_deckPage, o: 0f, x: -80f);
            UiNode.Of(_deckPage).Visible = true;
            // The browser drops a display: none box's scroll position: the list comes back at its top, and markChip then
            // scrolls the picked card's chip into view.
            _listBody.ScrollTop = 0f;
            MarkChip();   // the list is measured now that the page shows again
            _clock.Tween(a, new[] { ("o", 0f), ("x", 80f) }, 260f, Ease.In).Forget();
            await _clock.Tween(b, new[] { ("o", 1f), ("x", 0f) }, 340f, Ease.Out, 140f);
            if (!_alive) return;
            UiNode.Of(_supPage).Visible = false;
            UiNode.Of(_deckPage).ResetTransform();
            _busy = false;
        }

        // js: scenes/meta/departure.js:696
        private void Back()
        {
            if (_step == "supplies") { ShowDeck().Forget(); return; }
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

        // js: scenes/meta/departure.js:779
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
            if ((e.Key == "j" || e.Key == "J") && _ctx.OnJournal != null) { if (!e.Repeat) _ctx.OnJournal(); return true; }
            if (e.Key == "Enter" && KeyboardFocus()) return false;
            if (_step == "deck")
            {
                if (e.ShiftKey && (e.Key == "ArrowUp" || e.Key == "ArrowDown")) { ScrollList(e.Key == "ArrowDown" ? 1 : -1); return true; }
                if (e.Key == "PageUp") { Turn(-1); return true; }
                if (e.Key == "PageDown") { Turn(1); return true; }
                if (e.Key == "ArrowLeft") { MoveCursor(-1); return true; }
                if (e.Key == "ArrowRight") { MoveCursor(1); return true; }
                if (e.Key == "ArrowUp") { MoveCursor(-Columns, true); return true; }
                if (e.Key == "ArrowDown") { MoveCursor(Columns, true); return true; }
                if (e.Key == "+" || e.Key == "=" || e.Key == ";") { Cell c = CursorCell(); if (c != null) Add(c.Def, c); return true; }
                if (e.Key == "-" || e.Key == "_" || e.Key == "Backspace" || e.Key == "Delete") { Cell c = CursorCell(); if (c != null) Remove(c.Def, c); return true; }
                if (e.Key == "Enter") { if (_deck.IsValid) ShowSupplies().Forget(); else _sound.Play("refusal"); return true; }
            }
            else
            {
                if (e.Key == "ArrowLeft" || e.Key == "PageUp") { _supView.TurnTalent(-1); return true; }
                if (e.Key == "ArrowRight" || e.Key == "PageDown") { _supView.TurnTalent(1); return true; }
                if (e.Key == "Enter") { if (_supplies.CanSetOut(_deck)) SetOut(); else _sound.Play("refusal"); return true; }
            }
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
        /// The Tab order as the DOM holds it: the deck step (filters, each cell's face − ＋, the page turns, the presets,
        /// 出立の支度へ; the deck list's chips are tabindex -1), the supplies step, then ←, 設定 and 手記. A hidden step, a
        /// disabled button and a removed one are skipped (Focusable.IsFocusable); buttons drawn again replace the old.
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
                Push(FocusOf(c.Minus));
                Push(FocusOf(c.Plus));
            }
            Push(FocusOf(_prevBtn));
            Push(FocusOf(_nextBtn));
            foreach (MetaButton b in _presets) Push(FocusOf(b));
            Push(FocusOf(_toSupplies));
            if (_supView != null)
            {
                foreach (object o in _supView.FocusOrder())
                {
                    if (o is MetaButton mb) Push(FocusOf(mb));
                    else if (o is DepButton db) Push(db.Focusable);
                }
            }
            if (_backBtn != null) Push(_backBtn.Focusable);
            Push(FocusOf(_settingsBtn));
            Push(FocusOf(_journalBtn));

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

        /// <summary>A label from declarations at (x, y) (w / h null: sized to the text); p: a p / li / h3 (wraps by phrase).</summary>
        internal static TextMeshProUGUI Text(RectTransform parent, string text, string css, float x, float y, float? w = null, float? h = null,
            string name = null, bool p = false)
        {
            TextStyle s = p ? MetaStyle.P(css) : MetaStyle.Text(css);
            return Label.Make(parent, text ?? "", s, x, y, w, h, name);
        }

        /// <summary>The stepper's step (span: the number in its circle, then the words; .is-on lights it).</summary>
        private sealed class StepSpan
        {
            private readonly CssStates _span, _b;

            public StepSpan(RectTransform stepper, string n, string words, bool on)
            {
                RectTransform span = Ui.Div(stepper, "span");
                UiNode.Of(span);
                RectTransform b = Ui.Div(span, "b", 0f, 0f, 32f, 32f);
                UiNode.Of(b);
                BoxGraphic.On(b);
                Label.Make(b, n, MetaStyle.Text(StepperSpanCss + StepperBCss), "n");
                Ocb.Flow(b, StepperBCss, null, 32f, 32f);
                Ocb.Fixed(b);
                Label.Make(span, words, MetaStyle.Text(StepperSpanCss), "text");
                Ocb.Flow(span, StepperSpanCss);
                _span = CssStates.Of(span).Base(StepperSpanCss).State(".is-on", StepperSpanOnCss, on);
                _span.Apply(true);
                _b = CssStates.Of(b).Base(MetaStyle.Without(StepperBCss, "width", "height")).State(".is-on", StepperBOnCss, on);
                _b.Apply(true);
            }

            public void SetOn(bool on)
            {
                _span.SetState(".is-on", on);
                _b.SetState(".is-on", on);
            }
        }

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
        /// A button of the departure's own classes (.ocbg-meta-back, -filter, -cell-face, -deckchip, -slot, -talent): its box,
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
            /// it: drawList sets listBody.scrollTop after making the chips (the setter lays the list out), so markChip's
            /// is-sel then transitions in (box-shadow and background, .14s) on every redraw.
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
