using DungeonCore;

namespace DungeonCore.Tests
{
    /// <summary>
    /// #1049: layers 1〜6 draw 7〜9 rows, mix rows of different widths, and mix nodes that lead to 1, 2 and 3 others
    /// (seven_layers_v4.md §3.2 and §7). The king room keeps its 1,4,1.
    /// </summary>
    public class TangledMapTests
    {
        private static readonly ulong[] Seeds = Enumerable.Range(0, 1000).Select(i => (ulong)i).ToArray();

        private static IEnumerable<LayerProfile> DrawnLayers => Lair.Profiles.Where(p => !p.IsKingLayer);

        /// <summary>One profile of each distinct map spec: layer 1, layer 2, a layer 3 and a deep layer.</summary>
        private static IEnumerable<LayerProfile> OneOfEach => new[]
        {
            Lair.Of(1), Lair.Of(2), Lair.Of(3, Region.Dragon), Lair.Of(5, Region.Ghost),
        };

        [Test]
        public void EveryDrawnLayer_TakesSevenToNineNodesFromTheEntryToTheBoss_AndEachCountTurnsUp()
        {
            foreach (var profile in OneOfEach)
            {
                var seen = new HashSet<int>();
                foreach (ulong seed in Seeds)
                {
                    var map = profile.Map(seed);
                    Assert.That(map.ShortestPathCost, Is.InRange(7, 9), $"{profile} seed {seed}");
                    Assert.That(ShortestWalk(map), Is.EqualTo(map.RowCount), $"{profile} seed {seed}");
                    seen.Add(map.RowCount);
                }
                Assert.That(seen, Is.EquivalentTo(new[] { 7, 8, 9 }), profile.ToString());
            }
        }

        [Test]
        public void NodesPastTheEntry_LeadToOneTwoOrThreeNodes_NeverFour()
        {
            foreach (var profile in OneOfEach)
            {
                var ways = new HashSet<int>();
                foreach (ulong seed in Seeds)
                {
                    var map = profile.Map(seed);
                    foreach (var node in map.Nodes)
                    {
                        int n = map.Successors(node.Id).Count;
                        Assert.That(n, Is.LessThanOrEqualTo(LayerMapSpec.MaxBranches), $"{profile} seed {seed} node {node.Id}");
                        if (node.Id != map.EntryId && node.Id != map.BossId) ways.Add(n);
                    }
                }
                Assert.That(ways, Is.EquivalentTo(new[] { 1, 2, 3 }), profile.ToString());
            }
        }

        [Test]
        public void MiddleRowsMixTheirWidths_AndForksReachLeftAsWellAsRight()
        {
            foreach (var profile in OneOfEach)
            {
                int mixed = 0, left = 0, right = 0;
                foreach (ulong seed in Seeds)
                {
                    var map = profile.Map(seed);
                    var widths = Enumerable.Range(1, map.RowCount - 2).Select(r => map.Row(r).Count).Distinct().Count();
                    if (widths >= 2) mixed++;

                    for (int r = 1; r < map.RowCount - 1; r++)
                    {
                        int a = map.Row(r).Count, b = map.Row(r + 1).Count;
                        foreach (var node in map.Row(r))
                        {
                            var columns = map.Successors(node.Id).Select(id => map.Node(id).Column).ToList();
                            if (columns.Count < 2) continue;
                            // The run the old generator started from (MapGenerator before #1049): it could only widen it
                            // to the right.
                            int baseLo = node.Column * b / a;
                            int baseHi = Math.Max(baseLo, ((node.Column + 1) * b + a - 1) / a - 1);
                            if (columns.Min() < baseLo) left++;
                            if (columns.Max() > baseHi) right++;
                        }
                    }
                }
                Assert.That(mixed, Is.GreaterThan(Seeds.Length / 2), $"{profile}: maps whose middle rows mix widths");
                Assert.That(left, Is.GreaterThan(0), $"{profile}: forks that reach left");
                Assert.That(right, Is.GreaterThan(0), $"{profile}: forks that reach right");
            }
        }

        [Test]
        public void TheKingRoom_KeepsItsShape_AndDrawsTheSameMapsAsBefore()
        {
            // Fingerprints written by the generator before #1049: the king room's draws are unchanged, so a save in a
            // king room still names the same map.
            var before = new Dictionary<ulong, string>
            {
                [1] = "L7|0:0,0,battle->1.2.3.4;1:1,0,battle->5;2:1,1,event->5;3:1,2,elite->5;4:1,3,rest->5;5:2,0,boss->;",
                [7] = "L7|0:0,0,battle->1.2.3.4;1:1,0,event->5;2:1,1,battle->5;3:1,2,elite->5;4:1,3,rest->5;5:2,0,boss->;",
                [42] = "L7|0:0,0,battle->1.2.3.4;1:1,0,rest->5;2:1,1,battle->5;3:1,2,event->5;4:1,3,elite->5;5:2,0,boss->;",
                [2026] = "L7|0:0,0,battle->1.2.3.4;1:1,0,elite->5;2:1,1,rest->5;3:1,2,battle->5;4:1,3,event->5;5:2,0,boss->;",
            };
            foreach (Region region in Lair.Regions)
            {
                var king = Lair.Of(Lair.KingLayer, region);
                Assert.That(king.MapSpec.Shapes.Single().RowWidths, Is.EqualTo(new[] { 1, 4, 1 }), region.ToString());
                foreach (var pair in before)
                {
                    Assert.That(king.Map(pair.Key).Fingerprint(), Is.EqualTo(pair.Value), $"{region} seed {pair.Key}");
                }
            }
        }

