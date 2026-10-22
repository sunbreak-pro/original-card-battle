// The Expedition's trial extension (the browser trial's docs/EXTENSIONS.md 「探索の持ち物とイベント」「探索の結果と
// 再開」「出立の枠（DeckLimits）」): the Expedition half of test/ext/explore-results.test.mjs, test/ext/world.test.mjs and
// the hand-off tests of test/ext/limits.test.mjs. The session and screen halves are Exploration.Tests'
// (TrialSessionTests, TrialResultsTests). Where the JS replays a recorded log (app/recorder.js, Trial.Bridge's), the
// replay here is the same calls made again on a fresh expedition. Runs under `dotnet test` (Exploration.Bridge.Tests)
// and in Unity's EditMode runner.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BattleCore;
using DungeonContent;
using DungeonCore;
using Journal;
using NUnit.Framework;
using static Exploration.Bridge.Tests.TrialTestKit;

namespace Exploration.Bridge.Tests
{
    public class ExpeditionTrialTests
    {
        private static IReadOnlyList<string> LastRecord(ExplorationSession s) => s.Events[s.Events.Count - 1].Record;

        // ---------------------------------------------------------------- the pending result

        [Test]
        public void AWonBattlesResult_WaitsUntilClosed_AndAReplayBringsItBackWithItsPick()
        {
            // js: test/ext/explore-results.test.mjs:107
            Expedition Play(bool close)
            {
                Expedition ex = Trial(4);
                EndEveryFight(ex.StartEncounter(), GameResult.Won, 40, 5);
                ex.Return();
                if (close) ex.CloseResult();
                return ex;
            }

            Expedition raw = Trial(4);
            EndEveryFight(raw.StartEncounter(), GameResult.Won, 40, 5);
            BattleReturn back = raw.Return();
            ResultCard pending = raw.PendingResult;
            Assert.That(pending.Kind, Is.EqualTo(ResultCardKind.Battle));
            Assert.That(pending.Back, Is.SameAs(back));
            var offer = raw.Offer.ToList();

            // a reload: the same calls on a fresh expedition give the same pending card and the same pick
            Expedition again = Play(false);
            Assert.That(again.PendingResult.Kind, Is.EqualTo(ResultCardKind.Battle));
            Assert.That(again.Offer, Is.EqualTo(offer));
            Assert.That(again.PendingResult.Raised, Is.EqualTo(pending.Raised));

            // closing it (「受け取らない」) gives the pick up, and a replay closes it too
            Assert.That(raw.CloseResult(), Is.True);
            Assert.That(raw.PendingResult, Is.Null);
            Assert.That(raw.Offer, Is.Empty);
            Expedition third = Play(true);
            Assert.That(third.PendingResult, Is.Null);
            Assert.That(third.Offer, Is.Empty);
        }

        [Test]
        public void LearningThePick_ClosesTheResult()
        {
            // js: test/ext/explore-results.test.mjs:135
            for (ulong seed = 1; seed <= 40; seed++)
            {
                Expedition ex = Trial(seed);
                Win(ex);
                if (ex.Offer.Count == 0) continue;
                Assert.That(ex.Learn(ex.Offer[0]), Is.True);
                Assert.That(ex.PendingResult, Is.Null);
                return;
            }
            Assert.Fail("no seed dealt a card on the entry fight");
        }

        [Test]
        public void AResult_IsDroppedOnceTheExplorationMovesOn()
        {
            // js: test/ext/explore-results.test.mjs:147 — a save from before CloseResult existed
            Expedition ex = Trial(4);
            Win(ex);
            Assert.That(ex.PendingResult, Is.Not.Null);
            ClearChoices(ex.Session);
            int next = PathTo(ex.Session.State, _ => true)[0];
            Assert.That(ex.Session.Step(next), Is.True);
            Assert.That(ex.PendingResult, Is.Null);
        }

        [Test]
        public void AnAcceptedEvent_WaitsUntilClosed_ADeclinedOneShowsNothing()
        {
            // js: test/ext/explore-results.test.mjs:158
            int done = 0;
            for (ulong seed = 1; seed <= 60 && done < 2; seed++)
            {
                Expedition ex = Trial(seed);
                ExplorationSession s = ex.Session;
                MapNode node = s.State.Map.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Event);
                if (node == null) continue;
                WalkTo(ex, n => n.Id == node.Id);
                if (s.HasPendingBattle || s.State.IsOver) continue;
                if (s.HasPendingEvent)
                {
                    bool accept = done == 0;
                    s.Act(accept ? ScreenAction.AcceptEvent : ScreenAction.DeclineEvent);
                    if (accept)
                    {
                        Assert.That(ex.PendingResult.Kind, Is.EqualTo(ResultCardKind.Event));
                        Assert.That(ex.PendingResult.Outcome, Is.SameAs(s.Events[s.Events.Count - 1]));
                        ex.CloseResult();
                        Assert.That(ex.PendingResult, Is.Null);
                    }
                    else
                    {
                        Assert.That(ex.PendingResult, Is.Null);
                    }
                    done++;
                }
                else
                {
                    // a spring resolves on arrival
                    Assert.That(ex.PendingResult.Kind, Is.EqualTo(ResultCardKind.Event));
                    Assert.That(ex.PendingResult.Outcome.Accepted, Is.True);
                }
            }
            Assert.That(done, Is.GreaterThan(0), "some seed had an event with a choice");
        }

        [Test]
        public void OutsideTrial_NothingIsPending()
        {
            // js: test/ext/explore-results.test.mjs:186
            var ex = new Expedition(new RunStart { Seed = 4 });
            Win(ex);
            Assert.That(ex.PendingResult, Is.Null);
            Assert.That(ex.CloseResult(), Is.False);
        }

        // ---------------------------------------------------------------- what the result names

