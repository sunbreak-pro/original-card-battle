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
            var layer = SevenLayers.Of(1);

            Assert.That(screen.LayerTitle, Is.EqualTo("第 1 層　燦光の樹海"));
            Assert.That(screen.TimeLabel, Is.EqualTo($"刻限 {layer.TimeLimit - 1} / {layer.TimeLimit}"));
            Assert.That(screen.Miasma.Label, Is.EqualTo($"瘴気 {layer.Density}%"));
            Assert.That(screen.HpLabel, Is.EqualTo("HP 50 / 50"));
            Assert.That(screen.Nodes.Count, Is.EqualTo(session.State.Map.NodeCount));
            Assert.That(screen.CurrentNodeId, Is.EqualTo(session.State.Map.EntryId));
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
                Assert.That(node.ShowsContents, Is.False);
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

                        Assert.That(taken.TimeLeft, Is.EqualTo(option.Forecast.TimeAfter));
                        Assert.That(taken.MiasmaPercent, Is.EqualTo(option.Forecast.MiasmaAfter));
                        Assert.That(taken.MaxStamina, Is.EqualTo(option.Forecast.MaxStaminaAfter));
                        Assert.That(taken.Stamina, Is.EqualTo(option.Forecast.StaminaAfter));
                        Assert.That(taken.Hp, Is.EqualTo(option.Forecast.HpAfter));
                    }
                }
            }
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
            Assert.That(back.Note, Does.Contain("刻限を使わずに"));

            int timeBefore = session.State.TimeLeft;
            Assert.That(session.Step(entry), Is.True);
            Assert.That(session.State.TimeLeft, Is.EqualTo(timeBefore));
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
        public void TheGaugeForetellsTheNextNewNode()
        {
            var session = Explorable(new ExplorationSession());
            var gauge = session.Screen().Miasma;

            Assert.That(gauge.NextPercent, Is.EqualTo(gauge.Percent + SevenLayers.Of(1).Density));
            Assert.That(gauge.ForecastLabel, Is.EqualTo($"次の 1 歩で +{SevenLayers.Of(1).Density}%"));
            Assert.That(gauge.StepLines, Is.EqualTo(new[] { 20, 40, 60, 80 }));
        }

        [Test]
        public void CrossingATwentyPercentLineIsWarned()
        {
            var session = Explorable(new ExplorationSession(new RunStart { MiasmaPercent = 18 }));
            Assert.That(session.State.MiasmaPercent, Is.EqualTo(19));

            var node = session.Screen().Nodes.First(n => n.Standing == NodeStanding.Reachable && n.Kind != NodeKind.Rest);
            var forecast = node.Options.Single().Forecast;

            Assert.That(forecast.MiasmaAfter, Is.EqualTo(20));
            Assert.That(forecast.MaxStaminaAfter, Is.EqualTo(forecast.MaxStaminaBefore - 1));
            Assert.That(forecast.Warnings, Has.Member("瘴気が 20% を越え、最大スタミナが 10 から 9 に下がります"));
            Assert.That(forecast.IsLethal, Is.False);
        }

        [Test]
        public void RestingAcrossALineWarnsOnlyWhenTheMaximumReallyFalls()
        {
            // A 休息 lifts max stamina by 2, so crossing 20% on 「休む」 still raises it: no warning.
            // 「訓練する」 on the same node crosses the same line without the lift, and is warned.
            ExplorationSession session = null;
            for (ulong seed = 1; seed <= 200 && session == null; seed++)
            {
                var probe = Explorable(new ExplorationSession(new RunStart { Seed = seed, MiasmaPercent = 18 }));
                if (probe.Screen().Nodes.Any(n => n.Standing == NodeStanding.Reachable && n.Kind == NodeKind.Rest)) session = probe;
            }
            Assert.That(session, Is.Not.Null, "no seed in 1..200 opens next to a rest node");

            var rest = session.Screen().Nodes.First(n => n.Standing == NodeStanding.Reachable && n.Kind == NodeKind.Rest);
            var restForecast = rest.Options.Single(o => o.Choice == RestChoice.Rest).Forecast;
            var trainForecast = rest.Options.Single(o => o.Choice == RestChoice.Train).Forecast;

            Assert.That(restForecast.MaxStaminaAfter, Is.EqualTo(11));
            Assert.That(restForecast.Warnings, Is.Empty);
            Assert.That(trainForecast.MaxStaminaAfter, Is.EqualTo(9));
            Assert.That(trainForecast.Warnings, Has.Member("瘴気が 20% を越え、最大スタミナが 10 から 9 に下がります"));
        }

        [Test]
        public void TheDetailPanelTextComesWholeFromTheScreen()
        {
            var screen = Explorable(new ExplorationSession()).Screen();

            var far = screen.Nodes.First(n => n.Standing == NodeStanding.Far);
            Assert.That(far.Detail, Is.EqualTo(new[] { ExplorationText.FarNode }));
            Assert.That(far.Title, Is.EqualTo($"{far.Glyph}　{far.KindLabel}"));

            var near = screen.Nodes.First(n => n.Standing == NodeStanding.Reachable && n.Kind != NodeKind.Rest);
            Assert.That(near.Detail.First(), Is.EqualTo("入ると刻限を 1 使う"));
            Assert.That(near.Detail, Has.Some.StartsWith("【進む】刻限 "));
        }

        [Test]
        public void MaxStaminaIsSpeltOut()
        {
            var session = Explorable(new ExplorationSession(new RunStart { MiasmaPercent = 40 }));
            Assert.That(session.Screen().MaxStaminaLabel, Is.EqualTo("最大 8 = 10 − 瘴気 2"));
        }

        [Test]
        public void AStepThatEndsTheLifeWaitsForAConfirmation()
        {
            var session = Explorable(new ExplorationSession(new RunStart { MiasmaPercent = 98 }));
            int target = session.State.ReachableUnresolved().First();
            var option = session.Screen().Node(target).Options.First();
            Assert.That(option.NeedsConfirm, Is.True);
            Assert.That(option.Forecast.Warnings, Has.Member("ここで瘴気が 100% に達し、この生が終わります"));

            Assert.That(session.Step(target, option.Choice), Is.True);
            Assert.That(session.Screen().Card.Kind, Is.EqualTo(CardKind.ConfirmStep));
            Assert.That(session.State.CurrentNodeId, Is.Not.EqualTo(target), "nothing is taken before the confirmation");

            Assert.That(session.Act(ScreenAction.Cancel), Is.True);
            Assert.That(session.Screen().Card.IsShown, Is.False);
            Assert.That(session.State.MiasmaPercent, Is.EqualTo(99));

            session.Step(target, option.Choice);
            Assert.That(session.Act(ScreenAction.ConfirmStep), Is.True);
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.MiasmaDeath));

            var end = session.Screen().Card;
            Assert.That(end.Kind, Is.EqualTo(CardKind.MiasmaDeath), "瘴気死 comes before any fight");
            Assert.That(end.Buttons.Single().Action, Is.EqualTo(ScreenAction.NewLife));
        }

        [Test]
        public void TheLastTimeUnitWarnsOfBeingPushedOut()
        {
            // Layer seven: 刻限 4 over rows 1-3-1. Entry, two middles, then the third middle spends the last unit.
            var session = new ExplorationSession();
            WalkToLayer(session, 7);
            int entry = session.State.CurrentNodeId;
            var middle = session.State.Map.Row(1).Select(n => n.Id).ToList();

            Step(session, middle[0]);
            Assert.That(session.Step(entry), Is.True);
            Step(session, middle[1]);
            Assert.That(session.Step(entry), Is.True);

            var last = session.Screen().Node(middle[2]);
            Assert.That(last.Options.First().Forecast.Warnings, Has.Member("刻限が尽き、この層の行動が終わります"));
        }

        [Test]
        public void ConsumablesShowInTheirSlotsAndCostNoTime()
        {
            var session = Explorable(new ExplorationSession(new RunStart { Loadout = RunStart.TrialLoadout(), MiasmaPercent = 30 }));
            var screen = session.Screen();

            Assert.That(screen.ToolsLabel, Is.EqualTo("ツール: 防瘴の面（1 / 3）"));
            Assert.That(screen.Consumables[0].Note, Is.EqualTo("瘴気の蓄積が 10% 戻る"), "the design-document pointer is not for the player");
            Assert.That(screen.Consumables.Select(s => s.Name), Is.EqualTo(new[] { "浄化の香", "浄化の香", ExplorationText.EmptySlot }));
            Assert.That(screen.Consumables[0].CanUse, Is.True);
            Assert.That(screen.Consumables[2].CanUse, Is.False);

            int time = session.State.TimeLeft;
            int miasma = session.State.MiasmaPercent;
            Assert.That(session.UseConsumable(0), Is.True);
            Assert.That(session.State.TimeLeft, Is.EqualTo(time));
            Assert.That(session.State.MiasmaPercent, Is.EqualTo(miasma - 10));
            Assert.That(session.Screen().Consumables.Count(s => s.Name == "浄化の香"), Is.EqualTo(1));
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
            Assert.That(book.Pages.Select(p => p.Tab), Is.EqualTo(new[] { JournalTab.Enemy, JournalTab.Dungeon, JournalTab.Memo }));
            Assert.That(book.Openable.Count, Is.EqualTo(book.Pages.Count), "every tab opens while exploring");

            var dungeon = book.Pages.Single(p => p.Tab == JournalTab.Dungeon);
            Assert.That(dungeon.Title, Is.EqualTo("第 1 層　燦光の樹海"));
            Assert.That(dungeon.Rows.Select(r => r.Label), Is.EqualTo(new[] { "瘴気の濃さ", "刻限", "ノード", "情報収集" }));
            Assert.That(dungeon.TextOf(dungeon.Rows[0]), Is.EqualTo("1 刻限ごとに +1%"));
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

        /// <summary>Clears layers straight down until the given layer's entry is reported.</summary>
        internal static void WalkToLayer(ExplorationSession session, int layer)
        {
            Explorable(session);
            while (session.State.Profile.Layer < layer)
            {
                WalkToBoss(session);
                Assert.That(session.State.Phase, Is.EqualTo(RunPhase.LayerCleared));
                Assert.That(session.Act(ScreenAction.GoToInterlude), Is.True);
                Assert.That(session.Act(ScreenAction.Descend), Is.True);
                Explorable(session);
            }
        }
    }
}
