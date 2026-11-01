// #718: an イベント node resolves on the exploration screen (RunStart.ResolvesEvents), and the journal's dungeon page
// of its layer turns from 「？」 into what happened. Runs under `dotnet test` (Exploration.Bridge.Tests.csproj) and in
// Unity's EditMode runner.
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
    public class EventNodeTests
    {
        [Test]
        public void Off_TheNodeOnlySaysWhatMayHappen_AndTheJournalStaysUnknown()
        {
            var expedition = new Expedition(new RunStart { Seed = SeedWithEvent(EventKind.Spring) });
            ExplorationSession session = expedition.Session;
            int node = EventNode(session, EventKind.Spring);
            WalkTo(expedition, node);

            Assert.Multiple(() =>
            {
                Assert.That(session.Keeper, Is.Null);
                Assert.That(session.Events, Is.Empty);
                Assert.That(expedition.Journal.EntryLevel(DungeonJournal.EventId(1, "spring")), Is.EqualTo(0));
                Assert.That(session.Screen().Node(node).Lines, Is.EqualTo(new[] { "泉 / 重い宝箱 / 特殊な食事 のどれかが起きる" }));
            });
        }

        [Test]
        public void ASpring_Heals_AndItsRowTurnsFromUnknownIntoWhatHappened()
        {
            var expedition = OnWith(EventKind.Spring, hp: 20);
            ExplorationSession session = expedition.Session;
            int node = EventNode(session, EventKind.Spring);
            Assert.That(DungeonRow(session, "泉"), Is.EqualTo(JournalRow.Hidden));

            WalkTo(expedition, node);

            Assert.Multiple(() =>
            {
                Assert.That(session.HasPendingEvent, Is.False, "a 泉 asks nothing");
                Assert.That(session.State.Hp, Is.GreaterThan(20));
                Assert.That(session.Events.Single().Kind, Is.EqualTo(EventKind.Spring));
                Assert.That(DungeonRow(session, "泉"), Is.EqualTo("HP が最大の 20% 戻る"));
                Assert.That(DungeonRow(session, "重い宝箱"), Is.EqualTo(JournalRow.Hidden), "the other entries stay unknown");
            });
        }

        [Test]
        public void ASpringNextDoor_NamesItself_AndItsForecastCountsTheHp()
        {
            var expedition = OnWith(EventKind.Spring, hp: 20);
            ExplorationSession session = expedition.Session;
            int node = EventNode(session, EventKind.Spring);
            WalkTo(expedition, node, stopShort: true);

            NodeCard card = session.Screen().Node(node);
            Assert.Multiple(() =>
            {
                Assert.That(card.Lines[0], Does.StartWith("泉"));
                Assert.That(card.Options.Single().Forecast.HpAfter, Is.GreaterThan(card.Options.Single().Forecast.HpBefore));
            });
        }

        [Test]
        public void AChestAccepted_CostsMaxStamina_AndHandsItsFindsOverLikeABattlesReward()
        {
            var expedition = OnWith(EventKind.HeavyChest);
            ExplorationSession session = expedition.Session;
            WalkTo(expedition, EventNode(session, EventKind.HeavyChest));
            Assert.That(session.HasPendingEvent, Is.True);
            Assert.That(session.Screen().Card.Kind, Is.EqualTo(CardKind.EventChoice));
            Assert.That(session.MapIsLive, Is.False, "the card asks first");
            int mod = session.State.TempMaxStaminaMod;
            int found = expedition.Consumables.Count;

            Assert.That(session.Act(ScreenAction.AcceptEvent), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(session.State.TempMaxStaminaMod, Is.EqualTo(mod - LayerEvents.ChestStaminaCost));
                Assert.That(CardPicks.For(expedition).Kind, Is.EqualTo(PickKind.PileCard), "layer 1's chest holds a card from the pile");
                Assert.That(expedition.Consumables.Count, Is.EqualTo(found + 1), "and a consumable");
                Assert.That(session.State.Loadout.Consumables.Last().Id, Is.EqualTo(expedition.Consumables.Last()), "which went into a free slot");
                Assert.That(DungeonRow(session, "重い宝箱"), Does.StartWith("最大スタミナ -2"));
                Assert.That(session.MapIsLive, Is.True);
            });
        }

        [Test]
        public void AChestWithFullSlots_LeavesItsConsumableForTheSwapScreen()
        {
            var expedition = OnWith(EventKind.HeavyChest, Full());
            ExplorationSession session = expedition.Session;
            WalkTo(expedition, EventNode(session, EventKind.HeavyChest));
            ConsumableSwap none = ConsumableSwaps.For(expedition);
            int waiting = expedition.WaitingConsumables.Count;
            Assert.That(none.IsShown, Is.EqualTo(waiting > 0), "whatever a battle on the way left");

            Assert.That(session.Act(ScreenAction.AcceptEvent), Is.True);
            Assert.That(expedition.WaitingConsumables.Count, Is.EqualTo(waiting + 1));
            CardPick pick = CardPicks.For(expedition);
            Assert.That(pick.IsShown, Is.True, "the card first");
            Assert.That(CardPicks.Choose(expedition, pick.Choices[0].CardId), Is.True);
            Assert.That(ConsumableSwaps.For(expedition).IsShown, Is.True, "then the swap");
        }

        [Test]
        public void AChestDeclined_ChangesNothing_ButTheJournalKeepsWhatWasSeen()
        {
            var expedition = OnWith(EventKind.HeavyChest);
            ExplorationSession session = expedition.Session;
            int node = EventNode(session, EventKind.HeavyChest);
            WalkTo(expedition, node);
            int mod = session.State.TempMaxStaminaMod;
            int found = expedition.Consumables.Count;
            IReadOnlyList<string> offer = expedition.Offer.ToList();

            Assert.That(session.Act(ScreenAction.DeclineEvent), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(session.State.TempMaxStaminaMod, Is.EqualTo(mod));
                Assert.That(expedition.Consumables.Count, Is.EqualTo(found));
                Assert.That(expedition.Offer, Is.EqualTo(offer));
                Assert.That(expedition.Journal.EntryLevel(DungeonJournal.EventId(1, "heavy_chest")), Is.EqualTo(DungeonJournal.MetDisclosure));
                Assert.That(session.Screen().Node(node).Lines.Last(), Is.EqualTo("結果: 受けなかった"));
            });
        }

        [Test]
        public void AMealAccepted_RaisesMaxStamina_ForAShareOfTheHp()
        {
            var expedition = OnWith(EventKind.StrangeMeal);
            ExplorationSession session = expedition.Session;
            WalkTo(expedition, EventNode(session, EventKind.StrangeMeal));
            int hp = session.State.Hp;
            int mod = session.State.TempMaxStaminaMod;

            Assert.That(session.Act(ScreenAction.AcceptEvent), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(session.State.Hp, Is.EqualTo(hp - ExplorationReducer.MealCost(session.State)));
                Assert.That(session.State.TempMaxStaminaMod, Is.EqualTo(mod + LayerEvents.MealStaminaGain));
                Assert.That(DungeonRow(session, "特殊な食事"), Does.StartWith("最大スタミナ +1"));
            });
        }

        [Test]
        public void AChestsPage_IsReadIntoTheJournal_OrTurnsIntoACardWhereTheLayerHasNothingToTell()
        {
            var read = new Expedition(new RunStart { Seed = 3, ResolvesEvents = true });
            read.TakeChest(2, 0, new ChestContents(0, 0, 1));
            Assert.Multiple(() =>
            {
                Assert.That(read.Pages, Is.EqualTo(1));
                Assert.That(DungeonJournal.OfLayer(2).Count(e => read.Journal.EntryLevel(e.Id) == DungeonJournal.FullDisclosure), Is.EqualTo(1));
                Assert.That(read.Offer, Is.Empty);
            });

            var turned = new Expedition(new RunStart { Seed = 3, ResolvesEvents = true });
            turned.TakeChest(5, 0, new ChestContents(0, 0, 1));
            Assert.Multiple(() =>
            {
                Assert.That(turned.Pages, Is.EqualTo(0), "layers 4〜6 have no entry to tell");
                Assert.That(turned.Offer, Is.Not.Empty, "the page turned into the pile's cards");
            });
        }

        [Test]
        public void ANewLife_StartsTheEntriesUnknownAgain()
        {
            var expedition = OnWith(EventKind.Spring);
            ExplorationSession session = expedition.Session;
            WalkTo(expedition, EventNode(session, EventKind.Spring));
            Assert.That(DungeonRow(session, "泉"), Is.Not.EqualTo(JournalRow.Hidden));

            Assert.That(session.Act(ScreenAction.AskEndLife), Is.True);
            Assert.That(session.Act(ScreenAction.ConfirmEndLife), Is.True);
            Assert.That(session.Act(ScreenAction.NewLife), Is.True);
            Assert.That(DungeonRow(session, "泉"), Is.EqualTo(JournalRow.Hidden));
        }

        [Test]
        public void TheTrial_KeepsItsOwnRecord_AndLeavesThisJournalAlone()
        {
            var expedition = new Expedition(new RunStart { Seed = SeedWithEvent(EventKind.Spring), Trial = true });
            ExplorationSession session = expedition.Session;
            WalkTo(expedition, EventNode(session, EventKind.Spring));

            Assert.That(session.Events, Is.Not.Empty);
            Assert.That(expedition.Journal.EntryLevel(DungeonJournal.EventId(1, "spring")), Is.EqualTo(0));
        }

        // ---- helpers ----

        private static RunLoadout Full() =>
            new RunLoadout(null, Enumerable.Repeat(RunStart.Consumable("joka_no_ko"), RunLoadout.ConsumableSlots).ToList());

        private static Expedition OnWith(EventKind kind, RunLoadout loadout = null, int hp = RunStart.DefaultMaxHp) =>
            new Expedition(new RunStart { Seed = SeedWithEvent(kind), ResolvesEvents = true, Loadout = loadout ?? RunLoadout.Empty, Hp = hp });

        /// <summary>The first seed whose first layer holds this event. The kind depends on the seed alone.</summary>
        private static ulong SeedWithEvent(EventKind kind)
        {
            for (ulong seed = 1; seed < 200; seed++)
            {
                var session = new ExplorationSession(new RunStart { Seed = seed, ResolvesEvents = true });
                if (session.State.Map.Nodes.Any(n => session.EventAt(n.Id) == kind)) return seed;
            }
            Assert.Fail($"no seed in 1〜199 puts a {kind} on layer 1");
            return 0;
        }

        private static int EventNode(ExplorationSession session, EventKind kind)
        {
            var kinds = LayerEvents.Draw(session.State.Map, 1, id => ExplorationSession.EventSeed(session.RunSeed, 1, id));
            return kinds.First(p => p.Value == kind).Key;
        }

        /// <summary>What the layer's dungeon page says of this entry, as the drawer shows it.</summary>
        private static string DungeonRow(ExplorationSession session, string entry)
        {
            JournalPage page = session.Screen().Journal.Pages.First(p => p.Id == DungeonJournal.PageId(1));
            return page.TextOf(page.Rows.Single(r => r.Label == entry));
        }

        /// <summary>
        /// Walks the shortest way to a node, winning every battle and turning down every other event on the way.
        /// With <paramref name="stopShort"/>, stops on the node before it.
        /// </summary>
        private static void WalkTo(Expedition expedition, int target, bool stopShort = false)
        {
            ExplorationSession session = expedition.Session;
            if (session.HasPendingBattle) WinPending(expedition);
            ExplorationState state = session.State;
            LayerMap map = state.Map;
            var from = new Dictionary<int, int> { [state.CurrentNodeId] = -1 };
            var queue = new Queue<int>();
            queue.Enqueue(state.CurrentNodeId);
            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                foreach (int next in map.Neighbours(node))
                {
                    if (from.ContainsKey(next)) continue;
                    from[next] = node;
                    queue.Enqueue(next);
                }
            }
            var path = new List<int>();
            for (int node = target; node != state.CurrentNodeId; node = from[node]) path.Add(node);
            path.Reverse();
            if (stopShort) path.RemoveAt(path.Count - 1);
            foreach (int node in path)
            {
                ClearTheMap(expedition);
                Assert.That(session.Step(node), Is.True, $"step to {node}");
                if (session.HasPendingBattle) WinPending(expedition);
                if (node != target && session.HasPendingEvent) Assert.That(session.Act(ScreenAction.DeclineEvent), Is.True);
            }
        }

        /// <summary>What a battle on the way left: the card and the consumables are answered so the walk goes on.</summary>
        private static void ClearTheMap(Expedition expedition)
        {
            CardPick pick = CardPicks.For(expedition);
            if (pick.IsShown) CardPicks.Choose(expedition, pick.Choices[0].CardId);
        }

        /// <summary>Wins whatever battle waits, at full HP, without playing it out.</summary>
        private static void WinPending(Expedition expedition)
        {
            EncounterBattle battle = expedition.StartEncounter();
            Assert.That(battle, Is.Not.Null);
            int hp = expedition.Session.State.Hp;
            while (!battle.Over)
            {
                CoreBattleSource source = battle.StartNext();
                BattleState state = source.State;
                BattleState won = state with { Result = GameResult.Won, Player = state.Player with { Hp = hp, Stamina = 5 } };
                battle.Finish(won, new List<BattleEvent>(source.History));
            }
            expedition.Return();
        }
    }
}
