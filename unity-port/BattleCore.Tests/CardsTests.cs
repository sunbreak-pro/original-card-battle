using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>§8: six cards a turn, the whole hand away at the end, and one RNG for all of it.</summary>
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
        public void ATurnDrawsSix()
        {
            var deck = Fixtures.TwentyCardDeck();
            var result = Cards.Draw(deck, new CardInstance[0], new CardInstance[0], Combat.DrawCount(), Seeded());

            Assert.Multiple(() =>
            {
                Assert.That(result.Hand, Has.Count.EqualTo(6));
                Assert.That(result.DrawPile, Has.Count.EqualTo(14));
                Assert.That(result.DiscardPile, Is.Empty);
                Assert.That(result.Drawn, Is.EqualTo(6));
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

        // ---- §4 (v4.4): a stance stands alone, and the deck has no cap on stance cards ----

        /// <summary>A card with a stance face (§4).</summary>
        private static CardDef StanceCard(string id) =>
            Fixtures.Card(id, attributes: BattleAttribute.Stance, face: new Face(Stance: new StanceDef(StanceHook.TurnStart, Guard: 1)));

        [Test]
        public void ManyStanceCards_PassTheDeckCheck_ThereIsNoCapAnyMore()
        {
            // Eight plain kinds × 2 and four stance kinds × 3: 28 cards, twelve of them stances. The
            // three-card cap of §19.6 S15 is gone (v4.4); only three of a kind still holds.
            var plain = Enumerable.Range(0, 8).Select(i => Fixtures.Card($"k{i}")).ToList();
            var stances = Enumerable.Range(0, 4).Select(i => StanceCard($"s{i}")).ToList();
            var deck = Cards.BuildDeck(plain, copies: 2).Concat(Cards.BuildDeck(stances, copies: 3)).ToList();
            var result = Cards.Validate(deck);

            Assert.Multiple(() =>
            {
                Assert.That(deck, Has.Count.EqualTo(28));
                Assert.That(deck.Count(c => Cards.IsStanceCard(c.Def)), Is.EqualTo(12));
                Assert.That(result.Ok, Is.True, string.Join(" / ", result.Errors));
            });
        }

        [Test]
        public void TheFourteenStanceCards_CountAsStance_AndEachStandsAlone()
        {
            // v4.4 (#257, written by #333): the ten that carried another face are stance only now, so
            // every stance card passes the standing-alone check on its own and aims at nobody.
            var stances = CardCatalog.All.Where(Cards.IsStanceCard).ToList();
            var rewritten = new[]
            {
                "purge_flash", "abyss_stance", "wolf_stance", "iron_wall", "spear_wall",
                "anchor_stance", "root_stride", "mist_step", "root_bind", "keen_eye",
            };

            Assert.Multiple(() =>
            {
                Assert.That(Cards.IsStanceCard(CardCatalog.IronWall), Is.True);
                Assert.That(Cards.IsStanceCard(CardCatalog.Thrust), Is.False);
                Assert.That(stances.Count, Is.EqualTo(14));
                Assert.That(rewritten.All(id => stances.Any(c => c.Id == id)), Is.True, "the ten #257 rewrote are among them");
                Assert.That(stances.Where(c => !AttributeRule.StanceStandsAlone(c.Attributes, c.Face)).Select(c => c.Id), Is.Empty);
                Assert.That(stances.Where(c => c.Attributes != BattleAttribute.Stance).Select(c => c.Id), Is.Empty, "a stance declares nothing else");
                Assert.That(stances.Where(c => c.Targets != TargetKind.Self).Select(c => c.Id), Is.Empty, "a stance aims at nobody");
                Assert.That(stances.All(Cards.StanceStandsAlone), Is.True);
            });
        }

        [Test]
        public void AStanceThatAlsoAttacksOrMoves_IsRefused()
        {
            var attacks = Fixtures.Card("attacking_stance", attributes: BattleAttribute.Attack | BattleAttribute.Stance,
                face: new Face(Power: 8, Stance: new StanceDef(StanceHook.TurnStart, Guard: 1)));
            var moves = Fixtures.Card("moving_stance", attributes: BattleAttribute.Stance,
                face: new Face(Move: -1, Stance: new StanceDef(StanceHook.TurnStart, Guard: 1)));
            var stanceFaceOnly = Fixtures.Card("undeclared_stance", attributes: BattleAttribute.Guard,
                face: new Face(Guard: 3, Stance: new StanceDef(StanceHook.TurnStart, Guard: 1)));
            var plain = Enumerable.Range(0, 9).Select(i => Fixtures.Card($"k{i}")).ToList();

            foreach (var bad in new[] { attacks, moves, stanceFaceOnly })
            {
                var result = Cards.Validate(Cards.BuildDeck(plain.Append(bad).ToList(), copies: 2));
                Assert.That(result.Errors.Single(),
                    Is.EqualTo($"Card \"{bad.Id}\" is a stance and also declares another attribute or moves; a stance stands alone."), bad.Id);
            }
            Assert.That(Cards.StanceStandsAlone(StanceCard("pure")), Is.True);
        }

        [Test]
        public void FoldingCountsACardAsOneAttribute_AttackThenGuardThenSkill()
        {
            var dual = new Face(Power: 6, Guard: 4);
            var guardSkill = new Face(Guard: 4, Statuses: new[] { new StatusGrant(StatusKind.Regen, 1, OnSelf: true) });

            Assert.Multiple(() =>
            {
                Assert.That(AttributeRule.Fold(BattleAttribute.Attack | BattleAttribute.Guard, dual), Is.EqualTo(BattleAttribute.Attack));
                Assert.That(AttributeRule.Fold(BattleAttribute.Attack | BattleAttribute.Skill, dual), Is.EqualTo(BattleAttribute.Attack));
                Assert.That(AttributeRule.Fold(BattleAttribute.Guard | BattleAttribute.Skill, guardSkill), Is.EqualTo(BattleAttribute.Guard));
                Assert.That(AttributeRule.Fold(BattleAttribute.Skill, guardSkill), Is.EqualTo(BattleAttribute.Skill));
                Assert.That(AttributeRule.Fold(BattleAttribute.Attack | BattleAttribute.Guard | BattleAttribute.Skill, dual), Is.EqualTo(BattleAttribute.Attack));
                Assert.That(AttributeRule.Fold(BattleAttribute.Stance, new Face()), Is.EqualTo(BattleAttribute.Stance));
                // Movement only: a Guard makes it 防御, anything else makes it スキル (2026-09-28 の割り振り).
                Assert.That(AttributeRule.Fold(BattleAttribute.None, new Face(Move: -2, Guard: 4)), Is.EqualTo(BattleAttribute.Guard));
                Assert.That(AttributeRule.Fold(BattleAttribute.None, new Face(Move: 1, Draw: 1)), Is.EqualTo(BattleAttribute.Skill));
                Assert.That(AttributeRule.Fold(BattleAttribute.None, new Face(Move: 2, StaminaGain: 1)), Is.EqualTo(BattleAttribute.Skill));
                Assert.That(AttributeRule.Fold(BattleAttribute.None, new Face()), Is.EqualTo(BattleAttribute.None));
            });
        }

        [Test]
        public void TheEightyCards_FoldToThirtySixNineteenElevenFourteen()
        {
            // swordsman_cards_v4 §4.1 (v4.5), worked out by the rule and not by hand: 攻撃 36 / 防御 19 /
            // スキル 11 / スタンス 14. 観察・間合い切り・覚悟 moved from スキル to 防御 (#352).
            var counts = CardCatalog.All.GroupBy(c => c.Attribute).ToDictionary(g => g.Key, g => g.Count());

            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.All.Count, Is.EqualTo(80));
                Assert.That(counts[BattleAttribute.Attack], Is.EqualTo(36));
                Assert.That(counts[BattleAttribute.Guard], Is.EqualTo(19));
                Assert.That(counts[BattleAttribute.Skill], Is.EqualTo(11));
                Assert.That(counts[BattleAttribute.Stance], Is.EqualTo(14));
                Assert.That(counts.ContainsKey(BattleAttribute.None), Is.False, "no card folds to nothing");
            });
        }

        // ---- §2.4 hits (#253): a multi-hit card gives the opponent no status ----

        [Test]
        public void AMultiHitCard_WithAnOpponentStatus_OnTheFaceOrInATrait_IsRefused()
        {
            var onFace = Fixtures.Card("twin_bleed", face: new Face(Power: 4, Hits: 2, Statuses: new[] { new StatusGrant(StatusKind.Bleed, 1) }));
            var inTrait = Fixtures.Card("twin_fragile", face: new Face(Power: 4, Hits: 2),
                trait: new Trait(TraitCondition.FirstPlay, TraitEffect.Status, Grant: new StatusGrant(StatusKind.Fragile, 1)));
            var inExtraTrait = new CardDef("twin_slow", "twin_slow", BattleAttribute.Attack, 1, new Face(Power: 4, Hits: 2),
                ExtraTrait: new Trait(TraitCondition.FirstPlay, TraitEffect.Status, Grant: new StatusGrant(StatusKind.Slow, 1)));

            Assert.Multiple(() =>
            {
                Assert.That(Cards.MultiHitCarriesNoFoeStatus(onFace), Is.False);
                Assert.That(Cards.MultiHitCarriesNoFoeStatus(inTrait), Is.False);
                Assert.That(Cards.MultiHitCarriesNoFoeStatus(inExtraTrait), Is.False);
            });
        }

        [Test]
        public void AMultiHitCard_MayGiveItsOwnStatus_AndASingleBlowMayGiveTheOpponentOne()
        {
            var selfOnly = Fixtures.Card("twin_empower", face: new Face(Power: 4, Hits: 2, Statuses: new[] { new StatusGrant(StatusKind.Empower, 1, OnSelf: true) }),
                trait: new Trait(TraitCondition.FirstPlay, TraitEffect.Status, Grant: new StatusGrant(StatusKind.Regen, 1, OnSelf: true)));
            var singleBlow = Fixtures.Card("bleed_cut", face: new Face(Power: 6, Statuses: new[] { new StatusGrant(StatusKind.Bleed, 1) }));
            var twoHitsStance = Fixtures.Card("twin_stance", face: new Face(Power: 4, Hits: 2,
                Stance: new StanceDef(StanceHook.StatusOnAttack, Status: StatusKind.Bleed, StatusStacks: 1)), attributes: BattleAttribute.Attack | BattleAttribute.Stance);

            Assert.Multiple(() =>
            {
                Assert.That(Cards.MultiHitCarriesNoFoeStatus(selfOnly), Is.True);
                Assert.That(Cards.MultiHitCarriesNoFoeStatus(singleBlow), Is.True);
                Assert.That(Cards.MultiHitCarriesNoFoeStatus(twoHitsStance), Is.True, "rule 2: a stance gives it blow by blow");
            });
        }

        [Test]
        public void ADeckHoldingAMultiHitCardWithAnOpponentStatus_IsRefused()
        {
            var bad = Fixtures.Card("twin_bleed", face: new Face(Power: 4, Hits: 2, Statuses: new[] { new StatusGrant(StatusKind.Bleed, 1) }));
            var plain = Enumerable.Range(0, 9).Select(i => Fixtures.Card($"k{i}")).ToList();
            var result = Cards.Validate(Cards.BuildDeck(plain.Append(bad).ToList(), copies: 2));

            Assert.Multiple(() =>
            {
                Assert.That(result.Ok, Is.False);
                Assert.That(result.Errors.Single(), Is.EqualTo("Card \"twin_bleed\" strikes more than once and gives the opponent a status."));
            });
        }
    }
}
