using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonContent
{
    /// <summary>Raised when a loadout breaks one of the rules the 出立 screen has to enforce.</summary>
    public sealed class LoadoutRuleException : Exception
    {
        public LoadoutRuleException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// What was chosen at 出立, and what changes during a run. Three tool slots and three
    /// consumable slots, common to exploration and battle (concept-v3.md §14). The 生存者
    /// ボーナス raises the tool slots to four for one life (§8.2).
    ///
    /// Immutable: picking something up returns a new loadout, so a run can be replayed.
    ///
    /// The exploration core does not carry this type: it carries DungeonCore.RunLoadout, which holds
    /// only what the core acts on. The rules kept here and nowhere else — ids checked against the
    /// catalogue, picking up and swapping on the way, dropping consumables on leaving — belong to the
    /// 出立 screen and the run around it. #104 is where a chosen Loadout becomes a RunLoadout; until
    /// then only the tests build one (#517).
    /// </summary>
    public sealed class Loadout
    {
        public const int ToolSlots = 3;
        public const int ConsumableSlots = 3;
        public const int SurvivorToolSlots = ToolSlots + 1;

        private readonly IReadOnlyList<ItemDef> _tools;
        private readonly IReadOnlyList<ItemDef> _consumables;

        private Loadout(IReadOnlyList<ItemDef> tools, IReadOnlyList<ItemDef> consumables, int toolSlots)
        {
            _tools = tools;
            _consumables = consumables;
            ToolSlotCount = toolSlots;
        }

        public IReadOnlyList<ItemDef> Tools => _tools;

        public IReadOnlyList<ItemDef> Consumables => _consumables;

        public int ToolSlotCount { get; }

        /// <summary>Nothing carried. What a bare run sets out with.</summary>
        public static Loadout Empty { get; } =
            new Loadout(Array.Empty<ItemDef>(), Array.Empty<ItemDef>(), ToolSlots);

        /// <summary>
        /// Builds a loadout from catalogue ids. **The same tool may be taken twice**: the slot
        /// count is the constraint, and no canon rule forbids a second copy (Issue #100 decision).
        /// A second copy of a calendar tool adds nothing, so it only spends a slot.
        /// </summary>
        public static Loadout Of(IReadOnlyList<string> toolIds, IReadOnlyList<string>? consumableIds = null, int toolSlots = ToolSlots)
        {
            if (toolIds == null) throw new ArgumentNullException(nameof(toolIds));
            if (toolSlots < 0) throw new ArgumentOutOfRangeException(nameof(toolSlots));

            var consumables = consumableIds ?? Array.Empty<string>();
            if (toolIds.Count > toolSlots)
                throw new LoadoutRuleException($"{toolIds.Count} 個のツールは {toolSlots} 枠に入りません");
            if (consumables.Count > ConsumableSlots)
                throw new LoadoutRuleException($"{consumables.Count} 個の消耗品は {ConsumableSlots} 枠に入りません");

            return new Loadout(
                toolIds.Select(ItemCatalogue.Tool).ToList(),
                consumables.Select(ItemCatalogue.Consumable).ToList(),
                toolSlots);
        }

        public bool Has(ItemEffect effect) => _tools.Any(t => t.Effect == effect);

        /// <summary>日読みの砂 is carried: every layer's entry node costs no day. A second copy adds nothing.</summary>
        public bool EntryDayFree => Has(ItemEffect.EntryDayFree);

        /// <summary>帰り道の糸 is carried: a life that comes back alive hands over in 1 day.</summary>
        public bool ShortHandover => Has(ItemEffect.ShortHandover);

        /// <summary>
        /// Picks something up. With a free slot it goes in; with none, the player swaps it for
        /// one they are carrying. Consumables are never carried back to camp, so there is no
        /// storage to fall back on (concept-v3.md §14).
        /// </summary>
        public Loadout Take(string consumableId, int replaceSlot = -1)
        {
            var item = ItemCatalogue.Consumable(consumableId);
            var next = _consumables.ToList();

            if (next.Count < ConsumableSlots)
            {
                next.Add(item);
                return new Loadout(_tools, next, ToolSlotCount);
            }

            if (replaceSlot < 0 || replaceSlot >= next.Count)
                throw new LoadoutRuleException("消耗品の枠が埋まっています。入れ替える枠を指定してください");

            next[replaceSlot] = item;
            return new Loadout(_tools, next, ToolSlotCount);
        }

        /// <summary>Uses a consumable up and reports what it was.</summary>
        public Loadout Use(int slot, out ItemDef used)
        {
            if (slot < 0 || slot >= _consumables.Count)
                throw new LoadoutRuleException("その枠に消耗品がありません");

            used = _consumables[slot];
            var next = _consumables.ToList();
            next.RemoveAt(slot);
            return new Loadout(_tools, next, ToolSlotCount);
        }

        /// <summary>Closing a life keeps nothing consumable (concept-v3.md §14).</summary>
        public Loadout LeaveTheDungeon() =>
            new Loadout(_tools, Array.Empty<ItemDef>(), ToolSlotCount);
    }
}
