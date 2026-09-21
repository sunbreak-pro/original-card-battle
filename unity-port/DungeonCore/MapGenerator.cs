using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCore
{
    /// <summary>
    /// Builds one layer's node map from a spec and a seed. Nothing here reads the clock or
    /// the platform RNG, so the same spec and seed give the same map everywhere (#97).
    ///
    /// The Web build's `src/domain/dungeon/logic/dungeonLogic.ts` was read for reference and
    /// deliberately not ported: it draws from `Math.random()` and assumes the old five-depth
    /// loop with lives and Gold.
    /// </summary>
    public static class MapGenerator
    {
        public static LayerMap Generate(LayerMapSpec spec, ulong seed)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            return Generate(spec, new SeededRng(seed), seed);
        }

        public static LayerMap Generate(LayerMapSpec spec, IRng rng, ulong seed = 0)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var kinds = AssignKinds(spec, rng);
            var nodes = BuildNodes(spec, kinds);
            var edges = BuildEdges(spec, nodes, rng);

            var map = new LayerMap(spec.Layer, seed, nodes, edges);
            MapValidator.Validate(map, spec);
            return map;
        }

        /// <summary>
        /// Picks a kind for every slot. Detour kinds (精鋭 / 痕跡 / 彫り) go into rows that keep
        /// at least one node the player can take instead, so a detour is never forced (§3.1).
        /// </summary>
        private static NodeKind[][] AssignKinds(LayerMapSpec spec, IRng rng)
        {
            int rows = spec.RowCount;
            var grid = new NodeKind[rows][];
            for (int r = 0; r < rows; r++) grid[r] = new NodeKind[spec.RowWidths[r]];

            grid[0][0] = NodeKind.Battle;
            grid[rows - 1][0] = NodeKind.Boss;

            var detourBag = new List<NodeKind>();
            var plainBag = new List<NodeKind>();
            foreach (var pair in spec.Quotas.OrderBy(p => p.Key))
            {
                for (int i = 0; i < pair.Value; i++)
                {
                    if (pair.Key.IsDetour()) detourBag.Add(pair.Key);
                    else plainBag.Add(pair.Key);
                }
            }
            rng.Shuffle(detourBag);
            rng.Shuffle(plainBag);

            // One slot per middle row is held back for a non-detour kind. That single rule is
            // what makes every elite avoidable without a second pass over the map.
            var reserved = new (int Row, int Column)[rows - 2];
            for (int r = 1; r < rows - 1; r++)
            {
                reserved[r - 1] = (r, rng.NextInt(0, spec.RowWidths[r]));
            }

            var eligible = new List<(int Row, int Column)>();
            for (int r = 1; r < rows - 1; r++)
            {
                for (int c = 0; c < spec.RowWidths[r]; c++)
                {
                    if (reserved[r - 1].Column == c) continue;
                    eligible.Add((r, c));
                }
            }
            rng.Shuffle(eligible);

            var taken = new HashSet<(int, int)>();
            for (int i = 0; i < detourBag.Count; i++)
            {
                var slot = eligible[i];
                grid[slot.Row][slot.Column] = detourBag[i];
                taken.Add((slot.Row, slot.Column));
            }

            int plainIndex = 0;
            for (int r = 1; r < rows - 1; r++)
            {
                for (int c = 0; c < spec.RowWidths[r]; c++)
                {
                    if (taken.Contains((r, c))) continue;
                    grid[r][c] = plainBag[plainIndex++];
                }
            }

            return grid;
        }

        private static List<MapNode> BuildNodes(LayerMapSpec spec, NodeKind[][] kinds)
        {
            var nodes = new List<MapNode>(spec.NodeCount);
            int id = 0;
            for (int r = 0; r < spec.RowCount; r++)
            {
                for (int c = 0; c < spec.RowWidths[r]; c++)
                {
                    nodes.Add(new MapNode(id++, r, c, kinds[r][c]));
                }
            }
            return nodes;
        }

        /// <summary>
        /// Links each row to the next. Every node keeps at least one way forward and every node
        /// keeps at least one way back, so no node can be stranded. Links never cross, which is
        /// what lets the map be drawn as a ladder later.
        /// </summary>
        private static Dictionary<int, IReadOnlyList<int>> BuildEdges(
            LayerMapSpec spec, IReadOnlyList<MapNode> nodes, IRng rng)
        {
            var edges = new Dictionary<int, IReadOnlyList<int>>();
            foreach (var node in nodes) edges[node.Id] = Array.Empty<int>();

            var rowStart = new int[spec.RowCount];
            int running = 0;
            for (int r = 0; r < spec.RowCount; r++)
            {
                rowStart[r] = running;
                running += spec.RowWidths[r];
            }

            for (int r = 0; r < spec.RowCount - 1; r++)
            {
                int a = spec.RowWidths[r];
                int b = spec.RowWidths[r + 1];

                var lo = new int[a];
                var hi = new int[a];
                for (int i = 0; i < a; i++)
                {
                    lo[i] = (int)((long)i * b / a);
                    hi[i] = (int)(((long)(i + 1) * b + a - 1) / a) - 1;
                    if (hi[i] < lo[i]) hi[i] = lo[i];
                }

                // Widening one node's reach by a single column is what creates a fork. It is only
                // allowed where it cannot overtake the next node's reach, so links stay uncrossed.
                for (int i = 0; i < a - 1; i++)
                {
                    if (hi[i] + 1 <= hi[i + 1] && rng.NextChance(spec.ExtraEdgeChance)) hi[i]++;
                }

                for (int i = 0; i < a; i++)
                {
                    var targets = new List<int>();
                    for (int j = lo[i]; j <= hi[i]; j++) targets.Add(rowStart[r + 1] + j);
                    edges[rowStart[r] + i] = targets;
                }
            }

            return edges;
        }
    }
}
