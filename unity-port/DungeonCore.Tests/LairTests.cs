using DungeonCore;

namespace DungeonCore.Tests
{
    /// <summary>The lair of seven_layers_v4.md §3 and §7: two shared layers, three regions, three king rooms (#888).</summary>
    public class LairTests
    {
        [Test]
        public void ThereAreSeventeenDistinctLayers()
        {
            // 2 shared + 3 regions × (layers 3〜6 + the king room).
            Assert.That(Lair.Profiles.Count, Is.EqualTo(17));
            Assert.That(Lair.Profiles.Distinct().Count(), Is.EqualTo(17));
            Assert.That(Lair.Profiles.Select(p => (p.Layer, p.Region)).Distinct().Count(), Is.EqualTo(17));
            Assert.That(Lair.Regions, Is.EqualTo(new[] { Region.Dragon, Region.Parasite, Region.Ghost }));
        }

        [Test]
        public void EveryRouteRunsSevenLayersAndThirtyShortestDaysToTheKing()
        {
            foreach (Region region in Lair.Regions)
            {
                var route = Lair.Route(region);
                Assert.That(route.Select(p => p.Layer), Is.EqualTo(Enumerable.Range(1, 7)), region.ToString());
                // §3.2: 5 + 5 + 5 + 4 + 4 + 4 + 3.
                Assert.That(route.Select(p => p.ShortestPathCost), Is.EqualTo(new[] { 5, 5, 5, 4, 4, 4, 3 }), region.ToString());
                Assert.That(route.Sum(p => p.ShortestPathCost), Is.EqualTo(30), region.ToString());
                // §3.2: every node of a route is 63 days.
                Assert.That(route.Select(p => p.MapSpec.NodeCount), Is.EqualTo(new[] { 11, 11, 11, 8, 8, 8, 6 }), region.ToString());
                Assert.That(route.Sum(p => p.MapSpec.NodeCount), Is.EqualTo(63), region.ToString());
                Assert.That(route.Take(2).All(p => p.Region == Region.Common), Is.True);
                Assert.That(route.Skip(2).All(p => p.Region == region), Is.True);
                Assert.That(route.Last().IsKingLayer, Is.True);
                Assert.That(route.Take(6).Any(p => p.IsKingLayer || p.IsLast), Is.False);
            }
        }

        [Test]
        public void TheRowWidthsAreTheTable()
        {
            foreach (var profile in Lair.Profiles)
            {
                int[] expected = profile.Layer <= 3 ? new[] { 1, 3, 3, 3, 1 }
                    : profile.Layer <= 6 ? new[] { 1, 3, 3, 1 }
                    : new[] { 1, 4, 1 };
                Assert.That(profile.MapSpec.RowWidths, Is.EqualTo(expected), profile.ToString());
            }
        }

        // §7: battle (the entry counted), elite, rest, survey, event, carving, boss.
        [TestCase(1, 4, 1, 2, 1, 2, 0)]
        [TestCase(2, 3, 1, 2, 1, 2, 1)]
        [TestCase(3, 4, 1, 2, 1, 2, 0)]
        [TestCase(4, 3, 1, 2, 1, 0, 0)]
        [TestCase(5, 3, 1, 2, 1, 0, 0)]
        [TestCase(6, 3, 1, 2, 1, 0, 0)]
        [TestCase(7, 2, 1, 1, 0, 1, 0)]
        public void EveryRegionHasTheSameNodeMix(int layer, int battles, int elites, int rests, int surveys, int events, int carvings)
        {
            foreach (Region region in Lair.Regions)
            {
                var map = Lair.Of(layer, region).Map(7UL);
                string at = $"{region} {layer}";
                Assert.That(map.CountOf(NodeKind.Battle), Is.EqualTo(battles), at);
                Assert.That(map.CountOf(NodeKind.Elite), Is.EqualTo(elites), at);
                Assert.That(map.CountOf(NodeKind.Rest), Is.EqualTo(rests), at);
                Assert.That(map.CountOf(NodeKind.Survey), Is.EqualTo(surveys), at);
                Assert.That(map.CountOf(NodeKind.Event), Is.EqualTo(events), at);
                Assert.That(map.CountOf(NodeKind.Carving), Is.EqualTo(carvings), at);
                Assert.That(map.CountOf(NodeKind.Boss), Is.EqualTo(1), at);
            }
        }

