// The exploration screen's frame (dungeon_exploration_v4.md §6): what each node shows, what a step
// forecasts, and that the forecast is the step. Runs in Unity's EditMode runner and under
// `dotnet test` (unity-port/Exploration.Script.Tests) on the same files.
using System.Linq;
using DungeonCore;
using Exploration;
using Journal;
using NUnit.Framework;

namespace Exploration.Tests
{
    public class ExplorationScreenTests
    {
        [Test]
        public void TheFirstFrameIsLayerOneWithTheEntrySpent()
        {
            var session = new ExplorationSession();
            var screen = session.Screen();

            Assert.That(screen.LayerTitle, Is.EqualTo("第 1 層　燦光の樹海"));
            Assert.That(screen.DaysLeft, Is.EqualTo(Calendar.TotalDays - 1), "the entry node takes its day on arrival");
            Assert.That(screen.DaysLabel, Is.EqualTo($"残り {Calendar.TotalDays - 1} 日"));
            Assert.That(screen.CalendarFrozen, Is.False);
            Assert.That(screen.CalendarNote, Is.Empty);
            Assert.That(screen.HpLabel, Is.EqualTo("HP 50 / 50"));
            Assert.That(screen.Nodes.Count, Is.EqualTo(session.State.Map.NodeCount));
            Assert.That(screen.CurrentNodeId, Is.EqualTo(session.State.Map.EntryId));
        }

        [Test]
        public void TheScreenNamesTheBackdropSlotOfTheLayer()
        {
            // The view lays ExplorationBackdropSpec.For(screen.BackdropLayer) behind the map (#614). The regions share
            // depths, so the slot is the profile's, not the depth (seven_layers_v4.md §3.4).
            var session = new ExplorationSession();
            Assert.That(session.Screen().BackdropLayer, Is.EqualTo(Lair.Of(1).BackdropLayer));

            WalkToLayer(session, 3, Region.Ghost);
            var screen = session.Screen();

            Assert.That(screen.Layer, Is.EqualTo(3));
            Assert.That(screen.BackdropLayer, Is.EqualTo(Lair.Of(3, Region.Ghost).BackdropLayer));
        }

        [Test]
        public void ARegionLayerIsTitledWithItsRegion()
        {
            var session = new ExplorationSession(new RunStart { Region = Region.Dragon });
            WalkToLayer(session, 3);
            Assert.That(session.Screen().LayerTitle, Is.EqualTo("第 3 層　大黒蛇の住処（竜の領域）"));

            WalkToLayer(session, 4);
            Assert.That(session.Screen().LayerTitle, Is.EqualTo(Lair.Of(4, Region.Dragon).Name), "an unnamed layer reads 「竜の領域 第 4 層」");
            Assert.That(session.Screen().LayerTitle, Does.StartWith("竜の領域"));
        }

        [Test]
        public void TheEntryBattleCoversTheMapUntilItIsReported()
        {
            // The entry is always a battle (§4.1), so a life opens on the stand-in battle (§6.8).
            var session = new ExplorationSession();
            var first = session.Screen();

            Assert.That(first.Card.Kind, Is.EqualTo(CardKind.Battle));
            Assert.That(first.MapIsLive, Is.False);
            Assert.That(first.Nodes.All(n => n.Options.Count == 0), Is.True, "no step is offered under a card");
            Assert.That(session.Step(session.State.ReachableUnresolved().First()), Is.False);

            Assert.That(session.Act(ScreenAction.WinBattle), Is.True);
            var after = session.Screen();
            Assert.That(after.Card.IsShown, Is.False);
            Assert.That(after.MapIsLive, Is.True);
            Assert.That(after.Hint, Is.Not.Empty);
        }

