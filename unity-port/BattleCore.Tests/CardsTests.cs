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

        // ---- §19.6 S15: three cards with a stance face ----

        /// <summary>A card with a stance face (§4).</summary>
        private static CardDef StanceCard(string id) =>
            Fixtures.Card(id, attributes: BattleAttribute.Stance, face: new Face(Stance: new StanceDef(StanceHook.TurnStart, Guard: 1)));

        [Test]
        public void AFourthStanceCard_IsRefused_EvenWhenEachKindIsOne()
        {
            // Sixteen plain cards and four stance kinds × 1: 20 cards, none past three of a kind,
            // and still refused.
            var plain = Enumerable.Range(0, 8).Select(i => Fixtures.Card($"k{i}")).ToList();
            var stances = Enumerable.Range(0, 4).Select(i => StanceCard($"s{i}")).ToList();
            var deck = Cards.BuildDeck(plain, copies: 2).Concat(Cards.BuildDeck(stances, copies: 1)).ToList();
            var result = Cards.Validate(deck);

            Assert.Multiple(() =>
            {
                Assert.That(deck, Has.Count.EqualTo(20));
                Assert.That(result.Ok, Is.False);
                Assert.That(result.Errors.Single(), Is.EqualTo("Deck holds 4 stance cards; the maximum is 3."));
            });
        }

        [Test]
        public void ThreeStanceCards_Pass_WhetherOneKindOrThree()
        {
            var plain = Enumerable.Range(0, 17).Select(i => Fixtures.Card($"k{i}")).ToList();
            var oneKind = Cards.BuildDeck(plain, copies: 1).Concat(Cards.BuildDeck(new[] { StanceCard("s0") }, copies: 3)).ToList();
            var threeKinds = Cards.BuildDeck(plain, copies: 1)
                .Concat(Cards.BuildDeck(new[] { StanceCard("s0"), StanceCard("s1"), StanceCard("s2") }, copies: 1)).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(Cards.Validate(oneKind).Ok, Is.True, string.Join(" / ", Cards.Validate(oneKind).Errors));
                Assert.That(Cards.Validate(threeKinds).Ok, Is.True, string.Join(" / ", Cards.Validate(threeKinds).Errors));
            });
        }

        [Test]
        public void EveryCardWithTheStanceAttribute_CountsAsAStanceCard_TwoAttributeOnesToo()
        {
            // 鉄壁の構え is ガード + スタンス: it takes one of the three like 水の構え does.
            var plain = CardCatalog.All.Where(c => !Cards.IsStanceCard(c)).Take(8).ToList();
            var deck = Cards.BuildDeck(plain, copies: 2)
                .Concat(Cards.BuildDeck(new[] { CardCatalog.WaterStance, CardCatalog.RockStance, CardCatalog.FlowStance, CardCatalog.IronWall }, copies: 1))
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(Cards.IsStanceCard(CardCatalog.IronWall), Is.True);
                Assert.That(Cards.IsStanceCard(CardCatalog.Thrust), Is.False);
                Assert.That(CardCatalog.All.Count(Cards.IsStanceCard), Is.EqualTo(14));
                Assert.That(Cards.Validate(deck).Errors.Any(e => e.Contains("4 stance cards")), Is.True);
            });
        }
    }
}
