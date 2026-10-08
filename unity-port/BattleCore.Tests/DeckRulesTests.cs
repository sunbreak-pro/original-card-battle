using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// 出立 (#58): the deck of 20〜40 owned cards with three of a kind at most, the three tool and
    /// three consumable slots, the start a tool moves back (§7.3), and the talent card recorded.
    /// </summary>
    public class DeckRulesTests
    {
        private static readonly IReadOnlyCollection<string> AllOwned = CardCatalog.All.Select(d => d.Id).ToList();

        /// <summary>The first <paramref name="kinds"/> kinds of the catalog × <paramref name="copies"/>.</summary>
        private static List<CardInstance> Deck(int kinds, int copies) =>
            Cards.BuildDeck(CardCatalog.All.Take(kinds).ToList(), copies);

        private static Departure Ready(List<CardInstance>? deck = null, int cellsBack = 0) =>
            new Departure(deck ?? Deck(10, 2), new[] { "maai_no_kutsu" }, new[] { "yakuso" }, cellsBack, CardCatalog.All[0].Id);

        // ---- the deck ----

        [Test]
        public void NineteenCardsCannotSetOut()
        {
            var nineteen = Deck(10, 2).Take(19).ToList();

            var result = DeckRules.Validate(Ready(nineteen), AllOwned);

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("minimum is 20"));
        }

        [Test]
        public void TwentyAndFortyCardsSetOut_FortyOneDoNot()
        {
            Assert.That(DeckRules.Validate(Ready(Deck(10, 2)), AllOwned).Ok, Is.True, "20");
            Assert.That(DeckRules.Validate(Ready(Deck(20, 2)), AllOwned).Ok, Is.True, "40");
            var fortyOne = Deck(20, 2).Concat(Cards.BuildDeck(new[] { CardCatalog.All[30] }, 1)).ToList();
            Assert.That(DeckRules.Validate(Ready(fortyOne), AllOwned).Ok, Is.False, "41");
        }

        [Test]
        public void AFourthCopyDoesNotGoIn()
        {
            var deck = Deck(7, 3); // 21 cards, three of every kind
            string id = deck[0].Def.Id;

            Assert.That(DeckRules.CanAdd(deck, id, AllOwned), Is.False, "the fourth copy");
            Assert.That(DeckRules.CanAdd(deck, CardCatalog.All[7].Id, AllOwned), Is.True, "a new kind");

            var four = deck.Concat(new[] { new CardInstance(id + "-3", deck[0].Def) }).ToList();
            var result = DeckRules.Validate(Ready(four), AllOwned);
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("copies of \"" + id + "\""));
        }

        [Test]
        public void TheFortyFirstCardDoesNotGoIn()
        {
            Assert.That(DeckRules.CanAdd(Deck(20, 2), CardCatalog.All[30].Id, AllOwned), Is.False);
        }

        [Test]
        public void ACardNotOwnedIsRefused()
        {
            var deck = Deck(10, 2);
            var owned = AllOwned.Where(id => id != deck[0].Def.Id).ToList();

            var result = DeckRules.ValidateDeck(deck, owned);

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Errors, Has.Some.EqualTo("Card \"" + deck[0].Def.Id + "\" is not owned."));
            Assert.That(DeckRules.CanAdd(Deck(1, 1), deck[0].Def.Id, owned), Is.False);
        }

        [Test]
        public void TheCatalogsCardChecksStillHold()
        {
            // ValidateDeck is Cards.Validate plus ownership: an owned deck that passes one passes both.
            var deck = Deck(10, 2);
            Assert.That(DeckRules.ValidateDeck(deck, AllOwned).Errors, Is.EqualTo(Cards.Validate(deck).Errors));
        }

        // ---- the slots ----

        [Test]
        public void ThreeToolsAndThreeConsumablesFit_AFourthDoesNot()
        {
            var deck = Deck(10, 2);
            string talent = deck[0].Def.Id;
            var three = new[] { "a", "b", "a" }; // the same tool twice is allowed
            var four = new[] { "a", "b", "c", "d" };

            Assert.That(DeckRules.Validate(new Departure(deck, three, three, 0, talent), AllOwned).Ok, Is.True);
            Assert.That(DeckRules.Validate(new Departure(deck, four, three, 0, talent), AllOwned).Errors,
                Has.Some.EqualTo("4 tools do not fit 3 slots."));
            Assert.That(DeckRules.Validate(new Departure(deck, three, four, 0, talent), AllOwned).Errors,
                Has.Some.EqualTo("4 consumables do not fit 3 slots."));
        }

        // ---- the start ----

        [Test]
        public void TheDefaultStartIsCellTwoAtGapThree()
        {
            var plain = Departure.Plain(Deck(10, 2));
            Assert.That(plain.PlayerStartCell, Is.EqualTo(Constants.PlayerStartCell));
            Assert.That(plain.StartGap, Is.EqualTo(Constants.StartGap));
        }

        [Test]
        public void ACellBackStartsOnCellOneAtGapFour_AndNoFurther()
        {
            Assert.That(DeckRules.PlayerStartCell(1), Is.EqualTo(1));
            Assert.That(DeckRules.StartGap(1), Is.EqualTo(4));
            // Two 間合いの履: cell 1 is the end of the line, so the second gives nothing more.
            Assert.That(DeckRules.PlayerStartCell(2), Is.EqualTo(1));
            Assert.That(DeckRules.StartGap(2), Is.EqualTo(4));
        }

        [Test]
        public void TheStartReachesTheBattle_TheEnemyKeepsItsCell()
        {
            var enemy = Enemies.PolearmWarped;
            var plain = TurnLoop.Start(Departure.Plain(Deck(10, 2)).SetupFor(enemy, 6), new SystemRng(1)).State;
            var back = TurnLoop.Start(Ready(cellsBack: 1).SetupFor(enemy, 6), new SystemRng(1)).State;

            Assert.Multiple(() =>
            {
                Assert.That(plain.Player.Cell, Is.EqualTo(2));
                Assert.That(plain.Gap, Is.EqualTo(3));
                Assert.That(back.Player.Cell, Is.EqualTo(1));
                Assert.That(back.Gap, Is.EqualTo(4));
                Assert.That(back.Enemy.Cell, Is.EqualTo(plain.Enemy.Cell));
            });
        }

        [Test]
        public void AStartThatMovesForwardIsRefused()
        {
            Assert.That(DeckRules.Validate(Ready(cellsBack: -1), AllOwned).Ok, Is.False);
        }

        // ---- the deck reaches the battle ----

        [Test]
        public void TheDrawPileHoldsTheChosenDeck()
        {
            var deck = Cards.BuildDeck(new[] { CardCatalog.All[13], CardCatalog.All[41], CardCatalog.All[77] }, 3)
                .Concat(Deck(6, 2)).ToList(); // 21 cards
            var state = TurnLoop.Start(Ready(deck).SetupFor(Enemies.PolearmWarped, 6), new SystemRng(5)).State;

            Assert.That(state.DrawPile.Select(c => c.InstanceId).OrderBy(id => id, System.StringComparer.Ordinal),
                Is.EqualTo(deck.Select(c => c.InstanceId).OrderBy(id => id, System.StringComparer.Ordinal)));
        }

        // ---- the talent ----

        [Test]
        public void TheTalentIsRecorded()
        {
            var deck = Deck(10, 2);
            var departure = new Departure(deck, new string[0], new string[0], 0, deck[5].Def.Id);

            Assert.That(DeckRules.Validate(departure, AllOwned).Ok, Is.True);
            Assert.That(departure.TalentCardId, Is.EqualTo(deck[5].Def.Id));
        }

        [Test]
        public void NoTalentOrOneOutsideTheDeckCannotSetOut()
        {
            var deck = Deck(10, 2);
            var none = new Departure(deck, new string[0], new string[0], 0, null);
            var outside = new Departure(deck, new string[0], new string[0], 0, CardCatalog.All[60].Id);

            Assert.That(DeckRules.Validate(none, AllOwned).Errors, Has.Some.EqualTo("No talent card is chosen."));
            Assert.That(DeckRules.Validate(outside, AllOwned).Errors,
                Has.Some.EqualTo("The talent card \"" + CardCatalog.All[60].Id + "\" is not in the deck."));
        }
    }
}