        [Test]
        public void FarNodesShowTheirKindOnly()
        {
            var screen = Explorable(new ExplorationSession()).Screen();

            var far = screen.Nodes.Where(n => n.Standing == NodeStanding.Far).ToList();
            Assert.That(far, Is.Not.Empty);
            foreach (var node in far)
            {
                Assert.That(node.Lines, Is.Empty);
                Assert.That(node.Options, Is.Empty);
                Assert.That(node.Glyph, Is.Not.Empty);
                Assert.That(node.KindLabel, Is.Not.Empty);
            }
        }

        [Test]
        public void ReachableNodesShowTheirContentsAndAForecast()
        {
            var session = Explorable(new ExplorationSession());
            var screen = session.Screen();

            var reachable = screen.Nodes.Where(n => n.Standing == NodeStanding.Reachable).ToList();
            Assert.That(reachable.Select(n => n.Id), Is.EquivalentTo(session.State.ReachableUnresolved()));
            foreach (var node in reachable)
            {
                Assert.That(node.Lines, Is.Not.Empty);
                Assert.That(node.Options, Is.Not.Empty);
                Assert.That(node.Options.All(o => o.Forecast != null), Is.True);
            }
        }

        [Test]
        public void EveryForecastIsTheStepItForetells()
        {
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var probe = Explorable(new ExplorationSession(new RunStart { Seed = seed }));
                foreach (var node in probe.Screen().Nodes.Where(n => n.Standing == NodeStanding.Reachable))
                {
                    foreach (var option in node.Options)
                    {
                        var session = Explorable(new ExplorationSession(new RunStart { Seed = seed }));
                        Assert.That(session.Step(node.Id, option.Choice), Is.True);
                        var taken = session.State;

                        Assert.That(taken.DaysLeft, Is.EqualTo(option.Forecast.DaysAfter));
                        Assert.That(taken.MaxStamina, Is.EqualTo(option.Forecast.MaxStaminaAfter));
                        Assert.That(taken.Stamina, Is.EqualTo(option.Forecast.StaminaAfter));
                        Assert.That(taken.Hp, Is.EqualTo(option.Forecast.HpAfter));
                    }
                }
            }
        }

        [Test]
        public void AStepForetellsTheDayItTakes()
        {
            var session = Explorable(new ExplorationSession());
            int days = session.State.DaysLeft;
            var node = session.Screen().Nodes.First(n => n.Standing == NodeStanding.Reachable && n.Kind != NodeKind.Rest);
            var forecast = node.Options.Single().Forecast;

            Assert.That(forecast.DaysBefore, Is.EqualTo(days));
            Assert.That(forecast.DaysAfter, Is.EqualTo(days - 1));
            Assert.That(forecast.Lines.First(), Is.EqualTo($"暦 残り {days} → {days - 1} 日"));
            Assert.That(forecast.Warnings, Is.Empty);
            Assert.That(forecast.IsExpiry, Is.False);
        }

        [Test]
        public void ARestNodeOffersToRestOrToTrain()
        {
            var session = SeedWhere(s => s.Screen().Nodes.Any(n => n.Standing == NodeStanding.Reachable && n.Kind == NodeKind.Rest));
            var rest = session.Screen().Nodes.First(n => n.Standing == NodeStanding.Reachable && n.Kind == NodeKind.Rest);

            Assert.That(rest.Options.Select(o => o.Label), Is.EqualTo(new[] { "休む", "訓練する" }));
            Assert.That(rest.Options.Select(o => o.Choice), Is.EqualTo(new[] { RestChoice.Rest, RestChoice.Train }));
            Assert.That(rest.Lines.Any(l => l.StartsWith("休む: HP +8")), Is.True, "15% of 50, rounded half away from zero");

            // Resting lifts max stamina by 2; training leaves it.
            var restForecast = rest.Options[0].Forecast;
            var trainForecast = rest.Options[1].Forecast;
            Assert.That(restForecast.MaxStaminaAfter, Is.EqualTo(trainForecast.MaxStaminaAfter + ExplorationReducer.RestMaxStaminaBonus));
        }

