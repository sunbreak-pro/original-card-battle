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
            string preview = interlude.Card.Lines.Last();
            Assert.That(interlude.DaysForecastLabel, Is.EqualTo("降りると 1 日進む"));

            int days = session.State.DaysLeft;
            Assert.That(session.Act(ScreenAction.Descend), Is.True);
            var below = session.Screen();
            // #1049: the layer's size is the map's, so the preview names the map the descent drew
            var map = session.State.Map;
            Assert.That(preview, Is.EqualTo($"次は第 2 層　■■の秘跡: ノード {map.NodeCount}、真っ直ぐ抜けると {map.ShortestPathCost} 日"));
            Assert.That(below.LayerTitle, Is.EqualTo("第 2 層　■■の秘跡"));
            Assert.That(session.State.DaysLeft, Is.EqualTo(interlude.NextDaysLeft), "the interlude's forecast is the descent");
            Assert.That(session.State.DaysLeft, Is.EqualTo(days - 1), "the entry node takes its day at once");
            Assert.That(below.Card.Kind, Is.EqualTo(CardKind.Battle), "every layer opens on its entry battle");
        }

        [Test]
        public void TheLayerTwoInterludeOffersTheThreeRegions_AndDescendWaitsForTheChoice()
        {
            var session = new ExplorationSession();
            WalkToLayer(session, Lair.SacramentLayer);
            WalkToBoss(session);
            Assert.That(session.Act(ScreenAction.GoToInterlude), Is.True);

            Assert.That(session.NeedsRegion, Is.True);
            Assert.That(session.Region, Is.Null);
            var card = session.Screen().Card;
            Assert.That(card.Kind, Is.EqualTo(CardKind.Interlude));
            Assert.That(card.Buttons.Select(b => b.Action),
                Is.EqualTo(new[] { ScreenAction.ChooseRegion, ScreenAction.ChooseRegion, ScreenAction.ChooseRegion, ScreenAction.AskEndLife }));
            Assert.That(card.Buttons.Take(3).Select(b => b.Label), Is.EqualTo(new[] { "竜の領域", "寄生の領域", "亡霊の領域" }));
            Assert.That(card.Buttons.Take(3).Select(b => b.Arg),
                Is.EqualTo(Lair.Regions.Select(r => ((int)r).ToString(System.Globalization.CultureInfo.InvariantCulture))));
            Assert.That(card.Buttons[0].Note, Does.StartWith("第 3 層　大黒蛇の住処（竜の領域）: ノード "));
            string parasiteNote = card.Buttons[1].Note;

            Assert.That(session.Act(ScreenAction.Descend), Is.False, "the way down is one of the three");
            Assert.That(session.TryDescend(), Is.Null);
            Assert.That(session.Act(ScreenAction.ChooseRegion, (int)Region.Common), Is.False, "layer 3 belongs to a region");
            Assert.That(session.Act(ScreenAction.ChooseRegion, "nowhere"), Is.False);

            Assert.That(session.Act(ScreenAction.ChooseRegion, (int)Region.Parasite), Is.True);
            Assert.That(session.Region, Is.EqualTo(Region.Parasite));
            Assert.That(session.NeedsRegion, Is.False);
            Assert.That(session.Act(ScreenAction.ChooseRegion, (int)Region.Ghost), Is.False, "the region holds for the life");
            var chosen = session.Screen().Card;
            Assert.That(chosen.Buttons.Select(b => b.Action), Is.EqualTo(new[] { ScreenAction.Descend, ScreenAction.AskEndLife }));
            Assert.That(chosen.Buttons[0].Label, Is.EqualTo("寄生の領域へ降りる"));

            Assert.That(session.Act(ScreenAction.Descend), Is.True);
            Assert.That(session.State.Profile.Layer, Is.EqualTo(3));
            Assert.That(session.State.Profile.Region, Is.EqualTo(Region.Parasite));
            // #1049: the note named the map the descent drew
            var map = session.State.Map;
            Assert.That(parasiteNote, Is.EqualTo($"{session.Screen().LayerTitle}: ノード {map.NodeCount}、真っ直ぐ抜けると {map.ShortestPathCost} 日"));
        }

        [Test]
        public void TheRouteFollowsTheChosenRegion_DownToItsKingRoom()
        {
            var session = new ExplorationSession();
            WalkToLayer(session, Lair.KingLayer, Region.Ghost);
            Assert.That(session.Region, Is.EqualTo(Region.Ghost));
            Assert.That(session.State.Profile.Name, Is.EqualTo(Lair.Route(Region.Ghost).Last().Name));
            Assert.That(session.State.Profile.Region, Is.EqualTo(Region.Ghost));
            Assert.That(session.TryDescend(), Is.Null, "nothing below the king room yet");
        }

        [Test]
        public void ARegionHandedInWithTheStart_SkipsTheChoice()
        {
            var session = new ExplorationSession(new RunStart { Region = Region.Ghost });
            Assert.That(session.Region, Is.EqualTo(Region.Ghost));
            WalkToLayer(session, Lair.SacramentLayer);
            WalkToBoss(session);
            session.Act(ScreenAction.GoToInterlude);

            Assert.That(session.NeedsRegion, Is.False);
            Assert.That(session.Act(ScreenAction.Descend), Is.True);
            Assert.That(session.State.Profile.Name, Is.EqualTo(Lair.Of(3, Region.Ghost).Name));
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
        public void ADescentThatRunsTheCalendarOutIsWarnedAndAsksFirst()
        {
            // One day more than layer one's straight route: the route leaves one; layer two's entry takes it.
            var session = new ExplorationSession(new RunStart { Calendar = new CalendarState(LayerOneRows() + 1) });
            WalkToBoss(session);
            Assert.That(session.State.DaysLeft, Is.EqualTo(1));
            session.Act(ScreenAction.GoToInterlude);

            var interlude = session.Screen();
            Assert.That(interlude.Card.Lines, Has.Member("！ 降りると入口で暦が尽き、世界が終わります"));
            Assert.That(interlude.NextDaysLeft, Is.EqualTo(0));

            Assert.That(session.Act(ScreenAction.Descend), Is.True);
            Assert.That(session.Screen().Card.Kind, Is.EqualTo(CardKind.ConfirmDescend));
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Interlude), "nothing is taken before the confirmation");

            Assert.That(session.Act(ScreenAction.Cancel), Is.True);
            Assert.That(session.Screen().Card.Kind, Is.EqualTo(CardKind.Interlude));

            session.Act(ScreenAction.Descend);
            Assert.That(session.Act(ScreenAction.ConfirmDescend), Is.True);
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Expired));
            Assert.That(session.State.Profile.Layer, Is.EqualTo(2));
            Assert.That(session.Screen().Card.Kind, Is.EqualTo(CardKind.Expired), "the world ends before the entry battle");
        }

        [Test]
        public void WithTheEntryDayFree_TheDescentTakesNoDay_AndAsksNothing()
        {
            // 日読みの砂 (tools_and_consumables_v4.md §2.4): a layer's entry node takes no day, layer one's included.
            var sand = new RunLoadout(new[] { ReliefItem.EntryDay("hiyomi_no_suna") }, null, RunLoadout.ToolSlots, true);
            int rows = LayerOneRows();
            var session = new ExplorationSession(new RunStart { Calendar = new CalendarState(rows), Loadout = sand });
            Assert.That(session.State.DaysLeft, Is.EqualTo(rows), "layer one's entry was free");
            WalkToBoss(session);
            Assert.That(session.State.DaysLeft, Is.EqualTo(1));
            session.Act(ScreenAction.GoToInterlude);

            var interlude = session.Screen();
            Assert.That(interlude.Card.Lines, Has.None.StartsWith("！"));
            Assert.That(interlude.Card.Lines, Has.Member("降りても入口では日が進まない"));
            Assert.That(interlude.NextDaysLeft, Is.EqualTo(1));

            Assert.That(session.Act(ScreenAction.Descend), Is.True);
            Assert.That(session.ConfirmingDescend, Is.False);
            Assert.That(session.State.Profile.Layer, Is.EqualTo(2));
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Exploring));
            Assert.That(session.State.DaysLeft, Is.EqualTo(1));
        }

        [Test]
        public void ConsumablesStayUsableAtTheInterlude()
        {
            var session = new ExplorationSession(new RunStart { Loadout = RunStart.TrialLoadout() });
            WalkToBoss(session);
            session.Act(ScreenAction.GoToInterlude);

            var interlude = session.Screen();
            Assert.That(interlude.Consumables[0].CanUse, Is.True, "items stay usable while the interlude card is up");
            Assert.That(interlude.CanEndLife, Is.True);
            Assert.That(session.UseConsumable(0), Is.True);
            Assert.That(session.Screen().Card.Kind, Is.EqualTo(CardKind.Interlude));
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
            Assert.That(session.State.DaysLeft, Is.EqualTo(before.DaysLeft));
        }

        [Test]
        public void TheEntryBattle_DrawsWhoIsFought_FromTheLayersTable()
        {
            var session = new ExplorationSession(new RunStart { Seed = 7 });
            var kind = session.State.Map.Node(session.PendingBattleNode).Kind;

            Assert.That(session.PendingEncounter, Is.Not.Null);
            Assert.That(LayerEncounters.Of(1).PoolFor(kind), Does.Contain(session.PendingEncounter));
            Assert.That(new ExplorationSession(new RunStart { Seed = 7 }).PendingEncounter, Is.EqualTo(session.PendingEncounter),
                "the same life meets the same enemy on the same node");
        }

        [Test]
        public void TheBattlesResult_ComesBackAsHpAndStamina()
        {
            var session = new ExplorationSession();
            Assert.That(session.ReportBattle(31, 4), Is.True);

            Assert.That(session.State.Hp, Is.EqualTo(31));
            Assert.That(session.State.Stamina, Is.EqualTo(4));
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Exploring));
            Assert.That(session.HasPendingBattle, Is.False);
            Assert.That(session.PendingEncounter, Is.Null);
            Assert.That(session.ReportBattle(31, 4), Is.False, "no battle is waiting");
        }

        [Test]
        public void HpZero_FromTheBattle_EndsTheLife()
        {
            var session = new ExplorationSession();
            Assert.That(session.ReportBattle(0, 6), Is.True);
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Fallen));
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
            // The fall left a trace on layer 1 (#106), so the new life's layer 1 is drawn with it.
            Assert.That(session.State.Map.Fingerprint(),
                Is.EqualTo(MapGenerator.Generate(Heritage.WithTrace(Lair.Of(1).MapSpec), ExplorationSession.LayerSeed(6UL, 1)).Fingerprint()));
            // A death hands over in 10 days (seven_layers_v4.md §2.1); the new life's entry takes one more.
            Assert.That(session.Calendar.DaysLeft, Is.EqualTo(Calendar.TotalDays - 1 - Calendar.HandoverFallenDays - 1));
        }

        [Test]
        public void ANewLifeAfterSurviving_HandsOverInThreeDays_OrOneWithTheThread()
        {
            var plain = Explorable(new ExplorationSession());
            plain.Act(ScreenAction.AskEndLife);
            Assert.That(plain.Screen().Card.Lines, Has.Member("次の代の出立までに暦が 3 日進む"));
            plain.Act(ScreenAction.ConfirmEndLife);
            Assert.That(plain.Act(ScreenAction.NewLife), Is.True);
            Assert.That(plain.Calendar.DaysLeft, Is.EqualTo(Calendar.TotalDays - 1 - Calendar.HandoverSurvivedDays - 1));

            // 帰り道の糸 (tools_and_consumables_v4.md §2.4): 3 days become 1.
            var thread = Explorable(new ExplorationSession(new RunStart { Loadout = RunStart.TrialLoadout() }));
            thread.Act(ScreenAction.AskEndLife);
            Assert.That(thread.Screen().Card.Lines, Has.Member("次の代の出立までに暦が 1 日進む"));
            thread.Act(ScreenAction.ConfirmEndLife);
            Assert.That(thread.Act(ScreenAction.NewLife), Is.True);
            Assert.That(thread.Calendar.DaysLeft, Is.EqualTo(Calendar.TotalDays - 1 - Calendar.HandoverShortDays - 1));
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
            WalkToLayer(session, Lair.SacramentLayer);
            var map = session.State.Map;
            int carving = map.Nodes.Single(n => n.Kind == NodeKind.Carving).Id;

            Assert.That(session.Screen().LayerTitle, Is.EqualTo("第 2 層　■■の秘跡"));
            foreach (int id in RouteTo(map, session.State.CurrentNodeId, carving)) Step(session, id);

            Assert.That(session.CarvingTaken, Is.True);
            Assert.That(session.Screen().LayerTitle, Is.EqualTo("第 2 層　龍神の秘跡"));
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

            var combat = session.Screen().Nodes.First(n => n.Standing != NodeStanding.Far && n.Kind.IsCombat());
            Assert.That(combat.Lines, Has.Member("敵: 歪み兵（開示度 1 / 2）"));

            foreach (int id in RouteTo(map, session.State.CurrentNodeId, survey)) Step(session, id);
            Assert.That(session.Surveyed, Is.True);

            var screen = session.Screen();
            var anyCombat = screen.Nodes.First(n => n.Standing != NodeStanding.Far && n.Kind.IsCombat());
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

        /// <summary>The rows of the layer-one map a default session draws: the days of its straight route (#1049: 7〜9).</summary>
        private static int LayerOneRows() => new ExplorationSession().State.Map.ShortestPathCost;

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
