// The trial extension of the exploration core (the browser trial's docs/EXTENSIONS.md 「探索の持ち物とイベント」, W-1〜W-19
// and X-13): the core half of test/ext/world.test.mjs — the trial loadout, every item's effect, 標の石, the three
// events and their table — and that all of it is off by default. The session and screen half is in
// Assets/View/Exploration/Tests/TrialSessionTests.cs.
using DungeonCore;

namespace DungeonCore.Tests
{
    public class TrialRulesTests
    {
        private const int PlayerMaxHp = 50;

        // The catalogue's items as the trial carries them (DungeonContent's amounts; TrialItems in Exploration.Script).
        private static readonly ReliefItem Mask = ReliefItem.Density("bosho_no_men", 1);
        private static readonly ReliefItem Sand = new ReliefItem("kokugen_no_suna", ReliefKind.Time, 1);
        private static readonly ReliefItem Book = new ReliefItem("utsushi_no_tobari", ReliefKind.Survey, 1);
        private static readonly ReliefItem Wick = new ReliefItem("toshin_no_kakera", ReliefKind.Interlude, 15);
        private static readonly ReliefItem Flute = new ReliefItem("ikitsugi_no_fue", ReliefKind.Battle, 3);
        private static readonly ReliefItem Incense = ReliefItem.Gauge("joka_no_ko", 10);
        private static readonly ReliefItem Herb = new ReliefItem("yakuso", ReliefKind.HealHp, 20);
        private static readonly ReliefItem Meat = new ReliefItem("koi_hoshiniku", ReliefKind.MaxStamina, 1);
        private static readonly ReliefItem Water = new ReliefItem("wasuremizu", ReliefKind.RefillStamina, 1);
        private static readonly ReliefItem Stone = new ReliefItem("shirube_no_ishi", ReliefKind.Teleport, 1);

        private static RunLoadout Trial(ReliefItem[]? tools = null, ReliefItem[]? consumables = null) =>
            new RunLoadout(tools, consumables, Math.Max(RunLoadout.ToolSlots, tools?.Length ?? 0), trial: true);

        private static ExplorationState Enter(int layer, RunLoadout loadout, bool trial, ulong seed = 11UL, int hp = PlayerMaxHp,
            int stamina = 10, int miasma = 0)
        {
            var profile = SevenLayers.Of(layer);
            return ExplorationReducer.Enter(profile, profile.Map(seed), loadout, hp, PlayerMaxHp, stamina, miasma, 0, trial);
        }

        // ---------------------------------------------------------------- off by default (W-1)

        [Test]
        public void ByDefault_NothingIsOn_AndTheCoreIsTheCSharp()
        {
            var plain = Enter(1, RunLoadout.Empty, trial: false);
            Assert.That(plain.Trial, Is.False);
            Assert.That(plain.TimeBonus, Is.EqualTo(0));
            Assert.That(plain.TimeLimit, Is.EqualTo(SevenLayers.Of(1).TimeLimit));
            Assert.That(RunLoadout.Empty.Trial, Is.False);
            Assert.That(new RunLoadout().Trial, Is.False);
            Assert.That(ExplorationReducer.CanTeleport(plain, plain.Map.BossId), Is.False);
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.ResolveEvent(plain, EventKind.Spring, true));
            Assert.That(ExplorationReducer.InterludePercent(plain), Is.EqualTo(ExplorationReducer.InterludeHpPercent));
        }

        [Test]
        public void ACSharpRunLoadout_StillRefusesANonDensityTool_ATrialOneTakesEveryTool()
        {
            // js: test/ext/world.test.mjs:132
            Assert.Throws<ArgumentException>(() => new RunLoadout(new[] { Sand }));
            var lo = new RunLoadout(new[] { Sand, Mask, Flute }, null, 3, trial: true);
            Assert.That(lo.Trial, Is.True);
            Assert.That(lo.DensityRelief, Is.EqualTo(1), "only the density tools lower the density");
            Assert.That(lo.TimeBonus, Is.EqualTo(1));
            Assert.That(lo.InterludeBonus, Is.EqualTo(0));
            Assert.That(lo.ChoosesSurvey, Is.False);
            var ex = Assert.Throws<ArgumentException>(() => new RunLoadout(new[] { Incense }, null, 3, trial: true));
            Assert.That(ex!.Message, Does.StartWith("a trial tool is one of the eight tools of tools_and_consumables_v4.md §2"));
        }

