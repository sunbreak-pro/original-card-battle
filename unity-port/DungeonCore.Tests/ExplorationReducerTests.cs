using DungeonCore;

namespace DungeonCore.Tests
{
    public class ExplorationReducerTests
    {
        // battle_core_v4.md §10: PLAYER_MAX_HP 50.
        private const int PlayerMaxHp = 50;

        private static ExplorationState EnterLayer(int layer, ulong seed = 11UL, RunLoadout? loadout = null, int hp = PlayerMaxHp, int stamina = 10)
        {
            var profile = SevenLayers.Of(layer);
            return ExplorationReducer.Enter(profile, profile.Map(seed), loadout ?? RunLoadout.Empty, hp, PlayerMaxHp, stamina);
        }

        [Test]
        public void EnteringALayerSpendsTheEntryNodesTimeUnit()
        {
            var state = EnterLayer(3);
            Assert.That(state.TimeLeft, Is.EqualTo(SevenLayers.Of(3).TimeLimit - 1));
            Assert.That(state.MiasmaPercent, Is.EqualTo(SevenLayers.Of(3).Density));
            Assert.That(state.HasResolved(state.Map.EntryId), Is.True);
            Assert.That(state.Phase, Is.EqualTo(RunPhase.Exploring));
        }

        [Test]
        public void ANewNodeCostsOneTimeUnitAndOneLayersWorthOfMiasma()
        {
            var before = EnterLayer(5);
            int next = before.ReachableUnresolved().First();
            var after = ExplorationReducer.Step(before, next, RestChoice.Train);

            Assert.That(after.TimeLeft, Is.EqualTo(before.TimeLeft - 1));
            Assert.That(after.MiasmaPercent, Is.EqualTo(before.MiasmaPercent + SevenLayers.Of(5).Density));
        }

        [Test]
        public void WalkingBackOverAResolvedNodeIsFree()
        {
            // dungeon_exploration_v4.md §4.2: this is what lets a run take both sides of a fork.
            var start = EnterLayer(1);
            int entry = start.CurrentNodeId;
            int forward = start.ReachableUnresolved().First();

            var stepped = ExplorationReducer.Step(start, forward, RestChoice.Train);
            var back = ExplorationReducer.Step(stepped, entry);

            Assert.That(back.CurrentNodeId, Is.EqualTo(entry));
            Assert.That(back.TimeLeft, Is.EqualTo(stepped.TimeLeft));
            Assert.That(back.MiasmaPercent, Is.EqualTo(stepped.MiasmaPercent));
        }

        [Test]
        public void BothSidesOfAForkCostOneTimeUnitEach()
        {
            var start = EnterLayer(1);
            int entry = start.CurrentNodeId;
            var fork = start.ReachableUnresolved().Take(2).ToList();
            Assert.That(fork.Count, Is.EqualTo(2), "layer one's entry should fork");

            var left = ExplorationReducer.Step(start, fork[0], RestChoice.Train);
            var home = ExplorationReducer.Step(left, entry);
            var right = ExplorationReducer.Step(home, fork[1], RestChoice.Train);

            Assert.That(right.TimeLeft, Is.EqualTo(start.TimeLeft - 2));
            Assert.That(right.MiasmaPercent, Is.EqualTo(start.MiasmaPercent + 2 * SevenLayers.Of(1).Density));
        }

        [Test]
        public void SteppingSomewhereThatDoesNotTouchTheCurrentNodeThrows()
        {
            var state = EnterLayer(1);
            int far = state.Map.Nodes.Last(n => n.Row == state.Map.RowCount - 1).Id;
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.Step(state, far));
        }

        [Test]
        public void StaminaComesBackByThreeForEveryTimeUnitSpent()
        {
            // concept-v3.md §12-16.
            var start = EnterLayer(1, stamina: 0);
            Assert.That(start.Stamina, Is.EqualTo(Miasma.FieldRecovery));

            var next = ExplorationReducer.Step(start, start.ReachableUnresolved().First(), RestChoice.Train);
            Assert.That(next.Stamina, Is.EqualTo(Math.Min(next.MaxStamina, 2 * Miasma.FieldRecovery)));
        }