        [Test]
        public void EveryLayerOffersTwoNodesThatMoveMaxStamina()
        {
            // requirements/tier2-support.md R2-4, counted the way seven_layers_v4.md §7.1 counts. A rest always can
            // (+2). An event only counts in a king room, whose event draw leaves out 泉 (encounters_and_rewards_v4.md §5).
            foreach (var layer in Lair.Profiles)
            {
                var map = layer.Map(7UL);
                int rests = map.CountOf(NodeKind.Rest);
                int events = layer.IsKingLayer ? map.CountOf(NodeKind.Event) : 0;
                Assert.That(rests + events, Is.GreaterThanOrEqualTo(2), layer.ToString());
            }
        }

        [Test]
        public void TheKingRoomWidensItsOneMiddleRow()
        {
            var middle = Lair.Of(7, Region.Ghost).Map(7UL).Row(1).Select(n => n.Kind);
            Assert.That(middle, Is.EquivalentTo(new[] { NodeKind.Battle, NodeKind.Elite, NodeKind.Rest, NodeKind.Event }));
        }

        [TestCase(Region.Dragon, new[] { 1, 2, 3, 3, 6, 6, 7 })]
        [TestCase(Region.Parasite, new[] { 1, 2, 3, 3, 6, 6, 7 })]
        [TestCase(Region.Ghost, new[] { 1, 2, 4, 4, 5, 5, 7 })]
        public void EachLayerBorrowsTheBackdropOfTheNearestNamedLayer(Region region, int[] backdrops)
        {
            // §3.2 / §3.4. The parasite region has no art yet and stands in on the dragon's two backdrops (#891), and
            // every king room is drawn on the void's.
            Assert.That(Lair.Route(region).Select(p => p.BackdropLayer), Is.EqualTo(backdrops));
        }

        [Test]
        public void TheNamedLayersSitWhereTheTablePutsThem()
        {
            Assert.That(Lair.Route(Region.Dragon).Select(p => p.Name), Is.EqualTo(new[]
            {
                "燦光の樹海", "■■の秘跡", "大黒蛇の住処", "竜の領域 第 4 層", "竜の領域 第 5 層", "獄雷峡", "竜王の間",
            }));
            Assert.That(Lair.Route(Region.Parasite).Select(p => p.Name), Is.EqualTo(new[]
            {
                "燦光の樹海", "■■の秘跡", "蔓絡みの獣窟", "寄生の領域 第 4 層", "寄生の領域 第 5 層", "血染めの腐林", "爛れた世界樹の間",
            }));
            Assert.That(Lair.Route(Region.Ghost).Select(p => p.Name), Is.EqualTo(new[]
            {
                "燦光の樹海", "■■の秘跡", "亡霊の領域 第 3 層", "輝晶の間", "暗き死海", "亡霊の領域 第 6 層", "死霊王の間",
            }));
        }

        [Test]
        public void LayerTwoHidesItsNameUntilTheCarvingIsFound()
        {
            // world-v1.md §5.1 / §5.3: the true name is the dragon god's, 龍神, not 竜神.
            var sacrament = Lair.Of(Lair.SacramentLayer);
            Assert.That(sacrament.DisplayName(clueTaken: false), Is.EqualTo("■■の秘跡"));
            Assert.That(sacrament.DisplayName(clueTaken: true), Is.EqualTo("龍神の秘跡"));
            Assert.That(Lair.Profiles.Count(p => p.TrueName != null), Is.EqualTo(1));
        }

        [Test]
        public void OnlyLayerTwoCarriesTheCarving()
        {
            foreach (var layer in Lair.Profiles)
            {
                int expected = layer.Layer == Lair.SacramentLayer ? 1 : 0;
                Assert.That(layer.Map(7UL).CountOf(NodeKind.Carving), Is.EqualTo(expected), layer.ToString());
            }
        }

        [Test]
        public void TheCarvingIsADetourAndCostsADayLikeAnyNode()
        {
            // world-v1.md §5.2: picking the clue up buys the name and nothing else.
            var sacrament = Lair.Of(Lair.SacramentLayer);
            int checkedSeeds = 0;
            for (ulong seed = 0; seed < 200; seed++)
            {
                var map = sacrament.Map(seed);
                var carving = map.Nodes.Single(n => n.Kind == NodeKind.Carving);
                Assert.That(carving.Kind.IsDetour(), Is.True);
                Assert.That(carving.Row, Is.Not.Zero);
                Assert.That(carving.Row, Is.Not.EqualTo(map.RowCount - 1));

                var start = ExplorationReducer.Enter(sacrament, map, RunLoadout.Empty, hp: 20, maxHp: 30, stamina: 4, CalendarState.Fresh);
                var stepped = StepOnto(start, carving.Id);
                if (stepped == null) continue;
                checkedSeeds++;

                var (before, after) = stepped.Value;
                Assert.That(after.CurrentNodeId, Is.EqualTo(carving.Id));
                Assert.That(after.Hp, Is.EqualTo(before.Hp), $"seed {seed}");
                Assert.That(after.TempMaxStaminaMod, Is.EqualTo(before.TempMaxStaminaMod), $"seed {seed}");
                Assert.That(after.TrainingMarks, Is.EqualTo(before.TrainingMarks), $"seed {seed}");
                Assert.That(after.DaysLeft, Is.EqualTo(before.DaysLeft - 1), $"seed {seed}");
            }
            Assert.That(checkedSeeds, Is.GreaterThan(0));
        }

