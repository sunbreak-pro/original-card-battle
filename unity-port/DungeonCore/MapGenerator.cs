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

            // A choice with one answer draws nothing, so a spec of one fixed shape (the king room) spends its draws
            // exactly as it did before shapes were drawn (#1049).
            var shape = spec.Shapes.Count == 1 ? spec.Shapes[0] : spec.Shapes[rng.NextInt(0, spec.Shapes.Count)];
            var widths = DrawWidths(shape, rng);
            var kinds = AssignKinds(shape, widths, rng, out var reserved);
            var nodes = BuildNodes(widths, kinds);
            var edges = BuildEdges(widths, reserved, rng);

            var map = new LayerMap(spec.Layer, seed, nodes, edges);
            MapValidator.Validate(map, spec);
            return map;
        }

        /// <summary>
        /// The width of every row. A drawn shape starts each middle row at its narrowest and hands the rest of its
        /// middle nodes out one at a time to a row that still has room, so the widths mix (#1049).
        /// </summary>
        private static int[] DrawWidths(MapShape shape, IRng rng)
        {
            if (shape.RowWidths != null) return shape.RowWidths.ToArray();

            int rows = shape.RowCount;
            var widths = new int[rows];
            var max = new int[rows];
            widths[0] = widths[rows - 1] = 1;
            max[0] = max[rows - 1] = 1;
            for (int r = 1; r < rows - 1; r++)
            {
                widths[r] = r == 1 ? shape.FirstRowMin : shape.MinWidth;
                max[r] = r == 1 ? shape.FirstRowMax : shape.MaxWidth;
            }

            int left = shape.MiddleNodeCount - widths.Sum() + 2;
            var open = new List<int>();
            for (; left > 0; left--)
            {
                open.Clear();
                for (int r = 1; r < rows - 1; r++)
                {
                    if (widths[r] < max[r]) open.Add(r);
                }
                int row = open.Count == 1 ? open[0] : open[rng.NextInt(0, open.Count)];
                widths[row]++;
            }
            return widths;
        }

        /// <summary>
        /// Picks a kind for every slot. One slot per middle row is held back for a kind that is not a detour
        /// (精鋭 / 痕跡 / 彫り), and <see cref="BuildEdges"/> links those slots into one route from the entry to the boss,
        /// so a detour is never forced (§3.1).
        /// </summary>
        private static NodeKind[][] AssignKinds(MapShape shape, int[] widths, IRng rng, out int[] reservedColumn)
        {
            int rows = widths.Length;
            var grid = new NodeKind[rows][];
            for (int r = 0; r < rows; r++) grid[r] = new NodeKind[widths[r]];

            grid[0][0] = NodeKind.Battle;
            grid[rows - 1][0] = NodeKind.Boss;

            var detourBag = new List<NodeKind>();
            var plainBag = new List<NodeKind>();
            foreach (var pair in shape.Quotas.OrderBy(p => p.Key))
            {
                for (int i = 0; i < pair.Value; i++)
                {
                    if (pair.Key.IsDetour()) detourBag.Add(pair.Key);
                    else plainBag.Add(pair.Key);
                }
            }
            rng.Shuffle(detourBag);
            rng.Shuffle(plainBag);

            // Each held-back slot is drawn among the columns the one above can still be linked to. When every column can
            // be (any row of a fixed shape so far), this is the plain draw over the row it always was.
            reservedColumn = new int[rows];
            for (int r = 1; r < rows - 1; r++)
            {
                var columns = new List<int>();
                for (int c = 0; c < widths[r]; c++)
                {
                    if (r == 1 || Linkable(widths[r - 1], widths[r], reservedColumn[r - 1], c)) columns.Add(c);
                }
                reservedColumn[r] = columns[rng.NextInt(0, columns.Count)];
            }

            var eligible = new List<(int Row, int Column)>();
            for (int r = 1; r < rows - 1; r++)
            {
                for (int c = 0; c < widths[r]; c++)
                {
                    if (reservedColumn[r] == c) continue;
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
                for (int c = 0; c < widths[r]; c++)
                {
                    if (taken.Contains((r, c))) continue;
                    grid[r][c] = plainBag[plainIndex++];
                }
            }

            return grid;
        }

        private static List<MapNode> BuildNodes(int[] widths, NodeKind[][] kinds)
        {
            var nodes = new List<MapNode>(widths.Sum());
            int id = 0;
            for (int r = 0; r < widths.Length; r++)
            {
                for (int c = 0; c < widths[r]; c++)
                {
                    nodes.Add(new MapNode(id++, r, c, kinds[r][c]));
                }
            }
            return nodes;
        }

        // How often a node leads to 1, 2 or 3 nodes, before what the row can still fit decides: three is half as likely
        // as one or two, so it stands out and nine rows stay readable. Narrowing rows force one way, which is why about
        // seven nodes in ten end up with a single way out (#1049).
        private static readonly int[] ReachWeight = { 0, 2, 2, 1 };

        // Whether the next node starts on the column this node ends on (two ways merge there) or on the one after.
        private const int SharedStartWeight = 1;
        private const int FreshStartWeight = 2;

        /// <summary>
        /// Links each row to the next. Every node of a row reaches a run of neighbouring columns of the next row, the runs
        /// go left to right and touch or follow one another, so links never cross (the map is drawn as a ladder), every
        /// node keeps a way forward and every node keeps a way back. A run may reach left or right of the node, and holds
        /// 1 to <see cref="LayerMapSpec.MaxBranches"/> columns; only a node alone in its row reaches the whole next row.
        /// The held-back slots of <see cref="AssignKinds"/> are linked row to row.
        /// </summary>
        private static Dictionary<int, IReadOnlyList<int>> BuildEdges(int[] widths, int[] reservedColumn, IRng rng)
        {
            int rows = widths.Length;
            var edges = new Dictionary<int, IReadOnlyList<int>>();
            var rowStart = new int[rows];
            int running = 0;
            for (int r = 0; r < rows; r++)
            {
                rowStart[r] = running;
                running += widths[r];
            }
            edges[rowStart[rows - 1]] = Array.Empty<int>();

            for (int r = 0; r < rows - 1; r++)
            {
                // The held-back slot of a middle row must reach the held-back slot of the next middle row.
                bool linked = r >= 1 && r + 1 <= rows - 2;
                var runs = Runs(widths[r], widths[r + 1], linked ? reservedColumn[r] : -1, linked ? reservedColumn[r + 1] : -1, rng);
                for (int i = 0; i < widths[r]; i++)
                {
                    var targets = new List<int>();
                    for (int j = runs[i].Lo; j <= runs[i].Hi; j++) targets.Add(rowStart[r + 1] + j);
                    edges[rowStart[r] + i] = targets;
                }
            }

            return edges;
        }

        /// <summary>
        /// The run of next-row columns each of <paramref name="a"/> nodes reaches in a row of <paramref name="b"/>.
        /// Node <paramref name="from"/> must reach column <paramref name="to"/> (−1: no such link). Every pick is
        /// weighted among the picks that still let the rest of the row be linked, and a pick with one answer draws
        /// nothing.
        /// </summary>
        private static (int Lo, int Hi)[] Runs(int a, int b, int from, int to, IRng rng)
        {
            var link = new RowLink(a, b, from, to);
            var runs = new (int Lo, int Hi)[a];
            int start = 0;
            for (int i = 0; i < a; i++)
            {
                var choices = link.Choices(i, start);
                if (choices.Count == 0)
                    throw new InvalidOperationException($"a row of {a} cannot be linked to a row of {b} with node {from} on column {to}");
                var pick = choices.Count == 1 ? choices[0] : Weighted(choices, rng);
                runs[i] = (start, pick.Hi);
                start = pick.NextLo;
            }
            return runs;
        }

        /// <summary>Can a row of <paramref name="a"/> be linked to a row of <paramref name="b"/> with node <paramref name="from"/> reaching column <paramref name="to"/>?</summary>
        private static bool Linkable(int a, int b, int from, int to) => new RowLink(a, b, from, to).Choices(0, 0).Count > 0;

        /// <summary>The picks open to each node of a row being linked to the next, given where its run starts.</summary>
        private sealed class RowLink
        {
            private readonly int _a, _b, _from, _to, _cap;
            private readonly Dictionary<(int, int), bool> _feasible = new Dictionary<(int, int), bool>();

            public RowLink(int a, int b, int from, int to)
            {
                _a = a;
                _b = b;
                _from = from;
                _to = to;
                _cap = a == 1 ? b : LayerMapSpec.MaxBranches;
            }

            /// <summary>Node <paramref name="i"/>'s picks when its run starts at <paramref name="lo"/>: where it ends, where the next run starts.</summary>
            public List<(int Hi, int NextLo, int Weight)> Choices(int i, int lo)
            {
                var list = new List<(int, int, int)>();
                if (i == _from && lo > _to) return list;
                for (int hi = lo; hi <= Math.Min(lo + _cap - 1, _b - 1); hi++)
                {
                    if (i == _from && hi < _to) continue;
                    int weight = ReachWeight[Math.Min(hi - lo + 1, ReachWeight.Length - 1)];
                    if (i == _a - 1)
                    {
                        if (hi == _b - 1) list.Add((hi, -1, weight));
                        continue;
                    }
                    if (Feasible(i + 1, hi)) list.Add((hi, hi, weight * SharedStartWeight));
                    if (hi + 1 <= _b - 1 && Feasible(i + 1, hi + 1)) list.Add((hi, hi + 1, weight * FreshStartWeight));
                }
                return list;
            }

            private bool Feasible(int i, int lo)
            {
                if (_feasible.TryGetValue((i, lo), out bool known)) return known;
                bool ok = Choices(i, lo).Count > 0;
                _feasible[(i, lo)] = ok;
                return ok;
            }
        }

        private static (int Hi, int NextLo, int Weight) Weighted(List<(int Hi, int NextLo, int Weight)> choices, IRng rng)
        {
            int total = choices.Sum(c => c.Weight);
            int roll = rng.NextInt(0, total);
            foreach (var choice in choices)
            {
                if (roll < choice.Weight) return choice;
                roll -= choice.Weight;
            }
            return choices[choices.Count - 1];
        }
    }
}