        [Test]
        public void TheCountedKinds_StayAsTheyWere_OnEveryLayerAndEveryShape()
        {
            // Rests, the elite, the survey and the carving are what they were before #1049; the longer maps add battles
            // and events only.
            foreach (var profile in Lair.Profiles)
            {
                foreach (var shape in profile.MapSpec.Shapes)
                {
                    int Of(NodeKind kind) => shape.Quotas.TryGetValue(kind, out int n) ? n : 0;
                    string at = $"{profile} {shape.RowCount} rows";
                    Assert.That(Of(NodeKind.Elite), Is.EqualTo(1), at);
                    Assert.That(Of(NodeKind.Rest), Is.EqualTo(profile.IsKingLayer ? 1 : 2), at);
                    Assert.That(Of(NodeKind.Survey), Is.EqualTo(profile.IsKingLayer ? 0 : 1), at);
                    Assert.That(Of(NodeKind.Carving), Is.EqualTo(profile.Layer == Lair.SacramentLayer ? 1 : 0), at);
                    Assert.That(Of(NodeKind.Trace), Is.Zero, at);
                }
            }
        }

        [Test]
        public void TheRulesThatDidNotChange_HoldOnEveryDrawnLayer()
        {
            foreach (var profile in DrawnLayers)
            {
                foreach (var spec in new[] { profile.MapSpec, Heritage.WithTrace(profile.MapSpec) })
                {
                    foreach (ulong seed in Seeds.Take(300))
                    {
                        var map = MapGenerator.Generate(spec, seed);
                        string at = $"{profile} seed {seed}";
                        Assert.That(map.Node(map.EntryId).Kind, Is.EqualTo(NodeKind.Battle), at);
                        Assert.That(map.Node(map.BossId).Row, Is.EqualTo(map.RowCount - 1), at);
                        Assert.That(map.Row(map.RowCount - 1).Single().Kind, Is.EqualTo(NodeKind.Boss), at);
                        foreach (var node in map.Nodes)
                        {
                            if (node.Id != map.BossId) Assert.That(map.Successors(node.Id), Is.Not.Empty, $"{at}: node {node.Id} is a dead end");
                            if (node.Id != map.EntryId) Assert.That(map.Predecessors(node.Id), Is.Not.Empty, $"{at}: node {node.Id} cannot be entered");
                        }
                        Assert.That(Reach(map, map.EntryId, n => true).Count, Is.EqualTo(map.NodeCount), $"{at}: a node is out of reach");
                        Assert.That(Reach(map, map.EntryId, n => !n.Kind.IsDetour()), Does.Contain(map.BossId),
                            $"{at}: the boss cannot be reached without a detour");
                        Assert.That(MapGenerator.Generate(spec, seed).Fingerprint(), Is.EqualTo(map.Fingerprint()), $"{at}: drifted");
                    }
                }
            }
        }

        [Test]
        public void EveryShapeOfALayer_IsCountedOutInItsTable()
        {
            // seven_layers_v4.md §7: (layer, rows) → nodes, battles (the entry counted), events.
            var table = new (int Layer, int Rows, int Nodes, int Battles, int Events)[]
            {
                (1, 7, 17, 8, 4), (1, 8, 20, 10, 5), (1, 9, 23, 12, 6),
                (2, 7, 17, 7, 4), (2, 8, 20, 9, 5), (2, 9, 23, 11, 6),
                (3, 7, 17, 8, 4), (3, 8, 20, 10, 5), (3, 9, 23, 12, 6),
                (4, 7, 12, 5, 2), (4, 8, 14, 6, 3), (4, 9, 16, 8, 3),
            };
            foreach (var row in table)
            {
                foreach (int layer in row.Layer == 4 ? new[] { 4, 5, 6 } : new[] { row.Layer })
                {
                    var profile = layer < Lair.FirstRegionLayer ? Lair.Of(layer) : Lair.Of(layer, Region.Parasite);
                    var shape = profile.MapSpec.ShapeOf(row.Rows)!;
                    Assert.That(shape.NodeCount, Is.EqualTo(row.Nodes), $"layer {layer} {row.Rows} rows");
                    Assert.That(shape.Quotas[NodeKind.Battle] + 1, Is.EqualTo(row.Battles), $"layer {layer} {row.Rows} rows");
                    Assert.That(shape.Quotas[NodeKind.Event], Is.EqualTo(row.Events), $"layer {layer} {row.Rows} rows");
                }
            }
        }

        private static int ShortestWalk(LayerMap map)
        {
            var depth = new Dictionary<int, int> { [map.EntryId] = 1 };
            var queue = new Queue<int>();
            queue.Enqueue(map.EntryId);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int id in map.Successors(current))
                {
                    if (depth.ContainsKey(id)) continue;
                    depth[id] = depth[current] + 1;
                    queue.Enqueue(id);
                }
            }
            return depth[map.BossId];
        }

        private static HashSet<int> Reach(LayerMap map, int from, Func<MapNode, bool> through)
        {
            var seen = new HashSet<int> { from };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                foreach (int id in map.Successors(queue.Dequeue()))
                {
                    if (!through(map.Node(id)) || !seen.Add(id)) continue;
                    queue.Enqueue(id);
                }
            }
            return seen;
        }
    }
}
