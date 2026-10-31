// The trial extension's results and what the lineage knows (the browser trial's docs/EXTENSIONS.md 「探索の結果と再開」,
// X-5〜X-13): the session and screen half of test/ext/explore-results.test.mjs. The pending result cards, the deck's
// room and the trace card's gains are the Expedition's (Exploration.Bridge) and are tested there.
using System.Linq;
using DungeonCore;
using Exploration;
using NUnit.Framework;
using static Exploration.Tests.TrialTestKit;

namespace Exploration.Tests
{
    public class TrialResultsTests
    {
        /// <summary>The lineage's map seam (Trial.Bridge's Life): the carving is taken out once the lineage knows it.</summary>
        private static LayerMap MapForKnown(LayerProfile profile, ulong seed)
        {
            var spec = profile.MapSpec;
            if (spec.Quotas.ContainsKey(NodeKind.Carving)) spec = spec.With(NodeKind.Carving, 0);
            return MapGenerator.Generate(spec, seed);
        }

        // ---------------------------------------------------------------- the carving known from an earlier life (X-5, R-20)

        [Test]
        public void ACarvingKnownToTheLineage_NamesLayerTwoFromTheLifesStart()
        {
            // js: test/ext/explore-results.test.mjs:252
            var s = Trial(5, mapFor: MapForKnown);
            Assert.That(s.CarvingTaken, Is.True);
            WalkToLayer(s, 2);
            var screen = s.Screen();
            Assert.That(screen.LayerTitle, Is.EqualTo($"第 2 層　{SevenLayers.Of(2).TrueName}"));
            Assert.That(s.State.Map.CountOf(NodeKind.Carving), Is.EqualTo(0));
            Assert.That(screen.Journal.Find("dungeon-2").Title, Does.Contain(SevenLayers.Of(2).TrueName));
        }

        [Test]
        public void TheInterludeBeforeLayerTwo_NamesItAsTheLifeKnowsIt()
        {
            // js: test/ext/explore-results.test.mjs:262
            foreach (var (known, name) in new[] { (true, SevenLayers.Of(2).TrueName), (false, SevenLayers.Of(2).Name) })
            {
                var s = known ? Trial(5, mapFor: MapForKnown) : Trial(5);
                WalkTo(s, n => n.Kind == NodeKind.Boss);
                Win(s);
                ClearChoices(s);
                Assert.That(s.Act(ScreenAction.GoToInterlude), Is.True);
                Assert.That(s.Screen().Card.Lines.Any(l => l.StartsWith($"次は第 2 層　{name}:", System.StringComparison.Ordinal)), Is.True, name);
            }
        }

        [Test]
        public void CarvingKnown_TheOptionWins_AndOutsideTrialEachLifeReadsItAgain()
        {
            // js: test/ext/explore-results.test.mjs:274
            Assert.That(Trial(5, carvingKnown: true).CarvingTaken, Is.True);
            Assert.That(Trial(5, mapFor: MapForKnown, carvingKnown: false).CarvingTaken, Is.False);
            Assert.That(Trial(5).CarvingTaken, Is.False);
            var plain = new ExplorationSession(new RunStart { Seed = 5, MapFor = MapForKnown });
            Assert.That(plain.CarvingTaken, Is.False);
            Assert.That(plain.State.Map.CountOf(NodeKind.Carving), Is.EqualTo(0), "the map seam draws the maps all the same");
        }

        // ---------------------------------------------------------------- the interlude and the journal (X-6, X-7, X-8, X-13)

