using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;

namespace DungeonCore.Tests
{
    /// <summary>
    /// #106: a death leaves a trace (concept-v3.md §8.3), the trace offers the grown cards and the filled
    /// pages under the shallow-death rule (§8.4 / §14), and a life chooses at most three from the first
    /// trace it steps on.
    /// </summary>
    public class HeritageTests
    {
        private const int PlayerMaxHp = 50;

        private static readonly LegacyCard StepOne = new LegacyCard("thrust", 1, "鋭い突き");
        private static readonly LegacyCard StepTwo = new LegacyCard("kesa_cut", 2, "重ね袈裟");
        private static readonly LegacyCard StepThree = new LegacyCard("overhead", 3, "天墜");
        private static readonly LegacyCard StepZero = new LegacyCard("pierce", 0, "貫き");
        private static readonly LegacyPage PageA = new LegacyPage("enemy:rust_spear", "錆槍の竜兵の頁");
        private static readonly LegacyPage PageB = new LegacyPage("dungeon:1", "燦光の樹海の頁");

        // ---- (a) death from both causes ----

        [Test]
        public void ABattleLost_IsADeath_AndLeavesATraceWhereItFell()
        {
            ExplorationState fallen = ExplorationReducer.ReportBattle(Enter(2, 7UL), 0, 0);
            DeathTrace trace = Heritage.Leave(fallen, 1, 1, null, new[] { StepOne }, new[] { PageA });
            Assert.Multiple(() =>
            {
                Assert.That(Heritage.CauseOf(fallen), Is.EqualTo(DeathCause.Fallen));
                Assert.That(trace.Cause, Is.EqualTo(DeathCause.Fallen));
                Assert.That(trace.Place, Is.EqualTo(new DeathPlace(2, NodeKind.Battle)), "the entry is always a battle");
                Assert.That(trace.Name, Is.EqualTo("1 代目"), "a stand-in until #110 names the generations");
                Assert.That(trace.Candidates.Select(c => c.Key), Is.EqualTo(new[] { "card:thrust", "page:enemy:rust_spear" }));
            });
        }

        [Test]
        public void TheGaugeReachingDeathAt_IsADeath_OnTheNodeThatTippedIt()
        {
            var profile = SevenLayers.Of(1);
            ExplorationState dead = ExplorationReducer.Enter(profile, profile.Map(3UL), RunLoadout.Empty,
                PlayerMaxHp, PlayerMaxHp, 10, miasmaPercent: Miasma.DeathAt - 1);
            Assert.That(dead.Phase, Is.EqualTo(RunPhase.MiasmaDeath));

            DeathTrace trace = Heritage.Leave(dead, 4, 2, "ソラ", null, null);
            Assert.Multiple(() =>
            {
                Assert.That(Heritage.CauseOf(dead), Is.EqualTo(DeathCause.Miasma));
                Assert.That(trace.Place, Is.EqualTo(new DeathPlace(1, NodeKind.Battle)));
                Assert.That(trace.Id, Is.EqualTo(4));
                Assert.That(trace.Life, Is.EqualTo(2));
                Assert.That(trace.Name, Is.EqualTo("ソラ"));
                Assert.That(trace.Candidates, Is.Empty);
            });
        }

        [Test]
        public void ALifeThatGoesOn_SurvivesOrEnds_IsNoDeath_AndLeavesNoTrace()
        {
            ExplorationState walking = Enter(1, 3UL);
            ExplorationState survived = ExplorationReducer.EndLife(walking);
            Assert.Multiple(() =>
            {
                Assert.That(Heritage.CauseOf(walking), Is.Null);
                Assert.That(Heritage.CauseOf(survived), Is.Null, "the 生存ルート is not a death");
                Assert.That(Heritage.CauseOf(walking.With(phase: RunPhase.Completed)), Is.Null);
                Assert.Throws<ExplorationRuleException>(() => Heritage.Leave(survived, 1, 1, null, null, null));
            });
        }

