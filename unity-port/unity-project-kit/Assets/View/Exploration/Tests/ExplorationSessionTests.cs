// The exploration screen's flow (dungeon_exploration_v4.md §6.5〜§6.9): layer ends, the interlude,
// 「この生を終える」, the stand-in battle, a life's end, and the sockets for the carving and the survey.
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;
using Exploration;
using Journal;
using NUnit.Framework;
using static Exploration.Tests.ExplorationScreenTests;

namespace Exploration.Tests
{
    public class ExplorationSessionTests
    {
        [Test]
        public void TheBossLeadsThroughTheInterludeToTheNextLayer()
        {
            var session = new ExplorationSession();
            Explorable(session);
            WalkToBoss(session);

            var cleared = session.Screen().Card;
            Assert.That(cleared.Kind, Is.EqualTo(CardKind.LayerCleared));
            Assert.That(cleared.Buttons.Select(b => b.Action), Is.EqualTo(new[] { ScreenAction.GoToInterlude }));
            Assert.That(session.Screen().CanEndLife, Is.True, "the life may be closed at the layer's end too");

            Assert.That(session.Act(ScreenAction.GoToInterlude), Is.True);
            var interlude = session.Screen();
            Assert.That(interlude.Card.Kind, Is.EqualTo(CardKind.Interlude));
            Assert.That(interlude.Card.Buttons.Select(b => b.Action), Is.EqualTo(new[] { ScreenAction.Descend, ScreenAction.AskEndLife }));
            Assert.That(interlude.Card.Lines.Last(), Is.EqualTo("次は第 2 層　■■の秘跡: 1 歩ごとに瘴気 +1%、刻限 10"));
            Assert.That(interlude.Miasma.ForecastLabel, Is.EqualTo("降りると +1%"));

            int miasma = session.State.MiasmaPercent;
            Assert.That(session.Act(ScreenAction.Descend), Is.True);
            var below = session.Screen();
            Assert.That(below.LayerTitle, Is.EqualTo("第 2 層　■■の秘跡"));
            Assert.That(session.State.MiasmaPercent, Is.EqualTo(interlude.Miasma.NextPercent), "the interlude's forecast is the descent");
            Assert.That(session.State.MiasmaPercent, Is.EqualTo(miasma + 1));
            Assert.That(below.Card.Kind, Is.EqualTo(CardKind.Battle), "every layer opens on its entry battle");
        }

        [Test]
        public void BeingPushedOutOfTheLastLayerLeavesOnlyTheSurvivorRoute()
        {
            var session = new ExplorationSession();
            WalkToLayer(session, 7);
            int entry = session.State.CurrentNodeId;
            var middle = session.State.Map.Row(1).Select(n => n.Id).ToList();
            foreach (int id in middle)
            {
                Step(session, id);
                if (session.State.Phase != RunPhase.Exploring) break;
                Assert.That(session.Step(entry), Is.True);
            }

            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.PushedOut));
            Assert.That(session.Screen().Card.Kind, Is.EqualTo(CardKind.PushedOut));
            Assert.That(session.Act(ScreenAction.GoToInterlude), Is.True);