        [Test]
        public void TheInterludesLines_KeepWhatTheRestGave_WhenAConsumableIsUsedThere()
        {
            // js: test/ext/explore-results.test.mjs:284
            var s = Trial(2, consumables: new[] { "koi_hoshiniku", "yakuso" });
            WalkTo(s, n => n.Kind == NodeKind.Boss);
            Win(s, 14, 2);
            ClearChoices(s);
            Assert.That(s.Act(ScreenAction.GoToInterlude), Is.True);
            Assert.That(s.InterludeState, Is.SameAs(s.State));
            var rest = s.Screen().Card.Lines.Take(3).ToList();
            int meat = s.State.Loadout.Consumables.Select(c => c.Id).ToList().IndexOf("koi_hoshiniku");
            Assert.That(s.UseConsumable(meat), Is.True);
            int herb = s.State.Loadout.Consumables.Select(c => c.Id).ToList().IndexOf("yakuso");
            Assert.That(s.UseConsumable(herb), Is.True);
            Assert.That(s.Screen().Card.Lines.Take(3), Is.EqualTo(rest));
            Assert.That(rest[0], Does.Contain($"で HP {s.InterludeState.Hp} / 50"));
            Assert.That(s.State.Hp, Is.GreaterThan(s.InterludeState.Hp), "the band reads the state, the card the rest");
        }

        [Test]
        public void TheInterludeBand_SaysWhatAStepBelowAdds_NotTheGapToTheCap()
        {
            // js: test/ext/explore-results.test.mjs:301
            var s = Trial(2);
            WalkTo(s, n => n.Kind == NodeKind.Boss);
            Win(s);
            ClearChoices(s);
            s.Act(ScreenAction.GoToInterlude);
            var below = s.TryDescend();
            Assert.That(s.Screen().Miasma.ForecastLabel, Is.EqualTo($"降りると +{below.EffectiveDensity}%"));
        }

        [Test]
        public void TheJournalsDensityRow_StopsAtTheFloor()
        {
            // js: test/ext/explore-results.test.mjs:313 (X-7)
            string RowOf(ExplorationSession s) =>
                s.Screen().Journal.Pages.First(p => p.Id.StartsWith("dungeon-", System.StringComparison.Ordinal)).Rows.First(r => r.Label == "瘴気の濃さ").Text;
            Assert.That(RowOf(Trial(1, new[] { Mask, Mask })), Is.EqualTo("1 刻限ごとに +1%（層の濃さ 1 − ツール 2、下限 1）"));
            var plain = new ExplorationSession(new RunStart { Seed = 1, Loadout = RunStart.TrialLoadout() });
            Assert.That(RowOf(plain), Is.EqualTo("1 刻限ごとに +1%（層の濃さ 1 − ツール 1）"));
        }

        [Test]
        public void TheJournalsTimeRow_CountsTheSand()
        {
            // W-3: 「the journal's 刻限 row counts it」.
            Assert.That(Row(Trial(1, new[] { Sand }), "dungeon-1", "刻限"), Does.StartWith("11（"));
        }

        [Test]
        public void X8_TheInterludeCard_IsThereUnderItsConfirmation()
        {
            // js: test/ext/explore-results.test.mjs:466
            var s = Trial(2);
            Assert.That(ExplorationScreenBuilder.InterludeCard(s), Is.Null, "none outside the interlude");
            WalkTo(s, n => n.Kind == NodeKind.Boss);
            Win(s);
            ClearChoices(s);
            s.Act(ScreenAction.GoToInterlude);
            var rest = s.Screen().Card;
            Assert.That(s.Act(ScreenAction.AskEndLife), Is.True);
            Assert.That(s.Screen().Card.Kind, Is.EqualTo(CardKind.ConfirmEndLife));
            var under = ExplorationScreenBuilder.InterludeCard(s);
            Assert.That(under.Kind, Is.EqualTo(CardKind.Interlude));
            Assert.That(under.Lines, Is.EqualTo(rest.Lines));
            Assert.That(under.Buttons.Select(b => b.Label), Is.EqualTo(rest.Buttons.Select(b => b.Label)));
        }

        [Test]
        public void X6_TheMasksNote_AtTheInterlude_IsTheNextLayers()
        {
            // js: test/ext/explore-results.test.mjs:569
            var s = Trial(2, new[] { Mask });
            string names = ExplorationText.ItemName(Mask);
            Assert.That(s.Screen().ReliefLabel, Is.EqualTo(ExplorationText.Relief(names, s.State.Profile.Density, s.State.EffectiveDensity)));
            WalkTo(s, n => n.Kind == NodeKind.Boss);
            Win(s);
            ClearChoices(s);
            s.Act(ScreenAction.GoToInterlude);
            var below = s.TryDescend();
            Assert.That(s.Screen().ReliefLabel, Is.EqualTo(ExplorationText.Relief(names, below.Profile.Density, below.EffectiveDensity)));
            Assert.That(s.Screen().Miasma.ForecastLabel, Is.EqualTo($"降りると +{below.EffectiveDensity}%"));
        }

