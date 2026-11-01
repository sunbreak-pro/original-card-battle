// #651: a consumable a won battle drops goes into the exploration's free slot. With every slot full it
// waits on Expedition.WaitingConsumables until the player swaps it in for a named slot or gives it up;
// the exploration does not stop for it. #719: the swap screen (ConsumableSwaps) over that list. Runs under
// `dotnet test` (Exploration.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction.Bridge;
using DungeonCore;
using NUnit.Framework;

namespace Exploration.Bridge.Tests
{
    public class FoundConsumablesTests
    {
        [Test]
        public void AConsumableFound_GoesIntoAFreeSlot()
        {
            ulong seed = SeedWhoseEntryBattleDropsAConsumable(out string found);
            var expedition = new Expedition(new RunStart { Seed = seed });
            WinPending(expedition);

            Assert.Multiple(() =>
            {
                Assert.That(expedition.Session.State.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(new[] { found }));
                Assert.That(expedition.WaitingConsumables, Is.Empty);
                Assert.That(expedition.Consumables, Is.EqualTo(new[] { found }), "the record of what was found keeps it too");
            });
        }

        [Test]
        public void WithEverySlotFull_TheConsumableWaits_AndTheExplorationGoesOn()
        {
            ulong seed = SeedWhoseEntryBattleDropsAConsumable(out string found);
            var expedition = new Expedition(new RunStart { Seed = seed, Loadout = Full() });
            WinPending(expedition);

            Assert.Multiple(() =>
            {
                Assert.That(expedition.WaitingConsumables, Is.EqualTo(new[] { found }));
                Assert.That(expedition.Session.State.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(Enumerable.Repeat("joka_no_ko", 3)));
                Assert.That(expedition.Session.MapIsLive, Is.True, "the exploration does not stop for the swap");
            });
        }

