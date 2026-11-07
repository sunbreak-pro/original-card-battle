// #910: the cards a won fight raised, as the win result card lays them out (battle-visual-v1 §5.6).
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;
using Trial.Bridge;

namespace Trial.Bridge.Tests
{
    public class RaisedCardsTests
    {
        [Test]
        public void NoRankUp_GivesNoCard()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RaisedCards.Of(new List<RankUp>(), MasteryBook.Begin(null)), Is.Empty);
                Assert.That(RaisedCards.Of(null, MasteryBook.Begin(null)), Is.Empty);
                Assert.That(RaisedCards.OfLastFight(null), Is.Empty);
            });
        }

        [Test]
        public void ACard_IsShownAtItsNewRank_WithTheNameItHadBefore()
        {
            MasteryBook book = MasteryBook.Begin(null);
            MasteryProfile profile = book.ProfileOf("side_sweep");
            string atOne = Mastery.Master(CardCatalog.ById("side_sweep"), 1, profile).Name;

            RaisedCard card = RaisedCards.Of(new[] { new RankUp("side_sweep", 0, 1) }, book).Single();

            Assert.Multiple(() =>
            {
                Assert.That(card.From, Is.EqualTo(0));
                Assert.That(card.To, Is.EqualTo(1));
                Assert.That(card.Name, Is.EqualTo(atOne));
                Assert.That(card.Was, Is.EqualTo(CardCatalog.ById("side_sweep").Name));
                Assert.That(card.Face.Name, Is.EqualTo(atOne), "the face is the card as the next battle deals it");
                Assert.That(atOne, Is.Not.EqualTo(card.Was), "横薙ぎ renames at its first 段");
            });
        }

        [Test]
        public void TheHigherNewRank_ComesFirst_AndATieKeepsTheCoresOrder()
        {
            var ups = new[]
            {
                new RankUp("side_sweep", 0, 1),
                new RankUp("brace", 1, 2),
                new RankUp("kesa_cut", 0, 1),
            };

            IReadOnlyList<RaisedCard> cards = RaisedCards.Of(ups, MasteryBook.Begin(null));

            Assert.That(cards.Select(c => c.CardId), Is.EqualTo(new[] { "brace", "side_sweep", "kesa_cut" }));
        }
    }
}