        [Test]
        public void PlayerNote_DropsTheDesignStatusRemark_WhereThePlayerReadsIt()
        {
            // js: test/ext/explore-results.test.mjs:584 (X-14)
            Assert.That(ExplorationText.PlayerNote("ryurin_no_kakera"), Is.EqualTo("1 戦につき最初の 1 回だけ Guard 5"));
            Assert.That(ExplorationText.PlayerNote(Mask), Is.EqualTo(ExplorationText.ItemNote(Mask)));
            var s = Trial(1, new[] { "ryurin_no_kakera" });
            Assert.That(s.Screen().ToolCards.First(t => t.Id == "ryurin_no_kakera").Note, Is.EqualTo("1 戦につき最初の 1 回だけ Guard 5"));
        }

        [Test]
        public void TheHpLines_ReadTheRealGain_MarkedWhenTheMaximumStopsIt()
        {
            // js: test/ext/explore-results.test.mjs:591 (X-13)
            var s = Trial(2);
            WalkTo(s, n => n.Kind == NodeKind.Boss);
            Win(s, 45);
            ClearChoices(s);
            string line = s.Screen().Card.Lines.First(l => l.StartsWith("階層間の休憩", System.StringComparison.Ordinal));
            Assert.That(line, Is.EqualTo($"階層間の休憩: HP +5（最大の 30%{ExplorationText.CappedMark}）・スタミナ全回復・瘴気はたまらない"));
            s.Act(ScreenAction.GoToInterlude);
            Assert.That(s.Screen().Card.Lines[0], Is.EqualTo($"HP +5（最大の 30%{ExplorationText.CappedMark}）で HP 50 / 50"));
            // a rest node at full HP reads the same way (the first seed whose rest is in sight from the entry)
            string restLine = Enumerable.Range(1, 8)
                .Select(seed => Trial((ulong)seed).Screen().Nodes.FirstOrDefault(n => n.Kind == NodeKind.Rest && n.Lines.Count > 0))
                .First(n => n != null).Lines[0];
            Assert.That(restLine, Does.Match(@"^休む: HP \+0（最大の 15%・上限まで）・"));
            // the C# prints the share
            var plain = new ExplorationSession(new RunStart { Seed = 2 });
            WalkTo(plain, n => n.Kind == NodeKind.Boss);
            plain.ReportBattle(45, 5);
            Assert.That(plain.Screen().Card.Lines.First(l => l.StartsWith("階層間の休憩", System.StringComparison.Ordinal)),
                Is.EqualTo("階層間の休憩: HP +5（最大の 30%）・スタミナ全回復・瘴気はたまらない"));
        }

        [Test]
        public void X12_ReadingTheCarving_AddsAJournalPage_InTrialOnly()
        {
            // js: test/ext/explore-results.test.mjs:608
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var s = Trial(seed);
                WalkToLayer(s, 2);
                if (!s.State.Map.Nodes.Any(n => n.Kind == NodeKind.Carving)) continue;
                Assert.That(JournalPageOf(s), Is.Null, "not before it is read");
                WalkTo(s, n => n.Kind == NodeKind.Carving);
                if (s.State.IsOver) continue;
                Assert.That(s.CarvingTaken, Is.True);
                var page = JournalPageOf(s);
                Assert.That(page, Is.Not.Null);
                Assert.That(page.Title, Is.EqualTo(ExplorationText.CarvingPageTitle));
                Assert.That(page.Rows.Select(r => (r.Label, r.Text)), Is.EqualTo(ExplorationText.CarvingPageRows));
                Assert.That(JournalPageOf(Trial(seed, carvingKnown: true)), Is.Not.Null, "a lineage that read it knows the page too (R-20)");
                // outside trial the C#'s journal, even with the carving read
                var plain = new ExplorationSession(new RunStart { Seed = seed });
                WalkToLayer(plain, 2);
                WalkTo(plain, n => n.Kind == NodeKind.Carving);
                Assert.That(plain.CarvingTaken, Is.True);
                Assert.That(JournalPageOf(plain), Is.Null, "outside trial the C#'s journal");
                return;
            }
            Assert.Fail("no seed reached the layer-two carving");
        }

