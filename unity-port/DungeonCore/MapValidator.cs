using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCore
{
    /// <summary>Raised when a generated map breaks one of the rules the generator promises.</summary>
    public sealed class MapValidationException : Exception
    {
        public MapValidationException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Checks the promises a layer map makes. The generator runs this on every map it
    /// produces, so a bad seed fails loudly at generation instead of quietly at play time.
    /// </summary>
    public static class MapValidator
    {
        public static void Validate(LayerMap map, LayerMapSpec spec)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            CheckShape(map, spec);
            CheckEnds(map);
            CheckEdgesGoForwardOneRow(map);
            CheckEveryNodeIsOnARoute(map);
            CheckDetoursAreAvoidable(map);
            CheckThereIsAFork(map);
            CheckQuotas(map, spec);
        }

        private static void CheckShape(LayerMap map, LayerMapSpec spec)
        {
            if (map.NodeCount != spec.NodeCount)
                throw new MapValidationException($"map has {map.NodeCount} nodes, spec asks for {spec.NodeCount}");
            if (map.RowCount != spec.RowCount)
                throw new MapValidationException($"map has {map.RowCount} rows, spec asks for {spec.RowCount}");

            for (int r = 0; r < spec.RowCount; r++)
            {
                int width = map.Row(r).Count;
                if (width != spec.RowWidths[r])
                    throw new MapValidationException($"row {r} holds {width} nodes, spec asks for {spec.RowWidths[r]}");
            }
        }

        private static void CheckEnds(LayerMap map)
        {
            var entry = map.Node(map.EntryId);
            if (entry.Row != 0)
                throw new MapValidationException("the entry is not on row 0");
            if (entry.Kind != NodeKind.Battle)
                throw new MapValidationException($"the entry is {entry.Kind.ToToken()}, it must be a battle");

            int bossCount = map.CountOf(NodeKind.Boss);
            if (bossCount != 1)
                throw new MapValidationException($"a layer holds exactly one boss, this one holds {bossCount}");

            var boss = map.Node(map.BossId);
            if (boss.Row != map.RowCount - 1)
                throw new MapValidationException("the boss is not on the last row");
            if (map.Successors(boss.Id).Count != 0)
                throw new MapValidationException("the boss leads somewhere; it is the layer's exit");
        }

        private static void CheckEdgesGoForwardOneRow(LayerMap map)
        {
            foreach (var node in map.Nodes)
            {
                foreach (int target in map.Successors(node.Id))
                {
                    var next = map.Node(target);
                    if (next.Row != node.Row + 1)
                        throw new MapValidationException(
                            $"node {node.Id} (row {node.Row}) links to node {target} (row {next.Row}); links go to the next row only");
                }

                if (node.Id != map.BossId && map.Successors(node.Id).Count == 0)
                    throw new MapValidationException($"node {node.Id} is a dead end that is not the boss");
                if (node.Id != map.EntryId && map.Predecessors(node.Id).Count == 0)
                    throw new MapValidationException($"node {node.Id} cannot be entered");
            }
        }

        /// <summary>
        /// The Definition of Done for #97: no node is unreachable. A node counts as reachable
        /// only if the entry can get to it AND it can get to the boss, so nothing is a cul-de-sac.
        /// </summary>
        private static void CheckEveryNodeIsOnARoute(LayerMap map)
        {
            var fromEntry = Reach(map, map.EntryId, forward: true);
            var toBoss = Reach(map, map.BossId, forward: false);

            foreach (var node in map.Nodes)
            {
                if (!fromEntry.Contains(node.Id))
                    throw new MapValidationException($"node {node.Id} cannot be reached from the entry");
                if (!toBoss.Contains(node.Id))
                    throw new MapValidationException($"node {node.Id} cannot reach the boss");
            }
        }

        private static void CheckDetoursAreAvoidable(LayerMap map)
        {
            for (int r = 1; r < map.RowCount - 1; r++)
            {
                var row = map.Row(r);
                if (row.All(n => n.Kind.IsDetour()))
                    throw new MapValidationException($"row {r} is nothing but detours, so the player cannot avoid them");
            }
        }

        private static void CheckThereIsAFork(LayerMap map)
        {
            if (!map.Nodes.Any(n => map.Successors(n.Id).Count >= 2))
                throw new MapValidationException("the map has no fork; every layer must offer a choice");
        }

        private static void CheckQuotas(LayerMap map, LayerMapSpec spec)
        {
            foreach (var pair in spec.Quotas)
            {
                int wanted = pair.Value;
                int got = map.CountOf(pair.Key);
                // The entry is a battle the spec does not count, so battles run one over.
                int expected = pair.Key == NodeKind.Battle ? wanted + 1 : wanted;
                if (got != expected)
                    throw new MapValidationException(
                        $"map holds {got} × {pair.Key.ToToken()}, expected {expected}");
            }
        }

        private static HashSet<int> Reach(LayerMap map, int from, bool forward)
        {
            var seen = new HashSet<int> { from };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                var next = forward ? map.Successors(current) : map.Predecessors(current);
                foreach (int id in next)
                {
                    if (seen.Add(id)) queue.Enqueue(id);
                }
            }
            return seen;
        }
    }
}
