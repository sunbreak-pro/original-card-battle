// The 出立 step after the deck (#58): the three tool slots, the three consumable slots, the start a
// tool moves back, the other battle tools' amounts (#452), and the talent card. Pure C#: BattleCore
// only, no UnityEngine, so every rule the screen shows is held under `dotnet test`. The rules
// themselves are BattleCore.DeckRules'; this class keeps what the player picked and prints it.
//
// The tools and consumables are the dungeon side's (tools_and_consumables_v4.md, read only here).
// This assembly does not reference DungeonContent: the View hands them in as SupplyOptions
// (DepartureSupplies), carrying only what the battle reads: how many cells a tool moves the start,
// and the amounts of 息継ぎの笛 / 竜鱗の欠片 / 竜語の符 (battle_core_v4 §9.1).
//
// Trial extension (port of the browser trial's DepartureBuilder.js, docs/EXTENSIONS.md 「出立の枠（DeckLimits）」
// L-7): `new DepartureBuilder(tools, consumables, limits)` holds the slots to a DeckLimits (`Limits`):
// CanAddTool / CanAddConsumable, Validate / CanSetOut, ToolsLine / ConsumablesLine and the instance
// LifeTitle / LifeToolHeading / LifeConsumableHeading (static TitleFor / ToolHeadingFor / ConsumableHeadingFor)
// read it. Left out (null) it is DeckLimits.Default (3 and 3) and the class is what it was; the static Title /
// ToolHeading / ConsumableHeading stay as they were (the JS's instance getters of those names are Life* here,
// since C# cannot hold a static and an instance member of one name). concept-v3.md §14 生存者ボーナス: a
// survivor's heir has 4 tool slots.
using System;
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    /// <summary>One tool or consumable as the departure screen offers it.</summary>
    public sealed class SupplyOption
    {
        public readonly string Id;
        public readonly string Name;

        /// <summary>The catalogue's one line about it (ItemDef.Note), printed as it is.</summary>
        public readonly string Note;

        /// <summary>Cells the player's start moves back while it is carried (間合いの履: 1). 0 for the rest.</summary>
        public readonly int StartCellsBack;

        /// <summary>Stamina added at the start of each battle (息継ぎの笛: 3). 0 for the rest.</summary>
        public readonly int StartStaminaBonus;

        /// <summary>Guard given once a battle, before the first enemy blow that reaches the player (竜鱗の欠片: 5). 0 for the rest.</summary>
        public readonly int FirstHitGuard;

        /// <summary>Stacks lower the boss-only words sit on the player (竜語の符: 1). 0 for the rest.</summary>
        public readonly int BossWordWard;

        public SupplyOption(string id, string name, string note, int startCellsBack = 0,
            int startStaminaBonus = 0, int firstHitGuard = 0, int bossWordWard = 0)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A supply needs an id.", nameof(id));
            Id = id;
            Name = name ?? id;
            Note = note ?? "";
            StartCellsBack = Math.Max(0, startCellsBack);
            StartStaminaBonus = Math.Max(0, startStaminaBonus);
            FirstHitGuard = Math.Max(0, firstHitGuard);
            BossWordWard = Math.Max(0, bossWordWard);
        }

        /// <summary>Whether the battles of this demo feel it: the four battle tools since #452, none of the exploration ones.</summary>
        public bool WorksInBattle => StartCellsBack > 0 || StartStaminaBonus > 0 || FirstHitGuard > 0 || BossWordWard > 0;
    }

    public sealed class DepartureBuilder
    {
        private readonly List<string> _tools = new List<string>();
        private readonly List<string> _consumables = new List<string>();

        /// <param name="limits">Trial extension: the DeckLimits the slots are held to; null is the constants' 3 and 3.</param>
        // js: bridge/depiction/DepartureBuilder.js:64
        public DepartureBuilder(IReadOnlyList<SupplyOption> tools, IReadOnlyList<SupplyOption> consumables, DeckLimits limits = null)
        {
            ToolOptions = tools ?? throw new ArgumentNullException(nameof(tools));
            ConsumableOptions = consumables ?? throw new ArgumentNullException(nameof(consumables));
            Limits = DeckLimits.From(limits);
        }

        public IReadOnlyList<SupplyOption> ToolOptions { get; }

        public IReadOnlyList<SupplyOption> ConsumableOptions { get; }

        /// <summary>Trial extension: the slot counts (DeckLimits.Default is 3 tools and 3 consumables).</summary>
        public DeckLimits Limits { get; }

        /// <summary>The ids in the tool slots, in the order they went in. The same tool may fill two.</summary>
        public IReadOnlyList<string> Tools => _tools;

        public IReadOnlyList<string> Consumables => _consumables;

        /// <summary>The card picked as the talent, or null. Kept while the deck changes; <see cref="TalentIn"/> drops it when its kind leaves.</summary>
        public string Talent { get; private set; }

        // ---- the slots ----

        public int ToolCount(string id) => Count(_tools, id);

        public int ConsumableCount(string id) => Count(_consumables, id);

        /// <summary>(Trial: the slot count is Limits.ToolSlots — 4 with the survivor's bonus, concept-v3.md §14.)</summary>
        // js: bridge/depiction/DepartureBuilder.js:89
        public bool CanAddTool(string id) => _tools.Count < Limits.ToolSlots && Find(ToolOptions, id) != null;

        // js: bridge/depiction/DepartureBuilder.js:91
        public bool CanAddConsumable(string id) => _consumables.Count < Limits.ConsumableSlots && Find(ConsumableOptions, id) != null;

        public bool AddTool(string id)
        {
            if (!CanAddTool(id)) return false;
            _tools.Add(id);
            return true;
        }

        public bool RemoveTool(string id) => _tools.Remove(id);

        public bool AddConsumable(string id)
        {
            if (!CanAddConsumable(id)) return false;
            _consumables.Add(id);
            return true;
        }

        public bool RemoveConsumable(string id) => _consumables.Remove(id);

        // ---- the start ----

        /// <summary>The cells the carried tools move the start back, before DeckRules keeps it on the line.</summary>
        public int StartCellsBack
        {
            get
            {
                int back = 0;
                foreach (string id in _tools) back += Find(ToolOptions, id).StartCellsBack;
                return back;
            }
        }

        public int PlayerStartCell => DeckRules.PlayerStartCell(StartCellsBack);

        public int StartGap => DeckRules.StartGap(StartCellsBack);

        // ---- the other battle tools (#452) ----

        /// <summary>The stamina the carried tools add at each battle's start (息継ぎの笛 +3 each).</summary>
        public int StartStaminaBonus => Sum(o => o.StartStaminaBonus);

        /// <summary>The Guard the carried tools give before the first enemy blow of each battle (竜鱗の欠片 5 each).</summary>
        public int FirstHitGuard => Sum(o => o.FirstHitGuard);

        /// <summary>How many stacks lower the boss-only words sit (竜語の符 1 each).</summary>
        public int BossWordWard => Sum(o => o.BossWordWard);

        private int Sum(Func<SupplyOption, int> amount)
        {
            int total = 0;
            foreach (string id in _tools) total += amount(Find(ToolOptions, id));
            return total;
        }

        /// <summary>「開始のマス 2・開始の間合い 3」, and the tool that moved it: 「開始のマス 1・開始の間合い 4（間合いの履）」.</summary>
        public string StartLine
        {
            get
            {
                string line = "開始のマス " + PlayerStartCell + "・開始の間合い " + StartGap;
                var movers = new List<string>();
                foreach (string id in _tools)
                {
                    SupplyOption option = Find(ToolOptions, id);
                    if (option.StartCellsBack > 0 && !movers.Contains(option.Name)) movers.Add(option.Name);
                }
                return movers.Count == 0 ? line : line + "（" + string.Join("、", movers) + "）";
            }
        }

        // ---- the talent ----

        /// <summary>Picks the talent. Only a kind the deck holds is taken; anything else is refused.</summary>
        public bool ChooseTalent(DeckBuilder deck, string id)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (string.IsNullOrEmpty(id) || deck.CountOf(id) == 0) return false;
            Talent = id;
            return true;
        }

        /// <summary>The talent if its kind is still in <paramref name="deck"/>, else null.</summary>
        public string TalentIn(DeckBuilder deck)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            return Talent != null && deck.CountOf(Talent) > 0 ? Talent : null;
        }

        /// <summary>The deck's kinds in canon order: what the talent may be.</summary>
        public static List<CardDef> TalentChoices(DeckBuilder deck)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            var kinds = new List<CardDef>();
            foreach (CardDef def in CardCatalog.All)
            {
                if (deck.CountOf(def.Id) > 0) kinds.Add(def);
            }
            return kinds;
        }

        /// <summary>One page of <see cref="TalentChoices"/>. The page is clamped.</summary>
        public static DeckPage TalentPage(DeckBuilder deck, int page, int perPage)
        {
            if (perPage < 1) throw new ArgumentOutOfRangeException(nameof(perPage), perPage, "1 or more.");
            List<CardDef> kinds = TalentChoices(deck);
            int pages = Math.Max(1, (kinds.Count + perPage - 1) / perPage);
            page = Math.Max(0, Math.Min(pages - 1, page));
            int first = page * perPage;
            return new DeckPage(kinds.GetRange(first, Math.Min(perPage, kinds.Count - first)), page, pages, kinds.Count);
        }

        // ---- setting out ----

        /// <summary>What sets out with <paramref name="deck"/>: its cards, the slots, the start and the talent.</summary>
        public Departure Build(DeckBuilder deck)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            return new Departure(deck.Build(), _tools.ToArray(), _consumables.ToArray(), StartCellsBack, TalentIn(deck),
                StartStaminaBonus, FirstHitGuard, BossWordWard);
        }

        /// <summary>
        /// (Trial: DeckRules.Validate with limits made of the deck builder's bounds — the same ones its IsValid
        /// reads, so StatusText and CanSetOut agree — and this builder's slots. Both default to the constants.)
        /// </summary>
        // js: bridge/depiction/DepartureBuilder.js:200
        public DeckValidation Validate(DeckBuilder deck)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            return DeckRules.Validate(Build(deck), deck.Owned, LimitsWith(deck));
        }

        /// <summary>Trial extension: the deck's DeckMin / DeckMax with this builder's slots.</summary>
        // js: bridge/depiction/DepartureBuilder.js:206
        private DeckLimits LimitsWith(DeckBuilder deck)
        {
            // The JS falls back to this builder's limits when the deck carries none; a C# DeckBuilder always does.
            DeckLimits own = deck.Limits ?? Limits;
            if (ReferenceEquals(own, Limits)) return Limits;
            return Limits.With(deckMin: own.DeckMin, deckMax: own.DeckMax);
        }

        public bool CanSetOut(DeckBuilder deck) => Validate(deck).Ok;

        // ---- what the screen prints ----

        // js: bridge/depiction/DepartureBuilder.js:216
        public static string Title => TitleFor(DeckLimits.Default);

        // js: bridge/depiction/DepartureBuilder.js:218
        public static string ToolHeading => ToolHeadingFor(DeckLimits.Default);

        // js: bridge/depiction/DepartureBuilder.js:220
        public static string ConsumableHeading => ConsumableHeadingFor(DeckLimits.Default);

        /// <summary>Trial extension: 「出立の支度（ツール 4 枠・消耗品 3 枠・才能 1 枚）」 for some limits.</summary>
        // js: bridge/depiction/DepartureBuilder.js:223
        public static string TitleFor(DeckLimits limits)
        {
            DeckLimits l = DeckLimits.From(limits);
            return "出立の支度（ツール " + l.ToolSlots + " 枠・消耗品 " + l.ConsumableSlots + " 枠・才能 1 枚）";
        }

        /// <summary>Trial extension: 「ツール（探索と戦闘で共通の 4 枠。同じものを 2 つ持てます）」 for some limits.</summary>
        // js: bridge/depiction/DepartureBuilder.js:229
        public static string ToolHeadingFor(DeckLimits limits) =>
            "ツール（探索と戦闘で共通の " + DeckLimits.From(limits).ToolSlots + " 枠。同じものを 2 つ持てます）";

        /// <summary>Trial extension: 「消耗品（3 枠。拠点へは持ち帰りません）」 for some limits.</summary>
        // js: bridge/depiction/DepartureBuilder.js:234
        public static string ConsumableHeadingFor(DeckLimits limits) =>
            "消耗品（" + DeckLimits.From(limits).ConsumableSlots + " 枠。拠点へは持ち帰りません）";

        /// <summary>Trial extension: this builder's title (TitleFor(Limits)); the JS's instance `Title`.</summary>
        // js: bridge/depiction/DepartureBuilder.js:239
        public string LifeTitle => TitleFor(Limits);

        /// <summary>Trial extension: this builder's tool heading; the JS's instance `ToolHeading`.</summary>
        // js: bridge/depiction/DepartureBuilder.js:242
        public string LifeToolHeading => ToolHeadingFor(Limits);

        /// <summary>Trial extension: this builder's consumable heading; the JS's instance `ConsumableHeading`.</summary>
        // js: bridge/depiction/DepartureBuilder.js:245
        public string LifeConsumableHeading => ConsumableHeadingFor(Limits);

        /// <summary>
        /// Trial extension: the label over what the limits raise (concept-v3.md §14 生存者ボーナス; the hall's
        /// InheritanceText says the same word). Showing it at 出立 is 試作で決めたこと 6 in docs/EXTENSIONS.md
        /// 「出立の枠」: the documents show the bonus in 継承の間 (CAMP_FACILITIES_DESIGN.md §3.1).
        /// </summary>
        // js: bridge/depiction/DepartureBuilder.js:252
        public static string BonusLabel => "生存者ボーナス";

        /// <summary>
        /// Trial extension: the tool slots the limits add over the constants' 3, in the hall's words
        /// (InheritanceText.SurvivorTools): 「ツール枠 +1（3 → 4）」. "" when the slots are the constants'.
        /// </summary>
        // js: bridge/depiction/DepartureBuilder.js:258
        public string RaisedLine
        {
            get
            {
                int from = DeckLimits.Default.ToolSlots;
                int to = Limits.ToolSlots;
                return to > from ? "ツール枠 +" + (to - from) + "（" + from + " → " + to + "）" : "";
            }
        }

        /// <summary>Trial extension: whether tool slot <paramref name="index"/> (0-based) is one the limits add (the survivor's 4th).</summary>
        // js: bridge/depiction/DepartureBuilder.js:265
        public bool IsBonusToolSlot(int index) => index >= DeckLimits.Default.ToolSlots && index < Limits.ToolSlots;

        public static string TalentHeading => "才能（デッキの札から 1 枚。その札だけ習熟が 2 倍で溜まります）";

        public static string SetOutLabel => "出立する";

        public static string BackLabel => "デッキに戻る";

        /// <summary>「ツール 2 / 3: 間合いの履、防瘴の面」, or 「ツール 0 / 3: なし」.</summary>
        // js: bridge/depiction/DepartureBuilder.js:276
        public string ToolsLine => SlotsLine("ツール", _tools, ToolOptions, Limits.ToolSlots);

        // js: bridge/depiction/DepartureBuilder.js:278
        public string ConsumablesLine => SlotsLine("消耗品", _consumables, ConsumableOptions, Limits.ConsumableSlots);

        /// <summary>The line an option has on the screen: its name and note, and a word when this demo's battles do not feel it.</summary>
        public static string LineOf(SupplyOption option)
        {
            if (option == null) throw new ArgumentNullException(nameof(option));
            return option.Name + "　" + option.Note + (option.WorksInBattle ? "" : "（この試験の戦闘では効果が出ません）");
        }

        /// <summary>「才能: 突き」, or 「才能: まだ選んでいません」.</summary>
        public string TalentLine(DeckBuilder deck)
        {
            string id = TalentIn(deck);
            return "才能: " + (id == null ? "まだ選んでいません" : CardCatalog.ById(id).Name);
        }

        /// <summary>The one line beside 「出立する」: what is still missing, or that it may set out.</summary>
        public string StatusText(DeckBuilder deck)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (!deck.IsValid) return "デッキが決まりに合っていません。デッキに戻って直してください";
            if (TalentIn(deck) == null) return "才能にする札を 1 枚選ぶと出立できます";
            return "出立できます";
        }

        // ---- helpers ----

        private static string SlotsLine(string label, List<string> ids, IReadOnlyList<SupplyOption> options, int slots)
        {
            var names = new List<string>();
            foreach (string id in ids) names.Add(Find(options, id).Name);
            return label + " " + ids.Count + " / " + slots + ": " + (names.Count == 0 ? "なし" : string.Join("、", names));
        }

        private static SupplyOption Find(IReadOnlyList<SupplyOption> options, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (SupplyOption option in options)
            {
                if (option.Id == id) return option;
            }
            return null;
        }

        private static int Count(List<string> ids, string id)
        {
            int n = 0;
            foreach (string each in ids)
            {
                if (each == id) n++;
            }
            return n;
        }
    }
}
