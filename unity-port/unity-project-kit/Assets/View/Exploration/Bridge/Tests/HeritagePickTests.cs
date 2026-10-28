// #106: the choosing screen at a 死亡地点の痕跡 as HeritagePicks builds it from Expedition, what a confirmed
// choice hands the life (cards keep their step), and what an expedition's dying life leaves.
// Runs under `dotnet test` (Exploration.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction.Bridge;
using DungeonCore;
using NUnit.Framework;

namespace Exploration.Bridge.Tests
{
    public class HeritagePickTests
    {
        [Test]
        public void NoTraceWaiting_ShowsNoScreen_AndConfirmsNothing()
        {
            var expedition = new Expedition(new RunStart { Seed = 4, Traces = Ledger(1) });
            Assert.That(HeritagePicks.For(expedition).IsShown, Is.False, "the trace is not reached yet");
            Assert.That(HeritagePicks.Confirm(expedition, Array.Empty<string>()), Is.False);
            Assert.That(expedition.Session.Traces.Traces.Count, Is.EqualTo(1));
        }

        [Test]
        public void SteppingOnTheTrace_LaysOutEveryCandidate_CardsWithTheirFaceAtTheirStep()
        {
            var expedition = new Expedition(new RunStart { Seed = 4, Traces = Ledger(1) });
            WalkTo(expedition, NodeKind.Trace);

            HeritagePick pick = HeritagePicks.For(expedition);
            Assert.Multiple(() =>
            {
                Assert.That(pick.IsShown, Is.True);
                Assert.That(pick.TraceId, Is.EqualTo(1));
                Assert.That(pick.Title, Is.EqualTo(HeritagePicks.Title));
                Assert.That(pick.Prompt, Is.EqualTo("1 代目は第 1 層の通常戦闘で倒れた。3 つまで継げます。選ばなかったものは失われます。"));
                Assert.That(pick.Choices.Select(c => c.Key), Is.EqualTo(new[]
                {
                    "card:" + CardCatalog.Thrust.Id, "card:" + CardCatalog.FangRush.Id, "page:p1", "page:p2",
                }));
                Assert.That(pick.Choices[0].Face.Name, Is.EqualTo("鋭い突き"), "the face at step 1");
                Assert.That(pick.Choices[0].Caption, Is.EqualTo("習熟 1 段目"));
                Assert.That(pick.Choices[2].Face, Is.Null, "a page has no card face");
                Assert.That(pick.Choices[2].Caption, Is.EqualTo(HeritagePicks.PageCaption));
                Assert.That(pick.Choices[2].Title, Is.EqualTo("頁 1"));
                Assert.That(pick.SelectedCount, Is.EqualTo(0));
                Assert.That(pick.CountLine, Is.EqualTo("選んだ数 0 / 3"));
            });
        }

        [Test]
        public void Toggling_StopsAtThree_AndTakesAChoiceBackOff()
        {
            var expedition = new Expedition(new RunStart { Seed = 4, Traces = Ledger(1) });
            WalkTo(expedition, NodeKind.Trace);

            IReadOnlyList<string> selected = Array.Empty<string>();
            foreach (HeritageChoice choice in HeritagePicks.For(expedition).Choices) selected = HeritagePicks.Toggle(expedition, selected, choice.Key);
            Assert.That(selected, Is.EqualTo(new[] { "card:" + CardCatalog.Thrust.Id, "card:" + CardCatalog.FangRush.Id, "page:p1" }), "the fourth is refused");

            selected = HeritagePicks.Toggle(expedition, selected, "card:" + CardCatalog.FangRush.Id);
            selected = HeritagePicks.Toggle(expedition, selected, "page:p2");
            selected = HeritagePicks.Toggle(expedition, selected, "card:nothing");
            HeritagePick pick = HeritagePicks.For(expedition, selected);
            Assert.Multiple(() =>
            {
                Assert.That(selected, Is.EqualTo(new[] { "card:" + CardCatalog.Thrust.Id, "page:p1", "page:p2" }));
                Assert.That(pick.SelectedCount, Is.EqualTo(3));
                Assert.That(pick.Choices.Where(c => c.Selected).Select(c => c.Key), Is.EqualTo(selected));
            });
        }