        private static Journal.JournalPage JournalPageOf(ExplorationSession s) =>
            s.Screen().Journal.Pages.FirstOrDefault(p => p.Id == "carving");

        // ---------------------------------------------------------------- X-9: the event's record says what was kept

        /// <summary>A trial life whose layer 1 holds a 重い宝箱, with the three slots full, walked onto it and accepted.</summary>
        private static ExplorationSession AtAChest()
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var s = Trial(seed, consumables: new[] { "yakuso", "yakuso", "yakuso" });
                var node = s.State.Map.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Event && s.EventAt(n.Id) == EventKind.HeavyChest);
                if (node == null) continue;
                WalkTo(s, n => n.Id == node.Id);
                if (!s.HasPendingEvent || !s.State.Loadout.Consumables.All(c => c.Id == "yakuso")) continue;
                s.Act(ScreenAction.AcceptEvent);
                var last = s.Events[s.Events.Count - 1];
                if (last.Cards.Count == 0 || !s.HasPendingFound) continue;
                return s;
            }
            Assert.Fail("no seed walks onto a chest that deals a card with full slots");
            return null;
        }

        private static System.Collections.Generic.IReadOnlyList<string> LastRecord(ExplorationSession s) => s.Events[s.Events.Count - 1].Record;

        [Test]
        public void X9_AChestsCardAndConsumableTurnedDown_LeaveTheRecord()
        {
            // js: test/ext/explore-results.test.mjs:342 (the session half: Expedition.CloseResult calls TurnDownFind)
            var s = AtAChest();
            string found = ExplorationText.FoundConsumable(ExplorationText.ItemName(s.PendingFound[0]));
            Assert.That(LastRecord(s).Skip(1), Is.EqualTo(new[] { ExplorationText.RecordCards(1), found }), "while undecided the finds are listed");
            Assert.That(s.TurnDownFind(s.Events.Count - 1, ExplorationText.RecordCards(1)), Is.True); // 「受け取らない」
            Assert.That(LastRecord(s).Skip(1), Is.EqualTo(new[] { found }));
            Assert.That(s.TurnDownFind(s.Events.Count - 1, ExplorationText.RecordCards(1)), Is.False, "nothing left to drop");
            Assert.That(s.TurnDownFind(99, found), Is.False);
            s.Act(ScreenAction.LeaveFound); // 「諦める」
            Assert.That(LastRecord(s)[0], Does.Match(@"^最大スタミナ \d+ → \d+$"));
            Assert.That(LastRecord(s).Count, Is.EqualTo(1));
            Assert.That(Row(s, "dungeon-1", "出来事"), Does.EndWith("重い宝箱: " + LastRecord(s)[0]));
            Assert.That(s.Events[s.Events.Count - 1].Lines.All(l => LastRecord(s).Contains(l)), Is.False,
                "the result card still says what the chest held");
        }

        [Test]
        public void X9_AChestsConsumableSwappedIn_StaysInTheRecord()
        {
            // js: test/ext/explore-results.test.mjs:356 (the session half)
            var s = AtAChest();
            string found = ExplorationText.FoundConsumable(ExplorationText.ItemName(s.PendingFound[0]));
            Assert.That(s.Act(ScreenAction.TakeFound, "0"), Is.True);
            Assert.That(LastRecord(s).Skip(1), Is.EqualTo(new[] { ExplorationText.RecordCards(1), found }));
        }

        [Test]
        public void X9_ABattlesFindGivenUp_TouchesNoEventRecord()
        {
            // js: test/ext/explore-results.test.mjs:377
            var s = AtAChest();
            s.TurnDownFind(s.Events.Count - 1, ExplorationText.RecordCards(1));
            s.Act(ScreenAction.LeaveFound);
            var before = s.Events.Select(e => e.Summary).ToList();
            s.Receive(new[] { "yakuso" }); // a battle's find with the slots full waits on a swap
            Assert.That(s.HasPendingFound, Is.True);
            s.Act(ScreenAction.LeaveFound);
            Assert.That(s.Events.Select(e => e.Summary), Is.EqualTo(before));
        }

        // ---------------------------------------------------------------- X-10: a training mark waits on its card

        /// <summary>Walks to the nearest rest node and trains there (the mark is earned; the card would open).</summary>
        private static void TrainAtARest(ExplorationSession s) => WalkTo(s, n => n.Kind == NodeKind.Rest, RestChoice.Train);

        [Test]
        public void X10_ARestsTraining_WaitsOnItsCard_UntilPutOffOrSpent()
        {
            // js: test/ext/explore-results.test.mjs:409 (the session half: Expedition.Train spends the mark)
            var s = Trial(3);
            Assert.That(s.TrainingWaiting, Is.False);
            TrainAtARest(s);
            Assert.That(s.State.TrainingMarks, Is.EqualTo(1));
            Assert.That(s.TrainingWaiting, Is.True, "the card opens on arrival");

            Assert.That(s.PutOffTraining(), Is.True); // 「あとで」
            Assert.That(s.TrainingWaiting, Is.False);
            Assert.That(s.State.TrainingMarks, Is.EqualTo(1), "the mark stays on the band");
            Assert.That(s.PutOffTraining(), Is.False);

            Assert.That(s.OpenTraining(), Is.True); // the band's 「訓練の刻み」
            Assert.That(s.TrainingWaiting, Is.True);
            Assert.That(s.SpendTrainingMark(), Is.True);
            Assert.That(s.State.TrainingMarks, Is.EqualTo(0));
            Assert.That(s.TrainingWaiting, Is.False, "no mark left to wait on");
            Assert.That(s.OpenTraining(), Is.False, "nothing to open with no mark");
        }

        [Test]
        public void X10_AJumpOntoARestsTraining_OpensTheCardToo()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var s = Trial(seed, consumables: new[] { "shirube_no_ishi" });
                Win(s);
                var rest = s.State.Map.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Rest && ExplorationReducer.CanTeleport(s.State, n.Id));
                if (rest == null) continue;
                s.UseConsumable(0);
                var options = s.Screen().Node(rest.Id).Options;
                Assert.That(options.Select(o => o.Label), Is.EqualTo(new[] { ExplorationText.JumpRestLabel, ExplorationText.JumpTrainLabel }));
                Assert.That(s.Jump(rest.Id, RestChoice.Train), Is.True);
                Assert.That(s.State.TrainingMarks, Is.EqualTo(1));
                Assert.That(s.TrainingWaiting, Is.True);
                return;
            }
            Assert.Fail("no seed has a rest to jump to");
        }

        [Test]
        public void X10_LeavingTheLayer_EndsTheWait_AndOutsideTrialNothingWaits()
        {
            // js: test/ext/explore-results.test.mjs:442
            var s = Trial(3);
            TrainAtARest(s);
            Assert.That(s.TrainingWaiting, Is.True);
            WalkTo(s, n => n.Kind == NodeKind.Boss);
            Win(s);
            ClearChoices(s);
            Assert.That(s.Act(ScreenAction.GoToInterlude), Is.True);
            Assert.That(s.State.TrainingMarks, Is.EqualTo(1), "the mark is still this layer's at the interlude");
            Assert.That(s.Act(ScreenAction.Descend), Is.True);
            Assert.That(s.State.TrainingMarks, Is.EqualTo(0), "the next layer starts with none (ExplorationReducer.Enter)");
            Assert.That(s.TrainingWaiting, Is.False);

            var plain = new ExplorationSession(new RunStart { Seed = 3, Loadout = RunStart.TrialLoadout() });
            TrainAtARest(plain);
            Assert.That(plain.State.TrainingMarks, Is.EqualTo(1));
            Assert.That(plain.TrainingWaiting, Is.False);
            Assert.That(plain.OpenTraining(), Is.False);
        }
    }
}