        [Test]
        public void ACSharpState_UsesAConsumable_AsATrialOneDoes()
        {
            // js: test/ext/world.test.mjs:143 (the core half) said a C# state burnt 薬草 without effect; since #651
            // the C# reducer gives every consumable its effect, so the trial and the C# heal alike.
            var state = Enter(1, new RunLoadout(null, new[] { Herb }), trial: false, hp: 20);
            var after = ExplorationReducer.UseConsumable(state, 0);
            Assert.That(after.Hp, Is.EqualTo(30), "20% of 50");
            Assert.That(after.Loadout.Consumables, Is.Empty);
        }

        [Test]
        public void TheTrialLoadout_TakesAFind_IntoAFreeSlotOrInPlaceOfANamedOne_AndKeepsItsFlag()
        {
            var lo = Trial(null, new[] { Herb, Water });
            Assert.That(lo.ConsumableSlotsFull, Is.False);
            var three = lo.Take(Meat);
            Assert.That(three.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "yakuso", "wasuremizu", "koi_hoshiniku" }));
            Assert.That(three.ConsumableSlotsFull, Is.True);
            Assert.That(three.Trial, Is.True);
            Assert.Throws<ExplorationRuleException>(() => three.Take(Stone));
            Assert.Throws<ExplorationRuleException>(() => three.Take(Stone, 3));
            var swapped = three.Take(Stone, 1);
            Assert.That(swapped.Trial, Is.True, "a swap keeps the trial flag");
            Assert.That(swapped.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "yakuso", "shirube_no_ishi", "koi_hoshiniku" }));
            var spent = swapped.Spend(1, out var item);
            Assert.That(item, Is.EqualTo(Stone));
            Assert.That(spent.Trial, Is.True, "spending keeps the trial flag");
            Assert.That(spent.Consumables.Count, Is.EqualTo(2));
        }

        // ---------------------------------------------------------------- tools (§2)

        [Test]
        public void 刻限の砂_AddsItsTimeToEveryLayer_AndCopiesStack()
        {
            // js: test/ext/world.test.mjs:172 (the core half)
            var state = Enter(1, Trial(new[] { Sand }), trial: true);
            Assert.That(state.TimeLimit, Is.EqualTo(11));
            Assert.That(state.TimeBonus, Is.EqualTo(1));
            Assert.That(state.TimeLeft, Is.EqualTo(10), "the entry node took one");
            Assert.That(Enter(1, Trial(new[] { Sand, Sand }), trial: true).TimeLimit, Is.EqualTo(12));
            // the same loadout in a C# state does nothing
            var off = Enter(1, Trial(new[] { Sand }), trial: false);
            Assert.That(off.TimeLimit, Is.EqualTo(10));
            Assert.That(off.TimeLeft, Is.EqualTo(9));
        }

        [Test]
        public void TheTrialFlag_ReachesTheNextLayer_ThroughTheDescent()
        {
            // js: test/ext/world.test.mjs:188 (the core half)
            var state = Enter(1, Trial(new[] { Sand }), trial: true).With(phase: RunPhase.LayerCleared);
            var rest = ExplorationReducer.Interlude(state);
            var two = SevenLayers.Of(2);
            var below = ExplorationReducer.Descend(rest, two, two.Map(3UL));
            Assert.That(below.Trial, Is.True);
            Assert.That(below.TimeLimit, Is.EqualTo(11));
            Assert.That(below.TimeLeft, Is.EqualTo(10));
        }

        [Test]
        public void 灯芯の欠片_RaisesTheInterludeHealTo45Percent()
        {
            // js: test/ext/world.test.mjs:203 (the core half)
            var state = Enter(1, Trial(new[] { Wick }), trial: true, hp: 10).With(phase: RunPhase.LayerCleared);
            Assert.That(ExplorationReducer.InterludePercent(state), Is.EqualTo(45));
            Assert.That(ExplorationReducer.InterludeHealOf(state), Is.EqualTo(23), "45% of 50, rounded half away from zero");
            Assert.That(ExplorationReducer.Interlude(state).Hp, Is.EqualTo(33));
            var plain = Enter(1, Trial(), trial: true, hp: 10).With(phase: RunPhase.LayerCleared);
            Assert.That(ExplorationReducer.Interlude(plain).Hp, Is.EqualTo(25));
            Assert.That(ExplorationReducer.InterludeHealOf(plain), Is.EqualTo(15));
            // the C# reads 30% whatever is carried
            var off = Enter(1, Trial(new[] { Wick }), trial: false, hp: 10).With(phase: RunPhase.LayerCleared);
            Assert.That(ExplorationReducer.Interlude(off).Hp, Is.EqualTo(25));
            Assert.That(ExplorationReducer.InterludeHealOf(off), Is.EqualTo(ExplorationReducer.InterludeHeal(PlayerMaxHp)));
        }

        [Test]
        public void 写しの帳_IsChosen_OnlyInATrialLoadout()
        {
            Assert.That(Trial(new[] { Book }).ChoosesSurvey, Is.True);
            Assert.That(Trial(new[] { Mask }).ChoosesSurvey, Is.False);
        }

        // ---------------------------------------------------------------- consumables (§3)

        [Test]
        public void TheConsumables_ActInTrialMode()
        {
            // js: test/ext/world.test.mjs:313〜412 (the core half): 薬草 +20%, 濃い干し肉 +1 inside ±4, 忘れ水 full, 浄化の香 −10.
            var state = Enter(1, Trial(null, new[] { Herb, Meat, Water }), trial: true, hp: 25, stamina: 2, miasma: 30);
            var healed = ExplorationReducer.UseConsumable(state, 0);
            Assert.That(healed.Hp, Is.EqualTo(35));
            Assert.That(healed.Loadout.Consumables.Count, Is.EqualTo(2));

            var fed = ExplorationReducer.UseConsumable(state, 1);
            Assert.That(fed.TempMaxStaminaMod, Is.EqualTo(state.TempMaxStaminaMod + 1));
            Assert.That(fed.MaxStamina, Is.EqualTo(state.MaxStamina + 1));
            Assert.That(ExplorationReducer.UseConsumable(state.With(tempMaxStaminaMod: 4), 1).TempMaxStaminaMod, Is.EqualTo(4));

            var refilled = ExplorationReducer.UseConsumable(state, 2);
            Assert.That(refilled.Stamina, Is.EqualTo(state.MaxStamina));

            var incense = Enter(1, Trial(null, new[] { Incense }), trial: true, miasma: 30);
            Assert.That(ExplorationReducer.UseConsumable(incense, 0).MiasmaPercent, Is.EqualTo(21));
        }

        [Test]
        public void 標の石_IsSpentByTeleport_NotByUseConsumable()
        {
            var state = Enter(1, Trial(null, new[] { Stone }), trial: true);
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.UseConsumable(state, 0));
        }

        [Test]
        public void 標の石_JumpsToAnyUnresolvedNode_TheBossIncluded_PayingItsTime()
        {
            // js: test/ext/world.test.mjs:427 (the core half); 試作で決めたこと 3
            var state = Enter(1, Trial(null, new[] { Stone, Herb }), trial: true);
            var far = state.Map.Nodes.First(n => !state.HasResolved(n.Id) && !state.Map.Neighbours(state.CurrentNodeId).Contains(n.Id));
            Assert.That(ExplorationReducer.CanTeleport(state, far.Id), Is.True);
            Assert.That(ExplorationReducer.CanTeleport(state, state.Map.BossId), Is.True);
            Assert.That(ExplorationReducer.CanTeleport(state, state.CurrentNodeId), Is.False, "a resolved node is no landing");
            Assert.That(ExplorationReducer.CanTeleport(state, 999), Is.False, "nor a node of no map");

            var after = ExplorationReducer.Teleport(state, 0, far.Id);
            Assert.That(after.CurrentNodeId, Is.EqualTo(far.Id));
            Assert.That(after.TimeLeft, Is.EqualTo(state.TimeLeft - 1));
            Assert.That(after.MiasmaPercent, Is.EqualTo(state.MiasmaPercent + state.EffectiveDensity));
            Assert.That(after.HasResolved(far.Id), Is.True);
            Assert.That(after.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "yakuso" }));

            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.Teleport(state, 1, far.Id), "no 標の石 in that slot");
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.Teleport(state, 5, far.Id));
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.Teleport(state, 0, state.CurrentNodeId));
        }

        [Test]
        public void 標の石_CannotJumpOnTheLayerBreak_OrWithNoTimeLeft_OrOutsideTrial()
        {
            // js: test/ext/world.test.mjs:488 (the core half)
            var state = Enter(1, Trial(null, new[] { Stone }), trial: true);
            int boss = state.Map.BossId;
            Assert.That(ExplorationReducer.CanTeleport(state.With(phase: RunPhase.LayerCleared), boss), Is.False);
            Assert.That(ExplorationReducer.CanTeleport(state.With(timeLeft: 0), boss), Is.False);
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.Teleport(state.With(phase: RunPhase.LayerCleared), 0, boss));
            var off = Enter(1, new RunLoadout(null, new[] { Stone }), trial: false);
            Assert.That(ExplorationReducer.CanTeleport(off, boss), Is.False);
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.Teleport(off, 0, boss));
        }

        [Test]
        public void ALethalJump_EndsTheLifeOnTheNode()
        {
            var state = Enter(1, Trial(null, new[] { Stone }), trial: true, miasma: 98);
            Assert.That(state.MiasmaPercent, Is.EqualTo(99));
            var after = ExplorationReducer.Teleport(state, 0, state.Map.BossId);
            Assert.That(after.Phase, Is.EqualTo(RunPhase.MiasmaDeath), "瘴気死 outranks the boss, as for a step");
        }

        [Test]
        public void TakeConsumable_FillsTheSlots_AndRefusesAnEndedRun()
        {
            var state = Enter(1, Trial(null, new[] { Herb }), trial: true);
            var after = ExplorationReducer.TakeConsumable(state, Water);
            Assert.That(after.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "yakuso", "wasuremizu" }));
            Assert.That(after.TimeLeft, Is.EqualTo(state.TimeLeft), "costs no 刻限");
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.TakeConsumable(ExplorationReducer.EndLife(state), Water));
        }

        // ---------------------------------------------------------------- events (§5)

        [Test]
        public void TheEventTable_KeepsTheDocumentsWeightsAndContents()
        {
            // js: test/ext/world.test.mjs:631
            Assert.That(LayerEvents.Weights(1), Is.EqualTo(new[] { 50, 20, 30 }));
            Assert.That(LayerEvents.Weights(2), Is.EqualTo(new[] { 40, 30, 30 }));
            Assert.That(LayerEvents.Weights(3), Is.EqualTo(new[] { 30, 40, 30 }));
            Assert.That(LayerEvents.Weights(7), Is.EqualTo(new[] { 0, 70, 30 }));
            for (int layer = 1; layer <= 7; layer++) Assert.That(LayerEvents.Weights(layer).Sum(), Is.EqualTo(100));
            Assert.That(new[] { 1, 2, 3, 7 }.Select(LayerEvents.SpringPercent), Is.EqualTo(new[] { 20, 25, 30, 30 }));
            Assert.That(LayerEvents.ChestContents(1), Is.EqualTo(new ChestContents(1, 1, 0)));
            Assert.That(LayerEvents.ChestContents(2), Is.EqualTo(new ChestContents(1, 0, 1)));
            Assert.That(LayerEvents.ChestContents(3), Is.EqualTo(new ChestContents(1, 1, 1)));
            Assert.That(LayerEvents.ChestContents(7), Is.EqualTo(LayerEvents.ChestContents(3)));
            Assert.That(LayerEvents.HasChoice(EventKind.Spring), Is.False);
            Assert.That(LayerEvents.HasChoice(EventKind.HeavyChest), Is.True);
            Assert.That(LayerEvents.HasChoice(EventKind.StrangeMeal), Is.True);
            Assert.That(LayerEvents.WeightOf(EventKind.HeavyChest, 2), Is.EqualTo(30));
            Assert.That(LayerEvents.WeightOf(EventKind.Spring, 6), Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => LayerEvents.Weights(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => LayerEvents.SpringPercent(8));
        }

        [Test]
        public void TheMapGenerator_IsUntouched_TheEventsLiveBesideIt()
        {
            // js: test/ext/world.test.mjs:929
            var spec = SevenLayers.Of(1).MapSpec;
            var a = MapGenerator.Generate(spec, 11UL);
            var b = SevenLayers.Of(1).Map(11UL);
            Assert.That(a.Nodes.Select(n => n.Kind), Is.EqualTo(b.Nodes.Select(n => n.Kind)));
        }

        [Test]
        public void TheDraw_IsSeededFromTheNode_InIdOrder()
        {
            var map = SevenLayers.Of(1).Map(9UL);
            Func<int, ulong> seedOf = id => 9UL * 31UL + (ulong)id;
            var a = LayerEvents.Draw(map, 1, seedOf);
            var b = LayerEvents.Draw(map, 1, seedOf);
            var events = map.Nodes.Where(n => n.Kind == NodeKind.Event).Select(n => n.Id).OrderBy(id => id).ToList();
            Assert.That(a.Keys, Is.EqualTo(events));
            Assert.That(a.Values, Is.EqualTo(b.Values));
            Assert.That(a.ContainsKey(map.EntryId), Is.False);
        }

        [Test]
        public void 泉_HealsTheLayersShare()
        {
            var state = Enter(2, Trial(), trial: true, hp: 20);
            Assert.That(ExplorationReducer.SpringHeal(state), Is.EqualTo(13), "25% of 50, rounded half away from zero");
            Assert.That(ExplorationReducer.ResolveEvent(state, EventKind.Spring, true).Hp, Is.EqualTo(33));
            Assert.That(ExplorationReducer.ResolveEvent(state.With(hp: 45), EventKind.Spring, true).Hp, Is.EqualTo(50), "up to the maximum");
        }

        [Test]
        public void 重い宝箱_CostsTwoMaxStamina_AndCurrentStaminaFollowsTheLowerMaximum()
        {
            var state = Enter(1, Trial(), trial: true, stamina: 10);
            var after = ExplorationReducer.ResolveEvent(state, EventKind.HeavyChest, true);
            Assert.That(after.TempMaxStaminaMod, Is.EqualTo(state.TempMaxStaminaMod - 2));
            Assert.That(after.MaxStamina, Is.EqualTo(state.MaxStamina - 2));
            Assert.That(after.Stamina, Is.EqualTo(Math.Min(state.Stamina, after.MaxStamina)));
            Assert.That(after.TimeLeft, Is.EqualTo(state.TimeLeft), "the 刻限 was paid by the step");
            Assert.That(ExplorationReducer.ResolveEvent(state.With(tempMaxStaminaMod: -4), EventKind.HeavyChest, true).TempMaxStaminaMod,
                Is.EqualTo(-4), "inside ±4");
        }

        [Test]
        public void 特殊な食事_AddsOneMaxStamina_AndTakesTenPercentHp_NeverBelowOne()
        {
            // js: test/ext/world.test.mjs:761 (the core half)
            var state = Enter(1, Trial(), trial: true, hp: 30);
            Assert.That(ExplorationReducer.MealCost(state), Is.EqualTo(5));
            var after = ExplorationReducer.ResolveEvent(state, EventKind.StrangeMeal, true);
            Assert.That(after.Hp, Is.EqualTo(25));
            Assert.That(after.TempMaxStaminaMod, Is.EqualTo(state.TempMaxStaminaMod + 1));
            Assert.That(ExplorationReducer.ResolveEvent(state.With(hp: 3), EventKind.StrangeMeal, true).Hp, Is.EqualTo(1));
            Assert.That(ExplorationReducer.ResolveEvent(state.With(hp: 2), EventKind.StrangeMeal, true).Hp, Is.EqualTo(1));
            Assert.That(ExplorationReducer.ResolveEvent(state.With(hp: 1), EventKind.StrangeMeal, true).Hp, Is.EqualTo(1));
            Assert.That(ExplorationReducer.ResolveEvent(state.With(hp: 3), EventKind.StrangeMeal, true).Phase, Is.EqualTo(RunPhase.Exploring),
                "a meal never kills");
        }

        [Test]
        public void 受けない_ChangesNothing_AndAnEndedRunTakesNoEvent()
        {
            var state = Enter(1, Trial(), trial: true);
            Assert.That(ExplorationReducer.ResolveEvent(state, EventKind.HeavyChest, false), Is.SameAs(state));
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.ResolveEvent(ExplorationReducer.EndLife(state), EventKind.Spring, true));
        }
    }
}