        [Test]
        public void EverySeedGivesEveryLayerAValidMap()
        {
            foreach (var layer in Lair.Profiles)
            {
                for (ulong seed = 0; seed < 100; seed++)
                {
                    Assert.DoesNotThrow(() => layer.Map(seed), $"{layer}, seed {seed}");
                }
            }
        }

        [Test]
        public void TheSharedLayersIgnoreTheRegion()
        {
            foreach (Region region in Enum.GetValues(typeof(Region)))
            {
                Assert.That(Lair.Of(1, region), Is.SameAs(Lair.Of(1)));
                Assert.That(Lair.Of(2, region), Is.SameAs(Lair.Of(2)));
            }
            Assert.That(Lair.Of(1).Region, Is.EqualTo(Region.Common));
        }

        [Test]
        public void ADeepLayerNeedsARegion()
        {
            Assert.Throws<ArgumentException>(() => Lair.Of(3));
            Assert.Throws<ArgumentException>(() => Lair.Of(7, Region.Common));
            Assert.Throws<ArgumentException>(() => Lair.Route(Region.Common));
            Assert.That(Lair.Of(5, Region.Ghost).Name, Is.EqualTo("暗き死海"));
        }

        [Test]
        public void AskingForALayerOutsideTheLairThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Lair.Of(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Lair.Of(8, Region.Dragon));
            Assert.Throws<ArgumentOutOfRangeException>(() => Lair.LayerLabel(0, false));
        }

        [Test]
        public void ALayerLabelNamesTheLayerOnlyWhereItIsKnown()
        {
            Assert.That(Lair.LayerLabel(1, false), Is.EqualTo("燦光の樹海"));
            Assert.That(Lair.LayerLabel(2, false), Is.EqualTo("■■の秘跡"));
            Assert.That(Lair.LayerLabel(2, true), Is.EqualTo("龍神の秘跡"));
            Assert.That(Lair.LayerLabel(2, true, Region.Ghost), Is.EqualTo("龍神の秘跡"));
            Assert.That(Lair.LayerLabel(4, false), Is.EqualTo("第 4 層"));
            Assert.That(Lair.LayerLabel(4, false, Region.Common), Is.EqualTo("第 4 層"));
            Assert.That(Lair.LayerLabel(4, false, Region.Dragon), Is.EqualTo("竜の領域 第 4 層"));
            Assert.That(Lair.LayerLabel(7, false, Region.Parasite), Is.EqualTo("爛れた世界樹の間"));
        }

        [Test]
        public void AProfileRefusesARegionThatDoesNotFitItsDepth()
        {
            Assert.Throws<ArgumentException>(() => new LayerProfile(1, Region.Dragon, "x", null, 1, LayerMapSpec.Minimal(1)));
            Assert.Throws<ArgumentException>(() => new LayerProfile(3, Region.Common, "x", null, 3, LayerMapSpec.Minimal(3)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LayerProfile(3, Region.Ghost, "x", null, 8, LayerMapSpec.Minimal(3)));
            Assert.Throws<ArgumentException>(() => new LayerProfile(3, Region.Ghost, "x", null, 3, LayerMapSpec.Minimal(4)));
        }

        [Test]
        public void TheRegionsHaveTokensAndLabels()
        {
            Assert.That(Enum.GetValues(typeof(Region)).Cast<Region>().Select(r => r.ToToken()),
                Is.EqualTo(new[] { "common", "dragon", "parasite", "ghost" }));
            Assert.That(Enum.GetValues(typeof(Region)).Cast<Region>().Select(r => r.Label()),
                Is.EqualTo(new[] { "共通", "竜", "寄生", "亡霊" }));
            Assert.That(RegionExtensions.TryParseToken("ghost", out Region ghost) && ghost == Region.Ghost, Is.True);
            Assert.That(RegionExtensions.TryParseToken("Ghost", out _), Is.False);
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
