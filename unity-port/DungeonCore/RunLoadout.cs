using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCore
{
    /// <summary>
    /// What a relief carried into the dungeon actually does. The exploration core only needs
    /// the shape; which items exist and what they are called is #100's job.
    /// </summary>
    public enum ReliefKind
    {
        /// <summary>Lowers the layer's density for as long as the life lasts (防瘴の面).</summary>
        Density,

        /// <summary>Pulls the gauge back down once, then the item is gone (浄化の香).</summary>
        Gauge,

        // Trial extension (docs/EXTENSIONS.md 「探索の持ち物とイベント」): the rest of the departure's items, so the
        // exploration can carry every tool and consumable of tools_and_consumables_v4.md §2 / §3. Only a trial
        // RunLoadout holds tools of these kinds; the C# shape (Density tools, any consumable) is unchanged.

        /// <summary>刻限の砂: every layer's 刻限 + Amount (§2).</summary>
        Time,

        /// <summary>写しの帳: the player picks the 情報収集 target (§2.2).</summary>
        Survey,

        /// <summary>灯芯の欠片: 階層間の休憩 heals Amount more points of the maximum (§2).</summary>
        Interlude,

        /// <summary>間合いの履 / 息継ぎの笛 / 竜鱗の欠片 / 竜語の符: acts in battle only; carried for the slot count.</summary>
        Battle,

        /// <summary>薬草: HP back by Amount% of the maximum (§3).</summary>
        Heal,

        /// <summary>濃い干し肉: max stamina + Amount, a temporary change inside ±4 (§3).</summary>
        MaxStamina,

        /// <summary>忘れ水: current stamina full (§3).</summary>
        Refill,

        /// <summary>標の石: jump to an unresolved node of the layer, paying its 刻限 (§3).</summary>
        Teleport,
    }

    /// <summary>One carried item, reduced to its effect on 瘴気. Name and art live elsewhere.</summary>
    public sealed record ReliefItem(string Id, ReliefKind Kind, int Amount)
    {
        public static ReliefItem Density(string id, int amount) => new ReliefItem(id, ReliefKind.Density, amount);

        public static ReliefItem Gauge(string id, int amount) => new ReliefItem(id, ReliefKind.Gauge, amount);
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

        /// <summary>A consumable slot can take one more item. (Trial extension.)</summary>
        // js: core/dungeon/RunLoadout.js:111
        public bool HasFreeSlot => _consumables.Count < ConsumableSlots;

        /// <summary>
        /// Trial extension: picks a consumable up (encounters_and_rewards_v4.md §4.3,
        /// tools_and_consumables_v4.md §3: 「枠が埋まっているときは、その場で入れ替えるか諦めます」). With a free
        /// slot it goes in after the others; with none it replaces the item in <paramref name="replaceSlot"/>.
        /// The same rule as DungeonContent.Loadout.Take, on the core's items.
        /// </summary>
        // js: core/dungeon/RunLoadout.js:119
        public RunLoadout Take(ReliefItem item, int replaceSlot = -1)
        {
            if (item == null) throw new ArgumentNullException(nameof(item), "a consumable to take");
            var next = _consumables.ToList();
            if (next.Count < ConsumableSlots) next.Add(item);
            else if (replaceSlot < 0 || replaceSlot >= next.Count)
                throw new ArgumentOutOfRangeException(nameof(replaceSlot), replaceSlot, "the consumable slots are full; name the slot to swap");
            else next[replaceSlot] = item;
            return new RunLoadout(Tools, next, ToolSlotCount, Trial);
        }

        /// <summary>Spends one consumable and reports what it pulls off the gauge.</summary>
        public RunLoadout Spend(int index, out int gaugeRelief) => Spend(index, out gaugeRelief, out _);

        /// <summary>
        /// Spends one consumable and reports what it pulls off the gauge, and (trial extension) the item spent,
        /// for the trial effects.
        /// </summary>
        // js: core/dungeon/RunLoadout.js:133
        public RunLoadout Spend(int index, out int gaugeRelief, out ReliefItem item)
        {
            if (index < 0 || index >= _consumables.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "no consumable in that slot");

            item = _consumables[index];
            gaugeRelief = item.Kind == ReliefKind.Gauge ? item.Amount : 0;

            var rest = _consumables.ToList();
            rest.RemoveAt(index);
            return new RunLoadout(Tools, rest, ToolSlotCount, Trial);
        }

        /// <summary>Nothing carried. The baseline a bare run starts from.</summary>
        public static RunLoadout Empty { get; } = new RunLoadout();
    }
}