        [Test]
        public void Confirming_HandsTheCardsOnAtTheirStep_AndCollectsTheTrace()
        {
            var expedition = new Expedition(new RunStart { Seed = 4, Traces = Ledger(1) });
            WalkTo(expedition, NodeKind.Trace);
            string fang = CardCatalog.FangRush.Id;
            string thrust = CardCatalog.Thrust.Id;
            int thrustCopies = expedition.Deck.Count(c => c.Def.Id == thrust);
            Assert.That(expedition.Owned.Contains(fang), Is.False, "a learned card nobody taught this life");

            Assert.That(HeritagePicks.Confirm(expedition, new[] { "card:" + fang, "card:" + thrust, "page:p2" }), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(expedition.Owned.Contains(fang), Is.True);
                Assert.That(expedition.Deck.Count(c => c.Def.Id == fang), Is.EqualTo(1), "a kind the deck lacks goes in once");
                Assert.That(expedition.Deck.Count(c => c.Def.Id == thrust), Is.EqualTo(thrustCopies), "a kind the deck holds gets no copy");
                Assert.That(expedition.Mastery.RankOf(fang), Is.EqualTo(1));
                Assert.That(expedition.Mastery.RankOf(thrust), Is.EqualTo(1));
                Assert.That(expedition.Mastery.Mastered(CardCatalog.Thrust).Name, Is.EqualTo("鋭い突き"));
                Assert.That(expedition.InheritedPages.Select(p => p.PageId), Is.EqualTo(new[] { "p2" }));
                Assert.That(expedition.Session.Traces.Traces, Is.Empty);
                Assert.That(HeritagePicks.For(expedition).IsShown, Is.False);
                Assert.That(HeritagePicks.Confirm(expedition, Array.Empty<string>()), Is.False, "one trace per life");
            });
        }

        [Test]
        public void ARefusedChoice_ChangesNothing()
        {
            var expedition = new Expedition(new RunStart { Seed = 4, Traces = Ledger(1) });
            WalkTo(expedition, NodeKind.Trace);
            var four = HeritagePicks.For(expedition).Choices.Select(c => c.Key).ToList();
            Assert.That(HeritagePicks.Confirm(expedition, four), Is.False);
            Assert.That(HeritagePicks.For(expedition).IsShown, Is.True, "the trace still waits");
            Assert.That(expedition.Owned.Contains(CardCatalog.FangRush.Id), Is.False);
        }

        [Test]
        public void AnExpeditionsDeath_LeavesItsGrownCardsAndFilledPages()
        {
            var expedition = new Expedition(new RunStart { Seed = 4 });
            expedition.Legacy.Journal = new Pages(new LegacyPage("enemy:x", "x の頁"));
            Assert.That(expedition.Session.Act(ScreenAction.FallInBattle), Is.True);

            DeathTrace trace = expedition.Session.Traces.Traces.Single();
            Assert.Multiple(() =>
            {
                Assert.That(trace.Name, Is.EqualTo("1 代目"));
                Assert.That(trace.Candidates.Select(c => c.Key), Is.EqualTo(new[] { "page:enemy:x" }), "no card has grown on the entry yet");
            });

            Assert.That(expedition.Session.Act(ScreenAction.NewLife), Is.True);
            Assert.That(expedition.Session.State.Map.CountOf(NodeKind.Trace), Is.EqualTo(1));
        }

        [Test]
        public void CardsOf_ListsEveryCardAtStepOneOrMore_WithItsNameThere()
        {
            MasteryBook book = MasteryBook.Begin(null);
            for (int i = 0; i < Constants.MasteryThresholds[0]; i++) book = book.Train(CardCatalog.Thrust.Id).Book;
            book = book.Train(CardCatalog.KesaCut.Id).Book;

            Assert.That(Legacies.CardsOf(book), Is.EqualTo(new[] { new LegacyCard(CardCatalog.Thrust.Id, 1, "鋭い突き") }),
                "a card with ticks but no step is not left");
            Assert.That(Legacies.CardsOf(MasteryBook.Begin(null)), Is.Empty);
        }

