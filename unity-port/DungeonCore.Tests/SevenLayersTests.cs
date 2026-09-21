using DungeonCore;

namespace DungeonCore.Tests
{
    public class SevenLayersTests
    {
        [Test]
        public void ThereAreSevenLayersWithTheCanonNames()
        {
            // vision/world-v1.md §4.
            Assert.That(SevenLayers.All.Count, Is.EqualTo(7));
            Assert.That(SevenLayers.All.Select(l => l.Name), Is.EqualTo(new[]
            {
                "燦光の樹海", "■■の秘跡", "大黒蛇の住処", "輝晶の間", "血染めの死海", "獄雷峡", "果てなき世界",
            }));
        }

        [Test]
        public void TheTableMatchesTheDesignDoc()
        {
            // danjeon_document/seven_layers_v4.md §2.
            Assert.That(SevenLayers.All.Select(l => l.Density), Is.EqualTo(new[] { 1, 1, 2, 3, 4, 5, 7 }));
            Assert.That(SevenLayers.All.Select(l => l.TimeLimit), Is.EqualTo(new[] { 10, 10, 10, 7, 7, 6, 4 }));
            Assert.That(SevenLayers.All.Select(l => l.MapSpec.NodeCount), Is.EqualTo(new[] { 11, 11, 11, 8, 8, 7, 5 }));
            Assert.That(SevenLayers.All.Select(l => l.ShortestPathCost), Is.EqualTo(new[] { 5, 5, 5, 4, 4, 4, 3 }));
        }

        [Test]
        public void NoLayerCanBeSeenInFull()
        {
            // seven_layers_v4.md §2.2: 刻限 always falls one short of the node count.
            foreach (var layer in SevenLayers.All)
            {
                Assert.That(layer.TimeLimit, Is.LessThan(layer.MapSpec.NodeCount), $"layer {layer.Layer}");
            }
        }

        [Test]
        public void ALayerWhoseTimeLimitCoversEveryNodeIsRejected()
        {
            var error = Assert.Throws<ArgumentException>(() => new LayerProfile(
                1, "test", null, density: 1, timeLimit: 6, LayerMapSpec.Minimal(1)));
            Assert.That(error!.Message, Does.Contain("out of reach"));
        }

        [Test]
        public void RunningStraightDownCosts89Percent()
        {
            // seven_layers_v4.md §3.1. 1×5 + 1×5 + 2×5 + 3×4 + 4×4 + 5×4 + 7×3.
            Assert.That(SevenLayers.BareMinimumMiasma(), Is.EqualTo(89));
            Assert.That(Miasma.MaxStamina(0, 89), Is.EqualTo(6));
        }

        [Test]
        public void SpendingEveryTimeUnitWouldCost147Percent()
        {
            // Well past 100%, which is why no life sees all seven layers in full.
            Assert.That(SevenLayers.FullSpendMiasma(), Is.EqualTo(147));
        }

        [Test]
        public void TheMaskBuysBackTwentyPointsOnTheStraightRoute()
        {
            // seven_layers_v4.md §3.2: 防瘴の面 lowers density by 1, floored at 1.
            Assert.That(SevenLayers.BareMinimumMiasma(densityRelief: 1), Is.EqualTo(69));
            Assert.That(SevenLayers.FullSpendMiasma(densityRelief: 1), Is.EqualTo(113));
        }

        [Test]
        public void TwoMasksLetASurvivorSpendEveryTimeUnit()
        {
            // The 生存者ボーナス gives a fourth tool slot for one life (concept-v3.md §14).
            Assert.That(SevenLayers.BareMinimumMiasma(densityRelief: 2), Is.EqualTo(54));
            Assert.That(SevenLayers.FullSpendMiasma(densityRelief: 2), Is.EqualTo(89));
        }

        [Test]
        public void LayerTwoHidesItsNameUntilTheCarvingIsFound()
        {
            // world-v1.md §5.1.
            var sacrament = SevenLayers.Of(SevenLayers.SacramentLayer);
            Assert.That(sacrament.DisplayName(clueTaken: false), Is.EqualTo("■■の秘跡"));
            Assert.That(sacrament.DisplayName(clueTaken: true), Is.EqualTo("竜神の秘跡"));
        }

