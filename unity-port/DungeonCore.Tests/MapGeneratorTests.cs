using DungeonCore;

namespace DungeonCore.Tests
{
    public class MapGeneratorTests
    {
        private static readonly ulong[] Seeds = Enumerable.Range(0, 400).Select(i => (ulong)i).ToArray();

        [Test]
        public void SameSeed_SameMap()
        {
            var spec = LayerMapSpec.Full(3);
            foreach (ulong seed in Seeds.Take(120))
            {
                var first = MapGenerator.Generate(spec, seed);
                var second = MapGenerator.Generate(spec, seed);
                Assert.That(second.Fingerprint(), Is.EqualTo(first.Fingerprint()), $"seed {seed} drifted");
            }
        }

        [Test]
        public void DifferentSeeds_GiveDifferentMaps()
        {
            var spec = LayerMapSpec.Full(3);
            var shapes = Seeds.Select(s => MapGenerator.Generate(spec, s).Fingerprint()).ToHashSet();
            // Not every seed has to differ, but a generator that ignored its seed would collapse to one.
            Assert.That(shapes.Count, Is.GreaterThan(20));
        }

        [Test]
        public void EverySeed_ProducesAValidMap()
        {
            foreach (var spec in new[] { LayerMapSpec.Minimal(1), LayerMapSpec.Full(4) })
            {
                foreach (ulong seed in Seeds)
                {
                    Assert.DoesNotThrow(() => MapGenerator.Generate(spec, seed), $"seed {seed} failed validation");
                }
            }
        }

        [Test]
        public void NoNodeIsUnreachable()
        {
            var spec = LayerMapSpec.Full(2);
            foreach (ulong seed in Seeds)
            {
                var map = MapGenerator.Generate(spec, seed);
                var fromEntry = Walk(map, map.EntryId, forward: true);
                var toBoss = Walk(map, map.BossId, forward: false);

                foreach (var node in map.Nodes)
                {
                    Assert.That(fromEntry, Does.Contain(node.Id), $"seed {seed}: node {node.Id} is cut off from the entry");
                    Assert.That(toBoss, Does.Contain(node.Id), $"seed {seed}: node {node.Id} cannot reach the boss");
                }
            }
        }

        [Test]
        public void TheEntryIsABattleAndTheLastRowIsTheBoss()
        {
            var spec = LayerMapSpec.Full(5);
            foreach (ulong seed in Seeds.Take(100))
            {
                var map = MapGenerator.Generate(spec, seed);
                Assert.That(map.Node(map.EntryId).Kind, Is.EqualTo(NodeKind.Battle));
                Assert.That(map.Node(map.EntryId).Row, Is.Zero);
                Assert.That(map.CountOf(NodeKind.Boss), Is.EqualTo(1));
                Assert.That(map.Node(map.BossId).Row, Is.EqualTo(map.RowCount - 1));
                Assert.That(map.Successors(map.BossId), Is.Empty);
            }
        }

        [Test]
        public void TheShortestRouteCostsOneTimeUnitPerRow()
        {
            var spec = LayerMapSpec.Full(6);
            foreach (ulong seed in Seeds.Take(100))
            {
                var map = MapGenerator.Generate(spec, seed);
                Assert.That(BreadthFirstDepth(map), Is.EqualTo(map.RowCount));
                Assert.That(map.ShortestPathCost, Is.EqualTo(map.RowCount));
            }
        }

        [Test]
        public void EveryMapHasAFork()
        {
            var spec = LayerMapSpec.Full(1);
            foreach (ulong seed in Seeds.Take(100))
            {
                var map = MapGenerator.Generate(spec, seed);
                Assert.That(map.Nodes.Any(n => map.Successors(n.Id).Count >= 2), Is.True, $"seed {seed} has no fork");
            }
        }

        [Test]
        public void ADetourIsNeverForced()
        {
            var spec = LayerMapSpec.Full(4).With(NodeKind.Trace, 1);
            foreach (ulong seed in Seeds.Take(200))
            {
                var map = MapGenerator.Generate(spec, seed);
                for (int row = 1; row < map.RowCount - 1; row++)
                {
                    Assert.That(map.Row(row).Any(n => !n.Kind.IsDetour()), Is.True,
                        $"seed {seed}: row {row} offers nothing but detours");
                }
            }
        }