        [Test]
        public void WithRank_SetsTheStepAndItsTicks_KeepsAHigherOne_AndHonoursTheOneTopCard()
        {
            string thrust = CardCatalog.Thrust.Id;
            string overhead = CardCatalog.Overhead.Id;
            MasteryBook book = Legacies.WithRank(MasteryBook.Begin(null), thrust, 2);
            Assert.Multiple(() =>
            {
                Assert.That(book.RankOf(thrust), Is.EqualTo(2));
                Assert.That(book.TicksOf(thrust), Is.EqualTo(Constants.MasteryThresholds[1]));
                Assert.That(Legacies.WithRank(book, thrust, 1).RankOf(thrust), Is.EqualTo(2), "a step already passed is kept");
            });

            MasteryBook top = Legacies.WithRank(book, overhead, 3);
            MasteryBook second = Legacies.WithRank(top, thrust, 3);
            Assert.Multiple(() =>
            {
                Assert.That(top.TopRankHolder, Is.EqualTo(overhead));
                Assert.That(second.RankOf(thrust), Is.EqualTo(2), "only one card of a life holds step 3");
                Assert.That(second.TopRankHolder, Is.EqualTo(overhead));
                string plain = CardCatalog.All.First(d => MasteryProfiles.Find(d.Id) == null).Id;
                Assert.That(Legacies.WithRank(book, plain, 1), Is.SameAs(book), "a card with no profile holds no step");
            });
        }

        // ---- helpers ----

        /// <summary>A ledger with one trace on each layer given: two cards at step 1 (one the deck has, one learned) and two pages.</summary>
        private static TraceLedger Ledger(params int[] layers)
        {
            TraceLedger ledger = TraceLedger.Empty;
            for (int i = 0; i < layers.Length; i++)
            {
                var candidates = Heritage.Candidates(layers[i],
                    new[]
                    {
                        new LegacyCard(CardCatalog.Thrust.Id, 1, "鋭い突き"),
                        new LegacyCard(CardCatalog.FangRush.Id, 1, "牙の強襲"),
                    },
                    new[] { new LegacyPage("p1", "頁 1"), new LegacyPage("p2", "頁 2") });
                ledger = ledger.Add(new DeathTrace(i + 1, i + 1, Heritage.StandInName(i + 1),
                    new DeathPlace(layers[i], NodeKind.Battle), DeathCause.Fallen, candidates));
            }
            return ledger;
        }

        private sealed class Pages : ILegacyPages
        {
            private readonly LegacyPage[] _pages;

            public Pages(params LegacyPage[] pages)
            {
                _pages = pages;
            }

            public IReadOnlyList<LegacyPage> FilledPages() => _pages;
        }

        /// <summary>Wins whatever battle waits, at full HP, without playing it out.</summary>
        private static void WinPending(Expedition expedition)
        {
            EncounterBattle battle = expedition.StartEncounter();
            Assert.That(battle, Is.Not.Null);
            while (!battle.Over)
            {
                CoreBattleSource source = battle.StartNext();
                BattleState state = source.State;
                BattleState won = state with { Result = GameResult.Won, Player = state.Player with { Hp = Constants.PlayerMaxHp, Stamina = 5 } };
                battle.Finish(won, new List<BattleEvent>(source.History));
            }
            expedition.Return();
        }

        /// <summary>Walks the shortest way to the nearest unresolved node of this kind, winning every battle on the way.</summary>
        private static void WalkTo(Expedition expedition, NodeKind kind)
        {
            ExplorationSession session = expedition.Session;
            if (session.HasPendingBattle) WinPending(expedition);
            ExplorationState state = session.State;
            LayerMap map = state.Map;
            var from = new Dictionary<int, int> { [state.CurrentNodeId] = -1 };
            var queue = new Queue<int>();
            queue.Enqueue(state.CurrentNodeId);
            int target = -1;
            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                if (map.Node(node).Kind == kind && !state.HasResolved(node))
                {
                    target = node;
                    break;
                }
                foreach (int next in map.Neighbours(node))
                {
                    if (from.ContainsKey(next)) continue;
                    from[next] = node;
                    queue.Enqueue(next);
                }
            }
            Assert.That(target, Is.Not.EqualTo(-1), $"a {kind} node is reachable");
            var path = new List<int>();
            for (int node = target; node != state.CurrentNodeId; node = from[node]) path.Add(node);
            path.Reverse();
            foreach (int node in path)
            {
                Assert.That(session.Step(node), Is.True, $"step to {node}");
                if (session.HasPendingStep) Assert.That(session.Act(ScreenAction.ConfirmStep), Is.True);
                if (session.HasPendingBattle) WinPending(expedition);
            }
        }
    }
}