        [Test]
        public void Raised_ListsOnlyTheEnemiesWhoseDisclosureRose()
        {
            // js: test/ext/explore-results.test.mjs:195
            var enemies = Trial(4).Session.PendingEncounter.EnemyIds.Distinct().ToList();
            Expedition fresh = Trial(4);
            Win(fresh);
            Assert.That(fresh.PendingResult.Raised.Select(p => p.Key).OrderBy(id => id, StringComparer.Ordinal),
                Is.EqualTo(enemies.OrderBy(id => id, StringComparer.Ordinal)));
            Expedition known = Trial(4, journal: enemies.ToDictionary(id => id, _ => 1));
            Win(known);
            Assert.That(known.PendingResult.Raised, Is.Empty);
        }

        [Test]
        public void PileSpent_WhenTheRewardWantedACardAndThePilesHadNone()
        {
            // js: test/ext/explore-results.test.mjs:206 — the layer-1 stand-in boss always draws from the pile
            // (encounters_and_rewards_v4 §4.2); owning the pile spends it
            Expedition spent = Trial(2, deck: DeckWith(CardPile.Of(1)));
            WalkTo(spent, n => n.Kind == NodeKind.Boss);
            Win(spent);
            Assert.That(spent.PendingResult.PileSpent, Is.True);
            Assert.That(spent.Offer, Is.Empty);
            Assert.That(spent.PendingResult.Back.Reward.Consumables.Count, Is.GreaterThanOrEqualTo(2), "the card came as a consumable");

            Expedition full = Trial(2);
            WalkTo(full, n => n.Kind == NodeKind.Boss);
            Win(full);
            Assert.That(full.PendingResult.PileSpent, Is.False);
            Assert.That(full.Offer.Count, Is.EqualTo(3));
        }

        [Test]
        public void AFullDeck_KeepsLearnedCardsOut_AndTheResultSaysWhich()
        {
            // js: test/ext/explore-results.test.mjs:222 — a deck at its cap (20 of 20): the pile's offer does not fit,
            // a real boss's cards are owned only (§4.3)
            var limits = new DeckLimits(deckMax: 20);
            Expedition ex = Trial(2, limits: limits);
            WalkTo(ex, n => n.Kind == NodeKind.Boss);
            Win(ex);
            Assert.That(ex.DeckMax, Is.EqualTo(20));
            Assert.That(ex.OfferFits, Is.EqualTo(new[] { false, false, false }));
            Assert.That(ex.Learn(ex.Offer[0]), Is.True);
            Assert.That(ex.Deck.Count, Is.EqualTo(20));

            Expedition deep = Trial(2, limits: limits);
            WalkToLayer(deep, 3);
            WalkTo(deep, n => n.Kind == NodeKind.Boss);
            BattleReturn back = Win(deep);
            Assert.That(back.Learned.Count, Is.GreaterThan(0), "the layer-3 boss teaches its cards");
            Assert.That(deep.PendingResult.Benched, Is.EqualTo(back.Learned));
            Assert.That(deep.PendingResult.DeckMax, Is.EqualTo(20));
            Assert.That(deep.Deck.Count, Is.EqualTo(20));
            foreach (string id in back.Learned) Assert.That(deep.Owned.Contains(id), Is.True, id);
        }

        [Test]
        public void ADeckWithRoom_FitsTheOffer_AndBenchesNothing()
        {
            // js: Expedition.js:410 (OfferFits) and :233 (Benched) with the C#'s 40
            Expedition ex = Trial(2);
            WalkTo(ex, n => n.Kind == NodeKind.Boss);
            Win(ex);
            Assert.That(ex.DeckMax, Is.EqualTo(Constants.DeckMax));
            Assert.That(ex.OfferFits, Is.EqualTo(ex.Offer.Select(_ => true)));
            Assert.That(ex.PendingResult.Benched, Is.Empty);
        }

        // ---------------------------------------------------------------- the options

        private static LayerMap MapForKnown(LayerProfile profile, ulong seed)
        {
            var spec = profile.MapSpec;
            if (spec.Quotas.ContainsKey(NodeKind.Carving)) spec = spec.With(NodeKind.Carving, 0);
            return MapGenerator.Generate(spec, seed);
        }

        [Test]
        public void CarvingKnown_TheOptionWins_AndOutsideTrialEachLifeReadsItAgain()
        {
            // js: test/ext/explore-results.test.mjs:274 (the Expedition's options.carvingKnown)
            Assert.That(Trial(5, carvingKnown: true).Session.CarvingTaken, Is.True);
            Assert.That(Trial(5, mapFor: MapForKnown, carvingKnown: false).Session.CarvingTaken, Is.False);
            Assert.That(Trial(5, mapFor: MapForKnown).Session.CarvingTaken, Is.True);
            Assert.That(Trial(5).Session.CarvingTaken, Is.False);
            var plain = new Expedition(new RunStart { Seed = 5, MapFor = MapForKnown });
            Assert.That(plain.Session.CarvingTaken, Is.False);
            var notTrial = new Expedition(new RunStart { Seed = 5 }, options: new ExpeditionOptions { CarvingKnown = true });
            Assert.That(notTrial.Session.CarvingTaken, Is.False, "the option is read in trial mode only");
        }

        [Test]
        public void ATrialOption_OrATrialRunStart_TurnTheSameRulesOn()
        {
            // js: test/ext/world.test.mjs:841
            var byOption = new Expedition(new RunStart { Seed = 1 }, options: new ExpeditionOptions { Trial = true });
            var byStart = new Expedition(new RunStart { Seed = 1, Trial = true });
            foreach (Expedition ex in new[] { byOption, byStart })
            {
                Assert.That(ex.Trial, Is.True);
                Assert.That(ex.Session.Trial, Is.True);
                Assert.That(ex.Session.Keeper, Is.SameAs(ex));
                Assert.That(ex.Session.State.Trial, Is.True);
            }
        }

