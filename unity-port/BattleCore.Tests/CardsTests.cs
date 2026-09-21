using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>§8: five cards a turn, the whole hand away at the end, and one RNG for all of it.</summary>
    public class CardsTests
    {
        private static IRng Seeded(int seed = 12345) => new SystemRng(seed);

        [Test]
        public void BuildDeck_LaysOutCopiesWithUniqueInstanceIds()
        {
            var deck = Fixtures.TwentyCardDeck();
            Assert.Multiple(() =>
            {
                Assert.That(deck, Has.Count.EqualTo(20));
                Assert.That(deck.Select(c => c.InstanceId).Distinct().Count(), Is.EqualTo(20));
                Assert.That(deck.Count(c => c.Def.Id == "card_0"), Is.EqualTo(2));
            });
        }

        [Test]
        public void ATurnDrawsFive()
        {
            var deck = Fixtures.TwentyCardDeck();
            var result = Cards.Draw(deck, new CardInstance[0], new CardInstance[0], Combat.DrawCount(), Seeded());

            Assert.Multiple(() =>
            {
                Assert.That(result.Hand, Has.Count.EqualTo(5));
                Assert.That(result.DrawPile, Has.Count.EqualTo(15));
                Assert.That(result.DiscardPile, Is.Empty);
                Assert.That(result.Drawn, Is.EqualTo(5));
            });
        }

        [Test]
        public void TurnEndThrowsTheWholeHandAway()
        {
            var deck = Fixtures.TwentyCardDeck();
            var drawn = Cards.Draw(deck, new CardInstance[0], new CardInstance[0], 5, Seeded());
            var (hand, discard) = Cards.DiscardHand(drawn.Hand, drawn.DiscardPile);

            Assert.Multiple(() =>
            {
                Assert.That(hand, Is.Empty);
                Assert.That(discard, Has.Count.EqualTo(5));
            });
        }

        [Test]
        public void AnEmptyDrawPileReshufflesTheDiscardPile()
        {
            var deck = Fixtures.TwentyCardDeck();
            var drawPile = deck.Take(2).ToList();
            var discardPile = deck.Skip(2).ToList();

            var result = Cards.Draw(drawPile, discardPile, new CardInstance[0], 5, Seeded());

            Assert.Multiple(() =>
            {
                Assert.That(result.Reshuffled, Is.True);
                Assert.That(result.Hand, Has.Count.EqualTo(5));
                Assert.That(result.DrawPile, Has.Count.EqualTo(15));
                Assert.That(result.DiscardPile, Is.Empty);
            });
        }

        [Test]
        public void ADeckThatRunsOutEntirely_DrawsWhatIsLeft()
        {
            var deck = Fixtures.TwentyCardDeck().Take(3).ToList();
            var result = Cards.Draw(deck, new CardInstance[0], new CardInstance[0], 5, Seeded());

            Assert.Multiple(() =>
            {
                Assert.That(result.Drawn, Is.EqualTo(3));
                Assert.That(result.Hand, Has.Count.EqualTo(3));
            });
        }

        [Test]
        public void ADrawPastTheHandLimitGoesToTheDiscardPile()
        {
            // §17.6 F8. A flat 5 into an empty hand never reaches this; draw bonuses will.
            var deck = Fixtures.TwentyCardDeck();
            var hand = deck.Take(Constants.HandLimit).ToList();
            var drawPile = deck.Skip(Constants.HandLimit).ToList();

            var result = Cards.Draw(drawPile, new CardInstance[0], hand, 2, Seeded());

            Assert.Multiple(() =>
            {
                Assert.That(result.Hand, Has.Count.EqualTo(Constants.HandLimit));
                Assert.That(result.OverflowToDiscard, Is.EqualTo(2));
                Assert.That(result.DiscardPile, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public void NoCardIsEverLost()
        {
            var deck = Fixtures.TwentyCardDeck();
            var result = Cards.Draw(deck, new CardInstance[0], new CardInstance[0], 5, Seeded());
            int total = result.Hand.Count + result.DrawPile.Count + result.DiscardPile.Count;
            Assert.That(total, Is.EqualTo(20));
        }

        [Test]
        public void TheSameSeedShufflesTheSameWay()
        {
            var deck = Fixtures.TwentyCardDeck();
            var first = Cards.Shuffle(deck, new SystemRng(999));
            var second = Cards.Shuffle(deck, new SystemRng(999));

            Assert.That(
                first.Select(c => c.InstanceId),
                Is.EqualTo(second.Select(c => c.InstanceId)));
        }

        [Test]
        public void TheSameSeedDrawsTheSameHand()
        {
            var deck = Fixtures.TwentyCardDeck();
            var shuffledA = Cards.Shuffle(deck, new SystemRng(7));
            var shuffledB = Cards.Shuffle(deck, new SystemRng(7));

            var handA = Cards.Draw(shuffledA, new CardInstance[0], new CardInstance[0], 5, new SystemRng(7));
            var handB = Cards.Draw(shuffledB, new CardInstance[0], new CardInstance[0], 5, new SystemRng(7));

            Assert.That(
                handA.Hand.Select(c => c.InstanceId),
                Is.EqualTo(handB.Hand.Select(c => c.InstanceId)));
        }

        [Test]
        public void ADifferentSeedShufflesDifferently()
        {
            var deck = Fixtures.TwentyCardDeck();
            var first = Cards.Shuffle(deck, new SystemRng(1));
            var second = Cards.Shuffle(deck, new SystemRng(2));

            Assert.That(
                first.Select(c => c.InstanceId),
                Is.Not.EqualTo(second.Select(c => c.InstanceId)));
        }

        [Test]
        public void ShuffleKeepsEveryCard()
        {
            var deck = Fixtures.TwentyCardDeck();
            var shuffled = Cards.Shuffle(deck, Seeded());

            Assert.That(
                shuffled.Select(c => c.InstanceId).OrderBy(id => id),
                Is.EqualTo(deck.Select(c => c.InstanceId).OrderBy(id => id)));
        }

        [Test]
        public void TwentyCardsOfTenKindsPasses()
        {
            var result = Cards.Validate(Fixtures.TwentyCardDeck());
            Assert.That(result.Ok, Is.True, string.Join(" / ", result.Errors));
        }

        [Test]
        public void ADeckUnderTwentyIsRefused()
        {
            var deck = Fixtures.TwentyCardDeck().Take(19).ToList();
            var result = Cards.Validate(deck);

            Assert.Multiple(() =>
            {
                Assert.That(result.Ok, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("minimum")), Is.True);
            });
        }

        [Test]
        public void ADeckOverFortyIsRefused()
        {
            var defs = Enumerable.Range(0, 21).Select(i => Fixtures.Card($"k{i}")).ToList();
            var result = Cards.Validate(Cards.BuildDeck(defs, copies: 2));

            Assert.Multiple(() =>
            {
                Assert.That(result.Ok, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("maximum")), Is.True);
            });
        }

        [Test]
        public void AFourthCopyOfOneKindIsRefused()
        {
            var defs = Enumerable.Range(0, 5).Select(i => Fixtures.Card($"k{i}")).ToList();
            var result = Cards.Validate(Cards.BuildDeck(defs, copies: 4));

            Assert.Multiple(() =>
            {
                Assert.That(result.Ok, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("copies")), Is.True);
            });
        }
    }
}
