using DungeonCore;

namespace DungeonCore.Tests
{
    public class ExplorationReducerTests
    {
        // battle_core_v4.md §10: PLAYER_MAX_HP 50.
        private const int PlayerMaxHp = 50;

        private static readonly ReliefItem Sand = ReliefItem.EntryDay("hiyomi_no_suna");
        private static readonly ReliefItem Thread = ReliefItem.Return("kaerimichi_no_ito");
        private static readonly ReliefItem Pill = ReliefItem.Haste("hayagake_no_gan");

        private static ExplorationState EnterLayer(
            int layer, ulong seed = 11UL, RunLoadout? loadout = null, int hp = PlayerMaxHp, int stamina = 10,
            CalendarState? calendar = null, Region region = Region.Dragon)
        {
            var profile = Lair.Of(layer, region);
            return ExplorationReducer.Enter(
                profile, profile.Map(seed), loadout ?? RunLoadout.Empty, hp, PlayerMaxHp, stamina, calendar ?? CalendarState.Fresh);
        }

        // ---------------------------------------------------------------- the day a node costs

        [Test]
        public void EnteringALayerSpendsTheEntryNodesDay()
        {
            var state = EnterLayer(3);
            Assert.That(state.DaysLeft, Is.EqualTo(Calendar.TotalDays - 1));
            Assert.That(state.Calendar, Is.EqualTo(CalendarState.Fresh.Advance()));
            Assert.That(state.HasResolved(state.Map.EntryId), Is.True);
            Assert.That(state.Phase, Is.EqualTo(RunPhase.Exploring));
            Assert.That(state.Region, Is.EqualTo(Region.Dragon));
        }

        [Test]
        public void ANewNodeCostsOneDay()
        {
            var before = EnterLayer(5);
            int next = before.ReachableUnresolved().First();
            var after = ExplorationReducer.Step(before, next, RestChoice.Train);
            Assert.That(after.DaysLeft, Is.EqualTo(before.DaysLeft - 1));
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
            Assert.That(back.Calendar, Is.EqualTo(stepped.Calendar));
        }

        [Test]
        public void BothSidesOfAForkCostOneDayEach()
        {
            var start = EnterLayer(1);
            int entry = start.CurrentNodeId;
            var fork = start.ReachableUnresolved().Take(2).ToList();
            Assert.That(fork.Count, Is.EqualTo(2), "layer one's entry should fork");

            var left = ExplorationReducer.Step(start, fork[0], RestChoice.Train);
            var home = ExplorationReducer.Step(left, entry);
            var right = ExplorationReducer.Step(home, fork[1], RestChoice.Train);

            Assert.That(right.DaysLeft, Is.EqualTo(start.DaysLeft - 2));
        }

        [Test]
        public void SteppingSomewhereThatDoesNotTouchTheCurrentNodeThrows()
        {
            var state = EnterLayer(1);
            int far = state.Map.Nodes.Last(n => n.Row == state.Map.RowCount - 1).Id;
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.Step(state, far));
        }

        [Test]
        public void ALayerHasNoBudgetOfItsOwn()
        {
            // The per-layer 刻限 is gone: combing the king room's middle row leaves the life exploring, a day a node.
            var state = EnterLayer(7, seed: 4UL, stamina: 10);
            int entry = state.CurrentNodeId;
            var middle = state.Map.Row(1).Select(n => n.Id).ToList();
            Assert.That(middle.Count, Is.EqualTo(4));

            foreach (int id in middle)
            {
                state = ExplorationReducer.Step(state, id, RestChoice.Train);
                state = ExplorationReducer.Step(state, entry);
            }

            Assert.That(state.Phase, Is.EqualTo(RunPhase.Exploring));
            Assert.That(state.DaysLeft, Is.EqualTo(Calendar.TotalDays - 5));
            Assert.That(ExplorationReducer.CanStep(state, middle[0]), Is.True);
        }

        // ---------------------------------------------------------------- 日読みの砂 and 早駆けの丸薬

        [Test]
        public void TheSandMakesEveryEntryNodeFree()
        {
            // tools_and_consumables_v4.md §2.4.
            var sand = new RunLoadout(new[] { Sand });
            Assert.That(sand.EntryDayFree, Is.True);
            var state = EnterLayer(3, loadout: sand);
            Assert.That(state.DaysLeft, Is.EqualTo(Calendar.TotalDays));
            Assert.That(state.HasResolved(state.Map.EntryId), Is.True);

            var next = ExplorationReducer.Step(state, state.ReachableUnresolved().First(), RestChoice.Train);
            Assert.That(next.DaysLeft, Is.EqualTo(Calendar.TotalDays - 1), "only the entry node is free");
        }

