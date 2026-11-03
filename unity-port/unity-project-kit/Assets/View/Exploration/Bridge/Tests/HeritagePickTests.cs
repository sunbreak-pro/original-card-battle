// #106: the choosing screen at a 死亡地点の痕跡 as HeritagePicks builds it from Expedition, what a confirmed
// choice hands the life (cards keep their step), and what an expedition's dying life leaves.
// Runs under `dotnet test` (Exploration.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction.Bridge;
using DungeonCore;
using Journal;
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
        public void AStartTheOptionsRewrite_KeepsItsLedger()
        {
            var expedition = new Expedition(new RunStart { Seed = 4, Traces = Ledger(1) },
                options: new ExpeditionOptions { Trial = true, CarvingKnown = true });
            Assert.That(expedition.Session.CarvingTaken, Is.True, "the start was rewritten");
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
                Assert.That(expedition.InheritedLegacyPages.Select(p => p.PageId), Is.EqualTo(new[] { "p2" }));
                Assert.That(expedition.Session.Traces.Traces, Is.Empty);
                Assert.That(HeritagePicks.For(expedition).IsShown, Is.False);
                Assert.That(HeritagePicks.Confirm(expedition, Array.Empty<string>()), Is.False, "one trace per life");
            });
        }

        [Test]
        public void TheScreen_MarksWhatIsChosen_AndLastsUntilItIsConfirmed()
        {
            var expedition = new Expedition(new RunStart { Seed = 4, Traces = Ledger(1) });
            WalkTo(expedition, NodeKind.Trace);

            IReadOnlyList<string> chosen = HeritagePicks.Toggle(expedition, Array.Empty<string>(), "page:p1");
            HeritagePick pick = HeritagePicks.For(expedition, chosen);
            Assert.Multiple(() =>
            {
                Assert.That(pick.Choices.Where(c => c.Selected).Select(c => c.Key), Is.EqualTo(new[] { "page:p1" }));
                Assert.That(pick.CountLine, Is.EqualTo("選んだ数 1 / 3"));
                Assert.That(pick.ConfirmLabel, Is.EqualTo(HeritagePicks.ConfirmLabel));
                Assert.That(expedition.Session.MapIsLive, Is.True, "the screen covers the map; the rule does not stop it (§7.5)");
                Assert.That(expedition.Session.Traces.Traces.Count, Is.EqualTo(1), "marking collects nothing");
            });

            Assert.That(HeritagePicks.Confirm(expedition, chosen), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(HeritagePicks.For(expedition, chosen).IsShown, Is.False);
                Assert.That(expedition.InheritedLegacyPages.Select(p => p.PageId), Is.EqualTo(new[] { "p1" }));
            });
        }

        [Test]
        public void ChoosingNothing_StillCollectsTheTrace()
        {
            var expedition = new Expedition(new RunStart { Seed = 4, Traces = Ledger(1) });
            WalkTo(expedition, NodeKind.Trace);

            Assert.That(HeritagePicks.Confirm(expedition, Array.Empty<string>()), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(expedition.Session.Traces.Traces, Is.Empty, "what was not chosen is lost with the trace (§7.3)");
                Assert.That(HeritagePicks.For(expedition).IsShown, Is.False);
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
        public void AnExpeditionsDeath_TakesItsPagesFromTheJournalPluggedIn()
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
        public void AnExpeditionsDeath_LeavesItsGrownCardsAndTheJournalsFilledPages()
        {
            var expedition = new Expedition(new RunStart { Seed = 4, Traces = Ledger(1) });
            WalkTo(expedition, NodeKind.Trace);
            Assert.That(HeritagePicks.Confirm(expedition, new[] { "card:" + CardCatalog.Thrust.Id }), Is.True);
            WalkTo(expedition, NodeKind.Battle, winLast: false);

            var pages = EnemyJournal.All
                .Where(e => expedition.DisclosureOf(e.Id) >= 1)
                .Select(e => "page:" + EnemyJournal.PageId(e.Id))
                .ToList();
            Assert.That(pages, Is.Not.Empty, "the battles on the way filled the journal");
            string enemy = EnemyJournal.All.First(e => expedition.DisclosureOf(e.Id) >= 1).Id;
            expedition.Journal.Meet(DungeonJournal.EventId(1, "spring"));
            expedition.Journal.Memo.Write("罠に注意", enemy);
            Assert.That(expedition.Session.Act(ScreenAction.FallInBattle), Is.True);

            DeathTrace trace = expedition.Session.Traces.Traces.Single();
            Assert.That(trace.Candidates.Select(c => c.Key),
                Is.EqualTo(new[] { "card:" + CardCatalog.Thrust.Id }.Concat(pages).Concat(new[] { "page:" + DungeonJournal.PageId(1), "page:memo-1" }).ToList()),
                "the card the life took in at step 1, then the enemy pages, a layer's dungeon page and the note; the page of terms is never on it");
            Assert.That(trace.Candidates.Where(c => c.Kind == LegacyKind.Page).Select(c => c.Title),
                Is.EqualTo(expedition.Journal.LegacyLines().Select(l => l.Text).ToList()), "each page is its journal line (§6.11)");
        }

        [Test]
        public void APageChosen_GoesIntoTheNextLivesJournal_AtTheLevelItHeld()
        {
            string enemy = EnemyJournal.All[0].Id;
            string other = EnemyJournal.All[1].Id;
            string spring = DungeonJournal.EventId(1, "spring");
            var pages = new[]
            {
                new LegacyPage(EnemyJournal.PageId(enemy), "敵", new Dictionary<string, int> { [enemy] = 2 }),
                new LegacyPage(DungeonJournal.PageId(1), "ダンジョン", new Dictionary<string, int> { [spring] = 2 }),
                new LegacyPage("memo-1", "メモ", null, "罠に注意", other),
            };
            var expedition = new Expedition(new RunStart { Seed = 4, Traces = LedgerOf(1, pages) });
            WalkTo(expedition, NodeKind.Trace);
            Assert.That(expedition.DisclosureOf(enemy), Is.EqualTo(0), "the life has not met it");
            Assert.That(expedition.Journal.EntryLevel(spring), Is.EqualTo(0));

            Assert.That(HeritagePicks.Confirm(expedition, pages.Select(p => "page:" + p.PageId).ToList()), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(expedition.DisclosureOf(enemy), Is.EqualTo(2));
                Assert.That(expedition.Journal.EnemyPages().Select(p => p.Id), Does.Contain(EnemyJournal.PageId(enemy)));
                Assert.That(expedition.Journal.EntryLevel(spring), Is.EqualTo(2));
                MemoNote note = expedition.Journal.Memo.Notes.Single();
                Assert.That(note.Text, Is.EqualTo("罠に注意"));
                Assert.That(note.EnemyId, Is.EqualTo(other));
                Assert.That(expedition.InheritedLegacyPages.Select(p => p.PageId), Is.EqualTo(pages.Select(p => p.PageId)));
            });
        }

        [Test]
        public void APageHoldingLess_NeverLowersWhatTheLifeKnows()
        {
            string enemy = EnemyJournal.All[0].Id;
            string spring = DungeonJournal.EventId(1, "spring");
            var pages = new[]
            {
                new LegacyPage(EnemyJournal.PageId(enemy), "敵", new Dictionary<string, int> { [enemy] = 1 }),
                new LegacyPage(DungeonJournal.PageId(1), "ダンジョン", new Dictionary<string, int> { [spring] = 1 }),
                new LegacyPage("p9", "a page this journal does not know"),
            };
            var expedition = new Expedition(new RunStart { Seed = 4, Traces = LedgerOf(1, pages) },
                journal: new Dictionary<string, int> { [enemy] = 2 });
            WalkTo(expedition, NodeKind.Trace);
            expedition.Journal.Read(spring);

            Assert.That(HeritagePicks.Confirm(expedition, pages.Select(p => "page:" + p.PageId).ToList()), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(expedition.DisclosureOf(enemy), Is.EqualTo(2));
                Assert.That(expedition.Journal.EntryLevel(spring), Is.EqualTo(2));
                Assert.That(expedition.Journal.Memo.Notes, Is.Empty, "a page with no words is no note");
            });
        }

        [Test]
        public void ADeadLifesJournal_ComesBackThroughTheTraceItLeft()
        {
            var first = new Expedition(new RunStart { Seed = 4 });
            WalkTo(first, NodeKind.Battle, winLast: false);
            string enemy = EnemyJournal.All.First(e => first.DisclosureOf(e.Id) >= 1).Id;
            var levels = EnemyJournal.All.ToDictionary(e => e.Id, e => first.DisclosureOf(e.Id));
            first.Journal.Meet(DungeonJournal.EventId(1, "spring"));
            first.Journal.Memo.Write("罠に注意", enemy);
            Assert.That(first.Session.Act(ScreenAction.FallInBattle), Is.True);
            DeathTrace trace = first.Session.Traces.Traces.Single();

            var second = new Expedition(new RunStart { Seed = 5, Traces = TraceLedger.Empty.Add(trace) });
            WalkTo(second, NodeKind.Trace);
            var keys = trace.Candidates.Where(c => c.Kind == LegacyKind.Page).Select(c => c.Key).ToList();
            Assert.That(keys.Count, Is.GreaterThanOrEqualTo(3), "the enemy page, the dungeon page and the note");
            keys = keys.Skip(keys.Count - 3).ToList();
            Assert.That(HeritagePicks.Confirm(second, keys), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(second.Journal.EntryLevel(DungeonJournal.EventId(1, "spring")), Is.EqualTo(1));
                Assert.That(second.Journal.Memo.Notes.Select(n => (n.Text, n.EnemyId)), Is.EqualTo(new[] { ("罠に注意", enemy) }));
                foreach (string key in keys.Where(k => k.StartsWith("page:enemy-", StringComparison.Ordinal)))
                {
                    string id = key.Substring("page:enemy-".Length);
                    Assert.That(second.DisclosureOf(id), Is.EqualTo(levels[id]), id);
                }
            });
        }

        [Test]
        public void ADeathAsANewLifeEnters_LeavesNothingOfTheLifeBefore()
        {
            var expedition = new Expedition(new RunStart { Seed = 4, Traces = Ledger(1) });
            WalkTo(expedition, NodeKind.Trace);
            Assert.That(HeritagePicks.Confirm(expedition, new[] { "card:" + CardCatalog.Thrust.Id }), Is.True);
            WalkTo(expedition, NodeKind.Battle, winLast: false);
            Assert.That(expedition.Session.Act(ScreenAction.FallInBattle), Is.True);
            Assert.That(expedition.Session.Traces.Traces.Single().Candidates, Is.Not.Empty);

            // The new life falls on its entry battle before anything asks the expedition about it.
            Assert.That(expedition.Session.Act(ScreenAction.NewLife), Is.True);
            Assert.That(expedition.Session.Act(ScreenAction.FallInBattle), Is.True);

            DeathTrace second = expedition.Session.Traces.Traces.Single(t => t.Life == 2);
            Assert.That(second.Candidates, Is.Empty, "the book and the journal are the new life's, which has grown nothing");
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

        /// <summary>A ledger with one trace on a layer that offers these pages and no card.</summary>
        private static TraceLedger LedgerOf(int layer, params LegacyPage[] pages) =>
            TraceLedger.Empty.Add(new DeathTrace(1, 1, Heritage.StandInName(1), new DeathPlace(layer, NodeKind.Battle),
                DeathCause.Fallen, Heritage.Candidates(layer, Array.Empty<LegacyCard>(), pages)));

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

        /// <summary>
        /// Walks the shortest way to the nearest unresolved node of this kind, winning every battle on the
        /// way. With <paramref name="winLast"/> false, the battle on the node reached is left waiting.
        /// </summary>
        private static void WalkTo(Expedition expedition, NodeKind kind, bool winLast = true)
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
                if (session.HasPendingBattle && (winLast || node != target)) WinPending(expedition);
            }
        }
    }
}