        [Test]
        public void WalkingBackIsOfferedAndCostsNothing()
        {
            var session = Explorable(new ExplorationSession());
            int entry = session.State.CurrentNodeId;
            int forward = session.State.ReachableUnresolved().First();
            Step(session, forward);

            var screen = session.Screen();
            var back = screen.Node(entry);
            Assert.That(back.Standing, Is.EqualTo(NodeStanding.Revisit));
            Assert.That(back.Options.Single().Label, Is.EqualTo("移る"));
            Assert.That(back.Options.Single().Forecast, Is.Null);
            Assert.That(back.Note, Does.Contain("日数を使わずに"));

            int daysBefore = session.State.DaysLeft;
            Assert.That(session.Step(entry), Is.True);
            Assert.That(session.State.DaysLeft, Is.EqualTo(daysBefore));
        }

        [Test]
        public void OnlyTheLinksAroundThePlayerAreLit()
        {
            var session = Explorable(new ExplorationSession());
            var screen = session.Screen();
            int here = screen.CurrentNodeId;

            foreach (var edge in screen.Edges)
            {
                bool touches = edge.From == here || edge.To == here;
                if (!touches) Assert.That(edge.Lit, Is.False);
            }
            Assert.That(screen.Edges.Count(e => e.Lit), Is.EqualTo(session.State.Map.Neighbours(here).Count));
        }

        [Test]
        public void NodesAreLaidOutEntryOnTopBossAtTheBottom()
        {
            var screen = new ExplorationSession().Screen();
            var entry = screen.Node(screen.CurrentNodeId);
            var boss = screen.Nodes.Single(n => n.Kind == NodeKind.Boss);

            Assert.That(entry.Y, Is.EqualTo(1f));
            Assert.That(boss.Y, Is.EqualTo(0f));
            Assert.That(screen.Nodes.All(n => n.X > 0f && n.X < 1f), Is.True);
        }

        [Test]
        public void TheBandForetellsTheDayOfTheNextNewNode()
        {
            var screen = Explorable(new ExplorationSession()).Screen();

            Assert.That(screen.NextDaysLeft, Is.EqualTo(screen.DaysLeft - 1));
            Assert.That(screen.DaysForecastLabel, Is.EqualTo("次の 1 歩で 1 日進む"));
        }

        [Test]
        public void AFrozenCalendarSaysWhichKingIsDownAndForHowLong()
        {
            // seven_layers_v4.md §2.2: while a king is down no day passes; the days till its return do.
            var start = CalendarState.Fresh.KingDown(Region.Dragon);
            var session = Explorable(new ExplorationSession(new RunStart { Calendar = start }));
            var screen = session.Screen();

            Assert.That(screen.CalendarFrozen, Is.True);
            Assert.That(screen.DaysLeft, Is.EqualTo(Calendar.TotalDays), "the entry's day did not count");
            int back = session.State.Calendar.KingReturns[Region.Dragon];
            Assert.That(back, Is.EqualTo(Calendar.KingReturnDays - 1), "the entry's day brought the king a day nearer");
            Assert.That(screen.CalendarNote, Is.EqualTo($"竜王が戻るまで {back} 日。暦は止まっている"));
            Assert.That(screen.DaysForecastLabel, Is.Empty, "the note says it already");

            var node = screen.Nodes.First(n => n.Standing == NodeStanding.Reachable && n.Kind != NodeKind.Rest);
            Assert.That(node.Options.Single().Forecast.Lines.First(), Is.EqualTo("暦は止まっている"));
        }

        [Test]
        public void TheDetailPanelTextComesWholeFromTheScreen()
        {
            var screen = Explorable(new ExplorationSession()).Screen();

            var far = screen.Nodes.First(n => n.Standing == NodeStanding.Far);
            Assert.That(far.Detail, Is.EqualTo(new[] { ExplorationText.FarNode }));
            Assert.That(far.Title, Is.EqualTo($"{far.Glyph}　{far.KindLabel}"));

            var near = screen.Nodes.First(n => n.Standing == NodeStanding.Reachable && n.Kind != NodeKind.Rest);
            Assert.That(near.Detail.First(), Is.EqualTo("入ると暦が 1 日進む"));
            Assert.That(near.Detail, Has.Some.StartsWith("【進む】暦 残り "));
        }

