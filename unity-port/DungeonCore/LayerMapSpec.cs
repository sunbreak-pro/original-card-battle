using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCore
{
    /// <summary>
    /// One shape a layer's map may take: how many rows run from the entry to the boss, how wide each row is and what
    /// the middle nodes hold. A shape either names every row's width, or names a range the generator draws the middle
    /// widths from; a drawn shape still fixes the number of middle nodes, so its node mix is a table row
    /// (seven_layers_v4.md §7).
    ///
    /// The first and last row hold one node each: the entry is always a battle and the last row is always the layer
    /// boss (dungeon_exploration_v4.md §4.1).
    /// </summary>
    public sealed class MapShape
    {
        /// <summary>A shape whose every row width is given.</summary>
        public MapShape(IReadOnlyList<int> rowWidths, IReadOnlyDictionary<NodeKind, int> quotas)
        {
            if (rowWidths == null) throw new ArgumentNullException(nameof(rowWidths));
            if (rowWidths.Count < 3)
                throw new ArgumentException("a layer needs an entry row, at least one middle row and a boss row", nameof(rowWidths));
            if (rowWidths[0] != 1)
                throw new ArgumentException("the entry row holds exactly one node", nameof(rowWidths));
            if (rowWidths[rowWidths.Count - 1] != 1)
                throw new ArgumentException("the boss row holds exactly one node", nameof(rowWidths));
            if (rowWidths.Any(w => w < 1))
                throw new ArgumentException("every row holds at least one node", nameof(rowWidths));
            for (int r = 1; r + 1 < rowWidths.Count; r++)
            {
                if (rowWidths[r] > 1 && rowWidths[r + 1] > LayerMapSpec.MaxBranches * rowWidths[r])
                    throw new ArgumentException(
                        $"row {r} of {rowWidths[r]} cannot reach a row of {rowWidths[r + 1]} with at most {LayerMapSpec.MaxBranches} ways out of each node",
                        nameof(rowWidths));
            }

            RowWidths = rowWidths.ToArray();
            RowCount = rowWidths.Count;
            MinWidth = rowWidths.Skip(1).Take(RowCount - 2).Min();
            MaxWidth = rowWidths.Skip(1).Take(RowCount - 2).Max();
            Quotas = CheckQuotas(quotas);

            int middle = rowWidths.Sum() - 2;
            if (MiddleNodeCount != middle)
                throw new ArgumentException(
                    $"quotas total {MiddleNodeCount} but the map has {middle} middle nodes", nameof(quotas));
            CheckDetours();
        }

        /// <summary>
        /// A shape of <paramref name="rowCount"/> rows whose middle widths the generator draws, each between
        /// <paramref name="minWidth"/> and <paramref name="maxWidth"/>, so that they add up to the quotas' total.
        /// The row under the entry is 2 or 3 wide: the entry is always a choice and leads to no more than
        /// <see cref="LayerMapSpec.MaxBranches"/> nodes.
        /// </summary>
        public MapShape(int rowCount, int minWidth, int maxWidth, IReadOnlyDictionary<NodeKind, int> quotas)
        {
            if (rowCount < 3)
                throw new ArgumentException("a layer needs an entry row, at least one middle row and a boss row", nameof(rowCount));
            if (minWidth < 1)
                throw new ArgumentOutOfRangeException(nameof(minWidth), minWidth, "every row holds at least one node");
            if (maxWidth < minWidth)
                throw new ArgumentOutOfRangeException(nameof(maxWidth), maxWidth, "the widest row cannot be narrower than the narrowest");
            // Every node of a row may lead to at most MaxBranches nodes, so a row can only feed a row that many times wider.
            if (maxWidth > LayerMapSpec.MaxBranches * minWidth)
                throw new ArgumentOutOfRangeException(nameof(maxWidth), maxWidth,
                    $"a row of {minWidth} cannot reach a row of {maxWidth} with at most {LayerMapSpec.MaxBranches} ways out of each node");

            RowWidths = null;
            RowCount = rowCount;
            MinWidth = minWidth;
            MaxWidth = maxWidth;
            Quotas = CheckQuotas(quotas);

            int lo = FirstRowMin + (MiddleRowCount - 1) * minWidth;
            int hi = FirstRowMax + (MiddleRowCount - 1) * maxWidth;
            if (FirstRowMin > FirstRowMax)
                throw new ArgumentException(
                    $"the row under the entry must be 2..{LayerMapSpec.MaxBranches} wide, which {minWidth}..{maxWidth} does not allow", nameof(maxWidth));
            if (MiddleNodeCount < lo || MiddleNodeCount > hi)
                throw new ArgumentException(
                    $"quotas total {MiddleNodeCount} but {MiddleRowCount} middle rows of {minWidth}..{maxWidth} hold {lo}..{hi} middle nodes",
                    nameof(quotas));
            CheckDetours();
        }

        public int RowCount { get; }

        /// <summary>Every row's width, entry and boss included; null when the middle widths are drawn.</summary>
        public IReadOnlyList<int>? RowWidths { get; }

        /// <summary>True when the generator draws the middle widths.</summary>
        public bool IsDrawn => RowWidths == null;

        /// <summary>The narrowest a middle row may be.</summary>
        public int MinWidth { get; }

        /// <summary>The widest a middle row may be.</summary>
        public int MaxWidth { get; }

        public IReadOnlyDictionary<NodeKind, int> Quotas { get; }

        public int MiddleRowCount => RowCount - 2;

        public int MiddleNodeCount => Quotas.Values.Sum();

        public int NodeCount => MiddleNodeCount + 2;

        /// <summary>Days to reach the boss without detouring: one node per row.</summary>
        public int ShortestPathCost => RowCount;

        /// <summary>Days to resolve every node. Backtracking over resolved nodes is free (§4.2).</summary>
        public int FullSweepCost => NodeCount;

        /// <summary>The narrowest the row under the entry may be drawn: the entry always offers a choice.</summary>
        internal int FirstRowMin => Math.Max(2, MinWidth);

        /// <summary>The widest the row under the entry may be drawn: the entry leads to at most MaxBranches nodes.</summary>
        internal int FirstRowMax => Math.Min(LayerMapSpec.MaxBranches, MaxWidth);

        /// <summary>Copy of this shape with <paramref name="count"/> × <paramref name="kind"/>, traded against battles.</summary>
        public MapShape With(NodeKind kind, int count)
        {
            var next = Quotas.ToDictionary(p => p.Key, p => p.Value);
            int current = next.TryGetValue(kind, out int had) ? had : 0;
            int delta = count - current;
            next[kind] = count;
            if (next[kind] == 0) next.Remove(kind);

            int battles = next.TryGetValue(NodeKind.Battle, out int b) ? b : 0;
            if (battles - delta < 0)
                throw new ArgumentException($"not enough battle nodes to trade for {count} × {kind.ToToken()}", nameof(count));
            next[NodeKind.Battle] = battles - delta;
            if (next[NodeKind.Battle] == 0) next.Remove(NodeKind.Battle);

            return RowWidths != null ? new MapShape(RowWidths, next) : new MapShape(RowCount, MinWidth, MaxWidth, next);
        }

        private static IReadOnlyDictionary<NodeKind, int> CheckQuotas(IReadOnlyDictionary<NodeKind, int> quotas)
        {
            if (quotas == null) throw new ArgumentNullException(nameof(quotas));
            if (quotas.ContainsKey(NodeKind.Boss))
                throw new ArgumentException("the boss is placed by the generator, not by a quota", nameof(quotas));
            if (quotas.Values.Any(v => v < 0))
                throw new ArgumentException("a quota cannot be negative", nameof(quotas));
            return quotas;
        }

        // Each middle row keeps one node that is not a detour, and those nodes are linked into one route, so the room
        // left for detours is every middle node but one per row — however the widths are drawn.
        private void CheckDetours()
        {
            int detourTotal = Quotas.Where(q => q.Key.IsDetour()).Sum(q => q.Value);
            int detourCapacity = MiddleNodeCount - MiddleRowCount;
            if (detourTotal > detourCapacity)
                throw new ArgumentException(
                    $"{detourTotal} avoidable nodes do not fit: every row must keep one node that is not a detour, " +
                    $"which leaves room for {detourCapacity}", "quotas");

            int plainTotal = MiddleNodeCount - detourTotal;
            if (plainTotal < MiddleRowCount)
                throw new ArgumentException(
                    $"{plainTotal} non-detour nodes cannot cover {MiddleRowCount} middle rows", "quotas");
        }
    }

    /// <summary>
    /// The shapes a layer's map may take. The generator picks one of them and fills it in with an <see cref="IRng"/>;
    /// the spec itself carries no randomness, so a spec plus a seed names exactly one map.
    /// </summary>
    public sealed class LayerMapSpec
    {
        /// <summary>The most nodes one node leads to (#1049). Only a node alone in its row may lead to more.</summary>
        public const int MaxBranches = 3;

        /// <summary>A spec of one shape whose every row width is given.</summary>
        public LayerMapSpec(int layer, IReadOnlyList<int> rowWidths, IReadOnlyDictionary<NodeKind, int> quotas)
            : this(layer, new[] { new MapShape(rowWidths, quotas) })
        {
        }

        public LayerMapSpec(int layer, IReadOnlyList<MapShape> shapes)
        {
            if (shapes == null) throw new ArgumentNullException(nameof(shapes));
            if (shapes.Count == 0) throw new ArgumentException("a layer needs at least one shape", nameof(shapes));
            if (shapes.Any(s => s == null)) throw new ArgumentException("a shape is missing", nameof(shapes));
            // The validator finds the shape a map was drawn from by its row count.
            if (shapes.Select(s => s.RowCount).Distinct().Count() != shapes.Count)
                throw new ArgumentException("two shapes of the same row count", nameof(shapes));

            Layer = layer;
            Shapes = shapes.ToArray();
        }

        public int Layer { get; }

        /// <summary>The shapes the generator picks from, one per row count, shortest first as given.</summary>
        public IReadOnlyList<MapShape> Shapes { get; }

        /// <summary>
        /// The first shape's node mix. The kinds a layer counts out (精鋭 / 休息 / 情報収集 / 彫り / 痕跡) are the same in
        /// every shape of the lair; only battles and events grow with the row count.
        /// </summary>
        public IReadOnlyDictionary<NodeKind, int> Quotas => Shapes[0].Quotas;

        public int MinRowCount => Shapes.Min(s => s.RowCount);

        public int MaxRowCount => Shapes.Max(s => s.RowCount);

        /// <summary>The fewest nodes a map of this spec holds.</summary>
        public int NodeCount => Shapes.Min(s => s.NodeCount);

        /// <summary>The most nodes a map of this spec holds.</summary>
        public int MaxNodeCount => Shapes.Max(s => s.NodeCount);

        /// <summary>The fewest days to reach the boss without detouring: one per row of the shortest shape.</summary>
        public int ShortestPathCost => MinRowCount;

        /// <summary>The most days to reach the boss without detouring: one per row of the longest shape.</summary>
        public int MaxShortestPathCost => MaxRowCount;

        /// <summary>Days to resolve every node of the smallest shape. Backtracking over resolved nodes is free (§4.2).</summary>
        public int FullSweepCost => NodeCount;

        /// <summary>Days to resolve every node of the largest shape.</summary>
        public int MaxFullSweepCost => MaxNodeCount;

        /// <summary>The shape a map of <paramref name="rowCount"/> rows was drawn from, or null.</summary>
        public MapShape? ShapeOf(int rowCount) => Shapes.FirstOrDefault(s => s.RowCount == rowCount);

        /// <summary>Copy of this spec with <paramref name="count"/> × <paramref name="kind"/> in every shape, traded against battles.</summary>
        public LayerMapSpec With(NodeKind kind, int count) =>
            new LayerMapSpec(Layer, Shapes.Select(s => s.With(kind, count)).ToArray());

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
        /// The five-row layer of eleven nodes the lair used before #1049 drew 7〜9 rows. Kept as a fixed shape for the
        /// tests of the generator's rules.
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
