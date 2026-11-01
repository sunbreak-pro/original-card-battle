// #99: the exploration hands a combat node's battle in — the floor, the max stamina 瘴気 leaves, HP,
// stamina, the journal's 開示度, the deck, the tools and the enemy line — and takes its result back:
// HP, stamina, mastery ticks, what the journal learned, the unique cards and death. Runs under
// `dotnet test` (Exploration.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction;
using Depiction.Bridge;
using DungeonContent;
using DungeonCore;
using Journal;
using NUnit.Framework;

namespace Exploration.Bridge.Tests
{
    public class ExpeditionTests
    {
        // ---- handing in ----

        [Test]
        public void TheEntryBattle_HandsInTheExplorationsState()
        {
            var expedition = new Expedition(new RunStart { Seed = 3, Hp = 41, Stamina = 7 });
            ExplorationSession session = expedition.Session;
            Assert.That(session.HasPendingBattle, Is.True, "the entry node is always a battle");

            BattleHandIn handIn = expedition.HandIn();
            Assert.Multiple(() =>
            {
                Assert.That(handIn.Layer, Is.EqualTo(1));
                Assert.That(handIn.Kind, Is.EqualTo(NodeKind.Battle));
                Assert.That(handIn.Encounter, Is.EqualTo(session.PendingEncounter));
                Assert.That(handIn.Hp, Is.EqualTo(41));
                Assert.That(handIn.MaxHp, Is.EqualTo(Constants.PlayerMaxHp));
                Assert.That(handIn.Stamina, Is.EqualTo(session.State.Stamina), "the entry node's 刻限 has already given stamina back");
                Assert.That(handIn.MaxStamina, Is.EqualTo(session.State.MaxStamina));
                Assert.That(handIn.Deck.Count, Is.EqualTo(PrototypeDeck.Kinds.Count * PrototypeDeck.Copies));
                Assert.That(handIn.FieldCells, Is.EqualTo(new[] { BattleField.Cells(1, NodeKind.Battle) }));
                Assert.That(handIn.Disclosure.Values.Distinct(), Is.EqualTo(new[] { 0 }), "no enemy has been met yet");
            });
        }

        [Test]
        public void TheLaunch_CarriesHpStaminaAndTheLine_IntoTheBattle()
        {
            var expedition = new Expedition(new RunStart { Seed = 3, Hp = 41, Stamina = 7 });
            EncounterBattle battle = expedition.StartEncounter();
            CoreBattleSource source = battle.StartNext();

            CombatantState player = source.State.Player;
            Assert.Multiple(() =>
            {
                Assert.That(player.Hp, Is.EqualTo(41));
                Assert.That(player.MaxHp, Is.EqualTo(Constants.PlayerMaxHp));
                Assert.That(player.MaxStamina, Is.EqualTo(expedition.Session.State.MaxStamina));
                Assert.That(player.Stamina, Is.EqualTo(Math.Min(expedition.Session.State.Stamina, player.MaxStamina)));
                Assert.That(source.State.FieldCells, Is.EqualTo(6));
                Assert.That(source.State.EnemyDef.Id, Is.EqualTo(battle.HandIn.Encounter.Fights[0].EnemyIds[0]));
            });
        }

        [Test]
        public void Miasma_LowersTheMaxStamina_TheBattleStartsFrom()
        {
            // concept-v3 §6: one point per full 20%. 45% is two points down from 10.
            var expedition = new Expedition(new RunStart { Seed = 3, MiasmaPercent = 45 });
            BattleLaunch launch = expedition.StartEncounter().NextLaunch();
            Assert.That(launch.MaxStamina, Is.EqualTo(8));
            Assert.That(launch.CreateSource().State.Player.MaxStamina, Is.EqualTo(8));
        }