        [Test]
        public void AWaitingConsumable_SwappedForANamedSlot_TakesItsPlace()
        {
            ulong seed = SeedWhoseEntryBattleDropsAConsumable(out string found);
            var expedition = new Expedition(new RunStart { Seed = seed, Loadout = Full() });
            WinPending(expedition);

            Assert.That(expedition.SwapConsumable(found, 1), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(expedition.Session.State.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "joka_no_ko", found, "joka_no_ko" }));
                Assert.That(expedition.WaitingConsumables, Is.Empty);
                Assert.That(expedition.SwapConsumable(found, 1), Is.False, "it is no longer waiting");
            });
        }

        [Test]
        public void ASwapThatNamesNoSlot_Throws_AndTheConsumableKeepsWaiting()
        {
            ulong seed = SeedWhoseEntryBattleDropsAConsumable(out string found);
            var expedition = new Expedition(new RunStart { Seed = seed, Loadout = Full() });
            WinPending(expedition);

            Assert.Throws<ExplorationRuleException>(() => expedition.SwapConsumable(found, -1));
            Assert.That(expedition.WaitingConsumables, Is.EqualTo(new[] { found }));
        }

        [Test]
        public void AWaitingConsumable_GivenUp_LeavesTheList()
        {
            ulong seed = SeedWhoseEntryBattleDropsAConsumable(out string found);
            var expedition = new Expedition(new RunStart { Seed = seed, Loadout = Full() });
            WinPending(expedition);

            Assert.That(expedition.DiscardConsumable(found), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(expedition.WaitingConsumables, Is.Empty);
                Assert.That(expedition.Session.State.Loadout.Consumables.Count, Is.EqualTo(3), "the slots keep what they held");
                Assert.That(expedition.DiscardConsumable(found), Is.False);
            });
        }

        [Test]
        public void ANewLife_StartsWithNothingWaiting()
        {
            ulong seed = SeedWhoseEntryBattleDropsAConsumable(out _);
            var expedition = new Expedition(new RunStart { Seed = seed, Loadout = Full() });
            WinPending(expedition);
            Assert.That(expedition.WaitingConsumables, Is.Not.Empty);

            ExplorationSession session = expedition.Session;
            Assert.That(session.Act(ScreenAction.AskEndLife), Is.True);
            Assert.That(session.Act(ScreenAction.ConfirmEndLife), Is.True);
            Assert.That(session.Act(ScreenAction.NewLife), Is.True);
            Assert.That(expedition.WaitingConsumables, Is.Empty);
        }

        // ---- the swap screen (#719) ----

        [Test]
        public void NothingWaiting_ShowsNoSwapScreen()
        {
            ulong seed = SeedWhoseEntryBattleDropsAConsumable(out _);
            var expedition = new Expedition(new RunStart { Seed = seed });
            Assert.That(ConsumableSwaps.For(expedition).IsShown, Is.False, "the battle is not fought yet");
            WinPending(expedition);

            Assert.Multiple(() =>
            {
                Assert.That(ConsumableSwaps.For(expedition).IsShown, Is.False, "the find went into a free slot");
                Assert.That(ConsumableSwaps.Swap(expedition, 0), Is.False);
                Assert.That(ConsumableSwaps.Discard(expedition), Is.False);
            });
        }

        [Test]
        public void AConsumableWaiting_ShowsTheSwapScreen_WithItsThreeSlots()
        {
            ulong seed = SeedWhoseEntryBattleDropsAConsumable(out string found);
            var expedition = new Expedition(new RunStart { Seed = seed, Loadout = Full() });
            WinPending(expedition);

            ConsumableSwap swap = ConsumableSwaps.For(expedition);
            string name = ExplorationText.ItemName(found);
            Assert.Multiple(() =>
            {
                Assert.That(swap.IsShown, Is.True);
                Assert.That(swap.ItemId, Is.EqualTo(found));
                Assert.That(swap.Title, Is.EqualTo(ExplorationText.SwapTitle));
                Assert.That(swap.Prompt, Does.StartWith(name + "を拾った。"));
                Assert.That(swap.ItemLine, Does.StartWith(name + ": "));
                Assert.That(swap.Slots.Select(s => s.Index), Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(swap.Slots[0].Label, Is.EqualTo("浄化の香と入れ替える"));
                Assert.That(swap.Slots[0].Note, Is.Not.Empty);
                Assert.That(swap.DiscardLabel, Is.EqualTo("諦める"));
            });
        }

        [Test]
        public void PressingASlot_SwapsThatSlot_AndTheScreenCloses()
        {
            ulong seed = SeedWhoseEntryBattleDropsAConsumable(out string found);
            var expedition = new Expedition(new RunStart { Seed = seed, Loadout = Full() });
            WinPending(expedition);

            Assert.That(ConsumableSwaps.Swap(expedition, 3), Is.False, "no such slot");
            Assert.That(ConsumableSwaps.Swap(expedition, 2), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(expedition.Session.State.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "joka_no_ko", "joka_no_ko", found }));
                Assert.That(expedition.WaitingConsumables, Is.Empty);
                Assert.That(ConsumableSwaps.For(expedition).IsShown, Is.False);
            });
        }

        [Test]
        public void PressingGiveUp_TakesItOffTheList_AndTheSlotsKeepWhatTheyHeld()
        {
            ulong seed = SeedWhoseEntryBattleDropsAConsumable(out _);
            var expedition = new Expedition(new RunStart { Seed = seed, Loadout = Full() });
            WinPending(expedition);

            Assert.That(ConsumableSwaps.Discard(expedition), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(expedition.WaitingConsumables, Is.Empty);
                Assert.That(expedition.Session.State.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(Enumerable.Repeat("joka_no_ko", 3)));
                Assert.That(ConsumableSwaps.For(expedition).IsShown, Is.False);
            });
        }

        [Test]
        public void ACardToPick_ComesBeforeTheSwap()
        {
            ulong seed = SeedWhoseEntryBattleDropsAConsumable(out string found);
            var expedition = new Expedition(new RunStart { Seed = seed, Loadout = Full() });
            WinPending(expedition);
            expedition.TakeChest(1, expedition.Session.State.CurrentNodeId, new ChestContents(1, 0, 0));

            CardPick pick = CardPicks.For(expedition);
            Assert.That(pick.IsShown, Is.True);
            Assert.That(ConsumableSwaps.For(expedition).IsShown, Is.False, "the card first");
            Assert.That(ConsumableSwaps.Discard(expedition), Is.False, "a press cannot reach the hidden screen");

            Assert.That(CardPicks.Choose(expedition, pick.Choices[0].CardId), Is.True);
            Assert.That(ConsumableSwaps.For(expedition).ItemId, Is.EqualTo(found), "then the item");
        }

        [Test]
        public void ASlotFreedSinceItWasFound_IsOffered_AndTakesTheItemWithoutASwap()
        {
            ulong seed = SeedWhoseEntryBattleDropsAConsumable(out string found);
            var expedition = new Expedition(new RunStart { Seed = seed, Loadout = Full() });
            WinPending(expedition);
            Assert.That(expedition.Session.UseConsumable(0), Is.True);

            ConsumableSwap swap = ConsumableSwaps.For(expedition);
            Assert.That(swap.Slots.Last().Index, Is.EqualTo(-1));
            Assert.That(swap.Slots.Last().Label, Is.EqualTo(ExplorationText.EmptySlot));

            Assert.That(ConsumableSwaps.Swap(expedition, -1), Is.True);
            Assert.That(expedition.Session.State.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "joka_no_ko", "joka_no_ko", found }));
        }

        [Test]
        public void OnceTheLifeIsOver_NoSwapScreenIsShown()
        {
            ulong seed = SeedWhoseEntryBattleDropsAConsumable(out string found);
            var expedition = new Expedition(new RunStart { Seed = seed, Loadout = Full() });
            WinPending(expedition);
            ExplorationSession session = expedition.Session;
            Assert.That(session.Act(ScreenAction.AskEndLife), Is.True);
            Assert.That(session.Act(ScreenAction.ConfirmEndLife), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(expedition.WaitingConsumables, Is.EqualTo(new[] { found }), "still on the list");
                Assert.That(ConsumableSwaps.For(expedition).IsShown, Is.False, "no screen once the life is over");
            });
        }

        // ---- helpers ----

        private static RunLoadout Full() =>
            new RunLoadout(null, Enumerable.Repeat(RunStart.Consumable("joka_no_ko"), RunLoadout.ConsumableSlots).ToList());

        /// <summary>The first seed whose entry battle, won, drops a consumable; the reward depends on the seed alone.</summary>
        private static ulong SeedWhoseEntryBattleDropsAConsumable(out string found)
        {
            for (ulong seed = 1; seed < 200; seed++)
            {
                BattleReturn back = WinPending(new Expedition(new RunStart { Seed = seed }));
                if (back.Reward.Consumables.Count == 0) continue;
                found = back.Reward.Consumables[0];
                return seed;
            }
            Assert.Fail("no seed in 1〜199 dropped a consumable at the entry battle");
            found = null;
            return 0;
        }

        /// <summary>Wins whatever battle waits, at full HP, without playing it out.</summary>
        private static BattleReturn WinPending(Expedition expedition)
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
            return expedition.Return();
        }
    }
}
