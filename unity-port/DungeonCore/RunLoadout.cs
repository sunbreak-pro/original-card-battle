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

        private readonly List<ReliefItem> _consumables;

        public RunLoadout(
            IReadOnlyList<ReliefItem>? tools = null,
            IReadOnlyList<ReliefItem>? consumables = null,
            int toolSlots = ToolSlots)
        {
            var toolList = tools ?? Array.Empty<ReliefItem>();
            var consumableList = consumables ?? Array.Empty<ReliefItem>();

            if (toolSlots < 0) throw new ArgumentOutOfRangeException(nameof(toolSlots));
            if (toolList.Count > toolSlots)
                throw new ArgumentException($"{toolList.Count} tools do not fit in {toolSlots} slots", nameof(tools));
            if (consumableList.Count > ConsumableSlots)
                throw new ArgumentException(
                    $"{consumableList.Count} consumables do not fit in {ConsumableSlots} slots", nameof(consumables));
            if (toolList.Any(t => t.Kind != ReliefKind.Density))
                throw new ArgumentException("a tool thins the air for the whole life, so it lowers density", nameof(tools));

            Tools = toolList;
            ToolSlotCount = toolSlots;
            _consumables = consumableList.ToList();
        }

        public IReadOnlyList<ReliefItem> Tools { get; }

        public int ToolSlotCount { get; }

        public IReadOnlyList<ReliefItem> Consumables => _consumables;

        /// <summary>How much every layer's density drops for this life.</summary>
        public int DensityRelief => Tools.Sum(t => t.Amount);

        /// <summary>Spends one consumable and reports what it pulls off the gauge.</summary>
        public RunLoadout Spend(int index, out int gaugeRelief)
        {
            if (index < 0 || index >= _consumables.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "no consumable in that slot");

            var item = _consumables[index];
            gaugeRelief = item.Kind == ReliefKind.Gauge ? item.Amount : 0;

            var rest = _consumables.ToList();
            rest.RemoveAt(index);
            return new RunLoadout(Tools, rest, ToolSlotCount);
        }

        /// <summary>Nothing carried. The baseline a bare run starts from.</summary>
        public static RunLoadout Empty { get; } = new RunLoadout();
    }
}