        [Test]
        public void TheInBattleTools_ReachTheBattle_AsAmounts()
        {
            var tools = new[] { "maai_no_kutsu", "ikitsugi_no_fue", "ryurin_no_kakera" };
            var expedition = new Expedition(new RunStart { Seed = 3 }, battleToolIds: tools);
            BattleLaunch launch = expedition.StartEncounter().NextLaunch();
            Assert.Multiple(() =>
            {
                Assert.That(launch.PlayerStartCell, Is.EqualTo(Constants.PlayerStartCell - ItemCatalogue.Tool("maai_no_kutsu").Amount));
                Assert.That(launch.StartGap, Is.EqualTo(Constants.StartGap + ItemCatalogue.Tool("maai_no_kutsu").Amount));
                Assert.That(launch.StartStaminaBonus, Is.EqualTo(ItemCatalogue.Tool("ikitsugi_no_fue").Amount));
                Assert.That(launch.FirstHitGuard, Is.EqualTo(ItemCatalogue.Tool("ryurin_no_kakera").Amount));
                Assert.That(launch.BossWordWard, Is.EqualTo(0));
            });
            Assert.That(launch.CreateSource().State.Player.Cell, Is.EqualTo(1));
        }

        [Test]
        public void AToolThatActsOnTheExploration_IsNotABattleTool()
        {
            Assert.Throws<ArgumentException>(() => new Expedition(battleToolIds: new[] { "bosho_no_men" }));
        }

        [Test]
        public void TheJournalsLevel_ReachesTheOmen_ThroughTheLaunchsDisclosure()
        {
            var probe = new Expedition(new RunStart { Seed = 3 });
            string enemy = probe.Session.PendingEncounter.Fights[0].EnemyIds[0];

            var expedition = new Expedition(new RunStart { Seed = 3 }, journal: new Dictionary<string, int> { [enemy] = 1 });
            BattleLaunch launch = expedition.StartEncounter().NextLaunch();
            Assert.That(launch.Disclosure, Is.EqualTo(1));
            Assert.That(launch.CreateSource().Frame.Omen.Disclosure, Is.EqualTo(1));
        }

        // ---- bringing back ----

        [Test]
        public void AWonBattle_BringsHpAndStaminaBack_AndTheJournalReadsOne()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            EncounterBattle battle = expedition.StartEncounter();
            string enemy = battle.HandIn.Encounter.Fights[0].EnemyIds[0];

            EndEveryFight(battle, GameResult.Won, hp: 33, stamina: 4);
            BattleReturn back = expedition.Return();

            Assert.Multiple(() =>
            {
                Assert.That(back.Won, Is.True);
                Assert.That(back.Died, Is.False);
                Assert.That(expedition.Session.State.Hp, Is.EqualTo(33));
                Assert.That(expedition.Session.State.Stamina, Is.EqualTo(4));
                Assert.That(expedition.Session.HasPendingBattle, Is.False);
                Assert.That(expedition.Session.PendingEncounter, Is.Null);
                Assert.That(expedition.Session.State.Phase, Is.EqualTo(RunPhase.Exploring));
                Assert.That(expedition.DisclosureOf(enemy), Is.EqualTo(1));
                Assert.That(back.Disclosure[enemy], Is.EqualTo(1));
                Assert.That(back.Observed, Is.Empty);
            });
        }

        [Test]
        public void AnObservedEnemy_ReadsTwo_OnceTheBattleEnds()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            EncounterBattle battle = expedition.StartEncounter();
            string enemy = battle.HandIn.Encounter.Fights[0].EnemyIds[0];

            EndEveryFight(battle, GameResult.Won, observe: true);
            BattleReturn back = expedition.Return();