        [Test]
        public void TheTrialOption_CopiesTheRunStart_LeavingTheCallersAsItWas()
        {
            // js: Expedition.js:60 / :63 (`new RunStart({ ...start, Trial: true })`)
            var start = new RunStart { Seed = 7, Hp = 31, MiasmaPercent = 12, Loadout = RunStart.TrialLoadout() };
            var ex = new Expedition(start, options: new ExpeditionOptions { Trial = true, CarvingKnown = true });
            Assert.That(start.Trial, Is.False);
            Assert.That(start.CarvingKnown, Is.Null);
            Assert.That(ex.Session.Trial, Is.True);
            Assert.That(ex.Session.CarvingTaken, Is.True);
            Assert.That(ex.Session.RunSeed, Is.EqualTo(7UL));
            Assert.That(ex.Session.State.Hp, Is.EqualTo(31));
            Assert.That(ex.Session.State.Loadout, Is.SameAs(start.Loadout));
        }

        [Test]
        public void ByDefault_NothingIsOn_AndTheRunPlaysTheCSharp()
        {
            // js: test/ext/world.test.mjs:110 (the Expedition half)
            Assert.That(new RunStart().Trial, Is.False);
            var ex = new Expedition(new RunStart { Seed = 1, Loadout = RunStart.TrialLoadout() });
            Assert.That(ex.Trial, Is.False);
            Assert.That(ex.Session.Trial, Is.False);
            Assert.That(ex.Session.Keeper, Is.Null);
            Assert.That(ex.Session.State.Trial, Is.False);
            Assert.That(ex.Session.State.TimeLimit, Is.EqualTo(SevenLayers.Of(1).TimeLimit));
            Assert.That(ex.DeckMax, Is.EqualTo(Constants.DeckMax));
            Assert.That(ex.HandIn().Limits, Is.Null);
            BattleReturn back = Win(ex);
            // the battle's consumables stay in Expedition.Consumables, the slots are the C#'s
            Assert.That(ex.Session.State.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "joka_no_ko", "joka_no_ko" }));
            Assert.That(ex.Consumables, Is.EqualTo(back.Reward.Consumables));
            Assert.That(ex.Session.HasPendingFound, Is.False);
            Assert.That(ex.PendingResult, Is.Null);
            Assert.That(ex.InheritedPages, Is.EqualTo(0));
            Assert.That(ex.FoundPages, Is.EqualTo(ex.Pages));
        }

        [Test]
        public void TheLimits_RideTheHandIn_ToTheLaunch()
        {
            // js: test/ext/world.test.mjs:276
            var limits = new DeckLimits(20, 45, 4, 3);
            var ex = new Expedition(new RunStart { Seed = 1 }, options: new ExpeditionOptions { Limits = limits });
            Assert.That(ex.HandIn().Limits, Is.SameAs(limits));
            Assert.That(ex.StartEncounter().NextLaunch().Limits, Is.SameAs(limits));
            var plain = new Expedition(new RunStart { Seed = 1 });
            Assert.That(plain.HandIn().Limits, Is.Null);
            Assert.That(plain.StartEncounter().NextLaunch().Limits, Is.Null);
        }

        [Test]
        public void ASurvivorsDeckPastForty_ReachesTheBattle_UnderTheLifesLimits()
        {
            // js: test/ext/world.test.mjs:286 — concept-v3.md §14 生存者ボーナス: a survivor's deck may hold 45
            var limits = new DeckLimits(20, 45, 4, 3);
            List<CardInstance> deck = DeckOf(41);
            Assert.That(deck.Count, Is.EqualTo(41));
            var ex = new Expedition(new RunStart { Seed = 1 }, deck, options: new ExpeditionOptions { Limits = limits });
            Assert.That(ex.StartEncounter().NextLaunch().BuildDeck().Count, Is.EqualTo(41));
        }

        [Test]
        public void TheLimitsOption_ReachesTheDeckRules()
        {
            // js: test/ext/world.test.mjs:828 — a full deck by the given limits takes no copy; the JS calls the
            // (C#-private) Take, the same rule is reached here through Learn
            for (ulong seed = 1; seed < 100; seed++)
            {
                IReadOnlyList<CardInstance> deck = PrototypeDeck.Build();
                var tight = new DeckLimits(1, deck.Count, 3, 3);
                var ex = new Expedition(new RunStart { Seed = seed }, deck, options: new ExpeditionOptions { Limits = tight });
                Assert.That(ex.Trial, Is.False);
                Win(ex);
                if (ex.Offer.Count == 0) continue;
                string card = ex.Offer[0];
                Assert.That(ex.OfferFits, Has.All.False);
                int before = ex.Deck.Count;
                Assert.That(ex.Learn(card), Is.True);
                Assert.That(ex.Deck.Count, Is.EqualTo(before), "a full deck by the given limits takes no copy");
                Assert.That(ex.Owned.Contains(card), Is.True);
                return;
            }
            Assert.Fail("no seed in 1〜99 dealt a card at the entry battle");
        }

        // ---------------------------------------------------------------- the hand-off (出立する → exploration → battle)

        [Test]
        public void L2_HandOff_ASurvivorsFortyFiveCardsAndFourTools_ReachTheExplorationAndTheLaunch()
        {
            // js: test/ext/limits.test.mjs:440 (DepartureSupplies.Carry is Trial.Bridge's: its result is written out here)
            var limits = new DeckLimits(deckMax: 45, toolSlots: 4);
            List<CardInstance> deck = DeckOf(45);
            Assert.That(deck.Count, Is.EqualTo(45));
            RunLoadout runLoadout = Loadouts.ToRunLoadout(
                Loadout.Of(new[] { Mask, Mask, "maai_no_kutsu", Flute }, new[] { "yakuso" }, limits.ToolSlots), limits.ToolSlots);
            Assert.That(runLoadout.ToolSlotCount, Is.EqualTo(4));
            Assert.That(runLoadout.Tools.Select(t => t.Id), Is.EqualTo(new[] { Mask, Mask, "maai_no_kutsu", Flute }));
            var battleToolIds = new[] { "maai_no_kutsu", Flute };
            Expedition Begin(ExpeditionOptions options) =>
                new Expedition(new RunStart { Seed = 20261020, Loadout = runLoadout }, deck, battleToolIds, deck[0].Def.Id, null, null, options);

            // without the limits the C#'s Expedition refuses the 45 cards: the router must forward them
            var refused = Assert.Throws<ArgumentException>(() => Begin(null));
            Assert.That(refused.Message, Does.Contain("Deck has 45 cards; the maximum is 40."));
            Expedition ex = Begin(new ExpeditionOptions { Limits = limits });
            Assert.That(ex.Deck.Count, Is.EqualTo(45));
            // ... and they ride to the battle: the launch carries them and its deck passes Cards.Validate with them
            Assert.That(ex.Session.HasPendingBattle, Is.True, "the entry node is a battle");
            Depiction.Bridge.BattleLaunch launch = ex.StartEncounter().NextLaunch();
            Assert.That(launch.Limits, Is.SameAs(limits));
            Assert.That(launch.Deck.Count, Is.EqualTo(45));
            Assert.That(Cards.Validate(launch.Deck, launch.Limits).Ok, Is.True);
            Assert.That(Cards.Validate(launch.Deck).Errors, Is.EqualTo(new[] { "Deck has 45 cards; the maximum is 40." }));
        }

        [Test]
        public void L3_HandOff_ASurvivorsFortyFiveCardDeck_StartsItsFirstFight()
        {
            // js: test/ext/limits.test.mjs:466
            var limits = new DeckLimits(deckMax: 45, toolSlots: 4);
            List<CardInstance> deck = DeckOf(45);
            var ex = new Expedition(new RunStart { Seed = 20261020 }, deck, new[] { "maai_no_kutsu" }, null, null, null,
                new ExpeditionOptions { Limits = limits });
            EncounterBattle battle = ex.StartEncounter();
            Assert.DoesNotThrow(() => battle.StartNext(true));
        }

        [Test]
        public void ToRunLoadout_CarriesEveryToolAndConsumable_AsTheKindTheCoreReads()
        {
            // js: test/ext/world.test.mjs:154 (the save's round trip is in TraceGainTests, which reads InheritanceCore's JSON)
            Loadout lo = Loadout.Of(new[] { Mask, Sand, Book, Wick }, new[] { "joka_no_ko", "yakuso", "shirube_no_ishi" }, 4);
            RunLoadout run = Loadouts.ToRunLoadout(lo, 4);
            Assert.That(run.Tools.Select(t => (t.Id, t.Kind, t.Amount)),
                Is.EqualTo(new[] { (Mask, ReliefKind.Density, 1), (Sand, ReliefKind.Time, 1), (Book, ReliefKind.Survey, 1), (Wick, ReliefKind.Interlude, 15) }));
            Assert.That(run.Consumables.Select(t => (t.Id, t.Kind, t.Amount)),
                Is.EqualTo(new[] { ("joka_no_ko", ReliefKind.Gauge, 10), ("yakuso", ReliefKind.Heal, 20), ("shirube_no_ishi", ReliefKind.Teleport, 1) }));
            Assert.That(run.ToolSlotCount, Is.EqualTo(4));
            Assert.That(run.Trial, Is.True);
            RunLoadout battle = Loadouts.ToRunLoadout(Loadout.Of(new[] { "maai_no_kutsu", Flute, "ryurin_no_kakera", "ryugo_no_fu" }, null, 4), 4);
            Assert.That(battle.Tools.All(t => t.Kind == ReliefKind.Battle), Is.True);
            Assert.That(battle.DensityRelief, Is.EqualTo(0));
        }

        [Test]
        public void ToRunLoadout_ReadsTheSlotsFromTheLoadoutWhenNoneAreGiven_AndANullLoadoutIsEmpty()
        {
            // js: bridge/exploration/Loadouts.js:20-23
            RunLoadout own = Loadouts.ToRunLoadout(Loadout.Of(new[] { Mask }, null, 4));
            Assert.That(own.ToolSlotCount, Is.EqualTo(4));
            RunLoadout wider = Loadouts.ToRunLoadout(Loadout.Of(new[] { Mask, Mask, Mask, Mask }, null, 4), 3);
            Assert.That(wider.ToolSlotCount, Is.EqualTo(4), "never fewer slots than tools");
            RunLoadout none = Loadouts.ToRunLoadout(null, 4);
            Assert.That(none.Tools, Is.Empty);
            Assert.That(none.Consumables, Is.Empty);
            Assert.That(none.ToolSlotCount, Is.EqualTo(4));
            Assert.That(none.Trial, Is.True);
            Assert.That(Loadouts.ToRunLoadout(null).ToolSlotCount, Is.EqualTo(RunLoadout.ToolSlots));
        }

        // ---------------------------------------------------------------- finds into the slots, the chest, the survey

        private static ulong SeedWithConsumableReward()
        {
            // js: test/ext/world.test.mjs:582
            for (ulong seed = 1; seed <= 200; seed++)
            {
                if (Win(Trial(seed)).Reward.Consumables.Count > 0) return seed;
            }
            Assert.Fail("no seed drops a consumable at the entry");
            return 0;
        }

        [Test]
        public void ABattlesConsumable_GoesIntoAFreeSlot()
        {
            // js: test/ext/world.test.mjs:590
            ulong seed = SeedWithConsumableReward();
            Expedition ex = Trial(seed, consumables: new[] { "yakuso" });
            BattleReturn back = Win(ex);
            Assert.That(ex.Session.State.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "yakuso" }.Concat(back.Reward.Consumables)));
            Assert.That(ex.Session.HasPendingFound, Is.False);
            Assert.That(ex.Consumables, Is.EqualTo(back.Reward.Consumables), "the C# record of finds is kept");
        }

        [Test]
        public void WithTheSlotsFull_TheFindWaitsOnASwapOrOnGivingUp()
        {
            // js: test/ext/world.test.mjs:599 (the screen's card is TrialSessionTests')
            ulong seed = SeedWithConsumableReward();
            var carried = new[] { "yakuso", "wasuremizu", "koi_hoshiniku" };
            Expedition ex = Trial(seed, consumables: carried);
            ExplorationSession s = ex.Session;
            BattleReturn back = Win(ex);
            string found = back.Reward.Consumables[0];
            Assert.That(s.PendingFound, Is.EqualTo(back.Reward.Consumables));
            Assert.That(s.IsIdle, Is.False);
            Assert.That(s.MapIsLive, Is.False);
            Assert.That(s.Act(ScreenAction.TakeFound, "7"), Is.False, "no such slot");
            Assert.That(s.Act(ScreenAction.TakeFound, "1"), Is.True);
            Assert.That(s.State.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "yakuso", found, "koi_hoshiniku" }));
            Assert.That(s.HasPendingFound, Is.False);
            Assert.That(s.MapIsLive, Is.True);

            Expedition other = Trial(seed, consumables: carried);
            Win(other);
            Assert.That(other.Session.Act(ScreenAction.LeaveFound), Is.True);
            Assert.That(other.Session.State.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(carried));
            Assert.That(other.Session.Act(ScreenAction.LeaveFound), Is.False, "nothing waits any more");
        }

        [Test]
        public void 重い宝箱_Accepted_CostsTwoMaxStamina_AndGivesACardAConsumableAndNoPageOnLayerOne()
        {
            // js: test/ext/world.test.mjs:708 (the Expedition's TakeChest; the screen's card is TrialSessionTests')
            var (seed, node) = SeedWithEvent(EventKind.HeavyChest);
            Expedition ex = Trial(seed);
            ExplorationSession s = ex.Session;
            Win(ex, 50, 9);
            ClearChoices(s);
            WalkTo(ex, n => n.Id == node);
            Assert.That(s.HasPendingEvent, Is.True);
            ExplorationState before = s.State;
            int pages = ex.Pages;
            Assert.That(s.Act(ScreenAction.AcceptEvent), Is.True);
            Assert.That(s.State.TempMaxStaminaMod, Is.EqualTo(before.TempMaxStaminaMod - 2));
            Assert.That(s.State.MaxStamina, Is.EqualTo(before.MaxStamina - 2));
            Assert.That(s.State.Stamina, Is.LessThanOrEqualTo(s.State.MaxStamina), "current stamina follows the lower maximum");
            Assert.That(s.State.TimeLeft, Is.EqualTo(before.TimeLeft), "the 刻限 was paid by the step");
            Assert.That(ex.Offer.Count, Is.EqualTo(3), "the card pile lays out three");
            Assert.That(ex.Pages, Is.EqualTo(pages));
            EventOutcome last = s.Events[s.Events.Count - 1];
            Assert.That(last.Cards, Is.EqualTo(ex.Offer));
            Assert.That(last.Consumables.Count, Is.EqualTo(1));
            Assert.That(s.State.Loadout.Consumables.Any(c => c.Id == last.Consumables[0]), Is.True, "the consumable is in a slot");
            Assert.That(ex.PendingResult.Kind, Is.EqualTo(ResultCardKind.Event));
            string pick = ex.Offer[1];
            Assert.That(ex.Learn(pick), Is.True);
            Assert.That(ex.Owned.Contains(pick), Is.True);
            Assert.That(ex.PendingResult, Is.Null, "the pick closes the card");
        }

        [Test]
        public void 重い宝箱_OnLayerTwo_HoldsACardAndAPage()
        {
            // js: test/ext/world.test.mjs:737
            ulong seed = 1;
            for (; seed <= 200; seed++)
            {
                LayerMap map = SevenLayers.Of(2).Map(ExplorationSession.LayerSeed(seed, 2));
                ulong run = seed;
                if (LayerEvents.Draw(map, 2, id => ExplorationSession.EventSeed(run, 2, id)).Values.Contains(EventKind.HeavyChest)) break;
            }
            Assert.That(seed, Is.LessThanOrEqualTo(200UL));
            Expedition ex = Trial(seed);
            WalkToLayer(ex, 2);
            ExplorationSession s = ex.Session;
            MapNode node = s.State.Map.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Event && s.EventAt(n.Id) == EventKind.HeavyChest);
            Assert.That(node, Is.Not.Null, "the chest is on the map the session walks");
            WalkTo(ex, n => n.Id == node.Id);
            int pages = ex.Pages;
            s.Act(ScreenAction.AcceptEvent);
            Assert.That(ex.Pages, Is.EqualTo(pages + 1));
            Assert.That(ex.FoundPages, Is.EqualTo(ex.Pages), "a chest's page is found, not inherited");
            EventOutcome last = s.Events[s.Events.Count - 1];
            Assert.That(last.Consumables, Is.Empty);
            Assert.That(last.Cards.Count, Is.EqualTo(3));
            Assert.That(last.Record[0], Does.Match("^最大スタミナ \\d+ → \\d+$"));
            Assert.That(last.Record.Skip(1), Is.EqualTo(new[] { "カード 1 枚", "希少な頁 1 枚" }));
        }

        [Test]
        public void TheChest_WhenEveryPileIsSpent_GivesAConsumableForItsCard()
        {
            // js: bridge/exploration/Expedition.js:350-355 (§4.3 「札の代わりに消耗品」)
            var owned = Trial(1, deck: DeckWith(CardPile.Of(1)));
            var contents = LayerEvents.ChestContents(1);
            ChestFind find = owned.TakeChest(1, 5, contents);
            Assert.That(find.Cards, Is.Empty);
            Assert.That(find.PileSpent, Is.True);
            Assert.That(find.Consumables.Count, Is.EqualTo(contents.Consumables + 1));
            Assert.That(owned.Offer, Is.Empty);

            var fresh = Trial(1);
            ChestFind dealt = fresh.TakeChest(1, 5, contents);
            Assert.That(dealt.Cards.Count, Is.EqualTo(CardPile.Choices));
            Assert.That(dealt.PileSpent, Is.False);
            Assert.That(fresh.Offer, Is.EqualTo(dealt.Cards));
            Assert.That(Trial(1).TakeChest(1, 5, contents).Cards, Is.EqualTo(dealt.Cards), "seeded from the node");
        }

        [Test]
        public void 写しの帳_LetsThePlayerPickTheSurveyTarget_AndTheJournalRaisesIt()
        {
            // js: test/ext/world.test.mjs:223 (the Expedition half: the pick reaches the journal through the keeper)
            Expedition ex = Trial(3, tools: new[] { Book });
            ExplorationSession s = ex.Session;
            int survey = WalkTo(ex, n => n.Kind == NodeKind.Survey);
            Assert.That(s.State.Map.Node(survey).Kind, Is.EqualTo(NodeKind.Survey));
            Assert.That(s.HasPendingSurvey, Is.True);
            Assert.That(s.SurveyChoices, Is.EqualTo(ExplorationSession.LayerEnemies(1).Where(id => ex.DisclosureOf(id) < 2)));
            string target = s.SurveyChoices[s.SurveyChoices.Count - 1];
            Assert.That(s.Act(ScreenAction.ChooseSurvey, "no_such_enemy"), Is.False);
            Assert.That(s.Act(ScreenAction.ChooseSurvey, target), Is.True);
            Assert.That(ex.DisclosureOf(target), Is.EqualTo(2));
            Assert.That(s.SurveyTarget, Is.EqualTo(target));
            JournalRow row = s.Screen().Journal.Find("dungeon-1").Rows.First(r => r.Label == "情報収集");
            Assert.That(row.Text, Is.EqualTo($"済ませた。{ex.EnemyName(target)}が開示度 2 になった（写しの帳）"));
        }

        private sealed class SurveyIntel : Exploration.IEnemyIntel, Exploration.ISurveyIntel
        {
            public NodeIntel ForNode(MapNode node, int layer, bool surveyed) => null;

            public IReadOnlyList<JournalPage> Pages(int layer, bool surveyed) => new JournalPage[0];

            public string SurveyTarget(int layer, LayerMap map) => "shadow_hound";
        }

        [Test]
        public void WithoutTheBook_TheIntelsLayerTarget_IsRaisedInTheJournal_InTrialOnly()
        {
            // js: test/ext/world.test.mjs:254
            Expedition Make(bool trial) => new Expedition(new RunStart { Seed = 3 }, null, null, null, new SurveyIntel(), null,
                trial ? new ExpeditionOptions { Trial = true } : null);
            Expedition ex = Make(true);
            WalkTo(ex, n => n.Kind == NodeKind.Survey);
            Assert.That(ex.Session.SurveyTarget, Is.EqualTo("shadow_hound"));
            Assert.That(ex.Session.SurveyPicked, Is.False);
            Assert.That(ex.DisclosureOf("shadow_hound"), Is.EqualTo(2));
            Assert.That(ex.JournalLevels["shadow_hound"], Is.EqualTo(2));
            JournalRow row = ex.Session.Screen().Journal.Find("dungeon-1").Rows.First(r => r.Label == "情報収集");
            Assert.That(row.Text, Is.EqualTo("済ませた。瘴牙の走竜が開示度 2 になった"));

            // the C# session leaves the journal to the intel's reports
            Expedition off = Make(false);
            if (off.Session.HasPendingBattle) Win(off);
            foreach (int node in PathTo(off.Session.State, n => n.Kind == NodeKind.Survey))
            {
                off.Session.Step(node);
                if (off.Session.HasPendingBattle) Win(off);
            }
            Assert.That(off.Session.SurveyTarget, Is.Null);
            Assert.That(off.DisclosureOf("shadow_hound"), Is.LessThan(2));
        }

        [Test]
        public void EnemyName_IsTheCataloguesName_OrTheId()
        {
            // js: bridge/exploration/Expedition.js:456
            Expedition ex = Trial(1);
            Assert.That(ex.EnemyName("shadow_hound"), Is.EqualTo(Enemies.ById("shadow_hound").Name));
            Assert.That(ex.EnemyName("no_such_enemy"), Is.EqualTo("no_such_enemy"));
            Assert.That(ex.EnemyName(null), Is.Null);
        }

        // ---------------------------------------------------------------- X-9: the event's record says what was kept

        /// <summary>
        /// The seed's trial life (three 薬草 carried) walked onto its layer-1 重い宝箱 and accepting it, or null when the
        /// layer has none or the walk does not end on the chest with the slots full (js: explore-results.test.mjs:324).
        /// The same seed makes the same calls, so calling it again is the JS's replay of the recorded log.
        /// </summary>
        private static Expedition ChestWalk(ulong seed)
        {
            Expedition ex = Trial(seed, consumables: new[] { "yakuso", "yakuso", "yakuso" });
            ExplorationSession s = ex.Session;
            MapNode node = s.State.Map.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Event && s.EventAt(n.Id) == EventKind.HeavyChest);
            if (node == null) return null;
            WalkTo(ex, n => n.Id == node.Id);
            if (!s.HasPendingEvent || !s.State.Loadout.Consumables.All(c => c.Id == "yakuso")) return null;
            s.Act(ScreenAction.AcceptEvent);
            return ex;
        }

        /// <summary>A trial life whose layer 1 holds a 重い宝箱 dealing a card, with the three slots full, walked onto the chest (js: explore-results.test.mjs:324).</summary>
        private static Expedition AtAChest() => AtAChest(out _);

        private static Expedition AtAChest(out ulong found)
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                Expedition ex = ChestWalk(seed);
                if (ex == null) continue;
                ExplorationSession s = ex.Session;
                EventOutcome last = s.Events[s.Events.Count - 1];
                if (last.Cards.Count == 0 || !s.HasPendingFound) continue;
                found = seed;
                return ex;
            }
            Assert.Fail("no seed walks onto a chest that deals a card with full slots");
            found = 0;
            return null;
        }

        [Test]
        public void X9_AChestsCardTurnedDown_LeavesTheRecord()
        {
            // js: test/ext/explore-results.test.mjs:342 (the Expedition half: CloseResult drops 「カード 1 枚」)
            Expedition ex = AtAChest();
            ExplorationSession s = ex.Session;
            string found = ExplorationText.FoundConsumable(ExplorationText.ItemName(s.PendingFound[0]));
            Assert.That(LastRecord(s).Skip(1), Is.EqualTo(new[] { ExplorationText.RecordCards(1), found }), "while undecided the finds are listed");
            ex.CloseResult(); // 「受け取らない」
            Assert.That(LastRecord(s).Skip(1), Is.EqualTo(new[] { found }));
            Assert.That(ex.Offer, Is.Empty);
            s.Act(ScreenAction.LeaveFound); // 「諦める」
            Assert.That(LastRecord(s)[0], Does.Match("^最大スタミナ \\d+ → \\d+$"));
            Assert.That(LastRecord(s).Count, Is.EqualTo(1));
            JournalRow row = s.Screen().Journal.Find("dungeon-1").Rows.First(r => r.Label == "出来事");
            Assert.That(row.Text, Does.EndWith("重い宝箱: " + LastRecord(s)[0]));
            Assert.That(s.Events[s.Events.Count - 1].Lines.All(l => LastRecord(s).Contains(l)), Is.False, "the result card still says what the chest held");
        }

        [Test]
        public void X9_AChestsCardLearned_StaysInTheRecord_AndAReplayAgrees()
        {
            // js: test/ext/explore-results.test.mjs:356 (the replay: the same calls on a fresh expedition)
            void Keep(Expedition e)
            {
                Assert.That(e.Learn(e.Offer[0]), Is.True);
                e.CloseResult();
                e.Session.Act(ScreenAction.TakeFound, "0");
            }

            Expedition ex = AtAChest(out ulong seed);
            ExplorationSession s = ex.Session;
            string found = ExplorationText.FoundConsumable(ExplorationText.ItemName(s.PendingFound[0]));
            Keep(ex);
            Assert.That(LastRecord(s).Skip(1), Is.EqualTo(new[] { ExplorationText.RecordCards(1), found }));
            Expedition again = ChestWalk(seed);
            Keep(again);
            Assert.That(LastRecord(again.Session), Is.EqualTo(LastRecord(s)));

            // the other way round, replayed: turned down
            Expedition other = AtAChest(out ulong otherSeed);
            other.CloseResult();
            other.Session.Act(ScreenAction.LeaveFound);
            Expedition third = ChestWalk(otherSeed);
            third.CloseResult();
            third.Session.Act(ScreenAction.LeaveFound);
            Assert.That(LastRecord(third.Session), Is.EqualTo(LastRecord(other.Session)));
            Assert.That(LastRecord(third.Session).Count, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- X-10: a training mark waits on its card

        [Test]
        public void X10_ARestsTraining_WaitsOnItsCard_UntilPutOffOrSpentByTrain_AndAReplayAgrees()
        {
            // js: test/ext/explore-results.test.mjs:409 (the Expedition half: Train spends the last mark; the replay: the
            // same calls on a fresh expedition, up to each point the JS replays its log)
            Expedition Replay(int upTo, string card)
            {
                Expedition again = Trial(3);
                WalkTo(again, n => n.Kind == NodeKind.Rest, RestChoice.Train);
                if (upTo >= 1)
                {
                    again.Session.PutOffTraining();
                    again.Session.PutOffTraining();
                }
                if (upTo >= 2)
                {
                    again.Session.OpenTraining();
                    again.Train(card);
                    again.Session.OpenTraining();
                }
                return again;
            }

            Expedition ex = Trial(3);
            ExplorationSession s = ex.Session;
            Assert.That(s.TrainingWaiting, Is.False);
            WalkTo(ex, n => n.Kind == NodeKind.Rest, RestChoice.Train);
            Assert.That(s.State.TrainingMarks, Is.EqualTo(1));
            Assert.That(s.TrainingWaiting, Is.True, "the card opens on arrival");
            string id = ex.Deck[0].Def.Id;
            Assert.That(Replay(0, id).Session.TrainingWaiting, Is.True, "a reload brings the card back");

            Assert.That(s.PutOffTraining(), Is.True); // 「あとで」
            Assert.That(s.TrainingWaiting, Is.False);
            Assert.That(s.State.TrainingMarks, Is.EqualTo(1), "the mark stays on the band");
            Assert.That(s.PutOffTraining(), Is.False);
            Assert.That(Replay(1, id).Session.TrainingWaiting, Is.False, "put off, it stays put off after a reload");

            Assert.That(s.OpenTraining(), Is.True); // the band's 「訓練の刻み」
            Assert.That(s.TrainingWaiting, Is.True);
            int ticks = ex.Mastery.TicksOf(id);
            Assert.That(ex.Train(id), Is.True);
            Assert.That(s.State.TrainingMarks, Is.EqualTo(0));
            Assert.That(s.TrainingWaiting, Is.False, "no mark left to wait on");
            Assert.That(s.OpenTraining(), Is.False, "nothing to open with no mark");
            Assert.That(ex.Mastery.TicksOf(id), Is.EqualTo(ticks + Constants.TrainingTicks));
            Expedition fourth = Replay(2, id);
            Assert.That(fourth.Session.TrainingWaiting, Is.False);
            Assert.That(fourth.Mastery.TicksOf(id), Is.EqualTo(ex.Mastery.TicksOf(id)));
        }

        // ---------------------------------------------------------------- the pages the heir set out with (R-27)

        [Test]
        public void R27_ThePagesTheHeirSetOutWith_AreTheLifesInherited_NotFound_AndANewLifeCountsThemAgain()
        {
            // js: app/life.js:205-211 (lifeSetup wraps BeginLife), test/app/inherit-gains.test.mjs:55
            Expedition ex = Trial(3, startPages: 4);
            Assert.That(ex.Pages, Is.EqualTo(4));
            Assert.That(ex.InheritedPages, Is.EqualTo(4));
            Assert.That(ex.FoundPages, Is.EqualTo(0), "nothing found yet: 拾った物 lists no page");
            EndEveryFight(ex.StartEncounter(), GameResult.Lost, 0, 0);
            ex.Return();
            Assert.That(ex.Session.Act(ScreenAction.NewLife), Is.True);
            Assert.That(ex.Pages, Is.EqualTo(4), "a new life of the same expedition sets out with them again");
            Assert.That(ex.InheritedPages, Is.EqualTo(4));

            Expedition none = Trial(3);
            Assert.That(none.Pages, Is.EqualTo(0));
            Assert.That(none.InheritedPages, Is.EqualTo(0));

            // js: app/life.js:74 startPages reads anything but a positive count as 0
            Expedition below = Trial(3, startPages: -2);
            Assert.That(below.Pages, Is.EqualTo(0));
            Assert.That(below.InheritedPages, Is.EqualTo(0));
            Assert.That(below.FoundPages, Is.EqualTo(0));
            below.TakeChest(1, 0, new ChestContents(0, 0, 1));
            Assert.That(below.FoundPages, Is.EqualTo(1), "a found page is not hidden by a negative count");
        }

        // ---------------------------------------------------------------- L-5: the C#'s 3 slots on a first life

        [Test]
        public void L5_HandOff_OnAFirstLife_EveryToolInTheCSharpsThreeSlots_ReachesTheExploration()
        {
            // js: test/ext/limits.test.mjs:475 (DepartureSupplies.Carry is Trial.Bridge's: its result is written out here —
            // the loadout keeps the 3 tools, the battle tools also go to the battles; the save's round trip is in
            // TraceGainTests, which reads InheritanceCore's JSON)
            IReadOnlyList<CardInstance> deck = PrototypeDeck.Build();
            Assert.That(deck.Any(c => c.Def.Id == "thrust"), Is.True, "the talent is a card of the deck");
            int slots = DeckLimits.Default.ToolSlots;
            foreach (ItemDef tool in ItemCatalogue.Tools)
            {
                RunLoadout runLoadout = Loadouts.ToRunLoadout(Loadout.Of(new[] { tool.Id, tool.Id, Mask }, new[] { "joka_no_ko" }, slots), slots);
                Assert.That(runLoadout.ToolSlotCount, Is.EqualTo(3), tool.Id);
                Assert.That(runLoadout.Tools.Count, Is.EqualTo(3), tool.Id);
                string[] battleToolIds = tool.Side == EffectSide.Battle ? new[] { tool.Id, tool.Id } : new string[0];
                var ex = new Expedition(new RunStart { Seed = 7, Loadout = runLoadout }, deck, battleToolIds, "thrust");
                Assert.That(ex.Deck.Count, Is.EqualTo(20), tool.Id);
            }
        }

        // ---------------------------------------------------------------- the trace preview's edges (TraceGain.js:30)

        [Test]
        public void InheritPreview_ACardTheCatalogueDoesNotKnow_IsPreviewed_NotThrown()
        {
            // js: bridge/exploration/TraceGain.js:41 — the preview deck holds { Def: { Id } }, so an unknown id still takes
            // a copy's place; its name falls back to the candidate's (cardNameAt gives null)
            Expedition ex = Trial(3);
            int deck = ex.Deck.Count;
            IReadOnlyList<InheritedCard> rows = TraceGain.InheritPreview(ex, new[] { new TraceCard("no_such_card", 1), new TraceCard("no_such_card", 1) });
            Assert.That(rows.Select(r => (r.CardId, r.Rank, r.From, r.To, r.Joins, r.DeckFull, r.Owned)), Is.EqualTo(new[]
            {
                ("no_such_card", 1, 0, 1, true, false, false),
                ("no_such_card", 1, 0, 1, true, false, true),
            }));
            Assert.That(TraceGain.CardNameAt("no_such_card", 1), Is.Null);
            Assert.That(ex.Deck.Count, Is.EqualTo(deck), "a preview changes nothing");
            Assert.That(ex.Owned.Contains("no_such_card"), Is.False);
        }

        [Test]
        public void InheritPreview_WithNoCard_ReadsNoLimits()
        {
            // js: bridge/exploration/TraceGain.js:40 — the JS builds { DeckMax } on each CanAdd, so a list with no card
            // builds none, even for a cap under §8's floor of 20
            var limits = new DeckLimits(deckMin: 1, deckMax: 15);
            var ex = new Expedition(new RunStart { Seed = 3 }, PrototypeDeck.Build().Take(15).ToList(),
                options: new ExpeditionOptions { Limits = limits });
            Assert.That(ex.DeckMax, Is.EqualTo(15));
            Assert.That(TraceGain.InheritPreview(ex, new TraceCard[0]), Is.Empty);
            Assert.That(TraceGain.InheritPreview(ex, null), Is.Empty);
            Assert.That(TraceGain.DeckIsFull(ex), Is.True);
        }
    }
}