        [Test]
        public void TenTimeUnitsCannotTakeEveryNodeOfAFullLayer()
        {
            // dungeon_exploration_v4.md §3.1 asks for a layer the 刻限 budget cannot exhaust.
            var spec = LayerMapSpec.Full(1);
            Assert.That(spec.FullSweepCost, Is.GreaterThan(10));
            Assert.That(spec.ShortestPathCost, Is.EqualTo(5));
        }

        [Test]
        public void TheMinimalLayerIsTheOneInTheDesignDoc()
        {
            // dungeon_exploration_v4.md §4.1 / §4.2: six nodes, four rows, cleared in four 刻限,
            // both sides of both forks taken in six.
            var spec = LayerMapSpec.Minimal();
            Assert.That(spec.NodeCount, Is.EqualTo(6));
            Assert.That(spec.RowCount, Is.EqualTo(4));
            Assert.That(spec.ShortestPathCost, Is.EqualTo(4));
            Assert.That(spec.FullSweepCost, Is.EqualTo(6));

            var map = MapGenerator.Generate(spec, 2026UL);
            Assert.That(map.CountOf(NodeKind.Battle), Is.EqualTo(2));
            Assert.That(map.CountOf(NodeKind.Elite), Is.EqualTo(1));
            Assert.That(map.CountOf(NodeKind.Rest), Is.EqualTo(1));
            Assert.That(map.CountOf(NodeKind.Survey), Is.EqualTo(1));
            Assert.That(map.CountOf(NodeKind.Boss), Is.EqualTo(1));
        }

        [Test]
        public void BothSidesOfEveryForkCanBeTakenByWalkingBack()
        {
            // Stepping back over a node that is already resolved is free, so a run can resolve
            // every node in NodeCount 刻限. That holds only if the map is one connected piece.
            foreach (var spec in new[] { LayerMapSpec.Minimal(), LayerMapSpec.Full(3) })
            {
                foreach (ulong seed in Seeds.Take(150))
                {
                    var map = MapGenerator.Generate(spec, seed);
                    var resolved = new HashSet<int> { map.EntryId };
                    bool grew = true;
                    while (grew)
                    {
                        grew = false;
                        foreach (int id in resolved.ToList())
                        {
                            foreach (int next in map.Neighbours(id))
                            {
                                if (resolved.Add(next)) grew = true;
                            }
                        }
                    }
                    Assert.That(resolved.Count, Is.EqualTo(map.NodeCount),
                        $"seed {seed}: only {resolved.Count} of {map.NodeCount} nodes can ever be resolved");
                }
            }
        }

        [Test]
        public void QuotasAreHonoured()
        {
            var spec = LayerMapSpec.Full(2);
            foreach (ulong seed in Seeds.Take(100))
            {
                var map = MapGenerator.Generate(spec, seed);
                foreach (var pair in spec.Quotas)
                {
                    int expected = pair.Key == NodeKind.Battle ? pair.Value + 1 : pair.Value;
                    Assert.That(map.CountOf(pair.Key), Is.EqualTo(expected), $"seed {seed}: {pair.Key}");
                }
            }
        }

        [Test]
        public void LinksNeverCross()
        {
            var spec = LayerMapSpec.Full(7);
            foreach (ulong seed in Seeds.Take(150))
            {
                var map = MapGenerator.Generate(spec, seed);
                for (int row = 0; row < map.RowCount - 1; row++)
                {
                    var nodes = map.Row(row);
                    for (int i = 0; i + 1 < nodes.Count; i++)
                    {
                        var left = map.Successors(nodes[i].Id).Select(t => map.Node(t).Column).ToList();
                        var right = map.Successors(nodes[i + 1].Id).Select(t => map.Node(t).Column).ToList();
                        Assert.That(left.Min(), Is.LessThanOrEqualTo(right.Min()), $"seed {seed} row {row}");
                        Assert.That(left.Max(), Is.LessThanOrEqualTo(right.Max()), $"seed {seed} row {row}");
                    }
                }
            }
        }

        private static HashSet<int> Walk(LayerMap map, int from, bool forward)
        {
            var seen = new HashSet<int> { from };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int id in forward ? map.Successors(current) : map.Predecessors(current))
                {
                    if (seen.Add(id)) queue.Enqueue(id);
                }
            }
            return seen;
        }

        private static int BreadthFirstDepth(LayerMap map)
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
    }
}