            Assert.That(expedition.DisclosureOf(enemy), Is.EqualTo(2));
            Assert.That(back.Observed, Is.EqualTo(new[] { enemy }));
        }

        [Test]
        public void TheMasteryTicks_GoToTheBook_WhenTheBattleEnds()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 }, talent: "thrust");
            EncounterBattle battle = expedition.StartEncounter();
            var plays = new[]
            {
                new MasteryPlay("thrust", MasteryContext.Gap | MasteryContext.Omen),
                new MasteryPlay("brace", MasteryContext.Trait),
            };

            EndEveryFight(battle, GameResult.Won, plays: plays);
            BattleReturn back = expedition.Return();

            Assert.Multiple(() =>
            {
                Assert.That(back.Ticks.TicksOf("thrust"), Is.EqualTo(2));
                Assert.That(back.Ticks.TicksOf("brace"), Is.EqualTo(1));
                Assert.That(expedition.Mastery.TicksOf("thrust"), Is.EqualTo(2 * Constants.TalentTickMultiplier), "才能 doubles the talent card");
                Assert.That(expedition.Mastery.TicksOf("brace"), Is.EqualTo(1));
            });
        }

        [Test]
        public void ALostBattle_EndsTheLife()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            EncounterBattle battle = expedition.StartEncounter();

            EndEveryFight(battle, GameResult.Lost, hp: 0);
            BattleReturn back = expedition.Return();

            Assert.That(back.Died, Is.True);
            Assert.That(back.Won, Is.False);
            Assert.That(expedition.Session.State.Phase, Is.EqualTo(RunPhase.Fallen));
            Assert.That(expedition.Session.State.Hp, Is.EqualTo(0));
        }

        [Test]
        public void ARealBattle_FoughtToTheEnd_ComesBack()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            EncounterBattle battle = expedition.StartEncounter();
            while (!battle.Over)
            {
                CoreBattleSource source = battle.StartNext(suggestCards: true);
                Drive(source);
                battle.Finish(source);
            }
            BattleReturn back = expedition.Return();

            Assert.That(back.Hp, Is.EqualTo(expedition.Session.State.Hp));
            Assert.That(back.Died, Is.EqualTo(expedition.Session.State.Phase == RunPhase.Fallen));
            Assert.That(back.Ticks.Ticks.Values.Sum(), Is.GreaterThan(0), "playing cards gives ticks");
        }

        [Test]
        public void ANewLife_StartsFromWhatTheExpeditionSetOutWith()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            EncounterBattle battle = expedition.StartEncounter();
            string enemy = battle.HandIn.Encounter.Fights[0].EnemyIds[0];
            EndEveryFight(battle, GameResult.Lost, hp: 0, plays: new[] { new MasteryPlay("thrust", MasteryContext.Gap) });
            expedition.Return();

            Assert.That(expedition.Session.Act(ScreenAction.NewLife), Is.True);
            Assert.That(expedition.DisclosureOf(enemy), Is.EqualTo(0));
            Assert.That(expedition.Mastery.TicksOf("thrust"), Is.EqualTo(0));
            Assert.That(expedition.HandIn(), Is.Not.Null, "the new life's entry battle waits");
        }

        // ---- a node of two fights, and two enemies on one line ----

        [Test]
        public void TwoFightsBackToBack_CarryHpAndStamina()
        {
            var encounter = new Encounter(new[] { new Fight(new[] { "polearm_warped" }), new Fight(new[] { "shadow_hound" }) }, isStandIn: true);
            var battle = new EncounterBattle(HandIn(encounter, NodeKind.Elite));

            CoreBattleSource first = battle.StartNext();
            battle.Finish(Ended(first, GameResult.Won, 27, 3), first.History);
            Assert.That(battle.Over, Is.False);

            BattleLaunch second = battle.NextLaunch();
            Assert.Multiple(() =>
            {
                Assert.That(second.EnemyId, Is.EqualTo("shadow_hound"));
                Assert.That(second.StartHp, Is.EqualTo(27));
                Assert.That(second.StartStamina, Is.EqualTo(3));
                Assert.That(second.ChainIndex, Is.EqualTo(2));
                Assert.That(second.ChainTotal, Is.EqualTo(2));
                Assert.That(second.Seed, Is.Not.EqualTo(battle.HandIn.Seed), "each fight is dealt afresh");
            });
        }

        [Test]
        public void ALostFirstFight_EndsTheEncounter()
        {
            var encounter = new Encounter(new[] { new Fight(new[] { "polearm_warped" }), new Fight(new[] { "shadow_hound" }) }, isStandIn: true);
            var battle = new EncounterBattle(HandIn(encounter, NodeKind.Elite));
            CoreBattleSource first = battle.StartNext();
            battle.Finish(Ended(first, GameResult.Lost, 0, 0), first.History);

            Assert.That(battle.Over, Is.True);
            Assert.That(battle.Lost, Is.True);
            Assert.That(battle.HpAfter, Is.EqualTo(0));
            Assert.Throws<InvalidOperationException>(() => battle.NextLaunch());
        }

        [Test]
        public void ThePair_StandsOnOneLine_AndTheOmenReadsTheLowerLevel()
        {
            var encounter = new Encounter(new[] { new Fight(new[] { "polearm_warped", "crossbow_hunter" }) }, isStandIn: true);
            var levels = new Dictionary<string, int> { ["polearm_warped"] = 2, ["crossbow_hunter"] = 1 };
            var battle = new EncounterBattle(HandIn(encounter, NodeKind.Elite, levels, cells: 7));

            BattleLaunch launch = battle.NextLaunch();
            Assert.That(launch.MoreEnemyIds, Is.EqualTo(new[] { "crossbow_hunter" }));
            Assert.That(launch.Disclosure, Is.EqualTo(1));

            BattleState state = launch.CreateSource().State;
            Assert.That(state.Enemies.Select(e => e.Def.Id), Is.EqualTo(new[] { "polearm_warped", "crossbow_hunter" }));
            Assert.That(state.FieldCells, Is.EqualTo(7));
        }

        // ---- unique cards ----

        [Test]
        public void ARealBoss_TeachesItsThreeCards_IntoTheDeck()
        {
            var expedition = new Expedition(new RunStart { Seed = 5 });
            WalkToLayer(expedition, 3);
            WalkToBossAndWin(expedition, out BattleReturn back);

            var cards = UniqueCards.OfBoss("miasma_priest");
            Assert.That(back.Learned, Is.EqualTo(cards));
            Assert.That(cards.All(expedition.Owned.Contains), Is.True);
            Assert.That(cards.All(id => expedition.Deck.Any(c => c.Def.Id == id)), Is.True, "a deck of 20 has room");
            Assert.That(expedition.Session.State.Phase, Is.EqualTo(RunPhase.LayerCleared));
        }

        [Test]
        public void ANormalBattle_TeachesNoUniqueCard()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            EndEveryFight(expedition.StartEncounter(), GameResult.Won);
            BattleReturn back = expedition.Return();
            Assert.That(back.Learned, Is.Empty);
            Assert.That(back.Offered, Is.Empty);
        }

        [Test]
        public void TheRewardTable_FollowsTheNodeAndWhetherTheEnemyIsAStandIn()
        {
            var none = new HashSet<string>();
            Assert.Multiple(() =>
            {
                Assert.That(UniqueCards.For(NodeKind.Boss, Single("abyss_angler", false), none).Learned, Is.EqualTo(UniqueCards.OfBoss("abyss_angler")));
                Assert.That(UniqueCards.For(NodeKind.Boss, Single("armored_warden", true), none).Learned, Is.EqualTo(UniqueCards.OfElite("armored_warden")),
                    "layer 4's stand-in boss teaches both of its cards");
                Assert.That(UniqueCards.For(NodeKind.Boss, Single("shadow_hound", true), none).IsEmpty, Is.True, "layer 1's stand-in boss draws from the pile");
                Assert.That(UniqueCards.For(NodeKind.Elite, Single("pack_alpha", false), none).Offered, Is.EqualTo(UniqueCards.OfElite("pack_alpha")));
                Assert.That(UniqueCards.For(NodeKind.Elite, Single("pack_alpha", false), none).Learned, Is.Empty);
                Assert.That(UniqueCards.For(NodeKind.Battle, Single("polearm_warped", false), none).IsEmpty, Is.True);
            });
        }

        [Test]
        public void ACardAlreadyOwned_IsNotTaughtAgain()
        {
            var owned = new HashSet<string> { CardCatalog.FangRush.Id };
            UniqueCardReward reward = UniqueCards.For(NodeKind.Elite, Single("pack_alpha", false), owned);
            Assert.That(reward.Offered, Is.EqualTo(new[] { CardCatalog.PackHowl.Id }));
        }

        [Test]
        public void EveryCardTheTableNames_IsALearnedCard()
        {
            var learned = CardCatalog.All.Skip(40).Select(d => d.Id).ToList();
            var named = new[] { "miasma_priest", "abyss_angler", "distortion_root" }.SelectMany(UniqueCards.OfBoss)
                .Concat(new[] { "armored_warden", "pack_alpha", "polearm_unyielding" }.SelectMany(UniqueCards.OfElite))
                .ToList();
            Assert.That(named.Count, Is.EqualTo(15));
            Assert.That(named.All(learned.Contains), Is.True);
        }

        // ---- the rest of the reward and the picks (#625) ----

        [Test]
        public void AWonNormalBattle_BringsOneOfCardConsumableOrPage_Back()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            BattleReturn back = WinPending(expedition);
            BattleReward reward = back.Reward;
            int parts = (reward.CardChoices.Count > 0 ? 1 : 0) + reward.Consumables.Count + reward.Pages;
            Assert.That(parts, Is.EqualTo(1), "§4.2: one of the three");
            Assert.That(expedition.Offer, Is.EqualTo(reward.CardChoices));
            Assert.That(expedition.Consumables, Is.EqualTo(reward.Consumables));
            Assert.That(expedition.Pages, Is.EqualTo(reward.Pages));
        }

        [Test]
        public void TheReward_IsDrawnFromTheExplorationsSeed()
        {
            BattleReturn a = WinPending(new Expedition(new RunStart { Seed = 8 }));
            BattleReturn b = WinPending(new Expedition(new RunStart { Seed = 8 }));
            Assert.That(b.Reward.CardChoices, Is.EqualTo(a.Reward.CardChoices));
            Assert.That(b.Reward.Consumables, Is.EqualTo(a.Reward.Consumables));
            Assert.That(b.Reward.Pages, Is.EqualTo(a.Reward.Pages));
        }

        [Test]
        public void ALostBattle_BringsNoReward()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            EndEveryFight(expedition.StartEncounter(), GameResult.Lost, hp: 0);
            BattleReturn back = expedition.Return();
            Assert.That(back.Reward.IsEmpty, Is.True);
            Assert.That(expedition.Offer, Is.Empty);
        }

        [Test]
        public void APileCard_PickedWithLearn_IsOwnedAndInTheDeck()
        {
            Expedition expedition = WinUntilACardIsOffered();
            string card = expedition.Offer[0];
            Assert.That(CardPile.Of(1).Contains(card), Is.True, "layer 1 deals its own pile");
            Assert.That(expedition.Learn(card), Is.True);
            Assert.That(expedition.Owned.Contains(card), Is.True);
            Assert.That(expedition.Deck.Any(c => c.Def.Id == card), Is.True);
            Assert.That(expedition.Offer, Is.Empty, "one card of the three");
        }

        [Test]
        public void ARealElitesCard_PickedWithLearn_IsOwnedAndInTheDeck()
        {
            // Layer 7's elite is always 統牙の長竜, and no boss before it teaches its cards.
            var expedition = new Expedition(new RunStart { Seed = 5 });
            WalkToLayer(expedition, 7);
            BattleReturn back = WalkToAndWin(expedition, NodeKind.Elite);

            Assert.That(back.Offered, Is.EqualTo(UniqueCards.OfElite("pack_alpha")), "a real elite offers its two cards");
            Assert.That(expedition.Offer, Is.EqualTo(back.Offered));
            Assert.That(back.Reward.CardChoices, Is.Empty, "the elite's cards are the card");
            Assert.That(back.Reward.Consumables.Count, Is.EqualTo(1), "§4.2: an elite always drops a consumable");

            string card = back.Offered[1];
            Assert.That(expedition.Learn(card), Is.True);
            Assert.That(expedition.Owned.Contains(card), Is.True);
            Assert.That(expedition.Deck.Any(c => c.Def.Id == card), Is.True);
            Assert.That(expedition.Owned.Contains(back.Offered[0]), Is.False, "one of the two");
            Assert.That(expedition.Learn(back.Offered[0]), Is.False, "the offer is spent");
        }

        [Test]
        public void AnEliteWhoseCardsAreOwned_DealsThreeCardsFromThePileInstead()
        {
            // Seed 5 draws 鉄壁の門竜 for layer 5's elite. Layer 4's stand-in boss is the same 門竜, and
            // beating it taught both of its cards (§4.2), so the elite has nothing left to offer.
            var expedition = new Expedition(new RunStart { Seed = 5 });
            WalkToLayer(expedition, 5);
            Assert.That(UniqueCards.OfElite("armored_warden").All(expedition.Owned.Contains), Is.True);
            BattleReturn back = WalkToAndWin(expedition, NodeKind.Elite);

            Assert.That(back.Offered, Is.Empty);
            Assert.That(back.Reward.CardChoices.Count, Is.EqualTo(CardPile.Choices));
            Assert.That(back.Reward.CardChoices.All(CardPile.Of(5).Concat(CardPile.Of(4)).Contains), Is.True, "layer 5's pile first, then layer 4's");
            Assert.That(expedition.Offer, Is.EqualTo(back.Reward.CardChoices));
        }

        [Test]
        public void ATrainingMark_SpentOnACard_GoesThroughTrainIntoTheMasteryBook()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            ExplorationSession session = expedition.Session;
            WinPending(expedition);
            WalkTo(expedition, NodeKind.Rest, RestChoice.Train);
            Assert.That(session.State.TrainingMarks, Is.EqualTo(1));

            string card = CardCatalog.Thrust.Id;
            int before = expedition.Mastery.TicksOf(card);
            Assert.That(expedition.Train(card), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(expedition.Mastery.TicksOf(card), Is.EqualTo(before + Constants.TrainingTicks));
                Assert.That(session.State.TrainingMarks, Is.EqualTo(0), "the mark is spent");
                Assert.That(expedition.Train(card), Is.False, "no mark left");
            });
        }

        [Test]
        public void ATrainingMark_IsNotSpentOnACardNotOwned()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            WinPending(expedition);
            WalkTo(expedition, NodeKind.Rest, RestChoice.Train);
            Assert.That(expedition.Train(CardCatalog.LastStand.Id), Is.False, "#80 is not owned at the start");
            Assert.That(expedition.Session.State.TrainingMarks, Is.EqualTo(1));
        }

        // ---- the pages (#107, dungeon_exploration_v4.md §6.11 / encounters_and_rewards_v4.md §4.3) ----

        [Test]
        public void ARealBossesPage_TellsOneEntryOfItsLayerInFull()
        {
            // Layer 3's boss is a real boss: its three cards and a rare page, always (§4.2).
            var expedition = new Expedition(new RunStart { Seed = 3 });
            WalkToLayer(expedition, 3);
            int before = expedition.Pages;
            WalkToBossAndWin(expedition, out BattleReturn atBoss);

            Assert.That(atBoss.PagesRead.Count, Is.EqualTo(1), "the layer's pile still holds entries");
            string told = atBoss.PagesRead[0];
            Assert.Multiple(() =>
            {
                Assert.That(DungeonJournal.Find(told).Layer, Is.EqualTo(3), "a page tells an entry of its own layer");
                Assert.That(expedition.Journal.EntryLevel(told), Is.EqualTo(DungeonJournal.FullDisclosure));
                Assert.That(atBoss.PagesTurned, Is.Zero);
                Assert.That(atBoss.Reward.Pages, Is.EqualTo(1));
                Assert.That(expedition.Pages, Is.GreaterThan(before));
                Assert.That(expedition.Journal.DungeonRows(3).Any(r => r.Text != JournalRow.Hidden), Is.True, "the row no longer reads 「？」");
                Assert.That(expedition.Journal.LegacyLines().Any(l => l.PageId == DungeonJournal.PageId(3)), Is.True,
                    "the layer's dungeon page joins the legacy list");
            });
        }

        [Test]
        public void APageOfALayerWithNothingToTell_TurnsIntoTheCardPilesThree()
        {
            // Layer 4 holds no dungeon entry (§6.11), and its stand-in boss drops a page always (§4.2).
            var expedition = new Expedition(new RunStart { Seed = 3 });
            WalkToLayer(expedition, 4);
            Assert.That(expedition.Journal.RarePile(4), Is.Empty);
            int before = expedition.Pages;
            WalkToBossAndWin(expedition, out BattleReturn atBoss);

            Assert.Multiple(() =>
            {
                Assert.That(atBoss.PagesRead, Is.Empty);
                Assert.That(atBoss.PagesTurned, Is.EqualTo(1));
                Assert.That(atBoss.Reward.Pages, Is.Zero, "the page became a card");
                Assert.That(atBoss.Reward.CardChoices.Count, Is.EqualTo(CardPile.Choices));
                Assert.That(atBoss.Reward.CardChoices.All(CardPile.Of(4).Concat(CardPile.Of(3)).Contains), Is.True);
                Assert.That(expedition.Offer, Is.EqualTo(atBoss.Reward.CardChoices));
                Assert.That(expedition.Pages, Is.EqualTo(before));
            });
        }

        [Test]
        public void ATurnedPage_WaitsBehindTheElitesPick_AndIsDealtAfterIt()
        {
            // Seed 1, taking each layer's elite before its boss: layer 5's elite is a real one whose two
            // cards are not owned, and its 40% page lands on a layer with nothing to tell.
            var expedition = new Expedition(new RunStart { Seed = 1 });
            for (int layer = 1; layer <= 4; layer++)
            {
                WalkToAndWin(expedition, NodeKind.Elite);
                WalkToBossAndWin(expedition, out _);
                Assert.That(expedition.Session.Act(ScreenAction.GoToInterlude), Is.True);
                Assert.That(expedition.Session.Act(ScreenAction.Descend), Is.True);
            }
            BattleReturn elite = WalkToAndWin(expedition, NodeKind.Elite);

            Assert.That(elite.Offered.Count, Is.EqualTo(2), "a real elite offers its two cards");
            Assert.That(elite.PagesTurned, Is.EqualTo(1));
            Assert.That(elite.Reward.CardChoices, Is.Empty, "the turned card waits behind the elite's pick");
            Assert.That(expedition.Offer, Is.EqualTo(elite.Offered));

            string picked = elite.Offered[0];
            Assert.That(expedition.Learn(picked), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(expedition.Offer.Count, Is.EqualTo(CardPile.Choices), "the pile's three come next");
                Assert.That(expedition.Offer.All(CardPile.Of(5).Concat(CardPile.Of(4)).Concat(CardPile.Of(3)).Contains), Is.True);
                Assert.That(expedition.Offer, Has.No.Member(picked));
            });
            Assert.That(expedition.Learn(expedition.Offer[0]), Is.True);
            Assert.That(expedition.Offer, Is.Empty, "one card for one page");
        }

        // ---- helpers ----

        private static Encounter Single(string id, bool standIn) => new Encounter(new[] { new Fight(new[] { id }) }, standIn);

        private static BattleHandIn HandIn(Encounter encounter, NodeKind kind, IReadOnlyDictionary<string, int> levels = null, int cells = 7)
        {
            var deck = PrototypeDeck.Build();
            return new BattleHandIn(
                2, kind, encounter, encounter.Fights.Select(_ => cells).ToList(),
                Constants.PlayerMaxHp, Constants.PlayerMaxHp, Constants.BaseMaxStamina, Constants.BaseMaxStamina, 0,
                levels ?? encounter.EnemyIds.Distinct().ToDictionary(id => id, _ => 0),
                deck, Departure.Plain(deck), 11);
        }

        /// <summary>The source's state as a battle that ended this way: the end the core would report, without playing it out.</summary>
        private static BattleState Ended(CoreBattleSource source, GameResult result, int hp, int stamina, IEnumerable<MasteryPlay> plays = null)
        {
            BattleState state = source.State;
            BattleHistory history = state.History;
            foreach (MasteryPlay play in plays ?? Array.Empty<MasteryPlay>()) history = history.WithMasteryPlay(play);
            return state with { Result = result, Player = state.Player with { Hp = hp, Stamina = stamina }, PlayerHistory = history };
        }

        /// <summary>Ends every fight of the encounter this way. -1 keeps the HP and stamina the fight started with.</summary>
        private static void EndEveryFight(EncounterBattle battle, GameResult result, int hp = -1, int stamina = -1, bool observe = false,
            IEnumerable<MasteryPlay> plays = null)
        {
            while (!battle.Over)
            {
                CoreBattleSource source = battle.StartNext();
                CombatantState player = source.State.Player;
                var events = new List<BattleEvent>(source.History);
                if (observe) events.Add(new CardPlayed(Actor.Player, new CardInstance("observe-0", CardCatalog.Observe), 3));
                battle.Finish(Ended(source, result, hp < 0 ? player.Hp : hp, stamina < 0 ? player.Stamina : stamina, plays), events);
            }
        }

        /// <summary>Plays the source's suggested cards until the battle ends, as the unattended runs do.</summary>
        private static void Drive(CoreBattleSource source)
        {
            int guard = 0;
            while (!source.Finished && guard++ < 2000)
            {
                if (!source.WaitingForPlayer)
                {
                    source.AdvanceAuto();
                    continue;
                }
                string card = source.SuggestedCardId;
                if (card.Length == 0)
                {
                    source.EndTurn();
                    continue;
                }
                CardFace face = DepictionText.Find(source.Frame.Hand, card);
                Assert.That(source.TryPlay(card, DepictionText.RequiredZone(face.Aim), out _), Is.EqualTo(PlayVerdict.Accepted));
            }
            Assert.That(source.Finished, Is.True);
        }

        /// <summary>Wins whatever battle waits, keeping full HP so the walk never falls.</summary>
        private static BattleReturn WinPending(Expedition expedition)
        {
            EncounterBattle battle = expedition.StartEncounter();
            Assert.That(battle, Is.Not.Null);
            EndEveryFight(battle, GameResult.Won, hp: Constants.PlayerMaxHp, stamina: 5);
            return expedition.Return();
        }

        /// <summary>Walks the shortest way to the layer's boss and wins every battle on the way.</summary>
        private static void WalkToBossAndWin(Expedition expedition, out BattleReturn atBoss)
        {
            ExplorationSession session = expedition.Session;
            if (session.HasPendingBattle) WinPending(expedition);
            atBoss = null;
            foreach (int node in PathToBoss(session.State))
            {
                Assert.That(session.Step(node), Is.True, $"step to {node}");
                if (session.HasPendingStep) Assert.That(session.Act(ScreenAction.ConfirmStep), Is.True);
                if (!session.HasPendingBattle) continue;
                BattleReturn back = WinPending(expedition);
                if (session.State.Map.Node(node).Kind == NodeKind.Boss) atBoss = back;
            }
            Assert.That(atBoss, Is.Not.Null, "the walk reached the boss");
        }

        private static void WalkToLayer(Expedition expedition, int layer)
        {
            ExplorationSession session = expedition.Session;
            while (session.State.Profile.Layer < layer)
            {
                WalkToBossAndWin(expedition, out _);
                Assert.That(session.Act(ScreenAction.GoToInterlude), Is.True);
                Assert.That(session.Act(ScreenAction.Descend), Is.True);
                Assert.That(session.ConfirmingDescend, Is.False, "the descent does not end the life");
            }
        }

        /// <summary>The first seed whose entry battle, won, lays out the card pile to pick from.</summary>
        private static Expedition WinUntilACardIsOffered()
        {
            for (ulong seed = 1; seed < 100; seed++)
            {
                var expedition = new Expedition(new RunStart { Seed = seed });
                if (WinPending(expedition).Reward.CardChoices.Count > 0) return expedition;
            }
            Assert.Fail("no seed in 1〜99 dealt a card at the entry battle");
            return null;
        }

        /// <summary>
        /// Walks the shortest way to the nearest unresolved node of this kind, winning every battle on
        /// the way, and steps onto it with the choice given. The battle fought there, or null.
        /// </summary>
        private static BattleReturn WalkToAndWin(Expedition expedition, NodeKind kind, RestChoice choice = RestChoice.Rest)
        {
            ExplorationSession session = expedition.Session;
            if (session.HasPendingBattle) WinPending(expedition);
            IReadOnlyList<int> path = PathTo(session.State, kind);
            BattleReturn there = null;
            foreach (int node in path)
            {
                bool last = node == path[path.Count - 1];
                Assert.That(session.Step(node, last ? choice : RestChoice.Rest), Is.True, $"step to {node}");
                if (session.HasPendingStep) Assert.That(session.Act(ScreenAction.ConfirmStep), Is.True);
                if (!session.HasPendingBattle) continue;
                BattleReturn back = WinPending(expedition);
                if (last) there = back;
            }
            return there;
        }

        private static void WalkTo(Expedition expedition, NodeKind kind, RestChoice choice) => WalkToAndWin(expedition, kind, choice);

        private static IReadOnlyList<int> PathToBoss(ExplorationState state) => PathTo(state, NodeKind.Boss);

        /// <summary>Breadth-first over the map: the nodes from where the player stands to the nearest unresolved node of this kind, that node last.</summary>
        private static IReadOnlyList<int> PathTo(ExplorationState state, NodeKind kind)
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