        [Test]
        public void OnlyLayerTwoCarriesTheCarving()
        {
            foreach (var layer in SevenLayers.All)
            {
                int expected = layer.Layer == SevenLayers.SacramentLayer ? 1 : 0;
                Assert.That(layer.Map(7UL).CountOf(NodeKind.Carving), Is.EqualTo(expected), $"layer {layer.Layer}");
            }
        }

        [Test]
        public void TheCarvingIsADetourAndChangesNoCombatValue()
        {
            // world-v1.md §5.2: picking the clue up buys the name and nothing else.
            var sacrament = SevenLayers.Of(SevenLayers.SacramentLayer);
            for (ulong seed = 0; seed < 200; seed++)
            {
                var map = sacrament.Map(seed);
                var carving = map.Nodes.Single(n => n.Kind == NodeKind.Carving);
                Assert.That(carving.Kind.IsDetour(), Is.True);
                Assert.That(carving.Row, Is.Not.Zero);
                Assert.That(carving.Row, Is.Not.EqualTo(map.RowCount - 1));

                var start = ExplorationReducer.Enter(sacrament, map, RunLoadout.Empty, hp: 20, maxHp: 30, stamina: 4);
                var stepped = StepOnto(start, carving.Id);
                if (stepped == null) continue;

                var (before, after) = stepped.Value;
                Assert.That(after.CurrentNodeId, Is.EqualTo(carving.Id));
                Assert.That(after.Hp, Is.EqualTo(before.Hp), $"seed {seed}");
                Assert.That(after.TempMaxStaminaMod, Is.EqualTo(before.TempMaxStaminaMod), $"seed {seed}");
                Assert.That(after.TrainingMarks, Is.EqualTo(before.TrainingMarks), $"seed {seed}");

                // It costs exactly what any other node costs: one 刻限 and one layer's 瘴気.
                Assert.That(after.TimeLeft, Is.EqualTo(before.TimeLeft - 1));
                Assert.That(after.MiasmaPercent, Is.EqualTo(before.MiasmaPercent + sacrament.Density));
            }
        }

        [Test]
        public void EverySeedGivesEveryLayerAValidMap()
        {
            foreach (var layer in SevenLayers.All)
            {
                for (ulong seed = 0; seed < 150; seed++)
                {
                    Assert.DoesNotThrow(() => layer.Map(seed), $"layer {layer.Layer}, seed {seed}");
                }
            }
        }

        [Test]
        public void TheRootAndTheRiftSitOnLayerSeven()
        {
            // seven_layers_v4.md §6: moved down from layer 5.
            Assert.That(SevenLayers.RootLayer, Is.EqualTo(7));
            Assert.That(SevenLayers.Of(SevenLayers.RootLayer).IsLast, Is.True);
            Assert.That(SevenLayers.Of(6).IsLast, Is.False);
        }

        [Test]
        public void AskingForALayerOutsideTheLairThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SevenLayers.Of(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SevenLayers.Of(8));
        }

        /// <summary>
        /// Walks to the target and reports the state either side of the final step, so a test
        /// can read what standing on that one node did and nothing else.
        /// </summary>
        private static (ExplorationState Before, ExplorationState After)? StepOnto(ExplorationState start, int target)
        {
            var route = RouteTo(start.Map, start.CurrentNodeId, target);
            var state = start;
            foreach (int id in route)
            {
                if (state.Phase != RunPhase.Exploring) return null;
                var before = state;
                state = ExplorationReducer.Step(state, id, RestChoice.Train);
                if (id == target) return (before, state);
            }
            return null;
        }

        private static IReadOnlyList<int> RouteTo(LayerMap map, int from, int to)
        {
            var previous = new Dictionary<int, int>();
            var queue = new Queue<int>();
            queue.Enqueue(from);
            var seen = new HashSet<int> { from };
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == to) break;
                foreach (int next in map.Neighbours(current))
                {
                    if (!seen.Add(next)) continue;
                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }

            var route = new List<int>();
            for (int at = to; at != from; at = previous[at]) route.Add(at);
            route.Reverse();
            return route;
        }
    }
}