            var card = session.Screen().Card;
            Assert.That(card.Kind, Is.EqualTo(CardKind.Interlude));
            Assert.That(card.Buttons.Select(b => b.Action), Is.EqualTo(new[] { ScreenAction.AskEndLife }));
            Assert.That(card.Lines, Has.Member("これより下の層はありません。"));
            Assert.That(session.Act(ScreenAction.Descend), Is.False);
        }

        [Test]
        public void EndingTheLifeAsksFirst()
        {
            var session = Explorable(new ExplorationSession());
            var before = session.State;

            Assert.That(session.Act(ScreenAction.AskEndLife), Is.True);
            var ask = session.Screen();
            Assert.That(ask.Card.Kind, Is.EqualTo(CardKind.ConfirmEndLife));
            Assert.That(ask.MapIsLive, Is.False);
            Assert.That(ask.CanEndLife, Is.False);

            Assert.That(session.Act(ScreenAction.Cancel), Is.True);
            Assert.That(session.Screen().Card.IsShown, Is.False);
            Assert.That(session.State, Is.SameAs(before), "cancelling changes nothing");

            session.Act(ScreenAction.AskEndLife);
            Assert.That(session.Act(ScreenAction.ConfirmEndLife), Is.True);
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Survived));

            var end = session.Screen();
            Assert.That(end.Card.Kind, Is.EqualTo(CardKind.Survived));
            Assert.That(end.Card.Lines.First(), Is.EqualTo("生きたまま、第 1 層でこの生を閉じた。"));
            Assert.That(end.CanEndLife, Is.False);
            Assert.That(end.ShowsEndLife, Is.False, "the button goes once the life has ended");
            Assert.That(end.Consumables.All(s => !s.CanUse), Is.True);
        }

        [Test]
        public void TheLifeCanBeClosedFromTheInterlude()
        {
            var session = new ExplorationSession();
            WalkToBoss(session);
            session.Act(ScreenAction.GoToInterlude);

            Assert.That(session.Act(ScreenAction.AskEndLife), Is.True);
            Assert.That(session.Screen().Card.Kind, Is.EqualTo(CardKind.ConfirmEndLife));
            Assert.That(session.Act(ScreenAction.Cancel), Is.True);
            Assert.That(session.Screen().Card.Kind, Is.EqualTo(CardKind.Interlude), "cancelling returns to the interlude");

            session.Act(ScreenAction.AskEndLife);
            session.Act(ScreenAction.ConfirmEndLife);
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Survived));
        }

        [Test]
        public void TheInterludeTellsTheHpItReallyGivesBack()
        {
            var session = new ExplorationSession(new RunStart { Hp = 20 });
            WalkToBoss(session);
            int hp = session.State.Hp;
            int expected = Math.Min(session.State.MaxHp - hp, ExplorationReducer.InterludeHeal(session.State.MaxHp));

            Assert.That(session.Screen().Card.Lines, Has.Some.StartsWith($"階層間の休憩: HP +{expected}（最大の 30%）"));
            session.Act(ScreenAction.GoToInterlude);

            Assert.That(session.InterludeHealed, Is.EqualTo(expected));
            Assert.That(session.Screen().Card.Lines.First(), Is.EqualTo($"HP +{expected}（最大の 30%）で HP {hp + expected} / 50"));
        }

        [Test]
        public void ADescentThatEndsTheLifeIsWarnedAndAsksFirst()
        {
            // 94% + the five nodes of layer one's straight route = 99%; layer two's entry tips it to 100%.
            var session = new ExplorationSession(new RunStart { MiasmaPercent = 94 });
            WalkToBoss(session);
            Assert.That(session.State.MiasmaPercent, Is.EqualTo(99));
            session.Act(ScreenAction.GoToInterlude);

            var interlude = session.Screen();
            Assert.That(interlude.Card.Lines, Has.Member("！ 降りると入口で瘴気が 100% に達し、この生が終わります"));
            Assert.That(interlude.Miasma.NextPercent, Is.EqualTo(100));

            Assert.That(session.Act(ScreenAction.Descend), Is.True);
            Assert.That(session.Screen().Card.Kind, Is.EqualTo(CardKind.ConfirmDescend));
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Interlude), "nothing is taken before the confirmation");

            Assert.That(session.Act(ScreenAction.Cancel), Is.True);
            Assert.That(session.Screen().Card.Kind, Is.EqualTo(CardKind.Interlude));

            session.Act(ScreenAction.Descend);
            Assert.That(session.Act(ScreenAction.ConfirmDescend), Is.True);
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.MiasmaDeath));
            Assert.That(session.State.Profile.Layer, Is.EqualTo(2));
            Assert.That(session.Screen().Card.Kind, Is.EqualTo(CardKind.MiasmaDeath), "瘴気死 comes before the entry battle");
        }

        [Test]
        public void AConsumableAtTheInterludeCanSaveTheDescent()
        {
            var session = new ExplorationSession(new RunStart { MiasmaPercent = 94, Loadout = RunStart.TrialLoadout() });
            WalkToBoss(session);
            session.Act(ScreenAction.GoToInterlude);

            var interlude = session.Screen();
            Assert.That(interlude.Consumables[0].CanUse, Is.True, "items stay usable while the interlude card is up");
            Assert.That(interlude.CanEndLife, Is.True);

            Assert.That(session.UseConsumable(0), Is.True);
            Assert.That(session.State.MiasmaPercent, Is.EqualTo(89));
            Assert.That(session.Screen().Card.Lines, Has.None.StartsWith("！"));

            Assert.That(session.Act(ScreenAction.Descend), Is.True);
            Assert.That(session.State.Profile.Layer, Is.EqualTo(2));
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Exploring));
        }

        [Test]
        public void FallingInTheStandInBattleEndsTheLife()
        {
            var session = new ExplorationSession();
            Assert.That(session.Act(ScreenAction.FallInBattle), Is.True);

            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Fallen));
            Assert.That(session.State.Hp, Is.Zero);
            var card = session.Screen().Card;
            Assert.That(card.Kind, Is.EqualTo(CardKind.Fallen));
            Assert.That(card.Lines.First(), Is.EqualTo("第 1 層の戦いで倒れた。"));
        }

        [Test]
        public void WinningTheStandInBattleChangesNothingButTheCard()
        {
            var session = new ExplorationSession();
            var before = session.State;
            session.Act(ScreenAction.WinBattle);

            Assert.That(session.State.Hp, Is.EqualTo(before.Hp));
            Assert.That(session.State.Stamina, Is.EqualTo(before.Stamina));
            Assert.That(session.State.TimeLeft, Is.EqualTo(before.TimeLeft));
            Assert.That(session.State.MiasmaPercent, Is.EqualTo(before.MiasmaPercent));
        }

        [Test]
        public void ANewLifeStartsOverFromLayerOneWithTheNextSeed()
        {
            var session = new ExplorationSession(new RunStart { Seed = 5 });
            session.Act(ScreenAction.FallInBattle);

            Assert.That(session.Act(ScreenAction.NewLife), Is.True);
            Assert.That(session.Life, Is.EqualTo(2));
            Assert.That(session.RunSeed, Is.EqualTo(6UL));
            Assert.That(session.State.Profile.Layer, Is.EqualTo(1));
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Exploring));
            Assert.That(session.State.Hp, Is.EqualTo(RunStart.DefaultMaxHp));
            Assert.That(session.State.Map.Fingerprint(),
                Is.EqualTo(SevenLayers.Of(1).Map(ExplorationSession.LayerSeed(6UL, 1)).Fingerprint()));
        }

        [Test]
        public void ActionsThatDoNotApplyNowAreRefused()
        {
            var session = new ExplorationSession();

            // Under the entry battle's card only the battle's own buttons work.
            Assert.That(session.Act(ScreenAction.AskEndLife), Is.False);
            Assert.That(session.Act(ScreenAction.GoToInterlude), Is.False);
            Assert.That(session.Act(ScreenAction.NewLife), Is.False);
            Assert.That(session.Act(ScreenAction.ConfirmStep), Is.False);
            Assert.That(session.UseConsumable(0), Is.False);

            session.Act(ScreenAction.WinBattle);
            Assert.That(session.Act(ScreenAction.WinBattle), Is.False, "no battle is waiting");
            Assert.That(session.Act(ScreenAction.Descend), Is.False, "descending needs the interlude");
            Assert.That(session.Act(ScreenAction.Cancel), Is.False, "nothing to cancel");
            Assert.That(session.UseConsumable(0), Is.False, "the empty loadout has no slot 0");

            var map = session.State.Map;
            int far = map.Nodes.Select(n => n.Id).First(id => !map.Neighbours(session.State.CurrentNodeId).Contains(id) && id != session.State.CurrentNodeId);
            Assert.That(session.Step(far), Is.False, "only a neighbour can be walked to");
        }

        [Test]
        public void TheCarvingRevealsLayerTwosName()
        {
            var session = new ExplorationSession();
            WalkToLayer(session, SevenLayers.SacramentLayer);
            var map = session.State.Map;
            int carving = map.Nodes.Single(n => n.Kind == NodeKind.Carving).Id;

            Assert.That(session.Screen().LayerTitle, Is.EqualTo("第 2 層　■■の秘跡"));
            foreach (int id in RouteTo(map, session.State.CurrentNodeId, carving)) Step(session, id);

            Assert.That(session.CarvingTaken, Is.True);
            Assert.That(session.Screen().LayerTitle, Is.EqualTo("第 2 層　竜神の秘跡"));
            Assert.That(session.Screen().Node(carving).Lines, Has.Member("読んだ。この層の名前が分かった"));
        }

        [Test]
        public void TheSurveyIsHandedToTheEnemySocket()
        {
            var intel = new RecordingIntel();
            var session = new ExplorationSession(null, intel);
            Explorable(session);
            var map = session.State.Map;
            int survey = map.Nodes.Single(n => n.Kind == NodeKind.Survey).Id;

            var combat = session.Screen().Nodes.First(n => n.ShowsContents && ExplorationText.IsCombat(n.Kind));
            Assert.That(combat.Lines, Has.Member("敵: 歪み兵（開示度 1 / 2）"));

            foreach (int id in RouteTo(map, session.State.CurrentNodeId, survey)) Step(session, id);
            Assert.That(session.Surveyed, Is.True);

            var screen = session.Screen();
            var anyCombat = screen.Nodes.First(n => n.ShowsContents && ExplorationText.IsCombat(n.Kind));
            Assert.That(anyCombat.Lines, Has.Member("敵: 歪み兵（開示度 2 / 2）"));
            Assert.That(anyCombat.Lines, Has.Member("傾向: 間合い 1〜2 で突く"));
            Assert.That(screen.Journal.Pages.First().Title, Is.EqualTo("歪み兵"));
            Assert.That(screen.Journal.Pages.Single(p => p.Tab == JournalTab.Dungeon).Rows.Last().Text,
                Is.EqualTo("済ませた。この層の敵 1 種が開示度 2 になった"));
        }

        [Test]
        public void WithoutEnemyDataTheEnemyIsAQuestionMark()
        {
            var session = new ExplorationSession();
            var entry = session.Screen().Node(session.State.CurrentNodeId);

            Assert.That(entry.Lines, Has.Member("敵: ？"));
            var enemyPage = session.Screen().Journal.Pages.First();
            Assert.That(enemyPage.Tab, Is.EqualTo(JournalTab.Enemy));
            Assert.That(enemyPage.Disclosure, Is.Zero);
        }

        /// <summary>A breadth-first route over the map's links, excluding the start.</summary>
        private static IReadOnlyList<int> RouteTo(LayerMap map, int from, int to)
        {
            var previous = new Dictionary<int, int>();
            var queue = new Queue<int>();
            var seen = new HashSet<int> { from };
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == to) break;
                foreach (int next in map.Neighbours(current))
                {
                    if (map.Node(next).Kind == NodeKind.Boss && next != to) continue;
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

        /// <summary>An enemy source that knows one enemy and reports the survey it was told about.</summary>
        private sealed class RecordingIntel : IEnemyIntel
        {
            public NodeIntel ForNode(MapNode node, int layer, bool surveyed) =>
                new NodeIntel("歪み兵", surveyed ? 2 : 1, surveyed ? new[] { "傾向: 間合い 1〜2 で突く" } : Array.Empty<string>());

            public IReadOnlyList<JournalPage> Pages(int layer, bool surveyed) => new[]
            {
                new JournalPage("warped", JournalTab.Enemy, "歪み兵",
                    new[] { new JournalRow("傾向", "間合い 1〜2 で突く", needsDisclosure: 2) },
                    surveyed ? 2 : 1),
            };
        }
    }
}
