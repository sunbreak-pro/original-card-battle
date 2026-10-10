// The 出立 step after the deck (#58): the three tool slots, the three consumable slots, the start a
// tool moves back, and the talent card. Pure C#: BattleCore only, no UnityEngine, so every rule the
// screen shows is held under `dotnet test`. The rules themselves are BattleCore.DeckRules'; this
// class keeps what the player picked and prints it.
//
// The tools and consumables are the dungeon side's (tools_and_consumables_v4.md, read only here).
// This assembly does not reference DungeonContent: the View hands them in as SupplyOptions
// (DepartureSupplies), carrying only what the battle reads — how many cells a tool moves the start.
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

        public SupplyOption(string id, string name, string note, int startCellsBack = 0)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A supply needs an id.", nameof(id));
            Id = id;
            Name = name ?? id;
            Note = note ?? "";
            StartCellsBack = Math.Max(0, startCellsBack);
        }

        /// <summary>Whether the battles of this demo feel it. Only the start is wired so far (#58).</summary>
        public bool WorksInBattle => StartCellsBack > 0;
    }

    public sealed class DepartureBuilder
    {
        private readonly List<string> _tools = new List<string>();
        private readonly List<string> _consumables = new List<string>();

        public DepartureBuilder(IReadOnlyList<SupplyOption> tools, IReadOnlyList<SupplyOption> consumables)
        {
            ToolOptions = tools ?? throw new ArgumentNullException(nameof(tools));
            ConsumableOptions = consumables ?? throw new ArgumentNullException(nameof(consumables));
        }

        public IReadOnlyList<SupplyOption> ToolOptions { get; }

        public IReadOnlyList<SupplyOption> ConsumableOptions { get; }

        /// <summary>The ids in the tool slots, in the order they went in. The same tool may fill two.</summary>
        public IReadOnlyList<string> Tools => _tools;

        public IReadOnlyList<string> Consumables => _consumables;

        /// <summary>The card picked as the talent, or null. Kept while the deck changes; <see cref="TalentIn"/> drops it when its kind leaves.</summary>
        public string Talent { get; private set; }

        // ---- the slots ----

        public int ToolCount(string id) => Count(_tools, id);

        public int ConsumableCount(string id) => Count(_consumables, id);

        public bool CanAddTool(string id) => _tools.Count < DeckRules.ToolSlots && Find(ToolOptions, id) != null;

        public bool CanAddConsumable(string id) => _consumables.Count < DeckRules.ConsumableSlots && Find(ConsumableOptions, id) != null;

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
            return new Departure(deck.Build(), _tools.ToArray(), _consumables.ToArray(), StartCellsBack, TalentIn(deck));
        }

        public DeckValidation Validate(DeckBuilder deck)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            return DeckRules.Validate(Build(deck), deck.Owned);
        }

        public bool CanSetOut(DeckBuilder deck) => Validate(deck).Ok;

        // ---- what the screen prints ----

        public static string Title => "出立の支度（ツール " + DeckRules.ToolSlots + " 枠・消耗品 " + DeckRules.ConsumableSlots + " 枠・才能 1 枚）";

        public static string ToolHeading => "ツール（探索と戦闘で共通の " + DeckRules.ToolSlots + " 枠。同じものを 2 つ持てます）";

        public static string ConsumableHeading => "消耗品（" + DeckRules.ConsumableSlots + " 枠。拠点へは持ち帰りません）";

        public static string TalentHeading => "才能（デッキの札から 1 枚。その札だけ習熟が 2 倍で溜まります）";

        public static string SetOutLabel => "出立する";

        public static string BackLabel => "デッキに戻る";

        /// <summary>「ツール 2 / 3: 間合いの履、防瘴の面」, or 「ツール 0 / 3: なし」.</summary>
        public string ToolsLine => SlotsLine("ツール", _tools, ToolOptions, DeckRules.ToolSlots);

        public string ConsumablesLine => SlotsLine("消耗品", _consumables, ConsumableOptions, DeckRules.ConsumableSlots);

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