        // ---- (c) candidates and the shallow-death rule ----

        [Test]
        public void ShallowDeath_KeepsStepOneCardsOnly_DeepDeathKeepsEveryStep()
        {
            var cards = new[] { StepZero, StepOne, StepTwo, StepThree };
            Assert.Multiple(() =>
            {
                Assert.That(Heritage.Candidates(1, cards, null).Select(c => c.Id), Is.EqualTo(new[] { "thrust" }));
                Assert.That(Heritage.Candidates(Heritage.DeepLayer - 1, cards, null).Select(c => c.Id), Is.EqualTo(new[] { "thrust" }));
                Assert.That(Heritage.Candidates(Heritage.DeepLayer, cards, null).Select(c => c.Id),
                    Is.EqualTo(new[] { "overhead", "kesa_cut", "thrust" }), "deepest step first");
                Assert.That(Heritage.Candidates(7, cards, null).Select(c => c.Rank), Is.EqualTo(new[] { 3, 2, 1 }));
            });
        }

        [Test]
        public void IsHeritable_FollowsStepTimesLayer()
        {
            Assert.Multiple(() =>
            {
                for (int layer = 1; layer <= SevenLayers.Count; layer++)
                {
                    Assert.That(Heritage.IsHeritable(0, layer), Is.False, $"step 0 on layer {layer}");
                    Assert.That(Heritage.IsHeritable(1, layer), Is.True, $"step 1 on layer {layer}");
                    Assert.That(Heritage.IsHeritable(2, layer), Is.EqualTo(layer >= 3), $"step 2 on layer {layer}");
                    Assert.That(Heritage.IsHeritable(3, layer), Is.EqualTo(layer >= 3), $"step 3 on layer {layer}");
                }
            });
        }

        [Test]
        public void Candidates_PutCardsBeforePages_AndCountEachOnce()
        {
            var list = Heritage.Candidates(4,
                new[] { StepOne, StepTwo, StepOne },
                new[] { PageA, PageB, PageA });
            Assert.Multiple(() =>
            {
                Assert.That(list.Select(c => c.Key), Is.EqualTo(new[]
                {
                    "card:kesa_cut", "card:thrust", "page:enemy:rust_spear", "page:dungeon:1",
                }));
                Assert.That(list.Select(c => c.Title), Is.EqualTo(new[] { "重ね袈裟", "鋭い突き", "錆槍の竜兵の頁", "燦光の樹海の頁" }));
                Assert.That(list.Where(c => c.Kind == LegacyKind.Page).All(c => c.Rank == 0), Is.True);
            });
        }

        // ---- (d) choosing ----

        [Test]
        public void Choosing_HandsOnUpToThree_CardsKeepTheirStep()
        {
            DeathTrace trace = DeepTrace();
            Bequest bequest = Heritage.Choose(trace, new[] { "page:dungeon:1", "card:overhead", "card:thrust" });
            Assert.Multiple(() =>
            {
                Assert.That(bequest.TraceId, Is.EqualTo(trace.Id));
                Assert.That(bequest.Cards, Is.EqualTo(new[] { StepThree, StepOne }), "in the list's order, each with its step");
                Assert.That(bequest.Pages, Is.EqualTo(new[] { PageB }));
            });
        }