        [Test]
        public void TheSandTurnsThirtyDaysToTheKingIntoTwentyThree()
        {
            // tools_and_consumables_v4.md §2.4: seven entry nodes at 0 days.
            Assert.That(RunStraightDown(Region.Parasite, RunLoadout.Empty).DaysLeft, Is.EqualTo(Calendar.TotalDays - 30));
            Assert.That(RunStraightDown(Region.Parasite, new RunLoadout(new[] { Sand })).DaysLeft, Is.EqualTo(Calendar.TotalDays - 23));
        }

        [Test]
        public void ThePillMakesTheNextNewNodeFree()
        {
            // tools_and_consumables_v4.md §3.
            var state = EnterLayer(1, loadout: new RunLoadout(null, new[] { Pill }));
            int entry = state.CurrentNodeId;
            var taken = ExplorationReducer.UseConsumable(state, 0);
            Assert.That(taken.NextStepFree, Is.True);
            Assert.That(taken.DaysLeft, Is.EqualTo(state.DaysLeft), "taking it costs no day");
            Assert.That(taken.Loadout.Consumables, Is.Empty);

            var fork = taken.ReachableUnresolved().Take(2).ToList();
            var free = ExplorationReducer.Step(taken, fork[0], RestChoice.Train);
            Assert.That(free.DaysLeft, Is.EqualTo(state.DaysLeft));
            Assert.That(free.NextStepFree, Is.False);

            var back = ExplorationReducer.Step(free, entry);
            var paid = ExplorationReducer.Step(back, fork[1], RestChoice.Train);
            Assert.That(paid.DaysLeft, Is.EqualTo(state.DaysLeft - 1));
        }

        [Test]
        public void ThePillWaitsOverResolvedNodes()
        {
            var state = EnterLayer(1, loadout: new RunLoadout(null, new[] { Pill }));
            int entry = state.CurrentNodeId;
            int forward = state.ReachableUnresolved().First();
            var stepped = ExplorationReducer.UseConsumable(ExplorationReducer.Step(state, forward, RestChoice.Train), 0);

            var back = ExplorationReducer.Step(stepped, entry);
            Assert.That(back.NextStepFree, Is.True, "a walk back is free already, so the pill is kept");
        }

        [Test]
        public void APillTakenInTheInterludePaysTheNextLayersEntry()
        {
            var cleared = WalkStraightDown(Lair.Of(1), 9UL, new RunLoadout(null, new[] { Pill }), PlayerMaxHp, PlayerMaxHp, 10, CalendarState.Fresh);
            var rest = ExplorationReducer.UseConsumable(ExplorationReducer.Interlude(cleared), 0);
            var two = ExplorationReducer.Descend(rest, Lair.Of(2), Lair.Of(2).Map(2UL));
            Assert.That(two.DaysLeft, Is.EqualTo(cleared.DaysLeft));
            Assert.That(two.NextStepFree, Is.False);
        }

        [Test]
        public void WithTheSandThePillIsKeptForTheNodeAfterTheEntry()
        {
            var cleared = WalkStraightDown(Lair.Of(1), 9UL, new RunLoadout(new[] { Sand }, new[] { Pill }), PlayerMaxHp, PlayerMaxHp, 10, CalendarState.Fresh);
            var rest = ExplorationReducer.UseConsumable(ExplorationReducer.Interlude(cleared), 0);
            var two = ExplorationReducer.Descend(rest, Lair.Of(2), Lair.Of(2).Map(2UL));
            Assert.That(two.DaysLeft, Is.EqualTo(cleared.DaysLeft));
            Assert.That(two.NextStepFree, Is.True);

            var next = ExplorationReducer.Step(two, two.ReachableUnresolved().First(), RestChoice.Train);
            Assert.That(next.DaysLeft, Is.EqualTo(cleared.DaysLeft));
            Assert.That(next.NextStepFree, Is.False);
        }

        // ---------------------------------------------------------------- 期限切れ

