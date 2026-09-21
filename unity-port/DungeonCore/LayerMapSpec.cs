using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCore
{
    /// <summary>
    /// The shape a layer's map must take. The generator fills this in with an <see cref="IRng"/>;
    /// the spec itself carries no randomness, so a spec plus a seed names exactly one map.
    ///
    /// Rows run from the entry down to the boss. The first and last row hold one node each:
    /// the entry is always a battle and the last row is always the layer boss
    /// (dungeon_exploration_v4.md §4.1).
    /// </summary>
    public sealed class LayerMapSpec
    {
        public const double DefaultExtraEdgeChance = 0.5;

        public LayerMapSpec(
            int layer,
            IReadOnlyList<int> rowWidths,
            IReadOnlyDictionary<NodeKind, int> quotas,
            double extraEdgeChance = DefaultExtraEdgeChance)
        {
            if (rowWidths == null) throw new ArgumentNullException(nameof(rowWidths));
            if (quotas == null) throw new ArgumentNullException(nameof(quotas));
            if (rowWidths.Count < 3)
                throw new ArgumentException("a layer needs an entry row, at least one middle row and a boss row", nameof(rowWidths));
            if (rowWidths[0] != 1)
                throw new ArgumentException("the entry row holds exactly one node", nameof(rowWidths));
            if (rowWidths[rowWidths.Count - 1] != 1)
                throw new ArgumentException("the boss row holds exactly one node", nameof(rowWidths));
            if (rowWidths.Any(w => w < 1))
                throw new ArgumentException("every row holds at least one node", nameof(rowWidths));
            if (quotas.ContainsKey(NodeKind.Boss))
                throw new ArgumentException("the boss is placed by the generator, not by a quota", nameof(quotas));
            if (quotas.Values.Any(v => v < 0))
                throw new ArgumentException("a quota cannot be negative", nameof(quotas));
            if (extraEdgeChance < 0.0 || extraEdgeChance > 1.0)
                throw new ArgumentOutOfRangeException(nameof(extraEdgeChance), extraEdgeChance, "chance runs 0..1");

            Layer = layer;
            RowWidths = rowWidths;
            Quotas = quotas;
            ExtraEdgeChance = extraEdgeChance;

            NodeCount = rowWidths.Sum();
            MiddleNodeCount = NodeCount - 2;

            int quotaTotal = quotas.Values.Sum();
            if (quotaTotal != MiddleNodeCount)
                throw new ArgumentException(
                    $"quotas total {quotaTotal} but the map has {MiddleNodeCount} middle nodes", nameof(quotas));

            int middleRows = rowWidths.Count - 2;
            int detourTotal = quotas.Where(q => q.Key.IsDetour()).Sum(q => q.Value);
            int detourCapacity = rowWidths.Skip(1).Take(middleRows).Sum(w => w - 1);
            if (detourTotal > detourCapacity)
                throw new ArgumentException(
                    $"{detourTotal} avoidable nodes do not fit: every row must keep one node that is not a detour, " +
                    $"which leaves room for {detourCapacity}", nameof(quotas));

            int plainTotal = MiddleNodeCount - detourTotal;
            if (plainTotal < middleRows)
                throw new ArgumentException(
                    $"{plainTotal} non-detour nodes cannot cover {middleRows} middle rows", nameof(quotas));
        }

        public int Layer { get; }

        public IReadOnlyList<int> RowWidths { get; }

        public IReadOnlyDictionary<NodeKind, int> Quotas { get; }

        /// <summary>Odds that a row gains one extra forward edge, which is what makes a fork.</summary>
        public double ExtraEdgeChance { get; }

        public int NodeCount { get; }

        public int MiddleNodeCount { get; }

        public int RowCount => RowWidths.Count;

        /// <summary>刻限 to reach the boss without detouring: one node per row.</summary>
        public int ShortestPathCost => RowCount;

        /// <summary>刻限 to resolve every node. Backtracking over resolved nodes is free (§4.2).</summary>
        public int FullSweepCost => NodeCount;

        /// <summary>Copy of this spec with one node of <paramref name="kind"/> swapped in for a battle.</summary>
        public LayerMapSpec With(NodeKind kind, int count)
        {
            var next = new Dictionary<NodeKind, int>(Quotas.ToDictionary(p => p.Key, p => p.Value));
            int current = next.TryGetValue(kind, out int had) ? had : 0;
            int delta = count - current;
            next[kind] = count;
            if (next[kind] == 0) next.Remove(kind);

            int battles = next.TryGetValue(NodeKind.Battle, out int b) ? b : 0;
            if (battles - delta < 0)
                throw new ArgumentException($"not enough battle nodes to trade for {count} × {kind.ToToken()}", nameof(count));
            next[NodeKind.Battle] = battles - delta;
            if (next[NodeKind.Battle] == 0) next.Remove(NodeKind.Battle);

            return new LayerMapSpec(Layer, RowWidths, next, ExtraEdgeChance);
        }

        /// <summary>
        /// The smallest layer that still holds the whole loop, from dungeon_exploration_v4.md §4.1:
        /// six nodes, two forks, entry battle, boss exit. 刻限 for it is 6 (§3.3).
        /// </summary>
        public static LayerMapSpec Minimal(int layer = 1) => new LayerMapSpec(
            layer,
            new[] { 1, 2, 2, 1 },
            new Dictionary<NodeKind, int>
            {
                [NodeKind.Rest] = 1,
                [NodeKind.Survey] = 1,
                [NodeKind.Battle] = 1,
                [NodeKind.Elite] = 1,
            });

        /// <summary>
        /// A full-sized layer: eleven nodes over five rows. The shortest route is five 刻限,
        /// so a ten-刻限 budget leaves five to spend on detours and still cannot take
        /// every node — which is what dungeon_exploration_v4.md §3.1 asks for.
        /// </summary>
        public static LayerMapSpec Full(int layer) => new LayerMapSpec(
            layer,
            new[] { 1, 3, 3, 3, 1 },
            new Dictionary<NodeKind, int>
            {
                [NodeKind.Battle] = 3,
                [NodeKind.Elite] = 1,
                [NodeKind.Rest] = 2,
                [NodeKind.Survey] = 1,
                [NodeKind.Event] = 2,
            });
    }
}