        [Test]
        public void Choosing_RefusesMoreThanThree_UnknownKeys_AndTwiceTheSame()
        {
            DeathTrace trace = DeepTrace();
            var four = trace.Candidates.Take(Heritage.PickLimit + 1).Select(c => c.Key).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(four.Count, Is.EqualTo(Heritage.PickLimit + 1));
                Assert.That(Heritage.CanChoose(trace, four), Is.False);
                Assert.Throws<ExplorationRuleException>(() => Heritage.Choose(trace, four));
                Assert.Throws<ExplorationRuleException>(() => Heritage.Choose(trace, new[] { "card:nothing" }));
                Assert.Throws<ExplorationRuleException>(() => Heritage.Choose(trace, new[] { "card:thrust", "card:thrust" }));
                Assert.Throws<ExplorationRuleException>(() => Heritage.Choose(trace, new[] { "page:thrust" }), "a card's id is not a page's key");
                Assert.Throws<ExplorationRuleException>(() => Heritage.Choose(trace, null));
            });
        }

        [Test]
        public void ChoosingNothing_IsAChoice_AndHandsOnNothing()
        {
            Bequest bequest = Heritage.Choose(DeepTrace(), Array.Empty<string>());
            Assert.Multiple(() =>
            {
                Assert.That(bequest.Cards, Is.Empty);
                Assert.That(bequest.Pages, Is.Empty);
            });
        }

        // ---- (b) the ledger: traces stay, one per life ----

        [Test]
        public void Traces_StayUntilCollected_ALaterDeathDoesNotOverwrite()
        {
            TraceLedger ledger = TraceLedger.Empty
                .Leave(Fallen(3), 1, null, new[] { StepTwo }, null)
                .BeginLife()
                .Leave(Fallen(3), 2, null, new[] { StepOne }, null)
                .BeginLife();
            Assert.Multiple(() =>
            {
                Assert.That(ledger.Traces.Select(t => t.Id), Is.EqualTo(new[] { 1, 2 }));
                Assert.That(ledger.Traces.Select(t => t.Life), Is.EqualTo(new[] { 1, 2 }));
                Assert.That(ledger.NextId, Is.EqualTo(3));
                Assert.That(ledger.TraceIn(3)?.Id, Is.EqualTo(1), "the oldest trace of a layer is the one its map shows");
                Assert.That(ledger.TraceIn(2), Is.Null);
            });
        }

        [Test]
        public void ALife_ReceivesFromTheFirstTraceItStepsOn_Only()
        {
            TraceLedger ledger = TraceLedger.Empty
                .Leave(Fallen(1), 1, null, new[] { StepOne }, null)
                .Leave(Fallen(2), 2, null, new[] { StepOne }, new[] { PageA })
                .BeginLife();
            Assert.That(ledger.MayReceive, Is.True);

            TraceLedger visited = ledger.Visit(1);
            Assert.Multiple(() =>
            {
                Assert.That(visited.ClaimedTraceId, Is.EqualTo(1));
                Assert.That(visited.Pending?.Id, Is.EqualTo(1));
                Assert.That(visited.MayReceive, Is.False);
                Assert.That(visited.Visit(2).ClaimedTraceId, Is.EqualTo(1), "a second trace opens nothing");
                Assert.That(ledger.Visit(5).ClaimedTraceId, Is.Null, "a layer with no trace claims nothing");
            });

            TraceLedger collected = visited.Collect(new[] { "card:thrust" }, out Bequest bequest);
            Assert.Multiple(() =>
            {
                Assert.That(bequest.TraceId, Is.EqualTo(1));
                Assert.That(bequest.Cards, Is.EqualTo(new[] { StepOne }));
                Assert.That(collected.Traces.Select(t => t.Id), Is.EqualTo(new[] { 2 }), "the collected trace is gone, the other stays");
                Assert.That(collected.HasReceived, Is.True);
                Assert.That(collected.Pending, Is.Null);
                Assert.That(collected.Visit(2).Pending, Is.Null, "nothing more for this life");
                Assert.Throws<ExplorationRuleException>(() => collected.Collect(Array.Empty<string>(), out _));
            });

            TraceLedger nextLife = collected.BeginLife().Visit(2);
            Assert.That(nextLife.Pending?.Id, Is.EqualTo(2), "a new life may receive again");
        }

        [Test]
        public void ATraceLeftUnchosen_StaysForALaterLife()
        {
            TraceLedger visited = TraceLedger.Empty.Leave(Fallen(1), 1, null, new[] { StepOne }, null).BeginLife().Visit(1);
            TraceLedger afterDeath = visited.Leave(Fallen(2), 2, null, null, null).BeginLife();
            Assert.Multiple(() =>
            {
                Assert.That(afterDeath.Traces.Select(t => t.Id), Is.EqualTo(new[] { 1, 2 }));
                Assert.That(afterDeath.Pending, Is.Null);
                Assert.That(afterDeath.Visit(1).Pending?.Id, Is.EqualTo(1));
            });
        }

        [Test]
        public void Collecting_WithNothingPending_IsRefused()
        {
            TraceLedger ledger = TraceLedger.Empty.Leave(Fallen(1), 1, null, null, null).BeginLife();
            Assert.Throws<ExplorationRuleException>(() => ledger.Collect(Array.Empty<string>(), out _));
        }

        [Test]
        public void AddingATraceTwice_IsRefused_AndNextIdSkipsPastIt()
        {
            DeathTrace trace = DeepTrace();
            TraceLedger ledger = TraceLedger.Empty.Add(trace);
            Assert.Multiple(() =>
            {
                Assert.That(ledger.NextId, Is.EqualTo(trace.Id + 1));
                Assert.Throws<ArgumentException>(() => ledger.Add(trace));
            });
        }

        // ---- (e) the death place on a random map ----

        [Test]
        public void EveryLayer_TakesATrace_InPlaceOfOneBattle_OnADetour()
        {
            foreach (LayerProfile profile in SevenLayers.All)
            {
                TraceLedger ledger = TraceLedger.Empty.Add(new DeathTrace(1, 1, "x", new DeathPlace(profile.Layer, NodeKind.Battle), DeathCause.Fallen, Array.Empty<LegacyCandidate>()));
                for (ulong seed = 1; seed <= 20; seed++)
                {
                    LayerMap plain = profile.Map(seed);
                    LayerMap traced = ledger.MapFor(profile, seed);
                    Assert.Multiple(() =>
                    {
                        Assert.That(traced.CountOf(NodeKind.Trace), Is.EqualTo(1), $"layer {profile.Layer} seed {seed}");
                        Assert.That(traced.CountOf(NodeKind.Battle), Is.EqualTo(plain.CountOf(NodeKind.Battle) - 1));
                        Assert.That(traced.NodeCount, Is.EqualTo(plain.NodeCount));
                        Assert.That(traced.Node(traced.EntryId).Kind, Is.EqualTo(NodeKind.Battle), "the entry stays a battle");
                        MapNode trace = traced.Nodes.Single(n => n.Kind == NodeKind.Trace);
                        Assert.That(traced.Row(trace.Row).Any(n => !n.Kind.IsDetour()), Is.True, "a trace is never forced");
                    });
                }
            }
        }

        [Test]
        public void ALayerWithoutATrace_DrawsTheSameMapAsBefore()
        {
            TraceLedger ledger = TraceLedger.Empty.Add(new DeathTrace(1, 1, "x", new DeathPlace(4, NodeKind.Boss), DeathCause.Fallen, Array.Empty<LegacyCandidate>()));
            foreach (LayerProfile profile in SevenLayers.All.Where(p => p.Layer != 4))
            {
                Assert.That(ledger.MapFor(profile, 9UL).Fingerprint(), Is.EqualTo(profile.Map(9UL).Fingerprint()), $"layer {profile.Layer}");
            }
        }

        // ---- helpers ----

        private static ExplorationState Enter(int layer, ulong seed)
        {
            var profile = SevenLayers.Of(layer);
            return ExplorationReducer.Enter(profile, profile.Map(seed), RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);
        }

        private static ExplorationState Fallen(int layer) => ExplorationReducer.ReportBattle(Enter(layer, 5UL), 0, 0);

        private static DeathTrace DeepTrace() =>
            Heritage.Leave(Fallen(5), 7, 3, null, new[] { StepOne, StepTwo, StepThree }, new[] { PageA, PageB });
    }
}
