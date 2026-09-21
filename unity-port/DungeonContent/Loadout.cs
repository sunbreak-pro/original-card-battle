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
        /// count is the constraint, and no canon rule forbids a second copy. That is what makes
        /// the survivor's fourth slot able to hold a second 防瘴の面 and reach the density
        /// relief of 2 that seven_layers_v4.md §3.3 counts on (Issue #100 decision).
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

        /// <summary>How much every layer's 瘴気 density drops. Tools stack.</summary>
        public int MiasmaDensityRelief =>
            _tools.Where(t => t.Effect == ItemEffect.MiasmaDensity).Sum(t => t.Amount);

        /// <summary>How much 刻限 every layer gains.</summary>
        public int TimeLimitBonus =>
            _tools.Where(t => t.Effect == ItemEffect.TimeLimit).Sum(t => t.Amount);

        public bool Has(ItemEffect effect) => _tools.Any(t => t.Effect == effect);

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