        [Test]
        public void StaminaNeverPassesTheMaximumTheGaugeAllows()
        {
            var state = EnterLayer(6, stamina: 10);
            Assert.That(state.Stamina, Is.LessThanOrEqualTo(state.MaxStamina));
        }

        [Test]
        public void RunningOutOfTimeEndsTheLayer()
        {
            // dungeon_exploration_v4.md §4.3.
            // Layer seven holds 刻限 4 over five nodes. Combing the middle row instead of
            // dropping onto the root spends every unit and pushes the run out empty-handed.
            var state = EnterLayer(7, seed: 4UL, stamina: 10);
            int entry = state.CurrentNodeId;
            var middle = state.Map.Row(1).Select(n => n.Id).ToList();
            Assert.That(middle.Count, Is.EqualTo(3));

            foreach (int id in middle)
            {
                state = ExplorationReducer.Step(state, id, RestChoice.Train);
                if (state.Phase != RunPhase.Exploring) break;
                state = ExplorationReducer.Step(state, entry);
            }

            Assert.That(state.TimeLeft, Is.Zero);
            Assert.That(state.Phase, Is.EqualTo(RunPhase.PushedOut));
            Assert.That(state.HasResolved(state.Map.BossId), Is.False, "the root was never reached");

            // Being pushed out still leads to the interlude and the next layer (§4.3).
            Assert.DoesNotThrow(() => ExplorationReducer.Interlude(state));
        }

