// #107: the expedition's journal. The 開示度 a battle leaves (1 for every enemy fought, 2 once observed)
// and the survey's 2 both reach the next battle's omen through the hand-in, and the exploration screen
// reads its enemy names and pages from the same journal. Runs under `dotnet test`
// (Exploration.Bridge.Tests.csproj) and in Unity's EditMode runner.
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
    public class LifeJournalTests
    {
        [Test]
        public void EveryEnemyOfTheBattleCore_HasAPage_UnderItsOwnName()
        {
            foreach (EnemyDef enemy in Enemies.All)
            {
                EnemyJournalEntry entry = EnemyJournal.Find(enemy.Id);
                Assert.That(entry, Is.Not.Null, enemy.Id);
                Assert.That(entry.Name, Is.EqualTo(enemy.Name), enemy.Id);
            }
        }

        [Test]
        public void TheExpedition_IsTheScreensJournal_WhenNoOtherIsGiven()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            Assert.That(expedition.Session.Intel, Is.SameAs(expedition.Journal));

            var stub = new NoEnemyIntel();
            Assert.That(new Expedition(new RunStart { Seed = 3 }, intel: stub).Session.Intel, Is.SameAs(stub));
        }

        [Test]
        public void AFoughtEnemy_GetsItsPage_AndAnObservedOneItsWeakness()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            EncounterBattle battle = expedition.StartEncounter();
            string enemy = battle.HandIn.Encounter.Fights[0].EnemyIds[0];
            Assert.That(expedition.Journal.EnemyPages(), Is.Empty);

            EndEveryFight(battle, GameResult.Won, observe: false);
            expedition.Return();
            JournalPage met = expedition.Session.Screen().Journal.Pages.First();
            Assert.That(met.Id, Is.EqualTo(EnemyJournal.PageId(enemy)));
            Assert.That(met.Disclosure, Is.EqualTo(1));
            Assert.That(met.TextOf(met.Rows.Last()), Is.EqualTo(JournalRow.Hidden), "the weakness waits for observation");

            var observed = new Expedition(new RunStart { Seed = 3 });
            EndEveryFight(observed.StartEncounter(), GameResult.Won, observe: true);
            observed.Return();
            JournalPage read = observed.Journal.EnemyPages().Single();
            Assert.That(read.Disclosure, Is.EqualTo(2));
            Assert.That(read.TextOf(read.Rows.Last()), Is.EqualTo(EnemyJournal.Of(enemy).Weakness));
            Assert.That(observed.Journal.LegacyLines().Single().Text, Does.EndWith("開示度 2 / 2（弱点まで）"));
        }

        [Test]
        public void TheSurvey_ReachesTheBossesOmen_ThroughTheHandIn()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            string boss = LifeJournal.SurveyTarget(1);

            WalkTo(expedition, NodeKind.Survey);
            Assert.That(expedition.DisclosureOf(boss), Is.EqualTo(2));

            WalkTo(expedition, NodeKind.Boss);
            EncounterBattle battle = expedition.StartEncounter();
            Assert.That(battle.HandIn.Encounter.EnemyIds.Single(), Is.EqualTo(boss));
            Assert.That(battle.HandIn.Disclosure[boss], Is.EqualTo(2));
            BattleLaunch launch = battle.NextLaunch();
            Assert.That(launch.Disclosure, Is.EqualTo(2));
            Assert.That(launch.CreateSource().Frame.Omen.Disclosure, Is.EqualTo(2));

            EndEveryFight(battle, GameResult.Won);
            BattleReturn back = expedition.Return();
            Assert.That(back.Disclosure[boss], Is.EqualTo(2), "a fight after the survey keeps the 2");
        }

        [Test]
        public void ANewLife_ForgetsTheSurvey()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            string boss = LifeJournal.SurveyTarget(1);
            WalkTo(expedition, NodeKind.Survey);
            Assert.That(expedition.Session.Act(ScreenAction.AskEndLife), Is.True);
            Assert.That(expedition.Session.Act(ScreenAction.ConfirmEndLife), Is.True);
            Assert.That(expedition.Session.Act(ScreenAction.NewLife), Is.True);

            Assert.That(expedition.DisclosureOf(boss), Is.Zero);
        }

        // ---- helpers ----

        /// <summary>Ends every fight of the encounter this way, keeping the HP and stamina each fight started with.</summary>
        private static void EndEveryFight(EncounterBattle battle, GameResult result, bool observe = false)
        {
            while (!battle.Over)
            {
                CoreBattleSource source = battle.StartNext();
                BattleState state = source.State;
                var events = new List<BattleEvent>(source.History);
                if (observe) events.Add(new CardPlayed(Actor.Player, new CardInstance("observe-0", CardCatalog.Observe), 3));
                battle.Finish(state with { Result = result }, events);
            }
        }

        /// <summary>Wins whatever battle waits.</summary>
        private static void WinPending(Expedition expedition)
        {
            EncounterBattle battle = expedition.StartEncounter();
            Assert.That(battle, Is.Not.Null);
            EndEveryFight(battle, GameResult.Won);
            expedition.Return();
        }

        /// <summary>
        /// Walks to the nearest unresolved node of this kind, winning every battle on the way but the
        /// one on that node, which is left waiting.
        /// </summary>
        private static void WalkTo(Expedition expedition, NodeKind kind)
        {
            ExplorationSession session = expedition.Session;
            if (session.HasPendingBattle) WinPending(expedition);
            IReadOnlyList<int> path = Route(session.State, kind);
            foreach (int node in path)
            {
                Assert.That(session.Step(node), Is.True, $"step to {node}");
                if (session.HasPendingStep) Assert.That(session.Act(ScreenAction.ConfirmStep), Is.True);
                if (session.HasPendingBattle && node != path[path.Count - 1]) WinPending(expedition);
            }
        }

        private static IReadOnlyList<int> Route(ExplorationState state, NodeKind kind)
        {
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
            return path;
        }
    }
}
