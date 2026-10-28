using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCore
{
    /// <summary>
    /// What a carried item actually does. The exploration core only needs the shape; which items
    /// exist and what they are called is #100's job (DungeonContent.ItemCatalogue).
    /// </summary>
    public enum ReliefKind
    {
        /// <summary>Lowers the layer's density for as long as the life lasts (防瘴の面).</summary>
        Density,

        /// <summary>Pulls the gauge back down once, then the item is gone (浄化の香).</summary>
        Gauge,

        /// <summary>HP back by Amount percent of the maximum, never past it (薬草).</summary>
        HealHp,

        /// <summary>Max stamina up by Amount, inside the ±4 clamp the temporaries share (濃い干し肉).</summary>
        MaxStamina,

        /// <summary>Current stamina back to the maximum (忘れ水).</summary>
        RefillStamina,

        /// <summary>
        /// A jump to an unresolved node of the layer (標の石). It needs a node to jump to, which no
        /// screen picks yet: it rides in a slot but cannot be used. Trial extension: the trial's map
        /// picks the node, and ExplorationReducer.Teleport spends it there, paying the node's 刻限.
        /// </summary>
        Teleport,

        // Trial extension (docs/EXTENSIONS.md 「探索の持ち物とイベント」): the rest of the departure's tools, so the
        // exploration can carry every tool of tools_and_consumables_v4.md §2. Only a trial RunLoadout holds tools
        // of these kinds; the C# shape (Density tools, any consumable) is unchanged.

        /// <summary>刻限の砂: every layer's 刻限 + Amount (§2).</summary>
        Time,

        /// <summary>写しの帳: the player picks the 情報収集 target (§2.2).</summary>
        Survey,

        /// <summary>灯芯の欠片: 階層間の休憩 heals Amount more points of the maximum (§2).</summary>
        Interlude,

        /// <summary>間合いの履 / 息継ぎの笛 / 竜鱗の欠片 / 竜語の符: acts in battle only; carried for the slot count.</summary>
        Battle,
    }

    /// <summary>One carried item, reduced to its effect on the run. Name and art live elsewhere.</summary>
    public sealed record ReliefItem(string Id, ReliefKind Kind, int Amount)
    {
        public static ReliefItem Density(string id, int amount) => new ReliefItem(id, ReliefKind.Density, amount);

        public static ReliefItem Gauge(string id, int amount) => new ReliefItem(id, ReliefKind.Gauge, amount);

        public static ReliefItem HealHp(string id, int percent) => new ReliefItem(id, ReliefKind.HealHp, percent);

        public static ReliefItem MaxStamina(string id, int amount) => new ReliefItem(id, ReliefKind.MaxStamina, amount);

        public static ReliefItem RefillStamina(string id) => new ReliefItem(id, ReliefKind.RefillStamina, 0);

        public static ReliefItem Teleport(string id) => new ReliefItem(id, ReliefKind.Teleport, 0);
    }

    /// <summary>
    /// What the explorer set out with. Three tool slots and three consumable slots
    /// (concept-v3.md §14); the 生存者ボーナス raises the tool slots to four for one life (§8.2).
    ///
    /// Tools stay for the whole life. Consumables are spent where they are used and are not
    /// carried back to camp.
    /// </summary>
    public sealed class RunLoadout
    {
        public const int ToolSlots = 3;
        public const int ConsumableSlots = 3;
        public const int SurvivorBonusToolSlots = ToolSlots + 1;

        /// <summary>The tool kinds a trial RunLoadout may hold (ReliefKind's trial extension).</summary>
        private static readonly ReliefKind[] TrialToolKinds =
            { ReliefKind.Density, ReliefKind.Time, ReliefKind.Survey, ReliefKind.Interlude, ReliefKind.Battle };

        private readonly List<ReliefItem> _consumables;

        /// <param name="trial">
        /// Trial extension (js: core/dungeon/RunLoadout.js:63): a trial loadout may also carry the tools of kind
        /// Time / Survey / Interlude / Battle. False keeps the C# rule that every tool lowers density.
        /// </param>
        public RunLoadout(
            IReadOnlyList<ReliefItem>? tools = null,
            IReadOnlyList<ReliefItem>? consumables = null,
            int toolSlots = ToolSlots,
            bool trial = false)
        {
            var toolList = tools ?? Array.Empty<ReliefItem>();
            var consumableList = consumables ?? Array.Empty<ReliefItem>();

            if (toolSlots < 0) throw new ArgumentOutOfRangeException(nameof(toolSlots));
            if (toolList.Count > toolSlots)
                throw new ArgumentException($"{toolList.Count} tools do not fit in {toolSlots} slots", nameof(tools));
            if (consumableList.Count > ConsumableSlots)
                throw new ArgumentException(
                    $"{consumableList.Count} consumables do not fit in {ConsumableSlots} slots", nameof(consumables));
            if (!trial && toolList.Any(t => t.Kind != ReliefKind.Density))
                throw new ArgumentException("a tool thins the air for the whole life, so it lowers density", nameof(tools));
            if (trial && toolList.Any(t => Array.IndexOf(TrialToolKinds, t.Kind) < 0))
                throw new ArgumentException("a trial tool is one of the eight tools of tools_and_consumables_v4.md §2", nameof(tools));

            Tools = toolList;
            ToolSlotCount = toolSlots;
            _consumables = consumableList.ToList();
            Trial = trial;
        }

        public IReadOnlyList<ReliefItem> Tools { get; }

        public int ToolSlotCount { get; }

        public IReadOnlyList<ReliefItem> Consumables => _consumables;

        /// <summary>Trial extension: this loadout carries the trial items (see the constructor).</summary>
        public bool Trial { get; }

        /// <summary>
        /// How much every layer's density drops for this life. A C# loadout holds density tools only, so its
        /// sum is over every tool; a trial loadout counts its density tools.
        /// </summary>
        // js: core/dungeon/RunLoadout.js:92
        public int DensityRelief => Trial ? SumOf(ReliefKind.Density) : Tools.Sum(t => t.Amount);

        // ---- Trial extension: what the other exploration tools add (tools_and_consumables_v4.md §2).

        /// <summary>刻限の砂: 刻限 every layer gains. Copies stack, like Loadout.TimeLimitBonus. 0 outside trial.</summary>
        // js: core/dungeon/RunLoadout.js:99
        public int TimeBonus => Trial ? SumOf(ReliefKind.Time) : 0;

        /// <summary>灯芯の欠片: points of the HP maximum 階層間の休憩 heals on top of its 30%. 0 outside trial.</summary>
        // js: core/dungeon/RunLoadout.js:102
        public int InterludeBonus => Trial ? SumOf(ReliefKind.Interlude) : 0;

        /// <summary>写しの帳 is carried: the player picks the 情報収集 target (§2.2). False outside trial.</summary>
        // js: core/dungeon/RunLoadout.js:105
        public bool ChoosesSurvey => Trial && Tools.Any(t => t.Kind == ReliefKind.Survey);

        /// <summary>The tools of one kind summed. (Trial extension.)</summary>
        // js: core/dungeon/RunLoadout.js:108
        public int SumOf(ReliefKind kind) => Tools.Where(t => t.Kind == kind).Sum(t => t.Amount);

        /// <summary>Every consumable slot holds an item: picking one more up means swapping.</summary>
        public bool ConsumableSlotsFull => _consumables.Count >= ConsumableSlots;

        /// <summary>Spends one consumable and reports which item it was.</summary>
        public RunLoadout Spend(int index, out ReliefItem used)
        {
            if (index < 0 || index >= _consumables.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "no consumable in that slot");

            used = _consumables[index];

            var rest = _consumables.ToList();
            rest.RemoveAt(index);
            return new RunLoadout(Tools, rest, ToolSlotCount, Trial);
        }

        /// <summary>
        /// Picks a consumable up (tools_and_consumables_v4.md §3). With a free slot it goes in and
        /// <paramref name="replaceSlot"/> is not read; with none, it takes the place of the item in
        /// <paramref name="replaceSlot"/>. A swap that names no slot is refused: which item is given
        /// up is the player's to say. The same rule as DungeonContent.Loadout.Take. A trial loadout stays one.
        /// </summary>
        public RunLoadout Take(ReliefItem item, int replaceSlot = -1)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            var next = _consumables.ToList();
            if (!ConsumableSlotsFull)
            {
                next.Add(item);
                return new RunLoadout(Tools, next, ToolSlotCount, Trial);
            }

            if (replaceSlot < 0 || replaceSlot >= next.Count)
                throw new ExplorationRuleException("the consumable slots are full: name the slot to swap");

            next[replaceSlot] = item;
            return new RunLoadout(Tools, next, ToolSlotCount, Trial);
        }

        /// <summary>Nothing carried. The baseline a bare run starts from.</summary>
        public static RunLoadout Empty { get; } = new RunLoadout();
    }
}