        [Test]
        public void NoNewNodeCanBeTakenWithoutTimeLeft()
        {
            var state = EnterLayer(7, seed: 4UL);
            var zeroed = state.With(timeLeft: 0);
            int next = zeroed.ReachableUnresolved().First();
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.Step(zeroed, next));
        }

        [Test]
        public void BeatingTheLayerBossOpensTheInterlude()
        {
            var state = WalkStraightDown(SevenLayers.Of(2), 9UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);
            Assert.That(state.Phase, Is.EqualTo(RunPhase.LayerCleared));

            var rest = ExplorationReducer.Interlude(state);
            Assert.That(rest.Phase, Is.EqualTo(RunPhase.Interlude));
            Assert.That(rest.MiasmaPercent, Is.EqualTo(state.MiasmaPercent), "the interlude adds no 瘴気");
            Assert.That(rest.Stamina, Is.EqualTo(rest.MaxStamina));
        }

        [Test]
        public void TheInterludeGivesBackThirtyPercentOfHp()
        {
            var cleared = WalkStraightDown(SevenLayers.Of(1), 9UL, RunLoadout.Empty, hp: 10, maxHp: PlayerMaxHp, stamina: 10);
            var rest = ExplorationReducer.Interlude(cleared);
            Assert.That(rest.Hp, Is.EqualTo(Math.Min(PlayerMaxHp, cleared.Hp + 15)), "30% of 50");
        }

        [Test]
        public void BeatingTheLastLayersBossCompletesTheRun()
        {
            var state = WalkStraightDown(SevenLayers.Of(7), 5UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);
            Assert.That(state.Phase, Is.EqualTo(RunPhase.Completed));
            Assert.That(state.IsOver, Is.True);
        }

        [Test]
        public void TheGaugeReachingOneHundredEndsTheLife()
        {
            var profile = SevenLayers.Of(7);
            var state = ExplorationReducer.Enter(
                profile, profile.Map(3UL), RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, miasmaPercent: 90);
            Assert.That(state.MiasmaPercent, Is.EqualTo(97));
            Assert.That(state.Phase, Is.EqualTo(RunPhase.Exploring));

            var dead = ExplorationReducer.Step(state, state.ReachableUnresolved().First(), RestChoice.Train);
            Assert.That(dead.MiasmaPercent, Is.EqualTo(100));
            Assert.That(dead.Phase, Is.EqualTo(RunPhase.MiasmaDeath));
            Assert.That(dead.IsOver, Is.True);
        }

        [Test]
        public void MiasmaDeathOutranksTheBoss()
        {
            var profile = SevenLayers.Of(7);
            var map = profile.Map(3UL);
            var state = ExplorationReducer.Enter(
                profile, map, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, miasmaPercent: 79);

            while (state.Phase == RunPhase.Exploring)
            {
                int next = state.ReachableUnresolved().First();
                state = ExplorationReducer.Step(state, next, RestChoice.Train);
            }

            Assert.That(state.MiasmaPercent, Is.EqualTo(100));
            Assert.That(state.Phase, Is.EqualTo(RunPhase.MiasmaDeath));
        }

        [Test]
        public void ARestNodeHealsOrTrains_NeverBoth()
        {
            // dungeon_exploration_v4.md §3.2.
            var profile = SevenLayers.Of(4);
            var map = profile.Map(2UL);
            var restNode = map.Nodes.First(n => n.Kind == NodeKind.Rest);

            var start = ExplorationReducer.Enter(profile, map, RunLoadout.Empty, hp: 10, maxHp: PlayerMaxHp, stamina: 1);
            var atRest = WalkTo(start, restNode.Id, RestChoice.Rest);
            var atTrain = WalkTo(start, restNode.Id, RestChoice.Train);

            Assert.That(atRest.Hp, Is.EqualTo(atTrain.Hp + 8), "15% of 50 is 7.5, rounded away from zero");
            Assert.That(atRest.TempMaxStaminaMod, Is.EqualTo(ExplorationReducer.RestMaxStaminaBonus));
            Assert.That(atRest.Stamina, Is.EqualTo(atRest.MaxStamina));
            Assert.That(atRest.TrainingMarks, Is.Zero);

            Assert.That(atTrain.TrainingMarks, Is.EqualTo(1));
            Assert.That(atTrain.TempMaxStaminaMod, Is.Zero);
            Assert.That(atRest.TimeLeft, Is.EqualTo(atTrain.TimeLeft), "either choice costs the same one 刻限");
            Assert.That(atRest.MiasmaPercent, Is.EqualTo(atTrain.MiasmaPercent));
        }

        [Test]
        public void ATempModifierNeverPushesMaxStaminaPastItsClamp()
        {
            var state = EnterLayer(1).With(tempMaxStaminaMod: 9);
            Assert.That(state.MaxStamina, Is.EqualTo(14));
        }

        [Test]
        public void AToolThinsEveryLayerForTheWholeLife()
        {
            var masked = new RunLoadout(new[] { ReliefItem.Density("bosho_no_men", 1) });
            Assert.That(EnterLayer(7, loadout: masked).EffectiveDensity, Is.EqualTo(6));
            Assert.That(EnterLayer(1, loadout: masked).EffectiveDensity, Is.EqualTo(1), "density floors at 1");
        }

        [Test]
        public void AConsumablePullsTheGaugeBackWithoutSpendingTime()
        {
            var carried = new RunLoadout(
                null,
                new[] { ReliefItem.Gauge("joka_no_ko", 10) });
            var profile = SevenLayers.Of(6);
            var state = ExplorationReducer.Enter(
                profile, profile.Map(11UL), carried, PlayerMaxHp, PlayerMaxHp, 10, miasmaPercent: 40);
            Assert.That(state.MiasmaPercent, Is.EqualTo(45));

            var after = ExplorationReducer.UseConsumable(state, 0);
            Assert.That(after.MiasmaPercent, Is.EqualTo(35));
            Assert.That(after.TimeLeft, Is.EqualTo(state.TimeLeft));
            Assert.That(after.Loadout.Consumables, Is.Empty);
        }

        [Test]
        public void ThereAreThreeToolSlotsAndThreeConsumableSlots()
        {
            // concept-v3.md §14.
            var four = Enumerable.Range(0, 4).Select(i => ReliefItem.Density($"t{i}", 1)).ToArray();
            Assert.Throws<ArgumentException>(() => new RunLoadout(four));
            Assert.DoesNotThrow(() => new RunLoadout(four, null, RunLoadout.SurvivorBonusToolSlots));

            var fourConsumables = Enumerable.Range(0, 4).Select(i => ReliefItem.Gauge($"c{i}", 10)).ToArray();
            Assert.Throws<ArgumentException>(() => new RunLoadout(null, fourConsumables));
        }

        [Test]
        public void HpAndStaminaCarryAcrossBattlesButTheGaugeDoesNot()
        {
            // concept-v3.md §7.1 for the carry-over; battle_core_v4.md §6.2 for the boundary:
            // 瘴気纏い lowers max stamina inside a battle and never touches this gauge.
            var state = EnterLayer(3);
            var after = ExplorationReducer.ReportBattle(state, hpAfter: 12, staminaAfter: 2);

            Assert.That(after.Hp, Is.EqualTo(12));
            Assert.That(after.Stamina, Is.EqualTo(2));
            Assert.That(after.MiasmaPercent, Is.EqualTo(state.MiasmaPercent));
            Assert.That(after.MaxStamina, Is.EqualTo(state.MaxStamina));
        }

        [Test]
        public void ABattleThatTakesTheLastHpEndsTheRun()
        {
            var state = EnterLayer(3);
            var fallen = ExplorationReducer.ReportBattle(state, hpAfter: 0, staminaAfter: 0);
            Assert.That(fallen.Phase, Is.EqualTo(RunPhase.Fallen));
            Assert.That(fallen.IsOver, Is.True);
        }

        [Test]
        public void AStraightRunDownAllSevenLayersEndsAt89Percent()
        {
            // The headline number of seven_layers_v4.md §3.1, proved end to end rather than
            // summed on paper: enter each layer, walk the shortest route, take the interlude.
            var loadout = RunLoadout.Empty;
            var state = ExplorationReducer.Enter(
                SevenLayers.Of(1), SevenLayers.Of(1).Map(1UL), loadout, PlayerMaxHp, PlayerMaxHp, 10);
            state = WalkToBoss(state);

            var expectedAfterLayer = new[] { 5, 10, 20, 32, 48, 68, 89 };
            var expectedMaxStamina = new[] { 10, 10, 9, 9, 8, 7, 6 };
            Assert.That(state.MiasmaPercent, Is.EqualTo(expectedAfterLayer[0]));

            for (int layer = 2; layer <= SevenLayers.Count; layer++)
            {
                var profile = SevenLayers.Of(layer);
                state = ExplorationReducer.Descend(ExplorationReducer.Interlude(state), profile, profile.Map((ulong)layer));
                state = WalkToBoss(state);

                Assert.That(state.MiasmaPercent, Is.EqualTo(expectedAfterLayer[layer - 1]), $"after layer {layer}");
                Assert.That(state.MaxStamina, Is.EqualTo(expectedMaxStamina[layer - 1]), $"after layer {layer}");
            }

            Assert.That(state.Phase, Is.EqualTo(RunPhase.Completed));
            Assert.That(state.MiasmaPercent, Is.EqualTo(89));
            Assert.That(state.MaxStamina, Is.EqualTo(6));
            Assert.That(state.MiasmaPercent, Is.EqualTo(SevenLayers.BareMinimumMiasma()));
        }

        [Test]
        public void TakingTheRestOnTheLastLayerArrivesWithMoreStamina()
        {
            // seven_layers_v4.md §3.4: "max stamina 6 at 歪みの根" is the line where no rest
            // node was ever taken. 休息's +2 is a 一時変化 and carries into the battle, so a run
            // that spends its one middle node on rest reaches the root at 8 instead.
            var profile = SevenLayers.Of(7);
            var map = profile.Map(5UL);
            var rest = map.Nodes.Single(n => n.Kind == NodeKind.Rest);

            var trained = WalkThroughLayerSeven(profile, map, rest.Id, RestChoice.Train);
            var rested = WalkThroughLayerSeven(profile, map, rest.Id, RestChoice.Rest);

            Assert.That(trained.MiasmaPercent, Is.EqualTo(89));
            Assert.That(rested.MiasmaPercent, Is.EqualTo(89), "either choice costs the same 瘴気");

            Assert.That(trained.MaxStamina, Is.EqualTo(6));
            Assert.That(rested.MaxStamina, Is.EqualTo(8));
            Assert.That(rested.TrainingMarks, Is.Zero);
            Assert.That(trained.TrainingMarks, Is.EqualTo(1));
        }

        [Test]
        public void ADetourOnTheLastLayerCostsNoStamina()
        {
            // seven_layers_v4.md §3.5: the penalty caps at −4, so anything spent past 80% buys
            // nothing back and costs nothing either — except the distance left to 100%.
            var profile = SevenLayers.Of(7);
            var map = profile.Map(5UL);
            var straight = WalkThroughLayerSeven(profile, map, map.Row(1).First().Id, RestChoice.Train);

            var detoured = ExplorationReducer.Enter(
                profile, map, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, miasmaPercent: 68);
            var middle = map.Row(1).Select(n => n.Id).ToList();
            detoured = ExplorationReducer.Step(detoured, middle[0], RestChoice.Train);
            detoured = ExplorationReducer.Step(detoured, map.EntryId); // free: already resolved
            detoured = ExplorationReducer.Step(detoured, middle[1], RestChoice.Train);
            detoured = ExplorationReducer.Step(detoured, map.BossId, RestChoice.Train);

            Assert.That(detoured.MiasmaPercent, Is.EqualTo(96));
            Assert.That(detoured.MaxStamina, Is.EqualTo(straight.MaxStamina),
                "89% and 96% are both a −4 penalty");
            Assert.That(detoured.Phase, Is.EqualTo(RunPhase.Completed));
        }

        /// <summary>
        /// Enters layer seven at the 68% a straight run down the first six layers arrives with,
        /// spends its one middle node on <paramref name="middleId"/>, then drops onto the root.
        /// </summary>
        private static ExplorationState WalkThroughLayerSeven(
            LayerProfile profile, LayerMap map, int middleId, RestChoice choice)
        {
            var state = ExplorationReducer.Enter(
                profile, map, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, miasmaPercent: 68);
            state = ExplorationReducer.Step(state, middleId, choice);
            return ExplorationReducer.Step(state, map.BossId, choice);
        }

        [Test]
        public void TheSameSeedsGiveTheSameRun()
        {
            var first = WalkStraightDown(SevenLayers.Of(4), 77UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);
            var second = WalkStraightDown(SevenLayers.Of(4), 77UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);

            Assert.That(second.Map.Fingerprint(), Is.EqualTo(first.Map.Fingerprint()));
            Assert.That(second.MiasmaPercent, Is.EqualTo(first.MiasmaPercent));
            Assert.That(second.TimeLeft, Is.EqualTo(first.TimeLeft));
            Assert.That(second.Hp, Is.EqualTo(first.Hp));
        }

        private static ExplorationState WalkStraightDown(
            LayerProfile profile, ulong seed, RunLoadout loadout, int hp, int maxHp, int stamina) =>
            WalkToBoss(ExplorationReducer.Enter(profile, profile.Map(seed), loadout, hp, maxHp, stamina));

        /// <summary>
        /// Always steps to the first node of the next row. Every forward route is the same
        /// length, so this is a shortest route by construction.
        /// </summary>
        private static ExplorationState WalkToBoss(ExplorationState start)
        {
            var state = start;
            while (state.Phase == RunPhase.Exploring)
            {
                int currentRow = state.Map.Node(state.CurrentNodeId).Row;
                int next = state.Map.Successors(state.CurrentNodeId)
                    .First(id => state.Map.Node(id).Row == currentRow + 1);
                state = ExplorationReducer.Step(state, next, RestChoice.Train);
            }
            return state;
        }

        private static ExplorationState WalkTo(ExplorationState start, int target, RestChoice choice)
        {
            var previous = new Dictionary<int, int>();
            var queue = new Queue<int>();
            var seen = new HashSet<int> { start.CurrentNodeId };
            queue.Enqueue(start.CurrentNodeId);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == target) break;
                foreach (int next in start.Map.Neighbours(current))
                {
                    if (!seen.Add(next)) continue;
                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }

            var route = new List<int>();
            for (int at = target; at != start.CurrentNodeId; at = previous[at]) route.Add(at);
            route.Reverse();

            var state = start;
            foreach (int id in route) state = ExplorationReducer.Step(state, id, choice);
            return state;
        }
    }
}