        [Test]
        public void TheLastDayEndsTheWorld()
        {
            var state = EnterLayer(1, calendar: new CalendarState(2));
            Assert.That(state.DaysLeft, Is.EqualTo(1));
            Assert.That(state.Phase, Is.EqualTo(RunPhase.Exploring));

            var gone = ExplorationReducer.Step(state, state.ReachableUnresolved().First(), RestChoice.Train);
            Assert.That(gone.DaysLeft, Is.Zero);
            Assert.That(gone.Phase, Is.EqualTo(RunPhase.Expired));
            Assert.That(gone.IsOver, Is.True);
            Assert.That(ExplorationReducer.CanStep(gone, state.CurrentNodeId), Is.False);
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.EndLife(gone));
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.Interlude(gone));
        }

        [Test]
        public void ExpiryOutranksTheLayerBoss()
        {
            // Layer one's shortest route is five days: the boss takes the last one.
            var state = WalkStraightDown(Lair.Of(1), 9UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, new CalendarState(5));
            Assert.That(state.HasResolved(state.Map.BossId), Is.True);
            Assert.That(state.Phase, Is.EqualTo(RunPhase.Expired));
        }

        [Test]
        public void TheLastDaySpentReachingTheKingEndsTheWorldBeforeTheFight()
        {
            // An expired calendar starts no fight, so the king never falls.
            var state = WalkStraightDown(Lair.Of(7, Region.Dragon), 5UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, new CalendarState(3));
            Assert.That(state.Phase, Is.EqualTo(RunPhase.Expired));
            Assert.That(state.Calendar.KingReturns, Is.Empty);

            var reported = BeatTheBoss(state);
            Assert.That(reported.Phase, Is.EqualTo(RunPhase.Expired));
            Assert.That(reported.Calendar.KingReturns, Is.Empty);
        }

        [Test]
        public void NoNewNodeCanBeTakenOnARunOutCalendar()
        {
            var state = EnterLayer(1, seed: 4UL);
            var zeroed = state.With(calendar: new CalendarState(0));
            int next = zeroed.ReachableUnresolved().First();
            Assert.That(ExplorationReducer.CanStep(zeroed, next), Is.False);
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.Step(zeroed, next));
        }

        [Test]
        public void EnteringOnARunOutCalendarEndsTheLifeAtTheEntry()
        {
            var state = EnterLayer(1, calendar: new CalendarState(0));
            Assert.That(state.Phase, Is.EqualTo(RunPhase.Expired));
        }

        // ---------------------------------------------------------------- layers, interlude and kings

        [Test]
        public void BeatingTheLayerBossOpensTheInterlude()
        {
            var state = WalkStraightDown(Lair.Of(2), 9UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, CalendarState.Fresh);
            Assert.That(state.Phase, Is.EqualTo(RunPhase.LayerCleared));
            Assert.That(ExplorationReducer.CanInterlude(state), Is.True);

            var rest = ExplorationReducer.Interlude(state);
            Assert.That(rest.Phase, Is.EqualTo(RunPhase.Interlude));
            Assert.That(rest.Calendar, Is.EqualTo(state.Calendar), "the interlude costs no day");
            Assert.That(rest.Stamina, Is.EqualTo(rest.MaxStamina));
        }

        [Test]
        public void TheInterludeGivesBackThirtyPercentOfHp()
        {
            var cleared = WalkStraightDown(Lair.Of(1), 9UL, RunLoadout.Empty, hp: 10, maxHp: PlayerMaxHp, stamina: 10, CalendarState.Fresh);
            var rest = ExplorationReducer.Interlude(cleared);
            Assert.That(rest.Hp, Is.EqualTo(Math.Min(PlayerMaxHp, cleared.Hp + 15)), "30% of 50");
        }

        [Test]
        public void AKingFallingFreezesTheCalendarAndEndsTheWayOn()
        {
            var reached = WalkStraightDown(Lair.Of(7, Region.Ghost), 5UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, CalendarState.Fresh);
            Assert.That(reached.Calendar.KingReturns, Is.Empty, "standing in the king room is not beating the king");

            var state = BeatTheBoss(reached);
            Assert.That(state.Phase, Is.EqualTo(RunPhase.LayerCleared));
            Assert.That(state.IsOver, Is.False);
            Assert.That(state.Calendar.KingReturns, Is.EqualTo(new Dictionary<Region, int> { [Region.Ghost] = Calendar.KingReturnDays }));
            Assert.That(state.DaysLeft, Is.EqualTo(Calendar.TotalDays - 3));
            Assert.That(BeatTheBoss(state).Calendar, Is.EqualTo(state.Calendar), "a second report changes nothing");

            // No layer lies below the king room: the life ends alive from here (seven_layers_v4.md §10).
            Assert.That(ExplorationReducer.CanInterlude(state), Is.False);
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.Interlude(state));
            Assert.That(ExplorationReducer.EndLife(state).Phase, Is.EqualTo(RunPhase.Survived));
        }

        [Test]
        public void TheThirdKingWhileTheOtherTwoAreDownIsTheClear()
        {
            var twoDown = CalendarState.Fresh.KingDown(Region.Dragon).Advance(20).KingDown(Region.Parasite);
            var reached = WalkStraightDown(Lair.Of(7, Region.Ghost), 5UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, twoDown);
            Assert.That(reached.Phase, Is.EqualTo(RunPhase.LayerCleared), "the third king still has to be fought");
            Assert.That(reached.IsOver, Is.False);

            var state = BeatTheBoss(reached);
            Assert.That(state.Phase, Is.EqualTo(RunPhase.Completed));
            Assert.That(state.IsOver, Is.True);
            Assert.That(state.Calendar.ClearReached, Is.True);
            Assert.That(state.DaysLeft, Is.EqualTo(twoDown.DaysLeft), "every day of the room was frozen");
        }

        [Test]
        public void LosingTheKingFightLeavesTheKingStandingAndTheCalendarRunning()
        {
            var reached = WalkStraightDown(Lair.Of(7, Region.Dragon), 5UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, CalendarState.Fresh);
            var fallen = ExplorationReducer.ReportBattle(reached, 0, reached.Stamina);

            Assert.That(fallen.Phase, Is.EqualTo(RunPhase.Fallen));
            Assert.That(fallen.Calendar.KingReturns, Is.Empty);
            Assert.That(fallen.Calendar, Is.EqualTo(reached.Calendar));

            // The next heir's calendar counts down through the hand-over instead of freezing on a king nobody beat.
            var next = fallen.Calendar.Handover(fallen.Phase, fallen.Loadout.ShortHandover);
            Assert.That(next.IsFrozen, Is.False);
            Assert.That(next.DaysLeft, Is.EqualTo(fallen.DaysLeft - Calendar.HandoverFallenDays));
        }

        [Test]
        public void LosingTheThirdKingIsNoClear()
        {
            var twoDown = CalendarState.Fresh.KingDown(Region.Dragon).Advance(20).KingDown(Region.Parasite);
            var reached = WalkStraightDown(Lair.Of(7, Region.Ghost), 5UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, twoDown);
            var fallen = ExplorationReducer.ReportBattle(reached, 0, reached.Stamina);

            Assert.That(fallen.Phase, Is.EqualTo(RunPhase.Fallen));
            Assert.That(fallen.Calendar.ClearReached, Is.False);
            Assert.That(fallen.Calendar.KingReturns.Keys, Is.EqualTo(new[] { Region.Dragon, Region.Parasite }));
        }

        [Test]
        public void BeatingALayerBossLeavesTheKingsAlone()
        {
            var reached = WalkStraightDown(Lair.Of(6, Region.Dragon), 9UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, CalendarState.Fresh);
            var won = BeatTheBoss(reached);
            Assert.That(won.Phase, Is.EqualTo(RunPhase.LayerCleared));
            Assert.That(won.Calendar, Is.EqualTo(reached.Calendar));
        }

        [Test]
        public void AStraightRunToTheKingCostsThirtyDays()
        {
            // seven_layers_v4.md §3.2, walked end to end rather than summed on paper.
            foreach (Region region in Lair.Regions)
            {
                var state = BeatTheBoss(RunStraightDown(region, RunLoadout.Empty));
                Assert.That(state.DaysLeft, Is.EqualTo(Calendar.TotalDays - 30), region.ToString());
                Assert.That(state.Phase, Is.EqualTo(RunPhase.LayerCleared), region.ToString());
                Assert.That(state.Profile.IsKingLayer, Is.True);
                Assert.That(state.Calendar.KingReturns.Keys, Is.EqualTo(new[] { region }));
                Assert.That(state.MaxStamina, Is.EqualTo(StaminaRules.BaseMaxStamina), "no gauge lowers max stamina any more");
            }
        }

        // ---------------------------------------------------------------- stamina and rests

        [Test]
        public void StaminaComesBackByThreeForEveryNewNode()
        {
            // concept-v3.md §12-16.
            var start = EnterLayer(1, stamina: 0);
            Assert.That(start.Stamina, Is.EqualTo(StaminaRules.FieldRecovery));

            var next = ExplorationReducer.Step(start, start.ReachableUnresolved().First(), RestChoice.Train);
            Assert.That(next.Stamina, Is.EqualTo(Math.Min(next.MaxStamina, 2 * StaminaRules.FieldRecovery)));
        }

        [Test]
        public void StaminaNeverPassesTheMaximum()
        {
            var state = EnterLayer(6, stamina: 10);
            Assert.That(state.Stamina, Is.LessThanOrEqualTo(state.MaxStamina));
        }

        [Test]
        public void ARestNodeHealsOrTrains_NeverBoth()
        {
            // dungeon_exploration_v4.md §3.2.
            var profile = Lair.Of(4, Region.Ghost);
            var map = profile.Map(2UL);
            var restNode = map.Nodes.First(n => n.Kind == NodeKind.Rest);

            var start = ExplorationReducer.Enter(profile, map, RunLoadout.Empty, hp: 10, maxHp: PlayerMaxHp, stamina: 1, CalendarState.Fresh);
            var atRest = WalkTo(start, restNode.Id, RestChoice.Rest);
            var atTrain = WalkTo(start, restNode.Id, RestChoice.Train);

            Assert.That(atRest.Hp, Is.EqualTo(atTrain.Hp + 8), "15% of 50 is 7.5, rounded away from zero");
            Assert.That(atRest.TempMaxStaminaMod, Is.EqualTo(ExplorationReducer.RestMaxStaminaBonus));
            Assert.That(atRest.Stamina, Is.EqualTo(atRest.MaxStamina));
            Assert.That(atRest.TrainingMarks, Is.Zero);

            Assert.That(atTrain.TrainingMarks, Is.EqualTo(1));
            Assert.That(atTrain.TempMaxStaminaMod, Is.Zero);
            Assert.That(atRest.Calendar, Is.EqualTo(atTrain.Calendar), "either choice costs the same one day");
        }

        [Test]
        public void TakingTheRestInTheKingRoomArrivesWithMoreStamina()
        {
            // 休息's +2 is a 一時変化 and carries into the king's battle.
            var profile = Lair.Of(7, Region.Dragon);
            var map = profile.Map(5UL);
            var rest = map.Nodes.Single(n => n.Kind == NodeKind.Rest);

            var trained = ThroughTheKingRoom(profile, map, rest.Id, RestChoice.Train);
            var rested = ThroughTheKingRoom(profile, map, rest.Id, RestChoice.Rest);

            Assert.That(rested.Calendar, Is.EqualTo(trained.Calendar), "either choice costs the same day");
            Assert.That(trained.MaxStamina, Is.EqualTo(10));
            Assert.That(rested.MaxStamina, Is.EqualTo(12));
            Assert.That(rested.TrainingMarks, Is.Zero);
            Assert.That(trained.TrainingMarks, Is.EqualTo(1));
        }

        [Test]
        public void ATempModifierNeverPushesMaxStaminaPastItsClamp()
        {
            var state = EnterLayer(1).With(tempMaxStaminaMod: 9);
            Assert.That(state.MaxStamina, Is.EqualTo(14));
        }

        // ---------------------------------------------------------------- loadout

        [Test]
        public void ThereAreThreeToolSlotsAndThreeConsumableSlots()
        {
            // concept-v3.md §14.
            var four = new[] { Sand, Sand, Thread, Thread };
            Assert.Throws<ArgumentException>(() => new RunLoadout(four));
            Assert.DoesNotThrow(() => new RunLoadout(four, null, RunLoadout.SurvivorBonusToolSlots));

            var fourConsumables = Enumerable.Range(0, 4).Select(i => ReliefItem.Haste($"c{i}")).ToArray();
            Assert.Throws<ArgumentException>(() => new RunLoadout(null, fourConsumables));
        }

        [Test]
        public void OutsideTrialAToolIsOneTheCalendarReads()
        {
            Assert.Throws<ArgumentException>(() => new RunLoadout(new[] { new ReliefItem("ikitsugi_no_fue", ReliefKind.Battle, 3) }));
            var both = new RunLoadout(new[] { Sand, Thread });
            Assert.That(both.EntryDayFree, Is.True);
            Assert.That(both.ShortHandover, Is.True);
            Assert.That(RunLoadout.Empty.EntryDayFree, Is.False);
            Assert.That(RunLoadout.Empty.ShortHandover, Is.False);
        }

        [Test]
        public void TheThreadDoesNothingInsideTheLair()
        {
            var plain = EnterLayer(2);
            var threaded = EnterLayer(2, loadout: new RunLoadout(new[] { Thread }));
            Assert.That(threaded.Calendar, Is.EqualTo(plain.Calendar));
        }

        // ---------------------------------------------------------------- battles and the end of a life

        [Test]
        public void HpAndStaminaCarryAcrossBattles()
        {
            // concept-v3.md §7.1 for the carry-over; battle_core_v4.md §6.2 for the boundary: a battle's own swings of
            // max stamina are never written back.
            var state = EnterLayer(3);
            var after = ExplorationReducer.ReportBattle(state, hpAfter: 12, staminaAfter: 2);

            Assert.That(after.Hp, Is.EqualTo(12));
            Assert.That(after.Stamina, Is.EqualTo(2));
            Assert.That(after.Calendar, Is.EqualTo(state.Calendar));
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
        public void EndingTheLifeStopsTheRunWhereItStands()
        {
            // tier1-core.md R1-11: 「この生を終える」 can be chosen at any point of the exploration.
            var state = EnterLayer(3, loadout: new RunLoadout(new[] { Thread }));
            var ended = ExplorationReducer.EndLife(state);

            Assert.That(ended.Phase, Is.EqualTo(RunPhase.Survived));
            Assert.That(ended.IsOver, Is.True);
            Assert.That(ended.Calendar, Is.SameAs(state.Calendar), "closing the life spends no day; the handover is the calendar's");
            Assert.That(ended.Hp, Is.EqualTo(state.Hp));
        }

        [Test]
        public void TheLifeCanAlsoBeClosedFromTheInterlude()
        {
            var cleared = WalkStraightDown(Lair.Of(1), 9UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, CalendarState.Fresh);
            var rest = ExplorationReducer.Interlude(cleared);
            Assert.That(ExplorationReducer.EndLife(rest).Phase, Is.EqualTo(RunPhase.Survived));
        }

        [Test]
        public void AnEndedLifeTakesNoFurtherMoves()
        {
            var ended = ExplorationReducer.EndLife(EnterLayer(1));
            int next = ended.ReachableUnresolved().First();

            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.EndLife(ended));
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.Step(ended, next));
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.Interlude(ended));

            var fallen = ExplorationReducer.ReportBattle(EnterLayer(1), hpAfter: 0, staminaAfter: 0);
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.EndLife(fallen), "a fallen life is not a survivor");
        }

        [Test]
        public void TheSameSeedsGiveTheSameRun()
        {
            var first = WalkStraightDown(Lair.Of(4, Region.Ghost), 77UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, CalendarState.Fresh);
            var second = WalkStraightDown(Lair.Of(4, Region.Ghost), 77UL, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, CalendarState.Fresh);

            Assert.That(second.Map.Fingerprint(), Is.EqualTo(first.Map.Fingerprint()));
            Assert.That(second.Calendar, Is.EqualTo(first.Calendar));
            Assert.That(second.Hp, Is.EqualTo(first.Hp));
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Enters the king room, spends its one middle node on <paramref name="middleId"/>, then meets the king.</summary>
        private static ExplorationState ThroughTheKingRoom(LayerProfile profile, LayerMap map, int middleId, RestChoice choice)
        {
            var state = ExplorationReducer.Enter(profile, map, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, CalendarState.Fresh);
            state = ExplorationReducer.Step(state, middleId, choice);
            return ExplorationReducer.Step(state, map.BossId, choice);
        }

        /// <summary>Layer 1 to the region's king, the shortest route through each layer, an interlude between them.</summary>
        private static ExplorationState RunStraightDown(Region region, RunLoadout loadout)
        {
            var route = Lair.Route(region);
            var state = WalkToBoss(ExplorationReducer.Enter(
                route[0], route[0].Map(1UL), loadout, PlayerMaxHp, PlayerMaxHp, 10, CalendarState.Fresh));
            foreach (var profile in route.Skip(1))
            {
                state = ExplorationReducer.Descend(ExplorationReducer.Interlude(state), profile, profile.Map((ulong)profile.Layer));
                state = WalkToBoss(state);
            }
            return state;
        }

        private static ExplorationState BeatTheBoss(ExplorationState state) =>
            ExplorationReducer.ReportBattle(state, state.Hp, state.Stamina);

        private static ExplorationState WalkStraightDown(
            LayerProfile profile, ulong seed, RunLoadout loadout, int hp, int maxHp, int stamina, CalendarState calendar) =>
            WalkToBoss(ExplorationReducer.Enter(profile, profile.Map(seed), loadout, hp, maxHp, stamina, calendar));

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
