using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DungeonCore
{
    /// <summary>One node on a layer's map. Rows run from the entry (0) down to the boss.</summary>
    public sealed record MapNode(int Id, int Row, int Column, NodeKind Kind);

    /// <summary>
    /// A generated layer map: nodes in rows, forward-only edges, one entry and one boss.
    /// The map is immutable; progress through it lives in the exploration run (#98).
    /// </summary>
    public sealed class LayerMap
    {
        private readonly IReadOnlyDictionary<int, IReadOnlyList<int>> _edges;
        private readonly IReadOnlyDictionary<int, IReadOnlyList<int>> _incoming;
        private readonly IReadOnlyDictionary<int, MapNode> _byId;

        public LayerMap(
            int layer,
            ulong seed,
            IReadOnlyList<MapNode> nodes,
            IReadOnlyDictionary<int, IReadOnlyList<int>> edges)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (edges == null) throw new ArgumentNullException(nameof(edges));
            if (nodes.Count == 0) throw new ArgumentException("a layer map needs at least one node", nameof(nodes));

            Layer = layer;
            Seed = seed;
            Nodes = nodes;
            _edges = edges;
            _byId = nodes.ToDictionary(n => n.Id);

            RowCount = nodes.Max(n => n.Row) + 1;
            EntryId = nodes.Single(n => n.Row == 0).Id;
            BossId = nodes.Single(n => n.Kind == NodeKind.Boss).Id;

            var incoming = new Dictionary<int, List<int>>();
            foreach (var node in nodes) incoming[node.Id] = new List<int>();
            foreach (var pair in edges)
            {
                foreach (int target in pair.Value) incoming[target].Add(pair.Key);
            }
            _incoming = incoming.ToDictionary(p => p.Key, p => (IReadOnlyList<int>)p.Value);
        }

        /// <summary>1-based layer number (1..7).</summary>
        public int Layer { get; }

        /// <summary>The seed this map was generated from. Same seed and spec, same map.</summary>
        public ulong Seed { get; }

        public IReadOnlyList<MapNode> Nodes { get; }

        public int RowCount { get; }

        public int EntryId { get; }

        public int BossId { get; }

        public int NodeCount => Nodes.Count;

        /// <summary>
        /// 刻限 needed to walk from the entry to the boss without detouring. Every row
        /// costs one node, so this is the row count — the floor on the cost of a layer.
        /// </summary>
        public int ShortestPathCost => RowCount;

        public MapNode Node(int id) =>
            _byId.TryGetValue(id, out var node) ? node : throw new ArgumentOutOfRangeException(nameof(id), id, "no such node");

        public IReadOnlyList<int> Successors(int id) =>
            _edges.TryGetValue(id, out var next) ? next : Array.Empty<int>();

        public IReadOnlyList<int> Predecessors(int id) =>
            _incoming.TryGetValue(id, out var prev) ? prev : Array.Empty<int>();

        /// <summary>
        /// Everywhere the player may step from this node. Movement runs both ways along a link:
        /// walking back over a node that is already resolved is free, which is how a run takes
        /// both sides of a fork (dungeon_exploration_v4.md §4.2). Only entering an unresolved
        /// node costs 刻限, and that accounting belongs to the exploration run, not to the map.
        /// </summary>
        public IReadOnlyList<int> Neighbours(int id) =>
            Successors(id).Concat(Predecessors(id)).Distinct().OrderBy(x => x).ToList();

        public IReadOnlyList<MapNode> Row(int row) =>
            Nodes.Where(n => n.Row == row).OrderBy(n => n.Column).ToList();

        public int CountOf(NodeKind kind) => Nodes.Count(n => n.Kind == kind);

        /// <summary>
        /// Stable one-line rendering of the whole map. Two maps with the same fingerprint
        /// are the same map, so determinism tests compare this instead of walking the graph.
        /// </summary>
        public string Fingerprint()
        {
            var sb = new StringBuilder();
            sb.Append("L").Append(Layer).Append('|');
            foreach (var node in Nodes.OrderBy(n => n.Id))
            {
                sb.Append(node.Id).Append(':').Append(node.Row).Append(',').Append(node.Column)
                  .Append(',').Append(node.Kind.ToToken())
                  .Append("->").Append(string.Join(".", Successors(node.Id).OrderBy(x => x)))
                  .Append(';');
            }
            return sb.ToString();
        }
    }
}