        [Test]
        public void MaxStaminaIsSpeltOut()
        {
            var session = Explorable(new ExplorationSession());
            Assert.That(session.Screen().MaxStaminaLabel, Is.EqualTo("最大 10"), "nothing moves it");

            session.State = session.State.With(tempMaxStaminaMod: 2);
            Assert.That(session.Screen().MaxStaminaLabel, Is.EqualTo("最大 12 = 10 + 一時 2"));

            session.State = session.State.With(tempMaxStaminaMod: -3);
            Assert.That(session.Screen().MaxStaminaLabel, Is.EqualTo("最大 7 = 10 − 一時 3"));
        }

        [Test]
        public void AStepThatRunsTheCalendarOutWaitsForAConfirmation()
        {
            // Two days left: the entry takes one, so the next new node takes the last.
            var session = Explorable(new ExplorationSession(new RunStart { Calendar = new CalendarState(2) }));
            Assert.That(session.State.DaysLeft, Is.EqualTo(1));
            int target = session.State.ReachableUnresolved().First();
            var option = session.Screen().Node(target).Options.First();
            Assert.That(option.NeedsConfirm, Is.True);
            Assert.That(option.Forecast.IsExpiry, Is.True);
            Assert.That(option.Forecast.Warnings, Is.EqualTo(new[] { "ここで暦が尽き、龍神が目覚めます。世界が終わり、最初からやり直します" }));

            Assert.That(session.Step(target, option.Choice), Is.True);
            var ask = session.Screen().Card;
            Assert.That(ask.Kind, Is.EqualTo(CardKind.ConfirmStep));
            Assert.That(ask.Lines, Is.EqualTo(new[] { "ここで暦が尽き、龍神が目覚めます。世界が終わり、最初からやり直します。" }));
            Assert.That(session.State.CurrentNodeId, Is.Not.EqualTo(target), "nothing is taken before the confirmation");

            Assert.That(session.Act(ScreenAction.Cancel), Is.True);
            Assert.That(session.Screen().Card.IsShown, Is.False);
            Assert.That(session.State.DaysLeft, Is.EqualTo(1));

            session.Step(target, option.Choice);
            Assert.That(session.Act(ScreenAction.ConfirmStep), Is.True);
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Expired));
            Assert.That(session.State.IsOver, Is.True);
            Assert.That(session.HasPendingBattle, Is.False, "the world ends before any fight");

            var end = session.Screen().Card;
            Assert.That(end.Kind, Is.EqualTo(CardKind.Expired));
            Assert.That(end.Lines.First(), Is.EqualTo("暦が尽き、龍神が目覚めた。世界が終わり、最初からやり直す。"));
            // §6.9: the one button of every end card, and the one the Enter key presses (Buttons[0]); without it the
            // world's end is a card the player cannot leave.
            Assert.That(end.Buttons.Select(b => b.Action), Is.EqualTo(new[] { ScreenAction.NewLife }));
            Assert.That(end.Buttons[0].Label, Is.EqualTo("新しい生で潜り直す"));
        }

        [Test]
        public void ANewLifeAfterTheCalendarRanOut_StartsOverOnTheFirstDay_WithNoTraceOfTheWorldBefore()
        {
            var session = Explorable(new ExplorationSession(new RunStart { Seed = 5, Calendar = new CalendarState(2) }));
            int target = session.State.ReachableUnresolved().First();
            session.Step(target, session.Screen().Node(target).Options.First().Choice);
            session.Act(ScreenAction.ConfirmStep);
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Expired));
            Assert.That(session.Traces.Traces, Is.Not.Empty, "the expired life left its trace on this world");

            Assert.That(session.Act(ScreenAction.NewLife), Is.True);
            Assert.That(session.Life, Is.EqualTo(2));
            Assert.That(session.State.Profile.Layer, Is.EqualTo(1));
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Exploring));
            // A fresh calendar, less the new life's own entry day (not the 10 days a death hands over).
            Assert.That(session.Calendar.DaysLeft, Is.EqualTo(Calendar.TotalDays - 1));
            Assert.That(session.Calendar.IsFrozen, Is.False);
            Assert.That(session.Traces.Traces, Is.Empty, "the world before left nothing behind");
        }

        [Test]
        public void TheKingsNodeForetellsHowLongTheCalendarStops_AndItsFallLeavesOnlyTheSurvivorRoute()
        {
            var session = new ExplorationSession(new RunStart { Region = Region.Ghost });
            WalkToLayer(session, Lair.KingLayer);
            Assert.That(session.State.Profile.IsKingLayer, Is.True);
            Step(session, session.State.Map.Row(1).First().Id);

            var king = session.Screen().Nodes.Single(n => n.Kind == NodeKind.Boss);
            Assert.That(king.Standing, Is.EqualTo(NodeStanding.Reachable));
            Assert.That(king.Lines, Has.Member($"暦が {Calendar.KingReturnDays} 日延びる見込み"), "no other king is down");

            Step(session, king.Id);
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.LayerCleared));
            Assert.That(session.Calendar.IsFrozen, Is.True);

            var screen = session.Screen();
            Assert.That(screen.CalendarFrozen, Is.True);
            Assert.That(screen.Card.Kind, Is.EqualTo(CardKind.LayerCleared));
            Assert.That(screen.Card.Title, Is.EqualTo("死霊王を倒した"));
            Assert.That(screen.Card.Buttons.Select(b => b.Action), Is.EqualTo(new[] { ScreenAction.AskEndLife }), "layer 8 is shut");
            Assert.That(session.Act(ScreenAction.GoToInterlude), Is.False);
        }

        [Test]
        public void ConsumablesShowInTheirSlotsAndCostNoDay()
        {
            var session = Explorable(new ExplorationSession(new RunStart { Loadout = RunStart.TrialLoadout() }));
            var screen = session.Screen();

            Assert.That(screen.ToolsLabel, Is.EqualTo("ツール: 帰り道の糸（1 / 3）"));
            Assert.That(screen.Consumables[0].Note, Does.Not.Contain(".md"), "the design-document pointer is not for the player");
            Assert.That(screen.Consumables.Select(s => s.Name), Is.EqualTo(new[] { "早駆けの丸薬", "早駆けの丸薬", ExplorationText.EmptySlot }));
            Assert.That(screen.Consumables[0].CanUse, Is.True);
            Assert.That(screen.Consumables[2].CanUse, Is.False);

            int days = session.State.DaysLeft;
            Assert.That(session.UseConsumable(0), Is.True);
            Assert.That(session.State.DaysLeft, Is.EqualTo(days));
            Assert.That(session.Screen().Consumables.Count(s => s.Name == "早駆けの丸薬"), Is.EqualTo(1));

            var next = session.Screen();
            Assert.That(next.DaysForecastLabel, Is.EqualTo("次の 1 日は進まない"));
            var node = next.Nodes.First(n => n.Standing == NodeStanding.Reachable && n.Kind != NodeKind.Rest);
            Assert.That(node.Options.Single().Forecast.Lines.First(), Is.EqualTo("次の 1 日は進まない"));
        }

        [Test]
        public void TheSameSeedDrawsTheSameScreen()
        {
            var a = new ExplorationSession(new RunStart { Seed = 42 }).Screen();
            var b = new ExplorationSession(new RunStart { Seed = 42 }).Screen();

            Assert.That(b.Nodes.Select(n => (n.Id, n.Kind, n.X, n.Y)), Is.EqualTo(a.Nodes.Select(n => (n.Id, n.Kind, n.X, n.Y))));
            Assert.That(b.Edges.Select(e => (e.From, e.To)), Is.EqualTo(a.Edges.Select(e => (e.From, e.To))));
        }

        [Test]
        public void TheDungeonPageDescribesThisLayer()
        {
            var session = Explorable(new ExplorationSession());
            var book = session.Screen().Journal;

            Assert.That(book.Context, Is.EqualTo(JournalContext.Exploration));
            Assert.That(book.Pages.Select(p => p.Tab), Is.EqualTo(new[] { JournalTab.Enemy, JournalTab.Dungeon, JournalTab.Memo, JournalTab.Terms }));
            Assert.That(book.Pages.All(book.CanOpen), Is.True, "every tab opens while exploring");

            var dungeon = book.Pages.Single(p => p.Tab == JournalTab.Dungeon);
            Assert.That(dungeon.Title, Is.EqualTo("第 1 層　燦光の樹海"));
            Assert.That(dungeon.Rows.Select(r => r.Label), Is.EqualTo(new[] { "領域", "日数", "ノード", "情報収集" }));
            Assert.That(dungeon.TextOf(dungeon.Rows[0]), Is.EqualTo("共通"));
            Assert.That(dungeon.TextOf(dungeon.Rows[1]), Is.EqualTo($"寄り道なしで抜けると {session.State.Map.ShortestPathCost} 日"));
            Assert.That(dungeon.TextOf(dungeon.Rows[3]), Is.EqualTo("まだ"));
        }

        // ---- helpers ----

        /// <summary>Reports the entry battle won, so the map takes steps.</summary>
        internal static ExplorationSession Explorable(ExplorationSession session)
        {
            if (session.HasPendingBattle) session.Act(ScreenAction.WinBattle);
            Assert.That(session.MapIsLive, Is.True);
            return session;
        }

        /// <summary>Steps, and reports any battle the step opened as won.</summary>
        internal static void Step(ExplorationSession session, int nodeId, RestChoice choice = RestChoice.Rest)
        {
            Assert.That(session.Step(nodeId, choice), Is.True, $"step to {nodeId}");
            if (session.HasPendingBattle) session.Act(ScreenAction.WinBattle);
        }

        /// <summary>The first seed whose explorable opening frame passes the test.</summary>
        internal static ExplorationSession SeedWhere(System.Func<ExplorationSession, bool> test)
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var session = Explorable(new ExplorationSession(new RunStart { Seed = seed }));
                if (test(session)) return session;
            }
            Assert.Fail("no seed in 1..200 passes");
            return null;
        }

        /// <summary>Walks every node on the way to the boss straight down, winning each battle.</summary>
        internal static void WalkToBoss(ExplorationSession session)
        {
            Explorable(session);
            while (session.State.Phase == RunPhase.Exploring)
            {
                var map = session.State.Map;
                int row = map.Node(session.State.CurrentNodeId).Row;
                int next = map.Successors(session.State.CurrentNodeId).First(id => map.Node(id).Row == row + 1);
                Step(session, next);
            }
        }

        /// <summary>
        /// Clears layers straight down until the given layer's entry is reported. At the layer-two interlude it goes
        /// down into <paramref name="region"/> unless the life already chose one.
        /// </summary>
        internal static void WalkToLayer(ExplorationSession session, int layer, Region region = Region.Dragon)
        {
            Explorable(session);
            while (session.State.Profile.Layer < layer)
            {
                WalkToBoss(session);
                Assert.That(session.State.Phase, Is.EqualTo(RunPhase.LayerCleared));
                Assert.That(session.Act(ScreenAction.GoToInterlude), Is.True);
                if (session.NeedsRegion) Assert.That(session.Act(ScreenAction.ChooseRegion, (int)region), Is.True);
                Assert.That(session.Act(ScreenAction.Descend), Is.True);
                Explorable(session);
            }
        }
    }
}
